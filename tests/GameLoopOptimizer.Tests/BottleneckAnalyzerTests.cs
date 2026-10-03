using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class BottleneckAnalyzerTests
{
    [Fact]
    public void Analyze_ThermalBottleneck_TriggeredWhenTemperatureElevated()
    {
        var metrics = new PerformanceMetrics
        {
            CpuTemperatureC = 92,
            GpuTemperatureC = 75,
            CpuTotalPercent = 50,
            GpuPercent = 50,
            RamTotalGb = 16,
            RamUsedGb = 8
        };
        var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 16, UsedRamGb = 8 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var result = BottleneckAnalyzer.Analyze(metrics, hw, sys, gl);

        Assert.Equal(BottleneckType.ThermalBottleneck, result.PrimaryBottleneck);
        Assert.True(result.ConfidencePercent >= 80);
        Assert.NotEmpty(result.RecommendedActions);
        Assert.NotEmpty(result.ActionsToAvoid);
    }

    [Fact]
    public void Analyze_RamBottleneck_TriggeredWhenRamSaturated()
    {
        var metrics = new PerformanceMetrics
        {
            RamTotalGb = 8,
            RamUsedGb = 7.6, // >90%
            CpuTemperatureC = 60,
            GpuTemperatureC = 60,
            CpuTotalPercent = 40,
            GpuPercent = 40
        };
        var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 8, UsedRamGb = 7.5 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var result = BottleneckAnalyzer.Analyze(metrics, hw, sys, gl);

        Assert.Equal(BottleneckType.RamBottleneck, result.PrimaryBottleneck);
        Assert.Contains(result.RecommendedActions, a => a.Contains("Standby List"));
    }

    [Fact]
    public void Analyze_VramBottleneck_TriggeredWhenVramOver90Percent()
    {
        var metrics = new PerformanceMetrics
        {
            RamTotalGb = 16,
            RamUsedGb = 8,
            CpuTemperatureC = 60,
            GpuTemperatureC = 60,
            GpuVramUsedMb = 3800,
            CpuTotalPercent = 40,
            GpuPercent = 70
        };
        var hw = new HardwareInfo { DedicatedVramMb = 4096, TotalRamGb = 16, UsedRamGb = 8 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var result = BottleneckAnalyzer.Analyze(metrics, hw, sys, gl);

        Assert.Equal(BottleneckType.VramBottleneck, result.PrimaryBottleneck);
    }

    [Fact]
    public void Analyze_GpuBound_TriggeredWhenGpuHighAndCpuModerate()
    {
        var metrics = new PerformanceMetrics
        {
            RamTotalGb = 16,
            RamUsedGb = 8,
            CpuTemperatureC = 60,
            GpuTemperatureC = 65,
            CpuTotalPercent = 45,
            GpuPercent = 96
        };
        var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 16, UsedRamGb = 8 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var result = BottleneckAnalyzer.Analyze(metrics, hw, sys, gl);

        Assert.Equal(BottleneckType.GpuBottleneck, result.PrimaryBottleneck);
    }

    [Fact]
    public void Analyze_CpuBound_TriggeredWhenCpuHighAndGpuLow()
    {
        var metrics = new PerformanceMetrics
        {
            RamTotalGb = 16,
            RamUsedGb = 8,
            CpuTemperatureC = 65,
            GpuTemperatureC = 55,
            CpuTotalPercent = 88,
            GpuPercent = 40
        };
        var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 16, UsedRamGb = 8 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var result = BottleneckAnalyzer.Analyze(metrics, hw, sys, gl);

        Assert.Equal(BottleneckType.CpuBottleneck, result.PrimaryBottleneck);
    }

    [Fact]
    public void Analyze_None_WhenMetricsAreHealthy()
    {
        var metrics = new PerformanceMetrics
        {
            RamTotalGb = 16,
            RamUsedGb = 8,
            CpuTemperatureC = 55,
            GpuTemperatureC = 55,
            CpuTotalPercent = 50,
            GpuPercent = 60
        };
        var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 16, UsedRamGb = 8, SystemDrive = "C:" };
        var sys = new SystemInfo { HighCpuProcessesCount = 0 };
        var gl = new GameLoopConfig();

        var result = BottleneckAnalyzer.Analyze(metrics, hw, sys, gl);

        Assert.Equal(BottleneckType.None, result.PrimaryBottleneck);
    }
}
