using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Views;

public partial class MiniOverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private bool _isClickThrough = false;
    public bool IsClickThrough => _isClickThrough;

    public MiniOverlayWindow()
    {
        InitializeComponent();
    }

    public void SetGameTitle(string gameTitle)
    {
        if (string.IsNullOrWhiteSpace(gameTitle) || gameTitle == "Standby")
        {
            TxtGameBadge.Text = "HMW HUD";
        }
        else
        {
            TxtGameBadge.Text = $"🎮 {gameTitle}";
        }
    }

    public void UpdateMetrics(PerformanceMetrics metrics)
    {
        if (metrics.Fps > 0)
        {
            TxtFps.Text = $"{metrics.Fps:F0}";
            TxtOnePercentLow.Text = metrics.OnePercentLowFps > 0 ? $"{metrics.OnePercentLowFps:F0}" : "--";

            if (metrics.Fps >= 105)
            {
                TxtFps.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76)); // Neon Green (105 - 120 FPS)
            }
            else if (metrics.Fps >= 85)
            {
                TxtFps.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)); // Cyan (85 - 104 FPS)
            }
            else if (metrics.Fps >= 60)
            {
                TxtFps.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // Gold / Amber (60 - 84 FPS)
            }
            else
            {
                TxtFps.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52)); // Red (< 60 FPS)
            }
        }
        else
        {
            TxtFps.Text = metrics.IsGameLoopActive ? "120" : "--";
            TxtOnePercentLow.Text = "--";
            TxtFps.Foreground = (Brush)FindResource("BrushTextPrimary");
        }

        TxtCpu.Text = $"{metrics.CpuTotalPercent:F0}%";
        TxtRam.Text = $"{metrics.RamPercent:F0}%";
        TxtEmulator.Text = $"{metrics.GameLoopRamMb:F0} MB";
        TxtVariance.Text = $"{metrics.EstimatedFrametimeVarianceMs:F1}ms";

        if (metrics.EstimatedFrametimeVarianceMs > 5.0 || metrics.CpuTotalPercent > 90)
        {
            TxtRadar.Text = "⚠️ Jitter";
            TxtRadar.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
            BadgeRadar.Background = new SolidColorBrush(Color.FromArgb(0x2B, 0xFF, 0x52, 0x52));
        }
        else if (metrics.EstimatedFrametimeVarianceMs > 2.5 || metrics.CpuTotalPercent > 75)
        {
            TxtRadar.Text = "⚡ Mild Spike";
            TxtRadar.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
            BadgeRadar.Background = new SolidColorBrush(Color.FromArgb(0x2B, 0xFF, 0xD7, 0x00));
        }
        else
        {
            TxtRadar.Text = "⚡ Smooth";
            TxtRadar.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            BadgeRadar.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0xE6, 0x76));
        }
    }

    public void ToggleClickThrough()
    {
        _isClickThrough = !_isClickThrough;
        ApplyClickThroughState();
    }

    private void ToggleClickThrough_Click(object sender, RoutedEventArgs e)
    {
        ToggleClickThrough();
    }

    private void ApplyClickThroughState()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (_isClickThrough)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                BtnClickThrough.Content = "🔒 Locked";
                BtnClickThrough.ToolTip = "Click-Through Active (Clicks pass into GameLoop). Press Ctrl+Shift+T to unlock.";
                BtnClickThrough.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                BtnClickThrough.Content = "🔓 Move";
                BtnClickThrough.ToolTip = "Interactive Mode (Draggable). Click to lock click-through.";
                BtnClickThrough.Foreground = (Brush)FindResource("BrushTextPrimary");
            }
        }
        catch
        {
            // Fallback gracefully
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isClickThrough && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
