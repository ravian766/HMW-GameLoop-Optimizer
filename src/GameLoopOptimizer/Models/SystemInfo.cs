namespace GameLoopOptimizer.Models;

public class SystemInfo
{
    public string OsCaption { get; set; } = "Windows 10/11";
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = "64-bit";
    public bool IsGameModeEnabled { get; set; } = true;
    public string ActivePowerPlanGuid { get; set; } = string.Empty;
    public string ActivePowerPlanName { get; set; } = "Balanced";
    public bool IsHighPerformancePowerPlan => ActivePowerPlanName.Contains("High", StringComparison.OrdinalIgnoreCase) 
                                            || ActivePowerPlanName.Contains("Ultimate", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin { get; set; } = false;
    public double CurrentTimerResolutionMs { get; set; } = 15.6;
    public bool AreVisualEffectsOptimized { get; set; } = false;
    public int StartupAppsCount { get; set; } = 0;
    public int HighCpuProcessesCount { get; set; } = 0;
}
