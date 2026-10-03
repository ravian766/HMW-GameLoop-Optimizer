using System.IO;
using System.Xml.Linq;

namespace GameLoopOptimizer.Core;

public class MappingValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public int ValidatedControlCount { get; set; }
    public string Summary => IsValid
        ? $"Validation passed: {ValidatedControlCount} controls validated ({Warnings.Count} warnings)."
        : $"Validation failed with {Errors.Count} error(s): {string.Join("; ", Errors)}";
}

public static class MappingValidator
{
    public const double MinNormalizedBound = 0.005;
    public const double MaxNormalizedBound = 0.995;

    public static MappingValidationResult ValidateCoordinates(IEnumerable<(string name, double x, double y)> controls)
    {
        var result = new MappingValidationResult();
        int count = 0;

        foreach (var (name, x, y) in controls)
        {
            count++;

            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                result.Errors.Add($"Control '{name}' has invalid NaN/Infinity coordinate ({x}, {y}).");
                continue;
            }

            if (x < 0.0 || x > 1.0)
            {
                result.Errors.Add($"Control '{name}' X coordinate ({x:F4}) is outside [0.0, 1.0].");
            }
            else if (x < MinNormalizedBound || x > MaxNormalizedBound)
            {
                result.Warnings.Add($"Control '{name}' X coordinate ({x:F4}) is dangerously close to screen edge.");
            }

            if (y < 0.0 || y > 1.0)
            {
                result.Errors.Add($"Control '{name}' Y coordinate ({y:F4}) is outside [0.0, 1.0].");
            }
            else if (y < MinNormalizedBound || y > MaxNormalizedBound)
            {
                result.Warnings.Add($"Control '{name}' Y coordinate ({y:F4}) is dangerously close to screen edge.");
            }
        }

        result.ValidatedControlCount = count;
        return result;
    }

    public static MappingValidationResult ValidateResolution(int width, int height)
    {
        var result = new MappingValidationResult();

        if (width < 640 || width > 7680)
        {
            result.Errors.Add($"Target resolution width ({width}px) is outside supported range [640, 7680].");
        }

        if (height < 480 || height > 4320)
        {
            result.Errors.Add($"Target resolution height ({height}px) is outside supported range [480, 4320].");
        }

        if (width > 0 && height > 0)
        {
            double ratio = (double)width / height;
            if (ratio < 0.75 || ratio > 3.6)
            {
                result.Warnings.Add($"Unusual aspect ratio ({ratio:F2}:1). Standard ratios range from 4:3 (1.33) to 32:9 (3.55).");
            }
        }

        return result;
    }

    public static bool ValidateXmlFragment(string xmlContent, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            errorMessage = "XML content is empty.";
            return false;
        }

        try
        {
            // Wrap in dummy root to validate multi-root GameLoop fragments
            string wrapped = $"<root>{xmlContent}</root>";
            XDocument.Parse(wrapped);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"XML parsing error: {ex.Message}";
            return false;
        }
    }
}
