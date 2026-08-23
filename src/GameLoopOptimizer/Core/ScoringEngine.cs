using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public static class ScoringEngine
{
    public static OptimizationScore CalculateScore(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl, HardwareRecommendations rec)
    {
        var score = new OptimizationScore();
        var explanations = new List<string>();

        // 1. Windows Configuration (Max 20)
        int winPts = 0;
        if (sys.IsGameModeEnabled)
        {
            winPts += 10;
        }
        else
        {
            explanations.Add("Windows Game Mode is disabled; enabling it prioritizes CPU/GPU resources for the active emulator process.");
        }

        if (sys.CurrentTimerResolutionMs <= 1.0)
        {
            winPts += 5;
        }
        else
        {
            explanations.Add("Standard Windows 15.6ms timer resolution detected; high-resolution 0.5ms-1.0ms timer is recommended for lower frame-time variance.");
        }

        if (sys.AreVisualEffectsOptimized)
        {
            winPts += 5;
        }
        else
        {
            winPts += 3; // Partial credit
        }
        score.WindowsConfig.Score = Math.Min(score.WindowsConfig.MaxScore, winPts);
        score.WindowsConfig.Status = winPts >= 15 ? "Optimized" : "Needs Review";
        score.WindowsConfig.Details = $"Game Mode: {(sys.IsGameModeEnabled ? "Active" : "Disabled")}, Timer: {sys.CurrentTimerResolutionMs:F1}ms";

        // 2. Power Delivery (Max 15)
        int powerPts = 0;
        if (sys.IsHighPerformancePowerPlan)
        {
            powerPts = 15;
            score.PowerDelivery.Status = "Optimized";
            score.PowerDelivery.Details = $"Active Plan: {sys.ActivePowerPlanName}";
        }
        else
        {
            powerPts = 5;
            score.PowerDelivery.Status = "Balanced Plan";
            score.PowerDelivery.Details = $"Active Plan: {sys.ActivePowerPlanName}";
            explanations.Add($"Power scheme is currently set to '{sys.ActivePowerPlanName}'. Switching to High/Ultimate Performance may reduce CPU downclocking latency.");
        }
        score.PowerDelivery.Score = powerPts;

        // 3. GameLoop Resource Allocation (Max 25)
        int glPts = 0;
        if (gl.IsInstalled)
        {
            if (gl.VmCpuCount == rec.RecommendedCpuCores)
            {
                glPts += 15;
            }
            else
            {
                glPts += 8;
                explanations.Add($"GameLoop CPU is set to {gl.VmCpuCount} cores; {rec.RecommendedCpuCores} cores is recommended dynamically for your {hw.LogicalProcessors}-thread CPU.");
            }

            if (gl.VmMemorySizeInMb == rec.RecommendedRamMb)
            {
                glPts += 10;
            }
            else
            {
                glPts += 5;
                explanations.Add($"GameLoop RAM is allocated at {gl.VmMemorySizeInMb} MB; {rec.RecommendedRamMb} MB is recommended for optimal performance without starving Windows host memory.");
            }
            score.GameLoopConfig.Status = glPts >= 20 ? "Optimized" : "Recommended Changes";
            score.GameLoopConfig.Details = $"{gl.VmCpuCount} Cores, {gl.VmMemorySizeInMb} MB RAM";
        }
        else
        {
            glPts = 12; // Neutral
            score.GameLoopConfig.Status = "Not Installed";
            score.GameLoopConfig.Details = "GameLoop not detected";
        }
        score.GameLoopConfig.Score = glPts;

        // 4. Graphics & Shader Cache (Max 20)
        int gfxPts = 0;
        if (gl.IsInstalled)
        {
            if (gl.ForceDirectX)
            {
                gfxPts += 10;
            }
            else
            {
                gfxPts += 4;
                explanations.Add("GameLoop is not set to DirectX+ rendering mode; DirectX+ is recommended for modern dedicated GPUs.");
            }

            if (gl.LocalShaderCacheEnabled && gl.ShaderCacheEnabled)
            {
                gfxPts += 10;
            }
            else
            {
                explanations.Add("Shader cache is currently disabled in GameLoop; enabling it prevents in-game asset compilation stutters.");
            }
            score.GraphicsSettings.Status = gfxPts >= 18 ? "Optimized" : "Needs Review";
            score.GraphicsSettings.Details = $"DirectX+: {gl.ForceDirectX}, Shader Cache: {gl.LocalShaderCacheEnabled}";
        }
        else
        {
            gfxPts = 10;
            score.GraphicsSettings.Status = "Default";
            score.GraphicsSettings.Details = "Default graphics configuration";
        }
        score.GraphicsSettings.Score = gfxPts;

        // 5. Memory & Storage (Max 10)
        int memPts = 0;
        if (hw.FreeDiskSpaceGb >= 20) memPts += 5;
        else memPts += 2;

        memPts += 5; // Working set baseline
        score.MemoryStorage.Score = memPts;
        score.MemoryStorage.Status = "Good";
        score.MemoryStorage.Details = $"{hw.FreeDiskSpaceGb:F0} GB Free Space ({hw.PrimaryDriveType})";

        // 6. Background Processes (Max 10)
        int bgPts = 10;
        if (sys.HighCpuProcessesCount > 3)
        {
            bgPts = 5;
            explanations.Add($"Detected {sys.HighCpuProcessesCount} background processes with significant resource usage.");
        }
        score.BackgroundProcesses.Score = bgPts;
        score.BackgroundProcesses.Status = bgPts == 10 ? "Clean" : "Moderate Overhead";
        score.BackgroundProcesses.Details = $"{sys.HighCpuProcessesCount} high-overhead apps";

        // Total
        score.TotalScore = score.WindowsConfig.Score +
                           score.PowerDelivery.Score +
                           score.GameLoopConfig.Score +
                           score.GraphicsSettings.Score +
                           score.MemoryStorage.Score +
                           score.BackgroundProcesses.Score;

        if (explanations.Count == 0)
        {
            explanations.Add("System and GameLoop configuration is well-tuned for low frame-time variance.");
        }

        score.HonestExplanations = explanations;
        return score;
    }
}
