using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class GpuTdrDelayModule : IOptimizationModule
{
    private const string GraphicsDriversPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    public string Id => "gpu_tdr_delay_protection";
    public string Title => "GPU Timeout Detection & Recovery (TDR) Crash Guard";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Extends Windows GPU Timeout Detection and Recovery (TDR) watchdog delay from 2 seconds to 10 seconds to eliminate graphics driver timeout crashes during heavy UE4 shader loads.";
    public string TechnicalRationale => "DirectX and OpenGL emulation shaders in PUBG Mobile can occasionally take >2 seconds to compile during hot-drops or zone transitions. Windows normally interprets this as a frozen GPU and restarts the display driver, crashing GameLoop. Setting TdrDelay=10 provides shader buffer headroom while maintaining crash recovery.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "TdrDelay=10s, TdrDdiDelay=10s (Protected)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(GraphicsDriversPath);
            int tdrDelay = key?.GetValue("TdrDelay") is int td ? td : 2;
            int tdrDdi = key?.GetValue("TdrDdiDelay") is int tdd ? tdd : 2;

            bool isOptimal = tdrDelay >= 8 && tdrDdi >= 8;
            IsOptimized = isOptimal;
            CurrentStateDisplay = isOptimal ? $"Protected (TdrDelay: {tdrDelay}s)" : $"Default 2s Watchdog (TdrDelay: {tdrDelay}s)";
            State = isOptimal ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch (Exception ex)
        {
            Logger.Warn("GpuTdrDelayModule", $"Analyze error: {ex.Message}");
            CurrentStateDisplay = "Default (2s)";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(GraphicsDriversPath);
            if (key == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open GraphicsDrivers registry key."));

            var oldDelay = key.GetValue("TdrDelay");
            var oldDdi = key.GetValue("TdrDdiDelay");

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = "GPU TDR Watchdog Delay",
                Category = Category,
                TargetType = "Registry",
                TargetPath = $@"HKEY_LOCAL_MACHINE\{GraphicsDriversPath}",
                ValueName = "TdrDelay",
                PreviousValue = oldDelay?.ToString() ?? "2",
                PreviousValueKind = "DWord",
                NewValue = "10",
                Description = "GPU TDR Timeout Delay"
            });

            key.SetValue("TdrDelay", 10, RegistryValueKind.DWord);
            key.SetValue("TdrDdiDelay", 10, RegistryValueKind.DWord);
            key.SetValue("TdrLevel", 3, RegistryValueKind.DWord);

            IsOptimized = true;
            CurrentStateDisplay = "Protected (TdrDelay: 10s)";
            State = OptimizationState.Optimized;

            return Task.FromResult(OptimizationResult.Ok(Id, "Configured GPU TdrDelay and TdrDdiDelay to 10 seconds."));
        }
        catch (Exception ex)
        {
            Logger.Error("GpuTdrDelayModule", $"Apply failed: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to apply TDR delay: {ex.Message}", ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(GraphicsDriversPath);
            if (key != null)
            {
                key.DeleteValue("TdrDelay", false);
                key.DeleteValue("TdrDdiDelay", false);
            }

            IsOptimized = false;
            CurrentStateDisplay = "Default (2s)";
            State = OptimizationState.Recommended;

            return Task.FromResult(OptimizationResult.Ok(Id, "Restored default Windows GPU TDR watchdog configuration."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to rollback TDR: {ex.Message}", ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(GraphicsDriversPath);
            int tdrDelay = key?.GetValue("TdrDelay") is int td ? td : 2;
            return Task.FromResult(tdrDelay >= 8);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
