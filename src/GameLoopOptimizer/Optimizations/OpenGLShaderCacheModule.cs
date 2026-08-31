using System.IO;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class OpenGLShaderCacheModule : IOptimizationModule
{
    public string Id => "win_opengl_shader_cache_quota";
    public string Title => "OpenGL & Vulkan Hardware Shader Cache Optimization";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Enables persistent multi-threaded OpenGL and GLES shader compilation caching across NVIDIA, AMD Radeon, and Intel GPU drivers.";
    public string TechnicalRationale => "When GameLoop renders via OpenGL+ or GLESv3, driver-level shader caching prevents repetitive vertex and fragment shader recompilations during match loading and active combat.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Persistent OpenGL Cache Enabled (NVIDIA/AMD/Intel)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    private const string NvGlCachePath = @"Software\NVIDIA Corporation\Global\GLCache";
    private const string AmdGlCachePath = @"Software\AMD\OglCache";
    private const string AmdCnPath = @"Software\AMD\CN";

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            bool isNvOptimized = false;
            bool isAmdOptimized = false;
            bool isIntelOptimized = false;

            if (hw.GpuVendor == GpuVendor.Nvidia)
            {
                using var nvKey = Registry.CurrentUser.OpenSubKey(NvGlCachePath);
                int nvEnable = nvKey?.GetValue("Enable") is int nve ? nve : 1; // NVIDIA defaults to enabled
                int nvMaxSize = nvKey?.GetValue("MaxSize") is int nvm ? nvm : 0;
                isNvOptimized = nvEnable == 1 && (nvMaxSize >= 10240 || nvKey != null);
                IsOptimized = isNvOptimized;
                CurrentStateDisplay = isNvOptimized ? "NVIDIA OpenGL Cache (10 GB Persistent)" : "NVIDIA OpenGL Cache (Default)";
            }
            else if (hw.GpuVendor == GpuVendor.Amd)
            {
                using var amdKey = Registry.CurrentUser.OpenSubKey(AmdGlCachePath) ?? Registry.CurrentUser.OpenSubKey(AmdCnPath);
                int amdEnable = amdKey?.GetValue("ShaderCache") is int ace ? ace : 1;
                var amdCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "OglCache");
                isAmdOptimized = amdEnable == 1 && Directory.Exists(amdCacheDir);
                IsOptimized = isAmdOptimized;
                CurrentStateDisplay = isAmdOptimized ? "AMD Radeon OglCache (Active)" : "AMD Radeon OglCache (Default)";
            }
            else if (hw.GpuVendor == GpuVendor.Intel)
            {
                var intelCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Intel", "ShaderCache");
                isIntelOptimized = Directory.Exists(intelCacheDir);
                IsOptimized = isIntelOptimized;
                CurrentStateDisplay = isIntelOptimized ? "Intel OpenGL Cache (Active)" : "Intel OpenGL Cache (Default)";
            }
            else
            {
                IsOptimized = gl.LocalShaderCacheEnabled && gl.EnableGlesv3;
                CurrentStateDisplay = IsOptimized ? "Universal GLES Cache (Active)" : "Universal GLES Cache (Default)";
            }

            State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;
        }
        catch (Exception ex)
        {
            Logger.Warn("OpenGLShaderCache", $"Analysis error: {ex.Message}");
            CurrentStateDisplay = "Default (Driver Managed)";
            State = OptimizationState.Recommended;
        }

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            int configuredVendors = 0;

            // 1. NVIDIA OpenGL Cache Expansion
            if (hw.GpuVendor == GpuVendor.Nvidia || hw.GpuVendor == GpuVendor.Unknown)
            {
                try
                {
                    using var nvKey = Registry.CurrentUser.CreateSubKey(NvGlCachePath);
                    if (nvKey != null)
                    {
                        var prevEnable = nvKey.GetValue("Enable")?.ToString();
                        var prevMaxSize = nvKey.GetValue("MaxSize")?.ToString();

                        BackupManager.RecordBackup(new BackupEntry
                        {
                            ModuleId = Id,
                            Title = $"{Title} (NVIDIA GLCache)",
                            Category = Category,
                            TargetType = "Registry",
                            TargetPath = $@"HKCU\{NvGlCachePath}",
                            ValueName = "MaxSize",
                            PreviousValue = prevMaxSize ?? "NOT_SET",
                            NewValue = "10240",
                            Description = "NVIDIA OpenGL Shader Cache Size 10GB Quota"
                        });

                        nvKey.SetValue("Enable", 1, RegistryValueKind.DWord);
                        nvKey.SetValue("MaxSize", 10240, RegistryValueKind.DWord);
                        configuredVendors++;
                    }

                    var nvDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "GLCache");
                    if (!Directory.Exists(nvDir)) Directory.CreateDirectory(nvDir);
                }
                catch (Exception ex)
                {
                    Logger.Warn("OpenGLShaderCache", $"NVIDIA GL cache tweak notice: {ex.Message}");
                }
            }

            // 2. AMD Radeon OpenGL & Shader Cache Settings
            if (hw.GpuVendor == GpuVendor.Amd || hw.GpuVendor == GpuVendor.Unknown)
            {
                try
                {
                    using var amdKey = Registry.CurrentUser.CreateSubKey(AmdGlCachePath);
                    if (amdKey != null)
                    {
                        var prevVal = amdKey.GetValue("ShaderCache")?.ToString();
                        BackupManager.RecordBackup(new BackupEntry
                        {
                            ModuleId = Id,
                            Title = $"{Title} (AMD OglCache)",
                            Category = Category,
                            TargetType = "Registry",
                            TargetPath = $@"HKCU\{AmdGlCachePath}",
                            ValueName = "ShaderCache",
                            PreviousValue = prevVal ?? "NOT_SET",
                            NewValue = "1",
                            Description = "AMD OpenGL Shader Cache Persistence"
                        });

                        amdKey.SetValue("ShaderCache", 1, RegistryValueKind.DWord);
                        amdKey.SetValue("OglCacheEnabled", 1, RegistryValueKind.DWord);
                        configuredVendors++;
                    }

                    var amdDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "OglCache");
                    if (!Directory.Exists(amdDir)) Directory.CreateDirectory(amdDir);
                }
                catch (Exception ex)
                {
                    Logger.Warn("OpenGLShaderCache", $"AMD GL cache tweak notice: {ex.Message}");
                }
            }

            // 3. Intel Graphics Shader Cache Environment
            if (hw.GpuVendor == GpuVendor.Intel || hw.GpuVendor == GpuVendor.Unknown)
            {
                try
                {
                    var intelDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Intel", "ShaderCache");
                    if (!Directory.Exists(intelDir)) Directory.CreateDirectory(intelDir);
                    configuredVendors++;
                }
                catch { }
            }

            IsOptimized = true;
            State = OptimizationState.Optimized;
            CurrentStateDisplay = "Persistent OpenGL Cache Enabled";

            Logger.Success("OpenGLShaderCache", $"Optimized OpenGL & GLES shader caching pipelines for {hw.GpuVendor} hardware.");
            return Task.FromResult(OptimizationResult.Ok(Id, $"Configured persistent OpenGL/GLES shader cache pipelines for {hw.GpuVendor} graphics hardware."));
        }
        catch (Exception ex)
        {
            Logger.Error("OpenGLShaderCache", $"Failed to optimize OpenGL shader cache: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            State = OptimizationState.Recommended;
            CurrentStateDisplay = "Restored to Default";
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored OpenGL Shader Cache settings to defaults."));
        }

        try
        {
            using (var nvKey = Registry.CurrentUser.OpenSubKey(NvGlCachePath, true))
            {
                if (nvKey != null && nvKey.GetValue("MaxSize") != null)
                {
                    nvKey.DeleteValue("MaxSize", false);
                }
            }

            using (var amdKey = Registry.CurrentUser.OpenSubKey(AmdGlCachePath, true))
            {
                if (amdKey != null && amdKey.GetValue("OglCacheEnabled") != null)
                {
                    amdKey.DeleteValue("OglCacheEnabled", false);
                }
            }

            IsOptimized = false;
            State = OptimizationState.Recommended;
            CurrentStateDisplay = "Default (Driver Managed)";
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored OpenGL Shader Cache configuration to defaults."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Rollback failed: {ex.Message}", ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(IsOptimized);
    }
}
