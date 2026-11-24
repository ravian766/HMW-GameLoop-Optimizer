using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class UltimateOptimizationEngineTests
{
    [Fact]
    public void ProcessManager_CpuAffinityProfiles_ComputeCorrectMasks()
    {
        // Intel i9-13900K (24C / 32T): 8 P-Cores with HT = 16 threads (0xFFFF)
        string i9Cpu = "13th Gen Intel(R) Core(TM) i9-13900K";
        
        long maxPerfMask = ProcessManager.CalculateAffinityMaskForProfile(
            CpuAffinityProfile.MaximumPerformance, 32, 24, i9Cpu);
        Assert.Equal(0xFFFF, maxPerfMask);

        // BalancedGaming: 16 threads base -> frees top 2 threads (threads 14 & 15) -> 0x3FFF
        long balancedMask = ProcessManager.CalculateAffinityMaskForProfile(
            CpuAffinityProfile.BalancedGaming, 32, 24, i9Cpu);
        Assert.Equal(0x3FFF, balancedMask);

        // LowLatency: Disables sibling SMT threads -> masks 0xFFFF with 0x5555... -> 0x5555
        long lowLatencyMask = ProcessManager.CalculateAffinityMaskForProfile(
            CpuAffinityProfile.LowLatency, 32, 24, i9Cpu);
        Assert.Equal(0x5555, lowLatencyMask);

        // Custom Mask: returns user-specified mask
        long customMask = ProcessManager.CalculateAffinityMaskForProfile(
            CpuAffinityProfile.Custom, 32, 24, i9Cpu, customMask: 0x00FF);
        Assert.Equal(0x00FF, customMask);
    }

    [Fact]
    public void ProcessManager_DualCcdRyzen_PinsToCcd0()
    {
        // AMD Ryzen 9 5900X (12C / 24T) -> CCD0 = 12 threads (0x0FFF)
        long ryzen5900XMask = ProcessManager.CalculateOptimalAffinityMask(24, 12, "AMD Ryzen 9 5900X 12-Core Processor");
        Assert.Equal(0x0FFF, ryzen5900XMask);

        // AMD Ryzen 9 7950X (16C / 32T) -> CCD0 = 16 threads (0xFFFF)
        long ryzen7950XMask = ProcessManager.CalculateOptimalAffinityMask(32, 16, "AMD Ryzen 9 7950X 16-Core Processor");
        Assert.Equal(0xFFFF, ryzen7950XMask);
    }

    [Fact]
    public void HardwareDetector_CalculateTier_IdentifiesEnthusiastFlagship()
    {
        var enthusiastHw = new HardwareInfo
        {
            CpuName = "Intel Core i9-14900KS",
            PhysicalCores = 24,
            LogicalProcessors = 32,
            TotalRamGb = 64.0,
            GpuName = "NVIDIA GeForce RTX 4090",
            GpuVendor = GpuVendor.Nvidia,
            DedicatedVramMb = 24576
        };

        var tier = HardwareDetector.CalculateTier(enthusiastHw);
        Assert.Equal(HardwareTier.Enthusiast, tier);
    }

    [Fact]
    public void HardwareInfo_CpuTopologySummary_FormatsAccurately()
    {
        var hybridHw = new HardwareInfo
        {
            PhysicalCores = 24,
            LogicalProcessors = 32,
            PerformanceCoresCount = 8,
            EfficientCoresCount = 16
        };
        Assert.Equal("24C / 32T (8P+16E)", hybridHw.CpuTopologySummary);

        var ryzenHw = new HardwareInfo
        {
            CpuVendor = "AMD",
            CcdCount = 2,
            PhysicalCores = 12,
            LogicalProcessors = 24
        };
        Assert.Equal("12C / 24T (Dual-CCD)", ryzenHw.CpuTopologySummary);

        var x3dHw = new HardwareInfo
        {
            CpuVendor = "AMD",
            Has3dVcache = true,
            PhysicalCores = 8,
            LogicalProcessors = 16
        };
        Assert.Equal("8C / 16T (3D V-Cache)", x3dHw.CpuTopologySummary);
    }

    [Fact]
    public async Task MemoryOptimizerModule_AnalyzeAsync_AdaptsToMemoryPressure()
    {
        var mod = new MemoryOptimizerModule();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        // 1. Normal Memory Pressure
        var hwNormal = new HardwareInfo
        {
            TotalRamGb = 32.0,
            UsedRamGb = 8.0,
            MemoryPressureStatus = "Normal"
        };
        var stateNormal = await mod.AnalyzeAsync(hwNormal, sys, gl);
        Assert.Equal(OptimizationState.Optimized, stateNormal);
        Assert.Contains("Optimal", mod.RecommendedStateDisplay);

        // 2. High Memory Pressure
        var hwHigh = new HardwareInfo
        {
            TotalRamGb = 8.0,
            UsedRamGb = 7.2,
            MemoryPressureStatus = "High"
        };
        var stateHigh = await mod.AnalyzeAsync(hwHigh, sys, gl);
        Assert.Equal(OptimizationState.Recommended, stateHigh);
        Assert.Contains("Trim", mod.RecommendedStateDisplay);
    }

    [Fact]
    public void ProcessManager_GetCoreTopologyDetails_ReturnsPerThreadMetadata()
    {
        var hw = new HardwareInfo
        {
            PhysicalCores = 8,
            LogicalProcessors = 16,
            PerformanceCoresCount = 8,
            EfficientCoresCount = 0
        };

        var details = ProcessManager.GetCoreTopologyDetails(hw, 0x00FF);
        Assert.Equal(16, details.Count);
        Assert.True(details[0].IsSelected);
        Assert.True(details[7].IsSelected);
        Assert.False(details[8].IsSelected);
        Assert.False(details[15].IsSelected);
    }
}

