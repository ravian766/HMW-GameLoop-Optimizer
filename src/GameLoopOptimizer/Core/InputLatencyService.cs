using System.Runtime.InteropServices;
using GameLoopOptimizer.Models;
using Microsoft.Win32;

namespace GameLoopOptimizer.Core;

public class InputLatencyAuditResult
{
    public bool IsPointerPrecisionActive { get; set; } = false;
    public string MouseSpeed { get; set; } = "1";
    public bool IsVSyncActive { get; set; } = false;
    public int DisplayRefreshRateHz { get; set; } = 60;
    public int TargetFps { get; set; } = 60;
    public double EstimatedInputDelayMs { get; set; } = 16.6;
    public string Rating { get; set; } = "Good";
    public List<string> Recommendations { get; set; } = new();
}

public static class InputLatencyService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

    private const uint SPI_GETMOUSE = 0x0003;
    private const uint SPI_SETMOUSE = 0x0004;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public static InputLatencyAuditResult Audit(HardwareInfo hw, GameLoopConfig gl)
    {
        var result = new InputLatencyAuditResult
        {
            DisplayRefreshRateHz = hw.RefreshRateHz,
            TargetFps = gl.PubgFpsLevel > 0 ? gl.PubgFpsLevel : 60,
            IsVSyncActive = gl.VSyncEnabled
        };

        // 1. Detect Windows Pointer Precision (Mouse Acceleration)
        try
        {
            int[] mouseParams = new int[3];
            if (SystemParametersInfo(SPI_GETMOUSE, 0, mouseParams, 0))
            {
                result.IsPointerPrecisionActive = mouseParams[2] != 0;
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
                if (key != null)
                {
                    var speed = key.GetValue("MouseSpeed")?.ToString() ?? "0";
                    result.MouseSpeed = speed;
                    result.IsPointerPrecisionActive = speed != "0";
                }
            }
        }
        catch
        {
            result.IsPointerPrecisionActive = false;
        }

        // 2. Calculate Theoretical Input Pipeline Delay
        // Frame time delay = 1000 / FPS + (VSync ? 1.5 frame buffers : 0.5 frame buffer) + display scanout delay
        double frameDelayMs = result.TargetFps > 0 ? 1000.0 / result.TargetFps : 16.6;
        double vsyncPenaltyMs = result.IsVSyncActive ? (frameDelayMs * 1.5) : 0;
        double scanoutMs = result.DisplayRefreshRateHz > 0 ? 1000.0 / result.DisplayRefreshRateHz : 16.6;

        result.EstimatedInputDelayMs = Math.Round(frameDelayMs + vsyncPenaltyMs + (scanoutMs / 2.0), 1);

        // 3. Generate Competitive Recommendations
        if (result.IsPointerPrecisionActive)
        {
            result.Recommendations.Add("Disable 'Enhance pointer precision' in Windows mouse settings for true 1:1 linear raw aiming.");
        }

        if (result.IsVSyncActive)
        {
            result.Recommendations.Add("Disable V-Sync in GameLoop settings to prevent 15-30ms render queue input latency penalty.");
        }

        if (hw.MaxRefreshRateHz > hw.RefreshRateHz)
        {
            result.Recommendations.Add($"Your display supports {hw.MaxRefreshRateHz} Hz but is currently set to {hw.RefreshRateHz} Hz. Elevating refresh rate reduces display latency by {(1000.0/hw.RefreshRateHz - 1000.0/hw.MaxRefreshRateHz):F1}ms.");
        }

        if (result.TargetFps < 90 && hw.CalculatedTier != HardwareTier.LowEnd)
        {
            result.Recommendations.Add("Unlock 90 FPS or 120 FPS in PUBG Mobile to reduce frame generation input latency to under 11ms.");
        }

        // 4. Rating
        if (result.EstimatedInputDelayMs <= 18.0 && !result.IsPointerPrecisionActive && !result.IsVSyncActive)
        {
            result.Rating = "Pro Esports Ready (Sub-18ms Pipeline)";
        }
        else if (result.EstimatedInputDelayMs <= 30.0)
        {
            result.Rating = "Good Responsiveness";
        }
        else
        {
            result.Rating = "High Latency (Noticeable Input Lag)";
        }

        Logger.Info("InputLatencyService", $"Audited: Delay ~{result.EstimatedInputDelayMs}ms, PointerPrecision: {result.IsPointerPrecisionActive}, VSync: {result.IsVSyncActive}, Rating: {result.Rating}");
        return result;
    }

    public static bool SetMousePointerPrecision(bool enable)
    {
        try
        {
            int[] mouseParams = new int[3] { 0, 0, enable ? 1 : 0 };
            bool res = SystemParametersInfo(SPI_SETMOUSE, 0, mouseParams, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true);
            if (key != null)
            {
                key.SetValue("MouseSpeed", enable ? "1" : "0", RegistryValueKind.String);
                key.SetValue("MouseThreshold1", enable ? "6" : "0", RegistryValueKind.String);
                key.SetValue("MouseThreshold2", enable ? "10" : "0", RegistryValueKind.String);
            }

            Logger.Success("InputLatencyService", $"Windows Enhance pointer precision set to: {(enable ? "Enabled" : "Disabled (1:1 Raw Input)")}");
            return res;
        }
        catch (Exception ex)
        {
            Logger.Error("InputLatencyService", $"Failed to update mouse pointer precision: {ex.Message}");
            return false;
        }
    }
}
