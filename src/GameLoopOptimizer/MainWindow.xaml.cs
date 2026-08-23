using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.ViewModels;
using GameLoopOptimizer.Views;

namespace GameLoopOptimizer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private SystemTrayManager? _trayManager;
    private MiniOverlayWindow? _overlayWindow;
    private HotkeyManager? _hotkeyManager;
    private ContextMenu? _trayMenu;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        InitializeSystemTray();
        InitializeOverlayAndHotkeys();
    }

    private void InitializeSystemTray()
    {
        _trayManager = new SystemTrayManager(this);
        _trayManager.Initialize();

        _trayMenu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open Dashboard" };
        openItem.Click += (s, e) => ShowAndActivate();
        _trayMenu.Items.Add(openItem);

        var trimItem = new MenuItem { Header = "⚡ Quick Trim RAM" };
        trimItem.Click += (s, e) =>
        {
            int freed = ProcessManager.TrimWorkingSets();
            Logger.Success("MemoryTrim", $"Trimmed working sets across {freed} processes from System Tray.");
        };
        _trayMenu.Items.Add(trimItem);

        var hudItem = new MenuItem { Header = "🎯 Toggle In-Game HUD (Ctrl+Shift+O)" };
        hudItem.Click += (s, e) => ToggleOverlay();
        _trayMenu.Items.Add(hudItem);

        _trayMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit HMW Optimizer" };
        exitItem.Click += (s, e) =>
        {
            _isExplicitExit = true;
            Close();
        };
        _trayMenu.Items.Add(exitItem);

        _trayManager.DoubleClicked += ShowAndActivate;
        _trayManager.RightClicked += () =>
        {
            _trayMenu.IsOpen = true;
        };
    }

    private void InitializeOverlayAndHotkeys()
    {
        _overlayWindow = new MiniOverlayWindow();
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Register(this);

        _hotkeyManager.OverlayHotkeyPressed += ToggleOverlay;
        _hotkeyManager.TrimHotkeyPressed += () =>
        {
            int freed = ProcessManager.TrimWorkingSets();
            Logger.Success("MemoryTrim", $"HotKey Ctrl+Shift+M: Trimmed RAM across {freed} processes.");
        };

        _viewModel.MonitorService.MetricsUpdated += (s, metrics) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_overlayWindow != null && _overlayWindow.IsVisible)
                {
                    _overlayWindow.UpdateMetrics(metrics);
                }
            });
        };
    }

    private void ToggleOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            if (_overlayWindow == null) return;
            if (_overlayWindow.IsVisible)
            {
                _overlayWindow.Hide();
            }
            else
            {
                _overlayWindow.Show();
            }
        });
    }

    private void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            Logger.Info("App", "Minimized to system tray. Auto-Gaming Daemon remains active in background.");
        }
        else
        {
            _trayManager?.Dispose();
            _hotkeyManager?.Dispose();
            _overlayWindow?.Close();
            _viewModel.WatchdogService.Dispose();
            _viewModel.MonitorService.Dispose();
            base.OnClosing(e);
            Application.Current.Shutdown();
        }
    }
}