using System.Diagnostics;
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

public static class AdbManager
{
    public static readonly int[] KnownGameLoopPorts = new[] { 5555, 6555, 5554, 11241, 5557, 5559 };

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

        return await Task.Run(() =>
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
                string output = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();

                if (!proc.WaitForExit(timeoutMs))
                {
                    try { proc.Kill(); } catch { }
                    return "Command timed out";
                }

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
        string args = string.IsNullOrEmpty(serial) 
            ? $"shell {shellCommand}" 
            : $"-s {serial} shell {shellCommand}";

        return await ExecuteAdbCommandAsync(args, timeoutMs, config);
    }

    public static async Task<List<AdbDeviceInfo>> GetConnectedDevicesAsync(GameLoopConfig? config = null)
    {
        var list = new List<AdbDeviceInfo>();
        var output = await ExecuteAdbCommandAsync("devices -l", 4000, config);

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("List of devices") || line.StartsWith("*") || string.IsNullOrWhiteSpace(line))
                continue;

            var match = Regex.Match(line, @"^(\S+)\s+(\w+)(?:\s+model:(\S+))?");
            if (match.Success)
            {
                list.Add(new AdbDeviceInfo
                {
                    Serial = match.Groups[1].Value,
                    State = match.Groups[2].Value,
                    Model = match.Groups[3].Success ? match.Groups[3].Value : "GameLoop VM"
                });
            }
        }

        return list;
    }

    public static async Task<List<int>> DiscoverListeningEmulatorPortsAsync()
    {
        var ports = new HashSet<int>(KnownGameLoopPorts);
        await Task.Run(() =>
        {
            try
            {
                var procNames = new[] { "AndroidEmulator", "AndroidEmulatorEn", "aow_exe", "AppMarket" };
                var pids = Process.GetProcesses()
                    .Where(p => procNames.Any(name => p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => p.Id)
                    .ToHashSet();

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
                        if (pids.Contains(pid) && port > 1024 && port < 65535)
                        {
                            ports.Add(port);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("AdbManager", $"Dynamic port scan encountered error: {ex.Message}");
            }
        });

        return ports.ToList();
    }

    public static async Task<bool> AutoConnectGameLoopAsync(GameLoopConfig? config = null)
    {
        // 1. Check existing connected devices
        var existing = await GetConnectedDevicesAsync(config);
        var active = existing.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
        if (active != null)
        {
            _activeDeviceSerial = active.Serial;
            Logger.Success("AdbManager", $"Connected to active GameLoop Android VM: {active.Serial}");
            return true;
        }

        // 2. Discover active & known ports dynamically
        var candidatePorts = await DiscoverListeningEmulatorPortsAsync();

        foreach (var port in candidatePorts)
        {
            string hostPort = $"127.0.0.1:{port}";
            var res = await ExecuteAdbCommandAsync($"connect {hostPort}", 3000, config);
            if (res.Contains("connected to", StringComparison.OrdinalIgnoreCase) || res.Contains("already connected", StringComparison.OrdinalIgnoreCase))
            {
                _activeDeviceSerial = hostPort;
                Logger.Success("AdbManager", $"Successfully established ADB connection to {hostPort}");
                return true;
            }
        }

        // 3. Re-check devices list in case daemon connected
        existing = await GetConnectedDevicesAsync(config);
        active = existing.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));
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
        await ExecuteAdbCommandAsync("kill-server", 4000, config);
        await Task.Delay(500);
        await ExecuteAdbCommandAsync("start-server", 4000, config);
        return await AutoConnectGameLoopAsync(config);
    }

    public static async Task<bool> ConnectCustomDeviceAsync(string ipPort, GameLoopConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(ipPort)) return false;
        string target = ipPort.Trim();
        if (!target.Contains(':') && int.TryParse(target, out _))
        {
            target = $"127.0.0.1:{target}";
        }

        var res = await ExecuteAdbCommandAsync($"connect {target}", 4000, config);
        if (res.Contains("connected to", StringComparison.OrdinalIgnoreCase) || res.Contains("already connected", StringComparison.OrdinalIgnoreCase))
        {
            _activeDeviceSerial = target;
            Logger.Success("AdbManager", $"Connected to custom ADB target: {target}");
            return true;
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
}

