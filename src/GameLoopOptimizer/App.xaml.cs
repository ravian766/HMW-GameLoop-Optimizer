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
        ThemeManager.Instance.Initialize();
    }
}

