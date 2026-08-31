using System.Text;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class ActiveSavTests
{
    [Fact]
    public void ActiveSavProfile_Presets_AreProperlyConfigured()
    {
        // Arrange
        var presets = ActiveSavProfile.BuiltInPresets;

        // Assert
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name.Contains("120 FPS"));
        Assert.Contains(presets, p => p.Name.Contains("90 FPS"));
        Assert.Contains(presets, p => p.IsCustom);

        foreach (var p in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.InRange(p.FpsLevel, 1, 7);
            Assert.InRange(p.BattleQuality, 1, 5);
            Assert.InRange(p.Style, 1, 5);
        }
    }

    [Fact]
    public void ActiveSavService_BinaryByteSearchingAndPatching_ModifiesPayloadCorrectly()
    {
        // Arrange: Build a synthetic UE4 binary payload
        var ms = new MemoryStream();
        void WriteProperty(string name, int value)
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            ms.Write(nameBytes, 0, nameBytes.Length);
            ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, 4); // 4-byte padding/tag header
            ms.Write(BitConverter.GetBytes(value), 0, 4); // 4-byte integer payload
            ms.Write(new byte[] { 0xFF, 0xEE, 0xDD }, 0, 3); // Trailing data
        }

        WriteProperty("FPSLevel", 5); // 60 FPS
        WriteProperty("BattleFPS", 5);
        WriteProperty("LobbyFPS", 4); // 40 FPS
        WriteProperty("BattleQuality", 2); // Balanced
        WriteProperty("LobbyQuality", 2);
        WriteProperty("Style", 2); // Colorful
        WriteProperty("GraphicFavor", 1);

        byte[] buffer = ms.ToArray();

        // Act 1: Verify Initial Read
        var initial = ActiveSavService.ReadProfileFromBytes(buffer, "Synthetic Read");
        Assert.Equal(5, initial.FpsLevel);
        Assert.Equal(4, initial.LobbyFpsLevel);
        Assert.Equal(2, initial.BattleQuality);
        Assert.Equal(2, initial.Style);

        // Act 2: Apply 120 FPS Ultra-Low Latency Esports Preset
        var esportsPreset = ActiveSavProfile.BuiltInPresets.First(p => p.FpsLevel == 7 && p.BattleQuality == 1);
        int patchedCount = ActiveSavService.ApplyProfileToBytes(buffer, esportsPreset);

        // Assert 2
        Assert.True(patchedCount >= 6, $"Expected >= 6 patched fields, got {patchedCount}");

        // Act 3: Read back patched buffer
        var updated = ActiveSavService.ReadProfileFromBytes(buffer, "Patched Read");
        Assert.Equal(7, updated.FpsLevel); // 120 FPS
        Assert.Equal(7, updated.LobbyFpsLevel);
        Assert.Equal(1, updated.BattleQuality); // Smooth
        Assert.Equal(1, updated.Style); // Classic
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
        Assert.Contains("120 FPS", ActiveSavProfile.GetFpsLabel(7));
        Assert.Contains("90 FPS", ActiveSavProfile.GetFpsLabel(6));
        Assert.Contains("60 FPS", ActiveSavProfile.GetFpsLabel(5));

        Assert.Contains("Smooth", ActiveSavProfile.GetQualityLabel(1));
        Assert.Contains("HDR", ActiveSavProfile.GetQualityLabel(4));

        Assert.Contains("Classic", ActiveSavProfile.GetStyleLabel(1));
        Assert.Contains("Colorful", ActiveSavProfile.GetStyleLabel(2));
    }
}
