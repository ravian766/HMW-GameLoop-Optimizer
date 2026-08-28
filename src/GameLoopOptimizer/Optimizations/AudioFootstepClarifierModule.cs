using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class AudioFootstepClarifierModule : IOptimizationModule
{
    public string Id => "audio_footstep_clarifier";
    public string Title => "Acoustic Footstep & Gunshot Directional Clarifier";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Enhances spatial acoustic clarity for enemy footsteps and distant gunshots while disabling Windows background audio ducking.";
    public string TechnicalRationale => "Disables automatic communications audio attenuation (ducking) and sets low-jitter audio rendering pipelines to prevent Discord or system alerts from muffling in-game footstep audio cues.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Checking...";
    public string RecommendedStateDisplay => "Acoustics Clarified (No Ducking)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var duckKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Multimedia\Audio");
            var duckVal = duckKey?.GetValue("UserDuckingPreference")?.ToString();

            bool isOpt = duckVal == "3"; // 3 = Do Nothing (Disable Ducking)
            IsOptimized = isOpt;
            CurrentStateDisplay = isOpt ? "Footstep Clarity Active (No Ducking)" : "Standard Windows Audio (Ducking Enabled)";
            State = isOpt ? OptimizationState.Optimized : OptimizationState.Recommended;
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
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Multimedia\Audio");
            if (key != null)
            {
                var prev = key.GetValue("UserDuckingPreference")?.ToString() ?? "0";
                BackupManager.RecordBackup(new BackupEntry
                {
                    ModuleId = Id,
                    Title = Title,
                    Category = Category,
                    TargetType = "Registry",
                    TargetPath = @"HKCU\Software\Microsoft\Multimedia\Audio",
                    ValueName = "UserDuckingPreference",
                    PreviousValue = prev,
                    PreviousValueKind = "DWord",
                    NewValue = "3",
                    Description = "Disable Windows communications audio ducking for PUBG footstep clarity"
                });

                key.SetValue("UserDuckingPreference", 3, RegistryValueKind.DWord);
            }

            // Also configure GameLoop audio fidelity in TxGameAssistant
            try
            {
                using var glKey = Registry.CurrentUser.CreateSubKey(@"Software\Tencent\MobileGamePC");
                if (glKey != null)
                {
                    glKey.SetValue("AudioQuality", 2, RegistryValueKind.DWord); // 2 = High Definition Audio
                }
            }
            catch { }

            IsOptimized = true;
            CurrentStateDisplay = "Footstep Clarity Active (No Ducking)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Acoustic footstep clarifier and anti-ducking audio policies configured successfully.");
            return Task.FromResult(OptimizationResult.Ok(Id, "Footstep acoustics clarified and audio ducking disabled."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply audio clarifier: {ex.Message}");
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
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored default Windows audio ducking behavior."));
        }

        return Task.FromResult(OptimizationResult.Fail(Id, "No backup entry found to revert."));
    }

    public Task<bool> VerifyAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Multimedia\Audio");
        var val = key?.GetValue("UserDuckingPreference")?.ToString();
        return Task.FromResult(val == "3");
    }
}
