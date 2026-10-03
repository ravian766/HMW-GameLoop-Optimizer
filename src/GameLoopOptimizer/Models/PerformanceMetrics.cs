namespace GameLoopOptimizer.Models;

public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public double CpuTotalPercent { get; set; }
    public double[] CpuCoresPercent { get; set; } = Array.Empty<double>();

    public double GpuPercent { get; set; }
    public double GpuVramUsedMb { get; set; }
    public double GpuVramTotalMb { get; set; }
    public double? GpuTemperatureC { get; set; }
    public double? CpuTemperatureC { get; set; }

    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamPercent => RamTotalGb > 0 ? (RamUsedGb / RamTotalGb) * 100 : 0;
    public double RamAvailableGb => Math.Max(0, RamTotalGb - RamUsedGb);

    public double DiskReadMbSec { get; set; }
    public double DiskWriteMbSec { get; set; }
    public double DiskActivePercent { get; set; }

    public double GameLoopCpuPercent { get; set; }
    public double GameLoopRamMb { get; set; }
    public bool IsGameLoopActive { get; set; }

    public double Fps { get; set; }
    public double AvgFps { get; set; }
    public double OnePercentLowFps { get; set; }
    public double PointOnePercentLowFps { get; set; }
    public double EstimatedFrametimeVarianceMs { get; set; }
    public double StutterIndexPercent { get; set; }

    public bool IsFpsMeasurable { get; set; } = false;
    public bool IsFpsEstimated { get; set; } = false;
    public string FpsDisplayLabel => IsFpsMeasurable ? $"{Fps:F0}" : (IsFpsEstimated ? $"{Fps:F0} (Est)" : "--");
}

public class GamingSessionState
{
    public bool IsActive { get; set; } = false;
    public DateTime? StartTime { get; set; }
    public TimeSpan Duration => IsActive && StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;

    public double PeakRamMb { get; set; }
    public double PeakCpuPercent { get; set; }
    public double AvgCpuPercent { get; set; }
    public double AvgFps { get; set; }
    public double MinOnePercentLowFps { get; set; }
    public double MinPointOnePercentLowFps { get; set; }
    public int StutterEventsCount { get; set; }
    public int MetricSamplesCount { get; set; }
    public double TotalCpuAccumulator { get; set; }
    public double TotalFpsAccumulator { get; set; }

    public List<string> AppliedTemporaryChanges { get; set; } = new();
    public List<BackupEntry> SessionBackups { get; set; } = new();
}

public class BaselineBenchmark
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(15);
    public int SampleCount { get; set; } = 0;
    public bool IsValid => SampleCount >= 3;

    public double AverageFps { get; set; }
    public double OnePercentLowFps { get; set; }
    public double PointOnePercentLowFps { get; set; }
    public double FrameTimeAverageMs => AverageFps > 0 ? Math.Round(1000.0 / AverageFps, 2) : 0;
    public double FrameTimeVarianceMs { get; set; }
    public double StutterIndexPercent { get; set; }

    public double CpuUsagePercent { get; set; }
    public double GpuUsagePercent { get; set; }
    public double RamUsageGb { get; set; }
    public double VramUsageMb { get; set; }
    public double? CpuTempC { get; set; }
    public double? GpuTempC { get; set; }

    public double GameLoopCpuPercent { get; set; }
    public double GameLoopRamMb { get; set; }
    public double DiskActivePercent { get; set; }

    public string SummaryText =>
        $"Avg FPS: {AverageFps:F1} | 1% Low: {OnePercentLowFps:F1} | Stutter: {StutterIndexPercent:F1}% | CPU: {CpuUsagePercent:F0}% | GPU: {GpuUsagePercent:F0}%";
}

public class BottleneckAnalysisResult
{
    public BottleneckType PrimaryBottleneck { get; set; } = BottleneckType.None;
    public int ConfidencePercent { get; set; } = 80;
    public string Headline { get; set; } = "Balanced System Performance";
    public string Explanation { get; set; } = "No severe hardware or software bottleneck detected.";
    public List<string> RecommendedActions { get; set; } = new();
    public List<string> ActionsToAvoid { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string BadgeColor => PrimaryBottleneck switch
    {
        BottleneckType.None => "#10B981", // Emerald green
        BottleneckType.GpuBottleneck => "#F59E0B", // Amber
        BottleneckType.CpuBottleneck => "#EF4444", // Red
        BottleneckType.RamBottleneck => "#8B5CF6", // Purple
        BottleneckType.ThermalBottleneck => "#DC2626", // Bright red
        BottleneckType.EmulatorOverheadBottleneck => "#EC4899", // Pink
        _ => "#6B7280" // Gray
    };
}

public class OptimizationComparison
{
    public BaselineBenchmark BaselineBefore { get; set; } = new();
    public BaselineBenchmark BaselineAfter { get; set; } = new();

    public double FpsDelta => Math.Round(BaselineAfter.AverageFps - BaselineBefore.AverageFps, 1);
    public double OnePercentLowDelta => Math.Round(BaselineAfter.OnePercentLowFps - BaselineBefore.OnePercentLowFps, 1);
    public double FrameTimeVarianceDeltaMs => Math.Round(BaselineAfter.FrameTimeVarianceMs - BaselineBefore.FrameTimeVarianceMs, 2);
    public double StutterReductionPercent => Math.Round(BaselineBefore.StutterIndexPercent - BaselineAfter.StutterIndexPercent, 1);

    public bool HasImproved => (FpsDelta >= 0 || OnePercentLowDelta > 0) && FrameTimeVarianceDeltaMs <= 0.5;

    public string StabilityVerdict
    {
        get
        {
            if (OnePercentLowDelta >= 5.0 || StutterReductionPercent >= 3.0)
                return "Significant Stability Improvement";
            if (FpsDelta > 2.0 && FrameTimeVarianceDeltaMs <= 0.5)
                return "Improved Performance & Stability";
            if (FrameTimeVarianceDeltaMs > 2.0)
                return "Increased Frame-Time Variance";
            if (Math.Abs(FpsDelta) <= 2.0 && Math.Abs(OnePercentLowDelta) <= 2.0)
                return "Comparable Performance";
            return "Measured Change Recorded";
        }
    }

    public string SummaryText =>
        $"FPS: {BaselineBefore.AverageFps:F0} -> {BaselineAfter.AverageFps:F0} ({(FpsDelta >= 0 ? "+" : "")}{FpsDelta:F0}) | " +
        $"1% Low: {BaselineBefore.OnePercentLowFps:F0} -> {BaselineAfter.OnePercentLowFps:F0} ({(OnePercentLowDelta >= 0 ? "+" : "")}{OnePercentLowDelta:F0}) | " +
        $"Variance: {BaselineBefore.FrameTimeVarianceMs:F1}ms -> {BaselineAfter.FrameTimeVarianceMs:F1}ms";
}

public class OptimizationRecommendationItem
{
    public string ModuleId { get; set; } = string.Empty;
    public OptimizationCategory Category { get; set; } = OptimizationCategory.WindowsConfig;
    public string SettingName { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string RecommendedValue { get; set; } = string.Empty;
    public string TechnicalReason { get; set; } = string.Empty;
    public RiskLevel Risk { get; set; } = RiskLevel.Safe;
    public bool IsSelected { get; set; } = true;
    public string ActionVerb { get; set; } = "Optimize";
}

