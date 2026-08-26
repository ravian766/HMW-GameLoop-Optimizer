using System.Diagnostics;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class MouseBenchmarkMetrics
{
    public double CurrentHz { get; set; }
    public double PeakHz { get; set; }
    public double AverageHz { get; set; }
    public double IntervalJitterMs { get; set; }
    public int SampleCount { get; set; }
    public bool IsEnhancedPointerPrecisionEnabled { get; set; }
    public string RatingText { get; set; } = "Ready";
    public string Recommendation { get; set; } = string.Empty;
}

public class MouseBenchmarkService
{
    private readonly Stopwatch _stopwatch = new();
    private readonly List<double> _intervalsMs = new();
    private long _lastTimestampTicks;
    private double _peakHz;
    private int _sampleCount;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        _intervalsMs.Clear();
        _peakHz = 0;
        _sampleCount = 0;
        _stopwatch.Restart();
        _lastTimestampTicks = _stopwatch.ElapsedTicks;
        IsRunning = true;
    }

    public void Stop()
    {
        _stopwatch.Stop();
        IsRunning = false;
    }

    public MouseBenchmarkMetrics RecordMovement()
    {
        if (!IsRunning) return GetCurrentMetrics();

        long now = _stopwatch.ElapsedTicks;
        long diffTicks = now - _lastTimestampTicks;
        _lastTimestampTicks = now;

        double intervalMs = (double)diffTicks / Stopwatch.Frequency * 1000.0;

        if (intervalMs > 0.1 && intervalMs < 100.0)
        {
            _intervalsMs.Add(intervalMs);
            if (_intervalsMs.Count > 100) _intervalsMs.RemoveAt(0);

            double instantHz = 1000.0 / intervalMs;
            if (instantHz > _peakHz && instantHz < 4000.0) _peakHz = instantHz;
            _sampleCount++;
        }

        return GetCurrentMetrics();
    }

    public MouseBenchmarkMetrics GetCurrentMetrics()
    {
        double avgInterval = _intervalsMs.Count > 0 ? _intervalsMs.Average() : 0;
        double currentHz = avgInterval > 0 ? 1000.0 / avgInterval : 0;

        double jitter = 0;
        if (_intervalsMs.Count > 1)
        {
            double sumSquares = _intervalsMs.Sum(i => Math.Pow(i - avgInterval, 2));
            jitter = Math.Sqrt(sumSquares / _intervalsMs.Count);
        }

        bool accel = CheckEnhancedPointerPrecision();

        string rating = "Move mouse continuously in the box";
        string tip = "Keep moving your cursor to sample your gaming mouse sensor.";

        if (_sampleCount > 30)
        {
            if (_peakHz >= 850)
            {
                rating = "⚡ 1000 Hz Ultra-Fast Gaming Polling (Flawless)";
                tip = "Excellent! Sub-1ms input latency ensures immediate recoil response in GameLoop.";
            }
            else if (_peakHz >= 420)
            {
                rating = "⚡ 500 Hz High-Speed Polling (Optimal)";
                tip = "Solid 2ms polling rate with very low CPU scheduling overhead.";
            }
            else if (_peakHz >= 110)
            {
                rating = "⚠️ 125 Hz Standard Office Polling";
                tip = "Consider switching your mouse software (e.g. Logitech/Razer) to 500Hz or 1000Hz for smoother micro-aim.";
            }
            else
            {
                rating = "❓ Low Sampling Rate";
                tip = "Move mouse faster across the testing zone to measure peak frequency.";
            }
        }

        if (accel)
        {
            tip += " | ⚠️ Windows Pointer Precision (Mouse Acceleration) is ON. Disable it in Windows Settings for 1:1 muscle memory.";
        }

        return new MouseBenchmarkMetrics
        {
            CurrentHz = Math.Round(currentHz, 0),
            PeakHz = Math.Round(_peakHz, 0),
            AverageHz = Math.Round(currentHz, 0),
            IntervalJitterMs = Math.Round(jitter, 2),
            SampleCount = _sampleCount,
            IsEnhancedPointerPrecisionEnabled = accel,
            RatingText = rating,
            Recommendation = tip
        };
    }

    public static bool CheckEnhancedPointerPrecision()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
            if (key != null)
            {
                var val = key.GetValue("MouseSpeed")?.ToString();
                return val == "1" || val == "2";
            }
        }
        catch { }
        return false;
    }
}
