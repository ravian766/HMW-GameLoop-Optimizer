# GameLoop & Windows Performance Optimizer

A lightweight, modern Windows desktop utility designed to optimize the Windows environment and GameLoop emulator configuration for smoother PUBG Mobile gameplay, reducing stutters, frame-time spikes, unnecessary background activity, and input latency.

---

## 🛡️ Fair Play & Safety Guarantee

This application is strictly an **operating system and emulator configuration utility**:
- **NO Game File Modification**: Does not alter, replace, or touch PUBG Mobile game files or APK packages.
- **NO DLL Injection / Memory Tampering**: Does not inject code or hook memory of GameLoop or the game.
- **NO Anti-Cheat Bypass**: Operates completely outside the Android emulator sandbox.
- **NO Cheats or Automation**: Pure performance and resource management.
- **100% Reversible**: Every modification stores a pre-apply snapshot in an audit ledger for single-click individual or full system rollback.

---

## ✨ Features & Architecture

### 1. Dynamic Hardware-Aware Recommendation Engine
Automatically detects your hardware components and dynamically calculates the optimal resource allocation rather than applying hardcoded values:
- **CPU Thread Balancing**:
  - `4 Threads`: Allocates 2 cores to GameLoop (leaving 2 threads for host audio/OS).
  - `6–8 Threads` (e.g. Intel Core i3-12100F, Ryzen 5600): Allocates 4 cores to GameLoop.
  - `12+ Threads`: Allocates 4 cores (avoids Android thread scheduler lock contention).
- **RAM Allocation**:
  - `8 GB Host RAM`: Allocates 4096 MB to GameLoop (leaves 4 GB for OS).
  - `16 GB Host RAM`: Allocates 8192 MB to GameLoop (leaves 8 GB for OS and background apps).
  - `32+ GB Host RAM`: Allocates 8192 MB (optimal Android VM cap).
- **Graphics Pipeline**: Configures DirectX+ hardware rasterization, local shader caching, and low-latency V-Sync off.

### 2. 12 Independent Optimization Modules
Every module implements `IOptimizationModule` with independent analysis, application, verification, and rollback logic:
1. **Windows Game Mode**: Prioritizes CPU/GPU scheduling for the active emulator process while suppressing background Windows updates.
2. **High-Performance Power Delivery**: Switches to High/Ultimate performance power scheme, unparking CPU cores.
3. **GameLoop CPU & RAM Allocation**: Dynamically writes optimal core/RAM keys to `HKCU\Software\Tencent\MobileGamePC`.
4. **DirectX+ & Persistent Shader Cache**: Eliminates shader compilation stutters (1% lows).
5. **PUBG Mobile 90/120 FPS & Device Profile**: Sets high-refresh device profile (ROG Phone 2) to unlock 90/120 FPS in PUBG Mobile settings.
6. **RAM Working Set & Cache Trim**: Flushes idle background process working sets safely via `EmptyWorkingSet`.
7. **Safe Temp & Emulator Cache Cleanup**: Cleans obsolete download chunks and temp caches without touching assets.
8. **0.5ms High-Precision Multimedia Timer**: Increases Windows timer frequency from 15.6ms to 0.5ms–1.0ms for lower frame-time variance.
9. **Emulator Process Priority Boost**: Elevates `AndroidEmulator.exe` / `aow_exe.exe` to `Above Normal` priority.
10. **Network Latency & TCP ACK Tuning**: Configures `TcpAckFrequency = 1` and `TCPNoDelay = 1` for lower packet jitter.
11. **Windows Animation & DWM Overhead Check**: Minimizes non-essential UI animations to free GPU compositing queues.
12. **Background App Overhead Throttle**: Lowers CPU scheduling priority of resource-heavy background applications during gaming sessions without killing them.

### 3. Optimization Scoring (0–100)
Transparently grades your system and emulator state across 6 weighted categories:
- Windows Configuration (20 pts)
- Power Delivery (15 pts)
- GameLoop Virtual Engine (25 pts)
- DirectX+ & Shader Caching (20 pts)
- Memory & Storage Health (10 pts)
- Background Process Overhead (10 pts)

### 4. Real-Time Telemetry & Live Graphs
- 1-second interval rolling monitoring for:
  - CPU Total % & Per-Core Load
  - GPU 3D Rasterizer Utilization %
  - Physical RAM Usage % & Available Memory (GB)
  - Disk I/O Throughput (MB/s)
  - GameLoop Specific RAM Footprint (MB)
  - Estimated Frame-Time Variance Index

### 5. Gaming Session Controller
- **Start Gaming Session**: Snapshots system, arms 0.5ms high-precision timer, boosts emulator priority, trims idle memory, throttles background apps, and focuses/launches GameLoop.
- **End Gaming Session**: Automatically restores standard timer resolution, process priorities, and background app states, providing a comprehensive session performance report.

---

## 🚀 Building & Running

### Requirements
- **OS**: Windows 10 (1809+) or Windows 11 (64-bit)
- **Runtime / SDK**: .NET 10 SDK (or .NET 10 Desktop Runtime)

### Quick Build & Publish
Run the automated build script from PowerShell:
```powershell
.\build.ps1
```
The compiled executable will be in:
```
publish/GameLoopOptimizer.exe
```

### Running Unit Tests
```powershell
dotnet test
```

---

## 🔄 Reversibility & Rollback Ledger
All modifications are recorded to `%LocalAppData%\GameLoopOptimizer\backups.json`.
- **Restore All**: Click "Restore All" in the Optimizer or Backup tab to return every setting to its exact previous state.
- **Individual Rollback**: Click "Rollback" on any card or history item to revert a single setting.

---

## 🗑️ Safe Uninstall Procedure
1. Open the application, navigate to **Backup & Rollback**, and click **Restore All Modifications**.
2. Close the application.
3. Delete the application directory.
4. (Optional) Delete log and backup history from `%LocalAppData%\GameLoopOptimizer`.
