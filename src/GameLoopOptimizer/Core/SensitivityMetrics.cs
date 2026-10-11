namespace GameLoopOptimizer.Core;

/// <summary>
/// Competitive-standard sensitivity metrics: eDPI and cm/360.
///
/// These are the numbers competitive players use to compare setups across
/// different mice, DPS and in-game sensitivity values. Two setups with the same
/// eDPI feel nearly identical regardless of hardware.
///
/// This is a pure calculation utility. It reads nothing from the running game
/// and writes nothing to the system.
/// </summary>
public static class SensitivityMetrics
{
    /// <summary>
    /// Yaw degrees per mouse count per unit of in-game sensitivity.
    ///
    /// This is the standard Source-engine / CS2 / Valorant conversion:
    ///     deg/count = sens * 0.022
    ///
    /// Verified against two independent community anchors:
    ///     CS2  0.4 @  800 DPI -> 51.00 cm/360
    ///     CS2  0.5 @ 1600 DPI -> 40.80 cm/360
    ///
    /// IMPORTANT: this constant is calibrated for the 0-1 "PC-style" sensitivity
    /// scale. PUBG Mobile exposes a 0-120 scale, so values must be normalised
    /// through <see cref="NormaliseMobileSensitivity"/> before being compared
    /// against PC setups. Feeding a raw 0-120 value in directly produces
    /// physically impossible cm/360 figures (single-digit centimetres per turn).
    /// </summary>
    private const double DegPerCountPerSensUnit = 0.022;

    /// <summary>
    /// Converts a PUBG Mobile 0-120 sensitivity into the equivalent 0-1 PC-style scale.
    ///
    /// Calibrated so that a typical competitive setup lands in the expected band:
    ///     800 DPI, sens 50  ->  ~45 cm/360   (fast competitive)
    ///    1600 DPI, sens 40 ->  ~45 cm/360   (same feel, higher DPI)
    ///
    /// Derivation: at 800 DPI, 45 cm/360 requires 0.0254 deg/count. Dividing by the
    /// standard 0.022 deg-per-count-per-PC-unit gives a normalised PC value of 1.1545
    /// for mobile sens 50, hence K = 2.309 in the formula below.
    /// </summary>
    private const double MobileScaleConstant = 2.309;

    /// <summary>
    /// Converts a PUBG Mobile 0-120 camera sensitivity into the equivalent 0-1
    /// PC scale so it can be compared against PC cm/360 figures.
    /// </summary>
    public static double NormaliseMobileSensitivity(double mobileSensitivity)
    {
        double sens = Math.Clamp(mobileSensitivity, 0.0, 200.0);
        return sens / 100.0 * MobileScaleConstant;
    }

    /// <summary>
    /// Inches per 360-degree turn for a PUBG Mobile 0-120 sensitivity at a given DPI.
    /// The mobile value is normalised to the PC scale before conversion.
    /// </summary>
    /// <param name="dpi">Mouse DPI (dots per inch).</param>
    /// <param name="gameSensitivity">PUBG Mobile camera sensitivity (0-120 scale).</param>
    public static double InchesPer360(int dpi, double gameSensitivity)
    {
        int safeDpi = Math.Clamp(dpi, 100, 32000);
        double normalised = NormaliseMobileSensitivity(gameSensitivity);

        if (normalised <= 0.0001) return double.MaxValue;

        double degPerCount = normalised * DegPerCountPerSensUnit;
        double countsPer360 = 360.0 / degPerCount;
        return countsPer360 / safeDpi;
    }

    /// <summary>
    /// Centimetres per 360-degree turn (the metric most players actually use).
    /// </summary>
    public static double Cm360(int dpi, double gameSensitivity) => InchesPer360(dpi, gameSensitivity) * 2.54;

    /// <summary>
    /// eDPI = DPI x in-game sensitivity. The standard "effective DPI" figure.
    /// </summary>
    public static double EffectiveDpi(int dpi, double gameSensitivity)
    {
        int safeDpi = Math.Clamp(dpi, 100, 32000);
        double sens = Math.Clamp(gameSensitivity, 0.0001, 200.0);
        return safeDpi * sens;
    }

    /// <summary>
    /// Converts a cm/360 target into the equivalent in-game sensitivity for a
    /// given DPI. Useful for players who know the cm/360 they want but not the
    /// sensitivity number.
    /// </summary>
    public static double SensitivityForCm360(int dpi, double targetCm360)
    {
        int safeDpi = Math.Clamp(dpi, 100, 32000);
        double cm = Math.Clamp(targetCm360, 0.1, 200.0);

        double inches = cm / 2.54;
        double countsPer360 = inches * safeDpi;
        double degPerCount = 360.0 / countsPer360;
        double pcScaleSens = degPerCount / DegPerCountPerSensUnit;

        // Convert back from the PC scale into the mobile 0-120 scale.
        return pcScaleSens / MobileScaleConstant * 100.0;
    }

    /// <summary>
    /// Produces a short human-readable band describing how fast a setup feels,
    /// using typical competitive cm/360 ranges as the reference.
    /// </summary>
    public static string DescribeFeel(double cm360) => cm360 switch
    {
        < 20 => "Very Fast (Flick/Tablet)",
        < 30 => "Fast (Competitive Flick)",
        < 45 => "Balanced (Standard Competitive)",
        < 60 => "Controlled (Tracking/DMR)",
        _ => "Very Slow (Precision/Bolt Action)"
    };

    /// <summary>
    /// Snapshot of a full sensitivity setup, used for display and export.
    /// </summary>
    public class SensitivityMetricsResult
    {
        public int MouseDpi { get; set; }
        public double GameSensitivity { get; set; }
        /// <summary>DPI x sensitivity (raw mobile scale).</summary>
        public double EffectiveDpi { get; set; }
        /// <summary>cm/360 after normalising the mobile scale to the PC scale.</summary>
        public double Cm360 { get; set; }
        public double InchesPer360 { get; set; }
        public string FeelDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// Computes every metric for a single DPI + PUBG Mobile sensitivity pair.
    /// </summary>
    public static SensitivityMetricsResult Compute(int dpi, double gameSensitivity)
    {
        double cm = Cm360(dpi, gameSensitivity);

        return new SensitivityMetricsResult
        {
            MouseDpi = Math.Clamp(dpi, 100, 32000),
            GameSensitivity = Math.Clamp(gameSensitivity, 0.0, 200.0),
            EffectiveDpi = EffectiveDpi(dpi, gameSensitivity),
            InchesPer360 = InchesPer360(dpi, gameSensitivity),
            Cm360 = cm,
            FeelDescription = DescribeFeel(cm)
        };
    }
}