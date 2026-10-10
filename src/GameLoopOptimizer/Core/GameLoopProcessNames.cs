namespace GameLoopOptimizer.Core;

/// <summary>
/// Centralized constant definitions for Tencent GameLoop and PUBG Mobile emulator processes.
/// Updated for GameLoop 7.0.19.05 engine processes.
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
    /// <summary>GameLoop 7.0.19.05+ Vulkan/rendering pipeline manager.</summary>
    public const string GameLoopRenderer = "GameLoopRenderer";
    /// <summary>GameLoop 7.0.19.05+ unified service coordinator.</summary>
    public const string GameLoopService = "GameLoopService";
    /// <summary>GameLoop 7.0.19.05+ main emulator engine execution process.</summary>
    public const string GameLoopEmulator = "GameLoopEmulator";
    /// <summary>GameLoop 7.0.19.05+ virtual machine hypervisor process.</summary>
    public const string GameLoopVm = "GameLoopVm";
    /// <summary>GameLoop 7.0.19.05+ primary emulator runtime.</summary>
    public const string GameLoop = "GameLoop";
    /// <summary>GameLoop 7.0.19.05+ launcher shell process.</summary>
    public const string GameLoopLauncher = "GameLoopLauncher";
    /// <summary>GameLoop 7.0.19.05+ assistant background process.</summary>
    public const string GameLoopAssistant = "GameLoopAssistant";
    /// <summary>GameLoop 7.0.19.05+ download service process.</summary>
    public const string GameLoopDldSvr = "GameLoopDldSvr";
    /// <summary>GameLoop 7.0.19.05+ virtual filesystem service.</summary>
    public const string GameLoopVfs = "GameLoopVfs";
    /// <summary>GameLoop 7.0.19.05+ anti-cheat helper module.</summary>
    public const string TP3Helper = "TP3Helper";

    /// <summary>
    /// Active render engines that execute Android virtualization for PUBG Mobile.
    /// Excludes the desktop store/launcher so resources are focused on the VM.
    /// </summary>
    public static readonly string[] GameEngines =
    [
        AndroidEmulator,
        AndroidEmulatorEn,
        AndroidEmulatorEx,
        AowExe,
        GameLoopRenderer,   // 7.0.19.05+ Vulkan render process
        GameLoopEmulator,   // 7.0.19.05+ Primary engine process
        GameLoopVm,         // 7.0.19.05+ Hypervisor VM worker
        GameLoop            // 7.0.19.05+ Core runtime
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
        TxGameAssistant,
        GameLoopRenderer,   // 7.0.19.05+
        GameLoopService,    // 7.0.19.05+
        GameLoopEmulator,   // 7.0.19.05+
        GameLoopVm,         // 7.0.19.05+
        GameLoop,           // 7.0.19.05+
        GameLoopLauncher,   // 7.0.19.05+
        GameLoopAssistant,  // 7.0.19.05+
        GameLoopDldSvr,     // 7.0.19.05+
        GameLoopVfs,        // 7.0.19.05+
        TP3Helper           // 7.0.19.05+
    ];
}
