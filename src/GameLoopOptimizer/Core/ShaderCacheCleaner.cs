using System.IO;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class ShaderCacheCleanResult
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public double MegabytesFreed => Math.Round(BytesFreed / (1024.0 * 1024.0), 2);
    public List<string> CleanedPaths { get; } = new();
    public bool SkippedDueToRunningProcess { get; set; }
}

public static class ShaderCacheCleaner
{
    public static List<string> GetShaderCachePaths(GameLoopConfig? config = null)
    {
        var targetDirs = new List<string>();

        // 1. DirectX and GameLoop internal caches
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        targetDirs.Add(Path.Combine(localAppData, "D3DSCache"));
        targetDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant", "ShaderCache"));
        targetDirs.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC", "ShaderCache"));

        // 2. NVIDIA GPU Driver Shader Caches
        targetDirs.Add(Path.Combine(localAppData, "NVIDIA", "DXCache"));
        targetDirs.Add(Path.Combine(localAppData, "NVIDIA", "GLCache"));
        targetDirs.Add(Path.Combine(localAppData, "NVIDIA Corporation", "NV_Cache"));
        targetDirs.Add(Path.Combine(appData, "NVIDIA", "ComputeCache"));

        // 3. AMD Radeon Driver Shader Caches
        targetDirs.Add(Path.Combine(localAppData, "AMD", "DxCache"));
        targetDirs.Add(Path.Combine(localAppData, "AMD", "DxcCache"));
        targetDirs.Add(Path.Combine(localAppData, "AMD", "OglCache"));

        // 4. Intel Graphics Driver Shader Caches
        targetDirs.Add(Path.Combine(localAppData, "Intel", "ShaderCache"));
        targetDirs.Add(Path.Combine(localAppData, "Intel", "GfxCache"));

        // 5. Temp and GameLoop download components
        var temp = Path.GetTempPath();
        targetDirs.Add(Path.Combine(temp, "TxGameDownload", "ShaderCache"));
        targetDirs.Add(@"D:\Temp\TxGameDownload\Component\ShaderCache");
        targetDirs.Add(@"C:\Temp\TxGameDownload\Component\ShaderCache");

        // 6. GameLoop installation folder shader caches
        if (config != null && !string.IsNullOrEmpty(config.InstallPath))
        {
            targetDirs.Add(Path.Combine(config.InstallPath, "ShaderCache"));
            targetDirs.Add(Path.Combine(config.InstallPath, "ui", "ShaderCache"));
        }

        return targetDirs;
    }

    public static async Task<ShaderCacheCleanResult> PurgeShaderCacheAsync(GameLoopConfig config)
    {
        return await Task.Run(() =>
        {
            var result = new ShaderCacheCleanResult();

            // Safety Guard: Never purge shader cache while GameLoop is actively running
            if (ProcessManager.IsGameLoopRunning())
            {
                result.SkippedDueToRunningProcess = true;
                Logger.Warn("ShaderCacheCleaner", "GameLoop is currently running. Please exit GameLoop before purging shader cache to prevent rendering hitches or crashes.");
                return result;
            }

            var targetDirs = GetShaderCachePaths(config);

            foreach (var dir in targetDirs)
            {
                if (Directory.Exists(dir))
                {
                    CleanDirectory(dir, result);
                }
            }

            Logger.Success("ShaderCacheCleaner", $"Purged DirectX & OpenGL shader caches: {result.FilesDeleted} files removed ({result.MegabytesFreed} MB freed across {result.CleanedPaths.Count} directories).");
            return result;
        });
    }

    private static void CleanDirectory(string dirPath, ShaderCacheCleanResult result)
    {
        try
        {
            var dir = new DirectoryInfo(dirPath);
            foreach (var file in dir.GetFiles("*.*", SearchOption.AllDirectories))
            {
                try
                {
                    long size = file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    result.FilesDeleted++;
                    result.BytesFreed += size;
                }
                catch
                {
                    // File is locked by active driver thread - safely continue
                }
            }

            // Prune empty subdirectories
            foreach (var subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
            {
                try
                {
                    if (subDir.Exists && !subDir.EnumerateFileSystemInfos().Any())
                    {
                        subDir.Delete();
                    }
                }
                catch { }
            }

            result.CleanedPaths.Add(dirPath);
        }
        catch (Exception ex)
        {
            Logger.Warn("ShaderCacheCleaner", $"Could not clean folder '{dirPath}': {ex.Message}");
        }
    }
}
