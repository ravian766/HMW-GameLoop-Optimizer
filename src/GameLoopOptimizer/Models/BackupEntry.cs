namespace GameLoopOptimizer.Models;

public class BackupEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ModuleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public OptimizationCategory Category { get; set; } = OptimizationCategory.WindowsConfig;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    public string TargetType { get; set; } = "Registry"; // Registry, PowerPlan, Service, Setting
    public string TargetPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? PreviousValueKind { get; set; } // DWord, String, Binary, etc.
    public string? NewValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsReverted { get; set; } = false;
}
