using System.Collections.ObjectModel;
using System.Windows.Threading;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;

namespace GameLoopOptimizer.ViewModels;

public class MonitorViewModel : ViewModelBase
{
    private readonly PerformanceMonitorService _monitor;
    private readonly Dispatcher _dispatcher;

    private PerformanceMetrics _metrics = new();
    public PerformanceMetrics Metrics
    {
        get => _metrics;
        set => SetProperty(ref _metrics, value);
    }

    public ObservableCollection<double> CpuHistory { get; } = new();
    public ObservableCollection<double> GpuHistory { get; } = new();
    public ObservableCollection<double> RamHistory { get; } = new();
    public ObservableCollection<double> DiskHistory { get; } = new();

    public MonitorViewModel(PerformanceMonitorService monitor)
    {
        _monitor = monitor;
        _dispatcher = Dispatcher.CurrentDispatcher;

        // Initialize with zeros
        for (int i = 0; i < 40; i++)
        {
            CpuHistory.Add(0);
            GpuHistory.Add(0);
            RamHistory.Add(0);
            DiskHistory.Add(0);
        }

        _monitor.MetricsUpdated += (s, m) =>
        {
            _dispatcher.InvokeAsync(() =>
            {
                Metrics = m;

                PushMetric(CpuHistory, m.CpuTotalPercent);
                PushMetric(GpuHistory, m.GpuPercent);
                PushMetric(RamHistory, m.RamPercent);
                PushMetric(DiskHistory, Math.Min(100, (m.DiskReadMbSec + m.DiskWriteMbSec) * 2));
            });
        };
    }

    private static void PushMetric(ObservableCollection<double> col, double val)
    {
        col.Add(Math.Max(0, val));
        if (col.Count > 40)
        {
            col.RemoveAt(0);
        }
    }
}
