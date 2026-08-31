using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class DeepCleanerAndDiagnosticTests
{
    [Fact]
    public void DeepCleanerService_ScanJunk_DiscoversCategoriesWithFormattedSizes()
    {
        // Arrange
        var config = new GameLoopConfig
        {
            InstallPath = @"C:\Program Files\TxGameAssistant"
        };

        // Act
        var result = DeepCleanerService.ScanJunk(config);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Categories);
        Assert.Contains(result.Categories, c => c.Id == "shaders");
        Assert.Contains(result.Categories, c => c.Id == "crash_dumps");
        Assert.Contains(result.Categories, c => c.Id == "gl_logs");
        Assert.Contains(result.Categories, c => c.Id == "temp_buffers");

        foreach (var cat in result.Categories)
        {
            Assert.False(string.IsNullOrWhiteSpace(cat.Title));
            Assert.False(string.IsNullOrWhiteSpace(cat.Description));
            Assert.False(string.IsNullOrWhiteSpace(cat.SizeFormatted));
        }

        Assert.False(string.IsNullOrWhiteSpace(result.TotalSizeFormatted));
    }

    [Fact]
    public async Task DeepCleanerService_CleanJunkAsync_ExecutesSafely()
    {
        // Arrange
        var config = new GameLoopConfig();
        var scan = DeepCleanerService.ScanJunk(config);

        // Act
        var res = await DeepCleanerService.CleanJunkAsync(scan, config);

        // Assert
        Assert.NotNull(res);
        Assert.False(string.IsNullOrWhiteSpace(res.Message));
        Assert.True(res.BytesFreed >= 0);
        Assert.True(res.FilesDeleted >= 0);
    }

    [Fact]
    public void EmulatorDiagnosticService_RunDiagnostic_ProducesHealthReport()
    {
        // Arrange
        var config = new GameLoopConfig
        {
            ForceDirectX = true
        };
        var hw = new HardwareInfo
        {
            GpuVendor = GpuVendor.Nvidia,
            GpuName = "NVIDIA GeForce RTX 3060",
            TotalRamGb = 16.0
        };

        // Act
        var report = EmulatorDiagnosticService.RunDiagnostic(config, hw);

        // Assert
        Assert.NotNull(report);
        Assert.InRange(report.HealthScore, 0, 100);
        Assert.True(report.ChecksPerformed >= 4);
        Assert.False(string.IsNullOrWhiteSpace(report.StatusBadgeText));
        Assert.False(string.IsNullOrWhiteSpace(report.SummaryText));
    }

    [Fact]
    public async Task EmulatorDiagnosticService_AutoFixIssuesAsync_ExecutesSafely()
    {
        // Arrange
        var config = new GameLoopConfig();
        var hw = new HardwareInfo { GpuVendor = GpuVendor.Nvidia };

        // Act
        bool result = await EmulatorDiagnosticService.AutoFixIssuesAsync(config, hw);

        // Assert
        Assert.True(result);
    }
}
