using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private HardwareInfo _hardware = new();
    private SystemInfo _system = new();
    private GameLoopConfig _gameLoop = new();

    public HardwareInfo Hardware => _hardware;
    public SystemInfo System => _system;
    public GameLoopConfig GameLoop => _gameLoop;

    public PerformanceMonitorService MonitorService { get; }
    public GameLoopWatchdogService WatchdogService { get; }
    public List<IOptimizationModule> Modules { get; }

    public DashboardViewModel DashboardVM { get; }
    public OptimizerViewModel OptimizerVM { get; }
    public GameLoopViewModel GameLoopVM { get; }
    public KeymapResolutionViewModel KeymapResolutionVM { get; }
    public MonitorViewModel MonitorVM { get; }
    public GamingSessionViewModel GamingSessionVM { get; }
    public BackupViewModel BackupVM { get; }
    public LogsViewModel LogsVM { get; }

    private object _currentView;
    public object CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private string _activeTab = "Dashboard";
    public string ActiveTab
    {
        get => _activeTab;
        set => SetProperty(ref _activeTab, value);
    }

    private bool _isAdmin;
    public bool IsAdmin
    {
        get => _isAdmin;
        set => SetProperty(ref _isAdmin, value);
    }

    public bool IsDarkTheme => ThemeManager.Instance.IsDarkTheme;
    public string ThemeIcon => ThemeManager.Instance.IsDarkTheme ? "🌙" : "☀️";
    public string ThemeTooltip => ThemeManager.Instance.IsDarkTheme ? "Switch to Light Theme" : "Switch to Dark Theme";

    public ICommand NavigateCommand { get; }
    public ICommand ElevateCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public MainViewModel()
    {
        IsAdmin = PermissionManager.IsAdministrator;

        // Initialize Telemetry Monitor
        MonitorService = new PerformanceMonitorService();
        MonitorService.Start();

        // Initialize Optimization Modules (including Android VM / ADB Suite)
        Modules = new List<IOptimizationModule>
        {
            new WindowsGameModeModule(),
            new PowerPlanModule(),
            new GameLoopResourceModule(),
            new GameLoopGraphicsModule(),
            new GameLoopPUBGConfigModule(),
            new AdbGpuAccelerationModule(),
            new AdbAnimationLatencyModule(),
            new AdbInputPollingModule(),
            new Adb120FpsUnlockModule(),
            new AdbDexCompilationModule(),
            new AdbVmHeapTuningModule(),
            new AdbLogcatSuppressModule(),
            new AdbBackgroundDozeModule(),
            new AdbNetworkDnsModule(),
            new AdbAudioLatencyModule(),
            new CpuAffinityModule(),
            new GpuPreferenceModule(),
            new AudioLatencyModule(),
            new AudioFootstepClarifierModule(),
            new MemoryOptimizerModule(),
            new CleanupOptimizerModule(),
            new TimerResolutionModule(),
            new ProcessPriorityModule(),
            new NetworkLatencyModule(),
            new NetworkDnsModule(),
            new VisualEffectsModule(),
            new BackgroundThrottleModule()
        };

        // Initialize Auto-Gaming Watchdog Daemon
        WatchdogService = new GameLoopWatchdogService(() => _gameLoop);
        WatchdogService.Start();

        // Create child ViewModels
        DashboardVM = new DashboardViewModel(
            () => _hardware, 
            () => _system, 
            () => _gameLoop, 
            MonitorService, 
            QuickOptimizeAsync);

        OptimizerVM = new OptimizerViewModel(
            Modules, 
            () => _hardware, 
            () => _system, 
            () => _gameLoop);

        GameLoopVM = new GameLoopViewModel(
            () => _hardware, 
            () => _gameLoop);

        KeymapResolutionVM = new KeymapResolutionViewModel(
            () => _hardware,
            () => _gameLoop);

        MonitorVM = new MonitorViewModel(MonitorService);

        GamingSessionVM = new GamingSessionViewModel(
            () => _hardware, 
            () => _system, 
            () => _gameLoop, 
            MonitorService, 
            WatchdogService,
            Modules);

        BackupVM = new BackupViewModel();
        LogsVM = new LogsViewModel();

        _currentView = DashboardVM;

        // Wire events
        OptimizerVM.OptimizationsChanged += (s, e) =>
        {
            RefreshSystemData();
            DashboardVM.RefreshDashboard();
            GameLoopVM.RefreshData();
            KeymapResolutionVM.RefreshKeymaps();
        };

        BackupVM.BackupsRestored += async (s, e) =>
        {
            RefreshSystemData();
            DashboardVM.RefreshDashboard();
            await OptimizerVM.AnalyzeAllAsync();
            GameLoopVM.RefreshData();
            KeymapResolutionVM.RefreshKeymaps();
        };

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string tabName)
            {
                ActiveTab = tabName;
                CurrentView = tabName switch
                {
                    "Dashboard" => DashboardVM,
                    "Optimizer" => OptimizerVM,
                    "GameLoop" => GameLoopVM,
                    "KeymapResolution" => KeymapResolutionVM,
                    "Monitor" => MonitorVM,
                    "GamingSession" => GamingSessionVM,
                    "Backup" => BackupVM,
                    "Logs" => LogsVM,
                    _ => DashboardVM
                };
            }
        });

        ElevateCommand = new RelayCommand(() =>
        {
            PermissionManager.RestartAsAdministrator();
        });

        ToggleThemeCommand = new RelayCommand(() =>
        {
            ThemeManager.Instance.ToggleTheme();
        });

        ThemeManager.Instance.ThemeChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeTooltip));
        };

        // Initial Data Load
        Task.Run(async () => await InitializeAsync());
    }

    private async Task InitializeAsync()
    {
        RefreshSystemData();
        DashboardVM.RefreshDashboard();
        GameLoopVM.RefreshData();
        await OptimizerVM.AnalyzeAllAsync();
        DashboardVM.RefreshDashboard();
    }

    private void RefreshSystemData()
    {
        _hardware = HardwareDetector.DetectHardware();
        _system = SystemDetector.DetectSystem();
        _gameLoop = GameLoopDetector.DetectGameLoop();

        OnPropertyChanged(nameof(Hardware));
        OnPropertyChanged(nameof(System));
        OnPropertyChanged(nameof(GameLoop));
    }

    public async Task QuickOptimizeAsync()
    {
        OptimizerVM.CurrentProfile = OptimizationProfile.Safe;
        await OptimizerVM.OptimizeSelectedAsync();
        RefreshSystemData();
        DashboardVM.RefreshDashboard();
    }
}
