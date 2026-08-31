using System.IO;
using System.Text.RegularExpressions;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class KeymapSpeedResult
{
    public bool Success { get; set; }
    public int TargetSpeed { get; set; }
    public int FilesUpdated { get; set; }
    public int NodesUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class KeymapSpeedService
{
    public const int DefaultSpeed = 80;
    public const int StableSpeed = 90;
    public const int InstantMaxSpeed = 100;

    public static string KeymapBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameLoopOptimizer",
        "KeymapSpeedBackups"
    );

    public static List<string> EnumerateAllKeymapFiles(GameLoopConfig? config = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Files from ResolutionKeymapService standard paths
        if (config != null)
        {
            foreach (var p in ResolutionKeymapService.GetKeymapFilePaths(config))
            {
                if (File.Exists(p)) paths.Add(p);
            }
        }

        // 2. User Keymap Profile Directories in AppData & LocalAppData
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidateDirs = new[]
        {
            Path.Combine(appData, "Tencent", "GameLoop", "Keymap"),
            Path.Combine(appData, "Tencent", "TxGameAssistant", "Keymap"),
            Path.Combine(appData, "Tencent", "TxGameAssistant", "ConfigFile"),
            Path.Combine(localAppData, "Tencent", "TxGameAssistant", "Keymap"),
            Path.Combine(localAppData, "Tencent", "TxGameAssistant", "ConfigFile"),
            Path.Combine(localAppData, "Tencent", "MobileGamePC", "Keymap")
        };

        foreach (var dir in candidateDirs)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories))
                    {
                        paths.Add(file);
                    }
                }
                catch { }
            }
        }

        return paths.ToList();
    }

    public static (string updatedXml, int nodesChanged) InjectWasdSpeed(string xmlContent, int speed)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return (xmlContent, 0);

        int nodesChanged = 0;
        speed = Math.Clamp(speed, 50, 100);

        // 1. Update existing Speed attributes
        var speedRegex = new Regex(@"Speed=""\d+""", RegexOptions.IgnoreCase);
        var matches = speedRegex.Matches(xmlContent);
        nodesChanged += matches.Count;

        string updatedXml = speedRegex.Replace(xmlContent, $"Speed=\"{speed}\"");

        // 2. If WASD/Rocker elements exist without Speed attribute, inject Speed="{speed}"
        var rockerWithoutSpeedRegex = new Regex(@"(<(?:KeyMap|Rocker|Item)[^>]*?\b(?:Item=""WASD""|Type=""Rocker""|Mode=""Rocker"")[^>]*?)(?<!Speed=""\d+"")(>|\/>)", RegexOptions.IgnoreCase);
        if (rockerWithoutSpeedRegex.IsMatch(updatedXml))
        {
            updatedXml = rockerWithoutSpeedRegex.Replace(updatedXml, m =>
            {
                if (!m.Value.Contains("Speed=", StringComparison.OrdinalIgnoreCase))
                {
                    nodesChanged++;
                    string prefix = m.Groups[1].Value.TrimEnd();
                    string closing = m.Groups[2].Value;
                    return $"{prefix} Speed=\"{speed}\"{closing}";
                }
                return m.Value;
            });
        }

        return (updatedXml, nodesChanged);
    }

    public static async Task<KeymapSpeedResult> ApplyWasdSpeedAsync(int speed, GameLoopConfig? config = null)
    {
        var result = new KeymapSpeedResult
        {
            TargetSpeed = speed
        };

        try
        {
            var targetFiles = EnumerateAllKeymapFiles(config);
            if (targetFiles.Count == 0)
            {
                result.Success = false;
                result.Message = "No GameLoop keymap XML files found to configure.";
                return result;
            }

            Directory.CreateDirectory(KeymapBackupDirectory);
            int filesUpdated = 0;
            int totalNodes = 0;

            foreach (var file in targetFiles)
            {
                try
                {
                    string originalText = await File.ReadAllTextAsync(file);
                    if (string.IsNullOrWhiteSpace(originalText)) continue;

                    // Create backup
                    string backupFile = Path.Combine(KeymapBackupDirectory, $"{Path.GetFileNameWithoutExtension(file)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xml");
                    await File.WriteAllTextAsync(backupFile, originalText);

                    var (updatedText, nodesChanged) = InjectWasdSpeed(originalText, speed);
                    if (nodesChanged > 0 || updatedText != originalText)
                    {
                        await File.WriteAllTextAsync(file, updatedText);
                        filesUpdated++;
                        totalNodes += nodesChanged;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("KeymapSpeedService", $"Could not patch file '{file}': {ex.Message}");
                }
            }

            result.FilesUpdated = filesUpdated;
            result.NodesUpdated = totalNodes;
            result.Success = filesUpdated > 0;
            result.Message = result.Success
                ? $"Applied WASD speed {speed}% across {filesUpdated} keymap files ({totalNodes} joystick nodes updated)!"
                : "No WASD joystick nodes required speed modification.";

            Logger.Success("KeymapSpeedService", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error applying WASD speed: {ex.Message}";
            Logger.Error("KeymapSpeedService", result.Message);
        }

        return result;
    }
}
