using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class AimAndInputTests
{
    [Theory]
    [InlineData(400, AimPlaystyle.PrecisionLowSens)]
    [InlineData(800, AimPlaystyle.BalancedCompetitive)]
    [InlineData(1600, AimPlaystyle.HighSensFastFlick)]
    [InlineData(3200, AimPlaystyle.BalancedCompetitive)]
    public void SensitivityCalculator_ProducesValidADSAndScopeCurves(int dpi, AimPlaystyle playstyle)
    {
        var result = SensitivityCalculator.Calculate(dpi, playstyle, 1080);

        Assert.NotNull(result);
        Assert.Equal(dpi, result.MouseDpi);
        Assert.Equal(playstyle, result.Playstyle);
        Assert.True(result.GameLoopKeymapX is >= 20 and <= 80);
        Assert.True(result.GameLoopKeymapY is >= 30 and <= 95);
        Assert.True(result.GameLoopKeymapY >= result.GameLoopKeymapX); // Vertical bias applied

        Assert.NotEmpty(result.ScopeSettings);
        Assert.Equal(7, result.ScopeSettings.Count);

        foreach (var scope in result.ScopeSettings)
        {
            Assert.False(string.IsNullOrWhiteSpace(scope.ScopeName));
            Assert.True(scope.CameraSensitivity is >= 5 and <= 120);
            Assert.True(scope.AdsSensitivity is >= 5 and <= 120);
            Assert.False(string.IsNullOrWhiteSpace(scope.RecoilTip));
        }
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
