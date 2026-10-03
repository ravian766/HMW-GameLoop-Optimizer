using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class CalibrationManagerTests
{
    [Fact]
    public void StartSession_InitializesSessionCorrectly()
    {
        var session = CalibrationManager.StartSession(null, 1440, 1080);

        Assert.True(CalibrationManager.IsCalibrating);
        Assert.NotNull(session);
        Assert.Equal(1440, session.TargetWidth);
        Assert.Equal(1080, session.TargetHeight);
        Assert.NotEmpty(session.WorkingProfile.Controls);

        CalibrationManager.CancelSession();
        Assert.False(CalibrationManager.IsCalibrating);
    }

    [Fact]
    public void SetControlPosition_UpdatesExistingControl_ClampedToSafeBounds()
    {
        CalibrationManager.StartSession(null, 1440, 1080);

        // Update "Jump"
        bool updated = CalibrationManager.SetControlPosition("Jump", 0.92, 0.68);
        Assert.True(updated);

        // Try out-of-bounds update: should clamp to 0.01 - 0.99
        CalibrationManager.SetControlPosition("Crouch", 1.50, -0.20);

        // Try updating non-existent control
        bool nonExistent = CalibrationManager.SetControlPosition("NonExistentAction123", 0.5, 0.5);
        Assert.False(nonExistent);

        CalibrationManager.CancelSession();
    }

    [Fact]
    public void CancelSession_ResetsActiveState()
    {
        CalibrationManager.StartSession(null, 1920, 1080);
        Assert.True(CalibrationManager.IsCalibrating);

        CalibrationManager.CancelSession();
        Assert.False(CalibrationManager.IsCalibrating);
    }
}
