using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class AdbEnhancementsTests
{
    [Fact]
    public void NewAdbModules_HaveValidCategoriesAndMetadata()
    {
        var inputMod = new AdbInputPollingModule();
        var fps120Mod = new Adb120FpsUnlockModule();
        var dexMod = new AdbDexCompilationModule();

        Assert.Equal(OptimizationCategory.GameLoopEngine, inputMod.Category);
        Assert.Equal(OptimizationCategory.GameLoopEngine, fps120Mod.Category);
        Assert.Equal(OptimizationCategory.GameLoopEngine, dexMod.Category);

        Assert.Equal(RiskLevel.Safe, inputMod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, fps120Mod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, dexMod.RiskLevel);

        Assert.False(inputMod.RequiresAdmin);
        Assert.False(fps120Mod.RequiresAdmin);
        Assert.False(dexMod.RequiresAdmin);
    }

    [Fact]
    public async Task NewAdbModules_AnalyzeGracefully_WhenGameLoopNotInstalled()
    {
        var config = new GameLoopConfig { IsInstalled = false };
        var hw = new HardwareInfo();
        var sys = new SystemInfo();

        var inputMod = new AdbInputPollingModule();
        var fps120Mod = new Adb120FpsUnlockModule();
        var dexMod = new AdbDexCompilationModule();

        var state1 = await inputMod.AnalyzeAsync(hw, sys, config);
        var state2 = await fps120Mod.AnalyzeAsync(hw, sys, config);
        var state3 = await dexMod.AnalyzeAsync(hw, sys, config);

        Assert.Equal(OptimizationState.NotDetected, state1);
        Assert.Equal(OptimizationState.NotDetected, state2);
        Assert.Equal(OptimizationState.NotDetected, state3);
    }

    [Fact]
    public void AdbTelemetryService_ParseDisplayMetrics_ExtractsPhysicalAndOverride()
    {
        string wmSize = "Physical size: 1920x1080\nOverride size: 1440x1080";
        string wmDensity = "Physical density: 320\nOverride density: 240";

        var display = AdbTelemetryService.ParseDisplayMetrics(wmSize, wmDensity);

        Assert.Equal("1920x1080", display.PhysicalResolution);
        Assert.Equal("1440x1080", display.OverrideResolution);
        Assert.Equal("1440x1080", display.EffectiveResolution);
        Assert.Equal(240, display.DensityDpi);
    }

    [Fact]
    public void AdbTelemetryService_ParseMemoryMetrics_ExtractsPssNativeDalvikGraphics()
    {
        string meminfoSample = @"
 Applications Memory Usage (in Kilobytes):
 Uptime: 12345678 Realtime: 12345678

 ** MEMINFO in pid 5678 [com.tencent.ig] **
                    Pss  Private  Private  SwapPss     Heap     Heap     Heap
                  Total    Dirty    Clean    Dirty     Size    Alloc     Free
                 ------   ------   ------   ------   ------   ------   ------
   Native Heap   204800   190000        0        0   256000   200000    56000
   Dalvik Heap   102400    95000        0        0   128000   100000    28000
      Graphics    51200    51200        0        0        0        0        0
         TOTAL   524288   400000    10000        0   384000   300000    84000
";

        var mem = AdbTelemetryService.ParseMemoryMetrics(meminfoSample);

        Assert.Equal(512.0, mem.TotalPssMb, precision: 1);
        Assert.Equal(200.0, mem.NativeHeapMb, precision: 1);
        Assert.Equal(100.0, mem.DalvikHeapMb, precision: 1);
        Assert.Equal(50.0, mem.GraphicsMb, precision: 1);
        Assert.Contains("Total: 512.0 MB", mem.SummaryDisplay);
    }

    [Fact]
    public void AdbTelemetryService_ParseFpsEstimate_ExtractsFrameCount()
    {
        string gfxSample = @"
Stats since: 123456789ns
Total frames rendered: 118
Janky frames: 2 (1.69%)
50th percentile: 8ms
90th percentile: 9ms
95th percentile: 10ms
99th percentile: 12ms
Number Missed Vsync: 0
Number High Input Latency: 0
Number Slow UI thread: 0
Number Slow bitmap uploads: 0
Number Slow issue draw commands: 0
";
        double fps = AdbTelemetryService.ParseFpsEstimate(gfxSample);
        Assert.Equal(118.0, fps);
    }

    [Fact]
    public void AdbManager_KnownGamePackages_ContainsExpectedGlobalAndRegionalGames()
    {
        var pkgs = AdbManager.KnownGamePackages;

        Assert.Contains(pkgs, p => p.PackageName == "com.tencent.ig" && p.Region == "Global");
        Assert.Contains(pkgs, p => p.PackageName == "com.pubg.imobile" && p.Region == "India");
        Assert.Contains(pkgs, p => p.PackageName == "com.pubg.krmobile" && p.Region == "Korea / Japan");
        Assert.Contains(pkgs, p => p.PackageName == "com.vng.pubgmobile" && p.Region == "Vietnam");
        Assert.Contains(pkgs, p => p.PackageName == "com.dts.freefireth");
        Assert.Contains(pkgs, p => p.PackageName == "com.activision.callofduty.shooter");
    }

    [Fact]
    public void AdbNetworkDnsAndAudioModules_HaveCorrectMetadata()
    {
        var dnsMod = new AdbNetworkDnsModule();
        var audioMod = new AdbAudioLatencyModule();

        Assert.Equal(OptimizationCategory.GameLoopEngine, dnsMod.Category);
        Assert.Equal(OptimizationCategory.GameLoopEngine, audioMod.Category);

        Assert.Equal(RiskLevel.Safe, dnsMod.RiskLevel);
        Assert.Equal(RiskLevel.Safe, audioMod.RiskLevel);

        Assert.False(dnsMod.RequiresAdmin);
        Assert.False(audioMod.RequiresAdmin);

        Assert.Contains("DNS", dnsMod.Title);
        Assert.Contains("Audio", audioMod.Title);
    }

    [Fact]
    public async Task AdbNetworkDnsAndAudioModules_AnalyzeGracefully_WhenGameLoopNotInstalled()
    {
        var config = new GameLoopConfig { IsInstalled = false };
        var hw = new HardwareInfo();
        var sys = new SystemInfo();

        var dnsMod = new AdbNetworkDnsModule();
        var audioMod = new AdbAudioLatencyModule();

        var stateDns = await dnsMod.AnalyzeAsync(hw, sys, config);
        var stateAudio = await audioMod.AnalyzeAsync(hw, sys, config);

        Assert.Equal(OptimizationState.NotDetected, stateDns);
        Assert.Equal(OptimizationState.NotDetected, stateAudio);
    }

    [Fact]
    public async Task AdbManager_SafelyHandlesInvalidApkPathAndEmptyPackages()
    {
        var config = new GameLoopConfig { IsInstalled = true, InstallPath = @"C:\FakeGameLoop" };

        var apkRes = await AdbManager.InstallApkAsync(@"C:\NonExistent\Game.apk", config);
        Assert.Contains("not found", apkRes, StringComparison.OrdinalIgnoreCase);

        var launchRes = await AdbManager.LaunchGamePackageAsync(string.Empty, config);
        Assert.False(launchRes);

        var stopRes = await AdbManager.ForceStopGamePackageAsync(string.Empty, config);
        Assert.False(stopRes);

        var clearRes = await AdbManager.ClearGameDataAsync(string.Empty, config);
        Assert.False(clearRes);

        var connectRes = await AdbManager.ConnectCustomDeviceAsync(string.Empty, config);
        Assert.False(connectRes);
    }

    [Fact]
    public void AdbTelemetryService_ParseDisplayMetrics_HandlesSpacedAndCustomFormats()
    {
        string spacedWmSize = "Physical size: 2560 x 1440\nOverride size: 1920 x 1080";
        string wmDensity = "density: 480";

        var display = AdbTelemetryService.ParseDisplayMetrics(spacedWmSize, wmDensity);

        Assert.Equal("2560x1440", display.PhysicalResolution);
        Assert.Equal("1920x1080", display.OverrideResolution);
        Assert.Equal("1920x1080", display.EffectiveResolution);
        Assert.Equal(480, display.DensityDpi);
    }

    [Fact]
    public void AdbTelemetryService_ParseDisplayMetrics_FallsBackToGameLoopConfig()
    {
        string emptyWmSize = "";
        string emptyDensity = "";
        var config = new GameLoopConfig { VmResWidth = 1440, VmResHeight = 1080 };

        var display = AdbTelemetryService.ParseDisplayMetrics(emptyWmSize, emptyDensity, config);

        Assert.Equal("1440x1080", display.PhysicalResolution);
        Assert.Equal("1440x1080", display.EffectiveResolution);
    }

    [Fact]
    public void HardwareDetector_DetectHardware_DetectsCpuAndGpuSuccessfully()
    {
        var hw = HardwareDetector.DetectHardware();

        Assert.NotNull(hw);
        Assert.False(string.IsNullOrWhiteSpace(hw.CpuName));
        Assert.NotEqual("Unknown GPU", hw.GpuName);
        Assert.True(hw.PhysicalCores >= 1);
        Assert.True(hw.TotalRamGb > 0);
    }
}

