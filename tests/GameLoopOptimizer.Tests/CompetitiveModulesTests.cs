using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class CompetitiveModulesTests
{
    [Fact]
    public async Task MmcssGamingPriorityModule_ContractAndAnalyze_ExecutesGracefully()
    {
        // Arrange
        var module = new MmcssGamingPriorityModule();
        var hw = new HardwareInfo();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        // Assert contract
        Assert.Equal("win_mmcss_gaming_priority", module.Id);
        Assert.Equal(OptimizationCategory.WindowsConfig, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.True(module.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(module.Description));
        Assert.False(string.IsNullOrWhiteSpace(module.TechnicalRationale));

        // Act
        var state = await module.AnalyzeAsync(hw, sys, gl);

        // Assert analyze
        Assert.True(state == OptimizationState.Optimized || state == OptimizationState.Recommended);
        Assert.NotNull(module.CurrentStateDisplay);
    }

    [Fact]
    public async Task DisableGameDvrModule_ContractAndAnalyze_ExecutesGracefully()
    {
        // Arrange
        var module = new DisableGameDvrModule();
        var hw = new HardwareInfo();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        // Assert contract
        Assert.Equal("win_disable_game_dvr", module.Id);
        Assert.Equal(OptimizationCategory.WindowsConfig, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.True(module.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(module.Description));
        Assert.False(string.IsNullOrWhiteSpace(module.TechnicalRationale));

        // Act
        var state = await module.AnalyzeAsync(hw, sys, gl);

        // Assert analyze
        Assert.True(state == OptimizationState.Optimized || state == OptimizationState.Recommended);
        Assert.NotNull(module.CurrentStateDisplay);
    }

    [Fact]
    public async Task IfeoProcessPriorityModule_ContractAndAnalyze_ExecutesGracefully()
    {
        // Arrange
        var module = new IfeoProcessPriorityModule();
        var hw = new HardwareInfo();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        // Assert contract
        Assert.Equal("gl_ifeo_process_priority", module.Id);
        Assert.Equal(OptimizationCategory.GameLoopEngine, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.True(module.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(module.Description));
        Assert.False(string.IsNullOrWhiteSpace(module.TechnicalRationale));

        // Act
        var state = await module.AnalyzeAsync(hw, sys, gl);

        // Assert analyze
        Assert.True(state == OptimizationState.Optimized || state == OptimizationState.Recommended);
        Assert.NotNull(module.CurrentStateDisplay);
    }

    [Fact]
    public async Task GpuTdrDelayModule_ContractAndAnalyze_ExecutesGracefully()
    {
        // Arrange
        var module = new GpuTdrDelayModule();
        var hw = new HardwareInfo();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        // Assert contract
        Assert.Equal("gpu_tdr_delay_protection", module.Id);
        Assert.Equal(OptimizationCategory.GraphicsQuality, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.True(module.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(module.Description));
        Assert.False(string.IsNullOrWhiteSpace(module.TechnicalRationale));

        // Act
        var state = await module.AnalyzeAsync(hw, sys, gl);

        // Assert analyze
        Assert.True(state == OptimizationState.Optimized || state == OptimizationState.Recommended);
        Assert.NotNull(module.CurrentStateDisplay);
    }
}
