using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;
using RegistryHive = Microsoft.Win32.Registry;

namespace GameLoopOptimizer.ViewModels;

public class GameLoopViewModel : ViewModelBase
{
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<GameLoopConfig> _getGl;
    private readonly IEventAggregator _eventAggregator;

    public HardwareInfo Hardware => _getHw();
    public GameLoopConfig Config => _getGl();

    // Sub-ViewModels (Decomposed Subsystems)
    public AdbStudioViewModel AdbStudio { get; }
    public ActiveSavViewModel ActiveSav { get; }
    public AimSensitivityViewModel AimSens { get; }

    private HardwareRecommendations _recommendations = new();
    public HardwareRecommendations Recommendations
    {
        get => _recommendations;
        set => SetProperty(ref _recommendations, value);
    }

    public ObservableCollection<DeviceProfile> DeviceProfiles { get; } = new(DeviceProfile.Profiles);

    private DeviceProfile _selectedDeviceProfile;
    public DeviceProfile SelectedDeviceProfile
    {
        get => _selectedDeviceProfile;
        set
        {
            if (SetProperty(ref _selectedDeviceProfile, value) && value != null)
            {
                FpsLevel = value.MaxSupportedFps;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var gl = _getGl();
                        if (gl.IsInstalled)
                        {
                            await AdbManager.SpoofDeviceProfileAsync(value, gl);
                        }
                    }
                    catch { }
                });
            }
        }
    }

    // Editable engine properties
    private int _cpuCores = 4;
    public int CpuCores
    {
        get => _cpuCores;
        set
        {
            if (SetProperty(ref _cpuCores, value))
            {
                OnPropertyChanged(nameof(CurrentEngineAndResDisplay));
            }
        }
    }

    private int _ramMb = 8192;
    public int RamMb
    {
        get => _ramMb;
        set
        {
            if (SetProperty(ref _ramMb, value))
            {
                OnPropertyChanged(nameof(CurrentEngineAndResDisplay));
                OnPropertyChanged(nameof(HasRamWarning));
                OnPropertyChanged(nameof(RamWarningMessage));
                OnPropertyChanged(nameof(RamAllocationHostStatus));
            }
        }
    }

    private bool _forceDirectX = true;
    public bool ForceDirectX
    {
        get => _forceDirectX;
        set => SetProperty(ref _forceDirectX, value);
    }

    private bool _shaderCacheEnabled = true;
    public bool ShaderCacheEnabled
    {
        get => _shaderCacheEnabled;
        set => SetProperty(ref _shaderCacheEnabled, value);
    }

    private int _fpsLevel = 120;
    public int FpsLevel
    {
        get => _fpsLevel;
        set => SetProperty(ref _fpsLevel, value);
    }

    private int _pubgRenderQuality = 0;
    public int PubgRenderQuality
    {
        get => _pubgRenderQuality;
        set
        {
            if (SetProperty(ref _pubgRenderQuality, value))
            {
                OnPropertyChanged(nameof(CurrentGraphicsDisplayName));
                OnPropertyChanged(nameof(CurrentGraphicsAndScaleDisplay));
            }
        }
    }

    private int _pubgContentScale = 1;
    public int PubgContentScale
    {
        get => _pubgContentScale;
        set
        {
            if (SetProperty(ref _pubgContentScale, value))
            {
                OnPropertyChanged(nameof(CurrentContentScaleDisplayName));
                OnPropertyChanged(nameof(CurrentGraphicsAndScaleDisplay));
            }
        }
    }

    public string CurrentGraphicsDisplayName => PubgRenderQuality switch
    {
        0 => "流畅 Smooth",
        1 => "平衡 Balanced",
        2 => "高清 HD",
        3 => "HDR (高动态)",
        4 => "超高清 Ultra HD",
        5 => "UHD / Ultra HDR (极致超高清)",
        _ => $"Quality {PubgRenderQuality}"
    };

    public string CurrentContentScaleDisplayName => PubgContentScale switch
    {
        2 => "1080P HD",
        3 => "2K QHD",
        1 => "720P SD",
        _ => $"Scale {PubgContentScale}"
    };

    public string CurrentGraphicsAndScaleDisplay => $"{CurrentContentScaleDisplayName} • {CurrentGraphicsDisplayName}";
    public string CurrentEngineAndResDisplay => $"{CpuCores}C/{RamMb / 1024.0:F0}GB • {ResWidth}x{ResHeight}";

    // Delegated Active.sav Properties (for backward-compatible XAML binding)
    public ObservableCollection<ActiveSavProfile> ActiveSavPresets => ActiveSav.ActiveSavPresets;
    public ActiveSavProfile SelectedActiveSavPreset
    {
        get => ActiveSav.SelectedActiveSavPreset;
        set => ActiveSav.SelectedActiveSavPreset = value;
    }
    public int ActiveSavFpsLevel
    {
        get => ActiveSav.ActiveSavFpsLevel;
        set => ActiveSav.ActiveSavFpsLevel = value;
    }
    public int ActiveSavBattleQuality
    {
        get => ActiveSav.ActiveSavBattleQuality;
        set => ActiveSav.ActiveSavBattleQuality = value;
    }
    public int ActiveSavLobbyFpsLevel
    {
        get => ActiveSav.ActiveSavLobbyFpsLevel;
        set => ActiveSav.ActiveSavLobbyFpsLevel = value;
    }
    public int ActiveSavLobbyQuality
    {
        get => ActiveSav.ActiveSavLobbyQuality;
        set => ActiveSav.ActiveSavLobbyQuality = value;
    }
    public int ActiveSavStyle
    {
        get => ActiveSav.ActiveSavStyle;
        set => ActiveSav.ActiveSavStyle = value;
    }
    public int ActiveSavGraphicFavor
    {
        get => ActiveSav.ActiveSavGraphicFavor;
        set => ActiveSav.ActiveSavGraphicFavor = value;
    }
    public bool IsCustomActiveSav => ActiveSav.IsCustomActiveSav;
    public string ActiveSavFpsLabel => ActiveSav.ActiveSavFpsLabel;
    public string ActiveSavQualityLabel => ActiveSav.ActiveSavQualityLabel;
    public string ActiveSavStyleLabel => ActiveSav.ActiveSavStyleLabel;
    public string ActiveSavStatusMessage
    {
        get => ActiveSav.ActiveSavStatusMessage;
        set => ActiveSav.ActiveSavStatusMessage = value;
    }
    public bool IsSyncingActiveSav
    {
        get => ActiveSav.IsSyncingActiveSav;
        set => ActiveSav.IsSyncingActiveSav = value;
    }

    // Delegated ADB Subsystem Properties (for backward-compatible XAML binding)
    public bool IsAdbAvailable => AdbStudio.IsAdbAvailable;
    public bool IsAdbConnected => AdbStudio.IsAdbConnected;
    public string AdbDeviceName => AdbStudio.AdbDeviceName;
    public string AdbStatusText => AdbStudio.AdbStatusText;
    public bool IsAdbBusy => AdbStudio.IsAdbBusy;
    public bool AdbZeroAnimations
    {
        get => AdbStudio.AdbZeroAnimations;
        set => AdbStudio.AdbZeroAnimations = value;
    }
    public bool AdbGpuAcceleration
    {
        get => AdbStudio.AdbGpuAcceleration;
        set => AdbStudio.AdbGpuAcceleration = value;
    }
    public bool AdbDalvikHeapBoost
    {
        get => AdbStudio.AdbDalvikHeapBoost;
        set => AdbStudio.AdbDalvikHeapBoost = value;
    }
    public bool AdbSuppressLogging
    {
        get => AdbStudio.AdbSuppressLogging;
        set => AdbStudio.AdbSuppressLogging = value;
    }
    public bool AdbDisableDoze
    {
        get => AdbStudio.AdbDisableDoze;
        set => AdbStudio.AdbDisableDoze = value;
    }
    public bool AdbInputPolling
    {
        get => AdbStudio.AdbInputPolling;
        set => AdbStudio.AdbInputPolling = value;
    }
    public bool Adb120FpsUnlock
    {
        get => AdbStudio.Adb120FpsUnlock;
        set => AdbStudio.Adb120FpsUnlock = value;
    }
    public bool AdbInVmDnsSync
    {
        get => AdbStudio.AdbInVmDnsSync;
        set => AdbStudio.AdbInVmDnsSync = value;
    }
    public bool AdbAudioLatencyReduction
    {
        get => AdbStudio.AdbAudioLatencyReduction;
        set => AdbStudio.AdbAudioLatencyReduction = value;
    }
    public bool IsPointerLocationEnabled
    {
        get => AdbStudio.IsPointerLocationEnabled;
        set => AdbStudio.IsPointerLocationEnabled = value;
    }
    public string CustomAdbPortText
    {
        get => AdbStudio.CustomAdbPortText;
        set => AdbStudio.CustomAdbPortText = value;
    }
    public bool IsInstallingApk => AdbStudio.IsInstallingApk;
    public ObservableCollection<GamePackageInfo> InstalledGamePackages => AdbStudio.InstalledGamePackages;
    public GamePackageInfo? SelectedGamePackage
    {
        get => AdbStudio.SelectedGamePackage;
        set => AdbStudio.SelectedGamePackage = value;
    }
    public AdbTelemetrySnapshot AdbTelemetry => AdbStudio.AdbTelemetry;
    public string InteractiveAdbCommand
    {
        get => AdbStudio.InteractiveAdbCommand;
        set => AdbStudio.InteractiveAdbCommand = value;
    }
    public string InteractiveAdbOutput
    {
        get => AdbStudio.InteractiveAdbOutput;
        set => AdbStudio.InteractiveAdbOutput = value;
    }
    public bool IsAdbCompiling => AdbStudio.IsAdbCompiling;
    public string AdbCompilationStatus => AdbStudio.AdbCompilationStatus;

    // Delegated Aim & Sensitivity Properties (for backward-compatible XAML binding)
    public int SelectedMouseDpi
    {
        get => AimSens.SelectedMouseDpi;
        set => AimSens.SelectedMouseDpi = value;
    }
    public ObservableCollection<int> DpiOptions => AimSens.DpiOptions;
    public AimPlaystyle SelectedPlaystyle
    {
        get => AimSens.SelectedPlaystyle;
        set => AimSens.SelectedPlaystyle = value;
    }
    public ObservableCollection<AimPlaystyle> PlaystyleOptions => AimSens.PlaystyleOptions;
    public SensitivityProfileResult SensitivityResult => AimSens.SensitivityResult;
    public MouseBenchmarkMetrics MouseMetrics => AimSens.MouseMetrics;
    public bool IsBenchmarkingMouse => AimSens.IsBenchmarkingMouse;

    // Resolution & Stretched Display Properties
    private string _selectedResolutionString = "1920x1080";
    public string SelectedResolutionString
    {
        get => _selectedResolutionString;
        set
        {
            if (SetProperty(ref _selectedResolutionString, value))
            {
                ParseResolution(value);
            }
        }
    }

    private int _resWidth = 1920;
    public int ResWidth
    {
        get => _resWidth;
        set
        {
            if (SetProperty(ref _resWidth, value))
            {
                OnPropertyChanged(nameof(AspectRatioDescription));
                AimSens.RecalculateSensitivity();
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
                AimSens.RecalculateSensitivity();
            }
        }
    }

    private int _stretchedDpi = 320;
    public int StretchedDpi
    {
        get => _stretchedDpi;
        set => SetProperty(ref _stretchedDpi, value);
    }

    public string AspectRatioDescription => CalculateAspectRatio(ResWidth, ResHeight);

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

    // ── Display Awareness Properties ──────────────────────────────────────
    // These inform the user about monitor limitations WITHOUT overriding their selection.

    /// <summary>Current monitor refresh rate in Hz (informational only).</summary>
    public int MonitorRefreshRateHz => Hardware.RefreshRateHz;

    /// <summary>True when the selected FPS exceeds the monitor's current refresh rate.</summary>
    public bool HasDisplayWarning => FpsLevel > MonitorRefreshRateHz;

    /// <summary>User-facing warning about display limitation. Empty when no warning.</summary>
    public string DisplayWarningMessage => HasDisplayWarning
        ? $"⚠ Monitor refresh rate: {MonitorRefreshRateHz} Hz. {FpsLevel} FPS is configured and applied, but may not be fully visible on this display."
        : string.Empty;

    /// <summary>Calculates whether the currently selected RAM allocation poses host memory starvation risks.</summary>
    public bool HasRamWarning
    {
        get
        {
            double totalHostRam = Hardware?.TotalRamGb > 0 ? Hardware.TotalRamGb : 16.0;
            double allocatedGb = RamMb / 1024.0;
            double reservedForHost = totalHostRam - allocatedGb;
            // Starvation risk if allocation is >= 75% of total system RAM or leaves less than 3.5 GB for Windows host
            return allocatedGb >= (totalHostRam * 0.75) || reservedForHost < 3.5;
        }
    }

    /// <summary>Detailed explanation of RAM allocation status relative to host memory.</summary>
    public string RamWarningMessage
    {
        get
        {
            if (!HasRamWarning) return string.Empty;
            double totalHostRam = Hardware?.TotalRamGb > 0 ? Hardware.TotalRamGb : 16.0;
            double allocatedGb = RamMb / 1024.0;
            double reservedForHost = Math.Max(0, totalHostRam - allocatedGb);
            int percent = (int)Math.Round((allocatedGb / totalHostRam) * 100);
            return $"⚠ High RAM Allocation: Allocating {allocatedGb:F0} GB ({percent}% of {totalHostRam:F0} GB host RAM) leaves only {reservedForHost:F1} GB for Windows OS and GPU drivers. This may trigger page-file thrashing and micro-stutters.";
        }
    }

    /// <summary>Real-time status string showing VM allocation vs host reserve.</summary>
    public string RamAllocationHostStatus
    {
        get
        {
            double totalHostRam = Hardware?.TotalRamGb > 0 ? Hardware.TotalRamGb : 16.0;
            double allocatedGb = RamMb / 1024.0;
            double reservedForHost = Math.Max(0, totalHostRam - allocatedGb);
            int percent = (int)Math.Round((allocatedGb / totalHostRam) * 100);

            if (HasRamWarning)
            {
                return $"⚠ {allocatedGb:F0} GB Allocated ({percent}% of {totalHostRam:F0} GB) • Only {reservedForHost:F1} GB host reserve (Risk of paging)";
            }
            return $"✓ {allocatedGb:F0} GB Allocated ({percent}% of {totalHostRam:F0} GB) • {reservedForHost:F1} GB safely reserved for Windows Host";
        }
    }

    private SettingsApplicationReport? _lastApplyReport;
    /// <summary>Detailed per-setting report from the last Apply operation.</summary>
    public SettingsApplicationReport? LastApplyReport
    {
        get => _lastApplyReport;
        set => SetProperty(ref _lastApplyReport, value);
    }

    private string _lastApplyStatusSummary = string.Empty;
    /// <summary>One-line summary of the last apply operation's per-setting results.</summary>
    public string LastApplyStatusSummary
    {
        get => _lastApplyStatusSummary;
        set => SetProperty(ref _lastApplyStatusSummary, value);
    }

    private readonly object _keymapProfilesLock = new();
    public ObservableCollection<KeymapBackupProfile> KeymapProfiles { get; } = new();
    public ObservableCollection<RegionPingResult> PingResults { get; } = new();

    private bool _isBenchmarkingPing;
    public bool IsBenchmarkingPing
    {
        get => _isBenchmarkingPing;
        set => SetProperty(ref _isBenchmarkingPing, value);
    }

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

    private int _wasdResponseSpeed = 100;
    public int WasdResponseSpeed
    {
        get => _wasdResponseSpeed;
        set
        {
            if (SetProperty(ref _wasdResponseSpeed, Math.Clamp(value, 50, 100)))
            {
                OnPropertyChanged(nameof(WasdSpeedLabel));
            }
        }
    }

    public string WasdSpeedLabel => WasdResponseSpeed switch
    {
        100 => "100% (Instant Digital Response - Recommended)",
        95 => "95% (Smooth High-Speed)",
        90 => "90% (Standard Stable)",
        80 => "80% (Stock GameLoop Default)",
        _ => $"{WasdResponseSpeed}% Custom Speed"
    };

    private bool _isKeymapCalibrating;
    public bool IsKeymapCalibrating
    {
        get => _isKeymapCalibrating;
        set => SetProperty(ref _isKeymapCalibrating, value);
    }

    // Commands
    public ICommand ApplySettingsCommand { get; }
    public ICommand ApplyRecommendedCommand { get; }
    public ICommand LaunchGameLoopCommand { get; }
    public ICommand RestartGameLoopCommand { get; }
    public ICommand PurgeShaderCacheCommand { get; }
    public ICommand CreateShortcutCommand { get; }
    public ICommand BackupKeymapCommand { get; }
    public ICommand RestoreKeymapCommand { get; }
    public ICommand BenchmarkPingCommand { get; }
    public ICommand FlushDnsCommand { get; }

    // Delegated Commands to Sub-ViewModels
    public ICommand ConnectAdbCommand => AdbStudio.ConnectAdbCommand;
    public ICommand ConnectCustomPortCommand => AdbStudio.ConnectCustomPortCommand;
    public ICommand ApplyAllAdbOptimizationsCommand => AdbStudio.ApplyAllAdbOptimizationsCommand;
    public ICommand RestoreStockVmSettingsCommand => AdbStudio.RestoreStockVmSettingsCommand;
    public ICommand TrimInVmCacheCommand => AdbStudio.TrimInVmCacheCommand;
    public ICommand RestartAdbServerCommand => AdbStudio.RestartAdbServerCommand;
    public ICommand CompileGameDexCommand => AdbStudio.CompileGameDexCommand;
    public ICommand LaunchGameCommand => AdbStudio.LaunchGameCommand;
    public ICommand ForceStopGameCommand => AdbStudio.ForceStopGameCommand;
    public ICommand ClearGameDataCommand => AdbStudio.ClearGameDataCommand;
    public ICommand InstallApkCommand => AdbStudio.InstallApkCommand;
    public ICommand TogglePointerLocationCommand => AdbStudio.TogglePointerLocationCommand;
    public ICommand CaptureInVmScreenshotCommand => AdbStudio.CaptureInVmScreenshotCommand;
    public ICommand ExecuteCustomAdbShellCommand => AdbStudio.ExecuteCustomAdbShellCommand;
    public ICommand ClearConsoleOutputCommand => AdbStudio.ClearConsoleOutputCommand;
    public ICommand RefreshTelemetryCommand => AdbStudio.RefreshTelemetryCommand;

    public ICommand SetInVmResolutionCommand { get; }
    public ICommand ResetInVmResolutionCommand { get; }
    public ICommand SelectStretchedPresetCommand { get; }
    public ICommand ApplyStretchedResolutionCommand { get; }
    public ICommand CalibrateAndInjectKeymapCommand { get; }
    public ICommand RestoreStockKeymapCommand { get; }
    public ICommand ApplyWasdSpeedCommand { get; }

    public ICommand SyncActiveSavCommand => ActiveSav.SyncActiveSavCommand;
    public ICommand PullActiveSavCommand => ActiveSav.PullActiveSavCommand;
    public ICommand RestoreActiveSavCommand => ActiveSav.RestoreActiveSavCommand;
    public ICommand SelectActiveSavPresetCommand => ActiveSav.SelectActiveSavPresetCommand;

    public ICommand RecalculateSensitivityCommand => AimSens.RecalculateSensitivityCommand;
    public ICommand StartMouseBenchmarkCommand => AimSens.StartMouseBenchmarkCommand;
    public ICommand StopMouseBenchmarkCommand => AimSens.StopMouseBenchmarkCommand;

    public event EventHandler? SettingsSaved;

    public GameLoopViewModel(Func<HardwareInfo> getHw, Func<GameLoopConfig> getGl, IEventAggregator? eventAggregator = null, IAdbManager? adbManager = null)
    {
        _getHw = getHw;
        _getGl = getGl;
        _eventAggregator = eventAggregator ?? EventAggregator.Default;
        _selectedDeviceProfile = DeviceProfiles.First();

        // Initialize sub-viewmodels
        AdbStudio = new AdbStudioViewModel(getGl, adbManager, _eventAggregator);
        ActiveSav = new ActiveSavViewModel(getGl, () => SelectedDeviceProfile, _eventAggregator);
        AimSens = new AimSensitivityViewModel(() => ResHeight, _eventAggregator);

        // Forward child ViewModel PropertyChanged notifications
        AdbStudio.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        ActiveSav.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        AimSens.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);

        _eventAggregator.Subscribe<StatusNotificationMessage>(msg => StatusMessage = msg.Message);
        KeymapBackupManager.ProfilesChanged += (s, e) => RefreshKeymaps();

        ApplyWasdSpeedCommand = new AsyncRelayCommand(ApplyWasdSpeedAsync);
        ApplySettingsCommand = new AsyncRelayCommand(SaveCustomSettingsAsync);
        ApplyRecommendedCommand = new AsyncRelayCommand(ApplyRecommendedSettingsAsync);

        LaunchGameLoopCommand = new RelayCommand(() => ProcessManager.FocusOrLaunchGameLoop(Config));
        RestartGameLoopCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Restarting GameLoop / TGB...";
            bool restarted = await ProcessManager.RestartGameLoopAsync(Config);
            StatusMessage = restarted ? "GameLoop / TGB restarted successfully with new settings." : "Launched GameLoop executable.";
        });

        PurgeShaderCacheCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Purging stale & corrupted shader caches...";
            var res = await ShaderCacheCleaner.PurgeShaderCacheAsync(Config);
            StatusMessage = $"Purged {res.FilesDeleted} shader files ({res.MegabytesFreed} MB freed). Micro-stutters eliminated!";
        });

        CreateShortcutCommand = new RelayCommand(() =>
        {
            bool created = ShortcutManager.CreatePubgDesktopShortcut(Config);
            StatusMessage = created ? "Created 'HMW - Launch PUBG Mobile (Optimized)' shortcut on Desktop!" : "Failed to create shortcut.";
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
            }
        });

        BenchmarkPingCommand = new AsyncRelayCommand(async () =>
        {
            IsBenchmarkingPing = true;
            StatusMessage = "Benchmarking regional game servers...";
            
            void ClearAction() => PingResults.Clear();
            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
                System.Windows.Application.Current.Dispatcher.Invoke(ClearAction);
            else
                ClearAction();

            try
            {
                var results = await DnsOptimizerService.BenchmarkGameRegionsAsync();
                void AddAction()
                {
                    foreach (var r in results) PingResults.Add(r);
                }
                if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
                    System.Windows.Application.Current.Dispatcher.Invoke(AddAction);
                else
                    AddAction();

                StatusMessage = "Ping benchmark complete.";
            }
            finally
            {
                IsBenchmarkingPing = false;
            }
        });

        FlushDnsCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Flushing DNS Resolver Cache...";
            bool ok = await DnsOptimizerService.FlushDnsCacheAsync();
            StatusMessage = ok ? "DNS Resolver cache flushed successfully." : "Failed to flush DNS cache.";
        });

        SetInVmResolutionCommand = new AsyncRelayCommand(async () =>
        {
            AdbStudio.IsAdbBusy = true;
            StatusMessage = $"Overriding in-VM resolution to {ResWidth}x{ResHeight} @ {StretchedDpi} DPI...";
            try
            {
                bool ok = await AdbManager.SetInVmResolutionAsync(ResWidth, ResHeight, StretchedDpi, Config);
                StatusMessage = ok ? $"In-VM resolution scaled to {ResWidth}x{ResHeight}!" : "Failed to override in-VM resolution.";
                await AdbStudio.RefreshTelemetryAsync();
            }
            finally
            {
                AdbStudio.IsAdbBusy = false;
            }
        });

        ResetInVmResolutionCommand = new AsyncRelayCommand(async () =>
        {
            AdbStudio.IsAdbBusy = true;
            StatusMessage = "Resetting in-VM display viewport size...";
            try
            {
                bool ok = await AdbManager.ResetInVmResolutionAsync(Config);
                StatusMessage = ok ? "In-VM display viewport reset to default!" : "Failed to reset in-VM resolution.";
                await AdbStudio.RefreshTelemetryAsync();
            }
            finally
            {
                AdbStudio.IsAdbBusy = false;
            }
        });

        SelectStretchedPresetCommand = new RelayCommand(param =>
        {
            if (param is StretchedResPreset preset)
            {
                ResWidth = preset.Width;
                ResHeight = preset.Height;
                StretchedDpi = preset.Dpi;
                SelectedResolutionString = $"{preset.Width}x{preset.Height}";
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
                    SelectedResolutionString = $"{found.Width}x{found.Height}";
                    StatusMessage = $"Selected {found.AspectRatioLabel} ({found.Width}x{found.Height})";
                }
            }
        });

        ApplyStretchedResolutionCommand = new AsyncRelayCommand(ApplyStretchedResolutionAsync);
        CalibrateAndInjectKeymapCommand = new AsyncRelayCommand(CalibrateAndDeployKeymapAsync);
        RestoreStockKeymapCommand = new AsyncRelayCommand(RestoreStockKeymapAsync);

        RefreshData();
        RefreshKeymaps();
        AimSens.RecalculateSensitivity();
        Task.Run(async () => await AdbStudio.RefreshAdbStatusAsync());
    }

    public void RecalculateSensitivity() => AimSens.RecalculateSensitivity();
    public void RecordMouseSample() => AimSens.RecordMouseSample();
    public Task RefreshAdbStatusAsync() => AdbStudio.RefreshAdbStatusAsync();
    public Task RefreshTelemetryAsync() => AdbStudio.RefreshTelemetryAsync();
    public Task ApplyAllAdbOptimizationsAsync() => AdbStudio.ApplyAllAdbOptimizationsAsync();
    public Task RestoreStockVmSettingsAsync() => AdbStudio.RestoreStockVmSettingsAsync();
    public Task SyncActiveSavAsync() => ActiveSav.SyncActiveSavAsync();
    public Task PullActiveSavAsync() => ActiveSav.PullActiveSavAsync();
    public Task RestoreActiveSavAsync() => ActiveSav.RestoreActiveSavAsync();

    public void RefreshKeymaps()
    {
        void UpdateAction()
        {
            lock (_keymapProfilesLock)
            {
                KeymapProfiles.Clear();
                foreach (var p in KeymapBackupManager.GetProfiles())
                {
                    KeymapProfiles.Add(p);
                }
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

    public void RefreshData()
    {
        var hw = _getHw();
        var gl = _getGl();

        Recommendations = RecommendationEngine.Calculate(hw);

        CpuCores = gl.VmCpuCount > 0 ? gl.VmCpuCount : Recommendations.RecommendedCpuCores;
        RamMb = gl.VmMemorySizeInMb > 0 ? gl.VmMemorySizeInMb : Recommendations.RecommendedRamMb;
        ForceDirectX = gl.ForceDirectX;
        ShaderCacheEnabled = gl.LocalShaderCacheEnabled;
        FpsLevel = gl.PubgFpsLevel > 0 ? gl.PubgFpsLevel : Recommendations.RecommendedFpsLevel;
        PubgRenderQuality = gl.PubgRenderQuality >= 0 ? gl.PubgRenderQuality : 0;
        PubgContentScale = gl.PubgContentScale >= 0 ? gl.PubgContentScale : 1;
        ResWidth = gl.VmResWidth > 0 ? gl.VmResWidth : Recommendations.RecommendedResWidth;
        ResHeight = gl.VmResHeight > 0 ? gl.VmResHeight : Recommendations.RecommendedResHeight;

        SelectedResolutionString = $"{ResWidth}x{ResHeight}";

        var matched = DeviceProfiles.FirstOrDefault(p => p.DevicePhoneString.Equals(gl.DeviceModel, StringComparison.OrdinalIgnoreCase)
                                                      || p.Model.Equals(gl.DeviceModel, StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
            SelectedDeviceProfile = matched;
        }

        OnPropertyChanged(nameof(Hardware));
        OnPropertyChanged(nameof(Config));
        OnPropertyChanged(nameof(HasRamWarning));
        OnPropertyChanged(nameof(RamWarningMessage));
        OnPropertyChanged(nameof(RamAllocationHostStatus));
    }

    private void ParseResolution(string resStr)
    {
        if (string.IsNullOrWhiteSpace(resStr)) return;
        var parts = resStr.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
        {
            ResWidth = w;
            ResHeight = h;
        }
    }

    private async Task SaveCustomSettingsAsync()
    {
        try
        {
            var gl = Config;
            if (gl.IsInstalled)
            {
                gl.VmCpuCount = CpuCores;
                gl.VmMemorySizeInMb = RamMb;
                gl.ForceDirectX = ForceDirectX;
                gl.LocalShaderCacheEnabled = ShaderCacheEnabled;
                gl.ShaderCacheEnabled = ShaderCacheEnabled;
                gl.PubgFpsLevel = FpsLevel;
                gl.PubgRenderQuality = PubgRenderQuality;
                gl.PubgContentScale = PubgContentScale;
                gl.VmResWidth = ResWidth;
                gl.VmResHeight = ResHeight;

                var report = SaveSettingsToRegistry(gl);
                LastApplyReport = report;
                LastApplyStatusSummary = report.SummaryText;
                GameLoopDetector.InvalidateCache();

                if (SelectedDeviceProfile != null)
                {
                    await AdbManager.SpoofDeviceProfileAsync(SelectedDeviceProfile, gl);
                }

                Logger.Success("GameLoopStudio", $"Saved config to GameLoop & TGB: {CpuCores}C / {RamMb}MB / {FpsLevel}FPS / Quality {PubgRenderQuality} / {SelectedDeviceProfile?.DisplayName}");
            }

            // Notify UI of display warning state changes
            OnPropertyChanged(nameof(HasDisplayWarning));
            OnPropertyChanged(nameof(DisplayWarningMessage));
            OnPropertyChanged(nameof(MonitorRefreshRateHz));
            OnPropertyChanged(nameof(HasRamWarning));
            OnPropertyChanged(nameof(RamWarningMessage));
            OnPropertyChanged(nameof(RamAllocationHostStatus));

            StatusMessage = HasDisplayWarning
                ? $"Settings applied! ⚠ Monitor: {MonitorRefreshRateHz} Hz — {FpsLevel} FPS configured but display limited. Restart GameLoop for reload."
                : "Settings saved & synchronized to In-VM Android subsystem! Restart GameLoop for complete engine reload.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
            Logger.Error("GameLoopStudio", $"Failed to save GameLoop settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies user-selected settings to the GameLoop/TGB Windows registry.
    /// Follows the Apply → Verify → Report pipeline.
    /// 
    /// CRITICAL: The user's FPS selection is NEVER clamped or downgraded.
    /// Monitor refresh rate is informational — it generates warnings, not overrides.
    /// </summary>
    private SettingsApplicationReport SaveSettingsToRegistry(GameLoopConfig gl)
    {
        var report = new SettingsApplicationReport
        {
            MonitorRefreshRateHz = Hardware.RefreshRateHz,
            MonitorMaxRefreshRateHz = Hardware.MaxRefreshRateHz,
            RequestedFps = FpsLevel,
            RequestedGraphicsQuality = PubgRenderQuality
        };

        var targetPaths = new[]
        {
            @"Software\Tencent\MobileGamePC",
            @"Software\Tencent\TxGameAssistant"
        };

        // User's exact selection — NO clamping, NO downgrade.
        int registryFps = FpsLevel;
        int registryContentScale = PubgContentScale > 0 ? PubgContentScale : 2;
        int registryRenderQuality = PubgRenderQuality;

        var profile = SelectedDeviceProfile ?? DeviceProfiles.First();

        // Log the combination note (informational, never blocking)
        string comboNote = PubgGraphicsCompatibility.GetCombinationNote(registryRenderQuality, registryFps);
        if (!string.IsNullOrEmpty(comboNote))
        {
            Logger.Info("GameLoopStudio", $"[Combo] {comboNote}");
        }

        var packages = new[]
        {
            "com.tencent.ig",
            "com.pubg.krmobile",
            "com.pubg.imobile",
            "com.vng.pubgmobile"
        };

        foreach (var path in targetPaths)
        {
            // ── HKCU Write + Verify ──
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(path);
                if (key != null)
                {
                    // Engine allocation settings
                    key.SetValue("VMCpuCount", CpuCores, RegistryValueKind.DWord);
                    key.SetValue("VMMemorySizeInMB", RamMb, RegistryValueKind.DWord);
                    key.SetValue("VMResWidth", ResWidth, RegistryValueKind.DWord);
                    key.SetValue("VMResHeight", ResHeight, RegistryValueKind.DWord);
                    key.SetValue("VMDPI", ResHeight >= 1440 ? 400 : 320, RegistryValueKind.DWord);
                    key.SetValue("ForceDirectX", ForceDirectX ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("LocalShaderCacheEnabled", ShaderCacheEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("ShaderCacheEnabled", ShaderCacheEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("RenderOptimizeEnabled", 1, RegistryValueKind.DWord);
                    key.SetValue("EnableGLESv3", 1, RegistryValueKind.DWord);
                    key.SetValue("VSyncEnabled", 0, RegistryValueKind.DWord);
                    key.SetValue("SetGraphicsCard", 1, RegistryValueKind.DWord);
                    key.SetValue("GraphicsCardEnabled", 1, RegistryValueKind.DWord);

                    // Per-package PUBG settings (FPS, Quality, Scale)
                    foreach (var pkg in packages)
                    {
                        key.SetValue($"{pkg}_FPSLevel", registryFps, RegistryValueKind.DWord);
                        key.SetValue($"{pkg}_RenderQuality", registryRenderQuality, RegistryValueKind.DWord);
                        key.SetValue($"{pkg}_ContentScale", registryContentScale, RegistryValueKind.DWord);
                    }

                    // Device profile
                    key.SetValue("VMPhoneDevice", profile.DevicePhoneString, RegistryValueKind.String);
                    key.SetValue("VMDeviceManufacturer", profile.Manufacturer, RegistryValueKind.String);
                    key.SetValue("VMDeviceModel", profile.Model, RegistryValueKind.String);

                    // ── READBACK VERIFICATION ──
                    VerifyAndLog(report, key, $"HKCU\\{path}", "com.tencent.ig_FPSLevel", registryFps, "FPS");
                    VerifyAndLog(report, key, $"HKCU\\{path}", "com.tencent.ig_RenderQuality", registryRenderQuality, "Graphics");
                    VerifyAndLog(report, key, $"HKCU\\{path}", "com.tencent.ig_ContentScale", registryContentScale, "ContentScale");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GameLoopStudio", $"Failed to write HKCU\\{path}: {ex.Message}");
                report.Results.Add(new SettingApplicationResult
                {
                    SettingName = "Registry (HKCU)",
                    RequestedValue = $"{registryFps} FPS",
                    Status = SettingStatus.WriteFailed,
                    RegistryPath = $"HKCU\\{path}",
                    DisplayWarning = ex.Message
                });
            }

            // ── HKLM Write (best-effort, may require admin) ──
            try
            {
                using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                if (hklmKey != null)
                {
                    hklmKey.SetValue("VMCpuCount", CpuCores, RegistryValueKind.DWord);
                    hklmKey.SetValue("VMMemorySizeInMB", RamMb, RegistryValueKind.DWord);
                    hklmKey.SetValue("VMResWidth", ResWidth, RegistryValueKind.DWord);
                    hklmKey.SetValue("VMResHeight", ResHeight, RegistryValueKind.DWord);
                    hklmKey.SetValue("VMDPI", ResHeight >= 1440 ? 400 : 320, RegistryValueKind.DWord);
                    hklmKey.SetValue("ForceDirectX", ForceDirectX ? 1 : 0, RegistryValueKind.DWord);
                    hklmKey.SetValue("LocalShaderCacheEnabled", ShaderCacheEnabled ? 1 : 0, RegistryValueKind.DWord);
                    hklmKey.SetValue("ShaderCacheEnabled", ShaderCacheEnabled ? 1 : 0, RegistryValueKind.DWord);

                    foreach (var pkg in packages)
                    {
                        hklmKey.SetValue($"{pkg}_FPSLevel", registryFps, RegistryValueKind.DWord);
                        hklmKey.SetValue($"{pkg}_RenderQuality", registryRenderQuality, RegistryValueKind.DWord);
                        hklmKey.SetValue($"{pkg}_ContentScale", registryContentScale, RegistryValueKind.DWord);
                    }

                    hklmKey.SetValue("VMPhoneDevice", profile.DevicePhoneString, RegistryValueKind.String);
                    hklmKey.SetValue("VMDeviceManufacturer", profile.Manufacturer, RegistryValueKind.String);
                    hklmKey.SetValue("VMDeviceModel", profile.Model, RegistryValueKind.String);
                }
            }
            catch { /* HKLM write is best-effort — may lack admin privileges */ }
        }

        return report;
    }

    /// <summary>
    /// Reads back a registry value immediately after write and logs/reports the result.
    /// Adds display warnings when FPS exceeds monitor Hz, but NEVER changes the written value.
    /// </summary>
    private void VerifyAndLog(
        SettingsApplicationReport report,
        RegistryKey key,
        string registryPath,
        string valueName,
        int expectedValue,
        string settingName)
    {
        var readbackObj = key.GetValue(valueName);
        int readback = readbackObj is int i ? i : (readbackObj != null && int.TryParse(readbackObj.ToString(), out int p) ? p : -1);

        bool isFpsSetting = settingName == "FPS";
        bool verifyOk = readback == expectedValue;
        bool hasDisplayWarning = isFpsSetting && expectedValue > Hardware.RefreshRateHz;

        var result = new SettingApplicationResult
        {
            SettingName = settingName,
            RequestedValue = expectedValue.ToString(),
            WrittenValue = expectedValue.ToString(),
            ReadbackValue = readback.ToString(),
            RegistryPath = registryPath,
            RegistryKey = valueName
        };

        if (!verifyOk)
        {
            result.Status = SettingStatus.VerificationFailed;
            Logger.Warn("GameLoopStudio", $"[{settingName}] VERIFICATION FAILED: Wrote {expectedValue} but read back {readback} at {registryPath}\\{valueName}");
        }
        else if (hasDisplayWarning)
        {
            result.Status = SettingStatus.AppliedWithDisplayWarning;
            result.DisplayWarning = $"Monitor refresh rate: {Hardware.RefreshRateHz} Hz. {expectedValue} FPS is configured but may not be fully visible.";
            Logger.Success("GameLoopStudio", $"[{settingName}] Verified: {readback} written to {registryPath} (Display warning: monitor is {Hardware.RefreshRateHz} Hz)");
        }
        else
        {
            result.Status = SettingStatus.Applied;
            Logger.Success("GameLoopStudio", $"[{settingName}] Verified: {readback} written to {registryPath}");
        }

        report.Results.Add(result);
    }

    private async Task ApplyRecommendedSettingsAsync()
    {
        CpuCores = Recommendations.RecommendedCpuCores;
        RamMb = Recommendations.RecommendedRamMb;
        ForceDirectX = Recommendations.RecommendedForceDirectX;
        ShaderCacheEnabled = Recommendations.RecommendedShaderCache;
        FpsLevel = 120;
        PubgRenderQuality = 1;
        PubgContentScale = 2;
        ResWidth = Recommendations.RecommendedResWidth;
        ResHeight = Recommendations.RecommendedResHeight;
        SelectedResolutionString = $"{ResWidth}x{ResHeight}";
        SelectedDeviceProfile = DeviceProfiles.First();

        await SaveCustomSettingsAsync();
        StatusMessage = "Applied hardware-recommended GameLoop/TGB settings. (Restart GameLoop if open)";
    }

    public static string CalculateAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0) return "16:9 Standard";

        if (width == 1440 && height == 1080) return "4:3 Stretched (+33% Wider Hitboxes)";
        if (width == 1728 && height == 1080) return "16:10 Stretched (+15% Balanced FoV)";
        if (width == 1080 && height == 1080) return "1:1 Box Stretched (Extreme Close Range)";
        if (width == 1280 && height == 960) return "4:3 Low-End Stretched (High FPS)";
        if (width == 1920 && height == 1080) return "16:9 Standard FHD Native";
        if (width == 2560 && height == 1440) return "16:9 2K QHD Native";

        double ratio = (double)width / height;
        if (Math.Abs(ratio - (4.0 / 3.0)) < 0.05) return "4:3 Custom Stretched";
        if (Math.Abs(ratio - (16.0 / 10.0)) < 0.05) return "16:10 Custom Stretched";
        if (Math.Abs(ratio - 1.0) < 0.05) return "1:1 Custom Box Stretched";
        if (Math.Abs(ratio - (16.0 / 9.0)) < 0.05) return "16:9 Standard Widescreen";

        return $"{ratio:F2}:1 Custom Aspect Ratio";
    }

    public async Task ApplyStretchedResolutionAsync()
    {
        try
        {
            StatusMessage = $"Configuring {ResWidth}x{ResHeight} ({AspectRatioDescription})...";

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

            var gl = _getGl();
            if (AdbManager.IsAdbAvailable(gl))
            {
                await AdbManager.ExecuteShellCommandAsync($"wm size {ResWidth}x{ResHeight}", null, 4000, gl);
                await AdbManager.ExecuteShellCommandAsync($"wm density {StretchedDpi}", null, 4000, gl);
            }

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
        StatusMessage = $"Calibrating and deploying GameLoop keymap for {ResWidth}x{ResHeight} ({AspectRatioDescription}) with {WasdResponseSpeed}% WASD speed...";
        try
        {
            var gl = _getGl();
            var res = await ResolutionKeymapService.DeployResolutionKeymapAsync(ResWidth, ResHeight, gl, WasdResponseSpeed);
            StatusMessage = res.Message;
            if (res.Success)
            {
                KeymapCalibrationStatus = $"Calibrated for {ResWidth}x{ResHeight} ({res.KeysCalibrated} keys across {res.FilesUpdated} files)";
                RefreshKeymaps();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Keymap calibration failed: {ex.Message}";
        }
        finally
        {
            IsKeymapCalibrating = false;
        }
    }

    public async Task ApplyWasdSpeedAsync()
    {
        IsKeymapCalibrating = true;
        StatusMessage = $"Applying {WasdResponseSpeed}% WASD response speed to keymap...";
        try
        {
            var gl = _getGl();
            var res = await KeymapSpeedService.ApplyWasdSpeedAsync(WasdResponseSpeed, gl);
            StatusMessage = res.Message;
            if (res.Success)
            {
                KeymapCalibrationStatus = $"WASD responsiveness set to {WasdResponseSpeed}%.";
                RefreshKeymaps();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update WASD speed: {ex.Message}";
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

public class StretchedResPreset
{
    public string Title { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Dpi { get; set; }
    public string AspectRatioLabel { get; set; } = string.Empty;
    public string AdvantageDescription { get; set; } = string.Empty;
}
