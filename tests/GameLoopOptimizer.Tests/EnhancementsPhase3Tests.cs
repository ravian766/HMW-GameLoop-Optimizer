using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class EnhancementsPhase3Tests
{
    [Fact]
    public async Task GpuDriverProfileModule_Metadata_And_Analyze_ReturnsExpectedState()
    {
        var mod = new GpuDriverProfileModule();
        Assert.Equal("gpu_driver_profile_tuning", mod.Id);
        Assert.Equal(OptimizationCategory.GraphicsQuality, mod.Category);
        Assert.True(mod.RequiresAdmin);

        var hw = new HardwareInfo { GpuName = "NVIDIA GeForce RTX 4070", TotalRamGb = 16 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig { IsInstalled = true };

        var state = await mod.AnalyzeAsync(hw, sys, gl);
        Assert.True(state == OptimizationState.Recommended || state == OptimizationState.Optimized);
        Assert.False(string.IsNullOrEmpty(mod.CurrentStateDisplay));
    }

    [Fact]
    public async Task StandbyListCleanerModule_Metadata_Is_Configured()
    {
        var mod = new StandbyListCleanerModule();
        Assert.Equal("standby_list_cleaner", mod.Id);
        Assert.Equal(OptimizationCategory.MemoryStorage, mod.Category);
        Assert.True(mod.RequiresAdmin);

        var hw = new HardwareInfo { TotalRamGb = 16 };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var state = await mod.AnalyzeAsync(hw, sys, gl);
        Assert.Equal(OptimizationState.Recommended, state);
    }

    [Fact]
    public void StandbyListCleanerService_TrimBackgroundWorkingSets_RunsSafely()
    {
        // Safe cross-process memory trim test
        int count = StandbyListCleanerService.TrimBackgroundWorkingSets();
        Assert.True(count >= 0);
    }

    [Fact]
    public void ResolutionKeymapService_CalibrateCoordinateForHudMode_HandlesVehicleAndSwimmingModes()
    {
        int targetWidth = 1440;
        int targetHeight = 1080;

        // Standard Left Joystick
        var (onFootX, onFootY) = ResolutionKeymapService.CalibrateCoordinateForHudMode(0.20, 0.75, targetWidth, targetHeight, HudCalibrationMode.GeneralOnFoot);
        Assert.True(onFootX > 0.20); // Scaled outward for 4:3 stretched
        Assert.Equal(0.75, onFootY);

        // Vehicle Driving steering
        var (vehX, vehY) = ResolutionKeymapService.CalibrateCoordinateForHudMode(0.25, 0.70, targetWidth, targetHeight, HudCalibrationMode.VehicleDriving);
        Assert.True(vehX > 0.25);
        Assert.Equal(0.70, vehY);

        // Swimming & Parachute
        var (swimX, swimY) = ResolutionKeymapService.CalibrateCoordinateForHudMode(0.80, 0.50, targetWidth, targetHeight, HudCalibrationMode.SwimmingAndParachute);
        Assert.True(swimX > 0.50 && swimX < 1.0);
    }

    [Fact]
    public void ResolutionKeymapService_CalculateCompensatedWasdOffset_PreservesRatio()
    {
        double baseOffset = 0.08;
        double stretchedOffset = ResolutionKeymapService.CalculateCompensatedWasdOffset(baseOffset, 1440, 1080);
        // (1920/1080) / (1440/1080) = 1.3333, offset should be ~0.1066
        Assert.True(stretchedOffset > baseOffset);
        Assert.InRange(stretchedOffset, 0.04, 0.18);
    }

    [Fact]
    public void ScoringEngine_Detects_SingleVsDualChannelRam()
    {
        var hwDual = new HardwareInfo
        {
            TotalRamGb = 16,
            RamStickCount = 2,
            LogicalProcessors = 8,
            FreeDiskSpaceGb = 50
        };
        var hwSingle = new HardwareInfo
        {
            TotalRamGb = 16,
            RamStickCount = 1,
            LogicalProcessors = 8,
            FreeDiskSpaceGb = 50
        };

        var sys = new SystemInfo { IsGameModeEnabled = true, CurrentTimerResolutionMs = 0.5, ActivePowerPlanName = "High Performance" };
        var gl = new GameLoopConfig { IsInstalled = false };
        var rec = RecommendationEngine.Calculate(hwDual);

        var scoreDual = ScoringEngine.CalculateScore(hwDual, sys, gl, rec);
        var scoreSingle = ScoringEngine.CalculateScore(hwSingle, sys, gl, rec);

        Assert.True(scoreDual.MemoryStorage.Score > scoreSingle.MemoryStorage.Score);
        Assert.Contains("Dual-Channel", scoreDual.MemoryStorage.Details);
        Assert.Contains("Single-Channel", scoreSingle.MemoryStorage.Details);
    }
}
