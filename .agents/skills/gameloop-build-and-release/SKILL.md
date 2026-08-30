---
name: gameloop-build-and-release
description: >-
  Build, test, and release engineering guide for GameLoop Optimizer.
  Use when building, running unit tests, publishing binaries to publish/, 
  handling process locks, or preparing release artifacts.
---

# GameLoop Optimizer: Build & Release Workflow

This skill guides you through the exact standards, procedures, and safety checks required to compile, test, and publish the GameLoop Optimizer application.

---

## 1. 🛑 Mandatory Policies & Safeguards

### Git & Version Control Policy
* **NEVER automatically commit or push to Git/GitHub.**
* Always obtain explicit user confirmation before running `git add`, `git commit`, or `git push`.
* Format all commit messages using Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `perf:`).

### Single Canonical Release Directory
* The **only** authorized publish destination is:
  `d:\SourceCode\Gameloop_Opt\publish`
* Never output builds into ad-hoc folders (e.g. `Release-Build`, `Publish-Build`, `App-Release`).

---

## 2. Pre-Publish Process Check

Before executing `dotnet publish`, ensure no running instances of `GameLoopOptimizer.exe` are holding file locks (`MSB3027` build collision).

```powershell
# Terminate running GameLoopOptimizer instances safely
Get-Process -Name "GameLoopOptimizer" -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## 3. Standard Build & Test Procedures

### Step 1: Solution Restore & Compilation
```powershell
dotnet build GameLoopOptimizer.sln -c Release
```

### Step 2: Automated Test Execution
Run the full test suite and ensure all tests pass with **0 failures**:
```powershell
dotnet test tests/GameLoopOptimizer.Tests/GameLoopOptimizer.Tests.csproj -c Release --no-build --verbosity normal
```
*If any test fails, do not proceed to publish. Investigate and resolve all failing tests first.*

### Step 3: Canonical Publishing
Publish the application bundle directly into `publish/`:
```powershell
dotnet publish src/GameLoopOptimizer/GameLoopOptimizer.csproj -c Release -r win-x64 --self-contained false -o d:/SourceCode/Gameloop_Opt/publish
```

Alternatively, you can run the root build script which handles these steps sequentially:
```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```

---

## 4. Verification Checklist

After publishing:
1. Verify `d:\SourceCode\Gameloop_Opt\publish\GameLoopOptimizer.exe` exists and has an updated timestamp.
2. Confirm `app_icon.ico` / `app_icon.png` is properly bundled and rendered on the executable.
3. Confirm all required dependencies (`.dll`, `appsettings.json`, assets) are present in `publish/`.
4. Report the build status and summary of changes to the user.
