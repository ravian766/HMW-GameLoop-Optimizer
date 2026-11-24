using Xunit;
using Microsoft.Extensions.DependencyInjection;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Core.Navigation;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using GameLoopOptimizer.Upscaler;
using GameLoopOptimizer.ViewModels;

namespace GameLoopOptimizer.Tests;

public class ArchitectureTests
{
    [Fact]
    public void DependencyInjection_RegistersAndResolvesAllServicesAndModules()
    {
        var services = App.ConfigureServices();
        Assert.NotNull(services);

        var eventAggregator = services.GetService<IEventAggregator>();
        Assert.NotNull(eventAggregator);

        var adbManager = services.GetService<IAdbManager>();
        Assert.NotNull(adbManager);

        var daemonManager = services.GetService<IDaemonServiceManager>();
        Assert.NotNull(daemonManager);

        var modules = services.GetServices<IOptimizationModule>().ToList();
        Assert.NotEmpty(modules);
        Assert.True(modules.Count >= 30, $"Expected at least 30 modules, but found {modules.Count}");

        var mainVm = services.GetService<MainViewModel>();
        Assert.NotNull(mainVm);
        Assert.NotNull(mainVm.DashboardVM);
        Assert.NotNull(mainVm.OptimizerVM);
        Assert.NotNull(mainVm.GameLoopVM);
        Assert.NotNull(mainVm.AdbStudioVM);
        Assert.NotNull(mainVm.UpscalerVM);

        mainVm.WatchdogService.Dispose();
        mainVm.MonitorService.Dispose();
        mainVm.UpscalerVM.Cleanup();
    }

    [Fact]
    public void MainViewModel_AdbStudioWorkspace_IsSegregatedAndNavigable()
    {
        var mainVm = new MainViewModel();
        Assert.NotNull(mainVm.AdbStudioVM);

        // Verify navigation to dedicated AdbStudio workspace
        mainVm.NavigateCommand.Execute("AdbStudio");
        Assert.Equal("AdbStudio", mainVm.ActiveTab);
        Assert.Same(mainVm.AdbStudioVM, mainVm.CurrentView);

        // Verify OpenAdbStudioCommand from GameLoopVM switches workspace
        mainVm.NavigateCommand.Execute("GameLoop");
        Assert.Equal("GameLoop", mainVm.ActiveTab);

        mainVm.GameLoopVM.OpenAdbStudioCommand.Execute(null);
        Assert.Equal("AdbStudio", mainVm.ActiveTab);
        Assert.Same(mainVm.AdbStudioVM, mainVm.CurrentView);

        mainVm.WatchdogService.Dispose();
        mainVm.MonitorService.Dispose();
        mainVm.UpscalerVM.Cleanup();
    }

    [Fact]
    public void EventAggregator_PublishesAndSubscribesDecoupledMessages()
    {
        var eventAggregator = new EventAggregator();
        bool received = false;
        string receivedMsg = string.Empty;

        Action<StatusNotificationMessage> handler = msg =>
        {
            received = true;
            receivedMsg = msg.Message;
        };

        eventAggregator.Subscribe(handler);
        eventAggregator.Publish(new StatusNotificationMessage("Architecture Test"));

        Assert.True(received);
        Assert.Equal("Architecture Test", receivedMsg);

        // Test Unsubscribe
        received = false;
        eventAggregator.Unsubscribe(handler);
        eventAggregator.Publish(new StatusNotificationMessage("Should not receive"));

        Assert.False(received);
    }

    [Fact]
    public void GameLoopViewModel_DecomposedSubViewModels_AreSynchronized()
    {
        var hw = new HardwareInfo { CpuName = "Test CPU", TotalRamGb = 16 };
        var gl = new GameLoopConfig { InstallPath = @"C:\Test\GameLoop", IsInstalled = true };
        var eventAggregator = new EventAggregator();

        var vm = new GameLoopViewModel(() => hw, () => gl, eventAggregator);

        Assert.NotNull(vm.AdbStudio);
        Assert.NotNull(vm.ActiveSav);
        Assert.NotNull(vm.AimSens);

        // Verify delegation
        vm.AdbZeroAnimations = false;
        Assert.False(vm.AdbStudio.AdbZeroAnimations);

        vm.ActiveSavFpsLevel = 6;
        Assert.Equal(6, vm.ActiveSav.ActiveSavFpsLevel);

        vm.SelectedMouseDpi = 1600;
        Assert.Equal(1600, vm.AimSens.SelectedMouseDpi);
    }

    [Fact]
    public void DefaultAdbManager_ImplementsInterface()
    {
        IAdbManager manager = DefaultAdbManager.Instance;
        Assert.NotNull(manager);
    }

    [Fact]
    public void NavigationService_NavigatesAndUpdatesState()
    {
        var nav = new NavigationService();
        var dummyView = new object();
        bool eventFired = false;
        string? navigatedTab = null;

        nav.RegisterView("TestTab", () => dummyView);
        nav.Navigated += (s, tab) =>
        {
            eventFired = true;
            navigatedTab = tab;
        };

        nav.NavigateTo("TestTab");

        Assert.True(eventFired);
        Assert.Equal("TestTab", navigatedTab);
        Assert.Equal("TestTab", nav.ActiveTab);
        Assert.Same(dummyView, nav.CurrentView);
    }

    [Fact]
    public void OptimizationModuleRegistry_DiscoversAndInstantiatesConcreteModules()
    {
        var types = OptimizationModuleRegistry.GetModuleTypes();
        Assert.NotEmpty(types);
        Assert.True(types.Count >= 30, $"Expected >= 30 types, found {types.Count}");

        var modules = OptimizationModuleRegistry.CreateAllModules();
        Assert.Equal(types.Count, modules.Count);
        Assert.All(modules, m => Assert.NotNull(m));
    }

    [Fact]
    public void NativeMethods_ReturnsValidPhysicalMemoryLoad()
    {
        double load = NativeMethods.GetMemoryLoadPercent();
        Assert.True(load >= 0 && load <= 100, $"Expected memory load between 0-100, got {load}");
    }

    [Fact]
    public void AllViews_CanBeInstantiated_WithoutXamlException()
    {
        Exception? caughtEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new GameLoopOptimizer.App();
                    app.InitializeComponent();
                }

                // Instantiate all views to verify XAML resources and markup compile/parse cleanly
                var dashboard = new GameLoopOptimizer.Views.DashboardView();
                var optimizer = new GameLoopOptimizer.Views.OptimizerView();
                var gameloop = new GameLoopOptimizer.Views.GameLoopView();
                var ingame = new GameLoopOptimizer.Views.InGameGraphicsView();
                var keymap = new GameLoopOptimizer.Views.KeymapResolutionView();
                var adbStudio = new GameLoopOptimizer.Views.AdbStudioView();
                var monitor = new GameLoopOptimizer.Views.MonitorView();
                var logs = new GameLoopOptimizer.Views.LogsView();
                var session = new GameLoopOptimizer.Views.GamingSessionView();
                var upscaler = new GameLoopOptimizer.Views.UpscalerView();
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }
            finally
            {
                try
                {
                    if (System.Windows.Application.Current != null)
                    {
                        typeof(System.Windows.Application)
                            .GetField("_appCreatedInThisAppDomain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                            ?.SetValue(null, false);
                        typeof(System.Windows.Application)
                            .GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            ?.SetValue(null, null);
                    }
                }
                catch { }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (caughtEx != null)
        {
            throw new Exception($"Failed instantiating view: {caughtEx.InnerException?.Message ?? caughtEx.Message}", caughtEx);
        }
    }

    [Fact]
    public void MainViewModel_CanNavigateToAllTabs_WithoutException()
    {
        var services = App.ConfigureServices();
        var mainVm = services.GetRequiredService<MainViewModel>();

        string[] tabs = new[] { "Dashboard", "Optimizer", "GameLoop", "AdbStudio", "InGameGraphics", "KeymapResolution", "Monitor", "GamingSession", "Logs", "Upscaler" };
        foreach (var tab in tabs)
        {
            mainVm.NavigateCommand.Execute(tab);
            Assert.Equal(tab, mainVm.ActiveTab);
            Assert.NotNull(mainVm.CurrentView);
        }

        mainVm.WatchdogService.Dispose();
        mainVm.MonitorService.Dispose();
        mainVm.UpscalerVM.Cleanup();
    }
}
