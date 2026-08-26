using System.Collections.ObjectModel;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;

namespace GameLoopOptimizer.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<SystemInfo> _getSys;
    private readonly Func<GameLoopConfig> _getGl;
    private readonly PerformanceMonitorService _monitor;

    public HardwareInfo Hardware => _getHw();
    public SystemInfo System => _getSys();
    public GameLoopConfig GameLoop => _getGl();

    private OptimizationScore _score = new();
    public OptimizationScore Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    private HardwareRecommendations _recommendations = new();
    public HardwareRecommendations Recommendations
    {
        get => _recommendations;
        set => SetProperty(ref _recommendations, value);
    }

    private double _currentCpu;
    public double CurrentCpu
    {
        get => _currentCpu;
        set => SetProperty(ref _currentCpu, value);
    }

    private double _currentRam;
    public double CurrentRam
    {
        get => _currentRam;
        set => SetProperty(ref _currentRam, value);
    }

    private double _currentGpu;
    public double CurrentGpu
    {
        get => _currentGpu;
        set => SetProperty(ref _currentGpu, value);
    }

    private double _currentDiskMb;
    public double CurrentDiskMb
    {
        get => _currentDiskMb;
        set => SetProperty(ref _currentDiskMb, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public ObservableCollection<string> ScoreExplanations { get; } = new();

    public ICommand ScanSystemCommand { get; }
    public ICommand QuickOptimizeCommand { get; }

    public DashboardViewModel(
        Func<HardwareInfo> getHw, 
        Func<SystemInfo> getSys, 
        Func<GameLoopConfig> getGl, 
        PerformanceMonitorService monitor,
        Func<Task> onQuickOptimize)
    {
        _getHw = getHw;
        _getSys = getSys;
        _getGl = getGl;
        _monitor = monitor;

        _monitor.MetricsUpdated += (s, m) =>
        {
            CurrentCpu = m.CpuTotalPercent;
            CurrentRam = m.RamPercent;
            CurrentGpu = m.GpuPercent;
            CurrentDiskMb = Math.Round(m.DiskReadMbSec + m.DiskWriteMbSec, 1);
        };

        ScanSystemCommand = new AsyncRelayCommand(async () =>
        {
            IsScanning = true;
            try
            {
                await Task.Delay(400); // UI feedback
                RefreshDashboard();
            }
            finally
            {
                IsScanning = false;
            }
        });

        QuickOptimizeCommand = new AsyncRelayCommand(async () =>
        {
            IsScanning = true;
            try
            {
                await onQuickOptimize();
                RefreshDashboard();
            }
            finally
            {
                IsScanning = false;
            }
        });

        RefreshDashboard();
    }

    public void RefreshDashboard()
    {
        var hw = _getHw();
        var sys = _getSys();
        var gl = _getGl();

        Recommendations = RecommendationEngine.Calculate(hw);
        Score = ScoringEngine.CalculateScore(hw, sys, gl, Recommendations);

        void UpdateExplanations()
        {
            ScoreExplanations.Clear();
            foreach (var exp in Score.HonestExplanations)
            {
                ScoreExplanations.Add(exp);
            }
        }

        if (global::System.Windows.Application.Current?.Dispatcher != null && !global::System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            global::System.Windows.Application.Current.Dispatcher.Invoke(UpdateExplanations);
        }
        else
        {
            UpdateExplanations();
        }

        OnPropertyChanged(nameof(Hardware));
        OnPropertyChanged(nameof(System));
        OnPropertyChanged(nameof(GameLoop));
    }
}
