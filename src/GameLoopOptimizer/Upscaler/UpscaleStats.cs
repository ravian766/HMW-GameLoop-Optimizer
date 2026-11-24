using System.Diagnostics;

namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// Lightweight frame-time tracker for the upscaler output pipeline.
/// Provides instant FPS, average FPS, and frame-time metrics.
/// </summary>
public class UpscaleStats
{
    private readonly Stopwatch _stopwatch = new();
    private readonly double[] _frameTimes;
    private int _frameIndex;
    private int _frameCount;
    private long _lastTimestamp;
    private long _totalFrames;

    public double InstantFps { get; private set; }
    public double AverageFps { get; private set; }
    public double FrameTimeMs { get; private set; }
    public double MinFrameTimeMs { get; private set; }
    public double MaxFrameTimeMs { get; private set; }
    public long TotalFrames => _totalFrames;
    public int InputWidth { get; set; }
    public int InputHeight { get; set; }
    public int OutputWidth { get; set; }
    public int OutputHeight { get; set; }
    public bool IsActive { get; set; }

    public UpscaleStats(int sampleSize = 120)
    {
        _frameTimes = new double[sampleSize];
        _stopwatch.Start();
        _lastTimestamp = _stopwatch.ElapsedTicks;
        MinFrameTimeMs = double.MaxValue;
    }

    /// <summary>
    /// Call once per rendered frame to record timing.
    /// </summary>
    public void RecordFrame()
    {
        long now = _stopwatch.ElapsedTicks;
        double elapsedMs = (now - _lastTimestamp) * 1000.0 / Stopwatch.Frequency;
        _lastTimestamp = now;

        if (elapsedMs <= 0 || elapsedMs > 1000) return; // Skip bogus samples

        _frameTimes[_frameIndex] = elapsedMs;
        _frameIndex = (_frameIndex + 1) % _frameTimes.Length;
        if (_frameCount < _frameTimes.Length) _frameCount++;
        _totalFrames++;

        FrameTimeMs = elapsedMs;
        InstantFps = 1000.0 / elapsedMs;

        if (elapsedMs < MinFrameTimeMs) MinFrameTimeMs = elapsedMs;
        if (elapsedMs > MaxFrameTimeMs) MaxFrameTimeMs = elapsedMs;

        // Compute rolling average
        double sum = 0;
        for (int i = 0; i < _frameCount; i++)
            sum += _frameTimes[i];
        double avgMs = sum / _frameCount;
        AverageFps = avgMs > 0 ? 1000.0 / avgMs : 0;
    }

    public void Reset()
    {
        _frameIndex = 0;
        _frameCount = 0;
        _totalFrames = 0;
        _lastTimestamp = _stopwatch.ElapsedTicks;
        InstantFps = 0;
        AverageFps = 0;
        FrameTimeMs = 0;
        MinFrameTimeMs = double.MaxValue;
        MaxFrameTimeMs = 0;
        IsActive = false;
    }

    public string GetOverlayText()
    {
        return $"FSR {InputWidth}×{InputHeight} → {OutputWidth}×{OutputHeight}  |  " +
               $"{InstantFps:F0} FPS  ({FrameTimeMs:F1} ms)  Avg: {AverageFps:F0}";
    }
}
