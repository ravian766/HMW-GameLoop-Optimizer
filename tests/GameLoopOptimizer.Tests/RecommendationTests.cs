using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class RecommendationTests
{
    [Fact]
    public void Calculate_MidRangeRig_CalculatesOptimalAllocations()
    {
        // Arrange: Intel i3-12100F (4C/8T), AMD RX 6600 (8GB VRAM), 16GB RAM
        var hw = new HardwareInfo
        {
            CpuName = "12th Gen Intel Core i3-12100F",
            PhysicalCores = 4,
            LogicalProcessors = 8,
            TotalRamGb = 16.0,
            GpuName = "AMD Radeon RX 6600",
            GpuVendor = GpuVendor.Amd,
            DedicatedVramMb = 8192,
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            RefreshRateHz = 144
        };
        hw.CalculatedTier = HardwareDetector.CalculateTier(hw);

        // Act
        var rec = RecommendationEngine.Calculate(hw);

        // Assert
        Assert.Equal(4, rec.RecommendedCpuCores); // 4 cores for emulator, 4 threads for Windows
        Assert.Equal(8192, rec.RecommendedRamMb); // 8GB for GameLoop, 8GB for host
        Assert.True(rec.RecommendedForceDirectX);
        Assert.True(rec.RecommendedShaderCache);
        Assert.False(rec.RecommendedVSync);
        Assert.Equal(120, rec.RecommendedFpsLevel);
        Assert.Equal(1920, rec.RecommendedResWidth);
        Assert.Equal(1080, rec.RecommendedResHeight);
    }

    [Fact]
    public void Calculate_LowEndRig_CapsAllocationsToAvoidStarvingHost()
    {
        // Arrange: 2C/4T CPU, 8GB RAM, Integrated Graphics
        var hw = new HardwareInfo
        {
            CpuName = "Intel Core i3-7100",
            PhysicalCores = 2,
            LogicalProcessors = 4,
            TotalRamGb = 8.0,
            GpuName = "Intel HD Graphics 630",
            GpuVendor = GpuVendor.Intel,
            DedicatedVramMb = 128,
            ScreenWidth = 1366,
            ScreenHeight = 768,
            RefreshRateHz = 60
        };
        hw.CalculatedTier = HardwareDetector.CalculateTier(hw);

        // Act
        var rec = RecommendationEngine.Calculate(hw);

        // Assert
        Assert.Equal(2, rec.RecommendedCpuCores); // Never allocate 4 cores on 4-thread CPU
        Assert.Equal(4096, rec.RecommendedRamMb); // Leaves 4GB for Windows
        Assert.Equal(60, rec.RecommendedFpsLevel);
        Assert.Equal(1280, rec.RecommendedResWidth);
        Assert.Equal(720, rec.RecommendedResHeight);
        Assert.Equal(HardwareTier.LowEnd, hw.CalculatedTier);
    }

    [Fact]
    public void Calculate_HighEndRig_CapsAtOptimalAndroidSweetSpot()
    {
        // Arrange: 8C/16T CPU, 32GB RAM, RTX 4080
        var hw = new HardwareInfo
        {
            CpuName = "AMD Ryzen 7 7800X3D",
            PhysicalCores = 8,
            LogicalProcessors = 16,
            TotalRamGb = 32.0,
            GpuName = "NVIDIA GeForce RTX 4080",
            GpuVendor = GpuVendor.Nvidia,
            DedicatedVramMb = 16384,
            ScreenWidth = 2560,
            ScreenHeight = 1440,
            RefreshRateHz = 240
        };
        hw.CalculatedTier = HardwareDetector.CalculateTier(hw);

        // Act
        var rec = RecommendationEngine.Calculate(hw);

        // Assert
        Assert.Equal(4, rec.RecommendedCpuCores); // Prevents scheduler overhead beyond 4-6 cores
        Assert.Equal(8192, rec.RecommendedRamMb); // 8GB maximum Android memory footprint
        Assert.Equal(120, rec.RecommendedFpsLevel);
        Assert.Equal(2560, rec.RecommendedResWidth);
        Assert.Equal(1440, rec.RecommendedResHeight);
        Assert.Equal(HardwareTier.HighEnd, hw.CalculatedTier);
    }
}
