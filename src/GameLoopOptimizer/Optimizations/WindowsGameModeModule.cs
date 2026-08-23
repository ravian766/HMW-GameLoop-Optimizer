using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class WindowsGameModeModule : IOptimizationModule
{
    public string Id => "win_game_mode";
    public string Title => "Windows Game Mode";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Enables Windows Game Mode to prioritize CPU and GPU scheduling for the active emulator process while suppressing background updates.";
    public string TechnicalRationale => "Windows Game Mode instructs the DWM and process scheduler to allocate higher priority thread slices to the foreground rendering thread, reducing micro-stutters.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Enabled";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            bool enabled = true;
            if (key != null)
            {
                var val1 = key.GetValue("AllowAutoGameMode");
                var val2 = key.GetValue("AutoGameModeEnabled");
                if ((val1 is int i1 && i1 == 0) || (val2 is int i2 && i2 == 0))
                {
                    enabled = false;
                }
            }

            IsOptimized = enabled;
            CurrentStateDisplay = enabled ? "Enabled" : "Disabled";
            State = enabled ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch
        {
            CurrentStateDisplay = "Default";
            State = OptimizationState.Optimized;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
            if (key == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open GameBar registry key."));

            var prevObj = key.GetValue("AllowAutoGameMode");
            string? prevVal = prevObj?.ToString();
            string prevKind = "DWord";
            if (prevObj != null)
            {
                try { prevKind = key.GetValueKind("AllowAutoGameMode").ToString(); } catch { }
            }

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "Registry",
                TargetPath = @"HKCU\Software\Microsoft\GameBar",
                ValueName = "AllowAutoGameMode",
                PreviousValue = prevVal,
                PreviousValueKind = prevKind,
                NewValue = "1",
                Description = "Enable Windows Game Mode"
            });

            key.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
            key.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);

            IsOptimized = true;
            CurrentStateDisplay = "Enabled";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Windows Game Mode successfully enabled.");
            return Task.FromResult(OptimizationResult.Ok(Id, "Windows Game Mode enabled.", prevVal ?? "0", "1"));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to enable Game Mode: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            var target = backup ?? BackupManager.GetLatestForModule(Id);
            if (target != null && BackupManager.RestoreEntry(target))
            {
                IsOptimized = false;
                CurrentStateDisplay = "Reverted";
                State = OptimizationState.NotOptimized;
                return Task.FromResult(OptimizationResult.Ok(Id, "Reverted Windows Game Mode to previous state."));
            }
            return Task.FromResult(OptimizationResult.Fail(Id, "No backup found to revert."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
        if (key != null)
        {
            var val = key.GetValue("AllowAutoGameMode");
            return Task.FromResult(val is int i && i == 1);
        }
        return Task.FromResult(true);
    }
}
