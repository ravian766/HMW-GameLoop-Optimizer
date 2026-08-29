using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class UpdateManagerTests
{
    [Theory]
    [InlineData("v2.1.0", 2, 1, 0)]
    [InlineData("2.0.0", 2, 0, 0)]
    [InlineData("v2.0.0.45", 2, 0, 0, 45)]
    [InlineData("3.1", 3, 1, 0)]
    [InlineData("v4.0.0-rc1", 4, 0, 0)]
    public void TryParseVersion_ValidInputs_ParsesExpectedVersion(string input, int major, int minor, int build, int revision = -1)
    {
        bool success = UpdateManager.TryParseVersion(input, out var version);

        Assert.True(success);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(build, version.Build);
        if (revision >= 0)
        {
            Assert.Equal(revision, version.Revision);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid_version")]
    public void TryParseVersion_InvalidInputs_ReturnsFalse(string input)
    {
        bool success = UpdateManager.TryParseVersion(input, out _);
        Assert.False(success);
    }

    [Fact]
    public void ParseLatestUpdate_NewerVersionAvailable_ReturnsUpdateInfo()
    {
        var updater = new UpdateManager();
        var currentVersion = new Version(2, 0, 0);

        string mockJson = @"
[
  {
    ""tag_name"": ""v2.1.0"",
    ""name"": ""GameLoop Optimizer v2.1.0"",
    ""body"": ""- Added 120 FPS Android Unlock\n- Added Automatic Updates"",
    ""draft"": false,
    ""prerelease"": false,
    ""published_at"": ""2026-08-29T12:00:00Z"",
    ""html_url"": ""https://github.com/ravian766/HMW-GameLoop-Optimizer/releases/tag/v2.1.0"",
    ""assets"": [
      {
        ""name"": ""GameLoopOptimizer-v2.1.0-win-x64.zip"",
        ""browser_download_url"": ""https://github.com/ravian766/HMW-GameLoop-Optimizer/releases/download/v2.1.0/GameLoopOptimizer-v2.1.0-win-x64.zip"",
        ""size"": 15485760
      }
    ]
  }
]";

        var update = updater.ParseLatestUpdate(mockJson, currentVersion);

        Assert.NotNull(update);
        Assert.Equal(new Version(2, 1, 0), update.Version);
        Assert.Equal("v2.1.0", update.TagName);
        Assert.Equal("GameLoop Optimizer v2.1.0", update.ReleaseTitle);
        Assert.Contains("Added 120 FPS", update.ReleaseNotes);
        Assert.Equal("GameLoopOptimizer-v2.1.0-win-x64.zip", update.FileName);
        Assert.Equal("https://github.com/ravian766/HMW-GameLoop-Optimizer/releases/download/v2.1.0/GameLoopOptimizer-v2.1.0-win-x64.zip", update.DownloadUrl);
        Assert.Equal(15485760, update.FileSizeBytes);
        Assert.False(update.IsPreRelease);
    }

    [Fact]
    public void ParseLatestUpdate_SameOrOlderVersion_ReturnsNull()
    {
        var updater = new UpdateManager();
        var currentVersion = new Version(2, 1, 0);

        string mockJson = @"
[
  {
    ""tag_name"": ""v2.1.0"",
    ""name"": ""GameLoop Optimizer v2.1.0"",
    ""body"": ""Changelog"",
    ""draft"": false,
    ""prerelease"": false,
    ""assets"": [
      {
        ""name"": ""GameLoopOptimizer-v2.1.0-win-x64.zip"",
        ""browser_download_url"": ""https://example.com/download.zip"",
        ""size"": 1000
      }
    ]
  }
]";

        var update = updater.ParseLatestUpdate(mockJson, currentVersion);
        Assert.Null(update);
    }

    [Fact]
    public void ParseLatestUpdate_PreReleaseIgnoredWhenDisabled()
    {
        var updater = new UpdateManager();
        var currentVersion = new Version(2, 0, 0);

        string mockJson = @"
[
  {
    ""tag_name"": ""v2.2.0-beta1"",
    ""name"": ""GameLoop Optimizer v2.2.0 Beta"",
    ""body"": ""Beta testing release"",
    ""draft"": false,
    ""prerelease"": true,
    ""assets"": [
      {
        ""name"": ""GameLoopOptimizer-v2.2.0-win-x64.zip"",
        ""browser_download_url"": ""https://example.com/download.zip"",
        ""size"": 1000
      }
    ]
  }
]";

        var updateDisabled = updater.ParseLatestUpdate(mockJson, currentVersion, includePreReleases: false);
        Assert.Null(updateDisabled);

        var updateEnabled = updater.ParseLatestUpdate(mockJson, currentVersion, includePreReleases: true);
        Assert.NotNull(updateEnabled);
        Assert.True(updateEnabled.IsPreRelease);
    }
}
