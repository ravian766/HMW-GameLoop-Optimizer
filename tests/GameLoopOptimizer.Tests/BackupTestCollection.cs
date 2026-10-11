using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

/// <summary>
/// Test collection that owns the <see cref="BackupManager"/> ledger.
///
/// The ledger is a static singleton backed by the user's real
/// %LocalAppData%\GameLoopOptimizer\backups.json. Without this fixture any test calling
/// BackupManager.Clear()/RecordBackup() would overwrite the user's actual rollback ledger.
///
/// This collection definition redirects the ledger into a temp directory for every test
/// in the "BackupTests" collection and restores the original afterwards.
/// </summary>
[CollectionDefinition(Name)]
public class BackupTestCollection : ICollectionFixture<BackupTestCollection>
{
    public const string Name = "BackupTests";

    private readonly string _tempLedgerDir;

    public BackupTestCollection()
    {
        // Redirect BEFORE any test can touch the static managers.
        _tempLedgerDir = Path.Combine(
            Path.GetTempPath(),
            "GameLoopOptimizer_Tests",
            "ledger_" + Guid.NewGuid().ToString("N"));

        Environment.SetEnvironmentVariable(BackupManager.LedgerDirectoryEnvVar, _tempLedgerDir);
        BackupManager.InMemoryOnly = false;
        BackupManager.UseDirectory(_tempLedgerDir);

        KeymapBackupManager.InMemoryOnly = false;
        KeymapBackupManager.UseDirectory(_tempLedgerDir);
    }

    public void Dispose()
    {
        // Do not restore the real ledger directory on teardown: the process is exiting and any
        // later write must stay isolated. Clear the override so no state leaks to other hosts.
        BackupManager.InMemoryOnly = true;
        KeymapBackupManager.InMemoryOnly = true;
        Environment.SetEnvironmentVariable(BackupManager.LedgerDirectoryEnvVar, null);

        try
        {
            if (Directory.Exists(_tempLedgerDir))
            {
                Directory.Delete(_tempLedgerDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup of a temp directory.
        }
    }
}