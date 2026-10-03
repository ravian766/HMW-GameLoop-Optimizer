using System.IO;
using System.Text.Json;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.Core;

public class ProfileExportModel
{
    public string ProfileName { get; set; } = string.Empty;
    public string Version { get; set; } = "2.2.0";
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;
    public string TargetHardwareTier { get; set; } = string.Empty;
    public List<string> SelectedModuleIds { get; set; } = new();
    public int RecommendedCpuCores { get; set; } = 4;
    public int RecommendedRamMb { get; set; } = 4096;
    public int RecommendedResWidth { get; set; } = 1920;
    public int RecommendedResHeight { get; set; } = 1080;
    public int RecommendedFpsLevel { get; set; } = 90;
    public string Notes { get; set; } = string.Empty;
}

public static class OptimizationProfileManager
{
    public static OptimizationProfile RecommendProfile(HardwareInfo hw, SystemInfo sys)
    {
        // 1. Laptop on battery power -> Battery profile
        if (sys.IsOnBattery)
        {
            return OptimizationProfile.LaptopBattery;
        }

        // 2. Hardware Tier
        return hw.CalculatedTier switch
        {
            HardwareTier.LowEnd => OptimizationProfile.LowEndPC,
            HardwareTier.MidRange => OptimizationProfile.Balanced,
            HardwareTier.HighEnd => OptimizationProfile.CompetitiveFps,
            _ => OptimizationProfile.Balanced
        };
    }

    public static bool ShouldModuleBeSelectedForProfile(IOptimizationModule module, OptimizationProfile profile, HardwareInfo hw, SystemInfo sys)
    {
        // Safe mode or unsupported modules never auto-selected
        if (module.RiskLevel == RiskLevel.NotRecommended)
        {
            return false;
        }

        return profile switch
        {
            OptimizationProfile.Safe =>
                module.RiskLevel == RiskLevel.Safe,

            OptimizationProfile.Balanced =>
                module.RiskLevel == RiskLevel.Safe || module.RiskLevel == RiskLevel.Low,

            OptimizationProfile.MaximumPerformance =>
                module.RiskLevel != RiskLevel.NotRecommended,

            OptimizationProfile.Competitive =>
                // Competitive focuses on input responsiveness, timer resolution, MMCSS, GPU preference, and stable scheduling
                (module.Category == OptimizationCategory.WindowsConfig ||
                 module.Category == OptimizationCategory.PowerDelivery ||
                 module.Category == OptimizationCategory.GameLoopEngine ||
                 module.Category == OptimizationCategory.NetworkInput ||
                 module.Id.Contains("Timer") || module.Id.Contains("Mmcss") || module.Id.Contains("Audio") || module.Id.Contains("Input"))
                && module.RiskLevel != RiskLevel.High && module.RiskLevel != RiskLevel.NotRecommended,

            OptimizationProfile.StableFps =>
                // Focus on minimizing frame-time variance, shader caching, standby memory cleaning, and steady power
                (module.Id.Contains("Shader") || module.Id.Contains("Standby") || module.Id.Contains("GameMode") ||
                 module.Id.Contains("Resource") || module.Id.Contains("Graphics") || module.Id.Contains("Throttle"))
                && (module.RiskLevel == RiskLevel.Safe || module.RiskLevel == RiskLevel.Low),

            OptimizationProfile.LowEndPC =>
                // Focus on freeing RAM, lighter graphics, cleaning temp files, disabling game DVR
                (module.Category == OptimizationCategory.MemoryStorage ||
                 module.Category == OptimizationCategory.BackgroundProcess ||
                 module.Id.Contains("VisualEffects") || module.Id.Contains("GameDvr") || module.Id.Contains("Standby") || module.Id.Contains("Cleanup"))
                && module.RiskLevel == RiskLevel.Safe,

            OptimizationProfile.MidRangePC =>
                (module.RiskLevel == RiskLevel.Safe || module.RiskLevel == RiskLevel.Low) &&
                module.Category != OptimizationCategory.BackgroundProcess,

            OptimizationProfile.HighEndPC =>
                // Full high refresh-rate optimizations, 120 FPS unlock, GPU preference
                module.RiskLevel != RiskLevel.High && module.RiskLevel != RiskLevel.NotRecommended,

            OptimizationProfile.LaptopBattery =>
                // Conserve battery: keep balanced power, do not force 0.5ms timer (causes high CPU interrupt rate), do safe memory/storage cleanups
                !module.Id.Contains("Timer") && !module.Id.Contains("PowerPlan") && module.RiskLevel == RiskLevel.Safe,

            OptimizationProfile.Custom =>
                module.IsOptimized,

            _ => module.RiskLevel == RiskLevel.Safe
        };
    }

    public static async Task<bool> ExportProfileAsync(string filePath, OptimizationProfile profile, List<IOptimizationModule> modules, HardwareInfo hw)
    {
        try
        {
            var model = new ProfileExportModel
            {
                ProfileName = profile.ToString(),
                TargetHardwareTier = hw.CalculatedTier.ToString(),
                SelectedModuleIds = modules.Where(m => m.IsOptimized).Select(m => m.Id).ToList(),
                RecommendedCpuCores = hw.LogicalProcessors >= 8 ? 4 : 2,
                RecommendedRamMb = hw.TotalRamGb >= 16 ? 8192 : 4096,
                RecommendedResWidth = hw.ScreenWidth,
                RecommendedResHeight = hw.ScreenHeight,
                RecommendedFpsLevel = hw.RefreshRateHz >= 90 ? 120 : 60,
                Notes = $"Exported profile for {hw.CpuName} and {hw.GpuName}"
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            Logger.Success("OptimizationProfileManager", $"Profile exported successfully to {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("OptimizationProfileManager", $"Failed to export profile: {ex.Message}");
            return false;
        }
    }

    public static async Task<ProfileExportModel?> ImportProfileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var json = await File.ReadAllTextAsync(filePath);
            var model = JsonSerializer.Deserialize<ProfileExportModel>(json);
            Logger.Success("OptimizationProfileManager", $"Profile imported successfully from {filePath}");
            return model;
        }
        catch (Exception ex)
        {
            Logger.Error("OptimizationProfileManager", $"Failed to import profile: {ex.Message}");
            return null;
        }
    }
}
