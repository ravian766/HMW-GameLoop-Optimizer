namespace GameLoopOptimizer.Models;

/// <summary>
/// A known-good <c>Active.sav</c> shipped in the Assets folder that can be deployed to the VM.
///
/// WHY THESE EXIST
/// ---------------
/// Byte-patching the graphics fields into the existing save did not reliably take effect: the game
/// either discarded the values or clamped them, and the exact mechanism was never fully pinned down
/// (see the long note at the top of PubgQualityMapping for what was ruled out). What *is* proven
/// to work is deploying a complete save file that the game itself accepts.
///
/// Active_UHD_120FPS_v3.sav was verified in-game on 2026-10-11 - Ultra HD and 120 FPS both held
/// across a launch cycle, and the game rewrote its own quality value back into the file, which is
/// the signature of a genuinely adopted value rather than one merely displayed once.
///
/// Additional presets are expected. Dropping another .sav into Assets\ is all that is required;
/// nothing else needs to change.
/// </summary>
public sealed class BundledSaveFile
{
    /// <summary>File name inside the Assets folder, e.g. "Active_UHD_120FPS_v3.sav".</summary>
    public required string FileName { get; init; }

    /// <summary>Absolute path used to read the file.</summary>
    public required string FullPath { get; init; }

    /// <summary>Label shown in the UI, derived from the file name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Size of the file in bytes.</summary>
    public required long Length { get; init; }

    public override string ToString() => DisplayName;
}