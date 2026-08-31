namespace GameLoopOptimizer.Models;

public class GameLoopConfig
{
    public bool IsInstalled { get; set; } = false;
    public string InstallPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Brand { get; set; } = "gameloop";
    public bool IsRunning { get; set; } = false;
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
    public int PubgFpsLevel { get; set; } = 90; // 60, 90, 120
    public int PubgRenderQuality { get; set; } = 2; // 0=Smooth, 1=Balanced, 2=HD, 3=HDR, 4=Ultra HD
    public int PubgContentScale { get; set; } = 1;
    public string DeviceModel { get; set; } = "ROG 2";

    public string RegistryKeyPath { get; set; } = @"Software\Tencent\MobileGamePC";
}
