---
name: keymap-stretched-res-studio
description: >-
  GameLoop keymapping transformation, physical HUD anchor calibration, stretched resolution 
  scaling, Keymap Vault management, and mouse sensitivity tuning.
---

# Keymapping & Stretched Resolution Architecture

This skill provides guidelines and formulas for calibrating GameLoop keymaps, transforming HUD controls for non-16:9 aspect ratios, synchronizing keymap configuration files, and managing Keymap Vault backups.

---

## 1. Keymapping Core Principles

### Deterministic 16:9 Base Reference
* **Never apply compounding multipliers** to an already-stretched keymap.
* Always read baseline coordinates from the clean 16:9 reference base:
  `DefaultKeyMapping.stock_16_9.xml`
* Compute the transformed coordinates fresh for the target aspect ratio ($W \times H$).

### Dual-File Synchronization
GameLoop requires keymaps to be kept in sync across two distinct locations under `TxGameAssistant`:
1. `TxGameAssistant\ui\DefaultKeyMapping.xml`
2. `TxGameAssistant\ui\ConfigFile\DefaultKeyMapping.xml`

Both files must be updated simultaneously whenever keymaps are generated or restored.

---

## 2. Physical HUD Anchor Mathematics

When stretched resolutions (e.g. 1080x1080 [1:1], 1440x1080 [4:3], 1728x1080 [16:10]) are rendered in GameLoop, the Android HUD UI elements anchor differently depending on their screen alignment.

Given:
* Target Aspect Ratio Width $W$ and Height $H$ (with 1080 baseline height)
* Original 16:9 normalized coordinate $x \in [0.0, 1.0]$
* $ScaleFactor = \frac{1920}{W}$

### Anchor Formulas:
* **Left-Anchored Controls** (Movement joystick, bag, sprint):
  $$x' = x \times \frac{1920}{W}$$
* **Right-Anchored Controls** (Fire button, ADS, jump, reload, peek):
  $$x' = 1.0 - (1.0 - x) \times \frac{1920}{W}$$
* **Center-Anchored Controls** (Revive, loot list, vehicle enter/exit):
  $$x' = 0.5 + (x - 0.5) \times \frac{1920}{W}$$
* **Vertical Coordinates ($y$):**
  $$y' = y \quad (\text{remains invariant when height is preserved at 1080p})$$

---

## 3. Supported Resolution Profiles

| Aspect Ratio | Resolution | Common Use Case |
| :--- | :--- | :--- |
| **16:9** | $1920 \times 1080$ | Native default, baseline HUD calibration |
| **16:10** | $1728 \times 1080$ | Balanced competitive stretch |
| **4:3** | $1440 \times 1080$ | Maximum target hit-box width enlargement |
| **5:4** | $1350 \times 1080$ | Aggressive competitive stretch |
| **1:1** | $1080 \times 1080$ | Extreme square resolution stretch |
| **Custom** | User Defined | Dynamic calculation via `ResolutionKeymapService` |

---

## 4. Keymap Vault & Sensitivity Scaling

### Keymap Vault Management
* Vault snapshots are stored with timestamp, resolution tag, and XML payload.
* Always verify backup existence before replacing active keymaps:
  `KeymapBackupManager.CreateBackup("Before_Stretched_Apply")`
* Implement atomic file replacement to prevent partial or corrupted XML writes.

### Mouse Sensitivity & DPI Scaling
When stretching horizontally, the effective horizontal sensitivity increases:
* **X-Axis Sensitivity Adjustment:**
  $$Sens_X' = Sens_X \times \frac{W}{1920}$$
* Maintain Y-Axis sensitivity unchanged ($Sens_Y' = Sens_Y$) to preserve consistent vertical recoil control.

---

## 5. Testing & Validation

Whenever modifying keymapping services or math:
1. Run `dotnet test tests/GameLoopOptimizer.Tests/GameLoopOptimizer.Tests.csproj`
2. Validate `ResolutionKeymapTests.cs` and `AimAndInputTests.cs`.
3. Check XML node validity using `XDocument.Parse()` to ensure well-formed XML structure.
