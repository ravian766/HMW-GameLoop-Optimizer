using System.Diagnostics;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Monitoring;

public class PerformanceMonitorService : IDisposable
{


    private readonly System.Timers.Timer _timer;
    private readonly List<PerformanceMetrics> _history = new();
    private readonly FrameTimeTracker _frameTimeTracker = new();
    private readonly object _lock = new();

    private long _prevIdleTime;
    private long _prevKernelTime;
    private long _prevUserTime;

    private PerformanceCounter? _diskReadCounter;
    private PerformanceCounter? _diskWriteCounter;
    private PerformanceCounter? _gpuCounter;

    public event EventHandler<PerformanceMetrics>? MetricsUpdated;
    public IReadOnlyList<PerformanceMetrics> History
    {
        get
        {
            lock (_lock) return _history.ToList();
        }
    }

    public PerformanceMetrics LatestMetrics { get; private set; } = new();

    private DateTime _lastTempCheck = DateTime.MinValue;
    private double? _cachedCpuTemp = null;

    private Func<GameLoopConfig>? _getGlConfig;
    private Func<string?>? _getTargetPackage;
    private AdbTelemetrySnapshot? _lastAdbTelemetry;
    private int _adbFetchInProgress = 0;

    public void SetInterval(int milliseconds)
    {
        if (milliseconds < 100) milliseconds = 100;
        _timer.Interval = milliseconds;
        Logger.Info("PerformanceMonitor", $"Telemetry monitor interval updated to {milliseconds}ms.");
    }

    public PerformanceMonitorService(Func<GameLoopConfig>? getGlConfig = null, Func<string?>? getTargetPackage = null)
    {
        _getGlConfig = getGlConfig;
        _getTargetPackage = getTargetPackage;
        InitializeCounters();

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (s, e) => CollectMetrics();
    }

    public void ConfigureAdbTelemetry(Func<GameLoopConfig> getGlConfig, Func<string?> getTargetPackage)
    {
        _getGlConfig = getGlConfig;
        _getTargetPackage = getTargetPackage;
    }

    private void InitializeCounters()
    {
        try
        {
            _diskReadCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", "_Total");
            _diskWriteCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", "_Total");
            _diskReadCounter.NextValue();
            _diskWriteCounter.NextValue();
        }
        catch
        {
            // Fallback if perf counters restricted
        }

        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var names = category.GetInstanceNames().Where(n => n.Contains("engtype_3D")).ToArray();
            if (names.Length > 0)
            {
                _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", names[0]);
                _gpuCounter.NextValue();
            }
        }
        catch
        {
            // Graceful fallback
        }

        NativeMethods.GetSystemTimes(out _prevIdleTime, out _prevKernelTime, out _prevUserTime);
    }

    private void DetectTemperatures(PerformanceMetrics metrics)
    {
        try
        {
            if ((DateTime.Now - _lastTempCheck).TotalSeconds > 3)
            {
                _lastTempCheck = DateTime.Now;
                _cachedCpuTemp = QueryCpuTemperature();
            }

            metrics.CpuTemperatureC = _cachedCpuTemp;
            metrics.GpuTemperatureC = null;
        }
        catch
        {
            metrics.CpuTemperatureC = null;
            metrics.GpuTemperatureC = null;
        }
    }

    private static double? QueryCpuTemperature()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var item in searcher.Get())
            {
                var val = item["CurrentTemperature"];
                if (val != null)
                {
                    double kelvin10 = Convert.ToDouble(val);
                    double celsius = (kelvin10 - 2732.0) / 10.0;
                    if (celsius is >= 10.0 and <= 120.0)
                    {
                        return Math.Round(celsius, 1);
                    }
                }
            }
        }
        catch { }
        return null;
    }

    public void Start()
    {
        _timer.Start();
        Logger.Info("PerformanceMonitor", "Telemetry monitor started (1-sec interval).");
    }

    public void Stop()
    {
        _timer.Stop();
        Logger.Info("PerformanceMonitor", "Telemetry monitor stopped.");
    }

    private void CollectMetrics()
    {
        try
        {
            var metrics = new PerformanceMetrics
            {
                Timestamp = DateTime.Now
            };

            // 1. CPU Usage
            if (NativeMethods.GetSystemTimes(out long idle, out long kernel, out long user))
            {
                long usrDiff = user - _prevUserTime;
                long kerDiff = kernel - _prevKernelTime;
                long idlDiff = idle - _prevIdleTime;

                long sysDiff = (usrDiff + kerDiff);
                if (sysDiff > 0)
                {
                    double cpu = (double)(sysDiff - idlDiff) * 100.0 / sysDiff;
                    metrics.CpuTotalPercent = Math.Clamp(Math.Round(cpu, 1), 0, 100);
                }

                _prevIdleTime = idle;
                _prevKernelTime = kernel;
                _prevUserTime = user;
            }

            // 2. RAM Usage
            var mem = new NativeMethods.MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
            if (NativeMethods.GlobalMemoryStatusEx(ref mem))
            {
                metrics.RamTotalGb = Math.Round((double)mem.ullTotalPhys / (1024 * 1024 * 1024), 2);
                metrics.RamUsedGb = Math.Round((double)(mem.ullTotalPhys - mem.ullAvailPhys) / (1024 * 1024 * 1024), 2);
            }

            // 3. Disk I/O
            try
            {
                if (_diskReadCounter != null)
                {
                    metrics.DiskReadMbSec = Math.Round(_diskReadCounter.NextValue() / (1024 * 1024), 2);
                }
                if (_diskWriteCounter != null)
                {
                    metrics.DiskWriteMbSec = Math.Round(_diskWriteCounter.NextValue() / (1024 * 1024), 2);
                }
            }
            catch { }

            // 4. GPU Usage
            try
            {
                if (_gpuCounter != null)
                {
                    metrics.GpuPercent = Math.Clamp(Math.Round(_gpuCounter.NextValue(), 1), 0, 100);
                }
            }
            catch { }

            // 5. GameLoop Process Metrics
            try
            {
                var glProcs = Process.GetProcessesByName("AppMarket")
                    .Concat(Process.GetProcessesByName("AndroidEmulator"))
                    .Concat(Process.GetProcessesByName("AndroidEmulatorEn"))
                    .Concat(Process.GetProcessesByName("aow_exe"))
                    .ToList();

                if (glProcs.Count > 0)
                {
                    metrics.IsGameLoopActive = true;
                    long totalGlMem = 0;

                    foreach (var p in glProcs)
                    {
                        try
                        {
                            totalGlMem += p.WorkingSet64;
                        }
                        catch { }
                        finally
                        {
                            p.Dispose();
                        }
                    }

                    metrics.GameLoopRamMb = Math.Round((double)totalGlMem / (1024 * 1024), 1);
                }
            }
            catch { }

            // Temperature monitoring (honest WMI thermal zone check; null if unavailable)
            DetectTemperatures(metrics);

            // 6. In-VM Telemetry Bridge (Non-invasive via surfaceflinger frame timestamps)
            if (metrics.IsGameLoopActive && _getGlConfig != null)
            {
                if (Interlocked.CompareExchange(ref _adbFetchInProgress, 1, 0) == 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var gl = _getGlConfig();
                            string? targetPkg = _getTargetPackage?.Invoke();
                            var snap = await AdbTelemetryService.FetchTelemetryAsync(targetPkg, gl);
                            _lastAdbTelemetry = snap.IsConnected ? snap : null;
                        }
                        catch
                        {
                            _lastAdbTelemetry = null;
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _adbFetchInProgress, 0);
                        }
                    });
                }
            }
            else
            {
                _lastAdbTelemetry = null;
            }

            if (_lastAdbTelemetry != null && _lastAdbTelemetry.IsConnected && _lastAdbTelemetry.Fps > 0)
            {
                metrics.Fps = _lastAdbTelemetry.Fps;
                metrics.AvgFps = _lastAdbTelemetry.Fps;
                metrics.OnePercentLowFps = _lastAdbTelemetry.OnePercentLowFps;
                metrics.PointOnePercentLowFps = Math.Max(0, _lastAdbTelemetry.OnePercentLowFps * 0.85);
                metrics.EstimatedFrametimeVarianceMs = _lastAdbTelemetry.FrametimeVarianceMs;
                metrics.StutterIndexPercent = Math.Clamp(_lastAdbTelemetry.DroppedFramesRatio * 100.0, 0, 100);
                metrics.IsFpsMeasurable = true;
                metrics.IsFpsEstimated = false;
            }
            else
            {
                metrics.Fps = 0;
                metrics.AvgFps = 0;
                metrics.OnePercentLowFps = 0;
                metrics.PointOnePercentLowFps = 0;
                metrics.EstimatedFrametimeVarianceMs = 0;
                metrics.StutterIndexPercent = 0;
                metrics.IsFpsMeasurable = false;
                metrics.IsFpsEstimated = false;
                _frameTimeTracker.Reset();
            }

            LatestMetrics = metrics;

            lock (_lock)
            {
                _history.Add(metrics);
                // Keep 60 seconds rolling history
                while (_history.Count > 60)
                {
                    _history.RemoveAt(0);
                }
            }

            MetricsUpdated?.Invoke(this, metrics);
        }
        catch
        {
            // Ignore monitor cycle errors to prevent crashing
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _diskReadCounter?.Dispose();
        _diskWriteCounter?.Dispose();
        _gpuCounter?.Dispose();
    }
}
