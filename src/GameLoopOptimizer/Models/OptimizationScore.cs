namespace GameLoopOptimizer.Models;

public class ScoreCategory
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; } = 0;
    public int MaxScore { get; set; } = 20;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public class OptimizationScore
{
    public int TotalScore { get; set; } = 0;
    public int MaxTotalScore => 100;

    public ScoreCategory WindowsConfig { get; set; } = new() { Name = "Windows Configuration", MaxScore = 20 };
    public ScoreCategory PowerDelivery { get; set; } = new() { Name = "Power Delivery", MaxScore = 15 };
    public ScoreCategory GameLoopConfig { get; set; } = new() { Name = "GameLoop Resource Allocation", MaxScore = 25 };
    public ScoreCategory GraphicsSettings { get; set; } = new() { Name = "Graphics & Shader Cache", MaxScore = 20 };
    public ScoreCategory MemoryStorage { get; set; } = new() { Name = "Memory & Temp Cache", MaxScore = 10 };
    public ScoreCategory BackgroundProcesses { get; set; } = new() { Name = "Background Overhead", MaxScore = 10 };

    public List<string> HonestExplanations { get; set; } = new();
}
