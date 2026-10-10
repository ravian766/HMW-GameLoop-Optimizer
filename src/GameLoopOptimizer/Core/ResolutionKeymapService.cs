using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.ViewModels;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class KeymapCalibrationResult
{
    public bool Success { get; set; }
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }
    public string AspectRatioLabel { get; set; } = string.Empty;
    public int FilesUpdated { get; set; }
    public int KeysCalibrated { get; set; }
    public string BackupProfileId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public enum HudCalibrationMode
{
    GeneralOnFoot,
    VehicleDriving,
    SwimmingAndParachute
}

public static class ResolutionKeymapService
{
    public static readonly string[] PubgApkNames = GameLoopVersionDetector.AllPubgPackages;

    /// <summary>
    /// Calculates deadzone and radius compensated sprint offset for stretched resolutions.
    /// Prevents joystick sprint cancellation when moving diagonally on 4:3 or 1:1 stretched views.
    /// </summary>
    public static double CalculateCompensatedWasdOffset(double baseOffset, int targetWidth, int targetHeight, int baseWidth = 1920, int baseHeight = 1080)
    {
        if (targetWidth <= 0 || targetHeight <= 0) return baseOffset;
        double baseRatio = (double)baseWidth / baseHeight;
        double targetRatio = (double)targetWidth / targetHeight;
        double sx = baseRatio / targetRatio;
        return Math.Clamp(baseOffset * sx, 0.04, 0.18);
    }

    /// <summary>
    /// Transforms normalized coordinates with specialized HUD anchor modes (On-Foot, Vehicle Driving, Swimming/Parachute).
    /// </summary>
    public static (double newX, double newY) CalibrateCoordinateForHudMode(double x, double y, int targetWidth, int targetHeight, HudCalibrationMode mode, int baseWidth = 1920, int baseHeight = 1080)
    {
        if (targetWidth <= 0 || targetHeight <= 0) return (x, y);

        double baseRatio = (double)baseWidth / baseHeight;
        double targetRatio = (double)targetWidth / targetHeight;

        if (Math.Abs(baseRatio - targetRatio) < 0.005)
        {
            return (Math.Clamp(x, 0.01, 0.99), Math.Clamp(y, 0.01, 0.99));
        }

        double sx = baseRatio / targetRatio;
        double newX;

        switch (mode)
        {
            case HudCalibrationMode.VehicleDriving:
                // Vehicle controls: wide steering split on left, gas/brake pedal cluster on right
                if (x < 0.44)
                {
                    newX = x * sx;
                }
                else if (x > 0.56)
                {
                    double distFromRight = 1.0 - x;
                    newX = 1.0 - (distFromRight * sx);
                }
                else
                {
                    double offsetFromCenter = x - 0.5;
                    newX = 0.5 + (offsetFromCenter * sx);
                }
                break;

            case HudCalibrationMode.SwimmingAndParachute:
                // Floating and dive surface controls
                if (x < 0.35)
                {
                    newX = x * sx;
                }
                else if (x > 0.65)
                {
                    double distFromRight = 1.0 - x;
                    newX = 1.0 - (distFromRight * sx);
                }
                else
                {
                    double offsetFromCenter = x - 0.5;
                    newX = 0.5 + (offsetFromCenter * sx);
                }
                break;

            case HudCalibrationMode.GeneralOnFoot:
            default:
                if (x < 0.38)
                {
                    newX = x * sx;
                }
                else if (x > 0.62)
                {
                    double distFromRight = 1.0 - x;
                    newX = 1.0 - (distFromRight * sx);
                }
                else
                {
                    double offsetFromCenter = x - 0.5;
                    newX = 0.5 + (offsetFromCenter * sx);
                }
                break;
        }

        newX = Math.Clamp(newX, 0.01, 0.99);
        double newY = Math.Clamp(y, 0.01, 0.99);

        return (Math.Round(newX, 6), Math.Round(newY, 6));
    }

    /// <summary>
    /// Transforms normalized (0.0 - 1.0) coordinates from standard 16:9 (1920x1080) into target aspect ratio.
    /// </summary>
    public static (double newX, double newY) CalibrateCoordinate(double x, double y, int targetWidth, int targetHeight, int baseWidth = 1920, int baseHeight = 1080)
    {
        return CalibrateCoordinateForHudMode(x, y, targetWidth, targetHeight, HudCalibrationMode.GeneralOnFoot, baseWidth, baseHeight);
    }

    /// <summary>
    /// Parses GameLoop KeyMapping XML and calibrates all PUBG Mobile keybinding coordinates for the target resolution safely.
    /// Preserves mode integrity and XML attributes across 720p, 1080p, and 2K GameLoop profiles.
    /// </summary>
    public static (string calibratedXml, int calibratedCount) CalibrateKeymapXml(string xmlContent, int targetWidth, int targetHeight, int wasdSpeed = 100)
    {
        return GameLoopMappingProvider.TransformKeymapXml(xmlContent, targetWidth, targetHeight, wasdSpeed);
    }

    /// <summary>
    /// Discovers all GameLoop keymap files, backs them up, injects calibrated coordinates and WASD response speed from the stock 16:9 reference XML, and updates registry modes.
    /// </summary>
    public static async Task<KeymapCalibrationResult> DeployResolutionKeymapAsync(int targetWidth, int targetHeight, GameLoopConfig config, int wasdSpeed = 100)
    {
        var deployRes = await GameLoopMappingProvider.DeployKeymapAsync(targetWidth, targetHeight, config, wasdSpeed);

        var result = new KeymapCalibrationResult
        {
            Success = deployRes.Success,
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AspectRatioLabel = GameLoopViewModel.CalculateAspectRatio(targetWidth, targetHeight),
            FilesUpdated = deployRes.FilesUpdated,
            KeysCalibrated = deployRes.KeysCalibrated,
            BackupProfileId = deployRes.BackupId,
            Message = deployRes.Message
        };

        if (deployRes.Success)
        {
            try
            {
                var regPaths = new[]
                {
                    @"Software\Tencent\MobileGamePC",
                    @"Software\Tencent\TxGameAssistant"
                };

                foreach (var rp in regPaths)
                {
                    using var subKey = Registry.CurrentUser.CreateSubKey(rp);
                    if (subKey != null)
                    {
                        subKey.SetValue("KeymapResolutionWidth", targetWidth, RegistryValueKind.DWord);
                        subKey.SetValue("KeymapResolutionHeight", targetHeight, RegistryValueKind.DWord);
                        subKey.SetValue("KeymapAspectRatio", result.AspectRatioLabel, RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// Restores stock 16:9 widescreen keymap coordinates.
    /// </summary>
    public static async Task<KeymapCalibrationResult> RestoreStockKeymapAsync(GameLoopConfig config)
    {
        return await DeployResolutionKeymapAsync(1920, 1080, config);
    }

    /// <summary>
    /// Instantly toggles between Native 16:9 (1920x1080) and Stretched Resolution (e.g. 1440x1080),
    /// updating GameLoop resolution registry keys and deploying calibrated keymaps.
    /// </summary>
    public static async Task<KeymapCalibrationResult> ToggleStretchedResolutionAsync(GameLoopConfig config, int stretchedWidth = 1440, int stretchedHeight = 1080)
    {
        bool isCurrentlyStretched = (config.VmResWidth == stretchedWidth && config.VmResHeight == stretchedHeight);
        int targetW = isCurrentlyStretched ? 1920 : stretchedWidth;
        int targetH = isCurrentlyStretched ? 1080 : stretchedHeight;

        try
        {
            var regPaths = new[]
            {
                @"Software\Tencent\MobileGamePC\UI",
                @"Software\Tencent\TxGameAssistant\UI",
                @"Software\Tencent\MobileGamePC",
                @"Software\Tencent\TxGameAssistant"
            };

            foreach (var rp in regPaths)
            {
                using var key = Registry.CurrentUser.CreateSubKey(rp);
                if (key != null)
                {
                    key.SetValue("VMResWidth", targetW, RegistryValueKind.DWord);
                    key.SetValue("VMResHeight", targetH, RegistryValueKind.DWord);
                }
            }

            config.VmResWidth = targetW;
            config.VmResHeight = targetH;
        }
        catch { }

        var result = await DeployResolutionKeymapAsync(targetW, targetH, config);
        result.Message = isCurrentlyStretched
            ? $"Toggled to Native 16:9 ({targetW}x{targetH}). Keymaps synchronized!"
            : $"Toggled to Stretched Res ({targetW}x{targetH}). Keymaps synchronized!";

        Logger.Success("ResolutionKeymap", result.Message);
        return result;
    }

    private static async Task<string> GetStockBaseXmlAsync(GameLoopConfig config)
    {
        var stockCandidates = new List<string>
        {
            @"D:\Program Files\TxGameAssistant\ui\DefaultKeyMapping.stock_16_9.xml",
            @"D:\Program Files\TxGameAssistant\ui\ConfigFile\DefaultKeyMapping.stock_16_9.xml",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "stock_16_9_DefaultKeyMapping.xml")
        };

        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            stockCandidates.Insert(0, Path.Combine(config.InstallPath, "ui", "DefaultKeyMapping.stock_16_9.xml"));
            stockCandidates.Insert(1, Path.Combine(config.InstallPath, "ui", "ConfigFile", "DefaultKeyMapping.stock_16_9.xml"));
        }

        foreach (var p in stockCandidates)
        {
            if (File.Exists(p))
            {
                var text = await File.ReadAllTextAsync(p);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10000)
                {
                    return text;
                }
            }
        }

        // Fallback: Read first found DefaultKeyMapping.xml
        var normalFiles = GetKeymapFilePaths(config);
        foreach (var f in normalFiles)
        {
            if (File.Exists(f))
            {
                var text = await File.ReadAllTextAsync(f);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Save as reference
                    try
                    {
                        var stockDest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "stock_16_9_DefaultKeyMapping.xml");
                        await File.WriteAllTextAsync(stockDest, text);
                    }
                    catch { }
                    return text;
                }
            }
        }

        return string.Empty;
    }

    public static List<string> GetKeymapFilePaths(GameLoopConfig config)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var candidateDirs = new List<string>();

        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            candidateDirs.Add(config.InstallPath);
            candidateDirs.Add(Path.Combine(config.InstallPath, "ui"));
            candidateDirs.Add(Path.Combine(config.InstallPath, "ui", "ConfigFile"));
            candidateDirs.Add(Path.Combine(config.InstallPath, "AppMarket"));
            candidateDirs.Add(Path.Combine(config.InstallPath, "AppMarket", "ConfigFile"));
            // 7.0.19.05+ nested directory structure
            candidateDirs.Add(Path.Combine(config.InstallPath, "TxGameAssistant", "ui"));
            candidateDirs.Add(Path.Combine(config.InstallPath, "TxGameAssistant", "ui", "ConfigFile"));
        }

        if (!string.IsNullOrEmpty(config.UserDir))
        {
            candidateDirs.Add(config.UserDir);
            candidateDirs.Add(Path.Combine(config.UserDir, "ConfigFile"));
        }

        var standardRoots = new[]
        {
            @"D:\Program Files\TxGameAssistant\ui",
            @"D:\Program Files\TxGameAssistant\ui\ConfigFile",
            @"C:\Program Files\TxGameAssistant\ui",
            @"C:\Program Files\TxGameAssistant\ui\ConfigFile",
            @"C:\Program Files (x86)\TxGameAssistant\ui",
            @"C:\Program Files (x86)\TxGameAssistant\ui\ConfigFile",
            @"D:\TxGameAssistant\ui",
            @"D:\TxGameAssistant\ui\ConfigFile",
            @"E:\TxGameAssistant\ui",
            @"E:\TxGameAssistant\ui\ConfigFile",
            @"D:\Program Files\TxGameAssistant\AppMarket",
            @"C:\Program Files\TxGameAssistant\AppMarket",
            @"D:\GameLoop\ui",
            @"D:\GameLoop\ui\ConfigFile",
            @"C:\GameLoop\ui",
            @"C:\GameLoop\ui\ConfigFile",
            @"D:\GameLoop\TxGameAssistant\ui",
            @"D:\GameLoop\TxGameAssistant\ui\ConfigFile",
            @"C:\GameLoop\TxGameAssistant\ui",
            @"C:\GameLoop\TxGameAssistant\ui\ConfigFile"
        };

        candidateDirs.AddRange(standardRoots);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant"));
        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant", "ConfigFile"));
        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC"));
        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "GameLoop"));
        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "GameLoop", "ConfigFile"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "TxGameAssistant"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "TxGameAssistant", "ConfigFile"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "MobileGamePC"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "GameLoop"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "GameLoop", "ConfigFile"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "GameLoop", "config"));

        var targetFileNames = new[]
        {
            "DefaultKeyMapping.xml"
        };

        foreach (var dir in candidateDirs)
        {
            if (Directory.Exists(dir))
            {
                foreach (var fileName in targetFileNames)
                {
                    var full = Path.Combine(dir, fileName);
                    paths.Add(full);
                }
            }
        }

        return paths.ToList();
    }
}
