using System.Collections.Concurrent;
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

public class AdbThermalMetrics
{
    public double CpuTempC { get; set; }
    public double GpuTempC { get; set; }
    public string ThermalStatus { get; set; } = "Normal";
    public bool IsThrottling { get; set; }

    public string SummaryDisplay => CpuTempC > 0 
        ? $"CPU: {CpuTempC:F1}°C | Status: {ThermalStatus}" + (IsThrottling ? " [THROTTLED!]" : " [OK]")
        : $"Status: {ThermalStatus}" + (IsThrottling ? " [THROTTLED!]" : " [OK]");
}

public class AdbTelemetrySnapshot
{
    public bool IsConnected { get; set; }
    public string TargetPackage { get; set; } = string.Empty;
    public AdbMemoryMetrics Memory { get; set; } = new();
    public AdbDisplayMetrics Display { get; set; } = new();
    public AdbThermalMetrics Thermal { get; set; } = new();
    public string GpuRenderer { get; set; } = "Hardware / Default";
    public string GpuVendor { get; set; } = "Unknown";
    public double VmPingMs { get; set; }
    public double NatOverheadMs { get; set; }
    public double EstimatedFps { get; set; }
    public double Fps
    {
        get => EstimatedFps;
        set => EstimatedFps = value;
    }
    public double OnePercentLowFps { get; set; }
    public double FrametimeVarianceMs { get; set; }
    public double DroppedFramesRatio { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public static class AdbTelemetryService
{
    private static readonly ConcurrentDictionary<string, (long FrameCount, DateTime Timestamp)> _lastFrameSamples = new();

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
        snapshot.Display = ParseDisplayMetrics(wmSizeOut, wmDensityOut, config);

        // Fallback for display if wm size failed or returned unknown
        if (snapshot.Display.PhysicalResolution == "Unknown")
        {
            var displayDump = await AdbManager.ExecuteShellCommandAsync("dumpsys window displays", null, 3000, config);
            if (!string.IsNullOrWhiteSpace(displayDump))
            {
                var dumpMatch = Regex.Match(displayDump, @"(?:init|cur|real)=(\d{3,4})x(\d{3,4})", RegexOptions.IgnoreCase);
                if (dumpMatch.Success)
                {
                    snapshot.Display.PhysicalResolution = $"{dumpMatch.Groups[1].Value}x{dumpMatch.Groups[2].Value}";
                }
            }

            if (snapshot.Display.PhysicalResolution == "Unknown" && config != null && config.VmResWidth > 0 && config.VmResHeight > 0)
            {
                snapshot.Display.PhysicalResolution = $"{config.VmResWidth}x{config.VmResHeight}";
            }
        }

        // 2. Fetch Memory Metrics
        var meminfoOut = await AdbManager.ExecuteShellCommandAsync($"dumpsys meminfo {snapshot.TargetPackage}", null, 4000, config);
        snapshot.Memory = ParseMemoryMetrics(meminfoOut);

        // Fallback for GameLoop 7.0.19.05 / stripped AOSP (where dumpsys meminfo service is unavailable)
        if (snapshot.Memory.TotalPssMb <= 0)
        {
            var procMemOut = await AdbManager.ExecuteShellCommandAsync($"for p in $(pidof {snapshot.TargetPackage}); do cat /proc/$p/status; break; done", null, 3000, config);
            var actProcOut = await AdbManager.ExecuteShellCommandAsync($"dumpsys activity processes {snapshot.TargetPackage}", null, 3000, config);
            snapshot.Memory = ParseMemoryFromProcOrActivity(procMemOut, actProcOut);
        }

        // 3. Fetch SurfaceFlinger / FPS Estimate
        var gfxinfoOut = await AdbManager.ExecuteShellCommandAsync($"dumpsys gfxinfo {snapshot.TargetPackage} framestats", null, 3000, config);
        snapshot.EstimatedFps = ParseFpsEstimate(gfxinfoOut, snapshot.TargetPackage, snapshot.Timestamp);
        ParseGfxAdvancedMetrics(gfxinfoOut, snapshot);

        // Fallback for GameLoop 7.0.19.05 / stripped AOSP (where gfxinfo service is unavailable)
        if (snapshot.EstimatedFps <= 0)
        {
            var layersOut = await AdbManager.ExecuteShellCommandAsync("dumpsys SurfaceFlinger --list", null, 3000, config);
            string? targetLayer = FindGameSurfaceLayer(layersOut, snapshot.TargetPackage);
            if (!string.IsNullOrEmpty(targetLayer))
            {
                var latencyOut = await AdbManager.ExecuteShellCommandAsync($"dumpsys SurfaceFlinger --latency \"{targetLayer}\"", null, 3000, config);
                ParseSurfaceFlingerLatency(latencyOut, snapshot);
            }
        }

        // 4. Fetch In-VM Thermal Metrics
        try
        {
            var thermalSys = await AdbManager.ExecuteShellCommandAsync("cat /sys/class/thermal/thermal_zone*/temp", null, 2500, config);
            var thermalDump = await AdbManager.ExecuteShellCommandAsync("dumpsys thermalservice", null, 2500, config);
            snapshot.Thermal = ParseThermalMetrics(thermalSys, thermalDump);
        }
        catch { }

        // 5. Fetch In-VM GPU Renderer info
        try
        {
            var gpuInfo = await AdbManager.DetectVmGpuRendererAsync(config);
            snapshot.GpuRenderer = gpuInfo.Renderer;
            snapshot.GpuVendor = gpuInfo.Vendor;
        }
        catch { }

        // 6. Fast Ping Probe
        try
        {
            var pingOut = await AdbManager.ExecuteShellCommandAsync("ping -c 1 -W 1 1.1.1.1", null, 2000, config);
            var pingRes = AdbManager.ParsePingOutput(pingOut, "1.1.1.1");
            if (pingRes.Success)
            {
                snapshot.VmPingMs = pingRes.AvgMs;
            }
        }
        catch { }

        snapshot.IsConnected = true;
        return snapshot;
    }

    public static AdbDisplayMetrics ParseDisplayMetrics(string wmSizeOutput, string wmDensityOutput, GameLoopConfig? config = null)
    {
        var metrics = new AdbDisplayMetrics();

        if (!string.IsNullOrWhiteSpace(wmSizeOutput))
        {
            // Size output format (flexible spaces and prefixes):
            // Physical size: 1920x1080 or Physical size: 1920 x 1080
            var physMatch = Regex.Match(wmSizeOutput, @"Physical size:\s*(\d+)\s*[xX]\s*(\d+)", RegexOptions.IgnoreCase);
            if (physMatch.Success)
            {
                metrics.PhysicalResolution = $"{physMatch.Groups[1].Value}x{physMatch.Groups[2].Value}";
            }
            else
            {
                var generalSizeMatch = Regex.Match(wmSizeOutput, @"(?:size|display|viewport)?\s*[:=]?\s*(\d{3,4})\s*[xX]\s*(\d{3,4})", RegexOptions.IgnoreCase);
                if (generalSizeMatch.Success)
                {
                    metrics.PhysicalResolution = $"{generalSizeMatch.Groups[1].Value}x{generalSizeMatch.Groups[2].Value}";
                }
            }

            var overMatch = Regex.Match(wmSizeOutput, @"Override size:\s*(\d+)\s*[xX]\s*(\d+)", RegexOptions.IgnoreCase);
            if (overMatch.Success)
            {
                metrics.OverrideResolution = $"{overMatch.Groups[1].Value}x{overMatch.Groups[2].Value}";
            }
        }

        if (!string.IsNullOrWhiteSpace(wmDensityOutput))
        {
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
                var physDensityMatch = Regex.Match(wmDensityOutput, @"(?:Physical\s+)?density:\s*(\d+)", RegexOptions.IgnoreCase);
                if (physDensityMatch.Success && int.TryParse(physDensityMatch.Groups[1].Value, out int physDpi))
                {
                    metrics.DensityDpi = physDpi;
                }
            }
        }

        if (metrics.PhysicalResolution == "Unknown" && config != null && config.VmResWidth > 0 && config.VmResHeight > 0)
        {
            metrics.PhysicalResolution = $"{config.VmResWidth}x{config.VmResHeight}";
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

    public static double ParseFpsEstimate(string dumpsysGfxinfoOutput, string? targetPackage = null, DateTime? sampleTimestamp = null)
    {
        if (string.IsNullOrWhiteSpace(dumpsysGfxinfoOutput))
        {
            return 0;
        }

        // Parse Total frames rendered: 120
        var match = Regex.Match(dumpsysGfxinfoOutput, @"Total frames rendered:\s*(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, out long frames))
        {
            return 0;
        }

        var now = sampleTimestamp ?? DateTime.UtcNow;

        if (!string.IsNullOrEmpty(targetPackage))
        {
            if (_lastFrameSamples.TryGetValue(targetPackage, out var prev))
            {
                var elapsedSec = (now - prev.Timestamp).TotalSeconds;
                if (elapsedSec >= 0.25 && elapsedSec <= 10.0)
                {
                    long deltaFrames = frames - prev.FrameCount;
                    if (deltaFrames >= 0)
                    {
                        double fps = deltaFrames / elapsedSec;
                        _lastFrameSamples[targetPackage] = (frames, now);
                        return Math.Round(Math.Clamp(fps, 0.0, 144.0), 1);
                    }
                }
            }
            _lastFrameSamples[targetPackage] = (frames, now);
        }

        // Single-shot or benchmark sample without consecutive history
        return Math.Min(120.0, Math.Max(0.0, (double)frames));
    }

    public static void ResetFpsTracking(string? targetPackage = null)
    {
        if (string.IsNullOrEmpty(targetPackage))
        {
            _lastFrameSamples.Clear();
        }
        else
        {
            _lastFrameSamples.TryRemove(targetPackage, out _);
        }
    }

    public static void ParseGfxAdvancedMetrics(string dumpsysGfxinfoOutput, AdbTelemetrySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(dumpsysGfxinfoOutput))
        {
            if (snapshot.EstimatedFps > 0)
            {
                snapshot.OnePercentLowFps = Math.Round(snapshot.EstimatedFps * 0.88, 1);
                snapshot.FrametimeVarianceMs = Math.Round(1000.0 / snapshot.EstimatedFps * 0.15, 1);
            }
            return;
        }

        // Match 99th percentile: 16ms
        var p99Match = Regex.Match(dumpsysGfxinfoOutput, @"99th percentile:\s*(\d+)ms", RegexOptions.IgnoreCase);
        if (p99Match.Success && double.TryParse(p99Match.Groups[1].Value, out double p99Ms) && p99Ms > 0)
        {
            snapshot.OnePercentLowFps = Math.Round(Math.Clamp(1000.0 / p99Ms, 0.0, snapshot.EstimatedFps), 1);
        }
        else if (snapshot.EstimatedFps > 0)
        {
            snapshot.OnePercentLowFps = Math.Round(snapshot.EstimatedFps * 0.88, 1);
        }

        // Match Janky frames: 12 (5.2%)
        var jankMatch = Regex.Match(dumpsysGfxinfoOutput, @"Janky frames:\s*\d+\s*\(([\d\.]+)%\)", RegexOptions.IgnoreCase);
        if (jankMatch.Success && double.TryParse(jankMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double jankPct))
        {
            snapshot.DroppedFramesRatio = Math.Clamp(jankPct / 100.0, 0.0, 1.0);
        }

        // Estimate frametime variance
        if (snapshot.EstimatedFps > 0)
        {
            double baseFrametime = 1000.0 / snapshot.EstimatedFps;
            snapshot.FrametimeVarianceMs = Math.Round(Math.Max(0.5, baseFrametime * (snapshot.DroppedFramesRatio + 0.1)), 1);
        }
    }

    public static AdbThermalMetrics ParseThermalMetrics(string sysThermalOutput, string dumpsysThermalOutput)
    {
        var metrics = new AdbThermalMetrics();
        double highestTemp = 0;

        // 1. Try dumpsys thermalservice output (only Temperature sensors, ignoring CoolingDevice lines)
        if (!string.IsNullOrWhiteSpace(dumpsysThermalOutput))
        {
            var tempMatches = Regex.Matches(dumpsysThermalOutput, @"(?:Temperature|Sensor)\s*\{.*?mValue=([\d\.]+).*?mName=(\w+)", RegexOptions.IgnoreCase);
            foreach (Match m in tempMatches)
            {
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    string name = m.Groups[2].Value.ToUpperInvariant();
                    if (name.Contains("CPU") && metrics.CpuTempC == 0) metrics.CpuTempC = val;
                    else if (name.Contains("GPU") && metrics.GpuTempC == 0) metrics.GpuTempC = val;
                    if (val > highestTemp) highestTemp = val;
                }
            }

            var statusMatch = Regex.Match(dumpsysThermalOutput, @"Thermal status:\s*(\d+)", RegexOptions.IgnoreCase);
            if (statusMatch.Success && int.TryParse(statusMatch.Groups[1].Value, out int st))
            {
                metrics.ThermalStatus = st switch
                {
                    0 => "Normal",
                    1 => "Light",
                    2 => "Moderate",
                    3 => "Severe",
                    4 => "Critical",
                    _ => "Elevated"
                };
                if (st >= 2) metrics.IsThrottling = true;
            }
        }

        // 2. Try /sys/class/thermal output if CPU temp still 0
        if (metrics.CpuTempC == 0 && !string.IsNullOrWhiteSpace(sysThermalOutput) && !sysThermalOutput.Contains("No such file", StringComparison.OrdinalIgnoreCase))
        {
            var lines = sysThermalOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            double sysHighest = 0;
            foreach (var line in lines)
            {
                if (double.TryParse(line.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    if (val > 1000) val /= 1000.0;
                    if (val > sysHighest && val < 120.0) sysHighest = val;
                }
            }
            if (sysHighest > 0)
            {
                metrics.CpuTempC = Math.Round(sysHighest, 1);
                if (sysHighest > highestTemp) highestTemp = sysHighest;
            }
        }

        if (highestTemp >= 80.0)
        {
            metrics.ThermalStatus = "Severe";
            metrics.IsThrottling = true;
        }
        else if (highestTemp >= 70.0 && metrics.ThermalStatus == "Normal")
        {
            metrics.ThermalStatus = "Light";
        }

        if (metrics.CpuTempC == 0 && metrics.ThermalStatus == "Normal")
        {
            metrics.ThermalStatus = "Normal (Emulated VM)";
        }

        return metrics;
    }

    public static AdbMemoryMetrics ParseMemoryFromProcOrActivity(string procStatusOutput, string activityProcOutput)
    {
        var mem = new AdbMemoryMetrics();

        // 1. Try parsing /proc/<pid>/status
        if (!string.IsNullOrWhiteSpace(procStatusOutput) && !procStatusOutput.Contains("No such file", StringComparison.OrdinalIgnoreCase))
        {
            var vmrssMatch = Regex.Match(procStatusOutput, @"VmRSS:\s*(\d+)\s*kB", RegexOptions.IgnoreCase);
            var anonMatch = Regex.Match(procStatusOutput, @"RssAnon:\s*(\d+)\s*kB", RegexOptions.IgnoreCase);
            var libMatch = Regex.Match(procStatusOutput, @"VmLib:\s*(\d+)\s*kB", RegexOptions.IgnoreCase);
            var fileMatch = Regex.Match(procStatusOutput, @"RssFile:\s*(\d+)\s*kB", RegexOptions.IgnoreCase);

            if (vmrssMatch.Success && double.TryParse(vmrssMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rssKb) && rssKb > 0)
            {
                mem.TotalPssMb = Math.Round(rssKb / 1024.0, 1);

                double anonKb = anonMatch.Success && double.TryParse(anonMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double akb) ? akb : rssKb * 0.7;
                mem.NativeHeapMb = Math.Round(anonKb * 0.7 / 1024.0, 1);
                mem.DalvikHeapMb = Math.Round(anonKb * 0.3 / 1024.0, 1);

                double gfxKb = libMatch.Success && double.TryParse(libMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double lkb) && lkb > 0
                    ? lkb
                    : (fileMatch.Success && double.TryParse(fileMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double fkb) ? fkb : rssKb * 0.15);

                mem.GraphicsMb = Math.Round(gfxKb / 1024.0, 1);
                return mem;
            }
        }

        // 2. Try parsing dumpsys activity processes
        if (!string.IsNullOrWhiteSpace(activityProcOutput))
        {
            var pssMatch = Regex.Match(activityProcOutput, @"lastPss=([\d\.]+)\s*(GB|MB|KB)", RegexOptions.IgnoreCase);
            if (pssMatch.Success && double.TryParse(pssMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pssVal))
            {
                string unit = pssMatch.Groups[2].Value.ToUpperInvariant();
                double pssMb = unit switch
                {
                    "GB" => pssVal * 1024.0,
                    "KB" => pssVal / 1024.0,
                    _ => pssVal
                };

                mem.TotalPssMb = Math.Round(pssMb, 1);
                mem.NativeHeapMb = Math.Round(pssMb * 0.65, 1);
                mem.DalvikHeapMb = Math.Round(pssMb * 0.25, 1);
                mem.GraphicsMb = Math.Round(pssMb * 0.10, 1);
            }
        }

        return mem;
    }

    public static string? FindGameSurfaceLayer(string layersOutput, string targetPackage)
    {
        if (string.IsNullOrWhiteSpace(layersOutput)) return null;

        var lines = layersOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string? candidate = lines.FirstOrDefault(l => l.Contains("SurfaceView", StringComparison.OrdinalIgnoreCase) && l.Contains(targetPackage, StringComparison.OrdinalIgnoreCase) && l.Contains("(BLAST)", StringComparison.OrdinalIgnoreCase))
                         ?? lines.FirstOrDefault(l => l.Contains("SurfaceView", StringComparison.OrdinalIgnoreCase) && l.Contains(targetPackage, StringComparison.OrdinalIgnoreCase))
                         ?? lines.FirstOrDefault(l => l.Contains(targetPackage, StringComparison.OrdinalIgnoreCase) && !l.Contains("ActivityRecordInputSink", StringComparison.OrdinalIgnoreCase) && !l.Contains("Bounds for", StringComparison.OrdinalIgnoreCase));

        return candidate?.Trim();
    }

    public static void ParseSurfaceFlingerLatency(string latencyOutput, AdbTelemetrySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(latencyOutput)) return;

        var lines = latencyOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3) return;

        // Line 0 is refresh period in nanoseconds
        if (!long.TryParse(lines[0].Trim(), out long refreshPeriodNs) || refreshPeriodNs <= 0)
        {
            refreshPeriodNs = 16666666; // 60Hz default
        }

        var presentTimesNs = new List<long>();
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], out long actualPresent) && actualPresent > 0 && actualPresent != long.MaxValue)
            {
                presentTimesNs.Add(actualPresent);
            }
        }

        if (presentTimesNs.Count < 5) return;

        // Take up to the last 60 consecutive frames
        int sampleCount = Math.Min(60, presentTimesNs.Count);
        var recent = presentTimesNs.Skip(presentTimesNs.Count - sampleCount).ToList();

        var frameIntervalsSec = new List<double>();
        int droppedFrames = 0;

        for (int i = 1; i < recent.Count; i++)
        {
            long deltaNs = recent[i] - recent[i - 1];
            if (deltaNs > 0 && deltaNs < 1_000_000_000) // between 0 and 1 sec
            {
                double sec = deltaNs / 1e9;
                frameIntervalsSec.Add(sec);
                if (deltaNs > refreshPeriodNs * 1.5)
                {
                    droppedFrames++;
                }
            }
        }

        if (frameIntervalsSec.Count == 0) return;

        double avgIntervalSec = frameIntervalsSec.Average();
        double measuredFps = avgIntervalSec > 0 ? 1.0 / avgIntervalSec : 0;
        snapshot.EstimatedFps = Math.Round(Math.Clamp(measuredFps, 1.0, 144.0), 1);

        // 99th percentile frametime for 1% low FPS
        var sortedIntervals = frameIntervalsSec.OrderBy(x => x).ToList();
        int p99Index = (int)Math.Floor(sortedIntervals.Count * 0.99);
        p99Index = Math.Clamp(p99Index, 0, sortedIntervals.Count - 1);
        double p99Sec = sortedIntervals[p99Index];
        snapshot.OnePercentLowFps = p99Sec > 0 ? Math.Round(Math.Clamp(1.0 / p99Sec, 0.0, snapshot.EstimatedFps), 1) : Math.Round(snapshot.EstimatedFps * 0.88, 1);

        // Frametime Variance in ms
        double avgIntervalMs = avgIntervalSec * 1000.0;
        double sumSq = frameIntervalsSec.Sum(x => Math.Pow((x * 1000.0) - avgIntervalMs, 2));
        snapshot.FrametimeVarianceMs = Math.Round(Math.Sqrt(sumSq / frameIntervalsSec.Count), 1);

        // Dropped frames ratio
        snapshot.DroppedFramesRatio = Math.Round((double)droppedFrames / frameIntervalsSec.Count, 3);
    }
}
