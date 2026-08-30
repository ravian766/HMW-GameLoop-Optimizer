using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class AimAndInputTests
{
    [Theory]
    [InlineData(400, AimPlaystyle.PrecisionLowSens, 1.65)]
    [InlineData(800, AimPlaystyle.BalancedCompetitive, 1.35)]
    [InlineData(1600, AimPlaystyle.HighSensFastFlick, 2.0)]
    [InlineData(3200, AimPlaystyle.BalancedCompetitive, 1.0)]
    public void SensitivityCalculator_ProducesValidADSAndScopeCurves(int dpi, AimPlaystyle playstyle, double vMult)
    {
        var result = SensitivityCalculator.Calculate(dpi, playstyle, vMult, 1080);

        Assert.NotNull(result);
        Assert.Equal(dpi, result.MouseDpi);
        Assert.Equal(playstyle, result.Playstyle);
        Assert.Equal(vMult, result.VerticalMultiplier);
        Assert.NotEmpty(result.RecoilReliefLabel);
        Assert.NotEmpty(result.RecoilReliefDescription);
        Assert.True(result.GameLoopKeymapX is >= 15 and <= 85);
        Assert.True(result.GameLoopKeymapY is >= 20 and <= 100);
        Assert.True(result.GameLoopKeymapY >= result.GameLoopKeymapX);

        Assert.NotEmpty(result.ScopeSettings);
        Assert.Equal(10, result.ScopeSettings.Count);

        foreach (var scope in result.ScopeSettings)
        {
            Assert.False(string.IsNullOrWhiteSpace(scope.ScopeName));
            Assert.True(scope.CameraSensitivity is >= 5 and <= 120);
            Assert.True(scope.AdsSensitivity is >= 5 and <= 120);
            Assert.False(string.IsNullOrWhiteSpace(scope.RecoilTip));
        }
    }

    [Fact]
    public void SensitivityCalculator_CustomVerticalMultiplier_ScalesRecoilEaseCorrectly()
    {
        var neutral = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive, 1.0);
        var laser = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive, 1.65);
        var heavy = SensitivityCalculator.Calculate(800, AimPlaystyle.BalancedCompetitive, 2.0);

        Assert.Equal(neutral.GameLoopKeymapX, laser.GameLoopKeymapX);
        Assert.True(laser.GameLoopKeymapY > neutral.GameLoopKeymapY);
        Assert.True(heavy.GameLoopKeymapY > laser.GameLoopKeymapY);
        Assert.Contains("-39%", laser.RecoilReliefDescription);
        Assert.Contains("Neutral", neutral.RecoilReliefLabel);
    }

    [Fact]
    public void MouseBenchmarkService_MetricsCalculation_AggregatesProperly()
    {
        var benchmark = new MouseBenchmarkService();
        benchmark.Start();
        Assert.True(benchmark.IsRunning);

        for (int i = 0; i < 40; i++)
        {
            benchmark.RecordMovement();
            Thread.Sleep(1);
        }

        var metrics = benchmark.GetCurrentMetrics();
        Assert.NotNull(metrics);
        Assert.True(metrics.SampleCount > 0);
        Assert.NotNull(metrics.RatingText);
        Assert.NotNull(metrics.Recommendation);

        benchmark.Stop();
        Assert.False(benchmark.IsRunning);
    }
}
