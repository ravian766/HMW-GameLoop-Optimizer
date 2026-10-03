namespace GameLoopOptimizer.Core;

public class SafeModeController
{
    private static readonly Lazy<SafeModeController> _instance = new(() => new SafeModeController());
    public static SafeModeController Instance => _instance.Value;

    public bool IsSafeModeActive { get; set; } = false;

    public event EventHandler<bool>? SafeModeChanged;

    public void SetSafeMode(bool enabled)
    {
        if (IsSafeModeActive != enabled)
        {
            IsSafeModeActive = enabled;
            SafeModeChanged?.Invoke(this, enabled);
            Logger.Info("SafeMode", $"Safe Mode is now {(enabled ? "ACTIVE (Analyze & Recommend Only)" : "DISABLED (Normal Operation)")}");
        }
    }
}
