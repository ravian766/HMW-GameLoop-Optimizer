using System.Collections.ObjectModel;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.ViewModels;

public class ActiveSavViewModel : ViewModelBase
{
    private readonly Func<GameLoopConfig> _getGl;
    private readonly Func<DeviceProfile?> _getDeviceProfile;
    private readonly IEventAggregator _eventAggregator;

    public ObservableCollection<ActiveSavProfile> ActiveSavPresets { get; } = new(ActiveSavProfile.BuiltInPresets);

    private ActiveSavProfile _selectedActiveSavPreset = ActiveSavProfile.BuiltInPresets.First();
    public ActiveSavProfile SelectedActiveSavPreset
    {
        get => _selectedActiveSavPreset;
        set
        {
            if (SetProperty(ref _selectedActiveSavPreset, value))
            {
                if (!value.IsCustom)
                {
                    _activeSavFpsLevel = value.FpsLevel;
                    _activeSavBattleQuality = value.BattleQuality;
                    _activeSavLobbyFpsLevel = value.LobbyFpsLevel;
                    _activeSavLobbyQuality = value.LobbyQuality;
                    _activeSavStyle = value.Style;
                    _activeSavGraphicFavor = value.GraphicFavor;
                    OnPropertyChanged(nameof(ActiveSavFpsLevel));
                    OnPropertyChanged(nameof(ActiveSavBattleQuality));
                    OnPropertyChanged(nameof(ActiveSavLobbyFpsLevel));
                    OnPropertyChanged(nameof(ActiveSavLobbyQuality));
                    OnPropertyChanged(nameof(ActiveSavStyle));
                    OnPropertyChanged(nameof(ActiveSavGraphicFavor));
                }
                OnPropertyChanged(nameof(IsCustomActiveSav));
            }
        }
    }

    private int _activeSavFpsLevel = 7;
    public int ActiveSavFpsLevel
    {
        get => _activeSavFpsLevel;
        set
        {
            if (SetProperty(ref _activeSavFpsLevel, value))
            {
                OnPropertyChanged(nameof(ActiveSavFpsLabel));
            }
        }
    }

    private int _activeSavBattleQuality = 1;
    public int ActiveSavBattleQuality
    {
        get => _activeSavBattleQuality;
        set
        {
            if (SetProperty(ref _activeSavBattleQuality, value))
            {
                OnPropertyChanged(nameof(ActiveSavQualityLabel));
            }
        }
    }

    private int _activeSavLobbyFpsLevel = 7;
    public int ActiveSavLobbyFpsLevel
    {
        get => _activeSavLobbyFpsLevel;
        set => SetProperty(ref _activeSavLobbyFpsLevel, value);
    }

    private int _activeSavLobbyQuality = 1;
    public int ActiveSavLobbyQuality
    {
        get => _activeSavLobbyQuality;
        set => SetProperty(ref _activeSavLobbyQuality, value);
    }

    private int _activeSavStyle = 1;
    public int ActiveSavStyle
    {
        get => _activeSavStyle;
        set
        {
            if (SetProperty(ref _activeSavStyle, value))
            {
                OnPropertyChanged(nameof(ActiveSavStyleLabel));
            }
        }
    }

    private int _activeSavGraphicFavor = 4;
    public int ActiveSavGraphicFavor
    {
        get => _activeSavGraphicFavor;
        set => SetProperty(ref _activeSavGraphicFavor, value);
    }

    public bool IsCustomActiveSav => SelectedActiveSavPreset?.IsCustom == true;
    public string ActiveSavFpsLabel => ActiveSavProfile.GetFpsLabel(ActiveSavFpsLevel);
    public string ActiveSavQualityLabel => ActiveSavProfile.GetQualityLabel(ActiveSavBattleQuality);
    public string ActiveSavStyleLabel => ActiveSavProfile.GetStyleLabel(ActiveSavStyle);

    private string _activeSavStatusMessage = "Ready to sync UE4 in-game configuration";
    public string ActiveSavStatusMessage
    {
        get => _activeSavStatusMessage;
        set => SetProperty(ref _activeSavStatusMessage, value);
    }

    private bool _isSyncingActiveSav = false;
    public bool IsSyncingActiveSav
    {
        get => _isSyncingActiveSav;
        set => SetProperty(ref _isSyncingActiveSav, value);
    }

    public ICommand SyncActiveSavCommand { get; }
    public ICommand PullActiveSavCommand { get; }
    public ICommand RestoreActiveSavCommand { get; }
    public ICommand SelectActiveSavPresetCommand { get; }

    public ActiveSavViewModel(Func<GameLoopConfig> getGl, Func<DeviceProfile?> getDeviceProfile, IEventAggregator? eventAggregator = null)
    {
        _getGl = getGl;
        _getDeviceProfile = getDeviceProfile;
        _eventAggregator = eventAggregator ?? EventAggregator.Default;

        SelectActiveSavPresetCommand = new RelayCommand(p =>
        {
            if (p is ActiveSavProfile prof)
            {
                SelectedActiveSavPreset = prof;
            }
        });

        SyncActiveSavCommand = new AsyncRelayCommand(SyncActiveSavAsync);
        PullActiveSavCommand = new AsyncRelayCommand(PullActiveSavAsync);
        RestoreActiveSavCommand = new AsyncRelayCommand(RestoreActiveSavAsync);
    }

    public async Task SyncActiveSavAsync()
    {
        IsSyncingActiveSav = true;
        ActiveSavStatusMessage = "Injecting UE4 in-game bytecode via ADB...";
        try
        {
            var gl = _getGl();
            var profileToApply = new ActiveSavProfile
            {
                Name = SelectedActiveSavPreset.IsCustom ? "Custom In-Game Configuration" : SelectedActiveSavPreset.Name,
                FpsLevel = ActiveSavFpsLevel,
                LobbyFpsLevel = ActiveSavLobbyFpsLevel,
                BattleQuality = ActiveSavBattleQuality,
                LobbyQuality = ActiveSavLobbyQuality,
                Style = ActiveSavStyle,
                GraphicFavor = ActiveSavGraphicFavor,
                IsCustom = SelectedActiveSavPreset.IsCustom
            };

            var devProfile = _getDeviceProfile() ?? DeviceProfile.Profiles.FirstOrDefault(p => p.MaxSupportedFps >= 120) ?? DeviceProfile.Profiles.First();

            var res = await ActiveSavService.PushActiveSavProfileAsync(profileToApply, gl, devProfile);
            ActiveSavStatusMessage = res.Message;
            if (res.Success)
            {
                _eventAggregator.Publish(new StatusNotificationMessage($"Applied {profileToApply.Name} directly to In-Game Active.sav ({devProfile.DisplayName})!"));
            }
        }
        finally
        {
            IsSyncingActiveSav = false;
        }
    }

    public async Task PullActiveSavAsync()
    {
        IsSyncingActiveSav = true;
        ActiveSavStatusMessage = "Reading in-game Active.sav from VM...";
        try
        {
            var gl = _getGl();
            var res = await ActiveSavService.PullActiveSavAsync(gl);
            ActiveSavStatusMessage = res.Message;
            if (res.Success && res.CurrentProfile != null)
            {
                ActiveSavFpsLevel = res.CurrentProfile.FpsLevel;
                ActiveSavBattleQuality = res.CurrentProfile.BattleQuality;
                ActiveSavLobbyFpsLevel = res.CurrentProfile.LobbyFpsLevel;
                ActiveSavLobbyQuality = res.CurrentProfile.LobbyQuality;
                ActiveSavStyle = res.CurrentProfile.Style;
                ActiveSavGraphicFavor = res.CurrentProfile.GraphicFavor <= 0 ? 4 : res.CurrentProfile.GraphicFavor;

                var match = ActiveSavPresets.FirstOrDefault(p => !p.IsCustom && p.FpsLevel == res.CurrentProfile.FpsLevel && p.BattleQuality == res.CurrentProfile.BattleQuality && p.Style == res.CurrentProfile.Style);
                SelectedActiveSavPreset = match ?? ActiveSavPresets.First(p => p.IsCustom);
            }
        }
        finally
        {
            IsSyncingActiveSav = false;
        }
    }

    public async Task RestoreActiveSavAsync()
    {
        IsSyncingActiveSav = true;
        ActiveSavStatusMessage = "Restoring Active.sav from latest backup snapshot...";
        try
        {
            var gl = _getGl();
            var res = await ActiveSavService.RestoreLatestBackupAsync(gl);
            ActiveSavStatusMessage = res.Message;
            if (res.Success)
            {
                await PullActiveSavAsync();
            }
        }
        finally
        {
            IsSyncingActiveSav = false;
        }
    }
}
