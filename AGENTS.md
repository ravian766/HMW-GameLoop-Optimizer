# Workspace Rules & Development Standards

## 🛑 1. Git & Version Control Policy
- **Never commit or push automatically to Git/GitHub.**
- Always prompt and obtain explicit confirmation from the user before executing any `git commit` or `git push`.
- Format commit messages cleanly following Conventional Commits format (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).

---

## 📁 2. Single Canonical Release Directory
- The one and only output directory for published builds is **`publish/`** (`d:\SourceCode\Gameloop_Opt\publish`).
- Never generate arbitrary new release folders.
- Terminate any running `GameLoopOptimizer` process before publishing to avoid `MSB3027` file lock collisions.

---

## 🧪 3. Quality & Unit Testing
- Run `dotnet test` and ensure all tests pass (0 failures) after every code modification.
- Add unit tests for new features and math calculations.

---

## ⌨️ 4. Keymapping Deterministic Architecture
- Always calibrate keymapping coordinates from the clean 16:9 stock base (`DefaultKeyMapping.stock_16_9.xml`) to prevent compounding drift.
- Synchronize both `ui\DefaultKeyMapping.xml` and `ui\ConfigFile\DefaultKeyMapping.xml`.

---

## 🎨 5. UI & Theming Standards
- Full dynamic resource binding for both Obsidian Cyber Dark and Modern Slate Light modes.
- Keep `🎮 GameLoop & ADB Studio` and `🎯 Stretched Res & Keymaps Studio` cleanly separated.
