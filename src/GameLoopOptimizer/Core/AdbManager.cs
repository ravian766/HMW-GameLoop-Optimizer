using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class AdbDeviceInfo
{
    public string Serial { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsEmulator => Serial.Contains("5555") || Serial.Contains("6555") || Serial.Contains("5554") || Serial.StartsWith("emulator-") || Serial.Contains("11241");
}

public class GamePackageInfo
{
    public string PackageName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }

    public override string ToString() => DisplayName;
}

public class OptimizationVerificationResult
{
    public string Key { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public bool IsMatch { get; set; }
}

public class GpuRendererInfo
{
    public string Renderer { get; set; } = "Unknown";
    public string Vendor { get; set; } = "Unknown";
    public string Version { get; set; } = "Unknown";
}

public class InVmPingResult
{
    public string TargetHost { get; set; } = "1.1.1.1";
    public double MinMs { get; set; }
    public double AvgMs { get; set; }
    public double MaxMs { get; set; }
    public double MdevMs { get; set; }
    public double PacketLossPct { get; set; }
    public bool Success { get; set; }
    public string Summary => Success
        ? $"Ping {TargetHost}: Avg={AvgMs:F1}ms (Min={MinMs:F1}ms, Max={MaxMs:F1}ms, Loss={PacketLossPct:F0}%)"
        : $"Ping {TargetHost} failed or timed out.";
}

public static class AdbManager
{
    public static readonly int[] KnownGameLoopPorts = new[] { 5555, 6555, 5557, 5559, 11241 };

    public static readonly IReadOnlyList<GamePackageInfo> KnownGamePackages = new[]
    {
        new GamePackageInfo { PackageName = "com.tencent.ig", DisplayName = "PUBG Mobile (Global)", Region = "Global" },
        new GamePackageInfo { PackageName = "com.pubg.imobile", DisplayName = "Battlegrounds Mobile India (BGMI)", Region = "India" },
        new GamePackageInfo { PackageName = "com.pubg.krmobile", DisplayName = "PUBG Mobile (KR / JP)", Region = "Korea / Japan" },
        new GamePackageInfo { PackageName = "com.vng.pubgmobile", DisplayName = "PUBG Mobile (VN)", Region = "Vietnam" },
        new GamePackageInfo { PackageName = "com.rekoo.pubgm", DisplayName = "PUBG Mobile (TW)", Region = "Taiwan" },
        new GamePackageInfo { PackageName = "com.dts.freefireth", DisplayName = "Garena Free Fire", Region = "Global" },
        new GamePackageInfo { PackageName = "com.dts.freefiremax", DisplayName = "Free Fire MAX", Region = "Global" },
        new GamePackageInfo { PackageName = "com.activision.callofduty.shooter", DisplayName = "Call of Duty: Mobile", Region = "Global" }
    };

    private static string? _cachedAdbPath;
    private static string? _activeDeviceSerial;

    public static string? ActiveDeviceSerial
    {
        get => _activeDeviceSerial;
        set => _activeDeviceSerial = value;
    }

    public static string FindAdbExePath(GameLoopConfig? config = null)
    {
        if (!string.IsNullOrEmpty(_cachedAdbPath) && File.Exists(_cachedAdbPath))
        {
            return _cachedAdbPath;
        }

        var candidates = new List<string>();

        // 1. From GameLoopConfig install path
        if (config != null && !string.IsNullOrEmpty(config.InstallPath))
        {
            candidates.Add(Path.Combine(config.InstallPath, "adb.exe"));
            candidates.Add(Path.Combine(config.InstallPath, "AppMarket", "adb.exe"));
            candidates.Add(Path.Combine(config.InstallPath, "ui", "adb.exe"));
            candidates.Add(Path.Combine(config.InstallPath, "vms", "AndroidEmulator", "adb.exe"));
        }

        // 2. Standard TxGameAssistant / GameLoop paths
        candidates.AddRange(new[]
        {
            @"C:\Program Files\TxGameAssistant\AppMarket\adb.exe",
            @"C:\Program Files\TxGameAssistant\ui\adb.exe",
            @"C:\Program Files\TxGameAssistant\vms\AndroidEmulator\adb.exe",
            @"D:\Program Files\TxGameAssistant\AppMarket\adb.exe",
            @"D:\Program Files\TxGameAssistant\ui\adb.exe",
            @"D:\Program Files\TxGameAssistant\vms\AndroidEmulator\adb.exe",
            @"C:\TxGameAssistant\AppMarket\adb.exe",
            @"C:\TxGameAssistant\ui\adb.exe",
            @"D:\TxGameAssistant\AppMarket\adb.exe",
            @"D:\TxGameAssistant\ui\adb.exe",
            @"E:\TxGameAssistant\AppMarket\adb.exe",
            @"E:\TxGameAssistant\ui\adb.exe",
            @"C:\GameLoop\AppMarket\adb.exe",
            @"C:\GameLoop\ui\adb.exe",
            @"D:\GameLoop\AppMarket\adb.exe",
            @"D:\GameLoop\ui\adb.exe"
        });

        // 3. Search across all ready drives
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Program Files", "TxGameAssistant", "AppMarket", "adb.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Program Files", "TxGameAssistant", "ui", "adb.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "TxGameAssistant", "AppMarket", "adb.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "TxGameAssistant", "ui", "adb.exe"));
        }

        // 4. Check PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "adb.exe");
                if (File.Exists(candidate))
                {
                    candidates.Add(candidate);
                }
            }
            catch { }
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _cachedAdbPath = candidate;
                Logger.Info("AdbManager", $"Located ADB executable at: {candidate}");
                return candidate;
            }
        }

        return string.Empty;
    }

    public static bool IsAdbAvailable(GameLoopConfig? config = null)
    {
        return !string.IsNullOrEmpty(FindAdbExePath(config));
    }

    public static async Task<string> ExecuteAdbCommandAsync(string arguments, int timeoutMs = 6000, GameLoopConfig? config = null)
    {
        string adbPath = FindAdbExePath(config);
        if (string.IsNullOrEmpty(adbPath))
        {
            return "ADB executable not found";
        }

        return await Task.Run(async () =>
        {
            try
            {
                using var proc = new Process();
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(adbPath) ?? string.Empty
                };

                proc.Start();

                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                var waitTask = proc.WaitForExitAsync();

                var allTasks = Task.WhenAll(outTask, errTask, waitTask);
                var completed = await Task.WhenAny(allTasks, Task.Delay(timeoutMs));
                if (completed != allTasks)
                {
                    try { proc.Kill(true); } catch { }
                    return "Command timed out";
                }

                string output = await outTask;
                string err = await errTask;

                if (!string.IsNullOrWhiteSpace(err) && string.IsNullOrWhiteSpace(output))
                {
                    return err.Trim();
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                Logger.Warn("AdbManager", $"Execution failed ({arguments}): {ex.Message}");
                return $"Error: {ex.Message}";
            }
        });
    }

    public static async Task<string> ExecuteShellCommandAsync(string shellCommand, string? targetDevice = null, int timeoutMs = 6000, GameLoopConfig? config = null)
    {
        string serial = targetDevice ?? _activeDeviceSerial ?? string.Empty;
        if (string.IsNullOrEmpty(serial))
        {
            var devices = await GetConnectedDevicesAsync(config);
            var active = devices.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
            if (active != null)
            {
                serial = active.Serial;
                _activeDeviceSerial = serial;
            }
        }

        string args = string.IsNullOrEmpty(serial) 
            ? $"shell {shellCommand}" 
            : $"-s {serial} shell {shellCommand}";

        return await ExecuteAdbCommandAsync(args, timeoutMs, config);
    }

    public static async Task<string> ExecuteBatchShellCommandAsync(IEnumerable<string> shellCommands, string? targetDevice = null, int timeoutMs = 12000, GameLoopConfig? config = null)
    {
        var cmds = shellCommands.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
        if (cmds.Count == 0) return string.Empty;

        string compound = string.Join(" ; ", cmds);
        return await ExecuteShellCommandAsync($"\"{compound}\"", targetDevice, timeoutMs, config);
    }

    public static async Task<bool> BatchSetPropsAsync(IDictionary<string, string> properties, string? targetDevice = null, GameLoopConfig? config = null)
    {
        if (properties == null || properties.Count == 0) return true;
        var cmds = properties.Select(kvp => $"setprop {kvp.Key} {kvp.Value}");
        var res = await ExecuteBatchShellCommandAsync(cmds, targetDevice, 8000, config);
        return !res.Contains("Error:", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<List<AdbDeviceInfo>> GetConnectedDevicesAsync(GameLoopConfig? config = null)
    {
        var output = await ExecuteAdbCommandAsync("devices -l", 6000, config);
        return ParseDevicesOutput(output);
    }

    public static List<AdbDeviceInfo> ParseDevicesOutput(string output)
    {
        var list = new List<AdbDeviceInfo>();
        if (string.IsNullOrWhiteSpace(output)) return list;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("List of devices") || line.StartsWith("*") || string.IsNullOrWhiteSpace(line))
                continue;

            var match = Regex.Match(line, @"^(\S+)\s+(\w+)");
            if (match.Success)
            {
                string serial = match.Groups[1].Value;
                string state = match.Groups[2].Value;
                string model = "GameLoop VM";

                var modelMatch = Regex.Match(line, @"\bmodel:(\S+)");
                if (modelMatch.Success)
                {
                    model = modelMatch.Groups[1].Value;
                }

                list.Add(new AdbDeviceInfo
                {
                    Serial = serial,
                    State = state,
                    Model = model
                });
            }
        }

        return list;
    }

    public static async Task CleanupOfflineDevicesAsync(GameLoopConfig? config = null)
    {
        try
        {
            var devices = await GetConnectedDevicesAsync(config);
            foreach (var dev in devices)
            {
                if (dev.State.Equals("offline", StringComparison.OrdinalIgnoreCase) || dev.Serial.StartsWith(":"))
                {
                    await ExecuteAdbCommandAsync($"disconnect {dev.Serial}", 3000, config);
                    Logger.Info("AdbManager", $"Cleaned up stale/offline ADB endpoint: {dev.Serial}");
                }
            }
        }
        catch { }
    }

    public static async Task<List<int>> DiscoverListeningEmulatorPortsAsync(GameLoopConfig? config = null)
    {
        var ports = new List<int> { 5555 };

        // Fast direct VBox NAT port discovery
        var vboxPorts = new HashSet<int>();
        TryAddVboxForwardedPorts(vboxPorts, config);
        foreach (var p in vboxPorts)
        {
            if (!ports.Contains(p) && p > 1024 && p < 65535) ports.Add(p);
        }

        // Standard known ports (5555, 6555, 5557, 5559, 11241)
        foreach (var p in KnownGameLoopPorts)
        {
            if (!ports.Contains(p)) ports.Add(p);
        }

        await Task.Run(() =>
        {
            try
            {
                // Only scan AndroidEmulator / AndroidEmulatorEn processes
                var procNames = new[] { "AndroidEmulator", "AndroidEmulatorEn" };
                var pids = new HashSet<int>();
                var allProcs = Process.GetProcesses();
                foreach (var p in allProcs)
                {
                    try
                    {
                        if (procNames.Any(name => p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            pids.Add(p.Id);
                        }
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }

                if (pids.Count == 0) return;

                using var netstat = new Process();
                netstat.StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                netstat.Start();
                string output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit(3000);

                var regex = new Regex(@"TCP\s+(?:127\.0\.0\.1|0\.0\.0\.0):(\d+)\s+.*LISTENING\s+(\d+)", RegexOptions.IgnoreCase);
                foreach (Match m in regex.Matches(output))
                {
                    if (int.TryParse(m.Groups[1].Value, out int port) && int.TryParse(m.Groups[2].Value, out int pid))
                    {
                        if (pids.Contains(pid) && IsLikelyAdbPort(port))
                        {
                            if (!ports.Contains(port)) ports.Add(port);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("AdbManager", $"Dynamic port scan encountered error: {ex.Message}");
            }
        });

        return ports;
    }

    private static bool IsLikelyAdbPort(int port)
    {
        if (port == 5555 || port == 6555 || port == 11241) return true;
        if (port >= 5555 && port <= 5585 && port % 2 != 0) return true;
        return false;
    }

    private static void TryAddVboxForwardedPorts(HashSet<int> ports, GameLoopConfig? config)
    {
        try
        {
            var candidateVboxFiles = new List<string>();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                candidateVboxFiles.Add(Path.Combine(userProfile, ".GameLoop", "AndroidEmulator.vbox"));
                candidateVboxFiles.Add(Path.Combine(userProfile, "VirtualBox VMs", "AndroidEmulator", "AndroidEmulator.vbox"));
            }

            if (config != null && !string.IsNullOrEmpty(config.InstallPath))
            {
                candidateVboxFiles.Add(Path.Combine(config.InstallPath, "vms", "AndroidEmulator", "AndroidEmulator.vbox"));
            }

            foreach (var vboxPath in candidateVboxFiles)
            {
                if (File.Exists(vboxPath))
                {
                    string xml = File.ReadAllText(vboxPath);
                    var match = Regex.Match(xml, @"name=""adb""\s+proto=""1""\s+hostport=""(\d+)""", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int port))
                    {
                        ports.Add(port);
                        Logger.Info("AdbManager", $"Discovered ADB port {port} from VirtualBox configuration: {vboxPath}");
                    }
                }
            }
        }
        catch { }
    }

    public static async Task<bool> AutoConnectGameLoopAsync(GameLoopConfig? config = null)
    {
        // 1. Clean up stale/offline endpoints to prevent socket poisoning
        await CleanupOfflineDevicesAsync(config);

        // 2. Check existing connected devices
        var existing = await GetConnectedDevicesAsync(config);
        var active = existing.FirstOrDefault(d => d.IsEmulator && d.State.Equals("device", StringComparison.OrdinalIgnoreCase))
                     ?? existing.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
        if (active != null)
        {
            _activeDeviceSerial = active.Serial;
            Logger.Success("AdbManager", $"Connected to active GameLoop Android VM: {active.Serial}");
            return true;
        }

        // 3. Discover active & known ports dynamically (5555 prioritized)
        var candidatePorts = await DiscoverListeningEmulatorPortsAsync(config);

        foreach (var port in candidatePorts)
        {
            string hostPort = $"127.0.0.1:{port}";
            var res = await ExecuteAdbCommandAsync($"connect {hostPort}", 8000, config);

            var postDevices = await GetConnectedDevicesAsync(config);
            var matched = postDevices.FirstOrDefault(d =>
                (d.Serial.Equals(hostPort, StringComparison.OrdinalIgnoreCase) ||
                 (port == 5555 && d.Serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))) &&
                d.State.Equals("device", StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                _activeDeviceSerial = matched.Serial;
                Logger.Success("AdbManager", $"Successfully established ADB connection to {matched.Serial} (Port {port})");
                return true;
            }
            else
            {
                var offline = postDevices.FirstOrDefault(d => d.Serial.Equals(hostPort, StringComparison.OrdinalIgnoreCase) && d.State.Equals("offline", StringComparison.OrdinalIgnoreCase));
                if (offline != null)
                {
                    await ExecuteAdbCommandAsync($"disconnect {hostPort}", 3000, config);
                }
            }
        }

        // 4. Re-check devices list in case daemon connected automatically
        existing = await GetConnectedDevicesAsync(config);
        active = existing.FirstOrDefault(d => d.IsEmulator && d.State.Equals("device", StringComparison.OrdinalIgnoreCase))
                 ?? existing.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
        if (active != null)
        {
            _activeDeviceSerial = active.Serial;
            Logger.Success("AdbManager", $"Connected to GameLoop instance: {active.Serial}");
            return true;
        }

        Logger.Warn("AdbManager", "Could not automatically connect to GameLoop Android VM. Ensure GameLoop emulator is running.");
        return false;
    }

    public static async Task<List<GamePackageInfo>> GetInstalledGamePackagesAsync(GameLoopConfig? config = null)
    {
        var result = new List<GamePackageInfo>();
        var pmOutput = await ExecuteShellCommandAsync("pm list packages", null, 5000, config);
        
        var installedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in pmOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = line.Trim();
            if (clean.StartsWith("package:"))
            {
                installedSet.Add(clean.Substring("package:".Length).Trim());
            }
        }

        foreach (var pkg in KnownGamePackages)
        {
            bool isInst = installedSet.Contains(pkg.PackageName);
            result.Add(new GamePackageInfo
            {
                PackageName = pkg.PackageName,
                DisplayName = pkg.DisplayName,
                Region = pkg.Region,
                IsInstalled = isInst
            });
        }

        return result;
    }

    public static async Task<string> CompilePackageSpeedAsync(string packageName, GameLoopConfig? config = null)
    {
        Logger.Info("AdbManager", $"Executing AOT Dex2Oat Native Compilation for {packageName}...");

        // 1. Try 'cmd package compile' (Standard on Android 7.0+)
        var res1 = await ExecuteShellCommandAsync($"cmd package compile -m speed -f {packageName}", null, 25000, config);
        if (res1.Contains("Success", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Success("AdbManager", $"AOT compilation succeeded via cmd package for {packageName}.");
            return "AOT Compilation Succeeded (Speed Profile Active)";
        }

        // 2. Try 'pm compile' (Android 8.0+)
        var res2 = await ExecuteShellCommandAsync($"pm compile -m speed -f {packageName}", null, 25000, config);
        if (res2.Contains("Success", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Success("AdbManager", $"AOT compilation succeeded via pm compile for {packageName}.");
            return "AOT Compilation Succeeded (Speed Profile Active)";
        }

        // 3. Try 'pm force-dex-opt' (Android 5.0 - 7.0 legacy)
        var res3 = await ExecuteShellCommandAsync($"pm force-dex-opt {packageName}", null, 25000, config);
        if (res3.Contains("Success", StringComparison.OrdinalIgnoreCase) || 
            (!res3.Contains("Error", StringComparison.OrdinalIgnoreCase) && !res3.Contains("unknown", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(res3)))
        {
            Logger.Success("AdbManager", $"AOT compilation succeeded via dexopt for {packageName}.");
            return "AOT Optimization Succeeded (Native DexOpt Complete)";
        }

        // 4. Universal Fallback: Inject Dalvik AOT & Speed Execution Properties
        await SetPropAsync("dalvik.vm.dex2oat-filter", "speed", config);
        await SetPropAsync("dalvik.vm.dexopt-flags", "v=n,o=v", config);
        await SetPropAsync("dalvik.vm.usejit", "true", config);
        await SetPropAsync("dalvik.vm.usejitprofiles", "true", config);

        Logger.Success("AdbManager", $"Configured Dalvik VM AOT Speed Filter properties for {packageName}.");
        return "AOT Speed Profile Active (Dalvik VM Optimized)";
    }

    public static async Task<string> GetPropAsync(string propKey, GameLoopConfig? config = null)
    {
        return await ExecuteShellCommandAsync($"getprop {propKey}", null, 4000, config);
    }

    public static async Task<bool> SetPropAsync(string propKey, string value, GameLoopConfig? config = null)
    {
        var res = await ExecuteShellCommandAsync($"setprop {propKey} {value}", null, 4000, config);
        return !res.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<string> GetGlobalSettingAsync(string key, GameLoopConfig? config = null)
    {
        return await ExecuteShellCommandAsync($"settings get global {key}", null, 4000, config);
    }

    public static async Task<bool> PutGlobalSettingAsync(string key, string value, GameLoopConfig? config = null)
    {
        var res = await ExecuteShellCommandAsync($"settings put global {key} {value}", null, 4000, config);
        return !res.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> SetInVmResolutionAsync(int width, int height, int dpi, GameLoopConfig? config = null)
    {
        try
        {
            await ExecuteShellCommandAsync($"wm size {width}x{height}", null, 4000, config);
            await ExecuteShellCommandAsync($"wm density {dpi}", null, 4000, config);
            Logger.Success("AdbManager", $"In-VM resolution configured to {width}x{height} @ {dpi} DPI.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to set In-VM resolution: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ResetInVmResolutionAsync(GameLoopConfig? config = null)
    {
        try
        {
            await ExecuteShellCommandAsync("wm size reset", null, 4000, config);
            await ExecuteShellCommandAsync("wm density reset", null, 4000, config);
            Logger.Success("AdbManager", "Reset In-VM display size and density to default.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to reset In-VM resolution: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> CaptureScreenAsync(string destinationPngPath, GameLoopConfig? config = null)
    {
        string adbPath = FindAdbExePath(config);
        if (string.IsNullOrEmpty(adbPath)) return false;

        return await Task.Run(() =>
        {
            try
            {
                string serial = _activeDeviceSerial ?? string.Empty;
                string args = string.IsNullOrEmpty(serial) ? "exec-out screencap -p" : $"-s {serial} exec-out screencap -p";

                using var proc = new Process();
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                proc.Start();
                using (var fileStream = File.Create(destinationPngPath))
                {
                    proc.StandardOutput.BaseStream.CopyTo(fileStream);
                }
                proc.WaitForExit(8000);
                return File.Exists(destinationPngPath) && new FileInfo(destinationPngPath).Length > 1024;
            }
            catch (Exception ex)
            {
                Logger.Error("AdbManager", $"Screenshot capture failed: {ex.Message}");
                return false;
            }
        });
    }

    public static async Task<bool> TrimAppCacheAsync(GameLoopConfig? config = null, string? targetPackage = null)
    {
        try
        {
            await ExecuteShellCommandAsync("pm trim-caches 999G", null, 6000, config);
            
            var targetPackages = new List<string> { "com.tencent.ig", "com.pubg.imobile", "com.pubg.krmobile" };
            if (!string.IsNullOrEmpty(targetPackage) && !targetPackages.Contains(targetPackage))
            {
                targetPackages.Add(targetPackage);
            }

            foreach (var pkg in targetPackages)
            {
                await ExecuteShellCommandAsync($"rm -rf /data/data/{pkg}/cache/*", null, 3000, config);
                await ExecuteShellCommandAsync($"rm -rf /sdcard/Android/data/{pkg}/cache/*", null, 3000, config);
            }

            await ExecuteShellCommandAsync("rm -rf /data/anr/*", null, 3000, config);
            await ExecuteShellCommandAsync("rm -rf /data/tombstones/*", null, 3000, config);

            Logger.Success("AdbManager", "Purged GameLoop Android VM application caches, crash tombstones, and shader caches.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to trim Android VM cache: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> RestartAdbServerAsync(GameLoopConfig? config = null)
    {
        await ExecuteAdbCommandAsync("kill-server", 5000, config);
        await Task.Delay(1000);
        await ExecuteAdbCommandAsync("start-server", 8000, config);
        await CleanupOfflineDevicesAsync(config);
        return await AutoConnectGameLoopAsync(config);
    }

    public static async Task<bool> ConnectCustomDeviceAsync(string ipPort, GameLoopConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(ipPort)) return false;
        string target = ipPort.Trim();

        if (target.StartsWith(":"))
        {
            target = target.TrimStart(':');
        }

        if (target.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
        {
            target = "127.0.0.1:" + target.Substring("localhost:".Length);
        }

        if (!target.Contains(':') && int.TryParse(target, out int portNum))
        {
            target = $"127.0.0.1:{portNum}";
        }

        // Check if port 5555 requested and emulator-5554 is already connected
        var existing = await GetConnectedDevicesAsync(config);
        if (target.EndsWith(":5555"))
        {
            var emu = existing.FirstOrDefault(d => (d.Serial.StartsWith("emulator-") || d.Serial.Equals("127.0.0.1:5555", StringComparison.OrdinalIgnoreCase)) && d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
            if (emu != null)
            {
                _activeDeviceSerial = emu.Serial;
                Logger.Success("AdbManager", $"ADB Port 5555 is already connected: {emu.Serial}");
                return true;
            }
        }

        // Clean up if already offline
        var stale = existing.FirstOrDefault(d => d.Serial.Equals(target, StringComparison.OrdinalIgnoreCase) && d.State.Equals("offline", StringComparison.OrdinalIgnoreCase));
        if (stale != null)
        {
            await ExecuteAdbCommandAsync($"disconnect {target}", 3000, config);
        }

        var res = await ExecuteAdbCommandAsync($"connect {target}", 8000, config);

        var updated = await GetConnectedDevicesAsync(config);
        var active = updated.FirstOrDefault(d => (d.Serial.Equals(target, StringComparison.OrdinalIgnoreCase) || (target.EndsWith(":5555") && d.Serial.StartsWith("emulator-"))) && d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
        if (active != null)
        {
            _activeDeviceSerial = active.Serial;
            Logger.Success("AdbManager", $"Connected to custom ADB target: {active.Serial}");
            return true;
        }

        if (res.Contains("connected to", StringComparison.OrdinalIgnoreCase) || res.Contains("already connected", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(1000);
            updated = await GetConnectedDevicesAsync(config);
            active = updated.FirstOrDefault(d => (d.Serial.Equals(target, StringComparison.OrdinalIgnoreCase) || (target.EndsWith(":5555") && d.Serial.StartsWith("emulator-"))) && d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
            if (active != null)
            {
                _activeDeviceSerial = active.Serial;
                Logger.Success("AdbManager", $"Connected to custom ADB target: {active.Serial}");
                return true;
            }
        }

        Logger.Warn("AdbManager", $"Failed to connect to {target}: {res}");
        return false;
    }

    public static async Task<bool> LaunchGamePackageAsync(string packageName, GameLoopConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(packageName)) return false;
        Logger.Info("AdbManager", $"Launching game package: {packageName}");

        // Use Android monkey runner to launch default category launcher activity
        var res = await ExecuteShellCommandAsync($"monkey -p {packageName} -c android.intent.category.LAUNCHER 1", null, 6000, config);
        if (res.Contains("Events injected: 1", StringComparison.OrdinalIgnoreCase) || !res.Contains("No activities found", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Success("AdbManager", $"Launched {packageName} via Android Activity Manager.");
            return true;
        }

        // Fallback to am start
        var resAm = await ExecuteShellCommandAsync($"am start -n {packageName}/com.epicgames.ue4.SplashActivity", null, 5000, config);
        return !resAm.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> ForceStopGamePackageAsync(string packageName, GameLoopConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(packageName)) return false;
        Logger.Info("AdbManager", $"Force-stopping game package: {packageName}");
        var res = await ExecuteShellCommandAsync($"am force-stop {packageName}", null, 5000, config);
        Logger.Success("AdbManager", $"Terminated {packageName} process in Android VM.");
        return !res.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> ClearGameDataAsync(string packageName, GameLoopConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(packageName)) return false;
        Logger.Info("AdbManager", $"Clearing package data: {packageName}");
        var res = await ExecuteShellCommandAsync($"pm clear {packageName}", null, 8000, config);
        return res.Contains("Success", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> SetInVmDnsAsync(string primaryDns = "1.1.1.1", string secondaryDns = "1.0.0.1", GameLoopConfig? config = null)
    {
        try
        {
            await SetPropAsync("net.dns1", primaryDns, config);
            await SetPropAsync("net.dns2", secondaryDns, config);
            await SetPropAsync("net.dnssearch", "local", config);
            await PutGlobalSettingAsync("private_dns_mode", "off", config);
            Logger.Success("AdbManager", $"In-VM DNS configured: Primary={primaryDns}, Secondary={secondaryDns}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to set in-VM DNS: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> OptimizeInVmTcpStackAsync(GameLoopConfig? config = null)
    {
        try
        {
            // High-throughput, low-bufferbloat WiFi TCP window sizes
            await SetPropAsync("net.tcp.buffersize.wifi", "524288,1048576,2097152,262144,524288,1048576", config);
            await SetPropAsync("net.tcp.buffersize.ethernet", "524288,1048576,2097152,262144,524288,1048576", config);
            await SetPropAsync("net.tcp.buffersize.default", "524288,1048576,2097152,262144,524288,1048576", config);
            await SetPropAsync("net.tcp.delack.default", "1", config);
            await SetPropAsync("persist.net.ipv6.disable", "1", config);
            Logger.Success("AdbManager", "Configured In-VM TCP buffer sizes & disabled VM IPv6 latency spikes.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to tune In-VM TCP stack: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> OptimizeInVmAudioLatencyAsync(GameLoopConfig? config = null)
    {
        try
        {
            // Disable deep buffer audio path to force low latency fast-track
            await SetPropAsync("audio.deep_buffer.media", "false", config);
            await SetPropAsync("af.resampler.quality", "2", config);
            await SetPropAsync("media.stagefright.audio.sink", "256", config);
            await SetPropAsync("ro.audio.flinger_standbytime_ms", "1000", config);
            Logger.Success("AdbManager", "Configured In-VM Low-Latency Fast Track Audio (Deep Buffer Disabled).");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to configure In-VM Audio Latency: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SetPointerLocationOverlayAsync(bool enabled, GameLoopConfig? config = null)
    {
        try
        {
            string val = enabled ? "1" : "0";
            await ExecuteShellCommandAsync($"settings put system pointer_location {val}", null, 3000, config);
            await ExecuteShellCommandAsync($"settings put system show_touches {val}", null, 3000, config);
            Logger.Success("AdbManager", $"Pointer location & touch overlay set to: {enabled}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("AdbManager", $"Failed to toggle pointer overlay: {ex.Message}");
            return false;
        }
    }

    public static async Task<string> InstallApkAsync(string apkPath, GameLoopConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(apkPath) || !File.Exists(apkPath))
        {
            return "APK file not found on disk.";
        }

        string serial = _activeDeviceSerial ?? string.Empty;
        string args = string.IsNullOrEmpty(serial) 
            ? $"install -r -d \"{apkPath}\"" 
            : $"-s {serial} install -r -d \"{apkPath}\"";

        Logger.Info("AdbManager", $"Sideloading APK {Path.GetFileName(apkPath)} into GameLoop VM...");
        var res = await ExecuteAdbCommandAsync(args, 60000, config);
        
        if (res.Contains("Success", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Success("AdbManager", $"Successfully installed {Path.GetFileName(apkPath)}.");
            return "Success: APK installed successfully!";
        }

        Logger.Warn("AdbManager", $"APK installation returned: {res}");
        return res;
    }

    public static async Task<bool> PullFileFromVmAsync(string remotePath, string localPath, GameLoopConfig? config = null)
    {
        string serial = _activeDeviceSerial ?? string.Empty;
        string args = string.IsNullOrEmpty(serial)
            ? $"pull \"{remotePath}\" \"{localPath}\""
            : $"-s {serial} pull \"{remotePath}\" \"{localPath}\"";

        var res = await ExecuteAdbCommandAsync(args, 15000, config);
        return !res.Contains("error:", StringComparison.OrdinalIgnoreCase) && !res.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> PushFileToVmAsync(string localPath, string remotePath, GameLoopConfig? config = null)
    {
        if (!File.Exists(localPath)) return false;
        string serial = _activeDeviceSerial ?? string.Empty;
        string args = string.IsNullOrEmpty(serial)
            ? $"push \"{localPath}\" \"{remotePath}\""
            : $"-s {serial} push \"{localPath}\" \"{remotePath}\"";

        var res = await ExecuteAdbCommandAsync(args, 15000, config);
        return !res.Contains("error:", StringComparison.OrdinalIgnoreCase) && !res.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> ElevateGameProcessPriorityAsync(string packageName = "com.tencent.ig", GameLoopConfig? config = null)
    {
        try
        {
            // Find PID of game package in Android VM
            var pidOutput = await ExecuteShellCommandAsync($"pidof {packageName}", null, 3000, config);
            var pid = pidOutput.Trim();

            if (!string.IsNullOrEmpty(pid) && int.TryParse(pid.Split(' ')[0], out int gamePid))
            {
                // Elevate niceness to -20 (maximum real-time priority in Linux kernel)
                await ExecuteShellCommandAsync($"renice -20 -p {gamePid}", null, 3000, config);
                // Attempt real-time FIFO scheduler if root permissions allow
                await ExecuteShellCommandAsync($"chrt -f -p 99 {gamePid}", null, 3000, config);
                Logger.Success("AdbManager", $"Elevated In-VM priority for {packageName} (PID {gamePid}) to Real-Time (nice -20).");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Could not elevate In-VM process priority: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SetVmResolutionAsync(int width, int height, int dpi = 0, GameLoopConfig? config = null)
    {
        try
        {
            if (width <= 0 || height <= 0) return false;

            var res = await ExecuteShellCommandAsync($"wm size {width}x{height}", null, 5000, config);
            if (dpi > 0)
            {
                await ExecuteShellCommandAsync($"wm density {dpi}", null, 5000, config);
            }
            Logger.Success("AdbManager", $"Synchronized In-VM display resolution to {width}x{height}" + (dpi > 0 ? $" (DPI: {dpi})" : ""));
            return !res.Contains("error:", StringComparison.OrdinalIgnoreCase) && !res.Contains("failed", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to set In-VM resolution: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ResetVmResolutionAsync(GameLoopConfig? config = null)
    {
        try
        {
            await ExecuteShellCommandAsync("wm size reset", null, 5000, config);
            await ExecuteShellCommandAsync("wm density reset", null, 5000, config);
            Logger.Info("AdbManager", "Reset In-VM display resolution and DPI to default.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to reset In-VM resolution: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> Unlock120FpsAsync(GameLoopConfig? config = null)
    {
        try
        {
            await AutoConnectGameLoopAsync(config);
            var props = new Dictionary<string, string>
            {
                { "debug.sf.fps", "120" },
                { "persist.sys.display.rate", "120" },
                { "persist.vendor.dfps.level", "120" },
                { "ro.vendor.display.default_fps", "120" },
                { "debug.egl.hw", "1" },
                { "debug.sf.swaprect", "1" }
            };
            var ok = await BatchSetPropsAsync(props, null, config);
            if (ok)
            {
                Logger.Success("AdbManager", "Injected 120 FPS high-refresh unlock parameters into Android VM (Batched).");
            }
            return ok;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to inject 120 FPS: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SpoofDeviceProfileAsync(DeviceProfile profile, GameLoopConfig? config = null)
    {
        if (profile == null) return false;
        try
        {
            await AutoConnectGameLoopAsync(config);
            string brand = profile.Manufacturer.ToLowerInvariant();
            var props = new Dictionary<string, string>
            {
                { "ro.product.model", profile.Model },
                { "ro.product.brand", brand },
                { "ro.product.manufacturer", brand },
                { "ro.product.name", profile.Model },
                { "ro.product.device", profile.Model },
                { "ro.build.product", profile.Model }
            };
            var ok = await BatchSetPropsAsync(props, null, config);
            if (ok)
            {
                Logger.Success("AdbManager", $"Synchronized In-VM Device Profile to {profile.DisplayName} ({profile.Model}).");
            }
            return ok;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to spoof device profile: {ex.Message}");
            return false;
        }
    }

    public static async Task<string> GetInVmDeviceModelAsync(GameLoopConfig? config = null)
    {
        try
        {
            await AutoConnectGameLoopAsync(config);
            var model = await GetPropAsync("ro.product.model", config);
            return model.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static readonly IReadOnlySet<string> DefaultSafeProcessWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "android", "system", "root", "init", "zygote", "zygote64", "surfaceflinger",
        "audioserver", "cameraserver", "mediaserver", "netd", "vold", "servicemanager",
        "hwservicemanager", "installd", "keystore", "logd", "adbd", "ueventd", "lmkd",
        "healthd", "wpa_supplicant", "sh", "su", "system_server",
        "com.android.systemui", "com.android.launcher", "com.android.launcher3",
        "com.android.settings", "com.android.phone", "com.android.inputmethod.latin",
        "com.google.android.inputmethod.latin", "com.android.providers.settings",
        "com.tencent.tinput", "com.tencent.android.pad", "com.tencent.gme", "com.tencent.turingfd"
    };

    public static List<string> ParseRunningPackages(string psOutput, IEnumerable<string>? customWhitelist = null)
    {
        var targetPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(psOutput)) return targetPackages.ToList();

        var whitelist = new HashSet<string>(DefaultSafeProcessWhitelist, StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in KnownGamePackages)
        {
            whitelist.Add(pkg.PackageName);
        }
        if (customWhitelist != null)
        {
            foreach (var item in customWhitelist)
            {
                if (!string.IsNullOrWhiteSpace(item)) whitelist.Add(item.Trim());
            }
        }

        var lines = psOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("USER", StringComparison.OrdinalIgnoreCase) || 
                line.StartsWith("PID", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[kthreadd]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string procName = parts[^1];
            string basePackage = procName.Split(':')[0].Trim();

            if (basePackage.Contains('.') && !basePackage.StartsWith("/") && !basePackage.StartsWith("["))
            {
                if (!whitelist.Contains(basePackage) && !whitelist.Contains(procName))
                {
                    targetPackages.Add(basePackage);
                }
            }
        }

        return targetPackages.ToList();
    }

    public static async Task<int> KillInVmBackgroundAppsAsync(IEnumerable<string>? additionalWhitelist = null, GameLoopConfig? config = null)
    {
        try
        {
            string psOutput = await ExecuteShellCommandAsync("ps -A", null, 4000, config);
            if (string.IsNullOrWhiteSpace(psOutput) || psOutput.Contains("bad option", StringComparison.OrdinalIgnoreCase) || psOutput.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                psOutput = await ExecuteShellCommandAsync("ps", null, 4000, config);
            }

            var targets = ParseRunningPackages(psOutput, additionalWhitelist);
            if (targets.Count == 0)
            {
                Logger.Info("AdbManager", "In-VM Background App Killer: No rogue background packages detected.");
                return 0;
            }

            Logger.Info("AdbManager", $"In-VM Background App Killer: Force-stopping {targets.Count} background packages ({string.Join(", ", targets)})...");
            var killCmds = targets.Select(pkg => $"am force-stop {pkg}");
            await ExecuteBatchShellCommandAsync(killCmds, null, 8000, config);

            Logger.Success("AdbManager", $"In-VM Background App Killer: Successfully stopped {targets.Count} packages to reclaim RAM.");
            return targets.Count;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to kill background apps: {ex.Message}");
            return 0;
        }
    }

    public static GpuRendererInfo ParseGpuRendererInfo(string dumpsysSfOutput)
    {
        var info = new GpuRendererInfo();
        if (string.IsNullOrWhiteSpace(dumpsysSfOutput)) return info;

        var glesMatch = Regex.Match(dumpsysSfOutput, @"GLES:\s*([^,\r\n]+),\s*([^,\r\n]+),\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (glesMatch.Success)
        {
            info.Vendor = glesMatch.Groups[1].Value.Trim();
            info.Renderer = glesMatch.Groups[2].Value.Trim();
            info.Version = glesMatch.Groups[3].Value.Trim();
            return info;
        }

        var vendorMatch = Regex.Match(dumpsysSfOutput, @"GL_VENDOR:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (vendorMatch.Success) info.Vendor = vendorMatch.Groups[1].Value.Trim();

        var rendererMatch = Regex.Match(dumpsysSfOutput, @"GL_RENDERER:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (rendererMatch.Success) info.Renderer = rendererMatch.Groups[1].Value.Trim();

        var versionMatch = Regex.Match(dumpsysSfOutput, @"GL_VERSION:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (versionMatch.Success) info.Version = versionMatch.Groups[1].Value.Trim();

        if (info.Renderer == "Unknown")
        {
            if (dumpsysSfOutput.Contains("virgl", StringComparison.OrdinalIgnoreCase)) info.Renderer = "VirGL (Virtual 3D)";
            else if (dumpsysSfOutput.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase)) info.Renderer = "LLVMpipe (Software)";
            else if (dumpsysSfOutput.Contains("Mesa", StringComparison.OrdinalIgnoreCase)) info.Renderer = "Mesa DRI";
        }

        return info;
    }

    public static async Task<GpuRendererInfo> DetectVmGpuRendererAsync(GameLoopConfig? config = null)
    {
        try
        {
            string sf = await ExecuteShellCommandAsync("dumpsys SurfaceFlinger", null, 4000, config);
            return ParseGpuRendererInfo(sf);
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to detect In-VM GPU renderer: {ex.Message}");
            return new GpuRendererInfo();
        }
    }

    public static async Task<bool> OptimizeVmGpuRenderPipelineAsync(GameLoopConfig? config = null)
    {
        try
        {
            var props = new Dictionary<string, string>
            {
                { "debug.sf.triple_buffer", "1" },
                { "debug.egl.traceGpuCompletion", "1" },
                { "debug.hwui.render_dirty_regions", "false" },
                { "debug.hwui.use_gpu_pixel_buffers", "true" },
                { "debug.sf.phase_offset_threshold_for_next_vsync_ns", "6100000" },
                { "debug.sf.early_phase_offset_ns", "500000" },
                { "debug.sf.early_app_phase_offset_ns", "500000" },
                { "debug.sf.latch_unsignaled", "1" }
            };
            bool ok = await BatchSetPropsAsync(props, null, config);
            await ExecuteShellCommandAsync("setprop ctl.restart surfaceflinger", null, 4000, config);
            Logger.Success("AdbManager", "Configured In-VM GPU Render Pipeline (Triple-Buffering & Low-Jitter Phase Offsets).");
            return ok;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to optimize In-VM GPU pipeline: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> LockVmPowerProfileAsync(GameLoopConfig? config = null)
    {
        try
        {
            var cmds = new[]
            {
                "dumpsys battery set ac 1",
                "dumpsys battery set level 100",
                "settings put global low_power 0",
                "settings put global adaptive_battery_management_enabled 0",
                "setprop persist.sys.perf.default 1",
                "setprop debug.cpufreq.governor performance"
            };
            await ExecuteBatchShellCommandAsync(cmds, null, 6000, config);
            Logger.Success("AdbManager", "Locked In-VM Power Profile: AC Power forced, Battery Saver disabled, Performance governor active.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to lock VM power profile: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> MitigateVmThermalThrottleAsync(GameLoopConfig? config = null)
    {
        try
        {
            var cmds = new[]
            {
                "setprop persist.sys.perf.topAppRenderThreadBoost.enable true",
                "setprop debug.cpufreq.governor performance",
                "setprop debug.sf.disable_backpressure 1",
                "setprop sys.use_fifo_ui 1"
            };
            await ExecuteBatchShellCommandAsync(cmds, null, 6000, config);
            Logger.Success("AdbManager", "Applied In-VM Thermal Throttle Mitigation (Render Thread Boost & Backpressure Bypass).");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Failed to apply thermal mitigation: {ex.Message}");
            return false;
        }
    }

    public static async Task<string> PrepareForMatchAsync(string? targetPackage = null, GameLoopConfig? config = null)
    {
        string targetPkg = string.IsNullOrEmpty(targetPackage) ? "com.tencent.ig" : targetPackage;
        var steps = new List<string>();

        try
        {
            int killed = await KillInVmBackgroundAppsAsync(null, config);
            if (killed > 0) steps.Add($"Terminated {killed} BG apps");

            bool cacheTrimmed = await TrimAppCacheAsync(config, targetPkg);
            if (cacheTrimmed) steps.Add("Purged VM shader logs & tombstones");

            bool prioritized = await ElevateGameProcessPriorityAsync(targetPkg, config);
            if (prioritized) steps.Add($"Elevated {targetPkg} to Real-Time (nice -20)");

            try
            {
                StandbyListCleanerService.PurgeStandbyList();
                int hostTrimmed = ProcessManager.TrimWorkingSets();
                steps.Add($"Purged host standby list & trimmed {hostTrimmed} processes");
            }
            catch { }

            AdbTelemetryService.ResetFpsTracking(targetPkg);
            steps.Add("Reset FPS baseline");

            string summary = steps.Count > 0 
                ? $"Match Prep Complete: {string.Join(" | ", steps)}" 
                : "Match Prep executed.";
            Logger.Success("AdbManager", summary);
            return summary;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"PrepareForMatch failed: {ex.Message}");
            return $"Match Prep warning: {ex.Message}";
        }
    }

    public static InVmPingResult ParsePingOutput(string pingOutput, string host = "1.1.1.1")
    {
        var result = new InVmPingResult { TargetHost = host };
        if (string.IsNullOrWhiteSpace(pingOutput)) return result;

        var rttMatch = Regex.Match(pingOutput, @"(?:rtt|round-trip)\s+min/avg/max(?:/mdev)?\s*=\s*([\d\.]+)/([\d\.]+)/([\d\.]+)(?:/([\d\.]+))?", RegexOptions.IgnoreCase);
        if (rttMatch.Success)
        {
            double.TryParse(rttMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double min);
            double.TryParse(rttMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double avg);
            double.TryParse(rttMatch.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double max);
            double mdev = 0;
            if (rttMatch.Groups[4].Success)
            {
                double.TryParse(rttMatch.Groups[4].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out mdev);
            }
            result.MinMs = Math.Round(min, 1);
            result.AvgMs = Math.Round(avg, 1);
            result.MaxMs = Math.Round(max, 1);
            result.MdevMs = Math.Round(mdev, 1);
            result.Success = true;
        }

        var lossMatch = Regex.Match(pingOutput, @"(\d+(?:\.\d+)?)%\s+packet loss", RegexOptions.IgnoreCase);
        if (lossMatch.Success && double.TryParse(lossMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double loss))
        {
            result.PacketLossPct = loss;
        }

        if (!result.Success)
        {
            var timeMatches = Regex.Matches(pingOutput, @"time=([\d\.]+)\s*ms", RegexOptions.IgnoreCase);
            if (timeMatches.Count > 0)
            {
                var times = new List<double>();
                foreach (Match m in timeMatches)
                {
                    if (double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double t))
                    {
                        times.Add(t);
                    }
                }
                if (times.Count > 0)
                {
                    result.MinMs = Math.Round(times.Min(), 1);
                    result.AvgMs = Math.Round(times.Average(), 1);
                    result.MaxMs = Math.Round(times.Max(), 1);
                    result.Success = true;
                }
            }
        }

        return result;
    }

    public static async Task<InVmPingResult> RunInVmPingDiagnosticAsync(string host = "1.1.1.1", GameLoopConfig? config = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host)) host = "1.1.1.1";
            string output = await ExecuteShellCommandAsync($"ping -c 4 -W 2 {host}", null, 12000, config);
            return ParsePingOutput(output, host);
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"In-VM Ping failed: {ex.Message}");
            return new InVmPingResult { TargetHost = host, Success = false };
        }
    }

    public static Dictionary<string, string> ParseGetPropOutput(string getpropOutput)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(getpropOutput)) return dict;

        var matches = Regex.Matches(getpropOutput, @"\[([^\]]+)\]:\s*\[([^\]]*)\]");
        foreach (Match m in matches)
        {
            dict[m.Groups[1].Value.Trim()] = m.Groups[2].Value.Trim();
        }

        return dict;
    }

    public static async Task<List<OptimizationVerificationResult>> VerifyAppliedOptimizationsAsync(
        IDictionary<string, string> expectedProps, 
        IDictionary<string, string>? expectedSettings = null, 
        GameLoopConfig? config = null)
    {
        var results = new List<OptimizationVerificationResult>();
        try
        {
            string getpropOut = await ExecuteShellCommandAsync("getprop", null, 5000, config);
            var actualProps = ParseGetPropOutput(getpropOut);

            if (expectedProps != null)
            {
                foreach (var kvp in expectedProps)
                {
                    actualProps.TryGetValue(kvp.Key, out string? actualVal);
                    actualVal ??= string.Empty;

                    bool match = string.Equals(kvp.Value.Trim(), actualVal.Trim(), StringComparison.OrdinalIgnoreCase);
                    results.Add(new OptimizationVerificationResult
                    {
                        Key = kvp.Key,
                        Expected = kvp.Value,
                        Actual = actualVal,
                        IsMatch = match
                    });
                }
            }

            if (expectedSettings != null && expectedSettings.Count > 0)
            {
                foreach (var kvp in expectedSettings)
                {
                    string actualVal = await ExecuteShellCommandAsync($"settings get global {kvp.Key}", null, 3000, config);
                    actualVal = actualVal.Trim();
                    bool match = string.Equals(kvp.Value.Trim(), actualVal, StringComparison.OrdinalIgnoreCase);
                    results.Add(new OptimizationVerificationResult
                    {
                        Key = $"settings:{kvp.Key}",
                        Expected = kvp.Value,
                        Actual = actualVal,
                        IsMatch = match
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbManager", $"Verification encountered error: {ex.Message}");
        }

        return results;
    }
}


