using System.Globalization;

namespace GameLoopOptimizer.Core;

public enum ControlAnchorType
{
    Left,
    Right,
    Center,
    TopRight,
    BottomCenter,
    Custom
}

public class ViewportMetrics
{
    public int ClientWidth { get; set; } = 1920;
    public int ClientHeight { get; set; } = 1080;
    public int ViewportX { get; set; }
    public int ViewportY { get; set; }
    public int ViewportWidth { get; set; } = 1920;
    public int ViewportHeight { get; set; } = 1080;
    public bool HasLetterbox => ViewportY > 0 || ViewportHeight < ClientHeight;
    public bool HasPillarbox => ViewportX > 0 || ViewportWidth < ClientWidth;
}

public class MappingEnvironment
{
    public int DesktopWidth { get; set; } = 1920;
    public int DesktopHeight { get; set; } = 1080;
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public int WindowWidth { get; set; } = 1920;
    public int WindowHeight { get; set; } = 1080;
    public int ClientOriginX { get; set; }
    public int ClientOriginY { get; set; }
    public int ClientWidth { get; set; } = 1920;
    public int ClientHeight { get; set; } = 1080;
    public double DpiScale { get; set; } = 1.0;
    public uint DpiValue { get; set; } = 96;
    public string AspectRatioLabel { get; set; } = "16:9 Standard";
    public string GameLoopVersion { get; set; } = "Unknown";
    public string ActivePubgPackage { get; set; } = "com.tencent.ig_ss";
    public int ActiveModeId { get; set; } = 3; // 2 = 720p, 3 = 1080p, 4 = 2k
}

public static class CoordinateTransformService
{
    public const int ReferenceWidth = 1920;
    public const int ReferenceHeight = 1080;
    public const double ReferenceAspectRatio = 1920.0 / 1080.0;

    /// <summary>
    /// Computes horizontal scale factor Sx from 16:9 baseline for target width and height.
    /// </summary>
    public static double CalculateHorizontalScaleFactor(int targetWidth, int targetHeight, int baseWidth = ReferenceWidth, int baseHeight = ReferenceHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0) return 1.0;
        double baseRatio = (double)baseWidth / baseHeight;
        double targetRatio = (double)targetWidth / targetHeight;
        return baseRatio / targetRatio;
    }

    /// <summary>
    /// Calculates viewport bounds with optional letterboxing/pillarboxing compensation.
    /// </summary>
    public static ViewportMetrics CalculateViewport(int clientWidth, int clientHeight, double targetAspectRatio)
    {
        var v = new ViewportMetrics
        {
            ClientWidth = clientWidth,
            ClientHeight = clientHeight
        };

        if (clientWidth <= 0 || clientHeight <= 0 || targetAspectRatio <= 0)
        {
            v.ViewportWidth = clientWidth;
            v.ViewportHeight = clientHeight;
            return v;
        }

        double clientRatio = (double)clientWidth / clientHeight;

        if (Math.Abs(clientRatio - targetAspectRatio) < 0.01)
        {
            // Direct fit
            v.ViewportX = 0;
            v.ViewportY = 0;
            v.ViewportWidth = clientWidth;
            v.ViewportHeight = clientHeight;
        }
        else if (clientRatio > targetAspectRatio)
        {
            // Pillarboxed (bars on left and right)
            int vpWidth = (int)Math.Round(clientHeight * targetAspectRatio);
            int offsetX = (clientWidth - vpWidth) / 2;
            v.ViewportX = offsetX;
            v.ViewportY = 0;
            v.ViewportWidth = vpWidth;
            v.ViewportHeight = clientHeight;
        }
        else
        {
            // Letterboxed (bars on top and bottom)
            int vpHeight = (int)Math.Round(clientWidth / targetAspectRatio);
            int offsetY = (clientHeight - vpHeight) / 2;
            v.ViewportX = 0;
            v.ViewportY = offsetY;
            v.ViewportWidth = clientWidth;
            v.ViewportHeight = vpHeight;
        }

        return v;
    }

    /// <summary>
    /// Transforms normalized (0.0 - 1.0) coordinates using physical HUD anchor mathematics.
    /// </summary>
    public static (double newX, double newY) TransformCoordinate(double normX, double normY, int targetWidth, int targetHeight, ControlAnchorType anchor, int baseWidth = ReferenceWidth, int baseHeight = ReferenceHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0) return (normX, normY);

        double sx = CalculateHorizontalScaleFactor(targetWidth, targetHeight, baseWidth, baseHeight);

        // If target ratio is virtually identical to baseline (e.g. 1920x1080 and 2560x1440 16:9), preserve exact normalized coords
        if (Math.Abs(sx - 1.0) < 0.005)
        {
            return (Math.Clamp(normX, 0.01, 0.99), Math.Clamp(normY, 0.01, 0.99));
        }

        double newX;
        double newY = normY;

        switch (anchor)
        {
            case ControlAnchorType.Left:
                newX = normX * sx;
                break;

            case ControlAnchorType.Right:
            case ControlAnchorType.TopRight:
                double distFromRight = 1.0 - normX;
                newX = 1.0 - (distFromRight * sx);
                break;

            case ControlAnchorType.BottomCenter:
            case ControlAnchorType.Center:
                double offsetFromCenter = normX - 0.5;
                newX = 0.5 + (offsetFromCenter * sx);
                break;

            case ControlAnchorType.Custom:
            default:
                newX = ClassifyAndTransform(normX, normY, sx);
                break;
        }

        newX = Math.Clamp(newX, 0.01, 0.99);
        newY = Math.Clamp(newY, 0.01, 0.99);

        return (Math.Round(newX, 6), Math.Round(newY, 6));
    }

    /// <summary>
    /// Automatic anchor classification based on HUD layout ergonomics and screen quadrants.
    /// </summary>
    public static ControlAnchorType DetectAnchorType(string controlName, double x, double y)
    {
        string name = controlName.Trim().ToLowerInvariant();

        // 1. Explicit key and action rules
        if (name is "wasd" or "movement" or "joystick" or "sprint" or "bag" or "backpack" or "tab")
        {
            return ControlAnchorType.Left;
        }

        if (name is "fire" or "lclick" or "attack")
        {
            return x < 0.4 ? ControlAnchorType.Left : ControlAnchorType.Right;
        }

        if (name is "ads" or "scope" or "rclick" or "jump" or "space" or "crouch" or "c" or "prone" or "z" or "reload" or "r" or "q" or "e" or "peek" or "peekleft" or "peekright")
        {
            return ControlAnchorType.Right;
        }

        if (name is "map" or "m" or "settings" or "cancel")
        {
            return ControlAnchorType.TopRight;
        }

        if (name is "1" or "2" or "3" or "weapon1" or "weapon2" or "pistol" or "holster" or "grenade" or "melee")
        {
            return ControlAnchorType.BottomCenter;
        }

        // 2. Fallback to geometric heuristics
        if (y < 0.25 && x > 0.70) return ControlAnchorType.TopRight;
        if (y > 0.80 && x >= 0.35 && x <= 0.65) return ControlAnchorType.BottomCenter;
        if (x <= 0.38) return ControlAnchorType.Left;
        if (x >= 0.62) return ControlAnchorType.Right;

        return ControlAnchorType.Center;
    }

    private static double ClassifyAndTransform(double normX, double normY, double sx)
    {
        if (normX <= 0.38)
        {
            return normX * sx;
        }
        else if (normX >= 0.62)
        {
            double distFromRight = 1.0 - normX;
            return 1.0 - (distFromRight * sx);
        }
        else
        {
            double offsetFromCenter = normX - 0.5;
            return 0.5 + (offsetFromCenter * sx);
        }
    }

    /// <summary>
    /// Converts normalized coordinate (0.0 to 1.0) into GameLoop client pixel coordinate.
    /// </summary>
    public static (int clientX, int clientY) NormalizedToClientPixels(double normX, double normY, ViewportMetrics viewport)
    {
        int cx = viewport.ViewportX + (int)Math.Round(normX * viewport.ViewportWidth);
        int cy = viewport.ViewportY + (int)Math.Round(normY * viewport.ViewportHeight);
        return (cx, cy);
    }

    /// <summary>
    /// Converts GameLoop client pixel coordinate to physical Windows screen coordinate.
    /// </summary>
    public static (int screenX, int screenY) ClientPixelsToScreen(int clientX, int clientY, GameLoopWindowInfo windowInfo)
    {
        return (windowInfo.ClientOriginX + clientX, windowInfo.ClientOriginY + clientY);
    }

    /// <summary>
    /// Converts physical Windows screen coordinate back to normalized coordinate inside the viewport.
    /// </summary>
    public static (double normX, double normY) ScreenToNormalized(int screenX, int screenY, GameLoopWindowInfo windowInfo, ViewportMetrics viewport)
    {
        int clientX = screenX - windowInfo.ClientOriginX;
        int clientY = screenY - windowInfo.ClientOriginY;

        int vpX = clientX - viewport.ViewportX;
        int vpY = clientY - viewport.ViewportY;

        double nx = viewport.ViewportWidth > 0 ? (double)vpX / viewport.ViewportWidth : 0;
        double ny = viewport.ViewportHeight > 0 ? (double)vpY / viewport.ViewportHeight : 0;

        return (Math.Clamp(nx, 0.0, 1.0), Math.Clamp(ny, 0.0, 1.0));
    }
}
