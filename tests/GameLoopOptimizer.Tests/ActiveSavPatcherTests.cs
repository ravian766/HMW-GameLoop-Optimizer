using System.IO;
using System.Text;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

/// <summary>
/// Regression tests for the Active.sav binary patcher.
///
/// Context (2026-10-11): the HDR / UHD graphics presets appeared to do nothing.
/// Two real defects were found by scanning a genuine PUBG 4.x Active.sav:
///
///  1. FindIntPropertyOffset matched property names as raw substrings, so
///     "LobbyQuality" resolved to "nEnhancedLobbyQuality" and "Style" resolved to
///     "BattleRenderStyle". Both pairs shared one offset, so each apply overwrote an
///     unrelated property.
///  2. The key arrays listed names that do not exist in the save file
///     (BattleQuality, LobbyQuality, ShadowLevel, AntiAliasingLevel,
///     UserSetGraphicsQuality, ColorMode, UserSetColorStyle). PatchInt returned false
///     for those and the result was discarded, so a run could report success while
///     changing nothing.
///
/// Property names below mirror the real file's identifiers.
/// </summary>
public class ActiveSavPatcherTests
{
    /// <summary>Builds a UE4 IntProperty record exactly as FindIntPropertyOffset expects.</summary>
    private static void WriteIntProperty(MemoryStream ms, string name, int value)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name + "\0");
        ms.Write(nameBytes, 0, nameBytes.Length);
        ms.Write(BitConverter.GetBytes(12), 0, 4);
        byte[] typeBytes = Encoding.ASCII.GetBytes("IntProperty\0");
        ms.Write(typeBytes, 0, typeBytes.Length);
        ms.Write(BitConverter.GetBytes(4), 0, 4);
        ms.Write(BitConverter.GetBytes(0), 0, 4);
        ms.WriteByte(0x00);
        ms.Write(BitConverter.GetBytes(value), 0, 4);
    }

    /// <summary>
    /// A save file shaped like the real one, including the two names that used to collide.
    /// </summary>
    private static byte[] BuildRealisticSave()
    {
        using var ms = new MemoryStream();
        WriteIntProperty(ms, "BattleRenderQuality", 1);
        WriteIntProperty(ms, "LobbyRenderQuality", 1);
        WriteIntProperty(ms, "MainCityRenderQuality", 1);
        WriteIntProperty(ms, "ManorRenderQuality", 1);
        WriteIntProperty(ms, "BattleRenderStyle", 1);
        WriteIntProperty(ms, "LobbyRenderStyle", 1);
        WriteIntProperty(ms, "nEnhancedLobbyQuality", 1);
        WriteIntProperty(ms, "BattleFPS", 5);
        WriteIntProperty(ms, "FPSLevel", 5);
        WriteIntProperty(ms, "LobbyFPS", 5);
        WriteIntProperty(ms, "GraphicFavor", 1);
        WriteIntProperty(ms, "GFBestQBattle", 1);
        WriteIntProperty(ms, "GFBestQLobby", 1);
        return ms.ToArray();
    }

    [Fact]
    public void FindIntPropertyOffset_DoesNotMatchInsideLongerIdentifier()
    {
        byte[] buf = BuildRealisticSave();

        // "LobbyQuality" is NOT a property in the file; it must not resolve to
        // "nEnhancedLobbyQuality".
        int offset = ActiveSavService.FindIntPropertyOffset(buf, "LobbyQuality");
        Assert.Equal(-1, offset);

        // Bare "Style" must not resolve inside "BattleRenderStyle".
        Assert.Equal(-1, ActiveSavService.FindIntPropertyOffset(buf, "Style"));
    }

    [Fact]
    public void FindIntPropertyOffset_StillFindsExactNames()
    {
        byte[] buf = BuildRealisticSave();

        foreach (string key in new[]
        {
            "BattleRenderQuality", "LobbyRenderQuality", "nEnhancedLobbyQuality",
            "BattleRenderStyle", "BattleFPS", "GraphicFavor"
        })
        {
            Assert.True(
                ActiveSavService.FindIntPropertyOffset(buf, key) >= 0,
                $"Expected to locate '{key}' in the save file.");
        }
    }

    [Fact]
    public void KeyArrays_ContainNoKeysThatResolveToTheSameOffset()
    {
        byte[] buf = BuildRealisticSave();

        var all = new List<(string Key, int Offset)>();
        foreach (string k in ActiveSavService.QualityKeys) all.Add((k, ActiveSavService.FindIntPropertyOffset(buf, k)));
        foreach (string k in ActiveSavService.FpsKeys) all.Add((k, ActiveSavService.FindIntPropertyOffset(buf, k)));
        foreach (string k in ActiveSavService.StyleKeys) all.Add((k, ActiveSavService.FindIntPropertyOffset(buf, k)));

        var resolved = all.Where(x => x.Offset >= 0).ToList();

        var duplicates = resolved
            .GroupBy(x => x.Offset)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void KeyArrays_ExcludeKnownPhantomKeys()
    {
        // These never existed in a real PUBG 4.x save and caused silent no-ops or corruption.
        foreach (string phantom in new[] { "LobbyQuality", "BattleQuality", "Style" })
        {
            Assert.DoesNotContain(phantom, ActiveSavService.QualityKeys);
            Assert.DoesNotContain(phantom, ActiveSavService.FpsKeys);
            Assert.DoesNotContain(phantom, ActiveSavService.StyleKeys);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]  // HDR
    [InlineData(5)]  // Ultra HD
    public void ApplyProfile_WritesEveryQualityTier(int battleQuality)
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = battleQuality,
            LobbyQuality = battleQuality,
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            Style = 1,
            IsCustom = false
        };

        int patched = ActiveSavService.ApplyProfileToBytes(buf, profile);

        Assert.True(patched > 0, "At least the present keys must be patched.");

        Assert.Equal(battleQuality, ReadInt(buf, "BattleRenderQuality"));
        Assert.Equal(battleQuality, ReadInt(buf, "LobbyRenderQuality"));
        Assert.Equal(battleQuality, ReadInt(buf, "MainCityRenderQuality"));
        Assert.Equal(8, ReadInt(buf, "BattleFPS"));
        Assert.Equal(8, ReadInt(buf, "FPSLevel"));
    }

    [Fact]
    public void ApplyProfile_DoesNotCorruptUnrelatedProperties()
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = 5,
            LobbyQuality = 5,
            FpsLevel = 7,
            LobbyFpsLevel = 7,
            Style = 3,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        // LobbyRenderStyle is NOT in StyleKeys' lobby-independent set in a way that would let a
        // bare "Style" match clobber BattleRenderStyle, so both must carry their own values.
        // The original bug was "Style" and "BattleRenderStyle" sharing one offset.
        Assert.Equal(3, ReadInt(buf, "BattleRenderStyle"));
        Assert.Equal(3, ReadInt(buf, "LobbyRenderStyle"));
        Assert.Equal(5, ReadInt(buf, "BattleRenderQuality"));

        // nEnhancedLobbyQuality is a lobby-quality target and is expected to follow LobbyQuality.
        Assert.Equal(5, ReadInt(buf, "nEnhancedLobbyQuality"));
    }

    [Fact]
    public void ApplyProfile_BattleRenderStyleIsNotClobberedByBareStyleKey()
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = 1,
            FpsLevel = 5,
            Style = 4,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        // Before the fix, Style resolved to the same offset as BattleRenderStyle, so these
        // two writes fought over one property. Now BattleRenderStyle is written exactly once.
        Assert.Equal(4, ReadInt(buf, "BattleRenderStyle"));
    }

    [Fact]
    public void ApplyProfile_ReportsKeysMissingFromSaveFile()
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = 4,
            FpsLevel = 8,
            Style = 1,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        // Known-absent keys must be surfaced rather than silently swallowed.
        Assert.NotEmpty(ActiveSavService.LastMissedKeys);
    }

    [Fact]
    public void ApplyProfile_GraphicFavorForcesCustomizeSoGameDoesNotOverride()
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = 4,
            FpsLevel = 8,
            Style = 1,
            GraphicFavor = 0,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        Assert.Equal(4, ReadInt(buf, "GraphicFavor"));
    }

    [Fact]
    public void ReadProfile_RoundTripsQualityAppliedByApply()
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = 4,
            LobbyQuality = 4,
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            Style = 1,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        var readBack = ActiveSavService.ReadProfileFromBytes(buf, "verify");

        Assert.Equal(4, readBack.BattleQuality);
        Assert.Equal(8, readBack.FpsLevel);
    }

    private static int ReadInt(byte[] buf, string name)
    {
        Assert.True(
            ActiveSavService.TryReadInt(buf, name, out int value),
            $"Expected to read '{name}'.");
        return value;
    }
}