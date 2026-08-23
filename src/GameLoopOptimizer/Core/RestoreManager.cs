using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public static class RestoreManager
{
    public static async Task<int> RestoreAllAsync()
    {
        return await Task.Run(() =>
        {
            var entries = BackupManager.GetEntries().Where(e => !e.IsReverted).ToList();
            int successCount = 0;

            foreach (var entry in entries)
            {
                if (BackupManager.RestoreEntry(entry))
                {
                    successCount++;
                }
            }

            Logger.Info("RestoreManager", $"Restored {successCount} / {entries.Count} items to their original states.");
            return successCount;
        });
    }

    public static async Task<bool> RestoreSingleAsync(string backupId)
    {
        return await Task.Run(() =>
        {
            var entry = BackupManager.GetEntries().FirstOrDefault(e => e.Id == backupId);
            if (entry == null) return false;

            return BackupManager.RestoreEntry(entry);
        });
    }
}
