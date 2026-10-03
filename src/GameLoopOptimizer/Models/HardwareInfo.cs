namespace GameLoopOptimizer.Models;

public class MonitorInfo
{
    public string DeviceName { get; set; } = "Primary Display";
    public int ResolutionWidth { get; set; } = 1920;
    public int ResolutionHeight { get; set; } = 1080;
    public int RefreshRateHz { get; set; } = 60;
    public int MaxRefreshRateHz { get; set; } = 60;
    public List<int> SupportedRefreshRates { get; set; } = new();
    public int DpiScalingPercent { get; set; } = 100;
    public bool IsHdrEnabled { get; set; } = false;
    public bool IsPrimary { get; set; } = true;
    public string AdapterName { get; set; } = string.Empty;

    public string ResolutionSummary => $"{ResolutionWidth}x{ResolutionHeight} @ {RefreshRateHz}Hz ({DpiScalingPercent}%)";
}

public class HardwareInfo
{
    // CPU
    public string CpuName { get; set; } = "Unknown CPU";
    public string CpuVendor { get; set; } = "Unknown";
    public int PhysicalCores { get; set; } = 4;
    public int LogicalProcessors { get; set; } = 4;
    public double CpuBaseClockGhz { get; set; } = 2.5;
    public double CpuMaxClockGhz { get; set; } = 2.5;
    public double CpuCurrentUtilizationPercent { get; set; } = 0;
    public int PerformanceCoresCount { get; set; } = 0;
    public int EfficientCoresCount { get; set; } = 0;
    public bool HasHybridArchitecture => PerformanceCoresCount > 0 && EfficientCoresCount > 0;
    public bool Has3dVcache { get; set; } = false;
    public double? CpuTemperatureC { get; set; }
    public double? CpuPackagePowerWatts { get; set; }
    public string Architecture { get; set; } = "x64";

    // GPU
    public string GpuName { get; set; } = "Unknown GPU";
    public GpuVendor GpuVendor { get; set; } = GpuVendor.Unknown;
    public double DedicatedVramMb { get; set; } = 0;
    public double VramUsedMb { get; set; } = 0;
    public double VramUtilizationPercent => DedicatedVramMb > 0 ? Math.Round((VramUsedMb / DedicatedVramMb) * 100, 1) : 0;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public double CurrentGpuUtilizationPercent { get; set; } = 0;
    public double? GpuTemperatureC { get; set; }
    public double? GpuClockMhz { get; set; }
    public bool IsDedicatedGpu => GpuVendor == GpuVendor.Nvidia || GpuVendor == GpuVendor.Amd || DedicatedVramMb >= 2048;

    // RAM
    public double TotalRamGb { get; set; } = 8;
    public double UsedRamGb { get; set; } = 4;
    public double AvailableRamGb => Math.Max(0, TotalRamGb - UsedRamGb);
    public double RamUtilizationPercent => TotalRamGb > 0 ? Math.Round((UsedRamGb / TotalRamGb) * 100, 1) : 0;
    public string RamSpeedType { get; set; } = "DDR4";
    public int RamSpeedMhz { get; set; } = 2666;
    public int RamStickCount { get; set; } = 2;
    public bool IsDualChannel => RamStickCount >= 2;
    public string MemoryPressureStatus { get; set; } = "Normal";

    // Storage
    public string SystemDrive { get; set; } = "C:";
    public string GameLoopDrive { get; set; } = "C:";
    public string PubgDataDrive { get; set; } = "C:";
    public StorageType PrimaryDriveType { get; set; } = StorageType.Ssd;
    public StorageType GameLoopDriveType { get; set; } = StorageType.Ssd;
    public StorageType PubgDataDriveType { get; set; } = StorageType.Ssd;
    public double FreeDiskSpaceGb { get; set; } = 0;
    public double TotalDiskSpaceGb { get; set; } = 0;
    public double DiskUtilizationPercent => TotalDiskSpaceGb > 0 ? Math.Round(((TotalDiskSpaceGb - FreeDiskSpaceGb) / TotalDiskSpaceGb) * 100, 1) : 0;

    // Display / Monitor
    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;
    public int RefreshRateHz { get; set; } = 60;
    public int MaxRefreshRateHz { get; set; } = 60;
    public List<int> SupportedRefreshRates { get; set; } = new();
    public int DpiScalingPercent { get; set; } = 100;
    public bool IsHdrEnabled { get; set; } = false;
    public string DisplayAdapter { get; set; } = string.Empty;
    public List<MonitorInfo> ConnectedMonitors { get; set; } = new();

    public HardwareTier CalculatedTier { get; set; } = HardwareTier.MidRange;
}

