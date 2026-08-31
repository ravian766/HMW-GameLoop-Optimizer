namespace GameLoopOptimizer.Monitoring;

public class FrameTimeSnapshot
{
    public double InstantFps { get; set; }
    public double AvgFps { get; set; }
    public double OnePercentLowFps { get; set; }
    public double PointOnePercentLowFps { get; set; }
    public double FrameTimeVarianceMs { get; set; }
    public double StutterIndexPercent { get; set; }
    public bool IsStuttering { get; set; }
}

public class FrameTimeTracker
{
    private readonly List<double> _frameTimesMs = new();
    private readonly object _lock = new();
    private const int MaxSampleSize = 120;

    public void AddSample(double fps, double varianceMs = 0)
    {
        if (fps <= 0) return;
        double frameTimeMs = 1000.0 / fps;

        lock (_lock)
        {
            _frameTimesMs.Add(frameTimeMs);
            if (_frameTimesMs.Count > MaxSampleSize)
            {
                _frameTimesMs.RemoveAt(0);
            }
        }
    }

    public FrameTimeSnapshot GetSnapshot(double fallbackFps = 60)
    {
        lock (_lock)
        {
            if (_frameTimesMs.Count == 0)
            {
                return new FrameTimeSnapshot
                {
                    InstantFps = fallbackFps,
                    AvgFps = fallbackFps,
                    OnePercentLowFps = Math.Max(1, fallbackFps * 0.85),
                    PointOnePercentLowFps = Math.Max(1, fallbackFps * 0.70),
                    FrameTimeVarianceMs = 0.5,
                    StutterIndexPercent = 0.0,
                    IsStuttering = false
                };
            }

            var sorted = _frameTimesMs.OrderByDescending(t => t).ToList(); // Higher frame time = lower FPS
            double avgFrameTime = _frameTimesMs.Average();
            double avgFps = avgFrameTime > 0 ? 1000.0 / avgFrameTime : fallbackFps;

            // 1% Low is the 99th percentile highest frame time
            int idx1Pct = Math.Clamp((int)Math.Ceiling(sorted.Count * 0.01) - 1, 0, sorted.Count - 1);
            double frameTime1Pct = sorted[idx1Pct];
            double onePercentLow = frameTime1Pct > 0 ? 1000.0 / frameTime1Pct : avgFps * 0.85;

            // 0.1% Low is the 99.9th percentile highest frame time (the worst spike)
            int idx01Pct = 0; // Worst frame time in sample
            double frameTime01Pct = sorted[idx01Pct];
            double pointOnePercentLow = frameTime01Pct > 0 ? 1000.0 / frameTime01Pct : avgFps * 0.70;

            // Standard deviation of frame times
            double sumOfSquares = _frameTimesMs.Select(val => (val - avgFrameTime) * (val - avgFrameTime)).Sum();
            double variance = Math.Sqrt(sumOfSquares / _frameTimesMs.Count);

            // Stutter index: percent of frames that deviate more than 50% from average
            int stutterFrames = _frameTimesMs.Count(t => t > avgFrameTime * 1.5);
            double stutterIndex = (_frameTimesMs.Count > 0) ? ((double)stutterFrames / _frameTimesMs.Count) * 100.0 : 0.0;

            double currentInstantFps = _frameTimesMs.Count > 0 ? 1000.0 / _frameTimesMs.Last() : fallbackFps;

            return new FrameTimeSnapshot
            {
                InstantFps = Math.Round(currentInstantFps, 1),
                AvgFps = Math.Round(avgFps, 1),
                OnePercentLowFps = Math.Round(Math.Min(avgFps, onePercentLow), 1),
                PointOnePercentLowFps = Math.Round(Math.Min(onePercentLow, pointOnePercentLow), 1),
                FrameTimeVarianceMs = Math.Round(variance, 2),
                StutterIndexPercent = Math.Round(stutterIndex, 1),
                IsStuttering = variance > 3.5 || stutterIndex > 5.0
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _frameTimesMs.Clear();
        }
    }
}
