using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

/// <summary>
/// Covers <see cref="SensitivityMetrics"/> (eDPI / cm-360) and the vertical bias
/// tuner in <see cref="SensitivityCalculator"/>.
///
/// Context: the initial cm/360 constant was wrong and produced physically
/// impossible figures (3.46 cm per full turn). These tests pin the calibration
/// against verified reference points so it cannot silently drift again.
/// </summary>
public class SensitivityMetricsTests
{
    [Fact]
    public void Cm360_800Dpi_Sens50_MatchesCalibratedReference()
    {
        // Calibration anchor: 800 DPI at sens 50 is 45 cm/360.
        double cm = SensitivityMetrics.Cm360(800, 50);

        Assert.Equal(45.0, cm, precision: 1);
    }

    [Theory]
    [InlineData(400, 60)]
    [InlineData(800, 50)]
    [InlineData(1600, 40)]
    [InlineData(3200, 30)]
    public void Cm360_ProducesPhysicallyPlausibleValues(int dpi, double sens)
    {
        double cm = SensitivityMetrics.Cm360(dpi, sens);

        // No real setup turns a full circle in under ~10cm or needs over ~200cm.
        Assert.InRange(cm, 10.0, 200.0);
    }

    [Fact]
    public void Cm360_HigherDpiAtEquivalentFeel_GivesLowerCm()
    {
        // 1600 DPI @ sens 25 should match 800 DPI @ sens 50 in feel.
        double low = SensitivityMetrics.Cm360(800, 50);
        double high = SensitivityMetrics.Cm360(1600, 25);

        Assert.Equal(low, high, precision: 1);
    }

    [Fact]
    public void SensitivityForCm360_RoundTrips()
    {
        foreach (double target in new[] { 30.0, 45.0, 60.0 })
        {
            double sens = SensitivityMetrics.SensitivityForCm360(800, target);
            double verify = SensitivityMetrics.Cm360(800, sens);

            Assert.Equal(target, verify, precision: 1);
        }
    }

    [Fact]
    public void EffectiveDpi_IsDpiTimesSensitivity()
    {
        Assert.Equal(40000.0, SensitivityMetrics.EffectiveDpi(800, 50), precision: 2);
    }

    [Fact]
    public void Cm360_ZeroSensitivity_DoesNotDivideByZero()
    {
        double cm = SensitivityMetrics.Cm360(800, 0);

        Assert.True(cm > 1000 || double.IsInfinity(cm), "Zero sensitivity must not produce 0 or NaN.");
        Assert.False(double.IsNaN(cm));
    }

    [Theory]
    [InlineData(15.0, "Very Fast")]
    [InlineData(25.0, "Fast")]
    [InlineData(40.0, "Balanced")]
    [InlineData(55.0, "Controlled")]
    [InlineData(80.0, "Very Slow")]
    public void DescribeFeel_MapsRangesToBands(double cm, string expectedFragment)
    {
        Assert.Contains(expectedFragment, SensitivityMetrics.DescribeFeel(cm));
    }

    [Fact]
    public void NormaliseMobileSensitivity_IsLinear()
    {
        double half = SensitivityMetrics.NormaliseMobileSensitivity(50);
        double full = SensitivityMetrics.NormaliseMobileSensitivity(100);

        Assert.Equal(half * 2, full, precision: 6);
    }

    [Fact]
    public void Compute_PopulatesAllFields()
    {
        var result = SensitivityMetrics.Compute(1600, 40);

        Assert.Equal(1600, result.MouseDpi);
        Assert.Equal(40, result.GameSensitivity);
        Assert.True(result.Cm360 > 0);
        Assert.True(result.EffectiveDpi > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.FeelDescription));
    }

    // ---- Vertical bias tuner ----

    [Fact]
    public void CalculateVerticalBias_NeutralMultiplier_LeavesXandYEqual()
    {
        var result = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 1.0);

        Assert.Equal(result.KeymapX, result.KeymapY);
        Assert.Equal(0.0, result.PhysicalTravelReductionPercent, precision: 1);
    }

    [Fact]
    public void CalculateVerticalBias_KeepsXStableWhileYScales()
    {
        var neutral = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 1.0);
        var biased = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 1.65);

        Assert.Equal(neutral.KeymapX, biased.KeymapX);
        Assert.True(biased.KeymapY > biased.KeymapX);
    }

    [Fact]
    public void CalculateVerticalBias_ReliefIncreasesMonotonically()
    {
        double previous = -1;

        for (double v = 1.0; v <= 2.5; v += 0.25)
        {
            var result = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, v);
            Assert.True(
                result.PhysicalTravelReductionPercent >= previous,
                $"Relief must not decrease at vMult {v}.");
            previous = result.PhysicalTravelReductionPercent;
        }
    }

    [Fact]
    public void CalculateVerticalBias_ClampsOutOfRangeInputs()
    {
        var low = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 0.1);
        var high = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 99.0);

        Assert.Equal(1.0, low.VerticalMultiplier, precision: 2);
        Assert.Equal(2.5, high.VerticalMultiplier, precision: 2);
    }

    [Fact]
    public void CalculateVerticalBias_ClampsKeymapYToValidRange()
    {
        var result = SensitivityCalculator.CalculateVerticalBias(3200, AimPlaystyle.HighSensFastFlick, 2.5);

        Assert.InRange(result.KeymapY, 20, 100);
        Assert.InRange(result.KeymapX, 15, 85);
    }

    [Fact]
    public void CalculateVerticalBias_WarnsWhenOverBiased()
    {
        var result = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 2.2);

        Assert.True(result.IsOverBiased);
        Assert.False(string.IsNullOrWhiteSpace(result.Warning));
    }

    [Fact]
    public void CalculateVerticalBias_NoWarningInNormalRange()
    {
        var result = SensitivityCalculator.CalculateVerticalBias(800, AimPlaystyle.BalancedCompetitive, 1.65);

        Assert.False(result.IsOverBiased);
        Assert.True(string.IsNullOrEmpty(result.Warning));
    }

    [Fact]
    public void Calculate_RespectsVerticalMultiplier()
    {
        var neutral = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive, 1.0);
        var biased = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive, 1.65);

        Assert.True(biased.GameLoopKeymapY > neutral.GameLoopKeymapY);
        Assert.Equal(neutral.GameLoopKeymapX, biased.GameLoopKeymapX);
        Assert.Equal(1.65, biased.VerticalMultiplier, precision: 2);
    }
}