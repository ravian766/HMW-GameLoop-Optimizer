using System;
using System.IO;
using System.Linq;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

[Collection("BackupTests")]
public class GameLoop701905Tests
{
    [Fact]
    public void GameLoopCompatibilityManager_Version701905_IsRecognizedAsSupportedWithVulkanRationale()
    {
        var config = new GameLoopConfig
        {
            IsInstalled = true,
            InstallPath = @"D:\Program Files\Tencent\GameLoop",
            Version = "7.0.19.05"
        };

        GameLoopCompatibilityManager.EvaluateCompatibility(config);

        Assert.Equal(GameLoopCompatibilityTier.Supported, config.CompatibilityTier);
        Assert.Contains("7.0.19.05", config.CompatibilityReason);
        Assert.Contains("Vulkan", config.CompatibilityReason);
        Assert.Contains("Smart Mode", config.CompatibilityReason);
    }

    [Theory]
    [InlineData("7.0.19.05", true)]
    [InlineData("7.0.19.01", true)]
    [InlineData("7.0.19.100", true)]
    [InlineData("7.1.0.1", true)]
    [InlineData("4.4.2", false)]
    [InlineData(null, false)]
    public void GameLoopConfig_IsVersion70190x_IdentifiesCorrectly(string? version, bool expected)
    {
        var config = new GameLoopConfig { Version = version ?? string.Empty };
        Assert.Equal(expected, config.IsVersion70190x);
    }

    [Theory]
    [InlineData("7.0.19.05", 7, 0)]
    [InlineData("7.1.0.1", 7, 1)]
    [InlineData("4.4", 4, 4)]
    [InlineData("Invalid", 0, 0)]
    public void GameLoopConfig_VersionMajorMinor_ExtractsCorrectly(string version, int expectedMajor, int expectedMinor)
    {
        var config = new GameLoopConfig { Version = version };
        Assert.Equal((expectedMajor, expectedMinor), config.VersionMajorMinor);
    }

    [Fact]
    public void GameLoopConfig_NewProperties_HaveSensibleDefaults()
    {
        var config = new GameLoopConfig();

        Assert.Empty(config.UserDir);
        Assert.Empty(config.IniFilePath);
        Assert.False(config.ForceVulkan);
        Assert.False(config.SmartModeEnabled);
        Assert.Equal(-1, config.RenderingMode); // -1 by default (unconfigured / legacy fallback)
        Assert.Equal(0, config.AntiAliasingMode); // Off by default
        Assert.Empty(config.VulkanApiVersion);
    }

    [Fact]
    public void GameLoopProcessNames_ContainsNew701905ProcessNames()
    {
        Assert.Contains("GameLoopEmulator", GameLoopProcessNames.GameEngines);
        Assert.Contains("GameLoopVm", GameLoopProcessNames.GameEngines);

        Assert.Contains("GameLoopEmulator", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopVm", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoop", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopLauncher", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopAssistant", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopService", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopDldSvr", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopVfs", GameLoopProcessNames.AllProcesses);
        Assert.Contains("GameLoopRenderer", GameLoopProcessNames.AllProcesses);
    }

    [Fact]
    public void RecommendationEngine_GetRendererName_SupportsVulkanAndSmartMode()
    {
        Assert.Equal("Vulkan", RecommendationEngine.GetRendererName(GraphicsRenderer.Vulkan));
        Assert.Equal("Smart Mode (Auto)", RecommendationEngine.GetRendererName(GraphicsRenderer.SmartMode));
        Assert.Equal("DirectX+", RecommendationEngine.GetRendererName(GraphicsRenderer.DirectXPlus));
        Assert.Equal("OpenGL+", RecommendationEngine.GetRendererName(GraphicsRenderer.OpenGLPlus));
    }

    [Fact]
    public void RecommendationEngine_IsHardwareVulkanCapable_DetectsCapableHardware()
    {
        var hw = new HardwareInfo
        {
            GpuVendor = GpuVendor.Nvidia,
            GpuName = "NVIDIA GeForce RTX 4070",
            DedicatedVramMb = 12288,
            TotalRamGb = 32.0,
            PhysicalCores = 8,
            LogicalProcessors = 16,
            RefreshRateHz = 165
        };

        var rec = RecommendationEngine.Calculate(hw);

        Assert.NotNull(rec);
        Assert.True(rec.IsVulkanCapable);
        Assert.Equal(GraphicsRenderer.DirectXPlus, rec.RecommendedRenderer);
        Assert.Equal(2, rec.RecommendedRenderingMode);

        // Manually selecting Vulkan updates RecommendedRenderingMode to 3
        rec.RecommendedRenderer = GraphicsRenderer.Vulkan;
        Assert.Equal(3, rec.RecommendedRenderingMode);

        // Manually selecting Smart Mode updates RecommendedRenderingMode to 0
        rec.RecommendedRenderer = GraphicsRenderer.SmartMode;
        Assert.Equal(0, rec.RecommendedRenderingMode);
    }

    [Fact]
    public void GameLoopIniService_ReadWriteAndRestore_WorksCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gl_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var iniPath = Path.Combine(tempDir, "GameLoop.ini");
            var initialContent = @"# GameLoop Configuration
[Engine]
RenderingMode=2
ForceVulkan=0
SmartModeEnabled=0
AntiAliasingMode=0
VMCpuCount=4
VMMemorySizeInMB=4096
";
            File.WriteAllText(iniPath, initialContent);

            var readConfig = new GameLoopConfig();
            GameLoopIniService.ReadIniSettings(iniPath, readConfig);
            Assert.Equal(2, readConfig.RenderingMode);

            // Modify configuration and write with backup
            readConfig.RenderingMode = 3;
            readConfig.ForceVulkan = true;
            readConfig.AntiAliasingMode = 2;

            var writeSuccess = GameLoopIniService.WriteIniSettings(iniPath, readConfig, recordBackup: true);
            Assert.True(writeSuccess);

            // Verify written file
            var updatedConfig = new GameLoopConfig();
            GameLoopIniService.ReadIniSettings(iniPath, updatedConfig);
            Assert.Equal(3, updatedConfig.RenderingMode);
            Assert.True(updatedConfig.ForceVulkan);
            Assert.Equal(2, updatedConfig.AntiAliasingMode);

            // Find recorded backup
            var backup = BackupManager.GetEntries()
                .FirstOrDefault(b => b.TargetType == "IniFile" && b.ValueName == "RenderingMode");
            Assert.NotNull(backup);
            Assert.Equal("2", backup.PreviousValue);
            Assert.Equal("3", backup.NewValue);

            // Restore from backup
            var restoreSuccess = BackupManager.RestoreEntry(backup);
            Assert.True(restoreSuccess);

            // Re-read and verify reverted setting
            var restoredConfig = new GameLoopConfig();
            GameLoopIniService.ReadIniSettings(iniPath, restoredConfig);
            Assert.Equal(2, restoredConfig.RenderingMode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GameLoopIniService_DetectAndApply_SetsIniPathWhenPresent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gl_test_detect_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var iniPath = Path.Combine(tempDir, "GameLoop.ini");
            File.WriteAllText(iniPath, @"[Engine]
RenderingMode=3
ForceVulkan=1
SmartModeEnabled=0
AntiAliasingMode=2
");

            var config = new GameLoopConfig
            {
                IsInstalled = true,
                InstallPath = tempDir
            };

            GameLoopIniService.DetectAndApply(config);

            Assert.Equal(iniPath, config.IniFilePath);
            Assert.True(config.ForceVulkan);
            Assert.Equal(3, config.RenderingMode);
            Assert.Equal(2, config.AntiAliasingMode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
