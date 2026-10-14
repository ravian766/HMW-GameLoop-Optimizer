using System.Globalization;
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
    // ─────────────────────────────────────────────────────────────────────────────────
    // Write sets: the ONLY fields this app modifies.
    //
    // Derived empirically on 2026-10-11 by pulling Active.sav over ADB before and after
    // changing only the in-game Graphics preset. The game changed these three and nothing
    // else. Everything else in the file is left byte-identical so unrelated settings
    // (sensitivity, keybinds, audio, camera) survive untouched.
    // ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Render-quality fields written by the app. Both take the same value.</summary>
    public static readonly string[] QualityWriteKeys =
    {
        "BattleRenderQuality",
        "LobbyRenderQuality"
    };

    /// <summary>Frame-rate fields written by the app. Both take the same value.</summary>
    public static readonly string[] FpsWriteKeys =
    {
        "BattleFPS",
        "LobbyFPS"
    };

    /// <summary>Visual-style fields written by the app. Both take the same value.</summary>
    public static readonly string[] StyleWriteKeys =
    {
        "BattleRenderStyle",
        "LobbyRenderStyle"
    };

    // --- FPS property keys (legacy 2.x/3.x + PUBG 4.x) ---
    public static readonly string[] FpsKeys = new[]
    {
        "BattleFPS",
        "FPSLevel",
        "LobbyFPS",
        "MainCityFPS",
        // PUBG 4.x keys (primary in v4.6+)
        "UserSetFrameRate",
        "ExpectedFPSLevel",
        "CustormFrameRateLevel",   // sic — PUBG internal typo
        "BattleFrameRateLevel",
        "LobbyFrameRateLevel"
    };

    // --- Render quality keys ---
    // Verified against a real Active.sav (PUBG Mobile 4.x, 2026-10). Only names that
    // actually exist in the save file are listed here. The previous list contained
    // "BattleQuality", "LobbyQuality", "ShadowLevel", "AntiAliasingLevel" and
    // "UserSetGraphicsQuality", none of which exist in the file: those patches silently
    // no-op'd, which is why HDR/UHD appeared not to apply.
    // "LobbyQuality" was additionally harmful - it matched inside "nEnhancedLobbyQuality".
    public static readonly string[] QualityKeys = new[]
    {
        "BattleRenderQuality",
        "LobbyRenderQuality",
        "MainCityRenderQuality",
        "ManorRenderQuality",
        "nEnhancedLobbyQuality",
        // Graphic Favor quality targets
        "GFBestQBattle",
        "GFBestQLobby"
    };

    // --- Visual style keys (legacy + PUBG 4.x) ---
    // NOTE: bare "Style" was removed - it matched inside "BattleRenderStyle" and both
    // resolved to the same offset, so one overwrote the other.
    public static readonly string[] StyleKeys = new[]
    {
        "BattleRenderStyle",
        "LobbyRenderStyle",
        "MainCityRenderStyle"
    };

    public static readonly string[] GraphicFavorKeys = new[]
    {
        "GraphicFavor"
    };

    // --- PUBG 4.x user-set flags (must be 1 to prevent auto-override) ---
    public static readonly string[] UserFlagKeys = new[]
    {
        "bUserHasSetQuality",
        "bUserHasSetFrameRate"
    };

    // --- PUBG 4.x OverAllQuality (matches BattleQuality preset index: 1=Smooth, 2=Balanced, 3=HD, 4=HDR, 5=Ultra HD) ---
    public static readonly string[] OverAllQualityKeys = new[]
    {
        "OverAllQuality"
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

    #region Bundled Save Deployment

    /// <summary>
    /// Folder holding the known-good save files shipped alongside the executable.
    /// </summary>
    public static string BundledSaveDirectory
    {
        get
        {
            string baseDir = AppContext.BaseDirectory;

            // Published layout puts Assets\ next to the exe.
            string published = Path.Combine(baseDir, "Assets");
            if (Directory.Exists(published)) return published;

            // Debug/IDE runs have the working directory set to the output folder, so fall back
            // to walking up towards the project directory.
            DirectoryInfo? walk = new DirectoryInfo(baseDir);
            while (walk != null)
            {
                string candidate = Path.Combine(walk.FullName, "Assets");
                if (Directory.Exists(candidate)) return candidate;
                walk = walk.Parent;
            }

            return published;
        }
    }

    /// <summary>
    /// Finds every <c>*.sav</c> in the Assets folder. Adding a new preset file is therefore a
    /// matter of dropping it in - no code change required.
    /// </summary>
    public static IReadOnlyList<BundledSaveFile> DiscoverBundledSaves()
    {
        var results = new List<BundledSaveFile>();
        string dir = BundledSaveDirectory;

        if (!Directory.Exists(dir)) return results;

        foreach (string path in Directory.GetFiles(dir, "*.sav").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(path);
            results.Add(new BundledSaveFile
            {
                FileName = info.Name,
                FullPath = info.FullName,
                DisplayName = DescribeBundledSave(info.Name),
                Length = info.Length
            });
        }

        return results;
    }

    /// <summary>
    /// Turns a file name into a readable label:
    /// "Active_UHD_120FPS_v3.sav" becomes "Ultra HD - 120 FPS (v3)".
    /// </summary>
    public static string DescribeBundledSave(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);

        // Strip a leading "Active_" so the label reads as a settings preset rather than a filename.
        if (stem.StartsWith("Active_", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem["Active_".Length..];
        }

        // Trailing version marker, e.g. "..._v3".
        string version = string.Empty;
        int vIdx = stem.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        if (vIdx > 0 && int.TryParse(stem[(vIdx + 2)..], out int vNum))
        {
            version = $" (v{vNum})";
            stem = stem[..vIdx];
        }

        string[] tokens = stem.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var words = new List<string>();

        foreach (string t in tokens)
        {
            // "120fps" -> "120 FPS", "60fps" -> "60 FPS"
            if (t.EndsWith("fps", StringComparison.OrdinalIgnoreCase))
            {
                string number = t[..^3];
                words.Add(number.Length > 0 ? $"{number} FPS" : "FPS");
                continue;
            }

            string word = t.ToLowerInvariant() switch
            {
                "uhd" => "Ultra HD",
                "hd" => "HD",
                "hdr" => "HDR",
                "smooth" => "Smooth",
                "balanced" => "Balanced",
                _ => t
            };
            words.Add(word);
        }

        string label = string.Join(" ", words).Trim();
        if (label.Length == 0) label = stem;

        return label + version;
    }

    /// <summary>
    /// Deploys a bundled save file to the VM as Active.sav.
    ///
    /// Sequence: connect -> locate the running package -> back up the current device save ->
    /// validate the bundled file -> push -> pull back and confirm the bytes landed.
    ///
    /// The read-back is not optional. An earlier version of this code pushed and logged success
    /// without checking, which is how a run that wrote nothing still reported a green result.
    /// </summary>
    public static async Task<ActiveSavSyncResult> DeployBundledSaveAsync(BundledSaveFile save, GameLoopConfig gl)
    {
        var result = new ActiveSavSyncResult();

        if (!File.Exists(save.FullPath))
        {
            result.Success = false;
            result.Message = $"'{save.FileName}' was not found at {save.FullPath}.";
            return result;
        }

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
                result.Message = "Could not locate Active.sav in the VM. Ensure GameLoop is running with PUBG Mobile installed.";
                return result;
            }

            result.TargetPackage = pkg;

            // The game holds the save in memory and rewrites it on exit, so deploying while it is
            // running would be silently undone. Refuse rather than appear to succeed.
            string pidCheck = await AdbManager.ExecuteShellCommandAsync($"pidof {pkg}", config: gl);
            if (!string.IsNullOrWhiteSpace(pidCheck) && pidCheck.Trim().Length > 0)
            {
                result.Success = false;
                result.Message =
                    $"{pkg} is currently running and will overwrite the save when it closes. " +
                    "Close PUBG Mobile first, then deploy again.";
                Logger.Warn("ActiveSavService", result.Message);
                return result;
            }

            string remotePath = GetRemotePathForPackage(pkg);
            Directory.CreateDirectory(LocalStagingDirectory);
            string localCurrent = Path.Combine(LocalStagingDirectory, $"{pkg}_Active.sav");

            // Back up whatever is on the device right now, so this is always reversible.
            if (await AdbManager.PullFileFromVmAsync(remotePath, localCurrent, gl))
            {
                CreateBackupSnapshot(localCurrent, pkg);
            }

            // Validate the bundled file before it can overwrite a working save.
            byte[] payload = await File.ReadAllBytesAsync(save.FullPath);
            if (!LooksLikePubgSave(payload))
            {
                result.Success = false;
                result.Message =
                    $"'{save.FileName}' does not look like a valid PUBG save (missing the graphics fields) " +
                    "and was not deployed.";
                Logger.Error("ActiveSavService", result.Message);
                return result;
            }

            string localOutbound = Path.Combine(LocalStagingDirectory, $"{pkg}_deploy.sav");
            await File.WriteAllBytesAsync(localOutbound, payload);

            if (!await AdbManager.PushFileToVmAsync(localOutbound, remotePath, gl))
            {
                result.Success = false;
                result.Message =
                    $"Could not write {save.FileName} to the VM. The ADB connection is probably stale - " +
                    "close and reopen GameLoop, then try again.";
                Logger.Error("ActiveSavService", result.Message);
                return result;
            }

            // Independent confirmation: pull the file back and compare it byte for byte.
            string localVerify = Path.Combine(LocalStagingDirectory, $"{pkg}_deploy_verify.sav");
            if (!await AdbManager.PullFileFromVmAsync(remotePath, localVerify, gl))
            {
                result.Success = false;
                result.Message = "The file was pushed but could not be read back to confirm it.";
                return result;
            }

            byte[] written = await File.ReadAllBytesAsync(localVerify);
            if (!written.AsSpan().SequenceEqual(payload))
            {
                result.Success = false;
                result.Message =
                    $"Verification failed: the VM holds {written.Length} bytes but {save.FileName} is {payload.Length}. " +
                    "Something else overwrote the save.";
                Logger.Error("ActiveSavService", result.Message);
                return result;
            }

            result.Success = true;
            result.LocalStagedPath = localCurrent;
            result.Message =
                $"Deployed '{save.DisplayName}' ({save.FileName}, {payload.Length} bytes) to {pkg} and verified it on the device. " +
                "PUBG Mobile was NOT started - launch it yourself to see the change.";

            Logger.Success("ActiveSavService", result.Message);
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to deploy '{save.FileName}': {ex.Message}";
            Logger.Error("ActiveSavService", result.Message);
            return result;
        }
    }

    #endregion

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
                // Guard against matching INSIDE a longer identifier. Searching for
                // "LobbyQuality" previously matched "nEnhancedLobbyQuality" and overwrote an
                // unrelated property, while "Style" matched inside "BattleRenderStyle".
                // A real property name always starts after a non-identifier byte.
                if (i > 0 && IsIdentifierByte(buf[i - 1]))
                {
                    continue;
                }

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
            // Same word-boundary guard as the exact path above.
            if (i > 0 && IsIdentifierByte(buf[i - 1])) continue;

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
                // The character after the name must not continue an identifier either.
                int after = i + pattern.Length;
                if (after < buf.Length && IsIdentifierByte(buf[after]) && buf[after] != (byte)'\0')
                {
                    continue;
                }

                int nextOffset = after;
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

    /// <summary>
    /// True when the byte can be part of a property identifier (letters, digits, underscore).
    /// Used to ensure a key name only matches at a real identifier boundary.
    /// </summary>
    private static bool IsIdentifierByte(byte b)
    {
        return (b >= (byte)'a' && b <= (byte)'z') ||
               (b >= (byte)'A' && b <= (byte)'Z') ||
               (b >= (byte)'0' && b <= (byte)'9') ||
               b == (byte)'_';
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

        // FPS — try PUBG 4.x keys first, fall back to legacy
        if (TryReadInt(buf, "UserSetFrameRate", out int usFps)) profile.FpsLevel = usFps;
        else if (TryReadInt(buf, "BattleFPS", out int bFps)) profile.FpsLevel = bFps;
        else if (TryReadInt(buf, "FPSLevel", out int fps)) profile.FpsLevel = fps;
        else if (TryReadInt(buf, "CustormFrameRateLevel", out int cFps)) profile.FpsLevel = cFps;

        if (TryReadInt(buf, "LobbyFrameRateLevel", out int lfrl)) profile.LobbyFpsLevel = lfrl;
        else if (TryReadInt(buf, "LobbyFPS", out int lFps)) profile.LobbyFpsLevel = lFps;
        else profile.LobbyFpsLevel = profile.FpsLevel;

        // Render Quality — keys verified to exist in a real PUBG 4.x Active.sav.
        // These store 0-indexed values; convert back to the 1-indexed UI preset.
        if (TryReadInt(buf, "BattleRenderQuality", out int bq)) profile.BattleQuality = PubgQualityMapping.FromSaveQuality(bq);
        else if (TryReadInt(buf, "GFBestQBattle", out int gfq)) profile.BattleQuality = PubgQualityMapping.FromSaveQuality(gfq);

        if (TryReadInt(buf, "LobbyRenderQuality", out int lq)) profile.LobbyQuality = PubgQualityMapping.FromSaveQuality(lq);
        else if (TryReadInt(buf, "nEnhancedLobbyQuality", out int nelq)) profile.LobbyQuality = PubgQualityMapping.FromSaveQuality(nelq);
        else profile.LobbyQuality = profile.BattleQuality;

        // Style — keys verified to exist in a real PUBG 4.x Active.sav.
        if (TryReadInt(buf, "BattleRenderStyle", out int st)) profile.Style = st;
        else if (TryReadInt(buf, "LobbyRenderStyle", out int lst)) profile.Style = lst;

        // Graphic Favor
        if (TryReadInt(buf, "GraphicFavor", out int gf)) profile.GraphicFavor = gf;

        return profile;
    }

    public static int ApplyProfileToBytes(byte[] buf, ActiveSavProfile profile)
    {
        HealCorruptedIntPropertyHeaders(buf);
        int patchedCount = 0;

        // Track keys that were requested but are absent from this save file, so a partial
        // apply is never reported as a success. Previously PatchInt's false return was
        // discarded, which is how HDR/UHD appeared to "apply" while changing nothing.
        var missedKeys = new List<string>();

        // ─────────────────────────────────────────────────────────────────────────────
        // Only the fields below are written. A controlled before/after diff (2026-10-11),
        // changing ONLY the in-game Graphics preset, showed PUBG Mobile modifies exactly
        // three IntProperties and nothing else:
        //
        //     BattleRenderQuality, LobbyRenderQuality, GraphicFavor
        //
        // ManorRenderQuality, MainCityRenderQuality, nEnhancedLobbyQuality and the
        // GFBestQ* pair are managed per-scene by the game and are left untouched.
        // Earlier versions wrote them, which only made the fields disagree with each other.
        // ─────────────────────────────────────────────────────────────────────────────

        // Battle and Lobby quality always move together in a real save, so both are
        // written from the same value. The ladder (2=Smooth .. 6=Ultra HD) was measured
        // live on 2026-10-11.
        int saveQuality = PubgQualityMapping.ToSaveQuality(profile.BattleQuality);

        foreach (var key in QualityWriteKeys)
        {
            if (PatchInt(buf, key, saveQuality)) patchedCount++;
            else missedKeys.Add(key);
        }

        // Frame rate. Battle and Lobby FPS are written from the same choice by default, but
        // the caller can supply different values: they were observed as independent fields
        // (120 in battle, 90 in the lobby is a valid in-game combination).
        int saveBattleFps = PubgFpsMapping.ToSaveLevel(PubgFpsMapping.ToFps(profile.FpsLevel));
        int saveLobbyFps = PubgFpsMapping.ToSaveLevel(PubgFpsMapping.ToFps(profile.LobbyFpsLevel));

        if (PatchInt(buf, "BattleFPS", saveBattleFps)) patchedCount++; else missedKeys.Add("BattleFPS");
        if (PatchInt(buf, "LobbyFPS", saveLobbyFps)) patchedCount++; else missedKeys.Add("LobbyFPS");

        // Visual style, written from a single value to both fields.
        foreach (var key in StyleWriteKeys)
        {
            if (PatchInt(buf, key, profile.Style)) patchedCount++;
            else missedKeys.Add(key);
        }

        // GraphicFavor: the game writes 4 whenever a preset is chosen. It must be written
        // too, otherwise the game can treat the save as a stale/custom profile.
        if (PatchInt(buf, "GraphicFavor", PubgQualityMapping.GraphicFavorPreset)) patchedCount++;
        else missedKeys.Add("GraphicFavor");

        LastMissedKeys = missedKeys;
        if (missedKeys.Count > 0)
        {
            Logger.Warn("ActiveSavService",
                $"{missedKeys.Count} required setting(s) were not found in this save file: {string.Join(", ", missedKeys)}");
        }

        return patchedCount;
    }

    /// <summary>
    /// Keys requested by the most recent <see cref="ApplyProfileToBytes"/> call that were not
    /// found in the save file. Surfaced to the caller so partial application is visible.
    /// </summary>
    public static IReadOnlyList<string> LastMissedKeys { get; private set; } = Array.Empty<string>();

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

    /// <summary>
    /// Sanity check for a pulled save file.
    ///
    /// A genuine PUBG Mobile Active.sav always carries the battle and lobby quality fields
    /// together - verified against real saves, which contain all seven graphics fields. A file
    /// missing them is either a partial write (the game was saving when we pulled) or belongs
    /// to a different game version. Writing such a file back would replace a working save with
    /// one the game cannot read.
    /// </summary>
    public static bool LooksLikePubgSave(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 1024) return false;

        bool battleQuality = FindIntPropertyOffset(buffer, "BattleRenderQuality") >= 0;
        bool battleFps = FindIntPropertyOffset(buffer, "BattleFPS") >= 0;

        // Both must be present: that is the minimum signature of a real save, and it is what
        // was already true in every real capture taken on 2026-10-11.
        return battleQuality && battleFps;
    }

    /// <summary>
    /// Reads the graphics/FPS caps GameLoop currently enforces, so the UI can warn when the
    /// emulator is set below the tier the user asked for.
    /// </summary>
    public static (int Quality, int Fps) ReadEmulatorGraphicsCaps()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            int quality = key?.GetValue("com.tencent.ig_RenderQuality") is int q ? q : -1;
            int fps = key?.GetValue("com.tencent.ig_FPSLevel") is int f ? f : -1;
            return (quality, fps);
        }
        catch
        {
            return (-1, -1);
        }
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

            // NOTE: GameLoop's own graphics/FPS cap is deliberately NOT written from here.
            //
            // GameLoop caches its settings and enforces them from its Settings Center, so a
            // direct registry write is unreliable and can be overwritten by the emulator on
            // its next start. The user sets the cap in GameLoop's own Settings Center
            // (Game Settings > Game Quality) instead, and the UI warns them when that cap
            // is below the tier they selected here.
            //
            // This cap is the reason HDR/Ultra HD appeared to do nothing: with GameLoop set to
            // HD, the emulator capped the game and rewrote the save on exit. Verified live on
            // 2026-10-11 - raising the cap in GameLoop made the patch hold.

            // 1. The game must be stopped so the UE4 in-memory cache cannot overwrite the save
            //    while it is being patched. It is intentionally left stopped afterwards: the
            //    user launches it themselves so the applied setting can be verified.
            string pidCheck = await AdbManager.ExecuteShellCommandAsync($"pidof {pkg}", config: gl);
            bool wasRunning = !string.IsNullOrWhiteSpace(pidCheck) && pidCheck.Trim().Length > 0;
            if (wasRunning)
            {
                Logger.Info("ActiveSavService", $"Stopping {pkg} so its memory cache cannot overwrite the save being patched...");
                await AdbManager.ExecuteShellCommandAsync($"am force-stop {pkg}", config: gl);
                // Wait for the process to actually die before touching the save files.
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(300);
                    string after = await AdbManager.ExecuteShellCommandAsync($"pidof {pkg}", config: gl);
                    if (string.IsNullOrWhiteSpace(after) || after.Trim().Length == 0) break;
                }
            }
            else
            {
                Logger.Info("ActiveSavService", $"{pkg} is not running; proceeding with the save patch.");
            }

            // Purge temporary shaders, remote config caches, and logs so cached resolution/graphics state doesn't overwrite settings
            await AdbManager.ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/cache/*", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Logs/*", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/LightData/*", config: gl);

            // PUBG 4.x: Clear SharedPreferences graphics cache (the game caches quality/FPS state here and restores on launch)
            await AdbManager.ExecuteShellCommandAsync($"rm -f /data/data/{pkg}/shared_prefs/com.tencent.ig.v2.playerprefs.xml", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -f /data/data/{pkg}/shared_prefs/*graphics*.xml", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -f /data/data/{pkg}/shared_prefs/*quality*.xml", config: gl);
            await AdbManager.ExecuteShellCommandAsync($"rm -f /data/data/{pkg}/shared_prefs/*UserSetting*.xml", config: gl);
            Logger.Info("ActiveSavService", "Purged SharedPreferences graphics cache (PUBG 4.x anti-override bypass).");

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

            byte[] activeBytes = await File.ReadAllBytesAsync(localActiveSav);

            // Refuse to write back a file that does not look like a PUBG save.
            //
            // A save that is missing the quality fields belongs to a different game version
            // or is a partial write. Pushing it would replace the real save with a file the
            // game cannot read, and the previous in-game settings would be lost.
            if (!LooksLikePubgSave(activeBytes))
            {
                result.Success = false;
                result.Message =
                    $"The pulled Active.sav ({activeBytes.Length} bytes) does not contain the expected PUBG graphics " +
                    "fields, so it was not written back. This usually means the game is mid-save or the file is from " +
                    "an incompatible version. Close PUBG fully and retry.";
                Logger.Error("ActiveSavService", result.Message);
                return result;
            }

            CreateBackupSnapshot(localActiveSav, pkg);
            int patchedFields = ApplyProfileToBytes(activeBytes, profile);
            await File.WriteAllBytesAsync(localActiveSav, activeBytes);

            if (!await AdbManager.PushFileToVmAsync(localActiveSav, remoteActiveSav, gl))
            {
                // The old code discarded this bool and logged the patch as applied, which is how
                // a run that silently wrote nothing still ended in a green success message.
                result.Success = false;
                result.Message =
                    $"Patched {patchedFields} field(s) locally but the write to {pkg} failed, so nothing was changed on the device. " +
                    "The ADB connection is probably stale - close and reopen GameLoop, then run the sync again.";
                Logger.Error("ActiveSavService", result.Message);
                return result;
            }

            Logger.Info("ActiveSavService", $"Patched {patchedFields} IntProperty fields in Active.sav.");

            // 3. SettingConfig_Slot.sav is deliberately NOT written.
            //
            // Verified live on 2026-10-11. The game reads graphics settings from Active.sav ONLY.
            // The experiment: Active.sav was set to quality 6 (Ultra HD) while SettingConfig_Slot.sav
            // was left holding a stale quality 8. After launch the in-game menu showed Ultra HD, and
            // the game had WRITTEN quality 6 back into Active.sav - it adopted our value - while
            // SettingConfig_Slot.sav was never read and still held the stale 8.
            //
            // This contradicts the earlier conclusion in this file that "both files are required",
            // which was itself based on a contaminated test (the game was still running during it).
            // Writing the slot file is not merely unnecessary, it is actively harmful: a stale value
            // there competes with the one we just wrote and was a contributing cause of the settings
            // appearing not to apply.

            // 3b. Active_Result.sav is deliberately NOT written either.
            //
            // Same reasoning as the slot file. It was labelled a "redundant persistence layer in
            // v4.x+" but that was never verified against a real launch. Since the game demonstrably
            // reads Active.sav alone, writing this file has the same downside as the slot file -
            // a stale value that competes with the real one - and no demonstrated upside.

            // 4. UserCustom.ini and GameUserSettings.ini are deliberately NOT written.
            //
            // ROOT CAUSE (found 2026-10-11, fixed after the "settings don't apply in the app but
            // work when I patch by hand" report). The two ladders are different:
            //
            //   Active.sav BattleRenderQuality : 2..6   (2=Smooth 3=Balanced 4=HD 5=HDR 6=UltraHD)
            //   r.UserQualitySetting           : 0..4   (the game's own CVar scale)
            //
            // Proof the CVar scale is 0-based: a live UserCustom.ini pulled from the device on
            // 2026-10-11 contains r.UserQualitySetting=0 in [UserCustom DeviceProfile] and
            // r.UserQualitySetting=2 in [BackUp DeviceProfile]. A ladder that started at 2 could
            // never emit 0.
            //
            // The old SyncUserCustomIniAsync wrote ToSaveQuality() - i.e. the 2..6 save scale -
            // into r.UserQualitySetting / r.PUBGQualityLevel / r.PUBGMaxSupportQualityLevel.
            // So the engine CVar layer contradicted the save layer by exactly two tiers, and the
            // engine won: the user picked HDR, the save said 5, the CVar said 5 on a 0..4 ladder,
            // and the change never appeared.
            //
            // That is precisely why patching the .sav files by hand worked (no CVar was ever
            // touched) while the app never did. Patching the two save files is the verified path;
            // the ini files are now left completely alone.

            // 5. Trigger Android VM 120 FPS unlock props & sync high-refresh device profile
            await AdbManager.Unlock120FpsAsync(gl);
            var devProfile = deviceProfile ?? DeviceProfile.Profiles.FirstOrDefault(p => p.MaxSupportedFps >= 120) ?? DeviceProfile.Profiles.First();
            await AdbManager.SpoofDeviceProfileAsync(devProfile, gl);

            // Per user directive: Do NOT touch GameLoop Windows Registry when applying in-game settings.
            // The user configures GameLoop emulator settings manually in GameLoop's own settings center.

                        // 6. Do NOT relaunch PUBG Mobile.
            //
            // The game is deliberately left stopped. Auto-relaunching used to fire a
            // `monkey -p <pkg> ... LAUNCHER` command here, which meant the game started
            // immediately, rewrote the save files on exit, and left the user with no chance to
            // confirm the setting had actually applied. The user launches the game themselves
            // so the result can be checked in the Graphics menu.

            result.LocalStagedPath = localActiveSav;
            result.CurrentProfile = profile;
            result.Success = true;
            result.Message =
                $"Applied {profile.Name} to {pkg} " +
                $"({ActiveSavProfile.GetFpsLabel(profile.FpsLevel)}, {ActiveSavProfile.GetQualityLabel(profile.BattleQuality)}, " +
                $"{ActiveSavProfile.GetStyleLabel(profile.Style)}). PUBG Mobile was closed and has NOT been restarted - " +
                "launch it yourself to confirm the setting, and do not press Save inside the in-game Graphics menu.";
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

    /// <summary>
    /// Synchronizes GameLoop's Windows registry values to match the injected profile.
    /// This prevents GameLoop's host emulator daemon from forcing HD (quality 2) on boot.
    /// </summary>
    public static void SyncGameLoopWindowsRegistry(ActiveSavProfile profile, GameLoopConfig gl)
    {
        try
        {
            int cvarQuality = Math.Clamp(profile.BattleQuality - 1, 0, 4);
            int targetMaxFps = profile.FpsLevel switch
            {
                >= 7 => 120,
                6 => 90,
                5 => 60,
                4 => 40,
                _ => 30
            };
            // CRITICAL: User's FPS selection is NEVER clamped.
            // Previously this line capped >=90 to 90, silently downgrading 120→90.
            int registryFps = targetMaxFps;
            int scale = profile.BattleQuality <= 1 ? 1 : 2;

            Logger.Info("ActiveSavService", $"[FPS] Syncing registry: Profile FPS Level={profile.FpsLevel}, Target FPS={targetMaxFps}, Registry FPS={registryFps} (NO clamp)");

            var targetPaths = new[]
            {
                @"Software\Tencent\MobileGamePC",
                @"Software\Tencent\TxGameAssistant"
            };

            var packages = new[]
            {
                "com.tencent.ig",
                "com.pubg.krmobile",
                "com.pubg.imobile",
                "com.vng.pubgmobile"
            };

            foreach (var path in targetPaths)
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path);
                if (key != null)
                {
                    foreach (var pkg in packages)
                    {
                        key.SetValue($"{pkg}_RenderQuality", cvarQuality, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue($"{pkg}_FPSLevel", registryFps, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue($"{pkg}_ContentScale", scale, Microsoft.Win32.RegistryValueKind.DWord);
                    }

                    // Readback verification for primary package
                    var readbackFps = key.GetValue("com.tencent.ig_FPSLevel");
                    int verifiedFps = readbackFps is int i ? i : -1;
                    if (verifiedFps == registryFps)
                        Logger.Success("ActiveSavService", $"[FPS] Readback verified: {verifiedFps} at HKCU\\{path}");
                    else
                        Logger.Warn("ActiveSavService", $"[FPS] Readback MISMATCH: Wrote {registryFps} but read {verifiedFps} at HKCU\\{path}");
                }
            }

            gl.PubgRenderQuality = cvarQuality;
            gl.PubgFpsLevel = registryFps;
            gl.PubgContentScale = scale;
            Logger.Success("ActiveSavService", $"Synchronized GameLoop registry: RenderQuality={cvarQuality}, FPSLevel={registryFps}, ContentScale={scale}.");
        }
        catch (Exception ex)
        {
            Logger.Warn("ActiveSavService", $"Failed to sync GameLoop registry: {ex.Message}");
        }
    }

    #endregion
}
