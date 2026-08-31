using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class KeymapSpeedTests
{
    [Fact]
    public void KeymapSpeedService_InjectWasdSpeed_UpdatesExistingSpeedAttributes()
    {
        // Arrange
        string xml = @"<KeyMapping>
  <Item ApkName=""com.tencent.ig"" Mode=""Rocker"" Speed=""80"" Point_X=""0.25"" Point_Y=""0.75""/>
  <Item ApkName=""com.pubg.krmobile"" Mode=""Rocker"" Speed=""90"" Point_X=""0.25"" Point_Y=""0.75""/>
</KeyMapping>";

        // Act
        var (updatedXml, nodesChanged) = KeymapSpeedService.InjectWasdSpeed(xml, 100);

        // Assert
        Assert.Equal(2, nodesChanged);
        Assert.Contains("Speed=\"100\"", updatedXml);
        Assert.DoesNotContain("Speed=\"80\"", updatedXml);
        Assert.DoesNotContain("Speed=\"90\"", updatedXml);
    }

    [Fact]
    public void KeymapSpeedService_InjectWasdSpeed_InjectsSpeedIntoRockerIfMissing()
    {
        // Arrange
        string xml = @"<KeyMapping>
  <KeyMap Item=""WASD"" Point_X=""0.25"" Point_Y=""0.75"" />
  <Rocker Mode=""Rocker"" CenterX=""0.2"" CenterY=""0.7"" />
</KeyMapping>";

        // Act
        var (updatedXml, nodesChanged) = KeymapSpeedService.InjectWasdSpeed(xml, 100);

        // Assert
        Assert.True(nodesChanged >= 1);
        Assert.Contains("Speed=\"100\"", updatedXml);
    }

    [Fact]
    public void KeymapSpeedService_InjectWasdSpeed_ClampsSpeedToSafeRange()
    {
        string xml = @"<Item ApkName=""com.tencent.ig"" Mode=""Rocker"" Speed=""80""/>";

        // Act & Assert 1: > 100 clamps to 100
        var (clampedMaxXml, _) = KeymapSpeedService.InjectWasdSpeed(xml, 150);
        Assert.Contains("Speed=\"100\"", clampedMaxXml);

        // Act & Assert 2: < 50 clamps to 50
        var (clampedMinXml, _) = KeymapSpeedService.InjectWasdSpeed(xml, 20);
        Assert.Contains("Speed=\"50\"", clampedMinXml);
    }

    [Fact]
    public void ResolutionKeymapService_CalibrateKeymapXml_IntegratesWasdSpeed()
    {
        // Arrange: 16:9 stock XML containing coordinates and default speed
        string stockXml = @"<KeyMapping>
  <Item ApkName=""com.tencent.ig"" Mode=""Rocker"" Speed=""80"">
    <KeyMap Point_X=""0.200000"" Point_Y=""0.750000"" />
    <KeyMap Point_X=""0.800000"" Point_Y=""0.600000"" />
  </Item>
</KeyMapping>";

        // Act: Calibrate for 4:3 stretched (1440x1080) with 100% WASD speed
        var (calibratedXml, keysCalibrated) = ResolutionKeymapService.CalibrateKeymapXml(stockXml, 1440, 1080, 100);

        // Assert
        Assert.Equal(2, keysCalibrated);
        Assert.Contains("Speed=\"100\"", calibratedXml);
        Assert.DoesNotContain("Speed=\"80\"", calibratedXml);
        Assert.Contains("Point_X=\"0.266667\"", calibratedXml); // 0.20 * (1920/1440) = 0.266667
    }
}
