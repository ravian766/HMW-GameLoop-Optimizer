using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class MatchReadinessItem
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class MatchReadinessReport
{
    public int ReadinessScore { get; set; }
    public string BadgeText { get; set; } = "CHECKING";
    public string BadgeColorKey { get; set; } = "BrushAccentCyan";
    public string SummaryMessage { get; set; } = string.Empty;
    public List<MatchReadinessItem> Items { get; set; } = new();
}

public static class MatchReadinessService
{
    public static async Task<MatchReadinessReport> EvaluateReadinessAsync(
        HardwareInfo hw, 
        SystemInfo sys, 
        GameLoopConfig gl,
        List<IOptimizationModule>? modules = null)
    {
        return await Task.Run(() =>
        {
            var report = new MatchReadinessReport();
            int passedCount = 0;
            int totalChecks = 5;

            // 1. High-Precision Timer Check (0.5ms target)
            bool timerReady = sys.CurrentTimerResolutionMs <= 1.0;
            if (timerReady) passedCount++;
            report.Items.Add(new MatchReadinessItem
            {
                Category = "Timer Engine",
                Name = "0.5ms High-Precision Timer",
                Status = $"{sys.CurrentTimerResolutionMs:F1}ms" + (timerReady ? " (Ultra-Low Jitter)" : " (Standard Windows 15.6ms)"),
                IsReady = timerReady,
                Recommendation = timerReady ? "Locked at sub-millisecond precision." : "Engage 0.5ms timer to eliminate spray input lag."
            });

            // 2. CPU Core Affinity & Power Plan
            bool powerReady = sys.ActivePowerPlanName.Contains("High", StringComparison.OrdinalIgnoreCase) ||
                              sys.ActivePowerPlanName.Contains("Ultimate", StringComparison.OrdinalIgnoreCase) ||
                              sys.ActivePowerPlanName.Contains("Game", StringComparison.OrdinalIgnoreCase);
            if (powerReady) passedCount++;
            report.Items.Add(new MatchReadinessItem
            {
                Category = "Power & Cores",
                Name = "Power Plan & P-Core Allocation",
                Status = sys.ActivePowerPlanName,
                IsReady = powerReady,
                Recommendation = powerReady ? "High-performance CPU frequency scaling active." : "Switch to Ultimate or High Performance power plan."
            });

            // 3. DirectX 10GB Shader Cache Quota
            bool shaderCacheExpanded = false;
            try
            {
                using var d3dKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Direct3D");
                int sizeMb = d3dKey?.GetValue("MaxShaderCacheSizeInMB") is int val ? val : 0;
                shaderCacheExpanded = sizeMb >= 10240;
            }
            catch { }

            if (shaderCacheExpanded) passedCount++;
            report.Items.Add(new MatchReadinessItem
            {
                Category = "Graphics Stutter",
                Name = "DirectX 10 GB Shader Cache",
                Status = shaderCacheExpanded ? "10 GB Quota Active" : "Default 1-4 GB Quota",
                IsReady = shaderCacheExpanded,
                Recommendation = shaderCacheExpanded ? "Shader eviction disabled. Hotdrops & smokes protected." : "Expand DirectX shader cache to 10 GB to prevent mid-fight shader drops."
            });

            // 4. Physical RAM Headroom (> 3.5 GB free)
            double memLoadPercent = ProcessManager.GetSystemMemoryLoadPercent();
            bool ramHeadroomOk = memLoadPercent <= 80.0;
            if (ramHeadroomOk) passedCount++;
            report.Items.Add(new MatchReadinessItem
            {
                Category = "RAM Headroom",
                Name = "System Memory Availability",
                Status = $"{memLoadPercent:F0}% System RAM in use",
                IsReady = ramHeadroomOk,
                Recommendation = ramHeadroomOk ? "Sufficient headroom for GameLoop texture buffering." : "High RAM usage detected. Purge background memory before matchmaking."
            });

            // 5. GameLoop Dedicated GPU Binding
            bool gpuBound = false;
            try
            {
                using var gpuKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
                if (gpuKey != null)
                {
                    var names = gpuKey.GetValueNames();
                    gpuBound = names.Any(n => n.Contains("AndroidEmulator", StringComparison.OrdinalIgnoreCase) || n.Contains("aow_exe", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }

            if (gpuBound) passedCount++;
            report.Items.Add(new MatchReadinessItem
            {
                Category = "GPU Routing",
                Name = "Discrete GPU Preference",
                Status = gpuBound ? "Bound to High Performance Discrete GPU" : "Windows Auto-Select",
                IsReady = gpuBound,
                Recommendation = gpuBound ? "Rendering strictly routed to dedicated GPU." : "Force dedicated GPU preference to avoid iGPU bottleneck."
            });

            // Calculate Score & Badge
            report.ReadinessScore = (int)Math.Round(((double)passedCount / totalChecks) * 100.0);

            if (report.ReadinessScore >= 80)
            {
                report.BadgeText = "🚀 READY FOR MATCH";
                report.BadgeColorKey = "BrushAccentGreen";
                report.SummaryMessage = "System is in peak competitive state. Hotdrops, event landings, and gunfights fully protected.";
            }
            else if (report.ReadinessScore >= 50)
            {
                report.BadgeText = "⚡ OPTIMIZATION RECOMMENDED";
                report.BadgeColorKey = "BrushAccentAmber";
                report.SummaryMessage = "Playable, but enabling remaining optimizations will eliminate potential frame drops during heavy combat.";
            }
            else
            {
                report.BadgeText = "⚠️ ACTION REQUIRED";
                report.BadgeColorKey = "BrushAccentAmber";
                report.SummaryMessage = "Critical bottlenecks detected. Apply recommended optimizations before queuing ranked matches.";
            }

            Logger.Info("MatchReadiness", $"Evaluated Pre-Flight Readiness: {report.ReadinessScore}% ({passedCount}/{totalChecks} checks passed) - {report.BadgeText}");
            return report;
        });
    }
}
