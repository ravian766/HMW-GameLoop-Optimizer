# AGENTS.md — GameLoop Optimizer

Windows-only .NET 10 WPF app (`net10.0-windows`) that tunes Windows + the GameLoop emulator.
Two projects: `src/GameLoopOptimizer` (WinExe) and `tests/GameLoopOptimizer.Tests` (xUnit, 324 tests, ~3s).

## Commands

```powershell
dotnet build GameLoopOptimizer.sln -c Release
dotnet test tests/GameLoopOptimizer.Tests/GameLoopOptimizer.Tests.csproj -c Release --no-build
dotnet test tests/GameLoopOptimizer.Tests/GameLoopOptimizer.Tests.csproj --filter "FullyQualifiedName~ResolutionKeymapService"
.\build.ps1 -Configuration Release   # build -> test -> publish, in that order
```

- **Order matters:** `--no-build` runs *stale* binaries. Always build after editing before testing with it.
- `build.ps1` hardcodes the absolute publish path `d:/SourceCode/Gameloop_Opt/publish`; it breaks if the repo moves.
- Kill the app before publishing or publish fails with `MSB3027`:
  `Get-Process -Name GameLoopOptimizer -ErrorAction SilentlyContinue | Stop-Process -Force`
- `publish/` is gitignored and is the **only** allowed output dir. Never invent ad-hoc release folders.

## Optimization modules

- Implement `IOptimizationModule` (`src/GameLoopOptimizer/Optimizations/`): `Analyze`/`Apply`/`Rollback`/`Verify`.
- `OptimizationModuleRegistry` discovers modules by reflection and `App.ConfigureServices()` registers
  every one as a singleton. **There is no manual registration step** — do not add one.
- The Optimizer UI groups cards by the `OptimizationCategory` enum (`Models/Enums.cs`); reuse an
  existing category rather than inventing a string.
- `ApplyAsync` must call `BackupManager.RecordBackup(new BackupEntry { ... })`, otherwise the change
  cannot be rolled back — that is the product's core safety promise.

### Trap: `ModuleStructureTests.cs` hand-maintains a module list

It instantiates modules literally and asserts `Assert.Equal(34, modules.Count)`. Adding your module
to that list **without bumping the number fails the build**; not adding it leaves your module untested.
There are currently 36 `*Module.cs` files but only 34 listed — `GpuDriverProfileModule` and
`StandbyListCleanerModule` are missing from the list. Do not "fix" the count to match the file count
without also adding the two entries.

Tests enforce non-empty, unique `Id` plus non-empty `Title`, `Description`, `TechnicalRationale`
and non-null `CurrentStateDisplay`/`RecommendedStateDisplay`.

## Tests have real side effects — read before running them

`BackupManager` is a **static class backed by the user's actual ledger** at
`%LocalAppData%\GameLoopOptimizer\backups.json`. `BackupManager.Clear()` calls `Save()`, so
**running `dotnet test` overwrites the user's real rollback ledger with an empty list.**
`KeymapBackupManager` likewise writes to the real `...\KeymapVault`.

- Any test touching these statics must be marked `[Collection("BackupTests")]` — xUnit runs test
  classes in parallel by default and the shared static/file will race. See `BackupRestoreTests.cs`
  and `Phase1EnhancementsTests.cs` for the pattern.
- Modules are only *instantiated* by tests, never `ApplyAsync`, so no registry/power/GPU state is
  actually changed by the suite.

## Keymapping architecture

The keymap XML files are **not in this repo** — they live in the installed emulator and are written
to 7 locations at once (`GetKeymapFilePaths`). They are multi-root XML, so validation wraps them in
a synthetic root.

- **Never rewrite the keymap with regex.** GameLoop wraps attribute values across physical lines
  (e.g. `<SwitchOperation Description="??` / newline / `" EnableSwitch="SetUp" .../>`), so any
  line- or tag-oriented regex silently corrupts the document. `TransformKeymapXml` parses with
  `XDocument` for this reason — keep it that way.
- **Watch regex capture-group indices when editing the patterns.** `ItemBlockRegex` groups are
  `1=open, 2=Item|ItemEx, 3=apk, 4=inner, 5=close`; `KeyMapModeRegex` is
  `1=open, 2=modeId, 3=inner, 4=close`. Using the wrong index replaces close tags with data.
- `KeymapXmlSanitizer` repairs three pre-existing GameLoop defects in the stock base: a stray
  orphan `</Item>`, duplicate `Raw=` attributes, and unescaped `&`. All three were present in the
  real 2026-07-09 files. `GetStockBaseXmlAsync` now sanitizes **and** re-validates before caching,
  because one corrupt capture used to poison every later calibration.
- Always recompute from the pristine 16:9 base (`DefaultKeyMapping.stock_16_9.xml`). Never apply
  compounding multipliers to an already-stretched keymap; that is cumulative drift.
- If no stock file exists, the code seeds one from the first `DefaultKeyMapping.xml` it finds and
  caches it at `%LocalAppData%\GameLoopOptimizer\stock_16_9_DefaultKeyMapping.xml`. Delete that
  cache to force a clean re-seed.
- `ResolutionKeymapService.ToggleStretchedResolutionAsync` must only log `SUCCESS` when the deploy
  succeeded; it previously overwrote the failure message and claimed "Keymaps synchronized!".

## Theming

- `App.xaml` merges `Styles/ColorsDark.xaml` + `Styles/ModernControls.xaml`.
- `ThemeManager.ApplyTheme` removes any `Colors*` dictionary, inserts the target at index 0, then
  force-copies its keys into `Application.Current.Resources`.
- Use `{DynamicResource}` for theme brushes. `{StaticResource}` will not re-resolve on theme toggle.
- `Styles/Colors.xaml` is legacy; it exists only to be matched by the swap-out filter in `ThemeManager`.

## Git

Never run `git commit` or `git push` without explicit user confirmation. Use Conventional Commits
(`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).

## Deeper guidance

`.agents/skills/` holds task-specific skills — load the relevant one before touching modules,
keymaps, ADB tuning, theming, or releases:

- `optimization-module-architecture` — adding/rolling back a module
- `keymap-stretched-res-studio` — keymap transforms, HUD anchors, Keymap Vault
- `gameloop-adb-tuning` — in-VM ADB optimizations, 120 FPS, device spoofing
- `wpf-theming-and-ui` — XAML, DynamicResource theming, MVVM
- `gameloop-build-and-release` — build, test, publish, release artifacts

`.agents/rules/project_workflow.md` restates the git/publish/test rules.