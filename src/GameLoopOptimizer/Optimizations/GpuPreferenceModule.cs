using System.IO;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class GpuPreferenceModule : IOptimizationModule
{
    public string Id => "win_gpu_preference";
    public string Title => "Windows Discrete GPU Preference Enforcer";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Forces Windows DirectX and DWM to bind all GameLoop emulator executables strictly to your dedicated High-Performance NVIDIA/AMD GPU.";
    public string TechnicalRationale => "On laptops and dual-GPU desktops, Windows graphics scheduling may launch emulator sub-processes (like AndroidEmulatorEn.exe or aow_exe) on the integrated GPU, causing severe rendering slowdowns.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "High Performance GPU (GpuPreference=2)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    private const string UserGpuPrefPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UserGpuPrefPath);
            if (key == null)
            {
                CurrentStateDisplay = "Default (Windows Auto)";
                State = OptimizationState.Recommended;
                IsOptimized = false;
                return Task.FromResult(State);
            }

            var valueNames = key.GetValueNames();
            bool hasGameLoopEntry = valueNames.Any(n => n.Contains("AppMarket.exe", StringComparison.OrdinalIgnoreCase) ||
                                                        n.Contains("AndroidEmulator", StringComparison.OrdinalIgnoreCase) ||
                                                        n.Contains("aow_exe", StringComparison.OrdinalIgnoreCase));

            IsOptimized = hasGameLoopEntry;
            CurrentStateDisplay = hasGameLoopEntry ? "High Performance GPU Enforced" : "Default (Windows Auto)";
            State = hasGameLoopEntry ? OptimizationState.Optimized : OptimizationState.Recommended;
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
            using var key = Registry.CurrentUser.CreateSubKey(UserGpuPrefPath);
            if (key == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open DirectX UserGpuPreferences registry key."));

            var exeCandidates = new List<string>();
            string mainExe = GameLoopDetector.FindGameLoopExePath();
            if (!string.IsNullOrEmpty(mainExe)) exeCandidates.Add(mainExe);

            if (!string.IsNullOrEmpty(gl.InstallPath))
            {
                exeCandidates.Add(Path.Combine(gl.InstallPath, "AppMarket.exe"));
                exeCandidates.Add(Path.Combine(gl.InstallPath, "AppMarket", "AppMarket.exe"));
                exeCandidates.Add(Path.Combine(gl.InstallPath, "ui", "AndroidEmulatorEn.exe"));
                exeCandidates.Add(Path.Combine(gl.InstallPath, "ui", "AndroidEmulator.exe"));
                exeCandidates.Add(Path.Combine(gl.InstallPath, "ui", "AndroidEmulatorEx.exe"));
                exeCandidates.Add(Path.Combine(gl.InstallPath, "ui", "aow_exe.exe"));
            }

            int configuredCount = 0;
            foreach (var exe in exeCandidates.Distinct())
            {
                if (File.Exists(exe))
                {
                    var prev = key.GetValue(exe)?.ToString();
                    BackupManager.RecordBackup(new BackupEntry
                    {
                        ModuleId = Id,
                        Title = $"{Title} ({Path.GetFileName(exe)})",
                        Category = Category,
                        TargetType = "Registry",
                        TargetPath = $@"HKCU\{UserGpuPrefPath}",
                        ValueName = exe,
                        PreviousValue = prev,
                        PreviousValueKind = "String",
                        NewValue = "GpuPreference=2;",
                        Description = $"Force High Performance GPU for {Path.GetFileName(exe)}"
                    });

                    key.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
                    configuredCount++;
                }
            }

            IsOptimized = true;
            CurrentStateDisplay = $"High Performance ({configuredCount} executables)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, $"Enforced High-Performance discrete GPU preference for {configuredCount} GameLoop executables.");
            return Task.FromResult(OptimizationResult.Ok(Id, $"Configured Windows High Performance GPU preference for {configuredCount} executables."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to enforce GPU preference: {ex.Message}");
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
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored Windows GPU preference to defaults."));
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UserGpuPrefPath, writable: true);
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    if (name.Contains("TxGameAssistant", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("AppMarket", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("AndroidEmulator", StringComparison.OrdinalIgnoreCase))
                    {
                        key.DeleteValue(name, false);
                    }
                }
            }

            IsOptimized = false;
            CurrentStateDisplay = "Default (Windows Auto)";
            State = OptimizationState.Recommended;
            return Task.FromResult(OptimizationResult.Ok(Id, "Cleared GameLoop GPU preferences from Windows."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UserGpuPrefPath);
        if (key != null)
        {
            return Task.FromResult(key.GetValueNames().Any(n => n.Contains("AppMarket", StringComparison.OrdinalIgnoreCase) || n.Contains("AndroidEmulator", StringComparison.OrdinalIgnoreCase)));
        }
        return Task.FromResult(false);
    }
}
