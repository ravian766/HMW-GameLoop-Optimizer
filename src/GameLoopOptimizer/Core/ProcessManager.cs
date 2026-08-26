using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class ProcessResourceInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsSafeToThrottle { get; set; }
}

public static class ProcessManager
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    public static int TrimWorkingSets()
    {
        int trimmedCount = 0;
        var procs = Process.GetProcesses();
        foreach (var proc in procs)
        {
            try
            {
                if (proc.ProcessName.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                    proc.ProcessName.Contains("aow", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                EmptyWorkingSet(proc.Handle);
                trimmedCount++;
            }
            catch { }
            finally
            {
                proc.Dispose();
            }
        }
        return trimmedCount;
    }

    private const int SW_RESTORE = 9;

    private static readonly HashSet<string> SystemCriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "services",
        "lsass", "svchost", "fontdrvhost", "winlogon", "dwm", "spoolsv",
        "explorer", "audiodg", "SecurityHealthService", "MsMpEng", "NisSrv",
        "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost", "conhost",
        "GameLoopOptimizer"
    };

    public static List<ProcessResourceInfo> GetHighOverheadProcesses(double cpuThreshold = 2.0, double memThresholdMb = 250.0)
    {
        var result = new List<ProcessResourceInfo>();

        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    if (SystemCriticalProcesses.Contains(proc.ProcessName))
                    {
                        continue;
                    }

                    var memMb = Math.Round((double)proc.WorkingSet64 / (1024 * 1024), 1);
                    if (memMb >= memThresholdMb)
                    {
                        result.Add(new ProcessResourceInfo
                        {
                            Id = proc.Id,
                            Name = proc.ProcessName,
                            MemoryMb = memMb,
                            Description = proc.MainWindowTitle,
                            IsSafeToThrottle = true
                        });
                    }
                }
                catch
                {
                    // Ignore access errors on system processes
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("ProcessManager", $"Failed to list processes: {ex.Message}");
        }

        return result.OrderByDescending(p => p.MemoryMb).Take(15).ToList();
    }

    public static bool SetGameLoopPriority(ProcessPriorityClass priority = ProcessPriorityClass.AboveNormal)
    {
        var emulatorProcessNames = new[] { "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe" };
        int boostedCount = 0;
        int protectedCount = 0;

        foreach (var name in emulatorProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var proc in procs)
                {
                    try
                    {
                        if (proc.PriorityClass != priority)
                        {
                            proc.PriorityClass = priority;
                            boostedCount++;
                        }
                    }
                    catch
                    {
                        // aow_exe kernel worker threads are protected by Tencent ACE anti-cheat driver
                        protectedCount++;
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore process enumeration error
            }
        }

        if (boostedCount > 0)
        {
            Logger.Success("ProcessManager", $"Set priority to {priority} for {boostedCount} GameLoop processes.");
        }
        else if (protectedCount > 0)
        {
            Logger.Info("ProcessManager", $"Emulator virtualization kernel threads ({protectedCount} instances) are managed by Tencent driver.");
        }

        return boostedCount > 0;
    }

    public static bool FocusOrLaunchGameLoop(GameLoopConfig config)
    {
        try
        {
            // 1. Try to focus running process with a visible window handle
            var procs = Process.GetProcessesByName("AppMarket")
                .Concat(Process.GetProcessesByName("AndroidEmulator"))
                .Concat(Process.GetProcessesByName("AndroidEmulatorEn"))
                .Concat(Process.GetProcessesByName("AndroidEmulatorEx"))
                .ToArray();

            foreach (var proc in procs)
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(proc.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(proc.MainWindowHandle);
                    Logger.Info("ProcessManager", $"Focused running GameLoop window (PID: {proc.Id})");
                    return true;
                }
            }

            // 2. Resolve executable path
            string exePath = GameLoopDetector.FindGameLoopExePath();

            if (string.IsNullOrEmpty(exePath) && !string.IsNullOrEmpty(config.InstallPath))
            {
                var candidate1 = Path.Combine(config.InstallPath, "AppMarket.exe");
                var candidate2 = Path.Combine(config.InstallPath, "AppMarket", "AppMarket.exe");
                var candidate3 = Path.Combine(config.InstallPath, "ui", "AndroidEmulatorEn.exe");

                if (File.Exists(candidate1)) exePath = candidate1;
                else if (File.Exists(candidate2)) exePath = candidate2;
                else if (File.Exists(candidate3)) exePath = candidate3;
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(psi);
                Logger.Success("ProcessManager", $"Launched GameLoop executable: {exePath}");
                return true;
            }

            Logger.Warn("ProcessManager", "Could not locate GameLoop AppMarket.exe executable on system.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("ProcessManager", $"Failed to focus or launch GameLoop: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> RestartGameLoopAsync(GameLoopConfig config)
    {
        return await Task.Run(() =>
        {
            try
            {
                var names = new[] { "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe" };
                foreach (var name in names)
                {
                    try
                    {
                        var procs = Process.GetProcessesByName(name);
                        foreach (var p in procs)
                        {
                            try { p.Kill(); p.WaitForExit(2000); } catch { }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(800);
                return FocusOrLaunchGameLoop(config);
            }
            catch (Exception ex)
            {
                Logger.Error("ProcessManager", $"Failed to restart GameLoop: {ex.Message}");
                return false;
            }
        });
    }

    public static long CalculateOptimalAffinityMask(int logicalProcessors, int physicalCores)
    {
        if (logicalProcessors <= 4)
        {
            // All cores
            return (1L << logicalProcessors) - 1;
        }

        // On hybrid or high thread count CPUs, bind to first 4-8 threads (P-Cores / primary CCD)
        int targetThreads = Math.Min(8, logicalProcessors);
        if (logicalProcessors >= 16) targetThreads = 8;
        else if (logicalProcessors >= 8) targetThreads = 8;
        else if (logicalProcessors >= 6) targetThreads = 6;

        return (1L << targetThreads) - 1;
    }

    public static bool SetGameLoopAffinity(long affinityMask)
    {
        var emulatorNames = new[] { "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe" };
        int configuredCount = 0;

        foreach (var name in emulatorNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var proc in procs)
                {
                    try
                    {
                        proc.ProcessorAffinity = (IntPtr)affinityMask;
                        configuredCount++;
                    }
                    catch
                    {
                        // Some kernel worker processes may be access restricted
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch { }
        }

        if (configuredCount > 0)
        {
            Logger.Success("ProcessManager", $"Configured CPU core affinity mask (0x{affinityMask:X}) for {configuredCount} GameLoop processes.");
        }

        return configuredCount > 0;
    }

    public static bool ResetGameLoopAffinity()
    {
        long fullMask = (1L << Math.Min(64, Environment.ProcessorCount)) - 1;
        return SetGameLoopAffinity(fullMask);
    }
}
