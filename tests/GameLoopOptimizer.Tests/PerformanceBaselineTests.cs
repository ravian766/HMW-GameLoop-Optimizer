using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class PerformanceBaselineTests
{
    [Fact]
    public void OptimizationComparison_CalculatesDeltasAndVerdictCorrectly()
    {
        var before = new BaselineBenchmark
        {
            AverageFps = 60.0,
            OnePercentLowFps = 42.0,
            FrameTimeVarianceMs = 4.5,
            CpuUsagePercent = 65.0,
            RamUsageGb = 6.0
        };

        var after = new BaselineBenchmark
        {
            AverageFps = 72.0,
            OnePercentLowFps = 58.0,
            FrameTimeVarianceMs = 2.1,
            CpuUsagePercent = 52.0,
            RamUsageGb = 5.1
        };

        var comparison = new OptimizationComparison
        {
            BaselineBefore = before,
            BaselineAfter = after
        };

        Assert.Equal(12.0, comparison.FpsDelta);
        Assert.Equal(16.0, comparison.OnePercentLowDelta);
        Assert.Equal(-2.4, comparison.FrameTimeVarianceDeltaMs);
        Assert.True(comparison.HasImproved);
        Assert.Contains("Significant Stability Improvement", comparison.StabilityVerdict);
    }

    [Fact]
    public void OptimizationComparison_DetectsRegressionWhenVarianceSpikes()
    {
        var before = new BaselineBenchmark
        {
            AverageFps = 85.0,
            OnePercentLowFps = 70.0,
            FrameTimeVarianceMs = 2.0
        };

        var after = new BaselineBenchmark
        {
            AverageFps = 70.0,
            OnePercentLowFps = 50.0,
            FrameTimeVarianceMs = 5.0
        };

        var comparison = new OptimizationComparison
        {
            BaselineBefore = before,
            BaselineAfter = after
        };

        Assert.True(comparison.FpsDelta < 0);
        Assert.False(comparison.HasImproved);
        Assert.Contains("Increased Frame-Time Variance", comparison.StabilityVerdict);
    }

    [Fact]
    public void BaselineService_RecordsBeforeAndAfterAndGeneratesComparison()
    {
        var monitor = new PerformanceMonitorService();
        var service = new PerformanceBaselineService(monitor);

        var b1 = new BaselineBenchmark { AverageFps = 60 };
        var b2 = new BaselineBenchmark { AverageFps = 75 };

        bool eventFired = false;
        service.ComparisonGenerated += (s, e) =>
        {
            eventFired = true;
            Assert.Equal(15.0, e.FpsDelta);
        };

        service.RecordBaselineBefore(b1);
        Assert.NotNull(service.BaselineBefore);
        Assert.Null(service.LatestComparison);

        service.RecordBaselineAfter(b2);
        Assert.NotNull(service.BaselineAfter);
        Assert.NotNull(service.LatestComparison);
        Assert.True(eventFired);
    }
}
