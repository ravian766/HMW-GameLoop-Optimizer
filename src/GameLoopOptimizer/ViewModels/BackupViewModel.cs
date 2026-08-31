using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.ViewModels;

public class BackupViewModel : ViewModelBase
{
    private readonly Func<GameLoopConfig> _getGl;

    public ObservableCollection<BackupEntry> Backups { get; } = new();
    public ObservableCollection<PakBackupProfile> PakBackups { get; } = new();
    public ObservableCollection<GamePackageInfo> AvailableGamePackages { get; } = new(AdbManager.KnownGamePackages);

    private GamePackageInfo _selectedGamePackage = AdbManager.KnownGamePackages.First();
    public GamePackageInfo SelectedGamePackage
    {
        get => _selectedGamePackage;
        set => SetProperty(ref _selectedGamePackage, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _pakStatusMessage = "Ready to backup or restore in-game maps and resource packs.";
    public string PakStatusMessage
    {
        get => _pakStatusMessage;
        set => SetProperty(ref _pakStatusMessage, value);
    }

    private bool _isPakBusy;
    public bool IsPakBusy
    {
        get => _isPakBusy;
        set => SetProperty(ref _isPakBusy, value);
    }

    public string DetectedSharedFolderPath => GameResourceBackupService.GetGameLoopSharedFolderPath(_getGl());

    public string VaultLocationPath => GameResourceBackupService.PakVaultDirectory;

    public ICommand RestoreSingleCommand { get; }
    public ICommand RestoreAllCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public ICommand BackupGameResourcesCommand { get; }
    public ICommand RestorePakBackupCommand { get; }
    public ICommand DeletePakBackupCommand { get; }
    public ICommand OpenSharedFolderCommand { get; }
    public ICommand OpenPakVaultCommand { get; }
    public ICommand ImportBackupFolderCommand { get; }
    public ICommand ChangeVaultLocationCommand { get; }
    public ICommand AutoDiscoverVaultsCommand { get; }

    public event EventHandler? BackupsRestored;

    public BackupViewModel(Func<GameLoopConfig>? getGl = null)
    {
        _getGl = getGl ?? (() => GameLoopDetector.DetectGameLoop());

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

        BackupGameResourcesCommand = new AsyncRelayCommand(async () =>
        {
            IsPakBusy = true;
            string pkg = SelectedGamePackage?.PackageName ?? "com.tencent.ig";
            var prog = new Progress<string>(msg => PakStatusMessage = msg);

            try
            {
                var result = await GameResourceBackupService.BackupPaksAsync(pkg, _getGl(), prog);
                PakStatusMessage = result.Message;
                RefreshPakList();
            }
            finally
            {
                IsPakBusy = false;
            }
        });

        RestorePakBackupCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is PakBackupProfile profile)
            {
                IsPakBusy = true;
                var prog = new Progress<string>(msg => PakStatusMessage = msg);

                try
                {
                    var result = await GameResourceBackupService.RestorePaksAsync(profile, _getGl(), prog);
                    PakStatusMessage = result.Message;
                }
                finally
                {
                    IsPakBusy = false;
                }
            }
        });

        DeletePakBackupCommand = new RelayCommand(param =>
        {
            if (param is PakBackupProfile profile)
            {
                bool deleted = GameResourceBackupService.DeletePakBackup(profile.Id);
                PakStatusMessage = deleted ? $"Deleted snapshot '{profile.Title}'." : "Failed to delete snapshot.";
                RefreshPakList();
            }
        });

        OpenSharedFolderCommand = new RelayCommand(() =>
        {
            string path = DetectedSharedFolderPath;
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                PakStatusMessage = $"Could not open folder: {ex.Message}";
            }
        });

        OpenPakVaultCommand = new RelayCommand(() =>
        {
            string path = GameResourceBackupService.PakVaultDirectory;
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                PakStatusMessage = $"Could not open folder: {ex.Message}";
            }
        });

        ImportBackupFolderCommand = new AsyncRelayCommand(async () =>
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Folder Containing Game Maps & .pak Files"
                };

                if (dlg.ShowDialog() == true)
                {
                    IsPakBusy = true;
                    PakStatusMessage = $"Importing map pak resources from {dlg.FolderName}...";
                    var res = await GameResourceBackupService.ImportExistingFolderAsync(dlg.FolderName, SelectedGamePackage?.PackageName);
                    PakStatusMessage = res.Message;
                    RefreshPakList();
                }
            }
            catch (Exception ex)
            {
                PakStatusMessage = $"Import dialog error: {ex.Message}";
            }
            finally
            {
                IsPakBusy = false;
            }
        });

        ChangeVaultLocationCommand = new RelayCommand(() =>
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Dedicated Drive / Folder for Pak Vault (e.g. D:\\GameLoop_PakVault)"
                };

                if (dlg.ShowDialog() == true)
                {
                    GameResourceBackupService.SetVaultDirectory(dlg.FolderName);
                    OnPropertyChanged(nameof(VaultLocationPath));
                    RefreshPakList();
                    PakStatusMessage = $"Pak Vault location moved to: {dlg.FolderName}";
                }
            }
            catch (Exception ex)
            {
                PakStatusMessage = $"Could not change vault location: {ex.Message}";
            }
        });

        AutoDiscoverVaultsCommand = new RelayCommand(() =>
        {
            var discovered = GameResourceBackupService.DiscoverExistingVaultsOnAllDrives();
            if (discovered.Count > 0)
            {
                GameResourceBackupService.SetVaultDirectory(discovered.First());
                OnPropertyChanged(nameof(VaultLocationPath));
                RefreshPakList();
                PakStatusMessage = $"Auto-discovered and loaded Vault from {discovered.First()}!";
            }
            else
            {
                PakStatusMessage = "No existing Vaults discovered on secondary drives (D:, E:).";
            }
        });

        RefreshList();
        RefreshPakList();
        Task.Run(AutoDetectInstalledGameAsync);
    }

    public async Task AutoDetectInstalledGameAsync()
    {
        try
        {
            var gl = _getGl();
            if (!AdbManager.IsAdbAvailable(gl)) return;

            string pkgList = await AdbManager.ExecuteShellCommandAsync("pm list packages", null, 3500, gl);
            foreach (var pkg in AdbManager.KnownGamePackages)
            {
                if (pkgList.Contains(pkg.PackageName, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedGamePackage = pkg;
                    PakStatusMessage = $"Auto-detected active game: {pkg.DisplayName}";
                    break;
                }
            }
        }
        catch { }
    }

    public void RefreshList()
    {
        void UpdateAction()
        {
            Backups.Clear();
            foreach (var entry in BackupManager.GetEntries())
            {
                Backups.Add(entry);
            }
        }

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(UpdateAction);
        }
        else
        {
            UpdateAction();
        }
    }

    public void RefreshPakList()
    {
        void UpdatePakAction()
        {
            PakBackups.Clear();
            foreach (var p in GameResourceBackupService.ListPakBackups())
            {
                PakBackups.Add(p);
            }
        }

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(UpdatePakAction);
        }
        else
        {
            UpdatePakAction();
        }
    }
}
