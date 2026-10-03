namespace GameLoopOptimizer.Models;

public enum RiskLevel
{
    Safe,
    Low,
    Medium,
    Moderate = Medium,
    High,
    Advanced = High,
    NotRecommended
}

public enum OptimizationCategory
{
    WindowsConfig,
    PowerDelivery,
    GameLoopEngine,
    GraphicsQuality,
    MemoryStorage,
    BackgroundProcess,
    NetworkInput
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
    MaximumFps = MaximumPerformance,
    Competitive,
    CompetitiveFps = Competitive,
    StableFps,
    LowEndPC,
    MidRangePC,
    HighEndPC,
    LaptopBattery,
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

public enum BottleneckType
{
    None,
    CpuBottleneck,
    GpuBottleneck,
    RamBottleneck,
    VramBottleneck,
    ThermalBottleneck,
    StorageBottleneck,
    BackgroundProcessBottleneck,
    EmulatorOverheadBottleneck,
    Unknown
}

public enum GameLoopCompatibilityTier
{
    Supported,
    PartiallySupported,
    Unknown
}

public enum PowerSourceState
{
    AcPower,
    Battery,
    Unknown
}

public enum ProcessSafetyCategory
{
    SafeToClose,
    UserApplication,
    SystemProcess,
    CriticalProcess,
    Unknown
}

