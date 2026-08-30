---
name: wpf-theming-and-ui
description: >-
  WPF XAML design, dynamic theming (Obsidian Cyber Dark & Modern Slate Light), 
  glassmorphism styling, custom modal controls, and MVVM architecture.
---

# WPF MVVM, Theming & UI Development Standards

This skill establishes best practices and styling rules for developing and styling WPF XAML views, custom controls, and theme dictionaries within GameLoop Optimizer.

---

## 1. Dynamic Theming Standards

The application supports dual themes managed by [`ThemeManager.cs`](file:///d:/SourceCode/Gameloop_Opt/src/GameLoopOptimizer/Core/ThemeManager.cs):
1. **Obsidian Cyber Dark** (Default competitive gaming theme)
2. **Modern Slate Light** (Clean, high-contrast light theme)

### 🛑 Strict Rule: DynamicResource Bindings
Never hardcode static hex colors in XAML controls or views. All brushes, foregrounds, backgrounds, borders, and accents must use `{DynamicResource ...}` keys defined in `Styles/ThemeDictionaries.xaml`.

```xml
<!-- ✅ Correct -->
<Border Background="{DynamicResource CardBackgroundBrush}"
        BorderBrush="{DynamicResource CardBorderBrush}"
        BorderThickness="1"
        CornerRadius="8">
    <TextBlock Text="Performance Status" 
               Foreground="{DynamicResource PrimaryTextBrush}" 
               FontSize="14" FontWeight="SemiBold"/>
</Border>

<!-- ❌ Incorrect: Hardcoded colors break theme toggling -->
<Border Background="#1E1E2E" BorderBrush="#313244">
    <TextBlock Text="Performance Status" Foreground="#CDD6F4"/>
</Border>
```

### Essential Theme Keys
* `BackgroundBrush` / `SurfaceBrush` / `CardBackgroundBrush`
* `CardBorderBrush` / `SeparatorBrush`
* `PrimaryTextBrush` / `SecondaryTextBrush` / `MutedTextBrush`
* `AccentPrimaryBrush` / `AccentSecondaryBrush` / `AccentHoverBrush`
* `SuccessBrush` / `WarningBrush` / `DangerBrush` / `InfoBrush`

---

## 2. Studio Workspace Separation

Keep dedicated studios clean and visually cohesive:
1. **🎮 GameLoop & ADB Studio** (`GameLoopStudioView.xaml`):
   * Engine tweaks, resolution overrides, model spoofing, VM heap optimization, DNS latency testing, live ADB telemetry.
2. **🎯 Stretched Res & Keymaps Studio** (`KeymapStudioView.xaml`):
   * Aspect ratio presets (1:1, 4:3, 16:10), visual HUD coordinate calibration preview, Keymap Vault management, and mouse sensitivity calculator.

---

## 3. Custom Modals & Dialogs Architecture

Modals (e.g. `UpdateNotificationModal`, `KeymapVaultModal`, `MouseBenchmarkModal`) should follow these design standards:
* **Overlay Backdrop:** Semi-transparent dark background (`#99000000` or `{DynamicResource ModalBackdropBrush}`) with subtle backdrop blur where supported.
* **Card Frame:** Centered, elevated card with smooth border radius (`CornerRadius="12"`), subtle border glow, and shadow effects.
* **Typography:** Clear hierarchy with modern typography, badge pills for versioning or status, and clear primary/secondary call-to-action buttons.
* **Animations:** Smooth opacity and translation entry transitions.

---

## 4. MVVM & Thread Safety Best Practices

### Threading & Dispatcher
Background threads (ADB polling, DNS pings, watchdog timers, hardware detectors) must marshal updates to the UI thread safely:
```csharp
Application.Current.Dispatcher.Invoke(() =>
{
    // Update ObservableCollections or UI-bound properties here
    StatusMessage = "Optimization applied successfully";
});
```

### Resource Disposal & Leak Prevention
* Always detach event handlers and dispose timers/hotkeys (`IDisposable` pattern) when views or viewmodels are unloaded.
* Use weak event patterns or unbind listeners for long-lived static singletons (`ThemeManager.ThemeChanged`, `UpdateManager.UpdateChecked`).
