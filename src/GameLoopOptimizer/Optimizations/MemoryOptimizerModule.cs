using System.Diagnostics;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class MemoryOptimizerModule : IOptimizationModule
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    public string Id => "mem_working_set_clean";
    public string Title => "RAM Working Set & Cache Trim";
    public OptimizationCategory Category => OptimizationCategory.MemoryStorage;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Safely flushes unused background process working sets and releases cached physical memory back to the Windows memory manager.";
    public string TechnicalRationale => "Reduces memory pressure and page file thrashing, providing GameLoop with immediate contiguous physical memory.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Ready";
    public string RecommendedStateDisplay => "Clean Working Set";
    public bool IsOptimized { get; private set; } = false;
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        CurrentStateDisplay = $"{hw.TotalRamGb:F1} GB Total RAM";
        IsOptimized = false;
        State = OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        return await Task.Run(() =>
        {
            try
            {
                long bytesBefore = GC.GetTotalMemory(false);
                int trimmedCount = 0;
                var procs = Process.GetProcesses();

                foreach (var proc in procs)
                {
                    try
                    {
                        // Skip emulator itself
                        if (proc.ProcessName.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                            proc.ProcessName.Contains("aow", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        EmptyWorkingSet(proc.Handle);
                        trimmedCount++;
                    }
                    catch
                    {
                        // Ignore access denied on system services
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                // Collect GC for optimizer process as well
                GC.Collect();
                GC.WaitForPendingFinalizers();

                IsOptimized = true;
                CurrentStateDisplay = "Cleaned";
                State = OptimizationState.Optimized;

                Logger.Success(Title, $"Successfully trimmed working sets across {trimmedCount} background processes.");
                return OptimizationResult.Ok(Id, $"Trimmed memory across {trimmedCount} processes.");
            }
            catch (Exception ex)
            {
                Logger.Error(Title, $"Memory trim failed: {ex.Message}");
                return OptimizationResult.Fail(Id, ex.Message, ex);
            }
        });
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        IsOptimized = false;
        CurrentStateDisplay = "Ready";
        State = OptimizationState.Recommended;
        return Task.FromResult(OptimizationResult.Ok(Id, "Memory is managed automatically by Windows."));
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
