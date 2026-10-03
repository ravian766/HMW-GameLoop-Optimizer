using System.Runtime.InteropServices;

namespace GameLoopOptimizer.Core;

public class DpiInfo
{
    public uint DpiValue { get; set; } = 96;
    public double ScaleFactor => Math.Round(DpiValue / 96.0, 2);
    public int ScalePercentage => (int)Math.Round(ScaleFactor * 100);

    public override string ToString() => $"{ScalePercentage}% ({DpiValue} DPI)";
}

public static class DpiDetector
{
    public const uint StandardDpi = 96;

    /// <summary>
    /// Detects DPI for a specific window handle with fallback to system DPI.
    /// </summary>
    public static DpiInfo GetDpiForWindow(IntPtr hWnd)
    {
        uint dpi = 0;
        if (hWnd != IntPtr.Zero)
        {
            try
            {
                dpi = NativeMethods.GetDpiForWindow(hWnd);
            }
            catch { }
        }

        if (dpi == 0)
        {
            try
            {
                dpi = NativeMethods.GetDpiForSystem();
            }
            catch { }
        }

        if (dpi == 0)
        {
            dpi = StandardDpi;
        }

        return new DpiInfo { DpiValue = dpi };
    }

    /// <summary>
    /// Gets global system default DPI info.
    /// </summary>
    public static DpiInfo GetSystemDpi()
    {
        uint dpi = 0;
        try
        {
            dpi = NativeMethods.GetDpiForSystem();
        }
        catch { }

        if (dpi == 0) dpi = StandardDpi;
        return new DpiInfo { DpiValue = dpi };
    }

    public static double PhysicalToLogical(double physicalPixels, double scaleFactor)
    {
        if (scaleFactor <= 0) return physicalPixels;
        return physicalPixels / scaleFactor;
    }

    public static double LogicalToPhysical(double logicalPixels, double scaleFactor)
    {
        if (scaleFactor <= 0) return logicalPixels;
        return logicalPixels * scaleFactor;
    }

    public static double DipToPhysical(double dip, uint dpi)
    {
        return dip * (dpi / (double)StandardDpi);
    }

    public static double PhysicalToDip(double physical, uint dpi)
    {
        if (dpi == 0) return physical;
        return physical * ((double)StandardDpi / dpi);
    }
}
