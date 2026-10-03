using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GameLoopOptimizer.Core;

public class GameLoopWindowInfo
{
    public bool IsFound { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public IntPtr MainWindowHandle { get; set; } = IntPtr.Zero;
    public IntPtr RenderWindowHandle { get; set; } = IntPtr.Zero;
    public string WindowTitle { get; set; } = string.Empty;
    public string WindowClass { get; set; } = string.Empty;

    // Window Rect in physical screen coordinates
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }

    // Client Rect in physical screen coordinates (where 0,0 of client is at ClientOriginX, ClientOriginY)
    public int ClientOriginX { get; set; }
    public int ClientOriginY { get; set; }
    public int ClientWidth { get; set; }
    public int ClientHeight { get; set; }

    // Dimensions of borders / titlebar
    public int TitleBarHeight => Math.Max(0, ClientOriginY - WindowY);
    public int BorderWidth => Math.Max(0, ClientOriginX - WindowX);

    // Monitor coordinates
    public int MonitorX { get; set; }
    public int MonitorY { get; set; }
    public int MonitorWidth { get; set; }
    public int MonitorHeight { get; set; }

    public bool IsFullscreen => WindowWidth >= MonitorWidth && WindowHeight >= MonitorHeight;
    public DpiInfo Dpi { get; set; } = new();

    public double AspectRatio => ClientHeight > 0 ? (double)ClientWidth / ClientHeight : (16.0 / 9.0);
}

public static class WindowDetector
{
    private static readonly HashSet<string> TargetProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        GameLoopProcessNames.AndroidEmulator,
        GameLoopProcessNames.AndroidEmulatorEn,
        GameLoopProcessNames.AndroidEmulatorEx,
        GameLoopProcessNames.AowExe,
        GameLoopProcessNames.AppMarket
    };

    /// <summary>
    /// Locates the active GameLoop / PUBG Mobile emulator window and extracts client viewport and monitor metrics.
    /// </summary>
    public static GameLoopWindowInfo DetectGameLoopWindow()
    {
        var info = new GameLoopWindowInfo();

        var processMap = new Dictionary<uint, Process>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (TargetProcesses.Contains(p.ProcessName))
                {
                    processMap[(uint)p.Id] = p;
                }
            }
            catch { }
        }

        if (processMap.Count == 0)
        {
            return info;
        }

        IntPtr bestHwnd = IntPtr.Zero;
        uint bestPid = 0;
        string bestTitle = string.Empty;
        string bestClass = string.Empty;
        int maxArea = 0;

        NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (!processMap.TryGetValue(pid, out var proc)) return true;

            var sbTitle = new StringBuilder(256);
            var sbClass = new StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, sbTitle, 256);
            NativeMethods.GetClassName(hwnd, sbClass, 256);

            string title = sbTitle.ToString();
            string className = sbClass.ToString();

            NativeMethods.GetWindowRect(hwnd, out var rect);
            int width = rect.Width;
            int height = rect.Height;
            int area = width * height;

            // Prioritize windows that have dimensions or title related to emulator
            if (width > 300 && height > 200 && area > maxArea)
            {
                // Check if title or class indicates emulator
                bool isEmulatorCandidate = title.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) ||
                                           title.Contains("PUBG", StringComparison.OrdinalIgnoreCase) ||
                                           title.Contains("Turbo", StringComparison.OrdinalIgnoreCase) ||
                                           className.Contains("TXGuiFoundation", StringComparison.OrdinalIgnoreCase) ||
                                           proc.ProcessName.Contains("AndroidEmulator", StringComparison.OrdinalIgnoreCase);

                if (isEmulatorCandidate || bestHwnd == IntPtr.Zero)
                {
                    maxArea = area;
                    bestHwnd = hwnd;
                    bestPid = pid;
                    bestTitle = title;
                    bestClass = className;
                }
            }

            return true;
        }, IntPtr.Zero);

        if (bestHwnd != IntPtr.Zero && processMap.TryGetValue(bestPid, out var bestProc))
        {
            info.IsFound = true;
            info.ProcessName = bestProc.ProcessName;
            info.ProcessId = (int)bestPid;
            info.MainWindowHandle = bestHwnd;
            info.WindowTitle = bestTitle;
            info.WindowClass = bestClass;

            NativeMethods.GetWindowRect(bestHwnd, out var wRect);
            info.WindowX = wRect.Left;
            info.WindowY = wRect.Top;
            info.WindowWidth = wRect.Width;
            info.WindowHeight = wRect.Height;

            NativeMethods.GetClientRect(bestHwnd, out var cRect);
            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(bestHwnd, ref origin);

            info.ClientOriginX = origin.X;
            info.ClientOriginY = origin.Y;
            info.ClientWidth = cRect.Width;
            info.ClientHeight = cRect.Height;

            // Check if child render window exists
            IntPtr childRender = IntPtr.Zero;
            NativeMethods.EnumChildWindows(bestHwnd, (child, lParam) =>
            {
                var sbChildClass = new StringBuilder(256);
                NativeMethods.GetClassName(child, sbChildClass, 256);
                string cc = sbChildClass.ToString();
                if (cc.Contains("Render", StringComparison.OrdinalIgnoreCase) || cc.Contains("AEngine", StringComparison.OrdinalIgnoreCase))
                {
                    childRender = child;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            info.RenderWindowHandle = childRender != IntPtr.Zero ? childRender : bestHwnd;

            // Monitor detection
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(bestHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (hMonitor != IntPtr.Zero)
            {
                var mi = new NativeMethods.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    info.MonitorX = mi.rcMonitor.Left;
                    info.MonitorY = mi.rcMonitor.Top;
                    info.MonitorWidth = mi.rcMonitor.Width;
                    info.MonitorHeight = mi.rcMonitor.Height;
                }
            }

            info.Dpi = DpiDetector.GetDpiForWindow(bestHwnd);
        }

        return info;
    }
}
