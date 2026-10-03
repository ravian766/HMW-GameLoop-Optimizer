using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;

namespace GameLoopOptimizer.Core;

public class PerformanceBaselineService
{
    private readonly PerformanceMonitorService _monitorService;
    private readonly List<PerformanceMetrics> _benchmarkSamples = new();
    private readonly object _lock = new();

    public BaselineBenchmark? BaselineBefore { get; private set; }
    public BaselineBenchmark? BaselineAfter { get; private set; }
    public OptimizationComparison? LatestComparison { get; private set; }

    public bool IsBenchmarking { get; private set; } = false;
    public double BenchmarkProgressPercent { get; private set; } = 0;

    public event EventHandler<BaselineBenchmark>? BaselineCaptured;
    public event EventHandler<OptimizationComparison>? ComparisonGenerated;

    public PerformanceBaselineService(PerformanceMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    /// <summary>
    /// Captures a baseline from current rolling telemetry history (instant snapshot).
    /// </summary>
    public BaselineBenchmark CaptureInstantBaseline(string label = "Snapshot")
    {
        var history = _monitorService.History;
        var baseline = ComputeBenchmarkFromSamples(history);
        Logger.Info("PerformanceBaseline", $"Captured instant baseline ({label}): {baseline.SummaryText}");
        return baseline;
    }

    /// <summary>
    /// Executes a structured benchmark run for a specified duration in seconds.
    /// </summary>
    public async Task<BaselineBenchmark> RunStructuredBenchmarkAsync(int durationSeconds = 15, IProgress<double>? progress = null)
    {
        if (durationSeconds < 3) durationSeconds = 3;
        IsBenchmarking = true;
        _benchmarkSamples.Clear();

        var startTime = DateTime.Now;
        var endTime = startTime.AddSeconds(durationSeconds);

        void OnMetrics(object? s, PerformanceMetrics m)
        {
            lock (_lock)
            {
                _benchmarkSamples.Add(m);
            }
        }

        _monitorService.MetricsUpdated += OnMetrics;

        try
        {
            while (DateTime.Now < endTime)
            {
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                double pct = Math.Clamp((elapsed / durationSeconds) * 100.0, 0, 100);
                BenchmarkProgressPercent = pct;
                progress?.Report(pct);
                await Task.Delay(250);
            }
        }
        finally
        {
            _monitorService.MetricsUpdated -= OnMetrics;
            IsBenchmarking = false;
            BenchmarkProgressPercent = 100;
            progress?.Report(100);
        }

        List<PerformanceMetrics> captured;
        lock (_lock)
        {
            captured = _benchmarkSamples.ToList();
        }

        // If not enough samples collected during dedicated run, fallback to rolling monitor history
        if (captured.Count < 3)
        {
            captured = _monitorService.History.ToList();
        }

        var result = ComputeBenchmarkFromSamples(captured);
        result.Duration = TimeSpan.FromSeconds(durationSeconds);
        Logger.Info("PerformanceBaseline", $"Completed structured {durationSeconds}s benchmark: {result.SummaryText}");
        return result;
    }

    public void RecordBaselineBefore(BaselineBenchmark baseline)
    {
        BaselineBefore = baseline;
        BaselineCaptured?.Invoke(this, baseline);
    }

    public void RecordBaselineAfter(BaselineBenchmark baseline)
    {
        BaselineAfter = baseline;
        BaselineCaptured?.Invoke(this, baseline);

        if (BaselineBefore != null)
        {
            LatestComparison = new OptimizationComparison
            {
                BaselineBefore = BaselineBefore,
                BaselineAfter = BaselineAfter
            };
            ComparisonGenerated?.Invoke(this, LatestComparison);
            Logger.Success("PerformanceBaseline", $"Generated Before/After Comparison: {LatestComparison.SummaryText} ({LatestComparison.StabilityVerdict})");
        }
    }

    private static BaselineBenchmark ComputeBenchmarkFromSamples(IReadOnlyList<PerformanceMetrics> samples)
    {
        var b = new BaselineBenchmark
        {
            Timestamp = DateTime.Now,
            SampleCount = samples.Count
        };

        if (samples.Count == 0)
        {
            return b;
        }

        // Filter valid FPS samples
        var fpsSamples = samples.Where(s => s.Fps > 0).Select(s => s.Fps).ToList();
        if (fpsSamples.Count > 0)
        {
            b.AverageFps = Math.Round(fpsSamples.Average(), 1);
            var sortedFps = fpsSamples.OrderBy(f => f).ToList(); // lowest to highest
            int idx1Pct = Math.Clamp((int)Math.Floor(sortedFps.Count * 0.01), 0, sortedFps.Count - 1);
            b.OnePercentLowFps = Math.Round(sortedFps[idx1Pct], 1);
            b.PointOnePercentLowFps = Math.Round(sortedFps[0], 1);
        }
        else
        {
            b.AverageFps = 0;
            b.OnePercentLowFps = 0;
            b.PointOnePercentLowFps = 0;
        }

        // Frame-time variance and stutter calculation
        var varianceSamples = samples.Where(s => s.EstimatedFrametimeVarianceMs > 0).Select(s => s.EstimatedFrametimeVarianceMs).ToList();
        b.FrameTimeVarianceMs = varianceSamples.Count > 0 ? Math.Round(varianceSamples.Average(), 2) : 0;

        var stutterSamples = samples.Select(s => s.StutterIndexPercent).ToList();
        b.StutterIndexPercent = stutterSamples.Count > 0 ? Math.Round(stutterSamples.Average(), 1) : 0;

        // System Resource Averages
        b.CpuUsagePercent = Math.Round(samples.Average(s => s.CpuTotalPercent), 1);
        b.GpuUsagePercent = Math.Round(samples.Average(s => s.GpuPercent), 1);
        b.RamUsageGb = Math.Round(samples.Average(s => s.RamUsedGb), 2);
        b.VramUsageMb = Math.Round(samples.Average(s => s.GpuVramUsedMb), 0);
        b.DiskActivePercent = Math.Round(samples.Average(s => s.DiskActivePercent), 1);

        // GameLoop specific averages
        var glActiveSamples = samples.Where(s => s.IsGameLoopActive).ToList();
        if (glActiveSamples.Count > 0)
        {
            b.GameLoopCpuPercent = Math.Round(glActiveSamples.Average(s => s.GameLoopCpuPercent), 1);
            b.GameLoopRamMb = Math.Round(glActiveSamples.Average(s => s.GameLoopRamMb), 0);
        }

        // Temperatures
        var cpuTemps = samples.Where(s => s.CpuTemperatureC.HasValue).Select(s => s.CpuTemperatureC!.Value).ToList();
        if (cpuTemps.Count > 0) b.CpuTempC = Math.Round(cpuTemps.Average(), 1);

        var gpuTemps = samples.Where(s => s.GpuTemperatureC.HasValue).Select(s => s.GpuTemperatureC!.Value).ToList();
        if (gpuTemps.Count > 0) b.GpuTempC = Math.Round(gpuTemps.Average(), 1);

        return b;
    }
}
