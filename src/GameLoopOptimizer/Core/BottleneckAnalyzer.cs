using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public static class BottleneckAnalyzer
{
    public static BottleneckAnalysisResult Analyze(
        PerformanceMetrics metrics,
        HardwareInfo hw,
        SystemInfo sys,
        GameLoopConfig gl)
    {
        var result = new BottleneckAnalysisResult();

        // 1. Thermal Bottleneck Check
        double cpuTemp = metrics.CpuTemperatureC ?? hw.CpuTemperatureC ?? 0;
        double gpuTemp = metrics.GpuTemperatureC ?? hw.GpuTemperatureC ?? 0;

        if (cpuTemp >= 88 || gpuTemp >= 86)
        {
            result.PrimaryBottleneck = BottleneckType.ThermalBottleneck;
            result.ConfidencePercent = 90;
            result.Headline = "Thermal Throttling Detected";
            result.Explanation = $"Hardware thermal sensors indicate elevated temperatures (CPU: {cpuTemp:F0}°C, GPU: {gpuTemp:F0}°C). " +
                                  "The CPU/GPU frequency is likely throttling down to protect the silicon, inducing intermittent frame drops and stutter.";
            result.RecommendedActions = new List<string>
            {
                "Inspect laptop air vents / PC chassis fans for dust build-up",
                "Ensure device is placed on an elevated, well-ventilated surface",
                "Cap FPS to match monitor refresh rate to reduce continuous thermal load",
                "Use Balanced power profile rather than forcing aggressive unthrottled clocks"
            };
            result.ActionsToAvoid = new List<string>
            {
                "Do NOT force Ultimate Performance power plans that disable CPU thermal idle states",
                "Do NOT overclock CPU or GPU under high thermal load"
            };
            return result;
        }

        // 2. RAM Saturation Bottleneck Check
        if (metrics.RamPercent >= 88 || hw.AvailableRamGb < 1.8)
        {
            result.PrimaryBottleneck = BottleneckType.RamBottleneck;
            result.ConfidencePercent = 88;
            result.Headline = "System Memory Saturation Detected";
            result.Explanation = $"Host RAM usage is currently at {metrics.RamPercent:F0}% (Available: {hw.AvailableRamGb:F1} GB). " +
                                  "When memory headroom drops below threshold, Windows pages inactive working sets to disk, producing severe frame-time spikes during asset streaming.";
            result.RecommendedActions = new List<string>
            {
                "Purge Windows Standby List memory cache",
                "Close background memory-heavy applications (browsers, Discord, recording suites)",
                $"Set GameLoop RAM allocation to {Math.Min(4096, (int)(hw.TotalRamGb * 512))} MB to ensure host OS stability",
                hw.IsDualChannel ? "Maintain current memory channel" : "Upgrade to dual-channel RAM configuration for double memory bandwidth"
            };
            result.ActionsToAvoid = new List<string>
            {
                "Do NOT allocate all available RAM to GameLoop (host OS starvation triggers paging)",
                "Do NOT disable Windows pagefile completely"
            };
            return result;
        }

        // 3. VRAM Saturation Bottleneck Check
        if (hw.DedicatedVramMb > 0 && metrics.GpuVramUsedMb > 0)
        {
            double vramPercent = (metrics.GpuVramUsedMb / hw.DedicatedVramMb) * 100.0;
            if (vramPercent >= 90)
            {
                result.PrimaryBottleneck = BottleneckType.VramBottleneck;
                result.ConfidencePercent = 85;
                result.Headline = "GPU VRAM Capacity Bottleneck";
                result.Explanation = $"Dedicated Video RAM is {vramPercent:F0}% saturated ({metrics.GpuVramUsedMb:F0} MB / {hw.DedicatedVramMb:F0} MB). " +
                                      "Asset textures exceeding VRAM spill into shared system memory across PCIe, causing drastic stutter during rapid rotation.";
                result.RecommendedActions = new List<string>
                {
                    "Lower GameLoop rendering resolution (e.g., 2K/1440p down to 1080p)",
                    "Lower PUBG Mobile graphics preset to 'Smooth'",
                    "Clear DirectX and OpenGL driver shader cache",
                    "Disable Anti-Aliasing (FXAA)"
                };
                result.ActionsToAvoid = new List<string>
                {
                    "Do NOT increase emulator rendering resolution to 2560x1440 or higher",
                    "Do NOT use Ultra HD graphics quality on GPUs with <= 4GB VRAM"
                };
                return result;
            }
        }

        // 4. GPU-Bound Bottleneck Check
        if (metrics.GpuPercent >= 92 && metrics.CpuTotalPercent < 70)
        {
            result.PrimaryBottleneck = BottleneckType.GpuBottleneck;
            result.ConfidencePercent = 85;
            result.Headline = "GPU Fill-Rate / Shading Bottleneck";
            result.Explanation = $"GPU utilization is consistently high ({metrics.GpuPercent:F0}%) while CPU utilization remains moderate ({metrics.CpuTotalPercent:F0}%). " +
                                  "The graphics hardware is operating at peak shader capacity and limits maximum rendering throughput.";
            result.RecommendedActions = new List<string>
            {
                "Lower GameLoop display resolution (e.g. from 1080p to 720p or use stretched resolution)",
                "Set PUBG Mobile graphics to 'Smooth' for maximum frame-rate stability",
                "Disable Anti-Aliasing in GameLoop and in PUBG settings",
                "Set GameLoop renderer to DirectX+ for native D3D acceleration on dedicated GPUs"
            };
            result.ActionsToAvoid = new List<string>
            {
                "Do NOT allocate additional CPU cores to GameLoop (will not alleviate GPU saturation)",
                "Do NOT raise render quality to HD/HDR"
            };
            return result;
        }

        // 5. CPU-Bound Bottleneck Check
        if (metrics.CpuTotalPercent >= 80 && metrics.GpuPercent < 65)
        {
            result.PrimaryBottleneck = BottleneckType.CpuBottleneck;
            result.ConfidencePercent = 82;
            result.Headline = "CPU Scheduling / Thread Contention Bottleneck";
            result.Explanation = $"Total CPU utilization is heavily loaded ({metrics.CpuTotalPercent:F0}%) while GPU utilization remains low ({metrics.GpuPercent:F0}%). " +
                                  "The Android virtualization subsystem or main emulator render dispatch thread is CPU thread starved.";
            result.RecommendedActions = new List<string>
            {
                "Configure GameLoop CPU allocation to 4 cores (the sweet spot that avoids Android lock contention)",
                "Enable Windows Game Mode to prioritize CPU scheduling for the emulator process",
                "Set GameLoop process priority to Above Normal",
                "Terminate competing high-CPU background processes",
                "Enable MMCSS Game Scheduling priority in Windows registry"
            };
            result.ActionsToAvoid = new List<string>
            {
                "Do NOT allocate all physical/logical cores to GameLoop (causes scheduler thrashing)",
                "Do NOT set Realtime process priority (causes OS deadlock and input freezing)"
            };
            return result;
        }

        // 6. Emulator Overhead Bottleneck Check
        if (metrics.IsGameLoopActive && metrics.GameLoopCpuPercent >= 50 && metrics.GpuPercent < 50 && metrics.AvgFps < 55)
        {
            result.PrimaryBottleneck = BottleneckType.EmulatorOverheadBottleneck;
            result.ConfidencePercent = 80;
            result.Headline = "Emulator Translation Layer Overhead";
            result.Explanation = $"GameLoop emulator process consumes high CPU ({metrics.GameLoopCpuPercent:F0}%) while GPU remains underutilized ({metrics.GpuPercent:F0}%). " +
                                  "This indicates driver translation overhead in the OpenGL/DirectX rendering bridge.";
            result.RecommendedActions = new List<string>
            {
                hw.GpuVendor == GpuVendor.Nvidia ? "Switch renderer to DirectX+ (optimized for NVIDIA D3D pipeline)" : "Switch renderer to OpenGL+ for reduced driver API conversion overhead",
                "Enable Local Shader Cache to eliminate JIT shader compilation freezes",
                "Perform DEX compilation optimization on the PUBG APK via ADB",
                "Set VM Heap size to 512MB to eliminate garbage collection pauses"
            };
            result.ActionsToAvoid = new List<string>
            {
                "Do NOT disable shader cache in GameLoop engine settings"
            };
            return result;
        }

        // 7. Storage Bottleneck Check
        if (hw.PrimaryDriveType == StorageType.Hdd || metrics.DiskActivePercent > 80)
        {
            result.PrimaryBottleneck = BottleneckType.StorageBottleneck;
            result.ConfidencePercent = 75;
            result.Headline = "Storage I/O Bottleneck";
            result.Explanation = "GameLoop or Windows is operating on a mechanical Hard Disk Drive (HDD). " +
                                  "Slow random 4K read/write speeds produce noticeable micro-stutter when PUBG streams building assets and textures during fast movement.";
            result.RecommendedActions = new List<string>
            {
                "Migrate GameLoop installation and Temp folder to an SSD or NVMe drive",
                "Clean temporary files and shader cache regularly to prevent disk fragmentation",
                "Pause disk-heavy background downloads and Windows Search indexing during gameplay"
            };
            result.ActionsToAvoid = new List<string>
            {
                "Do NOT run heavy disk defragmentation while GameLoop is active"
            };
            return result;
        }

        // 8. Balanced Performance
        result.PrimaryBottleneck = BottleneckType.None;
        result.ConfidencePercent = 85;
        result.Headline = "Balanced System Performance";
        result.Explanation = "CPU, GPU, RAM, and storage resources are functioning within harmonious parameters. " +
                              "Frame pacing is consistent with no dominant hardware bottleneck limiting emulator execution.";
        result.RecommendedActions = new List<string>
        {
            "Apply Competitive FPS profile for minimal input latency and stable timer resolution",
            "Maintain clean background process environment",
            "Keep GPU drivers up to date"
        };
        result.ActionsToAvoid = new List<string>
        {
            "Do NOT apply random unmeasured Windows registry tweaks"
        };

        return result;
    }
}
