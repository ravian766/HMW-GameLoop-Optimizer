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
            new MmcssGamingPriorityModule(),
            new DisableGameDvrModule(),
            new PowerPlanModule(),
            new GameLoopResourceModule(),
            new GameLoopGraphicsModule(),
            new GameLoopPUBGConfigModule(),
            new IfeoProcessPriorityModule(),
            new AdbGpuAccelerationModule(),
            new AdbAnimationLatencyModule(),
            new AdbInputPollingModule(),
            new Adb120FpsUnlockModule(),
            new AdbDexCompilationModule(),
            new AdbVmHeapTuningModule(),
            new AdbLogcatSuppressModule(),
            new AdbBackgroundDozeModule(),
            new AdbNetworkDnsModule(),
            new AdbAudioLatencyModule(),
            new CpuAffinityModule(),
            new GpuPreferenceModule(),
            new GpuScalingModule(),
            new GpuTdrDelayModule(),
            new DirectXShaderCacheModule(),
            new OpenGLShaderCacheModule(),
            new AudioLatencyModule(),
            new AudioFootstepClarifierModule(),
            new MemoryOptimizerModule(),
            new CleanupOptimizerModule(),
            new TimerResolutionModule(),
            new ProcessPriorityModule(),
            new NetworkLatencyModule(),
            new NetworkQoSModule(),
            new NetworkDnsModule(),
            new VisualEffectsModule(),
            new BackgroundThrottleModule()
        };

        // Assert
        Assert.Equal(35, modules.Count);

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
