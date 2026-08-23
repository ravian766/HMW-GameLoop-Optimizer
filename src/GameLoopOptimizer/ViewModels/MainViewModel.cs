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

    public ICommand NavigateCommand { get; }
    public ICommand ElevateCommand { get; }

    public MainViewModel()
    {
        IsAdmin = PermissionManager.IsAdministrator;

        // Initialize Telemetry Monitor
        MonitorService = new PerformanceMonitorService();
        MonitorService.Start();

        // Initialize all 13 Optimization Modules
        Modules = new List<IOptimizationModule>
        {
            new WindowsGameModeModule(),
            new PowerPlanModule(),
            new GameLoopResourceModule(),
            new GameLoopGraphicsModule(),
            new GameLoopPUBGConfigModule(),
            new AudioLatencyModule(),
            new MemoryOptimizerModule(),
            new CleanupOptimizerModule(),
            new TimerResolutionModule(),
            new ProcessPriorityModule(),
            new NetworkLatencyModule(),
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

        MonitorVM = new MonitorViewModel(MonitorService);

        GamingSessionVM = new GamingSessionViewModel(
            () => _hardware, 
            () => _system, 
            () => _gameLoop, 
            MonitorService, 
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
        };

        BackupVM.BackupsRestored += (s, e) =>
        {
            RefreshSystemData();
            DashboardVM.RefreshDashboard();
            OptimizerVM.AnalyzeAllAsync();
            GameLoopVM.RefreshData();
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
