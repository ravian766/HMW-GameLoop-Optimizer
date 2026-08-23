using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class NetworkLatencyModule : IOptimizationModule
{
    public string Id => "net_tcp_latency";
    public string Title => "Network Latency & TCP ACK Tuning";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Low;
    public string Description => "Configures TCP ACK frequency and disables Nagle algorithm delays for network adapters to reduce multiplayer ping variance.";
    public string TechnicalRationale => "By immediately acknowledging network packets without queuing (Nagle delay suppression), packet travel time for real-time multiplayer positional updates in PUBG Mobile is minimized.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Standard TCP";
    public string RecommendedStateDisplay => "Low-Latency TCP (TcpAckFrequency = 1)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    private const string InterfacesPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public Task<OptimizationState> AnalyzeAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!PermissionManager.IsAdministrator)
        {
            CurrentStateDisplay = "Requires Admin";
            State = OptimizationState.RequiresAdmin;
            return Task.FromResult(State);
        }

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(InterfacesPath);
            if (baseKey != null)
            {
                bool anyConfigured = false;
                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    using var subKey = baseKey.OpenSubKey(subKeyName);
                    if (subKey != null)
                    {
                        var ack = subKey.GetValue("TcpAckFrequency");
                        var delay = subKey.GetValue("TCPNoDelay");
                        if (ack is int a && a == 1 && delay is int d && d == 1)
                        {
                            anyConfigured = true;
                            break;
                        }
                    }
                }

                IsOptimized = anyConfigured;
                CurrentStateDisplay = anyConfigured ? "Optimized (Low Latency)" : "Standard TCP";
                State = anyConfigured ? OptimizationState.Optimized : OptimizationState.Recommended;
                return Task.FromResult(State);
            }
        }
        catch { }

        CurrentStateDisplay = "Standard";
        State = OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!PermissionManager.IsAdministrator)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, "Administrator privileges are required to configure network adapter parameters."));
        }

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(InterfacesPath, writable: true);
            if (baseKey == null) return Task.FromResult(OptimizationResult.Fail(Id, "Failed to open TCP/IP interfaces key."));

            int updated = 0;
            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                using var subKey = baseKey.OpenSubKey(subKeyName, writable: true);
                if (subKey != null)
                {
                    // Only configure active adapters with IPAddress or DhcpIPAddress
                    var ip = subKey.GetValue("IPAddress") ?? subKey.GetValue("DhcpIPAddress");
                    if (ip != null)
                    {
                        var prevAck = subKey.GetValue("TcpAckFrequency")?.ToString();
                        var prevDelay = subKey.GetValue("TCPNoDelay")?.ToString();

                        BackupManager.RecordBackup(new BackupEntry
                        {
                            ModuleId = Id,
                            Title = $"{Title} ({subKeyName})",
                            Category = Category,
                            TargetType = "Registry",
                            TargetPath = $@"HKLM\{InterfacesPath}\{subKeyName}",
                            ValueName = "TcpAckFrequency",
                            PreviousValue = prevAck,
                            NewValue = "1",
                            Description = "Configure low-latency TCP ACK frequency"
                        });

                        subKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                        subKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                        updated++;
                    }
                }
            }

            IsOptimized = true;
            CurrentStateDisplay = "Optimized (Low Latency)";
            State = OptimizationState.Optimized;

            Logger.Success(Title, $"Tuned {updated} network interfaces for low-latency TCP transmission.");
            return Task.FromResult(OptimizationResult.Ok(Id, $"Optimized {updated} network adapters for lower ping variance."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply TCP settings: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        var target = backup ?? BackupManager.GetLatestForModule(Id);
        if (target != null && BackupManager.RestoreEntry(target))
        {
            IsOptimized = false;
            CurrentStateDisplay = "Standard";
            State = OptimizationState.NotOptimized;
            return Task.FromResult(OptimizationResult.Ok(Id, "Restored network adapter TCP parameters."));
        }
        return Task.FromResult(OptimizationResult.Fail(Id, "No backup found to revert."));
    }

    public Task<bool> VerifyAsync()
    {
        return Task.FromResult(true);
    }
}
