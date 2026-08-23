namespace GameLoopOptimizer.Models;

public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public double CpuTotalPercent { get; set; }
    public double[] CpuCoresPercent { get; set; } = Array.Empty<double>();

    public double GpuPercent { get; set; }
    public double GpuVramUsedMb { get; set; }
    public double GpuVramTotalMb { get; set; }
    public double? GpuTemperatureC { get; set; }
    public double? CpuTemperatureC { get; set; }

    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamPercent => RamTotalGb > 0 ? (RamUsedGb / RamTotalGb) * 100 : 0;
    public double RamAvailableGb => Math.Max(0, RamTotalGb - RamUsedGb);

    public double DiskReadMbSec { get; set; }
    public double DiskWriteMbSec { get; set; }
    public double DiskActivePercent { get; set; }

    public double GameLoopCpuPercent { get; set; }
    public double GameLoopRamMb { get; set; }
    public bool IsGameLoopActive { get; set; }

    public double EstimatedFrametimeVarianceMs { get; set; }
}

public class GamingSessionState
{
    public bool IsActive { get; set; } = false;
    public DateTime? StartTime { get; set; }
    public TimeSpan Duration => IsActive && StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;

    public double PeakRamMb { get; set; }
    public double PeakCpuPercent { get; set; }
    public double AvgCpuPercent { get; set; }
    public int MetricSamplesCount { get; set; }
    public double TotalCpuAccumulator { get; set; }

    public List<string> AppliedTemporaryChanges { get; set; } = new();
    public List<BackupEntry> SessionBackups { get; set; } = new();
}
