using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace GameLoopOptimizer.Core;

public class NetworkDiagnosticResult
{
    public string TargetHost { get; set; } = "1.1.1.1";
    public int SamplesSent { get; set; }
    public int SamplesReceived { get; set; }
    public double PacketLossPercent => SamplesSent > 0 ? Math.Round(((SamplesSent - SamplesReceived) / (double)SamplesSent) * 100.0, 1) : 0.0;

    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double AvgLatencyMs { get; set; }
    public double JitterMs { get; set; }
    public double DnsResolutionMs { get; set; }

    public string ActiveInterfaceName { get; set; } = "Unknown";
    public string ActiveInterfaceType { get; set; } = "Ethernet/Wi-Fi";
    public long InterfaceSpeedMbps { get; set; }

    public string QualityRating { get; set; } = "Unknown";
    public string Recommendation { get; set; } = string.Empty;
}

public static class NetworkDiagnosticsService
{
    public static async Task<NetworkDiagnosticResult> RunDiagnosticsAsync(
        string targetHost = "1.1.1.1",
        int pingCount = 6,
        int timeoutMs = 1200)
    {
        var result = new NetworkDiagnosticResult
        {
            TargetHost = targetHost,
            SamplesSent = pingCount
        };

        // 1. Detect Active Network Interface
        try
        {
            var activeNic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (activeNic != null)
            {
                result.ActiveInterfaceName = activeNic.Name;
                result.ActiveInterfaceType = activeNic.NetworkInterfaceType.ToString();
                result.InterfaceSpeedMbps = activeNic.Speed > 0 ? activeNic.Speed / (1000 * 1000) : 0;
            }
        }
        catch { }

        // 2. DNS Resolution Latency
        try
        {
            var sw = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(targetHost);
            sw.Stop();
            result.DnsResolutionMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
        }
        catch
        {
            result.DnsResolutionMs = -1; // Could not resolve hostname directly
        }

        // 3. ICMP Ping Sequence
        var latencies = new List<double>();
        using var pinger = new Ping();

        for (int i = 0; i < pingCount; i++)
        {
            try
            {
                var reply = await pinger.SendPingAsync(targetHost, timeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    latencies.Add(reply.RoundtripTime);
                    result.SamplesReceived++;
                }
            }
            catch
            {
                // Timeout or network drop
            }

            if (i < pingCount - 1)
            {
                await Task.Delay(100); // Small interval between samples
            }
        }

        // 4. Calculate Statistics
        if (latencies.Count > 0)
        {
            result.MinLatencyMs = Math.Round(latencies.Min(), 1);
            result.MaxLatencyMs = Math.Round(latencies.Max(), 1);
            result.AvgLatencyMs = Math.Round(latencies.Average(), 1);

            // Jitter: mean deviation between consecutive samples or standard deviation
            if (latencies.Count > 1)
            {
                double avg = result.AvgLatencyMs;
                double sumOfSquares = latencies.Select(val => (val - avg) * (val - avg)).Sum();
                result.JitterMs = Math.Round(Math.Sqrt(sumOfSquares / latencies.Count), 1);
            }
            else
            {
                result.JitterMs = 0.0;
            }
        }

        // 5. Honest Rating & Recommendations
        if (result.PacketLossPercent > 5.0)
        {
            result.QualityRating = "Poor (Packet Loss)";
            result.Recommendation = "Packet loss causes teleporting and desync in PUBG Mobile. Prefer Ethernet cable over Wi-Fi.";
        }
        else if (result.AvgLatencyMs < 35.0 && result.JitterMs < 4.0)
        {
            result.QualityRating = "Optimal (Esports Ready)";
            result.Recommendation = "Low latency and rock-solid jitter. Ideal routing for competitive combat.";
        }
        else if (result.AvgLatencyMs < 75.0 && result.JitterMs < 12.0)
        {
            result.QualityRating = "Good (Playable)";
            result.Recommendation = "Connection is stable. Optimizing DNS or QoS may shave another 2-5ms off routing variance.";
        }
        else
        {
            result.QualityRating = "High Latency / Jitter";
            result.Recommendation = "Noticeable ping variance detected. Close background downloaders and consider DNS optimization.";
        }

        return result;
    }
}
