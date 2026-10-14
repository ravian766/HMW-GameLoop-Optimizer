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
        // Save-file FPS levels, measured live on 2026-10-11: 8=120, 7=90, 6=60.
        Assert.Contains("120 FPS", ActiveSavProfile.GetFpsLabel(8));
        Assert.Contains("90 FPS", ActiveSavProfile.GetFpsLabel(7));
        Assert.Contains("60 FPS", ActiveSavProfile.GetFpsLabel(6));
        Assert.Contains("60 FPS", ActiveSavProfile.GetFpsLabel(5));

        Assert.Contains("Smooth", ActiveSavProfile.GetQualityLabel(1));
        Assert.Contains("Balanced", ActiveSavProfile.GetQualityLabel(2));
        Assert.Contains("HD", ActiveSavProfile.GetQualityLabel(3));
        Assert.Contains("HDR", ActiveSavProfile.GetQualityLabel(4));
        Assert.Contains("Ultra HD", ActiveSavProfile.GetQualityLabel(5));

        Assert.Contains("Classic", ActiveSavProfile.GetStyleLabel(1));
        Assert.Contains("Colorful", ActiveSavProfile.GetStyleLabel(2));
    }

    [Fact]
    public void ActiveSavProfile_Labels_AreEnglishOnly()
    {
        // The UI is English; labels must not leak Chinese text into status messages.
        string[] labels =
        {
            ActiveSavProfile.GetQualityLabel(1), ActiveSavProfile.GetQualityLabel(2),
            ActiveSavProfile.GetQualityLabel(3), ActiveSavProfile.GetQualityLabel(4),
            ActiveSavProfile.GetQualityLabel(5),
            ActiveSavProfile.GetStyleLabel(1), ActiveSavProfile.GetStyleLabel(2),
            ActiveSavProfile.GetStyleLabel(3), ActiveSavProfile.GetStyleLabel(4),
            ActiveSavProfile.GetStyleLabel(5),
            ActiveSavProfile.GetFpsLabel(1), ActiveSavProfile.GetFpsLabel(6),
            ActiveSavProfile.GetFpsLabel(7), ActiveSavProfile.GetFpsLabel(8)
        };

        foreach (string label in labels)
        {
            bool hasChinese = label.Any(c => c >= '\u4e00' && c <= '\u9fff');
            Assert.False(hasChinese, $"Label '{label}' contains Chinese characters.");
        }
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
}
