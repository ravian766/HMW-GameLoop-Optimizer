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
}

public class DefaultAdbManager : IAdbManager
{
    public static DefaultAdbManager Instance { get; } = new();

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
}
