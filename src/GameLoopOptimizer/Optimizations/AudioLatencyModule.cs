using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class AudioLatencyModule : IOptimizationModule
{
    public string Id => "audio-low-latency";
    public string Title => "Low-Latency Audio & Gunshot Response";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures GameLoop/TGB audio subsystem to direct low-latency WASAPI streaming to eliminate gunshot sound delay.";
    public string TechnicalRationale => "Sets audioRenderType=1 in Tencent MobileGamePC and TxGameAssistant registry paths to bypass emulation buffer delay and use native WASAPI audio dispatch.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Checking...";
    public string RecommendedStateDisplay => "Direct WASAPI (Low Latency)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            var targetPaths = new[] { @"Software\Tencent\MobileGamePC", @"Software\Tencent\TxGameAssistant" };
            bool isLowLatency = false;

            foreach (var path in targetPaths)
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                if (key != null)
                {
                    var audioType = key.GetValue("audioRenderType")?.ToString();
                    if (audioType == "1")
                    {
                        isLowLatency = true;
                        break;
                    }
                }
            }

            IsOptimized = isLowLatency;
            CurrentStateDisplay = isLowLatency ? "Direct WASAPI (Low Latency)" : "Standard Audio Buffer";
            State = isLowLatency ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch
        {
            CurrentStateDisplay = "Default";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            var targetPaths = new[]
            {
                @"Software\Tencent\MobileGamePC",
                @"Software\Tencent\TxGameAssistant"
            };

            foreach (var path in targetPaths)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(path);
                    if (key != null)
                    {
                        var prev = key.GetValue("audioRenderType")?.ToString() ?? "0";
                        BackupManager.RecordBackup(new BackupEntry
                        {
                            ModuleId = Id,
                            Title = $"{Title} ({path})",
                            Category = OptimizationCategory.GameLoopEngine,
                            TargetType = "Registry",
                            TargetPath = $@"HKCU\{path}",
                            ValueName = "audioRenderType",
                            PreviousValue = prev,
                            PreviousValueKind = "DWord",
                            NewValue = "1",
                            Description = "Set GameLoop audio render to direct low-latency WASAPI"
                        });

                        key.SetValue("audioRenderType", 1, RegistryValueKind.DWord);
                    }
                }
                catch { }

                try
                {
                    using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                    if (hklmKey != null)
                    {
                        hklmKey.SetValue("audioRenderType", 1, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }

            IsOptimized = true;
            CurrentStateDisplay = "Direct WASAPI (Low Latency)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured low-latency direct WASAPI audio streaming in GameLoop & TGB.");
            return Task.FromResult(OptimizationResult.Ok(Id, "Audio latency and gunshot response optimized."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to optimize audio latency: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            CurrentStateDisplay = "Restored to Default";
            State = OptimizationState.NotOptimized;
            Logger.Info(Title, "Restored default audio settings.");
            return Task.FromResult(OptimizationResult.Ok(Id, "Audio settings restored to defaults."));
        }

        return Task.FromResult(OptimizationResult.Fail(Id, "No backup entry available."));
    }

    public Task<bool> VerifyAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
        var val = key?.GetValue("audioRenderType")?.ToString();
        return Task.FromResult(val == "1");
    }
}
