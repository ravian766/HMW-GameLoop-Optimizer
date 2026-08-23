using System.Windows.Input;
using System.Windows.Threading;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Monitoring;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.ViewModels;

public class GamingSessionViewModel : ViewModelBase
{
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<SystemInfo> _getSys;
    private readonly Func<GameLoopConfig> _getGl;
    private readonly PerformanceMonitorService _monitor;
    private readonly List<IOptimizationModule> _sessionModules;
    private readonly DispatcherTimer _sessionTimer;

    private GamingSessionState _session = new();
    public GamingSessionState Session
    {
        get => _session;
        set => SetProperty(ref _session, value);
    }

    private string _formattedDuration = "00:00:00";
    public string FormattedDuration
    {
        get => _formattedDuration;
        set => SetProperty(ref _formattedDuration, value);
    }

    private string _summaryReport = string.Empty;
    public string SummaryReport
    {
        get => _summaryReport;
        set => SetProperty(ref _summaryReport, value);
    }

    public ICommand StartSessionCommand { get; }
    public ICommand EndSessionCommand { get; }

    public GamingSessionViewModel(
        Func<HardwareInfo> getHw, 
        Func<SystemInfo> getSys, 
        Func<GameLoopConfig> getGl, 
        PerformanceMonitorService monitor,
        List<IOptimizationModule> sessionModules)
    {
        _getHw = getHw;
        _getSys = getSys;
        _getGl = getGl;
        _monitor = monitor;
        _sessionModules = sessionModules;

        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _sessionTimer.Tick += (s, e) =>
        {
            if (Session.IsActive)
            {
                FormattedDuration = Session.Duration.ToString(@"hh\:mm\:ss");
            }
        };

        _monitor.MetricsUpdated += (s, m) =>
        {
            if (Session.IsActive)
            {
                Session.MetricSamplesCount++;
                Session.TotalCpuAccumulator += m.CpuTotalPercent;
                Session.AvgCpuPercent = Math.Round(Session.TotalCpuAccumulator / Session.MetricSamplesCount, 1);

                if (m.CpuTotalPercent > Session.PeakCpuPercent) Session.PeakCpuPercent = m.CpuTotalPercent;
                if (m.GameLoopRamMb > Session.PeakRamMb) Session.PeakRamMb = m.GameLoopRamMb;

                OnPropertyChanged(nameof(Session));
            }
        };

        StartSessionCommand = new AsyncRelayCommand(StartSessionAsync, () => !Session.IsActive);
        EndSessionCommand = new AsyncRelayCommand(EndSessionAsync, () => Session.IsActive);
    }

    private async Task StartSessionAsync()
    {
        var hw = _getHw();
        var sys = _getSys();
        var gl = _getGl();

        Session = new GamingSessionState
        {
            IsActive = true,
            StartTime = DateTime.Now
        };
        _sessionTimer.Start();
        SummaryReport = string.Empty;

        // Apply session tweaks (Timer resolution, Priority, Memory, Background throttle)
        foreach (var mod in _sessionModules)
        {
            if (mod is TimerResolutionModule || mod is ProcessPriorityModule || mod is MemoryOptimizerModule || mod is BackgroundThrottleModule)
            {
                await mod.ApplyAsync(hw, sys, gl);
                Session.AppliedTemporaryChanges.Add(mod.Title);
            }
        }

        // Focus or launch GameLoop
        ProcessManager.FocusOrLaunchGameLoop(gl);

        Logger.Success("GamingSession", "Started gaming session with high-precision timer and process priority boost.");
        OnPropertyChanged(nameof(Session));
    }

    private async Task EndSessionAsync()
    {
        _sessionTimer.Stop();

        // Revert session tweaks
        foreach (var mod in _sessionModules)
        {
            if (mod is TimerResolutionModule || mod is ProcessPriorityModule || mod is BackgroundThrottleModule)
            {
                await mod.RollbackAsync(null);
            }
        }

        SummaryReport = $"Session Duration: {FormattedDuration}\n" +
                        $"Average CPU Usage: {Session.AvgCpuPercent}%\n" +
                        $"Peak CPU Spike: {Session.PeakCpuPercent}%\n" +
                        $"Peak GameLoop Memory: {Session.PeakRamMb:F0} MB\n" +
                        $"Temporary Tweaks Reverted: {Session.AppliedTemporaryChanges.Count} modules safely restored.";

        Session.IsActive = false;
        Logger.Info("GamingSession", $"Session ended. Duration: {FormattedDuration}. Reverted temporary settings.");
        OnPropertyChanged(nameof(Session));
    }
}
