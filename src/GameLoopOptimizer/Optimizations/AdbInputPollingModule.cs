using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbInputPollingModule : IOptimizationModule
{
    public string Id => "gl_adb_input_polling";
    public string Title => "Android VM 240Hz Input Event Dispatch & Ultra Polling";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Increases Android Window Manager input event dispatch frequency to 240Hz and sets high touch/cursor sensitivity to reduce mouse aim latency.";
    public string TechnicalRationale => "By default Android VM limits event loops to 60Hz. Raising dispatch ceiling to 240Hz ensures micro mouse movements are immediately processed without input queue lag.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "240Hz Ultra-Low Latency";
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

        string maxEvents = await AdbManager.GetPropAsync("windowsmgr.max_events_per_sec", gl);
        string scrollCache = await AdbManager.GetPropAsync("persist.sys.scrollingcache", gl);

        bool isOpt = maxEvents.Trim() == "240" && scrollCache.Trim() == "3";
        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "240Hz High-Speed Input" : $"Max Events: {(string.IsNullOrEmpty(maxEvents) ? "Default (60Hz)" : maxEvents + "Hz")}";
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

            string prevEvents = await AdbManager.GetPropAsync("windowsmgr.max_events_per_sec", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "windowsmgr.max_events_per_sec",
                PreviousValue = prevEvents,
                NewValue = "240",
                Description = "Android Window Manager input event dispatch frequency"
            });

            await AdbManager.SetPropAsync("windowsmgr.max_events_per_sec", "240", gl);
            await AdbManager.SetPropAsync("persist.sys.scrollingcache", "3", gl);
            await AdbManager.SetPropAsync("persist.vendor.touch.sensitivity", "10", gl);
            await AdbManager.SetPropAsync("view.touch_slop", "2", gl);
            await AdbManager.SetPropAsync("touch.size.calibration", "geometric", gl);

            IsOptimized = true;
            CurrentStateDisplay = "240Hz High-Speed Input";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Android VM 240Hz input dispatch and pointer sensitivity via ADB.");
            return OptimizationResult.Ok(Id, "240Hz Android input polling and ultra-low pointer latency applied.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply ADB input polling optimizations: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        string restoreVal = target?.PreviousValue ?? "60";
        if (string.IsNullOrWhiteSpace(restoreVal)) restoreVal = "60";

        await AdbManager.SetPropAsync("windowsmgr.max_events_per_sec", restoreVal);
        await AdbManager.SetPropAsync("persist.sys.scrollingcache", "1");

        IsOptimized = false;
        CurrentStateDisplay = "Default (60Hz)";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default Android input event dispatch rates.");
    }

    public async Task<bool> VerifyAsync()
    {
        string maxEvents = await AdbManager.GetPropAsync("windowsmgr.max_events_per_sec");
        return maxEvents.Trim() == "240";
    }
}
