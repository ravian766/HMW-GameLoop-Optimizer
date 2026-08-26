using System.Diagnostics;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.Core;

public class GameLoopWatchdogService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly Func<GameLoopConfig> _getGl;
    private bool _isGameLoopRunning;
    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled && _isGameLoopRunning)
            {
                // Restore timer if disabled while running
                TimerResolutionModule.RestoreTimer();
            }
        }
    }

    public event Action<bool>? GameLoopStateChanged;

    public GameLoopWatchdogService(Func<GameLoopConfig> getGl)
    {
        _getGl = getGl;
        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start()
    {
        _timer.Start();
        Logger.Info("Watchdog", "GameLoop auto-detection daemon started (2s interval).");
    }

    public void Stop()
    {
        _timer.Stop();
        if (_isGameLoopRunning)
        {
            TimerResolutionModule.RestoreTimer();
        }
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isEnabled) return;

        try
        {
            var emulatorNames = new[] { "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe" };
            bool currentlyRunning = false;

            foreach (var name in emulatorNames)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    currentlyRunning = true;
                    foreach (var p in procs) p.Dispose();
                    break;
                }
            }

            if (currentlyRunning && !_isGameLoopRunning)
            {
                _isGameLoopRunning = true;
                OnGameLoopLaunched();
            }
            else if (!currentlyRunning && _isGameLoopRunning)
            {
                _isGameLoopRunning = false;
                OnGameLoopClosed();
            }
        }
        catch { }
    }

    private void OnGameLoopLaunched()
    {
        Logger.Success("Watchdog", "Detected GameLoop launch! Auto-activating 0.5ms high precision timer, priority boost & P-core affinity.");
        TimerResolutionModule.SetHighPrecision(0.5);
        ProcessManager.SetGameLoopPriority(ProcessPriorityClass.AboveNormal);

        long mask = ProcessManager.CalculateOptimalAffinityMask(Environment.ProcessorCount, Math.Max(1, Environment.ProcessorCount / 2));
        ProcessManager.SetGameLoopAffinity(mask);

        ProcessManager.TrimWorkingSets();
        GameLoopStateChanged?.Invoke(true);
    }

    private void OnGameLoopClosed()
    {
        Logger.Info("Watchdog", "GameLoop closed. Running post-game cleanup & restoring standard Windows timer.");
        TimerResolutionModule.RestoreTimer();
        ProcessManager.ResetGameLoopAffinity();
        int freed = ProcessManager.TrimWorkingSets();
        Logger.Success("Watchdog", $"Post-gaming maintenance: Cleaned working sets across {freed} processes.");
        GameLoopStateChanged?.Invoke(false);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
