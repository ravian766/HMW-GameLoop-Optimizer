using System.Diagnostics;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class PowerPlanModule : IOptimizationModule
{
    public string Id => "win_power_plan";
    public string Title => "High Performance Power Delivery";
    public OptimizationCategory Category => OptimizationCategory.PowerDelivery;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Switches Windows Power Scheme to High Performance or Ultimate Performance, preventing aggressive CPU downclocking and core parking during gameplay.";
    public string TechnicalRationale => "Reduces CPU frequency scaling latency when GameLoop encounters sudden compute spikes (such as rendering dense hot-drop areas in PUBG Mobile).";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Balanced";
    public string RecommendedStateDisplay => "High Performance";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimatePerfGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        CurrentStateDisplay = sys.ActivePowerPlanName;
        IsOptimized = sys.IsHighPerformancePowerPlan;
        State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        return await Task.Run(() =>
        {
            try
            {
                var prevGuid = sys.ActivePowerPlanGuid;
                var prevName = sys.ActivePowerPlanName;

                // Try Ultimate Performance first, fallback to High Performance
                string targetGuid = HighPerfGuid;
                string targetName = "High Performance";

                try
                {
                    var psiUltimate = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/setactive {UltimatePerfGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var pUlt = Process.Start(psiUltimate);
                    pUlt?.WaitForExit(3000);
                    if (pUlt?.ExitCode == 0)
                    {
                        targetGuid = UltimatePerfGuid;
                        targetName = "Ultimate Performance";
                    }
                }
                catch { }

                if (targetGuid == HighPerfGuid)
                {
                    var psiHigh = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/setactive {HighPerfGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var pHigh = Process.Start(psiHigh);
                    pHigh?.WaitForExit(3000);
                }

                // Record backup
                BackupManager.RecordBackup(new BackupEntry
                {
                    ModuleId = Id,
                    Title = Title,
                    Category = Category,
                    TargetType = "PowerPlan",
                    TargetPath = "powercfg",
                    ValueName = "ActiveScheme",
                    PreviousValue = prevGuid,
                    NewValue = targetGuid,
                    Description = $"Switch from '{prevName}' to {targetName}"
                });

                IsOptimized = true;
                CurrentStateDisplay = targetName;
                State = OptimizationState.Optimized;

                Logger.Success(Title, $"Switched power scheme to {targetName}.");
                return OptimizationResult.Ok(Id, $"Switched power scheme to {targetName}.", prevName, targetName);
            }
            catch (Exception ex)
            {
                Logger.Error(Title, $"Failed to set power scheme: {ex.Message}");
                return OptimizationResult.Fail(Id, ex.Message, ex);
            }
        });
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        return await Task.Run(() =>
        {
            var target = backup ?? BackupManager.GetLatestForModule(Id);
            if (target != null && BackupManager.RestoreEntry(target))
            {
                IsOptimized = false;
                CurrentStateDisplay = target.PreviousValue ?? "Balanced";
                State = OptimizationState.NotOptimized;
                return OptimizationResult.Ok(Id, "Restored previous power plan.");
            }
            return OptimizationResult.Fail(Id, "No power plan backup found to revert.");
        });
    }

    public Task<bool> VerifyAsync()
    {
        var sys = SystemDetector.DetectSystem();
        return Task.FromResult(sys.IsHighPerformancePowerPlan);
    }
}
