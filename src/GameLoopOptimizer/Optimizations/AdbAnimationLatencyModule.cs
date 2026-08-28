using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbAnimationLatencyModule : IOptimizationModule
{
    public string Id => "gl_adb_animation_latency";
    public string Title => "Android VM Zero Animation Latency";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Disables Android window, transition, and animator scales (0.0x) inside the GameLoop emulator to eliminate UI transition lag.";
    public string TechnicalRationale => "Default Android animations add up to 300ms visual and processing delay when switching menus, opening game overlays, and triggering UI elements.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Animations Disabled (0.0x)";
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

        string winScale = await AdbManager.GetGlobalSettingAsync("window_animation_scale", gl);
        string transScale = await AdbManager.GetGlobalSettingAsync("transition_animation_scale", gl);

        bool isOpt = (winScale.Trim() == "0" || winScale.Trim() == "0.0") &&
                     (transScale.Trim() == "0" || transScale.Trim() == "0.0");

        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "Zero Delay (0.0x)" : $"Window: {(string.IsNullOrEmpty(winScale) ? "1.0" : winScale)}x, Transition: {(string.IsNullOrEmpty(transScale) ? "1.0" : transScale)}x";
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

            string prevWin = await AdbManager.GetGlobalSettingAsync("window_animation_scale", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbSetting",
                TargetPath = "window_animation_scale",
                PreviousValue = prevWin,
                NewValue = "0",
                Description = "Android global animation scale reduction"
            });

            await AdbManager.PutGlobalSettingAsync("window_animation_scale", "0", gl);
            await AdbManager.PutGlobalSettingAsync("transition_animation_scale", "0", gl);
            await AdbManager.PutGlobalSettingAsync("animator_duration_scale", "0", gl);

            IsOptimized = true;
            CurrentStateDisplay = "Zero Delay (0.0x)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Android VM UI animation scales set to 0.0x for instantaneous transitions.");
            return OptimizationResult.Ok(Id, "Android VM animations disabled successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to disable Android animations: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        string restoreVal = target?.PreviousValue ?? "1.0";
        if (string.IsNullOrWhiteSpace(restoreVal) || restoreVal.StartsWith("null", StringComparison.OrdinalIgnoreCase))
        {
            restoreVal = "1.0";
        }

        await AdbManager.PutGlobalSettingAsync("window_animation_scale", restoreVal);
        await AdbManager.PutGlobalSettingAsync("transition_animation_scale", restoreVal);
        await AdbManager.PutGlobalSettingAsync("animator_duration_scale", restoreVal);

        IsOptimized = false;
        CurrentStateDisplay = "Restored (1.0x)";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default Android UI animation scales.");
    }

    public async Task<bool> VerifyAsync()
    {
        string winScale = await AdbManager.GetGlobalSettingAsync("window_animation_scale");
        return winScale.Trim() == "0" || winScale.Trim() == "0.0";
    }
}
