using System.Diagnostics;
using System.IO;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public static class GameLoopDetector
{
    private static readonly string[] PossibleRegistryPaths = new[]
    {
        @"Software\Tencent\MobileGamePC",
        @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
        @"Software\Tencent\TxGameAssistant",
        @"SOFTWARE\WOW6432Node\Tencent\TxGameAssistant"
    };

    private static readonly string[] EmulatorProcessNames = new[]
    {
        "AppMarket",
        "AndroidEmulator",
        "AndroidEmulatorEn",
        "AndroidEmulatorEx",
        "aow_exe",
        "TBSWebStore"
    };

    private static readonly object _cacheLock = new();
    private static GameLoopConfig? _cachedConfig;
    private static DateTime _lastDetectionTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);
    private static string _lastLoggedSummary = string.Empty;

    public static void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedConfig = null;
            _lastDetectionTime = DateTime.MinValue;
        }
    }

    public static async Task<GameLoopConfig> DetectGameLoopAsync(bool forceRefresh = false)
    {
        return await Task.Run(() => DetectGameLoop(forceRefresh));
    }

    public static GameLoopConfig DetectGameLoop(bool forceRefresh = false)
    {
        lock (_cacheLock)
        {
            var now = DateTime.UtcNow;
            GameLoopConfig config;

            if (!forceRefresh && _cachedConfig != null && (now - _lastDetectionTime) < CacheDuration)
            {
                config = _cachedConfig;
                DetectRunningProcesses(config);
            }
            else
            {
                config = new GameLoopConfig();

                // 1. Detect from Registry
                DetectFromRegistry(config);

                // 2. Check running processes
                DetectRunningProcesses(config);

                _cachedConfig = config;
                _lastDetectionTime = now;
            }

            string summary = $"GameLoop Installed: {config.IsInstalled}, Running: {config.IsRunning}, Renderer: {(config.ForceDirectX ? "DirectX+" : "OpenGL+")}, CPU: {config.VmCpuCount} cores, RAM: {config.VmMemorySizeInMb} MB, Res: {config.VmResWidth}x{config.VmResHeight}, ShaderCache: {config.LocalShaderCacheEnabled}, FPS Level: {config.PubgFpsLevel}";

            if (summary != _lastLoggedSummary)
            {
                _lastLoggedSummary = summary;
                Logger.Info("GameLoopDetector", summary);
            }

            return config;
        }
    }

    private static void DetectFromRegistry(GameLoopConfig config)
    {
        // Try HKCU first, then HKLM
        RegistryKey? targetKey = null;
        string foundPath = string.Empty;

        try
        {
            foreach (var path in PossibleRegistryPaths)
            {
                var hkcuKey = Registry.CurrentUser.OpenSubKey(path);
                if (hkcuKey != null)
                {
                    targetKey = hkcuKey;
                    foundPath = path;
                    config.RegistryKeyPath = path;
                    break;
                }

                var hklmKey = Registry.LocalMachine.OpenSubKey(path);
                if (hklmKey != null)
                {
                    targetKey = hklmKey;
                    foundPath = path;
                    config.RegistryKeyPath = path;
                    break;
                }
            }

            if (targetKey != null)
            {
                config.IsInstalled = true;

                // Read engine values
                config.VmCpuCount = ConvertToInt(targetKey.GetValue("VMCpuCount"), 4);
                config.VmMemorySizeInMb = ConvertToInt(targetKey.GetValue("VMMemorySizeInMB"), 4096);
                config.VmResWidth = ConvertToInt(targetKey.GetValue("VMResWidth"), 1920);
                config.VmResHeight = ConvertToInt(targetKey.GetValue("VMResHeight"), 1080);
                config.VmDpi = ConvertToInt(targetKey.GetValue("VMDPI"), 320);

                config.VSyncEnabled = ConvertToInt(targetKey.GetValue("VSyncEnabled"), 0) == 1;
                config.ForceDirectX = ConvertToInt(targetKey.GetValue("ForceDirectX"), 1) == 1;
                config.EnableGlesv3 = ConvertToInt(targetKey.GetValue("EnableGLESv3"), 1) == 1;
                config.LocalShaderCacheEnabled = ConvertToInt(targetKey.GetValue("LocalShaderCacheEnabled"), 1) == 1;
                config.ShaderCacheEnabled = ConvertToInt(targetKey.GetValue("ShaderCacheEnabled"), 1) == 1;
                config.RenderOptimizeEnabled = ConvertToInt(targetKey.GetValue("RenderOptimizeEnabled"), 1) == 1;
                config.FxaaQuality = ConvertToInt(targetKey.GetValue("FxaaQuality"), 0);

                // PUBG Mobile specific settings
                config.PubgFpsLevel = ConvertToInt(targetKey.GetValue("com.tencent.ig_FPSLevel"), 90);
                config.PubgRenderQuality = ConvertToInt(targetKey.GetValue("com.tencent.ig_RenderQuality"), 2);
                config.PubgContentScale = ConvertToInt(targetKey.GetValue("com.tencent.ig_ContentScale"), 1);

                var device = targetKey.GetValue("VMPhoneDevice") as string;
                if (!string.IsNullOrEmpty(device)) config.DeviceModel = device;

                var brand = targetKey.GetValue("brand") as string;
                if (!string.IsNullOrEmpty(brand)) config.Brand = brand;

                var ver = targetKey.GetValue("TSyzsVersion") as string ?? targetKey.GetValue("version")?.ToString();
                if (!string.IsNullOrEmpty(ver)) config.Version = ver;

                var regInstallPath = targetKey.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(regInstallPath) && Directory.Exists(regInstallPath))
                {
                    config.InstallPath = regInstallPath;
                }

                targetKey.Dispose();
            }

            // Resolve actual GameLoop executable install path if not set or invalid
            if (string.IsNullOrEmpty(config.InstallPath) || !Directory.Exists(config.InstallPath))
            {
                var resolvedExe = FindGameLoopExePath();
                if (!string.IsNullOrEmpty(resolvedExe))
                {
                    config.IsInstalled = true;
                    config.InstallPath = Path.GetDirectoryName(resolvedExe) ?? resolvedExe;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("GameLoopDetector", $"Registry read failed: {ex.Message}");
        }
    }

    public static void DetectRunningProcesses(GameLoopConfig config)
    {
        config.RunningProcessIds.Clear();
        config.IsRunning = false;

        foreach (var name in EmulatorProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var p in procs)
                {
                    config.RunningProcessIds.Add(p.Id);
                    config.IsRunning = true;
                }
            }
            catch
            {
                // Ignore process access errors
            }
        }
    }

    public static string FindGameLoopExePath()
    {
        // 1. Check running process main module
        foreach (var name in new[] { "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe" })
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var p in procs)
                {
                    try
                    {
                        var fn = p.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(fn) && File.Exists(fn))
                        {
                            return fn;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 2. Check standard drive locations
        var candidates = new List<string>
        {
            @"D:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
            @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
            @"C:\Program Files (x86)\TxGameAssistant\AppMarket\AppMarket.exe",
            @"D:\TxGameAssistant\AppMarket\AppMarket.exe",
            @"E:\TxGameAssistant\AppMarket\AppMarket.exe",
            @"D:\Program Files\TxGameAssistant\ui\AndroidEmulatorEn.exe",
            @"C:\Program Files\TxGameAssistant\ui\AndroidEmulatorEn.exe",
            @"D:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe",
            @"C:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe",
            @"D:\GameLoop\AppMarket\AppMarket.exe",
            @"C:\GameLoop\AppMarket\AppMarket.exe"
        };

        // Also check all drive letters
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Program Files", "TxGameAssistant", "AppMarket", "AppMarket.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "TxGameAssistant", "AppMarket", "AppMarket.exe"));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static int ConvertToInt(object? val, int fallback)
    {
        if (val == null) return fallback;
        if (val is int i) return i;
        if (val is long l) return (int)l;
        if (int.TryParse(val.ToString(), out int parsed)) return parsed;
        return fallback;
    }
}
