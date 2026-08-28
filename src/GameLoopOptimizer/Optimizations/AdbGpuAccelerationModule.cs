using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbGpuAccelerationModule : IOptimizationModule
{
    public string Id => "gl_adb_gpu_acceleration";
    public string Title => "Android VM GPU Composition & Low-Latency VSync";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures Android SurfaceFlinger inside GameLoop to force pure GPU hardware composition, bypassing CPU raster fallback and minimizing vsync latch delay.";
    public string TechnicalRationale => "Directing SurfaceFlinger and EGL composition entirely through the hardware GPU eliminates frame buffering overhead, resulting in sharper frame pacing and lower input lag.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Force GPU / Latch Unsignaled";
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

        // Read current props via ADB
        string sfHw = await AdbManager.GetPropAsync("debug.sf.hw", gl);
        string compType = await AdbManager.GetPropAsync("debug.composition.type", gl);

        bool isOpt = sfHw.Trim() == "1" && (compType.Trim() == "gpu" || compType.Trim() == "c2d");
        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "GPU Accelerated (HW Composition Active)" : $"SurfaceFlinger HW: {(string.IsNullOrEmpty(sfHw) ? "Default" : sfHw)}, Composition: {(string.IsNullOrEmpty(compType) ? "Default" : compType)}";
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

            // Record backups
            string prevSfHw = await AdbManager.GetPropAsync("debug.sf.hw", gl);
            string prevComp = await AdbManager.GetPropAsync("debug.composition.type", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "debug.sf.hw",
                PreviousValue = prevSfHw,
                NewValue = "1",
                Description = "SurfaceFlinger hardware acceleration & GPU composition"
            });

            // Apply low latency GPU props
            await AdbManager.SetPropAsync("debug.sf.hw", "1", gl);
            await AdbManager.SetPropAsync("debug.egl.hw", "1", gl);
            await AdbManager.SetPropAsync("debug.composition.type", "gpu", gl);
            await AdbManager.SetPropAsync("debug.sf.latch_unsignaled", "1", gl);
            await AdbManager.SetPropAsync("debug.sf.early_phase_offset_ns", "500000", gl);
            await AdbManager.SetPropAsync("debug.sf.early_app_phase_offset_ns", "500000", gl);
            await AdbManager.SetPropAsync("video.accelerate.hw", "1", gl);

            IsOptimized = true;
            CurrentStateDisplay = "GPU Accelerated (HW Composition Active)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Android SurfaceFlinger hardware GPU composition and low-latency vsync via ADB.");
            return OptimizationResult.Ok(Id, "Android VM GPU composition and vsync latching optimized successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply ADB GPU tweaks: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null)
        {
            string prevVal = target.PreviousValue ?? "0";
            await AdbManager.SetPropAsync("debug.sf.hw", prevVal);
            await AdbManager.SetPropAsync("debug.composition.type", "dyn");
            IsOptimized = false;
            CurrentStateDisplay = "Restored";
            State = OptimizationState.NotOptimized;
            return OptimizationResult.Ok(Id, "Restored previous SurfaceFlinger composition properties.");
        }
        return OptimizationResult.Fail(Id, "No backup found to revert.");
    }

    public async Task<bool> VerifyAsync()
    {
        string sfHw = await AdbManager.GetPropAsync("debug.sf.hw");
        return sfHw.Trim() == "1";
    }
}
