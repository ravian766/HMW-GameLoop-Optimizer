namespace GameLoopOptimizer.Models;

public class GameLoopConfig
{
    public bool IsInstalled { get; set; } = false;
    public string InstallPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string UiExecutablePath { get; set; } = string.Empty;
    public string ConfigDirectory { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string TSyzsVersion { get; set; } = string.Empty;
    public string ArchitectureBitness { get; set; } = "32-bit"; // "64-bit" or "32-bit"
    public GameLoopCompatibilityTier CompatibilityTier { get; set; } = GameLoopCompatibilityTier.Supported;
    public string CompatibilityReason { get; set; } = "Standard GameLoop 7.1/32/64-bit";
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
    public GraphicsRenderer ActiveRenderer => ForceDirectX ? GraphicsRenderer.DirectXPlus : GraphicsRenderer.OpenGLPlus;
    public bool EnableGlesv3 { get; set; } = true;
    public bool LocalShaderCacheEnabled { get; set; } = true;
    public bool ShaderCacheEnabled { get; set; } = true;
    public bool RenderOptimizeEnabled { get; set; } = true;
    public int FxaaQuality { get; set; } = 0; // 0=Off, 1=Ultra, 2=Balanced, 3=Close

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
}

