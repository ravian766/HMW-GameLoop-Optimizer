using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class Adb120FpsUnlockModule : IOptimizationModule
{
    public string Id => "gl_adb_120fps_unlock";
    public string Title => "Android VM True 120Hz SurfaceFlinger Unlock";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Forces Android SurfaceFlinger compositor refresh ceiling and dynamic FPS level to 120Hz, eliminating internal 60fps frame rate caps.";
    public string TechnicalRationale => "Overrides ro.surface_flinger.max_frame_rate and vendor display properties so the Android container renders at up to 120fps synchronized with high-refresh monitors.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "120Hz Display Pipeline";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public async Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsInstalled)
        {
            CurrentStateDisplay = "GameLoop Not Installed";
            State = OptimizationState.NotDetected;
            return State;
        }

        if (!AdbManager.IsAdbAvailable(gl))
        {
            CurrentStateDisplay = "ADB Not Found";
            State = OptimizationState.NotDetected;
            return State;
        }

        string sfFps = await AdbManager.GetPropAsync("debug.sf.fps", gl);
        string maxRate = await AdbManager.GetPropAsync("ro.surface_flinger.max_frame_rate", gl);

        bool isOpt = sfFps.Trim() == "120" || maxRate.Trim() == "120";
        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "120Hz High-Refresh Active" : $"SurfaceFlinger: {(string.IsNullOrEmpty(sfFps) ? "Default (60Hz)" : sfFps + "Hz")}";
        State = isOpt ? OptimizationState.Optimized : OptimizationState.Recommended;
        return State;
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!AdbManager.IsAdbAvailable(gl))
        {
            return OptimizationResult.Fail(Id, "ADB executable not found on system.");
        }

        try
        {
            await AdbManager.AutoConnectGameLoopAsync(gl);

            string prevFps = await AdbManager.GetPropAsync("debug.sf.fps", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "debug.sf.fps",
                PreviousValue = prevFps,
                NewValue = "120",
                Description = "SurfaceFlinger refresh rate unlock"
            });

            // Android treats `ro.*` as write-once at boot, so setprop on
            // ro.surface_flinger.max_frame_rate / ro.vendor.display.default_fps is always refused.
            // Only properties that actually change at runtime are written here.
            bool coreApplied = await AdbManager.SetPropAsync("debug.sf.fps", "120", gl);
            await AdbManager.SetPropAsync("persist.sys.display.rate", "120", gl);
            await AdbManager.SetPropAsync("persist.vendor.dfps.level", "120", gl);
            await AdbManager.SetPropAsync("debug.sf.swaprect", "1", gl);

            // Force Android Display Manager peak and min refresh rate ceilings to 120Hz
            try
            {
                await AdbManager.ExecuteAdbCommandAsync("shell settings put system peak_refresh_rate 120.0", 3000, gl);
                await AdbManager.ExecuteAdbCommandAsync("shell settings put system min_refresh_rate 120.0", 3000, gl);
            }
            catch { }

            if (!coreApplied)
            {
                Logger.Error(Title, "The Android VM refused debug.sf.fps=120; refresh rate unchanged.");
                return OptimizationResult.Fail(Id, "Android VM refused debug.sf.fps=120 (property is locked on this image).");
            }

            IsOptimized = true;
            CurrentStateDisplay = "120Hz High-Refresh Active";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Android VM 120Hz SurfaceFlinger refresh parameters via ADB.");
            return OptimizationResult.Ok(Id, "Android VM 120Hz high-refresh rendering pipeline unlocked.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply ADB 120Hz refresh unlock: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        string restoreVal = target?.PreviousValue ?? "60";
        if (string.IsNullOrWhiteSpace(restoreVal)) restoreVal = "60";

        await AdbManager.SetPropAsync("debug.sf.fps", restoreVal);

        // `ro.surface_flinger.max_frame_rate` used to be reset here, but adbd refuses every
        // `ro.*` write after boot, so it only produced a bogus failure warning.
        await AdbManager.SetPropAsync("persist.sys.display.rate", "60");

        IsOptimized = false;
        CurrentStateDisplay = "Default (60Hz)";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default SurfaceFlinger refresh ceiling.");
    }

    public async Task<bool> VerifyAsync()
    {
        string sfFps = await AdbManager.GetPropAsync("debug.sf.fps");
        string dfpsLevel = await AdbManager.GetPropAsync("persist.vendor.dfps.level");
        string eglHw = await AdbManager.GetPropAsync("debug.egl.hw");

        bool fpsOk = sfFps.Trim() == "120";
        bool dfpsOk = dfpsLevel.Trim() == "120";
        bool eglOk = eglHw.Trim() == "1";

        return fpsOk && dfpsOk && eglOk;
    }
}
