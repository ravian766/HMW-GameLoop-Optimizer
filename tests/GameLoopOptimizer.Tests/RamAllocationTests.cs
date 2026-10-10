using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.ViewModels;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class RamAllocationTests
{
    [Fact]
    public void RamAllocation_8GbOn16GbHost_DoesNotTriggerWarning()
    {
        var hw = new HardwareInfo { TotalRamGb = 16.0, RefreshRateHz = 144 };
        var gl = new GameLoopConfig { IsInstalled = true, VmMemorySizeInMb = 8192 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        vm.RamMb = 8192;

        Assert.False(vm.HasRamWarning);
        Assert.Empty(vm.RamWarningMessage);
        Assert.Contains("safely reserved for Windows Host", vm.RamAllocationHostStatus);
        Assert.Contains("8 GB Allocated", vm.RamAllocationHostStatus);
    }

    [Fact]
    public void RamAllocation_12GbOn16GbHost_TriggersWarning()
    {
        var hw = new HardwareInfo { TotalRamGb = 16.0, RefreshRateHz = 144 };
        var gl = new GameLoopConfig { IsInstalled = true, VmMemorySizeInMb = 8192 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        vm.RamMb = 12288;

        Assert.True(vm.HasRamWarning);
        Assert.Contains("High RAM Allocation", vm.RamWarningMessage);
        Assert.Contains("12 GB", vm.RamWarningMessage);
        Assert.Contains("Risk of paging", vm.RamAllocationHostStatus);
    }

    [Fact]
    public void RamAllocation_16GbOn16GbHost_TriggersSevereWarning()
    {
        var hw = new HardwareInfo { TotalRamGb = 16.0, RefreshRateHz = 144 };
        var gl = new GameLoopConfig { IsInstalled = true, VmMemorySizeInMb = 8192 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        vm.RamMb = 16384;

        Assert.True(vm.HasRamWarning);
        Assert.Contains("leaves only 0.0 GB for Windows OS", vm.RamWarningMessage);
        Assert.Contains("Risk of paging", vm.RamAllocationHostStatus);
    }

    [Fact]
    public void RamAllocation_12GbAnd16GbOn32GbHost_AreSafe()
    {
        var hw = new HardwareInfo { TotalRamGb = 32.0, RefreshRateHz = 144 };
        var gl = new GameLoopConfig { IsInstalled = true, VmMemorySizeInMb = 8192 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        // 10 GB on 32 GB
        vm.RamMb = 10240;
        Assert.False(vm.HasRamWarning);
        Assert.Contains("safely reserved", vm.RamAllocationHostStatus);

        // 12 GB on 32 GB
        vm.RamMb = 12288;
        Assert.False(vm.HasRamWarning);
        Assert.Contains("safely reserved", vm.RamAllocationHostStatus);

        // 16 GB on 32 GB (50% allocation)
        vm.RamMb = 16384;
        Assert.False(vm.HasRamWarning);
        Assert.Contains("16.0 GB safely reserved", vm.RamAllocationHostStatus);
    }

    [Fact]
    public void RamAllocation_16GbOn64GbHost_LeavesAbundantReserve()
    {
        var hw = new HardwareInfo { TotalRamGb = 64.0, RefreshRateHz = 144 };
        var gl = new GameLoopConfig { IsInstalled = true, VmMemorySizeInMb = 8192 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        vm.RamMb = 16384;

        Assert.False(vm.HasRamWarning);
        Assert.Contains("48.0 GB safely reserved", vm.RamAllocationHostStatus);
    }

    [Fact]
    public void RamAllocation_8GbOn8GbHost_TriggersWarning()
    {
        var hw = new HardwareInfo { TotalRamGb = 8.0, RefreshRateHz = 60 };
        var gl = new GameLoopConfig { IsInstalled = true, VmMemorySizeInMb = 4096 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        vm.RamMb = 8192;

        Assert.True(vm.HasRamWarning);
        Assert.Contains("leaves only 0.0 GB for Windows OS", vm.RamWarningMessage);
    }

    [Theory]
    [InlineData(10240, "10GB")]
    [InlineData(12288, "12GB")]
    [InlineData(16384, "16GB")]
    public void CurrentEngineAndResDisplay_FormatsHigherRamCorrectly(int ramMb, string expectedDisplay)
    {
        var hw = new HardwareInfo { TotalRamGb = 32.0, RefreshRateHz = 144 };
        var gl = new GameLoopConfig { IsInstalled = true };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());

        vm.CpuCores = 4;
        vm.ResWidth = 1920;
        vm.ResHeight = 1080;
        vm.RamMb = ramMb;

        Assert.Equal($"4C/{expectedDisplay} • 1920x1080", vm.CurrentEngineAndResDisplay);
    }
}
