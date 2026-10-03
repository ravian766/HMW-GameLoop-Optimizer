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

    [Fact]
    public void AdbManager_KnownGameLoopPorts_DoesNotIncludeConsolePort5554()
    {
        var ports = AdbManager.KnownGameLoopPorts;
        Assert.DoesNotContain(5554, ports);
        Assert.Equal(5555, ports[0]);
    }

    [Fact]
    public async Task AdbManager_DiscoverListeningEmulatorPorts_PrioritizesPort5555()
    {
        var config = new GameLoopConfig { IsInstalled = false };
        var ports = await AdbManager.DiscoverListeningEmulatorPortsAsync(config);
        Assert.NotEmpty(ports);
        Assert.Equal(5555, ports[0]);
        Assert.DoesNotContain(5554, ports);
    }

    [Fact]
    public void AdbManager_ParseDevicesOutput_ExtractsSerialStateAndModelWithInterveningProductTag()
    {
        string sample = "List of devices attached \r\n" +
                        "emulator-5554          device product:22081212C model:ASUS_AI2201_D device:22081212C\r\n" +
                        "127.0.0.1:5555         device product:22081212C model:ASUS_AI2201_D device:22081212C\r\n";

        var devices = AdbManager.ParseDevicesOutput(sample);

        Assert.Equal(2, devices.Count);
        Assert.Equal("emulator-5554", devices[0].Serial);
        Assert.Equal("device", devices[0].State);
        Assert.Equal("ASUS_AI2201_D", devices[0].Model);
        Assert.True(devices[0].IsEmulator);

        Assert.Equal("127.0.0.1:5555", devices[1].Serial);
        Assert.Equal("device", devices[1].State);
        Assert.Equal("ASUS_AI2201_D", devices[1].Model);
        Assert.True(devices[1].IsEmulator);
    }

    [Fact]
    public void AdbManager_ParseDevicesOutput_SkipsDaemonStartupMessages()
    {
        string sample = "* daemon not running. starting it now on port 5037 *\r\n" +
                        "* daemon started successfully *\r\n" +
                        "List of devices attached \r\n" +
                        "127.0.0.1:5555         device product:22081212C model:SM-S918B device:22081212C\r\n";

        var devices = AdbManager.ParseDevicesOutput(sample);

        Assert.Single(devices);
        Assert.Equal("127.0.0.1:5555", devices[0].Serial);
        Assert.Equal("device", devices[0].State);
        Assert.Equal("SM-S918B", devices[0].Model);
    }

    [Fact]
    public void AdbManager_ParseRunningPackages_FiltersWhitelistedAndIdentifiesRogueApps()
    {
        string samplePs = @"USER           PID  PPID     VSZ    RSS WCHAN            ADDR S NAME
root             1     0   20744   2560 0                   0 S init
root             2     0       0      0 0                   0 S [kthreadd]
system         611     1   35128   5740 0                   0 S servicemanager
system         854   523 2045612 124560 0                   0 S system_server
u0_a12        1560   523 1056784  85600 0                   0 S com.android.systemui
u0_a25        2104   523 1256784  95600 0                   0 S com.google.android.gms
u0_a30        2450   523 1156784  75600 0                   0 S com.google.android.gms:persistent
u0_a45        3020   523  985600  62400 0                   0 S com.android.vending
u0_a60        4100   523 1580000 350000 0                   0 S com.tencent.ig
u0_a70        5200   523  850000  45000 0                   0 S com.tencent.tinput
u0_a80        6100   523  500000  30000 0                   0 S com.bloatware.optimizer
";

        var rogueApps = AdbManager.ParseRunningPackages(samplePs);

        // Safe/whitelisted should not be in rogueApps
        Assert.DoesNotContain("com.tencent.ig", rogueApps);
        Assert.DoesNotContain("com.tencent.tinput", rogueApps);
        Assert.DoesNotContain("com.android.systemui", rogueApps);
        Assert.DoesNotContain("system_server", rogueApps);
        Assert.DoesNotContain("init", rogueApps);

        // Rogue/background services must be identified
        Assert.Contains("com.google.android.gms", rogueApps);
        Assert.Contains("com.android.vending", rogueApps);
        Assert.Contains("com.bloatware.optimizer", rogueApps);
    }

    [Fact]
    public void AdbManager_ParseGpuRendererInfo_ExtractsGlesVendorRendererVersion()
    {
        string sampleDump = @"
SurfaceFlinger state:
GLES: ARM, Mali-G78, OpenGL ES 3.2
";
        var info = AdbManager.ParseGpuRendererInfo(sampleDump);

        Assert.Equal("ARM", info.Vendor);
        Assert.Equal("Mali-G78", info.Renderer);
        Assert.Equal("OpenGL ES 3.2", info.Version);
    }

    [Fact]
    public void AdbManager_ParseGpuRendererInfo_HandlesMesaVirglFormat()
    {
        string sampleDump = @"
GL_VENDOR: Mesa
GL_RENDERER: virgl (Intel UHD Graphics 630)
GL_VERSION: OpenGL ES 3.1 Mesa 21.0.3
";
        var info = AdbManager.ParseGpuRendererInfo(sampleDump);

        Assert.Equal("Mesa", info.Vendor);
        Assert.Contains("virgl", info.Renderer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OpenGL ES 3.1", info.Version);
    }

    [Fact]
    public void AdbManager_ParseGetPropOutput_ExtractsPropertiesCorrectly()
    {
        string sampleGetProp = @"
[debug.sf.fps]: [120]
[dalvik.vm.heapgrowthlimit]: [512m]
[net.dns1]: [1.1.1.1]
[ro.product.model]: [ASUS_AI2201_D]
";
        var props = AdbManager.ParseGetPropOutput(sampleGetProp);

        Assert.Equal("120", props["debug.sf.fps"]);
        Assert.Equal("512m", props["dalvik.vm.heapgrowthlimit"]);
        Assert.Equal("1.1.1.1", props["net.dns1"]);
        Assert.Equal("ASUS_AI2201_D", props["ro.product.model"]);
    }

    [Fact]
    public void AdbManager_ParsePingOutput_ExtractsLatencyAndLoss()
    {
        string samplePing = @"
PING 1.1.1.1 (1.1.1.1) 56(84) bytes of data.
64 bytes from 1.1.1.1: icmp_seq=1 ttl=56 time=18.4 ms
64 bytes from 1.1.1.1: icmp_seq=2 ttl=56 time=22.6 ms
64 bytes from 1.1.1.1: icmp_seq=3 ttl=56 time=19.1 ms
64 bytes from 1.1.1.1: icmp_seq=4 ttl=56 time=20.3 ms

--- 1.1.1.1 ping statistics ---
4 packets transmitted, 4 received, 0% packet loss, time 3004ms
rtt min/avg/max/mdev = 18.400/20.100/22.600/1.543 ms
";
        var result = AdbManager.ParsePingOutput(samplePing, "1.1.1.1");

        Assert.True(result.Success);
        Assert.Equal(18.4, result.MinMs);
        Assert.Equal(20.1, result.AvgMs);
        Assert.Equal(22.6, result.MaxMs);
        Assert.Equal(1.5, result.MdevMs);
        Assert.Equal(0.0, result.PacketLossPct);
        Assert.Contains("Avg=20.1ms", result.Summary);
    }

    [Fact]
    public void AdbTelemetryService_ParseThermalMetrics_ExtractsCpuGpuAndThermalStatus()
    {
        string sysOut = "48000\n52000\n";
        string dumpOut = @"
Current temperatures from HAL:
    Temperature{mValue=58.5, mType=0, mName=CPU, mStatus=0}
    Temperature{mValue=61.0, mType=1, mName=GPU, mStatus=0}
Thermal status: 0
";
        var thermals = AdbTelemetryService.ParseThermalMetrics(sysOut, dumpOut);

        Assert.Equal(58.5, thermals.CpuTempC);
        Assert.Equal(61.0, thermals.GpuTempC);
        Assert.Equal("Normal", thermals.ThermalStatus);
        Assert.False(thermals.IsThrottling);
        Assert.Contains("Normal", thermals.SummaryDisplay);
    }

    [Fact]
    public void AdbTelemetryService_ParseThermalMetrics_DetectsThrottlingWhenSevere()
    {
        string sysOut = "84000\n";
        string dumpOut = @"
Current temperatures from HAL:
    Temperature{mValue=84.0, mType=0, mName=CPU, mStatus=2}
Thermal status: 2
";
        var thermals = AdbTelemetryService.ParseThermalMetrics(sysOut, dumpOut);

        Assert.Equal(84.0, thermals.CpuTempC);
        Assert.True(thermals.IsThrottling);
        Assert.Equal("Severe", thermals.ThermalStatus);
        Assert.Contains("THROTTLED", thermals.SummaryDisplay);
    }

    [Fact]
    public void AdbStudioViewModel_HasAllEnhancementTogglesAndCommands()
    {
        var vm = new GameLoopOptimizer.ViewModels.AdbStudioViewModel(() => new GameLoopConfig());

        Assert.True(vm.AdbKillBackgroundApps);
        Assert.True(vm.AdbGpuPipelineOptimize);
        Assert.True(vm.AdbPowerProfileLock);

        Assert.NotNull(vm.PrepareForMatchCommand);
        Assert.NotNull(vm.KillBackgroundAppsCommand);
        Assert.NotNull(vm.RunInVmPingCommand);
        Assert.NotNull(vm.LockPowerProfileCommand);
        Assert.NotNull(vm.OptimizeGpuPipelineCommand);

        vm.Dispose();
    }
}

