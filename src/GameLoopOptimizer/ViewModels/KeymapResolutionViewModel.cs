using System.Collections.ObjectModel;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.ViewModels;

public class KeymapResolutionViewModel : ViewModelBase
{
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<GameLoopConfig> _getGl;
    private readonly MouseBenchmarkService _mouseBenchmark = new();

    public GameLoopConfig Config => _getGl();

    // Resolution & Display Properties
    private int _resWidth = 1440;
    public int ResWidth
    {
        get => _resWidth;
        set
        {
            if (SetProperty(ref _resWidth, value))
            {
                OnPropertyChanged(nameof(AspectRatioDescription));
            }
        }
    }

    private int _resHeight = 1080;
    public int ResHeight
    {
        get => _resHeight;
        set
        {
            if (SetProperty(ref _resHeight, value))
            {
                OnPropertyChanged(nameof(AspectRatioDescription));
                RecalculateSensitivity();
            }
        }
    }

    private int _stretchedDpi = 320;
    public int StretchedDpi
    {
        get => _stretchedDpi;
        set => SetProperty(ref _stretchedDpi, value);
    }

    public string AspectRatioDescription => GameLoopViewModel.CalculateAspectRatio(ResWidth, ResHeight);

    public ObservableCollection<StretchedResPreset> StretchedPresets { get; } = new()
    {
        new StretchedResPreset { Title = "1440 x 1080", Tag = "1440x1080", Width = 1440, Height = 1080, Dpi = 320, AspectRatioLabel = "4:3 Stretched", AdvantageDescription = "+33% Wider Enemy Models & Hitboxes" },
        new StretchedResPreset { Title = "1728 x 1080", Tag = "1728x1080", Width = 1728, Height = 1080, Dpi = 320, AspectRatioLabel = "16:10 Stretched", AdvantageDescription = "+15% Model Width & Crisp FoV" },
        new StretchedResPreset { Title = "1080 x 1080", Tag = "1080x1080", Width = 1080, Height = 1080, Dpi = 240, AspectRatioLabel = "1:1 Box Stretch", AdvantageDescription = "Extreme Close-Quarter Combat Stretch" },
        new StretchedResPreset { Title = "1280 x 960", Tag = "1280x960", Width = 1280, Height = 960, Dpi = 240, AspectRatioLabel = "4:3 Low-End", AdvantageDescription = "Maximum FPS on Budget GPUs" },
        new StretchedResPreset { Title = "1920 x 1080", Tag = "1920x1080", Width = 1920, Height = 1080, Dpi = 320, AspectRatioLabel = "16:9 Standard", AdvantageDescription = "Standard 1080p FHD Native" },
        new StretchedResPreset { Title = "2560 x 1440", Tag = "2560x1440", Width = 2560, Height = 1440, Dpi = 400, AspectRatioLabel = "16:9 2K QHD", AdvantageDescription = "High-Res Competitive 1440p" }
    };

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Keymap Auto-Calibration Properties
    private bool _autoInjectKeymapWithResolution = true;
    public bool AutoInjectKeymapWithResolution
    {
        get => _autoInjectKeymapWithResolution;
        set => SetProperty(ref _autoInjectKeymapWithResolution, value);
    }

    private string _keymapCalibrationStatus = "Keymaps aligned with native 16:9 standard.";
    public string KeymapCalibrationStatus
    {
        get => _keymapCalibrationStatus;
        set => SetProperty(ref _keymapCalibrationStatus, value);
    }

    private bool _isKeymapCalibrating;
    public bool IsKeymapCalibrating
    {
        get => _isKeymapCalibrating;
        set => SetProperty(ref _isKeymapCalibrating, value);
    }

    public ObservableCollection<KeymapBackupProfile> KeymapProfiles { get; } = new();

    // Sensitivity & Recoil Calibration
    private int _selectedMouseDpi = 800;
    public int SelectedMouseDpi
    {
        get => _selectedMouseDpi;
        set
        {
            if (SetProperty(ref _selectedMouseDpi, value))
            {
                RecalculateSensitivity();
            }
        }
    }

    public ObservableCollection<int> DpiOptions { get; } = new() { 400, 800, 1000, 1200, 1600, 2400, 3200 };

    private double _verticalMultiplier = 1.65;
    public double VerticalMultiplier
    {
        get => _verticalMultiplier;
        set
        {
            if (SetProperty(ref _verticalMultiplier, Math.Round(value, 2)))
            {
                RecalculateSensitivity();
            }
        }
    }

    private AimPlaystyle _selectedPlaystyle = AimPlaystyle.BalancedCompetitive;
    public AimPlaystyle SelectedPlaystyle
    {
        get => _selectedPlaystyle;
        set
        {
            if (SetProperty(ref _selectedPlaystyle, value))
            {
                RecalculateSensitivity();
            }
        }
    }

    public ObservableCollection<AimPlaystyle> PlaystyleOptions { get; } = new()
    {
        AimPlaystyle.PrecisionLowSens,
        AimPlaystyle.BalancedCompetitive,
        AimPlaystyle.HighSensFastFlick
    };

    private SensitivityProfileResult _sensitivityResult = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive, 1.65);
    public SensitivityProfileResult SensitivityResult
    {
        get => _sensitivityResult;
        set => SetProperty(ref _sensitivityResult, value);
    }

    // Mouse Benchmark Tool
    private MouseBenchmarkMetrics _mouseMetrics = new();
    public MouseBenchmarkMetrics MouseMetrics
    {
        get => _mouseMetrics;
        set => SetProperty(ref _mouseMetrics, value);
    }

    private bool _isBenchmarkingMouse;
    public bool IsBenchmarkingMouse
    {
        get => _isBenchmarkingMouse;
        set => SetProperty(ref _isBenchmarkingMouse, value);
    }

    // Commands
    public ICommand SelectStretchedPresetCommand { get; }
    public ICommand ApplyStretchedResolutionCommand { get; }
    public ICommand CalibrateAndInjectKeymapCommand { get; }
    public ICommand RestoreStockKeymapCommand { get; }
    public ICommand BackupKeymapCommand { get; }
    public ICommand RestoreKeymapCommand { get; }
    public ICommand RecalculateSensitivityCommand { get; }
    public ICommand SetVerticalMultiplierPresetCommand { get; }
    public ICommand CopySensitivityToClipboardCommand { get; }
    public ICommand StartMouseBenchmarkCommand { get; }
    public ICommand StopMouseBenchmarkCommand { get; }

    public event EventHandler? SettingsSaved;

    public KeymapResolutionViewModel(Func<HardwareInfo> getHw, Func<GameLoopConfig> getGl)
    {
        _getHw = getHw;
        _getGl = getGl;

        KeymapBackupManager.ProfilesChanged += (s, e) => RefreshKeymaps();

        SelectStretchedPresetCommand = new RelayCommand(param =>
        {
            if (param is StretchedResPreset preset)
            {
                ResWidth = preset.Width;
                ResHeight = preset.Height;
                StretchedDpi = preset.Dpi;
                StatusMessage = $"Selected {preset.AspectRatioLabel} ({preset.Width}x{preset.Height}) - {preset.AdvantageDescription}";
            }
            else if (param is string str)
            {
                var found = StretchedPresets.FirstOrDefault(p => p.Tag.Equals(str, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    ResWidth = found.Width;
                    ResHeight = found.Height;
                    StretchedDpi = found.Dpi;
                    StatusMessage = $"Selected {found.AspectRatioLabel} ({found.Width}x{found.Height})";
                }
            }
        });

        ApplyStretchedResolutionCommand = new AsyncRelayCommand(ApplyStretchedResolutionAsync);
        CalibrateAndInjectKeymapCommand = new AsyncRelayCommand(CalibrateAndDeployKeymapAsync);
        RestoreStockKeymapCommand = new AsyncRelayCommand(RestoreStockKeymapAsync);

        SetVerticalMultiplierPresetCommand = new RelayCommand(param =>
        {
            if (param is double d)
            {
                VerticalMultiplier = d;
            }
            else if (param is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
            {
                VerticalMultiplier = parsed;
            }
        });

        CopySensitivityToClipboardCommand = new RelayCommand(() =>
        {
            try
            {
                var text = $"=== PUBG Mobile Sensitivity ({SelectedMouseDpi} DPI, {VerticalMultiplier:F2}x Vertical Recoil Multiplier) ===\n" +
                           $"GameLoop Keymap Sensitivity: X={SensitivityResult.GameLoopKeymapX}%, Y={SensitivityResult.GameLoopKeymapY}%\n\n" +
                           "In-Game ADS & Camera Sensitivity:\n" +
                           string.Join("\n", SensitivityResult.ScopeSettings.Select(s => $"• {s.ScopeName}: ADS {s.AdsSensitivity}% | Camera {s.CameraSensitivity}% ({s.RecoilTip})"));

                System.Windows.Clipboard.SetText(text);
                StatusMessage = "Sensitivity & Recoil configuration copied to clipboard!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy: {ex.Message}";
            }
        });

        BackupKeymapCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Archiving custom keymapping and sensitivity layout...";
            var profile = await KeymapBackupManager.CreateBackupAsync(Config);
            StatusMessage = profile != null 
                ? $"Keymap snapshot saved ({profile.FilesArchived} files archived)." 
                : "Failed to backup keymaps.";
            RefreshKeymaps();
        });

        RestoreKeymapCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is KeymapBackupProfile profile)
            {
                StatusMessage = $"Restoring keymap profile '{profile.Name}'...";
                bool ok = await KeymapBackupManager.RestoreBackupAsync(profile.Id, Config);
                StatusMessage = ok ? $"Keymap profile '{profile.Name}' restored!" : "Failed to restore keymap profile.";
                RefreshKeymaps();
            }
        });

        RecalculateSensitivityCommand = new RelayCommand(() => RecalculateSensitivity());

        StartMouseBenchmarkCommand = new RelayCommand(() =>
        {
            IsBenchmarkingMouse = true;
            _mouseBenchmark.Start();
            MouseMetrics = _mouseBenchmark.GetCurrentMetrics();
            StatusMessage = "Mouse Polling Benchmark active. Move your cursor inside the test canvas.";
        });

        StopMouseBenchmarkCommand = new RelayCommand(() =>
        {
            _mouseBenchmark.Stop();
            IsBenchmarkingMouse = false;
            MouseMetrics = _mouseBenchmark.GetCurrentMetrics();
            StatusMessage = "Mouse Polling Benchmark stopped.";
        });

        RefreshKeymaps();
        RecalculateSensitivity();
    }

    public void RefreshKeymaps()
    {
        void UpdateAction()
        {
            KeymapProfiles.Clear();
            foreach (var p in KeymapBackupManager.GetProfiles())
            {
                KeymapProfiles.Add(p);
            }
        }

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(UpdateAction);
        }
        else
        {
            UpdateAction();
        }
    }

    public void RecalculateSensitivity()
    {
        SensitivityResult = SensitivityCalculator.Calculate(SelectedMouseDpi, SelectedPlaystyle, VerticalMultiplier, ResHeight);
    }

    public void RecordMouseSample()
    {
        if (IsBenchmarkingMouse)
        {
            MouseMetrics = _mouseBenchmark.RecordMovement();
        }
    }

    public async Task ApplyStretchedResolutionAsync()
    {
        try
        {
            StatusMessage = $"Configuring {ResWidth}x{ResHeight} ({AspectRatioDescription})...";

            // 1. Save to Registry
            await Task.Run(() =>
            {
                var targetPaths = new[]
                {
                    @"Software\Tencent\MobileGamePC",
                    @"Software\Tencent\TxGameAssistant"
                };

                foreach (var path in targetPaths)
                {
                    try
                    {
                        using var subKey = Registry.CurrentUser.CreateSubKey(path);
                        if (subKey != null)
                        {
                            subKey.SetValue("VMResWidth", ResWidth, RegistryValueKind.DWord);
                            subKey.SetValue("VMResHeight", ResHeight, RegistryValueKind.DWord);
                            subKey.SetValue("VMDPI", StretchedDpi, RegistryValueKind.DWord);
                        }
                    }
                    catch { }

                    try
                    {
                        using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                        if (hklmKey != null)
                        {
                            hklmKey.SetValue("VMResWidth", ResWidth, RegistryValueKind.DWord);
                            hklmKey.SetValue("VMResHeight", ResHeight, RegistryValueKind.DWord);
                            hklmKey.SetValue("VMDPI", StretchedDpi, RegistryValueKind.DWord);
                        }
                    }
                    catch { }
                }
            });

            // 2. Synchronize Android VM via ADB if available
            var gl = _getGl();
            if (AdbManager.IsAdbAvailable(gl))
            {
                await AdbManager.ExecuteShellCommandAsync($"wm size {ResWidth}x{ResHeight}", null, 4000, gl);
                await AdbManager.ExecuteShellCommandAsync($"wm density {StretchedDpi}", null, 4000, gl);
            }

            // 3. Auto-calibrate and deploy GameLoop keymap if enabled
            if (AutoInjectKeymapWithResolution)
            {
                var kmRes = await ResolutionKeymapService.DeployResolutionKeymapAsync(ResWidth, ResHeight, gl);
                if (kmRes.Success)
                {
                    KeymapCalibrationStatus = $"Calibrated for {ResWidth}x{ResHeight} ({kmRes.KeysCalibrated} keys aligned across {kmRes.FilesUpdated} files)";
                    RefreshKeymaps();
                }
            }

            StatusMessage = $"Applied {ResWidth}x{ResHeight} ({AspectRatioDescription}) to Registry, VM & Keymap!";
            Logger.Success("StretchedRes", $"Applied {ResWidth}x{ResHeight} (DPI: {StretchedDpi}) stretched resolution.");
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to apply stretched resolution: {ex.Message}";
            Logger.Error("StretchedRes", $"Error applying resolution: {ex.Message}");
        }
    }

    public async Task CalibrateAndDeployKeymapAsync()
    {
        IsKeymapCalibrating = true;
        StatusMessage = $"Calibrating and deploying GameLoop keymap for {ResWidth}x{ResHeight} ({AspectRatioDescription})...";
        try
        {
            var gl = _getGl();
            var res = await ResolutionKeymapService.DeployResolutionKeymapAsync(ResWidth, ResHeight, gl);
            StatusMessage = res.Message;
            if (res.Success)
            {
                KeymapCalibrationStatus = $"Calibrated for {ResWidth}x{ResHeight} ({res.KeysCalibrated} keys aligned across {res.FilesUpdated} files)";
                RefreshKeymaps();
            }
        }
        finally
        {
            IsKeymapCalibrating = false;
        }
    }

    public async Task RestoreStockKeymapAsync()
    {
        IsKeymapCalibrating = true;
        StatusMessage = "Restoring default 16:9 widescreen keymap...";
        try
        {
            var gl = _getGl();
            var res = await ResolutionKeymapService.RestoreStockKeymapAsync(gl);
            StatusMessage = res.Success ? "Restored stock 16:9 1080P keymap layout!" : res.Message;
            if (res.Success)
            {
                KeymapCalibrationStatus = "Keymaps aligned with native 16:9 standard.";
                RefreshKeymaps();
            }
        }
        finally
        {
            IsKeymapCalibrating = false;
        }
    }
}
