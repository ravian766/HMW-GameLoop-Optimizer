using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class GameLoopResourceModule : IOptimizationModule
{
    public string Id => "gl_resource_allocation";
    public string Title => "GameLoop CPU & RAM Allocation";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Dynamically calculates and applies the optimal CPU core count and RAM allocation for the GameLoop virtualization engine based on your hardware.";
    public string TechnicalRationale => "Allocating excessive cores causes thread context-switching overhead in Windows host, while allocating too few starves Android game worker threads. Dynamic allocation finds the exact sweet spot.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay { get; private set; } = "Dynamic";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsInstalled)
        {
            CurrentStateDisplay = "Not Installed";
            RecommendedStateDisplay = "N/A";
            State = OptimizationState.NotDetected;
            return Task.FromResult(State);
        }

        var rec = RecommendationEngine.Calculate(hw);
        RecommendedStateDisplay = $"{rec.RecommendedCpuCores} Cores / {rec.RecommendedRamMb} MB";
        CurrentStateDisplay = $"{gl.VmCpuCount} Cores / {gl.VmMemorySizeInMb} MB";

        IsOptimized = (gl.VmCpuCount == rec.RecommendedCpuCores && gl.VmMemorySizeInMb == rec.RecommendedRamMb);
        State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsInstalled)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, "GameLoop installation was not detected."));
        }

        var rec = RecommendationEngine.Calculate(hw);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(gl.RegistryKeyPath);
            if (key == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open GameLoop registry key."));

            var prevCores = key.GetValue("VMCpuCount")?.ToString() ?? "4";
            var prevRam = key.GetValue("VMMemorySizeInMB")?.ToString() ?? "4096";

            // Record backup for Cores
            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = $"{Title} (CPU Cores)",
                Category = Category,
                TargetType = "Registry",
                TargetPath = $@"HKCU\{gl.RegistryKeyPath}",
                ValueName = "VMCpuCount",
                PreviousValue = prevCores,
                PreviousValueKind = "DWord",
                NewValue = rec.RecommendedCpuCores.ToString(),
                Description = $"Set GameLoop CPU Cores from {prevCores} to {rec.RecommendedCpuCores}"
            });

            // Record backup for RAM
            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = $"{Title} (RAM Allocation)",
                Category = Category,
                TargetType = "Registry",
                TargetPath = $@"HKCU\{gl.RegistryKeyPath}",
                ValueName = "VMMemorySizeInMB",
                PreviousValue = prevRam,
                PreviousValueKind = "DWord",
                NewValue = rec.RecommendedRamMb.ToString(),
                Description = $"Set GameLoop RAM from {prevRam} MB to {rec.RecommendedRamMb} MB"
            });

            var targetPaths = new[]
            {
                @"Software\Tencent\MobileGamePC",
                @"Software\Tencent\TxGameAssistant"
            };

            foreach (var path in targetPaths)
            {
                try
                {
                    using var subKey = Registry.CurrentUser.CreateSubKey(path);
                    if (subKey != null)
                    {
                        subKey.SetValue("VMCpuCount", rec.RecommendedCpuCores, RegistryValueKind.DWord);
                        subKey.SetValue("VMMemorySizeInMB", rec.RecommendedRamMb, RegistryValueKind.DWord);
                    }
                }
                catch { }

                try
                {
                    using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                    if (hklmKey != null)
                    {
                        hklmKey.SetValue("VMCpuCount", rec.RecommendedCpuCores, RegistryValueKind.DWord);
                        hklmKey.SetValue("VMMemorySizeInMB", rec.RecommendedRamMb, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }

            gl.VmCpuCount = rec.RecommendedCpuCores;
            gl.VmMemorySizeInMb = rec.RecommendedRamMb;

            IsOptimized = true;
            CurrentStateDisplay = $"{rec.RecommendedCpuCores} Cores / {rec.RecommendedRamMb} MB";
            State = OptimizationState.Optimized;

            Logger.Success(Title, $"Applied dynamic allocation to GameLoop & TGB: {rec.RecommendedCpuCores} CPU cores, {rec.RecommendedRamMb} MB RAM.");
            return Task.FromResult(OptimizationResult.Ok(Id, $"Configured GameLoop/TGB to {rec.RecommendedCpuCores} cores and {rec.RecommendedRamMb} MB RAM."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply resource allocation: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            CurrentStateDisplay = "Restored";
            State = OptimizationState.NotOptimized;
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored previous GameLoop CPU & RAM configuration."));
        }
        return Task.FromResult(OptimizationResult.Fail(Id, "No backup found to revert."));
    }

    public Task<bool> VerifyAsync()
    {
        var gl = GameLoopDetector.DetectGameLoop();
        return Task.FromResult(gl.IsInstalled);
    }
}
