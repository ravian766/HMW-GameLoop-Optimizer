---
name: gameloop-adb-tuning
description: >-
  In-VM Android subsystem tuning, GameLoop ADB connection discovery, 120 FPS unlock, 
  DEX compilation, Dalvik VM heap optimization, low-latency audio/input, and device model spoofing.
---

# GameLoop ADB & Android Subsystem Optimization

This skill provides guidelines and commands for interacting with GameLoop's internal Android Virtual Machine via ADB (Android Debug Bridge), executing low-latency optimizations, unlocking FPS, and spoofing device profiles.

---

## 1. ADB Connection & Daemon Discovery

GameLoop exposes its internal Android container over localhost with dynamic or standard ports:
* **Common Ports:** `127.0.0.1:5555`, `127.0.0.1:6555`, `127.0.0.1:5554`
* **Discovery Method:** `AdbManager.DetectAdbPort()` inspects registry keys (`AppMarket\AERegistry`) or scans active listening ports.

### Connection Workflow
```powershell
# Verify ADB server and connection
adb.exe kill-server
adb.exe start-server
adb.exe connect 127.0.0.1:<DetectedPort>
adb.exe devices
```

---

## 2. In-VM Optimization Modules

### A. 120 FPS & Display Refresh Unlock
Forces Android SurfaceFlinger and WindowManager to allow high refresh rate rendering:
```bash
setprop debug.sf.showfps 0
setprop debug.sf.swaprect 1
setprop debug.egl.hw 1
setprop debug.sf.nobootanimation 1
setprop persist.sys.display.rate 120
```

### B. Dalvik VM Heap & GC Tuning
Reduces garbage collector hitches and increases memory ceiling:
```bash
setprop dalvik.vm.heapgrowthlimit 512m
setprop dalvik.vm.heapsize 1024m
setprop dalvik.vm.heaptargetutilization 0.75
setprop dalvik.vm.heapminfree 2m
setprop dalvik.vm.heapmaxfree 16m
```

### C. Ahead-of-Time (AOT) DEX Compilation
Pre-compiles game bytecode to native machine code, eliminating runtime JIT stuttering:
```bash
cmd package compile -m speed -f com.tencent.ig
cmd package compile -m speed -f com.pubg.krmobile
cmd package compile -m speed -f com.vng.pubgmobile
```

### D. Low-Latency Input & Touch Boost
Optimizes touch event queues and polling responsiveness:
```bash
setprop persist.sys.touch.pressure.scale 0.001
setprop persist.sys.input.boost 1
setprop debug.input.touch_boost 1
setprop persist.sys.pointer_velocity 1
```

### E. Audio Buffer & Latency Tuning
Decreases Android audio buffer latency to ensure real-time footstep clarity:
```bash
setprop af.resampler.quality 4
setprop persist.audio.vr.enable 0
setprop persist.audio.lowlatency 1
```

### F. Logcat & Debugging Telemetry Suppression
Suppresses heavy I/O overhead caused by continuous background Android logging:
```bash
setprop logd.logcat.enable 0
setprop logcat.live false
stop logd
```

---

## 3. Device Model & Manufacturer Spoofing

Certain games (e.g. PUBG Mobile) limit graphic settings (90 FPS / 120 FPS / Extreme+ / Ultra HD) based on detected device model strings.

### Recommended Profiles
| Profile | Model (`ro.product.model`) | Manufacturer (`ro.product.manufacturer`) | Brand (`ro.product.brand`) |
| :--- | :--- | :--- | :--- |
| **ROG Phone 6 Pro** | `ASUS_AI2201_D` | `asus` | `asus` |
| **iPad Pro 11** | `iPad13,4` | `Apple` | `Apple` |
| **Samsung S23 Ultra**| `SM-S918B` | `samsung` | `samsung` |

### Setting Spoofing Properties
```bash
setprop ro.product.model <TargetModel>
setprop ro.product.manufacturer <TargetManufacturer>
setprop ro.product.brand <TargetBrand>
```

---

## 4. Safety & Rollback Guidelines

1. **Timeout Handling:** Every ADB command must execute with a cancellation token and timeout (max 5-10s) to prevent UI thread hangs on unresponsive emulator instances.
2. **State Tracking:** Record previously applied properties before modification to allow clean `Restore()` cycles in `IOptimizationModule`.
3. **Automated Tests:** Validate ADB modules via `tests/GameLoopOptimizer.Tests/AdbOptimizationTests.cs` and `AdbEnhancementsTests.cs`.
