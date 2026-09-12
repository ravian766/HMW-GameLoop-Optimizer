using System.Diagnostics;

namespace GameLoopOptimizer.Models;

/// <summary>
/// Point-in-time snapshot of system resource utilization and optimization posture.
/// Used for honest before-and-after comparison metrics.
/// </summary>
public class OptimizationSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Label { get; set; } = "Snapshot";
    public double CpuTotalPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamPercent { get; set; }
    public double GpuPercent { get; set; }
    public double DiskActivePercent { get; set; }
    public int ActiveProcessCount { get; set; }
    public string ActivePowerPlan { get; set; } = string.Empty;
    public int OptimizedModulesCount { get; set; }
    public int TotalModulesCount { get; set; }

    public static OptimizationSnapshot Capture(
        PerformanceMetrics metrics,
        SystemInfo sys,
        int optimizedCount,
        int totalCount,
        string label = "Snapshot")
    {
        int processCount = 0;
        try
        {
            processCount = Process.GetProcesses().Length;
        }
        catch { }

        return new OptimizationSnapshot
        {
            Timestamp = DateTime.Now,
            Label = label,
            CpuTotalPercent = metrics.CpuTotalPercent,
            RamUsedGb = metrics.RamUsedGb,
            RamTotalGb = metrics.RamTotalGb,
            RamPercent = metrics.RamPercent,
            GpuPercent = metrics.GpuPercent,
            DiskActivePercent = metrics.DiskActivePercent,
            ActiveProcessCount = processCount,
            ActivePowerPlan = sys.ActivePowerPlanName,
            OptimizedModulesCount = optimizedCount,
            TotalModulesCount = totalCount
        };
    }
}

/// <summary>
/// Measured differences between a pre-optimization and post-optimization snapshot.
/// </summary>
public class SnapshotComparison
{
    public OptimizationSnapshot Before { get; }
    public OptimizationSnapshot After { get; }

    public double CpuDeltaPercent => Math.Round(After.CpuTotalPercent - Before.CpuTotalPercent, 1);
    public double RamDeltaGb => Math.Round(After.RamUsedGb - Before.RamUsedGb, 2);
    public double RamDeltaPercent => Math.Round(After.RamPercent - Before.RamPercent, 1);
    public int ProcessDelta => After.ActiveProcessCount - Before.ActiveProcessCount;
    public int NewlyOptimizedModules => After.OptimizedModulesCount - Before.OptimizedModulesCount;

    public SnapshotComparison(OptimizationSnapshot before, OptimizationSnapshot after)
    {
        Before = before;
        After = after;
    }

    public string SummaryText =>
        $"Modules Optimized: +{Math.Max(0, NewlyOptimizedModules)} | " +
        $"RAM Delta: {(RamDeltaGb <= 0 ? $"{Math.Abs(RamDeltaGb):F2} GB freed" : $"+{RamDeltaGb:F2} GB")} | " +
        $"Background Procs: {(ProcessDelta <= 0 ? $"{Math.Abs(ProcessDelta)} trimmed" : $"+{ProcessDelta}")}";
}
