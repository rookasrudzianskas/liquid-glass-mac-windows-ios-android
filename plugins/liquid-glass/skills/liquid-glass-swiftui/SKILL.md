---
name: liquid-glass-swiftui
description: Builds Liquid Glass interfaces in SwiftUI for iOS 17+ and macOS 14+ with the LiquidGlass Swift package, whose GlassScene, GlassContent, GlassSurface, GlassButton, GlassToolbar, GlassTabBar, GlassSlider, GlassSwitch, GlassMenu, GlassProgressiveBlur and GlassScrollView refract a live backdrop through a Metal shader with Clear and Regular (frosted) materials, rim light and elastic press. Use when writing SwiftUI for liquid glass, glass UI, refraction, frosted or clear glass toolbars, tab bars, buttons, sliders, switches, menus or cards on iPhone, iPad or Mac; when glass must run before iOS 26 and macOS 26 (where Apple's glassEffect is unavailable) or match the Android and Windows ports; or when SwiftUI glass shows nothing, only a rim, the wrong part of the backdrop, or the fallback material. Not for UIKit or AppKit views, or for Apple's own glassEffect API in iOS 26-only projects that do not use this package.
license: MIT
metadata:
  homepage: https://liquid-glass-engineering.vercel.app
  repository: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android
---

# Liquid Glass for SwiftUI

`LiquidGlass` renders Liquid Glass natively in SwiftUI with a Metal `layerEffect`: a 20-point bevel that bends the
backdrop, the Clear and Regular materials, a one-pixel rim light, tint, and controls that grow, stretch and glow under
the finger or pointer. It shares one optical model with the Compose and WPF ports (the `liquid-glass` skill's
`references/optics.md`), so the same options give the same pixels on every platform.

## When to use

- A SwiftUI app on iOS 17+, iPadOS 17+ or macOS 14+ needs glass toolbars, tab bars, buttons, sliders, switches,
  menus or cards over imagery, art or scrolling content.
- The app must look the same on iOS 17 to 26 and macOS 14 to 26. On iOS 26 and macOS 26 Apple also ships its own
  `glassEffect`; this library gives the same optical look from iOS 17 with full control (material, refraction, tint,
  rim) and rendering identical to the Android and Windows ports. The two coexist (no type is called plain `Glass`),
  but never apply both to the same view.
- Existing SwiftUI glass shows nothing, only the rim, the system material, or the wrong part of the backdrop.
- Not for UIKit, AppKit, visionOS, tvOS or watchOS. For *where* glass belongs and which material to pick, read the
  `liquid-glass` skill; this skill applies those rules to SwiftUI (the essentials are under Design defaults below).

## Install

Swift Package Manager. In Xcode: **File › Add Package Dependencies…**, paste
`https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android`, add the product **LiquidGlass** to the
app target. In `Package.swift`:

```swift
dependencies: [
    .package(url: "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android", from: "1.0.0"),
],
targets: [
    .target(name: "MyApp", dependencies: [
        .product(name: "LiquidGlass", package: "liquid-glass-mac-windows-ios-android"),
    ]),
]
```

| Requirement | Value |
| --- | --- |
| Deployment target | iOS 17.0 / macOS 14.0 or later (`platforms: [.iOS(.v17), .macOS(.v14)]`) |
| Toolchain | Xcode 16 or later (iOS 18 / macOS 15 SDK) to build; the deployment target stays iOS 17 / macOS 14. Xcode 15 cannot compile the iOS target. No third-party dependencies |
| Module | `import LiquidGlass` next to `import SwiftUI` |
| Shaders | `.metal` files compiled by Xcode or `xcodebuild`. Plain `swift build` / `swift run` does not compile Metal (with only the Command Line Tools the package does not build at all): build, run and preview with Xcode |

## Mental model: scene, content, surfaces

```
GlassScene(material:appearance:refraction:motion:)   fills the space offered (like GeometryReader); glass defaults
├─ GlassContent { … }.ignoresSafeArea()   FIRST element: the backdrop. Pure SwiftUI only (Image, gradients, shapes,
│                                          Text, Canvas, GlassScrollView). Drawn once on screen and once more,
│                                          clipped, inside every surface            ← this is what glass refracts
└─ foreground (ideally one container)     laid out over the whole scene like ZStack layers; safe area respected
   ├─ GlassProgressiveBlur                blurred strip of the backdrop (never takes touches)
   ├─ GlassToolbar / GlassTabBar / GlassSurface / GlassButton / GlassSlider / GlassSwitch / GlassMenu
   │    │                                 glass, at any depth: stacks, native ScrollView, your views
   │    └─ their content                  labels and icons: sharp, on top, clipped to the outline, NOT refracted
   ├─ plain views, TextField, List, Map…  drawn over the backdrop, NOT refracted, NOT visible through glass
   └─ overlay                             open GlassMenu panels, above the foreground layer that holds the menu
```

1. **Scene** = `GlassScene`. Glass refracts only its own scene's backdrop. Use one per screen; a single scene at the
   app root is fine when the app is one screen under a shared floating bar (as the demo app does), because cost
   follows the number of surfaces, not the scene's size. Outside a scene, surfaces fall back to the system material
   with the rim light and menus fall back to the system `Menu`.
2. **Content** = `GlassContent`, the backdrop. Each surface draws its own copy of it, aligned at render time to the
   surface's exact on-screen frame (also while it is dragged, offset, scaled or animated), then runs the shader on it.
3. **Surfaces** = every glass view in the foreground. The content of a `GlassSurface`, `.liquidGlass` or
   `GlassButton`, and toolbar items, tab labels and menu items, draw above the glass in the glass foreground color
   (`#111113` on light glass, white on dark; on tinted glass whichever of the two contrasts more).
4. **Options inherit.** `GlassScene(material:appearance:refraction:motion:)` and the `.glassMaterial(_:)`,
   `.glassAppearance(_:)`, `.glassRefraction(_:)`, `.glassMotion(_:)` modifiers set defaults for all glass below;
   a component's own non-`nil` parameter wins.

## Minimal example

```swift
import LiquidGlass
import SwiftUI

@main
struct GlassApp: App {
    var body: some Scene {
        WindowGroup { ContentView() }
    }
}

struct ContentView: View {
    @State private var volume = 0.4

    var body: some View {
        GlassScene {
            GlassContent {
                Image("wallpaper")                       // any vivid image in Assets.xcassets
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityHidden(true)           // decorative
            }
            .ignoresSafeArea()                           // backdrop under the status bar and home indicator

            VStack(spacing: 20) {
                Spacer()
                GlassSurface(material: .regular, radius: 28) {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("Liquid Glass").font(.largeTitle.bold())
                        Text("Refraction, frost and light.").opacity(0.75)
                        HStack(spacing: 10) {
                            Image(systemName: "speaker.fill")
                            GlassSlider(value: $volume) { Text("Volume") }   // label is for VoiceOver only
                            Image(systemName: "speaker.wave.3.fill")
                        }
                    }
                    .padding(24)
                    .frame(maxWidth: 360)
                }
                Spacer()
                GlassToolbar {                           // one cluster at the edge, not loose buttons
                    GlassButton(icon: "play.fill", label: "Play") {}
                    GlassButton(icon: "heart", label: "Like") {}
                    GlassButton(icon: "square.and.arrow.up", label: "Share") {}
                }
            }
            .padding(20)
        }
    }
}
```

## Components

Every signature, default and enum case: [references/api.md](references/api.md). `nil` means "inherit from the scene
or environment" (defaults: material `.clear`, appearance `.automatic`, refraction by size,
`min(60, 0.75 × shorter side)`, motion `.full`).

| Component | Purpose | Key parameters (default) |
| --- | --- | --- |
| `GlassScene` | The region glass may refract; backdrop plus foreground | `material` (nil), `appearance` (nil), `refraction` (nil), `motion` (nil), `@GlassSceneBuilder content` |
| `GlassContent` | The backdrop; first element of a scene only (not a `View`) | `@ViewBuilder content`; `.ignoresSafeArea(_ edges: Edge.Set = .all)` |
| `GlassSurface` / `.liquidGlass(…)` | Glass container: cards, panels, chips, draggable lenses | `material` (nil), `appearance` (nil), `radius` (`16`), `refraction` (nil), `tint` (nil), `interactive` (`false`), `motion` (nil) |
| `GlassButton` | Button on interactive glass: capsule, circle when icon-only; a plain item inside a toolbar | `(_ title, systemImage:, role:, material:, appearance:, refraction:, tint:, prominent:, interactive: true, motion:, action:)`, `(icon:label:role:…)`, `(role:radius:…, action:, label:)`; height from `.controlSize`: 34 / 44 / 52 pt iOS, 22 / 28 / 34 pt macOS |
| `GlassToolbar` | Row of items on capsule glass clusters, 52 pt (macOS 34) | `material` (nil), `appearance` (nil), `tint` (nil), `interactive` (`true`), `motion` (nil) |
| `GlassToolbarSpacer` | Splits a toolbar into clusters | `_ sizing` (`.flexible`; or `.fixed`), `minLength` (`8`) |
| `GlassTabBar` + `GlassTab` | Capsule tabs with a sliding, draggable lens of the bar's material; 62 pt iOS (40 small), 28 pt macOS (24 small) | `selection:` binding, `material`, `appearance`, `tint` (nil), `interactive` (`true`), `motion` (nil); `GlassTab(_ title, systemImage: nil, value:)` |
| `GlassSlider` | 6 pt track with tinted fill; white thumb lifts into glass while held; 44 pt tall (macOS 28) | `value:`, `in:` (`0...1`), `step:` (nil), `tint` (nil = accent), `appearance`, `interactive` (`true`), `motion`, `label` (VoiceOver only), `onEditingChanged` |
| `GlassSwitch` / `GlassSwitchStyle` | A `Toggle` with a glass thumb; track 63 × 28 pt (macOS 54 × 24) | `(_ title, isOn:, tint: nil, appearance: nil, interactive: true, motion: nil)`, `(isOn:…, label:)`; `GlassSwitchStyle(tint:appearance:interactive:motion:)` |
| `GlassMenu` + `GlassMenuItem` + `GlassMenuDivider` | A glass button whose Regular panel grows out of it | `(_ title, systemImage: nil, …)`, `(icon:label:…)`, `(…, content:, label:)`; `material` (nil = Regular panel), `appearance` (nil; trigger and panel), `radius` (`28`), `tint` (nil; trigger and panel), `motion` (nil); `GlassMenuItem(_ title, systemImage: nil, role: nil, checked: nil, action:)` |
| `GlassProgressiveBlur` | Blur that deepens toward an edge of the backdrop | `edge` (`.top`), `size` (`80`), `blur` (σ `20`) |
| `GlassScrollView` + `GlassScrollPosition` | Scrolls a backdrop (native `ScrollView` cannot be refracted) | `_ axes` (`.vertical`), `position:`, `showsIndicators` (`true`); `offset`, `contentSize`, `viewportSize`, `isScrolling`, `maximumOffset`, `scrollTo(_:animated:)`, `scrollTo(edge:animated:)` |
| `GlassShape` | The exact glass outline as an `InsettableShape` | `radius` (`16`); `.inset(by:)` keeps corners concentric |
| `GlassRadius` | Corner radius | number literal (`24`), `.points(Double)`, `.capsule`, `.circle` |
| `GlassMaterial` / `GlassAppearance` / `GlassMotion` | Options | `.clear`, `.regular` / `.automatic`, `.light`, `.dark` / `.full`, `.reduced`, `.none` |
| `GlassOptics`, `GlassSpring` | The optical model and springs on the CPU | for custom renderers, layout and tests |

## Recipes

Six complete, runnable files are in [references/recipes.md](references/recipes.md): app shell with a floating tab
bar, toolbar over a photo, media card with a slider, settings card with switches, glass menu from a button, and
progressive blur over a scrolling list. The two layouts that every app needs:

**App shell with a floating tab bar over content.** One scene at the root; the page is the backdrop and swaps with
the selection; the bar floats over every page so its lens glides between them. Pages swapped with `.id(tab)` are
rebuilt on every switch, so keep per-tab state (scroll positions, selections) in models above the scene.

```swift
GlassScene {
    GlassContent { backdrop.id(tab).transition(.opacity) }    // full-bleed image for the current tab
        .ignoresSafeArea()
    ZStack(alignment: .bottom) {
        page.id(tab).transition(.opacity).frame(maxWidth: .infinity, maxHeight: .infinity)
        GlassTabBar(selection: $tab) {                  // 3 to 5 tabs; material: .regular when the page is text
            GlassTab("Home", systemImage: "house", value: AppTab.home)
            GlassTab("Explore", systemImage: "safari", value: AppTab.explore)
            GlassTab("Library", systemImage: "books.vertical", value: AppTab.library)
        }
        .frame(maxWidth: 440).padding(.horizontal, 24).padding(.bottom, 4)   // macOS: .fixedSize()
    }
}
.animation(.easeInOut(duration: 0.3), value: tab)
```

**Progressive blur over a scrolling list.** The list is the backdrop, so it must scroll with `GlassScrollView`; the
position lives in `@State` beside the scene; bars over text are Regular.

```swift
@State private var scroll = GlassScrollPosition()

GlassScene {
    GlassContent {
        GlassScrollView(position: scroll) {
            VStack(alignment: .leading, spacing: 20) {
                ForEach(0..<30, id: \.self) { Text("Story \($0 + 1)").font(.title3) }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal, 20).padding(.top, 132).padding(.bottom, 120)
        }
        .background(Color(white: 0.96))
    }
    .ignoresSafeArea()
    VStack(spacing: 0) {
        ZStack(alignment: .top) {
            GlassProgressiveBlur(edge: .top, size: 128).ignoresSafeArea(edges: .top)
            GlassToolbar(material: .regular) {
                GlassButton(icon: "arrow.up", label: "Scroll to top") { scroll.scrollTo(edge: .top) }
            }
            .padding(.horizontal, 20).padding(.top, 8)
        }
        Spacer(minLength: 0)
        GlassProgressiveBlur(edge: .bottom, size: 120).ignoresSafeArea(edges: .bottom)
    }
}
```

## Rules and pitfalls

**Design defaults** (from the `liquid-glass` skill)

- Glass is the control layer: bars, floating buttons, menus and compact cards over content. Long text, full
  settings forms and tables get solid backgrounds, not glass.
- One layer of glass. Controls inside a Regular card are the only nesting; never go deeper.
- No glass list rows, grid cells or feed cards: put one glass bar above the list instead.
- Group related buttons into one `GlassToolbar` cluster rather than separate glass buttons side by side, and keep
  glass at the edges and corners of the content.
- Tab bars hold 3 to 5 tabs. At most one tinted or prominent control per view.

**The scene and its content layer**

- `GlassContent` must be the **first** element of `GlassScene`. The scene builder takes a fixed list of views: no
  top-level `if`, `switch` or `ForEach`. Put conditionals inside a container (`ZStack`, `Group`, `VStack`) or inside
  the content closure (`@ViewBuilder var backdrop` with a `switch`, as the demo app does).
- `GlassContent` is not a `View`. The only method after it is `.ignoresSafeArea(_:)` (an `Edge.Set`, default
  `.all`); put `.id`, `.transition`, `.background`, `.overlay` and gestures **inside** its closure.
- Give the scene a definite size. It is a `GeometryReader`, so inside a `ScrollView`, `List` or lazy stack it
  collapses to 10 pt: never nest a scene in scrolling content.
- Content must be **pure SwiftUI**. UIKit/AppKit-backed views cannot be drawn into a shader: native `ScrollView`,
  `List`, `Form`, `TextField`, `TextEditor`, `Map`, `VideoPlayer`, web views and any `UIViewRepresentable` /
  `NSViewRepresentable` vanish from every surface's copy, so glass over them shows nothing. Put them in the
  foreground, scroll the backdrop with `GlassScrollView`, or use a snapshot image (for example of a map).
- Content renders once per surface, so keep it **a pure function of external state**. `@State` stored inside the
  content is separate in each copy, and `.onAppear`, `.task` and timers inside it run once per copy. Load data
  outside and pass values, bindings or `@Observable` models in. `GlassScrollPosition` must live in `@State` next to
  the scene, never inside the content.
- No `AsyncImage` (or any loader with internal state) in the content: it downloads once per copy, and glass shows
  the placeholder while the visible backdrop shows the image. Load remote images outside the scene and pass an
  `Image` in. An animated backdrop (`TimelineView`) re-renders in every copy each frame.
- The backdrop **lays out without safe-area insets** and must lay out identically in every copy: no dependence on
  safe-area insets, no `GeometryReader` values written back into state, no randomness. Give scrolling content
  explicit top and bottom padding for bars (the demo uses 132 pt top on iOS, 84 pt on macOS).
- **Never put glass inside `GlassContent`**: it would refract itself, so copies draw it without glass. Glass goes in
  the foreground.
- Give the backdrop color and detail (photos, gradients, art) and an opaque background. Over a flat color or a
  transparent backdrop glass reads as a gray pill.
- Glass refracts only the backdrop. Foreground views, other glass and the progressive blur's strip are not
  refracted: move anything that should show through glass into `GlassContent`.
- Glass shows only `GlassContent`. Do not float glass over a foreground `Map`, `VideoPlayer`, `List`, `Form` or opaque
  page: it shows the backdrop hidden underneath, not what the user sees. For a map or video use a still snapshot
  (`MKMapSnapshotter`, a poster frame) as the content; for a list, make the list the backdrop with `GlassScrollView`.
  Only Apple's own `glassEffect` (iOS 26 and later) can sit over live UIKit views.

**Foreground and layout**

- Each foreground view is proposed the whole scene and centered, like a `ZStack` layer. Pin bars with a `VStack`
  plus `Spacer()`, a `ZStack(alignment: .bottom)`, or `.frame(maxHeight: .infinity, alignment: .bottom)`; a bare
  `GlassTabBar` in a scene sits in the middle.
- Put the whole foreground in one container (`ZStack` or `VStack`) whenever it holds a `GlassMenu`. Each top-level
  foreground view gets its own menu overlay, so a later layer would draw over an earlier layer's open panel.
- Glass works at any depth in the foreground, including native `ScrollView` and `List` rows (possible, but do not
  make list rows glass): only the backdrop has to be pure SwiftUI. Text fields, maps and video can sit in the
  foreground, but glass over them will not show them. Inside a scroll view, sliders and tab bars take a touch drag
  only once it moves sideways, so a vertical swipe that starts on them scrolls (a tap on a slider still jumps).
- Keep full-screen foreground containers non-hit-testable (no `.background`, `.contentShape` or tap gesture on
  them), so a `GlassScrollView` backdrop under them still receives drags.
- `GlassSurface` sizes to its content and adds no padding: pad inside it (`.padding(20)`) and cap the width
  (`.frame(maxWidth: 380)`) when the content is greedy (sliders, switches, `maxWidth: .infinity`).
- Greedy controls: `GlassSlider` fills the width it is offered (min 80 pt); `GlassSwitch` is label, spacer, track,
  so it fills the row (`.fixedSize()` makes it compact; `.labelsHidden()` leaves only the track on iOS 18 and
  macOS 15, while iOS 17 and macOS 14 still show the label); on iOS `GlassTabBar` fills the width (cap it with
  `.frame(maxWidth: 440)` and horizontal padding; on macOS use `.fixedSize()`); `GlassToolbar` hugs its items
  unless a flexible `GlassToolbarSpacer` stretches it.
- `GlassButton` sets its own font, a one-line limit and padding, and its glass hugs the label. For a full-width or
  custom-font button use the `label:` initializer and put `.frame(maxWidth: .infinity)` or `.font(…)` inside the
  label closure; those modifiers outside the button reach neither the glass nor the label.
- Move glass freely with `.offset`, a `DragGesture`, layout changes or SwiftUI animations: the refraction stays
  exact. Do not rotate it (`rotationEffect`, `rotation3DEffect` on the glass or an ancestor misaligns the copy).
  Animating `scaleEffect` and `offset` together on an ancestor is off only until the animation settles.
- Attach `.sheet`, `.popover` and `.fullScreenCover` to the `GlassScene` itself (outside it), not to views in its
  foreground, and give the presented view its own `GlassScene` and backdrop. A presentation attached inside a scene
  inherits the scene's glass environment: its glass draws a misaligned copy of the parent backdrop and a `GlassMenu`
  in it opens no panel, so do not rely on the fallback there.
- Do not put glass in `.toolbar {}` items, a `NavigationStack` bar or a system `TabView`: system bars are hosted
  outside the scene (and get Apple's own glass on iOS 26). Hide them (`.toolbar(.hidden, for: .navigationBar)` on
  iOS) and use `GlassToolbar` and `GlassTabBar` in the foreground.
- On macOS, `.windowStyle(.hiddenTitleBar)` on the `WindowGroup` lets the backdrop run under the title bar.

**Materials, appearance, tint and shape**

- Defaults: material `.clear`, appearance `.automatic`, refraction by size, motion `.full`. Menu panels are Regular
  unless you pass a material.
- Clear over imagery; Regular for compact panels with short text (quick settings, now playing), menus and any bar
  over scrolling text (`GlassProgressiveBlur` blurs only the backdrop; glass above it still refracts the sharp text).
- A surface's `material` applies to that surface only; it does not cascade to controls inside it. Set defaults for a
  subtree with `GlassScene(material:)` or `.glassMaterial(.regular)`.
- Everything drawn on glass (the content of `GlassSurface`, `.liquidGlass` and `GlassButton`, toolbar items, tab
  labels, menu triggers and items) gets `#111113` (light) or white (dark) automatically, and an explicit `.light` or
  `.dark` appearance (on the scene, a surface, a bar or a menu) also sets its `colorScheme`, so `.primary` and
  `.secondary` adapt. Do not color labels to match the backdrop.
- `.automatic` follows the color scheme. `GlassScene(appearance: .dark)` gives dark glass with light labels on every
  glass element, but plain foreground views and the status bar keep the system scheme. To force a whole screen dark
  (or light), such as a dark photo viewer in light mode, set the color scheme on the scene:
  `.environment(\.colorScheme, .dark)` on the `GlassScene`, or `.preferredColorScheme(.dark)`, which also switches
  the status bar; `.automatic` glass follows it. `GlassMenu(appearance:)` applies to the trigger and the panel.
- Tint sparingly: one `prominent: true` (accent tint) or `tint:` control per view; never tint whole toolbars or tab
  bars. Labels on tinted glass (buttons, bars, menus, surfaces) default to white or `#111113`, whichever has the
  higher WCAG contrast with the tinted glass (white on blue, red or purple; dark on yellow, mint, green or orange), so
  do not set `.foregroundStyle(.white)` yourself; a `foregroundStyle` set inside still wins. A `GlassMenu(tint:)`
  colors the trigger and the panel. `role: .destructive` draws an untinted button label red. On `GlassSlider` and
  `GlassSwitch`, `tint` colors the fill and the on-track. Tints are clamped to sRGB.
- Shapes: capsules for controls (the default for buttons, toolbars, tab bars), circles for icon-only buttons and
  menus (`GlassButton(icon:label:)`, `GlassMenu(icon:label:)`: 44 pt on iOS, 28 pt on macOS), continuous
  radii 16 to 28 for cards and panels (up to 34 for large media cards). Nested corners are concentric: inner radius
  = outer radius − padding, as the library's own insets compute it. Clip artwork inside a 34 pt card padded 14 with
  `GlassShape(radius: 20)`; `GlassShape(radius: 34).inset(by: 14)` draws the same outline in the card's own frame.
  (The `liquid-glass` design guide also gives `R − i/1.528`, about 25 here, as the edge-exact value.) Clip images
  and draw borders with `GlassShape`, not `RoundedRectangle`, so they line up with the glass.
- Leave `refraction` unset. It then follows each surface's resting size, `min(60, 0.75 × shorter side)`
  (`GlassOptics.defaultRefraction(width:height:)`): 60 for cards and panels, 33 for a 44 pt button, 39 for a 52 pt
  toolbar, 46.5 for a 62 pt tab bar, unchanged by presses. A value set on a surface, `GlassScene(refraction:)` or
  `.glassRefraction(_:)` applies at every size, so `GlassScene(refraction: 60)` makes small controls smear backdrop
  in from outside their outline; `.glassRefraction(nil)` restores the default. 0 turns it off; very high values make
  the backdrop unrecognizable.
- `motion:` parameters are `GlassMotion?`, so `motion: .none` means `nil` (inherit) and only warns. Write
  `motion: GlassMotion.none`; the `.glassMotion(.none)` modifier takes a non-optional value and is fine.
- A `Double` or `CGFloat` variable or an expression is not a `GlassRadius`: write `radius: .points(value)` or
  `.points(34 - 14)`; only bare literals like `24` convert (`radius: 34 - 14` does not compile).
- `interactive: true` on a `GlassSurface` (draggable cards, lenses) adds press grow, drag stretch and glow; buttons,
  toolbars, tab bars and thumbs are interactive already. Static panels stay non-interactive.

**Toolbars, tab bars and menus**

- Put `GlassButton` and `GlassMenu` directly inside `GlassToolbar`: they drop their own glass and highlight at 8%
  foreground. Their `tint`, `prominent` and `material` are then ignored, so put a primary action as its own
  `GlassButton` beside the toolbar. Do not wrap a toolbar in a `GlassSurface` or give its items `.liquidGlass`.
- Keep toolbars, tab bars and menus in the scene's foreground: outside a scene they draw the fallback material and a
  `GlassMenu` falls back to the system `Menu`. Let the scene cover the window so menus have room.
- A menu panel opens below its trigger with trailing edges aligned, flips above when it does not fit below but fits
  above, and keeps 8 pt from the scene's left and right edges. It never scrolls: a menu that fits on neither side
  runs past the scene's bottom edge. Keep menus short (about 8 items or fewer); there are no submenus (a
  `GlassMenu` inside a panel never opens).
- Put only `GlassMenuItem`, `GlassMenuDivider` and `ForEach` of them in a menu. The panel renders in the scene's
  overlay with the scene's environment, so `.environment(model)`, `.glassAppearance`, `.disabled` or `.font` set
  between the scene and the menu do not reach its items (a custom row reading an environment object injected there
  crashes). Set appearance on the menu or the scene, and pass data into item actions through closures.
- `GlassTab(value:)` must have exactly the selection's type (`value: Page.home` for `Binding<Page>`); a mismatched
  value is never selected. `ForEach` inside a tab bar, toolbar or menu is fine.
- `GlassMenu(_:systemImage:…)` takes only a `LocalizedStringKey`: literals and interpolation (`"Sort: \(name)"`)
  work; pass a `String` variable as `LocalizedStringKey(name)`. `GlassButton` titles and `icon:label:`,
  `GlassMenu(icon:label:)`, `GlassTab`, `GlassSwitch` and `GlassMenuItem` also accept a `String`.
- For a choice, give items `checked:` (`GlassMenuItem("Grid", checked: layout == .grid) { layout = .grid }`): they
  draw a checkmark column and VoiceOver reads the checked one as selected; outside a scene they become toggles in the
  system `Menu`. `.disabled(true)` on an item dims it and the arrow keys and Return skip it.
- `GlassMenu(radius:)` takes points; `.capsule` or `.circle` falls back to 28.

**Rendering, limits and performance**

- No fixed surface limit, but each surface costs one copy of the backdrop (its whole view tree) plus one shader pass
  over its padded bounds; Regular adds a GPU blur. Keep about a dozen visible surfaces per scene (16 is the
  comfortable ceiling). A toolbar costs one per cluster, a tab bar two (bar and lens), slider and switch thumbs one
  only while held, an open menu one more.
- Each `GlassProgressiveBlur` renders six blurred copies of the backdrop (one per blur level), so count it as six
  surfaces; use at most one per edge, 80 to 130 pt deep.
- Keep the backdrop lean: hundreds of views are instantiated once per surface. `GlassScrollView` lays content out
  at its ideal length, so `LazyVStack` renders every row.
- Springs run on a display link only while something moves; idle glass costs nothing per frame. On ProMotion iPhones
  set `CADisableMinimumFrameDurationOnPhone` to `YES` in the app's Info.plist, or the glass springs run at 60 Hz
  (iPad needs nothing); on macOS the link follows the key window's screen.
- Glass renders whole, rim included, over transparent, narrow or clipped backdrops, and an outline thinner than 3 pt
  (an empty label, a first layout pass) draws no glass, so collapsed views leave no stray rim.
- Color: shaders expect SwiftUI's default (non-linear) pipeline. Never put a scene inside
  `.drawingGroup(colorMode: .linear)`.
- Fallbacks: outside a scene, `GlassSurface` uses `Material.regular` / `Material.thin` with the rim light,
  `GlassProgressiveBlur` uses `.ultraThinMaterial` under the same fade, `GlassMenu` uses the system `Menu`.
- Build with Xcode 16 or `xcodebuild` (also in CI); `swift build` does not compile the Metal library.

**Accessibility**

- Reduce Transparency swaps the material for an opaque fill (`#F2F2F7` / `#1C1C1E` at 94%) and keeps the rim;
  Increase Contrast switches to Regular and adds a 1 pt inner border; Reduce Motion turns `.full` into `.reduced`
  (no grow, stretch or overshoot; the tab lens, switch thumb and thumb lift move as critically damped transitions).
  All automatic.
- `.glassMotion(.none)` (or `GlassScene(motion: GlassMotion.none)`) removes every transition, for UI tests and
  screenshots.
- Dynamic Type scales button, toolbar and tab bar heights. Toolbars and tab bars stop growing, with their labels, at
  `.xxxLarge` on their own, and a long press on a toolbar item (button or menu trigger) or a tab shows it in the
  Large Content Viewer. Never fix bar heights.
- VoiceOver: controls keep their roles (`GlassButton` is a `Button`, `GlassSwitch` a `Toggle`, `GlassSlider` exposes
  a standard `Slider`, an iOS `GlassTabBar` is a tab bar, checked menu items are selected); `.keyboardShortcut` and
  focus work as usual. Icon-only buttons and menus use their `label:` as the VoiceOver label and macOS tooltip. Give
  a `GlassSlider` a `label` (or an `accessibilityLabel` when using the label-less init); the label is not drawn, so
  show a visible `Text` or icons beside the slider. Glass layers are hidden from accessibility; a `GlassSurface`
  contains its children, so give a grouping card an `.accessibilityLabel`. Hide decorative backdrops with `.accessibilityHidden(true)` inside `GlassContent`.
- `.disabled` dims a `GlassButton` (45%) and a `GlassMenuItem`, but a disabled `GlassSlider` or `GlassSwitch` looks
  enabled: pair `.disabled(off)` with `.opacity(off ? 0.45 : 1)`.
- Keyboard (macOS): tab bars (with a focus ring, arrows in visual order) and sliders use the arrow keys, switches
  Space, menus arrows, Return and Escape, a focused `GlassScrollView` the arrows, Page Up and Down, Space
  (Shift-Space up), Home and End. Haptics (iOS): tab selection and stepped sliders play selection feedback, switches
  a light impact.
- Right to left (`layoutDirection`, Arabic, Hebrew) mirrors every component with nothing to set: glass refracts its
  mirrored position, toolbar clusters and menus mirror, the tab lens and arrow keys follow visual order, sliders fill
  from the right, and `GlassProgressiveBlur(edge: .leading)` blurs toward the right edge.

## Checklist

- [ ] Package added (`from: "1.0.0"`), product `LiquidGlass`, deployment target iOS 17 / macOS 14, built with
      Xcode 16 or later, `import LiquidGlass`; `CADisableMinimumFrameDurationOnPhone` = `YES` in an iPhone app's
      Info.plist.
- [ ] One `GlassScene` per screen with a definite size (never inside a `ScrollView`); `GlassContent` first,
      `.ignoresSafeArea()`, pure SwiftUI, no `@State`, `AsyncImage` or side effects inside, no glass inside,
      explicit padding for bars.
- [ ] Scrolling backdrop uses `GlassScrollView` with a `GlassScrollPosition` held in `@State` outside the content;
      no glass floats over a foreground `Map`, video or `List`.
- [ ] Foreground in one container when it holds a menu; bars pinned with `VStack` + `Spacer` or alignment; tab bar
      width capped on iOS, `.fixedSize()` on macOS.
- [ ] Clear over imagery, Regular for compact panels with text and bars over text; no glass list rows; capsules,
      circles, concentric radii; at most one tinted or prominent control per view, outside toolbars.
- [ ] A screen forced dark or light sets the color scheme on the scene (`appearance:` alone changes only the glass
      and what is drawn on it); no hand-set white labels on tinted glass.
- [ ] Toolbar items are `GlassButton` / `GlassMenu` placed directly in `GlassToolbar`; `GlassTab` values match the
      selection type; menus short; presented views have their own `GlassScene`.
- [ ] Icon-only controls have meaningful `label:`s; sliders have labels and visible captions; disabled sliders and
      switches dimmed; text readable over the busiest backdrop area in light and dark.
- [ ] Built and run from Xcode: rim crisp, refraction pulls the backdrop inward at the rim, Reduce Transparency,
      Increase Contrast and Reduce Motion checked.

## References

- [references/api.md](references/api.md): every public type, initializer, parameter default, enum case,
  environment value and modifier, with a snippet per component.
- [references/recipes.md](references/recipes.md): six complete views: app shell with a floating tab bar, toolbar
  over a photo, media card with a slider, settings card with switches, glass menu from a button, progressive blur
  over a scrolling list.
