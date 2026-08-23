using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GameLoopOptimizer.Core;

public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    public const int HOTKEY_OVERLAY_ID = 9001; // Ctrl + Shift + O
    public const int HOTKEY_TRIM_ID = 9002;    // Ctrl + Shift + M

    private IntPtr _hwnd;
    private HwndSource? _source;

    public event Action? OverlayHotkeyPressed;
    public event Action? TrimHotkeyPressed;

    public void Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;

        if (_hwnd == IntPtr.Zero)
        {
            window.Loaded += (s, e) => RegisterInternal(new WindowInteropHelper(window).Handle);
        }
        else
        {
            RegisterInternal(_hwnd);
        }
    }

    private void RegisterInternal(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);

        // VK_O = 0x4F, VK_M = 0x4D
        RegisterHotKey(_hwnd, HOTKEY_OVERLAY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 0x4F);
        RegisterHotKey(_hwnd, HOTKEY_TRIM_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 0x4D);

        Logger.Info("HotkeyManager", "Global hotkeys active: Ctrl+Shift+O (HUD Overlay), Ctrl+Shift+M (Trim RAM).");
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_OVERLAY_ID)
            {
                OverlayHotkeyPressed?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_TRIM_ID)
            {
                TrimHotkeyPressed?.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_OVERLAY_ID);
            UnregisterHotKey(_hwnd, HOTKEY_TRIM_ID);
        }
        _source?.RemoveHook(HwndHook);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
