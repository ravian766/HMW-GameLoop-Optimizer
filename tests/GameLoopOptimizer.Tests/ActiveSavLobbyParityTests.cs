using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.ViewModels;
using Xunit;

namespace GameLoopOptimizer.Tests;

/// <summary>
/// Guards the lobby/battle parity invariant.
///
/// The lobby and the match must always show the same graphics preset and the same FPS
/// target. They previously drifted apart: two built-in presets shipped mismatched values,
/// and changing the Battle dropdown did not update the lobby at all, so the player saw a
/// different quality tier in the lobby than in-game.
/// </summary>
public class ActiveSavLobbyParityTests
{
    [Fact]
    public void ActiveSavProfile_BattleQualityChange_MirrorsToLobby()
    {
        var profile = new ActiveSavProfile { BattleQuality = 4 };

        Assert.Equal(4, profile.LobbyQuality);
    }

    [Fact]
    public void ActiveSavProfile_FpsChange_MirrorsToLobby()
    {
        var profile = new ActiveSavProfile { FpsLevel = 6 };

        Assert.Equal(6, profile.LobbyFpsLevel);
    }

    [Fact]
    public void BuiltInPresets_AllHaveMatchingLobbyAndBattleValues()
    {
        foreach (var preset in ActiveSavProfile.BuiltInPresets)
        {
            Assert.True(
                preset.BattleQuality == preset.LobbyQuality,
                $"Preset '{preset.Name}' has BattleQuality={preset.BattleQuality} but LobbyQuality={preset.LobbyQuality}.");

            Assert.True(
                preset.FpsLevel == preset.LobbyFpsLevel,
                $"Preset '{preset.Name}' has FpsLevel={preset.FpsLevel} but LobbyFpsLevel={preset.LobbyFpsLevel}.");
        }
    }

    [Fact]
    public void BuiltInPresets_QualityAndFpsAreWithinValidRange()
    {
        foreach (var preset in ActiveSavProfile.BuiltInPresets)
        {
            Assert.InRange(preset.BattleQuality, PubgQualityMapping.Smooth, PubgQualityMapping.UltraHd);
            Assert.InRange(preset.LobbyQuality, PubgQualityMapping.Smooth, PubgQualityMapping.UltraHd);
            Assert.InRange(preset.FpsLevel, 5, 8);
            Assert.InRange(preset.LobbyFpsLevel, 5, 8);
        }
    }

    private static ActiveSavViewModel CreateViewModel()
    {
        var gl = new GameLoopConfig { InstallPath = @"C:\Test\GameLoop", IsInstalled = true };
        var hw = new HardwareInfo { CpuName = "Test CPU", TotalRamGb = 16 };
        var vm = new GameLoopViewModel(() => hw, () => gl, new EventAggregator());
        return vm.ActiveSav;
    }

    [Fact]
    public void ViewModel_SettingBattleQuality_UpdatesLobby()
    {
        var av = CreateViewModel();

        av.ActiveSavBattleQuality = 4;

        Assert.Equal(4, av.ActiveSavLobbyQuality);
    }

    [Fact]
    public void ViewModel_SettingLobbyQuality_UpdatesBattle()
    {
        var av = CreateViewModel();

        av.ActiveSavLobbyQuality = 3;

        Assert.Equal(3, av.ActiveSavBattleQuality);
    }

    [Fact]
    public void ViewModel_SettingBattleFps_UpdatesLobbyFps()
    {
        var av = CreateViewModel();

        av.ActiveSavFpsLevel = 6;

        Assert.Equal(6, av.ActiveSavLobbyFpsLevel);
    }

    [Fact]
    public void ViewModel_SettingLobbyFps_UpdatesBattleFps()
    {
        var av = CreateViewModel();

        av.ActiveSavLobbyFpsLevel = 5;

        Assert.Equal(5, av.ActiveSavFpsLevel);
    }

    [Fact]
    public void ViewModel_SelectingEachPreset_KeepsLobbyAndBattleParity()
    {
        var av = CreateViewModel();

        foreach (var preset in av.ActiveSavPresets)
        {
            av.SelectedActiveSavPreset = preset;

            Assert.True(
                av.ActiveSavBattleQuality == av.ActiveSavLobbyQuality,
                $"Selecting '{preset.Name}' left battle={av.ActiveSavBattleQuality} and lobby={av.ActiveSavLobbyQuality} different.");

            Assert.True(
                av.ActiveSavFpsLevel == av.ActiveSavLobbyFpsLevel,
                $"Selecting '{preset.Name}' left battle FPS={av.ActiveSavFpsLevel} and lobby FPS={av.ActiveSavLobbyFpsLevel} different.");
        }
    }

    [Fact]
    public void ViewModel_RaisedChangeNotifications_IncludeTheLinkedProperty()
    {
        var av = CreateViewModel();

        var raised = new List<string>();
        av.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        av.ActiveSavBattleQuality = 5;

        Assert.Contains(nameof(ActiveSavViewModel.ActiveSavLobbyQuality), raised);
        Assert.Contains(nameof(ActiveSavViewModel.ActiveSavQualityLabel), raised);
    }
}