using System.Collections.ObjectModel;
using System.Windows.Threading;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;

namespace GameLoopOptimizer.ViewModels;

public class CoreLoadItem : ViewModelBase
{
    public int CoreIndex { get; set; }
    public string CoreLabel { get; set; } = string.Empty;
    private double _loadPercent;
    public double LoadPercent
    {
        get => _loadPercent;
        set => SetProperty(ref _loadPercent, value);
    }
}

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
    public ObservableCollection<CoreLoadItem> CoreLoads { get; } = new();

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

                if (m.CpuCoresPercent != null && m.CpuCoresPercent.Length > 0)
                {
                    while (CoreLoads.Count < m.CpuCoresPercent.Length)
                    {
                        int idx = CoreLoads.Count;
                        CoreLoads.Add(new CoreLoadItem
                        {
                            CoreIndex = idx,
                            CoreLabel = $"T{idx}",
                            LoadPercent = m.CpuCoresPercent[idx]
                        });
                    }
                    for (int i = 0; i < m.CpuCoresPercent.Length && i < CoreLoads.Count; i++)
                    {
                        CoreLoads[i].LoadPercent = m.CpuCoresPercent[i];
                    }
                }
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
