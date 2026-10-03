using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class GameLoopCompatibilityAndLatencyTests
{
    [Fact]
    public void EvaluateCompatibility_SupportedVersion_SetsSupportedTier()
    {
        var config = new GameLoopConfig
        {
            IsInstalled = true,
            InstallPath = @"C:\Program Files\TxGameAssistant",
            Version = "7.1.0.1"
        };

        GameLoopCompatibilityManager.EvaluateCompatibility(config);

        Assert.Equal(GameLoopCompatibilityTier.Supported, config.CompatibilityTier);
        Assert.Contains("7.1.0.1", config.CompatibilityReason);
    }

    [Fact]
    public void EvaluateCompatibility_LegacyVersion_SetsPartiallySupported()
    {
        var config = new GameLoopConfig
        {
            IsInstalled = true,
            InstallPath = @"C:\Program Files\TxGameAssistant",
            Version = "3.2.1.0"
        };

        GameLoopCompatibilityManager.EvaluateCompatibility(config);

        Assert.Equal(GameLoopCompatibilityTier.PartiallySupported, config.CompatibilityTier);
    }

    [Fact]
    public void EvaluateCompatibility_NotInstalled_SetsUnknown()
    {
        var config = new GameLoopConfig
        {
            IsInstalled = false
        };

        GameLoopCompatibilityManager.EvaluateCompatibility(config);

        Assert.Equal(GameLoopCompatibilityTier.Unknown, config.CompatibilityTier);
    }

    [Fact]
    public void InputLatencyAudit_VsyncEnabled_AppliesLatencyPenalty()
    {
        var hw = new HardwareInfo { RefreshRateHz = 144, MaxRefreshRateHz = 144 };
        var gl = new GameLoopConfig { PubgFpsLevel = 90, VSyncEnabled = true };

        var result = InputLatencyService.Audit(hw, gl);

        Assert.True(result.IsVSyncActive);
        Assert.True(result.EstimatedInputDelayMs > 20.0);
        Assert.Contains(result.Recommendations, r => r.Contains("V-Sync"));
    }

    [Fact]
    public void InputLatencyAudit_HighRefreshNoVsync_CalculatesLowInputDelay()
    {
        var hw = new HardwareInfo { RefreshRateHz = 240, MaxRefreshRateHz = 240, CalculatedTier = HardwareTier.HighEnd };
        var gl = new GameLoopConfig { PubgFpsLevel = 120, VSyncEnabled = false };

        var result = InputLatencyService.Audit(hw, gl);

        Assert.False(result.IsVSyncActive);
        // At 120 FPS, frame delay is ~8.3ms; 240Hz scanout/2 is ~2.1ms -> ~10.4ms total
        Assert.True(result.EstimatedInputDelayMs < 15.0);
    }
}
