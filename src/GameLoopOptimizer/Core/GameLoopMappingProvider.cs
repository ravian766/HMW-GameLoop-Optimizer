using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class KeymapDeploymentResult
{
    public bool Success { get; set; }
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }
    public string AspectRatioLabel { get; set; } = string.Empty;
    public int FilesUpdated { get; set; }
    public int KeysCalibrated { get; set; }
    public string BackupId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> UpdatedPaths { get; } = new();
}

public static class GameLoopMappingProvider
{
    private static readonly Regex ItemBlockRegex = new(
        @"(<(Item|ItemEx)\s+[^>]*ApkName=""(?<apk>[^""]+)""[^>]*>)(?<inner>[\s\S]*?)(</\2>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeyMapModeRegex = new(
        @"(<KeyMapMode\s+[^>]*ModeID=""(?<modeId>\d+)""[^>]*>)(?<modeInner>[\s\S]*?)(</KeyMapMode>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PointXRegex = new(
        @"Point_X=""(?<x>[0-9\.]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PointYRegex = new(
        @"Point_Y=""(?<y>[0-9\.]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OffsetRegex = new(
        @"Offset=""(?<offset>[0-9\.]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Transforms PUBG keybindings inside GameLoop XML for the target resolution safely,
    /// respecting individual KeyMapMode baselines (720p, 1080p, 2K) and preserving all XML attributes.
    /// </summary>
    public static (string transformedXml, int calibratedCount) TransformKeymapXml(
        string xmlContent,
        int targetWidth,
        int targetHeight,
        int wasdSpeed = 100,
        KeyMappingProfile? customProfile = null)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return (xmlContent, 0);

        int count = 0;

        try
        {
            var result = ItemBlockRegex.Replace(xmlContent, itemMatch =>
            {
                var apk = itemMatch.Groups["apk"].Value;
                bool isPubg = GameLoopVersionDetector.AllPubgPackages.Any(p => apk.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                if (!isPubg) return itemMatch.Value;

                var openTag = itemMatch.Groups[1].Value;
                var inner = itemMatch.Groups["inner"].Value;
                var closeTag = itemMatch.Groups[3].Value;

                // Function to transform tags inside any content block with given baseline resolution
                string TransformBlockTags(string content, int baseW, int baseH, int hudModeOverride = 0)
                {
                    var tagPattern = new Regex(@"<(?<tag>[A-Za-z0-9_]+)\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase);

                    return tagPattern.Replace(content, tagMatch =>
                    {
                        string tagName = tagMatch.Groups["tag"].Value;
                        string attrs = tagMatch.Groups["attrs"].Value;

                        var mx = PointXRegex.Match(attrs);
                        var my = PointYRegex.Match(attrs);

                        if (mx.Success && my.Success &&
                            double.TryParse(mx.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ox) &&
                            double.TryParse(my.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double oy))
                        {
                            // Extract control name or description for anchor detection
                            string controlName = string.Empty;
                            var nameMatch = Regex.Match(attrs, @"(ItemName|Description|comm|Id)=""(?<name>[^""]+)""", RegexOptions.IgnoreCase);
                            if (nameMatch.Success) controlName = nameMatch.Groups["name"].Value;

                            var anchor = CoordinateTransformService.DetectAnchorType(controlName, ox, oy);

                            // Respect HUD mode override if provided (1/2 = Vehicle, 3 = Swim)
                            if (hudModeOverride is 1 or 2)
                            {
                                anchor = ox < 0.44 ? ControlAnchorType.Left : (ox > 0.56 ? ControlAnchorType.Right : ControlAnchorType.Center);
                            }
                            else if (hudModeOverride == 3)
                            {
                                anchor = ox < 0.35 ? ControlAnchorType.Left : (ox > 0.65 ? ControlAnchorType.Right : ControlAnchorType.Center);
                            }

                            var (nx, ny) = CoordinateTransformService.TransformCoordinate(ox, oy, targetWidth, targetHeight, anchor, baseW, baseH);

                            count++;

                            // Replace Point_X and Point_Y individually preserving all other attributes
                            string updatedAttrs = PointXRegex.Replace(attrs, $"Point_X=\"{nx.ToString("F6", CultureInfo.InvariantCulture)}\"");
                            updatedAttrs = PointYRegex.Replace(updatedAttrs, $"Point_Y=\"{ny.ToString("F6", CultureInfo.InvariantCulture)}\"");

                            // Scale Offset if present on WASD or joystick
                            if (attrs.Contains("Offset=", StringComparison.OrdinalIgnoreCase))
                            {
                                updatedAttrs = OffsetRegex.Replace(updatedAttrs, offMatch =>
                                {
                                    if (double.TryParse(offMatch.Groups["offset"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double origOffset))
                                    {
                                        double sx = CoordinateTransformService.CalculateHorizontalScaleFactor(targetWidth, targetHeight, baseW, baseH);
                                        double newOffset = Math.Clamp(origOffset * sx, 0.04, 0.18);
                                        return $"Offset=\"{newOffset.ToString("F6", CultureInfo.InvariantCulture)}\"";
                                    }
                                    return offMatch.Value;
                                });
                            }

                            return $"<{tagName}{updatedAttrs}>";
                        }

                        return tagMatch.Value;
                    });
                }

                // Check if inner content contains KeyMapMode blocks
                bool hasKeyMapModes = KeyMapModeRegex.IsMatch(inner);

                string transformedInner;
                if (hasKeyMapModes)
                {
                    transformedInner = KeyMapModeRegex.Replace(inner, modeMatch =>
                    {
                        var modeOpen = modeMatch.Groups[1].Value;
                        var modeInner = modeMatch.Groups["modeInner"].Value;
                        var modeClose = modeMatch.Groups[3].Value;
                        int modeId = int.TryParse(modeMatch.Groups["modeId"].Value, out int mid) ? mid : 3;

                        // Determine baseline for this mode (ModeID 1/2 = 720p, ModeID 3 = 1080p, ModeID 4 = 2K)
                        int baseW = modeId switch
                        {
                            1 or 2 => 1280,
                            4 => 2560,
                            _ => 1920
                        };

                        int baseH = modeId switch
                        {
                            1 or 2 => 720,
                            4 => 1440,
                            _ => 1080
                        };

                        string transformedModeInner = TransformBlockTags(modeInner, baseW, baseH);
                        return modeOpen + transformedModeInner + modeClose;
                    });
                }
                else
                {
                    // Item block directly contains Key/KeyMapping tags
                    int itemMode = 0;
                    var itemModeMatch = Regex.Match(openTag, @"Mode=""(?<mode>\d+)""", RegexOptions.IgnoreCase);
                    if (itemModeMatch.Success && int.TryParse(itemModeMatch.Groups["mode"].Value, out int im))
                    {
                        itemMode = im;
                    }

                    transformedInner = TransformBlockTags(inner, 1920, 1080, itemMode);
                }

                return openTag + transformedInner + closeTag;
            });

            // Inject WASD response speed if requested
            if (wasdSpeed > 0)
            {
                var (speedXml, _) = KeymapSpeedService.InjectWasdSpeed(result, wasdSpeed);
                result = speedXml;
            }

            return (result, count);
        }
        catch (Exception ex)
        {
            Logger.Error("GameLoopMappingProvider", $"Error during XML keymap transformation: {ex.Message}");
            return (xmlContent, 0);
        }
    }

    /// <summary>
    /// Synchronously backs up and applies the calibrated keymap to all GameLoop keymap files.
    /// </summary>
    public static async Task<KeymapDeploymentResult> DeployKeymapAsync(
        int targetWidth,
        int targetHeight,
        GameLoopConfig config,
        int wasdSpeed = 100,
        KeyMappingProfile? profile = null)
    {
        var result = new KeymapDeploymentResult
        {
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AspectRatioLabel = $"{targetWidth}x{targetHeight}"
        };

        // 1. Validate Target Resolution
        var resVal = MappingValidator.ValidateResolution(targetWidth, targetHeight);
        if (!resVal.IsValid)
        {
            result.Success = false;
            result.Message = resVal.Summary;
            return result;
        }

        // 2. Create atomic safety backup prior to modifications
        var backup = await KeymapBackupManager.CreateBackupAsync(config, $"Auto-Backup before {targetWidth}x{targetHeight} Keymap Calibration");
        if (backup != null)
        {
            result.BackupId = backup.Id;
        }

        // 3. Obtain clean stock 16:9 base reference XML to prevent compounding drift
        string stockXml = await GetStockBaseXmlAsync(config);
        if (string.IsNullOrWhiteSpace(stockXml))
        {
            result.Success = false;
            result.Message = "Could not locate clean stock 16:9 reference XML.";
            Logger.Warn("GameLoopMappingProvider", result.Message);
            return result;
        }

        // 4. Transform XML fresh from 16:9 stock base
        var (calibratedXml, calibratedCount) = TransformKeymapXml(stockXml, targetWidth, targetHeight, wasdSpeed, profile);
        result.KeysCalibrated = calibratedCount;

        // 5. Validate output XML before writing
        if (!MappingValidator.ValidateXmlFragment(calibratedXml, out string xmlErr))
        {
            result.Success = false;
            result.Message = $"Generated keymap failed XML validation: {xmlErr}";
            Logger.Error("GameLoopMappingProvider", result.Message);
            return result;
        }

        // 6. Write calibrated XML atomically to all target files
        var targetPaths = ResolutionKeymapService.GetKeymapFilePaths(config);
        int updated = 0;

        foreach (var path in targetPaths)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(path, calibratedXml);
                result.UpdatedPaths.Add(path);
                updated++;
                Logger.Success("GameLoopMappingProvider", $"Deployed calibrated keymap to '{path}'.");
            }
            catch (Exception ex)
            {
                Logger.Error("GameLoopMappingProvider", $"Failed writing to keymap file '{path}': {ex.Message}");
            }
        }

        result.FilesUpdated = updated;
        result.Success = updated > 0;
        result.Message = result.Success
            ? $"Successfully calibrated {calibratedCount} keys for {targetWidth}x{targetHeight} across {updated} file(s)."
            : "Failed to write keymap configuration files.";

        return result;
    }

    private static async Task<string> GetStockBaseXmlAsync(GameLoopConfig config)
    {
        var candidates = new List<string>
        {
            @"D:\Program Files\TxGameAssistant\ui\DefaultKeyMapping.stock_16_9.xml",
            @"D:\Program Files\TxGameAssistant\ui\ConfigFile\DefaultKeyMapping.stock_16_9.xml",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "stock_16_9_DefaultKeyMapping.xml")
        };

        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            candidates.Insert(0, Path.Combine(config.InstallPath, "ui", "DefaultKeyMapping.stock_16_9.xml"));
            candidates.Insert(1, Path.Combine(config.InstallPath, "ui", "ConfigFile", "DefaultKeyMapping.stock_16_9.xml"));
        }

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                var text = await File.ReadAllTextAsync(c);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10000)
                {
                    return text;
                }
            }
        }

        // Fallback: Read first existing DefaultKeyMapping.xml and cache it as the stock base
        var keymapFiles = ResolutionKeymapService.GetKeymapFilePaths(config);
        foreach (var f in keymapFiles)
        {
            if (File.Exists(f))
            {
                var text = await File.ReadAllTextAsync(f);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10000)
                {
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
}
