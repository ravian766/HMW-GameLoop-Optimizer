using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class MappingValidatorTests
{
    [Fact]
    public void ValidateCoordinates_AllInsideSafeBounds_PassesValidation()
    {
        var controls = new List<(string name, double x, double y)>
        {
            ("WASD", 0.15, 0.75),
            ("Fire", 0.85, 0.75),
            ("Scope", 0.79, 0.41),
            ("Jump", 0.95, 0.70)
        };

        var result = MappingValidator.ValidateCoordinates(controls);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal(4, result.ValidatedControlCount);
    }

    [Fact]
    public void ValidateCoordinates_NearEdge_ProducesWarnings()
    {
        var controls = new List<(string name, double x, double y)>
        {
            ("EdgeButton", 0.003, 0.997) // Outside [0.005, 0.995]
        };

        var result = MappingValidator.ValidateCoordinates(controls);

        Assert.True(result.IsValid); // Still valid (not out of 0.0-1.0)
        Assert.Empty(result.Errors);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void ValidateCoordinates_OutOfBoundsOrNaN_ProducesErrors()
    {
        var controls = new List<(string name, double x, double y)>
        {
            ("OffscreenLeft", -0.05, 0.50),
            ("OffscreenRight", 1.25, 0.50),
            ("InvalidNaN", double.NaN, 0.50),
            ("InvalidInfinity", 0.50, double.PositiveInfinity)
        };

        var result = MappingValidator.ValidateCoordinates(controls);

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Errors.Count);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1440, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(1280, 720)]
    [InlineData(3840, 2160)]
    public void ValidateResolution_StandardGamingResolutions_Pass(int width, int height)
    {
        var result = MappingValidator.ValidateResolution(width, height);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(400, 1080)]   // Too narrow
    [InlineData(1920, 300)]   // Too short
    [InlineData(9000, 2160)]  // Too wide
    [InlineData(1920, 5000)]  // Too tall
    public void ValidateResolution_ExtremeDimensions_Fail(int width, int height)
    {
        var result = MappingValidator.ValidateResolution(width, height);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateXmlFragment_WellFormedFragment_ReturnsTrue()
    {
        string validFragment = @"
<Item ApkName=""com.tencent.ig"">
    <KeyMapping ItemName=""Space"" Point_X=""0.95"" Point_Y=""0.70"" />
</Item>";

        bool ok = MappingValidator.ValidateXmlFragment(validFragment, out string error);

        Assert.True(ok);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidateXmlFragment_MalformedXml_ReturnsFalseWithError()
    {
        string brokenXml = @"<Item ApkName=""com.tencent.ig""><UnclosedTag></Item>";

        bool ok = MappingValidator.ValidateXmlFragment(brokenXml, out string error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void ValidateXmlFragment_EmptyOrWhitespace_ReturnsFalse()
    {
        bool ok = MappingValidator.ValidateXmlFragment("   ", out string error);

        Assert.False(ok);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }
}
