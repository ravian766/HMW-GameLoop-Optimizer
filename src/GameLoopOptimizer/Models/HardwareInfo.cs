namespace GameLoopOptimizer.Models;

public class HardwareInfo
{
    public string CpuName { get; set; } = "Unknown CPU";
    public int PhysicalCores { get; set; } = 4;
    public int LogicalProcessors { get; set; } = 4;
    public double CpuBaseClockGhz { get; set; } = 2.5;
    public string Architecture { get; set; } = "x64";

    public string GpuName { get; set; } = "Unknown GPU";
    public GpuVendor GpuVendor { get; set; } = GpuVendor.Unknown;
    public double DedicatedVramMb { get; set; } = 0;
    public string DriverVersion { get; set; } = string.Empty;
    public bool IsDedicatedGpu => GpuVendor == GpuVendor.Nvidia || GpuVendor == GpuVendor.Amd || DedicatedVramMb >= 2048;

    public double TotalRamGb { get; set; } = 8;
    public string RamSpeedType { get; set; } = "DDR4";
    public int RamStickCount { get; set; } = 2;
    public bool IsDualChannel => RamStickCount >= 2;

    public string SystemDrive { get; set; } = "C:";
    public StorageType PrimaryDriveType { get; set; } = StorageType.Ssd;
    public double FreeDiskSpaceGb { get; set; } = 0;
    public double TotalDiskSpaceGb { get; set; } = 0;

    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;
    public int RefreshRateHz { get; set; } = 60;

    public HardwareTier CalculatedTier { get; set; } = HardwareTier.MidRange;
}
