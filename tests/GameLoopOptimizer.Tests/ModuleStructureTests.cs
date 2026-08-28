using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class ModuleStructureTests
{
    [Fact]
    public void AllModules_HaveUniqueIdsAndDescriptions()
    {
        // Arrange
        var modules = new List<IOptimizationModule>
        {
            new WindowsGameModeModule(),
            new PowerPlanModule(),
            new GameLoopResourceModule(),
            new GameLoopGraphicsModule(),
            new GameLoopPUBGConfigModule(),
            new AdbGpuAccelerationModule(),
            new AdbAnimationLatencyModule(),
            new AdbVmHeapTuningModule(),
            new AdbLogcatSuppressModule(),
            new AdbBackgroundDozeModule(),
            new CpuAffinityModule(),
            new GpuPreferenceModule(),
            new AudioLatencyModule(),
            new AudioFootstepClarifierModule(),
            new MemoryOptimizerModule(),
            new CleanupOptimizerModule(),
            new TimerResolutionModule(),
            new ProcessPriorityModule(),
            new NetworkLatencyModule(),
            new NetworkDnsModule(),
            new VisualEffectsModule(),
            new BackgroundThrottleModule()
        };

        // Assert
        Assert.Equal(22, modules.Count);

        var ids = modules.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count()); // All IDs unique

        foreach (var mod in modules)
        {
            Assert.False(string.IsNullOrWhiteSpace(mod.Id));
            Assert.False(string.IsNullOrWhiteSpace(mod.Title));
            Assert.False(string.IsNullOrWhiteSpace(mod.Description));
            Assert.False(string.IsNullOrWhiteSpace(mod.TechnicalRationale));
            Assert.NotNull(mod.CurrentStateDisplay);
            Assert.NotNull(mod.RecommendedStateDisplay);
        }
    }
}
