using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class OpenGLOptimizationTests
{
    [Fact]
    public void OpenGLShaderCacheModule_ImplementsContractCorrectly()
    {
        // Arrange
        var module = new OpenGLShaderCacheModule();

        // Assert
        Assert.Equal("win_opengl_shader_cache_quota", module.Id);
        Assert.Equal("OpenGL & Vulkan Hardware Shader Cache Optimization", module.Title);
        Assert.Equal(OptimizationCategory.GraphicsQuality, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.False(module.RequiresAdmin);
        Assert.False(string.IsNullOrWhiteSpace(module.Description));
        Assert.False(string.IsNullOrWhiteSpace(module.TechnicalRationale));
        Assert.NotNull(module.CurrentStateDisplay);
        Assert.NotNull(module.RecommendedStateDisplay);
    }

    [Fact]
    public async Task OpenGLShaderCacheModule_AnalyzeAsync_IdentifiesVendorState()
    {
        // Arrange
        var module = new OpenGLShaderCacheModule();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig { IsInstalled = true, LocalShaderCacheEnabled = true, EnableGlesv3 = true };

        // 1. NVIDIA hardware
        var nvHw = new HardwareInfo { GpuVendor = GpuVendor.Nvidia, GpuName = "NVIDIA GeForce RTX 4070" };
        var nvState = await module.AnalyzeAsync(nvHw, sys, gl);
        Assert.True(nvState == OptimizationState.Optimized || nvState == OptimizationState.Recommended);
        Assert.Contains("NVIDIA", module.CurrentStateDisplay);

        // 2. AMD hardware
        var amdHw = new HardwareInfo { GpuVendor = GpuVendor.Amd, GpuName = "AMD Radeon RX 6800" };
        var amdState = await module.AnalyzeAsync(amdHw, sys, gl);
        Assert.True(amdState == OptimizationState.Optimized || amdState == OptimizationState.Recommended);
        Assert.Contains("AMD", module.CurrentStateDisplay);

        // 3. Intel hardware
        var intelHw = new HardwareInfo { GpuVendor = GpuVendor.Intel, GpuName = "Intel Iris Xe Graphics" };
        var intelState = await module.AnalyzeAsync(intelHw, sys, gl);
        Assert.True(intelState == OptimizationState.Optimized || intelState == OptimizationState.Recommended);
        Assert.Contains("Intel", module.CurrentStateDisplay);
    }

    [Fact]
    public void GameLoopConfig_ActiveRenderer_MapsDirectXAndOpenGLProperly()
    {
        var configDx = new GameLoopConfig { ForceDirectX = true };
        Assert.Equal(GraphicsRenderer.DirectXPlus, configDx.ActiveRenderer);

        var configGl = new GameLoopConfig { ForceDirectX = false };
        Assert.Equal(GraphicsRenderer.OpenGLPlus, configGl.ActiveRenderer);
    }

    [Fact]
    public async Task GameLoopGraphicsModule_Analyze_RecommendsCorrectRenderer()
    {
        // Arrange
        var module = new GameLoopGraphicsModule();
        var sys = new SystemInfo();

        // Case 1: Intel iGPU where OpenGL+ is recommended, but GameLoop is currently set to DirectX+
        var intelHw = new HardwareInfo { GpuVendor = GpuVendor.Intel, GpuName = "Intel UHD Graphics 630" };
        var glDx = new GameLoopConfig { IsInstalled = true, ForceDirectX = true, LocalShaderCacheEnabled = true, ShaderCacheEnabled = true, VSyncEnabled = false };
        
        var stateIntel = await module.AnalyzeAsync(intelHw, sys, glDx);
        Assert.Equal(OptimizationState.Recommended, stateIntel);
        Assert.False(module.IsOptimized); // Mismatch: OpenGL+ is recommended, but DirectX+ is active
        Assert.Contains("OpenGL+", module.RecommendedStateDisplay);

        // Case 2: Intel iGPU where OpenGL+ is configured and matches recommendation
        var glOgl = new GameLoopConfig { IsInstalled = true, ForceDirectX = false, LocalShaderCacheEnabled = true, ShaderCacheEnabled = true, VSyncEnabled = false };
        var stateIntelMatch = await module.AnalyzeAsync(intelHw, sys, glOgl);
        Assert.Equal(OptimizationState.Optimized, stateIntelMatch);
        Assert.True(module.IsOptimized);

        // Case 3: Dedicated NVIDIA GPU where DirectX+ is configured and matches recommendation
        var nvHw = new HardwareInfo { GpuVendor = GpuVendor.Nvidia, GpuName = "NVIDIA GeForce RTX 3080", DedicatedVramMb = 10240 };
        var stateNvMatch = await module.AnalyzeAsync(nvHw, sys, glDx);
        Assert.Equal(OptimizationState.Optimized, stateNvMatch);
        Assert.True(module.IsOptimized);
        Assert.Contains("DirectX+", module.RecommendedStateDisplay);
    }
}
