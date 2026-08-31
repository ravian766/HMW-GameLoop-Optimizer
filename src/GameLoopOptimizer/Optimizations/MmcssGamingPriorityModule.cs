using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class MmcssGamingPriorityModule : IOptimizationModule
{
    private const string SystemProfilePath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GamesTaskPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    public string Id => "win_mmcss_gaming_priority";
    public string Title => "MMCSS Multimedia & GPU Gaming Scheduler Priority";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures Windows Multimedia Class Scheduler Service (MMCSS) to dedicate 100% CPU time slices to games and raises GPU priority to maximum (8) with high scheduling category.";
    public string TechnicalRationale => "By default, Windows reserves 20% of CPU time for background maintenance and limits GPU gaming task priority. Setting SystemResponsiveness=0 and Games GPU Priority=8 ensures foreground emulator frames never get starved.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "SystemResponsiveness=0, GPU Priority=8 (High)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var sysProfile = Registry.LocalMachine.OpenSubKey(SystemProfilePath);
            using var gamesTask = Registry.LocalMachine.OpenSubKey(GamesTaskPath);

            int sysResp = sysProfile?.GetValue("SystemResponsiveness") is int sr ? sr : 20;
            int gpuPrio = gamesTask?.GetValue("GPU Priority") is int gp ? gp : 0;
            int prio = gamesTask?.GetValue("Priority") is int pr ? pr : 2;
            string? schedCat = gamesTask?.GetValue("Scheduling Category") as string;

            bool isOptimal = (sysResp == 0) && (gpuPrio == 8) && (prio == 6) && string.Equals(schedCat, "High", StringComparison.OrdinalIgnoreCase);

            IsOptimized = isOptimal;
            CurrentStateDisplay = isOptimal 
                ? "Optimal (SystemResponsiveness: 0, GPU Priority: 8)" 
                : $"Non-Optimal (SystemResponsiveness: {sysResp}, GPU Priority: {gpuPrio})";
            State = isOptimal ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch (Exception ex)
        {
            Logger.Warn("MmcssGamingPriorityModule", $"Analyze error: {ex.Message}");
            CurrentStateDisplay = "Default";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            // 1. SystemProfile root key
            using (var sysProfile = Registry.LocalMachine.CreateSubKey(SystemProfilePath))
            {
                if (sysProfile != null)
                {
                    var oldSysResp = sysProfile.GetValue("SystemResponsiveness");
                    var oldNetThrottle = sysProfile.GetValue("NetworkThrottlingIndex");

                    BackupManager.RecordBackup(new BackupEntry
                    {
                        ModuleId = Id,
                        Title = "MMCSS SystemResponsiveness",
                        Category = Category,
                        TargetType = "Registry",
                        TargetPath = $@"HKEY_LOCAL_MACHINE\{SystemProfilePath}",
                        ValueName = "SystemResponsiveness",
                        PreviousValue = oldSysResp?.ToString() ?? "20",
                        PreviousValueKind = "DWord",
                        NewValue = "0",
                        Description = "MMCSS System Responsiveness"
                    });

                    sysProfile.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                    sysProfile.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                }
            }

            // 2. Games Task subkey
            using (var gamesTask = Registry.LocalMachine.CreateSubKey(GamesTaskPath))
            {
                if (gamesTask != null)
                {
                    var oldGpu = gamesTask.GetValue("GPU Priority");
                    var oldPrio = gamesTask.GetValue("Priority");
                    var oldSched = gamesTask.GetValue("Scheduling Category");

                    BackupManager.RecordBackup(new BackupEntry
                    {
                        ModuleId = Id,
                        Title = "MMCSS Games Tasks Priority",
                        Category = Category,
                        TargetType = "Registry",
                        TargetPath = $@"HKEY_LOCAL_MACHINE\{GamesTaskPath}",
                        ValueName = "GPU Priority",
                        PreviousValue = oldGpu?.ToString() ?? "2",
                        PreviousValueKind = "DWord",
                        NewValue = "8",
                        Description = "MMCSS Games GPU Priority"
                    });

                    gamesTask.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                    gamesTask.SetValue("Priority", 6, RegistryValueKind.DWord);
                    gamesTask.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                    gamesTask.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                    gamesTask.SetValue("Affinity", 0, RegistryValueKind.DWord);
                    gamesTask.SetValue("Background Only", "False", RegistryValueKind.String);
                    gamesTask.SetValue("Clock Rate", 10000, RegistryValueKind.DWord);
                }
            }

            IsOptimized = true;
            CurrentStateDisplay = "Optimal (SystemResponsiveness: 0, GPU Priority: 8)";
            State = OptimizationState.Optimized;

            return Task.FromResult(OptimizationResult.Ok(Id, "Configured MMCSS SystemResponsiveness to 0 and Games GPU Priority to 8 (High)."));
        }
        catch (Exception ex)
        {
            Logger.Error("MmcssGamingPriorityModule", $"Apply failed: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to apply MMCSS priority: {ex.Message}", ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            using var sysProfile = Registry.LocalMachine.CreateSubKey(SystemProfilePath);
            if (sysProfile != null)
            {
                sysProfile.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
            }

            using var gamesTask = Registry.LocalMachine.CreateSubKey(GamesTaskPath);
            if (gamesTask != null)
            {
                gamesTask.SetValue("GPU Priority", 2, RegistryValueKind.DWord);
                gamesTask.SetValue("Priority", 2, RegistryValueKind.DWord);
                gamesTask.SetValue("Scheduling Category", "Medium", RegistryValueKind.String);
            }

            IsOptimized = false;
            CurrentStateDisplay = "Default (SystemResponsiveness: 20, GPU Priority: 2)";
            State = OptimizationState.Recommended;

            return Task.FromResult(OptimizationResult.Ok(Id, "Restored default MMCSS gaming scheduler configuration."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to rollback MMCSS: {ex.Message}", ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        try
        {
            using var sysProfile = Registry.LocalMachine.OpenSubKey(SystemProfilePath);
            using var gamesTask = Registry.LocalMachine.OpenSubKey(GamesTaskPath);

            int sysResp = sysProfile?.GetValue("SystemResponsiveness") is int sr ? sr : 20;
            int gpuPrio = gamesTask?.GetValue("GPU Priority") is int gp ? gp : 0;

            return Task.FromResult(sysResp == 0 && gpuPrio == 8);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
