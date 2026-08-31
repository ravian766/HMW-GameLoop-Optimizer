using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class GameLoopGraphicsModule : IOptimizationModule
{
    public string Id => "gl_graphics_engine";
    public string Title => "GameLoop Rendering Engine & Shader Caching";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures GameLoop to use the optimal hardware rasterization pipeline (DirectX+ or OpenGL+) and enables persistent shader cache compilation.";
    public string TechnicalRationale => "Enabling pre-compiled shader caching eliminates in-game asset compilation drops (1% lows), while selecting the optimal rendering backend (DirectX+ for dedicated NVIDIA/modern AMD, OpenGL+ for Intel iGPUs and legacy architectures) maximizes pipeline efficiency.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay { get; private set; } = "DirectX+ / Cache On / VSync Off";
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
        string recRenderer = rec.RecommendedRenderer == GraphicsRenderer.DirectXPlus ? "DirectX+" : "OpenGL+";
        RecommendedStateDisplay = $"{recRenderer} / Cache On / VSync Off";

        bool rendererMatches = gl.ActiveRenderer == rec.RecommendedRenderer;
        bool ok = rendererMatches && gl.LocalShaderCacheEnabled && gl.ShaderCacheEnabled && !gl.VSyncEnabled;
        IsOptimized = ok;
        CurrentStateDisplay = ok ? $"{recRenderer} / Shader Cache Enabled" : $"{(gl.ForceDirectX ? "DirectX+" : "OpenGL+")}, Cache: {gl.LocalShaderCacheEnabled}, VSync: {gl.VSyncEnabled}";
        State = ok ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsInstalled)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, "GameLoop installation was not detected."));
        }

        try
        {
            var rec = RecommendationEngine.Calculate(hw);
            int targetDirectXVal = rec.RecommendedForceDirectX ? 1 : 0;
            string targetRendererName = rec.RecommendedRenderer == GraphicsRenderer.DirectXPlus ? "DirectX+" : "OpenGL+";

            using var key = Registry.CurrentUser.CreateSubKey(gl.RegistryKeyPath);
            if (key == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open GameLoop registry key."));

            // Record backups
            BackupRegistryValue(key, gl.RegistryKeyPath, "ForceDirectX", targetDirectXVal.ToString());
            BackupRegistryValue(key, gl.RegistryKeyPath, "LocalShaderCacheEnabled", "1");
            BackupRegistryValue(key, gl.RegistryKeyPath, "ShaderCacheEnabled", "1");
            BackupRegistryValue(key, gl.RegistryKeyPath, "RenderOptimizeEnabled", "1");
            BackupRegistryValue(key, gl.RegistryKeyPath, "VSyncEnabled", "0");
            BackupRegistryValue(key, gl.RegistryKeyPath, "EnableGLESv3", "1");

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
                        subKey.SetValue("ForceDirectX", targetDirectXVal, RegistryValueKind.DWord);
                        subKey.SetValue("LocalShaderCacheEnabled", 1, RegistryValueKind.DWord);
                        subKey.SetValue("ShaderCacheEnabled", 1, RegistryValueKind.DWord);
                        subKey.SetValue("RenderOptimizeEnabled", 1, RegistryValueKind.DWord);
                        subKey.SetValue("VSyncEnabled", 0, RegistryValueKind.DWord);
                        subKey.SetValue("EnableGLESv3", 1, RegistryValueKind.DWord);
                    }
                }
                catch { }

                try
                {
                    using var hklmKey = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\WOW6432Node\{path}");
                    if (hklmKey != null)
                    {
                        hklmKey.SetValue("ForceDirectX", targetDirectXVal, RegistryValueKind.DWord);
                        hklmKey.SetValue("LocalShaderCacheEnabled", 1, RegistryValueKind.DWord);
                        hklmKey.SetValue("ShaderCacheEnabled", 1, RegistryValueKind.DWord);
                        hklmKey.SetValue("RenderOptimizeEnabled", 1, RegistryValueKind.DWord);
                        hklmKey.SetValue("VSyncEnabled", 0, RegistryValueKind.DWord);
                        hklmKey.SetValue("EnableGLESv3", 1, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }

            gl.ForceDirectX = rec.RecommendedForceDirectX;
            gl.LocalShaderCacheEnabled = true;
            gl.ShaderCacheEnabled = true;
            gl.RenderOptimizeEnabled = true;
            gl.VSyncEnabled = false;

            IsOptimized = true;
            CurrentStateDisplay = $"{targetRendererName} / Shader Cache Enabled";
            State = OptimizationState.Optimized;

            Logger.Success(Title, $"Applied {targetRendererName} renderer, local shader cache, and low-latency VSync settings to GameLoop & TGB.");
            return Task.FromResult(OptimizationResult.Ok(Id, $"{targetRendererName} rendering and Shader Cache successfully optimized on GameLoop/TGB."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to optimize graphics settings: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    private void BackupRegistryValue(RegistryKey key, string path, string valName, string newVal)
    {
        var prevObj = key.GetValue(valName);
        var prev = prevObj?.ToString();
        var kind = "DWord";
        if (prevObj != null)
        {
            try { kind = key.GetValueKind(valName).ToString(); } catch { }
        }

        BackupManager.RecordBackup(new BackupEntry
        {
            ModuleId = Id,
            Title = $"{Title} ({valName})",
            Category = Category,
            TargetType = "Registry",
            TargetPath = $@"HKCU\{path}",
            ValueName = valName,
            PreviousValue = prev,
            PreviousValueKind = kind,
            NewValue = newVal,
            Description = $"Set {valName} from {prev} to {newVal}"
        });
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            CurrentStateDisplay = "Restored";
            State = OptimizationState.NotOptimized;
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored previous graphics engine settings."));
        }
        return Task.FromResult(OptimizationResult.Fail(Id, "No backup found to revert."));
    }

    public Task<bool> VerifyAsync()
    {
        var gl = GameLoopDetector.DetectGameLoop();
        return Task.FromResult(gl.LocalShaderCacheEnabled && gl.ShaderCacheEnabled);
    }
}
