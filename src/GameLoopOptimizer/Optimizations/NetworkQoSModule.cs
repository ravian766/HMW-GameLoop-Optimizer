using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Optimizations;

public class NetworkQoSModule : IOptimizationModule
{
    public string Id => "net_udp_qos_priority";
    public string Title => "Windows QoS & GameLoop UDP DSCP Priority Tagging";
    public OptimizationCategory Category => OptimizationCategory.WindowsConfig;
    public RiskLevel RiskLevel => RiskLevel.Safe;
    public string Description => "Configures Windows Quality of Service (QoS) to tag GameLoop UDP traffic with DSCP 46 (Expedited Forwarding) for priority routing through network routers and switches.";
    public string TechnicalRationale => "Tagging real-time multiplayer UDP packets with DSCP 46 (0x2E) ensures home and ISP QoS routers process GameLoop combat packets ahead of background streaming or downloads, preventing ping spikes during firefights.";
    public bool RequiresAdmin => true;

    public string CurrentStateDisplay { get; private set; } = "Standard QoS";
    public string RecommendedStateDisplay => "DSCP 46 (Expedited Forwarding)";
    public bool IsOptimized { get; private set; }
    public OptimizationState State { get; private set; } = OptimizationState.Unknown;

    private const string QosPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\QoS";
    private const string TcpipQosPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\QoS";

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
            using var qosKey = Registry.LocalMachine.OpenSubKey($@"{QosPolicyPath}\GameLoop_UDP_Priority");
            if (qosKey != null)
            {
                var dscp = qosKey.GetValue("DSCP Value")?.ToString();
                bool isOpt = dscp == "46" || dscp == "0x2E";
                IsOptimized = isOpt;
                CurrentStateDisplay = isOpt ? "DSCP 46 Priority Active" : "Configured";
                State = isOpt ? OptimizationState.Optimized : OptimizationState.Recommended;
                return Task.FromResult(State);
            }
        }
        catch { }

        CurrentStateDisplay = "Standard QoS (Unprioritized)";
        State = OptimizationState.Recommended;
        return Task.FromResult(State);
    }

    public Task<OptimizationResult> ApplyAsync(HardwareInfo hw, SystemInfo sys, GameLoopConfig gl)
    {
        if (!PermissionManager.IsAdministrator)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, "Administrator privileges are required to configure Windows QoS policies."));
        }

        try
        {
            // 1. Enable Non-NLA QoS under TCPIP
            using (var tcpKey = Registry.LocalMachine.CreateSubKey(TcpipQosPath))
            {
                if (tcpKey != null)
                {
                    var prevNla = tcpKey.GetValue("Do not use NLA")?.ToString();
                    BackupManager.RecordBackup(new BackupEntry
                    {
                        ModuleId = Id,
                        Title = "QoS NLA Bypass",
                        Category = Category,
                        TargetType = "Registry",
                        TargetPath = $@"HKLM\{TcpipQosPath}",
                        ValueName = "Do not use NLA",
                        PreviousValue = prevNla,
                        NewValue = "1",
                        Description = "Enable QoS on all network adapters without NLA restriction"
                    });
                    tcpKey.SetValue("Do not use NLA", "1", RegistryValueKind.String);
                }
            }

            // 2. Configure GameLoop UDP QoS Policy
            var apps = new[]
            {
                ("GameLoop_UDP_Priority", "AndroidEmulator.exe"),
                ("GameLoop_Aow_QoS", "aow_exe.exe"),
                ("GameLoop_Market_QoS", "AppMarket.exe")
            };

            foreach (var (policyName, appName) in apps)
            {
                using var subKey = Registry.LocalMachine.CreateSubKey($@"{QosPolicyPath}\{policyName}");
                if (subKey != null)
                {
                    subKey.SetValue("Version", "1.0", RegistryValueKind.String);
                    subKey.SetValue("Application Name", appName, RegistryValueKind.String);
                    subKey.SetValue("Protocol", "UDP", RegistryValueKind.String);
                    subKey.SetValue("Local Port", "*", RegistryValueKind.String);
                    subKey.SetValue("Local IP", "*", RegistryValueKind.String);
                    subKey.SetValue("Local IP Prefix Length", "*", RegistryValueKind.String);
                    subKey.SetValue("Remote Port", "*", RegistryValueKind.String);
                    subKey.SetValue("Remote IP", "*", RegistryValueKind.String);
                    subKey.SetValue("Remote IP Prefix Length", "*", RegistryValueKind.String);
                    subKey.SetValue("DSCP Value", "46", RegistryValueKind.String);
                    subKey.SetValue("Throttle Rate", "-1", RegistryValueKind.String);
                }
            }

            IsOptimized = true;
            CurrentStateDisplay = "DSCP 46 Priority Active";
            State = OptimizationState.Optimized;

            Logger.Success(Title, "Configured Windows QoS policies: GameLoop UDP streams tagged with DSCP 46 (Expedited Forwarding).");
            return Task.FromResult(OptimizationResult.Ok(Id, "GameLoop UDP packet streams tagged with DSCP 46 Expedited Forwarding QoS."));
        }
        catch (Exception ex)
        {
            Logger.Error(Title, $"Failed to apply Windows QoS policies: {ex.Message}");
            return Task.FromResult(OptimizationResult.Fail(Id, ex.Message, ex));
        }
    }

    public Task<OptimizationResult> RollbackAsync(BackupEntry? backup)
    {
        try
        {
            var apps = new[] { "GameLoop_UDP_Priority", "GameLoop_Aow_QoS", "GameLoop_Market_QoS" };
            foreach (var app in apps)
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree($@"{QosPolicyPath}\{app}", throwOnMissingSubKey: false);
                }
                catch { }
            }

            var target = backup ?? BackupManager.GetLatestForModule(Id);
            if (target != null)
            {
                BackupManager.RestoreEntry(target);
            }

            IsOptimized = false;
            CurrentStateDisplay = "Standard QoS (Unprioritized)";
            State = OptimizationState.NotOptimized;
            return Task.FromResult(OptimizationResult.Ok(Id, "Reverted Windows QoS policies."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OptimizationResult.Fail(Id, $"Rollback failed: {ex.Message}"));
        }
    }

    public Task<bool> VerifyAsync()
    {
        try
        {
            using var qosKey = Registry.LocalMachine.OpenSubKey($@"{QosPolicyPath}\GameLoop_UDP_Priority");
            return Task.FromResult(qosKey != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
