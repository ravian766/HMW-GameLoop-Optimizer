using System.IO;
using System.Text;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class ActiveSavSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TargetPackage { get; set; } = string.Empty;
    public string LocalStagedPath { get; set; } = string.Empty;
    public ActiveSavProfile? CurrentProfile { get; set; }
}

public static class ActiveSavService
{
    public static readonly string[] FpsKeys = { "FPSLevel", "BattleFPS", "LobbyFPS" };
    public static readonly string[] QualityKeys = { "BattleQuality", "LobbyQuality" };
    public static readonly string[] StyleKeys = { "Style", "GraphicFavor" };

    public static readonly string[] SupportedPackages = new[]
    {
        "com.tencent.ig",
        "com.pubg.krmobile",
        "com.pubg.imobile",
        "com.vng.pubgmobile",
        "com.rekoo.pubgm"
    };

    public static string LocalStagingDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameLoopOptimizer",
        "ActiveSav"
    );

    public static string BackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameLoopOptimizer",
        "ActiveSavBackups"
    );

    public static string GetRemotePathForPackage(string packageName)
    {
        return $"/sdcard/Android/data/{packageName}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/SaveGames/Active.sav";
    }

    #region Bytecode Parsing & Patching

    public static int FindValueOffset(byte[] buf, string name)
    {
        if (buf == null || string.IsNullOrEmpty(name)) return -1;
        byte[] pattern = Encoding.ASCII.GetBytes(name);

        for (int i = 0; i <= buf.Length - pattern.Length - 8; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buf[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                // In UE4 UProperty serialized binaries, the 4-byte payload begins 4 bytes after property name
                return i + pattern.Length + 4;
            }
        }
        return -1;
    }

    public static bool TryReadInt(byte[] buf, string name, out int value)
    {
        value = 0;
        int off = FindValueOffset(buf, name);
        if (off >= 0 && off + 4 <= buf.Length)
        {
            value = BitConverter.ToInt32(buf, off);
            return true;
        }
        return false;
    }

    public static bool PatchInt(byte[] buf, string name, int value)
    {
        int off = FindValueOffset(buf, name);
        if (off >= 0 && off + 4 <= buf.Length)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buf, off, 4);
            return true;
        }
        return false;
    }

    public static ActiveSavProfile ReadProfileFromBytes(byte[] buf, string profileName = "Current In-Game")
    {
        var profile = new ActiveSavProfile
        {
            Name = profileName,
            Description = "Extracted from game memory file",
            IsCustom = true
        };

        if (TryReadInt(buf, "FPSLevel", out int fps)) profile.FpsLevel = fps;
        if (TryReadInt(buf, "LobbyFPS", out int lFps)) profile.LobbyFpsLevel = lFps;
        if (TryReadInt(buf, "BattleQuality", out int bq)) profile.BattleQuality = bq;
        if (TryReadInt(buf, "LobbyQuality", out int lq)) profile.LobbyQuality = lq;
        if (TryReadInt(buf, "Style", out int st)) profile.Style = st;
        if (TryReadInt(buf, "GraphicFavor", out int gf)) profile.GraphicFavor = gf;

        return profile;
    }

    public static int ApplyProfileToBytes(byte[] buf, ActiveSavProfile profile)
    {
        int patchedCount = 0;

        foreach (var k in FpsKeys)
        {
            int targetVal = k == "LobbyFPS" ? profile.LobbyFpsLevel : profile.FpsLevel;
            if (PatchInt(buf, k, targetVal)) patchedCount++;
        }

        foreach (var k in QualityKeys)
        {
            int targetVal = k == "LobbyQuality" ? profile.LobbyQuality : profile.BattleQuality;
            if (PatchInt(buf, k, targetVal)) patchedCount++;
        }

        if (PatchInt(buf, "Style", profile.Style)) patchedCount++;
        if (PatchInt(buf, "GraphicFavor", profile.GraphicFavor)) patchedCount++;

        return patchedCount;
    }

    #endregion

    #region ADB Synchronization & Local Fallback

    public static async Task<string?> DetectActiveRemoteGamePackageAsync(GameLoopConfig gl)
    {
        if (!AdbManager.IsAdbAvailable(gl)) return null;
        await AdbManager.AutoConnectGameLoopAsync(gl);

        foreach (var pkg in SupportedPackages)
        {
            string remotePath = GetRemotePathForPackage(pkg);
            string output = await AdbManager.ExecuteAdbCommandAsync($"shell \"ls {remotePath}\"", config: gl);
            if (!output.Contains("No such file", StringComparison.OrdinalIgnoreCase) &&
                output.Contains("Active.sav", StringComparison.OrdinalIgnoreCase))
            {
                return pkg;
            }
        }

        return null;
    }

    public static async Task<ActiveSavSyncResult> PullActiveSavAsync(GameLoopConfig gl)
    {
        var result = new ActiveSavSyncResult();

        if (!AdbManager.IsAdbAvailable(gl))
        {
            result.Success = false;
            result.Message = "ADB is not available on this system.";
            return result;
        }

        try
        {
            string? pkg = await DetectActiveRemoteGamePackageAsync(gl);
            if (string.IsNullOrEmpty(pkg))
            {
                result.Success = false;
                result.Message = "Could not locate Active.sav in any installed PUBG Mobile editions. Ensure GameLoop is running with PUBG installed.";
                return result;
            }

            result.TargetPackage = pkg;
            Directory.CreateDirectory(LocalStagingDirectory);
            string localPath = Path.Combine(LocalStagingDirectory, $"{pkg}_Active.sav");

            string remotePath = GetRemotePathForPackage(pkg);
            string pullOutput = await AdbManager.ExecuteAdbCommandAsync($"pull \"{remotePath}\" \"{localPath}\"", config: gl);

            if (File.Exists(localPath))
            {
                byte[] bytes = await File.ReadAllBytesAsync(localPath);
                result.CurrentProfile = ReadProfileFromBytes(bytes, $"{pkg} Settings");
                result.LocalStagedPath = localPath;
                result.Success = true;
                result.Message = $"Successfully pulled Active.sav from {pkg} (Current: {ActiveSavProfile.GetFpsLabel(result.CurrentProfile.FpsLevel)}).";
                Logger.Success("ActiveSavService", result.Message);
            }
            else
            {
                result.Success = false;
                result.Message = $"ADB pull returned: {pullOutput}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error syncing Active.sav: {ex.Message}";
            Logger.Error("ActiveSavService", result.Message);
        }

        return result;
    }

    public static async Task<ActiveSavSyncResult> PushActiveSavProfileAsync(ActiveSavProfile profile, GameLoopConfig gl)
    {
        var result = new ActiveSavSyncResult();

        if (!AdbManager.IsAdbAvailable(gl))
        {
            result.Success = false;
            result.Message = "ADB is not available on this system.";
            return result;
        }

        try
        {
            string? pkg = await DetectActiveRemoteGamePackageAsync(gl);
            if (string.IsNullOrEmpty(pkg))
            {
                result.Success = false;
                result.Message = "Could not locate Active.sav in VM. Ensure GameLoop and PUBG are launched.";
                return result;
            }

            result.TargetPackage = pkg;
            Directory.CreateDirectory(LocalStagingDirectory);
            string localPath = Path.Combine(LocalStagingDirectory, $"{pkg}_Active.sav");

            string remotePath = GetRemotePathForPackage(pkg);

            // 1. Pull latest binary
            await AdbManager.ExecuteAdbCommandAsync($"pull \"{remotePath}\" \"{localPath}\"", config: gl);
            if (!File.Exists(localPath))
            {
                result.Success = false;
                result.Message = "Failed to pull base Active.sav for patching.";
                return result;
            }

            // 2. Create automated backup snapshot
            CreateBackupSnapshot(localPath, pkg);

            // 3. Patch binary
            byte[] bytes = await File.ReadAllBytesAsync(localPath);
            int patchedCount = ApplyProfileToBytes(bytes, profile);
            await File.WriteAllBytesAsync(localPath, bytes);

            // 4. Push back to running VM
            string pushOutput = await AdbManager.ExecuteAdbCommandAsync($"push \"{localPath}\" \"{remotePath}\"", config: gl);

            result.LocalStagedPath = localPath;
            result.CurrentProfile = profile;
            result.Success = true;
            result.Message = $"Injected {profile.Name} into {pkg} ({ActiveSavProfile.GetFpsLabel(profile.FpsLevel)}, {ActiveSavProfile.GetQualityLabel(profile.BattleQuality)}).";
            Logger.Success("ActiveSavService", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to inject Active.sav: {ex.Message}";
            Logger.Error("ActiveSavService", result.Message);
        }

        return result;
    }

    public static string CreateBackupSnapshot(string localSavPath, string package)
    {
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            string bakPath = Path.Combine(BackupDirectory, $"Active_{package}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sav");
            File.Copy(localSavPath, bakPath, true);

            // Prune backups keeping top 10
            var files = new DirectoryInfo(BackupDirectory).GetFiles("Active_*.sav")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(10);
            foreach (var f in files)
            {
                try { f.Delete(); } catch { }
            }

            return bakPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static async Task<ActiveSavSyncResult> RestoreLatestBackupAsync(GameLoopConfig gl)
    {
        var result = new ActiveSavSyncResult();
        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                result.Success = false;
                result.Message = "No Active.sav backups found.";
                return result;
            }

            var latest = new DirectoryInfo(BackupDirectory).GetFiles("Active_*.sav")
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();

            if (latest == null)
            {
                result.Success = false;
                result.Message = "No Active.sav backup files found.";
                return result;
            }

            string? pkg = await DetectActiveRemoteGamePackageAsync(gl);
            if (string.IsNullOrEmpty(pkg))
            {
                result.Success = false;
                result.Message = "Could not locate running PUBG package to restore.";
                return result;
            }

            string remotePath = GetRemotePathForPackage(pkg);
            await AdbManager.ExecuteAdbCommandAsync($"push \"{latest.FullName}\" \"{remotePath}\"", config: gl);

            result.Success = true;
            result.Message = $"Restored backup snapshot from {latest.CreationTimeUtc:g} to {pkg}.";
            Logger.Success("ActiveSavService", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Restore failed: {ex.Message}";
        }

        return result;
    }

    #endregion
}
