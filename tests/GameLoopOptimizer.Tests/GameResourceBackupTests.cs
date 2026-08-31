using System.IO;
using Xunit;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Tests;

public class GameResourceBackupTests
{
    [Fact]
    public void GetRemotePaksPath_ReturnsCorrectInVmAndroidDirectory()
    {
        string pathPubg = GameResourceBackupService.GetRemotePaksPath("com.tencent.ig");
        Assert.Equal("/sdcard/Android/data/com.tencent.ig/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks", pathPubg);

        string pathBgmi = GameResourceBackupService.GetRemotePaksPath("com.pubg.imobile");
        Assert.Equal("/sdcard/Android/data/com.pubg.imobile/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/Paks", pathBgmi);
    }

    [Fact]
    public void GetGameLoopSharedFolderPath_ReturnsValidDirectoryString()
    {
        string sharedPath = GameResourceBackupService.GetGameLoopSharedFolderPath();
        Assert.False(string.IsNullOrWhiteSpace(sharedPath));
        Assert.Contains("MobileGamePCShared", sharedPath);
    }

    [Fact]
    public void PakBackupProfile_FormatsGigabytesAndMegabytesAccurately()
    {
        var gbProfile = new PakBackupProfile
        {
            TotalSizeBytes = 8L * 1024 * 1024 * 1024, // 8 GB
            FileCount = 42
        };
        Assert.Contains("8.00 GB", gbProfile.FormattedSize);

        var mbProfile = new PakBackupProfile
        {
            TotalSizeBytes = 550L * 1024 * 1024, // 550 MB
            FileCount = 12
        };
        Assert.Contains("550.0 MB", mbProfile.FormattedSize);
    }

    [Fact]
    public void ListPakBackups_ExecutesSafelyWithoutExceptions()
    {
        var list = GameResourceBackupService.ListPakBackups();
        Assert.NotNull(list);
    }

    [Fact]
    public void SetVaultDirectory_UpdatesAndPersistsVaultPath()
    {
        string tempVault = Path.Combine(Path.GetTempPath(), "TestPakVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            GameResourceBackupService.SetVaultDirectory(tempVault);
            Assert.Equal(tempVault, GameResourceBackupService.PakVaultDirectory);
        }
        finally
        {
            if (Directory.Exists(tempVault)) Directory.Delete(tempVault, true);
        }
    }

    [Fact]
    public async Task ImportExistingFolderAsync_ImportsRawPakFilesAndGeneratesManifest()
    {
        string tempSource = Path.Combine(Path.GetTempPath(), "RawPaks_" + Guid.NewGuid().ToString("N"));
        string tempVault = Path.Combine(Path.GetTempPath(), "TestVault_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempSource);
            File.WriteAllBytes(Path.Combine(tempSource, "game_patch_3.2.0.18550.pak"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(tempSource, "res_erangel_hd.pak"), new byte[4096]);

            GameResourceBackupService.SetVaultDirectory(tempVault);
            var result = await GameResourceBackupService.ImportExistingFolderAsync(tempSource, "com.tencent.ig");

            Assert.True(result.Success);
            Assert.Equal(2, result.FilesCopied);
            Assert.Equal(6144, result.BytesTransferred);
            Assert.NotNull(result.Profile);

            var backups = GameResourceBackupService.ListPakBackups();
            Assert.Contains(backups, b => b.FileCount == 2 && b.PackageName == "com.tencent.ig");
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (Directory.Exists(tempVault)) Directory.Delete(tempVault, true);
        }
    }

    [Fact]
    public void DiscoverExistingVaultsOnAllDrives_ExecutesSafely()
    {
        var vaults = GameResourceBackupService.DiscoverExistingVaultsOnAllDrives();
        Assert.NotNull(vaults);
    }
}
