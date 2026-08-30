namespace GameLoopOptimizer.Core;

public enum AimPlaystyle
{
    PrecisionLowSens,
    BalancedCompetitive,
    HighSensFastFlick
}

public class ScopeSensitivityItem
{
    public string ScopeName { get; set; } = string.Empty;
    public string IdealFor { get; set; } = string.Empty;
    public int CameraSensitivity { get; set; }
    public int AdsSensitivity { get; set; }
    public string RecoilTip { get; set; } = string.Empty;
}

public class SensitivityProfileResult
{
    public int MouseDpi { get; set; }
    public AimPlaystyle Playstyle { get; set; }
    public int GameLoopKeymapX { get; set; }
    public int GameLoopKeymapY { get; set; }
    public double VerticalMultiplier { get; set; }
    public string RecoilReliefLabel { get; set; } = string.Empty;
    public string RecoilReliefDescription { get; set; } = string.Empty;
    public List<ScopeSensitivityItem> ScopeSettings { get; set; } = new();
    public string GeneralRecoilRecommendation { get; set; } = string.Empty;
}

public static class SensitivityCalculator
{
    public static SensitivityProfileResult Calculate(int mouseDpi, AimPlaystyle playstyle, int screenHeight = 1080)
    {
        return Calculate(mouseDpi, playstyle, 1.65, screenHeight);
    }

    public static SensitivityProfileResult Calculate(int mouseDpi, AimPlaystyle playstyle, double verticalMultiplier, int screenHeight = 1080)
    {
        int dpi = Math.Clamp(mouseDpi, 400, 3200);
        double vMult = Math.Clamp(verticalMultiplier, 1.0, 2.5);

        // Base DPI factor normalized around 800 DPI
        double dpiFactor = 800.0 / dpi;

        double playstyleBaseMultiplier = playstyle switch
        {
            AimPlaystyle.PrecisionLowSens => 0.85,
            AimPlaystyle.BalancedCompetitive => 1.0,
            AimPlaystyle.HighSensFastFlick => 1.25,
            _ => 1.0
        };

        // Base keymap sensitivity (percentage 10-100)
        int keymapX = (int)Math.Round(Math.Clamp(50 * dpiFactor * playstyleBaseMultiplier, 15, 85));
        int keymapY = (int)Math.Round(Math.Clamp(keymapX * vMult, 20, 100));

        // Recoil relief calculation (e.g. 1.65x multiplier reduces physical downward hand travel by ~39%)
        double physicalTravelReduction = (1.0 - (1.0 / vMult)) * 100.0;
        string reliefLabel = vMult switch
        {
            <= 1.05 => "Standard 1:1 (Neutral)",
            <= 1.40 => "Balanced (Mild Relief)",
            <= 1.75 => "Laser Spray (Recommended)",
            _ => "Heavy Recoil Control (Max Relief)"
        };

        var result = new SensitivityProfileResult
        {
            MouseDpi = dpi,
            Playstyle = playstyle,
            GameLoopKeymapX = keymapX,
            GameLoopKeymapY = keymapY,
            VerticalMultiplier = Math.Round(vMult, 2),
            RecoilReliefLabel = reliefLabel,
            RecoilReliefDescription = vMult > 1.01 
                ? $"-{physicalTravelReduction:F0}% Downward Mouse Travel Required" 
                : "1:1 Symmetric Horizontal & Vertical Sensitivity",
            ScopeSettings = new List<ScopeSensitivityItem>()
        };

        // In-game sensitivity calculations matching exact PUBG Mobile settings menu (10 tiers)
        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "TPP No Scope",
            IdealFor = "Hip-fire, CQB Brawls & Free Movement",
            CameraSensitivity = CalculateSens(65, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(60, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Hip-fire sprays tighten significantly when crouched; keep crosshair at chest/head level."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "FPP No Scope",
            IdealFor = "First-Person CQB & Fast Room Clearing",
            CameraSensitivity = CalculateSens(60, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(55, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Slightly lower than TPP to prevent visual overshooting during tight indoor turns."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "TPP Aim",
            IdealFor = "Over-the-Shoulder Tight Hip-Fire Aim",
            CameraSensitivity = CalculateSens(55, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(65, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Gives high accuracy during close-quarters combat without switching to full ADS."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "FPP Aim",
            IdealFor = "First-Person Focused Hip-Fire Aim",
            CameraSensitivity = CalculateSens(50, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(60, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Optimal for steady tracking on fast strafing targets in first-person mode."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "Red Dot, Holographic, Canted",
            IdealFor = "Close-to-Mid Range Engagements (0–50m)",
            CameraSensitivity = CalculateSens(35, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(42, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Higher ADS allows steady micro-corrections during rapid 30-round automatic sprays."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "2x Scope",
            IdealFor = "Mid-Range Combat (30–70m)",
            CameraSensitivity = CalculateSens(28, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(36, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Great balance of zoom and peripheral vision; pull down steadily after bullet #6."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "3x Scope, Win94",
            IdealFor = "M416 / SCAR-L Laser Sprays (50–120m)",
            CameraSensitivity = CalculateSens(22, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(32, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Pair with a Half Grip or Compensator. Pull down firmly for the first 8 bullets."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "4x Scope, VSS",
            IdealFor = "DMR Taps (Mini14/SKS) & DP-28 Sprays",
            CameraSensitivity = CalculateSens(15, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(24, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "For 5.56 AR sprays on 4x, crouch first to cut horizontal jump by 50%."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "6x Scope",
            IdealFor = "Full-Auto AR Sprays (Adjusted to 3x) & DMRs",
            CameraSensitivity = CalculateSens(14, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(22, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Dial zoom down to 3x in-game. Provides superior reticle clarity with 3x spray physics."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "8x Scope",
            IdealFor = "Bolt-Action Snipers (AWM, M24, Kar98k)",
            CameraSensitivity = CalculateSens(10, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(12, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Low sensitivity ensures pixel-perfect headshot tracking on moving targets."
        });

        result.GeneralRecoilRecommendation = $"At {dpi} DPI and {vMult:F2}x Vertical Bias, set GameLoop Keymap Sensitivity to X={keymapX}%, Y={keymapY}%. In PUBG Settings > Sensitivity, apply the ADS values above for optimal recoil pull-down.";

        return result;
    }

    private static int CalculateSens(int baseSens, double dpiFactor, double playstyleMult)
    {
        // Smooth logarithmic dampening for higher DPI to keep in-game values comfortable
        double factor = Math.Sqrt(dpiFactor) * playstyleMult;
        int val = (int)Math.Round(baseSens * factor);
        return Math.Clamp(val, 5, 120);
    }
}
