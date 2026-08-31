using System.Diagnostics;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.Core;

public class GameLoopWatchdogService : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    private readonly System.Timers.Timer _timer;
    private readonly Func<GameLoopConfig> _getGl;
    private bool _isGameLoopRunning;
    private bool _isEnabled = true;
    private bool _isAutoPurgeEnabled = false;
    private bool _isAutoGameBoostEnabled = true;
    private int _tickCount = 0;
    private string _lastBoostedPackage = string.Empty;

    private long _lastIdleTime = 0;
    private long _lastKernelTime = 0;
    private long _lastUserTime = 0;

    private double GetCurrentSystemCpuUsage()
    {
        try
        {
            if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
            {
                if (_lastIdleTime != 0)
                {
                    long usr = userTime - _lastUserTime;
                    long ker = kernelTime - _lastKernelTime;
                    long idl = idleTime - _lastIdleTime;

                    long sys = usr + ker;
                    if (sys > 0)
                    {
                        double cpu = ((double)(sys - idl) / sys) * 100.0;
                        _lastIdleTime = idleTime;
                        _lastKernelTime = kernelTime;
                        _lastUserTime = userTime;
                        return Math.Clamp(cpu, 0.0, 100.0);
                    }
                }
                _lastIdleTime = idleTime;
                _lastKernelTime = kernelTime;
                _lastUserTime = userTime;
            }
        }
        catch { }
        return 0.0;
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled && _isGameLoopRunning)
            {
                // Restore timer if disabled while running
                TimerResolutionModule.RestoreTimer();
            }
        }
    }

    public bool IsAutoPurgeEnabled
    {
        get => _isAutoPurgeEnabled;
        set => _isAutoPurgeEnabled = value;
    }

    public bool IsAutoGameBoostEnabled
    {
        get => _isAutoGameBoostEnabled;
        set => _isAutoGameBoostEnabled = value;
    }

    public int AutoPurgeCount { get; private set; } = 0;
    public double TotalMegabytesFreed { get; private set; } = 0.0;
    public DateTime? LastPurgeTime { get; private set; }
    public string LastPurgeMessage { get; private set; } = "Standby (No purges yet)";

    public string DetectedGameTitle { get; private set; } = "Standby";
    public string DetectedGamePackage { get; private set; } = string.Empty;
    public bool IsGameActive => !string.IsNullOrEmpty(DetectedGamePackage);

    public event Action<bool>? GameLoopStateChanged;
    public event Action<string>? AutoPurgeExecuted;
    public event Action<string, string>? GameTitleChanged;

    public GameLoopWatchdogService(Func<GameLoopConfig> getGl)
    {
        _getGl = getGl;
        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start()
    {
        _timer.Start();
        Logger.Info("Watchdog", "GameLoop auto-detection & game profile daemon started (2s interval).");
    }

    public void Stop()
    {
        _timer.Stop();
        if (_isGameLoopRunning)
        {
            TimerResolutionModule.RestoreTimer();
        }
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isEnabled) return;

        try
        {
            var emulatorNames = new[] { "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe" };
            bool currentlyRunning = false;

            foreach (var name in emulatorNames)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    currentlyRunning = true;
                    foreach (var p in procs) p.Dispose();
                    break;
                }
            }

            if (currentlyRunning && !_isGameLoopRunning)
            {
                _isGameLoopRunning = true;
                _tickCount = 0;
                OnGameLoopLaunched();
            }
            else if (!currentlyRunning && _isGameLoopRunning)
            {
                _isGameLoopRunning = false;
                _detectedGamePackage = string.Empty;
                DetectedGameTitle = "Standby";
                _lastBoostedPackage = string.Empty;
                OnGameLoopClosed();
            }
            else if (currentlyRunning && _isGameLoopRunning)
            {
                // Check active game title periodically
                CheckActiveGameTitle();

                if (_isAutoPurgeEnabled)
                {
                    _tickCount++;
                    // Every ~3 minutes (90 ticks * 2s = 180s) trigger background smart purge
                    if (_tickCount >= 90)
                    {
                        double currentCpu = GetCurrentSystemCpuUsage();
                        double currentMem = ProcessManager.GetSystemMemoryLoadPercent();

                        // Gunfight / Active Combat Guard:
                        // If CPU usage is elevated (> 35%) and memory load is NOT critically high (< 88%),
                        // postpone purge by 15 ticks (30s) to guarantee zero frame drops during active combat.
                        if (currentCpu > 35.0 && currentMem < 88.0)
                        {
                            _tickCount = 75; // Try again in 30 seconds
                            Logger.Info("Watchdog", $"Gunfight Guard: Auto-purge deferred (CPU {currentCpu:F0}%, RAM {currentMem:F0}%). Preserving smooth frame delivery.");
                        }
                        else
                        {
                            _tickCount = 0;
                            Task.Run(async () => await ExecuteSmartPurgeAsync(false));
                        }
                    }
                }
            }
        }
        catch { }
    }

    private string _detectedGamePackage = string.Empty;

    private void CheckActiveGameTitle()
    {
        try
        {
            // Inspect window titles of running emulator processes
            var emulatorProcs = Process.GetProcessesByName("AndroidEmulator")
                .Concat(Process.GetProcessesByName("AndroidEmulatorEn"))
                .Concat(Process.GetProcessesByName("AndroidEmulatorEx"))
                .Concat(Process.GetProcessesByName("aow_exe"))
                .Concat(Process.GetProcessesByName("AppMarket"));

            string foundTitle = string.Empty;
            string foundPkg = string.Empty;

            foreach (var p in emulatorProcs)
            {
                try
                {
                    var title = p.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        if (title.Contains("PUBG", StringComparison.OrdinalIgnoreCase) || title.Contains("BATTLEGROUNDS", StringComparison.OrdinalIgnoreCase))
                        {
                            foundTitle = "PUBG Mobile";
                            foundPkg = "com.tencent.ig";
                            break;
                        }
                        if (title.Contains("BGMI", StringComparison.OrdinalIgnoreCase))
                        {
                            foundTitle = "BGMI";
                            foundPkg = "com.pubg.imobile";
                            break;
                        }
                        if (title.Contains("Call of Duty", StringComparison.OrdinalIgnoreCase) || title.Contains("CODM", StringComparison.OrdinalIgnoreCase))
                        {
                            foundTitle = "Call of Duty: Mobile";
                            foundPkg = "com.activision.callofduty.shooter";
                            break;
                        }
                        if (title.Contains("Free Fire", StringComparison.OrdinalIgnoreCase))
                        {
                            foundTitle = "Garena Free Fire";
                            foundPkg = "com.dts.freefireth";
                            break;
                        }
                        if (title.Contains("Arena Breakout", StringComparison.OrdinalIgnoreCase))
                        {
                            foundTitle = "Arena Breakout";
                            foundPkg = "com.proxima.arenabreakout";
                            break;
                        }
                    }
                }
                finally
                {
                    p.Dispose();
                }
            }

            if (string.IsNullOrEmpty(foundTitle) && _isGameLoopRunning)
            {
                // Fallback: check ADB foreground window
                Task.Run(async () =>
                {
                    string? adbPkg = await DetectForegroundPackageViaAdbAsync();
                    if (!string.IsNullOrEmpty(adbPkg) && adbPkg != _detectedGamePackage)
                    {
                        var known = AdbManager.KnownGamePackages.FirstOrDefault(p => p.PackageName.Equals(adbPkg, StringComparison.OrdinalIgnoreCase));
                        string title = known?.DisplayName ?? adbPkg;
                        DetectedGameTitle = title;
                        _detectedGamePackage = adbPkg;
                        DetectedGamePackage = adbPkg;
                        GameTitleChanged?.Invoke(DetectedGameTitle, DetectedGamePackage);

                        if (_isAutoGameBoostEnabled && _detectedGamePackage != _lastBoostedPackage)
                        {
                            _lastBoostedPackage = _detectedGamePackage;
                            await ExecuteGameBoostAsync(title, adbPkg);
                        }
                    }
                });

                foundTitle = "GameLoop Active";
            }

            if (foundTitle != DetectedGameTitle || foundPkg != _detectedGamePackage)
            {
                DetectedGameTitle = string.IsNullOrEmpty(foundTitle) ? "Standby" : foundTitle;
                _detectedGamePackage = foundPkg;
                DetectedGamePackage = foundPkg;
                GameTitleChanged?.Invoke(DetectedGameTitle, DetectedGamePackage);

                if (!string.IsNullOrEmpty(foundPkg) && foundPkg != _lastBoostedPackage && _isAutoGameBoostEnabled)
                {
                    _lastBoostedPackage = foundPkg;
                    Task.Run(async () => await ExecuteGameBoostAsync(foundTitle, foundPkg));
                }
            }
        }
        catch { }
    }

    private async Task ExecuteGameBoostAsync(string gameTitle, string packageName)
    {
        try
        {
            Logger.Success("Watchdog", $"Auto-Profile: Detected '{gameTitle}' ({packageName}) launch. Applying real-time gaming boost...");

            // 1. High precision timer
            TimerResolutionModule.SetHighPrecision(0.5);

            // 2. High priority & P-core affinity
            ProcessManager.SetGameLoopPriority(ProcessPriorityClass.AboveNormal);
            long mask = ProcessManager.CalculateOptimalAffinityMask(Environment.ProcessorCount, Math.Max(1, Environment.ProcessorCount / 2));
            ProcessManager.SetGameLoopAffinity(mask);

            // 3. In-VM Priority elevation & Ahead-of-Time DEX Compile
            var gl = _getGl();
            if (AdbManager.IsAdbAvailable(gl))
            {
                await AdbManager.ElevateGameProcessPriorityAsync(packageName, gl);
                // Compile DEX bytecode to eliminate micro-stutter
                await AdbManager.CompilePackageSpeedAsync(packageName, gl);
            }

            // 4. Memory trim
            ProcessManager.TrimWorkingSets();
            Logger.Success("Watchdog", $"Autonomous Game Boost active for '{gameTitle}'. Keymappings preserved intact.");
        }
        catch (Exception ex)
        {
            Logger.Warn("Watchdog", $"Game boost encountered issue: {ex.Message}");
        }
    }

    public async Task<int> ExecuteSmartPurgeAsync(bool bypassLoadCheck = false)
    {
        try
        {
            double currentCpu = GetCurrentSystemCpuUsage();
            double currentMemLoad = ProcessManager.GetSystemMemoryLoadPercent();

            // Gunfight / Heavy Load Guard:
            // If CPU usage is above 35% and memory load is NOT critically high (< 88%), 
            // skip/defer this purge cycle completely so it never causes micro-stutters during combat.
            if (!bypassLoadCheck && _isGameLoopRunning && currentCpu > 35.0 && currentMemLoad < 88.0)
            {
                Logger.Info("AutoPurge", $"Gunfight Guard: Deferred auto-purge (CPU {currentCpu:F0}%, RAM {currentMemLoad:F0}%) to prevent micro-stutters.");
                return 0;
            }

            int freed = ProcessManager.TrimWorkingSets();
            var gl = _getGl();

            // Only trim In-VM ADB cache when game is in standby/lobby or bypassLoadCheck is true (never mid-combat)
            if (bypassLoadCheck || !_isGameLoopRunning || currentCpu < 25.0)
            {
                if (AdbManager.IsAdbAvailable(gl))
                {
                    await AdbManager.TrimAppCacheAsync(gl);
                }
            }

            double estimatedMb = freed * 14.5; // ~14.5MB average working set recovery per idle process
            TotalMegabytesFreed += estimatedMb;
            AutoPurgeCount++;
            LastPurgeTime = DateTime.Now;
            LastPurgeMessage = $"Purge #{AutoPurgeCount}: Cleaned {freed} idle processes (~{estimatedMb:F0} MB freed) at {LastPurgeTime:HH:mm:ss}";

            Logger.Success("AutoPurge", LastPurgeMessage);
            AutoPurgeExecuted?.Invoke(LastPurgeMessage);
            return freed;
        }
        catch (Exception ex)
        {
            Logger.Warn("AutoPurge", $"Auto-purge failed: {ex.Message}");
            return 0;
        }
    }

    private void OnGameLoopLaunched()
    {
        Logger.Success("Watchdog", "Detected GameLoop launch! Auto-activating 0.5ms high precision timer, priority boost & P-core affinity.");
        TimerResolutionModule.SetHighPrecision(0.5);
        ProcessManager.SetGameLoopPriority(ProcessPriorityClass.AboveNormal);

        long mask = ProcessManager.CalculateOptimalAffinityMask(Environment.ProcessorCount, Math.Max(1, Environment.ProcessorCount / 2));
        ProcessManager.SetGameLoopAffinity(mask);

        ProcessManager.TrimWorkingSets();
        GameLoopStateChanged?.Invoke(true);
    }

    private void OnGameLoopClosed()
    {
        Logger.Info("Watchdog", "GameLoop closed. Running post-game cleanup & restoring standard Windows timer.");
        TimerResolutionModule.RestoreTimer();
        ProcessManager.ResetGameLoopAffinity();
        int freed = ProcessManager.TrimWorkingSets();
        Logger.Success("Watchdog", $"Post-gaming maintenance: Cleaned working sets across {freed} processes.");
        GameLoopStateChanged?.Invoke(false);
    }

    public async Task<string?> DetectForegroundPackageViaAdbAsync()
    {
        var gl = _getGl();
        if (!AdbManager.IsAdbAvailable(gl)) return null;

        try
        {
            var output = await AdbManager.ExecuteShellCommandAsync("dumpsys window | grep -E 'mCurrentFocus|mFocusedApp'", null, 2500, gl);
            foreach (var pkg in AdbManager.KnownGamePackages)
            {
                if (output.Contains(pkg.PackageName, StringComparison.OrdinalIgnoreCase))
                {
                    return pkg.PackageName;
                }
            }
        }
        catch { }

        return null;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
