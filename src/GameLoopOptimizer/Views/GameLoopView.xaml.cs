using System.Windows.Controls;
using System.Windows.Input;
using GameLoopOptimizer.ViewModels;

namespace GameLoopOptimizer.Views;

public partial class GameLoopView : UserControl
{
    public GameLoopView()
    {
        InitializeComponent();
    }

    private void MouseBenchmarkCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is GameLoopViewModel vm)
        {
            vm.RecordMouseSample();
        }
    }
}
