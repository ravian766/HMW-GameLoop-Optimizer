using System.Diagnostics;
using System.IO;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public static class ShortcutManager
{
    public static bool CreatePubgDesktopShortcut(GameLoopConfig config)
    {
        try
        {
            var exePath = GameLoopDetector.FindGameLoopExePath();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Logger.Error("ShortcutManager", "Cannot create shortcut: GameLoop executable was not found.");
                return false;
            }

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktopPath, "HMW - Launch PUBG Mobile (Optimized).lnk");
            var iconPath = exePath;

            // Use PowerShell to create Windows Shell Link reliably without hard COM interop dependency
            var psScript = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$Shortcut.TargetPath = '{exePath.Replace("'", "''")}'
$Shortcut.Arguments = '-startpkg com.tencent.ig -runinbackground'
$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(exePath)?.Replace("'", "''")}'
$Shortcut.Description = 'Launch PUBG Mobile with HMW Performance Optimizations'
$Shortcut.IconLocation = '{iconPath.Replace("'", "''")},0'
$Shortcut.Save()
";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"").Replace("\r\n", " ")}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);

            if (File.Exists(shortcutPath))
            {
                Logger.Success("ShortcutManager", $"Created 1-click PUBG Mobile desktop shortcut at: {shortcutPath}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("ShortcutManager", $"Failed to create desktop shortcut: {ex.Message}");
            return false;
        }
    }
}
