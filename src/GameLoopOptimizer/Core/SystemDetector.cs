using System.Diagnostics;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public static class SystemDetector
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryTimerResolution(out uint minimumResolution, out uint maximumResolution, out uint currentResolution);

    public static async Task<SystemInfo> DetectSystemAsync()
    {
        return await Task.Run(() => DetectSystem());
    }

    public static SystemInfo DetectSystem()
    {
        var info = new SystemInfo();

        DetectOsInfo(info);
        DetectVirtualizationAndSecurity(info);
        DetectGameMode(info);
        DetectHagsAndGraphics(info);
        DetectPowerPlanAndBattery(info);
        DetectTimerResolution(info);
        DetectVisualEffects(info);
        info.IsAdmin = PermissionManager.IsAdministrator;

        Logger.Info("SystemDetector", $"Detected: {info.OsCaption} {info.OsEdition} (Build {info.OsBuild}), Game Mode: {info.IsGameModeEnabled}, HAGS: {info.IsHagsEnabled}, Power: {info.ActivePowerPlanName} ({info.PowerSource}), Admin: {info.IsAdmin}, HVCI: {info.IsMemoryIntegrityEnabled}");

        return info;
    }

    private static void DetectOsInfo(SystemInfo info)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var productName = key.GetValue("ProductName") as string;
                var displayVersion = key.GetValue("DisplayVersion") as string;
                var currentBuild = key.GetValue("CurrentBuild") as string;
                var editionId = key.GetValue("EditionID") as string;

                info.OsCaption = productName ?? "Windows";
                info.OsVersion = displayVersion ?? string.Empty;
                info.OsBuild = currentBuild ?? Environment.OSVersion.Version.Build.ToString();
                info.OsEdition = editionId ?? "Windows";
            }
            else
            {
                info.OsCaption = $"Windows {Environment.OSVersion.Version.Major}";
                info.OsBuild = Environment.OSVersion.Version.Build.ToString();
            }

            // UAC Status check
            using var uacKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            if (uacKey != null)
            {
                var enableLua = uacKey.GetValue("EnableLUA");
                info.IsUacEnabled = enableLua is int val && val == 1;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("SystemDetector", $"OS detection failed: {ex.Message}");
        }

        info.OsArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
    }

    private static void DetectVirtualizationAndSecurity(SystemInfo info)
    {
        try
        {
            // 1. Hypervisor / Hyper-V check via WMI
            using var searcherComp = new System.Management.ManagementObjectSearcher("SELECT HypervisorPresent FROM Win32_ComputerSystem");
            foreach (var item in searcherComp.Get())
            {
                if (item["HypervisorPresent"] != null)
                {
                    info.IsHyperVPresent = Convert.ToBoolean(item["HypervisorPresent"]);
                }
                break;
            }
        }
        catch { }

        try
        {
            // 2. BIOS Virtualization (VT-x / AMD-V)
            using var searcherProc = new System.Management.ManagementObjectSearcher("SELECT VirtualizationFirmwareEnabled FROM Win32_Processor");
            foreach (var item in searcherProc.Get())
            {
                if (item["VirtualizationFirmwareEnabled"] != null)
                {
                    info.IsVirtualizationEnabledInBios = Convert.ToBoolean(item["VirtualizationFirmwareEnabled"]);
                }
                break;
            }
        }
        catch { }

        try
        {
            // 3. Core Isolation / Memory Integrity (HVCI)
            using var hvciKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            if (hvciKey != null)
            {
                var enabled = hvciKey.GetValue("Enabled");
                info.IsMemoryIntegrityEnabled = enabled is int val && val == 1;
                info.IsCoreIsolationEnabled = info.IsMemoryIntegrityEnabled;
            }
        }
        catch { }
    }

    private static void DetectHagsAndGraphics(SystemInfo info)
    {
        try
        {
            // Hardware-Accelerated GPU Scheduling (HAGS)
            // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers -> HwSchMode (2 = On, 1 = Off)
            using var gfxKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            if (gfxKey != null)
            {
                var mode = gfxKey.GetValue("HwSchMode");
                info.IsHagsEnabled = mode is int m && m == 2;
            }
        }
        catch { }

        try
        {
            // Fullscreen Optimizations status
            using var gameConfigKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
            if (gameConfigKey != null)
            {
                var fse = gameConfigKey.GetValue("GameDVR_FSEBehaviorMode");
                // 2 = Disabled (native exclusive fullscreen), 0/default = Enabled
                info.AreFullscreenOptimizationsEnabled = fse is not int val || val != 2;
            }
        }
        catch { }
    }

    public static void DetectPowerPlanAndBattery(SystemInfo info)
    {
        DetectPowerPlan(info);

        try
        {
            if (NativeMethods.GetSystemPowerStatus(out var powerStatus))
            {
                // ACLineStatus: 0 = Offline (Battery), 1 = Online (AC), 255 = Unknown
                info.PowerSource = powerStatus.ACLineStatus == 0 ? PowerSourceState.Battery : PowerSourceState.AcPower;
            }
        }
        catch
        {
            info.PowerSource = PowerSourceState.AcPower;
        }
    }


    public static void DetectGameMode(SystemInfo info)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            if (key != null)
            {
                var autoGameMode = key.GetValue("AllowAutoGameMode");
                var autoGameModeEnabled = key.GetValue("AutoGameModeEnabled");

                if (autoGameMode is int val1 && val1 == 0)
                {
                    info.IsGameModeEnabled = false;
                    return;
                }
                if (autoGameModeEnabled is int val2 && val2 == 0)
                {
                    info.IsGameModeEnabled = false;
                    return;
                }
            }
            info.IsGameModeEnabled = true; // Enabled by default in Windows 10/11
        }
        catch (Exception ex)
        {
            Logger.Warn("SystemDetector", $"Game Mode check warning: {ex.Message}");
            info.IsGameModeEnabled = true;
        }
    }

    public static void DetectPowerPlan(SystemInfo info)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);

                // Sample: "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var guidMatch = System.Text.RegularExpressions.Regex.Match(output, @"([a-f0-9\-]{36})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (guidMatch.Success)
                    {
                        info.ActivePowerPlanGuid = guidMatch.Groups[1].Value;
                    }

                    var nameMatch = System.Text.RegularExpressions.Regex.Match(output, @"\(([^)]+)\)");
                    if (nameMatch.Success)
                    {
                        info.ActivePowerPlanName = nameMatch.Groups[1].Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("SystemDetector", $"Powercfg check warning: {ex.Message}");
        }
    }

    public static void DetectTimerResolution(SystemInfo info)
    {
        try
        {
            if (NtQueryTimerResolution(out _, out _, out uint current) == 0)
            {
                // Resolution is in 100-nanosecond units (10,000 units = 1 ms)
                info.CurrentTimerResolutionMs = Math.Round((double)current / 10000.0, 2);
            }
        }
        catch
        {
            info.CurrentTimerResolutionMs = 15.6; // Default standard Windows timer tick
        }
    }

    public static void DetectVisualEffects(SystemInfo info)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
            if (key != null)
            {
                var minAnimate = key.GetValue("MinAnimate") as string;
                info.AreVisualEffectsOptimized = minAnimate == "0";
            }
        }
        catch
        {
            info.AreVisualEffectsOptimized = false;
        }
    }
}
