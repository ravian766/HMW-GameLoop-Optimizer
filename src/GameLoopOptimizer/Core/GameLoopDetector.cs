using System.Diagnostics;
using System.IO;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public static class GameLoopDetector
{
    private static readonly string[] PossibleRegistryPaths = new[]
    {
        @"Software\Tencent\GameLoop",
        @"SOFTWARE\Tencent\GameLoop",
        @"SOFTWARE\WOW6432Node\Tencent\GameLoop",
        @"Software\Tencent\MobileGamePC",
        @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
        @"Software\Tencent\TxGameAssistant",
        @"SOFTWARE\WOW6432Node\Tencent\TxGameAssistant",
        @"Software\Tencent\Androws",
        @"SOFTWARE\WOW6432Node\Tencent\Androws"
    };

    private static readonly string[] EmulatorProcessNames =
        GameLoopProcessNames.AllProcesses.Concat(new[] { "TBSWebStore" }).Distinct().ToArray();

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

                // 3. Compatibility & Architecture Assessment
                GameLoopCompatibilityManager.EvaluateCompatibility(config);

                // 4. PUBG Mobile Profile Detection
                PUBGDetectionService.DetectPubgProfile(config);

                config.IsAdbAvailable = AdbManager.IsAdbAvailable(config);

                _cachedConfig = config;
                _lastDetectionTime = now;
            }

            string summary = $"GameLoop Installed: {config.IsInstalled}, Running: {config.IsRunning}, Compat: {config.CompatibilityTier}, Renderer: {(config.ForceDirectX ? "DirectX+" : "OpenGL+")}, CPU: {config.VmCpuCount} cores, RAM: {config.VmMemorySizeInMb} MB, Res: {config.VmResWidth}x{config.VmResHeight}, ShaderCache: {config.LocalShaderCacheEnabled}, FPS Level: {config.PubgFpsLevel}";


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
        try
        {
            // Inspect both HKCU and HKLM across all possible registry locations to accumulate configuration
            foreach (var path in PossibleRegistryPaths)
            {
                using (var hkcuKey = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (hkcuKey != null)
                    {
                        ReadFromRegistryKey(hkcuKey, path, config);
                    }
                }

                using (var hklmKey = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (hklmKey != null)
                    {
                        ReadFromRegistryKey(hklmKey, path, config);
                    }
                }
            }

            // If InstallPath is detected but Version is still unset, check MAIN_VERSION file (GameLoop 7.0.19.05 layout)
            if (!string.IsNullOrEmpty(config.InstallPath) && string.IsNullOrEmpty(config.Version) && Directory.Exists(config.InstallPath))
            {
                var mainVerPath = Path.Combine(config.InstallPath, "MAIN_VERSION");
                if (!File.Exists(mainVerPath))
                {
                    var appDir = Path.Combine(config.InstallPath, "Application");
                    if (Directory.Exists(appDir))
                    {
                        var match = Directory.GetFiles(appDir, "MAIN_VERSION", SearchOption.AllDirectories).FirstOrDefault();
                        if (match != null) mainVerPath = match;
                    }
                }

                if (File.Exists(mainVerPath))
                {
                    var text = File.ReadAllText(mainVerPath).Trim();
                    if (!string.IsNullOrEmpty(text)) config.Version = text;
                }
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

            // 7.0.19.05+ UserDir & GameLoop.ini detection and synchronization
            GameLoopIniService.DetectAndApply(config);
        }
        catch (Exception ex)
        {
            Logger.Warn("GameLoopDetector", $"Registry read failed: {ex.Message}");
        }
    }

    private static void ReadFromRegistryKey(RegistryKey key, string registryPath, GameLoopConfig config)
    {
        config.IsInstalled = true;
        if (string.IsNullOrEmpty(config.RegistryKeyPath) || config.RegistryKeyPath == @"Software\Tencent\MobileGamePC")
        {
            config.RegistryKeyPath = registryPath;
        }

        // Install path & user data
        var regInstall = key.GetValue("InstallPath") as string;
        if (!string.IsNullOrEmpty(regInstall) && Directory.Exists(regInstall))
        {
            config.InstallPath = regInstall;
        }

        var gameLoopData = key.GetValue("GameLoopData") as string;
        if (!string.IsNullOrEmpty(gameLoopData) && Directory.Exists(gameLoopData))
        {
            config.UserDir = gameLoopData;
        }

        var userDir = key.GetValue("UserDir") as string ?? key.GetValue("UserDataDir") as string;
        if (!string.IsNullOrEmpty(userDir) && Directory.Exists(userDir))
        {
            config.UserDir = userDir;
        }

        var ver = key.GetValue("Version")?.ToString() ?? key.GetValue("TSyzsVersion") as string;
        if (!string.IsNullOrEmpty(ver) && (string.IsNullOrEmpty(config.Version) || ver.StartsWith("7.0.19", StringComparison.OrdinalIgnoreCase)))
        {
            config.Version = ver;
        }

        var brand = key.GetValue("brand") as string;
        if (!string.IsNullOrEmpty(brand)) config.Brand = brand;

        var device = key.GetValue("VMPhoneDevice") as string;
        if (!string.IsNullOrEmpty(device)) config.DeviceModel = device;

        // Engine settings
        var cpu = key.GetValue("VMCpuCount");
        if (cpu != null) config.VmCpuCount = ConvertToInt(cpu, config.VmCpuCount);

        var mem = key.GetValue("VMMemorySizeInMB");
        if (mem != null) config.VmMemorySizeInMb = ConvertToInt(mem, config.VmMemorySizeInMb);

        var rw = key.GetValue("VMResWidth");
        if (rw != null) config.VmResWidth = ConvertToInt(rw, config.VmResWidth);

        var rh = key.GetValue("VMResHeight");
        if (rh != null) config.VmResHeight = ConvertToInt(rh, config.VmResHeight);

        var dpi = key.GetValue("VMDPI");
        if (dpi != null) config.VmDpi = ConvertToInt(dpi, config.VmDpi);

        var vsync = key.GetValue("VSyncEnabled");
        if (vsync != null) config.VSyncEnabled = ConvertToInt(vsync, 0) == 1;

        var fdx = key.GetValue("ForceDirectX");
        if (fdx != null) config.ForceDirectX = ConvertToInt(fdx, 1) == 1;

        var gles3 = key.GetValue("EnableGLESv3");
        if (gles3 != null) config.EnableGlesv3 = ConvertToInt(gles3, 1) == 1;

        var lsc = key.GetValue("LocalShaderCacheEnabled");
        if (lsc != null) config.LocalShaderCacheEnabled = ConvertToInt(lsc, 1) == 1;

        var sc = key.GetValue("ShaderCacheEnabled");
        if (sc != null) config.ShaderCacheEnabled = ConvertToInt(sc, 1) == 1;

        var ro = key.GetValue("RenderOptimizeEnabled");
        if (ro != null) config.RenderOptimizeEnabled = ConvertToInt(ro, 1) == 1;

        var fxaa = key.GetValue("FxaaQuality");
        if (fxaa != null) config.FxaaQuality = ConvertToInt(fxaa, config.FxaaQuality);

        // GameLoop 7.0.19.05+ keys
        var fv = key.GetValue("ForceVulkan");
        if (fv != null) config.ForceVulkan = ConvertToInt(fv, 0) == 1;

        var sm = key.GetValue("SmartModeEnabled");
        if (sm != null) config.SmartModeEnabled = ConvertToInt(sm, 0) == 1;

        var rm = key.GetValue("RenderingMode");
        if (rm != null) config.RenderingMode = ConvertToInt(rm, config.RenderingMode);

        var aa = key.GetValue("AntiAliasingMode");
        if (aa != null) config.AntiAliasingMode = ConvertToInt(aa, config.AntiAliasingMode);

        var vkVer = key.GetValue("VulkanApiVersion") as string;
        if (!string.IsNullOrEmpty(vkVer)) config.VulkanApiVersion = vkVer;

        // PUBG Mobile specific
        var fps = key.GetValue("com.tencent.ig_FPSLevel");
        if (fps != null) config.PubgFpsLevel = ConvertToInt(fps, config.PubgFpsLevel);

        var rq = key.GetValue("com.tencent.ig_RenderQuality");
        if (rq != null) config.PubgRenderQuality = ConvertToInt(rq, config.PubgRenderQuality);

        var cs = key.GetValue("com.tencent.ig_ContentScale");
        if (cs != null) config.PubgContentScale = ConvertToInt(cs, config.PubgContentScale);
    }

    public static void DetectRunningProcesses(GameLoopConfig config)
    {
        config.RunningProcessIds.Clear();
        config.IsRunning = false;
        config.EmulatorProcessName = string.Empty;

        foreach (var name in EmulatorProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var p in procs)
                {
                    config.RunningProcessIds.Add(p.Id);
                    config.IsRunning = true;
                    if (string.IsNullOrEmpty(config.EmulatorProcessName))
                    {
                        config.EmulatorProcessName = name;
                    }
                }
            }
            catch
            {
                // Ignore process access errors
            }
        }

        if (config.IsRunning)
        {
            try
            {
                var winInfo = WindowDetector.DetectGameLoopWindow();
                if (winInfo.IsFound)
                {
                    config.EmulatorWindowHandle = winInfo.RenderWindowHandle != IntPtr.Zero ? winInfo.RenderWindowHandle : winInfo.MainWindowHandle;
                }
            }
            catch { }
        }
    }


    public static string FindGameLoopExePath()
    {
        // 1. Check running process main module
        foreach (var name in new[] { "GameLoopEmulator", "GameLoop", "GameLoopLauncher", "GameLoopVm", "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe", "GameLoopRenderer" })
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

        // 2. Check standard locations including GameLoop 7.0.19.05 Tencent paths
        var candidates = new List<string>
        {
            @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoop.exe",
            @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopEmulator.exe",
            @"C:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoop.exe",
            @"C:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopEmulator.exe",
            @"D:\Program Files\Tencent\GameLoop\Application\GameLoopLauncher.exe",
            @"C:\Program Files\Tencent\GameLoop\Application\GameLoopLauncher.exe",
            @"D:\Program Files\Tencent\GameLoopData\Component\GameLoop\GameLoopEmulator.exe",
            @"C:\Program Files\Tencent\GameLoopData\Component\GameLoop\GameLoopEmulator.exe",
            @"D:\Program Files\Tencent\GameLoopData\Component\GameLoop\GameLoop.exe",
            @"C:\Program Files\Tencent\GameLoopData\Component\GameLoop\GameLoop.exe",
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
            @"C:\GameLoop\AppMarket\AppMarket.exe",
            @"D:\GameLoop\TxGameAssistant\AppMarket\AppMarket.exe",
            @"C:\GameLoop\TxGameAssistant\AppMarket\AppMarket.exe",
            @"D:\GameLoop\TxGameAssistant\ui\AndroidEmulatorEn.exe",
            @"C:\GameLoop\TxGameAssistant\ui\AndroidEmulatorEn.exe"
        };

        // Also check all drive letters
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Program Files", "Tencent", "GameLoop", "Application", "GameLoopLauncher.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Program Files", "Tencent", "GameLoopData", "Component", "GameLoop", "GameLoopEmulator.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Program Files", "TxGameAssistant", "AppMarket", "AppMarket.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "TxGameAssistant", "AppMarket", "AppMarket.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "GameLoop", "TxGameAssistant", "AppMarket", "AppMarket.exe"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "GameLoop", "TxGameAssistant", "ui", "AndroidEmulatorEn.exe"));

            var appDir = Path.Combine(drive.RootDirectory.FullName, "Program Files", "Tencent", "GameLoop", "Application");
            if (Directory.Exists(appDir))
            {
                try
                {
                    foreach (var sub in Directory.GetDirectories(appDir))
                    {
                        candidates.Add(Path.Combine(sub, "GameLoop.exe"));
                        candidates.Add(Path.Combine(sub, "GameLoopEmulator.exe"));
                    }
                }
                catch { }
            }
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
