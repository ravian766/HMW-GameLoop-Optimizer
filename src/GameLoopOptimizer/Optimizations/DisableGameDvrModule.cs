using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class DisableGameDvrModule : IOptimizationModule
{
    private const string GameConfigStorePath = @"System\GameConfigStore";
    private const string UserGameDvrPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string PolicyGameDvrPath = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";

    public string Id => "win_disable_game_dvr";
    public string Title => "Disable Windows Game DVR & Background Capture Overhead";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Disables Windows background Game DVR video capture and Xbox Game Bar frame hooks that cause micro-stuttering and FPS drops in emulators.";
    public string TechnicalRationale => "Windows Game DVR constantly hooks the DXGI/Direct3D swap chain and buffers video frames in memory. Disabling Game DVR eliminates frame hook overhead, frees memory bandwidth, and prevents FPS drops during intensive firefights.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Disabled (Zero Capture Overhead)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var configStore = Registry.CurrentUser.OpenSubKey(GameConfigStorePath);
            using var userDvr = Registry.CurrentUser.OpenSubKey(UserGameDvrPath);
            using var policyDvr = Registry.LocalMachine.OpenSubKey(PolicyGameDvrPath);

            int dvrEnabled = configStore?.GetValue("GameDVR_Enabled") is int de ? de : 1;
            int appCapture = userDvr?.GetValue("AppCaptureEnabled") is int ac ? ac : 1;
            int allowDvr = policyDvr?.GetValue("AllowGameDVR") is int ad ? ad : 1;

            bool isOptimal = (dvrEnabled == 0) && (appCapture == 0) && (allowDvr == 0);

            IsOptimized = isOptimal;
            CurrentStateDisplay = isOptimal ? "Disabled (Optimized)" : "Active (Background Capturing Allowed)";
            State = isOptimal ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch (Exception ex)
        {
            Logger.Warn("DisableGameDvrModule", $"Analyze error: {ex.Message}");
            CurrentStateDisplay = "Active";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            // 1. User GameConfigStore
            using (var configStore = Registry.CurrentUser.CreateSubKey(GameConfigStorePath))
            {
                if (configStore != null)
                {
                    var oldVal = configStore.GetValue("GameDVR_Enabled");
                    BackupManager.RecordBackup(new BackupEntry
                    {
                        ModuleId = Id,
                        Title = "GameConfigStore GameDVR_Enabled",
                        Category = Category,
                        TargetType = "Registry",
                        TargetPath = $@"HKEY_CURRENT_USER\{GameConfigStorePath}",
                        ValueName = "GameDVR_Enabled",
                        PreviousValue = oldVal?.ToString() ?? "1",
                        PreviousValueKind = "DWord",
                        NewValue = "0",
                        Description = "Game DVR User Configuration"
                    });

                    configStore.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    configStore.SetValue("GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
                    configStore.SetValue("GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord);
                    configStore.SetValue("GameDVR_DXGIHonorFSEWindowsCompatible", 1, RegistryValueKind.DWord);
                }
            }

            // 2. User GameDVR
            using (var userDvr = Registry.CurrentUser.CreateSubKey(UserGameDvrPath))
            {
                if (userDvr != null)
                {
                    userDvr.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                }
            }

            // 3. System Policy GameDVR
            using (var policyDvr = Registry.LocalMachine.CreateSubKey(PolicyGameDvrPath))
            {
                if (policyDvr != null)
                {
                    policyDvr.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
                }
            }

            IsOptimized = true;
            CurrentStateDisplay = "Disabled (Optimized)";
            State = OptimizationState.Optimized;

            return Task.FromResult(OptimizationResult.Ok(Id, "Disabled Windows Game DVR and background app capture overhead."));
        }
        catch (Exception ex)
        {
            Logger.Error("DisableGameDvrModule", $"Apply failed: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to disable Game DVR: {ex.Message}", ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            using var configStore = Registry.CurrentUser.CreateSubKey(GameConfigStorePath);
            if (configStore != null)
            {
                configStore.SetValue("GameDVR_Enabled", 1, RegistryValueKind.DWord);
            }

            using var userDvr = Registry.CurrentUser.CreateSubKey(UserGameDvrPath);
            if (userDvr != null)
            {
                userDvr.SetValue("AppCaptureEnabled", 1, RegistryValueKind.DWord);
            }

            using var policyDvr = Registry.LocalMachine.CreateSubKey(PolicyGameDvrPath);
            if (policyDvr != null)
            {
                policyDvr.SetValue("AllowGameDVR", 1, RegistryValueKind.DWord);
            }

            IsOptimized = false;
            CurrentStateDisplay = "Active";
            State = OptimizationState.Recommended;

            return Task.FromResult(OptimizationResult.Ok(Id, "Restored default Windows Game DVR configuration."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Failed to rollback Game DVR: {ex.Message}", ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        try
        {
            using var configStore = Registry.CurrentUser.OpenSubKey(GameConfigStorePath);
            int dvrEnabled = configStore?.GetValue("GameDVR_Enabled") is int de ? de : 1;
            return Task.FromResult(dvrEnabled == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
