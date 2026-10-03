namespace GameLoopOptimizer.Core;

/// <summary>
/// Centralized constant definitions for Tencent GameLoop and PUBG Mobile emulator processes.
/// </summary>
public static class GameLoopProcessNames
{
    public const string AppMarket = "AppMarket";
    public const string AndroidEmulator = "AndroidEmulator";
    public const string AndroidEmulatorEn = "AndroidEmulatorEn";
    public const string AndroidEmulatorEx = "AndroidEmulatorEx";
    public const string AowExe = "aow_exe";
    public const string QmEmulatorService = "QMEmulatorService";
    public const string AndroidProcess = "AndroidProcess";
    public const string TbsWebStore = "TBSWebStore";
    public const string TxEx = "TxEx";
    public const string TSettingCenter = "TSettingCenter";
    public const string SyEngine = "SyEngine";
    public const string TxGameAssistant = "TxGameAssistant";

    /// <summary>
    /// Active render engines that execute Android virtualization for PUBG Mobile.
    /// Excludes the desktop store/launcher (AppMarket.exe) so resources are focused on the VM.
    /// </summary>
    public static readonly string[] GameEngines =
    [
        AndroidEmulator,
        AndroidEmulatorEn,
        AndroidEmulatorEx,
        AowExe
    ];

    /// <summary>
    /// All processes belonging to the Tencent GameLoop ecosystem.
    /// </summary>
    public static readonly string[] AllProcesses =
    [
        AppMarket,
        AndroidEmulator,
        AndroidEmulatorEn,
        AndroidEmulatorEx,
        AowExe,
        QmEmulatorService,
        AndroidProcess,
        TbsWebStore,
        TxEx,
        TSettingCenter,
        SyEngine,
        TxGameAssistant
    ];
}
