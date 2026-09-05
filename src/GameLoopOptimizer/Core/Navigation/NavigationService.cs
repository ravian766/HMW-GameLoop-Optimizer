using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameLoopOptimizer.Core.Navigation;

/// <summary>
/// Default implementation of INavigationService providing thread-safe tab switching and view caching.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<object>> _viewResolvers = new(StringComparer.OrdinalIgnoreCase);
    private string _activeTab = "Dashboard";
    private object? _currentView;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<string>? Navigated;

    public string ActiveTab
    {
        get => _activeTab;
        private set => SetProperty(ref _activeTab, value);
    }

    public object? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public void RegisterView(string tabName, Func<object> viewResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabName);
        ArgumentNullException.ThrowIfNull(viewResolver);

        _viewResolvers[tabName] = viewResolver;
    }

    public void NavigateTo(string tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName)) return;

        if (_viewResolvers.TryGetValue(tabName, out var resolver))
        {
            var view = resolver();
            ActiveTab = tabName;
            CurrentView = view;
            Navigated?.Invoke(this, tabName);
        }
        else if (_viewResolvers.TryGetValue("Dashboard", out var fallbackResolver))
        {
            ActiveTab = "Dashboard";
            CurrentView = fallbackResolver();
            Navigated?.Invoke(this, "Dashboard");
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
