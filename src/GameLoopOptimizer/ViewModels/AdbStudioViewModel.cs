using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.ViewModels;

public class AdbStudioViewModel : ViewModelBase
{
    private readonly Func<GameLoopConfig> _getGl;
    private readonly IEventAggregator _eventAggregator;

    private bool _isAdbAvailable;
    public bool IsAdbAvailable
    {
        get => _isAdbAvailable;
        set => SetProperty(ref _isAdbAvailable, value);
    }

    private bool _isAdbConnected;
    public bool IsAdbConnected
    {
        get => _isAdbConnected;
        set => SetProperty(ref _isAdbConnected, value);
    }

    private string _adbDeviceName = "Not Connected";
    public string AdbDeviceName
    {
        get => _adbDeviceName;
        set => SetProperty(ref _adbDeviceName, value);
    }

    private string _adbStatusText = "Checking ADB Status...";
    public string AdbStatusText
    {
        get => _adbStatusText;
        set => SetProperty(ref _adbStatusText, value);
    }

    private bool _isAdbBusy;
    public bool IsAdbBusy
    {
        get => _isAdbBusy;
        set => SetProperty(ref _isAdbBusy, value);
    }

    private bool _adbZeroAnimations = true;
    public bool AdbZeroAnimations
    {
        get => _adbZeroAnimations;
        set => SetProperty(ref _adbZeroAnimations, value);
    }

    private bool _adbGpuAcceleration = true;
    public bool AdbGpuAcceleration
    {
        get => _adbGpuAcceleration;
        set => SetProperty(ref _adbGpuAcceleration, value);
    }

    private bool _adbDalvikHeapBoost = true;
    public bool AdbDalvikHeapBoost
    {
        get => _adbDalvikHeapBoost;
        set => SetProperty(ref _adbDalvikHeapBoost, value);
    }

    private bool _adbSuppressLogging = true;
    public bool AdbSuppressLogging
    {
        get => _adbSuppressLogging;
        set => SetProperty(ref _adbSuppressLogging, value);
    }

    private bool _adbDisableDoze = true;
    public bool AdbDisableDoze
    {
        get => _adbDisableDoze;
        set => SetProperty(ref _adbDisableDoze, value);
    }

    private bool _adbInputPolling = true;
    public bool AdbInputPolling
    {
        get => _adbInputPolling;
        set => SetProperty(ref _adbInputPolling, value);
    }

    private bool _adb120FpsUnlock = true;
    public bool Adb120FpsUnlock
    {
        get => _adb120FpsUnlock;
        set => SetProperty(ref _adb120FpsUnlock, value);
    }

    private bool _adbInVmDnsSync = true;
    public bool AdbInVmDnsSync
    {
        get => _adbInVmDnsSync;
        set => SetProperty(ref _adbInVmDnsSync, value);
    }

    private bool _adbAudioLatencyReduction = true;
    public bool AdbAudioLatencyReduction
    {
        get => _adbAudioLatencyReduction;
        set => SetProperty(ref _adbAudioLatencyReduction, value);
    }

    private bool _isPointerLocationEnabled;
    public bool IsPointerLocationEnabled
    {
        get => _isPointerLocationEnabled;
        set => SetProperty(ref _isPointerLocationEnabled, value);
    }

    private string _customAdbPortText = "127.0.0.1:5555";
    public string CustomAdbPortText
    {
        get => _customAdbPortText;
        set => SetProperty(ref _customAdbPortText, value);
    }

    private bool _isInstallingApk;
    public bool IsInstallingApk
    {
        get => _isInstallingApk;
        set => SetProperty(ref _isInstallingApk, value);
    }

    public ObservableCollection<GamePackageInfo> InstalledGamePackages { get; } = new();

    private GamePackageInfo? _selectedGamePackage;
    public GamePackageInfo? SelectedGamePackage
    {
        get => _selectedGamePackage;
        set
        {
            if (SetProperty(ref _selectedGamePackage, value))
            {
                _ = Task.Run(async () => await RefreshTelemetryAsync());
            }
        }
    }

    private AdbTelemetrySnapshot _adbTelemetry = new();
    public AdbTelemetrySnapshot AdbTelemetry
    {
        get => _adbTelemetry;
        set => SetProperty(ref _adbTelemetry, value);
    }

    private string _interactiveAdbCommand = string.Empty;
    public string InteractiveAdbCommand
    {
        get => _interactiveAdbCommand;
        set => SetProperty(ref _interactiveAdbCommand, value);
    }

    private string _interactiveAdbOutput = "ADB Shell Console Ready. Type a command (e.g., 'getprop ro.product.model' or 'pm list packages') and click Execute.\n";
    public string InteractiveAdbOutput
    {
        get => _interactiveAdbOutput;
        set => SetProperty(ref _interactiveAdbOutput, value);
    }

    private bool _isAdbCompiling;
    public bool IsAdbCompiling
    {
        get => _isAdbCompiling;
        set => SetProperty(ref _isAdbCompiling, value);
    }

    private string _adbCompilationStatus = "Ready to compile DEX bytecode to native AOT.";
    public string AdbCompilationStatus
    {
        get => _adbCompilationStatus;
        set => SetProperty(ref _adbCompilationStatus, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                _eventAggregator.Publish(new StatusNotificationMessage(value));
            }
        }
    }

    public ICommand ConnectAdbCommand { get; }
    public ICommand ConnectCustomPortCommand { get; }
    public ICommand ApplyAllAdbOptimizationsCommand { get; }
    public ICommand RestoreStockVmSettingsCommand { get; }
    public ICommand TrimInVmCacheCommand { get; }
    public ICommand RestartAdbServerCommand { get; }
    public ICommand CompileGameDexCommand { get; }
    public ICommand LaunchGameCommand { get; }
    public ICommand ForceStopGameCommand { get; }
    public ICommand ClearGameDataCommand { get; }
    public ICommand InstallApkCommand { get; }
    public ICommand TogglePointerLocationCommand { get; }
    public ICommand CaptureInVmScreenshotCommand { get; }
    public ICommand ExecuteCustomAdbShellCommand { get; }
    public ICommand ClearConsoleOutputCommand { get; }
    public ICommand RefreshTelemetryCommand { get; }

    public AdbStudioViewModel(Func<GameLoopConfig> getGl, IEventAggregator? eventAggregator = null)
    {
        _getGl = getGl;
        _eventAggregator = eventAggregator ?? EventAggregator.Default;

        ConnectAdbCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            AdbStatusText = "Connecting to GameLoop Android VM via ADB...";
            try
            {
                bool connected = await AdbManager.AutoConnectGameLoopAsync(_getGl());
                await RefreshAdbStatusAsync();
                StatusMessage = connected 
                    ? $"ADB Connected successfully to {AdbDeviceName}!" 
                    : "Failed to connect to GameLoop ADB. Ensure GameLoop is running.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ConnectCustomPortCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(CustomAdbPortText)) return;
            IsAdbBusy = true;
            StatusMessage = $"Connecting to ADB target {CustomAdbPortText}...";
            try
            {
                bool connected = await AdbManager.ConnectCustomDeviceAsync(CustomAdbPortText, _getGl());
                await RefreshAdbStatusAsync();
                StatusMessage = connected 
                    ? $"Connected to ADB target {CustomAdbPortText}!" 
                    : $"Failed to connect to {CustomAdbPortText}.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        LaunchGameCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbBusy = true;
            StatusMessage = $"Launching {SelectedGamePackage?.DisplayName ?? targetPkg} in Android VM...";
            try
            {
                bool ok = await AdbManager.LaunchGamePackageAsync(targetPkg, _getGl());
                StatusMessage = ok 
                    ? $"Launched {SelectedGamePackage?.DisplayName ?? targetPkg} successfully!" 
                    : $"Failed to launch {targetPkg}. Ensure GameLoop is running.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ForceStopGameCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbBusy = true;
            StatusMessage = $"Force-stopping {SelectedGamePackage?.DisplayName ?? targetPkg}...";
            try
            {
                bool ok = await AdbManager.ForceStopGamePackageAsync(targetPkg, _getGl());
                StatusMessage = ok 
                    ? $"Terminated {SelectedGamePackage?.DisplayName ?? targetPkg} process in VM." 
                    : $"Failed to stop {targetPkg}.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ClearGameDataCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbBusy = true;
            StatusMessage = $"Clearing app data for {SelectedGamePackage?.DisplayName ?? targetPkg}...";
            try
            {
                bool ok = await AdbManager.ClearGameDataAsync(targetPkg, _getGl());
                StatusMessage = ok 
                    ? $"Cleared app data for {SelectedGamePackage?.DisplayName ?? targetPkg}!" 
                    : $"Failed to clear app data for {targetPkg}.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        TrimInVmCacheCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Trimming Android VM caches and shader logs...";
            try
            {
                bool ok = await AdbManager.TrimAppCacheAsync(_getGl(), SelectedGamePackage?.PackageName);
                StatusMessage = ok ? "Android VM caches and tombstones purged!" : "Cache trim failed.";
                await RefreshTelemetryAsync();
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        RestartAdbServerCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Restarting ADB daemon subsystem...";
            try
            {
                bool ok = await AdbManager.RestartAdbServerAsync(_getGl());
                await RefreshAdbStatusAsync();
                StatusMessage = ok ? "ADB server restarted and reconnected!" : "ADB restart finished.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        CompileGameDexCommand = new AsyncRelayCommand(async () =>
        {
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            IsAdbCompiling = true;
            AdbCompilationStatus = $"Compiling DEX bytecode to AOT machine code for {SelectedGamePackage?.DisplayName ?? targetPkg}...";
            StatusMessage = $"Pre-compiling {SelectedGamePackage?.DisplayName ?? targetPkg} via Dex2Oat AOT...";
            try
            {
                var res = await AdbManager.CompilePackageSpeedAsync(targetPkg, _getGl());
                AdbCompilationStatus = res;
                StatusMessage = $"AOT compilation result: {res}";
                Logger.Success("AdbStudioVM", $"Dex2Oat result for {targetPkg}: {res}");
            }
            finally
            {
                IsAdbCompiling = false;
            }
        });

        TogglePointerLocationCommand = new AsyncRelayCommand(async () =>
        {
            IsPointerLocationEnabled = !IsPointerLocationEnabled;
            IsAdbBusy = true;
            try
            {
                await AdbManager.SetPointerLocationOverlayAsync(IsPointerLocationEnabled, _getGl());
                StatusMessage = $"In-VM touch and pointer overlay {(IsPointerLocationEnabled ? "Enabled" : "Disabled")}.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        CaptureInVmScreenshotCommand = new AsyncRelayCommand(async () =>
        {
            IsAdbBusy = true;
            StatusMessage = "Capturing native Android VM screenshot...";
            try
            {
                string shotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"GameLoop_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bool ok = await AdbManager.CaptureScreenAsync(shotPath, _getGl());
                StatusMessage = ok ? $"Screenshot saved to Pictures: {Path.GetFileName(shotPath)}" : "Screenshot capture failed.";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ExecuteCustomAdbShellCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(InteractiveAdbCommand)) return;
            string cmd = InteractiveAdbCommand.Trim();
            InteractiveAdbOutput += $"\n$ {cmd}\n";
            InteractiveAdbCommand = string.Empty;
            IsAdbBusy = true;
            try
            {
                string result = await AdbManager.ExecuteShellCommandAsync(cmd, null, 10000, _getGl());
                InteractiveAdbOutput += string.IsNullOrWhiteSpace(result) ? "[Command executed successfully with no output]\n" : $"{result}\n";
            }
            catch (Exception ex)
            {
                InteractiveAdbOutput += $"Error: {ex.Message}\n";
            }
            finally
            {
                IsAdbBusy = false;
            }
        });

        ClearConsoleOutputCommand = new RelayCommand(() =>
        {
            InteractiveAdbOutput = "ADB Shell Console Ready. Type a command (e.g., 'getprop ro.product.model' or 'pm list packages') and click Execute.\n";
        });

        RefreshTelemetryCommand = new AsyncRelayCommand(async () => await RefreshTelemetryAsync());

        InstallApkCommand = new AsyncRelayCommand(async () =>
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Android APK to Sideload into GameLoop",
                Filter = "Android Package Files (*.apk)|*.apk|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string apk = dlg.FileName;
                IsInstallingApk = true;
                StatusMessage = $"Sideloading APK {Path.GetFileName(apk)} into GameLoop VM...";
                try
                {
                    var res = await AdbManager.InstallApkAsync(apk, _getGl());
                    StatusMessage = res;
                    await RefreshAdbStatusAsync();
                }
                finally
                {
                    IsInstallingApk = false;
                }
            }
        });

        ApplyAllAdbOptimizationsCommand = new AsyncRelayCommand(ApplyAllAdbOptimizationsAsync);
        RestoreStockVmSettingsCommand = new AsyncRelayCommand(RestoreStockVmSettingsAsync);
    }

    public async Task RefreshAdbStatusAsync()
    {
        var gl = _getGl();
        IsAdbAvailable = AdbManager.IsAdbAvailable(gl);

        if (!IsAdbAvailable)
        {
            AdbStatusText = "ADB Binary Not Detected";
            IsAdbConnected = false;
            AdbDeviceName = "Not Installed";
            return;
        }

        var devices = await AdbManager.GetConnectedDevicesAsync(gl);
        var active = devices.FirstOrDefault(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase));

        if (active != null)
        {
            IsAdbConnected = true;
            AdbManager.ActiveDeviceSerial = active.Serial;
            AdbDeviceName = active.Serial;
            AdbStatusText = $"Connected ({active.Serial} - {active.Model})";
        }
        else
        {
            bool connected = await AdbManager.AutoConnectGameLoopAsync(gl);
            if (connected)
            {
                IsAdbConnected = true;
                AdbDeviceName = AdbManager.ActiveDeviceSerial ?? "127.0.0.1:5555";
                AdbStatusText = $"Connected ({AdbDeviceName})";
            }
            else
            {
                IsAdbConnected = false;
                AdbDeviceName = "Disconnected";
                AdbStatusText = "GameLoop Android VM Offline / Disconnected";
            }
        }

        if (IsAdbConnected)
        {
            var pkgs = await AdbManager.GetInstalledGamePackagesAsync(gl);
            void UpdatePkgsAction()
            {
                InstalledGamePackages.Clear();
                foreach (var p in pkgs) InstalledGamePackages.Add(p);
                if (SelectedGamePackage == null || !InstalledGamePackages.Contains(SelectedGamePackage))
                {
                    SelectedGamePackage = InstalledGamePackages.FirstOrDefault(p => p.IsInstalled) ?? InstalledGamePackages.FirstOrDefault();
                }
            }

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdatePkgsAction);
            }
            else
            {
                UpdatePkgsAction();
            }

            await RefreshTelemetryAsync();
        }
    }

    public async Task RefreshTelemetryAsync()
    {
        if (!IsAdbConnected) return;
        try
        {
            var gl = _getGl();
            var snap = await AdbTelemetryService.FetchTelemetryAsync(SelectedGamePackage?.PackageName, gl);
            AdbTelemetry = snap;
        }
        catch (Exception ex)
        {
            Logger.Warn("AdbTelemetry", $"Failed to fetch telemetry: {ex.Message}");
        }
    }

    public async Task ApplyAllAdbOptimizationsAsync()
    {
        IsAdbBusy = true;
        StatusMessage = "Applying Android VM In-Emulator Optimizations via ADB...";

        try
        {
            var gl = _getGl();
            if (!IsAdbConnected)
            {
                await AdbManager.AutoConnectGameLoopAsync(gl);
                await RefreshAdbStatusAsync();
            }

            int count = 0;
            string targetPkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            
            var batchCmds = new List<string>();

            if (AdbGpuAcceleration)
            {
                batchCmds.Add("setprop debug.sf.hw 1");
                batchCmds.Add("setprop debug.egl.hw 1");
                batchCmds.Add("setprop debug.composition.type gpu");
                batchCmds.Add("setprop debug.sf.latch_unsignaled 1");
                batchCmds.Add("setprop debug.sf.early_phase_offset_ns 500000");
                batchCmds.Add("setprop debug.sf.early_app_phase_offset_ns 500000");
                count++;
            }

            if (AdbZeroAnimations)
            {
                batchCmds.Add("settings put global window_animation_scale 0");
                batchCmds.Add("settings put global transition_animation_scale 0");
                batchCmds.Add("settings put global animator_duration_scale 0");
                count++;
            }

            if (AdbInputPolling)
            {
                batchCmds.Add("setprop windowsmgr.max_events_per_sec 240");
                batchCmds.Add("setprop persist.sys.scrollingcache 3");
                batchCmds.Add("setprop persist.vendor.touch.sensitivity 10");
                count++;
            }

            if (Adb120FpsUnlock)
            {
                batchCmds.Add("setprop debug.sf.fps 120");
                batchCmds.Add("setprop ro.surface_flinger.max_frame_rate 120");
                batchCmds.Add("setprop persist.vendor.dfps.level 120");
                count++;
            }

            if (AdbDalvikHeapBoost)
            {
                batchCmds.Add("setprop dalvik.vm.heapgrowthlimit 512m");
                batchCmds.Add("setprop dalvik.vm.heapsize 1024m");
                batchCmds.Add("setprop dalvik.vm.heaptargetutilization 0.75");
                batchCmds.Add("setprop dalvik.vm.dexopt-flags v=n,o=v");
                count++;
            }

            if (AdbSuppressLogging)
            {
                batchCmds.Add("setprop log.tag ALL=SUPPRESS");
                batchCmds.Add("setprop log.tag.stats_log OFF");
                count++;
            }

            if (AdbDisableDoze)
            {
                batchCmds.Add("settings put global app_standby_enabled 0");
                batchCmds.Add("settings put global adaptive_battery_management_enabled 0");
                batchCmds.Add($"cmd appops set {targetPkg} RUN_IN_BACKGROUND allow");
                count++;
            }

            if (AdbInVmDnsSync)
            {
                batchCmds.Add("setprop net.dns1 1.1.1.1");
                batchCmds.Add("setprop net.dns2 1.0.0.1");
                batchCmds.Add("setprop net.dnssearch local");
                batchCmds.Add("settings put global private_dns_mode off");
                batchCmds.Add("setprop net.tcp.buffersize.wifi 524288,1048576,2097152,262144,524288,1048576");
                batchCmds.Add("setprop net.tcp.buffersize.ethernet 524288,1048576,2097152,262144,524288,1048576");
                batchCmds.Add("setprop net.tcp.buffersize.default 524288,1048576,2097152,262144,524288,1048576");
                batchCmds.Add("setprop net.tcp.delack.default 1");
                batchCmds.Add("setprop persist.net.ipv6.disable 1");
                count++;
            }

            if (AdbAudioLatencyReduction)
            {
                batchCmds.Add("setprop audio.deep_buffer.media false");
                batchCmds.Add("setprop af.resampler.quality 2");
                batchCmds.Add("setprop media.stagefright.audio.sink 256");
                batchCmds.Add("setprop ro.audio.flinger_standbytime_ms 1000");
                count++;
            }

            if (batchCmds.Count > 0)
            {
                // 1. Batch execute all commands in one ADB process call
                await AdbManager.ExecuteBatchShellCommandAsync(batchCmds, null, 12000, gl);

                // 2. VM Reboot Persistence via local.prop
                var propLines = batchCmds.Where(c => c.StartsWith("setprop")).Select(c => c.Replace("setprop ", "").Replace(" ", "=")).ToList();
                if (propLines.Count > 0)
                {
                    string propFileContent = string.Join("\\n", propLines);
                    await AdbManager.ExecuteShellCommandAsync($"echo -e \"{propFileContent}\" > /data/local.prop", null, 4000, gl);
                    await AdbManager.ExecuteShellCommandAsync("chmod 644 /data/local.prop", null, 4000, gl);
                }

                // 3. Apply surfaceflinger graphics tweaks instantly
                if (AdbGpuAcceleration || Adb120FpsUnlock)
                {
                    await AdbManager.ExecuteShellCommandAsync("setprop ctl.restart surfaceflinger", null, 4000, gl);
                }
            }

            await RefreshTelemetryAsync();

            StatusMessage = $"Applied {count} Android VM optimizations via ADB successfully!";
            Logger.Success("AdbStudioVM", $"Applied {count} ADB in-VM optimization profiles for {targetPkg}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to apply ADB optimizations: {ex.Message}";
            Logger.Error("AdbStudioVM", $"ADB optimization failed: {ex.Message}");
        }
        finally
        {
            IsAdbBusy = false;
        }
    }

    public async Task RestoreStockVmSettingsAsync()
    {
        IsAdbBusy = true;
        StatusMessage = "Restoring Android VM to stock settings...";
        try
        {
            var gl = _getGl();
            await AdbManager.ExecuteShellCommandAsync("rm -f /data/local.prop", null, 4000, gl);

            var resetCmds = new List<string>
            {
                "settings put global window_animation_scale 1",
                "settings put global transition_animation_scale 1",
                "settings put global animator_duration_scale 1",
                "setprop debug.sf.fps \"\"",
                "setprop ro.surface_flinger.max_frame_rate \"\"",
                "setprop debug.sf.hw 0",
                "setprop dalvik.vm.heapsize 256m",
                "setprop net.dns1 \"\"",
                "setprop net.dns2 \"\""
            };

            await AdbManager.ExecuteBatchShellCommandAsync(resetCmds, null, 8000, gl);
            await AdbManager.ExecuteShellCommandAsync("setprop ctl.restart surfaceflinger", null, 4000, gl);

            StatusMessage = "Android VM restored to stock settings successfully!";
            Logger.Success("AdbStudioVM", "Restored stock VM settings via ADB.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to restore stock VM settings: {ex.Message}";
            Logger.Error("AdbStudioVM", $"Restore stock VM settings failed: {ex.Message}");
        }
        finally
        {
            IsAdbBusy = false;
        }
    }
}
