using System.IO;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class JunkCategory
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> TargetPaths { get; set; } = new();
    public List<string> SearchPatterns { get; set; } = new();
    public long TotalBytes { get; set; }
    public int FileCount { get; set; }
    public bool IsSelected { get; set; } = true;

    public string SizeFormatted
    {
        get
        {
            if (TotalBytes >= 1024 * 1024 * 1024)
                return $"{TotalBytes / (1024.0 * 1024 * 1024):F2} GB";
            if (TotalBytes >= 1024 * 1024)
                return $"{TotalBytes / (1024.0 * 1024):F1} MB";
            if (TotalBytes >= 1024)
                return $"{TotalBytes / 1024.0:F0} KB";
            return $"{TotalBytes} B";
        }
    }
}

public class DeepCleanScanResult
{
    public List<JunkCategory> Categories { get; set; } = new();
    public long TotalJunkBytes => Categories.Where(c => c.IsSelected).Sum(c => c.TotalBytes);
    public int TotalFileCount => Categories.Where(c => c.IsSelected).Sum(c => c.FileCount);

    public string TotalSizeFormatted
    {
        get
        {
            long b = TotalJunkBytes;
            if (b >= 1024 * 1024 * 1024)
                return $"{b / (1024.0 * 1024 * 1024):F2} GB";
            if (b >= 1024 * 1024)
                return $"{b / (1024.0 * 1024):F1} MB";
            if (b >= 1024)
                return $"{b / 1024.0:F0} KB";
            return $"{b} B";
        }
    }
}

public class DeepCleanExecutionResult
{
    public bool Success { get; set; }
    public long BytesFreed { get; set; }
    public int FilesDeleted { get; set; }
    public int ErrorsCount { get; set; }
    public string Message { get; set; } = string.Empty;

    public string SizeFreedFormatted
    {
        get
        {
            if (BytesFreed >= 1024 * 1024 * 1024)
                return $"{BytesFreed / (1024.0 * 1024 * 1024):F2} GB";
            if (BytesFreed >= 1024 * 1024)
                return $"{BytesFreed / (1024.0 * 1024):F1} MB";
            if (BytesFreed >= 1024)
                return $"{BytesFreed / 1024.0:F0} KB";
            return $"{BytesFreed} B";
        }
    }
}

public static class DeepCleanerService
{
    public static DeepCleanScanResult ScanJunk(GameLoopConfig config)
    {
        var result = new DeepCleanScanResult();
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string tempPath = Path.GetTempPath();

        // 1. DirectX & GPU Shader Caches
        var shaderCat = new JunkCategory
        {
            Id = "shaders",
            Title = "DirectX & GPU Driver Shader Caches",
            Description = "Stale compiled shader pipelines (DirectX, NVIDIA GLCache/DXCache, AMD, Intel)."
        };
        AddDir(shaderCat, Path.Combine(localApp, "D3DSCache"));
        AddDir(shaderCat, Path.Combine(localApp, "Microsoft", "DirectX Shader Cache"));
        AddDir(shaderCat, Path.Combine(localApp, "NVIDIA", "DXCache"));
        AddDir(shaderCat, Path.Combine(localApp, "NVIDIA", "GLCache"));
        AddDir(shaderCat, Path.Combine(localApp, "AMD", "DxCache"));
        AddDir(shaderCat, Path.Combine(localApp, "AMD", "GLCache"));
        AddDir(shaderCat, Path.Combine(localApp, "Intel", "ShaderCache"));
        result.Categories.Add(shaderCat);

        // 2. GameLoop Crash Dumps & MiniDumps
        var dumpCat = new JunkCategory
        {
            Id = "crash_dumps",
            Title = "Crash Dumps & Error Memory Logs",
            Description = "Leftover .dmp and .mdmp crash diagnostics generated after emulator errors."
        };
        AddDir(dumpCat, Path.Combine(localApp, "Tencent", "TxGameAssistant", "CrashDumps"));
        AddDir(dumpCat, Path.Combine(localApp, "CrashDumps"));
        AddDir(dumpCat, Path.Combine(programData, "Tencent", "CrashDumps"));
        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            AddDir(dumpCat, Path.Combine(config.InstallPath, "ui", "CrashDumps"));
            AddDir(dumpCat, Path.Combine(config.InstallPath, "AppMarket", "CrashDumps"));
        }
        dumpCat.SearchPatterns.AddRange(new[] { "*.dmp", "*.mdmp", "*.hdmp" });
        result.Categories.Add(dumpCat);

        // 3. GameLoop Logs & Diagnostic Traces
        var logCat = new JunkCategory
        {
            Id = "gl_logs",
            Title = "GameLoop Runtime & Update Logs",
            Description = "Outdated emulator startup logs, TLog files, and diagnostic traces."
        };
        AddDir(logCat, Path.Combine(localApp, "Tencent", "TxGameAssistant", "Logs"));
        AddDir(logCat, Path.Combine(localApp, "Tencent", "MobileGamePC", "Logs"));
        AddDir(logCat, Path.Combine(appData, "Tencent", "TxGameAssistant", "Logs"));
        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            AddDir(logCat, Path.Combine(config.InstallPath, "ui", "logs"));
            AddDir(logCat, Path.Combine(config.InstallPath, "AppMarket", "logs"));
        }
        logCat.SearchPatterns.AddRange(new[] { "*.log", "*.tlog", "*.txt" });
        result.Categories.Add(logCat);

        // 4. Temporary Download Buffers & AOW Installer Clutter
        var tempCat = new JunkCategory
        {
            Id = "temp_buffers",
            Title = "AOW Temp Buffers & Download Fragments",
            Description = "Installer fragments, temporary game resource extraction buffers, and APK staging files."
        };
        AddDir(tempCat, Path.Combine(tempPath, "TxGameAssistant"));
        AddDir(tempCat, Path.Combine(localApp, "Tencent", "TxGameAssistant", "TBox", "Temp"));
        AddDir(tempCat, Path.Combine(localApp, "Tencent", "MobileGamePC", "Temp"));
        result.Categories.Add(tempCat);

        // Calculate size for all categories
        foreach (var cat in result.Categories)
        {
            var (bytes, count) = CalculateCategorySize(cat);
            cat.TotalBytes = bytes;
            cat.FileCount = count;
        }

        return result;
    }

    public static async Task<DeepCleanExecutionResult> CleanJunkAsync(DeepCleanScanResult scanResult, GameLoopConfig config, Action<string>? log = null)
    {
        var execResult = new DeepCleanExecutionResult();

        await Task.Run(() =>
        {
            foreach (var cat in scanResult.Categories.Where(c => c.IsSelected))
            {
                log?.Invoke($"Cleaning {cat.Title}...");
                foreach (var path in cat.TargetPaths)
                {
                    if (Directory.Exists(path))
                    {
                        var (freed, deleted, errors) = PurgeDirectory(path, cat.SearchPatterns, log);
                        execResult.BytesFreed += freed;
                        execResult.FilesDeleted += deleted;
                        execResult.ErrorsCount += errors;
                    }
                }
            }
        });

        // If ADB device is active, also trigger in-VM trim
        try
        {
            if (!string.IsNullOrEmpty(AdbManager.ActiveDeviceSerial))
            {
                log?.Invoke("Executing In-VM Android cache trim via ADB...");
                await AdbManager.ExecuteShellCommandAsync("pm trim-caches 21474836480"); // 20 GB request to purge all caches
                await AdbManager.ExecuteShellCommandAsync("rm -rf /sdcard/Android/data/*/cache/*");
            }
        }
        catch { }

        execResult.Success = execResult.FilesDeleted > 0 || execResult.BytesFreed > 0;
        execResult.Message = execResult.Success
            ? $"Cleaned {execResult.SizeFreedFormatted} across {execResult.FilesDeleted} junk files!"
            : "No junk files found or files currently in use by running processes.";

        Logger.Success("DeepCleaner", execResult.Message);
        return execResult;
    }

    private static void AddDir(JunkCategory cat, string dir)
    {
        if (Directory.Exists(dir) && !cat.TargetPaths.Contains(dir, StringComparer.OrdinalIgnoreCase))
        {
            cat.TargetPaths.Add(dir);
        }
    }

    private static (long bytes, int count) CalculateCategorySize(JunkCategory cat)
    {
        long totalBytes = 0;
        int fileCount = 0;

        foreach (var dir in cat.TargetPaths)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = cat.SearchPatterns.Count > 0
                    ? cat.SearchPatterns.SelectMany(p => Directory.EnumerateFiles(dir, p, SearchOption.AllDirectories))
                    : Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        totalBytes += fi.Length;
                        fileCount++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        return (totalBytes, fileCount);
    }

    private static (long freed, int deleted, int errors) PurgeDirectory(string dir, List<string> patterns, Action<string>? log)
    {
        long freed = 0;
        int deleted = 0;
        int errors = 0;

        if (!Directory.Exists(dir)) return (0, 0, 0);

        try
        {
            var files = patterns.Count > 0
                ? patterns.SelectMany(p => Directory.EnumerateFiles(dir, p, SearchOption.AllDirectories))
                : Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);

            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    long len = fi.Length;
                    File.Delete(f);
                    freed += len;
                    deleted++;
                }
                catch
                {
                    errors++; // File in use / access locked
                }
            }

            if (patterns.Count == 0)
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    try
                    {
                        Directory.Delete(sub, true);
                    }
                    catch { }
                }
            }
        }
        catch { }

        return (freed, deleted, errors);
    }
}
