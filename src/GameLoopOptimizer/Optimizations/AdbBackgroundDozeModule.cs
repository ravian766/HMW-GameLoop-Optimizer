using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbBackgroundDozeModule : IOptimizationModule
{
    public string Id => "gl_adb_bg_doze";
    public string Title => "Android VM Standby & Process Throttling Disabler";
    public OptimizationCategory Category => OptimizationCategory.BackgroundProcess;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Disables Android OS adaptive battery management and app standby inside the emulator, preventing background game throttling and keymapping latency.";
    public string TechnicalRationale => "Android energy-saving policies are intended for battery-powered smartphones and cause unnecessary thread sleep and wake-up latencies inside desktop emulators.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Throttling & Doze Disabled";
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

        string appStandby = await AdbManager.GetGlobalSettingAsync("app_standby_enabled", gl);
        string adaptiveBat = await AdbManager.GetGlobalSettingAsync("adaptive_battery_management_enabled", gl);

        bool isOpt = (appStandby.Trim() == "0") && (adaptiveBat.Trim() == "0");

        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "Doze & App Standby Disabled" : $"Standby: {(string.IsNullOrEmpty(appStandby) ? "Default" : appStandby)}, Adaptive: {(string.IsNullOrEmpty(adaptiveBat) ? "Default" : adaptiveBat)}";
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

            string prevStandby = await AdbManager.GetGlobalSettingAsync("app_standby_enabled", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbSetting",
                TargetPath = "app_standby_enabled",
                PreviousValue = prevStandby,
                NewValue = "0",
                Description = "Android emulator standby & doze power policy"
            });

            await AdbManager.PutGlobalSettingAsync("app_standby_enabled", "0", gl);
            await AdbManager.PutGlobalSettingAsync("adaptive_battery_management_enabled", "0", gl);
            await AdbManager.ExecuteShellCommandAsync("cmd appops set com.tencent.ig RUN_IN_BACKGROUND allow", null, 4000, gl);

            IsOptimized = true;
            CurrentStateDisplay = "Doze & App Standby Disabled";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Disabled Android power throttling and app standby for PUBG Mobile.");
            return OptimizationResult.Ok(Id, "Android VM background doze and power throttling disabled.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to configure Android power settings: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        await AdbManager.PutGlobalSettingAsync("app_standby_enabled", "1");
        await AdbManager.PutGlobalSettingAsync("adaptive_battery_management_enabled", "1");

        IsOptimized = false;
        CurrentStateDisplay = "Restored";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default Android power standby policies.");
    }

    public async Task<bool> VerifyAsync()
    {
        string appStandby = await AdbManager.GetGlobalSettingAsync("app_standby_enabled");
        return appStandby.Trim() == "0";
    }
}
