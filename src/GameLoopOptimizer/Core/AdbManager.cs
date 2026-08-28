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
    public bool IsEmulator => Serial.Contains("5555") || Serial.Contains("6555") || Serial.Contains("5554") || Serial.StartsWith("emulator-");
}

public static class AdbManager
{
    private static readonly int[] KnownGameLoopPorts = new[] { 5555, 6555, 5554, 11241 };
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
            ? $"shell \"{shellCommand}\"" 
            : $"-s {serial} shell \"{shellCommand}\"";

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

        // 2. Try probing known GameLoop localhost ports
        foreach (var port in KnownGameLoopPorts)
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

    public static async Task<bool> TrimAppCacheAsync(GameLoopConfig? config = null)
    {
        try
        {
            await ExecuteShellCommandAsync("pm trim-caches 999G", null, 6000, config);
            await ExecuteShellCommandAsync("rm -rf /data/data/com.tencent.ig/cache/*", null, 4000, config);
            await ExecuteShellCommandAsync("rm -rf /sdcard/Android/data/com.tencent.ig/cache/*", null, 4000, config);
            Logger.Success("AdbManager", "Purged GameLoop Android VM application and shader caches.");
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
}
