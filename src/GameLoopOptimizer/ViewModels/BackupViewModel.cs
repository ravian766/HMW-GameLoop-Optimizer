using System.Collections.ObjectModel;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.ViewModels;

public class BackupViewModel : ViewModelBase
{
    public ObservableCollection<BackupEntry> Backups { get; } = new();

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RestoreSingleCommand { get; }
    public ICommand RestoreAllCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public event EventHandler? BackupsRestored;

    public BackupViewModel()
    {
        BackupManager.BackupsChanged += (s, e) => RefreshList();

        RestoreSingleCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is BackupEntry entry)
            {
                bool ok = await RestoreManager.RestoreSingleAsync(entry.Id);
                StatusMessage = ok ? $"Restored '{entry.Title}'" : "Failed to restore entry.";
                RefreshList();
                BackupsRestored?.Invoke(this, EventArgs.Empty);
            }
        });

        RestoreAllCommand = new AsyncRelayCommand(async () =>
        {
            int restored = await RestoreManager.RestoreAllAsync();
            StatusMessage = $"Restored {restored} items to original values.";
            RefreshList();
            BackupsRestored?.Invoke(this, EventArgs.Empty);
        });

        ClearHistoryCommand = new RelayCommand(() =>
        {
            BackupManager.Clear();
            RefreshList();
            StatusMessage = "Backup history cleared.";
        });

        RefreshList();
    }

    public void RefreshList()
    {
        Backups.Clear();
        foreach (var entry in BackupManager.GetEntries())
        {
            Backups.Add(entry);
        }
    }
}
