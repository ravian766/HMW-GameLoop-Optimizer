using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

    public static double GetSystemMemoryLoadPercent()
    {
        return NativeMethods.GetMemoryLoadPercent();
    }

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref int processInformation,
        int processInformationLength);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref MEMORY_PRIORITY_INFORMATION processInformation,
        int processInformationLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_PRIORITY_INFORMATION
    {
        public uint MemoryPriority;
    }

    private const int ProcessIoPriority = 21;
    private const int ProcessMemoryPriority = 39;

    /// <summary>
    /// Active 3D graphics and VM kernel render processes that directly run the game.
    /// These receive high CPU priority, maximum I/O priority, memory priority, and P-Core affinity.
    /// AppMarket.exe (the desktop launcher store) is intentionally excluded so it does not compete for resources.
    /// </summary>
    public static readonly string[] EmulatorEngineProcessNames =
        global::GameLoopOptimizer.Core.GameLoopProcessNames.GameEngines.Concat(new[] { "TxEx" }).Distinct().ToArray();

    /// <summary>
    /// All processes belonging to GameLoop installation including the desktop store launcher.
    /// Used for launch detection, process focusing, and full shutdown/restart.
    /// </summary>
    public static readonly string[] AllGameLoopProcessNames =
        global::GameLoopOptimizer.Core.GameLoopProcessNames.AllProcesses.Concat(new[] { "TxEx", "TSettingCenter" }).Distinct().ToArray();

    public static readonly string[] GameLoopProcessNames = AllGameLoopProcessNames;

    public static int SetGameLoopIoAndMemoryPriority(int ioPriority = 3, uint memoryPriority = 5)
    {
        int configuredCount = 0;
        foreach (var name in EmulatorEngineProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var proc in procs)
                {
                    try
                    {
                        // 1. Set I/O Priority (3 = High)
                        int ioVal = ioPriority;
                        NtSetInformationProcess(proc.Handle, ProcessIoPriority, ref ioVal, sizeof(int));

                        // 2. Set Memory Priority (5 = Very High)
                        var memInfo = new MEMORY_PRIORITY_INFORMATION { MemoryPriority = memoryPriority };
                        NtSetInformationProcess(proc.Handle, ProcessMemoryPriority, ref memInfo, Marshal.SizeOf(typeof(MEMORY_PRIORITY_INFORMATION)));

                        configuredCount++;
                    }
                    catch { }
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
            Logger.Success("ProcessManager", $"Elevated Disk I/O & Memory streaming priority to High for {configuredCount} active emulator engines (AppMarket launcher excluded).");
        }
        return configuredCount;
    }

    public static bool IsGameLoopRunning()
    {
        foreach (var name in AllGameLoopProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    foreach (var p in procs) p.Dispose();
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

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
        int boostedCount = 0;
        int protectedCount = 0;

        foreach (var name in EmulatorEngineProcessNames)
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

        // Also elevate Disk I/O & Memory streaming priority
        SetGameLoopIoAndMemoryPriority(ioPriority: 3, memoryPriority: 5);

        if (boostedCount > 0)
        {
            Logger.Success("ProcessManager", $"Set priority to {priority} for {boostedCount} active emulator engines (AppMarket excluded).");
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
                foreach (var name in AllGameLoopProcessNames)
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

    public static long CalculateOptimalAffinityMask(int logicalProcessors, int physicalCores, string? cpuName = null)
    {
        if (logicalProcessors <= 4)
        {
            // All cores
            return (1L << logicalProcessors) - 1;
        }

        string cpu = cpuName ?? string.Empty;

        // 1. AMD 3D V-Cache dual-CCD processors (Ryzen 9 7900X3D, 7950X3D, 9900X3D, 9950X3D)
        // Bind exclusively to CCD0 which possesses the massive 3D V-Cache stack.
        if (cpu.Contains("7900X3D", StringComparison.OrdinalIgnoreCase) || cpu.Contains("9900X3D", StringComparison.OrdinalIgnoreCase))
        {
            return (1L << 12) - 1; // 6 cores on CCD0 = 12 threads (0x0FFF)
        }
        if (cpu.Contains("7950X3D", StringComparison.OrdinalIgnoreCase) || cpu.Contains("9950X3D", StringComparison.OrdinalIgnoreCase))
        {
            return (1L << 16) - 1; // 8 cores on CCD0 = 16 threads (0xFFFF)
        }

        // 2. Intel Hybrid Architecture (12th, 13th, 14th Gen, Core Ultra)
        // P-cores reside on the lowest logical processor IDs.
        bool isIntelHybrid = cpu.Contains("Intel", StringComparison.OrdinalIgnoreCase) && 
            (cpu.Contains("12th", StringComparison.OrdinalIgnoreCase) || 
             cpu.Contains("13th", StringComparison.OrdinalIgnoreCase) || 
             cpu.Contains("14th", StringComparison.OrdinalIgnoreCase) ||
             cpu.Contains("Ultra", StringComparison.OrdinalIgnoreCase) ||
             Regex.IsMatch(cpu, @"i[579]-1[234]\d{3}", RegexOptions.IgnoreCase));

        if (isIntelHybrid)
        {
            // Check i5 FIRST because i5-13600K/14600K has 14 cores (6P+8E) and 20 threads
            if (cpu.Contains("i5", StringComparison.OrdinalIgnoreCase))
            {
                return (1L << 12) - 1; // 6 P-Cores with HT = 12 threads (0x0FFF)
            }
            // i7 (e.g. 12700K 8P+4E=20T, 13700K/14700K 8P+8E=24T): 8 P-Cores with HT = 16 threads (0xFFFF)
            if (cpu.Contains("i7", StringComparison.OrdinalIgnoreCase))
            {
                return (1L << 16) - 1;
            }
            // i9 (e.g. 12900K 8P+8E=24T, 13900K/14900K 8P+16E=32T): 8 P-Cores with HT = 16 threads (0xFFFF)
            if (cpu.Contains("i9", StringComparison.OrdinalIgnoreCase) || logicalProcessors >= 20)
            {
                return (1L << 16) - 1;
            }
        }

        // 3. Fallback for standard CPUs without hybrid naming:
        // Bind to first 8 threads (0xFF = 255) to maintain full backwards compatibility with standard architecture
        int targetThreads = Math.Min(8, logicalProcessors);
        if (logicalProcessors >= 8) targetThreads = 8;
        else if (logicalProcessors >= 6) targetThreads = 6;

        return (1L << targetThreads) - 1;
    }

    public static bool SetGameLoopAffinity(long affinityMask)
    {
        int configuredCount = 0;

        foreach (var name in EmulatorEngineProcessNames)
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
