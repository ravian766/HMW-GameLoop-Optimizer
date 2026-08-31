using Xunit;
using Microsoft.Extensions.DependencyInjection;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
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
}
