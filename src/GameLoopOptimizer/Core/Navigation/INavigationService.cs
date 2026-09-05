using System.ComponentModel;

namespace GameLoopOptimizer.Core.Navigation;

/// <summary>
/// Service interface governing tab navigation and active view state.
/// </summary>
public interface INavigationService : INotifyPropertyChanged
{
    string ActiveTab { get; }
    object? CurrentView { get; }

    void RegisterView(string tabName, Func<object> viewResolver);
    void NavigateTo(string tabName);

    event EventHandler<string>? Navigated;
}
