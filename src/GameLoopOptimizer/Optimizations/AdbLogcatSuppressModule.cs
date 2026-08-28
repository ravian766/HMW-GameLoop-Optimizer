using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbLogcatSuppressModule : IOptimizationModule
{
    public string Id => "gl_adb_logcat_suppress";
    public string Title => "Android VM Logcat & Debug Telemetry Suppression";
    public OptimizationCategory Category => OptimizationCategory.BackgroundProcess;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Suppresses verbose Android system logcat and statistics logging buffers inside the GameLoop emulator to free host and guest CPU cycles.";
    public string TechnicalRationale => "Under default emulator builds, Android services continuously write tens of thousands of log entries per minute, wasting emulator CPU interrupts and causing unnecessary IO churn.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Logging Suppressed (ALL=SUPPRESS)";
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

        string logTag = await AdbManager.GetPropAsync("log.tag", gl);
        string statsLog = await AdbManager.GetPropAsync("log.tag.stats_log", gl);

        bool isOpt = logTag.Contains("SUPPRESS", StringComparison.OrdinalIgnoreCase) ||
                     statsLog.Contains("OFF", StringComparison.OrdinalIgnoreCase);

        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "Suppressed (Low CPU Overhead)" : $"Logcat Tag: {(string.IsNullOrEmpty(logTag) ? "Active (Verbose)" : logTag)}";
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

            string prevTag = await AdbManager.GetPropAsync("log.tag", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "log.tag",
                PreviousValue = prevTag,
                NewValue = "SUPPRESS",
                Description = "Suppress Android emulator logcat and telemetry"
            });

            await AdbManager.SetPropAsync("log.tag", "ALL=SUPPRESS", gl);
            await AdbManager.SetPropAsync("log.tag.stats_log", "OFF", gl);
            await AdbManager.SetPropAsync("persist.logd.size", "64K", gl);

            IsOptimized = true;
            CurrentStateDisplay = "Suppressed (Low CPU Overhead)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Android VM debug logcat and telemetry buffers suppressed via ADB.");
            return OptimizationResult.Ok(Id, "Android VM debug logging overhead suppressed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to suppress Android logging: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        await AdbManager.SetPropAsync("log.tag", "");
        await AdbManager.SetPropAsync("log.tag.stats_log", "");
        await AdbManager.SetPropAsync("persist.logd.size", "256K");

        IsOptimized = false;
        CurrentStateDisplay = "Restored";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default Android logcat configuration.");
    }

    public async Task<bool> VerifyAsync()
    {
        string logTag = await AdbManager.GetPropAsync("log.tag");
        return logTag.Contains("SUPPRESS", StringComparison.OrdinalIgnoreCase);
    }
}
