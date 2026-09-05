using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Core.Navigation;
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

    public IEventAggregator EventAggregator { get; }
    public PerformanceMonitorService MonitorService { get; }
    public GameLoopWatchdogService WatchdogService { get; }
    public List<IOptimizationModule> Modules { get; }
    public INavigationService Navigation { get; }

    public DashboardViewModel DashboardVM { get; }
    public OptimizerViewModel OptimizerVM { get; }
    public GameLoopViewModel GameLoopVM { get; }
    public KeymapResolutionViewModel KeymapResolutionVM { get; }
    public MonitorViewModel MonitorVM { get; }
    public GamingSessionViewModel GamingSessionVM { get; }
    public BackupViewModel BackupVM { get; }
    public LogsViewModel LogsVM { get; }

    public object CurrentView => Navigation?.CurrentView ?? DashboardVM;

    public string ActiveTab
    {
        get => Navigation?.ActiveTab ?? "Dashboard";
        set => Navigation?.NavigateTo(value);
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

    // Auto-Update Properties
    private UpdateInfo? _availableUpdate;
    public UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        set => SetProperty(ref _availableUpdate, value);
    }

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    private bool _isCheckingForUpdate;
    public bool IsCheckingForUpdate
    {
        get => _isCheckingForUpdate;
        set => SetProperty(ref _isCheckingForUpdate, value);
    }

    private bool _isDownloadingUpdate;
    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set => SetProperty(ref _isDownloadingUpdate, value);
    }

    private double _updateProgress;
    public double UpdateProgress
    {
        get => _updateProgress;
        set => SetProperty(ref _updateProgress, value);
    }

    private string _updateStatusMessage = string.Empty;
    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        set => SetProperty(ref _updateStatusMessage, value);
    }

    private bool _showUpdateModal;
    public bool ShowUpdateModal
    {
        get => _showUpdateModal;
        set => SetProperty(ref _showUpdateModal, value);
    }

    public string CurrentVersionDisplay => $"v{UpdateManager.Instance.GetCurrentVersion().ToString(3)}";

    public ICommand NavigateCommand { get; }
    public ICommand ElevateCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand OpenUpdateModalCommand { get; }
    public ICommand DismissUpdateCommand { get; }
    public ICommand DownloadAndApplyUpdateCommand { get; }

    public MainViewModel() : this(null, null, null, null, null)
    {
    }

    public MainViewModel(
        IEventAggregator? eventAggregator = null,
        PerformanceMonitorService? monitorService = null,
        GameLoopWatchdogService? watchdogService = null,
        IEnumerable<IOptimizationModule>? modules = null,
        INavigationService? navigationService = null)
    {
        IsAdmin = PermissionManager.IsAdministrator;
        EventAggregator = eventAggregator ?? Core.EventAggregator.Default;

        // Initialize Telemetry Monitor & Watchdog
        MonitorService = monitorService ?? new PerformanceMonitorService();
        MonitorService.Start();

        WatchdogService = watchdogService ?? new GameLoopWatchdogService(() => _gameLoop);
        WatchdogService.Start();

        // Initialize Optimization Modules via unified registry
        Modules = modules?.ToList() ?? OptimizationModuleRegistry.CreateAllModules();

        // Create child ViewModels
        DashboardVM = new DashboardViewModel(
            () => _hardware, 
            () => _system, 
            () => _gameLoop, 
            MonitorService, 
            QuickOptimizeAsync,
            ProEsportsOptimizeAsync);

        OptimizerVM = new OptimizerViewModel(
            Modules, 
            () => _hardware, 
            () => _system, 
            () => _gameLoop);

        GameLoopVM = new GameLoopViewModel(
            () => _hardware, 
            () => _gameLoop,
            EventAggregator);

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

        // Initialize and configure decoupled Navigation Service
        Navigation = navigationService ?? new NavigationService();
        Navigation.RegisterView("Dashboard", () => DashboardVM);
        Navigation.RegisterView("Optimizer", () => OptimizerVM);
        Navigation.RegisterView("GameLoop", () => GameLoopVM);
        Navigation.RegisterView("KeymapResolution", () => KeymapResolutionVM);
        Navigation.RegisterView("Monitor", () => MonitorVM);
        Navigation.RegisterView("GamingSession", () => GamingSessionVM);
        Navigation.RegisterView("Backup", () => BackupVM);
        Navigation.RegisterView("Logs", () => LogsVM);

        Navigation.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(INavigationService.ActiveTab))
                OnPropertyChanged(nameof(ActiveTab));
            else if (e.PropertyName == nameof(INavigationService.CurrentView))
                OnPropertyChanged(nameof(CurrentView));
        };

        Navigation.NavigateTo("Dashboard");

        // Wire decoupled events via EventAggregator
        EventAggregator.Subscribe<OptimizationsChangedMessage>(_ =>
        {
            RefreshSystemData();
            DashboardVM.RefreshDashboard();
            GameLoopVM.RefreshData();
            KeymapResolutionVM.RefreshKeymaps();
        });

        EventAggregator.Subscribe<BackupRestoredMessage>(async _ =>
        {
            RefreshSystemData();
            DashboardVM.RefreshDashboard();
            await OptimizerVM.AnalyzeAllAsync();
            GameLoopVM.RefreshData();
            KeymapResolutionVM.RefreshKeymaps();
        });

        // Direct VM event bridge to publish messages
        OptimizerVM.OptimizationsChanged += (s, e) => EventAggregator.Publish(new OptimizationsChangedMessage());
        BackupVM.BackupsRestored += (s, e) => EventAggregator.Publish(new BackupRestoredMessage());

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string tabName)
            {
                Navigation.NavigateTo(tabName);
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

        CheckForUpdatesCommand = new RelayCommand(async () =>
        {
            await CheckForUpdatesManuallyAsync();
        });

        OpenUpdateModalCommand = new RelayCommand(() =>
        {
            ShowUpdateModal = true;
        });

        DismissUpdateCommand = new RelayCommand(() =>
        {
            ShowUpdateModal = false;
        });

        DownloadAndApplyUpdateCommand = new RelayCommand(async () =>
        {
            await DownloadAndApplyUpdateAsync();
        });

        ThemeManager.Instance.ThemeChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeTooltip));
        };

        // Initial Data Load (Safe execution with error trapping)
        Task.Run(async () =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("MainViewModel", $"Async initialization error: {ex.Message}");
            }
        });
    }

    private async Task InitializeAsync()
    {
        try
        {
            RefreshSystemData();
            DashboardVM.RefreshDashboard();
            GameLoopVM.RefreshData();
            await OptimizerVM.AnalyzeAllAsync();
            DashboardVM.RefreshDashboard();

            // Silent background update check
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2500);
                    await CheckForUpdatesSilentlyAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error("MainViewModel", $"Background update check error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error("MainViewModel", $"InitializeAsync error: {ex.Message}");
        }
    }

    public async Task CheckForUpdatesSilentlyAsync()
    {
        try
        {
            var update = await UpdateManager.Instance.CheckForUpdatesAsync();
            if (update != null)
            {
                AvailableUpdate = update;
                IsUpdateAvailable = true;
                Logger.Info("MainViewModel", $"New update discovered: {update.TagName} ({update.ReleaseTitle})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("MainViewModel", $"Silent update check error: {ex.Message}");
        }
    }

    public async Task CheckForUpdatesManuallyAsync()
    {
        if (IsCheckingForUpdate) return;

        IsCheckingForUpdate = true;
        UpdateStatusMessage = "Checking for latest release on GitHub...";

        try
        {
            var update = await UpdateManager.Instance.CheckForUpdatesAsync();
            if (update != null)
            {
                AvailableUpdate = update;
                IsUpdateAvailable = true;
                ShowUpdateModal = true;
            }
            else
            {
                IsUpdateAvailable = false;
                Logger.Info("MainViewModel", "Application is up to date.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("MainViewModel", $"Manual update check error: {ex.Message}");
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    public async Task DownloadAndApplyUpdateAsync()
    {
        if (AvailableUpdate == null || IsDownloadingUpdate) return;

        IsDownloadingUpdate = true;
        UpdateProgress = 0;
        UpdateStatusMessage = "Starting download from GitHub Releases...";

        var progress = new Progress<double>(p =>
        {
            UpdateProgress = p;
            UpdateStatusMessage = $"Downloading update: {p:F0}%";
        });

        try
        {
            string zipPath = await UpdateManager.Instance.DownloadUpdateAsync(AvailableUpdate, progress);
            UpdateStatusMessage = "Applying update and restarting application...";
            await Task.Delay(600);

            bool success = UpdateManager.Instance.ApplyUpdateAndRestart(zipPath);
            if (!success)
            {
                UpdateStatusMessage = "Failed to launch updater. Please try updating manually.";
                IsDownloadingUpdate = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("MainViewModel", $"Failed to download or apply update: {ex.Message}");
            UpdateStatusMessage = $"Update failed: {ex.Message}";
            IsDownloadingUpdate = false;
        }
    }

    private void RefreshSystemData()
    {
        _hardware = HardwareDetector.DetectHardware();
        _system = SystemDetector.DetectSystem();
        _gameLoop = GameLoopDetector.DetectGameLoop();

        OnPropertyChanged(nameof(Hardware));
        OnPropertyChanged(nameof(System));
        OnPropertyChanged(nameof(GameLoop));

        EventAggregator.Publish(new SystemDataRefreshedMessage());
    }

    public async Task QuickOptimizeAsync()
    {
        OptimizerVM.CurrentProfile = OptimizationProfile.Safe;
        await OptimizerVM.OptimizeSelectedAsync();
        RefreshSystemData();
        DashboardVM.RefreshDashboard();
    }

    public async Task ProEsportsOptimizeAsync()
    {
        Logger.Info("EsportsMode", "Engaging 1-Click Pro Esports Setup...");
        OptimizerVM.CurrentProfile = OptimizationProfile.MaximumPerformance;
        await OptimizerVM.OptimizeSelectedAsync();

        // Apply specialized low-latency modules via module pipeline for scoring & rollback tracking
        var standbyModule = Modules.OfType<StandbyListCleanerModule>().FirstOrDefault();
        if (standbyModule != null)
        {
            await standbyModule.ApplyAsync(_hardware, _system, _gameLoop);
        }
        else
        {
            StandbyListCleanerService.PurgeStandbyList();
        }

        var timerModule = Modules.OfType<TimerResolutionModule>().FirstOrDefault();
        if (timerModule != null)
        {
            await timerModule.ApplyAsync(_hardware, _system, _gameLoop);
        }
        else
        {
            Optimizations.TimerResolutionModule.SetHighPrecision(0.5);
        }

        if (AdbManager.IsAdbAvailable(_gameLoop))
        {
            var adb120Module = Modules.OfType<Adb120FpsUnlockModule>().FirstOrDefault();
            if (adb120Module != null)
            {
                await adb120Module.ApplyAsync(_hardware, _system, _gameLoop);
            }
            else
            {
                await AdbManager.Unlock120FpsAsync(_gameLoop);
            }
        }

        RefreshSystemData();
        DashboardVM.RefreshDashboard();
        Logger.Success("EsportsMode", "1-Click Pro Esports Setup completed successfully!");
    }
}
