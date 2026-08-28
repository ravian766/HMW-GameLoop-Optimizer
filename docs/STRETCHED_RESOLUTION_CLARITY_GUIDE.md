# 🎯 Stretched Resolution Visual Clarity & Image Enhancement Guide

> **Target Audience:** Competitive GameLoop & PUBG Mobile players using stretched aspect ratios (4:3 `1440x1080`, 16:10 `1728x1080`, 1:1 `1080x1080`, 4:3 `1280x960`).
>
> **Goal:** Eliminate horizontal stretching blur, pixel jaggies, and blurry reticles while maintaining widened enemy hitboxes and high FPS.

---

## 1. ⚡ GPU Driver Hardware Sharpening (Essential)

Stretching 1440px across a 1920px physical monitor introduces linear scaling interpolation blur. Applying hardware-level GPU image sharpening restores crisp polygon edges and distant player silhouettes with **zero input lag or FPS penalty**.

### 🟢 NVIDIA Graphics (GeForce GTX / RTX)
1. Right-click Desktop $\rightarrow$ Open **NVIDIA Control Panel**.
2. Navigate to **3D Settings** $\rightarrow$ **Manage 3D Settings** $\rightarrow$ **Program Settings** (select `AndroidEmulator.exe` or `Global`).
3. Set **Image Scaling / Image Sharpening** to:
   - **Sharpen:** `55% – 65%`
   - **Ignore Film Grain:** `15%`
4. Click **Apply**.

### 🔴 AMD Radeon Graphics (RX Series)
1. Open **AMD Software: Adrenalin Edition** $\rightarrow$ **Gaming** $\rightarrow$ **Graphics**.
2. Toggle **Radeon Image Sharpening (RIS)** to **Enabled**.
3. Set the **Sharpness Slider** to `75% – 85%`.
4. Optionally enable **Radeon Anti-Lag** for lower input latency.

### 🔵 Intel Arc & Iris Xe Graphics
1. Open **Intel Graphics Command Center**.
2. Go to **Display / 3D Settings** $\rightarrow$ Enable **Sharpness Enhancer**.
3. Set level to `60% – 70%`.

---

## 2. 📱 Android Subsystem DPI Calibration (320 – 400 DPI)

Low DPI settings (e.g., 240 DPI or 160 DPI) force the Android container to rasterize UI elements, crosshairs, scope reticles, and fonts at low resolutions.

| Stretched Preset | Resolution | Optimal Target DPI | Visual Result |
| :--- | :--- | :--- | :--- |
| **4:3 Competitive** | `1440 x 1080` | **320 DPI** | Crisp HUD text, ultra-sharp red dot / holo reticles |
| **16:10 Pro** | `1728 x 1080` | **320 DPI** | Razor-sharp minimap & loot labels |
| **1:1 Close Quarters**| `1080 x 1080` | **320 / 400 DPI** | High definition icons under heavy horizontal stretch |
| **4:3 Budget Tier** | `1280 x 960` | **240 / 320 DPI** | Balanced clarity on lower-end VRAM |
| **16:9 QHD** | `2560 x 1440` | **400 DPI** | Native 2K ultra-crisp fidelity |

> 💡 *In **HMW GameLoop Optimizer**, set the **Target Screen DPI** slider/textbox to `320` or `400` in the **🎯 Stretched Res & Keymaps** tab.*

---

## 3. 🖥️ GPU-Side Display Scaling

Ensure your graphics card processes the horizontal screen expansion rather than relying on your monitor's internal scaler (which introduces processing lag and scaling blur).

### NVIDIA Control Panel Setup:
1. Go to **Display** $\rightarrow$ **Adjust desktop size and position**.
2. Select **Scaling Mode**: `Full-screen`.
3. Select **Perform scaling on**: **`GPU`**.
4. Check the box: **`Override the scaling mode set by games and programs`**.

### AMD Radeon Software Setup:
1. Go to **Gaming** $\rightarrow$ **Display**.
2. Set **GPU Scaling**: `Enabled`.
3. Set **Scaling Mode**: `Full panel`.

---

## 4. 🎮 GameLoop Internal Render Super-Sampling (2K Downsampling)

You can force GameLoop to render 3D game models in **2K (QHD)** internally while displaying them through your 4:3 `1440x1080` viewport. This acts as built-in Super-Sample Anti-Aliasing (SSAA):

1. In GameLoop $\rightarrow$ Click the **Hamburger Menu (≡)** $\rightarrow$ **Settings**.
2. Go to the **Game** Tab:
   - **Game Resolution:** Set to **HD 1080P** (or **Ultra HD 2K** if you have a GTX 1660 / RTX 2060 or better).
   - **Display Quality:** Set to **HD** or **Auto**.
3. In the **Engine** Tab:
   - **Screen Rendering Mode:** `DirectX+` (for lowest frame-time spikes on Windows 10/11).
   - **Enable local shader cache:** `Enabled` (prevents mid-combat stutters).

---

## 5. 🔫 PUBG Mobile In-Game Configuration

| In-Game Setting | Recommended Value | Reason |
| :--- | :--- | :--- |
| **Graphics** | `Smooth` | Removes heavy foliage clutter, drops frame-time variance, and maximizes FPS. |
| **Frame Rate** | `90 fps` / `Extreme` (120 fps) | Smoothest input response and camera tracking. |
| **Style** | `Soft` or `Colorful` | **Soft** enhances edge contrast on player models against terrain; **Colorful** increases saturation. |
| **Anti-Aliasing** | `Close (Disabled)` | In-game AA causes temporal blur on stretched resolutions. Let GPU driver sharpening do the work. |
| **Brightness** | `125% – 130%` | Eliminates dark interior corners in buildings without blowing out sky highlights. |
| **Auto-Adjust Graphics**| `Disabled` | Prevents the game engine from dynamically lowering resolution during intense fire fights. |

---

## 6. 🚀 Quick Checklist Before Playing

- [ ] Applied `1440x1080` (or desired resolution) with `320 DPI` in **HMW GameLoop Optimizer**.
- [ ] Calibrated Keymap in **🎯 Stretched Res & Keymaps** tab.
- [ ] GPU Image Sharpening enabled in NVIDIA / AMD / Intel driver panel (`60%`).
- [ ] GPU Display Scaling set to `Full-Screen` on `GPU`.
- [ ] In-game Anti-Aliasing set to `Disabled` and Graphics set to `Smooth`.
