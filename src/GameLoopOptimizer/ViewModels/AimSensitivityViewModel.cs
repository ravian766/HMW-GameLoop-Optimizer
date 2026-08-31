using System.Collections.ObjectModel;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.ViewModels;

public class AimSensitivityViewModel : ViewModelBase
{
    private readonly Func<int> _getResHeight;
    private readonly IEventAggregator _eventAggregator;
    private readonly MouseBenchmarkService _mouseBenchmark = new();

    private int _selectedMouseDpi = 800;
    public int SelectedMouseDpi
    {
        get => _selectedMouseDpi;
        set
        {
            if (SetProperty(ref _selectedMouseDpi, value))
            {
                RecalculateSensitivity();
            }
        }
    }

    public ObservableCollection<int> DpiOptions { get; } = new() { 400, 800, 1000, 1200, 1600, 2400, 3200 };

    private AimPlaystyle _selectedPlaystyle = AimPlaystyle.BalancedCompetitive;
    public AimPlaystyle SelectedPlaystyle
    {
        get => _selectedPlaystyle;
        set
        {
            if (SetProperty(ref _selectedPlaystyle, value))
            {
                RecalculateSensitivity();
            }
        }
    }

    public ObservableCollection<AimPlaystyle> PlaystyleOptions { get; } = new()
    {
        AimPlaystyle.PrecisionLowSens,
        AimPlaystyle.BalancedCompetitive,
        AimPlaystyle.HighSensFastFlick
    };

    private SensitivityProfileResult _sensitivityResult = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive);
    public SensitivityProfileResult SensitivityResult
    {
        get => _sensitivityResult;
        set => SetProperty(ref _sensitivityResult, value);
    }

    private MouseBenchmarkMetrics _mouseMetrics = new();
    public MouseBenchmarkMetrics MouseMetrics
    {
        get => _mouseMetrics;
        set => SetProperty(ref _mouseMetrics, value);
    }

    private bool _isBenchmarkingMouse;
    public bool IsBenchmarkingMouse
    {
        get => _isBenchmarkingMouse;
        set => SetProperty(ref _isBenchmarkingMouse, value);
    }

    public ICommand RecalculateSensitivityCommand { get; }
    public ICommand StartMouseBenchmarkCommand { get; }
    public ICommand StopMouseBenchmarkCommand { get; }

    public AimSensitivityViewModel(Func<int> getResHeight, IEventAggregator? eventAggregator = null)
    {
        _getResHeight = getResHeight;
        _eventAggregator = eventAggregator ?? EventAggregator.Default;

        RecalculateSensitivityCommand = new RelayCommand(() => RecalculateSensitivity());

        StartMouseBenchmarkCommand = new RelayCommand(() =>
        {
            IsBenchmarkingMouse = true;
            _mouseBenchmark.Start();
            MouseMetrics = _mouseBenchmark.GetCurrentMetrics();
            _eventAggregator.Publish(new StatusNotificationMessage("Mouse Polling Benchmark active. Move your cursor inside the test canvas."));
        });

        StopMouseBenchmarkCommand = new RelayCommand(() =>
        {
            _mouseBenchmark.Stop();
            IsBenchmarkingMouse = false;
            MouseMetrics = _mouseBenchmark.GetCurrentMetrics();
            _eventAggregator.Publish(new StatusNotificationMessage("Mouse Polling Benchmark stopped."));
        });

        RecalculateSensitivity();
    }

    public void RecalculateSensitivity()
    {
        int h = _getResHeight();
        SensitivityResult = SensitivityCalculator.Calculate(SelectedMouseDpi, SelectedPlaystyle, h > 0 ? h : 1080);
    }

    public void RecordMouseSample()
    {
        if (IsBenchmarkingMouse)
        {
            MouseMetrics = _mouseBenchmark.RecordMovement();
        }
    }
}
