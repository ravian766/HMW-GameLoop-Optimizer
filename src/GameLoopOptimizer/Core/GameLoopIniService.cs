using System.IO;
using System.Text;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

/// <summary>
/// Service responsible for locating, reading, updating, and backing up GameLoop configuration INI files
/// (GameLoop.ini / AppMarket.ini) and UserDir paths across GameLoop versions including 7.0.19.05.
/// </summary>
public static class GameLoopIniService
{
    private static readonly object _fileLock = new();

    /// <summary>
    /// Resolves the GameLoop UserDir (user profile and engine state directory).
    /// </summary>
    public static string FindUserDir(GameLoopConfig config)
    {
        if (!string.IsNullOrEmpty(config.UserDir) && Directory.Exists(config.UserDir))
        {
            return config.UserDir;
        }

        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            candidates.Add(Path.Combine(config.InstallPath, "UserDir"));
            candidates.Add(Path.Combine(config.InstallPath, "ui", "UserDir"));
            candidates.Add(Path.Combine(config.InstallPath, "TxGameAssistant", "UserDir"));
            candidates.Add(Path.Combine(config.InstallPath, "TxGameAssistant", "ui", "UserDir"));
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        candidates.Add(Path.Combine(appData, "Tencent", "MobileGamePC", "UserDir"));
        candidates.Add(Path.Combine(appData, "Tencent", "MobileGamePC"));
        candidates.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC", "UserDir"));
        candidates.Add(Path.Combine(localAppData, "Tencent", "MobileGamePC"));
        candidates.Add(Path.Combine(localAppData, "Tencent", "TxGameAssistant", "UserDir"));

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Locates the GameLoop.ini (or equivalent configuration INI) file on disk.
    /// </summary>
    public static string FindIniFilePath(GameLoopConfig config)
    {
        if (!string.IsNullOrEmpty(config.IniFilePath) && File.Exists(config.IniFilePath))
        {
            return config.IniFilePath;
        }

        var candidates = new List<string>();

        // 1. Explicitly configured UserDir (if set and exists)
        if (!string.IsNullOrEmpty(config.UserDir) && Directory.Exists(config.UserDir))
        {
            candidates.Add(Path.Combine(config.UserDir, "GameLoop.ini"));
            candidates.Add(Path.Combine(config.UserDir, "config", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.UserDir, "ConfigFile", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.UserDir, "AppMarket.ini"));
        }

        // 2. InstallPath specific locations (takes precedence over machine-wide ambient AppData)
        if (!string.IsNullOrEmpty(config.InstallPath))
        {
            candidates.Add(Path.Combine(config.InstallPath, "GameLoop.ini"));
            candidates.Add(Path.Combine(config.InstallPath, "ui", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.InstallPath, "ui", "ConfigFile", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.InstallPath, "TxGameAssistant", "ui", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.InstallPath, "TxGameAssistant", "ui", "ConfigFile", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.InstallPath, "UserDir", "GameLoop.ini"));
            candidates.Add(Path.Combine(config.InstallPath, "ui", "UserDir", "GameLoop.ini"));
        }

        // 3. Fallback to auto-detected UserDir
        var userDir = FindUserDir(config);
        if (!string.IsNullOrEmpty(userDir))
        {
            candidates.Add(Path.Combine(userDir, "GameLoop.ini"));
            candidates.Add(Path.Combine(userDir, "config", "GameLoop.ini"));
            candidates.Add(Path.Combine(userDir, "ConfigFile", "GameLoop.ini"));
            candidates.Add(Path.Combine(userDir, "AppMarket.ini"));
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        candidates.Add(Path.Combine(appData, "Tencent", "MobileGamePC", "GameLoop.ini"));

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Inspects and detects UserDir and GameLoop.ini, populating the configuration object.
    /// If an INI file exists, parses its values to supplement registry configuration.
    /// </summary>
    public static void DetectAndApply(GameLoopConfig config)
    {
        try
        {
            if (string.IsNullOrEmpty(config.IniFilePath))
            {
                config.IniFilePath = FindIniFilePath(config);
            }

            if (string.IsNullOrEmpty(config.UserDir))
            {
                config.UserDir = FindUserDir(config);
            }

            if (!string.IsNullOrEmpty(config.IniFilePath) && File.Exists(config.IniFilePath))
            {
                ReadIniSettings(config.IniFilePath, config);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("GameLoopIniService", $"DetectAndApply warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads and parses GameLoop.ini settings into the GameLoopConfig object.
    /// </summary>
    public static void ReadIniSettings(string iniPath, GameLoopConfig config)
    {
        if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath)) return;

        lock (_fileLock)
        {
            try
            {
                var lines = File.ReadAllLines(iniPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;

                    int eqIdx = trimmed.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = trimmed[..eqIdx].Trim();
                    string val = trimmed[(eqIdx + 1)..].Trim();

                    // Parse resolution
                    if (key.Equals("vm_res_width", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("VMResWidth", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("width", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int w) && w > 0) config.VmResWidth = w;
                    }
                    else if (key.Equals("vm_res_height", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("VMResHeight", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("height", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int h) && h > 0) config.VmResHeight = h;
                    }
                    else if (key.Equals("vm_dpi", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("VMDPI", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int dpi) && dpi > 0) config.VmDpi = dpi;
                    }
                    // Parse CPU & Memory
                    else if (key.Equals("vm_cpu_count", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("VMCpuCount", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int cpu) && cpu > 0) config.VmCpuCount = cpu;
                    }
                    else if (key.Equals("vm_memory_size", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("VMMemorySizeInMB", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int ram) && ram > 0) config.VmMemorySizeInMb = ram;
                    }
                    // Parse Rendering Mode
                    else if (key.Equals("RenderingMode", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("renderer_mode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int rm)) config.RenderingMode = rm;
                    }
                    else if (key.Equals("ForceDirectX", StringComparison.OrdinalIgnoreCase))
                    {
                        config.ForceDirectX = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("ForceVulkan", StringComparison.OrdinalIgnoreCase))
                    {
                        config.ForceVulkan = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("SmartModeEnabled", StringComparison.OrdinalIgnoreCase))
                    {
                        config.SmartModeEnabled = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    // Anti-aliasing
                    else if (key.Equals("AntiAliasingMode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int aa)) config.AntiAliasingMode = aa;
                    }
                    else if (key.Equals("FxaaQuality", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int fxaa)) config.FxaaQuality = fxaa;
                    }
                    // VSync & Caches
                    else if (key.Equals("VSyncEnabled", StringComparison.OrdinalIgnoreCase))
                    {
                        config.VSyncEnabled = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("LocalShaderCacheEnabled", StringComparison.OrdinalIgnoreCase))
                    {
                        config.LocalShaderCacheEnabled = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }

                Logger.Info("GameLoopIniService", $"Parsed settings from '{iniPath}': Res={config.VmResWidth}x{config.VmResHeight}, Renderer={config.ActiveRenderer}");
            }
            catch (Exception ex)
            {
                Logger.Warn("GameLoopIniService", $"Failed to parse INI file '{iniPath}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Synchronizes current GameLoopConfig values into GameLoop.ini, recording backups for rollbacks.
    /// </summary>
    public static bool SyncConfigToIni(GameLoopConfig config, bool recordBackup = true)
    {
        string iniPath = FindIniFilePath(config);
        if (string.IsNullOrEmpty(iniPath))
        {
            // If UserDir exists, construct default GameLoop.ini
            string userDir = FindUserDir(config);
            if (!string.IsNullOrEmpty(userDir) && Directory.Exists(userDir))
            {
                iniPath = Path.Combine(userDir, "GameLoop.ini");
                config.IniFilePath = iniPath;
            }
            else
            {
                return false;
            }
        }

        return WriteIniSettings(iniPath, config, recordBackup);
    }

    /// <summary>
    /// Writes settings to GameLoop.ini, preserving existing comments and unrelated keys.
    /// Records changes in BackupManager for safe rollback.
    /// </summary>
    public static bool WriteIniSettings(string iniPath, GameLoopConfig config, bool recordBackup = true)
    {
        if (string.IsNullOrEmpty(iniPath)) return false;

        lock (_fileLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(iniPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var targetValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["VMResWidth"] = config.VmResWidth.ToString(),
                    ["VMResHeight"] = config.VmResHeight.ToString(),
                    ["VMDPI"] = config.VmDpi.ToString(),
                    ["VMCpuCount"] = config.VmCpuCount.ToString(),
                    ["VMMemorySizeInMB"] = config.VmMemorySizeInMb.ToString(),
                    ["ForceDirectX"] = config.ForceDirectX ? "1" : "0",
                    ["ForceVulkan"] = config.ForceVulkan ? "1" : "0",
                    ["SmartModeEnabled"] = config.SmartModeEnabled ? "1" : "0",
                    ["RenderingMode"] = config.RenderingMode.ToString(),
                    ["AntiAliasingMode"] = config.AntiAliasingMode.ToString(),
                    ["FxaaQuality"] = config.FxaaQuality.ToString(),
                    ["LocalShaderCacheEnabled"] = config.LocalShaderCacheEnabled ? "1" : "0",
                    ["ShaderCacheEnabled"] = config.ShaderCacheEnabled ? "1" : "0",
                    ["VSyncEnabled"] = config.VSyncEnabled ? "1" : "0"
                };

                var existingLines = File.Exists(iniPath) ? File.ReadAllLines(iniPath).ToList() : new List<string>();
                var updatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < existingLines.Count; i++)
                {
                    string line = existingLines[i].Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";")) continue;

                    int eqIdx = line.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = line[..eqIdx].Trim();
                    string oldVal = line[(eqIdx + 1)..].Trim();

                    if (targetValues.TryGetValue(key, out var newVal))
                    {
                        if (recordBackup && oldVal != newVal)
                        {
                            BackupManager.RecordBackup(new BackupEntry
                            {
                                ModuleId = "gameloop_ini_config",
                                Title = $"GameLoop INI ({key})",
                                Category = OptimizationCategory.GameLoopEngine,
                                TargetType = "IniFile",
                                TargetPath = iniPath,
                                ValueName = key,
                                PreviousValue = oldVal,
                                PreviousValueKind = "String",
                                NewValue = newVal,
                                Description = $"Update GameLoop.ini {key}={newVal} (Previous: {oldVal})"
                            });
                        }

                        existingLines[i] = $"{key}={newVal}";
                        updatedKeys.Add(key);
                    }
                }

                // If file didn't exist or some keys were missing, append them under [Engine]
                var missingKeys = targetValues.Where(kvp => !updatedKeys.Contains(kvp.Key)).ToList();
                if (missingKeys.Count > 0)
                {
                    if (existingLines.Count == 0 || !existingLines.Any(l => l.Trim().Equals("[Engine]", StringComparison.OrdinalIgnoreCase)))
                    {
                        existingLines.Add("[Engine]");
                    }

                    foreach (var kvp in missingKeys)
                    {
                        if (recordBackup)
                        {
                            BackupManager.RecordBackup(new BackupEntry
                            {
                                ModuleId = "gameloop_ini_config",
                                Title = $"GameLoop INI ({kvp.Key})",
                                Category = OptimizationCategory.GameLoopEngine,
                                TargetType = "IniFile",
                                TargetPath = iniPath,
                                ValueName = kvp.Key,
                                PreviousValue = null,
                                PreviousValueKind = "String",
                                NewValue = kvp.Value,
                                Description = $"Add GameLoop.ini {kvp.Key}={kvp.Value}"
                            });
                        }

                        existingLines.Add($"{kvp.Key}={kvp.Value}");
                    }
                }

                File.WriteAllLines(iniPath, existingLines, Encoding.UTF8);
                Logger.Success("GameLoopIniService", $"Successfully synchronized GameLoop INI configuration at '{iniPath}'");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("GameLoopIniService", $"Failed to write INI settings to '{iniPath}': {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Restores an individual key within GameLoop.ini to its prior value during rollback.
    /// </summary>
    public static bool RestoreIniEntry(string iniPath, string keyName, string? previousValue)
    {
        if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath)) return false;

        lock (_fileLock)
        {
            try
            {
                var lines = File.ReadAllLines(iniPath).ToList();
                bool found = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";")) continue;

                    int eqIdx = line.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = line[..eqIdx].Trim();
                    if (key.Equals(keyName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (previousValue != null)
                        {
                            lines[i] = $"{keyName}={previousValue}";
                        }
                        else
                        {
                            lines.RemoveAt(i);
                        }
                        found = true;
                        break;
                    }
                }

                if (!found && previousValue != null)
                {
                    lines.Add($"{keyName}={previousValue}");
                }

                File.WriteAllLines(iniPath, lines, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("GameLoopIniService", $"Failed to restore INI key '{keyName}' in '{iniPath}': {ex.Message}");
                return false;
            }
        }
    }
}
