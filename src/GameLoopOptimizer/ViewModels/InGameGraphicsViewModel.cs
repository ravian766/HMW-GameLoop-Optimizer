using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.ViewModels;

/// <summary>
/// ViewModel for the dedicated "In-Game Graphics &amp; Frame Pacing" tab.
///
/// It deliberately does NOT duplicate state. Every Active.sav / ADB-bridge member is
/// delegated to <see cref="GameLoopViewModel"/>, which already owns the
/// <c>ActiveSav</c> and <c>AdbStudio</c> sub-view models. Its only real job is to provide a
/// distinct ViewModel type, because WPF resolves ViewModel -&gt; View through a DataTemplate
/// and <see cref="GameLoopViewModel"/> is already mapped to GameLoopView.
/// </summary>
public class InGameGraphicsViewModel : ViewModelBase
{
    private readonly GameLoopViewModel _gameLoop;

    public InGameGraphicsViewModel(GameLoopViewModel gameLoop)
    {
        _gameLoop = gameLoop ?? throw new ArgumentNullException(nameof(gameLoop));

        // Keep this VM in step with the delegated ADB-bridge state.
        _gameLoop.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameLoopViewModel.IsAdbBridgeReady)
                or nameof(GameLoopViewModel.AdbGateVisibility)
                or nameof(GameLoopViewModel.AdbBridgeStatus)
                or nameof(GameLoopViewModel.AdbBridgeDetail))
            {
                RaiseAdbState();
            }
        };

        // Forward change notifications from the delegated Active.sav VM for the bundled-save
        // members, so the preset list and the "no files found" banner stay in sync if the
        // Assets folder is rescanned while the app is running.
        _gameLoop.ActiveSav.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ActiveSavViewModel.BundledSaves)
                or nameof(ActiveSavViewModel.SelectedBundledSave)
                or nameof(ActiveSavViewModel.HasBundledSaves))
            {
                OnPropertyChanged(nameof(BundledSaves));
                OnPropertyChanged(nameof(SelectedBundledSave));
                OnPropertyChanged(nameof(HasBundledSaves));
            }
        };
    }

    // ── ADB gate ──
    public bool IsAdbBridgeReady => _gameLoop.IsAdbBridgeReady;
    public Visibility AdbGateVisibility => _gameLoop.AdbGateVisibility;
    public string AdbBridgeStatus => _gameLoop.AdbBridgeStatus;
    public string AdbBridgeDetail => _gameLoop.AdbBridgeDetail;

    // ── Delegated Active.sav members ──
    // These remain raw ints because the ComboBoxes bind to them through SelectedValue.
    public ObservableCollection<ActiveSavProfile> ActiveSavPresets => _gameLoop.ActiveSav.ActiveSavPresets;

    public ActiveSavProfile? SelectedActiveSavPreset
    {
        get => _gameLoop.ActiveSav.SelectedActiveSavPreset;
        set
        {
            if (value != null) _gameLoop.ActiveSav.SelectedActiveSavPreset = value;
            OnPropertyChanged();
        }
    }

    public string ActiveSavStatusMessage => _gameLoop.ActiveSav.ActiveSavStatusMessage;
    public string ActiveSavFpsLabel => _gameLoop.ActiveSav.ActiveSavFpsLabel;
    public string ActiveSavQualityLabel => _gameLoop.ActiveSav.ActiveSavQualityLabel;
    public string ActiveSavStyleLabel => _gameLoop.ActiveSav.ActiveSavStyleLabel;
    public bool IsCustomActiveSav => _gameLoop.ActiveSav.IsCustomActiveSav;
    public bool IsSyncingActiveSav => _gameLoop.ActiveSav.IsSyncingActiveSav;

    /// <summary>Warning shown when GameLoop's registry cap is below the selected tier.</summary>
    public string EmulatorCapWarning
    {
        get => _gameLoop.ActiveSav.EmulatorCapWarning;
        set => SetProperty(ref _emulatorCapWarning, value);
    }

    private string _emulatorCapWarning = string.Empty;

    public int ActiveSavFpsLevel
    {
        get => _gameLoop.ActiveSav.ActiveSavFpsLevel;
        set { _gameLoop.ActiveSav.ActiveSavFpsLevel = value; OnPropertyChanged(); }
    }

    public int ActiveSavLobbyFpsLevel
    {
        get => _gameLoop.ActiveSav.ActiveSavLobbyFpsLevel;
        set { _gameLoop.ActiveSav.ActiveSavLobbyFpsLevel = value; OnPropertyChanged(); }
    }

    public int ActiveSavBattleQuality
    {
        get => _gameLoop.ActiveSav.ActiveSavBattleQuality;
        set { _gameLoop.ActiveSav.ActiveSavBattleQuality = value; OnPropertyChanged(); }
    }

    public int ActiveSavLobbyQuality
    {
        get => _gameLoop.ActiveSav.ActiveSavLobbyQuality;
        set { _gameLoop.ActiveSav.ActiveSavLobbyQuality = value; OnPropertyChanged(); }
    }

    public int ActiveSavStyle
    {
        get => _gameLoop.ActiveSav.ActiveSavStyle;
        set { _gameLoop.ActiveSav.ActiveSavStyle = value; OnPropertyChanged(); }
    }

    public int ActiveSavGraphicFavor
    {
        get => _gameLoop.ActiveSav.ActiveSavGraphicFavor;
        set { _gameLoop.ActiveSav.ActiveSavGraphicFavor = value; OnPropertyChanged(); }
    }

    // ── Delegated commands ──
    public ICommand SyncActiveSavCommand => _gameLoop.SyncActiveSavCommand;
    public ICommand PullActiveSavCommand => _gameLoop.PullActiveSavCommand;
    public ICommand RestoreActiveSavCommand => _gameLoop.RestoreActiveSavCommand;
    public ICommand SelectActiveSavPresetCommand => _gameLoop.SelectActiveSavPresetCommand;

    // ── Bundled save deployment ──
    //
    // These must be delegated explicitly. The XAML for this tab binds to
    // InGameGraphicsViewModel, not to ActiveSavViewModel, so anything added to the latter is
    // invisible here unless it is forwarded. That is exactly how the preset dropdown came up
    // empty while the files were sitting in Assets\ - the bindings resolved to nothing and WPF
    // failed silently.

    public ObservableCollection<BundledSaveFile> BundledSaves => _gameLoop.ActiveSav.BundledSaves;

    public BundledSaveFile? SelectedBundledSave
    {
        get => _gameLoop.ActiveSav.SelectedBundledSave;
        set
        {
            _gameLoop.ActiveSav.SelectedBundledSave = value;
            OnPropertyChanged();
        }
    }

    public bool HasBundledSaves => _gameLoop.ActiveSav.HasBundledSaves;

    public ICommand DeployBundledSaveCommand => _gameLoop.ActiveSav.DeployBundledSaveCommand;

    /// <summary>Re-reads the ADB bridge state and re-raises every delegated property. Called when the
    /// tab becomes visible so the gate banner reflects the current connection.
    /// </summary>
    public void Refresh()
    {
        _gameLoop.RefreshAdbBridgeState();
        RaiseAll();
    }

    private void RaiseAdbState()
    {
        OnPropertyChanged(nameof(IsAdbBridgeReady));
        OnPropertyChanged(nameof(AdbGateVisibility));
        OnPropertyChanged(nameof(AdbBridgeStatus));
        OnPropertyChanged(nameof(AdbBridgeDetail));
    }

    private void RaiseAll()
    {
        RaiseAdbState();
        OnPropertyChanged(nameof(ActiveSavPresets));
        OnPropertyChanged(nameof(SelectedActiveSavPreset));
        OnPropertyChanged(nameof(ActiveSavStatusMessage));
        OnPropertyChanged(nameof(ActiveSavFpsLabel));
        OnPropertyChanged(nameof(ActiveSavQualityLabel));
        OnPropertyChanged(nameof(ActiveSavStyleLabel));
        OnPropertyChanged(nameof(IsCustomActiveSav));
        OnPropertyChanged(nameof(IsSyncingActiveSav));
        OnPropertyChanged(nameof(ActiveSavFpsLevel));
        OnPropertyChanged(nameof(ActiveSavLobbyFpsLevel));
        OnPropertyChanged(nameof(ActiveSavBattleQuality));
        OnPropertyChanged(nameof(ActiveSavLobbyQuality));
        OnPropertyChanged(nameof(ActiveSavStyle));
        OnPropertyChanged(nameof(ActiveSavGraphicFavor));
        OnPropertyChanged(nameof(EmulatorCapWarning));
        OnPropertyChanged(nameof(BundledSaves));
        OnPropertyChanged(nameof(SelectedBundledSave));
        OnPropertyChanged(nameof(HasBundledSaves));
    }
}