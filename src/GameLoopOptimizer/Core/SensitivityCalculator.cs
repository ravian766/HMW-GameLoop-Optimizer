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
    public List<ScopeSensitivityItem> ScopeSettings { get; set; } = new();
    public string GeneralRecoilRecommendation { get; set; } = string.Empty;
}

public static class SensitivityCalculator
{
    public static SensitivityProfileResult Calculate(int mouseDpi, AimPlaystyle playstyle, int screenHeight = 1080)
    {
        int dpi = Math.Clamp(mouseDpi, 400, 3200);

        // Base DPI factor normalized around 800 DPI
        double dpiFactor = 800.0 / dpi;

        double playstyleBaseMultiplier = playstyle switch
        {
            AimPlaystyle.PrecisionLowSens => 0.85,
            AimPlaystyle.BalancedCompetitive => 1.0,
            AimPlaystyle.HighSensFastFlick => 1.25,
            _ => 1.0
        };

        // Vertical bias ratio for GameLoop (Y is scaled higher than X so pulling down requires less physical mouse travel)
        double verticalRatio = 1.35;

        // Base keymap sensitivity (percentage 10-100)
        int keymapX = (int)Math.Round(Math.Clamp(50 * dpiFactor * playstyleBaseMultiplier, 20, 80));
        int keymapY = (int)Math.Round(Math.Clamp(keymapX * verticalRatio, 30, 95));

        var result = new SensitivityProfileResult
        {
            MouseDpi = dpi,
            Playstyle = playstyle,
            GameLoopKeymapX = keymapX,
            GameLoopKeymapY = keymapY,
            VerticalMultiplier = Math.Round((double)keymapY / keymapX, 2),
            ScopeSettings = new List<ScopeSensitivityItem>()
        };

        // In-game sensitivity calculations based on standard UE4 FOV scaling & optimal recoil control
        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "3rd Person (No Scope)",
            IdealFor = "Hip-fire, CQB Brawls & Movement",
            CameraSensitivity = CalculateSens(55, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(50, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Hip-fire sprays tighten significantly when crouched; keep crosshair at chest/head level."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "Red Dot / Holographic / Canted",
            IdealFor = "Close-to-Mid Range Engagements (0–50m)",
            CameraSensitivity = CalculateSens(32, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(44, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Higher ADS allows steady micro-corrections during rapid 30-round automatic sprays."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "2x Scope",
            IdealFor = "Mid-Range Combat (30–70m)",
            CameraSensitivity = CalculateSens(28, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(38, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Great balance of zoom and peripheral vision; pull down steadily after bullet #6."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "3x Scope (Pro Meta)",
            IdealFor = "M416 / SCAR-L Laser Sprays (50–120m)",
            CameraSensitivity = CalculateSens(22, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(32, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Pair with a Half Grip or Compensator. Pull down firmly for the first 8 bullets."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "4x Scope",
            IdealFor = "DMR Taps (Mini14/SKS) & DP-28 Sprays",
            CameraSensitivity = CalculateSens(15, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(24, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "For 5.56 AR sprays on 4x, crouch first to cut horizontal jump by 50%."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "6x Scope (Adjusted to 3x)",
            IdealFor = "Full-Auto AR Sprays & Long DMR Tagging",
            CameraSensitivity = CalculateSens(18, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(28, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Dial zoom wheel down to 3x. Provides superior reticle clarity with 3x spray physics."
        });

        result.ScopeSettings.Add(new ScopeSensitivityItem
        {
            ScopeName = "8x Scope",
            IdealFor = "Bolt-Action Snipers (AWM, M24, Kar98k)",
            CameraSensitivity = CalculateSens(10, dpiFactor, playstyleBaseMultiplier),
            AdsSensitivity = CalculateSens(12, dpiFactor, playstyleBaseMultiplier),
            RecoilTip = "Low sensitivity ensures pixel-perfect headshot tracking on moving targets."
        });

        result.GeneralRecoilRecommendation = $"At {dpi} DPI, set GameLoop Keymap Sensitivity to X={keymapX}%, Y={keymapY}%. In PUBG Settings > Sensitivity, apply the ADS values above for optimal recoil pull-down.";

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
