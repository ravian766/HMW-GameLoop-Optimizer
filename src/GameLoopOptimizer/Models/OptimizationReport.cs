namespace GameLoopOptimizer.Models;

/// <summary>
/// Individual result record for a specific optimization module in an optimization run.
/// </summary>
public class OptimizationReportItem
{
    public string ModuleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public OptimizationCategory Category { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
}

/// <summary>
/// Comprehensive post-optimization report providing an auditable record of all modifications.
/// </summary>
public class OptimizationReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ProfileName { get; set; } = string.Empty;
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalConsidered { get; set; }
    public List<OptimizationReportItem> Items { get; set; } = new();
    public int ScoreBefore { get; set; }
    public int ScoreAfter { get; set; }
    public SnapshotComparison? SnapshotComparison { get; set; }
    public List<string> Warnings { get; set; } = new();

    public string SummaryText =>
        $"Applied {AppliedCount} optimizations ({FailedCount} failed). " +
        $"Optimization Score: {ScoreBefore}/100 ➔ {ScoreAfter}/100 (+{Math.Max(0, ScoreAfter - ScoreBefore)} pts).";
}
