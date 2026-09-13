namespace GameLoopOptimizer.Models;

/// <summary>
/// PUBG Mobile graphics/FPS compatibility helper.
/// 
/// Per user directive: ALL graphics/FPS combinations are treated as valid.
/// The user's selection always takes priority — the optimizer never refuses
/// or downgrades a combination just because the game might natively restrict it.
/// 
/// With device spoofing (e.g., ROG Phone 6 Pro, Galaxy Tab S9 Ultra),
/// PUBG Mobile unlocks combinations that are otherwise restricted on stock devices.
/// Since this optimizer always uses device spoofing, all combinations are valid.
/// </summary>
public static class PubgGraphicsCompatibility
{
    /// <summary>
    /// All supported FPS values that can be written to the GameLoop registry.
    /// </summary>
    public static readonly int[] SupportedFpsValues = { 30, 40, 60, 90, 120 };

    /// <summary>
    /// Graphics quality levels (registry value → display name).
    /// Registry uses 0-indexed values; Active.sav uses 1-indexed.
    /// </summary>
    public static readonly Dictionary<int, string> QualityLevels = new()
    {
        { 0, "Smooth (流畅)" },
        { 1, "Balanced (均衡)" },
        { 2, "HD (高清)" },
        { 3, "HDR (高动态)" },
        { 4, "Ultra HD (超高清)" },
        { 5, "UHD / Ultra HDR (极致超高清)" }
    };

    /// <summary>
    /// Content scale levels.
    /// </summary>
    public static readonly Dictionary<int, string> ContentScaleLevels = new()
    {
        { 1, "720P SD" },
        { 2, "1080P HD" },
        { 3, "2K QHD" }
    };

    /// <summary>
    /// All combinations are valid per user directive. Returns true for any supported values.
    /// </summary>
    public static bool IsValidCombination(int quality, int fps)
    {
        bool validQuality = quality >= 0 && quality <= 5;
        bool validFps = Array.Exists(SupportedFpsValues, f => f == fps);
        return validQuality && validFps;
    }

    /// <summary>
    /// Returns a descriptive note about a combination. Never blocks application.
    /// </summary>
    public static string GetCombinationNote(int quality, int fps)
    {
        if (!IsValidCombination(quality, fps))
        {
            return fps > 120 || fps < 30
                ? $"FPS value {fps} is outside the supported range (30-120)."
                : $"Graphics quality {quality} is outside the supported range (0-5).";
        }

        // Informational notes (non-blocking)
        if (quality >= 3 && fps >= 120)
        {
            string qualityName = QualityLevels.GetValueOrDefault(quality, $"Quality {quality}");
            return $"{qualityName} + {fps} FPS is an advanced combination. " +
                   "Enabled via device fingerprint spoofing.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Returns the display name for a graphics quality level.
    /// </summary>
    public static string GetQualityName(int quality)
        => QualityLevels.GetValueOrDefault(quality, $"Quality {quality}");

    /// <summary>
    /// Returns the display name for a content scale level.
    /// </summary>
    public static string GetContentScaleName(int scale)
        => ContentScaleLevels.GetValueOrDefault(scale, $"Scale {scale}");

    /// <summary>
    /// Converts an Active.sav FPS level (1-8) to the registry FPS integer.
    /// </summary>
    public static int ActiveSavFpsLevelToRegistryFps(int activeSavLevel) => activeSavLevel switch
    {
        >= 7 => 120,
        6 => 90,
        5 => 60,
        4 => 40,
        _ => 30
    };
}
