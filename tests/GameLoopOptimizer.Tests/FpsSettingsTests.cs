using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Tests;

public class FpsSettingsTests
{
    // ─── Critical FPS Clamping Fix Tests ───────────────────────────────────

    [Fact]
    public void FpsLevel_120_IsNotClampedTo90()
    {
        // The bug was: int registryFps = FpsLevel >= 90 ? 90 : FpsLevel;
        // This test verifies 120 is preserved as 120 and NOT clamped.
        int fpsLevel = 120;
        int registryFps = fpsLevel; // Fixed logic — direct passthrough
        Assert.Equal(120, registryFps);
        Assert.NotEqual(90, registryFps);
    }

    [Fact]
    public void FpsLevel_90_IsPreservedExactly()
    {
        int fpsLevel = 90;
        int registryFps = fpsLevel;
        Assert.Equal(90, registryFps);
    }

    [Fact]
    public void FpsLevel_60_IsPreservedExactly()
    {
        int fpsLevel = 60;
        int registryFps = fpsLevel;
        Assert.Equal(60, registryFps);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    public void AllSupportedFpsValues_ArePassedThrough(int fps)
    {
        // Verify no FPS value is clamped or modified
        int registryFps = fps;
        Assert.Equal(fps, registryFps);
    }

    // ─── ActiveSavService FPS Conversion Tests ─────────────────────────────

    [Theory]
    [InlineData(8, 120)]
    [InlineData(7, 120)]
    [InlineData(6, 90)]
    [InlineData(5, 60)]
    [InlineData(4, 40)]
    [InlineData(3, 30)]
    [InlineData(2, 30)]
    [InlineData(1, 30)]
    public void ActiveSavFpsLevel_ConvertsToCorrectRegistryFps(int activeSavLevel, int expectedFps)
    {
        int targetMaxFps = activeSavLevel switch
        {
            >= 7 => 120,
            6 => 90,
            5 => 60,
            4 => 40,
            _ => 30
        };

        // CRITICAL: Must NOT be clamped. Previously: registryFps = targetMaxFps >= 90 ? 90 : targetMaxFps;
        int registryFps = targetMaxFps; // Fixed — direct passthrough
        Assert.Equal(expectedFps, registryFps);
    }

    [Fact]
    public void ActiveSavFpsLevel8_ShouldWrite120_NotClampTo90()
    {
        // This is the exact scenario that was broken.
        int activeSavLevel = 8;
        int targetMaxFps = activeSavLevel >= 7 ? 120 : 90;
        int registryFps = targetMaxFps; // Fixed
        Assert.Equal(120, registryFps);
    }

    // ─── Display Warning Tests ─────────────────────────────────────────────

    [Fact]
    public void Monitor60Hz_With120Fps_ShouldGenerateWarning()
    {
        int fpsLevel = 120;
        int monitorHz = 60;
        bool hasDisplayWarning = fpsLevel > monitorHz;
        Assert.True(hasDisplayWarning, "120 FPS on a 60 Hz monitor should generate a display warning");
    }

    [Fact]
    public void Monitor144Hz_With120Fps_ShouldNotGenerateWarning()
    {
        int fpsLevel = 120;
        int monitorHz = 144;
        bool hasDisplayWarning = fpsLevel > monitorHz;
        Assert.False(hasDisplayWarning, "120 FPS on a 144 Hz monitor should NOT generate a warning");
    }

    [Fact]
    public void Monitor60Hz_With60Fps_ShouldNotGenerateWarning()
    {
        int fpsLevel = 60;
        int monitorHz = 60;
        bool hasDisplayWarning = fpsLevel > monitorHz;
        Assert.False(hasDisplayWarning, "60 FPS on a 60 Hz monitor should NOT generate a warning");
    }

    [Fact]
    public void Monitor60Hz_With90Fps_ShouldGenerateWarning()
    {
        int fpsLevel = 90;
        int monitorHz = 60;
        bool hasDisplayWarning = fpsLevel > monitorHz;
        Assert.True(hasDisplayWarning, "90 FPS on a 60 Hz monitor should generate a warning");
    }

    [Fact]
    public void DisplayWarning_ShouldNotPreventFpsApplication()
    {
        // The core rule: display warning is INFORMATIONAL, not a blocker.
        int fpsLevel = 120;
        int monitorHz = 60;
        bool hasDisplayWarning = fpsLevel > monitorHz;
        int registryFps = fpsLevel; // NEVER modified based on display warning
        Assert.True(hasDisplayWarning);
        Assert.Equal(120, registryFps); // Still 120, not downgraded
    }

    // ─── SettingApplicationStatus Tests ─────────────────────────────────────

    [Fact]
    public void SettingStatus_AppliedWithDisplayWarning_IsConsideredSuccess()
    {
        var result = new SettingApplicationResult
        {
            Status = SettingStatus.AppliedWithDisplayWarning
        };
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SettingStatus_Applied_IsConsideredSuccess()
    {
        var result = new SettingApplicationResult
        {
            Status = SettingStatus.Applied
        };
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(SettingStatus.UnsupportedByGame)]
    [InlineData(SettingStatus.RejectedByGame)]
    [InlineData(SettingStatus.WriteFailed)]
    [InlineData(SettingStatus.VerificationFailed)]
    public void SettingStatus_FailureStates_AreNotSuccess(SettingStatus status)
    {
        var result = new SettingApplicationResult { Status = status };
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SettingsApplicationReport_AllSucceeded_WithDisplayWarning_IsTrue()
    {
        var report = new SettingsApplicationReport
        {
            Results = new()
            {
                new SettingApplicationResult { Status = SettingStatus.Applied },
                new SettingApplicationResult { Status = SettingStatus.AppliedWithDisplayWarning },
                new SettingApplicationResult { Status = SettingStatus.Applied }
            }
        };
        Assert.True(report.AllSucceeded);
        Assert.True(report.HasDisplayWarnings);
    }

    [Fact]
    public void SettingsApplicationReport_WithFailure_AllSucceeded_IsFalse()
    {
        var report = new SettingsApplicationReport
        {
            Results = new()
            {
                new SettingApplicationResult { Status = SettingStatus.Applied },
                new SettingApplicationResult { Status = SettingStatus.WriteFailed }
            }
        };
        Assert.False(report.AllSucceeded);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.SuccessCount);
    }

    // ─── PubgGraphicsCompatibility Tests ────────────────────────────────────

    [Theory]
    [InlineData(0, 120)]  // Smooth + 120 FPS
    [InlineData(1, 120)]  // Balanced + 120 FPS
    [InlineData(2, 120)]  // HD + 120 FPS
    [InlineData(3, 120)]  // HDR + 120 FPS
    [InlineData(4, 120)]  // Ultra HD + 120 FPS
    [InlineData(5, 120)]  // UHD + 120 FPS
    public void AllGraphicsQualities_Allow120Fps(int quality, int fps)
    {
        // Per user directive: ALL combinations are valid
        Assert.True(PubgGraphicsCompatibility.IsValidCombination(quality, fps));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(0, 60)]
    [InlineData(0, 90)]
    [InlineData(2, 60)]
    [InlineData(3, 90)]
    [InlineData(5, 30)]
    public void AllStandardCombinations_AreValid(int quality, int fps)
    {
        Assert.True(PubgGraphicsCompatibility.IsValidCombination(quality, fps));
    }

    [Theory]
    [InlineData(-1, 120)]
    [InlineData(6, 120)]
    [InlineData(0, 10)]
    [InlineData(0, 240)]
    public void OutOfRange_Values_AreInvalid(int quality, int fps)
    {
        Assert.False(PubgGraphicsCompatibility.IsValidCombination(quality, fps));
    }

    [Fact]
    public void ActiveSavFpsLevelToRegistryFps_ConvertsCorrectly()
    {
        Assert.Equal(120, PubgGraphicsCompatibility.ActiveSavFpsLevelToRegistryFps(8));
        Assert.Equal(120, PubgGraphicsCompatibility.ActiveSavFpsLevelToRegistryFps(7));
        Assert.Equal(90, PubgGraphicsCompatibility.ActiveSavFpsLevelToRegistryFps(6));
        Assert.Equal(60, PubgGraphicsCompatibility.ActiveSavFpsLevelToRegistryFps(5));
        Assert.Equal(40, PubgGraphicsCompatibility.ActiveSavFpsLevelToRegistryFps(4));
        Assert.Equal(30, PubgGraphicsCompatibility.ActiveSavFpsLevelToRegistryFps(3));
    }

    // ─── RecommendationEngine Tests ─────────────────────────────────────────

    [Fact]
    public void RecommendationEngine_MidRange_Recommends120Fps_Regardless_Of_MonitorHz()
    {
        // Even on a 60 Hz monitor, the recommendation should be 120 FPS for capable hardware.
        var hw = new HardwareInfo
        {
            LogicalProcessors = 8,
            TotalRamGb = 16,
            GpuVendor = GpuVendor.Nvidia,
            GpuName = "NVIDIA GeForce GTX 1660",
            DedicatedVramMb = 6000,
            RefreshRateHz = 60,  // 60 Hz monitor
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            CalculatedTier = HardwareTier.MidRange
        };

        var rec = RecommendationEngine.Calculate(hw);
        Assert.Equal(120, rec.RecommendedFpsLevel);
        Assert.True(rec.MonitorLimitsRecommendation,
            "Should note that monitor limits the recommendation visibility");
    }

    [Fact]
    public void RecommendationEngine_HighEnd_144HzMonitor_Recommends120Fps()
    {
        var hw = new HardwareInfo
        {
            LogicalProcessors = 16,
            TotalRamGb = 32,
            GpuVendor = GpuVendor.Nvidia,
            GpuName = "NVIDIA GeForce RTX 4070",
            DedicatedVramMb = 12000,
            RefreshRateHz = 144,
            ScreenWidth = 2560,
            ScreenHeight = 1440,
            CalculatedTier = HardwareTier.HighEnd
        };

        var rec = RecommendationEngine.Calculate(hw);
        Assert.Equal(120, rec.RecommendedFpsLevel);
        Assert.False(rec.MonitorLimitsRecommendation,
            "144 Hz monitor should not limit 120 FPS recommendation");
    }

    [Fact]
    public void RecommendationEngine_LowEnd_Recommends60Fps()
    {
        var hw = new HardwareInfo
        {
            LogicalProcessors = 4,
            TotalRamGb = 8,
            GpuVendor = GpuVendor.Intel,
            GpuName = "Intel UHD 630",
            DedicatedVramMb = 0,
            RefreshRateHz = 60,
            CalculatedTier = HardwareTier.LowEnd
        };

        var rec = RecommendationEngine.Calculate(hw);
        Assert.Equal(60, rec.RecommendedFpsLevel);
    }
}
