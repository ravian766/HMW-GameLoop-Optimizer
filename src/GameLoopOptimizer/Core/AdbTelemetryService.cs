using System.Globalization;
using System.Text.RegularExpressions;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class AdbMemoryMetrics
{
    public double NativeHeapMb { get; set; }
    public double DalvikHeapMb { get; set; }
    public double GraphicsMb { get; set; }
    public double TotalPssMb { get; set; }

    public string SummaryDisplay => TotalPssMb > 0 
        ? $"Total: {TotalPssMb:F1} MB (Native: {NativeHeapMb:F1}MB | Dalvik: {DalvikHeapMb:F1}MB | GFX: {GraphicsMb:F1}MB)"
        : "No active game process memory detected";
}

public class AdbDisplayMetrics
{
    public string PhysicalResolution { get; set; } = "Unknown";
    public string OverrideResolution { get; set; } = "None";
    public int DensityDpi { get; set; } = 320;
    public string EffectiveResolution => !string.IsNullOrEmpty(OverrideResolution) && OverrideResolution != "None" 
        ? OverrideResolution 
        : PhysicalResolution;
}

public class AdbTelemetrySnapshot
{
    public bool IsConnected { get; set; }
    public string TargetPackage { get; set; } = string.Empty;
    public AdbMemoryMetrics Memory { get; set; } = new();
    public AdbDisplayMetrics Display { get; set; } = new();
    public double EstimatedFps { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public static class AdbTelemetryService
{
    public static async Task<AdbTelemetrySnapshot> FetchTelemetryAsync(string? targetPackage = null, GameLoopConfig? config = null)
    {
        var snapshot = new AdbTelemetrySnapshot
        {
            TargetPackage = string.IsNullOrEmpty(targetPackage) ? "com.tencent.ig" : targetPackage,
            Timestamp = DateTime.Now
        };

        if (!AdbManager.IsAdbAvailable(config))
        {
            snapshot.IsConnected = false;
            return snapshot;
        }

        // 1. Fetch Display Metrics
        var wmSizeOut = await AdbManager.ExecuteShellCommandAsync("wm size", null, 3000, config);
        var wmDensityOut = await AdbManager.ExecuteShellCommandAsync("wm density", null, 3000, config);
        snapshot.Display = ParseDisplayMetrics(wmSizeOut, wmDensityOut);

        // 2. Fetch Memory Metrics
        var meminfoOut = await AdbManager.ExecuteShellCommandAsync($"dumpsys meminfo {snapshot.TargetPackage}", null, 4000, config);
        snapshot.Memory = ParseMemoryMetrics(meminfoOut);

        // 3. Fetch SurfaceFlinger / FPS Estimate
        var gfxinfoOut = await AdbManager.ExecuteShellCommandAsync($"dumpsys gfxinfo {snapshot.TargetPackage} framestats", null, 3000, config);
        snapshot.EstimatedFps = ParseFpsEstimate(gfxinfoOut);

        snapshot.IsConnected = true;
        return snapshot;
    }

    public static AdbDisplayMetrics ParseDisplayMetrics(string wmSizeOutput, string wmDensityOutput)
    {
        var metrics = new AdbDisplayMetrics();

        // Size output format:
        // Physical size: 1920x1080
        // Override size: 1440x1080
        var physMatch = Regex.Match(wmSizeOutput, @"Physical size:\s*(\d+x\d+)", RegexOptions.IgnoreCase);
        if (physMatch.Success)
        {
            metrics.PhysicalResolution = physMatch.Groups[1].Value;
        }

        var overMatch = Regex.Match(wmSizeOutput, @"Override size:\s*(\d+x\d+)", RegexOptions.IgnoreCase);
        if (overMatch.Success)
        {
            metrics.OverrideResolution = overMatch.Groups[1].Value;
        }

        // Density output format:
        // Physical density: 320
        // Override density: 240
        var overDensityMatch = Regex.Match(wmDensityOutput, @"Override density:\s*(\d+)", RegexOptions.IgnoreCase);
        if (overDensityMatch.Success && int.TryParse(overDensityMatch.Groups[1].Value, out int overDpi))
        {
            metrics.DensityDpi = overDpi;
        }
        else
        {
            var physDensityMatch = Regex.Match(wmDensityOutput, @"Physical density:\s*(\d+)", RegexOptions.IgnoreCase);
            if (physDensityMatch.Success && int.TryParse(physDensityMatch.Groups[1].Value, out int physDpi))
            {
                metrics.DensityDpi = physDpi;
            }
        }

        return metrics;
    }

    public static AdbMemoryMetrics ParseMemoryMetrics(string dumpsysMeminfoOutput)
    {
        var mem = new AdbMemoryMetrics();
        if (string.IsNullOrWhiteSpace(dumpsysMeminfoOutput) || dumpsysMeminfoOutput.Contains("No process found", StringComparison.OrdinalIgnoreCase))
        {
            return mem;
        }

        // Search for Total PSS: "TOTAL: 512340", "TOTAL PSS: 512340", or "TOTAL   512340"
        var totalMatch = Regex.Match(dumpsysMeminfoOutput, @"TOTAL(?:\s+PSS)?(?::|\s+)\s*(\d+)", RegexOptions.IgnoreCase);
        if (totalMatch.Success && double.TryParse(totalMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double totalKb))
        {
            mem.TotalPssMb = totalKb / 1024.0;
        }

        // Search for Native Heap: "Native Heap    123456"
        var nativeMatch = Regex.Match(dumpsysMeminfoOutput, @"Native Heap\s+(\d+)", RegexOptions.IgnoreCase);
        if (nativeMatch.Success && double.TryParse(nativeMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double natKb))
        {
            mem.NativeHeapMb = natKb / 1024.0;
        }

        // Search for Dalvik Heap: "Dalvik Heap    65432"
        var dalvikMatch = Regex.Match(dumpsysMeminfoOutput, @"Dalvik Heap\s+(\d+)", RegexOptions.IgnoreCase);
        if (dalvikMatch.Success && double.TryParse(dalvikMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double dalKb))
        {
            mem.DalvikHeapMb = dalKb / 1024.0;
        }

        // Search for Graphics: "Graphics    45678" or "EGL mtrack    12345"
        var gfxMatch = Regex.Match(dumpsysMeminfoOutput, @"(?:Graphics|EGL mtrack|GL mtrack)\s+(\d+)", RegexOptions.IgnoreCase);
        if (gfxMatch.Success && double.TryParse(gfxMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double gfxKb))
        {
            mem.GraphicsMb = gfxKb / 1024.0;
        }

        return mem;
    }

    public static double ParseFpsEstimate(string dumpsysGfxinfoOutput)
    {
        if (string.IsNullOrWhiteSpace(dumpsysGfxinfoOutput))
        {
            return 0;
        }

        // Parse Total frames rendered: 120
        var match = Regex.Match(dumpsysGfxinfoOutput, @"Total frames rendered:\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int frames))
        {
            // Sample rate normalization or direct count indicator
            return Math.Min(120.0, Math.Max(0.0, frames % 121));
        }

        return 0;
    }
}
