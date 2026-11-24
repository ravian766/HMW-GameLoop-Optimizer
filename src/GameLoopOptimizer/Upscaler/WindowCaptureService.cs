using System.Runtime.InteropServices;
using GameLoopOptimizer.Core;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// Captures frames from a target window using DXGI Desktop Duplication API.
/// Falls back to BitBlt GDI capture when desktop duplication is unavailable
/// (e.g., WDDM 1.1, secure/protected content, or remote desktop).
/// </summary>
public sealed class WindowCaptureService : IDisposable
{
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _stagingTexture;
    private IntPtr _targetHwnd;
    private int _outputIndex;
    private int _outputLeft;
    private int _outputTop;
    private int _outputWidth;
    private int _outputHeight;
    private bool _disposed;
    private bool _useFallbackGdi;

    // Frame data for GDI fallback
    private byte[]? _gdiBitmapBuffer;

    public int CaptureWidth { get; private set; }
    public int CaptureHeight { get; private set; }
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Initialize capture for the specified window.
    /// Creates a separate D3D11 device for capture to avoid contention with the render device.
    /// </summary>
    public bool Initialize(IntPtr targetHwnd, ID3D11Device renderDevice)
    {
        _targetHwnd = targetHwnd;
        if (_targetHwnd == IntPtr.Zero) return false;

        try
        {
            // Get the window's client dimensions
            NativeMethods.GetClientRect(_targetHwnd, out var clientRect);
            CaptureWidth = clientRect.Width;
            CaptureHeight = clientRect.Height;

            if (CaptureWidth <= 0 || CaptureHeight <= 0) return false;

            // Try to set up Desktop Duplication
            if (!TryInitDesktopDuplication(renderDevice))
            {
                Logger.Warn("WindowCapture", "Desktop Duplication unavailable, using GDI fallback.");
                _useFallbackGdi = true;
            }

            IsInitialized = true;
            Logger.Info("WindowCapture", $"Capture initialized: {CaptureWidth}×{CaptureHeight} (DXGI={!_useFallbackGdi})");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("WindowCapture", $"Failed to initialize capture: {ex.Message}");
            return false;
        }
    }

    private bool TryInitDesktopDuplication(ID3D11Device renderDevice)
    {
        try
        {
            // Find which output the target window is on
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(_targetHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            using var dxgiDevice = renderDevice.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();

            // Enumerate outputs to find matching monitor
            _outputIndex = 0;
            IDXGIOutput? matchedOutput = null;

            for (int i = 0; ; i++)
            {
                var result = adapter.EnumOutputs((uint)i, out var output);
                if (result.Failure || output == null) break;

                var desc = output.Description;
                if (desc.Monitor == hMonitor)
                {
                    matchedOutput = output;
                    _outputIndex = i;
                    _outputLeft = desc.DesktopCoordinates.Left;
                    _outputTop = desc.DesktopCoordinates.Top;
                    _outputWidth = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
                    _outputHeight = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;
                    break;
                }
                output.Dispose();
            }

            if (matchedOutput == null)
            {
                // Fall back to output 0
                var result = adapter.EnumOutputs(0, out matchedOutput);
                if (result.Failure || matchedOutput == null)
                    return false;
                var desc = matchedOutput.Description;
                _outputLeft = desc.DesktopCoordinates.Left;
                _outputTop = desc.DesktopCoordinates.Top;
                _outputWidth = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
                _outputHeight = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;
            }

            using var output1 = matchedOutput.QueryInterface<IDXGIOutput1>();
            matchedOutput.Dispose();

            // Create Desktop Duplication
            _duplication = output1.DuplicateOutput(renderDevice);
            return _duplication != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Captures the current frame from the target window into the provided texture.
    /// Returns true if a new frame was captured.
    /// </summary>
    public bool CaptureFrame(ID3D11Device device, ID3D11DeviceContext context, ref ID3D11Texture2D? targetTexture)
    {
        if (!IsInitialized) return false;

        // Update capture dimensions in case the window was resized
        NativeMethods.GetClientRect(_targetHwnd, out var clientRect);
        int newW = clientRect.Width;
        int newH = clientRect.Height;

        if (newW > 0 && newH > 0 && (newW != CaptureWidth || newH != CaptureHeight))
        {
            CaptureWidth = newW;
            CaptureHeight = newH;
            _stagingTexture?.Dispose();
            _stagingTexture = null;
        }

        if (_useFallbackGdi)
            return CaptureFrameGdi(device, context, ref targetTexture);

        return CaptureFrameDxgi(device, context, ref targetTexture);
    }

    private bool CaptureFrameDxgi(ID3D11Device device, ID3D11DeviceContext context, ref ID3D11Texture2D? targetTexture)
    {
        if (_duplication == null) return false;

        try
        {
            var result = _duplication.AcquireNextFrame(16, out var frameInfo, out var desktopResource);
            if (result.Failure || desktopResource == null)
            {
                if (result.Code == unchecked((int)0x887A0026)) // DXGI_ERROR_ACCESS_LOST
                {
                    Logger.Warn("WindowCapture", "DXGI Desktop Duplication access lost, reinitializing...");
                    _duplication?.Dispose();
                    _duplication = null;
                    TryInitDesktopDuplication(device);
                }
                return false;
            }

            using var desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();

            // Ensure target texture matches dimensions
            EnsureTargetTexture(device, ref targetTexture, CaptureWidth, CaptureHeight);

            // We need to crop the desktop texture to the window's client area
            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(_targetHwnd, ref origin);

            int localX = origin.X - _outputLeft;
            int localY = origin.Y - _outputTop;

            int srcLeft = Math.Clamp(localX, 0, _outputWidth);
            int srcTop = Math.Clamp(localY, 0, _outputHeight);
            int srcRight = Math.Clamp(localX + CaptureWidth, srcLeft, _outputWidth);
            int srcBottom = Math.Clamp(localY + CaptureHeight, srcTop, _outputHeight);

            int copyW = srcRight - srcLeft;
            int copyH = srcBottom - srcTop;

            if (copyW > 0 && copyH > 0 && targetTexture != null)
            {
                int dstX = srcLeft > localX ? (srcLeft - localX) : 0;
                int dstY = srcTop > localY ? (srcTop - localY) : 0;

                context.CopySubresourceRegion(
                    targetTexture, 0, (uint)dstX, (uint)dstY, 0,
                    desktopTexture, 0,
                    new Box(srcLeft, srcTop, 0, srcRight, srcBottom, 1));
            }

            _duplication.ReleaseFrame();
            desktopResource.Dispose();
            return copyW > 0 && copyH > 0;
        }
        catch (Exception ex)
        {
            Logger.Warn("WindowCapture", $"DXGI capture frame exception: {ex.Message}");
            try { _duplication?.ReleaseFrame(); } catch { }
            return false;
        }
    }

    private bool CaptureFrameGdi(ID3D11Device device, ID3D11DeviceContext context, ref ID3D11Texture2D? targetTexture)
    {
        if (_targetHwnd == IntPtr.Zero) return false;

        try
        {
            IntPtr hdcWindow = GetDC(_targetHwnd);
            if (hdcWindow == IntPtr.Zero) return false;

            IntPtr hdcMem = CreateCompatibleDC(hdcWindow);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, CaptureWidth, CaptureHeight);
            IntPtr hOld = SelectObject(hdcMem, hBitmap);

            // Try PrintWindow with PW_RENDERFULLCONTENT (captures DWM-accelerated content), fall back to BitBlt
            bool printed = PrintWindow(_targetHwnd, hdcMem, PW_RENDERFULLCONTENT);
            if (!printed)
            {
                BitBlt(hdcMem, 0, 0, CaptureWidth, CaptureHeight, hdcWindow, 0, 0, 0x00CC0020 /* SRCCOPY */);
            }

            SelectObject(hdcMem, hOld);

            // Get bitmap bits
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = CaptureWidth,
                    biHeight = -CaptureHeight, // Top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                }
            };

            int stride = CaptureWidth * 4;
            int bufferSize = stride * CaptureHeight;
            if (_gdiBitmapBuffer == null || _gdiBitmapBuffer.Length < bufferSize)
                _gdiBitmapBuffer = new byte[bufferSize];

            GetDIBits(hdcMem, hBitmap, 0, (uint)CaptureHeight, _gdiBitmapBuffer, ref bmi, 0);

            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(_targetHwnd, hdcWindow);

            // Upload to GPU texture
            EnsureTargetTexture(device, ref targetTexture, CaptureWidth, CaptureHeight);
            EnsureStagingTexture(device, CaptureWidth, CaptureHeight);

            if (_stagingTexture != null && targetTexture != null)
            {
                var mapped = context.Map(_stagingTexture, 0, MapMode.WriteDiscard);
                try
                {
                    // Copy row by row to handle pitch differences
                    for (int y = 0; y < CaptureHeight; y++)
                    {
                        Marshal.Copy(_gdiBitmapBuffer, y * stride, mapped.DataPointer + y * (int)mapped.RowPitch, stride);
                    }
                }
                finally
                {
                    context.Unmap(_stagingTexture, 0);
                }
                context.CopyResource(targetTexture, _stagingTexture);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("WindowCapture", $"GDI capture error: {ex.Message}");
            return false;
        }
    }

    private void EnsureTargetTexture(ID3D11Device device, ref ID3D11Texture2D? texture, int width, int height)
    {
        if (texture != null)
        {
            var desc = texture.Description;
            if ((int)desc.Width == width && (int)desc.Height == height)
                return;
            texture.Dispose();
        }

        texture = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        });
    }

    private void EnsureStagingTexture(ID3D11Device device, int width, int height)
    {
        if (_stagingTexture != null)
        {
            var desc = _stagingTexture.Description;
            if ((int)desc.Width == width && (int)desc.Height == height)
                return;
            _stagingTexture.Dispose();
        }

        _stagingTexture = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.None
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _duplication?.Dispose();
        _stagingTexture?.Dispose();
        IsInitialized = false;
    }

    #region Native GDI P/Invoke

    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BITMAPINFO lpbmi, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    #endregion
}
