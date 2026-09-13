namespace GameLoopOptimizer.Models;

/// <summary>
/// Precise status of a single setting application attempt.
/// Separates "was it configured?" from "can the monitor show it?"
/// </summary>
public enum SettingStatus
{
    /// <summary>Setting was written and verified successfully.</summary>
    Applied,

    /// <summary>Setting was applied but the monitor refresh rate is lower than the configured FPS.</summary>
    AppliedWithDisplayWarning,

    /// <summary>The GameLoop/PUBG configuration does not support this value.</summary>
    UnsupportedByGame,

    /// <summary>The game/emulator actively rejected or overwrote the setting.</summary>
    RejectedByGame,

    /// <summary>The setting was initially applied but was later reverted by the game/emulator.</summary>
    RevertedByGame,

    /// <summary>The registry/file write operation failed.</summary>
    WriteFailed,

    /// <summary>The setting was written but readback returned a different value.</summary>
    VerificationFailed,

    /// <summary>The setting was written but cannot be independently verified.</summary>
    NotVerifiable
}

/// <summary>
/// Result of applying a single configuration setting (e.g., FPS, Graphics Quality, Content Scale).
/// </summary>
public class SettingApplicationResult
{
    public string SettingName { get; set; } = string.Empty;
    public string RequestedValue { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string WrittenValue { get; set; } = string.Empty;
    public string ReadbackValue { get; set; } = string.Empty;
    public SettingStatus Status { get; set; } = SettingStatus.NotVerifiable;
    public string DisplayWarning { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
    public string RegistryKey { get; set; } = string.Empty;

    public bool IsSuccess => Status is SettingStatus.Applied or SettingStatus.AppliedWithDisplayWarning;

    public string StatusIcon => Status switch
    {
        SettingStatus.Applied => "✓",
        SettingStatus.AppliedWithDisplayWarning => "✓",
        SettingStatus.UnsupportedByGame => "✗",
        SettingStatus.RejectedByGame => "✗",
        SettingStatus.RevertedByGame => "⟲",
        SettingStatus.WriteFailed => "✗",
        SettingStatus.VerificationFailed => "⚠",
        SettingStatus.NotVerifiable => "?",
        _ => "?"
    };

    public string StatusLabel => Status switch
    {
        SettingStatus.Applied => "Applied",
        SettingStatus.AppliedWithDisplayWarning => "Applied (Display Warning)",
        SettingStatus.UnsupportedByGame => "Unsupported by Game",
        SettingStatus.RejectedByGame => "Rejected by Game",
        SettingStatus.RevertedByGame => "Reverted by Game",
        SettingStatus.WriteFailed => "Write Failed",
        SettingStatus.VerificationFailed => "Verification Failed",
        SettingStatus.NotVerifiable => "Not Verifiable",
        _ => "Unknown"
    };
}

/// <summary>
/// Comprehensive report of all settings applied during a single optimization operation.
/// </summary>
public class SettingsApplicationReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<SettingApplicationResult> Results { get; set; } = new();
    public int MonitorRefreshRateHz { get; set; }
    public int MonitorMaxRefreshRateHz { get; set; }
    public int RequestedFps { get; set; }
    public int RequestedGraphicsQuality { get; set; }

    public bool HasDisplayWarnings => Results.Any(r => r.Status == SettingStatus.AppliedWithDisplayWarning);
    public bool AllSucceeded => Results.All(r => r.IsSuccess);
    public int SuccessCount => Results.Count(r => r.IsSuccess);
    public int FailedCount => Results.Count(r => !r.IsSuccess);

    public string SummaryText
    {
        get
        {
            var parts = new List<string>();
            foreach (var r in Results)
            {
                parts.Add($"{r.StatusIcon} {r.SettingName}: {r.StatusLabel}");
            }

            if (HasDisplayWarnings)
            {
                parts.Add($"⚠ Monitor: {MonitorRefreshRateHz} Hz");
            }

            return string.Join(" | ", parts);
        }
    }
}
