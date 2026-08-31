namespace GameLoopOptimizer.Models;

public class PakBackupProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string PackageName { get; set; } = "com.tencent.ig";
    public string GameTitle { get; set; } = "PUBG Mobile";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long TotalSizeBytes { get; set; }
    public int FileCount { get; set; }
    public string LocalBackupPath { get; set; } = string.Empty;
    public List<string> PakFileNames { get; set; } = new();

    public string FormattedSize => TotalSizeBytes switch
    {
        >= 1024L * 1024 * 1024 => $"{TotalSizeBytes / (1024.0 * 1024 * 1024):F2} GB",
        >= 1024L * 1024 => $"{TotalSizeBytes / (1024.0 * 1024):F1} MB",
        >= 1024L => $"{TotalSizeBytes / 1024.0:F0} KB",
        _ => $"{TotalSizeBytes} Bytes"
    };

    public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm");
    public string SummaryText => $"{GameTitle} ({PackageName}) • {FileCount} Pak Files • {FormattedSize}";
}
