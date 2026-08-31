using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class IfeoProcessPriorityModule : IOptimizationModule
{
    private const string IfeoBasePath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private static readonly string[] TargetProcesses = new[]
    {
        "AndroidProcess.exe",
        "AppMarket.exe",
        "QMEmulatorService.exe"
    };

    public string Id => "gl_ifeo_process_priority";
    public string Title => "Permanent IFEO High CPU & I/O Priority Injection";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Permanently binds High CPU and I/O Priority to GameLoop's core rendering engine (AndroidProcess.exe) and services at kernel launch via Windows IFEO PerfOptions.";
    public string TechnicalRationale => "Unlike temporary task manager priority changes which revert when the emulator restarts, Image File Execution Options (IFEO) PerfOptions force the Windows NT kernel to spawn every GameLoop process in High Priority (Class 3) and High I/O (Class 3) automatically.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Permanent High Priority (CpuPriorityClass=3, IoPriority=3)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            int configuredCount = 0;
            foreach (var proc in TargetProcesses)
            {
                string path = $@"{IfeoBasePath}\{proc}\PerfOptions";
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key != null)
                {
                    int cpuPrio = key.GetValue("CpuPriorityClass") is int cp ? cp : 0;
                    if (cpuPrio == 3) configuredCount++;
                }
            }

            bool isOptimal = configuredCount == TargetProcesses.Length;
            IsOptimized = isOptimal;
            CurrentStateDisplay = isOptimal
                ? "Permanent High Priority Configured (All 3 Processes)"
                : $"{configuredCount}/{TargetProcesses.Length} Processes Configured in IFEO";
            State = isOptimal ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch (Exception ex)
        {
            Logger.Warn("IfeoProcessPriorityModule", $"Analyze error: {ex.Message}");
            CurrentStateDisplay = "Default";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            int configured = 0;
            foreach (var proc in TargetProcesses)
            {
                string subPath = $@"{IfeoBasePath}\{proc}\PerfOptions";
                using var key = Registry.LocalMachine.CreateSubKey(subPath);
                if (key != null)
                {
                    var oldCpu = key.GetValue("CpuPriorityClass");
                    BackupManager.RecordBackup(new BackupEntry
                    {
                        ModuleId = Id,
                        Title = $"IFEO Priority ({proc})",
                        Category = Category,
                        TargetType = "Registry",
                        TargetPath = $@"HKEY_LOCAL_MACHINE\{subPath}",
                        ValueName = "CpuPriorityClass",
                        PreviousValue = oldCpu?.ToString() ?? "2",
                        PreviousValueKind = "DWord",
                        NewValue = "3",
                        Description = $"IFEO CPU Priority for {proc}"
                    });

                    key.SetValue("CpuPriorityClass", 3, RegistryValueKind.DWord);
                    key.SetValue("IoPriority", 3, RegistryValueKind.DWord);
                    key.SetValue("PagePriority", 5, RegistryValueKind.DWord);
                    configured++;
                }
            }

            IsOptimized = true;
            CurrentStateDisplay = "Permanent High Priority Configured (All 3 Processes)";
            State = OptimizationState.Optimized;

            return Task.FromResult(OptimizationResult.Ok(Id, $"Configured permanent High CPU & I/O Priority across {configured} GameLoop executables."));
        }
        catch (Exception ex)
        {
            Logger.Error("IfeoProcessPriorityModule", $"Apply failed: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to configure IFEO priorities: {ex.Message}", ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            foreach (var proc in TargetProcesses)
            {
                string subPath = $@"{IfeoBasePath}\{proc}\PerfOptions";
                Registry.LocalMachine.DeleteSubKeyTree(subPath, false);
            }

            IsOptimized = false;
            CurrentStateDisplay = "Default Windows Scheduling";
            State = OptimizationState.Recommended;

            return Task.FromResult(OptimizationResult.Ok(Id, "Removed IFEO custom process priority entries."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to rollback IFEO: {ex.Message}", ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        try
        {
            foreach (var proc in TargetProcesses)
            {
                string path = $@"{IfeoBasePath}\{proc}\PerfOptions";
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key == null || key.GetValue("CpuPriorityClass") is not int cp || cp != 3)
                {
                    return Task.FromResult(false);
                }
            }
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
