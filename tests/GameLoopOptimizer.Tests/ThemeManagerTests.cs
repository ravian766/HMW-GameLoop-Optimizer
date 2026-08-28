using System.IO;
using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class ThemeManagerTests
{
    [Fact]
    public void ThemeManager_DefaultTheme_IsDark()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"theme_test_{Guid.NewGuid():N}.json");
        try
        {
            var manager = new ThemeManager(tempFile);
            Assert.Equal(AppTheme.Dark, manager.CurrentTheme);
            Assert.True(manager.IsDarkTheme);
            Assert.False(manager.IsLightTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ThemeManager_ToggleTheme_SwitchesBetweenDarkAndLight()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"theme_test_{Guid.NewGuid():N}.json");
        try
        {
            var manager = new ThemeManager(tempFile);
            Assert.Equal(AppTheme.Dark, manager.CurrentTheme);

            AppTheme? reportedTheme = null;
            manager.ThemeChanged += (s, theme) => reportedTheme = theme;

            // Toggle to Light
            manager.ToggleTheme();
            Assert.Equal(AppTheme.Light, manager.CurrentTheme);
            Assert.True(manager.IsLightTheme);
            Assert.False(manager.IsDarkTheme);
            Assert.Equal(AppTheme.Light, reportedTheme);

            // Toggle back to Dark
            manager.ToggleTheme();
            Assert.Equal(AppTheme.Dark, manager.CurrentTheme);
            Assert.True(manager.IsDarkTheme);
            Assert.False(manager.IsLightTheme);
            Assert.Equal(AppTheme.Dark, reportedTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ThemeManager_SaveAndLoadThemePreference_PersistsAccurately()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"theme_test_{Guid.NewGuid():N}.json");
        try
        {
            var manager1 = new ThemeManager(tempFile);
            manager1.SaveThemePreference(AppTheme.Light);

            var manager2 = new ThemeManager(tempFile);
            var loaded = manager2.LoadThemePreference();
            Assert.Equal(AppTheme.Light, loaded);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
