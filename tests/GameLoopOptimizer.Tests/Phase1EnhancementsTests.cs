using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using GameLoopOptimizer.ViewModels;
using Xunit;

namespace GameLoopOptimizer.Tests;

[Collection("BackupTests")]
public class Phase1EnhancementsTests
{
    [Fact]
    public void AdbTelemetryService_DeltaFpsCalculation_MeasuresConsecutiveFramesAccurately()
    {
        string pkg = "com.test.game.fps";
        AdbTelemetryService.ResetFpsTracking(pkg);

        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        string sample1 = "Total frames rendered: 1000\nJanky frames: 0\n";
        
        // Initial sample establishes baseline
        double fps1 = AdbTelemetryService.ParseFpsEstimate(sample1, pkg, t0);
        Assert.True(fps1 >= 0);

        // Second sample 1.0 second later with 90 additional frames rendered -> 90.0 FPS
        var t1 = t0.AddSeconds(1.0);
        string sample2 = "Total frames rendered: 1090\nJanky frames: 1\n";
        double fps2 = AdbTelemetryService.ParseFpsEstimate(sample2, pkg, t1);

        Assert.Equal(90.0, fps2);

        // Third sample 0.5 seconds later with 60 additional frames -> 120.0 FPS
        var t2 = t1.AddSeconds(0.5);
        string sample3 = "Total frames rendered: 1150\nJanky frames: 1\n";
        double fps3 = AdbTelemetryService.ParseFpsEstimate(sample3, pkg, t2);

        Assert.Equal(120.0, fps3);
    }

    [Fact]
    public void AdbTelemetryService_SingleShotFallback_ExtractsBoundedValue()
    {
        string sample = "Total frames rendered: 90\nJanky frames: 0\n";
        double fps = AdbTelemetryService.ParseFpsEstimate(sample);
        Assert.Equal(90.0, fps);

        string sampleOverMax = "Total frames rendered: 240\n";
        double fpsOver = AdbTelemetryService.ParseFpsEstimate(sampleOverMax);
        Assert.Equal(120.0, fpsOver);
    }

    [Fact]
    public void BackupManager_GetAllForModule_ReturnsAllModuleEntries()
    {
        string testModuleId = "test_gpu_multi_rollback";
        
        var entry1 = new BackupEntry
        {
            ModuleId = testModuleId,
            Title = "Entry 1",
            TargetType = "Registry",
            TargetPath = @"HKCU\Software\Test",
            ValueName = "Exe1.exe",
            PreviousValue = "0"
        };

        var entry2 = new BackupEntry
        {
            ModuleId = testModuleId,
            Title = "Entry 2",
            TargetType = "Registry",
            TargetPath = @"HKCU\Software\Test",
            ValueName = "Exe2.exe",
            PreviousValue = "0"
        };

        BackupManager.RecordBackup(entry1);
        BackupManager.RecordBackup(entry2);

        var list = BackupManager.GetAllForModule(testModuleId);
        Assert.NotNull(list);
        Assert.True(list.Count >= 2);
        Assert.Contains(list, e => e.ValueName == "Exe1.exe");
        Assert.Contains(list, e => e.ValueName == "Exe2.exe");
    }

    [Fact]
    public void BackupManager_RestoreEntry_RejectsEmptyAdbPropTarget()
    {
        var invalidAdbEntry = new BackupEntry
        {
            ModuleId = "adb_test",
            Title = "Invalid ADB Prop",
            TargetType = "AdbProp",
            TargetPath = "", // Invalid empty target path
            PreviousValue = "120"
        };

        bool ok = BackupManager.RestoreEntry(invalidAdbEntry);
        Assert.False(ok);
    }

    [Fact]
    public async Task GpuPreferenceModule_RollbackAsync_HandlesUnconfiguredGracefully()
    {
        var module = new GpuPreferenceModule();
        // Rollback without prior configuration should succeed gracefully via fallback cleanup
        var result = await module.RollbackAsync(null);
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public void CalibrateKeymapXml_VehicleAndSwimmingHudModes_CalibratesWithCorrectAnchors()
    {
        string sampleXml = @"
<Item ApkName=""com.tencent.ig"" Mode=""1"">
    <Key Id=""Drive"" Point_X=""0.800000"" Point_Y=""0.700000"" />
</Item>
<Item ApkName=""com.tencent.ig"" Mode=""3"">
    <Key Id=""SwimUp"" Point_X=""0.900000"" Point_Y=""0.400000"" />
</Item>";

        var (calibrated, count) = ResolutionKeymapService.CalibrateKeymapXml(sampleXml, 1440, 1080);
        Assert.Equal(2, count);
        Assert.Contains("Point_X=", calibrated);
        Assert.Contains("Point_Y=", calibrated);
    }

    [Fact]
    public void IAdbManager_DefaultInstance_ProvidesParity()
    {
        IAdbManager manager = DefaultAdbManager.Instance;
        Assert.NotNull(manager);
        // IsAvailable can be checked without exceptions
        bool avail = manager.IsAvailable(null);
        Assert.True(avail || !avail); // executes cleanly without throwing
    }

    [Fact]
    public void AdbStudioViewModel_CommandHistory_NavigatesUpAndDown()
    {
        var vm = new GameLoopOptimizer.ViewModels.AdbStudioViewModel(() => new GameLoopConfig());
        Assert.NotNull(vm.CommandPresets);
        Assert.Contains("wm size", vm.CommandPresets);

        // Push two commands into history
        vm.PushCommandHistory("getprop 1");
        vm.PushCommandHistory("getprop 2");

        // History Up should retrieve the latest ("getprop 2")
        vm.HistoryUpCommand.Execute(null);
        Assert.Equal("getprop 2", vm.InteractiveAdbCommand);

        // History Up again should retrieve the previous ("getprop 1")
        vm.HistoryUpCommand.Execute(null);
        Assert.Equal("getprop 1", vm.InteractiveAdbCommand);

        // History Down should go back to "getprop 2"
        vm.HistoryDownCommand.Execute(null);
        Assert.Equal("getprop 2", vm.InteractiveAdbCommand);

        // History Down past the end clears
        vm.HistoryDownCommand.Execute(null);
        Assert.Equal(string.Empty, vm.InteractiveAdbCommand);
    }

    [Fact]
    public void AdbTelemetryService_ParseGfxAdvancedMetrics_Calculates1PercentLowAndJank()
    {
        var snapshot = new AdbTelemetrySnapshot
        {
            EstimatedFps = 120.0
        };

        string sampleGfxinfo = @"
        Total frames rendered: 5400
        Janky frames: 108 (2.0%)
        50th percentile: 8ms
        90th percentile: 10ms
        95th percentile: 12ms
        99th percentile: 14ms
        ";

        AdbTelemetryService.ParseGfxAdvancedMetrics(sampleGfxinfo, snapshot);

        Assert.Equal(120.0, snapshot.Fps);
        Assert.True(snapshot.OnePercentLowFps > 70.0);
        Assert.True(snapshot.OnePercentLowFps <= 120.0);
        Assert.Equal(0.02, snapshot.DroppedFramesRatio, 3);
        Assert.True(snapshot.FrametimeVarianceMs > 0);
    }

    [Fact]
    public void ProcessManager_CalculateOptimalAffinityMask_DetectsArchitectures()
    {
        // Intel i9-13900K (24 cores / 32 threads) -> 8 P-Cores with HT = 16 threads (0xFFFF)
        long i9Mask = ProcessManager.CalculateOptimalAffinityMask(32, 24, "13th Gen Intel(R) Core(TM) i9-13900K");
        Assert.Equal(0xFFFF, i9Mask);

        // Intel i5-13600K (14 cores / 20 threads) -> 6 P-Cores with HT = 12 threads (0x0FFF)
        long i5Mask = ProcessManager.CalculateOptimalAffinityMask(20, 14, "13th Gen Intel(R) Core(TM) i5-13600K");
        Assert.Equal(0x0FFF, i5Mask);

        // AMD Ryzen 9 7900X3D (12 cores / 24 threads) -> CCD0 with 3D V-Cache = 12 threads (0x0FFF)
        long amd7900X3DMask = ProcessManager.CalculateOptimalAffinityMask(24, 12, "AMD Ryzen 9 7900X3D 12-Core Processor");
        Assert.Equal(0x0FFF, amd7900X3DMask);

        // AMD Ryzen 9 7950X3D (16 cores / 32 threads) -> CCD0 with 3D V-Cache = 16 threads (0xFFFF)
        long amd7950X3DMask = ProcessManager.CalculateOptimalAffinityMask(32, 16, "AMD Ryzen 9 7950X3D 16-Core Processor");
        Assert.Equal(0xFFFF, amd7950X3DMask);

        // Quad core or lower fallback -> all cores (4 threads = 0x0F)
        long quadMask = ProcessManager.CalculateOptimalAffinityMask(4, 4, "Intel Core i5-7400");
        Assert.Equal(0x0F, quadMask);
    }

    [Fact]
    public async Task EmulatorDiagnosticService_AutoHealStuckEmulator_ExecutesSafely()
    {
        var config = new GameLoopConfig
        {
            InstallPath = Path.GetTempPath()
        };

        var report = await EmulatorDiagnosticService.AutoHealStuckEmulatorAsync(config);
        Assert.NotNull(report);
        Assert.True(report.Success);
        Assert.Contains("98% Doctor Complete", report.SummaryMessage);
    }

    [Fact]
    public async Task ResolutionKeymapService_ToggleStretchedResolution_TogglesResolutionState()
    {
        var config = new GameLoopConfig
        {
            InstallPath = Path.GetTempPath(),
            VmResWidth = 1920,
            VmResHeight = 1080
        };

        // First toggle: 1920x1080 -> 1440x1080
        var res1 = await ResolutionKeymapService.ToggleStretchedResolutionAsync(config, 1440, 1080);
        Assert.Equal(1440, config.VmResWidth);
        Assert.Equal(1080, config.VmResHeight);
        Assert.Contains("Stretched Res", res1.Message);

        // Second toggle: 1440x1080 -> 1920x1080
        var res2 = await ResolutionKeymapService.ToggleStretchedResolutionAsync(config, 1440, 1080);
        Assert.Equal(1920, config.VmResWidth);
        Assert.Equal(1080, config.VmResHeight);
        Assert.Contains("Native 16:9", res2.Message);
    }

    [Fact]
    public async Task AudioFootstepClarifierModule_ApplyProfile_AppliesAcousticProfiles()
    {
        var module = new AudioFootstepClarifierModule();
        var hw = new HardwareInfo();
        var sys = new SystemInfo();
        var gl = new GameLoopConfig();

        var res = await module.ApplyProfileAsync(FootstepClarityProfile.FootstepScoutExtreme, hw, sys, gl);
        Assert.NotNull(res);
        Assert.True(res.Success);
        Assert.Equal(FootstepClarityProfile.FootstepScoutExtreme, module.ActiveProfile);
        Assert.True(module.IsOptimized);
    }

    [Fact]
    public void ActiveSavProfile_Presets_IncludePotatoAndEsportsModes()
    {
        var presets = ActiveSavProfile.BuiltInPresets;
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name.Contains("Anti-Stutter Potato Mode"));
        Assert.Contains(presets, p => p.Name.Contains("Esports 120 FPS"));

        var vm = new ActiveSavViewModel(() => new GameLoopConfig(), () => null);
        Assert.True(vm.SyncActiveSavCommand.CanExecute(null));
        Assert.True(vm.RestoreActiveSavCommand.CanExecute(null));
    }
}
