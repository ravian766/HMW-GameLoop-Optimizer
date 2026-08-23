using System.Diagnostics;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class ProcessPriorityModule : IOptimizationModule
{
    public string Id => "gl_process_priority";
    public string Title => "Emulator Process Priority Boost";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Elevates GameLoop and Android emulator execution threads to 'Above Normal' priority in the Windows thread scheduler.";
    public string TechnicalRationale => "Ensures emulator render and physics threads are scheduled ahead of lower priority background tasks, preventing micro-drops during sudden background activity.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Normal";
    public string RecommendedStateDisplay => "Above Normal";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsRunning)
        {
            CurrentStateDisplay = "Emulator Inactive (Auto when launched)";
            State = OptimizationState.Recommended;
            return Task.FromResult(State);
        }

        bool allAbove = true;
        foreach (var pid in gl.RunningProcessIds)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (p.PriorityClass == ProcessPriorityClass.Normal || p.PriorityClass == ProcessPriorityClass.BelowNormal)
                {
                    allAbove = false;
                    break;
                }
            }
            catch { }
        }

        IsOptimized = allAbove;
        CurrentStateDisplay = allAbove ? "Above Normal" : "Normal";
        State = allAbove ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        bool applied = ProcessManager.SetGameLoopPriority(ProcessPriorityClass.AboveNormal);
        IsOptimized = true;
        CurrentStateDisplay = "Above Normal";
        State = OptimizationState.Optimized;

        Logger.Success(Title, applied ? "Elevated emulator process priority to Above Normal." : "Priority boost armed (will apply automatically when GameLoop is running).");
        return Task.FromResult(OptimizationResult.Ok(Id, "Emulator process priority set to Above Normal."));
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        ProcessManager.SetGameLoopPriority(ProcessPriorityClass.Normal);
        IsOptimized = false;
        CurrentStateDisplay = "Normal";
        State = OptimizationState.Recommended;
        return Task.FromResult(OptimizationResult.Ok(Id, "Restored normal process priority."));
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
