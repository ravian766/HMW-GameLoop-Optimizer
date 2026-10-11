namespace GameLoopOptimizer.Core;

/// <summary>
/// Single source of truth for translating a user-facing graphics preset into the
/// numeric values PUBG Mobile actually reads.
///
/// WHY THIS EXISTS
/// ---------------
/// The app's UI and presets are 1-indexed (1 = Smooth ... 5 = Ultra HD), because that
/// matches the "Battle Quality" preset list players see. But PUBG Mobile's engine
/// enums in Active.sav and in the UserCustom.ini CVars are 0-indexed. Writing the
/// 1-indexed value straight through therefore applied the WRONG PRESET: selecting
/// HDR (4) was interpreted by the game as Balanced, and Ultra HD (5) as HD.
///
/// Verified against the game's own save data: a naturally game-written Active.sav
/// contained BattleRenderQuality = 3 and LobbyRenderQuality = 4, i.e. 0-indexed
/// values, and community reference configs use r.UserQualitySetting = 0 for Smooth.
///
/// Different keys also use DIFFERENT scales, which is why they cannot simply all be
/// "preset - 1":
///
///   Active.sav render-quality keys : 0-indexed quality ladder (0 = Smooth)
///   r.UserQualitySetting           : 0-indexed quality ladder (0 = Smooth)
///   r.PUBGQualityLevel             : 0-indexed quality ladder (0 = Smooth)
///   r.UserHDRSetting               : 3-value HDR enum (0 = SDR, 1 = HDR, 2 = Ultra HD)
///   r.ACESStyle / BattleRenderStyle: 1-indexed style ladder (1 = Classic)
/// </summary>
public static class PubgQualityMapping
{
    // ── User-facing preset (1-indexed, matches the app UI) ──
    public const int Smooth = 1;
    public const int Balanced = 2;
    public const int Hd = 3;
    public const int Hdr = 4;
    public const int UltraHd = 5;

    // ── r.UserHDRSetting values (its own 3-value enum) ──
    public const int HdrSettingSdr = 0;
    public const int HdrSettingHdr = 1;
    public const int HdrSettingUltraHd = 2;

    /// <summary>
    /// Converts a 1-indexed user preset into the 0-indexed value that
    /// Active.sav render-quality keys and r.UserQualitySetting expect.
    /// Out-of-range input is clamped to the valid 0-4 band.
    /// </summary>
    public static int ToEngineQuality(int userPreset) => Math.Clamp(userPreset - 1, 0, 4);

    /// <summary>
    /// Converts a 0-indexed engine value back to the 1-indexed user preset, for
    /// reading the current setting back out of a save file.
    /// </summary>
    public static int FromEngineQuality(int engineValue) => Math.Clamp(engineValue + 1, 1, 5);

    /// <summary>
    /// Maps a user preset to the r.UserHDRSetting enum.
    /// Smooth / Balanced / HD are SDR; HDR = 1; Ultra HD = 2.
    /// </summary>
    public static int ToHdrSetting(int userPreset) => userPreset switch
    {
        >= UltraHd => HdrSettingUltraHd,
        Hdr => HdrSettingHdr,
        _ => HdrSettingSdr
    };

    /// <summary>
    /// Whether r.MobileHDR should be enabled (1.0) for this preset.
    /// </summary>
    public static double ToMobileHdr(int userPreset) => userPreset >= Hdr ? 1.0 : 0.0;

    /// <summary>
    /// r.PUBGMaxSupportQualityLevel - the highest quality the device profile claims to
    /// support. Must be at least as high as the requested quality or the game silently
    /// downgrades the preset. This is the "dominant" setting per the community docs.
    /// </summary>
    public static int ToMaxSupportQuality(int userPreset) => Math.Clamp(userPreset - 1, 0, 4);

    /// <summary>
    /// True when the preset is one of the high-fidelity HDR modes, which the game only
    /// exposes when Graphics is at HD or above.
    /// </summary>
    public static bool RequiresHdrTier(int userPreset) => userPreset >= Hdr;

    /// <summary>
    /// Human-readable name for a user preset (1-indexed).
    /// </summary>
    public static string Describe(int userPreset) => userPreset switch
    {
        Smooth => "Smooth",
        Balanced => "Balanced",
        Hd => "HD",
        Hdr => "HDR",
        UltraHd => "Ultra HD",
        _ => $"Quality {userPreset}"
    };
}