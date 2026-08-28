using System.IO;
using System.Text.Json;
using System.Windows;

namespace GameLoopOptimizer.Core;

public enum AppTheme
{
    Dark,
    Light
}

public class ThemeManager
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    private const string DarkThemeFile = "ColorsDark.xaml";
    private const string LightThemeFile = "ColorsLight.xaml";
    private const string ConfigFileName = "theme_preference.json";

    private AppTheme _currentTheme = AppTheme.Dark;
    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeChanged?.Invoke(this, _currentTheme);
            }
        }
    }

    public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;
    public bool IsLightTheme => CurrentTheme == AppTheme.Light;

    public event EventHandler<AppTheme>? ThemeChanged;

    private readonly string _configPath;

    public ThemeManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "GameLoopOptimizer");
        _configPath = Path.Combine(dir, ConfigFileName);
    }

    public ThemeManager(string customConfigPath)
    {
        _configPath = customConfigPath;
    }

    /// <summary>
    /// Loads saved theme or defaults to Dark theme, then applies it.
    /// </summary>
    public void Initialize()
    {
        var savedTheme = LoadThemePreference();
        ApplyTheme(savedTheme, persist: false);
    }

    /// <summary>
    /// Toggles between Dark and Light themes.
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme(newTheme, persist: true);
    }

    /// <summary>
    /// Applies the specified theme to Application resources.
    /// </summary>
    public void ApplyTheme(AppTheme theme, bool persist = true)
    {
        var themeFile = theme == AppTheme.Light ? LightThemeFile : DarkThemeFile;

        if (Application.Current != null)
        {
            void PerformSwap()
            {
                try
                {
                    ResourceDictionary newDict;
                    try
                    {
                        var packUri = new Uri($"pack://application:,,,/GameLoopOptimizer;component/Styles/{themeFile}", UriKind.Absolute);
                        newDict = new ResourceDictionary { Source = packUri };
                    }
                    catch
                    {
                        var relUri = new Uri($"Styles/{themeFile}", UriKind.Relative);
                        newDict = new ResourceDictionary { Source = relUri };
                    }

                    var mergedDicts = Application.Current.Resources.MergedDictionaries;
                    for (int i = mergedDicts.Count - 1; i >= 0; i--)
                    {
                        var src = mergedDicts[i].Source?.ToString() ?? string.Empty;
                        if (src.Contains("ColorsDark", StringComparison.OrdinalIgnoreCase) ||
                            src.Contains("ColorsLight", StringComparison.OrdinalIgnoreCase) ||
                            src.Contains("Colors.xaml", StringComparison.OrdinalIgnoreCase))
                        {
                            mergedDicts.RemoveAt(i);
                        }
                    }

                    mergedDicts.Insert(0, newDict);

                    // Update every single resource key directly to guarantee immediate dynamic brush invalidation
                    foreach (var key in newDict.Keys)
                    {
                        Application.Current.Resources[key] = newDict[key];
                    }

                    Logger.Info("ThemeManager", $"Applied {theme} theme successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error("ThemeManager", $"Failed to swap theme dictionary to {themeFile}: {ex.Message}");
                }
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                PerformSwap();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(PerformSwap);
            }
        }

        CurrentTheme = theme;

        if (persist)
        {
            SaveThemePreference(theme);
        }
    }

    public AppTheme LoadThemePreference()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Theme", out var prop))
                {
                    var val = prop.GetString();
                    if (Enum.TryParse<AppTheme>(val, true, out var parsedTheme))
                    {
                        return parsedTheme;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("ThemeManager", $"Could not load theme preference: {ex.Message}");
        }

        return AppTheme.Dark;
    }

    public void SaveThemePreference(AppTheme theme)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(new { Theme = theme.ToString() }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Logger.Warn("ThemeManager", $"Could not save theme preference: {ex.Message}");
        }
    }
}
