using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class DpiDetectorTests
{
    [Theory]
    [InlineData(96u, 1.0, 100)]
    [InlineData(120u, 1.25, 125)]
    [InlineData(144u, 1.50, 150)]
    [InlineData(168u, 1.75, 175)]
    [InlineData(192u, 2.00, 200)]
    public void DpiInfo_CalculatesScaleFactorAndPercentage(uint dpi, double expectedScale, int expectedPercent)
    {
        var info = new DpiInfo { DpiValue = dpi };

        Assert.Equal(expectedScale, info.ScaleFactor);
        Assert.Equal(expectedPercent, info.ScalePercentage);
        Assert.Contains($"{expectedPercent}%", info.ToString());
    }

    [Fact]
    public void PhysicalAndLogicalConversions_RoundTripAccurately()
    {
        double scale = 1.5;
        double physical = 1500.0;

        double logical = DpiDetector.PhysicalToLogical(physical, scale);
        Assert.Equal(1000.0, logical);

        double roundTrip = DpiDetector.LogicalToPhysical(logical, scale);
        Assert.Equal(1500.0, roundTrip);
    }

    [Fact]
    public void PhysicalAndLogicalConversions_ZeroOrNegativeScale_ReturnsOriginal()
    {
        Assert.Equal(100.0, DpiDetector.PhysicalToLogical(100.0, 0));
        Assert.Equal(100.0, DpiDetector.LogicalToPhysical(100.0, -1.0));
    }

    [Fact]
    public void DipAndPhysicalConversions_RoundTripAccurately()
    {
        uint dpi = 144; // 150%
        double dip = 100.0;

        double physical = DpiDetector.DipToPhysical(dip, dpi);
        Assert.Equal(150.0, physical);

        double backToDip = DpiDetector.PhysicalToDip(physical, dpi);
        Assert.Equal(100.0, backToDip);
    }

    [Fact]
    public void GameLoopWindowInfo_MetricsCalculation_BehavesCorrectly()
    {
        var win = new GameLoopWindowInfo
        {
            WindowX = 100,
            WindowY = 100,
            WindowWidth = 1936,
            WindowHeight = 1119,
            ClientOriginX = 108,
            ClientOriginY = 131,
            ClientWidth = 1920,
            ClientHeight = 1080,
            MonitorWidth = 1920,
            MonitorHeight = 1080
        };

        Assert.Equal(31, win.TitleBarHeight); // 131 - 100 = 31
        Assert.Equal(8, win.BorderWidth);     // 108 - 100 = 8
        Assert.Equal(16.0 / 9.0, win.AspectRatio, 4);
    }
}
