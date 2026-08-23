using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class HardwareRecommendations
{
    public int RecommendedCpuCores { get; set; }
    public int RecommendedRamMb { get; set; }
    public int RecommendedResWidth { get; set; }
    public int RecommendedResHeight { get; set; }
    public int RecommendedDpi { get; set; }
    public bool RecommendedForceDirectX { get; set; }
    public bool RecommendedShaderCache { get; set; }
    public bool RecommendedVSync { get; set; }
    public int RecommendedFpsLevel { get; set; }
    public int RecommendedRenderQuality { get; set; } // 0=Smooth, 1=Balanced, 2=HD, 3=HDR
    public string RecommendationSummary { get; set; } = string.Empty;
    public string TierLabel { get; set; } = string.Empty;
}

public static class RecommendationEngine
{
    public static HardwareRecommendations Calculate(HardwareInfo hw)
    {
        var rec = new HardwareRecommendations();

        // 1. Dynamic CPU Core Allocation
        if (hw.LogicalProcessors <= 4)
        {
            rec.RecommendedCpuCores = 2;
        }
        else if (hw.LogicalProcessors <= 8)
        {
            rec.RecommendedCpuCores = 4; // Sweet spot for 4C/8T (like i3-12100F, Ryzen 3600/5600)
        }
        else
        {
            rec.RecommendedCpuCores = 4; // Prevents Android scheduler lock contention on 12+ thread CPUs
        }

        // 2. Dynamic RAM Allocation
        if (hw.TotalRamGb <= 8.5)
        {
            rec.RecommendedRamMb = 4096; // Leave 4GB for Windows OS
        }
        else if (hw.TotalRamGb <= 16.5)
        {
            rec.RecommendedRamMb = 8192; // 8GB for GameLoop, 8GB for Host
        }
        else
        {
            rec.RecommendedRamMb = 8192; // 8GB is optimal cap for Android VM
        }

        // 3. Dynamic Resolution & DPI
        if (hw.CalculatedTier == HardwareTier.LowEnd)
        {
            rec.RecommendedResWidth = 1280;
            rec.RecommendedResHeight = 720;
            rec.RecommendedDpi = 240;
            rec.RecommendedFpsLevel = 60;
            rec.RecommendedRenderQuality = 0; // Smooth
            rec.TierLabel = "Entry-Level (Focus on Low Frame-Time Variance)";
        }
        else if (hw.CalculatedTier == HardwareTier.MidRange)
        {
            rec.RecommendedResWidth = 1920;
            rec.RecommendedResHeight = 1080;
            rec.RecommendedDpi = 320;
            rec.RecommendedFpsLevel = hw.RefreshRateHz >= 90 ? 120 : 90;
            rec.RecommendedRenderQuality = 2; // HD
            rec.TierLabel = "Mid-Range (Balanced High-FPS & Visual Clarity)";
        }
        else // HighEnd
        {
            if (hw.ScreenWidth >= 2560)
            {
                rec.RecommendedResWidth = 2560;
                rec.RecommendedResHeight = 1440;
                rec.RecommendedDpi = 400;
            }
            else
            {
                rec.RecommendedResWidth = 1920;
                rec.RecommendedResHeight = 1080;
                rec.RecommendedDpi = 320;
            }

            rec.RecommendedFpsLevel = 120;
            rec.RecommendedRenderQuality = 2; // HD (preferred for lowest latency competitive play)
            rec.TierLabel = "High-End (Maximum 120 FPS Target & Low Input Lag)";
        }

        // 4. Graphics Engine & Shader Cache
        rec.RecommendedForceDirectX = true; // DirectX+ is the most stable and responsive renderer on Windows
        rec.RecommendedShaderCache = true;  // Crucial for eliminating micro-stutters during asset streaming
        rec.RecommendedVSync = false;       // Eliminates render queue input lag

        rec.RecommendationSummary = $"Based on your {hw.CpuName} ({hw.LogicalProcessors} threads), {hw.GpuName} ({hw.DedicatedVramMb:F0}MB VRAM), and {hw.TotalRamGb:F0}GB RAM, " +
            $"allocating {rec.RecommendedCpuCores} cores and {rec.RecommendedRamMb / 1024}GB RAM with DirectX+ and local shader caching is recommended for optimal frame pacing.";

        return rec;
    }
}
