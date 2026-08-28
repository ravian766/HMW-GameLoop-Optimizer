# Project Rules & Development Workflow

## 1. 🛑 Git & Version Control Policy (MANDATORY)
- **NEVER automatically commit or push to Git/GitHub.**
- Always wait for explicit user instruction or confirmation before running `git add`, `git commit`, or `git push`.
- When work is completed, present the summary of changes and ask the user if they would like to commit and push.
- When committing, use clear, descriptive Conventional Commit messages (e.g., `feat: ...`, `fix: ...`, `docs: ...`, `refactor: ...`).

---

## 2. 📁 Build & Release Standards
- **Single Canonical Publish Directory:** Always publish exclusively to `publish/` (`d:\SourceCode\Gameloop_Opt\publish`).
- **No Temporary Folders:** Never create ad-hoc release folders (e.g. `App-Release`, `Release-Build`, `Publish-Build`).
- **Process Lock Prevention:** Always terminate any running instances of `GameLoopOptimizer.exe` before publishing.
- **Icon Integrity:** Ensure the high-resolution `app_icon.ico` / `app_icon.png` is embedded as the application icon.

---

## 3. 🧪 Testing & Validation Requirements
- Always run `dotnet test` before declaring any task complete or publishing.
- All unit tests must pass with 0 failures before proposing commits or releases.
- Any new optimization module, keymap math change, or theme feature must be accompanied by automated unit tests.

---

## 4. ⌨️ Keymapping & Stretched Resolution Architecture
- **Deterministic 16:9 Base Reference:** Never modify keymap XML files with compounding multipliers. Always calculate transformations fresh from the pristine 16:9 stock base reference (`DefaultKeyMapping.stock_16_9.xml`).
- **Multi-File Synchronization:** Always update both `TxGameAssistant\ui\DefaultKeyMapping.xml` and `TxGameAssistant\ui\ConfigFile\DefaultKeyMapping.xml`.
- **Physical HUD Anchor Math:**
  - Left Anchors: $x' = x \times \frac{1920}{W}$
  - Right Anchors: $x' = 1.0 - (1.0 - x) \times \frac{1920}{W}$
  - Center Anchors: $x' = 0.5 + (x - 0.5) \times \frac{1920}{W}$

---

## 5. 🎨 UI & Design Guidelines
- Support both **Obsidian Cyber Dark** and **Modern Slate Light** themes with `{DynamicResource}` brushes.
- Keep dedicated workspaces focused:
  - `🎮 GameLoop & ADB Studio`: Engine configuration, device model spoofing, in-VM ADB optimizations, and DNS ping tests.
  - `🎯 Stretched Res & Keymaps`: Competitive aspect ratio presets, HUD keymap auto-calibration, Keymap Vault, and mouse benchmarks.
