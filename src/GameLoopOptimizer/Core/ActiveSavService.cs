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
    public static readonly string[] FpsKeys = new[]
    {
        "BattleFPS",
        "FPSLevel",
        "LobbyFPS",
        "MainCityFPS"
    };

    public static readonly string[] QualityKeys = new[]
    {
        "BattleRenderQuality",
        "LobbyRenderQuality",
        "MainCityRenderQuality",
        "ManorRenderQuality",
        "nEnhancedLobbyQuality",
        "BattleQuality",
        "LobbyQuality"
    };

    public static readonly string[] StyleKeys = new[]
    {
        "BattleRenderStyle",
        "LobbyRenderStyle",
        "Style"
    };

    public static readonly string[] GraphicFavorKeys = new[]
    {
        "GraphicFavor"
    };

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

    /// <summary>
    /// Scans the binary buffer and repairs any IntProperty header strings that were corrupted
    /// by legacy offset calculations (e.g., 0x0C 00 00 [val] [val] [val] [val] 'Property\0').
    /// </summary>
    public static int HealCorruptedIntPropertyHeaders(byte[] buf)
    {
        if (buf == null || buf.Length < 20) return 0;
        int healed = 0;
        byte[] propBytes = Encoding.ASCII.GetBytes("Property\0");

        for (int i = 0; i <= buf.Length - 17; i++)
        {
            if (buf[i] == 0x0C && buf[i + 1] == 0 && buf[i + 2] == 0)
            {
                bool isProp = true;
                for (int j = 0; j < propBytes.Length; j++)
                {
                    if (buf[i + 7 + j] != propBytes[j])
                    {
                        isProp = false;
                        break;
                    }
                }

                if (isProp)
                {
                    // Check if the 4 bytes at i+3..i+6 are NOT '0x00', 'I', 'n', 't'
                    if (buf[i + 3] != 0 || buf[i + 4] != 0x49 || buf[i + 5] != 0x6E || buf[i + 6] != 0x74)
                    {
                        buf[i + 3] = 0;
                        buf[i + 4] = 0x49; // 'I'
                        buf[i + 5] = 0x6E; // 'n'
                        buf[i + 6] = 0x74; // 't'
                        healed++;
                    }
                }
            }
        }

        return healed;
    }

    /// <summary>
    /// Locates the exact 4-byte payload offset of a named UE4 IntProperty in an Active.sav binary.
    /// Format: [PropertyName\0] [0x0C 0x00 0x00 0x00] ["IntProperty\0"] [0x04 0x00 0x00 0x00] [0x00 0x00 0x00 0x00] [0x00] [int32 value]
    /// </summary>
    public static int FindIntPropertyOffset(byte[] buf, string name)
    {
        if (buf == null || string.IsNullOrEmpty(name)) return -1;

        // Try exact null-terminated property name match first
        byte[] exactPattern = Encoding.ASCII.GetBytes(name + "\0");
        for (int i = 0; i <= buf.Length - exactPattern.Length - 25; i++)
        {
            bool match = true;
            for (int j = 0; j < exactPattern.Length; j++)
            {
                if (buf[i + j] != exactPattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                int hdrOffset = i + exactPattern.Length;
                int typeLen = BitConverter.ToInt32(buf, hdrOffset);
                if (typeLen == 12 && hdrOffset + 16 <= buf.Length)
                {
                    string typeStr = Encoding.ASCII.GetString(buf, hdrOffset + 4, 12);
                    if (typeStr == "IntProperty\0")
                    {
                        int dataSize = BitConverter.ToInt32(buf, hdrOffset + 16);
                        if (dataSize == 4 && hdrOffset + 29 <= buf.Length)
                        {
                            // Value payload offset: hdrOffset + 4(typeLen) + 12(typeStr) + 4(dataSize) + 4(arrIdx) + 1(tag) = hdrOffset + 25
                            return hdrOffset + 25;
                        }
                    }
                }
            }
        }

        // Fallback: Check without trailing null terminator if buffer has custom mock format
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
                int nextOffset = i + pattern.Length;
                // If followed by 4 null bytes (legacy synthetic mock format)
                if (nextOffset + 8 <= buf.Length &&
                    buf[nextOffset] == 0 && buf[nextOffset + 1] == 0 &&
                    buf[nextOffset + 2] == 0 && buf[nextOffset + 3] == 0)
                {
                    return nextOffset + 4;
                }
            }
        }

        return -1;
    }

    public static int FindValueOffset(byte[] buf, string name)
    {
        return FindIntPropertyOffset(buf, name);
    }

    public static bool TryReadInt(byte[] buf, string name, out int value)
    {
        value = 0;
        int off = FindIntPropertyOffset(buf, name);
        if (off >= 0 && off + 4 <= buf.Length)
        {
            value = BitConverter.ToInt32(buf, off);
            return true;
        }
        return false;
    }

    public static bool PatchInt(byte[] buf, string name, int value)
    {
        int off = FindIntPropertyOffset(buf, name);
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
        HealCorruptedIntPropertyHeaders(buf);

        var profile = new ActiveSavProfile
        {
            Name = profileName,
            Description = "Extracted from game memory file",
            IsCustom = true
        };

        // FPS
        if (TryReadInt(buf, "BattleFPS", out int bFps)) profile.FpsLevel = bFps;
        else if (TryReadInt(buf, "FPSLevel", out int fps)) profile.FpsLevel = fps;

        if (TryReadInt(buf, "LobbyFPS", out int lFps)) profile.LobbyFpsLevel = lFps;
        else profile.LobbyFpsLevel = profile.FpsLevel;

        // Render Quality
        if (TryReadInt(buf, "BattleRenderQuality", out int bq)) profile.BattleQuality = bq;
        else if (TryReadInt(buf, "BattleQuality", out int bq2)) profile.BattleQuality = bq2;

        if (TryReadInt(buf, "LobbyRenderQuality", out int lq)) profile.LobbyQuality = lq;
        else if (TryReadInt(buf, "LobbyQuality", out int lq2)) profile.LobbyQuality = lq2;
        else profile.LobbyQuality = profile.BattleQuality;

        // Style
        if (TryReadInt(buf, "BattleRenderStyle", out int st)) profile.Style = st;
        else if (TryReadInt(buf, "LobbyRenderStyle", out int lst)) profile.Style = lst;
        else if (TryReadInt(buf, "Style", out int st2)) profile.Style = st2;

        // Graphic Favor
        if (TryReadInt(buf, "GraphicFavor", out int gf)) profile.GraphicFavor = gf;

        return profile;
    }

    public static int ApplyProfileToBytes(byte[] buf, ActiveSavProfile profile)
    {
        HealCorruptedIntPropertyHeaders(buf);
        int patchedCount = 0;

        foreach (var k in FpsKeys)
        {
            int targetVal = k.Contains("Lobby", StringComparison.OrdinalIgnoreCase)
                ? profile.LobbyFpsLevel
                : profile.FpsLevel;
            if (PatchInt(buf, k, targetVal)) patchedCount++;
        }

        foreach (var k in QualityKeys)
        {
            int targetVal = k.Contains("Lobby", StringComparison.OrdinalIgnoreCase)
                ? profile.LobbyQuality
                : profile.BattleQuality;
            if (PatchInt(buf, k, targetVal)) patchedCount++;
        }

        foreach (var k in StyleKeys)
        {
            if (PatchInt(buf, k, profile.Style)) patchedCount++;
        }

        int favorVal = profile.GraphicFavor <= 0 ? 1 : profile.GraphicFavor;
        if (PatchInt(buf, "GraphicFavor", favorVal)) patchedCount++;

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
            string output = await AdbManager.ExecuteShellCommandAsync($"ls {remotePath}", config: gl);
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
            await AdbManager.AutoConnectGameLoopAsync(gl);
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

            bool pulled = await AdbManager.PullFileFromVmAsync(remotePath, localPath, gl);

            if (pulled && File.Exists(localPath))
            {
                byte[] bytes = await File.ReadAllBytesAsync(localPath);
                result.CurrentProfile = ReadProfileFromBytes(bytes, $"{pkg} Settings");
                result.LocalStagedPath = localPath;
                result.Success = true;
                result.Message = $"Successfully pulled Active.sav from {pkg} (Current: {ActiveSavProfile.GetFpsLabel(result.CurrentProfile.FpsLevel)}, {ActiveSavProfile.GetQualityLabel(result.CurrentProfile.BattleQuality)}).";
                Logger.Success("ActiveSavService", result.Message);
            }
            else
            {
                result.Success = false;
                result.Message = "Failed to pull Active.sav from VM.";
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

    public static async Task<ActiveSavSyncResult> PushActiveSavProfileAsync(ActiveSavProfile profile, GameLoopConfig gl, DeviceProfile? deviceProfile = null)
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
            await AdbManager.AutoConnectGameLoopAsync(gl);
            string? pkg = await DetectActiveRemoteGamePackageAsync(gl);
            if (string.IsNullOrEmpty(pkg))
            {
                result.Success = false;
                result.Message = "Could not locate Active.sav in VM. Ensure GameLoop and PUBG are launched.";
                return result;
            }

            result.TargetPackage = pkg;
            Directory.CreateDirectory(LocalStagingDirectory);

            // 1. Check if the game process is running; if so, stop it so UE4 memory cache doesn't overwrite save files
            string pidCheck = await AdbManager.ExecuteShellCommandAsync($"pidof {pkg}", config: gl);
            bool wasRunning = !string.IsNullOrWhiteSpace(pidCheck) && pidCheck.Trim().Length > 0;
            if (wasRunning)
            {
                Logger.Info("ActiveSavService", $"Stopping {pkg} to prevent UE4 memory cache collision during injection...");
                await AdbManager.ExecuteShellCommandAsync($"am force-stop {pkg}", config: gl);
                await Task.Delay(1000);
            }

            // Purge temporary shaders, remote config caches, and logs so cached resolution/graphics state doesn't overwrite settings
            await AdbManager.ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/cache/*", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Logs/*", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/LightData/*", config: gl);

            string remoteActiveSav = GetRemotePathForPackage(pkg);
            string localActiveSav = Path.Combine(LocalStagingDirectory, $"{pkg}_Active.sav");

            // 2. Pull, backup, and patch Active.sav
            bool pulledActive = await AdbManager.PullFileFromVmAsync(remoteActiveSav, localActiveSav, gl);
            if (!pulledActive || !File.Exists(localActiveSav))
            {
                result.Success = false;
                result.Message = "Failed to pull base Active.sav for patching.";
                return result;
            }

            CreateBackupSnapshot(localActiveSav, pkg);
            byte[] activeBytes = await File.ReadAllBytesAsync(localActiveSav);
            ApplyProfileToBytes(activeBytes, profile);
            await File.WriteAllBytesAsync(localActiveSav, activeBytes);
            await AdbManager.PushFileToVmAsync(localActiveSav, remoteActiveSav, gl);

            // 3. Also patch SettingConfig_Slot.sav if it exists in the SaveGames directory
            string remoteSaveGamesDir = $"/sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/SaveGames";
            string remoteSlotSav = $"{remoteSaveGamesDir}/SettingConfig_Slot.sav";
            string localSlotSav = Path.Combine(LocalStagingDirectory, $"{pkg}_SettingConfig_Slot.sav");

            string slotCheck = await AdbManager.ExecuteShellCommandAsync($"ls \"{remoteSlotSav}\"", config: gl);
            if (!string.IsNullOrWhiteSpace(slotCheck) && !slotCheck.Contains("No such file", StringComparison.OrdinalIgnoreCase))
            {
                bool pulledSlot = await AdbManager.PullFileFromVmAsync(remoteSlotSav, localSlotSav, gl);
                if (pulledSlot && File.Exists(localSlotSav))
                {
                    CreateBackupSnapshot(localSlotSav, $"{pkg}_Slot");
                    byte[] slotBytes = await File.ReadAllBytesAsync(localSlotSav);
                    ApplyProfileToBytes(slotBytes, profile);
                    await File.WriteAllBytesAsync(localSlotSav, slotBytes);
                    await AdbManager.PushFileToVmAsync(localSlotSav, remoteSlotSav, gl);
                    Logger.Success("ActiveSavService", "Synchronized in-game profile into SettingConfig_Slot.sav.");
                }
            }

            // 4. Synchronize low-level UE4 engine CVars in UserCustom.ini (Render Quality, HDR, Style, Shadows)
            await SyncUserCustomIniAsync(pkg, profile, gl);

            // 5. Trigger Android VM 120 FPS unlock props & sync high-refresh device profile
            await AdbManager.Unlock120FpsAsync(gl);
            var devProfile = deviceProfile ?? DeviceProfile.Profiles.FirstOrDefault(p => p.MaxSupportedFps >= 120) ?? DeviceProfile.Profiles.First();
            await AdbManager.SpoofDeviceProfileAsync(devProfile, gl);

            // 6. Relaunch PUBG Mobile cleanly
            Logger.Info("ActiveSavService", $"Relaunching {pkg} with patched graphics...");
            await AdbManager.ExecuteShellCommandAsync($"monkey -p {pkg} -c android.intent.category.LAUNCHER 1", config: gl);

            result.LocalStagedPath = localActiveSav;
            result.CurrentProfile = profile;
            result.Success = true;
            result.Message = $"Injected {profile.Name} into {pkg} ({ActiveSavProfile.GetFpsLabel(profile.FpsLevel)}, {ActiveSavProfile.GetQualityLabel(profile.BattleQuality)}, {ActiveSavProfile.GetStyleLabel(profile.Style)}).";
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

    public static string EncodeCVar(string cvar)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(cvar);
        var sb = new StringBuilder("+CVars=");
        foreach (byte b in bytes)
        {
            sb.Append((b ^ 0x79).ToString("X2"));
        }
        return sb.ToString();
    }

    public static string DecodeCVar(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("+CVars=")) return line;
        string hex = line.Substring(7).Trim();
        if (hex.Length % 2 != 0) return line;
        try
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(Convert.ToByte(hex.Substring(i * 2, 2), 16) ^ 0x79);
            }
            return Encoding.ASCII.GetString(bytes);
        }
        catch
        {
            return line;
        }
    }

    public static async Task<bool> SyncUserCustomIniAsync(string pkg, ActiveSavProfile profile, GameLoopConfig gl)
    {
        try
        {
            string remoteIni = $"/sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Config/Android/UserCustom.ini";
            string localIni = Path.Combine(LocalStagingDirectory, $"{pkg}_UserCustom.ini");

            string check = await AdbManager.ExecuteShellCommandAsync($"ls \"{remoteIni}\"", config: gl);
            if (string.IsNullOrWhiteSpace(check) || check.Contains("No such file", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool pulled = await AdbManager.PullFileFromVmAsync(remoteIni, localIni, gl);
            if (!pulled || !File.Exists(localIni)) return false;

            CreateBackupSnapshot(localIni, $"{pkg}_UserCustom");

            var lines = await File.ReadAllLinesAsync(localIni);
            var updatedLines = new List<string>();

            int targetQuality = profile.BattleQuality;
            int targetStyle = profile.Style;
            string targetHdr = targetQuality >= 4 ? "1.0" : "0.0";
            int shadowQuality = targetQuality >= 4 ? 1 : 0;

            // Determine max FPS target from profile (7=120 FPS, 6=90 FPS, 5=60 FPS, 4=40 FPS)
            int targetMaxFps = profile.FpsLevel switch
            {
                7 => 120,
                6 => 90,
                5 => 60,
                4 => 40,
                _ => 30
            };

            // Track which FPS-bypass CVars we've seen so we don't duplicate them
            var seenFpsCVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                if (line.StartsWith("+CVars="))
                {
                    string dec = DecodeCVar(line);
                    if (dec.StartsWith("r.UserQualitySetting=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.UserQualitySetting={targetQuality}"));
                    }
                    else if (dec.StartsWith("r.UserHDRSetting=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.UserHDRSetting={targetQuality}"));
                    }
                    else if (dec.StartsWith("r.MobileHDR=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.MobileHDR={targetHdr}"));
                    }
                    else if (dec.StartsWith("r.ACESStyle=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.ACESStyle={targetStyle}"));
                    }
                    else if (dec.StartsWith("r.ShadowQuality=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.ShadowQuality={shadowQuality}"));
                    }
                    // FPS cap bypass CVars - replace existing or mark as seen
                    else if (dec.StartsWith("r.PUBGDeviceFPSLow=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.PUBGDeviceFPSLow={targetMaxFps}"));
                        seenFpsCVars.Add("r.PUBGDeviceFPSLow");
                    }
                    else if (dec.StartsWith("r.PUBGDeviceFPSMid=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.PUBGDeviceFPSMid={targetMaxFps}"));
                        seenFpsCVars.Add("r.PUBGDeviceFPSMid");
                    }
                    else if (dec.StartsWith("r.PUBGDeviceFPSHigh=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.PUBGDeviceFPSHigh={targetMaxFps}"));
                        seenFpsCVars.Add("r.PUBGDeviceFPSHigh");
                    }
                    else if (dec.StartsWith("r.PUBGMaxFPS=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedLines.Add(EncodeCVar($"r.PUBGMaxFPS={targetMaxFps}"));
                        seenFpsCVars.Add("r.PUBGMaxFPS");
                    }
                    else
                    {
                        updatedLines.Add(line);
                    }
                }
                else
                {
                    updatedLines.Add(line);

                    // After each section header, inject any missing FPS bypass CVars
                    if (line.Trim() == "[UserCustom DeviceProfile]" || line.Trim() == "[BackUp DeviceProfile]")
                    {
                        // We'll inject after the last CVar in each section (handled below)
                    }
                }
            }

            // Inject any FPS bypass CVars that weren't already present in the file
            // Append them at the end of [UserCustom DeviceProfile] section
            var missingCVars = new List<string>();
            if (!seenFpsCVars.Contains("r.PUBGDeviceFPSLow"))
                missingCVars.Add(EncodeCVar($"r.PUBGDeviceFPSLow={targetMaxFps}"));
            if (!seenFpsCVars.Contains("r.PUBGDeviceFPSMid"))
                missingCVars.Add(EncodeCVar($"r.PUBGDeviceFPSMid={targetMaxFps}"));
            if (!seenFpsCVars.Contains("r.PUBGDeviceFPSHigh"))
                missingCVars.Add(EncodeCVar($"r.PUBGDeviceFPSHigh={targetMaxFps}"));
            if (!seenFpsCVars.Contains("r.PUBGMaxFPS"))
                missingCVars.Add(EncodeCVar($"r.PUBGMaxFPS={targetMaxFps}"));

            if (missingCVars.Count > 0)
            {
                // Find the last +CVars= line in [UserCustom DeviceProfile] section and insert after it
                int insertIdx = -1;
                bool inUserCustomSection = false;
                for (int i = 0; i < updatedLines.Count; i++)
                {
                    if (updatedLines[i].Trim() == "[UserCustom DeviceProfile]")
                        inUserCustomSection = true;
                    else if (updatedLines[i].Trim().StartsWith("[") && inUserCustomSection)
                        break; // Hit next section

                    if (inUserCustomSection && updatedLines[i].StartsWith("+CVars="))
                        insertIdx = i;
                }

                if (insertIdx >= 0)
                {
                    updatedLines.InsertRange(insertIdx + 1, missingCVars);
                }
                else
                {
                    // Fallback: append at end of file
                    updatedLines.AddRange(missingCVars);
                }
            }

            await File.WriteAllLinesAsync(localIni, updatedLines);
            await AdbManager.PushFileToVmAsync(localIni, remoteIni, gl);
            Logger.Success("ActiveSavService", $"Synchronized UE4 CVars + FPS bypass (max {targetMaxFps}) into UserCustom.ini.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("ActiveSavService", $"Failed to sync UserCustom.ini: {ex.Message}");
            return false;
        }
    }

    #endregion
}
