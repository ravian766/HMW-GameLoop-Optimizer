using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbAudioLatencyModule : IOptimizationModule
{
    public string Id => "gl_adb_audio_latency";
    public string Title => "Android VM Fast-Track Audio Latency Tuning";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Disables deep buffer media audio queues and routes game sound effects through the low-latency fast track in Android AudioFlinger.";
    public string TechnicalRationale => "Overrides audio.deep_buffer.media to false and sets stagefright audio sink buffer size to 256 samples, eliminating the 50-100ms gunshot audio delay inside GameLoop.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Fast-Track Low Latency Audio";
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

        string deepBuf = await AdbManager.GetPropAsync("audio.deep_buffer.media", gl);
        bool isOpt = deepBuf.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);

        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "Low-Latency Fast Track Active" : "Default Deep Buffer (Delayed)";
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

            string prevVal = await AdbManager.GetPropAsync("audio.deep_buffer.media", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "audio.deep_buffer.media",
                PreviousValue = prevVal,
                NewValue = "false",
                Description = "In-VM Audio Latency Optimization"
            });

            await AdbManager.OptimizeInVmAudioLatencyAsync(gl);

            IsOptimized = true;
            CurrentStateDisplay = "Low-Latency Fast Track Active";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured In-VM fast-track audio routing via ADB.");
            return OptimizationResult.Ok(Id, "In-VM audio latency reduced (deep buffer disabled).");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to configure In-VM Audio: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        string restoreVal = target?.PreviousValue ?? "true";
        if (string.IsNullOrWhiteSpace(restoreVal)) restoreVal = "true";

        await AdbManager.SetPropAsync("audio.deep_buffer.media", restoreVal);
        IsOptimized = false;
        CurrentStateDisplay = "Default Deep Buffer (Delayed)";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default AudioFlinger deep buffer settings.");
    }

    public async Task<bool> VerifyAsync()
    {
        string deepBuf = await AdbManager.GetPropAsync("audio.deep_buffer.media");
        return deepBuf.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);
    }
}
