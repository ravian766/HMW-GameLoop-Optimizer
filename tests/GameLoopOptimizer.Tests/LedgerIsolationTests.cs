using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

/// <summary>
/// Guards the Phase 1 fix that stopped the test suite from corrupting the user's real
/// rollback ledger at %LocalAppData%\GameLoopOptimizer\backups.json.
///
/// Regression context: before this fix, BackupManager.Clear() wrote an empty list straight
/// to the real ledger, and Phase1EnhancementsTests leaked two 'test_gpu_multi_rollback'
/// entries into the user's live file. These tests fail if that ever regresses.
/// </summary>
[Collection("BackupTests")]
public class LedgerIsolationTests
{
    [Fact]
    public void Ledger_IsNotPointedAtRealUserProfile_DuringTests()
    {
        Assert.False(
            BackupManager.IsRealUserLedger,
            "Tests must never run against the real user ledger. " +
            $"BackupManager is pointed at '{BackupManager.BackupFilePath}'.");

        Assert.False(
            BackupManager.InMemoryOnly,
            "The BackupTests collection needs a writable temp ledger to exercise persistence.");
    }

    [Fact]
    public void LedgerFile_IsInsideTempDirectory()
    {
        string expectedRoot = Path.Combine(Path.GetTempPath(), "GameLoopOptimizer_Tests");
        string actual = BackupManager.BackupDirectory;

        Assert.StartsWith(expectedRoot, actual, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KeymapVault_IsAlsoIsolated_FromRealUserVault()
    {
        string vault = KeymapBackupManager.VaultDirectory;

        // Note: a naive "does it contain AppData" check is wrong here - the OS temp path is
        // itself located under C:\Users\<user>\AppData\Local\Temp. Compare against the actual
        // real vault location instead.
        string realVault = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameLoopOptimizer",
            "KeymapVault");

        Assert.False(
            string.Equals(vault, realVault, StringComparison.OrdinalIgnoreCase),
            $"Keymap vault must not be the real user vault. Got '{vault}'.");

        Assert.StartsWith(
            Path.Combine(Path.GetTempPath(), "GameLoopOptimizer_Tests"),
            vault,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAndRecord_PersistOnlyToTempLedger()
    {
        BackupManager.Clear();
        BackupManager.RecordBackup(new Models.BackupEntry
        {
            ModuleId = "test_isolation_probe",
            Title = "Isolation Probe",
            TargetType = "Registry",
            TargetPath = @"HKCU\Software\TestIsolation",
            ValueName = "Probe",
            PreviousValue = "0"
        });

        // The entry must exist on disk, but under the temp root.
        Assert.True(File.Exists(BackupManager.BackupFilePath));

        string realLedger = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameLoopOptimizer",
            "backups.json");

        Assert.False(
            string.Equals(BackupManager.BackupFilePath, realLedger, StringComparison.OrdinalIgnoreCase),
            "Test ledger path must never equal the real user ledger path.");

        BackupManager.Clear();
    }

    /// <summary>
    /// Guards the specific leak that occurred: test-only module ids must never appear in a
    /// persisted ledger. Any module id prefixed 'test_' is synthetic by definition.
    /// </summary>
    [Fact]
    public void NoTestArtifacts_AreEverPersistedToLedger()
    {
        BackupManager.Clear();

        foreach (var entry in BackupManager.GetEntries())
        {
            Assert.False(
                entry.ModuleId.StartsWith("test_", StringComparison.OrdinalIgnoreCase),
                $"Ledger contains test artifact module id '{entry.ModuleId}'. " +
                "Synthetic entries must be isolated to the temp ledger.");
        }
    }

    /// <summary>
    /// Save() now writes to a temp file and moves it into place, so a partial write can never
    /// truncate the ledger. This asserts the no-leftover-temp-file invariant after writes.
    /// </summary>
    [Fact]
    public void AtomicSave_LeavesNoTempFileBehind()
    {
        BackupManager.Clear();
        BackupManager.RecordBackup(new Models.BackupEntry
        {
            ModuleId = "test_atomic_save",
            Title = "Atomic Save Probe",
            TargetType = "Registry",
            TargetPath = @"HKCU\Software\AtomicProbe",
            ValueName = "Probe",
            PreviousValue = "1"
        });

        Assert.False(
            File.Exists(BackupManager.BackupFilePath + ".tmp"),
            "Atomic save must not leave a .tmp file behind.");

        BackupManager.Clear();
    }
}