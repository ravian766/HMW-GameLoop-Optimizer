using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public class UpdateManager
{
    private const string RepoOwner = "ravian766";
    private const string RepoName = "HMW-GameLoop-Optimizer";
    private const string GitHubApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases";

    private static readonly Lazy<UpdateManager> _instance = new(() => new UpdateManager());
    public static UpdateManager Instance => _instance.Value;

    private readonly HttpClient _httpClient;

    public UpdateManager(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GameLoopOptimizer-Updater/1.0");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }
    }

    /// <summary>
    /// Gets the current running application version.
    /// </summary>
    public Version GetCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        if (ver != null && ver != new Version(0, 0, 0, 0))
        {
            return ver;
        }

        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer) && TryParseVersion(infoVer, out var parsed))
        {
            return parsed;
        }

        return new Version(2, 0, 0);
    }

    /// <summary>
    /// Checks GitHub Releases API for a newer release.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool includePreReleases = false, CancellationToken ct = default)
    {
        try
        {
            Logger.Info("UpdateManager", "Checking for application updates on GitHub...");

            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn("UpdateManager", $"GitHub API returned status code: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseLatestUpdate(json, GetCurrentVersion(), includePreReleases);
        }
        catch (Exception ex)
        {
            Logger.Error("UpdateManager", $"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the GitHub Releases JSON payload and returns UpdateInfo if an update is available.
    /// </summary>
    public UpdateInfo? ParseLatestUpdate(string json, Version currentVersion, bool includePreReleases = false)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                bool isDraft = release.TryGetProperty("draft", out var dProp) && dProp.GetBoolean();
                if (isDraft) continue;

                bool isPreRelease = release.TryGetProperty("prerelease", out var prProp) && prProp.GetBoolean();
                if (isPreRelease && !includePreReleases) continue;

                if (!release.TryGetProperty("tag_name", out var tagProp)) continue;
                string rawTag = tagProp.GetString() ?? string.Empty;

                if (!TryParseVersion(rawTag, out var releaseVersion)) continue;

                // Compare version with current app version
                if (releaseVersion > currentVersion)
                {
                    string name = release.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? rawTag : rawTag;
                    string body = release.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
                    string htmlUrl = release.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? string.Empty : string.Empty;
                    DateTime publishedAt = release.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow;

                    // Locate zip asset
                    if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string assetName = asset.TryGetProperty("name", out var anProp) ? anProp.GetString() ?? string.Empty : string.Empty;
                            string downloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() ?? string.Empty : string.Empty;
                            long size = asset.TryGetProperty("size", out var szProp) && szProp.TryGetInt64(out var s) ? s : 0;

                            if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                return new UpdateInfo
                                {
                                    Version = releaseVersion,
                                    VersionString = releaseVersion.ToString(),
                                    TagName = rawTag,
                                    ReleaseTitle = name,
                                    ReleaseNotes = body,
                                    DownloadUrl = downloadUrl,
                                    FileName = assetName,
                                    FileSizeBytes = size,
                                    PublishedAt = publishedAt,
                                    IsPreRelease = isPreRelease,
                                    HtmlUrl = htmlUrl
                                };
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("UpdateManager", $"Error parsing release payload: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Parses a version string like 'v2.1.0', '2.0.0.15', or 'v1.0-alpha'.
    /// </summary>
    public static bool TryParseVersion(string input, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(input)) return false;

        string clean = input.Trim().TrimStart('v', 'V');
        int dashIdx = clean.IndexOf('-');
        if (dashIdx > 0)
        {
            clean = clean.Substring(0, dashIdx);
        }

        // Standardize parts
        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out int major))
        {
            version = new Version(major, 0, 0);
            return true;
        }
        if (parts.Length == 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out int minor))
        {
            version = new Version(major, minor, 0);
            return true;
        }

        return Version.TryParse(clean, out version!);
    }

    /// <summary>
    /// Downloads the update package with progress reporting.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string updateDir = Path.Combine(Path.GetTempPath(), "GameLoopOptimizer_Update");
        if (Directory.Exists(updateDir))
        {
            try { Directory.Delete(updateDir, true); } catch { }
        }
        Directory.CreateDirectory(updateDir);

        string zipPath = Path.Combine(updateDir, string.IsNullOrEmpty(update.FileName) ? "update.zip" : update.FileName);

        Logger.Info("UpdateManager", $"Downloading update from {update.DownloadUrl} to {zipPath}...");

        using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
            totalRead += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double percentage = (double)totalRead / totalBytes.Value * 100.0;
                progress?.Report(percentage);
            }
        }

        progress?.Report(100.0);
        Logger.Success("UpdateManager", $"Downloaded update package ({totalRead} bytes).");
        return zipPath;
    }

    /// <summary>
    /// Extracts the downloaded update, creates the updater batch script, spawns it, and terminates the application.
    /// </summary>
    public bool ApplyUpdateAndRestart(string zipPath)
    {
        try
        {
            string updateDir = Path.GetDirectoryName(zipPath)!;
            string stagedDir = Path.Combine(updateDir, "staged");
            if (Directory.Exists(stagedDir))
            {
                try { Directory.Delete(stagedDir, true); } catch { }
            }
            Directory.CreateDirectory(stagedDir);

            Logger.Info("UpdateManager", $"Extracting {zipPath} to {stagedDir}...");
            ZipFile.ExtractToDirectory(zipPath, stagedDir, true);

            // Locate app target directory
            string appExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            string appDir = !string.IsNullOrEmpty(appExePath) ? Path.GetDirectoryName(appExePath)! : AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

            int currentPid = Process.GetCurrentProcess().Id;
            string scriptPath = Path.Combine(updateDir, "apply_update.cmd");

            // Write atomic updater batch script
            string scriptContent = $@"@echo off
setlocal
chcp 65001 >nul
title GameLoop Optimizer - Updating to latest release...

echo ===================================================
echo  Updating GameLoop Precision Engine Optimizer...
echo ===================================================
echo Waiting for application process (PID {currentPid}) to close...

set count=0
:WAIT_LOOP
tasklist /FI ""PID eq {currentPid}"" 2>NUL | find /I ""{currentPid}"" >NUL
if %ERRORLEVEL% equ 0 (
    timeout /t 1 /nobreak >nul
    set /a count+=1
    if %count% geq 8 (
        taskkill /F /PID {currentPid} >nul 2>&1
    )
    goto WAIT_LOOP
)

echo.
echo Installing new version files into: ""{appDir}""
robocopy ""{stagedDir}"" ""{appDir}"" /E /IS /IT /NP /R:3 /W:1 >nul

echo.
echo Launching updated GameLoop Optimizer...
timeout /t 1 /nobreak >nul
start """" ""{Path.Combine(appDir, "GameLoopOptimizer.exe")}""

echo Cleaning temporary update files...
timeout /t 2 /nobreak >nul
(goto) 2>nul & rmdir /s /q ""{updateDir}""
";

            File.WriteAllText(scriptPath, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = false,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            Logger.Info("UpdateManager", "Launching updater script and exiting application...");
            Process.Start(psi);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Application.Current.Shutdown();
            });

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("UpdateManager", $"Failed to apply update: {ex.Message}");
            return false;
        }
    }
}
