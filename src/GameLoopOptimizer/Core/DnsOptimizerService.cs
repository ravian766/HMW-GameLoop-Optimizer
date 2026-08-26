using System.Diagnostics;
using System.Net.NetworkInformation;

namespace GameLoopOptimizer.Core;

public class DnsPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RegionPingResult
{
    public string RegionName { get; set; } = string.Empty;
    public string HostOrIp { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public bool IsSuccess { get; set; }
    public string StatusText => IsSuccess ? $"{LatencyMs} ms" : "Timeout";
}

public static class DnsOptimizerService
{
    public static readonly List<DnsPreset> Presets = new()
    {
        new DnsPreset
        {
            Id = "cloudflare",
            Name = "Cloudflare Gaming (1.1.1.1)",
            PrimaryDns = "1.1.1.1",
            SecondaryDns = "1.0.0.1",
            Description = "Fastest global gaming resolver with minimal routing hops."
        },
        new DnsPreset
        {
            Id = "google",
            Name = "Google Public DNS (8.8.8.8)",
            PrimaryDns = "8.8.8.8",
            SecondaryDns = "8.8.4.4",
            Description = "Highly reliable with massive global edge presence."
        },
        new DnsPreset
        {
            Id = "quad9",
            Name = "Quad9 Secure (9.9.9.9)",
            PrimaryDns = "9.9.9.9",
            SecondaryDns = "149.112.112.112",
            Description = "Low latency with built-in malicious domain blocking."
        },
        new DnsPreset
        {
            Id = "opendns",
            Name = "OpenDNS (208.67.222.222)",
            PrimaryDns = "208.67.222.222",
            SecondaryDns = "208.67.220.220",
            Description = "Cisco Anycast network optimized for stability."
        }
    };

    public static readonly List<(string Region, string Host)> GameServers = new()
    {
        ("Middle East (Dubai)", "15.185.0.1"),
        ("Europe (Frankfurt)", "3.120.0.0"),
        ("Asia (Singapore)", "13.228.0.0"),
        ("Asia (Mumbai)", "13.126.0.0"),
        ("North America (US East)", "3.80.0.0")
    };

    public static async Task<bool> FlushDnsCacheAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                Logger.Success("DnsOptimizer", "Flushed Windows DNS Resolver cache.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("DnsOptimizer", $"DNS flush error: {ex.Message}");
                return false;
            }
        });
    }

    public static async Task<List<RegionPingResult>> BenchmarkGameRegionsAsync()
    {
        var results = new List<RegionPingResult>();

        foreach (var (region, host) in GameServers)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 1200);

                results.Add(new RegionPingResult
                {
                    RegionName = region,
                    HostOrIp = host,
                    LatencyMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1,
                    IsSuccess = reply.Status == IPStatus.Success
                });
            }
            catch
            {
                results.Add(new RegionPingResult
                {
                    RegionName = region,
                    HostOrIp = host,
                    LatencyMs = -1,
                    IsSuccess = false
                });
            }
        }

        return results;
    }

    public static async Task<bool> ApplyDnsPresetAsync(DnsPreset preset)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Set DNS on primary network adapters using PowerShell for reliability
                var psScript = $@"
$adapters = Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' -and $_.Virtual -eq $false }}
foreach ($a in $adapters) {{
    Set-DnsClientServerAddress -InterfaceAlias $a.Name -ServerAddresses ('{preset.PrimaryDns}', '{preset.SecondaryDns}') -ErrorAction SilentlyContinue
}}
";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"").Replace("\r\n", " ")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(4000);

                _ = FlushDnsCacheAsync();
                Logger.Success("DnsOptimizer", $"Configured {preset.Name} across active network adapters.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("DnsOptimizer", $"Failed to apply DNS preset: {ex.Message}");
                return false;
            }
        });
    }

    public static async Task<bool> ResetDnsToDhcpAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var psScript = @"
$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.Virtual -eq $false }
foreach ($a in $adapters) {
    Set-DnsClientServerAddress -InterfaceAlias $a.Name -ResetServerAddresses -ErrorAction SilentlyContinue
}
";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"").Replace("\r\n", " ")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(4000);

                _ = FlushDnsCacheAsync();
                Logger.Info("DnsOptimizer", "Reset network adapters DNS to Automatic (DHCP).");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("DnsOptimizer", $"Failed to reset DNS: {ex.Message}");
                return false;
            }
        });
    }
}
