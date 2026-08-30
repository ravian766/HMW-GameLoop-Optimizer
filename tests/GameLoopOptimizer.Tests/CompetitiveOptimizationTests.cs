using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class CompetitiveOptimizationTests
{
    [Fact]
    public void DirectXShaderCacheModule_HasCorrectMetadata()
    {
        var module = new DirectXShaderCacheModule();
        Assert.Equal("win_directx_shader_cache_quota", module.Id);
        Assert.Equal(OptimizationCategory.GraphicsQuality, module.Category);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.True(module.RequiresAdmin);
        Assert.Contains("10 GB", module.Title);
        Assert.Contains("10240 MB", module.RecommendedStateDisplay);
    }

    [Fact]
    public async Task DirectXShaderCacheModule_AnalyzeAsync_ExecutesGracefully()
    {
        var module = new DirectXShaderCacheModule();
        var hw = new HardwareInfo { LogicalProcessors = 8, TotalRamGb = 16 };
        var sys = new SystemInfo { ActivePowerPlanName = "Balanced" };
        var gl = new GameLoopConfig { IsInstalled = false };

        var state = await module.AnalyzeAsync(hw, sys, gl);
        Assert.NotNull(module.CurrentStateDisplay);
        Assert.True(state is OptimizationState.Optimized or OptimizationState.Recommended);
    }

    [Fact]
    public void ProcessManager_IoAndMemoryPriority_ExecutesWithoutCrashing()
    {
        int count = ProcessManager.SetGameLoopIoAndMemoryPriority(ioPriority: 3, memoryPriority: 5);
        Assert.True(count >= 0);
    }

    [Fact]
    public void ProcessManager_GameLoopProcessNames_ContainsAllCoreAndEnEmulators()
    {
        var allNames = ProcessManager.AllGameLoopProcessNames;
        Assert.Contains("AndroidEmulator", allNames);
        Assert.Contains("AndroidEmulatorEn", allNames);
        Assert.Contains("AndroidEmulatorEx", allNames);
        Assert.Contains("aow_exe", allNames);
        Assert.Contains("AppMarket", allNames);

        var engineNames = ProcessManager.EmulatorEngineProcessNames;
        Assert.Contains("AndroidEmulator", engineNames);
        Assert.Contains("AndroidEmulatorEn", engineNames);
        Assert.Contains("AndroidEmulatorEx", engineNames);
        Assert.Contains("aow_exe", engineNames);
        Assert.DoesNotContain("AppMarket", engineNames); // AppMarket launcher excluded from resource-heavy engine optimizations
    }

    [Fact]
    public async Task MatchReadinessService_EvaluatesReport_Accurately()
    {
        var hw = new HardwareInfo { LogicalProcessors = 16, TotalRamGb = 32 };
        var sys = new SystemInfo
        {
            CurrentTimerResolutionMs = 0.5,
            ActivePowerPlanName = "Ultimate Performance"
        };
        var gl = new GameLoopConfig { IsInstalled = true };

        var report = await MatchReadinessService.EvaluateReadinessAsync(hw, sys, gl);
        Assert.NotNull(report);
        Assert.InRange(report.ReadinessScore, 0, 100);
        Assert.NotEmpty(report.Items);
        Assert.NotEmpty(report.BadgeText);
        Assert.Equal(5, report.Items.Count);
    }

    [Fact]
    public void HotkeyConstants_AreUniquelyDefined()
    {
        Assert.Equal(9001, HotkeyManager.HOTKEY_OVERLAY_ID);
        Assert.Equal(9002, HotkeyManager.HOTKEY_TRIM_ID);
        Assert.Equal(9003, HotkeyManager.HOTKEY_TIMER_ID);
        Assert.Equal(9004, HotkeyManager.HOTKEY_FPS_ID);
    }
}
