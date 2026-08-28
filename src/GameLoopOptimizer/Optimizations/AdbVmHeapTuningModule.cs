using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbVmHeapTuningModule : IOptimizationModule
{
    public string Id => "gl_adb_vm_heap";
    public string Title => "Android Dalvik / ART Virtual Memory Heap Tuning";
    public OptimizationCategory Category => OptimizationCategory.MemoryStorage;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Expands Dalvik VM heap limit to 1024MB and adjusts heap target utilization to prevent garbage collection frame drops during intense matches.";
    public string TechnicalRationale => "Default Android heap caps cause aggressive Garbage Collector (GC) thread execution whenever texture and shader assets are streamed, resulting in sudden frame-time spikes and micro-stutter.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Heap: 1024M / Growth: 512M";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    public async Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!gl.IsInstalled)
        {
            CurrentStateDisplay = "GameLoop Not Installed";
            State = OptimizationState.NotDetected;
            return State;
        }

        if (!AdbManager.IsAdbAvailable(gl))
        {
            CurrentStateDisplay = "ADB Not Found";
            State = OptimizationState.NotDetected;
            return State;
        }

        string heapSize = await AdbManager.GetPropAsync("dalvik.vm.heapsize", gl);
        string growth = await AdbManager.GetPropAsync("dalvik.vm.heapgrowthlimit", gl);

        bool isOpt = (heapSize.Contains("1024m", StringComparison.OrdinalIgnoreCase) || heapSize.Contains("1024M")) &&
                     (growth.Contains("512m", StringComparison.OrdinalIgnoreCase) || growth.Contains("512M"));

        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? "Tuned (1024M Heap / 512M Growth)" : $"Heap: {(string.IsNullOrEmpty(heapSize) ? "Default" : heapSize)}, Growth: {(string.IsNullOrEmpty(growth) ? "Default" : growth)}";
        State = isOpt ? OptimizationState.Optimized : OptimizationState.Recommended;
        return State;
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!AdbManager.IsAdbAvailable(gl))
        {
            return OptimizationResult.Fail(Id, "ADB executable not found on system.");
        }

        try
        {
            await AdbManager.AutoConnectGameLoopAsync(gl);

            string prevHeap = await AdbManager.GetPropAsync("dalvik.vm.heapsize", gl);
            string prevGrowth = await AdbManager.GetPropAsync("dalvik.vm.heapgrowthlimit", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "dalvik.vm.heapsize",
                PreviousValue = prevHeap,
                NewValue = "1024m",
                Description = "Dalvik VM heap size expansion"
            });

            await AdbManager.SetPropAsync("dalvik.vm.heapgrowthlimit", "512m", gl);
            await AdbManager.SetPropAsync("dalvik.vm.heapsize", "1024m", gl);
            await AdbManager.SetPropAsync("dalvik.vm.heaptargetutilization", "0.75", gl);
            await AdbManager.SetPropAsync("dalvik.vm.dexopt-flags", "v=n,o=v", gl);

            IsOptimized = true;
            CurrentStateDisplay = "Tuned (1024M Heap / 512M Growth)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Dalvik VM 1024M heap ceiling and 0.75 target utilization.");
            return OptimizationResult.Ok(Id, "Dalvik VM memory heap tuned successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to tune Dalvik VM heap: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        string prevVal = target?.PreviousValue ?? "512m";
        if (string.IsNullOrWhiteSpace(prevVal)) prevVal = "512m";

        await AdbManager.SetPropAsync("dalvik.vm.heapsize", prevVal);
        await AdbManager.SetPropAsync("dalvik.vm.heapgrowthlimit", "256m");

        IsOptimized = false;
        CurrentStateDisplay = "Restored";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored previous Dalvik VM heap limits.");
    }

    public async Task<bool> VerifyAsync()
    {
        string heapSize = await AdbManager.GetPropAsync("dalvik.vm.heapsize");
        return heapSize.Contains("1024m", StringComparison.OrdinalIgnoreCase);
    }
}
