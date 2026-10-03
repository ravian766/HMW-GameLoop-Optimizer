using System.IO;
using GameLoopOptimizer.Models;

namespace GameLoopOptimizer.Core;

public static class GameLoopCompatibilityManager
{
    public static void EvaluateCompatibility(GameLoopConfig config)
    {
        if (!config.IsInstalled)
        {
            config.CompatibilityTier = GameLoopCompatibilityTier.Unknown;
            config.CompatibilityReason = "GameLoop installation not detected.";
            return;
        }

        // Determine executable bitness (64-bit vs 32-bit)
        string exePath = !string.IsNullOrEmpty(config.ExecutablePath) ? config.ExecutablePath : GameLoopDetector.FindGameLoopExePath();
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            config.ExecutablePath = exePath;
            config.ArchitectureBitness = DetectBitness(exePath);
        }

        // Evaluate version compatibility
        string version = config.Version?.Trim() ?? string.Empty;
        string syzsVersion = config.TSyzsVersion?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(syzsVersion))
        {
            version = syzsVersion;
        }

        if (version.StartsWith("3.", StringComparison.OrdinalIgnoreCase))
        {
            config.CompatibilityTier = GameLoopCompatibilityTier.PartiallySupported;
            config.CompatibilityReason = $"Legacy GameLoop 3.x detected. Some modern DirectX+/Direct3D rendering optimizations may operate in legacy fallback mode.";
        }
        else if (version.StartsWith("7.1", StringComparison.OrdinalIgnoreCase) ||
            version.StartsWith("7.0", StringComparison.OrdinalIgnoreCase) ||
            version.StartsWith("1.0", StringComparison.OrdinalIgnoreCase) || // Syzs 1.0.x
            version.Contains("gameloop", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(config.InstallPath))
        {
            config.CompatibilityTier = GameLoopCompatibilityTier.Supported;
            config.CompatibilityReason = $"GameLoop ({config.ArchitectureBitness}) version '{version}' is fully supported for precision tuning.";
        }
        else
        {
            config.CompatibilityTier = GameLoopCompatibilityTier.Unknown;
            config.CompatibilityReason = $"Unrecognized or experimental GameLoop version ('{version}'). Operating in safe analyze-only mode to prevent configuration corruption.";
        }

        Logger.Info("GameLoopCompatibility", $"Compatibility evaluated: {config.CompatibilityTier} ({config.CompatibilityReason})");
    }

    public static string DetectBitness(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);

            // Read DOS Header
            if (reader.ReadUInt16() != 0x5A4D) // "MZ"
                return "32-bit";

            stream.Seek(0x3C, SeekOrigin.Begin);
            int e_lfanew = reader.ReadInt32();

            stream.Seek(e_lfanew, SeekOrigin.Begin);
            if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
                return "32-bit";

            ushort machine = reader.ReadUInt16();
            if (machine == 0x8664) // IMAGE_FILE_MACHINE_AMD64
                return "64-bit";
            if (machine == 0x014c) // IMAGE_FILE_MACHINE_I386
                return "32-bit";
            if (machine == 0xAA64) // IMAGE_FILE_MACHINE_ARM64
                return "ARM64";
        }
        catch (Exception ex)
        {
            Logger.Warn("GameLoopCompatibility", $"PE header bitness check failed: {ex.Message}");
        }

        return "32-bit";
    }
}
