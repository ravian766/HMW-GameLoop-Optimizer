using System.Collections.ObjectModel;
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
        set => SetProperty(ref _resWidth, value);
    }

    private int _resHeight = 1080;
    public int ResHeight
    {
        get => _resHeight;
        set => SetProperty(ref _resHeight, value);
    }

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

        RefreshData();
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
}
