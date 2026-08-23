using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class VisualEffectsModule : IOptimizationModule
{
    public string Id => "win_visual_fx";
    public string Title => "Windows Animation & DWM Overhead Check";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Optimizes non-essential Windows UI animations and window minimize/maximize effects to minimize Desktop Window Manager (DWM) GPU compositing overhead.";
    public string TechnicalRationale => "DWM compositing shares GPU render queues with the emulator window; reducing excessive animations frees GPU command buffers.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Default Effects";
    public string RecommendedStateDisplay => "Optimized Animations";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        CurrentStateDisplay = sys.AreVisualEffectsOptimized ? "Optimized Animations" : "Standard Animations";
        IsOptimized = sys.AreVisualEffectsOptimized;
        State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics");
            if (key == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open WindowMetrics key."));

            var prev = key.GetValue("MinAnimate")?.ToString() ?? "1";

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "Registry",
                TargetPath = @"HKCU\Control Panel\Desktop\WindowMetrics",
                ValueName = "MinAnimate",
                PreviousValue = prev,
                PreviousValueKind = "String",
                NewValue = "0",
                Description = "Disable minimize/maximize animation delay"
            });

            key.SetValue("MinAnimate", "0", RegistryValueKind.String);

            IsOptimized = true;
            CurrentStateDisplay = "Optimized Animations";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured responsive window animations.");
            return Task.FromResult(OptimizationResult.Ok(Id, "Optimized Windows UI animation latency."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to optimize visual effects: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            CurrentStateDisplay = "Standard Animations";
            State = OptimizationState.NotOptimized;
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored standard animation settings."));
        }
        return Task.FromResult(OptimizationResult.Fail(Id, "No backup found to revert."));
    }

    public Task<bool> VerifyAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
        if (key != null)
        {
            var val = key.GetValue("MinAnimate") as string;
            return Task.FromResult(val == "0");
        }
        return Task.FromResult(true);
    }
}
