using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class AdbNetworkDnsModule : IOptimizationModule
{
    public string Id => "gl_adb_network_dns";
    public string Title => "Android VM In-Emulator DNS & TCP Stack Optimization";
    public OptimizationCategory Category => OptimizationCategory.GameLoopEngine;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Syncs ultra-low jitter DNS resolvers (Cloudflare 1.1.1.1 & Google 8.8.8.8) and high-throughput TCP buffers directly into the GameLoop Android kernel.";
    public string TechnicalRationale => "Overrides net.dns1, net.dns2, and net.tcp.buffersize.wifi properties inside the emulator VM to minimize packet queuing delay and DNS lookup latency during online matches.";
    public bool RequiresAdmin => false;

    public string CurrentStateDisplay { get; private set; } = "Unknown";
    public string RecommendedStateDisplay => "Low-Latency DNS & TCP Buffers";
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

        string dns1 = await AdbManager.GetPropAsync("net.dns1", gl);
        string tcpBuf = await AdbManager.GetPropAsync("net.tcp.buffersize.wifi", gl);

        bool isOpt = (dns1.Contains("1.1.1.1") || dns1.Contains("8.8.8.8")) && !string.IsNullOrEmpty(tcpBuf);
        IsOptimized = isOpt;
        CurrentStateDisplay = isOpt ? $"Optimized ({dns1.Trim()})" : $"VM DNS: {(string.IsNullOrEmpty(dns1) ? "Default Internal" : dns1.Trim())}";
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

            string prevDns = await AdbManager.GetPropAsync("net.dns1", gl);

            BackupManager.RecordBackup(new BackupEntry
            {
                ModuleId = Id,
                Title = Title,
                Category = Category,
                TargetType = "AdbProp",
                TargetPath = "net.dns1",
                PreviousValue = prevDns,
                NewValue = "1.1.1.1",
                Description = "In-VM DNS and TCP Stack Optimization"
            });

            await AdbManager.SetInVmDnsAsync("1.1.1.1", "1.0.0.1", gl);
            await AdbManager.OptimizeInVmTcpStackAsync(gl);

            IsOptimized = true;
            CurrentStateDisplay = "Cloudflare 1.1.1.1 & Fast TCP";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Synced Cloudflare DNS and tuned TCP buffer sizes inside GameLoop Android VM.");
            return OptimizationResult.Ok(Id, "In-VM DNS (1.1.1.1) and TCP buffer optimization applied.");
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to configure In-VM DNS & TCP: {ex.Message}");
            return OptimizationResult.Fail(Id, ex.Message, ex);
        }
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        string restoreDns = target?.PreviousValue ?? "8.8.8.8";
        if (string.IsNullOrWhiteSpace(restoreDns)) restoreDns = "8.8.8.8";

        await AdbManager.SetPropAsync("net.dns1", restoreDns);
        IsOptimized = false;
        CurrentStateDisplay = $"Default ({restoreDns})";
        State = OptimizationState.NotOptimized;
        return OptimizationResult.Ok(Id, "Restored default Android VM DNS.");
    }

    public async Task<bool> VerifyAsync()
    {
        string dns1 = await AdbManager.GetPropAsync("net.dns1");
        return dns1.Contains("1.1.1.1") || dns1.Contains("8.8.8.8");
    }
}
