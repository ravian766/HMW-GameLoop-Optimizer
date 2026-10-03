using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class GameLoopMappingProviderTests
{
    [Fact]
    public void TransformKeymapXml_PreservesSiblingAttributesWithoutDestruction()
    {
        string sampleXml = @"
<Item ApkName=""com.tencent.ig"" Description=""PUBG Global"">
    <KeyMapMode ModeID=""3"" Name=""HD 1080P"">
        <KeyMapping ItemName=""Space"" Mode=""1"" Point_X=""0.950000"" Point_Y=""0.750000"" Description=""Jump"" AsciiCode=""32"" TexturePosition=""0.95,0.75"" Delay=""15""/>
        <KeyMappingEx ItemName=""WASD"" Mode=""2"" Point_X=""0.120000"" Point_Y=""0.750000"" Description=""Movement"" Type=""CrossKey"" Offset=""0.087805"" SwitchOperation=""True""/>
    </KeyMapMode>
</Item>";

        var (transformed, count) = GameLoopMappingProvider.TransformKeymapXml(sampleXml, 1440, 1080);

        Assert.Equal(2, count);

        // Verify preserved sibling attributes
        Assert.Contains("Mode=\"1\"", transformed);
        Assert.Contains("AsciiCode=\"32\"", transformed);
        Assert.Contains("TexturePosition=\"0.95,0.75\"", transformed);
        Assert.Contains("Delay=\"15\"", transformed);
        Assert.Contains("Mode=\"2\"", transformed);
        Assert.Contains("Type=\"CrossKey\"", transformed);
        Assert.Contains("SwitchOperation=\"True\"", transformed);

        // Verify coordinates were updated for 4:3 stretched
        Assert.DoesNotContain("Point_X=\"0.950000\"", transformed);
        Assert.DoesNotContain("Point_X=\"0.120000\"", transformed);
    }

    [Fact]
    public void TransformKeymapXml_ProcessesAllSupportedPubgPackages()
    {
        string sampleXml = @"
<Item ApkName=""com.tencent.ig""><KeyMapping ItemName=""Space"" Point_X=""0.95"" Point_Y=""0.75""/></Item>
<Item ApkName=""com.pubg.krmobile""><KeyMapping ItemName=""Space"" Point_X=""0.95"" Point_Y=""0.75""/></Item>
<Item ApkName=""com.vng.pubgmobile""><KeyMapping ItemName=""Space"" Point_X=""0.95"" Point_Y=""0.75""/></Item>
<Item ApkName=""com.pubg.imobile""><KeyMapping ItemName=""Space"" Point_X=""0.95"" Point_Y=""0.75""/></Item>
<Item ApkName=""com.other.unrelated""><KeyMapping ItemName=""Space"" Point_X=""0.95"" Point_Y=""0.75""/></Item>";

        var (transformed, count) = GameLoopMappingProvider.TransformKeymapXml(sampleXml, 1440, 1080);

        Assert.Equal(4, count); // All 4 PUBG packages updated, unrelated skipped
        Assert.Contains("com.other.unrelated", transformed);
    }

    [Fact]
    public void TransformKeymapXml_SupportsDiverseTagNames()
    {
        string sampleXml = @"
<Item ApkName=""com.tencent.ig"">
    <Key Point_X=""0.900000"" Point_Y=""0.500000"" ItemName=""Action1""/>
    <KeyMap Point_X=""0.800000"" Point_Y=""0.600000"" ItemName=""Action2""/>
    <KeyMapping Point_X=""0.700000"" Point_Y=""0.700000"" ItemName=""Action3""/>
    <KeyMappingEx Point_X=""0.600000"" Point_Y=""0.800000"" ItemName=""Action4""/>
    <Point Point_X=""0.500000"" Point_Y=""0.900000"" ItemName=""Action5""/>
    <SwitchOperation Point_X=""0.400000"" Point_Y=""0.400000"" ItemName=""Action6""/>
</Item>";

        var (transformed, count) = GameLoopMappingProvider.TransformKeymapXml(sampleXml, 1440, 1080);

        Assert.Equal(6, count);
        Assert.Contains("<Key ", transformed);
        Assert.Contains("<KeyMap ", transformed);
        Assert.Contains("<KeyMapping ", transformed);
        Assert.Contains("<KeyMappingEx ", transformed);
        Assert.Contains("<Point ", transformed);
        Assert.Contains("<SwitchOperation ", transformed);
    }

    [Fact]
    public void TransformKeymapXml_AppliesCorrectBaselineByModeId()
    {
        // Mode 1: 720p base (1280x720)
        // Mode 3: 1080p base (1920x1080)
        // Mode 4: 2K base (2560x1440)
        string sampleXml = @"
<Item ApkName=""com.tencent.ig"">
    <KeyMapMode ModeID=""1"" Name=""720P"">
        <KeyMapping ItemName=""WASD"" Point_X=""0.200000"" Point_Y=""0.750000"" />
    </KeyMapMode>
    <KeyMapMode ModeID=""3"" Name=""1080P"">
        <KeyMapping ItemName=""WASD"" Point_X=""0.200000"" Point_Y=""0.750000"" />
    </KeyMapMode>
    <KeyMapMode ModeID=""4"" Name=""2K"">
        <KeyMapping ItemName=""WASD"" Point_X=""0.200000"" Point_Y=""0.750000"" />
    </KeyMapMode>
</Item>";

        var (transformed, count) = GameLoopMappingProvider.TransformKeymapXml(sampleXml, 1440, 1080);

        Assert.Equal(3, count);
        Assert.Contains("ModeID=\"1\"", transformed);
        Assert.Contains("ModeID=\"3\"", transformed);
        Assert.Contains("ModeID=\"4\"", transformed);
    }

    [Fact]
    public void TransformKeymapXml_InjectsWasdSpeedWhenSpecified()
    {
        string sampleXml = @"
<Item ApkName=""com.tencent.ig"" Mode=""Rocker"" Speed=""70"">
    <KeyMapping ItemName=""Jump"" Point_X=""0.950000"" Point_Y=""0.750000"" />
</Item>";

        var (transformed, count) = GameLoopMappingProvider.TransformKeymapXml(sampleXml, 1440, 1080, wasdSpeed: 95);

        Assert.Equal(1, count);
        Assert.Contains("Speed=\"95\"", transformed);
        Assert.DoesNotContain("Speed=\"70\"", transformed);
    }
}
