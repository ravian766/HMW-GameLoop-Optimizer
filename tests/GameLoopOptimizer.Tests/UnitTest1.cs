using Xunit;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Tests;

public class HonestMetricsAndHardwareTests
{
    [Fact]
    public void NativeMethods_MemoryLoad_ReturnsValidPercentage()
    {
        double load = NativeMethods.GetMemoryLoadPercent();
        Assert.True(load >= 0 && load <= 100, $"Memory load should be between 0 and 100, was: {load}");
    }

    [Fact]
    public void NativeMethods_GetAvailableMemoryMb_ReturnsPositiveValue()
    {
        double availMb = NativeMethods.GetAvailableMemoryMb();
        Assert.True(availMb > 0, $"Available physical memory should be > 0 MB, was: {availMb}");
    }

    [Fact]
    public void PerformanceMetrics_DefaultFpsLabels_ReflectHonestNonFabricatedState()
    {
        var metrics = new PerformanceMetrics();
        Assert.False(metrics.IsFpsMeasurable);
        Assert.False(metrics.IsFpsEstimated);
        Assert.Equal(0, metrics.Fps);
        Assert.Equal("--", metrics.FpsDisplayLabel);

        metrics.Fps = 90;
        metrics.IsFpsMeasurable = true;
        Assert.Equal("90", metrics.FpsDisplayLabel);

        metrics.IsFpsMeasurable = false;
        metrics.IsFpsEstimated = true;
        Assert.Equal("90 (Est)", metrics.FpsDisplayLabel);
    }

    [Fact]
    public void GameLoopProcessNames_ContainsEssentialProcesses()
    {
        Assert.Contains(GameLoopProcessNames.AndroidEmulator, GameLoopProcessNames.GameEngines);
        Assert.Contains(GameLoopProcessNames.AowExe, GameLoopProcessNames.GameEngines);
        Assert.Contains(GameLoopProcessNames.AppMarket, GameLoopProcessNames.AllProcesses);
        Assert.DoesNotContain(GameLoopProcessNames.AppMarket, GameLoopProcessNames.GameEngines);
    }

    [Fact]
    public void OptimizationSnapshot_And_Comparison_ComputesAccurateDeltas()
    {
        var before = new OptimizationSnapshot
        {
            Timestamp = DateTime.Now.AddMinutes(-5),
            CpuTotalPercent = 45.0,
            RamUsedGb = 8.5,
            RamTotalGb = 16.0,
            RamPercent = 53.1,
            ActiveProcessCount = 180,
            OptimizedModulesCount = 10,
            TotalModulesCount = 38
        };

        var after = new OptimizationSnapshot
        {
            Timestamp = DateTime.Now,
            CpuTotalPercent = 32.0,
            RamUsedGb = 7.1,
            RamTotalGb = 16.0,
            RamPercent = 44.3,
            ActiveProcessCount = 165,
            OptimizedModulesCount = 25,
            TotalModulesCount = 38
        };

        var comparison = new SnapshotComparison(before, after);

        Assert.Equal(-13.0, comparison.CpuDeltaPercent);
        Assert.Equal(-1.4, comparison.RamDeltaGb);
        Assert.Equal(-15, comparison.ProcessDelta);
        Assert.Equal(15, comparison.NewlyOptimizedModules);
        Assert.Contains("1.40 GB freed", comparison.SummaryText);
        Assert.Contains("15 trimmed", comparison.SummaryText);
    }

    [Fact]
    public void OptimizationReport_ProducesAccurateSummary()
    {
        var report = new OptimizationReport
        {
            ProfileName = "Competitive",
            AppliedCount = 12,
            FailedCount = 0,
            TotalConsidered = 12,
            ScoreBefore = 55,
            ScoreAfter = 88
        };

        Assert.Contains("Applied 12 optimizations", report.SummaryText);
        Assert.Contains("55/100 ➔ 88/100", report.SummaryText);
        Assert.Contains("+33 pts", report.SummaryText);
    }

    [Fact]
    public async Task NetworkDiagnosticsService_Loopback_ExecutesAccurately()
    {
        // 127.0.0.1 responds reliably in all environments
        var res = await NetworkDiagnosticsService.RunDiagnosticsAsync("127.0.0.1", 3, 500);

        Assert.NotNull(res);
        Assert.Equal("127.0.0.1", res.TargetHost);
        Assert.Equal(3, res.SamplesSent);
        Assert.True(res.SamplesReceived > 0, "Loopback should respond with at least 1 ping sample");
        Assert.True(res.JitterMs >= 0, "Jitter should be non-negative");
        Assert.False(string.IsNullOrWhiteSpace(res.QualityRating));
        Assert.False(string.IsNullOrWhiteSpace(res.Recommendation));
    }

    [Fact]
    public void OptimizationProfile_AllProfiles_IncludeCompetitiveAndLowEndPC()
    {
        var values = Enum.GetValues<OptimizationProfile>();
        Assert.Contains(OptimizationProfile.Safe, values);
        Assert.Contains(OptimizationProfile.Balanced, values);
        Assert.Contains(OptimizationProfile.MaximumPerformance, values);
        Assert.Contains(OptimizationProfile.Competitive, values);
        Assert.Contains(OptimizationProfile.LowEndPC, values);
        Assert.Contains(OptimizationProfile.Custom, values);
    }
}
