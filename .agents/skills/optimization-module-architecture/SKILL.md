---
name: optimization-module-architecture
description: >-
  Developing, registering, and unit testing optimization modules implementing IOptimizationModule, 
  managing registry/system state rollbacks with BackupManager, and scoring heuristic integrations.
---

# Optimization Module & Testing Architecture

This skill provides guidelines and patterns for developing, registering, and testing system/GameLoop optimization modules within the GameLoop Optimizer architecture.

---

## 1. The `IOptimizationModule` Contract

Every optimization module in `src/GameLoopOptimizer/Optimizations/` must implement `IOptimizationModule`:

```csharp
public interface IOptimizationModule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; }      // "Windows", "GameLoop", "GPU", "Network", "Audio", "ADB"
    bool IsApplied { get; }
    bool RequiresAdmin { get; }
    int PerformanceImpactScore { get; } // 1-10 rating for ScoringEngine

    Task<bool> ApplyAsync();
    Task<bool> RestoreAsync();
    Task<bool> CheckStatusAsync();
}
```

---

## 2. Safety, Registry & Rollback Standards

### Registry Safety (`Microsoft.Win32.Registry`)
* When modifying Windows Registry values (`HKCU`, `HKLM`), always save the original values before modification via `BackupManager`.
* If a registry value did not exist previously, record a delete instruction for clean `RestoreAsync()` behavior.
* Wrap all registry writes in try/catch blocks with detailed logging via `Logger.LogError()`.

```csharp
// Example pattern for safe registry modification with backup
public async Task<bool> ApplyAsync()
{
    try
    {
        // 1. Snapshot previous state
        var currentValue = Registry.GetValue(KeyPath, ValueName, null);
        BackupManager.RecordOriginalValue(Id, KeyPath, ValueName, currentValue);

        // 2. Apply new optimized value
        Registry.SetValue(KeyPath, ValueName, 1, RegistryValueKind.DWord);
        return true;
    }
    catch (Exception ex)
    {
        Logger.LogError($"Failed to apply {Name}", ex);
        return false;
    }
}
```

---

## 3. Integration with Engines

### Scoring Engine (`ScoringEngine.cs`)
* Modules contribute to the overall Performance Score (0 - 100).
* Ensure `PerformanceImpactScore` reflects real-world latency/FPS impact (e.g. GPU preference: 8, Timer resolution: 9, DEX compile: 8).

### Recommendation Engine (`RecommendationEngine.cs`)
* Check detected hardware (`HardwareDetector.cs` - e.g. Low RAM, NVIDIA vs AMD, Intel Core count) before recommending modules.
* Avoid recommending optimizations that conflict with specific hardware profiles.

---

## 4. Automated Testing Requirements

All modules must be covered by automated unit tests in `tests/GameLoopOptimizer.Tests/`:
1. **Idempotency:** Applying an already applied module must succeed safely without corruption.
2. **Restoration:** Restoring an unapplied or applied module must return the system to its initial baseline.
3. **Status Check:** `CheckStatusAsync()` should accurately reflect the active state.

```csharp
[Fact]
public async Task OptimizationModule_ApplyAndRestore_MaintainsIntegrity()
{
    var module = new CustomOptimizationModule();
    
    // Act & Assert
    var applyResult = await module.ApplyAsync();
    Assert.True(applyResult);
    Assert.True(module.IsApplied);

    var restoreResult = await module.RestoreAsync();
    Assert.True(restoreResult);
    Assert.False(module.IsApplied);
}
```

Run test suite via:
```powershell
dotnet test tests/GameLoopOptimizer.Tests/GameLoopOptimizer.Tests.csproj -c Release --verbosity normal
```
