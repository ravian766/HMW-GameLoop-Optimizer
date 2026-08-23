using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class BackupRestoreTests
{
    [Fact]
    public void BackupManager_RecordAndRetrieve_MaintainsLedger()
    {
        // Arrange
        BackupManager.Clear();

        var entry = new BackupEntry
        {
            ModuleId = "test_module",
            Title = "Test Optimization",
            Category = OptimizationCategory.WindowsConfig,
            TargetType = "Registry",
            TargetPath = @"HKCU\Software\Test",
            ValueName = "TestValue",
            PreviousValue = "0",
            NewValue = "1",
            Description = "Unit test backup"
        };

        // Act
        BackupManager.RecordBackup(entry);
        var entries = BackupManager.GetEntries();
        var latest = BackupManager.GetLatestForModule("test_module");

        // Assert
        Assert.NotEmpty(entries);
        Assert.NotNull(latest);
        Assert.Equal("test_module", latest.ModuleId);
        Assert.Equal("0", latest.PreviousValue);
        Assert.False(latest.IsReverted);

        // Mark Reverted
        BackupManager.MarkReverted(latest.Id);
        var updatedEntries = BackupManager.GetEntries();
        Assert.True(updatedEntries.First(e => e.Id == latest.Id).IsReverted);

        // Cleanup
        BackupManager.Clear();
    }
}
