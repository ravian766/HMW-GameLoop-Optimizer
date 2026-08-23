using System.Diagnostics;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Monitoring;

public class PerformanceMonitorService : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    private readonly System.Timers.Timer _timer;
    private readonly List<PerformanceMetrics> _history = new();
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

    public PerformanceMonitorService()
    {
        InitializeCounters();

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (s, e) => CollectMetrics();
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
            // Fallback
        }

        GetSystemTimes(out _prevIdleTime, out _prevKernelTime, out _prevUserTime);
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
            if (GetSystemTimes(out long idle, out long kernel, out long user))
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
            var mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref mem))
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

            // Estimated frame-time variance index
            metrics.EstimatedFrametimeVarianceMs = Math.Round(Math.Max(1.0, (metrics.CpuTotalPercent / 20.0) + (metrics.DiskReadMbSec > 10 ? 3.0 : 0.5)), 2);

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
