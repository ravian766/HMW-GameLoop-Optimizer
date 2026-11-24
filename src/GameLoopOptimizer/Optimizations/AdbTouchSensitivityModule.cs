using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbTouchSensitivityModule : IOptimizationModule
{
    public string Id => "gl_adb_touch_sensitivity";
    public string Title => "Android VM Touch Sensitivity Calibration";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Adjusts Android VM touch event sensitivity and calibration curves to reduce input latency and improve aim precision in FPS and shooter games.";
    public string TechnicalRationale => "Default Android VM touch sensitivity curves can add processing lag and smoothing that increases aim latency. Calibrating the sensitivity and reducing touch slop helps micro-mouse movements be registered immediately without queue delay.";

    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Calibrated (Optimized)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

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

        string touchSensitivity = await AdbManager.GetPropAsync("persist.vendor.touch.sensitivity", gl);
        string touchSlop = await AdbManager.GetPropAsync("view.touch_slop", gl);
        string touchCalibration = await AdbManager.GetPropAsync("touch.size.calibration", gl);

        bool isOpt = touchSensitivity.Trim() == "10" && touchSlop.Trim() == "2" && touchCalibration.Trim() == "geometric";

        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "Calibrated (10 / 2 / geometric)" :
            $"Sensitivity: {(string.IsNullOrEmpty(touchSensitivity) ? "Default" : touchSensitivity)}, Slop: {(string.IsNullOrEmpty(touchSlop) ? "Default" : touchSlop)}, Cal: {(string.IsNullOrEmpty(touchCalibration) ? "Default" : touchCalibration)}";
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

            // Record backup before sensitivity changes
            string prevSensitivity = await AdbManager.GetPropAsync("persist.vendor.touch.sensitivity", gl);
            string prevSlop = await AdbManager.GetPropAsync("view.touch_slop", gl);
            string prevCalibration = await AdbManager.GetPropAsync("touch.size.calibration", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "persist.vendor.touch.sensitivity",
                PreviousValue = prevSensitivity,
                NewValue = "10",
                Description = "Android VM touch sensitivity calibration"
            });

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "view.touch_slop",
                PreviousValue = prevSlop,
                NewValue = "2",
                Description = "Android VM touch slop reduction"
            });

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "touch.size.calibration",
                PreviousValue = prevCalibration,
                NewValue = "geometric",
                Description = "Android VM touch size calibration mode"
            });

            // Apply calibrated sensitivity settings for low-latency input
            await AdbManager.SetPropAsync("persist.vendor.touch.sensitivity", "10", gl);
            await AdbManager.SetPropAsync("view.touch_slop", "2", gl);
            await AdbManager.SetPropAsync("touch.size.calibration", "geometric", gl);

            IsOptimized = true;
            CurrentStateDisplay = "Calibrated (10 / 2 / geometric)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Android VM touch sensitivity calibration for low-latency input.");
            return OptimizationResult.Ok(Id, "Android VM touch sensitivity calibrated for reduced input latency.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply touch sensitivity calibration: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        // Restore each property from its respective backup entry
        var moduleEntries = BackupManager.GetAllForModule(Id);
        string? PreviousFor(string targetPath) => moduleEntries
            .LastOrDefault(e => string.Equals(e.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
            ?.PreviousValue;

        string? sensPrev = backup?.TargetPath == "persist.vendor.touch.sensitivity" ? backup.PreviousValue : null;
        sensPrev ??= PreviousFor("persist.vendor.touch.sensitivity");
        if (string.IsNullOrWhiteSpace(sensPrev)) sensPrev = "1";

        string? slopPrev = backup?.TargetPath == "view.touch_slop" ? backup.PreviousValue : null;
        slopPrev ??= PreviousFor("view.touch_slop");
        if (string.IsNullOrWhiteSpace(slopPrev)) slopPrev = "4";

        string? calPrev = backup?.TargetPath == "touch.size.calibration" ? backup.PreviousValue : null;
        calPrev ??= PreviousFor("touch.size.calibration");
        if (string.IsNullOrWhiteSpace(calPrev)) calPrev = "default";

        await AdbManager.SetPropAsync("persist.vendor.touch.sensitivity", sensPrev);
        await AdbManager.SetPropAsync("view.touch_slop", slopPrev);
        await AdbManager.SetPropAsync("touch.size.calibration", calPrev);

        IsOptimized = false;
        CurrentStateDisplay = "Restored";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored previous Android VM touch sensitivity settings.");
    }

    public async Task<bool> VerifyAsync()
    {
        string sensitivity = await AdbManager.GetPropAsync("persist.vendor.touch.sensitivity");
        string slop = await AdbManager.GetPropAsync("view.touch_slop");
        string calibration = await AdbManager.GetPropAsync("touch.size.calibration");

        bool sensitivityOk = sensitivity.Trim() == "10";
        bool slopOk = slop.Trim() == "2";
        bool calibrationOk = calibration.Trim() == "geometric";

        return sensitivityOk && slopOk && calibrationOk;
    }
}