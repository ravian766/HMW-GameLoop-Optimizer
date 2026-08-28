using System.Globalization;
using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class ResolutionKeymapTests
{
    [Fact]
    public void CalibrateCoordinate_Native1080p_ReturnsExactCoordinates()
    {
        var (x, y) = ResolutionKeymapService.CalibrateCoordinate(0.85, 0.75, 1920, 1080);
        Assert.Equal(0.85, x);
        Assert.Equal(0.75, y);
    }

    [Theory]
    [InlineData(1440, 1080)] // 4:3 Stretched
    [InlineData(1728, 1080)] // 16:10 Stretched
    [InlineData(1080, 1080)] // 1:1 Box Stretched
    [InlineData(1280, 960)]  // 4:3 Low-End
    [InlineData(2560, 1440)] // 16:9 2K
    public void CalibrateCoordinate_AllPresets_StayWithinSafeScreenBounds(int width, int height)
    {
        double[] testX = { 0.05, 0.12, 0.25, 0.45, 0.50, 0.55, 0.75, 0.88, 0.96 };
        double[] testY = { 0.10, 0.30, 0.50, 0.70, 0.85, 0.95 };

        foreach (var ox in testX)
        {
            foreach (var oy in testY)
            {
                var (nx, ny) = ResolutionKeymapService.CalibrateCoordinate(ox, oy, width, height);

                Assert.InRange(nx, 0.01, 0.99);
                Assert.InRange(ny, 0.01, 0.99);
            }
        }
    }

    [Fact]
    public void CalibrateCoordinate_43Stretched_AnchorMathCalculatesCorrectPixelOffsets()
    {
        // 1. Left zone control (Joystick at 0.12 on 1920 is 230.4px. On 1440 screen, 230.4 / 1440 = 0.160)
        var (leftX, _) = ResolutionKeymapService.CalibrateCoordinate(0.12, 0.75, 1440, 1080);
        Assert.Equal(0.160, Math.Round(leftX, 3));

        // 2. Right zone control (Scope at 0.95 on 1920 is 96px from right edge. On 1440 screen, (1440-96)/1440 = 0.9333)
        var (rightX, _) = ResolutionKeymapService.CalibrateCoordinate(0.95, 0.65, 1440, 1080);
        Assert.Equal(0.933, Math.Round(rightX, 3));

        // 3. Center control (Weapon slot at exactly 0.50 remains centered at 0.50)
        var (centerX, _) = ResolutionKeymapService.CalibrateCoordinate(0.50, 0.90, 1440, 1080);
        Assert.Equal(0.50, Math.Round(centerX, 2));

        // 4. Center-left offset (Weapon 1 at 0.45 on 1920 is 96px left of center. On 1440, (720-96)/1440 = 0.4333)
        var (weaponX, _) = ResolutionKeymapService.CalibrateCoordinate(0.45, 0.90, 1440, 1080);
        Assert.Equal(0.433, Math.Round(weaponX, 3));
    }

    [Fact]
    public void CalibrateKeymapXml_TransformsPubgBindingsAndCountsAccurately()
    {
        string sampleXml = @"<root>
<Item ApkName=""com.tencent.ig_ss"" 备注=""PUBG Mobile"">
    <KeyMapMode ModeID=""1"" Name=""HD 1080P"">
        <KeyMapping ItemName=""Space"" Point_X=""0.950000"" Point_Y=""0.750000"" Description=""Jump"" AsciiCode=""32""/>
        <KeyMapping ItemName=""C"" Point_X=""0.850000"" Point_Y=""0.850000"" Description=""Crouch"" AsciiCode=""67""/>
        <KeyMapping ItemName=""Z"" Point_X=""0.950000"" Point_Y=""0.920000"" Description=""Prone"" AsciiCode=""90""/>
        <KeyMappingEx ItemName=""WASD"" Point_X=""0.120000"" Point_Y=""0.750000"" Description=""Movement"" Type=""CrossKey"" Offset=""0.087805""/>
    </KeyMapMode>
</Item>
<Item ApkName=""com.other.game"" 备注=""Other Game"">
    <KeyMapping ItemName=""K"" Point_X=""0.500000"" Point_Y=""0.500000""/>
</Item>
</root>";

        var (calibratedXml, count) = ResolutionKeymapService.CalibrateKeymapXml(sampleXml, 1440, 1080);

        Assert.Equal(4, count); // 4 PUBG bindings updated, other game skipped
        Assert.Contains("com.tencent.ig_ss", calibratedXml);
        Assert.Contains("Point_X=", calibratedXml);
        Assert.Contains("Point_Y=", calibratedXml);
    }

    [Fact]
    public void CalibrateKeymapXml_IgnoresEmptyOrNullGracefully()
    {
        var (emptyRes, emptyCount) = ResolutionKeymapService.CalibrateKeymapXml("", 1440, 1080);
        Assert.Equal("", emptyRes);
        Assert.Equal(0, emptyCount);
    }
}
