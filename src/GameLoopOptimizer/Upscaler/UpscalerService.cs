using System.Windows;
using GameLoopOptimizer.Core;
using Vortice.Direct3D11;

namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// Main entry point for the FSR 1.0 spatial upscaling feature.
/// Orchestrates window capture, D3D11 rendering pipeline, and the overlay window.
///
/// Usage:
///   var service = new UpscalerService();
///   service.StartUpscaling(gameLoopHwnd, config);
///   // ...
///   service.StopUpscaling();
/// </summary>
public sealed class UpscalerService : IDisposable
{
    private UpscalerOverlayWindow? _overlayWindow;
    private D3D11RenderPipeline? _pipeline;
    private WindowCaptureService? _capture;
    private Thread? _renderThread;
    private volatile bool _isRunning;
    private volatile bool _stopRequested;
    private UpscaleConfig _config = new();
    private readonly UpscaleStats _stats = new();
    private IntPtr _targetHwnd;

    public bool IsRunning => _isRunning;
    public UpscaleStats Stats => _stats;
    public UpscaleConfig Config => _config;

    public event EventHandler? UpscalingStarted;
    public event EventHandler? UpscalingStopped;
    public event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Start the upscaling pipeline targeting the specified GameLoop window.
    /// </summary>
    /// <param name="targetHwnd">Handle to the GameLoop rendering window.</param>
    /// <param name="config">Upscale configuration (quality mode, sharpness, etc.).</param>
    public void StartUpscaling(IntPtr targetHwnd, UpscaleConfig config)
    {
        if (_isRunning)
        {
            Logger.Warn("UpscalerService", "Upscaler is already running.");
            return;
        }

        if (targetHwnd == IntPtr.Zero)
        {
            Logger.Error("UpscalerService", "Invalid target window handle.");
            StatusChanged?.Invoke(this, "Error: No valid GameLoop window found.");
            return;
        }

        _targetHwnd = targetHwnd;
        _config = config;
        _stopRequested = false;

        // Detect target window info
        var windowInfo = WindowDetector.DetectGameLoopWindow();
        if (!windowInfo.IsFound)
        {
            Logger.Error("UpscalerService", "GameLoop window not detected.");
            StatusChanged?.Invoke(this, "Error: GameLoop window not found.");
            return;
        }

        int outputW = windowInfo.MonitorWidth;
        int outputH = windowInfo.MonitorHeight;

        Logger.Info("UpscalerService", $"Starting FSR upscaler: {windowInfo.ClientWidth}×{windowInfo.ClientHeight} → {outputW}×{outputH} ({config.GetModeLabel()})");
        StatusChanged?.Invoke(this, $"Starting: {windowInfo.ClientWidth}×{windowInfo.ClientHeight} → {outputW}×{outputH}");

        // Create and position overlay window (must be on UI thread)
        IntPtr overlayHwnd = IntPtr.Zero;
        Application.Current.Dispatcher.Invoke(() =>
        {
            _overlayWindow = new UpscalerOverlayWindow();
            var helper = new System.Windows.Interop.WindowInteropHelper(_overlayWindow);
            helper.EnsureHandle();
            overlayHwnd = helper.Handle;
            _overlayWindow.PositionOverMonitor(windowInfo);
            // Window stays hidden until the first frame is rendered to avoid flashing black
        });

        // Update stats
        _stats.Reset();
        _stats.InputWidth = windowInfo.ClientWidth;
        _stats.InputHeight = windowInfo.ClientHeight;
        _stats.OutputWidth = outputW;
        _stats.OutputHeight = outputH;
        _stats.IsActive = true;

        // Start render thread (D3D11 must run on its own thread)
        _renderThread = new Thread(() => RenderLoop(overlayHwnd, outputW, outputH, windowInfo))
        {
            Name = "FSR_UpscalerRenderThread",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _renderThread.Start();
    }

    private void RenderLoop(IntPtr overlayHwnd, int outputW, int outputH, GameLoopWindowInfo windowInfo)
    {
        try
        {
            // Initialize D3D11 pipeline
            _pipeline = new D3D11RenderPipeline();
            if (!_pipeline.Initialize(overlayHwnd, outputW, outputH))
            {
                Logger.Error("UpscalerService", "Failed to initialize D3D11 pipeline.");
                StatusChanged?.Invoke(this, "Error: D3D11 initialization failed.");
                return;
            }

            // Initialize capture
            _capture = new WindowCaptureService();
            if (!_capture.Initialize(_targetHwnd, _pipeline.Device!))
            {
                Logger.Error("UpscalerService", "Failed to initialize window capture.");
                StatusChanged?.Invoke(this, "Error: Window capture initialization failed.");
                _pipeline.Dispose();
                return;
            }

            _isRunning = true;
            UpscalingStarted?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, $"FSR Active: {_config.GetModeLabel()}");

            ID3D11Texture2D? capturedFrame = null;
            bool overlayShown = false;

            // Main render loop
            while (!_stopRequested)
            {
                // Check if target window still exists
                if (!NativeMethods.IsWindow(_targetHwnd))
                {
                    Logger.Warn("UpscalerService", "Target window closed, stopping upscaler.");
                    break;
                }

                // Capture frame
                bool newFrame = _capture.CaptureFrame(_pipeline.Device!, _pipeline.Context!, ref capturedFrame);

                if (newFrame && capturedFrame != null)
                {
                    // Update capture dimensions
                    _stats.InputWidth = _capture.CaptureWidth;
                    _stats.InputHeight = _capture.CaptureHeight;

                    // Render FSR pipeline (EASU → RCAS)
                    _pipeline.RenderFrame(capturedFrame, _capture.CaptureWidth, _capture.CaptureHeight, _config.Sharpness);

                    // Show overlay only after the first upscaled frame has been successfully rendered and presented
                    if (!overlayShown)
                    {
                        overlayShown = true;
                        Logger.Info("UpscalerService", "First upscaled frame presented! Showing overlay window.");
                        Application.Current?.Dispatcher.InvokeAsync(() => _overlayWindow?.ShowOverlay());
                    }

                    // Record frame timing
                    _stats.RecordFrame();
                }
                else
                {
                    if (capturedFrame != null && overlayShown)
                    {
                        Thread.Sleep(2);
                    }
                    else
                    {
                        // Waiting for initial frame
                        Thread.Sleep(5);
                    }
                }

                // Periodically reposition overlay if window moves
                if (_stats.TotalFrames > 0 && _stats.TotalFrames % 60 == 0)
                {
                    RepositionOverlay();
                }
            }

            // Cleanup
            capturedFrame?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error("UpscalerService", $"Render loop error: {ex.Message}\n{ex.StackTrace}");
            StatusChanged?.Invoke(this, $"Error: {ex.Message}");
        }
        finally
        {
            _capture?.Dispose();
            _pipeline?.Dispose();
            _isRunning = false;
            _stats.IsActive = false;

            var disp = Application.Current?.Dispatcher;
            if (disp != null && disp.Thread.IsAlive && !disp.HasShutdownStarted)
            {
                disp.Invoke(() =>
                {
                    _overlayWindow?.HideOverlay();
                    _overlayWindow?.Close();
                    _overlayWindow = null;
                });
            }
            else
            {
                _overlayWindow = null;
            }

            UpscalingStopped?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, "FSR Upscaler stopped.");
            Logger.Info("UpscalerService", "Upscaler render loop terminated.");
        }
    }

    private void RepositionOverlay()
    {
        if (_targetHwnd == IntPtr.Zero || _overlayWindow == null) return;

        try
        {
            // Get current window position
            NativeMethods.GetWindowRect(_targetHwnd, out var rect);
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(_targetHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (hMonitor != IntPtr.Zero)
            {
                var mi = new NativeMethods.MONITORINFO();
                mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    int monW = mi.rcMonitor.Width;
                    int monH = mi.rcMonitor.Height;

                    if (monW != _stats.OutputWidth || monH != _stats.OutputHeight)
                    {
                        _stats.OutputWidth = monW;
                        _stats.OutputHeight = monH;
                        _pipeline?.Resize(monW, monH);

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            _overlayWindow?.PositionOverTarget(mi.rcMonitor.Left, mi.rcMonitor.Top, monW, monH);
                        });
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Stop the upscaling pipeline and restore the original GameLoop window state.
    /// </summary>
    public void StopUpscaling()
    {
        if (!_isRunning)
        {
            Logger.Warn("UpscalerService", "Upscaler is not running.");
            return;
        }

        Logger.Info("UpscalerService", "Stopping FSR upscaler...");
        _stopRequested = true;

        // Wait for render thread to finish (with timeout)
        _renderThread?.Join(TimeSpan.FromSeconds(3));

        Logger.Success("UpscalerService", "FSR upscaler stopped cleanly.");
    }

    /// <summary>
    /// Update sharpness in real-time without restarting the pipeline.
    /// </summary>
    public void UpdateSharpness(float sharpness)
    {
        _config.Sharpness = Math.Clamp(sharpness, 0.0f, 1.0f);
    }

    /// <summary>
    /// Update quality mode. Requires pipeline restart for resolution change.
    /// </summary>
    public void UpdateQualityMode(UpscaleQualityMode mode)
    {
        if (_config.QualityMode == mode) return;
        _config.QualityMode = mode;

        if (_isRunning)
        {
            Logger.Info("UpscalerService", $"Quality mode changed to {mode}, restarting pipeline...");
            var hwnd = _targetHwnd;
            StopUpscaling();
            StartUpscaling(hwnd, _config);
        }
    }

    public void Dispose()
    {
        if (_isRunning)
        {
            StopUpscaling();
        }
    }
}
