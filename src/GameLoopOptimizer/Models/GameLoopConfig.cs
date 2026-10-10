namespace GameLoopOptimizer.Models;

public class GameLoopConfig
{
    public bool IsInstalled { get; set; } = false;
    public string InstallPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string UiExecutablePath { get; set; } = string.Empty;
    public string ConfigDirectory { get; set; } = string.Empty;
    /// <summary>User data directory for GameLoop (e.g. %APPDATA%\Tencent\MobileGamePC or InstallPath\UserDir).</summary>
    public string UserDir { get; set; } = string.Empty;
    /// <summary>Path to GameLoop.ini configuration file if located.</summary>
    public string IniFilePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string TSyzsVersion { get; set; } = string.Empty;
    public string ArchitectureBitness { get; set; } = "32-bit"; // "64-bit" or "32-bit"
    public GameLoopCompatibilityTier CompatibilityTier { get; set; } = GameLoopCompatibilityTier.Supported;
    public string CompatibilityReason { get; set; } = "Standard GameLoop 7.0/7.1/32/64-bit";
    public bool IsAnalyzeOnlyMode => CompatibilityTier == GameLoopCompatibilityTier.Unknown;

    public string Brand { get; set; } = "gameloop";
    public bool IsRunning { get; set; } = false;
    public string EmulatorProcessName { get; set; } = string.Empty;
    public IntPtr EmulatorWindowHandle { get; set; } = IntPtr.Zero;
    public List<int> RunningProcessIds { get; set; } = new();

    // Engine settings (stored in HKCU\Software\Tencent\MobileGamePC or HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC)
    public int VmCpuCount { get; set; } = 4;
    public int VmMemorySizeInMb { get; set; } = 4096;
    public int VmResWidth { get; set; } = 1920;
    public int VmResHeight { get; set; } = 1080;
    public int VmDpi { get; set; } = 320;
    public bool VSyncEnabled { get; set; } = false;
    public bool ForceDirectX { get; set; } = true;
    /// <summary>Vulkan rendering forced via registry (GameLoop 7.0.19.05+).</summary>
    public bool ForceVulkan { get; set; } = false;
    /// <summary>Smart Mode auto-renderer selection enabled (GameLoop 7.0.19.05+).</summary>
    public bool SmartModeEnabled { get; set; } = false;
    /// <summary>Numeric RenderingMode from registry: 0=Smart, 1=OpenGL+, 2=DirectX+, 3=Vulkan (GameLoop 7.0.19.05+).</summary>
    public int RenderingMode { get; set; } = -1;
    public GraphicsRenderer ActiveRenderer
    {
        get
        {
            // 7.0.19.05+ uses RenderingMode integer
            if (RenderingMode >= 0)
            {
                return RenderingMode switch
                {
                    0 => GraphicsRenderer.SmartMode,
                    1 => GraphicsRenderer.OpenGLPlus,
                    2 => GraphicsRenderer.DirectXPlus,
                    3 => GraphicsRenderer.Vulkan,
                    _ => ForceDirectX ? GraphicsRenderer.DirectXPlus : GraphicsRenderer.OpenGLPlus
                };
            }
            // Legacy fallback
            if (SmartModeEnabled) return GraphicsRenderer.SmartMode;
            if (ForceVulkan) return GraphicsRenderer.Vulkan;
            return ForceDirectX ? GraphicsRenderer.DirectXPlus : GraphicsRenderer.OpenGLPlus;
        }
    }
    public bool EnableGlesv3 { get; set; } = true;
    public bool LocalShaderCacheEnabled { get; set; } = true;
    public bool ShaderCacheEnabled { get; set; } = true;
    public bool RenderOptimizeEnabled { get; set; } = true;
    public int FxaaQuality { get; set; } = 0; // 0=Off, 1=Ultra, 2=Balanced, 3=Close
    /// <summary>Anti-aliasing mode: 0=Off, 1=FXAA, 2=MSAA 2x, 3=MSAA 4x (GameLoop 7.0.19.05+).</summary>
    public int AntiAliasingMode { get; set; } = 0;
    /// <summary>Vulkan API version string reported by the engine (e.g. "1.3.0").</summary>
    public string VulkanApiVersion { get; set; } = string.Empty;

    // PUBG Mobile specific settings (com.tencent.ig_...)
    public bool IsPubgInstalled { get; set; } = false;
    public string ActivePubgPackage { get; set; } = "com.tencent.ig";
    public string PubgVersion { get; set; } = string.Empty;
    public int PubgFpsLevel { get; set; } = 90; // 60, 90, 120
    public int PubgRenderQuality { get; set; } = 2; // 0=Smooth, 1=Balanced, 2=HD, 3=HDR, 4=Ultra HD
    public int PubgContentScale { get; set; } = 1;
    public bool PubgShadowsEnabled { get; set; } = false;
    public bool PubgAutoAdjustGraphics { get; set; } = false;
    public int PubgAntiAliasing { get; set; } = 0;
    public string DeviceModel { get; set; } = "ROG 2";

    public bool IsAdbAvailable { get; set; } = false;
    public bool IsRootEnabled { get; set; } = false;

    public string RegistryKeyPath { get; set; } = @"Software\Tencent\MobileGamePC";

    /// <summary>Parses Version into a comparable major.minor tuple (e.g. "7.0.19.05" → (7, 0)).</summary>
    public (int Major, int Minor) VersionMajorMinor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Version)) return (0, 0);
            var parts = Version.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            return (major, minor);
        }
    }

    /// <summary>True if GameLoop version is 7.0.19.05 or later.</summary>
    public bool IsVersion70190x => Version != null &&
        (Version.StartsWith("7.0.19.", StringComparison.OrdinalIgnoreCase) ||
         Version.StartsWith("7.0.2", StringComparison.OrdinalIgnoreCase) ||
         Version.StartsWith("7.1", StringComparison.OrdinalIgnoreCase) ||
         VersionMajorMinor.Major > 7 ||
         (VersionMajorMinor.Major == 7 && VersionMajorMinor.Minor >= 1));
}

