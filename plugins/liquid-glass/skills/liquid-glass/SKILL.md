---
name: liquid-glass
description: Design and build Liquid Glass interfaces (refracting glass controls with Clear and Regular materials, rim light and elastic press) using the Liquid Glass Engineering libraries on iOS, macOS, Android and Windows. Use when asked for liquid glass, glass UI, refraction, frosted or clear glass toolbars, tab bars, buttons, sliders, switches or menus, or an Apple-style Liquid Glass look on any platform. Chooses the platform skill and applies the design rules.
license: MIT
metadata:
  homepage: https://liquid-glass-engineering.vercel.app
  repository: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android
---

# Liquid Glass

Liquid Glass Engineering renders the same optical glass on four platforms: a bevel that bends the content behind it, a Clear or frosted Regular material, a rim of light, and controls that stretch and glow under the finger. This skill decides *where* and *how* to use it; the platform skills hold the APIs.

## 1. Pick the platform skill

| Target | Library | Skill |
| --- | --- | --- |
| iOS 17+, macOS 14+ (SwiftUI) | Swift package product `LiquidGlass` from `https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android` (`from: "1.0.1"`) | `liquid-glass-swiftui` |
| Android 7+ with glass on 13+ (Jetpack Compose) | `io.github.rookasrudzianskas:liquid-glass-compose:1.0.1` (repository `maven("https://liquid-glass-engineering.vercel.app/maven")`, not Maven Central) | `liquid-glass-compose` |
| Windows 10 1809+ / 11 (WPF on .NET Framework 4.8, .NET 8 or .NET 10) | NuGet `LiquidGlassEngineering.Wpf` 1.0.1 (feed `https://liquid-glass-engineering.vercel.app/nuget/index.json`, not nuget.org) | `liquid-glass-wpf` |
| Anything else (Flutter, Unity, web canvas, games) | the shared optical model | `liquid-glass-port` |

Read the platform skill before writing code. If the project mixes platforms, keep the same material, radius and tint choices everywhere: the optical model is identical, so matching options give matching pixels.

## 2. The mental model (same on every platform)

1. **Scene**: the region where glass may appear. Glass can only refract what is inside its scene.
2. **Content**: the one backdrop layer inside the scene that glass refracts (wallpaper, photo, map-like art, a list).
3. **Surfaces**: glass shapes anywhere inside the scene. Their own children (labels, icons) draw on top of the glass, sharp and unrefracted.

Put floating controls in the scene, not in the content. Content that must stay readable under glass needs the Regular material or enough distance from the rim.

## 3. Design rules

Follow [references/design.md](references/design.md) for the full rationale. The short version:

- **Glass is the control layer, never the content.** Use it for things that float above content: tab bars, toolbars, floating buttons, menus, sliders over media, compact cards over imagery. Don't make list rows, paragraphs or every card glass.
- **One layer of glass.** Don't stack glass on glass. Two exceptions: a menu or popover opening from a glass control, and glass controls (buttons, slider, switch) inside a Regular card, which is one level of nesting. Inside a glass toolbar or tab bar use plain items: the cluster already is the glass.
- **Material:** Clear over rich imagery when you want the backdrop to sing; Regular (frosted) for legibility over text or busy content, for menus, sheets and any panel larger than about a third of the screen.
- **Appearance** follows the system unless the backdrop dictates otherwise: dark appearance over dark imagery, light over light.
- **Shape:** capsules for controls, circles for icon-only buttons, continuous corners for panels. Nested shapes are concentric: inner radius = outer radius − inset.
- **Tint sparingly:** one tinted (prominent) action per view, never tint whole toolbars. Tint colors the glass, not the label.
- **Refraction:** keep the default 60 for surfaces. Small controls inherit lower values automatically (thumbs use 5–7.75). Don't push it so high that the backdrop becomes unrecognizable.
- **Motion:** interactive glass (buttons, toolbars, tab bars, thumbs) responds to touch with press, stretch and glow. Static panels don't. Respect reduced motion.
- **Legibility first:** labels on glass use the appearance foreground (near-black or white). Check contrast over the busiest part of the backdrop; if it fails, switch to Regular, move the control, or dim the backdrop locally.
- **Accessibility:** reduce transparency swaps glass for an opaque fill, increase contrast adds a border, controls keep native roles, labels and keyboard focus. Never encode meaning only in glass.

## 4. Recipes

- **Floating tab bar:** a `GlassTabBar` capsule near the bottom safe area, Clear over media, Regular over text; 3 to 5 items; the selection lens slides between them.
- **Top toolbar over a photo:** a `GlassToolbar` with icon buttons grouped into clusters, a flexible spacer between leading and trailing clusters.
- **Media controls:** a Regular `GlassSurface` card with transport buttons and a `GlassSlider` scrubber; the slider thumb lifts into glass while dragging.
- **Contextual menu:** `GlassMenu` from a glass button, Regular, radius 28; it grows out of the trigger and collapses back into it.
- **Edge legibility in scrolling content:** `GlassProgressiveBlur` at the top and bottom edges under floating bars.

## 5. Performance

- Keep scenes bounded: one scene per screen region that needs glass, not the whole app.
- Keep the surface count modest (up to 16 per scene is comfortable). Count one per button, surface, toolbar cluster and open menu, two per tab bar (bar and lens). Group toolbar buttons into one glass cluster instead of one surface each.
- Regular glass costs blur passes; prefer Clear for many small controls.
- Animate surfaces freely; avoid animating the entire content layer under many surfaces at once.

## 6. Before you call it done

- [ ] Each glass surface sits inside a scene whose content is what it should refract.
- [ ] Material, appearance, radius and tint follow section 3.
- [ ] Labels pass contrast over the busiest backdrop area in light and dark.
- [ ] Reduce motion, reduce transparency and increase contrast checked.
- [ ] Ran the app and looked at it: rim light crisp, refraction pulls content inward toward the rim, no seams.

The optical model, with every constant, is in [references/optics.md](references/optics.md).
