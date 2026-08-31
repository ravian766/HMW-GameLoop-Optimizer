namespace GameLoopOptimizer.Models;

public enum RiskLevel
{
    Safe,
    Low,
    Moderate,
    Advanced
}

public enum OptimizationCategory
{
    WindowsConfig,
    PowerDelivery,
    GameLoopEngine,
    GraphicsQuality,
    MemoryStorage,
    BackgroundProcess
}

public enum OptimizationState
{
    NotOptimized,
    Optimized,
    Recommended,
    Disabled,
    NotDetected,
    RequiresAdmin,
    Unknown
}

public enum OptimizationProfile
{
    Safe,
    Balanced,
    MaximumPerformance,
    Custom
}

public enum HardwareTier
{
    LowEnd,
    MidRange,
    HighEnd
}

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
    Unknown
}

public enum StorageType
{
    Nvme,
    Ssd,
    Hdd,
    Unknown
}

public enum GraphicsRenderer
{
    Auto,
    DirectXPlus,
    OpenGLPlus
}
