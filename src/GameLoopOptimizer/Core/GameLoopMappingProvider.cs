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

    /// <summary>
    /// Attributes searched, in order, to determine a control's descriptive name for anchor
    /// detection. Mirrors the original regex-based lookup order.
    /// </summary>
    private static readonly string[] ControlNameAttributes = { "ItemName", "Description", "comm", "Id" };

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

        // GameLoop emits multi-root XML, so wrap it in a synthetic root to parse it.
        //
        // NOTE: this method previously used regex rewriting, which cannot correctly handle
        // GameLoop wrapping attribute values across physical lines. That corrupted the document
        // (nearly doubling its size, dropping </KeyMapMode> tags and emitting stray digits such as
        // "3" / "4"). This implementation parses the XML instead, so multi-line attribute values
        // are handled natively and the document structure is guaranteed to be preserved.
        string wrapped = $"<KeymapRoot>{xmlContent}</KeymapRoot>";

        System.Xml.Linq.XDocument doc;
        try
        {
            doc = System.Xml.Linq.XDocument.Parse(wrapped, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            Logger.Error("GameLoopMappingProvider", $"Keymap XML is not parseable, transformation aborted: {ex.Message}");
            return (xmlContent, 0);
        }

        var root = doc.Root;
        if (root == null) return (xmlContent, 0);

        int count = 0;

        // Locate Item / ItemEx elements. Real GameLoop files are multi-root, so the items sit directly
        // under the synthetic root; but callers (and tests) sometimes pass an already-wrapped
        // document, so search one level of nesting as well.
        var itemElements = root.Elements().Where(e =>
                string.Equals(e.Name.LocalName, "Item", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Name.LocalName, "ItemEx", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (itemElements.Count == 0)
        {
            itemElements = root.Elements()
                .SelectMany(child => child.Elements().Where(e =>
                    string.Equals(e.Name.LocalName, "Item", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Name.LocalName, "ItemEx", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        foreach (var item in itemElements)
        {
            var apkAttr = item.Attribute("ApkName");
            if (apkAttr == null) continue;

            string apk = apkAttr.Value;
            bool isPubg = GameLoopVersionDetector.AllPubgPackages.Any(p => apk.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            if (!isPubg) continue;

            var keyMapModes = item.Elements().Where(e =>
                string.Equals(e.Name.LocalName, "KeyMapMode", StringComparison.OrdinalIgnoreCase)).ToList();

            if (keyMapModes.Count > 0)
            {
                foreach (var mode in keyMapModes)
                {
                    int modeId = 3;
                    var modeAttr = mode.Attribute("ModeID");
                    if (modeAttr != null && int.TryParse(modeAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        modeId = parsed;
                    }

                    // Determine baseline for this mode (ModeID 1/2 = 720p, 3 = 1080p, 4 = 2K)
                    int baseW = modeId switch { 1 or 2 => 1280, 4 => 2560, _ => 1920 };
                    int baseH = modeId switch { 1 or 2 => 720, 4 => 1440, _ => 1080 };

                    TransformElement(mode, targetWidth, targetHeight, baseW, baseH, modeId, ref count);
                }
            }
            else
            {
                // Item block directly contains KeyMapping tags.
                int itemMode = 0;
                var itemModeAttr = item.Attribute("Mode");
                if (itemModeAttr != null && int.TryParse(itemModeAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int im))
                {
                    itemMode = im;
                }

                TransformElement(item, targetWidth, targetHeight, 1920, 1080, itemMode, ref count);
            }
        }

        string result;

        try
        {
            // Serialize without an XML declaration and without re-indenting so the output stays
            // as close as possible to the original layout.
            var sb = new System.Text.StringBuilder();
            var settings = new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                ConformanceLevel = System.Xml.ConformanceLevel.Fragment
            };

            using (var writer = System.Xml.XmlWriter.Create(sb, settings))
            {
                foreach (var node in root.Nodes())
                {
                    node.WriteTo(writer);
                }
            }

            result = sb.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error("GameLoopMappingProvider", $"Failed to serialize transformed keymap: {ex.Message}");
            return (xmlContent, 0);
        }

        // Inject WASD response speed if requested
        if (wasdSpeed > 0)
        {
            var (speedXml, _) = KeymapSpeedService.InjectWasdSpeed(result, wasdSpeed);
            result = speedXml;
        }

        return (result, count);
    }

    /// <summary>
    /// Applies the anchor-aware coordinate transform to every element beneath
    /// <paramref name="element"/> that carries both Point_X and Point_Y attributes.
    /// </summary>
    private static void TransformElement(
        System.Xml.Linq.XElement element,
        int targetWidth,
        int targetHeight,
        int baseW,
        int baseH,
        int hudModeOverride,
        ref int count)
    {
        foreach (var node in element.DescendantsAndSelf().ToList())
        {
            var xAttr = node.Attribute("Point_X");
            var yAttr = node.Attribute("Point_Y");

            if (xAttr == null || yAttr == null) continue;

            if (!double.TryParse(xAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ox)) continue;
            if (!double.TryParse(yAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double oy)) continue;

            // Extract control name or description for anchor detection.
            string controlName = string.Empty;
            foreach (string attrName in ControlNameAttributes)
            {
                var a = node.Attribute(attrName);
                if (a != null && !string.IsNullOrWhiteSpace(a.Value))
                {
                    controlName = a.Value;
                    break;
                }
            }

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

            xAttr.Value = nx.ToString("F6", CultureInfo.InvariantCulture);
            yAttr.Value = ny.ToString("F6", CultureInfo.InvariantCulture);

            // Scale Offset if present on WASD or joystick.
            var offsetAttr = node.Attribute("Offset");
            if (offsetAttr != null &&
                double.TryParse(offsetAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double origOffset))
            {
                double sx = CoordinateTransformService.CalculateHorizontalScaleFactor(targetWidth, targetHeight, baseW, baseH);
                double newOffset = Math.Clamp(origOffset * sx, 0.04, 0.18);
                offsetAttr.Value = newOffset.ToString("F6", CultureInfo.InvariantCulture);
            }
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
                    return SanitizeStockBase(text, c);
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
                    var repaired = SanitizeStockBase(text, f);

                    try
                    {
                        var stockDest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "stock_16_9_DefaultKeyMapping.xml");
                        await File.WriteAllTextAsync(stockDest, repaired);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("GameLoopMappingProvider", $"Could not cache stock base: {ex.Message}");
                    }

                    return repaired;
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Repairs known GameLoop-emitted keymap defects and verifies the result parses as XML
    /// before it is allowed to become the cached "pristine stock base".
    ///
    /// Without this, a single malformed capture is cached permanently and every subsequent
    /// calibration fails validation - which is exactly what happened on 2026-10-11.
    /// </summary>
    private static string SanitizeStockBase(string text, string sourcePath)
    {
        var repaired = KeymapXmlSanitizer.Sanitize(text, out var info);

        if (info.Changed)
        {
            Logger.Warn("GameLoopMappingProvider",
                $"Repaired malformed keymap stock base from '{sourcePath}' ({info.Summary}).");
        }

        if (!KeymapXmlSanitizer.IsWellFormed(repaired))
        {
            Logger.Error("GameLoopMappingProvider",
                $"Keymap stock base '{sourcePath}' is still malformed after repair and will be rejected.");
            return string.Empty;
        }

        return repaired;
    }
}
