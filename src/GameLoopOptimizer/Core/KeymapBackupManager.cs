using System.IO;
using System.IO.Compression;
using System.Text.Json;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class KeymapBackupProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string ZipFilePath { get; set; } = string.Empty;
    public int FilesArchived { get; set; }
    public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}

public static class KeymapBackupManager
{
    private static readonly string VaultDirectory;
    private static readonly string IndexFilePath;
    private static readonly List<KeymapBackupProfile> _profiles = new();
    private static readonly object _lock = new();

    public static event EventHandler? ProfilesChanged;

    static KeymapBackupManager()
    {
        VaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "KeymapVault");
        IndexFilePath = Path.Combine(VaultDirectory, "vault_index.json");

        LoadProfiles();
    }

    public static IReadOnlyList<KeymapBackupProfile> GetProfiles()
    {
        lock (_lock) return _profiles.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public static async Task<KeymapBackupProfile?> CreateBackupAsync(GameLoopConfig config, string customName = "PUBG Custom Keymap & Sensitivity")
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(VaultDirectory))
                {
                    Directory.CreateDirectory(VaultDirectory);
                }

                var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Resolve root from InstallPath
                if (!string.IsNullOrEmpty(config.InstallPath))
                {
                    AddTargetDirectories(config.InstallPath, candidateDirs);

                    try
                    {
                        var parent = Directory.GetParent(config.InstallPath);
                        if (parent != null && parent.Exists)
                        {
                            AddTargetDirectories(parent.FullName, candidateDirs);
                        }
                    }
                    catch { }
                }

                // 2. Standard installation roots
                var standardRoots = new[]
                {
                    @"D:\Program Files\TxGameAssistant",
                    @"C:\Program Files\TxGameAssistant",
                    @"C:\Program Files (x86)\TxGameAssistant",
                    @"D:\TxGameAssistant",
                    @"E:\TxGameAssistant",
                    @"D:\GameLoop",
                    @"C:\GameLoop"
                };

                foreach (var r in standardRoots)
                {
                    if (Directory.Exists(r))
                    {
                        AddTargetDirectories(r, candidateDirs);
                    }
                }

                // 3. User AppData configuration paths
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                candidateDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant"));
                candidateDirs.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC"));
                candidateDirs.Add(Path.Combine(appData, "Tencent", "TxGameAssistant"));
                candidateDirs.Add(Path.Combine(appData, "Tencent", "MobileGamePC"));

                // Keymap & sensitivity file patterns
                var targetPatterns = new[]
                {
                    "DefaultKeyMapping.xml",
                    "AowConfig.ini",
                    "Config.ini",
                    "ConfigPath.xml",
                    "phone_device*.json",
                    "hardinput*.conf",
                    "smk*.conf",
                    "block_rule.json",
                    "*.cfg"
                };

                var filesToBackup = new List<string>();

                foreach (var dir in candidateDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        foreach (var pattern in targetPatterns)
                        {
                            try
                            {
                                var matches = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                                filesToBackup.AddRange(matches);
                            }
                            catch { }
                        }
                    }
                }

                // Distinct by full path
                filesToBackup = filesToBackup.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // Export registry settings into dictionary
                var regData = new Dictionary<string, string>();
                try
                {
                    using var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
                    if (regKey != null)
                    {
                        foreach (var valName in regKey.GetValueNames())
                        {
                            var val = regKey.GetValue(valName)?.ToString();
                            if (val != null) regData[valName] = val;
                        }
                    }
                }
                catch { }

                int totalItemCount = filesToBackup.Count + (regData.Count > 0 ? 1 : 0);

                var profile = new KeymapBackupProfile
                {
                    Name = customName,
                    CreatedAt = DateTime.Now,
                    FilesArchived = totalItemCount
                };

                string zipName = $"Keymap_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                string zipPath = Path.Combine(VaultDirectory, zipName);
                profile.ZipFilePath = zipPath;

                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    int index = 1;
                    var fileMapping = new List<object>();

                    foreach (var file in filesToBackup)
                    {
                        try
                        {
                            string entryName = $"file_{index}_{Path.GetFileName(file)}";
                            archive.CreateEntryFromFile(file, entryName);
                            fileMapping.Add(new { EntryName = entryName, OriginalPath = file });
                            index++;
                        }
                        catch { }
                    }

                    // Save registry profile
                    var regJson = JsonSerializer.Serialize(regData, new JsonSerializerOptions { WriteIndented = true });
                    var regEntry = archive.CreateEntry("registry_profile.json");
                    using (var writer = new StreamWriter(regEntry.Open()))
                    {
                        writer.Write(regJson);
                    }

                    // Manifest
                    var manifestJson = JsonSerializer.Serialize(new
                    {
                        ProfileName = customName,
                        Date = DateTime.Now,
                        FileMapping = fileMapping,
                        RegistryEntriesCount = regData.Count
                    }, new JsonSerializerOptions { WriteIndented = true });

                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using var manifestWriter = new StreamWriter(manifestEntry.Open());
                    manifestWriter.Write(manifestJson);
                }

                lock (_lock)
                {
                    _profiles.Add(profile);
                    SaveProfiles();
                }

                ProfilesChanged?.Invoke(null, EventArgs.Empty);
                Logger.Success("KeymapVault", $"Created targeted keymap & sensitivity snapshot '{customName}' ({filesToBackup.Count} files + registry layout archived).");
                return profile;
            }
            catch (Exception ex)
            {
                Logger.Error("KeymapVault", $"Failed to create keymap backup: {ex.Message}");
                return null;
            }
        });
    }

    private static void AddTargetDirectories(string root, HashSet<string> candidateDirs)
    {
        candidateDirs.Add(root);
        candidateDirs.Add(Path.Combine(root, "ui"));
        candidateDirs.Add(Path.Combine(root, "ui", "ConfigFile"));
        candidateDirs.Add(Path.Combine(root, "AppMarket"));
        candidateDirs.Add(Path.Combine(root, "AppMarket", "config"));
    }

    public static async Task<bool> RestoreBackupAsync(string profileId, GameLoopConfig config)
    {
        return await Task.Run(() =>
        {
            try
            {
                KeymapBackupProfile? target = null;
                lock (_lock)
                {
                    target = _profiles.FirstOrDefault(p => p.Id == profileId);
                }

                if (target == null || !File.Exists(target.ZipFilePath))
                {
                    Logger.Error("KeymapVault", "Backup archive file not found.");
                    return false;
                }

                using (var archive = ZipFile.OpenRead(target.ZipFilePath))
                {
                    // 1. Read manifest and restore targeted files
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry != null)
                    {
                        using var reader = new StreamReader(manifestEntry.Open());
                        var json = reader.ReadToEnd();
                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("FileMapping", out var mappingArray))
                        {
                            foreach (var item in mappingArray.EnumerateArray())
                            {
                                string? entryName = item.GetProperty("EntryName").GetString();
                                string? originalPath = item.GetProperty("OriginalPath").GetString();

                                if (!string.IsNullOrEmpty(entryName) && !string.IsNullOrEmpty(originalPath))
                                {
                                    var fileEntry = archive.GetEntry(entryName);
                                    if (fileEntry != null)
                                    {
                                        try
                                        {
                                            var parentDir = Path.GetDirectoryName(originalPath);
                                            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                                            {
                                                Directory.CreateDirectory(parentDir);
                                            }
                                            fileEntry.ExtractToFile(originalPath, overwrite: true);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }

                    // 2. Restore registry keymap & sensitivity settings
                    var regEntry = archive.GetEntry("registry_profile.json");
                    if (regEntry != null)
                    {
                        using var reader = new StreamReader(regEntry.Open());
                        var json = reader.ReadToEnd();
                        var regDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (regDict != null)
                        {
                            using var regKey = Registry.CurrentUser.CreateSubKey(@"Software\Tencent\MobileGamePC");
                            if (regKey != null)
                            {
                                foreach (var (k, v) in regDict)
                                {
                                    try
                                    {
                                        if (int.TryParse(v, out int intVal))
                                        {
                                            regKey.SetValue(k, intVal, RegistryValueKind.DWord);
                                        }
                                        else
                                        {
                                            regKey.SetValue(k, v, RegistryValueKind.String);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                Logger.Success("KeymapVault", $"Restored keymap and configuration snapshot '{target.Name}'.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("KeymapVault", $"Failed to restore keymap profile: {ex.Message}");
                return false;
            }
        });
    }

    public static void DeleteProfile(string profileId)
    {
        lock (_lock)
        {
            var item = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (item != null)
            {
                try
                {
                    if (File.Exists(item.ZipFilePath)) File.Delete(item.ZipFilePath);
                }
                catch { }
                _profiles.Remove(item);
                SaveProfiles();
            }
        }
        ProfilesChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void LoadProfiles()
    {
        try
        {
            if (File.Exists(IndexFilePath))
            {
                var json = File.ReadAllText(IndexFilePath);
                var list = JsonSerializer.Deserialize<List<KeymapBackupProfile>>(json);
                if (list != null)
                {
                    lock (_lock)
                    {
                        _profiles.Clear();
                        _profiles.AddRange(list);
                    }
                }
            }
        }
        catch { }
    }

    private static void SaveProfiles()
    {
        try
        {
            if (!Directory.Exists(VaultDirectory)) Directory.CreateDirectory(VaultDirectory);
            var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(IndexFilePath, json);
        }
        catch { }
    }
}
