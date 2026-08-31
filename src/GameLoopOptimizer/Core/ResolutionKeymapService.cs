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
    public static readonly string[] PubgApkNames = new[]
    {
        "com.tencent.ig",
        "com.tencent.ig_ss",
        "com.pubg.krmobile",
        "com.vng.pubgmobile",
        "com.rebel.tw",
        "com.tencent.tmgp.pubgm",
        "com.tencent.igce"
    };

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
    /// Parses GameLoop KeyMapping XML and calibrates all PUBG Mobile keybinding coordinates for the target resolution.
    /// Handles fragmented multi-root XML structures safely and updates only PUBG Mobile APK sections.
    /// </summary>
    public static (string calibratedXml, int calibratedCount) CalibrateKeymapXml(string xmlContent, int targetWidth, int targetHeight, int wasdSpeed = 100)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return (xmlContent, 0);

        int count = 0;

        try
        {
            // Match every PUBG Item or ItemEx block
            var itemPattern = new Regex(@"(<(Item|ItemEx)\s+[^>]*ApkName=""(?<apk>[^""]+)""[^>]*>)(?<inner>[\s\S]*?)(</\2>)", RegexOptions.IgnoreCase);

            var result = itemPattern.Replace(xmlContent, match =>
            {
                var apk = match.Groups["apk"].Value;
                bool isPubg = PubgApkNames.Any(p => apk.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!isPubg) return match.Value;

                var openTag = match.Groups[1].Value;
                var inner = match.Groups["inner"].Value;
                var closeTag = match.Groups[3].Value;

                // Calibrate Point_X and Point_Y inside this PUBG block
                var pointPattern = new Regex(@"Point_X=""(?<x>[0-9\.]+)""\s+Point_Y=""(?<y>[0-9\.]+)""", RegexOptions.IgnoreCase);
                var calibratedInner = pointPattern.Replace(inner, m =>
                {
                    if (double.TryParse(m.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ox) &&
                        double.TryParse(m.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double oy))
                    {
                        var (nx, ny) = CalibrateCoordinate(ox, oy, targetWidth, targetHeight);
                        count++;
                        return $"Point_X=\"{nx.ToString("F6", CultureInfo.InvariantCulture)}\" Point_Y=\"{ny.ToString("F6", CultureInfo.InvariantCulture)}\"";
                    }
                    return m.Value;
                });

                // Also scale Offset if present (for joystick radius)
                var offsetPattern = new Regex(@"Offset=""(?<offset>[0-9\.]+)""", RegexOptions.IgnoreCase);
                calibratedInner = offsetPattern.Replace(calibratedInner, m =>
                {
                    if (double.TryParse(m.Groups["offset"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double origOffset))
                    {
                        double sx = (1920.0 / 1080.0) / ((double)targetWidth / targetHeight);
                        double newOffset = Math.Clamp(origOffset * sx, 0.04, 0.18);
                        return $"Offset=\"{newOffset.ToString("F6", CultureInfo.InvariantCulture)}\"";
                    }
                    return m.Value;
                });

                return openTag + calibratedInner + closeTag;
            });

            if (wasdSpeed > 0)
            {
                var (speedXml, _) = KeymapSpeedService.InjectWasdSpeed(result, wasdSpeed);
                result = speedXml;
            }

            return (result, count);
        }
        catch (Exception ex)
        {
            Logger.Error("KeymapCalibration", $"Error during keymap calibration: {ex.Message}");
            return (xmlContent, 0);
        }
    }

    /// <summary>
    /// Discovers all GameLoop keymap files, backs them up, injects calibrated coordinates and WASD response speed from the stock 16:9 reference XML, and updates registry modes.
    /// </summary>
    public static async Task<KeymapCalibrationResult> DeployResolutionKeymapAsync(int targetWidth, int targetHeight, GameLoopConfig config, int wasdSpeed = 100)
    {
        var result = new KeymapCalibrationResult
        {
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AspectRatioLabel = GameLoopViewModel.CalculateAspectRatio(targetWidth, targetHeight)
        };

        try
        {
            // 1. Take automatic safety snapshot before modifying
            var backup = await KeymapBackupManager.CreateBackupAsync(config, $"Auto-Backup before {targetWidth}x{targetHeight} Keymap Calibration");
            if (backup != null)
            {
                result.BackupProfileId = backup.Id;
            }

            // 2. Obtain clean Stock 16:9 base reference XML to prevent compounding drift
            string stockXml = await GetStockBaseXmlAsync(config);

            if (string.IsNullOrWhiteSpace(stockXml))
            {
                result.Success = false;
                result.Message = "Could not locate stock 16:9 keymap base file.";
                Logger.Warn("KeymapCalibration", result.Message);
                return result;
            }

            // 3. Calibrate fresh from the 16:9 stock base with WASD response speed
            var (calibratedXml, totalCalibrated) = CalibrateKeymapXml(stockXml, targetWidth, targetHeight, wasdSpeed);

            // 4. Discover all target keymap files and write the calibrated XML
            var targetFiles = GetKeymapFilePaths(config);
            int filesUpdated = 0;

            foreach (var filePath in targetFiles)
            {
                try
                {
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    await File.WriteAllTextAsync(filePath, calibratedXml);
                    filesUpdated++;
                    Logger.Success("KeymapCalibration", $"Injected calibrated keymap into '{filePath}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error("KeymapCalibration", $"Failed writing to keymap file '{filePath}': {ex.Message}");
                }
            }

            // 5. Update Registry Keymapping Settings
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

            result.Success = filesUpdated > 0;
            result.FilesUpdated = filesUpdated;
            result.KeysCalibrated = totalCalibrated;
            result.Message = result.Success
                ? $"Calibrated {totalCalibrated} key bindings for {targetWidth}x{targetHeight} across {filesUpdated} files! (Restart GameLoop to load into memory)"
                : "Failed to write keymap files.";

            Logger.Success("KeymapCalibration", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error during keymap deployment: {ex.Message}";
            Logger.Error("KeymapCalibration", result.Message);
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
            @"C:\GameLoop\ui\ConfigFile"
        };

        candidateDirs.AddRange(standardRoots);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant"));
        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant", "ConfigFile"));
        candidateDirs.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "TxGameAssistant"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "TxGameAssistant", "ConfigFile"));
        candidateDirs.Add(Path.Combine(appData, "Tencent", "MobileGamePC"));

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
