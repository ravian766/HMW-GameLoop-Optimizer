using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public static class HardwareDetector
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public short dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    public static async Task<HardwareInfo> DetectHardwareAsync()
    {
        return await Task.Run(() => DetectHardware());
    }

    public static HardwareInfo DetectHardware()
    {
        var info = new HardwareInfo();

        DetectCpu(info);
        DetectMemory(info);
        DetectGpu(info);
        DetectDisplay(info);
        DetectStorage(info);

        // Calculate Tier
        info.CalculatedTier = CalculateTier(info);

        Logger.Info("HardwareDetector", $"Detected: CPU: {info.CpuName} ({info.PhysicalCores}C/{info.LogicalProcessors}T), GPU: {info.GpuName} ({info.DedicatedVramMb:F0} MB), RAM: {info.TotalRamGb:F1} GB, Tier: {info.CalculatedTier}");

        return info;
    }

    private static void DetectCpu(HardwareInfo info)
    {
        info.LogicalProcessors = Environment.ProcessorCount;
        info.PhysicalCores = Math.Max(1, info.LogicalProcessors / 2); // Default fallback

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key != null)
            {
                var name = key.GetValue("ProcessorNameString") as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    info.CpuName = name.Trim();
                }

                var mhz = key.GetValue("~MHz");
                if (mhz is int speedInt)
                {
                    info.CpuBaseClockGhz = Math.Round(speedInt / 1000.0, 2);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"Registry CPU check failed: {ex.Message}");
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                if (item["Name"] != null && string.IsNullOrEmpty(info.CpuName))
                {
                    info.CpuName = item["Name"].ToString()!.Trim();
                }
                if (item["NumberOfCores"] != null)
                {
                    info.PhysicalCores = Convert.ToInt32(item["NumberOfCores"]);
                }
                if (item["NumberOfLogicalProcessors"] != null)
                {
                    info.LogicalProcessors = Convert.ToInt32(item["NumberOfLogicalProcessors"]);
                }
                if (item["MaxClockSpeed"] != null)
                {
                    info.CpuBaseClockGhz = Math.Round(Convert.ToDouble(item["MaxClockSpeed"]) / 1000.0, 2);
                }
                break;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"WMI CPU query warning: {ex.Message}");
        }

        info.Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
    }

    private static void DetectMemory(HardwareInfo info)
    {
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                info.TotalRamGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 1);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"GlobalMemoryStatusEx failed: {ex.Message}");
            info.TotalRamGb = 8.0;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    private static void ClassifyGpuVendor(HardwareInfo info, string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("nvidia") || lower.Contains("geforce") || lower.Contains("rtx") || lower.Contains("gtx") || lower.Contains("quadro") || lower.Contains("titan"))
        {
            info.GpuVendor = GpuVendor.Nvidia;
        }
        else if (lower.Contains("amd") || lower.Contains("radeon") || lower.Contains("rx ") || lower.Contains("rx6") || lower.Contains("rx7") || lower.Contains("rx5") || lower.Contains("vega") || lower.Contains("navi"))
        {
            info.GpuVendor = GpuVendor.Amd;
        }
        else if (lower.Contains("intel") || lower.Contains("arc") || lower.Contains("iris") || lower.Contains("uhd") || lower.Contains("hd graphics") || lower.Contains(" xe"))
        {
            info.GpuVendor = GpuVendor.Intel;
        }
    }

    private static bool DetectGpuViaNativeWin32(HardwareInfo info)
    {
        try
        {
            var d = new DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);
            for (uint id = 0; EnumDisplayDevices(null, id, ref d, 0); id++)
            {
                if ((d.StateFlags & 0x00000001) != 0 && !string.IsNullOrWhiteSpace(d.DeviceString))
                {
                    string gpu = d.DeviceString.Trim();
                    if (!gpu.Contains("Basic", StringComparison.OrdinalIgnoreCase) &&
                        !gpu.Contains("Remote", StringComparison.OrdinalIgnoreCase) &&
                        !gpu.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                    {
                        info.GpuName = gpu;
                        ClassifyGpuVendor(info, gpu);
                        if (info.GpuVendor == GpuVendor.Nvidia || info.GpuVendor == GpuVendor.Amd)
                        {
                            return true;
                        }
                    }
                }
                d.cb = Marshal.SizeOf(d);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"Native Win32 GPU query failed: {ex.Message}");
        }
        return info.GpuName != "Unknown GPU";
    }

    private static bool DetectGpuViaRegistry(HardwareInfo info)
    {
        try
        {
            const string videoClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var classKey = Registry.LocalMachine.OpenSubKey(videoClassKey);
            if (classKey != null)
            {
                foreach (var subName in classKey.GetSubKeyNames())
                {
                    if (subName.Length != 4 || !int.TryParse(subName, out _)) continue;
                    using var subKey = classKey.OpenSubKey(subName);
                    if (subKey == null) continue;

                    var desc = subKey.GetValue("DriverDesc") as string;
                    if (string.IsNullOrWhiteSpace(desc) ||
                        desc.Contains("Basic", StringComparison.OrdinalIgnoreCase) ||
                        desc.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                        desc.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (info.GpuName == "Unknown GPU")
                    {
                        info.GpuName = desc.Trim();
                        ClassifyGpuVendor(info, desc);
                    }

                    if (string.IsNullOrEmpty(info.DriverVersion))
                    {
                        info.DriverVersion = subKey.GetValue("DriverVersion") as string ?? string.Empty;
                    }

                    // True 64-bit VRAM size (bypasses 32-bit WMI 4GB cap)
                    var qwMem = subKey.GetValue("HardwareInformation.qwMemorySize");
                    if (qwMem is long qwLong && qwLong > 0)
                    {
                        info.DedicatedVramMb = Math.Round((double)qwLong / (1024 * 1024), 0);
                    }
                    else if (qwMem is byte[] qwBytes && qwBytes.Length >= 8)
                    {
                        ulong bytes = BitConverter.ToUInt64(qwBytes, 0);
                        if (bytes > 0) info.DedicatedVramMb = Math.Round((double)bytes / (1024 * 1024), 0);
                    }
                    else
                    {
                        var dwordMem = subKey.GetValue("HardwareInformation.MemorySize");
                        if (dwordMem is int dwInt && dwInt > 0)
                        {
                            info.DedicatedVramMb = Math.Round((double)dwInt / (1024 * 1024), 0);
                        }
                    }

                    if (info.GpuVendor == GpuVendor.Nvidia || info.GpuVendor == GpuVendor.Amd)
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"Registry GPU query failed: {ex.Message}");
        }
        return info.GpuName != "Unknown GPU";
    }

    private static void DetectGpu(HardwareInfo info)
    {
        // 1. Fast Native Win32 API (EnumDisplayDevices) - zero latency, reliable under all user privilege levels
        DetectGpuViaNativeWin32(info);

        // 2. Registry Display Driver info (fetches exact 64-bit VRAM & driver version)
        DetectGpuViaRegistry(info);

        // 3. WMI Win32_VideoController fallback / enrichment
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (var item in searcher.Get())
            {
                var name = item["Name"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) ||
                    name.Contains("Basic", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (info.GpuName == "Unknown GPU")
                {
                    info.GpuName = name.Trim();
                    ClassifyGpuVendor(info, name);
                }

                if (string.IsNullOrEmpty(info.DriverVersion) && item["DriverVersion"] != null)
                {
                    info.DriverVersion = item["DriverVersion"].ToString()!;
                }

                if (info.DedicatedVramMb <= 0 && item["AdapterRAM"] != null)
                {
                    try
                    {
                        long raw = Convert.ToInt64(item["AdapterRAM"]);
                        ulong bytes = (ulong)Math.Abs(raw);
                        if (raw < 0) bytes = (ulong)((uint)raw);
                        if (bytes > 0)
                        {
                            info.DedicatedVramMb = Math.Round((double)bytes / (1024 * 1024), 0);
                        }
                    }
                    catch { }
                }

                // If dedicated GPU found (NVIDIA/AMD), prioritize over integrated
                if (info.GpuVendor == GpuVendor.Nvidia || info.GpuVendor == GpuVendor.Amd)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"WMI GPU query warning: {ex.Message}");
        }
    }

    private static void DetectDisplay(HardwareInfo info)
    {
        try
        {
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                info.ScreenWidth = devMode.dmPelsWidth;
                info.ScreenHeight = devMode.dmPelsHeight;
                info.RefreshRateHz = devMode.dmDisplayFrequency;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"Display mode query failed: {ex.Message}");
        }
    }

    private static void DetectStorage(HardwareInfo info)
    {
        try
        {
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            info.SystemDrive = systemDrivePath.TrimEnd('\\');

            var drive = new DriveInfo(systemDrivePath);
            if (drive.IsReady)
            {
                info.TotalDiskSpaceGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 1);
                info.FreeDiskSpaceGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 1);
            }

            // Detect Drive Type
            info.PrimaryDriveType = StorageType.Ssd; // Modern standard default
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"Storage query failed: {ex.Message}");
        }
    }

    public static HardwareTier CalculateTier(HardwareInfo hw)
    {
        // Scoring formula based on CPU threads, GPU dedication, and RAM
        int points = 0;

        // CPU Points
        if (hw.LogicalProcessors >= 12) points += 35;
        else if (hw.LogicalProcessors >= 8) points += 28;
        else if (hw.LogicalProcessors >= 6) points += 20;
        else points += 10;

        // RAM Points
        if (hw.TotalRamGb >= 31) points += 30;
        else if (hw.TotalRamGb >= 15) points += 25;
        else if (hw.TotalRamGb >= 7.5) points += 15;
        else points += 5;

        // GPU Points
        if (hw.IsDedicatedGpu)
        {
            if (hw.DedicatedVramMb >= 6000 || hw.GpuName.Contains("RTX") || hw.GpuName.Contains("RX 6") || hw.GpuName.Contains("RX 7"))
            {
                points += 35;
            }
            else
            {
                points += 25;
            }
        }
        else
        {
            points += 10;
        }

        if (points >= 75) return HardwareTier.HighEnd;
        if (points >= 45) return HardwareTier.MidRange;
        return HardwareTier.LowEnd;
    }
}
