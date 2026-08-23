using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class ScoringTests
{
    [Fact]
    public void CalculateScore_OptimizedSystem_ReturnsHighScore()
    {
        // Arrange
        var hw = new HardwareInfo { LogicalProcessors = 8, TotalRamGb = 16, FreeDiskSpaceGb = 100 };
        var rec = RecommendationEngine.Calculate(hw);

        var sys = new SystemInfo
        {
            IsGameModeEnabled = true,
            ActivePowerPlanName = "High Performance",
            CurrentTimerResolutionMs = 0.5,
            AreVisualEffectsOptimized = true,
            HighCpuProcessesCount = 0
        };

        var gl = new GameLoopConfig
        {
            IsInstalled = true,
            VmCpuCount = rec.RecommendedCpuCores,
            VmMemorySizeInMb = rec.RecommendedRamMb,
            ForceDirectX = true,
            LocalShaderCacheEnabled = true,
            ShaderCacheEnabled = true
        };

        // Act
        var score = ScoringEngine.CalculateScore(hw, sys, gl, rec);

        // Assert
        Assert.True(score.TotalScore >= 95, $"Expected score >= 95, got {score.TotalScore}");
        Assert.Equal(20, score.WindowsConfig.Score);
        Assert.Equal(15, score.PowerDelivery.Score);
        Assert.Equal(25, score.GameLoopConfig.Score);
        Assert.Equal(20, score.GraphicsSettings.Score);
    }

    [Fact]
    public void CalculateScore_UnoptimizedSystem_ReturnsExplanations()
    {
        // Arrange
        var hw = new HardwareInfo { LogicalProcessors = 8, TotalRamGb = 16, FreeDiskSpaceGb = 50 };
        var rec = RecommendationEngine.Calculate(hw);

        var sys = new SystemInfo
        {
            IsGameModeEnabled = false,
            ActivePowerPlanName = "Power Saver",
            CurrentTimerResolutionMs = 15.6,
            AreVisualEffectsOptimized = false,
            HighCpuProcessesCount = 5
        };

        var gl = new GameLoopConfig
        {
            IsInstalled = true,
            VmCpuCount = 1, // Bad allocation
            VmMemorySizeInMb = 1024,
            ForceDirectX = false,
            LocalShaderCacheEnabled = false,
            ShaderCacheEnabled = false
        };

        // Act
        var score = ScoringEngine.CalculateScore(hw, sys, gl, rec);

        // Assert
        Assert.True(score.TotalScore < 60, $"Expected score < 60, got {score.TotalScore}");
        Assert.NotEmpty(score.HonestExplanations);
        Assert.Contains(score.HonestExplanations, e => e.Contains("Game Mode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(score.HonestExplanations, e => e.Contains("Shader cache", StringComparison.OrdinalIgnoreCase));
    }
}
