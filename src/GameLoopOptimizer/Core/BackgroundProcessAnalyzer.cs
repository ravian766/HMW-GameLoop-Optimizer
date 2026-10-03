using System.Diagnostics;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class ProcessAuditItem
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string MainWindowTitle { get; set; } = string.Empty;
    public double WorkingSetMb { get; set; }
    public double EstimatedCpuPercent { get; set; }
    public ProcessSafetyCategory Category { get; set; } = ProcessSafetyCategory.Unknown;
    public bool IsSelectedForTermination { get; set; } = false;

    public bool CanBeTerminatedSafely =>
        Category == ProcessSafetyCategory.SafeToClose || Category == ProcessSafetyCategory.UserApplication;
}

public static class BackgroundProcessAnalyzer
{
    private static readonly HashSet<string> CriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "smss", "csrss", "wininit", "winlogon", "services", "lsass",
        "dwm", "svchost", "fontdrvhost", "sihost", "explorer", "taskhostw", "spoolsv",
        "RuntimeBroker", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
        "MsMpEng", "NisSrv", "SecurityHealthService", "audiodg"
    };

    private static readonly HashSet<string> KnownSafeToClose = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "discord", "spotify",
        "EpicGamesLauncher", "Steam", "Battle.net", "Origin", "EAConnect", "Telegram",
        "WhatsApp", "Viber", "Skype", "slack", "Teams", "Dropbox", "OneDrive"
    };

    public static List<ProcessAuditItem> AuditBackgroundProcesses()
    {
        var result = new List<ProcessAuditItem>();
        var currentPid = Environment.ProcessId;

        var procs = Process.GetProcesses();
        foreach (var p in procs)
        {
            try
            {
                if (p.Id == currentPid || p.Id == 0 || p.Id == 4)
                    continue;

                // Skip emulator processes
                if (ProcessManager.AllGameLoopProcessNames.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase))
                    continue;

                double memMb = Math.Round((double)p.WorkingSet64 / (1024 * 1024), 1);
                string name = p.ProcessName;
                string title = string.Empty;

                try
                {
                    title = p.MainWindowTitle;
                }
                catch { }

                var cat = ClassifyProcess(name);

                // Only include processes with meaningful memory footprints or window titles
                if (memMb > 30 || !string.IsNullOrEmpty(title))
                {
                    result.Add(new ProcessAuditItem
                    {
                        ProcessId = p.Id,
                        ProcessName = name,
                        MainWindowTitle = title,
                        WorkingSetMb = memMb,
                        Category = cat,
                        IsSelectedForTermination = cat == ProcessSafetyCategory.SafeToClose && memMb > 150
                    });
                }
            }
            catch
            {
                // Access denied or process exited
            }
            finally
            {
                p.Dispose();
            }
        }

        return result.OrderByDescending(r => r.WorkingSetMb).ToList();
    }

    public static ProcessSafetyCategory ClassifyProcess(string processName)
    {
        if (CriticalProcesses.Contains(processName))
            return ProcessSafetyCategory.CriticalProcess;

        if (KnownSafeToClose.Contains(processName))
            return ProcessSafetyCategory.SafeToClose;

        if (processName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
            processName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            processName.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessSafetyCategory.SystemProcess;
        }

        return ProcessSafetyCategory.UserApplication;
    }

    public static int TerminateSelectedProcesses(IEnumerable<ProcessAuditItem> items)
    {
        int closedCount = 0;
        foreach (var item in items.Where(i => i.IsSelectedForTermination))
        {
            if (!item.CanBeTerminatedSafely)
            {
                Logger.Warn("BackgroundProcessAnalyzer", $"Skipped terminating protected process: {item.ProcessName} (PID: {item.ProcessId})");
                continue;
            }

            try
            {
                using var proc = Process.GetProcessById(item.ProcessId);
                if (proc != null && !proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(1000);
                    closedCount++;
                    Logger.Info("BackgroundProcessAnalyzer", $"Terminated background process {item.ProcessName} (freed ~{item.WorkingSetMb:F0} MB)");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("BackgroundProcessAnalyzer", $"Could not terminate {item.ProcessName}: {ex.Message}");
            }
        }

        return closedCount;
    }
}
