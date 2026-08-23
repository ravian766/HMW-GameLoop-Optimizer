using System.IO;
using System.Text.Json;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public static class BackupManager
{
    private static readonly string _backupDirectory;
    private static readonly string _backupFilePath;
    private static readonly List<BackupEntry> _entries = new();
    private static readonly object _lock = new();

    public static event EventHandler? BackupsChanged;

    static BackupManager()
    {
        _backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer");
        _backupFilePath = Path.Combine(_backupDirectory, "backups.json");

        Load();
    }

    public static IReadOnlyList<BackupEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.OrderByDescending(e => e.Timestamp).ToList();
        }
    }

    public static BackupEntry? GetLatestForModule(string moduleId)
    {
        lock (_lock)
        {
            return _entries.Where(e => e.ModuleId == moduleId && !e.IsReverted)
                           .OrderByDescending(e => e.Timestamp)
                           .FirstOrDefault();
        }
    }

    public static void RecordBackup(BackupEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
            Save();
        }
        BackupsChanged?.Invoke(null, EventArgs.Empty);
        Logger.Info("BackupManager", $"Recorded backup for {entry.Title} (Prev: '{entry.PreviousValue}', Target: {entry.TargetPath}\\{entry.ValueName})");
    }

    public static void MarkReverted(string id)
    {
        lock (_lock)
        {
            var item = _entries.FirstOrDefault(e => e.Id == id);
            if (item != null)
            {
                item.IsReverted = true;
                Save();
            }
        }
        BackupsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            Save();
        }
        BackupsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(_backupFilePath))
            {
                var json = File.ReadAllText(_backupFilePath);
                var list = JsonSerializer.Deserialize<List<BackupEntry>>(json);
                if (list != null)
                {
                    lock (_lock)
                    {
                        _entries.Clear();
                        _entries.AddRange(list);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("BackupManager", $"Failed to load backups: {ex.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }

            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_backupFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error("BackupManager", $"Failed to save backups: {ex.Message}");
        }
    }

    public static bool RestoreEntry(BackupEntry entry)
    {
        try
        {
            if (entry.TargetType == "Registry")
            {
                return RestoreRegistryValue(entry);
            }
            else if (entry.TargetType == "PowerPlan")
            {
                return RestorePowerPlan(entry);
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("BackupManager", $"Failed to restore entry {entry.Id}: {ex.Message}");
            return false;
        }
    }

    private static bool RestoreRegistryValue(BackupEntry entry)
    {
        try
        {
            RegistryKey? baseKey = null;
            string subPath = entry.TargetPath;

            if (entry.TargetPath.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
            {
                baseKey = Registry.CurrentUser;
                subPath = entry.TargetPath[5..];
            }
            else if (entry.TargetPath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
            {
                baseKey = Registry.LocalMachine;
                subPath = entry.TargetPath[5..];
            }
            else
            {
                baseKey = Registry.CurrentUser;
            }

            using var key = baseKey.OpenSubKey(subPath, writable: true);
            if (key == null)
            {
                // Try create if restoring
                using var createdKey = baseKey.CreateSubKey(subPath);
                if (createdKey == null) return false;
                return ApplyValueToKey(createdKey, entry);
            }

            return ApplyValueToKey(key, entry);
        }
        catch (Exception ex)
        {
            Logger.Error("BackupManager", $"Registry restore error for {entry.TargetPath}: {ex.Message}");
            return false;
        }
    }

    private static bool ApplyValueToKey(RegistryKey key, BackupEntry entry)
    {
        if (entry.PreviousValue == null)
        {
            // Value did not exist before
            try
            {
                key.DeleteValue(entry.ValueName, false);
                MarkReverted(entry.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        RegistryValueKind kind = RegistryValueKind.DWord;
        if (Enum.TryParse<RegistryValueKind>(entry.PreviousValueKind, out var parsedKind))
        {
            kind = parsedKind;
        }

        object valToSet = entry.PreviousValue;
        if (kind == RegistryValueKind.DWord && int.TryParse(entry.PreviousValue, out int intVal))
        {
            valToSet = intVal;
        }

        key.SetValue(entry.ValueName, valToSet, kind);
        MarkReverted(entry.Id);
        Logger.Success("BackupManager", $"Restored {entry.TargetPath}\\{entry.ValueName} to '{entry.PreviousValue}'");
        return true;
    }

    private static bool RestorePowerPlan(BackupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.PreviousValue)) return false;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/setactive {entry.PreviousValue}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3000);
            MarkReverted(entry.Id);
            Logger.Success("BackupManager", $"Restored Power Plan to {entry.PreviousValue}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("BackupManager", $"Failed to restore power plan: {ex.Message}");
            return false;
        }
    }
}
