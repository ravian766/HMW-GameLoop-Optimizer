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

    // Deep Junk Cleaner Properties
    private DeepCleanScanResult _junkScanResult = new();
    public DeepCleanScanResult JunkScanResult
    {
        get => _junkScanResult;
        set => SetProperty(ref _junkScanResult, value);
    }

    private bool _isCleaningJunk;
    public bool IsCleaningJunk
    {
        get => _isCleaningJunk;
        set => SetProperty(ref _isCleaningJunk, value);
    }

    private string _cleanerStatusMessage = string.Empty;
    public string CleanerStatusMessage
    {
        get => _cleanerStatusMessage;
        set => SetProperty(ref _cleanerStatusMessage, value);
    }

    // Emulator Health Diagnostic Properties
    private EmulatorHealthReport _healthReport = new();
    public EmulatorHealthReport HealthReport
    {
        get => _healthReport;
        set => SetProperty(ref _healthReport, value);
    }

    private bool _isRunningDiagnostic;
    public bool IsRunningDiagnostic
    {
        get => _isRunningDiagnostic;
        set => SetProperty(ref _isRunningDiagnostic, value);
    }

    public ObservableCollection<string> ScoreExplanations { get; } = new();

    public ICommand ScanSystemCommand { get; }
    public ICommand QuickOptimizeCommand { get; }
    public ICommand ScanJunkCommand { get; }
    public ICommand ExecuteDeepCleanCommand { get; }
    public ICommand RunDiagnosticCommand { get; }
    public ICommand AutoRepairIssuesCommand { get; }

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

        ScanJunkCommand = new AsyncRelayCommand(async () =>
        {
            IsCleaningJunk = true;
            CleanerStatusMessage = "Scanning emulator caches, crash dumps, and temp buffers...";
            try
            {
                var gl = _getGl();
                JunkScanResult = await Task.Run(() => DeepCleanerService.ScanJunk(gl));
                CleanerStatusMessage = $"Found {JunkScanResult.TotalSizeFormatted} junk across {JunkScanResult.TotalFileCount} files.";
            }
            finally
            {
                IsCleaningJunk = false;
            }
        });

        ExecuteDeepCleanCommand = new AsyncRelayCommand(async () =>
        {
            IsCleaningJunk = true;
            CleanerStatusMessage = "Purging shader caches, crash dumps, and temp files...";
            try
            {
                var gl = _getGl();
                var res = await DeepCleanerService.CleanJunkAsync(JunkScanResult, gl, msg => CleanerStatusMessage = msg);
                CleanerStatusMessage = res.Message;
                JunkScanResult = await Task.Run(() => DeepCleanerService.ScanJunk(gl));
            }
            finally
            {
                IsCleaningJunk = false;
            }
        });

        RunDiagnosticCommand = new AsyncRelayCommand(async () =>
        {
            IsRunningDiagnostic = true;
            try
            {
                var gl = _getGl();
                var hw = _getHw();
                HealthReport = await Task.Run(() => EmulatorDiagnosticService.RunDiagnostic(gl, hw));
            }
            finally
            {
                IsRunningDiagnostic = false;
            }
        });

        AutoRepairIssuesCommand = new AsyncRelayCommand(async () =>
        {
            IsRunningDiagnostic = true;
            try
            {
                var gl = _getGl();
                var hw = _getHw();
                await EmulatorDiagnosticService.AutoFixIssuesAsync(gl, hw);
                HealthReport = await Task.Run(() => EmulatorDiagnosticService.RunDiagnostic(gl, hw));
            }
            finally
            {
                IsRunningDiagnostic = false;
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
        HealthReport = EmulatorDiagnosticService.RunDiagnostic(gl, hw);
        JunkScanResult = DeepCleanerService.ScanJunk(gl);

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
