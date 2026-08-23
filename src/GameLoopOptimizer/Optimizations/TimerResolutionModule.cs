using System.Runtime.InteropServices;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class TimerResolutionModule : IOptimizationModule
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

    public string Id => "win_timer_resolution";
    public string Title => "Windows High-Precision Timer (0.5ms)";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Increases Windows system timer resolution from standard 15.6ms to 0.5ms / 1.0ms for the duration of the session.";
    public string TechnicalRationale => "A higher timer frequency reduces scheduler sleep quantum rounding, resulting in smoother thread wakeups and more consistent frame delivery.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "15.6 ms (Standard)";
    public string RecommendedStateDisplay => "0.5 ms - 1.0 ms";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    private static bool _isTimerActive = false;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        CurrentStateDisplay = $"{sys.CurrentTimerResolutionMs:F1} ms";
        IsOptimized = _isTimerActive || sys.CurrentTimerResolutionMs <= 1.0;
        State = IsOptimized ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        try
        {
            // Set 1ms via winmm
            TimeBeginPeriod(1);

            // Try 0.5ms (5000 in 100ns units) via NtSetTimerResolution
            NtSetTimerResolution(5000, true, out uint currentRes);

            _isTimerActive = true;
            IsOptimized = true;
            double resMs = Math.Round((double)currentRes / 10000.0, 2);
            CurrentStateDisplay = $"{resMs:F2} ms (High Precision)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, $"High precision timer active: {resMs:F2} ms");
            return Task.FromResult(OptimizationResult.Ok(Id, $"Timer resolution set to {resMs:F2} ms."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to set timer resolution: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            TimeEndPeriod(1);
            NtSetTimerResolution(156250, false, out _); // Restore default

            _isTimerActive = false;
            IsOptimized = false;
            CurrentStateDisplay = "15.6 ms (Standard)";
            State = OptimizationState.Recommended;

            Logger.Info(Title, "Restored standard timer resolution.");
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored standard system timer resolution."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(_isTimerActive);
    }

    public static void SetHighPrecision(double ms = 0.5)
    {
        try
        {
            TimeBeginPeriod(1);
            uint desired = (uint)(ms * 10000);
            NtSetTimerResolution(desired, true, out _);
            _isTimerActive = true;
        }
        catch { }
    }

    public static void RestoreTimer()
    {
        try
        {
            TimeEndPeriod(1);
            NtSetTimerResolution(156250, false, out _);
            _isTimerActive = false;
        }
        catch { }
    }
}
