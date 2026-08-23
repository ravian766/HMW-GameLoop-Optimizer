using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public interface IOptimizationModule
{
    string Id { get; }
    string Title { get; }
    OptimizationCategory Category { get; }
    RiskLevel RiskLevel { get; }
    string Description { get; }
    string TechnicalRationale { get; }
    bool RequiresAdmin { get; }

    string CurrentStateDisplay { get; }
    string RecommendedStateDisplay { get; }
    bool IsOptimized { get; }
    OptimizationState State { get; }

    Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl);
    Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl);
    Task<OptimizationResult> RollbackAsync(BackupEntry? backup);
    Task<bool> VerifyAsync();
}
