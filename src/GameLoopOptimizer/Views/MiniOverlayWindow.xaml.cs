using System.Windows;
using System.Windows.Input;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Views;

public partial class MiniOverlayWindow : Window
{
    public MiniOverlayWindow()
    {
        InitializeComponent();
    }

    public void UpdateMetrics(PerformanceMetrics metrics)
    {
        TxtCpu.Text = $"{metrics.CpuTotalPercent:F0}%";
        TxtRam.Text = $"{metrics.RamPercent:F0}%";
        TxtEmulator.Text = $"{metrics.GameLoopRamMb:F0} MB";

        if (metrics.EstimatedFrametimeVarianceMs > 5.0 || metrics.CpuTotalPercent > 90)
        {
            TxtRadar.Text = "⚠️ Jitter";
            TxtRadar.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x52, 0x52));
            BadgeRadar.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x2B, 0xFF, 0x52, 0x52));
        }
        else if (metrics.EstimatedFrametimeVarianceMs > 2.5 || metrics.CpuTotalPercent > 75)
        {
            TxtRadar.Text = "⚡ Mild Spike";
            TxtRadar.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
            BadgeRadar.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x2B, 0xFF, 0xD7, 0x00));
        }
        else
        {
            TxtRadar.Text = "⚡ Stable";
            TxtRadar.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xE6, 0x76));
            BadgeRadar.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0x00, 0xE6, 0x76));
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
