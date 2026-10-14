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
    /// A save file shaped like the real one, including the two names that used to collide
    /// and a set of unrelated settings that must survive a patch untouched.
    /// The values mirror what the game actually wrote on 2026-10-11.
    /// </summary>
    private static byte[] BuildRealisticSave()
    {
        using var ms = new MemoryStream();
        // Fields this app writes
        WriteIntProperty(ms, "BattleRenderQuality", 2);   // Smooth
        WriteIntProperty(ms, "LobbyRenderQuality", 2);
        WriteIntProperty(ms, "BattleRenderStyle", 2);
        WriteIntProperty(ms, "LobbyRenderStyle", 2);
        WriteIntProperty(ms, "BattleFPS", 8);             // 120 FPS
        WriteIntProperty(ms, "LobbyFPS", 7);
        WriteIntProperty(ms, "GraphicFavor", 2);

        // Fields the game manages per-scene - must never be touched
        WriteIntProperty(ms, "MainCityRenderQuality", 4);
        WriteIntProperty(ms, "ManorRenderQuality", 3);
        WriteIntProperty(ms, "nEnhancedLobbyQuality", 2);
        WriteIntProperty(ms, "GFBestQBattle", 4);
        WriteIntProperty(ms, "GFBestQLobby", 4);
        WriteIntProperty(ms, "FPSLevel", 4);
        WriteIntProperty(ms, "MainCityFPS", 4);

        // Unrelated player settings
        WriteIntProperty(ms, "CameraLensSensibility", 4);
        WriteIntProperty(ms, "AutoPickMeleeType", 2);
        WriteIntProperty(ms, "SoundVisualizationType", 3);
        WriteIntProperty(ms, "VehicleControlMode", 2);
        WriteIntProperty(ms, "CrossHairColor", 4);

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

        // The engine ladder was measured live on 2026-10-11 and starts at 2, not 0 or 1.
        int expectedEngine = PubgQualityMapping.ToSaveQuality(battleQuality);

        Assert.Equal(expectedEngine, ReadInt(buf, "BattleRenderQuality"));
        Assert.Equal(expectedEngine, ReadInt(buf, "LobbyRenderQuality"));
        Assert.Equal(8, ReadInt(buf, "BattleFPS"));
        Assert.Equal(8, ReadInt(buf, "LobbyFPS"));

        // Per-scene fields are managed by the game and must not be written.
        Assert.Equal(4, ReadInt(buf, "MainCityRenderQuality"));
        Assert.Equal(3, ReadInt(buf, "ManorRenderQuality"));
    }

    [Theory]
    [InlineData(1, 2)]  // Smooth -> 2
    [InlineData(2, 3)]  // Balanced -> 3
    [InlineData(3, 4)]  // HD -> 4
    [InlineData(4, 5)]  // HDR -> 5
    [InlineData(5, 6)]  // Ultra HD -> 6
    public void ApplyProfile_WritesCorrectMeasuredEngineValue(int preset, int expected)
    {
        byte[] buf = BuildRealisticSave();

        var profile = new ActiveSavProfile
        {
            BattleQuality = preset,
            LobbyQuality = preset,
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            Style = 1,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        // This is the regression that caused "I picked HDR, the game showed Balanced".
        Assert.Equal(expected, ReadInt(buf, "BattleRenderQuality"));
    }

    [Fact]
    public void ApplyProfile_WritesGraphicFavorPresetValue()
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

        // The game writes 4 whenever a preset is selected; the app must match it.
        Assert.Equal(PubgQualityMapping.GraphicFavorPreset, ReadInt(buf, "GraphicFavor"));
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

        // Both style fields are written from the single chosen style.
        Assert.Equal(3, ReadInt(buf, "BattleRenderStyle"));
        Assert.Equal(3, ReadInt(buf, "LobbyRenderStyle"));

        // Ultra HD is engine value 6 (measured ladder).
        Assert.Equal(6, ReadInt(buf, "BattleRenderQuality"));
        Assert.Equal(6, ReadInt(buf, "LobbyRenderQuality"));

        // Per-scene fields the game owns must be left exactly as they were.
        Assert.Equal(2, ReadInt(buf, "nEnhancedLobbyQuality"));
        Assert.Equal(3, ReadInt(buf, "ManorRenderQuality"));
        Assert.Equal(4, ReadInt(buf, "MainCityRenderQuality"));
        Assert.Equal(4, ReadInt(buf, "GFBestQBattle"));
        Assert.Equal(4, ReadInt(buf, "GFBestQLobby"));
        Assert.Equal(4, ReadInt(buf, "FPSLevel"));
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
    public void ApplyProfile_ReportsWhenARequiredFieldIsAbsent()
    {
        // A save missing one of the required fields must be reported, not silently skipped.
        using var ms = new MemoryStream();
        WriteIntProperty(ms, "BattleRenderQuality", 2);
        WriteIntProperty(ms, "BattleFPS", 8);
        // deliberately no LobbyRenderQuality / LobbyFPS / GraphicFavor
        byte[] buf = ms.ToArray();

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

        Assert.Contains("LobbyRenderQuality", ActiveSavService.LastMissedKeys);
        Assert.Contains("LobbyFPS", ActiveSavService.LastMissedKeys);
        Assert.Contains("GraphicFavor", ActiveSavService.LastMissedKeys);
    }

    [Fact]
    public void ApplyProfile_ReportsNoMissedKeysOnACompleteSave()
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

        Assert.Empty(ActiveSavService.LastMissedKeys);
    }

    [Fact]
    public void ApplyProfile_AlwaysWritesGraphicFavorPresetValue()
    {
        byte[] buf = BuildRealisticSave();

        // The fixture starts at 2 (the game's own value in the captured save).
        Assert.Equal(2, ReadInt(buf, "GraphicFavor"));

        var profile = new ActiveSavProfile
        {
            BattleQuality = 4,
            FpsLevel = 8,
            Style = 1,
            GraphicFavor = 0,   // ignored; the app always writes the measured preset value
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        // Measured live: selecting any preset in-game made the game write 4 here, so the app
        // must match. An earlier version deliberately skipped this field, which left the save
        // in a stale state the game then overrode.
        Assert.Equal(PubgQualityMapping.GraphicFavorPreset, ReadInt(buf, "GraphicFavor"));
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

        // Reading must convert the 0-indexed engine value back to the 1-indexed UI preset.
        Assert.Equal(4, readBack.BattleQuality);
        Assert.Equal(8, readBack.FpsLevel);
    }

    [Theory]
    [InlineData(1, 2)]   // Smooth -> 2   (measured live 2026-10-11)
    [InlineData(2, 3)]   // Balanced -> 3 (measured live)
    [InlineData(3, 4)]   // HD -> 4       (measured live)
    [InlineData(4, 5)]   // HDR -> 5
    [InlineData(5, 6)]   // Ultra HD -> 6
    public void ToEngineQuality_UsesTheMeasuredLadder(int preset, int expected)
    {
        // Measured against the running game, NOT derived from documentation. The commonly
        // published 1-indexed mapping is off by one for this build, which is what made HDR
        // apply as Balanced.
        Assert.Equal(expected, PubgQualityMapping.ToSaveQuality(preset));
    }

    [Theory]
    [InlineData(2, 1)]   // stored 2 -> Smooth
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    [InlineData(6, 5)]
    public void FromEngineQuality_RoundTripsTheMeasuredLadder(int engine, int preset)
    {
        Assert.Equal(preset, PubgQualityMapping.FromSaveQuality(engine));
    }

    [Fact]
    public void EngineLadder_RoundTripsForEveryTier()
    {
        for (int preset = PubgQualityMapping.MinPreset; preset <= PubgQualityMapping.MaxPreset; preset++)
        {
            int engine = PubgQualityMapping.ToSaveQuality(preset);
            Assert.Equal(preset, PubgQualityMapping.FromSaveQuality(engine));
            Assert.True(PubgQualityMapping.IsKnownSaveValue(engine));
        }
    }

    [Fact]
    public void ToEngineQuality_ClampsOutOfRangePresets()
    {
        Assert.Equal(2, PubgQualityMapping.ToSaveQuality(0));
        Assert.Equal(2, PubgQualityMapping.ToSaveQuality(-5));
        Assert.Equal(6, PubgQualityMapping.ToSaveQuality(99));
    }

    [Fact]
    public void MeasuredTierValues_AreAllDistinct()
    {
        var seen = new HashSet<int>();
        for (int preset = PubgQualityMapping.MinPreset; preset <= PubgQualityMapping.MaxPreset; preset++)
        {
            Assert.True(seen.Add(PubgQualityMapping.ToSaveQuality(preset)),
                "Each quality tier must map to a distinct engine value.");
        }
    }

    private static int ReadInt(byte[] buf, string name)
    {
        Assert.True(
            ActiveSavService.TryReadInt(buf, name, out int value),
            $"Expected to read '{name}'.");
        return value;
    }

    [Fact]
    public void LooksLikePubgSave_AcceptsARealSave()
    {
        // Every real capture from 2026-10-11 had both fields, so a real save must pass.
        // Pad to a realistic size because the guard also rejects implausibly small files.
        Assert.True(ActiveSavService.LooksLikePubgSave(BuildRealisticSavePadded()));
    }

    /// <summary>
    /// The realistic save padded past the minimum-size threshold used by the guard.
    /// </summary>
    private static byte[] BuildRealisticSavePadded()
    {
        var small = BuildRealisticSave();
        var padded = new byte[4096];
        small.CopyTo(padded, 0);
        // Fill the tail with non-zero filler so it cannot be mistaken for an empty buffer.
        for (int i = small.Length; i < padded.Length; i++) padded[i] = (byte)(i % 251);
        return padded;
    }

    [Fact]
    public void LooksLikePubgSave_RejectsTruncatedOrForeignFiles()
    {
        // Regression guard. A stale 18,599-byte leftover was patched and pushed over the real
        // 15,387-byte save because nothing validated the file first.
        Assert.False(ActiveSavService.LooksLikePubgSave(Array.Empty<byte>()));
        Assert.False(ActiveSavService.LooksLikePubgSave(new byte[512]));

        // A padded file with no graphics fields at all.
        using var ms = new MemoryStream();
        WriteIntProperty(ms, "SomeOtherGameProperty", 1);
        var foreign = new byte[4096];
        var head = ms.ToArray();
        head.CopyTo(foreign, 0);
        for (int i = head.Length; i < foreign.Length; i++) foreign[i] = (byte)(i % 251);
        Assert.False(ActiveSavService.LooksLikePubgSave(foreign));
    }

    [Fact]
    public void LooksLikePubgSave_RejectsFileMissingEitherRequiredField()
    {
        byte[] Pad(byte[] head)
        {
            var padded = new byte[4096];
            head.CopyTo(padded, 0);
            for (int i = head.Length; i < padded.Length; i++) padded[i] = (byte)(i % 251);
            return padded;
        }

        using var onlyQuality = new MemoryStream();
        WriteIntProperty(onlyQuality, "BattleRenderQuality", 4);
        Assert.False(ActiveSavService.LooksLikePubgSave(Pad(onlyQuality.ToArray())));

        using var onlyFps = new MemoryStream();
        WriteIntProperty(onlyFps, "BattleFPS", 8);
        Assert.False(ActiveSavService.LooksLikePubgSave(Pad(onlyFps.ToArray())));
    }

    /// <summary>
    /// Fields the game manages per-scene and that this app must NOT touch.
    /// Verified by a before/after diff of real saves taken while changing only the
    /// in-game Graphics preset (2026-10-11).
    /// </summary>
    public static TheoryData<string> UnrelatedFields => new()
    {
        "ManorRenderQuality",
        "MainCityRenderQuality",
        "nEnhancedLobbyQuality",
        "GFBestQBattle",
        "GFBestQLobby",
        "FPSLevel",
        "MainCityFPS",
        "CameraLensSensibility",
        "AutoPickMeleeType",
        "SoundVisualizationType",
        "VehicleControlMode",
        "CrossHairColor"
    };

    [Theory]
    [MemberData(nameof(UnrelatedFields))]
    public void ApplyProfile_LeavesUnrelatedSettingsUntouched(string field)
    {
        byte[] original = BuildRealisticSave();
        byte[] buf = (byte[])original.Clone();

        int before = ReadInt(buf, field);

        var profile = new ActiveSavProfile
        {
            BattleQuality = 4,   // HDR
            LobbyQuality = 4,
            FpsLevel = 8,       // 120 FPS
            LobbyFpsLevel = 8,
            Style = 3,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        Assert.Equal(before, ReadInt(buf, field));
    }

    [Fact]
    public void ApplyProfile_ChangesOnlyTheVerifiedWriteSet()
    {
        byte[] original = BuildRealisticSave();
        byte[] buf = (byte[])original.Clone();

        var profile = new ActiveSavProfile
        {
            BattleQuality = 5,   // Ultra HD
            LobbyQuality = 5,
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            Style = 2,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        // The three fields the game itself changes, plus the two FPS fields.
        Assert.Equal(6, ReadInt(buf, "BattleRenderQuality"));  // Ultra HD = 6
        Assert.Equal(6, ReadInt(buf, "LobbyRenderQuality"));
        Assert.Equal(PubgQualityMapping.GraphicFavorPreset, ReadInt(buf, "GraphicFavor"));
        Assert.Equal(8, ReadInt(buf, "BattleFPS"));
        Assert.Equal(8, ReadInt(buf, "LobbyFPS"));

        // Everything outside the write set must be untouched. A raw byte-diff is not used
        // because small int32 payloads share identical high-order bytes, which makes the
        // count misleading. Instead the file length and every non-write field are compared.
        Assert.Equal(original.Length, buf.Length);

        foreach (string field in new[]
        {
            "ManorRenderQuality", "MainCityRenderQuality", "nEnhancedLobbyQuality",
            "GFBestQBattle", "GFBestQLobby", "FPSLevel", "MainCityFPS",
            "CameraLensSensibility", "AutoPickMeleeType", "SoundVisualizationType",
            "VehicleControlMode", "CrossHairColor"
        })
        {
            int before = ReadInt(original, field);
            int after = ReadInt(buf, field);
            Assert.True(before == after, $"'{field}' changed from {before} to {after}.");
        }
    }

    [Fact]
    public void ApplyProfile_WritesTheSameValueToBattleAndLobby()
    {
        byte[] buf = BuildRealisticSave();

        foreach (int preset in new[] { 1, 2, 3, 4, 5 })
        {
            var profile = new ActiveSavProfile
            {
                BattleQuality = preset,
                LobbyQuality = preset,
                FpsLevel = 8,
                LobbyFpsLevel = 8,
                Style = 1,
                IsCustom = false
            };

            ActiveSavService.ApplyProfileToBytes(buf, profile);

            // The real save always carries these in lockstep; never let them diverge.
            Assert.Equal(ReadInt(buf, "BattleRenderQuality"), ReadInt(buf, "LobbyRenderQuality"));
            Assert.Equal(ReadInt(buf, "BattleFPS"), ReadInt(buf, "LobbyFPS"));
        }
    }

    [Theory]
    [InlineData(8, 120)]
    [InlineData(7, 90)]
    [InlineData(6, 60)]
    public void FpsMapping_SupportsTheThreeVerifiedRates(int level, int fps)
    {
        Assert.Equal(fps, PubgFpsMapping.ToFps(level));
        Assert.Equal(level, PubgFpsMapping.ToSaveLevel(fps));
    }

    [Fact]
    public void FpsMapping_OnlyExposesThreeRates()
    {
        // SupportedFrameRates holds FPS values; the save file stores them as levels 6/7/8.
        Assert.Equal(3, PubgFpsMapping.SupportedFrameRates.Count);
        Assert.Contains(60, PubgFpsMapping.SupportedFrameRates);
        Assert.Contains(90, PubgFpsMapping.SupportedFrameRates);
        Assert.Contains(120, PubgFpsMapping.SupportedFrameRates);

        foreach (int fps in PubgFpsMapping.SupportedFrameRates)
        {
            Assert.True(PubgFpsMapping.IsKnownSaveLevel(PubgFpsMapping.ToSaveLevel(fps)));
        }
    }

    [Fact]
    public void QualityScales_DifferByExactlyTwo()
    {
        // The registry and save-file ladders are offset by two. This is the relationship that
        // made the two settings disagree and silently capped the in-game quality.
        for (int preset = PubgQualityMapping.MinPreset; preset <= PubgQualityMapping.MaxPreset; preset++)
        {
            int registry = PubgQualityMapping.ToRegistryQuality(preset);
            int save = PubgQualityMapping.ToSaveQuality(preset);

            Assert.Equal(2, save - registry);
            Assert.Equal(preset, PubgQualityMapping.FromRegistryQuality(registry));
            Assert.Equal(preset, PubgQualityMapping.FromSaveQuality(save));
        }
    }

    [Theory]
    [InlineData(1, 0, 2)]   // Smooth
    [InlineData(2, 1, 3)]   // Balanced
    [InlineData(3, 2, 4)]   // HD
    [InlineData(4, 3, 5)]   // HDR
    [InlineData(5, 4, 6)]   // Ultra HD
    public void QualityScales_MatchTheVerifiedLadders(int preset, int registry, int save)
    {
        Assert.Equal(registry, PubgQualityMapping.ToRegistryQuality(preset));
        Assert.Equal(save, PubgQualityMapping.ToSaveQuality(preset));
    }

    [Fact]
    public void RegistryAndSaveValues_AreClampedToValidRanges()
    {
        Assert.Equal(0, PubgQualityMapping.ToRegistryQuality(0));
        Assert.Equal(4, PubgQualityMapping.ToRegistryQuality(99));
        Assert.Equal(2, PubgQualityMapping.ToSaveQuality(0));
        Assert.Equal(6, PubgQualityMapping.ToSaveQuality(99));
    }

    [Fact]
    public void HighDynamicRange_TiersAreHdrAndAbove()
    {
        Assert.False(PubgQualityMapping.IsHighDynamicRange(PubgQualityMapping.Smooth));
        Assert.False(PubgQualityMapping.IsHighDynamicRange(PubgQualityMapping.Balanced));
        Assert.False(PubgQualityMapping.IsHighDynamicRange(PubgQualityMapping.Hd));
        Assert.True(PubgQualityMapping.IsHighDynamicRange(PubgQualityMapping.Hdr));
        Assert.True(PubgQualityMapping.IsHighDynamicRange(PubgQualityMapping.UltraHd));
    }

    [Theory]
    [InlineData(120, 8)]
    [InlineData(90, 7)]
    [InlineData(60, 6)]
    public void ApplyProfile_WritesTheRequestedFrameRate(int fps, int expectedLevel)
    {
        byte[] buf = BuildRealisticSave();

        int level = PubgFpsMapping.ToSaveLevel(fps);
        var profile = new ActiveSavProfile
        {
            BattleQuality = 3,
            LobbyQuality = 3,
            FpsLevel = level,
            LobbyFpsLevel = level,
            Style = 1,
            IsCustom = false
        };

        ActiveSavService.ApplyProfileToBytes(buf, profile);

        Assert.Equal(expectedLevel, ReadInt(buf, "BattleFPS"));
        Assert.Equal(expectedLevel, ReadInt(buf, "LobbyFPS"));
    }

    [Fact]
    public void BuiltInPresets_UseOnlySupportedFpsRates()
    {
        foreach (var preset in ActiveSavProfile.BuiltInPresets)
        {
            // Preset levels must be one of the three known save levels (6/7/8).
            Assert.True(
                PubgFpsMapping.IsKnownSaveLevel(preset.FpsLevel),
                $"Preset '{preset.Name}' uses FPS level {preset.FpsLevel}, which is outside the supported 6/7/8 range.");

            Assert.True(
                PubgFpsMapping.IsKnownSaveLevel(preset.LobbyFpsLevel),
                $"Preset '{preset.Name}' uses lobby FPS level {preset.LobbyFpsLevel}, which is outside the supported 6/7/8 range.");
        }
    }
}