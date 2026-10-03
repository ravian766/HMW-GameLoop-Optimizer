using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class CoordinateTransformTests
{
    [Theory]
    [InlineData(1920, 1080, 1.0)]       // 16:9 native
    [InlineData(2560, 1440, 1.0)]       // 16:9 2K
    [InlineData(3840, 2160, 1.0)]       // 16:9 4K
    [InlineData(1280, 720, 1.0)]        // 16:9 720p
    [InlineData(1440, 1080, 1.333333)]  // 4:3 stretched (16/9) / (4/3) = 1.333333
    [InlineData(1728, 1080, 1.111111)]  // 16:10 stretched (16/9) / (16/10) = 1.111111
    [InlineData(1080, 1080, 1.777778)]  // 1:1 stretched (16/9) / 1.0 = 1.777778
    [InlineData(2560, 1080, 0.75)]      // 21:9 ultrawide (16/9) / (21.33/9)
    public void CalculateHorizontalScaleFactor_ComputesAccurateRatios(int width, int height, double expectedRatio)
    {
        double sx = CoordinateTransformService.CalculateHorizontalScaleFactor(width, height);
        Assert.Equal(expectedRatio, Math.Round(sx, 6));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1920, 1080)]
    public void CalculateHorizontalScaleFactor_InvalidDimensions_ReturnsDefaultOne(int width, int height)
    {
        double sx = CoordinateTransformService.CalculateHorizontalScaleFactor(width, height);
        Assert.Equal(1.0, sx);
    }

    [Fact]
    public void CalculateViewport_ExactAspectRatio_NoBars()
    {
        var vp = CoordinateTransformService.CalculateViewport(1920, 1080, 16.0 / 9.0);

        Assert.Equal(1920, vp.ViewportWidth);
        Assert.Equal(1080, vp.ViewportHeight);
        Assert.Equal(0, vp.ViewportX);
        Assert.Equal(0, vp.ViewportY);
        Assert.False(vp.HasLetterbox);
        Assert.False(vp.HasPillarbox);
    }

    [Fact]
    public void CalculateViewport_UltrawideClient_PillarboxBarsOnLeftAndRight()
    {
        // 2560x1080 window displaying 16:9 content
        var vp = CoordinateTransformService.CalculateViewport(2560, 1080, 16.0 / 9.0);

        Assert.Equal(1920, vp.ViewportWidth);
        Assert.Equal(1080, vp.ViewportHeight);
        Assert.Equal(320, vp.ViewportX); // (2560 - 1920) / 2 = 320
        Assert.Equal(0, vp.ViewportY);
        Assert.False(vp.HasLetterbox);
        Assert.True(vp.HasPillarbox);
    }

    [Fact]
    public void CalculateViewport_TallClient_LetterboxBarsOnTopAndBottom()
    {
        // 1920x1440 window displaying 16:9 content
        var vp = CoordinateTransformService.CalculateViewport(1920, 1440, 16.0 / 9.0);

        Assert.Equal(1920, vp.ViewportWidth);
        Assert.Equal(1080, vp.ViewportHeight);
        Assert.Equal(0, vp.ViewportX);
        Assert.Equal(180, vp.ViewportY); // (1440 - 1080) / 2 = 180
        Assert.True(vp.HasLetterbox);
        Assert.False(vp.HasPillarbox);
    }

    [Fact]
    public void TransformCoordinate_Native16x9_PreservesExactNormalizedCoords()
    {
        var (x, y) = CoordinateTransformService.TransformCoordinate(0.85, 0.75, 1920, 1080, ControlAnchorType.Right);
        Assert.Equal(0.85, x);
        Assert.Equal(0.75, y);

        var (x2k, y2k) = CoordinateTransformService.TransformCoordinate(0.85, 0.75, 2560, 1440, ControlAnchorType.Right);
        Assert.Equal(0.85, x2k);
        Assert.Equal(0.75, y2k);
    }

    [Fact]
    public void TransformCoordinate_LeftAnchor_ScalesFromLeftEdge()
    {
        // Joystick at 0.12 on 16:9. On 4:3 stretched (sx = 1.333333), newX = 0.12 * 1.333333 = 0.160
        var (x, y) = CoordinateTransformService.TransformCoordinate(0.12, 0.75, 1440, 1080, ControlAnchorType.Left);
        Assert.Equal(0.160, Math.Round(x, 3));
        Assert.Equal(0.75, y);
    }

    [Fact]
    public void TransformCoordinate_RightAnchor_MaintainsDistanceFromRightEdge()
    {
        // Scope at 0.95 (distance from right = 0.05). On 4:3 (sx = 1.333333), dist becomes 0.05 * 1.333333 = 0.066667
        // newX = 1 - 0.066667 = 0.933333
        var (x, y) = CoordinateTransformService.TransformCoordinate(0.95, 0.65, 1440, 1080, ControlAnchorType.Right);
        Assert.Equal(0.933333, Math.Round(x, 6));
        Assert.Equal(0.65, y);
    }

    [Fact]
    public void TransformCoordinate_CenterAnchor_StaysCenteredOrOffsetsFromCenter()
    {
        // Exactly at center 0.5
        var (centerX, _) = CoordinateTransformService.TransformCoordinate(0.50, 0.90, 1440, 1080, ControlAnchorType.Center);
        Assert.Equal(0.50, centerX);

        // Offset 0.05 to the left (0.45): offset is -0.05 * 1.333333 = -0.066667 -> 0.5 - 0.066667 = 0.433333
        var (offX, _) = CoordinateTransformService.TransformCoordinate(0.45, 0.90, 1440, 1080, ControlAnchorType.Center);
        Assert.Equal(0.433333, Math.Round(offX, 6));
    }

    [Theory]
    [InlineData("WASD", ControlAnchorType.Left)]
    [InlineData("movement", ControlAnchorType.Left)]
    [InlineData("Sprint", ControlAnchorType.Left)]
    [InlineData("Tab", ControlAnchorType.Left)]
    [InlineData("backpack", ControlAnchorType.Left)]
    [InlineData("Scope", ControlAnchorType.Right)]
    [InlineData("ADS", ControlAnchorType.Right)]
    [InlineData("RClick", ControlAnchorType.Right)]
    [InlineData("Space", ControlAnchorType.Right)]
    [InlineData("Jump", ControlAnchorType.Right)]
    [InlineData("Crouch", ControlAnchorType.Right)]
    [InlineData("Prone", ControlAnchorType.Right)]
    [InlineData("Reload", ControlAnchorType.Right)]
    [InlineData("Q", ControlAnchorType.Right)]
    [InlineData("E", ControlAnchorType.Right)]
    [InlineData("PeekLeft", ControlAnchorType.Right)]
    [InlineData("PeekRight", ControlAnchorType.Right)]
    [InlineData("Map", ControlAnchorType.TopRight)]
    [InlineData("M", ControlAnchorType.TopRight)]
    [InlineData("Settings", ControlAnchorType.TopRight)]
    [InlineData("Weapon1", ControlAnchorType.BottomCenter)]
    [InlineData("Weapon2", ControlAnchorType.BottomCenter)]
    [InlineData("1", ControlAnchorType.BottomCenter)]
    [InlineData("2", ControlAnchorType.BottomCenter)]
    public void DetectAnchorType_IdentifiesNamedControlsCorrectly(string actionName, ControlAnchorType expectedAnchor)
    {
        var anchor = CoordinateTransformService.DetectAnchorType(actionName, 0.5, 0.5);
        Assert.Equal(expectedAnchor, anchor);
    }

    [Fact]
    public void DetectAnchorType_FallbackQuadrantHeuristics()
    {
        // Top right quadrant
        Assert.Equal(ControlAnchorType.TopRight, CoordinateTransformService.DetectAnchorType("UnknownButton", 0.85, 0.15));

        // Bottom center quadrant
        Assert.Equal(ControlAnchorType.BottomCenter, CoordinateTransformService.DetectAnchorType("UnknownButton", 0.50, 0.88));

        // Left quadrant
        Assert.Equal(ControlAnchorType.Left, CoordinateTransformService.DetectAnchorType("UnknownButton", 0.20, 0.50));

        // Right quadrant
        Assert.Equal(ControlAnchorType.Right, CoordinateTransformService.DetectAnchorType("UnknownButton", 0.80, 0.50));

        // Dead center
        Assert.Equal(ControlAnchorType.Center, CoordinateTransformService.DetectAnchorType("UnknownButton", 0.50, 0.50));
    }

    [Fact]
    public void CoordinateConversions_ClientScreenNormalized_RoundTripAccurately()
    {
        var vp = new ViewportMetrics
        {
            ClientWidth = 1920,
            ClientHeight = 1080,
            ViewportX = 0,
            ViewportY = 0,
            ViewportWidth = 1920,
            ViewportHeight = 1080
        };

        var win = new GameLoopWindowInfo
        {
            ClientOriginX = 100,
            ClientOriginY = 200,
            ClientWidth = 1920,
            ClientHeight = 1080
        };

        // 1. Normalized to Client Pixels
        var (cx, cy) = CoordinateTransformService.NormalizedToClientPixels(0.50, 0.50, vp);
        Assert.Equal(960, cx);
        Assert.Equal(540, cy);

        // 2. Client to Screen
        var (sx, sy) = CoordinateTransformService.ClientPixelsToScreen(cx, cy, win);
        Assert.Equal(1060, sx);
        Assert.Equal(740, sy);

        // 3. Screen back to Normalized
        var (nx, ny) = CoordinateTransformService.ScreenToNormalized(sx, sy, win, vp);
        Assert.Equal(0.50, nx);
        Assert.Equal(0.50, ny);
    }
}
