using System.IO;
using System.Text.Json;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class PakBackupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PakBackupProfile? Profile { get; set; }
    public int FilesCopied { get; set; }
    public long BytesTransferred { get; set; }
}

public static class GameResourceBackupService
{
    private static string? _customVaultDirectory;

    public static string CustomVaultDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_customVaultDirectory)) return _customVaultDirectory;

            // Check saved registry setting
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\GameLoopOptimizer");
                if (k != null)
                {
                    string? saved = k.GetValue("PakVaultPath") as string;
                    if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved))
                    {
                        _customVaultDirectory = saved;
                        return _customVaultDirectory;
                    }
                }
            }
            catch { }

            // Auto-check secondary non-system drives for existing "GameLoop_PakVault" or "PakVault"
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.Name != "C:\\").Select(d => d.Name).ToList();
                foreach (var d in drives)
                {
                    string cand1 = Path.Combine(d, "GameLoop_PakVault");
                    if (Directory.Exists(cand1)) return cand1;

                    string cand2 = Path.Combine(d, "GameLoopOptimizer", "PakVault");
                    if (Directory.Exists(cand2)) return cand2;
                }
            }
            catch { }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameLoopOptimizer",
                "PakVault"
            );
        }
        set
        {
            _customVaultDirectory = value;
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(@"Software\GameLoopOptimizer");
                if (k != null && !string.IsNullOrEmpty(value))
                {
                    k.SetValue("PakVaultPath", value);
                }
            }
            catch { }
        }
    }

    public static string PakVaultDirectory => CustomVaultDirectory;

    public static void SetVaultDirectory(string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath)) return;
        Directory.CreateDirectory(newPath);
        CustomVaultDirectory = newPath;
        Logger.Info("PakVault", $"Vault location updated to: {newPath}");
    }

    public static string GetGameLoopSharedFolderPath(GameLoopConfig? config = null)
    {
        // 1. Check HKCU/HKLM registry
        try
        {
            var keys = new[]
            {
                @"Software\Tencent\MobileGamePC",
                @"Software\Tencent\TxGameAssistant",
                @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                @"SOFTWARE\WOW6432Node\Tencent\TxGameAssistant"
            };

            foreach (var keyPath in keys)
            {
                using var k = Registry.CurrentUser.OpenSubKey(keyPath) ?? Registry.LocalMachine.OpenSubKey(keyPath);
                if (k != null)
                {
                    var shared = k.GetValue("SharedPath") as string;
                    if (!string.IsNullOrWhiteSpace(shared) && Directory.Exists(shared))
                    {
                        return shared;
                    }

                    var temp = k.GetValue("TempPath") as string ?? k.GetValue("TempDir") as string;
                    if (!string.IsNullOrWhiteSpace(temp))
                    {
                        var candidate = Path.Combine(temp, "TxGameDownload", "MobileGamePCShared");
                        if (Directory.Exists(candidate)) return candidate;
                    }
                }
            }
        }
        catch { }

        // 2. Scan standard drive candidate paths
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
        foreach (var d in drives)
        {
            var candidate = Path.Combine(d, "Temp", "TxGameDownload", "MobileGamePCShared");
            if (Directory.Exists(candidate)) return candidate;
        }

        // 3. Fallback to default
        string fallback = @"C:\Temp\TxGameDownload\MobileGamePCShared";
        try
        {
            Directory.CreateDirectory(fallback);
        }
        catch { }
        return fallback;
    }

    public static string GetRemotePaksPath(string packageName)
    {
        packageName = string.IsNullOrWhiteSpace(packageName) ? "com.tencent.ig" : packageName.Trim();
        return $"/sdcard/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks";
    }

    public static List<string> DiscoverExistingVaultsOnAllDrives()
    {
        var discovered = new List<string>();
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
            foreach (var d in drives)
            {
                string p1 = Path.Combine(d, "GameLoop_PakVault");
                if (Directory.Exists(p1) && !discovered.Contains(p1)) discovered.Add(p1);

                string p2 = Path.Combine(d, "GameLoopOptimizer", "PakVault");
                if (Directory.Exists(p2) && !discovered.Contains(p2)) discovered.Add(p2);
            }
        }
        catch { }
        return discovered;
    }

    public static async Task<PakBackupResult> ImportExistingFolderAsync(string sourceFolderPath, string? targetPackageName = null)
    {
        var result = new PakBackupResult();
        try
        {
            if (!Directory.Exists(sourceFolderPath))
            {
                result.Success = false;
                result.Message = "Selected folder does not exist.";
                return result;
            }

            var pakFiles = Directory.EnumerateFiles(sourceFolderPath, "*.pak", SearchOption.AllDirectories).ToList();
            if (pakFiles.Count == 0)
            {
                result.Success = false;
                result.Message = "No .pak files found in the selected folder.";
                return result;
            }

            string metaFile = Path.Combine(sourceFolderPath, "manifest.json");
            PakBackupProfile? profile = null;

            if (File.Exists(metaFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(metaFile);
                    profile = JsonSerializer.Deserialize<PakBackupProfile>(json);
                }
                catch { }
            }

            string pkg = targetPackageName ?? profile?.PackageName ?? "com.tencent.ig";
            var known = AdbManager.KnownGamePackages.FirstOrDefault(p => p.PackageName.Equals(pkg, StringComparison.OrdinalIgnoreCase));
            string gameTitle = known?.DisplayName ?? "PUBG Mobile";

            long totalBytes = pakFiles.Sum(f => new FileInfo(f).Length);
            string backupId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string destDir = Path.Combine(PakVaultDirectory, $"{pkg}_{backupId}");
            Directory.CreateDirectory(destDir);

            int copied = 0;
            foreach (var file in pakFiles)
            {
                string target = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, target, true);
                copied++;
            }

            var newProfile = new PakBackupProfile
            {
                Id = $"{pkg}_{backupId}",
                Title = $"{gameTitle} Imported Map Snapshot",
                PackageName = pkg,
                GameTitle = gameTitle,
                CreatedAt = DateTime.Now,
                TotalSizeBytes = totalBytes,
                FileCount = copied,
                LocalBackupPath = destDir,
                PakFileNames = pakFiles.Select(Path.GetFileName).ToList()!
            };

            string manifestJson = JsonSerializer.Serialize(newProfile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(destDir, "manifest.json"), manifestJson);

            result.Success = true;
            result.Profile = newProfile;
            result.FilesCopied = copied;
            result.BytesTransferred = totalBytes;
            result.Message = $"Successfully imported {copied} map files ({newProfile.FormattedSize}) into your Vault!";
            Logger.Success("PakVault", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Import failed: {ex.Message}";
            Logger.Error("PakVault", result.Message);
        }

        return result;
    }

    public static List<PakBackupProfile> ListPakBackups()
    {
        var list = new List<PakBackupProfile>();
        try
        {
            string vaultDir = PakVaultDirectory;
            if (!Directory.Exists(vaultDir))
            {
                Directory.CreateDirectory(vaultDir);
                return list;
            }

            foreach (var dir in Directory.EnumerateDirectories(vaultDir))
            {
                var metaFile = Path.Combine(dir, "manifest.json");
                if (File.Exists(metaFile))
                {
                    try
                    {
                        string json = File.ReadAllText(metaFile);
                        var profile = JsonSerializer.Deserialize<PakBackupProfile>(json);
                        if (profile != null)
                        {
                            // Ensure local path is updated to current folder location in case folder was moved
                            profile.LocalBackupPath = dir;
                            list.Add(profile);
                        }
                    }
                    catch { }
                }
                else
                {
                    // Auto-generate manifest if user copied raw pak folders manually
                    var paks = Directory.EnumerateFiles(dir, "*.pak").ToList();
                    if (paks.Count > 0)
                    {
                        long size = paks.Sum(f => new FileInfo(f).Length);
                        string folderName = Path.GetFileName(dir);
                        string pkg = AdbManager.KnownGamePackages.FirstOrDefault(k => folderName.Contains(k.PackageName, StringComparison.OrdinalIgnoreCase))?.PackageName ?? "com.tencent.ig";
                        var known = AdbManager.KnownGamePackages.FirstOrDefault(p => p.PackageName == pkg);

                        var autoProfile = new PakBackupProfile
                        {
                            Id = folderName,
                            Title = $"{known?.DisplayName ?? "PUBG Mobile"} Auto-Detected Snapshot",
                            PackageName = pkg,
                            GameTitle = known?.DisplayName ?? "PUBG Mobile",
                            CreatedAt = Directory.GetCreationTime(dir),
                            TotalSizeBytes = size,
                            FileCount = paks.Count,
                            LocalBackupPath = dir,
                            PakFileNames = paks.Select(Path.GetFileName).ToList()!
                        };
                        try
                        {
                            File.WriteAllText(metaFile, JsonSerializer.Serialize(autoProfile, new JsonSerializerOptions { WriteIndented = true }));
                        }
                        catch { }
                        list.Add(autoProfile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("PakVault", $"Failed to list backups: {ex.Message}");
        }

        return list.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public static async Task<(string? ValidPath, List<string> PakFiles, string? AlternativePackage)> DiscoverRemotePaksAsync(string packageName, GameLoopConfig config)
    {
        var candidatePaths = new List<string>
        {
            $"/sdcard/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks",
            $"/sdcard/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/Saved/Paks",
            $"/storage/emulated/0/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks",
            $"/storage/emulated/0/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/Saved/Paks",
            $"/data/media/0/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks",
            $"/data/data/{packageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks"
        };

        foreach (var path in candidatePaths)
        {
            try
            {
                // Run ls without nested quotes to prevent toybox/toolbox quote parse failures
                string output = await AdbManager.ExecuteShellCommandAsync($"ls -1 {path}", null, 4000, config);
                var files = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(f => f.Trim())
                                  .Where(f => f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
                                  .Select(f => Path.GetFileName(f))
                                  .Distinct()
                                  .ToList();

                if (files.Count > 0)
                {
                    return (path, files, null);
                }
            }
            catch { }
        }

        // Deep Search: Use Android find command across the entire package directory
        try
        {
            string findOutput = await AdbManager.ExecuteShellCommandAsync($"find /sdcard/Android/data/{packageName} -name *.pak 2>/dev/null", null, 6000, config);
            var foundLines = findOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(f => f.Trim())
                                       .Where(f => f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
                                       .ToList();

            if (foundLines.Count > 0)
            {
                string firstFile = foundLines[0];
                int lastSlash = firstFile.LastIndexOf('/');
                string dir = lastSlash > 0 ? firstFile.Substring(0, lastSlash) : firstFile;
                var files = foundLines.Select(f => {
                    int s = f.LastIndexOf('/');
                    return s >= 0 ? f.Substring(s + 1) : f;
                }).Distinct().ToList();

                return (dir, files, null);
            }
        }
        catch { }

        // Cross-Package Discovery: Check if another popular game is installed (e.g. BGMI, PUBG KR, Free Fire)
        foreach (var pkg in AdbManager.KnownGamePackages)
        {
            if (pkg.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                string checkPath = $"/sdcard/Android/data/{pkg.PackageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks";
                string outCheck = await AdbManager.ExecuteShellCommandAsync($"ls -1 {checkPath}", null, 3000, config);
                if (outCheck.Contains(".pak", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, new List<string>(), pkg.PackageName);
                }
            }
            catch { }
        }

        return (null, new List<string>(), null);
    }

    public static async Task<PakBackupResult> BackupPaksAsync(string packageName, GameLoopConfig config, IProgress<string>? progress = null)
    {
        var result = new PakBackupResult();
        string sharedFolder = GetGameLoopSharedFolderPath(config);
        string backupId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string localDest = Path.Combine(PakVaultDirectory, $"{packageName}_{backupId}");

        try
        {
            progress?.Report("Connecting to GameLoop Android VM via ADB...");
            if (!AdbManager.IsAdbAvailable(config))
            {
                result.Success = false;
                result.Message = "ADB daemon not reachable. Ensure GameLoop is running.";
                return result;
            }

            // 1. Discover remote Paks directory across multiple Android storage candidate layouts
            progress?.Report($"Scanning storage paths for {packageName} maps & resources...");
            var (remotePaks, pakFiles, alternativePkg) = await DiscoverRemotePaksAsync(packageName, config);

            if (string.IsNullOrEmpty(remotePaks) || pakFiles.Count == 0)
            {
                result.Success = false;
                if (!string.IsNullOrEmpty(alternativePkg))
                {
                    var altKnown = AdbManager.KnownGamePackages.FirstOrDefault(p => p.PackageName.Equals(alternativePkg, StringComparison.OrdinalIgnoreCase));
                    string altName = altKnown?.DisplayName ?? alternativePkg;
                    result.Message = $"No maps found in selected game, but detected maps in {altName}! Please select {altName} from the Target Game dropdown.";
                }
                else
                {
                    result.Message = $"No .pak maps found for {packageName}. Please launch the game inside GameLoop at least once to download maps, or verify the selected game region.";
                }
                return result;
            }

            Directory.CreateDirectory(localDest);
            progress?.Report($"Found {pakFiles.Count} map & resource pak files in {remotePaks}. Staging backup...");

            // 2. Fast copy from Android VM to PC Shared Folder first
            string remoteShared = "/sdcard/TxGameDownload/MobileGamePCShared";
            await AdbManager.ExecuteShellCommandAsync($"mkdir -p {remoteShared}/PakBackup_{backupId}", null, 4000, config);
            await AdbManager.ExecuteShellCommandAsync($"cp {remotePaks}/*.pak {remoteShared}/PakBackup_{backupId}/", null, 25000, config);

            // 3. Move/Copy from PC Shared folder to local persistent Vault
            string hostSharedBackup = Path.Combine(sharedFolder, $"PakBackup_{backupId}");
            long totalBytes = 0;
            int copied = 0;

            if (Directory.Exists(hostSharedBackup) && Directory.EnumerateFiles(hostSharedBackup, "*.pak").Any())
            {
                foreach (var file in Directory.EnumerateFiles(hostSharedBackup, "*.pak"))
                {
                    string target = Path.Combine(localDest, Path.GetFileName(file));
                    File.Copy(file, target, true);
                    totalBytes += new FileInfo(target).Length;
                    copied++;
                }

                // Clean temporary shared staging directory
                try { Directory.Delete(hostSharedBackup, true); } catch { }
                await AdbManager.ExecuteShellCommandAsync($"rm -rf {remoteShared}/PakBackup_{backupId}", null, 4000, config);
            }
            else
            {
                // Direct ADB Pull fallback
                progress?.Report("Pulling .pak blobs directly via ADB bridge...");
                foreach (var pak in pakFiles)
                {
                    string remoteFile = $"{remotePaks}/{pak}";
                    string localFile = Path.Combine(localDest, pak);
                    await AdbManager.ExecuteAdbCommandAsync($"pull \"{remoteFile}\" \"{localFile}\"", 20000, config);
                    if (File.Exists(localFile))
                    {
                        totalBytes += new FileInfo(localFile).Length;
                        copied++;
                    }
                }
            }

            var known = AdbManager.KnownGamePackages.FirstOrDefault(p => p.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase));
            string gameTitle = known?.DisplayName ?? "PUBG Mobile";

            var profile = new PakBackupProfile
            {
                Id = $"{packageName}_{backupId}",
                Title = $"{gameTitle} Map & Resource Snapshot",
                PackageName = packageName,
                GameTitle = gameTitle,
                CreatedAt = DateTime.Now,
                TotalSizeBytes = totalBytes,
                FileCount = copied,
                LocalBackupPath = localDest,
                PakFileNames = pakFiles
            };

            // Save manifest
            string manifestJson = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(localDest, "manifest.json"), manifestJson);

            result.Success = true;
            result.Profile = profile;
            result.FilesCopied = copied;
            result.BytesTransferred = totalBytes;
            result.Message = $"Successfully backed up {copied} Pak files ({profile.FormattedSize}) to PC Vault!";

            Logger.Success("PakVault", result.Message);
            progress?.Report(result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Pak Backup Error: {ex.Message}";
            Logger.Error("PakVault", result.Message);
        }

        return result;
    }

    public static async Task<PakBackupResult> RestorePaksAsync(PakBackupProfile profile, GameLoopConfig config, IProgress<string>? progress = null)
    {
        var result = new PakBackupResult();
        string remotePaks = GetRemotePaksPath(profile.PackageName);
        string sharedFolder = GetGameLoopSharedFolderPath(config);

        try
        {
            if (!Directory.Exists(profile.LocalBackupPath))
            {
                result.Success = false;
                result.Message = $"Backup folder not found: {profile.LocalBackupPath}";
                return result;
            }

            progress?.Report($"Restoring {profile.FileCount} maps & resource packs ({profile.FormattedSize})...");

            // 1. Ensure target directory exists in Android VM
            await AdbManager.ExecuteShellCommandAsync($"mkdir -p \"{remotePaks}\"", null, 4000, config);

            // 2. Fast copy via Shared Folder if available
            string stagingFolder = Path.Combine(sharedFolder, $"Restore_{profile.Id}");
            Directory.CreateDirectory(stagingFolder);

            foreach (var file in Directory.EnumerateFiles(profile.LocalBackupPath, "*.pak"))
            {
                File.Copy(file, Path.Combine(stagingFolder, Path.GetFileName(file)), true);
            }

            string remoteStaging = $"/sdcard/TxGameDownload/MobileGamePCShared/Restore_{profile.Id}";
            await AdbManager.ExecuteShellCommandAsync($"cp -f \"{remoteStaging}/\"*.pak \"{remotePaks}/\"", null, 20000, config);

            // 3. Fix Android VM Linux File Permissions (Crucial!)
            progress?.Report("Configuring Android in-VM file permissions (chmod 777)...");
            await AdbManager.ExecuteShellCommandAsync($"chmod -R 777 \"{remotePaks}\"", null, 4000, config);
            await AdbManager.ExecuteShellCommandAsync($"chown -R media_rw:media_rw \"{remotePaks}\"", null, 4000, config);

            // Clean up staging
            try
            {
                Directory.Delete(stagingFolder, true);
                await AdbManager.ExecuteShellCommandAsync($"rm -rf \"{remoteStaging}\"", null, 4000, config);
            }
            catch { }

            result.Success = true;
            result.Profile = profile;
            result.FilesCopied = profile.FileCount;
            result.BytesTransferred = profile.TotalSizeBytes;
            result.Message = $"Successfully restored {profile.FileCount} map packs ({profile.FormattedSize}) directly to {profile.GameTitle}!";

            Logger.Success("PakVault", result.Message);
            progress?.Report(result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Pak Restore Error: {ex.Message}";
            Logger.Error("PakVault", result.Message);
        }

        return result;
    }

    public static bool DeletePakBackup(string backupId)
    {
        try
        {
            var dir = Directory.EnumerateDirectories(PakVaultDirectory).FirstOrDefault(d => Path.GetFileName(d).Equals(backupId, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(d).Contains(backupId));
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Logger.Info("PakVault", $"Deleted backup snapshot: {backupId}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PakVault", $"Failed to delete backup: {ex.Message}");
        }
        return false;
    }
}
