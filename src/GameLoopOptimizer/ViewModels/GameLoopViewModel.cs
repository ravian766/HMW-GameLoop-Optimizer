using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.ViewModels;

public class GameLoopViewModel : ViewModelBase
{
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<GameLoopConfig> _getGl;

    public HardwareInfo Hardware => _getHw();
    public GameLoopConfig Config => _getGl();

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
            }
        }
    }

    // Editable properties
    private int _cpuCores = 4;
    public int CpuCores
    {
        get => _cpuCores;
        set => SetProperty(ref _cpuCores, value);
    }

    private int _ramMb = 8192;
    public int RamMb
    {
        get => _ramMb;
        set => SetProperty(ref _ramMb, value);
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
                RecalculateSensitivity();
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

    public ObservableCollection<KeymapBackupProfile> KeymapProfiles { get; } = new();
    public ObservableCollection<RegionPingResult> PingResults { get; } = new();

    private bool _isBenchmarkingPing;
    public bool IsBenchmarkingPing
    {
        get => _isBenchmarkingPing;
        set => SetProperty(ref _isBenchmarkingPing, value);
    }

    // ADB Subsystem Properties
    private bool _isAdbAvailable;
    public bool IsAdbAvailable
    {
        get => _isAdbAvailable;
        set => SetProperty(ref _isAdbAvailable, value);
    }

    private bool _isAdbConnected;
    public bool IsAdbConnected
    {
        get => _isAdbConnected;
        set => SetProperty(ref _isAdbConnected, value);
    }

    private string _adbDeviceName = "Not Connected";
    public string AdbDeviceName
    {
        get => _adbDeviceName;
        set => SetProperty(ref _adbDeviceName, value);
    }

    private string _adbStatusText = "Checking ADB Status...";
    public string AdbStatusText
    {
        get => _adbStatusText;
        set => SetProperty(ref _adbStatusText, value);
    }

    private bool _isAdbBusy;
    public bool IsAdbBusy
    {
        get => _isAdbBusy;
        set => SetProperty(ref _isAdbBusy, value);
    }

    private bool _adbZeroAnimations = true;
    public bool AdbZeroAnimations
    {
        get => _adbZeroAnimations;
        set => SetProperty(ref _adbZeroAnimations, value);
    }

    private bool _adbGpuAcceleration = true;
    public bool AdbGpuAcceleration
    {
        get => _adbGpuAcceleration;
        set => SetProperty(ref _adbGpuAcceleration, value);
    }

    private bool _adbDalvikHeapBoost = true;
    public bool AdbDalvikHeapBoost
    {
        get => _adbDalvikHeapBoost;
        set => SetProperty(ref _adbDalvikHeapBoost, value);
    }

    private bool _adbSuppressLogging = true;
    public bool AdbSuppressLogging
    {
        get => _adbSuppressLogging;
        set => SetProperty(ref _adbSuppressLogging, value);
    }

    private bool _adbDisableDoze = true;
    public bool AdbDisableDoze
    {
        get => _adbDisableDoze;
        set => SetProperty(ref _adbDisableDoze, value);
    }

    private bool _adbInputPolling = true;
    public bool AdbInputPolling
    {
        get => _adbInputPolling;
        set => SetProperty(ref _adbInputPolling, value);
    }

    private bool _adb120FpsUnlock = true;
    public bool Adb120FpsUnlock
    {
        get => _adb120FpsUnlock;
        set => SetProperty(ref _adb120FpsUnlock, value);
    }

    private bool _adbInVmDnsSync = true;
    public bool AdbInVmDnsSync
    {
        get => _adbInVmDnsSync;
        set => SetProperty(ref _adbInVmDnsSync, value);
    }

    private bool _adbAudioLatencyReduction = true;
    public bool AdbAudioLatencyReduction
    {
        get => _adbAudioLatencyReduction;
        set => SetProperty(ref _adbAudioLatencyReduction, value);
    }

    private bool _isPointerLocationEnabled;
    public bool IsPointerLocationEnabled
    {
        get => _isPointerLocationEnabled;
        set => SetProperty(ref _isPointerLocationEnabled, value);
    }

    private string _customAdbPortText = "127.0.0.1:5555";
    public string CustomAdbPortText
    {
        get => _customAdbPortText;
        set => SetProperty(ref _customAdbPortText, value);
    }

    private bool _isInstallingApk;
    public bool IsInstallingApk
    {
        get => _isInstallingApk;
        set => SetProperty(ref _isInstallingApk, value);
    }

    public ObservableCollection<GamePackageInfo> InstalledGamePackages { get; } = new();

    private GamePackageInfo? _selectedGamePackage;
    public GamePackageInfo? SelectedGamePackage
    {
        get => _selectedGamePackage;
        set
        {
            if (SetProperty(ref _selectedGamePackage, value))
            {
                _ = Task.Run(async () => await RefreshTelemetryAsync());
            }
        }
    }

    private AdbTelemetrySnapshot _adbTelemetry = new();
    public AdbTelemetrySnapshot AdbTelemetry
    {
        get => _adbTelemetry;
        set => SetProperty(ref _adbTelemetry, value);
    }

    private string _interactiveAdbCommand = string.Empty;
    public string InteractiveAdbCommand
    {
        get => _interactiveAdbCommand;
        set => SetProperty(ref _interactiveAdbCommand, value);
    }

    private string _interactiveAdbOutput = "ADB Shell Console Ready. Type a command (e.g., 'getprop ro.product.model' or 'pm list packages') and click Execute.\n";
    public string InteractiveAdbOutput
    {
        get => _interactiveAdbOutput;
        set => SetProperty(ref _interactiveAdbOutput, value);
    }

    private bool _isAdbCompiling;
    public bool IsAdbCompiling
    {
        get => _isAdbCompiling;
        set => SetProperty(ref _isAdbCompiling, value);
    }

    private string _adbCompilationStatus = "Ready to compile DEX bytecode to native AOT.";
    public string AdbCompilationStatus
    {
        get => _adbCompilationStatus;
        set => SetProperty(ref _adbCompilationStatus, value);
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

    private bool _isKeymapCalibrating;
    public bool IsKeymapCalibrating
    {
        get => _isKeymapCalibrating;
        set => SetProperty(ref _isKeymapCalibrating, value);
    }

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
    public ICommand ConnectAdbCommand { get; }
    public ICommand ConnectCustomPortCommand { get; }
    public ICommand ApplyAllAdbOptimizationsCommand { get; }
    public ICommand TrimInVmCacheCommand { get; }
    public ICommand RestartAdbServerCommand { get; }
    public ICommand CompileGameDexCommand { get; }
    public ICommand LaunchGameCommand { get; }
    public ICommand ForceStopGameCommand { get; }
    public ICommand ClearGameDataCommand { get; }
    public ICommand InstallApkCommand { get; }
    public ICommand TogglePointerLocationCommand { get; }
    public ICommand CaptureInVmScreenshotCommand { get; }
    public ICommand ExecuteCustomAdbShellCommand { get; }
    public ICommand ClearConsoleOutputCommand { get; }
    public ICommand RefreshTelemetryCommand { get; }
    public ICommand SetInVmResolutionCommand { get; }
    public ICommand ResetInVmResolutionCommand { get; }
    public ICommand SelectStretchedPresetCommand { get; }
    public ICommand ApplyStretchedResolutionCommand { get; }
    public ICommand CalibrateAndInjectKeymapCommand { get; }
    public ICommand RestoreStockKeymapCommand { get; }

    public event EventHandler? SettingsSaved;

    public GameLoopViewModel(Func<HardwareInfo> getHw, Func<GameLoopConfig> getGl)
    {
        _getHw = getHw;
        _getGl = getGl;
        _selectedDeviceProfile = DeviceProfiles.First();

        KeymapBackupManager.ProfilesChanged += (s, e) => RefreshKeymaps();

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

        ConnectAdbCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            AdbStatusText = "Connecting to GameLoop Android VM via ADB...";
            try
            {
                bool connected = await AdbManager.AutoConnectGameLoopAsync(Config);
                await RefreshAdbStatusAsync();
                StatusMessage = connected 
                    ? $"ADB Connected successfully to {AdbDeviceName}!" 
                    : "Failed to connect to GameLoop ADB. Ensure GameLoop is running.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ConnectCustomPortCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(CustomAdbPortText)) return;
            IsAdbBusy = true;
            StatusMessage = $"Connecting to ADB target {CustomAdbPortText}...";
            try
            {
                bool connected = await AdbManager.ConnectCustomDeviceAsync(CustomAdbPortText, Config);
                await RefreshAdbStatusAsync();
                StatusMessage = connected 
                    ? $"Connected to ADB target {CustomAdbPortText}!" 
                    : $"Failed to connect to {CustomAdbPortText}.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        LaunchGameCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbBusy = true;
            StatusMessage = $"Launching {SelectedGamePackage?.DisplayName ?? targetPkg} in Android VM...";
            try
            {
                bool ok = await AdbManager.LaunchGamePackageAsync(targetPkg, Config);
                StatusMessage = ok 
                    ? $"Launched {SelectedGamePackage?.DisplayName ?? targetPkg} successfully!" 
                    : $"Failed to launch {targetPkg}. Ensure GameLoop is running.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ForceStopGameCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbBusy = true;
            StatusMessage = $"Force-stopping {SelectedGamePackage?.DisplayName ?? targetPkg}...";
            try
            {
                bool ok = await AdbManager.ForceStopGamePackageAsync(targetPkg, Config);
                StatusMessage = ok 
                    ? $"Terminated {SelectedGamePackage?.DisplayName ?? targetPkg} process in VM." 
                    : $"Failed to stop {targetPkg}.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ClearGameDataCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbBusy = true;
            StatusMessage = $"Clearing app data for {SelectedGamePackage?.DisplayName ?? targetPkg}...";
            try
            {
                bool ok = await AdbManager.ClearGameDataAsync(targetPkg, Config);
                StatusMessage = ok 
                    ? $"Cleared app data for {SelectedGamePackage?.DisplayName ?? targetPkg}!" 
                    : $"Failed to clear app data for {targetPkg}.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        InstallApkCommand = new AsyncRelayCommand(async () =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Android Application Package (APK)",
                Filter = "Android Package (*.apk)|*.apk|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string apkPath = dialog.FileName;
                IsInstallingApk = true;
                IsAdbBusy = true;
                StatusMessage = $"Sideloading {Path.GetFileName(apkPath)} into GameLoop VM...";
                try
                {
                    string res = await AdbManager.InstallApkAsync(apkPath, Config);
                    StatusMessage = res;
                    await RefreshAdbStatusAsync();
                }
                finally
                {
                    IsInstallingApk = false;
                    IsAdbBusy = false;
                }
            }
        });

        TogglePointerLocationCommand = new AsyncRelayCommand(async () =>
        {
            IsPointerLocationEnabled = !IsPointerLocationEnabled;
            IsAdbBusy = true;
            StatusMessage = IsPointerLocationEnabled 
                ? "Enabled on-screen touch coordinate crosshairs & pointer overlay." 
                : "Disabled touch coordinate crosshairs overlay.";
            try
            {
                await AdbManager.SetPointerLocationOverlayAsync(IsPointerLocationEnabled, Config);
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ApplyAllAdbOptimizationsCommand = new AsyncRelayCommand(ApplyAllAdbOptimizationsAsync);

        TrimInVmCacheCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Trimming GameLoop Android VM application & shader caches...";
            try
            {
                bool trimmed = await AdbManager.TrimAppCacheAsync(Config);
                StatusMessage = trimmed 
                    ? "In-VM app and shader caches trimmed successfully!" 
                    : "Failed to trim Android VM caches. Ensure ADB is connected.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        RestartAdbServerCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Restarting ADB daemon service...";
            try
            {
                bool ok = await AdbManager.RestartAdbServerAsync(Config);
                await RefreshAdbStatusAsync();
                StatusMessage = ok ? "ADB Server restarted and reconnected." : "ADB Server restarted (device not detected).";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        CompileGameDexCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbCompiling = true;
            AdbCompilationStatus = $"Compiling DEX bytecode to native machine code for {targetPkg}...";
            StatusMessage = $"Pre-compiling {targetPkg} into AOT native code...";
            try
            {
                string result = await AdbManager.CompilePackageSpeedAsync(targetPkg, Config);
                AdbCompilationStatus = result;
                StatusMessage = $"AOT Compilation Complete for {targetPkg}!";
                await RefreshTelemetryAsync();
            }
            catch (Exception ex)
            {
                AdbCompilationStatus = $"Error: {ex.Message}";
                StatusMessage = $"AOT compilation failed: {ex.Message}";
            }
            finally
            {
                IsAdbCompiling = false;
            }
        });

        CaptureInVmScreenshotCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Capturing direct framebuffer screenshot from Android VM...";
            try
            {
                string picFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string shotDir = Path.Combine(picFolder, "GameLoop_Screenshots");
                if (!Directory.Exists(shotDir)) Directory.CreateDirectory(shotDir);
                string shotPath = Path.Combine(shotDir, $"GameLoop_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                bool ok = await AdbManager.CaptureScreenAsync(shotPath, Config);
                if (ok)
                {
                    StatusMessage = $"Screenshot saved to: {shotPath}";
                    Logger.Success("AdbStudio", $"Saved in-VM screenshot to {shotPath}");
                }
                else
                {
                    StatusMessage = "Failed to capture in-VM screenshot. Ensure ADB is connected.";
                }
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ExecuteCustomAdbShellCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(InteractiveAdbCommand)) return;
            string cmd = InteractiveAdbCommand.Trim();
            InteractiveAdbOutput += $"\n> {cmd}\n";
            InteractiveAdbCommand = string.Empty;
            try
            {
                string outText = await AdbManager.ExecuteShellCommandAsync(cmd, null, 8000, Config);
                InteractiveAdbOutput += outText + "\n";
            }
            catch (Exception ex)
            {
                InteractiveAdbOutput += $"[Error] {ex.Message}\n";
            }
        });

        ClearConsoleOutputCommand = new RelayCommand(() =>
        {
            InteractiveAdbOutput = "ADB Shell Console Cleared.\n";
        });

        RefreshTelemetryCommand = new AsyncRelayCommand(async () =>
        {
            await RefreshTelemetryAsync();
        });

        SetInVmResolutionCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = $"Overriding in-VM resolution to {ResWidth}x{ResHeight} @ {StretchedDpi} DPI...";
            try
            {
                bool ok = await AdbManager.SetInVmResolutionAsync(ResWidth, ResHeight, StretchedDpi, Config);
                StatusMessage = ok ? $"In-VM resolution scaled to {ResWidth}x{ResHeight}!" : "Failed to override in-VM resolution.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ResetInVmResolutionCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Resetting in-VM display viewport size...";
            try
            {
                bool ok = await AdbManager.ResetInVmResolutionAsync(Config);
                StatusMessage = ok ? "In-VM display viewport reset to default!" : "Failed to reset in-VM resolution.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
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
        RecalculateSensitivity();
        Task.Run(async () => await RefreshAdbStatusAsync());
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
        await Task.Run(() =>
        {
            try
            {
                var gl = _getGl();
                var targetPaths = new[]
                {
                    @"Software\Tencent\MobileGamePC",
                    @"Software\Tencent\TxGameAssistant"
                };

                foreach (var path in targetPaths)
                {
                    try
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(path);
                        if (key != null)
                        {
                            ApplyValuesToRegistryKey(key);
                        }
                    }
                    catch { }

                    try
                    {
                        using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                        if (hklmKey != null)
                        {
                            ApplyValuesToRegistryKey(hklmKey);
                        }
                    }
                    catch { }
                }

                gl.VmCpuCount = CpuCores;
                gl.VmMemorySizeInMb = RamMb;
                gl.VmResWidth = ResWidth;
                gl.VmResHeight = ResHeight;
                gl.ForceDirectX = ForceDirectX;
                gl.LocalShaderCacheEnabled = ShaderCacheEnabled;
                gl.ShaderCacheEnabled = ShaderCacheEnabled;
                gl.PubgFpsLevel = FpsLevel;
                if (SelectedDeviceProfile != null)
                {
                    gl.DeviceModel = SelectedDeviceProfile.DevicePhoneString;
                }

                StatusMessage = "Settings saved to GameLoop & TGB! (Restart GameLoop if open to apply)";
                Logger.Success("GameLoopStudio", $"Saved config to GameLoop & TGB: {CpuCores}C / {RamMb}MB / {FpsLevel}FPS / {SelectedDeviceProfile?.DisplayName}");
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Logger.Error("GameLoopStudio", $"Save error: {ex.Message}");
            }
        });
    }

    private void ApplyValuesToRegistryKey(RegistryKey key)
    {
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

        // PUBG Mobile Specific
        key.SetValue("com.tencent.ig_FPSLevel", FpsLevel, RegistryValueKind.DWord);
        key.SetValue("com.tencent.ig_RenderQuality", 2, RegistryValueKind.DWord);
        key.SetValue("com.tencent.ig_ContentScale", 1, RegistryValueKind.DWord);

        var profile = SelectedDeviceProfile ?? DeviceProfiles.First();
        key.SetValue("VMPhoneDevice", profile.DevicePhoneString, RegistryValueKind.String);
        key.SetValue("VMDeviceManufacturer", profile.Manufacturer, RegistryValueKind.String);
        key.SetValue("VMDeviceModel", profile.Model, RegistryValueKind.String);
    }

    private async Task ApplyRecommendedSettingsAsync()
    {
        CpuCores = Recommendations.RecommendedCpuCores;
        RamMb = Recommendations.RecommendedRamMb;
        ForceDirectX = Recommendations.RecommendedForceDirectX;
        ShaderCacheEnabled = Recommendations.RecommendedShaderCache;
        FpsLevel = Recommendations.RecommendedFpsLevel;
        ResWidth = Recommendations.RecommendedResWidth;
        ResHeight = Recommendations.RecommendedResHeight;
        SelectedResolutionString = $"{ResWidth}x{ResHeight}";
        SelectedDeviceProfile = DeviceProfiles.First();

        await SaveCustomSettingsAsync();
        StatusMessage = "Applied hardware-recommended GameLoop/TGB settings. (Restart GameLoop if open)";
    }

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

    private SensitivityProfileResult _sensitivityResult = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive);
    public SensitivityProfileResult SensitivityResult
    {
        get => _sensitivityResult;
        set => SetProperty(ref _sensitivityResult, value);
    }

    // Mouse Benchmark Tool
    private readonly MouseBenchmarkService _mouseBenchmark = new();
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

    public ICommand RecalculateSensitivityCommand { get; }
    public ICommand StartMouseBenchmarkCommand { get; }
    public ICommand StopMouseBenchmarkCommand { get; }

    public void RecalculateSensitivity()
    {
        SensitivityResult = SensitivityCalculator.Calculate(SelectedMouseDpi, SelectedPlaystyle, ResHeight);
    }

    public void RecordMouseSample()
    {
        if (IsBenchmarkingMouse)
        {
            MouseMetrics = _mouseBenchmark.RecordMovement();
        }
    }

    private void InitAimAndBenchmarkCommands()
    {
        // Already initialized
    }

    public async Task RefreshAdbStatusAsync()
    {
        var gl = _getGl();
        IsAdbAvailable = AdbManager.IsAdbAvailable(gl);

        if (!IsAdbAvailable)
        {
            AdbStatusText = "ADB Binary Not Detected";
            IsAdbConnected = false;
            AdbDeviceName = "Not Installed";
            return;
        }

        var devices = await AdbManager.GetConnectedDevicesAsync(gl);
        var active = devices.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));

        if (active != null)
        {
            IsAdbConnected = true;
            AdbManager.ActiveDeviceSerial = active.Serial;
            AdbDeviceName = active.Serial;
            AdbStatusText = $"Connected ({active.Serial} - {active.Model})";
        }
        else
        {
            // Try auto-connecting to localhost ports
            bool connected = await AdbManager.AutoConnectGameLoopAsync(gl);
            if (connected)
            {
                IsAdbConnected = true;
                AdbDeviceName = AdbManager.ActiveDeviceSerial ?? "127.0.0.1:5555";
                AdbStatusText = $"Connected ({AdbDeviceName})";
            }
            else
            {
                IsAdbConnected = false;
                AdbDeviceName = "Disconnected";
                AdbStatusText = "GameLoop Android VM Offline / Disconnected";
            }
        }

        if (IsAdbConnected)
        {
            var pkgs = await AdbManager.GetInstalledGamePackagesAsync(gl);
            void UpdatePkgsAction()
            {
                InstalledGamePackages.Clear();
                foreach (var p in pkgs) InstalledGamePackages.Add(p);
                if (SelectedGamePackage == null || !InstalledGamePackages.Contains(SelectedGamePackage))
                {
                    SelectedGamePackage = InstalledGamePackages.FirstOrDefault(p => p.IsInstalled) ?? InstalledGamePackages.FirstOrDefault();
                }
            }

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdatePkgsAction);
            }
            else
            {
                UpdatePkgsAction();
            }

            await RefreshTelemetryAsync();
        }
    }

    public async Task RefreshTelemetryAsync()
    {
        if (!IsAdbConnected) return;
        try
        {
            var gl = _getGl();
            var snap = await AdbTelemetryService.FetchTelemetryAsync(SelectedGamePackage?.PackageName, gl);
            AdbTelemetry = snap;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbTelemetry", $"Failed to fetch telemetry: {ex.Message}");
        }
    }

    public async Task ApplyAllAdbOptimizationsAsync()
    {
        IsAdbBusy = true;
        StatusMessage = "Applying Android VM In-Emulator Optimizations via ADB...";

        try
        {
            var gl = _getGl();
            if (!IsAdbConnected)
            {
                await AdbManager.AutoConnectGameLoopAsync(gl);
                await RefreshAdbStatusAsync();
            }

            int count = 0;
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";

            if (AdbGpuAcceleration)
            {
                await AdbManager.SetPropAsync("debug.sf.hw", "1", gl);
                await AdbManager.SetPropAsync("debug.egl.hw", "1", gl);
                await AdbManager.SetPropAsync("debug.composition.type", "gpu", gl);
                await AdbManager.SetPropAsync("debug.sf.latch_unsignaled", "1", gl);
                await AdbManager.SetPropAsync("debug.sf.early_phase_offset_ns", "500000", gl);
                await AdbManager.SetPropAsync("debug.sf.early_app_phase_offset_ns", "500000", gl);
                count++;
            }

            if (AdbZeroAnimations)
            {
                await AdbManager.PutGlobalSettingAsync("window_animation_scale", "0", gl);
                await AdbManager.PutGlobalSettingAsync("transition_animation_scale", "0", gl);
                await AdbManager.PutGlobalSettingAsync("animator_duration_scale", "0", gl);
                count++;
            }

            if (AdbInputPolling)
            {
                await AdbManager.SetPropAsync("windowsmgr.max_events_per_sec", "240", gl);
                await AdbManager.SetPropAsync("persist.sys.scrollingcache", "3", gl);
                await AdbManager.SetPropAsync("persist.vendor.touch.sensitivity", "10", gl);
                count++;
            }

            if (Adb120FpsUnlock)
            {
                await AdbManager.SetPropAsync("debug.sf.fps", "120", gl);
                await AdbManager.SetPropAsync("ro.surface_flinger.max_frame_rate", "120", gl);
                await AdbManager.SetPropAsync("persist.vendor.dfps.level", "120", gl);
                count++;
            }

            if (AdbDalvikHeapBoost)
            {
                await AdbManager.SetPropAsync("dalvik.vm.heapgrowthlimit", "512m", gl);
                await AdbManager.SetPropAsync("dalvik.vm.heapsize", "1024m", gl);
                await AdbManager.SetPropAsync("dalvik.vm.heaptargetutilization", "0.75", gl);
                await AdbManager.SetPropAsync("dalvik.vm.dexopt-flags", "v=n,o=v", gl);
                count++;
            }

            if (AdbSuppressLogging)
            {
                await AdbManager.SetPropAsync("log.tag", "ALL=SUPPRESS", gl);
                await AdbManager.SetPropAsync("log.tag.stats_log", "OFF", gl);
                count++;
            }

            if (AdbDisableDoze)
            {
                await AdbManager.PutGlobalSettingAsync("app_standby_enabled", "0", gl);
                await AdbManager.PutGlobalSettingAsync("adaptive_battery_management_enabled", "0", gl);
                await AdbManager.ExecuteShellCommandAsync($"cmd appops set {targetPkg} RUN_IN_BACKGROUND allow", null, 4000, gl);
                count++;
            }

            if (AdbInVmDnsSync)
            {
                await AdbManager.SetInVmDnsAsync("1.1.1.1", "1.0.0.1", gl);
                await AdbManager.OptimizeInVmTcpStackAsync(gl);
                count++;
            }

            if (AdbAudioLatencyReduction)
            {
                await AdbManager.OptimizeInVmAudioLatencyAsync(gl);
                count++;
            }

            await RefreshTelemetryAsync();

            StatusMessage = $"Applied {count} Android VM optimizations via ADB successfully!";
            Logger.Success("GameLoopViewModel", $"Applied {count} ADB in-VM optimization profiles for {targetPkg}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to apply ADB optimizations: {ex.Message}";
            Logger.Error("GameLoopViewModel", $"ADB optimization failed: {ex.Message}");
        }
        finally
        {
            IsAdbBusy = false;
        }
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
