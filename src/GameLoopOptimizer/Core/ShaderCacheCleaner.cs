using System.IO;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class ShaderCacheCleanResult
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public double MegabytesFreed => Math.Round(BytesFreed / (1024.0 * 1024.0), 2);
    public List<string> CleanedPaths { get; } = new();
}

public static class ShaderCacheCleaner
{
    public static async Task<ShaderCacheCleanResult> PurgeShaderCacheAsync(GameLoopConfig config)
    {
        return await Task.Run(() =>
        {
            var result = new ShaderCacheCleanResult();
            var targetDirs = new List<string>();

            // 1. Local AppData DirectX shader cache
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            targetDirs.Add(Path.Combine(localAppData, "D3DSCache"));
            targetDirs.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant", "ShaderCache"));
            targetDirs.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC", "ShaderCache"));

            // 2. Temp and GameLoop download components
            var temp = Path.GetTempPath();
            targetDirs.Add(Path.Combine(temp, "TxGameDownload", "ShaderCache"));
            targetDirs.Add(@"D:\Temp\TxGameDownload\Component\ShaderCache");
            targetDirs.Add(@"C:\Temp\TxGameDownload\Component\ShaderCache");

            // 3. GameLoop installation folder shader caches
            if (!string.IsNullOrEmpty(config.InstallPath))
            {
                targetDirs.Add(Path.Combine(config.InstallPath, "ShaderCache"));
                targetDirs.Add(Path.Combine(config.InstallPath, "ui", "ShaderCache"));
            }

            foreach (var dir in targetDirs)
            {
                if (Directory.Exists(dir))
                {
                    CleanDirectory(dir, result);
                }
            }

            Logger.Success("ShaderCacheCleaner", $"Purged corrupted shader caches: {result.FilesDeleted} files removed ({result.MegabytesFreed} MB freed).");
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
