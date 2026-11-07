using System.Collections.ObjectModel;
using System.IO;
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
                    // Lobby quality always mirrors battle quality, even if a preset
                    // somehow carried a different value.
                    _activeSavLobbyQuality = value.BattleQuality;
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

    private string _emulatorCapWarning = string.Empty;
    /// <summary>
    /// Warning shown when GameLoop's own registry cap is below the tier the user selected.
    /// The emulator enforces this cap and will silently downgrade the in-game setting, which
    /// is the cause of "HDR shows Balanced". Empty string means the cap is sufficient.
    /// </summary>
    public string EmulatorCapWarning
    {
        get => _emulatorCapWarning;
        private set => SetProperty(ref _emulatorCapWarning, value);
    }

    /// <summary>
    /// Reads GameLoop's enforced graphics/FPS cap and reports whether it would downgrade the
    /// current selection.
    /// </summary>
    public void CheckEmulatorCap()
    {
        var (quality, fps) = ActiveSavService.ReadEmulatorGraphicsCaps();

        if (quality < 0)
        {
            EmulatorCapWarning = string.Empty;
            return;
        }

        int selectedPreset = ActiveSavBattleQuality;
        int requiredRegistry = PubgQualityMapping.ToRegistryQuality(selectedPreset);
        int selectedFps = PubgFpsMapping.ToFps(ActiveSavFpsLevel);

        var problems = new List<string>();

        if (quality < requiredRegistry)
        {
            problems.Add(
                $"GameLoop is set to '{PubgQualityMapping.Describe(quality + 1)}' but you chose " +
                $"'{PubgQualityMapping.Describe(selectedPreset)}'. The emulator caps the game, so it will fall back.");
        }

        if (fps > 0 && fps < selectedFps)
        {
            problems.Add($"GameLoop is limited to {fps} FPS but you chose {selectedFps} FPS. The emulator will cap it.");
        }

        EmulatorCapWarning = problems.Count > 0
            ? string.Join("  ", problems) +
              "  Fix in GameLoop: open its Settings Center, go to Game Settings > Game Quality, " +
              "set it to your chosen level, press Save, then run the sync again."
            : string.Empty;
    }

    private int _activeSavFpsLevel = 8;
    /// <summary>
    /// Battle (in-match) FPS level, 1-indexed: 5=60, 6=90, 7=120 (Legacy), 8=120 (Ultra Extreme).
    /// Changing this also updates <see cref="ActiveSavLobbyFpsLevel"/> so both stay matched.
    /// </summary>
    public int ActiveSavFpsLevel
    {
        get => _activeSavFpsLevel;
        set
        {
            if (SetProperty(ref _activeSavFpsLevel, value))
            {
                // Keep the lobby FPS in step with the battle FPS.
                if (_activeSavLobbyFpsLevel != value)
                {
                    _activeSavLobbyFpsLevel = value;
                    OnPropertyChanged(nameof(ActiveSavLobbyFpsLevel));
                }

                OnPropertyChanged(nameof(ActiveSavFpsLabel));
            }
        }
    }

    private int _activeSavBattleQuality = 1;
    /// <summary>
    /// Battle (in-match) graphics quality preset, 1-indexed: 1=Smooth .. 5=Ultra HD.
    /// Changing this also updates <see cref="ActiveSavLobbyQuality"/>, because the lobby and
    /// battle quality are expected to match. They previously drifted apart, which left the
    /// lobby on a different preset than the user selected.
    /// </summary>
    public int ActiveSavBattleQuality
    {
        get => _activeSavBattleQuality;
        set
        {
            if (SetProperty(ref _activeSavBattleQuality, value))
            {
                // Keep the lobby in step with the battle quality.
                if (_activeSavLobbyQuality != value)
                {
                    _activeSavLobbyQuality = value;
                    OnPropertyChanged(nameof(ActiveSavLobbyQuality));
                }

                OnPropertyChanged(nameof(ActiveSavQualityLabel));
            }
        }
    }

    private int _activeSavLobbyFpsLevel = 8;
    public int ActiveSavLobbyFpsLevel
    {
        get => _activeSavLobbyFpsLevel;
        set
        {
            if (SetProperty(ref _activeSavLobbyFpsLevel, value))
            {
                // Keep the battle FPS in step with the lobby FPS.
                if (_activeSavFpsLevel != value)
                {
                    _activeSavFpsLevel = value;
                    OnPropertyChanged(nameof(ActiveSavFpsLevel));
                    OnPropertyChanged(nameof(ActiveSavFpsLabel));
                }
            }
        }
    }

    private int _activeSavLobbyQuality = 1;
    /// <summary>
    /// Lobby graphics quality preset, 1-indexed. Kept in sync with
    /// <see cref="ActiveSavBattleQuality"/>; exposed so a save read can report it.
    /// </summary>
    public int ActiveSavLobbyQuality
    {
        get => _activeSavLobbyQuality;
        set
        {
            if (SetProperty(ref _activeSavLobbyQuality, value))
            {
                // Mirror back so the two never disagree.
                if (_activeSavBattleQuality != value)
                {
                    _activeSavBattleQuality = value;
                    OnPropertyChanged(nameof(ActiveSavBattleQuality));
                    OnPropertyChanged(nameof(ActiveSavQualityLabel));
                }
            }
        }
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

    private int _activeSavGraphicFavor = 0;
    /// <summary>
    /// PUBG's Graphics-screen mode selector. 0 means "leave whatever the game already has",
    /// which is the default and the correct behaviour: forcing a value (notably 4 = Customize)
    /// makes the game ignore the preset-linked quality keys and display Balanced instead.
    /// </summary>
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

    private bool _lastOperationSucceeded = true;
    /// <summary>
    /// Outcome of the most recent operation. Drives the status panel color: previously it was
    /// hardwired green, so a verification failure read as success until the user read the text.
    /// </summary>
    public bool LastOperationSucceeded
    {
        get => _lastOperationSucceeded;
        private set => SetProperty(ref _lastOperationSucceeded, value);
    }

    public ICommand SyncActiveSavCommand { get; }
    public ICommand PullActiveSavCommand { get; }
    public ICommand RestoreActiveSavCommand { get; }
    public ICommand SelectActiveSavPresetCommand { get; }

    /// <summary>Deploys the bundled save file chosen in <see cref="SelectedBundledSave"/>.</summary>
    public ICommand DeployBundledSaveCommand { get; }

    /// <summary>Re-scans the Assets folder for .sav presets without restarting the app.</summary>
    public ICommand RescanCommand { get; }

    /// <summary>Opens the Assets folder in Explorer so a .sav can be dropped in.</summary>
    public ICommand OpenAssetsFolderCommand { get; }

    /// <summary>Force-stops PUBG Mobile in the VM (remedy for the "game is running" refusal).</summary>
    public ICommand ForceStopGameCommand { get; }

    /// <summary>Launches PUBG Mobile in the VM (verification step after a deploy).</summary>
    public ICommand LaunchGameCommand { get; }

    /// <summary>The folder that DiscoverBundledSaves scans - shown in the UI so "no files found"
    /// is instantly diagnosable.</summary>
    public string BundledSaveScanPath => ActiveSavService.BundledSaveDirectory;

    private ObservableCollection<BundledSaveFile> _bundledSaves = new();
    /// <summary>
    /// Known-good save files found in the Assets folder. Populated by scanning the disk at
    /// construction, so dropping a new .sav in makes it appear without a code change.
    /// </summary>
    public ObservableCollection<BundledSaveFile> BundledSaves
    {
        get => _bundledSaves;
        private set => SetProperty(ref _bundledSaves, value);
    }

    private BundledSaveFile? _selectedBundledSave;
    public BundledSaveFile? SelectedBundledSave
    {
        get => _selectedBundledSave;
        set => SetProperty(ref _selectedBundledSave, value);
    }

    /// <summary>True when at least one bundled save file was found to deploy.</summary>
    public bool HasBundledSaves => BundledSaves.Count > 0;

    public ActiveSavViewModel(Func<GameLoopConfig> getGl, Func<DeviceProfile?> getDeviceProfile, IEventAggregator? eventAggregator = null)
    {
        _getGl = getGl;
        _getDeviceProfile = getDeviceProfile;
        _eventAggregator = eventAggregator ?? EventAggregator.Default;

        RefreshBundledSaves();

        SelectActiveSavPresetCommand = new RelayCommand(p =>
        {
            if (p is ActiveSavProfile prof)
            {
                SelectedActiveSavPreset = prof;
            }
        });

        SyncActiveSavCommand = new AsyncRelayCommand(async () =>
        {
            await SyncActiveSavAsync();
            CheckEmulatorCap();
        });
        PullActiveSavCommand = new AsyncRelayCommand(PullActiveSavAsync);
        RestoreActiveSavCommand = new AsyncRelayCommand(RestoreActiveSavAsync);

        DeployBundledSaveCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedBundledSave is null)
            {
                ActiveSavStatusMessage = "No save file selected.";
                return;
            }

            await DeployBundledSaveAsync(SelectedBundledSave);
        });

        RescanCommand = new RelayCommand(_ =>
        {
            RefreshBundledSaves();
            ActiveSavStatusMessage = BundledSaves.Count > 0
                ? $"Found {BundledSaves.Count} preset file(s) in {BundledSaveScanPath}."
                : $"No .sav files found in {BundledSaveScanPath}. Drop one there and rescan.";
        });

        OpenAssetsFolderCommand = new RelayCommand(_ =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = BundledSaveScanPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ActiveSavStatusMessage = $"Could not open '{BundledSaveScanPath}': {ex.Message}";
                Logger.Error("ActiveSavViewModel", ActiveSavStatusMessage);
            }
        });

        ForceStopGameCommand = new AsyncRelayCommand(async () =>
        {
            IsSyncingActiveSav = true;
            ActiveSavStatusMessage = "Stopping PUBG Mobile in the VM...";
            try
            {
                var res = await ActiveSavService.ForceStopPUBGAsync(_getGl());
                LastOperationSucceeded = res.Success;
                ActiveSavStatusMessage = res.Message;
            }
            finally
            {
                IsSyncingActiveSav = false;
            }
        });

        LaunchGameCommand = new AsyncRelayCommand(async () =>
        {
            IsSyncingActiveSav = true;
            ActiveSavStatusMessage = "Starting PUBG Mobile in the VM...";
            try
            {
                var res = await ActiveSavService.LaunchPUBGAsync(_getGl());
                LastOperationSucceeded = res.Success;
                ActiveSavStatusMessage = res.Message;
            }
            finally
            {
                IsSyncingActiveSav = false;
            }
        });

        CheckEmulatorCap();
    }

    /// <summary>Re-scans the Assets folder. Called at construction and by <see cref="RescanCommand"/>.</summary>
    public void RefreshBundledSaves()
    {
        string? previousName = SelectedBundledSave?.FileName;
        var found = ActiveSavService.DiscoverBundledSaves();
        BundledSaves = new ObservableCollection<BundledSaveFile>(OrderByPresetFamilyThenNewest(found));
        OnPropertyChanged(nameof(HasBundledSaves));

        // Group by preset family (file name minus the "_vN" version suffix) and order families
        // alphabetically with the newest version first, so a v3 automatically supersedes a v2 in
        // both the list and the default selection. The previous code took First() over plain
        // alphabetical order, which selected the OLDEST file ("Active_X.sav" sorts before
        // "Active_X_v2.sav").
        SelectedBundledSave =
            found.FirstOrDefault(f => f.FileName == previousName) ??   // keep the user's pick on rescan
            found.FirstOrDefault();                                   // newest version of first family
    }

    /// <summary>
    /// Parse the trailing "_v&lt;number&gt;" version marker from a bundled save file name.
    /// Returns -1 when the name carries no version marker, which sorts before any version.
    /// </summary>
    public static int BundledSaveVersion(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        int vIdx = stem.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        return vIdx > 0 && int.TryParse(stem[(vIdx + 2)..], out int vNum) ? vNum : -1;
    }

    /// <summary>
    /// Orders bundled saves: preset family (alphabetical) first, then newest version first,
    /// so the best-known revision of each preset is at the top of its group.
    /// </summary>
    public static IEnumerable<BundledSaveFile> OrderByPresetFamilyThenNewest(IEnumerable<BundledSaveFile> saves)
    {
        static string FamilyName(BundledSaveFile f)
        {
            string stem = Path.GetFileNameWithoutExtension(f.FileName);
            if (stem.StartsWith("Active_", StringComparison.OrdinalIgnoreCase)) stem = stem["Active_".Length..];
            int vIdx = stem.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
            return vIdx > 0 && int.TryParse(stem[(vIdx + 2)..], out _) ? stem[..vIdx] : stem;
        }

        return saves
            .OrderBy(f => FamilyName(f), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(f => BundledSaveVersion(f.FileName));
    }

    /// <summary>
    /// Pushes the selected bundled save to the VM and reports exactly what happened.
    /// </summary>
    public async Task DeployBundledSaveAsync(BundledSaveFile save)
    {
        IsSyncingActiveSav = true;
        ActiveSavStatusMessage = $"Deploying '{save.DisplayName}' to PUBG Mobile...";

        try
        {
            var res = await ActiveSavService.DeployBundledSaveAsync(save, _getGl());
            LastOperationSucceeded = res.Success;
            ActiveSavStatusMessage = res.Message;

            // The registry cap still governs whether the game keeps the deployed tier on exit,
            // so re-evaluate it now that the new save is on the device.
            if (res.Success)
            {
                Logger.Success("ActiveSavViewModel", res.Message);
                CheckEmulatorCap();
            }
            else
            {
                Logger.Error("ActiveSavViewModel", res.Message);
            }
        }
        catch (Exception ex)
        {
            ActiveSavStatusMessage = $"Deploy failed: {ex.Message}";
            Logger.Error("ActiveSavViewModel", ActiveSavStatusMessage);
        }
        finally
        {
            IsSyncingActiveSav = false;
        }
    }

    public async Task SyncActiveSavAsync()
    {
        await ExecuteSyncActiveSavInternalAsync();
    }

    private async Task ExecuteSyncActiveSavInternalAsync()
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
            LastOperationSucceeded = res.Success;
            ActiveSavStatusMessage = res.Message;
            if (res.Success)
            {
                _eventAggregator.Publish(new StatusNotificationMessage($"Applied {profileToApply.Name} ({ActiveSavProfile.GetQualityLabel(profileToApply.BattleQuality)}) directly to In-Game Active.sav ({devProfile.DisplayName})!"));
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
            LastOperationSucceeded = res.Success;
            ActiveSavStatusMessage = res.Message;
            if (res.Success && res.CurrentProfile != null)
            {
                ActiveSavFpsLevel = res.CurrentProfile.FpsLevel;
                ActiveSavBattleQuality = res.CurrentProfile.BattleQuality;
                ActiveSavLobbyFpsLevel = res.CurrentProfile.LobbyFpsLevel;
                ActiveSavLobbyQuality = res.CurrentProfile.LobbyQuality;
                ActiveSavStyle = res.CurrentProfile.Style;
                ActiveSavGraphicFavor = res.CurrentProfile.GraphicFavor;

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
            LastOperationSucceeded = res.Success;
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
