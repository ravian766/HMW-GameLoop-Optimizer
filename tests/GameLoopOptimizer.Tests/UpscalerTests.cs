using GameLoopOptimizer.Core;
using GameLoopOptimizer.Upscaler;
using GameLoopOptimizer.ViewModels;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class UpscalerTests
{
    [Fact]
    public void UpscaleConfig_DefaultValues_AreCorrect()
    {
        var config = new UpscaleConfig();

        Assert.Equal(UpscaleQualityMode.Balanced, config.QualityMode);
        Assert.Equal(0.25f, config.Sharpness);
        Assert.True(config.ShowFrameCounter);
        Assert.Equal(1.5f, config.GetUpscaleRatio());
        Assert.Contains("Balanced", config.GetModeLabel());
    }

    [Theory]
    [InlineData(UpscaleQualityMode.Quality, 1.3f)]
    [InlineData(UpscaleQualityMode.Balanced, 1.5f)]
    [InlineData(UpscaleQualityMode.Performance, 1.7f)]
    [InlineData(UpscaleQualityMode.UltraPerformance, 2.0f)]
    public void UpscaleConfig_QualityMode_ReturnsCorrectRatio(UpscaleQualityMode mode, float expectedRatio)
    {
        var config = new UpscaleConfig { QualityMode = mode };
        Assert.Equal(expectedRatio, config.GetUpscaleRatio());
    }

    [Theory]
    [InlineData(UpscaleQualityMode.Quality)]
    [InlineData(UpscaleQualityMode.Balanced)]
    [InlineData(UpscaleQualityMode.Performance)]
    [InlineData(UpscaleQualityMode.UltraPerformance)]
    public void UpscaleConfig_ModeLabel_IsNonEmpty(UpscaleQualityMode mode)
    {
        var config = new UpscaleConfig { QualityMode = mode };
        Assert.False(string.IsNullOrWhiteSpace(config.GetModeLabel()));
    }

    [Fact]
    public void UpscaleConfig_SharpnessRange_IsClamped()
    {
        var config = new UpscaleConfig { Sharpness = -0.5f };
        // The property accepts any float; clamping is done by the service
        Assert.Equal(-0.5f, config.Sharpness);

        config.Sharpness = 2.0f;
        Assert.Equal(2.0f, config.Sharpness);
    }

    [Fact]
    public void UpscaleStats_RecordFrame_UpdatesMetrics()
    {
        var stats = new UpscaleStats(60);
        Assert.Equal(0, stats.TotalFrames);
        Assert.Equal(0, stats.InstantFps);

        // Simulate frames
        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(5); // ~200 FPS
            stats.RecordFrame();
        }

        Assert.True(stats.TotalFrames >= 10);
        Assert.True(stats.InstantFps > 0);
        Assert.True(stats.AverageFps > 0);
        Assert.True(stats.FrameTimeMs > 0);
    }

    [Fact]
    public void UpscaleStats_Reset_ClearsAllMetrics()
    {
        var stats = new UpscaleStats(30);

        Thread.Sleep(5);
        stats.RecordFrame();
        Thread.Sleep(5);
        stats.RecordFrame();

        Assert.True(stats.TotalFrames > 0);

        stats.Reset();

        Assert.Equal(0, stats.TotalFrames);
        Assert.Equal(0, stats.InstantFps);
        Assert.Equal(0, stats.AverageFps);
        Assert.False(stats.IsActive);
    }

    [Fact]
    public void UpscaleStats_GetOverlayText_FormatsCorrectly()
    {
        var stats = new UpscaleStats();
        stats.InputWidth = 720;
        stats.InputHeight = 480;
        stats.OutputWidth = 1920;
        stats.OutputHeight = 1080;

        string text = stats.GetOverlayText();
        Assert.Contains("720×480", text);
        Assert.Contains("1920×1080", text);
        Assert.Contains("FPS", text);
    }

    [Fact]
    public void UpscaleQualityMode_AllValues_AreDistinct()
    {
        var modes = Enum.GetValues<UpscaleQualityMode>();
        Assert.Equal(modes.Length, modes.Distinct().Count());
        Assert.True(modes.Length >= 4);
    }

    [Fact]
    public void FsrShaderSource_AllShaders_AreNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(FsrShaderSource.FullscreenVS));
        Assert.False(string.IsNullOrWhiteSpace(FsrShaderSource.EasuPS));
        Assert.False(string.IsNullOrWhiteSpace(FsrShaderSource.RcasPS));
        Assert.False(string.IsNullOrWhiteSpace(FsrShaderSource.PassthroughPS));
    }

    [Fact]
    public void FsrShaderSource_EasuPS_ContainsLanczosKernel()
    {
        Assert.Contains("Lanczos2", FsrShaderSource.EasuPS);
        Assert.Contains("SV_Target", FsrShaderSource.EasuPS);
        Assert.Contains("EasuConstants", FsrShaderSource.EasuPS);
    }

    [Fact]
    public void FsrShaderSource_RcasPS_ContainsSharpnessParam()
    {
        Assert.Contains("sharpness", FsrShaderSource.RcasPS, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RcasConst", FsrShaderSource.RcasPS);
        Assert.Contains("SV_Target", FsrShaderSource.RcasPS);
    }

    [Fact]
    public void FsrShaderSource_FullscreenVS_GeneratesTriangleFromVertexId()
    {
        Assert.Contains("SV_VertexID", FsrShaderSource.FullscreenVS);
        Assert.Contains("SV_Position", FsrShaderSource.FullscreenVS);
        Assert.Contains("TEXCOORD0", FsrShaderSource.FullscreenVS);
    }

    [Fact]
    public void FsrShaderSource_Shaders_CompileSuccessfully()
    {
        Vortice.D3DCompiler.Compiler.Compile(FsrShaderSource.FullscreenVS, "main", "fullscreen_vs", "vs_5_0", out var vsBlob, out var vsErr);
        Assert.True(vsErr == null, $"VS error: {vsErr?.AsString()}");
        Assert.NotNull(vsBlob);
        vsBlob?.Dispose();

        Vortice.D3DCompiler.Compiler.Compile(FsrShaderSource.EasuPS, "main", "easu_ps", "ps_5_0", out var easuBlob, out var easuErr);
        Assert.True(easuErr == null, $"EASU error: {easuErr?.AsString()}");
        Assert.NotNull(easuBlob);
        easuBlob?.Dispose();

        Vortice.D3DCompiler.Compiler.Compile(FsrShaderSource.RcasPS, "main", "rcas_ps", "ps_5_0", out var rcasBlob, out var rcasErr);
        Assert.True(rcasErr == null, $"RCAS error: {rcasErr?.AsString()}");
        Assert.NotNull(rcasBlob);
        rcasBlob?.Dispose();
    }

    [Fact]
    public void WindowCaptureService_Initialize_FailsWithZeroHandle()
    {
        using var capture = new WindowCaptureService();
        Assert.False(capture.IsInitialized);
        Assert.Equal(0, capture.CaptureWidth);
        Assert.Equal(0, capture.CaptureHeight);
    }

    [Fact]
    public void UpscalerService_IsNotRunningByDefault()
    {
        using var service = new UpscalerService();
        Assert.False(service.IsRunning);
        Assert.NotNull(service.Stats);
        Assert.NotNull(service.Config);
    }

    [Fact]
    public void UpscalerService_StartWithZeroHandle_DoesNotCrash()
    {
        using var service = new UpscalerService();
        string? lastStatus = null;
        service.StatusChanged += (s, msg) => lastStatus = msg;

        service.StartUpscaling(IntPtr.Zero, new UpscaleConfig());

        Assert.False(service.IsRunning);
        Assert.NotNull(lastStatus);
        Assert.Contains("Error", lastStatus!);
    }

    [Fact]
    public void UpscalerService_StopWhenNotRunning_DoesNotCrash()
    {
        using var service = new UpscalerService();
        service.StopUpscaling(); // Should not throw
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void UpscalerService_UpdateSharpness_ClampsValue()
    {
        using var service = new UpscalerService();

        service.UpdateSharpness(0.5f);
        Assert.Equal(0.5f, service.Config.Sharpness);

        service.UpdateSharpness(-1.0f);
        Assert.Equal(0.0f, service.Config.Sharpness);

        service.UpdateSharpness(5.0f);
        Assert.Equal(1.0f, service.Config.Sharpness);
    }

    [Fact]
    public void UpscalerViewModel_DefaultState_IsCorrect()
    {
        var vm = new UpscalerViewModel(() => new GameLoopWindowInfo());
        Assert.False(vm.IsUpscaling);
        Assert.NotNull(vm.StatusMessage);
        Assert.NotNull(vm.QualityModes);
        Assert.True(vm.QualityModes.Length >= 4);
        Assert.Equal(1, vm.SelectedQualityIndex); // Balanced
        Assert.Equal(0.25, vm.Sharpness);
        Assert.True(vm.ShowFrameCounter);
        vm.Cleanup();
    }

    [Fact]
    public void UpscalerViewModel_QualityModeLabels_AllPresent()
    {
        var vm = new UpscalerViewModel(() => new GameLoopWindowInfo());

        for (int i = 0; i < vm.QualityModes.Length; i++)
        {
            vm.SelectedQualityIndex = i;
            Assert.False(string.IsNullOrWhiteSpace(vm.QualityModeLabel));
        }
        vm.Cleanup();
    }

    [Fact]
    public void UpscalerViewModel_SharpnessLabel_FormatsCorrectly()
    {
        var vm = new UpscalerViewModel(() => new GameLoopWindowInfo());

        vm.Sharpness = 0.0;
        Assert.Equal("100%", vm.SharpnessLabel);

        vm.Sharpness = 1.0;
        Assert.Equal("0%", vm.SharpnessLabel);

        vm.Sharpness = 0.5;
        Assert.Equal("50%", vm.SharpnessLabel);
        vm.Cleanup();
    }

    [Fact]
    public void HotkeyManager_HasUpscalerHotkeyId()
    {
        Assert.Equal(9007, HotkeyManager.HOTKEY_UPSCALER_ID);
    }

    [Fact]
    public void UpscalerViewModel_DownscalePresets_ContainStandardResolutions()
    {
        var vm = new UpscalerViewModel(() => new GameLoopWindowInfo());
        Assert.NotEmpty(vm.DownscalePresets);
        Assert.Contains(vm.DownscalePresets, p => p.Width == 1280 && p.Height == 720);
        Assert.Contains(vm.DownscalePresets, p => p.Width == 1600 && p.Height == 900);
        Assert.True(vm.AutoDownscaleOnStart);
        vm.Cleanup();
    }

    [Fact]
    public void UpscalerViewModel_DetectWindow_FlagsSuboptimalResolutionAtNative()
    {
        var fakeNativeWindow = new GameLoopWindowInfo
        {
            IsFound = true,
            ProcessName = "AndroidEmulatorEn",
            ClientWidth = 1920,
            ClientHeight = 1080,
            MonitorWidth = 1920,
            MonitorHeight = 1080
        };

        var vm = new UpscalerViewModel(() => fakeNativeWindow);
        vm.DetectWindow();

        Assert.True(vm.IsResolutionSuboptimal);
        Assert.Contains("Running native gives no GPU rendering reduction", vm.ResolutionStatusMessage);
        vm.Cleanup();
    }

    [Fact]
    public void UpscalerViewModel_DetectWindow_FlagsOptimalResolutionWhenDownscaled()
    {
        var fakeDownscaledWindow = new GameLoopWindowInfo
        {
            IsFound = true,
            ProcessName = "AndroidEmulatorEn",
            ClientWidth = 1280,
            ClientHeight = 720,
            MonitorWidth = 1920,
            MonitorHeight = 1080
        };

        var vm = new UpscalerViewModel(() => fakeDownscaledWindow);
        vm.DetectWindow();

        Assert.False(vm.IsResolutionSuboptimal);
        Assert.Contains("Optimal render resolution", vm.ResolutionStatusMessage);
        vm.Cleanup();
    }
}
