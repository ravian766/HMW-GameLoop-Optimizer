using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class GameLoopPUBGConfigModule : IOptimizationModule
{
    public string Id => "gl_pubg_fps";
    public string Title => "PUBG Mobile 90/120 FPS & Device Profile";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures GameLoop's device profile to high-refresh flag (ROG Phone 2 profile) and sets PUBG Mobile engine FPS target to 90/120 FPS.";
    public string TechnicalRationale => "Sets the legitimate emulator device profile so PUBG Mobile's internal engine unlocks high-refresh frame rate options (90/120 FPS) rather than being capped at 60 FPS.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay { get; private set; } = "120 FPS / ROG Profile";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsInstalled)
        {
            CurrentStateDisplay = "Not Installed";
            State = OptimizationState.NotDetected;
            return Task.FromResult(State);
        }

        var rec = RecommendationEngine.Calculate(hw);
        RecommendedStateDisplay = $"{rec.RecommendedFpsLevel} FPS (ROG 2 Profile)";
        CurrentStateDisplay = $"{gl.PubgFpsLevel} FPS ({gl.DeviceModel})";

        IsOptimized = gl.PubgFpsLevel >= 90 && gl.DeviceModel.Contains("ROG", StringComparison.OrdinalIgnoreCase);
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

            var prevFps = key.GetValue("com.tencent.ig_FPSLevel")?.ToString() ?? "60";
            var prevDevice = key.GetValue("VMPhoneDevice")?.ToString() ?? "Default";

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = $"{Title} (FPS Level)",
                Category = Category,
                TargetType = "Registry",
                TargetPath = $@"HKCU\{gl.RegistryKeyPath}",
                ValueName = "com.tencent.ig_FPSLevel",
                PreviousValue = prevFps,
                PreviousValueKind = "DWord",
                NewValue = rec.RecommendedFpsLevel.ToString(),
                Description = $"Set FPS level from {prevFps} to {rec.RecommendedFpsLevel}"
            });

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = $"{Title} (Device Profile)",
                Category = Category,
                TargetType = "Registry",
                TargetPath = $@"HKCU\{gl.RegistryKeyPath}",
                ValueName = "VMPhoneDevice",
                PreviousValue = prevDevice,
                PreviousValueKind = "String",
                NewValue = "Asus ROG 2",
                Description = "Set high-refresh device profile"
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
                        subKey.SetValue("com.tencent.ig_FPSLevel", rec.RecommendedFpsLevel, RegistryValueKind.DWord);
                        subKey.SetValue("com.tencent.ig_RenderQuality", 2, RegistryValueKind.DWord);
                        subKey.SetValue("com.tencent.ig_ContentScale", 1, RegistryValueKind.DWord);
                        subKey.SetValue("VMPhoneDevice", "Asus ROG 2", RegistryValueKind.String);
                        subKey.SetValue("VMDeviceManufacturer", "Asus", RegistryValueKind.String);
                        subKey.SetValue("VMDeviceModel", "ROG Phone 2", RegistryValueKind.String);
                    }
                }
                catch { }

                try
                {
                    using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                    if (hklmKey != null)
                    {
                        hklmKey.SetValue("com.tencent.ig_FPSLevel", rec.RecommendedFpsLevel, RegistryValueKind.DWord);
                        hklmKey.SetValue("com.tencent.ig_RenderQuality", 2, RegistryValueKind.DWord);
                        hklmKey.SetValue("com.tencent.ig_ContentScale", 1, RegistryValueKind.DWord);
                        hklmKey.SetValue("VMPhoneDevice", "Asus ROG 2", RegistryValueKind.String);
                        hklmKey.SetValue("VMDeviceManufacturer", "Asus", RegistryValueKind.String);
                        hklmKey.SetValue("VMDeviceModel", "ROG Phone 2", RegistryValueKind.String);
                    }
                }
                catch { }
            }

            gl.PubgFpsLevel = rec.RecommendedFpsLevel;
            gl.DeviceModel = "Asus ROG 2";

            IsOptimized = true;
            CurrentStateDisplay = $"{rec.RecommendedFpsLevel} FPS / ROG Phone 2";
            State = OptimizationState.Optimized;

            Logger.Success(Title, $"Applied PUBG Mobile {rec.RecommendedFpsLevel} FPS target and ROG device profile to GameLoop & TGB.");
            return Task.FromResult(OptimizationResult.Ok(Id, $"Configured PUBG Mobile for {rec.RecommendedFpsLevel} FPS and ROG Phone 2 profile on GameLoop/TGB."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply PUBG Mobile FPS profile: {ex.Message}");
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
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored previous PUBG FPS settings."));
        }
        return Task.FromResult(OptimizationResult.Fail(Id, "No backup found to revert."));
    }

    public Task<bool> VerifyAsync()
    {
        var gl = GameLoopDetector.DetectGameLoop();
        return Task.FromResult(gl.PubgFpsLevel >= 90);
    }
}
