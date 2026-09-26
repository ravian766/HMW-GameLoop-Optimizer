namespace GameLoopOptimizer.Models;

public class ActiveSavProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCustom { get; set; } = false;

    // In-game FPS Cap (matches PanelLoop/PUBG engine values):
    // 1=Low (20 FPS), 2=Medium (25 FPS), 3=High (30 FPS), 4=Ultra (40 FPS),
    // 5=Extreme (60 FPS), 6=90 FPS, 7=120 FPS
    public int FpsLevel { get; set; } = 7;
    public int LobbyFpsLevel { get; set; } = 7;

    // In-game Render Quality: 1=Smooth, 2=Balanced, 3=HD, 4=HDR, 5=Ultra HD
    public int BattleQuality { get; set; } = 1;
    public int LobbyQuality { get; set; } = 1;

    // Visual Style: 1=Classic, 2=Colorful, 3=Realistic, 4=Soft, 5=Movie
    public int Style { get; set; } = 1;

    // Graphic Favor: 4=Customize (Bypasses game preset overrides), 1=Better Graphics, 2=Balanced, 3=Better Frame Rate
    public int GraphicFavor { get; set; } = 4;

    public static string GetFpsLabel(int fps) => fps switch
    {
        1 => "Low (20 FPS)",
        2 => "Medium (25 FPS)",
        3 => "High (30 FPS)",
        4 => "Ultra (40 FPS)",
        5 => "Extreme (60 FPS)",
        6 => "90 FPS",
        7 => "120 FPS (Legacy)",
        8 => "120 FPS (Ultra Extreme)",
        _ => $"{fps} Level"
    };

    public static string GetQualityLabel(int q) => q switch
    {
        1 => "Smooth (流畅 - Lowest Latency)",
        2 => "Balanced (均衡)",
        3 => "HD (高清)",
        4 => "HDR (高清高动态)",
        5 => "Ultra HD / UHD (超高清 - Level 5)",
        >= 6 => "Ultra HD / UHD (超高清)",
        _ => $"{q} Level"
    };

    public static string GetStyleLabel(int s) => s switch
    {
        1 => "Classic (经典 - Crisp Player Visibility)",
        2 => "Colorful (鲜艳 - High Vibrancy)",
        3 => "Realistic (写实)",
        4 => "Soft (柔和)",
        5 => "Movie (电影)",
        _ => $"{s} Style"
    };

    public static IReadOnlyList<ActiveSavProfile> BuiltInPresets { get; } = new List<ActiveSavProfile>
    {
        new ActiveSavProfile
        {
            Name = "Esports 120 FPS Ultra-Low Latency",
            Description = "Unlocks maximum 120 FPS frame rate (FPS Level 8) with Smooth graphics and Classic rendering for lowest input lag and fastest reaction time.",
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            BattleQuality = 1,
            LobbyQuality = 1,
            Style = 1,
            GraphicFavor = 4,
            IsCustom = false
        },
        new ActiveSavProfile
        {
            Name = "Ultra HDR / UHD 120 FPS",
            Description = "Forces Ultra HD / UHD maximum graphics quality (Level 5) with 120 FPS target unlock (FPS Level 8) for flagship visuals and performance.",
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            BattleQuality = 5,
            LobbyQuality = 5,
            Style = 2,
            GraphicFavor = 4,
            IsCustom = false
        },
        new ActiveSavProfile
        {
            Name = "Competitive 90 FPS Smooth",
            Description = "Optimized 90 FPS frame ceiling (FPS Level 6) with zero motion blur and soft shadows for smooth competitive ranked matches.",
            FpsLevel = 6,
            LobbyFpsLevel = 6,
            BattleQuality = 1,
            LobbyQuality = 1,
            Style = 2,
            GraphicFavor = 4,
            IsCustom = false
        },
        new ActiveSavProfile
        {
            Name = "Streamer 120 FPS HDR",
            Description = "Max 120 FPS (FPS Level 8) with HDR graphics quality (Level 4) and vibrant dynamic range for content creation and high-end rigs.",
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            BattleQuality = 4,
            LobbyQuality = 4,
            Style = 2,
            GraphicFavor = 4,
            IsCustom = false
        },
        new ActiveSavProfile
        {
            Name = "Balanced 90 FPS HD",
            Description = "Sharp 90 FPS visual clarity with HD textures (Level 3) and balanced shadows for mid-range gaming systems.",
            FpsLevel = 6,
            LobbyFpsLevel = 6,
            BattleQuality = 3,
            LobbyQuality = 2,
            Style = 1,
            GraphicFavor = 4,
            IsCustom = false
        },
        new ActiveSavProfile
        {
            Name = "Anti-Stutter Potato Mode (Max Frame Pacing)",
            Description = "Stripped-down visual overhead with lowest texture complexity, uncapped 120 FPS in battle and 60 FPS in lobby for maximum frame stability on entry-level PCs.",
            FpsLevel = 8,
            LobbyFpsLevel = 5,
            BattleQuality = 1,
            LobbyQuality = 1,
            Style = 1,
            GraphicFavor = 4,
            IsCustom = false
        },
        new ActiveSavProfile
        {
            Name = "Custom In-Game Configuration",
            Description = "User-customized in-game parameters allowing precision manual control over battle/lobby frame rates, quality, and visual styles.",
            FpsLevel = 8,
            LobbyFpsLevel = 8,
            BattleQuality = 1,
            LobbyQuality = 1,
            Style = 1,
            GraphicFavor = 4,
            IsCustom = true
        }
    };
}
