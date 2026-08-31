using GameLoopOptimizer.Monitoring;

namespace GameLoopOptimizer.Core;

public interface IDaemonServiceManager : IDisposable
{
    PerformanceMonitorService MonitorService { get; }
    GameLoopWatchdogService WatchdogService { get; }
    void StartAll();
    void StopAll();
}

public class DaemonServiceManager : IDaemonServiceManager
{
    public PerformanceMonitorService MonitorService { get; }
    public GameLoopWatchdogService WatchdogService { get; }

    public DaemonServiceManager(PerformanceMonitorService monitorService, GameLoopWatchdogService watchdogService)
    {
        MonitorService = monitorService;
        WatchdogService = watchdogService;
    }

    public void StartAll()
    {
        try
        {
            MonitorService.Start();
            WatchdogService.Start();
            Logger.Info("DaemonManager", "Background performance and watchdog daemons started.");
        }
        catch (Exception ex)
        {
            Logger.Error("DaemonManager", $"Failed to start background daemons: {ex.Message}");
        }
    }

    public void StopAll()
    {
        try
        {
            MonitorService.Stop();
            WatchdogService.Stop();
            Logger.Info("DaemonManager", "Background daemons stopped.");
        }
        catch (Exception ex)
        {
            Logger.Error("DaemonManager", $"Failed to stop background daemons: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopAll();
        MonitorService.Dispose();
        WatchdogService.Dispose();
    }
}
