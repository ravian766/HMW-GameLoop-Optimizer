using System.IO;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class GameLoopEnvironmentDetails
{
    public bool IsInstalled { get; set; }
    public string Version { get; set; } = "Unknown";
    public string Architecture { get; set; } = "32-bit";
    public string InstallPath { get; set; } = string.Empty;
    public string PrimaryKeymapPath { get; set; } = string.Empty;
    public bool KeymapFileExists { get; set; }
    public bool KeymapFileWritable { get; set; }
    public List<string> DetectedPubgPackages { get; } = new();
    public int ConfiguredWidth { get; set; } = 1920;
    public int ConfiguredHeight { get; set; } = 1080;
    public int ConfiguredDpi { get; set; } = 320;
    public string KeymapStorageStatusText => KeymapFileWritable 
        ? "Writable (Unlocked)" 
        : (KeymapFileExists ? "Read-Only (Locked)" : "Not Created Yet");
    public string Summary => $"GameLoop {Version} ({Architecture}) at '{InstallPath}' | Config: {ConfiguredWidth}x{ConfiguredHeight} (DPI {ConfiguredDpi})";
}

public static class GameLoopVersionDetector
{
    public static readonly string[] AllPubgPackages = new[]
    {
        "com.tencent.ig",
        "com.tencent.ig_ss",
        "com.pubg.krmobile",
        "com.pubg.krmobile_ss",
        "com.vng.pubgmobile",
        "com.vng.pubgmobile_ss",
        "com.rekoo.pubgm",
        "com.rekoo.pubgm_ss",
        "com.pubg.imobile",
        "com.pubg.imobile_ss",
        "com.tencent.tmgp.pubgmhd",
        "com.tencent.tmgp.pubgmhd_ss"
    };

    public static GameLoopEnvironmentDetails Detect(GameLoopConfig? config = null)
    {
        var details = new GameLoopEnvironmentDetails();
        config ??= GameLoopDetector.DetectGameLoop();

        details.IsInstalled = config.IsInstalled;
        details.InstallPath = config.InstallPath;
        details.Version = string.IsNullOrWhiteSpace(config.Version) ? "3.x / 7.x" : config.Version;
        details.ConfiguredWidth = config.VmResWidth > 0 ? config.VmResWidth : 1920;
        details.ConfiguredHeight = config.VmResHeight > 0 ? config.VmResHeight : 1080;
        details.ConfiguredDpi = config.VmDpi > 0 ? config.VmDpi : 320;

        // Detect architecture (check if aow_exe or AndroidEmulator is 64-bit or install path contains x86)
        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            if (config.InstallPath.Contains("x86", StringComparison.OrdinalIgnoreCase))
            {
                details.Architecture = "32-bit (x86)";
            }
            else
            {
                details.Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            }

            var keymapPaths = ResolutionKeymapService.GetKeymapFilePaths(config);
            if (keymapPaths.Count > 0)
            {
                details.PrimaryKeymapPath = keymapPaths[0];
                details.KeymapFileExists = File.Exists(details.PrimaryKeymapPath);
                if (details.KeymapFileExists)
                {
                    try
                    {
                        using var fs = File.Open(details.PrimaryKeymapPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        details.KeymapFileWritable = true;
                    }
                    catch
                    {
                        details.KeymapFileWritable = false;
                    }
                }
            }
        }

        // Detect configured/installed PUBG packages from registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            if (key != null)
            {
                var names = key.GetValueNames();
                foreach (var apk in AllPubgPackages)
                {
                    if (names.Any(n => n.StartsWith(apk, StringComparison.OrdinalIgnoreCase)))
                    {
                        details.DetectedPubgPackages.Add(apk);
                    }
                }
            }
        }
        catch { }

        if (details.DetectedPubgPackages.Count == 0)
        {
            details.DetectedPubgPackages.Add("com.tencent.ig");
            details.DetectedPubgPackages.Add("com.tencent.ig_ss");
        }

        return details;
    }
}
