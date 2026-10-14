namespace GameLoopOptimizer.Core;

/// <summary>
/// A single graphics/FPS choice expressed in BOTH scales PUBG Mobile uses, so they can never
/// disagree.
///
/// WHY TWO SCALES EXIST (discovered 2026-10-11)
/// -------------------------------------------
/// A graphics preset has to be written to two independent places:
///
///  1. The GameLoop emulator registry
///     HKCU\Software\Tencent\MobileGamePC\com.tencent.ig_RenderQuality
///     Scale: 0 = Smooth, 1 = Balanced, 2 = HD, 3 = HDR, 4 = Ultra HD
///     This is what GameLoop's own Settings Center shows and enforces. It is a CAP: if the
///     registry is left at HD (2), the game can never be pushed above HD no matter what the
///     save file says.
///
///  2. The PUBG Mobile save file (Active.sav)
///     Field: BattleRenderQuality / LobbyRenderQuality
///     Scale: 2 = Smooth, 3 = Balanced, 4 = HD, 5 = HDR, 6 = Ultra HD
///
///     VERIFIED IN-GAME on 2026-10-11 against this build:
///       2 = Smooth    - measured by selecting it in the in-game menu
///       3 = Balanced  - measured
///       4 = HD        - measured
///       6 = Ultra HD  - measured. Patched into Active.sav; the game adopted it, displayed
///                        Ultra HD on the next launch, and rewrote 6 back into the file.
///       8 = INVALID   - clamped to Smooth. The file retained the 8 but the menu showed Smooth,
///                        which is what proves retention alone is not proof of application.
///       5 = presumed HDR, NOT yet confirmed in-game.
///
///     The ladder is neither 0-based nor 1-based, which is why the widely published 1-5 values
///     produce a tier below what the user asked for.
///
/// WHICH FILE THE GAME ACTUALLY READS
/// ----------------------------------
/// Active.sav ONLY. Verified on 2026-10-11: with Active.sav at quality 6 and SettingConfig_Slot.sav
/// left holding a stale 8, the game displayed Ultra HD and wrote 6 back into Active.sav. The slot
/// file was never read. Writing it is therefore not merely unnecessary but harmful, because a
/// stale value there competes with the real one. See ActiveSavService.PushActiveSavProfileAsync.
///
/// The offset between the registry and the save is exactly 2:
///     registry 0 (Smooth) <-> save 2
///     registry 1 (Balanced) <-> save 3
///     registry 2 (HD)       <-> save 4
///     registry 3 (HDR)      <-> save 5
///     registry 4 (Ultra HD) <-> save 6
///
/// THE GAME'S OWN CVar LAYER
/// -------------------------
/// UserCustom.ini also carries r.UserQualitySetting, on a third, 0-based ladder (0..4).
/// Proof it starts at 0: a live UserCustom.ini contained r.UserQualitySetting=0 in
/// [UserCustom DeviceProfile] and 2 in [BackUp DeviceProfile]. A ladder starting at 2 could not
/// emit 0. This app deliberately does NOT write that file - see the comment in ActiveSavService -
/// because putting the save-scale value into this 0-based CVar is what made earlier attempts
/// appear to do nothing.
/// </summary>
public sealed record GraphicsSelection
{
    /// <summary>User-facing preset, 1..5 (Smooth..Ultra HD).</summary>
    public required int Preset { get; init; }

    /// <summary>True for the HDR and Ultra HD tiers.</summary>
    public required bool IsHighDynamicRange { get; init; }
}

public static class PubgQualityMapping
{
    // ── User-facing preset (matches the app UI order, 1-based) ──
    public const int Smooth = 1;
    public const int Balanced = 2;
    public const int Hd = 3;
    public const int Hdr = 4;
    public const int UltraHd = 5;

    public const int MinPreset = Smooth;
    public const int MaxPreset = UltraHd;

    public const int TierCount = 5;

    /// <summary>
    /// GraphicFavor value the game writes whenever a preset is selected. Verified live across
    /// the Smooth, Balanced and HD captures.
    /// </summary>
    public const int GraphicFavorPreset = 4;

    /// <summary>
    /// Value written to the GameLoop registry
    /// (com.tencent.ig_RenderQuality). Scale: 0 = Smooth .. 4 = Ultra HD.
    /// </summary>
    public static int ToRegistryQuality(int userPreset)
        => Math.Clamp(userPreset, MinPreset, MaxPreset) - 1;

    /// <summary>
    /// Value written to Active.sav (BattleRenderQuality, LobbyRenderQuality).
    /// Scale: 2 = Smooth .. 6 = Ultra HD. Verified in-game against this build; see the remarks
    /// at the top of this file. Values above 6 are not valid - 8 was observed to be retained in
    /// the file yet clamped to Smooth by the engine.
    /// </summary>
    public static int ToSaveQuality(int userPreset)
        => Math.Clamp(userPreset, MinPreset, MaxPreset) + 1;

    /// <summary>Converts a value read back from the save file to the user preset.</summary>
    public static int FromSaveQuality(int saveValue)
        => Math.Clamp(saveValue - 1, MinPreset, MaxPreset);

    /// <summary>Converts a value read back from the registry to the user preset.</summary>
    public static int FromRegistryQuality(int registryValue)
        => Math.Clamp(registryValue + 1, MinPreset, MaxPreset);

    /// <summary>True when a save value is one this build is known to write.</summary>
    public static bool IsKnownSaveValue(int saveValue)
        => saveValue >= ToSaveQuality(MinPreset) && saveValue <= ToSaveQuality(MaxPreset);

    /// <summary>True for the HDR and Ultra HD tiers.</summary>
    public static bool IsHighDynamicRange(int userPreset)
        => userPreset >= Hdr;

    /// <summary>Human-readable name for a user preset.</summary>
    public static string Describe(int userPreset) => userPreset switch
    {
        Smooth => "Smooth",
        Balanced => "Balanced",
        Hd => "HD",
        Hdr => "HDR",
        UltraHd => "Ultra HD",
        _ => $"Quality {userPreset}"
    };

    /// <summary>Returns both representations of a preset, for callers that must write both.</summary>
    public static GraphicsSelection DescribeSelection(int userPreset)
    {
        int preset = Math.Clamp(userPreset, MinPreset, MaxPreset);
        return new GraphicsSelection
        {
            Preset = preset,
            IsHighDynamicRange = preset >= Hdr
        };
    }
}

/// <summary>
/// Frame-rate values for both the GameLoop registry and the PUBG Mobile save file.
///
/// STATUS
/// ------
/// Save-file values (BattleFPS / LobbyFPS) were MEASURED live on 2026-10-11:
///   8 = 120 FPS, 7 = 90 FPS  - confirmed by setting 120-Battle / 90-Lobby in game
/// and the ladder extends to 6 = 60 FPS.
///
/// The REGISTRY value is a different scale. GameLoop's own Settings Center (screenshot
/// 2026-10-11) offers only Default / 30 / 40 / 60 / 90 - it has no 120 option at all, and its
/// values are the plain frame numbers, not a 0-based level index. That registry scale is
/// therefore NOT yet verified and must be confirmed before the app writes it, otherwise the
/// emulator will clamp the save back down - the same failure that produced "HDR shows
/// Balanced" when only the save file was patched.
/// </summary>
public static class PubgFpsMapping
{
    // ── Save-file levels (measured) ──
    public const int SaveLevel60 = 6;
    public const int SaveLevel90 = 7;
    public const int SaveLevel120 = 8;

    public const int MinSaveLevel = SaveLevel60;
    public const int MaxSaveLevel = SaveLevel120;

    /// <summary>The only frame rates this app offers.</summary>
    public static IReadOnlyList<int> SupportedFrameRates { get; } = new[] { 60, 90, 120 };

    /// <summary>Converts an FPS value to the level stored in the save file.</summary>
    public static int ToSaveLevel(int fps) => fps switch
    {
        >= 100 => SaveLevel120,
        >= 75 => SaveLevel90,
        _ => SaveLevel60
    };

    /// <summary>Converts a save-file level back to the FPS value it represents.</summary>
    public static int ToFps(int saveLevel) => saveLevel switch
    {
        >= SaveLevel120 => 120,
        SaveLevel90 => 90,
        _ => 60
    };

    /// <summary>True when a save value is one this build is known to write.</summary>
    public static bool IsKnownSaveLevel(int saveLevel)
        => saveLevel >= MinSaveLevel && saveLevel <= MaxSaveLevel;

    /// <summary>Human-readable label for a save-file level.</summary>
    public static string Describe(int saveLevel) => saveLevel switch
    {
        SaveLevel120 => "120 FPS",
        SaveLevel90 => "90 FPS",
        SaveLevel60 => "60 FPS",
        _ => $"{saveLevel} (unrecognised)"
    };
}