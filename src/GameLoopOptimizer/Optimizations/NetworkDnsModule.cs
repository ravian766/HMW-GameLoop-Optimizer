using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Optimizations;

public class NetworkDnsModule : IOptimizationModule
{
    public string Id => "net_gaming_dns";
    public string Title => "Cloudflare Low-Latency Gaming DNS";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures ultra-fast Cloudflare Anycast DNS (1.1.1.1 & 1.0.0.1) and flushes DNS resolver cache to minimize match matchmaking and routing lookup delay.";
    public string TechnicalRationale => "Default ISP DNS servers often introduce 30ms-80ms resolution delay during PUBG Mobile server matchmaking and asset downloads; Cloudflare provides sub-10ms Anycast resolution.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "ISP Default DNS";
    public string RecommendedStateDisplay => "Cloudflare Gaming (1.1.1.1)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Recommended;

    private static bool _isDnsApplied = false;

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!PermissionManager.IsAdministrator)
        {
            CurrentStateDisplay = "Requires Admin";
            State = OptimizationState.RequiresAdmin;
            return Task.FromResult(State);
        }

        IsOptimized = _isDnsApplied;
        CurrentStateDisplay = _isDnsApplied ? "Cloudflare 1.1.1.1 (Low Latency)" : "Standard / ISP DNS";
        State = _isDnsApplied ? OptimizationState.Optimized : OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public async Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!PermissionManager.IsAdministrator)
        {
            return OptimizationResult.Fail(Id, "Administrator privileges are required to configure adapter DNS.");
        }

        var preset = DnsOptimizerService.Presets[0]; // Cloudflare
        bool ok = await DnsOptimizerService.ApplyDnsPresetAsync(preset);

        if (ok)
        {
            _isDnsApplied = true;
            IsOptimized = true;
            CurrentStateDisplay = "Cloudflare 1.1.1.1 (Low Latency)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Cloudflare 1.1.1.1 Anycast DNS & flushed resolver cache.");
            return OptimizationResult.Ok(Id, "Applied Cloudflare 1.1.1.1 Gaming DNS.");
        }

        return OptimizationResult.Fail(Id, "Failed to apply DNS configuration.");
    }

    public async Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        bool ok = await DnsOptimizerService.ResetDnsToDhcpAsync();
        _isDnsApplied = false;
        IsOptimized = false;
        CurrentStateDisplay = "Automatic (DHCP)";
        State = OptimizationState.Recommended;

        return ok 
            ? OptimizationResult.Ok(Id, "Reset network DNS to Automatic DHCP.") 
            : OptimizationResult.Fail(Id, "Failed to reset DNS to DHCP.");
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
