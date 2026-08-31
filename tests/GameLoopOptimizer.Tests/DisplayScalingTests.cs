using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class DisplayScalingTests
{
    [Fact]
    public void GpuScalingModule_ImplementsContractCorrectly()
    {
        // Arrange
        var module = new GpuScalingModule();

        // Assert
        Assert.Equal("gpu_fullscreen_scaling", module.Id);
        Assert.Equal(OptimizationCategory.GraphicsQuality, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.True(module.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(module.Title));
        Assert.False(string.IsNullOrWhiteSpace(module.Description));
        Assert.False(string.IsNullOrWhiteSpace(module.TechnicalRationale));
        Assert.NotNull(module.CurrentStateDisplay);
        Assert.NotNull(module.RecommendedStateDisplay);
    }

    [Theory]
    [InlineData(GpuVendor.Nvidia, "Full-screen")]
    [InlineData(GpuVendor.Amd, "Full Panel")]
    [InlineData(GpuVendor.Intel, "Scale Full Screen")]
    public void DisplayScalingService_VendorGuide_ReturnsValidSteps(GpuVendor vendor, string expectedKeyword)
    {
        // Act
        var guide = DisplayScalingService.GetVendorStepByStepGuide(vendor);

        // Assert
        Assert.NotEmpty(guide);
        Assert.Contains(guide, step => step.Contains(expectedKeyword, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DisplayScalingService_CheckCurrentScaling_ExecutesGracefully()
    {
        // Arrange
        var hw = new HardwareInfo
        {
            GpuVendor = GpuVendor.Nvidia,
            GpuName = "NVIDIA GeForce RTX 4070"
        };

        // Act
        var status = DisplayScalingService.CheckCurrentScaling(hw);

        // Assert
        Assert.NotNull(status);
        Assert.False(string.IsNullOrWhiteSpace(status.Message));
        Assert.Equal(GpuVendor.Nvidia, status.DetectedVendor);
    }

    [Fact]
    public async Task GpuScalingModule_AnalyzeAsync_ExecutesGracefully()
    {
        // Arrange
        var module = new GpuScalingModule();
        var hw = new HardwareInfo { GpuVendor = GpuVendor.Nvidia };
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        // Act
        var state = await module.AnalyzeAsync(hw, sys, gl);

        // Assert
        Assert.True(state == OptimizationState.Optimized || state == OptimizationState.NotOptimized);
        Assert.NotNull(module.CurrentStateDisplay);
    }
}
