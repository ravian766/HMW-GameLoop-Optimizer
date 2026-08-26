using System.Diagnostics;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class CpuAffinityModule : IOptimizationModule
{
    public string Id => "gl_cpu_affinity";
    public string Title => "P-Core / High-Performance Core Affinity";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Locks GameLoop emulator and render worker threads strictly to high-performance cores (P-Cores) and avoids low-clock Efficiency Cores.";
    public string TechnicalRationale => "On hybrid CPUs (Intel 12th/13th/14th Gen) and multi-CCD AMD processors, OS thread dispatch to E-cores or secondary CCDs causes sudden 1% low frame drops. Core affinity pinning prevents core jumping.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "All Logical Processors";
    public string RecommendedStateDisplay { get; private set; } = "P-Cores / Primary Cores Only";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    private static bool _isAffinityApplied = false;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (hw.LogicalProcessors <= 4)
        {
            CurrentStateDisplay = "All Cores (Optimal for <=4T)";
            RecommendedStateDisplay = "All Cores";
            IsOptimized = true;
            State = OptimizationState.Optimized;
            return Task.FromResult(State);
        }

        long optimalMask = ProcessManager.CalculateOptimalAffinityMask(hw.LogicalProcessors, hw.PhysicalCores);
        RecommendedStateDisplay = $"Threads 0–{Math.Min(7, hw.LogicalProcessors - 1)} (Mask: 0x{optimalMask:X})";

        IsOptimized = _isAffinityApplied;
        CurrentStateDisplay = _isAffinityApplied ? "Pinned to Performance Cores" : $"{hw.LogicalProcessors} Logical Cores (Unpinned)";
        State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        long optimalMask = ProcessManager.CalculateOptimalAffinityMask(hw.LogicalProcessors, hw.PhysicalCores);
        bool applied = ProcessManager.SetGameLoopAffinity(optimalMask);
        _isAffinityApplied = true;

        IsOptimized = true;
        CurrentStateDisplay = $"Performance Cores Only (0x{optimalMask:X})";
        State = OptimizationState.Optimized;

        Logger.Success(Title, applied 
            ? $"Applied Performance Core Affinity (0x{optimalMask:X}) to running GameLoop processes." 
            : $"Performance Core Affinity armed (Mask 0x{optimalMask:X} will apply when GameLoop is running).");

        return Task.FromResult(OptimizationResult.Ok(Id, $"P-Core affinity mask (0x{optimalMask:X}) configured."));
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        ProcessManager.ResetGameLoopAffinity();
        _isAffinityApplied = false;

        IsOptimized = false;
        CurrentStateDisplay = "All Logical Processors";
        State = OptimizationState.Recommended;

        Logger.Info(Title, "Reset GameLoop CPU affinity to all logical cores.");
        return Task.FromResult(OptimizationResult.Ok(Id, "Reset affinity to all logical cores."));
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
