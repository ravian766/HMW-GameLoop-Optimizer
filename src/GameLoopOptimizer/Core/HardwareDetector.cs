using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public static class HardwareDetector
{

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

        Logger.Info("HardwareDetector", $"Detected: CPU: {info.CpuName} ({info.PhysicalCores}C/{info.LogicalProcessors}T, Vendor: {info.CpuVendor}), GPU: {info.GpuName} ({info.DedicatedVramMb:F0} MB, Drv: {info.DriverVersion}), RAM: {info.UsedRamGb:F1}/{info.TotalRamGb:F1} GB ({info.RamSpeedType} {info.RamSpeedMhz}MHz), Tier: {info.CalculatedTier}");

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
                    double maxMhz = Convert.ToDouble(item["MaxClockSpeed"]);
                    info.CpuMaxClockGhz = Math.Round(maxMhz / 1000.0, 2);
                    if (info.CpuBaseClockGhz <= 0) info.CpuBaseClockGhz = info.CpuMaxClockGhz;
                }
                break;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"WMI CPU query warning: {ex.Message}");
        }

        // Vendor classification
        if (info.CpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            info.CpuVendor = "Intel";
        }
        else if (info.CpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) || info.CpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
        {
            info.CpuVendor = "AMD";
        }
        else if (info.CpuName.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase) || info.CpuName.Contains("Snapdragon", StringComparison.OrdinalIgnoreCase))
        {
            info.CpuVendor = "Qualcomm";
        }
        else
        {
            info.CpuVendor = "Unknown";
        }

        // Check for AMD 3D V-Cache (e.g. 7800X3D, 5800X3D, 9800X3D, 7950X3D)
        info.Has3dVcache = info.CpuName.Contains("X3D", StringComparison.OrdinalIgnoreCase) ||
                           info.CpuName.Contains("3D V-Cache", StringComparison.OrdinalIgnoreCase);

        // Intel Hybrid Architecture Detection (Performance Cores + Efficient Cores)
        // Hybrid CPUs have physical cores < logical processors < physical cores * 2
        if (info.CpuVendor == "Intel" && info.PhysicalCores > 0 && info.LogicalProcessors > info.PhysicalCores && info.LogicalProcessors < info.PhysicalCores * 2)
        {
            int pCores = info.LogicalProcessors - info.PhysicalCores;
            int eCores = info.PhysicalCores - pCores;
            if (pCores > 0 && eCores > 0)
            {
                info.PerformanceCoresCount = pCores;
                info.EfficientCoresCount = eCores;
            }
        }
        else
        {
            info.PerformanceCoresCount = info.PhysicalCores;
            info.EfficientCoresCount = 0;
        }

        info.Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
    }

    private static void DetectMemory(HardwareInfo info)
    {
        try
        {
            var memStatus = new NativeMethods.MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
            if (NativeMethods.GlobalMemoryStatusEx(ref memStatus))
            {
                info.TotalRamGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 1);
                info.UsedRamGb = Math.Round((double)(memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024 * 1024 * 1024), 1);
                double pressureRatio = info.TotalRamGb > 0 ? (info.UsedRamGb / info.TotalRamGb) : 0;
                info.MemoryPressureStatus = pressureRatio > 0.85 ? "High" : (pressureRatio > 0.70 ? "Elevated" : "Normal");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"GlobalMemoryStatusEx failed: {ex.Message}");
            info.TotalRamGb = 8.0;
            info.UsedRamGb = 4.0;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, MemoryType, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            int stickCount = 0;
            int maxSpeed = 0;
            foreach (var item in searcher.Get())
            {
                stickCount++;
                if (item["Speed"] != null)
                {
                    int spd = Convert.ToInt32(item["Speed"]);
                    if (spd > maxSpeed) maxSpeed = spd;
                }
            }
            if (stickCount > 0)
            {
                info.RamStickCount = stickCount;
            }
            if (maxSpeed > 0)
            {
                info.RamSpeedMhz = maxSpeed;
                info.RamSpeedType = maxSpeed >= 4800 ? "DDR5" : (maxSpeed >= 2133 ? "DDR4" : "DDR3");
            }
        }
        catch
        {
            info.RamStickCount = 2;
        }
    }


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
            var d = new NativeMethods.DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);

            for (uint id = 0; NativeMethods.EnumDisplayDevices(null, id, ref d, 0); id++)
            {
                if ((d.StateFlags & 0x00000001) != 0 && !string.IsNullOrWhiteSpace(d.DeviceString))
                {
                    string gpu = d.DeviceString.Trim();
                    if (!gpu.Contains("Basic", StringComparison.OrdinalIgnoreCase) &&
                        !gpu.Contains("Remote", StringComparison.OrdinalIgnoreCase) &&
                        !gpu.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                    {
                        info.GpuName = gpu;
                        info.DisplayAdapter = gpu;
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
                        info.DisplayAdapter = desc.Trim();
                        ClassifyGpuVendor(info, desc);
                    }

                    if (string.IsNullOrEmpty(info.DriverVersion))
                    {
                        info.DriverVersion = subKey.GetValue("DriverVersion") as string ?? string.Empty;
                    }

                    if (string.IsNullOrEmpty(info.DriverDate))
                    {
                        info.DriverDate = subKey.GetValue("DriverDate") as string ?? string.Empty;
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

        // 2. Registry Display Driver info (fetches exact 64-bit VRAM, driver date & driver version)
        DetectGpuViaRegistry(info);

        // 3. WMI Win32_VideoController fallback / enrichment
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion, DriverDate FROM Win32_VideoController");
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
                    info.DisplayAdapter = name.Trim();
                    ClassifyGpuVendor(info, name);
                }

                if (string.IsNullOrEmpty(info.DriverVersion) && item["DriverVersion"] != null)
                {
                    info.DriverVersion = item["DriverVersion"].ToString()!;
                }

                if (string.IsNullOrEmpty(info.DriverDate) && item["DriverDate"] != null)
                {
                    var rawDate = item["DriverDate"].ToString()!;
                    if (rawDate.Length >= 8)
                    {
                        info.DriverDate = $"{rawDate[0..4]}-{rawDate[4..6]}-{rawDate[6..8]}";
                    }
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
            // 1. Get current primary display mode
            var devMode = new NativeMethods.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            if (NativeMethods.EnumDisplaySettings(null, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                info.ScreenWidth = devMode.dmPelsWidth;
                info.ScreenHeight = devMode.dmPelsHeight;
                info.RefreshRateHz = devMode.dmDisplayFrequency;
            }

            // 2. Discover all refresh rates
            var supportedRates = new HashSet<int>();
            int maxRate = info.RefreshRateHz;
            var modeEnum = new NativeMethods.DEVMODE();
            modeEnum.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));

            for (int modeIndex = 0; NativeMethods.EnumDisplaySettings(null, modeIndex, ref modeEnum); modeIndex++)
            {
                int rate = modeEnum.dmDisplayFrequency;
                if (rate > 0)
                {
                    supportedRates.Add(rate);
                    if (rate > maxRate)
                    {
                        maxRate = rate;
                    }
                }
                modeEnum.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            }

            info.MaxRefreshRateHz = maxRate;
            info.SupportedRefreshRates = supportedRates.OrderBy(r => r).ToList();

            // 3. Detect System DPI Scaling
            var dpi = DpiDetector.GetSystemDpi();
            info.DpiScalingPercent = dpi.ScalePercentage;

            // 4. Multi-monitor discovery
            info.ConnectedMonitors.Clear();
            var d = new NativeMethods.DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);

            for (uint id = 0; NativeMethods.EnumDisplayDevices(null, id, ref d, 0); id++)
            {
                if ((d.StateFlags & 0x00000001) != 0) // Attached to desktop
                {
                    bool isPrimary = (d.StateFlags & 0x00000004) != 0;
                    var monDevMode = new NativeMethods.DEVMODE();
                    monDevMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));

                    if (NativeMethods.EnumDisplaySettings(d.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref monDevMode))
                    {
                        var mon = new MonitorInfo
                        {
                            DeviceName = d.DeviceString?.Trim() ?? $"Display {id + 1}",
                            ResolutionWidth = monDevMode.dmPelsWidth,
                            ResolutionHeight = monDevMode.dmPelsHeight,
                            RefreshRateHz = monDevMode.dmDisplayFrequency,
                            MaxRefreshRateHz = monDevMode.dmDisplayFrequency,
                            IsPrimary = isPrimary,
                            DpiScalingPercent = info.DpiScalingPercent,
                            AdapterName = info.GpuName
                        };
                        info.ConnectedMonitors.Add(mon);
                    }
                }
                d.cb = Marshal.SizeOf(d);
            }

            if (info.ConnectedMonitors.Count == 0)
            {
                info.ConnectedMonitors.Add(new MonitorInfo
                {
                    DeviceName = "Primary Display",
                    ResolutionWidth = info.ScreenWidth,
                    ResolutionHeight = info.ScreenHeight,
                    RefreshRateHz = info.RefreshRateHz,
                    MaxRefreshRateHz = info.MaxRefreshRateHz,
                    SupportedRefreshRates = info.SupportedRefreshRates,
                    DpiScalingPercent = info.DpiScalingPercent,
                    IsPrimary = true,
                    AdapterName = info.GpuName
                });
            }

            Logger.Info("HardwareDetector",
                $"Display: {info.ScreenWidth}x{info.ScreenHeight} @ {info.RefreshRateHz} Hz (Scale: {info.DpiScalingPercent}%), " +
                $"Max: {info.MaxRefreshRateHz} Hz, Connected Monitors: {info.ConnectedMonitors.Count}");
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

            info.PrimaryDriveType = DetectStorageTypeForPath(systemDrivePath);

            // Locate GameLoop drive and PUBG data drive
            string glExe = GameLoopDetector.FindGameLoopExePath();
            if (!string.IsNullOrEmpty(glExe))
            {
                var glRoot = Path.GetPathRoot(glExe) ?? "C:\\";
                info.GameLoopDrive = glRoot.TrimEnd('\\');
                info.GameLoopDriveType = DetectStorageTypeForPath(glRoot);
                info.PubgDataDrive = info.GameLoopDrive;
                info.PubgDataDriveType = info.GameLoopDriveType;
            }
            else
            {
                info.GameLoopDrive = info.SystemDrive;
                info.GameLoopDriveType = info.PrimaryDriveType;
                info.PubgDataDrive = info.SystemDrive;
                info.PubgDataDriveType = info.PrimaryDriveType;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("HardwareDetector", $"Storage query failed: {ex.Message}");
            info.PrimaryDriveType = StorageType.Ssd;
            info.GameLoopDriveType = StorageType.Ssd;
            info.PubgDataDriveType = StorageType.Ssd;
        }
    }

    private static StorageType DetectStorageTypeForPath(string path)
    {
        try
        {
            // Try MSFT_PhysicalDisk in Storage namespace (Windows 8+)
            using var searcherStorage = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT MediaType, BusType FROM MSFT_PhysicalDisk");
            foreach (var disk in searcherStorage.Get())
            {
                var busType = disk["BusType"] != null ? Convert.ToInt32(disk["BusType"]) : 0;
                var mediaType = disk["MediaType"] != null ? Convert.ToInt32(disk["MediaType"]) : 0;

                // BusType 17 = NVMe
                if (busType == 17) return StorageType.Nvme;
                if (mediaType == 4) return StorageType.Ssd;
                if (mediaType == 3) return StorageType.Hdd;
            }
        }
        catch { }

        try
        {
            // Fallback: Win32_DiskDrive in root\CIMV2
            using var searcherDisk = new ManagementObjectSearcher("SELECT Model, MediaType, InterfaceType FROM Win32_DiskDrive");
            foreach (var disk in searcherDisk.Get())
            {
                var model = (disk["Model"]?.ToString() ?? "").ToUpperInvariant();
                var mediaType = (disk["MediaType"]?.ToString() ?? "").ToUpperInvariant();
                var iface = (disk["InterfaceType"]?.ToString() ?? "").ToUpperInvariant();

                if (model.Contains("NVME") || model.Contains("NVM EXPRESS") || iface.Contains("NVME"))
                    return StorageType.Nvme;

                if (model.Contains("SSD") || mediaType.Contains("SSD") || mediaType.Contains("SOLID STATE"))
                    return StorageType.Ssd;

                if (mediaType.Contains("FIXED HARD DISK") || model.Contains("HDD"))
                    return StorageType.Hdd;
            }
        }
        catch { }

        return StorageType.Ssd;
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
