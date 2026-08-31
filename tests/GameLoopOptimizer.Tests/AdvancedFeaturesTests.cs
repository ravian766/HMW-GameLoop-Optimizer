using Xunit;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;

namespace GameLoopOptimizer.Tests;

public class AdvancedFeaturesTests
{
    [Fact]
    public void FrameTimeTracker_CalculatesPercentilesAndLowsAccurately()
    {
        var tracker = new FrameTimeTracker();

        // Feed 100 samples at 120 FPS
        for (int i = 0; i < 95; i++)
        {
            tracker.AddSample(120.0);
        }

        // Add 5 stutter drops to 60 FPS
        for (int i = 0; i < 5; i++)
        {
            tracker.AddSample(60.0);
        }

        var snapshot = tracker.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.True(snapshot.AvgFps > 100.0, $"Avg FPS should be > 100 but was {snapshot.AvgFps}");
        Assert.True(snapshot.OnePercentLowFps <= snapshot.AvgFps, "1% Low should be <= Avg FPS");
        Assert.True(snapshot.PointOnePercentLowFps <= snapshot.OnePercentLowFps, "0.1% Low should be <= 1% Low");
        Assert.True(snapshot.FrameTimeVarianceMs >= 0, "Variance must be non-negative");
    }

    [Fact]
    public void FrameTimeTracker_EmptySamples_ReturnsFallbackGracefully()
    {
        var tracker = new FrameTimeTracker();
        var snapshot = tracker.GetSnapshot(90.0);

        Assert.Equal(90.0, snapshot.InstantFps);
        Assert.Equal(90.0, snapshot.AvgFps);
        Assert.True(snapshot.OnePercentLowFps > 0);
    }

    [Fact]
    public void EmulatorDiagnosticService_RunsDeepDiagnosticsAndScoresCorrectly()
    {
        var hw = new HardwareInfo
        {
            CpuName = "Intel Core i7-13700K",
            GpuName = "NVIDIA GeForce RTX 4070",
            GpuVendor = GpuVendor.Nvidia,
            TotalRamGb = 32
        };

        var config = new GameLoopConfig
        {
            InstallPath = @"C:\Program Files\TxGameAssistant",
            IsInstalled = true,
            ForceDirectX = true
        };

        var report = EmulatorDiagnosticService.RunDiagnostic(config, hw);
        Assert.NotNull(report);
        Assert.True(report.ChecksPerformed >= 6, $"Expected at least 6 checks, got {report.ChecksPerformed}");
        Assert.InRange(report.HealthScore, 0, 100);
        Assert.NotNull(report.StatusBadgeText);
    }

    [Fact]
    public async Task EmulatorDiagnosticService_AutoFixIssuesAsync_ExecutesSafely()
    {
        var hw = new HardwareInfo { GpuVendor = GpuVendor.Nvidia };
        var config = new GameLoopConfig { InstallPath = @"C:\NonExistent\Path" };

        bool result = await EmulatorDiagnosticService.AutoFixIssuesAsync(config, hw);
        Assert.True(result);
    }

    [Fact]
    public void AdbManager_KnownGamePackages_MapsPopularCompetitiveGames()
    {
        Assert.Contains(AdbManager.KnownGamePackages, p => p.PackageName == "com.tencent.ig");
        Assert.Contains(AdbManager.KnownGamePackages, p => p.PackageName == "com.pubg.imobile");
        Assert.Contains(AdbManager.KnownGamePackages, p => p.PackageName == "com.dts.freefireth");
        Assert.Contains(AdbManager.KnownGamePackages, p => p.PackageName == "com.activision.callofduty.shooter");
    }
}
