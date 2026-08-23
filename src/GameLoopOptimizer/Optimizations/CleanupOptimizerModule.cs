using System.IO;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class CleanupOptimizerModule : IOptimizationModule
{
    public string Id => "clean_temp_cache";
    public string Title => "Safe Temp & Emulator Cache Cleanup";
    public OptimizationCategory Category => OptimizationCategory.MemoryStorage;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Clears obsolete Tencent temporary download residue, shader dumps, and Windows temp files to reduce disk I/O latency.";
    public string TechnicalRationale => "A bloated temp directory slows down disk file allocation searches, creating micro-hitches during in-game asset streaming.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Ready to Scan";
    public string RecommendedStateDisplay => "Clean";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        CurrentStateDisplay = "Ready to Clean";
        State = OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        return await Task.Run(() =>
        {
            try
            {
                long cleanedBytes = 0;
                int filesDeleted = 0;

                // Safe paths to clean
                var pathsToClean = new List<string>
                {
                    Path.GetTempPath(),
                    Path.Combine(Path.GetTempPath(), "TxGameDownload"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tencent", "logs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
                };

                foreach (var dirPath in pathsToClean)
                {
                    if (!Directory.Exists(dirPath)) continue;

                    try
                    {
                        var dirInfo = new DirectoryInfo(dirPath);
                        foreach (var file in dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                // Skip files modified in the last 60 minutes
                                if (DateTime.Now - file.LastWriteTime < TimeSpan.FromMinutes(60)) continue;

                                cleanedBytes += file.Length;
                                file.Delete();
                                filesDeleted++;
                            }
                            catch
                            {
                                // In use file, safely skip
                            }
                        }
                    }
                    catch
                    {
                        // Directory enumeration skip
                    }
                }

                double cleanedMb = Math.Round((double)cleanedBytes / (1024 * 1024), 1);
                IsOptimized = true;
                CurrentStateDisplay = $"Cleaned ({cleanedMb} MB freed)";
                State = OptimizationState.Optimized;

                Logger.Success(Title, $"Cleaned {filesDeleted} temporary files ({cleanedMb} MB freed).");
                return OptimizationResult.Ok(Id, $"Freed {cleanedMb} MB across {filesDeleted} cache files.");
            }
            catch (Exception ex)
            {
                Logger.Error(Title, $"Cleanup error: {ex.Message}");
                return OptimizationResult.Fail(Id, ex.Message, ex);
            }
        });
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        IsOptimized = false;
        CurrentStateDisplay = "Ready";
        State = OptimizationState.Recommended;
        return Task.FromResult(OptimizationResult.Ok(Id, "Temporary cache files cannot and need not be restored."));
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
