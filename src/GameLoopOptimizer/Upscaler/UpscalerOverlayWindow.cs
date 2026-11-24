using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GameLoopOptimizer.Core;

namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// A borderless, topmost, click-through overlay window that sits directly over the
/// GameLoop emulator window. The FSR-upscaled output is rendered to this window via
/// a D3D11 swap chain.
///
/// Win32 extended styles ensure:
/// - WS_EX_TRANSPARENT: mouse clicks pass through to the window underneath
/// - WS_EX_LAYERED: required for per-pixel alpha (transparent regions)
/// - WS_EX_TOPMOST: always on top
/// - WS_EX_NOACTIVATE: doesn't steal focus from GameLoop
/// - WS_EX_TOOLWINDOW: doesn't appear in Alt+Tab or taskbar
/// </summary>
public class UpscalerOverlayWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;
    private const uint LWA_ALPHA = 0x00000002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public IntPtr Hwnd { get; private set; }

    public UpscalerOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = null;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;

        // Start hidden
        Visibility = Visibility.Collapsed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        Hwnd = helper.Handle;

        // Apply extended styles for click-through, transparent overlay
        int exStyle = GetWindowLong(Hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(Hwnd, GWL_EXSTYLE, exStyle);
        SetLayeredWindowAttributes(Hwnd, 0, 255, LWA_ALPHA);

        // Exclude overlay from desktop duplication and screen capture to prevent infinite feedback
        bool affinityOk = SetWindowDisplayAffinity(Hwnd, WDA_EXCLUDEFROMCAPTURE);
        Logger.Info("UpscalerOverlay", $"Overlay window created: HWND={Hwnd}, WDA_EXCLUDEFROMCAPTURE={affinityOk}");
    }

    /// <summary>
    /// Position and size the overlay to cover the target GameLoop window exactly.
    /// </summary>
    public void PositionOverTarget(int x, int y, int width, int height)
    {
        Left = x;
        Top = y;
        Width = width;
        Height = height;

        if (Hwnd != IntPtr.Zero)
        {
            SetWindowPos(Hwnd, new IntPtr(-1), x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    /// <summary>
    /// Position and size the overlay to cover the monitor that contains the target window.
    /// </summary>
    public void PositionOverMonitor(GameLoopWindowInfo windowInfo)
    {
        Left = windowInfo.MonitorX;
        Top = windowInfo.MonitorY;
        Width = windowInfo.MonitorWidth;
        Height = windowInfo.MonitorHeight;

        if (Hwnd != IntPtr.Zero)
        {
            SetWindowPos(Hwnd, new IntPtr(-1), windowInfo.MonitorX, windowInfo.MonitorY, windowInfo.MonitorWidth, windowInfo.MonitorHeight, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    /// <summary>
    /// Shows the overlay window without activating it (preserving GameLoop focus).
    /// </summary>
    public void ShowOverlay()
    {
        Visibility = Visibility.Visible;

        if (Hwnd != IntPtr.Zero)
        {
            // SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
            SetWindowPos(Hwnd, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, SWP_NOACTIVATE | 0x0001 | 0x0002 | SWP_SHOWWINDOW);
        }

        Logger.Info("UpscalerOverlay", "Overlay shown.");
    }

    /// <summary>
    /// Hides the overlay window.
    /// </summary>
    public void HideOverlay()
    {
        Visibility = Visibility.Collapsed;
        Logger.Info("UpscalerOverlay", "Overlay hidden.");
    }

    #region Native P/Invoke

    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_MONITOR = 0x00000001;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    #endregion
}
