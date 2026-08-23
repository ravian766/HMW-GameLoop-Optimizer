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
    public async Task AudioLatencyModule_HasSafeRiskAndAppliesCleanly()
    {
        var mod = new AudioLatencyModule();
        Assert.Equal("audio-low-latency", mod.Id);
        Assert.Equal(RiskLevel.Safe, mod.RiskLevel);

        var hw = new HardwareInfo();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var state = await mod.AnalyzeAsync(hw, sys, gl);
        Assert.True(state is OptimizationState.Optimized or OptimizationState.Recommended);
    }
}
