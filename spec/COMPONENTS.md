# Component contract

Every platform ships the same concepts with the same names, adapted to its idiom (SwiftUI views and modifiers, Compose composables and `Modifier`s, WPF controls in XAML). Defaults come from [`OPTICS.md`](OPTICS.md). A developer who knows one port should be able to read another.

## Concepts

| Concept | SwiftUI (iOS 17+, macOS 14+) | Jetpack Compose (Android 7+, glass on 13+) | WPF (.NET Framework 4.8, .NET 8, .NET 10; Windows 10 1809+) |
| --- | --- | --- | --- |
| Scene: the region glass may refract | `GlassScene { … }` | `GlassScene { … }` | `<lg:GlassScene>` |
| Backdrop layer the glass refracts | `GlassContent { … }` | `GlassContent { … }` | `<lg:GlassScene.Content>` |
| Glass container | `GlassSurface { … }` / `.liquidGlass()` | `GlassSurface { … }` / `Modifier.liquidGlass()` | `<lg:GlassSurface>` |
| Button | `GlassButton` | `GlassButton` | `<lg:GlassButton>` |
| Toolbar (clusters, spacers) | `GlassToolbar`, `GlassToolbarSpacer` | `GlassToolbar`, `GlassToolbarSpacer` | `<lg:GlassToolbar>`, `<lg:GlassToolbarSpacer>` |
| Tabs with a moving glass lens | `GlassTabBar` | `GlassTabBar` | `<lg:GlassTabBar>` |
| Tab item | `GlassTab` | `GlassTab` | `<lg:GlassTabBarItem>` |
| Slider | `GlassSlider` | `GlassSlider` | `<lg:GlassSlider>` |
| Switch | `GlassSwitch` | `GlassSwitch` | `<lg:GlassSwitch>` |
| Menu that grows out of its trigger | `GlassMenu` | `GlassMenu` | `<lg:GlassMenu>` |
| Menu item | `GlassMenuItem` | `GlassMenuItem` | `<lg:GlassMenuItem>` |
| Menu divider | `GlassMenuDivider` | `GlassMenuDivider` | `<Separator />` |
| Progressive edge blur | `GlassProgressiveBlur` | `GlassProgressiveBlur` | `<lg:GlassProgressiveBlur>` |
| Continuous-corner shape | `GlassShape` (a `Shape`) | `GlassShape` (a `Shape`) | `GlassGeometry.Create(...)` |

Item types follow each platform's idiom where it has one: WPF tab items are `GlassTabBarItem` (a `ListBoxItem`, like `TabItem` for `TabControl`), and WPF menus take the standard `<Separator />`.

### Platform-specific additions

Optional APIs that one port adds for its platform. They are not part of the shared contract, and other ports need not provide them.

| Port | API | Purpose |
| --- | --- | --- |
| SwiftUI | `GlassScrollView`, `GlassScrollPosition` | A scroll view drawn by SwiftUI, so scrolling content can be the backdrop (UIKit and AppKit views cannot be refracted). |
| SwiftUI | `GlassSwitchStyle` | The glass switch as a `ToggleStyle` for any `Toggle`. |
| SwiftUI | `GlassOptics`, `GlassSpring` | The optical model and the springs on the CPU. |
| Jetpack Compose | `GlassText`, `GlassIcon`, `LocalGlassContentColor` | Text and icons in the glass foreground color. |
| Jetpack Compose | `GlassDefaults` | The default constants (refraction, radii, content colors, surfaces per scene). |
| WPF | `GlassDiagnostics` | Render tier, pixel shader support and shader status, for logs and about boxes. |
| WPF | `GlassOptics` and the rest of `LiquidGlassEngineering.Core` | The optical model on the CPU (`netstandard2.0`). |
| WPF | `GlassLens`, `GlassValueThumb` | The glass and value-thumb elements, named template parts (`PART_…`) for restyling controls. |
| WPF | `GlassRenderMode`, `GlassShaderSource`, `GlassLensMode` | Choose shader or fallback rendering, see where the shader came from, and read which mode a lens actually rendered (`RenderedMode`). |
| WPF | `GlassMenuPlacement`, `GlassSpacerSizing` | Where a menu panel opens, and fixed or flexible toolbar spacers. |
| WPF | `GlassSurfaceAutomationPeer`, `GlassToolbarAutomationPeer`, `GlassTabBarAutomationPeer`, `GlassTabBarItemAutomationPeer`, `GlassMenuAutomationPeer`, `GlassMenuItemAutomationPeer`, `GlassSwitchAutomationPeer` | UI Automation peers, public so apps can subclass them for custom controls. |

## Shared options

| Option | Values | Default | Notes |
| --- | --- | --- | --- |
| `material` | `clear`, `regular` | `clear` (menus: `regular`) | Inherited from the scene. |
| `appearance` | `automatic`, `light`, `dark` | `automatic` (follows the system) | Inherited. Foreground color follows it. |
| `shape` / `radius` | number, `capsule`, `circle` | surface 16, button `capsule` (icon-only `circle`), toolbar and tab bar `capsule`, menu 28 | Continuous corners. |
| `refraction` | ≥ 0 | `min(60, 0.75 × shorter side)` | Optical travel at the rim. Unset, it follows the surface's resting size ([OPTICS §2](OPTICS.md#2-refraction)); an explicit value is used as given. |
| `tint` | color or none | none | Colors the glass, never the content. On slider and switch it colors the fill/track (default: platform accent). |
| `interactive` | bool | `true` for buttons, toolbars, tab bars | Press grow, drag stretch, glow. |
| `motion` | `full`, `reduced`, `none` | `full` | System reduce-motion always applies. |

## Sizing per platform

| Control | iOS / Android (touch) | macOS | Windows |
| --- | --- | --- | --- |
| Button height (small / regular / large) | 34 / 44 / 52 | 22 / 28 / 34 | 24 / 32 / 40 |
| Icon button | circle 44 | circle 28 | circle 32 |
| Toolbar | capsule 52 tall, 4 inset | capsule 34 tall, 3 inset | capsule 40 tall, 4 inset |
| Tab bar | capsule 62 tall, icon + label | capsule 28 tall, label | capsule 34 tall, label |
| Label font | 17 semibold | 13 medium | 14 semibold (Segoe UI Variable) |
| Slider thumb (lifted glass grows per side) | iOS 36 × 24, grows 5 × 4; Android 20 × 16, grows 2.5 × 2 | 20 × 16, grows 2.5 × 2 | 20 × 16, grows 2.5 × 2 |
| Switch track, thumb (lifted glass grows per side) | iOS 63 × 28, 37 × 24, grows 10 × 6.5; Android 54 × 24, 32 × 20, grows 9 × 5.5 | 54 × 24, 32 × 20, grows 9 × 5.5 | 54 × 24, 32 × 20, grows 9 × 5.5 |

## Behavior every port implements

1. **Scene and content.** The scene renders its content once as the backdrop. Glass surfaces anywhere inside the scene (any depth) register with it and refract that content at their exact on-screen position, including while they move, scale or animate. Surface children (labels, icons) draw above the glass and are not refracted. Where glass overlaps other scene layers the ports differ: WPF glass refracts the scene's backdrop, any progressive blur and the glass of the surfaces that enclose it; Compose glass over a progressive-blur band refracts the blurred content; SwiftUI glass refracts the sharp backdrop only.
2. **Materials and appearance** exactly as in `OPTICS.md`, including tint, rim light and the Regular frost.
3. **Interaction** with the press/release/glow springs, volume-preserving stretch, and reduced-motion behavior.
4. **Tabs:** a glass lens of the bar's material (a Regular bar stays frosted under the selection) slides between items on the track spring and stretches with its speed. On iOS the selected label takes the accent color and the rest are at 62%; on Android and desktop the selected label is at full opacity and the rest at 0.85, which stays legible over mid-tone backdrops. Keyboard arrows move selection on desktop.
5. **Slider and switch:** native-feeling value controls; the thumb lifts into Clear glass while pressed (sizes in `OPTICS.md` §6 and the sizing table above). Switch uses the measured spring (response 0.47, damping 0.7). Haptics on touch platforms where the OS provides them.
6. **Menu:** a Regular glass panel with the legibility backing (`legibility = 0.6`, fading in with the morph, [OPTICS §3](OPTICS.md#legibility-backing-menus)) that grows out of its trigger with the morph spring and collapses back into it; items take `menuForeground` (the appearance foreground, or on a tinted menu white or `#111113` by contrast with the tint mixed toward the backing) and highlight with that color at 8% (labels keep at least 5.2:1 untinted and 5.0:1 on system-color tints over any backdrop); destructive items take `menuDestructive`, `#560D0A` where the labels are `#111113` (at least 5.0:1) and `#FFEEED` where they are white (at least 4.5:1); dismiss on outside tap and Escape.
7. **Progressive blur** at any edge of the content with the level/weight model of `OPTICS.md` §7.
8. **Fallbacks:** where the GPU path is unavailable (Android below 13, WPF without pixel shader 3.0, reduced transparency), draw the fallback material: the platform's own blur where available, otherwise a translucent fill, plus the rim light. Never crash, never draw nothing.
9. **Accessibility:** platform roles and labels on every control, focus visuals on desktop, Dynamic Type / font scaling on touch, reduce transparency and increase contrast honored.

## Demo app ("Liquid Glass Studio")

Each platform ships the same demo, so screenshots line up across platforms:

1. **Hero:** a vivid wallpaper (bundled, generated by `assets/wallpapers`) under a draggable Regular glass card with a title, a floating toolbar and a bottom tab bar.
2. **Controls:** buttons (all sizes, tinted, icon-only), slider, switch, tab bar, menu.
3. **Materials lab:** live controls for material, appearance, refraction, tint and radius over the wallpaper, with a draggable lens.
4. **Progressive blur:** a scrolling photo/article list with blurred top and bottom edges and a floating glass toolbar.
