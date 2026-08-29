namespace GameLoopOptimizer.Models;

public class UpdateInfo
{
    public Version Version { get; set; } = new(0, 0, 0);
    public string VersionString { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string ReleaseTitle { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime PublishedAt { get; set; }
    public bool IsPreRelease { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes <= 0) return "Unknown size";
            double mb = FileSizeBytes / (1024.0 * 1024.0);
            return $"{mb:F1} MB";
        }
    }
}
