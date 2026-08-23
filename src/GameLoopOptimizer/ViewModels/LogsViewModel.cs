using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GameLoopOptimizer.Core;

namespace GameLoopOptimizer.ViewModels;

public class LogsViewModel : ViewModelBase
{
    private readonly Dispatcher _dispatcher;
    private readonly List<LogEventArgs> _allLogs = new();

    public ObservableCollection<LogEventArgs> FilteredLogs { get; } = new();

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand ClearLogsCommand { get; }
    public ICommand CopyLogsCommand { get; }

    public LogsViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        ClearLogsCommand = new RelayCommand(() =>
        {
            Logger.Clear();
            _allLogs.Clear();
            FilteredLogs.Clear();
        });

        CopyLogsCommand = new RelayCommand(() =>
        {
            try
            {
                var text = string.Join(Environment.NewLine, FilteredLogs.Select(l => l.Formatted));
                Clipboard.SetText(text);
            }
            catch { }
        });

        Logger.LogAdded += (s, entry) =>
        {
            _dispatcher.InvokeAsync(() =>
            {
                _allLogs.Add(entry);
                if (MatchesFilter(entry))
                {
                    FilteredLogs.Add(entry);
                }
            });
        };

        foreach (var l in Logger.GetAllLogs())
        {
            _allLogs.Add(l);
            FilteredLogs.Add(l);
        }
    }

    private void ApplyFilter()
    {
        FilteredLogs.Clear();
        foreach (var entry in _allLogs)
        {
            if (MatchesFilter(entry))
            {
                FilteredLogs.Add(entry);
            }
        }
    }

    private bool MatchesFilter(LogEventArgs entry)
    {
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        return entry.Formatted.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }
}
