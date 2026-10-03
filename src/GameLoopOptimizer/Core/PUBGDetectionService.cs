using System.IO;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class PubgProfileDetectionResult
{
    public bool IsDetected { get; set; } = false;
    public string PackageName { get; set; } = "com.tencent.ig";
    public string PackageDisplayName { get; set; } = "PUBG Mobile (Global)";
    public int FpsLevel { get; set; } = 90;
    public int RenderQuality { get; set; } = 2; // 0=Smooth, 1=Balanced, 2=HD, 3=HDR, 4=Ultra HD
    public bool ShadowsEnabled { get; set; } = false;
    public bool AutoAdjustGraphics { get; set; } = false;
    public int AntiAliasingLevel { get; set; } = 0;
    public string ActiveSavPath { get; set; } = string.Empty;
    public bool HasActiveSavFile { get; set; } = false;
    public string Version { get; set; } = string.Empty;
}

public static class PUBGDetectionService
{
    public static readonly Dictionary<string, string> KnownPackages = new()
    {
        { "com.tencent.ig", "PUBG Mobile (Global)" },
        { "com.pubg.krmobile", "PUBG Mobile (Korea / Japan)" },
        { "com.pubg.imobile", "Battlegrounds Mobile India (BGMI)" },
        { "com.vng.pubgmobile", "PUBG Mobile (Vietnam)" },
        { "com.rekoo.pubgm", "PUBG Mobile (Taiwan)" }
    };

    public static async Task<PubgProfileDetectionResult> DetectPubgProfileAsync(GameLoopConfig gl)
    {
        return await Task.Run(() => DetectPubgProfile(gl));
    }

    public static PubgProfileDetectionResult DetectPubgProfile(GameLoopConfig gl)
    {
        var result = new PubgProfileDetectionResult();

        // 1. Detect active package from GameLoopConfig / Registry
        string targetPkg = !string.IsNullOrEmpty(gl.ActivePubgPackage) ? gl.ActivePubgPackage : "com.tencent.ig";
        result.PackageName = targetPkg;
        if (KnownPackages.TryGetValue(targetPkg, out var name))
        {
            result.PackageDisplayName = name;
        }

        // 2. Read Registry values for target package
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(gl.RegistryKeyPath) 
                         ?? Registry.LocalMachine.OpenSubKey(gl.RegistryKeyPath);

            if (key != null)
            {
                var fpsVal = key.GetValue($"{targetPkg}_FPSLevel");
                if (fpsVal is int fpsInt) result.FpsLevel = fpsInt;

                var qualVal = key.GetValue($"{targetPkg}_RenderQuality");
                if (qualVal is int qualInt) result.RenderQuality = qualInt;

                result.IsDetected = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("PUBGDetectionService", $"Registry query warning: {ex.Message}");
        }

        // 3. Inspect staged or extracted Active.sav if available
        try
        {
            string stagedSav = Path.Combine(ActiveSavService.LocalStagingDirectory, $"{targetPkg}_Active.sav");
            if (File.Exists(stagedSav))
            {
                result.ActiveSavPath = stagedSav;
                result.HasActiveSavFile = true;
                result.IsDetected = true;

                byte[] bytes = File.ReadAllBytes(stagedSav);
                var prof = ActiveSavService.ReadProfileFromBytes(bytes);
                if (prof != null)
                {
                    result.FpsLevel = prof.FpsLevel switch
                    {
                        6 => 90,
                        7 => 120,
                        8 => 120,
                        5 => 60,
                        4 => 40,
                        _ => 60
                    };
                    result.RenderQuality = prof.BattleQuality;
                    result.ShadowsEnabled = prof.BattleQuality >= 3;
                    result.AntiAliasingLevel = prof.BattleQuality >= 4 ? 2 : (prof.BattleQuality >= 3 ? 1 : 0);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("PUBGDetectionService", $"Active.sav check warning: {ex.Message}");
        }

        gl.IsPubgInstalled = result.IsDetected;
        gl.ActivePubgPackage = result.PackageName;
        gl.PubgFpsLevel = result.FpsLevel;
        gl.PubgRenderQuality = result.RenderQuality;
        gl.PubgShadowsEnabled = result.ShadowsEnabled;
        gl.PubgAntiAliasing = result.AntiAliasingLevel;

        Logger.Info("PUBGDetectionService", $"PUBG Mobile Profile: {result.PackageDisplayName} ({result.PackageName}), FPS: {result.FpsLevel}, Quality: {result.RenderQuality}, Active.sav: {result.HasActiveSavFile}");

        return result;
    }
}
