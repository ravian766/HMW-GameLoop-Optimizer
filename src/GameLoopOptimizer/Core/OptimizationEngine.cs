using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.Core;

public class OptimizationEngine
{
    private readonly List<IOptimizationModule> _modules;
    private readonly PerformanceBaselineService _baselineService;
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<SystemInfo> _getSys;
    private readonly Func<GameLoopConfig> _getGl;

    public OptimizationEngine(
        IEnumerable<IOptimizationModule> modules,
        PerformanceBaselineService baselineService,
        Func<HardwareInfo> getHw,
        Func<SystemInfo> getSys,
        Func<GameLoopConfig> getGl)
    {
        _modules = modules.ToList();
        _baselineService = baselineService;
        _getHw = getHw;
        _getSys = getSys;
        _getGl = getGl;
    }

    /// <summary>
    /// Executes the full measured optimization pipeline.
    /// </summary>
    public async Task<OptimizationReport> ExecutePipelineAsync(
        IEnumerable<IOptimizationModule> targetModules,
        OptimizationProfile profile,
        bool isAutomated = false)
    {
        var hw = _getHw();
        var sys = _getSys();
        var gl = _getGl();
        var rec = RecommendationEngine.Calculate(hw);

        var report = new OptimizationReport
        {
            Timestamp = DateTime.Now,
            ProfileName = profile.ToString(),
            ScoreBefore = ScoringEngine.CalculateScore(hw, sys, gl, rec).TotalScore
        };

        // 1. Collect Baseline Before Optimization
        var baselineBefore = _baselineService.CaptureInstantBaseline("Pre-Optimization Baseline");
        _baselineService.RecordBaselineBefore(baselineBefore);

        // 2. Safe Mode & Unknown Version Gating
        bool isSafeMode = SafeModeController.Instance.IsSafeModeActive;
        bool isUnknownGl = gl.CompatibilityTier == GameLoopCompatibilityTier.Unknown;

        if (isSafeMode || isUnknownGl)
        {
            string reason = isSafeMode
                ? "Safe Mode is active. Optimizations simulated and analyzed without modifying system or registry."
                : $"GameLoop version '{gl.Version}' is unverified. Running in safe analyze-only mode.";

            Logger.Warn("OptimizationEngine", reason);

            foreach (var mod in targetModules)
            {
                report.Items.Add(new OptimizationReportItem
                {
                    ModuleId = mod.Id,
                    Title = mod.Title,
                    Category = mod.Category,
                    Success = true,
                    Message = $"[SIMULATED - NO CHANGES MADE] Recommended: {mod.RecommendedStateDisplay}",
                    PreviousState = mod.CurrentStateDisplay,
                    NewState = mod.CurrentStateDisplay
                });
            }

            report.AppliedCount = 0;
            report.FailedCount = 0;
            report.ScoreAfter = report.ScoreBefore;
            return report;
        }

        // 3. Filter Safe / Low-risk optimizations if running automated
        var toApply = targetModules.ToList();
        if (isAutomated)
        {
            toApply = toApply.Where(m => m.RiskLevel == RiskLevel.Safe || m.RiskLevel == RiskLevel.Low).ToList();
        }

        report.TotalConsidered = toApply.Count;
        int successCount = 0;
        int failCount = 0;

        // 4. Apply Changes
        foreach (var mod in toApply)
        {
            string prevState = mod.CurrentStateDisplay;
            try
            {
                var result = await mod.ApplyAsync(hw, sys, gl);
                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }

                report.Items.Add(new OptimizationReportItem
                {
                    ModuleId = mod.Id,
                    Title = mod.Title,
                    Category = mod.Category,
                    Success = result.Success,
                    Message = result.Message,
                    PreviousState = prevState,
                    NewState = mod.CurrentStateDisplay
                });
            }
            catch (Exception ex)
            {
                failCount++;
                Logger.Error("OptimizationEngine", $"Error applying {mod.Title}: {ex.Message}");
                report.Items.Add(new OptimizationReportItem
                {
                    ModuleId = mod.Id,
                    Title = mod.Title,
                    Category = mod.Category,
                    Success = false,
                    Message = $"Exception: {ex.Message}",
                    PreviousState = prevState,
                    NewState = mod.CurrentStateDisplay
                });
            }
        }

        report.AppliedCount = successCount;
        report.FailedCount = failCount;

        // 5. Verification Phase
        foreach (var mod in toApply.Where(m => m.IsOptimized))
        {
            try
            {
                await mod.VerifyAsync();
            }
            catch { }
        }

        // 6. Collect Post-Optimization Baseline
        var baselineAfter = _baselineService.CaptureInstantBaseline("Post-Optimization Baseline");
        _baselineService.RecordBaselineAfter(baselineAfter);

        report.ScoreAfter = ScoringEngine.CalculateScore(hw, sys, gl, rec).TotalScore;

        if (_baselineService.LatestComparison != null)
        {
            report.SnapshotComparison = new SnapshotComparison(
                OptimizationSnapshot.Capture(new PerformanceMetrics { Fps = baselineBefore.AverageFps, CpuTotalPercent = baselineBefore.CpuUsagePercent, GpuPercent = baselineBefore.GpuUsagePercent, RamUsedGb = baselineBefore.RamUsageGb }, sys, 0, toApply.Count, "Pre-Optimization"),
                OptimizationSnapshot.Capture(new PerformanceMetrics { Fps = baselineAfter.AverageFps, CpuTotalPercent = baselineAfter.CpuUsagePercent, GpuPercent = baselineAfter.GpuUsagePercent, RamUsedGb = baselineAfter.RamUsageGb }, sys, successCount, toApply.Count, "Post-Optimization")
            );
        }

        Logger.Success("OptimizationEngine", $"Optimization pipeline completed: {successCount} applied, {failCount} failed. Score: {report.ScoreBefore} -> {report.ScoreAfter}");
        return report;
    }
}
