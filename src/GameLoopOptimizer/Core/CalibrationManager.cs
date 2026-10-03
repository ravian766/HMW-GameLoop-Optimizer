namespace GameLoopOptimizer.Core;

public class CalibrationSession
{
    public bool IsActive { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public KeyMappingProfile WorkingProfile { get; set; } = new();
    public int TargetWidth { get; set; } = 1920;
    public int TargetHeight { get; set; } = 1080;
    public List<string> CalibratedActions { get; } = new();
}

public static class CalibrationManager
{
    private static CalibrationSession? _currentSession;

    public static bool IsCalibrating => _currentSession?.IsActive ?? false;

    public static CalibrationSession StartSession(KeyMappingProfile? baseProfile = null, int targetWidth = 1920, int targetHeight = 1080)
    {
        baseProfile ??= MappingProfileManager.GetBuiltInProfiles().FirstOrDefault() ?? new KeyMappingProfile();

        // Clone base profile controls
        var working = new KeyMappingProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{baseProfile.Name} (Calibrated {targetWidth}x{targetHeight})",
            Description = $"User-calibrated HUD mapping for {targetWidth}x{targetHeight}.",
            ReferenceWidth = baseProfile.ReferenceWidth,
            ReferenceHeight = baseProfile.ReferenceHeight,
            Controls = baseProfile.Controls.Select(c => new KeyMappingControl
            {
                Action = c.Action,
                Key = c.Key,
                AsciiCode = c.AsciiCode,
                InputType = c.InputType,
                Anchor = c.Anchor,
                ReferenceX = c.ReferenceX,
                ReferenceY = c.ReferenceY,
                Offset = c.Offset,
                Enabled = c.Enabled,
                SensitivityX = c.SensitivityX,
                SensitivityY = c.SensitivityY
            }).ToList()
        };

        _currentSession = new CalibrationSession
        {
            IsActive = true,
            WorkingProfile = working,
            TargetWidth = targetWidth,
            TargetHeight = targetHeight
        };

        Logger.Info("CalibrationManager", $"Started calibration session for {targetWidth}x{targetHeight} based on '{baseProfile.Name}'.");
        return _currentSession;
    }

    public static bool SetControlPosition(string action, double normalizedX, double normalizedY)
    {
        if (_currentSession == null || !_currentSession.IsActive) return false;

        var ctrl = _currentSession.WorkingProfile.Controls.FirstOrDefault(c => c.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
        if (ctrl != null)
        {
            ctrl.ReferenceX = Math.Clamp(normalizedX, 0.01, 0.99);
            ctrl.ReferenceY = Math.Clamp(normalizedY, 0.01, 0.99);
            if (!_currentSession.CalibratedActions.Contains(action))
            {
                _currentSession.CalibratedActions.Add(action);
            }
            return true;
        }

        return false;
    }

    public static async Task<KeyMappingProfile?> CommitSessionAsync()
    {
        if (_currentSession == null || !_currentSession.IsActive) return null;

        var profile = _currentSession.WorkingProfile;
        await MappingProfileManager.SaveProfileAsync(profile);

        _currentSession.IsActive = false;
        Logger.Success("CalibrationManager", $"Committed calibration profile '{profile.Name}' with {profile.Controls.Count} controls.");
        return profile;
    }

    public static void CancelSession()
    {
        if (_currentSession != null)
        {
            _currentSession.IsActive = false;
            _currentSession = null;
            Logger.Info("CalibrationManager", "Calibration session cancelled.");
        }
    }
}
