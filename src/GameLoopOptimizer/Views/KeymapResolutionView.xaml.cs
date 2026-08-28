using System.Windows.Controls;
using System.Windows.Input;
using GameLoopOptimizer.ViewModels;

namespace GameLoopOptimizer.Views;

public partial class KeymapResolutionView : UserControl
{
    public KeymapResolutionView()
    {
        InitializeComponent();
    }

    private void MouseCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is KeymapResolutionViewModel vm)
        {
            vm.RecordMouseSample();
        }
    }
}
