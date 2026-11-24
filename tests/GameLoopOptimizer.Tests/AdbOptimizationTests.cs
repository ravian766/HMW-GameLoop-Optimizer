using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class AdbOptimizationTests
{
    [Fact]
    public void AdbModules_HaveValidCategoriesAndMetadata()
    {
        var gpuMod = new AdbGpuAccelerationModule();
        var animMod = new AdbAnimationLatencyModule();
        var heapMod = new AdbVmHeapTuningModule();
        var logMod = new AdbLogcatSuppressModule();
        var dozeMod = new AdbBackgroundDozeModule();

        Assert.Equal(OptimizationCategory.GameLoopEngine, gpuMod.Category);
        Assert.Equal(OptimizationCategory.GameLoopEngine, animMod.Category);
        Assert.Equal(OptimizationCategory.MemoryStorage, heapMod.Category);
        Assert.Equal(OptimizationCategory.BackgroundProcess, logMod.Category);
        Assert.Equal(OptimizationCategory.BackgroundProcess, dozeMod.Category);

        Assert.Equal(RiskLevel.Safe, gpuMod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, animMod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, heapMod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, logMod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, dozeMod.RiskLevel);

        Assert.False(gpuMod.RequiresAdmin);
        Assert.False(animMod.RequiresAdmin);
    }

    [Fact]
    public async Task AdbModules_AnalyzeGracefully_WhenGameLoopNotInstalled()
    {
        var config = new GameLoopConfig { IsInstalled = false };
        var hw = new HardwareInfo();
        var sys = new SystemInfo();

        var gpuMod = new AdbGpuAccelerationModule();
        var animMod = new AdbAnimationLatencyModule();
        var heapMod = new AdbVmHeapTuningModule();

        var state1 = await gpuMod.AnalyzeAsync(hw, sys, config);
        var state2 = await animMod.AnalyzeAsync(hw, sys, config);
        var state3 = await heapMod.AnalyzeAsync(hw, sys, config);

        Assert.Equal(OptimizationState.NotDetected, state1);
        Assert.Equal(OptimizationState.NotDetected, state2);
        Assert.Equal(OptimizationState.NotDetected, state3);
    }

    [Fact]
    public void AdbDeviceInfo_IdentifiesEmulatorSerial()
    {
        var device1 = new AdbDeviceInfo { Serial = "127.0.0.1:5555", State = "device" };
        var device2 = new AdbDeviceInfo { Serial = "emulator-5554", State = "device" };
        var device3 = new AdbDeviceInfo { Serial = "HT456789", State = "device" };

        Assert.True(device1.IsEmulator);
        Assert.True(device2.IsEmulator);
        Assert.False(device3.IsEmulator);
    }

    [Theory]
    [InlineData("setprop: failed to set property 'ro.product.model' to 'SM-X910'")]
    [InlineData("avc: denied { set } for property=debug.sf.fps Permission denied")]
    [InlineData("setprop: could not set property 'ro.product.brand'")]
    [InlineData("Error: The system cannot find the file specified.")]
    [InlineData("Command timed out")]
    [InlineData("mount: Read-only file system")]
    public void AdbFailureDetection_FlagsRefusedPropertyWrites(string output)
    {
        // adb reports refused setprop calls on stderr without a non-zero exit code, so the old
        // `!StartsWith("Error")` check treated every one of these as a success.
        Assert.True(AdbManager.IsAdbFailure(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("120")]
    [InlineData("Success")]
    [InlineData("512m")]
    [InlineData("SIM-S918B")]
    public void AdbFailureDetection_DoesNotFlagNormalValues(string output)
    {
        Assert.False(AdbManager.IsAdbFailure(output));
    }
}
