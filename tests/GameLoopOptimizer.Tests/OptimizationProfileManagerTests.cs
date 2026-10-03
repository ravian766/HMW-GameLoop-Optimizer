using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class OptimizationProfileManagerTests
{
    private class DummyModule : IOptimizationModule
    {
        public string Id { get; set; } = "TestModule";
        public string Title { get; set; } = "Test Module";
        public string Description { get; set; } = "Test Description";
        public string TechnicalRationale { get; set; } = "Technical test rationale";
        public OptimizationCategory Category { get; set; } = OptimizationCategory.WindowsConfig;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Safe;
        public bool RequiresAdmin { get; set; } = false;
        public string CurrentStateDisplay { get; set; } = "Default";
        public string RecommendedStateDisplay { get; set; } = "Optimized";
        public bool IsOptimized { get; set; } = false;
        public OptimizationState State { get; set; } = OptimizationState.NotOptimized;

        public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl) =>
            Task.FromResult(State);

        public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl) =>
            Task.FromResult(new OptimizationResult { Success = true });

        public Task<OptimizationResult> RollbackAsync(BackupEntry? backup) =>
            Task.FromResult(new OptimizationResult { Success = true });

        public Task<bool> VerifyAsync() => Task.FromResult(true);
    }

    [Fact]
    public void RecommendProfile_LaptopOnBattery_ReturnsLaptopBattery()
    {
        var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 16 };
        var sys = new SystemInfo { PowerSource = PowerSourceState.Battery };

        var profile = OptimizationProfileManager.RecommendProfile(hw, sys);

        Assert.Equal(OptimizationProfile.LaptopBattery, profile);
    }

    [Fact]
    public void RecommendProfile_LowEndHardware_ReturnsLowEndPC()
    {
        var hw = new HardwareInfo
        {
            LogicalProcessors = 4,
            TotalRamGb = 8,
            DedicatedVramMb = 0,
            GpuVendor = GpuVendor.Intel,
            CalculatedTier = HardwareTier.LowEnd
        };
        var sys = new SystemInfo { PowerSource = PowerSourceState.AcPower };

        var profile = OptimizationProfileManager.RecommendProfile(hw, sys);

        Assert.Equal(OptimizationProfile.LowEndPC, profile);
    }

    [Fact]
    public void RecommendProfile_HighEndHardware_ReturnsCompetitiveFps()
    {
        var hw = new HardwareInfo
        {
            LogicalProcessors = 16,
            TotalRamGb = 32,
            DedicatedVramMb = 12288,
            GpuVendor = GpuVendor.Nvidia,
            CalculatedTier = HardwareTier.HighEnd
        };
        var sys = new SystemInfo { PowerSource = PowerSourceState.AcPower };

        var profile = OptimizationProfileManager.RecommendProfile(hw, sys);

        Assert.Equal(OptimizationProfile.CompetitiveFps, profile);
    }

    [Fact]
    public void ShouldModuleBeSelected_NotRecommendedRisk_IsNeverSelected()
    {
        var mod = new DummyModule { RiskLevel = RiskLevel.NotRecommended };
        var hw = new HardwareInfo();
        var sys = new SystemInfo();

        bool selected = OptimizationProfileManager.ShouldModuleBeSelectedForProfile(mod, OptimizationProfile.MaximumFps, hw, sys);

        Assert.False(selected);
    }

    [Fact]
    public void ShouldModuleBeSelected_LaptopBattery_ExcludesTimerAndPowerPlan()
    {
        var timerMod = new DummyModule { Id = "SystemTimerResolution", RiskLevel = RiskLevel.Safe };
        var powerMod = new DummyModule { Id = "UltimatePowerPlan", RiskLevel = RiskLevel.Safe };
        var safeMod = new DummyModule { Id = "ShaderCacheCleanup", RiskLevel = RiskLevel.Safe };
        var hw = new HardwareInfo();
        var sys = new SystemInfo { PowerSource = PowerSourceState.Battery };

        Assert.False(OptimizationProfileManager.ShouldModuleBeSelectedForProfile(timerMod, OptimizationProfile.LaptopBattery, hw, sys));
        Assert.False(OptimizationProfileManager.ShouldModuleBeSelectedForProfile(powerMod, OptimizationProfile.LaptopBattery, hw, sys));
        Assert.True(OptimizationProfileManager.ShouldModuleBeSelectedForProfile(safeMod, OptimizationProfile.LaptopBattery, hw, sys));
    }

    [Fact]
    public async Task ExportAndImportProfile_RoundTripSucceeds()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_profile_{Guid.NewGuid():N}.json");
        try
        {
            var hw = new HardwareInfo { DedicatedVramMb = 8192, TotalRamGb = 16 };
            var modules = new List<IOptimizationModule>
            {
                new DummyModule { Id = "Mod1", IsOptimized = true },
                new DummyModule { Id = "Mod2", IsOptimized = false }
            };

            bool exportOk = await OptimizationProfileManager.ExportProfileAsync(tempPath, OptimizationProfile.CompetitiveFps, modules, hw);
            Assert.True(exportOk);
            Assert.True(File.Exists(tempPath));

            var imported = await OptimizationProfileManager.ImportProfileAsync(tempPath);
            Assert.NotNull(imported);
            Assert.Equal(OptimizationProfile.CompetitiveFps.ToString(), imported!.ProfileName);
            Assert.Contains("Mod1", imported.SelectedModuleIds);
            Assert.DoesNotContain("Mod2", imported.SelectedModuleIds);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
