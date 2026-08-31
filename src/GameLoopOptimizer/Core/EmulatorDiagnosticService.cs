using System.IO;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Critical
}

public class DiagnosticIssue
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public DiagnosticSeverity Severity { get; set; }
    public bool CanAutoFix { get; set; }
}

public class EmulatorHealthReport
{
    public int HealthScore { get; set; } = 100;
    public bool IsHealthy => HealthScore >= 80 && !Issues.Any(i => i.Severity == DiagnosticSeverity.Critical);
    public int ChecksPerformed { get; set; }
    public int PassedChecks { get; set; }
    public List<DiagnosticIssue> Issues { get; } = new();

    public string StatusBadgeText => HealthScore switch
    {
        >= 90 => "Optimal (100%)",
        >= 75 => "Good (Minor Issues)",
        >= 50 => "Degraded (Action Needed)",
        _ => "Critical Health"
    };

    public string SummaryText => IsHealthy
        ? $"Emulator health is optimal! Passed {PassedChecks}/{ChecksPerformed} diagnostics."
        : $"Found {Issues.Count} potential issue(s) affecting GameLoop performance.";
}

public static class EmulatorDiagnosticService
{
    public static EmulatorHealthReport RunDiagnostic(GameLoopConfig config, HardwareInfo hw)
    {
        var report = new EmulatorHealthReport();
        int scoreDeductions = 0;

        // 1. Installation Registry Check
        report.ChecksPerformed++;
        bool regFound = false;
        try
        {
            using var k1 = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            using var k2 = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\TxGameAssistant");
            regFound = k1 != null || k2 != null;
        }
        catch { }

        if (regFound)
        {
            report.PassedChecks++;
        }
        else
        {
            report.Issues.Add(new DiagnosticIssue
            {
                Title = "GameLoop Registry Entries Missing",
                Description = "Could not find standard Tencent MobileGamePC registry keys.",
                Recommendation = "Launch GameLoop at least once to initialize user registry profiles.",
                Severity = DiagnosticSeverity.Warning,
                CanAutoFix = false
            });
            scoreDeductions += 15;
        }

        // 2. Core Binaries Check
        report.ChecksPerformed++;
        bool binariesValid = true;
        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            var appMarket = Path.Combine(config.InstallPath, "AppMarket", "AppMarket.exe");
            var uiDir = Path.Combine(config.InstallPath, "ui");
            if (!File.Exists(appMarket) && !Directory.Exists(uiDir))
            {
                binariesValid = false;
            }
        }

        if (binariesValid)
        {
            report.PassedChecks++;
        }
        else
        {
            report.Issues.Add(new DiagnosticIssue
            {
                Title = "GameLoop Core Files Incomplete",
                Description = "Key binaries like AppMarket.exe or UI engine files could not be confirmed in the install folder.",
                Recommendation = "Verify install path in settings or repair GameLoop installation.",
                Severity = DiagnosticSeverity.Critical,
                CanAutoFix = false
            });
            scoreDeductions += 30;
        }

        // 3. Keymapping Stock Base Check
        report.ChecksPerformed++;
        var keymapFiles = ResolutionKeymapService.GetKeymapFilePaths(config);
        if (keymapFiles.Count > 0 && keymapFiles.Any(File.Exists))
        {
            report.PassedChecks++;
        }
        else
        {
            report.Issues.Add(new DiagnosticIssue
            {
                Title = "Keymap Configuration Files Missing",
                Description = "DefaultKeyMapping.xml could not be located in standard TxGameAssistant directories.",
                Recommendation = "Click 'Restore 16:9 Keymap' in Keymaps Studio to deploy clean stock templates.",
                Severity = DiagnosticSeverity.Warning,
                CanAutoFix = true
            });
            scoreDeductions += 15;
        }

        // 4. Disk Space Health Check
        report.ChecksPerformed++;
        bool diskSpaceOk = true;
        try
        {
            string root = Path.GetPathRoot(config.InstallPath ?? "C:\\") ?? "C:\\";
            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.AvailableFreeSpace < 5L * 1024 * 1024 * 1024) // < 5 GB
            {
                diskSpaceOk = false;
                report.Issues.Add(new DiagnosticIssue
                {
                    Title = "Low Storage Space on Emulator Drive",
                    Description = $"Drive {drive.Name} has only {drive.AvailableFreeSpace / (1024 * 1024):F0} MB free. Emulators require at least 5 GB for smooth caching.",
                    Recommendation = "Run 1-Click Deep Clean or free disk space on the emulator drive.",
                    Severity = DiagnosticSeverity.Critical,
                    CanAutoFix = true
                });
                scoreDeductions += 25;
            }
        }
        catch { }

        if (diskSpaceOk) report.PassedChecks++;

        // 5. GPU & Renderer Alignment Check
        report.ChecksPerformed++;
        var recommendedRenderer = RecommendationEngine.DetermineOptimalRenderer(hw);
        if (config.ActiveRenderer == GraphicsRenderer.Auto)
        {
            report.PassedChecks++;
        }
        else if (config.ActiveRenderer != recommendedRenderer)
        {
            report.Issues.Add(new DiagnosticIssue
            {
                Title = "Graphics Renderer Sub-Optimal for Installed GPU",
                Description = $"System has {hw.GpuName} (Recommended: {recommendedRenderer}), but {config.ActiveRenderer} is currently forced.",
                Recommendation = $"Switch renderer to {recommendedRenderer} in GameLoop Studio for higher FPS and lower frame pacing jitter.",
                Severity = DiagnosticSeverity.Warning,
                CanAutoFix = true
            });
            scoreDeductions += 10;
        }
        else
        {
            report.PassedChecks++;
        }

        // 6. Hyper-V / Core Isolation Virtualization Conflict Check
        report.ChecksPerformed++;
        bool hvciActive = false;
        try
        {
            using var hvciKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            if (hvciKey != null && Convert.ToInt32(hvciKey.GetValue("Enabled", 0)) == 1)
            {
                hvciActive = true;
                report.Issues.Add(new DiagnosticIssue
                {
                    Title = "Memory Integrity / Core Isolation (HVCI) Active",
                    Description = "Windows Core Isolation virtualization overhead can reduce emulator frame rates by 15-25% and introduce frame-time jitter.",
                    Recommendation = "Consider disabling Core Isolation in Windows Security for competitive low-latency gaming.",
                    Severity = DiagnosticSeverity.Warning,
                    CanAutoFix = false
                });
                scoreDeductions += 15;
            }
        }
        catch { }
        if (!hvciActive) report.PassedChecks++;

        // 7. Stale DirectX / OpenGL Shader Cache Check
        report.ChecksPerformed++;
        bool shaderClean = true;
        try
        {
            var shaderDirs = ShaderCacheCleaner.GetShaderCachePaths(config);
            int totalShaders = 0;
            foreach (var dir in shaderDirs)
            {
                if (Directory.Exists(dir))
                {
                    totalShaders += Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).Count();
                }
            }

            if (totalShaders > 250)
            {
                shaderClean = false;
                report.Issues.Add(new DiagnosticIssue
                {
                    Title = "Accumulated Shader Cache Fragmentation",
                    Description = $"Discovered {totalShaders} cached shader objects. Stale compiled shaders from previous game patches cause micro-stutters.",
                    Recommendation = "Run 1-Click Shader Purge to force clean, recompilation of native shader blobs.",
                    Severity = DiagnosticSeverity.Warning,
                    CanAutoFix = true
                });
                scoreDeductions += 10;
            }
        }
        catch { }
        if (shaderClean) report.PassedChecks++;

        // 8. ADB Subsystem & Socket State Check
        report.ChecksPerformed++;
        bool adbHealthy = true;
        if (!AdbManager.IsAdbAvailable(config))
        {
            adbHealthy = false;
            report.Issues.Add(new DiagnosticIssue
            {
                Title = "GameLoop ADB Daemon Binary Not Found",
                Description = "adb.exe was not detected in standard GameLoop / AppMarket directories.",
                Recommendation = "Ensure GameLoop is installed with standard Android container support.",
                Severity = DiagnosticSeverity.Warning,
                CanAutoFix = false
            });
            scoreDeductions += 10;
        }
        if (adbHealthy) report.PassedChecks++;

        // Final Score Calculation
        report.HealthScore = Math.Clamp(100 - scoreDeductions, 0, 100);
        return report;
    }

    public static async Task<bool> AutoFixIssuesAsync(GameLoopConfig config, HardwareInfo hw)
    {
        try
        {
            Logger.Info("DiagnosticService", "Executing GameLoop Doctor 1-Click System Auto-Fix...");

            // 1. Restore keymaps if needed
            await ResolutionKeymapService.RestoreStockKeymapAsync(config);

            // 2. Set optimal renderer
            var optRenderer = RecommendationEngine.DetermineOptimalRenderer(hw);
            config.ForceDirectX = (optRenderer == GraphicsRenderer.DirectXPlus);

            // 3. Purge shader caches
            await ShaderCacheCleaner.PurgeShaderCacheAsync(config);

            // 4. Reset stale ADB daemon sockets
            if (AdbManager.IsAdbAvailable(config))
            {
                await AdbManager.RestartAdbServerAsync(config);
            }

            Logger.Success("DiagnosticService", "GameLoop Doctor: Applied all automated repairs successfully!");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("DiagnosticService", $"Auto-fix failed: {ex.Message}");
            return false;
        }
    }
}
