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

### 2. 16 Independent Optimization Modules
Every module implements `IOptimizationModule` with independent analysis, application, verification, and rollback logic:
1. **Windows Game Mode**: Prioritizes CPU/GPU scheduling for the active emulator process while suppressing background Windows updates.
2. **High-Performance Power Delivery**: Switches to High/Ultimate performance power scheme, unparking CPU cores.
3. **GameLoop CPU & RAM Allocation**: Dynamically writes optimal core/RAM keys to `HKCU\Software\Tencent\MobileGamePC`.
4. **DirectX+ & Persistent Shader Cache**: Eliminates shader compilation stutters (1% lows).
5. **PUBG Mobile 90/120 FPS & Device Profile**: Sets high-refresh device profile (ROG Phone 2) to unlock 90/120 FPS in PUBG Mobile settings.
6. **P-Core / High-Performance Core Affinity**: Locks GameLoop worker threads to Performance Cores (P-Cores), preventing thread allocation to low-clock Efficiency Cores.
7. **Windows Discrete GPU Preference Enforcer**: Enforces `GpuPreference=2;` for all GameLoop executables to guarantee dedicated NVIDIA/AMD GPU rendering.
8. **Low-Latency WASAPI Audio**: Sets `audioRenderType=1` to eliminate gunshot sound delay.
9. **RAM Working Set & Cache Trim**: Flushes idle background process working sets safely via `EmptyWorkingSet`.
10. **Safe Temp & Emulator Cache Cleanup**: Cleans obsolete download chunks and temp caches without touching assets.
11. **0.5ms High-Precision Multimedia Timer**: Increases Windows timer frequency from 15.6ms to 0.5ms–1.0ms for lower frame-time variance.
12. **Emulator Process Priority Boost**: Elevates `AndroidEmulator.exe` / `aow_exe.exe` to `Above Normal` priority.
13. **Network Latency & TCP ACK Tuning**: Configures `TcpAckFrequency = 1` and `TCPNoDelay = 1` for lower packet jitter.
14. **Cloudflare Low-Latency Gaming DNS**: Configures fast Anycast DNS (`1.1.1.1` & `1.0.0.1`) and flushes DNS cache.
15. **Windows Animation & DWM Overhead Check**: Minimizes non-essential UI animations to free GPU compositing queues.
16. **Background App Overhead Throttle**: Lowers CPU scheduling priority of resource-heavy background applications during gaming sessions without killing them.

### 3. Keymap & Sensitivity Profile Vault
- **1-Click Backup & Restore**: Snapshot custom keymappings and sensitivity configurations from `TxGameAssistant` to timestamped archives, protecting your controls against emulator update resets.

### 4. Gaming DNS & Multi-Region Ping Benchmark
- **Server Latency Radar**: Benchmark real-time ICMP ping and jitter to regional PUBG game servers (Middle East, Europe, Asia, North America).
- **DNS Resolver Flush**: 1-click Windows resolver cache cleaner.

### 5. Auto-Gaming Watchdog & Post-Gaming Maintenance
- **Auto Launch Detection**: Daemon detects GameLoop startup and auto-engages 0.5ms timer resolution, priority boost, and P-core affinity.
- **Post-Game Maintenance**: Automatically restores normal timers, cleans RAM working sets, and resets affinity upon GameLoop exit.

### 6. HUD In-Game Stutter Radar (Ctrl+Shift+O)
- Mini draggable overlay with live CPU %, RAM %, GameLoop MB, and real-time **Stutter & Jitter Radar** health badge.

---

## 📖 Stretched Resolution Clarity & Keymapping Guide
For in-depth competitive setup on 4:3 stretched aspect ratios (`1440x1080`, `1728x1080`, `1080x1080`), GPU image sharpening, and DPI scaling, read the complete guide:
- [🎯 Stretched Resolution Visual Clarity & Image Enhancement Guide](docs/STRETCHED_RESOLUTION_CLARITY_GUIDE.md)

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
