using System.Diagnostics;
using System.Security.Principal;

namespace GameLoopOptimizer.Core;

public static class PermissionManager
{
    private static bool? _isAdmin;

    public static bool IsAdministrator
    {
        get
        {
            if (_isAdmin.HasValue) return _isAdmin.Value;

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                _isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                _isAdmin = false;
            }

            return _isAdmin.Value;
        }
    }

    public static bool RestartAsAdministrator(string? arguments = null)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("PermissionManager", $"Failed to elevate process: {ex.Message}");
            return false;
        }
    }
}
