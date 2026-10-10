using System.IO;
using System.Text;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class ActiveSavTests
{
    private static void WriteUe4IntProperty(MemoryStream ms, string name, int value)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name + "\0");
        ms.Write(nameBytes, 0, nameBytes.Length);
        ms.Write(BitConverter.GetBytes(12), 0, 4); // String length of "IntProperty\0" = 12
        byte[] typeBytes = Encoding.ASCII.GetBytes("IntProperty\0");
        ms.Write(typeBytes, 0, typeBytes.Length);
        ms.Write(BitConverter.GetBytes(4), 0, 4); // Data size = 4 bytes
        ms.Write(BitConverter.GetBytes(0), 0, 4); // Array index = 0
        ms.WriteByte(0x00); // Tag byte = 0
        ms.Write(BitConverter.GetBytes(value), 0, 4); // 4-byte integer payload
    }

    [Fact]
    public void ActiveSavProfile_Presets_AreProperlyConfigured()
    {
        // Arrange
        var presets = ActiveSavProfile.BuiltInPresets;

        // Assert
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name.Contains("120 FPS") && (p.FpsLevel == 7 || p.FpsLevel == 8));
        Assert.Contains(presets, p => p.Name.Contains("90 FPS") && p.FpsLevel == 6);
        Assert.Contains(presets, p => p.IsCustom);

        foreach (var p in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.InRange(p.FpsLevel, 1, 8);
            Assert.InRange(p.BattleQuality, 1, 6);
            Assert.InRange(p.Style, 1, 5);
        }
    }

    [Fact]
    public void ActiveSavService_Ue4BinarySerializationAndPatching_ModifiesPayloadCorrectly()
    {
        // Arrange: Build authentic UE4 GVAS binary payload (legacy + PUBG 4.x keys)
        var ms = new MemoryStream();
        WriteUe4IntProperty(ms, "CrossHairColor", 4);
        // Legacy FPS keys
        WriteUe4IntProperty(ms, "FPSLevel", 6); // 90 FPS
        WriteUe4IntProperty(ms, "BattleFPS", 6);
        WriteUe4IntProperty(ms, "LobbyFPS", 6);
        WriteUe4IntProperty(ms, "MainCityFPS", 6);
        // PUBG 4.x FPS keys
        WriteUe4IntProperty(ms, "UserSetFrameRate", 6);
        WriteUe4IntProperty(ms, "ExpectedFPSLevel", 6);
        WriteUe4IntProperty(ms, "CustormFrameRateLevel", 6);
        WriteUe4IntProperty(ms, "BattleFrameRateLevel", 6);
        WriteUe4IntProperty(ms, "LobbyFrameRateLevel", 6);
        // Legacy quality/style keys
        WriteUe4IntProperty(ms, "BattleRenderStyle", 2); // Colorful
        WriteUe4IntProperty(ms, "BattleRenderQuality", 2); // Balanced
        WriteUe4IntProperty(ms, "LobbyRenderStyle", 2);
        WriteUe4IntProperty(ms, "LobbyRenderQuality", 2);
        WriteUe4IntProperty(ms, "MainCityRenderQuality", 2);
        // PUBG 4.x quality/style keys
        WriteUe4IntProperty(ms, "UserSetGraphicsQuality", 2);
        WriteUe4IntProperty(ms, "ColorMode", 2);
        WriteUe4IntProperty(ms, "UserSetColorStyle", 2);
        WriteUe4IntProperty(ms, "ShadowLevel", 0);
        WriteUe4IntProperty(ms, "AntiAliasingLevel", 1);
        // Control keys
        WriteUe4IntProperty(ms, "GraphicFavor", 1);
        WriteUe4IntProperty(ms, "OverAllQuality", 2);
        // PUBG 4.x user-set flags
        WriteUe4IntProperty(ms, "bUserHasSetQuality", 0);
        WriteUe4IntProperty(ms, "bUserHasSetFrameRate", 0);
        WriteUe4IntProperty(ms, "bIsCustomQuality", 0);

        byte[] buffer = ms.ToArray();

        // Act 1: Verify Initial Read (should pick PUBG 4.x keys first)
        var initial = ActiveSavService.ReadProfileFromBytes(buffer, "Authentic Read");
        Assert.Equal(6, initial.FpsLevel);      // UserSetFrameRate=6 (4.x key takes priority)
        Assert.Equal(6, initial.LobbyFpsLevel); // LobbyFrameRateLevel=6 (4.x key takes priority)
        Assert.Equal(2, initial.BattleQuality);  // UserSetGraphicsQuality=2 (4.x key takes priority)
        Assert.Equal(2, initial.LobbyQuality);
        Assert.Equal(2, initial.Style);          // ColorMode=2 (4.x key takes priority)
        Assert.Equal(1, initial.GraphicFavor);

        // Act 2: Apply 120 FPS Ultra-Low Latency Esports Preset (FpsLevel = 8, BattleQuality = 1, Style = 1)
        var esportsPreset = ActiveSavProfile.BuiltInPresets.First(p => p.FpsLevel == 8 && p.BattleQuality == 1);
        int patchedCount = ActiveSavService.ApplyProfileToBytes(buffer, esportsPreset);

        // Assert 2: Should patch legacy + 4.x keys + user flags + OverAllQuality
        Assert.True(patchedCount >= 15, $"Expected >= 15 patched fields (legacy + 4.x + flags), got {patchedCount}");

        // Act 3: Read back patched buffer
        var updated = ActiveSavService.ReadProfileFromBytes(buffer, "Patched Read");
        Assert.Equal(8, updated.FpsLevel); // 120 FPS (Level 8)
        Assert.Equal(8, updated.LobbyFpsLevel);
        Assert.Equal(1, updated.BattleQuality); // Smooth
        Assert.Equal(1, updated.LobbyQuality); // Smooth
        Assert.Equal(1, updated.Style); // Classic
    }

    [Fact]
    public void ActiveSavService_UltraHdrUhd_ForcesQuality6AndFps8()
    {
        // Arrange: Buffer with PUBG quality and FPS fields
        var ms = new MemoryStream();
        WriteUe4IntProperty(ms, "BattleRenderQuality", 3);
        WriteUe4IntProperty(ms, "LobbyRenderQuality", 3);
        WriteUe4IntProperty(ms, "BattleQuality", 3);
        WriteUe4IntProperty(ms, "LobbyQuality", 3);
        WriteUe4IntProperty(ms, "UserSetGraphicsQuality", 3);
        WriteUe4IntProperty(ms, "OverAllQuality", 3);
        WriteUe4IntProperty(ms, "FPSLevel", 5);
        WriteUe4IntProperty(ms, "BattleFPS", 5);
        WriteUe4IntProperty(ms, "LobbyFPS", 5);
        WriteUe4IntProperty(ms, "UserSetFrameRate", 5);
        byte[] buffer = ms.ToArray();

        // Act: Apply UHD (Level 6) + 120 FPS (Level 8)
        var uhdPreset = new ActiveSavProfile
        {
            BattleQuality = 6,
            LobbyQuality = 6,
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            Style = 2,
            GraphicFavor = 4
        };
        int patched = ActiveSavService.ApplyProfileToBytes(buffer, uhdPreset);
        Assert.True(patched >= 10);

        // Assert: Quality is 6 and FPS is 8 across both legacy and 4.x properties
        Assert.True(ActiveSavService.TryReadInt(buffer, "BattleRenderQuality", out int brq));
        Assert.Equal(6, brq);

        Assert.True(ActiveSavService.TryReadInt(buffer, "LobbyRenderQuality", out int lrq));
        Assert.Equal(6, lrq);

        Assert.True(ActiveSavService.TryReadInt(buffer, "UserSetGraphicsQuality", out int usgq));
        Assert.Equal(6, usgq);

        Assert.True(ActiveSavService.TryReadInt(buffer, "OverAllQuality", out int oaq));
        Assert.Equal(6, oaq);

        Assert.True(ActiveSavService.TryReadInt(buffer, "FPSLevel", out int fps));
        Assert.Equal(8, fps);

        Assert.True(ActiveSavService.TryReadInt(buffer, "BattleFPS", out int bFps));
        Assert.Equal(8, bFps);

        Assert.True(ActiveSavService.TryReadInt(buffer, "UserSetFrameRate", out int usFps));
        Assert.Equal(8, usFps);
    }

    [Fact]
    public void ActiveSavService_PubgV4OverAllQuality_MatchesBattleQualityPreset()
    {
        // Arrange: Build buffer with OverAllQuality preset value
        var ms = new MemoryStream();
        WriteUe4IntProperty(ms, "OverAllQuality", 3); // HD preset
        WriteUe4IntProperty(ms, "GraphicFavor", 2);    // Balanced
        byte[] buffer = ms.ToArray();

        // Act: Apply Smooth profile (BattleQuality = 1)
        var profile = new ActiveSavProfile { FpsLevel = 7, BattleQuality = 1, Style = 1, GraphicFavor = 4 };
        ActiveSavService.ApplyProfileToBytes(buffer, profile);

        // Assert: OverAllQuality must match BattleQuality (1=Smooth, 3=HD, 4=HDR) so the game UI cleanly selects the preset
        Assert.True(ActiveSavService.TryReadInt(buffer, "OverAllQuality", out int oaq));
        Assert.Equal(1, oaq);

        // Assert: GraphicFavor must be 4 (Customize)
        Assert.True(ActiveSavService.TryReadInt(buffer, "GraphicFavor", out int gf));
        Assert.Equal(4, gf);
    }

    [Fact]
    public void ActiveSavService_PubgV4UserFlags_SetCorrectly()
    {
        // Arrange: Build buffer with user flags at 0 (auto-detect mode)
        var ms = new MemoryStream();
        WriteUe4IntProperty(ms, "bUserHasSetQuality", 0);
        WriteUe4IntProperty(ms, "bUserHasSetFrameRate", 0);
        WriteUe4IntProperty(ms, "bIsCustomQuality", 0);
        WriteUe4IntProperty(ms, "FPSLevel", 5);
        byte[] buffer = ms.ToArray();

        // Act 1: Standard preset (IsCustom = false)
        var preset = new ActiveSavProfile { FpsLevel = 7, BattleQuality = 1, Style = 1, GraphicFavor = 4, IsCustom = false };
        ActiveSavService.ApplyProfileToBytes(buffer, preset);

        // Assert: User confirmation flags are 1, but bIsCustomQuality is 0 for standard presets
        Assert.True(ActiveSavService.TryReadInt(buffer, "bUserHasSetQuality", out int q));
        Assert.Equal(1, q);

        Assert.True(ActiveSavService.TryReadInt(buffer, "bUserHasSetFrameRate", out int f));
        Assert.Equal(1, f);

        Assert.True(ActiveSavService.TryReadInt(buffer, "bIsCustomQuality", out int c));
        Assert.Equal(0, c);

        // Act 2: Custom profile (IsCustom = true)
        var custom = new ActiveSavProfile { FpsLevel = 7, BattleQuality = 1, Style = 1, GraphicFavor = 4, IsCustom = true };
        ActiveSavService.ApplyProfileToBytes(buffer, custom);
        Assert.True(ActiveSavService.TryReadInt(buffer, "bIsCustomQuality", out int cCustom));
        Assert.Equal(1, cCustom);
    }

    [Fact]
    public void ActiveSavService_PubgV4FpsKeys_ReadAndPatchCorrectly()
    {
        // Arrange: Buffer with ONLY PUBG 4.x keys (simulates a pure v4.6 save file)
        var ms = new MemoryStream();
        WriteUe4IntProperty(ms, "UserSetFrameRate", 5);         // 60 FPS
        WriteUe4IntProperty(ms, "ExpectedFPSLevel", 5);
        WriteUe4IntProperty(ms, "CustormFrameRateLevel", 5);
        WriteUe4IntProperty(ms, "BattleFrameRateLevel", 5);
        WriteUe4IntProperty(ms, "LobbyFrameRateLevel", 5);
        WriteUe4IntProperty(ms, "UserSetGraphicsQuality", 3);   // HD
        WriteUe4IntProperty(ms, "ColorMode", 3);                 // Realistic
        WriteUe4IntProperty(ms, "UserSetColorStyle", 3);
        byte[] buffer = ms.ToArray();

        // Act: Read initial values
        var initial = ActiveSavService.ReadProfileFromBytes(buffer, "V4 Read");
        Assert.Equal(5, initial.FpsLevel);      // 60 FPS
        Assert.Equal(5, initial.LobbyFpsLevel); // LobbyFrameRateLevel=5
        Assert.Equal(3, initial.BattleQuality); // HD
        Assert.Equal(3, initial.Style);         // Realistic

        // Act: Patch to 120 FPS Smooth Classic
        var target = new ActiveSavProfile { FpsLevel = 7, LobbyFpsLevel = 7, BattleQuality = 1, LobbyQuality = 1, Style = 1, GraphicFavor = 4 };
        int patched = ActiveSavService.ApplyProfileToBytes(buffer, target);
        Assert.True(patched >= 8, $"Expected >= 8 patched 4.x fields, got {patched}");

        // Assert: Verify 4.x keys were patched
        Assert.True(ActiveSavService.TryReadInt(buffer, "UserSetFrameRate", out int fps));
        Assert.Equal(7, fps);

        Assert.True(ActiveSavService.TryReadInt(buffer, "LobbyFrameRateLevel", out int lfps));
        Assert.Equal(7, lfps);

        Assert.True(ActiveSavService.TryReadInt(buffer, "UserSetGraphicsQuality", out int qual));
        Assert.Equal(1, qual);

        Assert.True(ActiveSavService.TryReadInt(buffer, "ColorMode", out int style));
        Assert.Equal(1, style);
    }

    [Fact]
    public void ActiveSavService_CorruptedIntPropertyHeaders_AreSelfHealed()
    {
        // Arrange: Build authentic UE4 GVAS binary payload and corrupt IntProperty header
        var ms = new MemoryStream();
        WriteUe4IntProperty(ms, "FPSLevel", 6);
        WriteUe4IntProperty(ms, "BattleFPS", 6);
        WriteUe4IntProperty(ms, "BattleRenderQuality", 2);
        WriteUe4IntProperty(ms, "BattleRenderStyle", 1);

        byte[] buffer = ms.ToArray();

        // Simulate legacy bug: overwrite IntProperty header with int bytes
        int off = ActiveSavService.FindIntPropertyOffset(buffer, "FPSLevel");
        Assert.True(off > 0);

        // Corrupt 'Int' in IntProperty
        int intPropIndex = off - 21; // Offset of 'I'
        buffer[intPropIndex] = 0x07;
        buffer[intPropIndex + 1] = 0x00;
        buffer[intPropIndex + 2] = 0x00;

        // Act: Heal headers
        int healedCount = ActiveSavService.HealCorruptedIntPropertyHeaders(buffer);

        // Assert
        Assert.True(healedCount > 0);
        Assert.True(ActiveSavService.TryReadInt(buffer, "FPSLevel", out int fpsVal));
        Assert.Equal(6, fpsVal);
    }

    [Fact]
    public void ActiveSavService_RemotePathResolution_ContainsExpectedDirectoryStructure()
    {
        // Assert
        foreach (var pkg in ActiveSavService.SupportedPackages)
        {
            string path = ActiveSavService.GetRemotePathForPackage(pkg);
            Assert.StartsWith("/sdcard/Android/data/", path);
            Assert.Contains(pkg, path);
            Assert.EndsWith("/Saved/SaveGames/Active.sav", path);
        }
    }

    [Fact]
    public void ActiveSavProfile_LabelHelpers_ReturnFriendlyStrings()
    {
        // Assert
        Assert.Contains("120 FPS", ActiveSavProfile.GetFpsLabel(8));
        Assert.Contains("120 FPS", ActiveSavProfile.GetFpsLabel(7));
        Assert.Contains("90 FPS", ActiveSavProfile.GetFpsLabel(6));
        Assert.Contains("60 FPS", ActiveSavProfile.GetFpsLabel(5));

        Assert.Contains("Smooth", ActiveSavProfile.GetQualityLabel(1));
        Assert.Contains("Balanced", ActiveSavProfile.GetQualityLabel(2));
        Assert.Contains("HD", ActiveSavProfile.GetQualityLabel(3));
        Assert.Contains("HDR", ActiveSavProfile.GetQualityLabel(4));
        Assert.Contains("Ultra HD", ActiveSavProfile.GetQualityLabel(5));
        Assert.Contains("UHD", ActiveSavProfile.GetQualityLabel(6));

        Assert.Contains("Classic", ActiveSavProfile.GetStyleLabel(1));
        Assert.Contains("Colorful", ActiveSavProfile.GetStyleLabel(2));
    }

    [Fact]
    public void ActiveSavService_CVarEncodingAndDecoding_PreservesValues()
    {
        // Arrange
        string cvar = "r.UserQualitySetting=1";

        // Act
        string encoded = ActiveSavService.EncodeCVar(cvar);
        string decoded = ActiveSavService.DecodeCVar(encoded);

        // Assert
        Assert.StartsWith("+CVars=", encoded);
        Assert.Equal("r.UserQualitySetting=1", decoded);

        // Also test PUBG 4.x CVar roundtrip
        string v4Cvar = "r.PUBGDeviceMaxFrameRate=120";
        string v4Encoded = ActiveSavService.EncodeCVar(v4Cvar);
        string v4Decoded = ActiveSavService.DecodeCVar(v4Encoded);
        Assert.Equal(v4Cvar, v4Decoded);
    }

    [Fact]
    public void ActiveSavService_SmoothOptimizationCVars_EncodeDecodeCorrectly()
    {
        string[] smoothCvars = new[]
        {
            "r.BloomQuality=0.0",
            "r.MobileSimpleShader=1",
            "r.UserShadowSwitch=0",
            "r.SkyAtmosphere=0.0",
            "r.MaterialQualitySuperHigh=0.0",
            "Engine.GSleepTimeThod=0.0001",
            "r.Streaming.PoolSize=150"
        };

        foreach (var cvar in smoothCvars)
        {
            string enc = ActiveSavService.EncodeCVar(cvar);
            string dec = ActiveSavService.DecodeCVar(enc);
            Assert.Equal(cvar, dec);
        }
    }

    [Fact]
    public async Task ActiveSavService_LiveSync_CanInjectEsportsPreset()
    {
        var gl = new GameLoopConfig { InstallPath = @"D:\Program Files\Tencent" };
        if (!AdbManager.IsAdbAvailable(gl)) return;
        var devices = await AdbManager.GetConnectedDevicesAsync(gl);
        if (!devices.Any(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase))) return;

        var preset = ActiveSavProfile.BuiltInPresets.First(p => p.Name.Contains("Esports 120 FPS"));
        var result = await ActiveSavService.PushActiveSavProfileAsync(preset, gl);

        Assert.True(result.Success, $"Push failed: {result.Message}");
    }
}
