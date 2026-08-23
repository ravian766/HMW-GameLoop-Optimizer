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
