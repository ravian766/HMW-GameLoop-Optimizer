using System.Configuration;
using System.Data;
using System.Windows;

using GameLoopOptimizer.Core;

namespace GameLoopOptimizer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Error("AppCrash", $"Unhandled UI exception: {args.Exception.Message}\n{args.Exception.StackTrace}");
            MessageBox.Show($"An unexpected error occurred:\n{args.Exception.Message}\n\nCheck logs for details.", "GameLoop Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Logger.Error("AppDomainCrash", $"Fatal unhandled exception: {ex.Message}\n{ex.StackTrace}");
            }
        };

        ThemeManager.Instance.Initialize();
    }
}

