using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Upscaler;

namespace GameLoopOptimizer.ViewModels;

public class DownscalePreset
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Dpi { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class UpscalerViewModel : ViewModelBase
{
    private readonly UpscalerService _upscalerService;
    private readonly DispatcherTimer _statsTimer;
    private readonly Func<GameLoopWindowInfo> _detectWindow;

    // ---- State Properties ----

    private bool _isUpscaling;
    public bool IsUpscaling
    {
        get => _isUpscaling;
        set => SetProperty(ref _isUpscaling, value);
    }

    private string _statusMessage = "FSR Spatial Upscaler ready. Detect GameLoop window and click Start.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _fpsDisplay = "— FPS";
    public string FpsDisplay
    {
        get => _fpsDisplay;
        set => SetProperty(ref _fpsDisplay, value);
    }

    private string _frameTimeDisplay = "— ms";
    public string FrameTimeDisplay
    {
        get => _frameTimeDisplay;
        set => SetProperty(ref _frameTimeDisplay, value);
    }

    private string _resolutionDisplay = "—";
    public string ResolutionDisplay
    {
        get => _resolutionDisplay;
        set => SetProperty(ref _resolutionDisplay, value);
    }

    private string _avgFpsDisplay = "— FPS";
    public string AvgFpsDisplay
    {
        get => _avgFpsDisplay;
        set => SetProperty(ref _avgFpsDisplay, value);
    }

    private long _totalFrames;
    public long TotalFrames
    {
        get => _totalFrames;
        set => SetProperty(ref _totalFrames, value);
    }

    // ---- Configuration Properties ----

    private int _selectedQualityIndex = 1; // Default: Balanced
    public int SelectedQualityIndex
    {
        get => _selectedQualityIndex;
        set
        {
            if (SetProperty(ref _selectedQualityIndex, value))
            {
                OnPropertyChanged(nameof(QualityModeLabel));
                if (IsUpscaling)
                {
                    _upscalerService.UpdateQualityMode(GetQualityMode());
                }
            }
        }
    }

    public string QualityModeLabel => GetQualityMode() switch
    {
        UpscaleQualityMode.Quality => "Quality (1.3×)",
        UpscaleQualityMode.Balanced => "Balanced (1.5×)",
        UpscaleQualityMode.Performance => "Performance (1.7×)",
        UpscaleQualityMode.UltraPerformance => "Ultra Performance (2.0×)",
        _ => "Balanced"
    };

    public string[] QualityModes { get; } = { "Quality (1.3×)", "Balanced (1.5×)", "Performance (1.7×)", "Ultra Performance (2.0×)" };

    private double _sharpness = 0.25;
    public double Sharpness
    {
        get => _sharpness;
        set
        {
            if (SetProperty(ref _sharpness, Math.Round(value, 2)))
            {
                OnPropertyChanged(nameof(SharpnessLabel));
                _upscalerService.UpdateSharpness((float)_sharpness);
            }
        }
    }

    public string SharpnessLabel => $"{(1.0 - _sharpness) * 100:F0}%";

    private bool _showFrameCounter = true;
    public bool ShowFrameCounter
    {
        get => _showFrameCounter;
        set
        {
            if (SetProperty(ref _showFrameCounter, value))
            {
                _upscalerService.Config.ShowFrameCounter = value;
            }
        }
    }

    // ---- Resolution Downscaling & Optimization Properties ----

    public ObservableCollection<DownscalePreset> DownscalePresets { get; } = new()
    {
        new DownscalePreset { Name = "1280×720 (720p HD)", Width = 1280, Height = 720, Dpi = 240, Description = "Recommended: ~55% GPU load reduction, massive FPS boost" },
        new DownscalePreset { Name = "1600×900 (900p HD+)", Width = 1600, Height = 900, Dpi = 280, Description = "Balanced: ~30% GPU load reduction, crisp detail on 1080p" },
        new DownscalePreset { Name = "1024×576 (576p SD)", Width = 1024, Height = 576, Dpi = 200, Description = "Extreme Performance: maximum FPS on budget GPUs" },
        new DownscalePreset { Name = "1920×1080 (1080p FHD)", Width = 1920, Height = 1080, Dpi = 320, Description = "Native 1080p: recommended for 1440p / 4K displays" }
    };

    private int _selectedDownscalePresetIndex;
    public int SelectedDownscalePresetIndex
    {
        get => _selectedDownscalePresetIndex;
        set => SetProperty(ref _selectedDownscalePresetIndex, value);
    }

    private bool _autoDownscaleOnStart = true;
    public bool AutoDownscaleOnStart
    {
        get => _autoDownscaleOnStart;
        set => SetProperty(ref _autoDownscaleOnStart, value);
    }

    private bool _isResolutionSuboptimal;
    public bool IsResolutionSuboptimal
    {
        get => _isResolutionSuboptimal;
        set => SetProperty(ref _isResolutionSuboptimal, value);
    }

    private string _resolutionTipMessage = "For FSR upscaling to actually boost FPS, GameLoop must render at a lower resolution (e.g., 1280×720 or 1600×900 in windowed mode) before upscaling to your native display. Rendering at native resolution gives no GPU performance savings!";
    public string ResolutionTipMessage
    {
        get => _resolutionTipMessage;
        set => SetProperty(ref _resolutionTipMessage, value);
    }

    private string _resolutionStatusMessage = "Detect GameLoop window to analyze render resolution.";
    public string ResolutionStatusMessage
    {
        get => _resolutionStatusMessage;
        set => SetProperty(ref _resolutionStatusMessage, value);
    }

    // ---- Window Detection ----

    private string _detectedWindowInfo = "No GameLoop window detected.";
    public string DetectedWindowInfo
    {
        get => _detectedWindowInfo;
        set => SetProperty(ref _detectedWindowInfo, value);
    }

    private bool _isWindowDetected;
    public bool IsWindowDetected
    {
        get => _isWindowDetected;
        set => SetProperty(ref _isWindowDetected, value);
    }

    private IntPtr _detectedHwnd;

    // ---- Commands ----

    public ICommand StartUpscalingCommand { get; }
    public ICommand StopUpscalingCommand { get; }
    public ICommand DetectWindowCommand { get; }
    public ICommand ToggleUpscalingCommand { get; }
    public ICommand ApplyDownscaleCommand { get; }
    public ICommand Set720pCommand { get; }
    public ICommand Set900pCommand { get; }

    public UpscalerViewModel() : this(() => WindowDetector.DetectGameLoopWindow())
    {
    }

    public UpscalerViewModel(Func<GameLoopWindowInfo> detectWindow)
    {
        _detectWindow = detectWindow;
        _upscalerService = new UpscalerService();

        // Stats polling timer (only active while upscaling is in progress)
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _statsTimer.Tick += (s, e) => RefreshStats();

        _upscalerService.StatusChanged += (s, msg) =>
        {
            var disp = System.Windows.Application.Current?.Dispatcher;
            if (disp != null && disp.Thread.IsAlive && !disp.HasShutdownStarted && !disp.CheckAccess())
            {
                disp.Invoke(() => StatusMessage = msg);
            }
            else
            {
                StatusMessage = msg;
            }
        };

        _upscalerService.UpscalingStarted += (s, e) =>
        {
            var disp = System.Windows.Application.Current?.Dispatcher;
            if (disp != null && disp.Thread.IsAlive && !disp.HasShutdownStarted && !disp.CheckAccess())
            {
                disp.Invoke(() =>
                {
                    IsUpscaling = true;
                    _statsTimer.Start();
                });
            }
            else
            {
                IsUpscaling = true;
                _statsTimer.Start();
            }
        };

        _upscalerService.UpscalingStopped += (s, e) =>
        {
            var disp = System.Windows.Application.Current?.Dispatcher;
            if (disp != null && disp.Thread.IsAlive && !disp.HasShutdownStarted && !disp.CheckAccess())
            {
                disp.Invoke(() =>
                {
                    IsUpscaling = false;
                    _statsTimer.Stop();
                    FpsDisplay = "— FPS";
                    FrameTimeDisplay = "— ms";
                    AvgFpsDisplay = "— FPS";
                    ResolutionDisplay = "—";
                });
            }
            else
            {
                IsUpscaling = false;
                _statsTimer.Stop();
                FpsDisplay = "— FPS";
                FrameTimeDisplay = "— ms";
                AvgFpsDisplay = "— FPS";
                ResolutionDisplay = "—";
            }
        };

        StartUpscalingCommand = new AsyncRelayCommand(StartUpscalingAsync, () => IsWindowDetected && !IsUpscaling);
        StopUpscalingCommand = new RelayCommand(StopUpscaling, () => IsUpscaling);
        DetectWindowCommand = new RelayCommand(DetectWindow);
        ToggleUpscalingCommand = new AsyncRelayCommand(ToggleUpscalingAsync);
        ApplyDownscaleCommand = new AsyncRelayCommand(ApplySelectedDownscaleAsync, () => IsWindowDetected && !IsUpscaling);
        Set720pCommand = new AsyncRelayCommand(() => ApplyDownscaleResolutionAsync(1280, 720, 240), () => IsWindowDetected && !IsUpscaling);
        Set900pCommand = new AsyncRelayCommand(() => ApplyDownscaleResolutionAsync(1600, 900, 280), () => IsWindowDetected && !IsUpscaling);

        // Auto-detect on startup
        DetectWindow();
    }

    public void DetectWindow()
    {
        try
        {
            var info = _detectWindow();
            if (info.IsFound)
            {
                _detectedHwnd = info.RenderWindowHandle != IntPtr.Zero ? info.RenderWindowHandle : info.MainWindowHandle;
                IsWindowDetected = true;
                DetectedWindowInfo = $"✅ {info.ProcessName} — {info.WindowTitle}\n" +
                                    $"Class: {info.WindowClass}  |  " +
                                    $"Client: {info.ClientWidth}×{info.ClientHeight}  |  " +
                                    $"Monitor: {info.MonitorWidth}×{info.MonitorHeight}";
                Logger.Info("UpscalerVM", $"Detected window: {info.ProcessName} ({info.ClientWidth}×{info.ClientHeight})");

                // Evaluate resolution optimization status
                int monW = info.MonitorWidth > 0 ? info.MonitorWidth : 1920;
                int monH = info.MonitorHeight > 0 ? info.MonitorHeight : 1080;
                if (info.ClientWidth >= (monW - 50) && info.ClientHeight >= (monH - 100))
                {
                    IsResolutionSuboptimal = true;
                    ResolutionStatusMessage = $"⚠️ GameLoop is rendering at {info.ClientWidth}×{info.ClientHeight} (~100% native monitor resolution). Running native gives no GPU rendering reduction! Downscale GameLoop below to 720p or 900p to gain massive FPS.";
                }
                else
                {
                    IsResolutionSuboptimal = false;
                    double ratio = (double)monW / Math.Max(1, info.ClientWidth);
                    ResolutionStatusMessage = $"✅ Optimal render resolution: {info.ClientWidth}×{info.ClientHeight} → FSR upscaling to {monW}×{monH} ({ratio:F1}× render savings)!";
                }
            }
            else
            {
                _detectedHwnd = IntPtr.Zero;
                IsWindowDetected = false;
                IsResolutionSuboptimal = false;
                DetectedWindowInfo = "⚠️ No GameLoop window detected. Launch GameLoop and click Detect.";
                ResolutionStatusMessage = "Launch GameLoop to evaluate resolution.";
            }
        }
        catch (Exception ex)
        {
            IsWindowDetected = false;
            IsResolutionSuboptimal = false;
            DetectedWindowInfo = $"❌ Detection error: {ex.Message}";
            ResolutionStatusMessage = "Detection error.";
        }
    }

    public async Task<bool> ApplyDownscaleResolutionAsync(int targetW, int targetH, int targetDpi = 240)
    {
        var info = _detectWindow();
        if (!info.IsFound || info.MainWindowHandle == IntPtr.Zero)
        {
            StatusMessage = "Cannot resize: GameLoop window not detected.";
            return false;
        }

        try
        {
            StatusMessage = $"Resizing GameLoop to {targetW}×{targetH} windowed...";

            // 1. Restore if maximized
            NativeMethods.ShowWindow(info.MainWindowHandle, NativeMethods.SW_RESTORE);

            // 2. Calculate window size to achieve desired client render area
            int borderWidth = info.BorderWidth > 0 ? info.BorderWidth : 8;
            int titleBarHeight = info.TitleBarHeight > 0 ? info.TitleBarHeight : 38;

            int winW = targetW + (borderWidth * 2);
            int winH = targetH + titleBarHeight + borderWidth;

            // Center on target monitor
            int monW = info.MonitorWidth > 0 ? info.MonitorWidth : 1920;
            int monH = info.MonitorHeight > 0 ? info.MonitorHeight : 1080;
            int posX = info.MonitorX + Math.Max(0, (monW - winW) / 2);
            int posY = info.MonitorY + Math.Max(0, (monH - winH) / 2);

            NativeMethods.SetWindowPos(info.MainWindowHandle, IntPtr.Zero, posX, posY, winW, winH, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);

            // 3. If ADB is available, override in-VM display size and density
            if (AdbManager.IsAdbAvailable())
            {
                await AdbManager.SetInVmResolutionAsync(targetW, targetH, targetDpi);
            }

            // Brief delay for window and DWM to adjust client viewport
            await Task.Delay(250);

            // Re-detect window dimensions
            DetectWindow();

            StatusMessage = $"✅ GameLoop set to {targetW}×{targetH} windowed! Ready to upscale.";
            Logger.Success("UpscalerVM", $"Set GameLoop resolution to {targetW}×{targetH} (DPI: {targetDpi})");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("UpscalerVM", $"Failed to resize GameLoop: {ex.Message}");
            StatusMessage = $"Resize failed: {ex.Message}";
            return false;
        }
    }

    private async Task ApplySelectedDownscaleAsync()
    {
        if (SelectedDownscalePresetIndex >= 0 && SelectedDownscalePresetIndex < DownscalePresets.Count)
        {
            var p = DownscalePresets[SelectedDownscalePresetIndex];
            await ApplyDownscaleResolutionAsync(p.Width, p.Height, p.Dpi);
        }
    }

    private async Task StartUpscalingAsync()
    {
        if (_detectedHwnd == IntPtr.Zero)
        {
            DetectWindow();
            if (_detectedHwnd == IntPtr.Zero) return;
        }

        // If resolution is suboptimal (e.g. running at full native monitor resolution) and auto-downscale is on,
        // automatically downscale to the selected lower resolution before starting upscaler
        if (AutoDownscaleOnStart && IsResolutionSuboptimal)
        {
            var preset = DownscalePresets[SelectedDownscalePresetIndex];
            await ApplyDownscaleResolutionAsync(preset.Width, preset.Height, preset.Dpi);
        }

        var config = new UpscaleConfig
        {
            QualityMode = GetQualityMode(),
            Sharpness = (float)_sharpness,
            ShowFrameCounter = _showFrameCounter
        };

        _upscalerService.StartUpscaling(_detectedHwnd, config);
    }

    private void StopUpscaling()
    {
        _upscalerService.StopUpscaling();
    }

    public void ToggleUpscaling()
    {
        _ = ToggleUpscalingAsync();
    }

    public async Task ToggleUpscalingAsync()
    {
        if (IsUpscaling)
            StopUpscaling();
        else
            await StartUpscalingAsync();
    }

    private void RefreshStats()
    {
        if (!_upscalerService.IsRunning) return;

        var stats = _upscalerService.Stats;
        FpsDisplay = $"{stats.InstantFps:F0} FPS";
        FrameTimeDisplay = $"{stats.FrameTimeMs:F1} ms";
        AvgFpsDisplay = $"{stats.AverageFps:F0} FPS";
        ResolutionDisplay = $"{stats.InputWidth}×{stats.InputHeight} → {stats.OutputWidth}×{stats.OutputHeight}";
        TotalFrames = stats.TotalFrames;
    }

    private UpscaleQualityMode GetQualityMode() => _selectedQualityIndex switch
    {
        0 => UpscaleQualityMode.Quality,
        1 => UpscaleQualityMode.Balanced,
        2 => UpscaleQualityMode.Performance,
        3 => UpscaleQualityMode.UltraPerformance,
        _ => UpscaleQualityMode.Balanced
    };

    public void Cleanup()
    {
        _statsTimer.Stop();
        _upscalerService.Dispose();
    }
}
