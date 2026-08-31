using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class GpuScalingModule : IOptimizationModule
{
    private bool _isOptimized;
    private string _currentState = "Unknown";

    public string Id => "gpu_fullscreen_scaling";
    public string Title => "GPU Fullscreen Stretched Scaling (No Black Bars)";
    public OptimizationCategory Category => OptimizationCategory.GraphicsQuality;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures Windows GPU driver adapter scaling to Fullscreen Stretched (Scaling=3). Eliminates black side-bars when rendering in 4:3 (1440x1080) and 16:10 stretched aspect ratios.";
    public string TechnicalRationale => "By default, GPU display adapters often enforce Aspect Ratio Scaling (Scaling=2) which places black bars on both sides of stretched resolutions. Setting Scaling=3 instructs the hardware display engine to stretch the frame buffer across the full physical panel with zero added latency.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay => _currentState;
    public string RecommendedStateDisplay => "Fullscreen Stretched (Scaling = 3)";
    public bool IsOptimized => _isOptimized;
    public OptimizationState State => _isOptimized ? OptimizationState.Optimized : OptimizationState.NotOptimized;

    public GpuScalingModule()
    {
    }

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        var status = DisplayScalingService.CheckCurrentScaling(hw);
        _isOptimized = status.IsFullScreenScalingActive;
        _currentState = status.IsFullScreenScalingActive
            ? $"Fullscreen Stretched (Active on {status.AdaptersConfigured} adapter(s))"
            : "Aspect Ratio / Centered (Black Bars Enabled)";

        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            int updated = DisplayScalingService.ApplyFullScreenScaling(true, true);
            _isOptimized = updated > 0;
            _currentState = $"Fullscreen Stretched ({updated} adapter(s) configured)";

            return Task.FromResult(new OptimizationResult
            {
                Success = true,
                Message = $"Configured GPU Fullscreen Scaling on {updated} display adapter(s)."
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OptimizationResult
            {
                Success = false,
                Message = $"Failed to apply GPU Fullscreen Scaling: {ex.Message}"
            });
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            int updated = DisplayScalingService.ApplyFullScreenScaling(false, false);
            _isOptimized = false;
            _currentState = "Aspect Ratio / Default";

            return Task.FromResult(new OptimizationResult
            {
                Success = true,
                Message = "Restored default GPU display scaling."
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OptimizationResult
            {
                Success = false,
                Message = $"Failed to rollback GPU scaling: {ex.Message}"
            });
        }
    }

    public Task<bool> VerifyAsync()
    {
        var status = DisplayScalingService.CheckCurrentScaling();
        return Task.FromResult(status.IsFullScreenScalingActive);
    }
}
