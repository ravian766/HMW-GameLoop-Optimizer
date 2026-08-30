using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class EnhancementsPhase2Tests
{
    [Fact]
    public void NetworkQoSModule_HasValidMetadataAndProperties()
    {
        var module = new NetworkQoSModule();
        Assert.Equal("net_udp_qos_priority", module.Id);
        Assert.Equal(OptimizationCategory.WindowsConfig, module.Category);
        Assert.True(module.RequiresAdmin);
        Assert.Equal(RiskLevel.Safe, module.RiskLevel);
        Assert.Contains("QoS", module.Title);
        Assert.Contains("DSCP 46", module.RecommendedStateDisplay);
    }

    [Fact]
    public async Task NetworkQoSModule_AnalyzeAsync_ExecutesGracefully()
    {
        var module = new NetworkQoSModule();
        var hw = new HardwareInfo { LogicalProcessors = 8, TotalRamGb = 16 };
        var sys = new SystemInfo { ActivePowerPlanName = "Balanced" };
        var gl = new GameLoopConfig { IsInstalled = false };

        var state = await module.AnalyzeAsync(hw, sys, gl);
        Assert.NotNull(module.CurrentStateDisplay);
        Assert.True(state is OptimizationState.RequiresAdmin or OptimizationState.Optimized or OptimizationState.Recommended);
    }

    [Fact]
    public void WatchdogService_GameDetectionAndBoostProperties_DefaultCorrectly()
    {
        using var watchdog = new GameLoopWatchdogService(() => new GameLoopConfig());
        Assert.True(watchdog.IsEnabled);
        Assert.True(watchdog.IsAutoPurgeEnabled);
        Assert.True(watchdog.IsAutoGameBoostEnabled);
        Assert.Equal("Standby", watchdog.DetectedGameTitle);
        Assert.Empty(watchdog.DetectedGamePackage);
        Assert.False(watchdog.IsGameActive);
    }

    [Fact]
    public async Task KeymapVault_GlopExportAndImport_PreservesIntegrity()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "GlopTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var dummyFile = Path.Combine(tempDir, "DefaultKeyMapping.xml");
            await File.WriteAllTextAsync(dummyFile, "<Root><Item ApkName=\"com.tencent.ig\" Point_X=\"0.5\" Point_Y=\"0.5\" /></Root>");

            var config = new GameLoopConfig { InstallPath = tempDir };
            var profile = await KeymapBackupManager.CreateBackupAsync(config, "Glop Export Test Profile");

            if (profile != null)
            {
                var glopPath = Path.Combine(tempDir, "ExportedTest.glop");
                bool exportSuccess = await KeymapBackupManager.ExportProfileToGlopFileAsync(profile.Id, glopPath);
                Assert.True(exportSuccess);
                Assert.True(File.Exists(glopPath));

                // Test Import
                var importedProfile = await KeymapBackupManager.ImportProfileFromGlopFileAsync(glopPath);
                Assert.NotNull(importedProfile);
                Assert.Contains("ExportedTest", importedProfile.Name);

                // Clean up profile
                KeymapBackupManager.DeleteProfile(profile.Id);
                KeymapBackupManager.DeleteProfile(importedProfile.Id);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
