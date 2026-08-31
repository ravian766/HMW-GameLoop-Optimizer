using System.Diagnostics;
using System.IO;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class DisplayScalingStatus
{
    public bool IsFullScreenScalingActive { get; set; }
    public int AdaptersFound { get; set; }
    public int AdaptersConfigured { get; set; }
    public GpuVendor DetectedVendor { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class DisplayScalingService
{
    private const string DisplayClassGuid = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    public const int ScalingMaintainAspectRatio = 2;
    public const int ScalingFullScreen = 3;
    public const int ScalingCentered = 4;

    public static DisplayScalingStatus CheckCurrentScaling(HardwareInfo? hw = null)
    {
        var status = new DisplayScalingStatus
        {
            DetectedVendor = hw?.GpuVendor ?? GpuVendor.Unknown
        };

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(DisplayClassGuid);
            if (baseKey != null)
            {
                foreach (var subName in baseKey.GetSubKeyNames())
                {
                    if (subName.StartsWith("000", StringComparison.OrdinalIgnoreCase))
                    {
                        using var sub = baseKey.OpenSubKey(subName);
                        if (sub != null)
                        {
                            status.AdaptersFound++;
                            var val = sub.GetValue("Scaling");
                            if (val is int intVal && intVal == ScalingFullScreen)
                            {
                                status.IsFullScreenScalingActive = true;
                                status.AdaptersConfigured++;
                            }
                        }
                    }
                }
            }

            status.Message = status.IsFullScreenScalingActive
                ? $"Windows GPU Fullscreen Scaling is active across {status.AdaptersConfigured} adapter(s) (Stretched - No Black Bars)."
                : "GPU Scaling is currently set to Aspect Ratio / Centered (Black Bars present in stretched resolutions).";
        }
        catch (Exception ex)
        {
            status.Message = $"Could not query GPU registry scaling: {ex.Message}";
        }

        return status;
    }

    public static int ApplyFullScreenScaling(bool enable, bool recordBackup = true)
    {
        int modifiedCount = 0;
        int targetVal = enable ? ScalingFullScreen : ScalingMaintainAspectRatio;

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(DisplayClassGuid, true);
            if (baseKey == null) return 0;

            foreach (var subName in baseKey.GetSubKeyNames())
            {
                if (subName.StartsWith("000", StringComparison.OrdinalIgnoreCase))
                {
                    using var sub = baseKey.OpenSubKey(subName, true);
                    if (sub != null)
                    {
                        string fullPath = $@"HKEY_LOCAL_MACHINE\{DisplayClassGuid}\{subName}";
                        var oldVal = sub.GetValue("Scaling");

                        if (recordBackup)
                        {
                            BackupManager.RecordBackup(new BackupEntry
                            {
                                ModuleId = "gpu_fullscreen_scaling",
                                Title = "GPU Driver Scaling",
                                Category = OptimizationCategory.GraphicsQuality,
                                TargetType = "Registry",
                                TargetPath = fullPath,
                                ValueName = "Scaling",
                                PreviousValue = oldVal?.ToString() ?? "2",
                                PreviousValueKind = "DWord",
                                NewValue = targetVal.ToString(),
                                Description = "Windows GPU driver display scaling setting"
                            });
                        }

                        sub.SetValue("Scaling", targetVal, RegistryValueKind.DWord);
                        modifiedCount++;
                        Logger.Success("DisplayScaling", $"Updated GPU Adapter '{subName}' Scaling -> {targetVal} ({(enable ? "Stretched Fullscreen" : "Aspect Ratio")}).");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("DisplayScaling", $"Failed to update GPU scaling registry: {ex.Message}");
        }

        return modifiedCount;
    }

    public static bool LaunchGpuControlPanel(GpuVendor vendor)
    {
        try
        {
            switch (vendor)
            {
                case GpuVendor.Nvidia:
                    var nvidiaCandidates = new[]
                    {
                        @"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe",
                        @"C:\Program Files (x86)\NVIDIA Corporation\Control Panel Client\nvcplui.exe",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvcplui.exe")
                    };

                    foreach (var path in nvidiaCandidates)
                    {
                        if (File.Exists(path))
                        {
                            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                            return true;
                        }
                    }
                    Process.Start(new ProcessStartInfo("explorer.exe", "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}") { UseShellExecute = true });
                    return true;

                case GpuVendor.Amd:
                    var amdCandidates = new[]
                    {
                        @"C:\Program Files\AMD\CNext\CNext\RadeonSoftware.exe",
                        @"C:\Program Files\AMD\CNext\CNext\RadeonSettings.exe",
                        @"C:\Program Files\AMD\CNext\CNext\amdcmd.exe"
                    };

                    foreach (var path in amdCandidates)
                    {
                        if (File.Exists(path))
                        {
                            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                            return true;
                        }
                    }
                    Process.Start(new ProcessStartInfo("RadeonSoftware.exe") { UseShellExecute = true });
                    return true;

                case GpuVendor.Intel:
                    Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\AppUp.IntelGraphicsExperience_8j3eq9eme6ctt!App") { UseShellExecute = true });
                    return true;

                default:
                    Process.Start(new ProcessStartInfo("desk.cpl") { UseShellExecute = true });
                    return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("DisplayScaling", $"Could not launch GPU control panel for {vendor}: {ex.Message}");
            return false;
        }
    }

    public static string[] GetVendorStepByStepGuide(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => new[]
        {
            "1. Open NVIDIA Control Panel (Display -> Adjust desktop size and position).",
            "2. Under 'Select a scaling mode', choose 'Full-screen'.",
            "3. Set 'Perform scaling on:' to 'GPU' (bypasses display scaler input lag).",
            "4. Check the box: 'Override the scaling mode set by games and programs'.",
            "5. Click Apply."
        },
        GpuVendor.Amd => new[]
        {
            "1. Open AMD Radeon Software -> Gaming -> Display.",
            "2. Set 'Scaling Mode' to 'Full Panel' (Stretched).",
            "3. Enable 'GPU Scaling' toggle for hardware-accelerated scaling.",
            "4. Disable 'Integer Scaling' to allow smooth stretched viewport mapping."
        },
        GpuVendor.Intel => new[]
        {
            "1. Open Intel Graphics Command Center -> Display.",
            "2. Under 'Scale', select 'Scale Full Screen'.",
            "3. Ensure Display Refresh Rate matches your monitor's maximum (e.g. 144Hz / 240Hz)."
        },
        _ => new[]
        {
            "1. Open Windows Display Settings -> Advanced Display.",
            "2. Select target stretched resolution and verify monitor refresh rate."
        }
    };
}
