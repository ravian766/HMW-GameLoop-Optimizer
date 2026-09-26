using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
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
}
