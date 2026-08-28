using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using GameLoopOptimizer.ViewModels;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class StretchedResAndAudioTests
{
    [Theory]
    [InlineData(1440, 1080, "4:3 Stretched")]
    [InlineData(1728, 1080, "16:10 Stretched")]
    [InlineData(1080, 1080, "1:1 Box Stretched")]
    [InlineData(1280, 960, "4:3 Low-End Stretched")]
    [InlineData(1920, 1080, "16:9 Standard FHD Native")]
    [InlineData(2560, 1440, "16:9 2K QHD Native")]
    public void AspectRatioCalculator_IdentifiesCompetitivePresetsCorrectly(int width, int height, string expectedSubstr)
    {
        string desc = GameLoopViewModel.CalculateAspectRatio(width, height);
        Assert.Contains(expectedSubstr, desc);
    }

    [Fact]
    public void AudioFootstepClarifier_HasValidMetadata()
    {
        var mod = new AudioFootstepClarifierModule();
        Assert.Equal("audio_footstep_clarifier", mod.Id);
        Assert.Equal(OptimizationCategory.WindowsConfig, mod.Category);
        Assert.Equal(RiskLevel.Safe, mod.RiskLevel);
        Assert.False(mod.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(mod.Description));
        Assert.False(string.IsNullOrWhiteSpace(mod.TechnicalRationale));
    }

    [Fact]
    public async Task WatchdogService_SmartPurge_ExecutesSafely()
    {
        var watchdog = new GameLoopWatchdogService(() => new GameLoopConfig());
        int freed = await watchdog.ExecuteSmartPurgeAsync();

        Assert.True(watchdog.AutoPurgeCount >= 1);
        Assert.NotNull(watchdog.LastPurgeTime);
        Assert.Contains("Purge #1", watchdog.LastPurgeMessage);
    }
}
