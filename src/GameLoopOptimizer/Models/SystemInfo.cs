namespace GameLoopOptimizer.Models;

public class SystemInfo
{
    public string OsCaption { get; set; } = "Windows 10/11";
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = "64-bit";
    public string OsEdition { get; set; } = "Windows";

    public bool IsAdmin { get; set; } = false;
    public bool IsUacEnabled { get; set; } = true;

    // Virtualization / Hypervisor
    public bool IsVirtualizationEnabledInBios { get; set; } = true;
    public bool IsHyperVPresent { get; set; } = false;
    public bool IsVirtualMachinePlatformEnabled { get; set; } = false;
    public bool IsWindowsHypervisorPlatformEnabled { get; set; } = false;

    // Security
    public bool IsCoreIsolationEnabled { get; set; } = false;
    public bool IsMemoryIntegrityEnabled { get; set; } = false; // HVCI

    // Power & Battery
    public PowerSourceState PowerSource { get; set; } = PowerSourceState.AcPower;
    public bool IsOnBattery => PowerSource == PowerSourceState.Battery;
    public string ActivePowerPlanGuid { get; set; } = string.Empty;
    public string ActivePowerPlanName { get; set; } = "Balanced";
    public bool IsHighPerformancePowerPlan => ActivePowerPlanName.Contains("High", StringComparison.OrdinalIgnoreCase) 
                                            || ActivePowerPlanName.Contains("Ultimate", StringComparison.OrdinalIgnoreCase);

    // Gaming Features
    public bool IsGameModeEnabled { get; set; } = true;
    public bool IsHagsEnabled { get; set; } = false; // Hardware Accelerated GPU Scheduling
    public bool AreFullscreenOptimizationsEnabled { get; set; } = true;
    public double CurrentTimerResolutionMs { get; set; } = 15.6;
    public bool AreVisualEffectsOptimized { get; set; } = false;

    public int StartupAppsCount { get; set; } = 0;
    public int HighCpuProcessesCount { get; set; } = 0;
}

