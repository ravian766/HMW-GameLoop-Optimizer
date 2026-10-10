using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public interface IAdbManager
{
    bool IsAvailable(GameLoopConfig? config = null);
    Task<string> ExecuteAdbCommandAsync(string arguments, int timeoutMs = 6000, GameLoopConfig? config = null);
    Task<string> ExecuteShellCommandAsync(string shellCommand, string? targetDevice = null, int timeoutMs = 6000, GameLoopConfig? config = null);
    Task<string> ExecuteBatchShellCommandAsync(IEnumerable<string> shellCommands, string? targetDevice = null, int timeoutMs = 12000, GameLoopConfig? config = null);
    Task<bool> BatchSetPropsAsync(IDictionary<string, string> properties, string? targetDevice = null, GameLoopConfig? config = null);
    Task<List<AdbDeviceInfo>> GetConnectedDevicesAsync(GameLoopConfig? config = null);
    Task<bool> AutoConnectGameLoopAsync(GameLoopConfig? config = null);
    Task<List<GamePackageInfo>> GetInstalledGamePackagesAsync(GameLoopConfig? config = null);
    Task<string> CompilePackageSpeedAsync(string packageName, GameLoopConfig? config = null);
    Task<bool> SetInVmResolutionAsync(int width, int height, int dpi, GameLoopConfig? config = null);
    Task<bool> ResetInVmResolutionAsync(GameLoopConfig? config = null);
    Task<bool> CaptureScreenAsync(string destinationPngPath, GameLoopConfig? config = null);
    Task<bool> TrimAppCacheAsync(GameLoopConfig? config = null, string? targetPackage = null);
    Task<bool> RestartAdbServerAsync(GameLoopConfig? config = null);
    Task<bool> Unlock120FpsAsync(GameLoopConfig? config = null);
    Task<bool> SpoofDeviceProfileAsync(DeviceProfile profile, GameLoopConfig? config = null);
    Task<bool> ConnectCustomDeviceAsync(string ipPort, GameLoopConfig? config = null);
    Task<bool> LaunchGamePackageAsync(string packageName, GameLoopConfig? config = null);
    Task<bool> ForceStopGamePackageAsync(string packageName, GameLoopConfig? config = null);
    Task<bool> ClearGameDataAsync(string packageName, GameLoopConfig? config = null);
    Task<bool> SetInVmDnsAsync(string primaryDns = "1.1.1.1", string secondaryDns = "1.0.0.1", GameLoopConfig? config = null);
    Task<bool> OptimizeInVmTcpStackAsync(GameLoopConfig? config = null);
    Task<bool> OptimizeInVmAudioLatencyAsync(GameLoopConfig? config = null);
    Task<bool> SetPointerLocationOverlayAsync(bool enabled, GameLoopConfig? config = null);
    Task<string> InstallApkAsync(string apkPath, GameLoopConfig? config = null);
    Task<bool> PullFileFromVmAsync(string remotePath, string localPath, GameLoopConfig? config = null);
    Task<bool> PushFileToVmAsync(string localPath, string remotePath, GameLoopConfig? config = null);
    Task<bool> ElevateGameProcessPriorityAsync(string packageName = "com.tencent.ig", GameLoopConfig? config = null);
    Task<string> GetPropAsync(string propKey, GameLoopConfig? config = null);
    Task<bool> SetPropAsync(string propKey, string value, GameLoopConfig? config = null);
    Task<string> GetGlobalSettingAsync(string key, GameLoopConfig? config = null);
    Task<bool> PutGlobalSettingAsync(string key, string value, GameLoopConfig? config = null);
    Task<string> GetInVmDeviceModelAsync(GameLoopConfig? config = null);
    Task<List<int>> DiscoverListeningEmulatorPortsAsync(GameLoopConfig? config = null);
    Task CleanupOfflineDevicesAsync(GameLoopConfig? config = null);
    Task<int> KillInVmBackgroundAppsAsync(IEnumerable<string>? additionalWhitelist = null, GameLoopConfig? config = null);
    Task<bool> OptimizeVmGpuRenderPipelineAsync(GameLoopConfig? config = null);
    Task<GpuRendererInfo> DetectVmGpuRendererAsync(GameLoopConfig? config = null);
    Task<bool> LockVmPowerProfileAsync(GameLoopConfig? config = null);
    Task<List<OptimizationVerificationResult>> VerifyAppliedOptimizationsAsync(IDictionary<string, string> expectedProps, IDictionary<string, string>? expectedSettings = null, GameLoopConfig? config = null);
    Task<string> PrepareForMatchAsync(string? targetPackage = null, GameLoopConfig? config = null);
    Task<bool> MitigateVmThermalThrottleAsync(GameLoopConfig? config = null);
    Task<InVmPingResult> RunInVmPingDiagnosticAsync(string host = "1.1.1.1", GameLoopConfig? config = null);
    bool EnsureAdbEnabledInRegistry(out bool wasDisabledBefore);
}

public class DefaultAdbManager : IAdbManager
{
    public static DefaultAdbManager Instance { get; } = new();

    public bool EnsureAdbEnabledInRegistry(out bool wasDisabledBefore) => AdbManager.EnsureAdbEnabledInRegistry(out wasDisabledBefore);
    public bool IsAvailable(GameLoopConfig? config = null) => AdbManager.IsAdbAvailable(config);
    public Task<string> ExecuteAdbCommandAsync(string arguments, int timeoutMs = 6000, GameLoopConfig? config = null) => AdbManager.ExecuteAdbCommandAsync(arguments, timeoutMs, config);
    public Task<string> ExecuteShellCommandAsync(string shellCommand, string? targetDevice = null, int timeoutMs = 6000, GameLoopConfig? config = null) => AdbManager.ExecuteShellCommandAsync(shellCommand, targetDevice, timeoutMs, config);
    public Task<string> ExecuteBatchShellCommandAsync(IEnumerable<string> shellCommands, string? targetDevice = null, int timeoutMs = 12000, GameLoopConfig? config = null) => AdbManager.ExecuteBatchShellCommandAsync(shellCommands, targetDevice, timeoutMs, config);
    public Task<bool> BatchSetPropsAsync(IDictionary<string, string> properties, string? targetDevice = null, GameLoopConfig? config = null) => AdbManager.BatchSetPropsAsync(properties, targetDevice, config);
    public Task<List<AdbDeviceInfo>> GetConnectedDevicesAsync(GameLoopConfig? config = null) => AdbManager.GetConnectedDevicesAsync(config);
    public Task<bool> AutoConnectGameLoopAsync(GameLoopConfig? config = null) => AdbManager.AutoConnectGameLoopAsync(config);
    public Task<List<GamePackageInfo>> GetInstalledGamePackagesAsync(GameLoopConfig? config = null) => AdbManager.GetInstalledGamePackagesAsync(config);
    public Task<string> CompilePackageSpeedAsync(string packageName, GameLoopConfig? config = null) => AdbManager.CompilePackageSpeedAsync(packageName, config);
    public Task<bool> SetInVmResolutionAsync(int width, int height, int dpi, GameLoopConfig? config = null) => AdbManager.SetInVmResolutionAsync(width, height, dpi, config);
    public Task<bool> ResetInVmResolutionAsync(GameLoopConfig? config = null) => AdbManager.ResetInVmResolutionAsync(config);
    public Task<bool> CaptureScreenAsync(string destinationPngPath, GameLoopConfig? config = null) => AdbManager.CaptureScreenAsync(destinationPngPath, config);
    public Task<bool> TrimAppCacheAsync(GameLoopConfig? config = null, string? targetPackage = null) => AdbManager.TrimAppCacheAsync(config, targetPackage);
    public Task<bool> RestartAdbServerAsync(GameLoopConfig? config = null) => AdbManager.RestartAdbServerAsync(config);
    public Task<bool> Unlock120FpsAsync(GameLoopConfig? config = null) => AdbManager.Unlock120FpsAsync(config);
    public Task<bool> SpoofDeviceProfileAsync(DeviceProfile profile, GameLoopConfig? config = null) => AdbManager.SpoofDeviceProfileAsync(profile, config);
    public Task<bool> ConnectCustomDeviceAsync(string ipPort, GameLoopConfig? config = null) => AdbManager.ConnectCustomDeviceAsync(ipPort, config);
    public Task<bool> LaunchGamePackageAsync(string packageName, GameLoopConfig? config = null) => AdbManager.LaunchGamePackageAsync(packageName, config);
    public Task<bool> ForceStopGamePackageAsync(string packageName, GameLoopConfig? config = null) => AdbManager.ForceStopGamePackageAsync(packageName, config);
    public Task<bool> ClearGameDataAsync(string packageName, GameLoopConfig? config = null) => AdbManager.ClearGameDataAsync(packageName, config);
    public Task<bool> SetInVmDnsAsync(string primaryDns = "1.1.1.1", string secondaryDns = "1.0.0.1", GameLoopConfig? config = null) => AdbManager.SetInVmDnsAsync(primaryDns, secondaryDns, config);
    public Task<bool> OptimizeInVmTcpStackAsync(GameLoopConfig? config = null) => AdbManager.OptimizeInVmTcpStackAsync(config);
    public Task<bool> OptimizeInVmAudioLatencyAsync(GameLoopConfig? config = null) => AdbManager.OptimizeInVmAudioLatencyAsync(config);
    public Task<bool> SetPointerLocationOverlayAsync(bool enabled, GameLoopConfig? config = null) => AdbManager.SetPointerLocationOverlayAsync(enabled, config);
    public Task<string> InstallApkAsync(string apkPath, GameLoopConfig? config = null) => AdbManager.InstallApkAsync(apkPath, config);
    public Task<bool> PullFileFromVmAsync(string remotePath, string localPath, GameLoopConfig? config = null) => AdbManager.PullFileFromVmAsync(remotePath, localPath, config);
    public Task<bool> PushFileToVmAsync(string localPath, string remotePath, GameLoopConfig? config = null) => AdbManager.PushFileToVmAsync(localPath, remotePath, config);
    public Task<bool> ElevateGameProcessPriorityAsync(string packageName = "com.tencent.ig", GameLoopConfig? config = null) => AdbManager.ElevateGameProcessPriorityAsync(packageName, config);
    public Task<string> GetPropAsync(string propKey, GameLoopConfig? config = null) => AdbManager.GetPropAsync(propKey, config);
    public Task<bool> SetPropAsync(string propKey, string value, GameLoopConfig? config = null) => AdbManager.SetPropAsync(propKey, value, config);
    public Task<string> GetGlobalSettingAsync(string key, GameLoopConfig? config = null) => AdbManager.GetGlobalSettingAsync(key, config);
    public Task<bool> PutGlobalSettingAsync(string key, string value, GameLoopConfig? config = null) => AdbManager.PutGlobalSettingAsync(key, value, config);
    public Task<string> GetInVmDeviceModelAsync(GameLoopConfig? config = null) => AdbManager.GetInVmDeviceModelAsync(config);
    public Task<List<int>> DiscoverListeningEmulatorPortsAsync(GameLoopConfig? config = null) => AdbManager.DiscoverListeningEmulatorPortsAsync(config);
    public Task CleanupOfflineDevicesAsync(GameLoopConfig? config = null) => AdbManager.CleanupOfflineDevicesAsync(config);
    public Task<int> KillInVmBackgroundAppsAsync(IEnumerable<string>? additionalWhitelist = null, GameLoopConfig? config = null) => AdbManager.KillInVmBackgroundAppsAsync(additionalWhitelist, config);
    public Task<bool> OptimizeVmGpuRenderPipelineAsync(GameLoopConfig? config = null) => AdbManager.OptimizeVmGpuRenderPipelineAsync(config);
    public Task<GpuRendererInfo> DetectVmGpuRendererAsync(GameLoopConfig? config = null) => AdbManager.DetectVmGpuRendererAsync(config);
    public Task<bool> LockVmPowerProfileAsync(GameLoopConfig? config = null) => AdbManager.LockVmPowerProfileAsync(config);
    public Task<List<OptimizationVerificationResult>> VerifyAppliedOptimizationsAsync(IDictionary<string, string> expectedProps, IDictionary<string, string>? expectedSettings = null, GameLoopConfig? config = null) => AdbManager.VerifyAppliedOptimizationsAsync(expectedProps, expectedSettings, config);
    public Task<string> PrepareForMatchAsync(string? targetPackage = null, GameLoopConfig? config = null) => AdbManager.PrepareForMatchAsync(targetPackage, config);
    public Task<bool> MitigateVmThermalThrottleAsync(GameLoopConfig? config = null) => AdbManager.MitigateVmThermalThrottleAsync(config);
    public Task<InVmPingResult> RunInVmPingDiagnosticAsync(string host = "1.1.1.1", GameLoopConfig? config = null) => AdbManager.RunInVmPingDiagnosticAsync(host, config);
}
