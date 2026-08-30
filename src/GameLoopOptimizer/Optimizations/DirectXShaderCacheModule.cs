using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class DirectXShaderCacheModule : IOptimizationModule
{
    public string Id => "win_directx_shader_cache_quota";
    public string Title => "DirectX 10 GB Shader Cache Quota";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Expands the Windows DirectX Shader Cache ceiling to 10 GB (10240 MB) and enables persistent shader compilation retention.";
    public string TechnicalRationale => "Windows defaults DirectX shader cache to 1-4 GB. When full, older shaders are purged, causing noticeable frame drops and stutters during gunfights when smoke, explosions, or weapon effects are first rendered.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "10 GB Quota (10240 MB, Persistent)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    private const string D3DKeyPath = @"SOFTWARE\Microsoft\Direct3D";
    private const string D3DKeyPath64 = @"SOFTWARE\WOW6432Node\Microsoft\Direct3D";
    private const string ValueName = "MaxShaderCacheSizeInMB";
    private const int TargetSizeMb = 10240;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(D3DKeyPath);
            int currentSize = key?.GetValue(ValueName) is int val ? val : 0;

            if (currentSize >= TargetSizeMb)
            {
                CurrentStateDisplay = $"{currentSize / 1024.0:F0} GB ({currentSize} MB)";
                IsOptimized = true;
                State = OptimizationState.Optimized;
            }
            else if (currentSize > 0)
            {
                CurrentStateDisplay = $"{currentSize} MB (Restricted)";
                IsOptimized = false;
                State = OptimizationState.Recommended;
            }
            else
            {
                CurrentStateDisplay = "Default (Windows Auto ~1-4 GB)";
                IsOptimized = false;
                State = OptimizationState.Recommended;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("DirectXShaderCache", $"Analysis error: {ex.Message}");
            CurrentStateDisplay = "Default (Windows Auto)";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!PermissionManager.IsAdministrator)
        {
            return OptimizationResult.Fail(Id, "Administrator rights required to configure system DirectX shader cache quota.");
        }

        try
        {
            // Record backup
            using var readKey = Registry.LocalMachine.OpenSubKey(D3DKeyPath);
            var prevVal = readKey?.GetValue(ValueName);
            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "RegistryDWord",
                TargetPath = $"HKLM\\{D3DKeyPath}",
                ValueName = ValueName,
                PreviousValue = prevVal?.ToString() ?? "NOT_SET",
                NewValue = TargetSizeMb.ToString(),
                Description = "DirectX Shader Cache Size 10GB Quota"
            });

            // Apply 64-bit & 32-bit Direct3D cache ceiling
            using (var key = Registry.LocalMachine.CreateSubKey(D3DKeyPath))
            {
                key?.SetValue(ValueName, TargetSizeMb, RegistryValueKind.DWord);
            }

            try
            {
                using var key64 = Registry.LocalMachine.CreateSubKey(D3DKeyPath64);
                key64?.SetValue(ValueName, TargetSizeMb, RegistryValueKind.DWord);
            }
            catch { }

            IsOptimized = true;
            State = OptimizationState.Optimized;
            CurrentStateDisplay = "10 GB (10240 MB)";

            Logger.Success("DirectXShaderCache", "Expanded Windows DirectX Shader Cache ceiling to 10 GB (10240 MB).");

            return OptimizationResult.Ok(Id, "Expanded Windows DirectX Shader Cache quota to 10 GB (10240 MB).", prevVal?.ToString() ?? "Default", "10240 MB");
        }
        catch (Exception ex)
        {
            Logger.Error("DirectXShaderCache", $"Failed to set DirectX shader cache quota: {ex.Message}");
            return OptimizationResult.Fail(Id, $"Failed to set shader cache quota: {ex.Message}", ex);
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        if (!PermissionManager.IsAdministrator)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, "Administrator rights required to restore DirectX shader cache."));
        }

        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            State = OptimizationState.Recommended;
            CurrentStateDisplay = "Default (Windows Auto)";
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored DirectX Shader Cache quota to Windows default."));
        }

        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(D3DKeyPath, true))
            {
                if (key != null && key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }

            try
            {
                using var key64 = Registry.LocalMachine.OpenSubKey(D3DKeyPath64, true);
                if (key64 != null && key64.GetValue(ValueName) != null)
                {
                    key64.DeleteValue(ValueName, false);
                }
            }
            catch { }

            IsOptimized = false;
            State = OptimizationState.Recommended;
            CurrentStateDisplay = "Default (Windows Auto)";

            Logger.Info("DirectXShaderCache", "Restored DirectX Shader Cache quota to Windows default.");

            return Task.FromResult(OptimizationResult.Ok(Id, "Restored DirectX Shader Cache quota to Windows default."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Rollback failed: {ex.Message}", ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(D3DKeyPath);
            int currentSize = key?.GetValue(ValueName) is int val ? val : 0;
            return Task.FromResult(currentSize >= TargetSizeMb);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
