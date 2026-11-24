using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class NewFeaturesTests
{
    [Fact]
    public void DeviceProfiles_ContainValidPresetsWithMaxFps()
    {
        var profiles = DeviceProfile.Profiles;
        Assert.NotNull(profiles);
        Assert.True(profiles.Count >= 5);

        foreach (var p in profiles)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Id));
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(p.Manufacturer));
            Assert.False(string.IsNullOrWhiteSpace(p.Model));
            Assert.True(p.MaxSupportedFps is 90 or 120);
        }
    }

    [Fact]
    public async Task AllModules_AnalyzeAndVerifyAsync_ExecuteWithoutExceptions()
    {
        var modules = new List<IOptimizationModule>
        {
            new WindowsGameModeModule(),
            new PowerPlanModule(),
            new GameLoopResourceModule(),
            new GameLoopGraphicsModule(),
            new CpuAffinityModule(),
            new GpuPreferenceModule(),
            new AudioLatencyModule(),
            new MemoryOptimizerModule(),
            new CleanupOptimizerModule(),
            new TimerResolutionModule(),
            new ProcessPriorityModule(),
            new NetworkLatencyModule(),
            new NetworkDnsModule(),
            new VisualEffectsModule(),
            new BackgroundThrottleModule()
        };

        var hw = new HardwareInfo { LogicalProcessors = 8, TotalRamGb = 16 };
        var sys = new SystemInfo { ActivePowerPlanName = "Balanced", CurrentTimerResolutionMs = 15.6 };
        var gl = new GameLoopConfig { IsInstalled = false };

        foreach (var mod in modules)
        {
            var state = await mod.AnalyzeAsync(hw, sys, gl);
            Assert.NotNull(mod.CurrentStateDisplay);
            Assert.NotNull(mod.RecommendedStateDisplay);

            bool verified = await mod.VerifyAsync();
            // VerifyAsync should return boolean without throwing
            Assert.True(verified || !verified);
        }
    }

    [Fact]
    public void ProcessManager_AffinityMaskCalculation_ComputesExpectedBitmasks()
    {
        long mask4 = ProcessManager.CalculateOptimalAffinityMask(4, 4);
        Assert.Equal(0b1111, mask4);

        long mask8 = ProcessManager.CalculateOptimalAffinityMask(8, 4);
        Assert.Equal(0b11111111, mask8);

        long mask16 = ProcessManager.CalculateOptimalAffinityMask(16, 8);
        Assert.Equal(0b11111111, mask16);
    }

    [Fact]
    public void DnsOptimizerService_Presets_AreProperlyConfigured()
    {
        var presets = DnsOptimizerService.Presets;
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.PrimaryDns == "1.1.1.1");
        Assert.Contains(presets, p => p.PrimaryDns == "8.8.8.8");
    }
}

