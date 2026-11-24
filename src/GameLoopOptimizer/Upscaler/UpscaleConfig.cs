namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// Quality preset that determines the internal-to-output resolution ratio.
/// </summary>
public enum UpscaleQualityMode
{
    /// <summary>Highest quality — smallest upscale factor (1.3×).</summary>
    Quality,
    /// <summary>Balanced quality / performance (1.5×).</summary>
    Balanced,
    /// <summary>Maximum performance — largest upscale factor (1.7×).</summary>
    Performance,
    /// <summary>Ultra performance — extreme upscale (2.0×).</summary>
    UltraPerformance
}

/// <summary>
/// Configuration for the spatial upscaler pipeline.
/// </summary>
public class UpscaleConfig
{
    /// <summary>Quality preset controlling the upscale ratio.</summary>
    public UpscaleQualityMode QualityMode { get; set; } = UpscaleQualityMode.Balanced;

    /// <summary>RCAS sharpening strength (0.0 = maximum sharpening, 1.0 = no sharpening).</summary>
    public float Sharpness { get; set; } = 0.25f;

    /// <summary>Whether the FPS/frame-time overlay is visible on the upscaled output.</summary>
    public bool ShowFrameCounter { get; set; } = true;

    /// <summary>Returns the upscale ratio for the selected quality mode.</summary>
    public float GetUpscaleRatio() => QualityMode switch
    {
        UpscaleQualityMode.Quality => 1.3f,
        UpscaleQualityMode.Balanced => 1.5f,
        UpscaleQualityMode.Performance => 1.7f,
        UpscaleQualityMode.UltraPerformance => 2.0f,
        _ => 1.5f
    };

    /// <summary>Returns a user-friendly label for the current mode.</summary>
    public string GetModeLabel() => QualityMode switch
    {
        UpscaleQualityMode.Quality => "Quality (1.3×)",
        UpscaleQualityMode.Balanced => "Balanced (1.5×)",
        UpscaleQualityMode.Performance => "Performance (1.7×)",
        UpscaleQualityMode.UltraPerformance => "Ultra Performance (2.0×)",
        _ => "Balanced"
    };
}
