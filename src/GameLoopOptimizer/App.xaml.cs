using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Core.Navigation;
using GameLoopOptimizer.Monitoring;
using GameLoopOptimizer.Optimizations;
using GameLoopOptimizer.ViewModels;

namespace GameLoopOptimizer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core Infrastructure Singletons
        services.AddSingleton<IEventAggregator>(EventAggregator.Default);
        services.AddSingleton<IAdbManager>(DefaultAdbManager.Instance);
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<PerformanceMonitorService>();
        services.AddSingleton(sp => new GameLoopWatchdogService(() => GameLoopDetector.DetectGameLoop()));
        services.AddSingleton<IDaemonServiceManager, DaemonServiceManager>();

        // Auto-discover all IOptimizationModule implementations via registry
        foreach (var type in OptimizationModuleRegistry.GetModuleTypes())
        {
            services.AddSingleton(typeof(IOptimizationModule), type);
        }

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<OptimizerViewModel>();
        services.AddTransient<GameLoopViewModel>();
        services.AddTransient<KeymapResolutionViewModel>();
        services.AddTransient<MonitorViewModel>();
        services.AddTransient<GamingSessionViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<AdbStudioViewModel>();
        services.AddTransient<ActiveSavViewModel>();
        services.AddTransient<AimSensitivityViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Services = ConfigureServices();

        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Error("AppCrash", $"Unhandled UI exception: {args.Exception.Message}\n{args.Exception.StackTrace}");
            MessageBox.Show($"An unexpected error occurred:\n{args.Exception.Message}\n\nCheck logs for details.", "GameLoop Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Logger.Error("AppDomainCrash", $"Fatal unhandled exception: {ex.Message}\n{ex.StackTrace}");
            }
        };

        ThemeManager.Instance.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (Services?.GetService<IDaemonServiceManager>() is IDaemonServiceManager daemonManager)
            {
                daemonManager.Dispose();
            }
        }
        catch { }

        base.OnExit(e);
    }
}
