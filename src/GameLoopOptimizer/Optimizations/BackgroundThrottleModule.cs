using System.Diagnostics;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class BackgroundThrottleModule : IOptimizationModule
{
    public string Id => "bg_process_throttle";
    public string Title => "Background App Overhead Throttle";
    public OptimizationCategory Category => OptimizationCategory.BackgroundProcess;
    public RiskLevel RiskLevel => RiskLevel.Low;
    public string Description => "Lowers CPU scheduling priority for non-essential background applications (e.g. background updaters, cloud sync) to prevent emulator CPU core contention.";
    public string TechnicalRationale => "Instead of forcibly terminating user applications, lowering their process priority to 'Below Normal' allows them to run safely without interrupting GameLoop's primary execution threads.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Normal";
    public string RecommendedStateDisplay => "Throttled during Session";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    private static readonly List<int> _throttledPids = new();

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        var hogs = ProcessManager.GetHighOverheadProcesses();
        sys.HighCpuProcessesCount = hogs.Count;
        CurrentStateDisplay = hogs.Count > 0 ? $"{hogs.Count} background apps found" : "Clean Background";
        IsOptimized = _throttledPids.Count > 0 || hogs.Count == 0;
        State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        return await Task.Run(() =>
        {
            try
            {
                var hogs = ProcessManager.GetHighOverheadProcesses();
                int throttledCount = 0;
                _throttledPids.Clear();

                foreach (var item in hogs)
                {
                    try
                    {
                        var p = Process.GetProcessById(item.Id);
                        if (p.PriorityClass == ProcessPriorityClass.Normal)
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                            _throttledPids.Add(item.Id);
                            throttledCount++;
                        }
                    }
                    catch
                    {
                        // Ignore access restrictions on protected processes
                    }
                }

                IsOptimized = true;
                CurrentStateDisplay = $"Throttled {throttledCount} background apps";
                State = OptimizationState.Optimized;

                Logger.Success(Title, $"Throttled {throttledCount} background processes to Below Normal priority.");
                return OptimizationResult.Ok(Id, $"Throttled {throttledCount} background processes.");
            }
            catch (Exception ex)
            {
                Logger.Error(Title, $"Background throttle failed: {ex.Message}");
                return OptimizationResult.Fail(Id, ex.Message, ex);
            }
        });
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        int restoredCount = 0;
        foreach (var pid in _throttledPids)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.PriorityClass = ProcessPriorityClass.Normal;
                restoredCount++;
            }
            catch { }
        }

        _throttledPids.Clear();
        IsOptimized = false;
        CurrentStateDisplay = "Normal";
        State = OptimizationState.Recommended;

        Logger.Info(Title, $"Restored priority on {restoredCount} background processes.");
        return Task.FromResult(OptimizationResult.Ok(Id, $"Restored {restoredCount} processes to Normal priority."));
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
