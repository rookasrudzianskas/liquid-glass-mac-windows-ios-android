# Liquid Glass for SwiftUI (iOS and macOS)

`LiquidGlass` is a native Liquid Glass library for SwiftUI on **iOS 17+** and **macOS 14+**, rendered with Metal. It implements the shared optical model of [`spec/OPTICS.md`](../spec/OPTICS.md): continuous-corner shapes, bevel refraction, the Clear and Regular materials, tint, rim light, coverage antialiasing, the pointer glow, the elastic press-and-drag response and progressive blur. The same equations, constants and golden test vectors drive the Android, Windows and web ports, so a design looks the same everywhere.

It does not depend on iOS 26's `glassEffect`. No type is called plain `Glass`, so the library coexists with SwiftUI's own glass on newer systems.

![Liquid Glass Studio on iPhone](images/apple/ios-hero.png)

- [Install](#install)
- [Quick start](#quick-start)
- [Concepts](#concepts)
- [Components](#components)
- [Options and theming](#options-and-theming)
- [Accessibility](#accessibility)
- [Performance](#performance)
- [How it works](#how-it-works)
- [Limitations](#limitations)
- [Liquid Glass Studio](#liquid-glass-studio)
- [Building and testing](#building-and-testing)
- [FAQ](#faq)

## Install

Swift Package Manager, in Xcode: **File › Add Package Dependencies…** and enter

```
https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android
```

or in `Package.swift`:

```swift
dependencies: [
    .package(url: "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android", from: "1.0.2"),
],
targets: [
    .target(name: "MyApp", dependencies: [
        .product(name: "LiquidGlass", package: "liquid-glass-mac-windows-ios-android"),
    ]),
]
```

In Xcode, the dependency rule **Up to Next Major Version** from `1.0.2` is the same requirement as `from: "1.0.2"`. To follow unreleased changes instead, choose the **Branch** rule with `main` (`branch: "main"` in `Package.swift`).

Requirements: iOS 17 or macOS 14 deployment targets, built with Xcode 16 or later (iOS 18 / macOS 15 SDK). Xcode 16 is required: the manifest uses Swift tools 6.0, so Xcode 15 cannot resolve the package (the iOS code also uses the iOS 18 SDK behind an availability check). There are no third-party dependencies.

> The package contains Metal shaders. Xcode compiles them (in an app, with `xcodebuild`, or in Xcode's own package build). The bare command line `swift build` does not compile Metal, so build and test the package with Xcode or `xcodebuild`.

## Quick start

```swift
import LiquidGlass
import SwiftUI

struct ContentView: View {
    var body: some View {
        GlassScene {
            GlassContent {
                Image("wallpaper")
                    .resizable()
                    .scaledToFill()
            }
            .ignoresSafeArea()

            VStack(spacing: 24) {
                GlassSurface(material: .regular, radius: 32, interactive: true) {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Liquid Glass").font(.largeTitle.bold())
                        Text("Refraction, frost and light.")
                    }
                    .padding(28)
                }

                HStack {
                    GlassButton("Play", systemImage: "play.fill") { }
                    GlassButton(icon: "heart", label: "Like") { }
                }
            }
        }
    }
}
```

Everything glass lives inside a `GlassScene`. The scene's first element, `GlassContent`, is the backdrop glass refracts; everything after it is the foreground, where glass can sit at any depth: in stacks, in a native `ScrollView`, in your own views.

For a complete app, open [`examples/swiftui/HelloGlass`](../examples/swiftui/HelloGlass): one multiplatform iOS and macOS target with a Regular glass card, a toolbar, a tab bar and a switch between Regular and Clear, the same screen as the Compose and WPF examples.

## Concepts

| Concept | API |
| --- | --- |
| Scene: the region glass may refract | `GlassScene { … }` |
| Backdrop layer the glass refracts | `GlassContent { … }` |
| Glass container | `GlassSurface { … }` and `.liquidGlass(…)` |
| Button | `GlassButton` |
| Toolbar with clusters and spacers | `GlassToolbar`, `GlassToolbarSpacer` |
| Tabs with a moving glass lens | `GlassTabBar`, `GlassTab` |
| Slider | `GlassSlider` |
| Switch | `GlassSwitch`, `GlassSwitchStyle` |
| Menu that grows out of its trigger | `GlassMenu`, `GlassMenuItem`, `GlassMenuDivider` |
| Progressive edge blur | `GlassProgressiveBlur` |
| Continuous-corner shape | `GlassShape` (an `InsettableShape`) |
| Scrolling backdrop | `GlassScrollView`, `GlassScrollPosition` |
| Optical model on the CPU | `GlassOptics`, `GlassSpring` |

### GlassScene and GlassContent

```swift
GlassScene(material: .regular, appearance: .dark) {
    GlassContent { MapSnapshot() }
        .ignoresSafeArea()          // backdrop under the status bar and home indicator
    MyForeground()                  // respects the safe area as usual
}
```

- A scene fills the space it is offered, like `GeometryReader`, and lays its foreground over the backdrop like a `ZStack`.
- `GlassContent` must be the **first** element. The scene builder takes a fixed list of views; put `if` or `ForEach` inside a container (`ZStack`, `Group`, `VStack`).
- `GlassContent { … }.ignoresSafeArea(_:)` extends the backdrop under the safe area while the foreground keeps respecting it.
- Scene parameters (`material`, `appearance`, `refraction`, `motion`) become the defaults of every glass element inside. `nil` inherits from the environment. A scene's `refraction` applies at every size; leave it `nil` and each surface's travel follows its own size (see [Refraction](#options-and-theming)).

### What can go in GlassContent

The backdrop renders once on screen and once more, clipped, inside every glass surface (see [How it works](#how-it-works)). So:

- Keep it **a pure function of external state**: pass bindings or `@Observable` models into it. `@State` stored *inside* the content is separate in each copy.
- Use **pure SwiftUI views**: images, shapes, gradients, text, `Canvas`, custom layouts, color and layer effects. Views backed by UIKit or AppKit (native `ScrollView`, `List`, `Form`, `TextField`, `TextEditor`, `Map`, `VideoPlayer`, `WebView`, any `UIViewRepresentable`/`NSViewRepresentable`) cannot be drawn into a shader effect: SwiftUI drops them from the glass layer, so glass over them shows nothing. Place such views in the foreground instead, or use `GlassScrollView` to scroll a backdrop.
- Its layout does not see safe-area insets; give it explicit padding where it needs room for bars.
- Glass surfaces placed *inside* `GlassContent` render without glass in the copies (glass never refracts glass).

## Components

### GlassSurface

```swift
GlassSurface(material: .regular, radius: 28, tint: .blue, interactive: true) {
    Label("Now Playing", systemImage: "music.note").padding()
}

// The same, as a modifier:
Label("Now Playing", systemImage: "music.note")
    .padding()
    .liquidGlass(.regular, radius: 28, tint: .blue, interactive: true)
```

| Parameter | Default | |
| --- | --- | --- |
| `material` | scene's (`.clear`) | `.clear` or `.regular` |
| `appearance` | scene's (`.automatic`) | `.automatic`, `.light`, `.dark` |
| `radius` | `16` | points, `.capsule` or `.circle` |
| `refraction` | scene's, else by size: `min(60, 0.75 × shorter side)` | optical travel at the rim, points |
| `tint` | none | colors the glass; the content's default foreground turns white or `#111113`, whichever contrasts more |
| `interactive` | `false` | press grow, drag stretch, glow |
| `motion` | scene's (`.full`) | `.full`, `.reduced`, `.none` |

Content draws above the glass, clipped to the outline, in the glass's foreground color (`#111113` on light glass, white on dark; on tinted glass white or `#111113`, whichever has the higher contrast with the tinted glass). A `foregroundStyle` you set inside wins. Interactive surfaces grow under a press, stretch toward a drag with constant volume, settle with a gel-like overshoot and light up under the finger (and under the pointer on macOS and iPad). Surfaces you move yourself (with `.offset`, a `DragGesture`, layout changes or SwiftUI animations) keep refracting exactly what is under them, and a surface that SwiftUI animates to a new size (`withAnimation`, `.animation(_:value:)`) carries its outline and rim along the content's animated frame; without an animation the glass takes the new size at once, like the content.

### GlassButton

```swift
GlassButton("Play", systemImage: "play.fill") { play() }
GlassButton(icon: "plus", label: "Add") { add() }                 // icon-only circle
GlassButton("Buy", prominent: true) { buy() }                     // accent-tinted glass, white label
GlassButton("Delete", systemImage: "trash", role: .destructive) { delete() }  // red label
GlassButton("Warning", tint: .yellow) { }                         // dark label on light tints
GlassButton("Frosted", material: .regular) { }
GlassButton("Static", interactive: false) { }                     // no grow, stretch or glow
GlassButton(icon: "xmark", label: name) { close() }               // a String label works too
GlassButton(radius: 12, action: { }) { Text("Custom label") }     // any label, any outline
```

Every initializer takes `role`, `material`, `appearance`, `refraction`, `tint`, `prominent`, `interactive` (default `true`) and `motion`; `nil` inherits from the scene. Unset everywhere, the refraction follows the button's size: 33 points for a regular 44 pt iOS button, 21 for a 28 pt macOS one.

Sizes follow `controlSize`:

```swift
GlassButton("Small") { }.controlSize(.small)     // 34 pt (iOS), 22 pt (macOS)
GlassButton("Regular") { }                        // 44 pt, 28 pt
GlassButton("Large") { }.controlSize(.large)     // 52 pt, 34 pt
```

Heights scale with Dynamic Type. Labels are 17 pt semibold on iOS and 13 pt medium on macOS. On tinted and prominent glass the label is white or `#111113`, whichever has the higher WCAG contrast with the tinted glass: white on blue, red or purple, dark on yellow, mint or green. A destructive button draws an untinted label in red. Icon-only buttons are circles whose title becomes the VoiceOver label and the macOS tooltip. A `GlassButton` is a SwiftUI `Button`, so `.disabled`, `.keyboardShortcut`, roles and focus work as usual.

### GlassToolbar and GlassToolbarSpacer

```swift
GlassToolbar {
    GlassButton(icon: "chevron.left", label: "Back") { }
    GlassToolbarSpacer()                    // splits the row into clusters
    GlassButton(icon: "bold", label: "Bold") { }
    GlassButton(icon: "italic", label: "Italic") { }
    GlassToolbarSpacer(.fixed)              // a fixed 8 pt gap
    GlassMenu(icon: "ellipsis", label: "More") { … }
}
```

Each cluster sits on its own capsule (52 pt tall with a 4 pt inset on iOS, 34 and 3 on macOS). Buttons and menus inside a toolbar drop their own glass and highlight with the foreground color at 8% when pressed, hovered or focused; pressing anywhere on a cluster grows it and lights it under the finger. Items take the toolbar's foreground (and, under a forced `appearance`, its color scheme; on a `tint`, white or dark for contrast). At the largest text sizes the bar stops growing and a long press shows an item in the Large Content Viewer. Right to left, the clusters mirror with their items. `GlassToolbar(material:appearance:tint:interactive:motion:)`.

### GlassTabBar

```swift
enum Page: Hashable { case home, search, library }
@State private var page = Page.home

GlassTabBar(selection: $page) {
    GlassTab("Home", systemImage: "house", value: Page.home)
    GlassTab("Search", systemImage: "magnifyingglass", value: Page.search)
    GlassTab("Library", systemImage: "books.vertical", value: Page.library)
}
```

A glass lens of the bar's own material (so a Regular bar stays frosted under the selection) slides to the selected tab on the track spring and stretches with its speed; drag across the bar to carry the lens and release to choose the tab under it. On touch screens the lens follows a finger only once it moves sideways, so a vertical swipe that starts on the bar still scrolls an enclosing scroll view. On iOS the bar is 62 pt tall with icon and label, the selected item takes the accent color, the bar and its labels stop growing at the largest text sizes (the Large Content Viewer shows them) and the bar carries the tab bar accessibility trait. `.controlSize(.small)` makes a compact, segmented-style bar (40 pt on iOS, 24 pt on macOS). On macOS the bar is 28 pt tall with labels, unselected labels at 85%, it draws a focus ring when it has keyboard focus and the arrow keys move the selection (in visual order, right to left too). Haptic selection feedback plays on change. `GlassTabBar(selection:material:appearance:tint:interactive:motion:)`.

### GlassSlider

```swift
@State private var volume = 40.0

GlassSlider(value: $volume, in: 0...100) { Text("Volume") }
GlassSlider(value: $volume, in: 0...100, step: 10, tint: .orange) { Text("Volume") }
```

The thumb is an opaque white capsule at rest and lifts into clear glass while held (20 × 16 pt growing 2.5 × 2 pt per side with refraction 5 on macOS; 36 × 24 pt on iOS for touch). The lifted lens refracts the track and fill beneath it and stretches along the track with its speed. Press anywhere on the track to jump there and keep dragging; on touch screens a drag starts once the finger moves sideways, so a vertical swipe that starts on a slider scrolls the page and leaves the value alone. Stepped sliders play a selection haptic per step. VoiceOver sees a standard adjustable slider (`accessibilityRepresentation`), and on macOS the arrow keys adjust it when focused. `onEditingChanged` reports drags. `.disabled(true)` dims the whole slider (track, fill and thumb) to 45%.

### GlassSwitch

```swift
@State private var wifi = true

GlassSwitch("Wi-Fi", isOn: $wifi)
GlassSwitch("AirDrop", isOn: $airdrop, tint: .green)
Toggle("Bluetooth", isOn: $bluetooth).toggleStyle(GlassSwitchStyle())
```

Tap to toggle or drag the thumb across. The thumb travels on the measured switch spring (response 0.47 s, damping 0.7) and lifts into clear glass while held (32 × 20 pt in a 54 × 24 pt track, growing 9 × 5.5 pt per side with refraction 7.75 on macOS; 37 × 24 in 63 × 28 on iOS). The track takes `tint` (the accent color by default) when on. It's a `Toggle`, so VoiceOver and focus work as usual; Space toggles it on macOS. On iOS 18 and macOS 15, `labelsHidden()` hides the label (it stays the VoiceOver label) and the switch hugs its track; iOS 17 and macOS 14 don't tell toggle styles about hidden labels, so there the label still shows (leave the title empty and set `accessibilityLabel` instead). `.disabled(true)` dims the track and thumb to 45%.

### GlassMenu

```swift
GlassMenu("Sort", systemImage: "arrow.up.arrow.down") {
    GlassMenuItem("Newest", systemImage: "clock") { sort = .newest }
    GlassMenuItem("Oldest", systemImage: "clock.arrow.circlepath") { sort = .oldest }
    GlassMenuDivider()
    GlassMenuItem("Reset", systemImage: "trash", role: .destructive) { reset() }
}

GlassMenu(icon: "ellipsis", label: "More") { … }   // icon-only trigger, the icon button circle

GlassMenu("Appearance", systemImage: "circle.lefthalf.filled") {
    GlassMenuItem("Light", checked: appearance == .light) { appearance = .light }
    GlassMenuItem("Dark", checked: appearance == .dark) { appearance = .dark }
    GlassMenuItem("Export…", systemImage: "square.and.arrow.up") { export() }
        .disabled(!canExport)
}
```

![A Regular glass menu with its legibility backing, grown out of its trigger](images/apple/ios-menu.png)

The trigger is a `GlassButton` (an icon-only trigger is the 44 pt circle on iOS, 28 pt on macOS); `appearance` and `tint` apply to the trigger and the panel. Items with `checked:` draw a checkmark in a leading column when `true` (VoiceOver reads them as selected); once any item of a panel has `checked:`, every item of that panel keeps the column, so icons and titles line up as in a system menu; disabled items dim and the arrow keys skip them. Opening morphs the trigger's glass into a Regular glass panel (radius 28) on the morph spring while the items condense out of a blur (an unset refraction is the open panel's throughout); closing reverses it. As the panel opens, its glass mixes toward a solid backing, `#F5F5F7` light and `#1C1C1E` dark, by up to 0.6 (`GlassOptics.menuLegibility`, [`spec/OPTICS.md` §3](../spec/OPTICS.md#legibility-backing-menus)) times the open progress, clamped to 1 while the morph spring overshoots, so the glass leaves the trigger as the trigger's own glass, and the open panel still refracts and frosts what is behind it at 40% strength. Items take the menu's label color, `GlassOptics.menuForeground(tint:material:dark:)`: the appearance foreground (`#111113` light, white dark) on an untinted menu; on a tinted one, white or `#111113`, whichever has the higher WCAG 2 contrast with the tint as the panel draws it, mixed toward the backing (`mix(tint · (body + (1 − body) · 0.5), backing, 0.6)`, body 0.78 Regular and 0.68 Clear), so a light menu tinted `.blue` takes `#111113` items where its trigger keeps a white label. The label color stays the same while the panel opens and closes. **Floors:** untinted menus, Regular or Clear, in both appearances, keep at least 5.2:1 contrast with their items over any backdrop, highlighted items included (5.24:1 at worst, a Clear menu in dark appearance over white; 6.51:1 for white over white on a dark Regular menu without the highlight, where Regular glass alone gives 1.8:1, and 8.74:1 for `#111113` over black in light). Menus tinted with the system colors, as they resolve in either appearance, keep at least 5.0:1, highlighted items included (5.07:1 at worst, the dark-appearance `.yellow` on a highlighted item of a dark Regular menu over white; the light-appearance system colors keep 5.26:1). Very light tints (pure yellow and white, 4.37:1 and 4.13:1 at worst) can fall below 4.5:1 over a white backdrop in dark appearance: tint a menu with a deeper color, or not at all. Destructive items keep a floor of their own: `role: .destructive` draws the title, icon and checkmark in `GlassOptics.menuDestructive(dark:)`, `#560D0A` (system red darkened) on a light menu, at least 5.0:1, and in dark appearance a light pink, `#FFEEED`, so they stay readable on any backdrop: at least 4.5:1, highlighted items included (the title carries the meaning); system red itself would fall to about 1.3:1 there at worst, and over a white backdrop no saturated red reaches 4.5:1 on dark menu glass. Their highlight mixes the menu's label color, not red. In the system `Menu` fallback outside a scene, the system draws them. No other surface has the backing. The panel opens below the trigger with trailing edges aligned, flips above when it does not fit below but fits above, and keeps 8 pt from the scene's left and right edges; it never scrolls, so a menu that fits on neither side runs past the bottom (keep menus short). A highlighted item (hovered, focused with the keyboard or pressed) mixes the label color into the glass at 8% (`GlassOptics.menuHighlight`). Tap outside, press Escape, or choose an item to dismiss; the up and down arrows and Return navigate. The panel draws in the scene's overlay, above the rest of the foreground. Outside a `GlassScene`, `GlassMenu` falls back to the system `Menu` with the same items.

### GlassProgressiveBlur

```swift
GlassScene {
    GlassContent { GlassScrollView(position: scroll) { Articles() } }
        .ignoresSafeArea()

    VStack {
        GlassProgressiveBlur(edge: .top, size: 120)
            .ignoresSafeArea(edges: .top)
        Spacer()
        GlassProgressiveBlur(edge: .bottom)
            .ignoresSafeArea(edges: .bottom)
    }
}
```

Six blur levels over the sharp backdrop, level *i* at σ = `blur`·(*i*/6)², weighted by the smoothstep of the depth so the weights sum to 1 (OPTICS §7). Defaults: `size` 80, `blur` 20. It fills the width (or height) it is offered, never takes touches, and blurs the backdrop only (glass above it still refracts the sharp backdrop, so bars over scrolling text usually want `material: .regular`).

### GlassScrollView

```swift
@State private var scroll = GlassScrollPosition()

GlassContent {
    GlassScrollView(position: scroll) {
        VStack { ForEach(stories) { StoryCard($0) } }
            .padding(.top, 120)        // room for the toolbar; backdrops don't see the safe area
    }
}

GlassButton(icon: "arrow.up", label: "Top") { scroll.scrollTo(edge: .top) }
```

A scroll view drawn entirely by SwiftUI, so glass can refract it. On iOS it scrolls with a drag, decelerates like `UIScrollView` (× 0.998 per millisecond) and rubber-bands past the ends with `GlassOptics.rubberBand`, settling back on a critically damped spring. On macOS it follows the trackpad (with the system's momentum) and the mouse wheel, takes only its own window's wheel events (an AppKit scroll view or text view in front scrolls itself), and once it has focus scrolls with the arrow keys, Page Up and Down, Space (Shift-Space up), Home and End. `GlassScrollPosition` is `@Observable`: read `offset`, `contentSize`, `viewportSize` and `isScrolling`, or call `scrollTo(_:animated:)` and `scrollTo(edge:animated:)`. The position must live outside the content (in `@State` next to the scene) because every copy of the backdrop reads it. VoiceOver's three-finger scroll works. Content is laid out at its ideal length along the scrolling axes, so `LazyVStack` renders every row.

### GlassShape

```swift
Image("cover")
    .clipShape(GlassShape(radius: 24))

GlassShape(radius: .capsule).strokeBorder(.white.opacity(0.3), lineWidth: 1)
```

The same continuous-corner outline the shader draws: superellipse corners (exponent 3.26) whose diagonal inset matches a circular corner of radius *r*, blending into a capsule for large radii. `GlassShape` is an `InsettableShape`.

### GlassOptics and GlassSpring

The optical model is public, for custom renderers, layout and tests:

```swift
let params = GlassOptics.shapeParameters(width: 200, height: 64, radius: .capsule)
let field = GlassOptics.shapeField(x: 90, y: 0, width: 200, height: 64, extent: params.extent, exponent: params.exponent)
let travel = GlassOptics.defaultRefraction(width: 200, height: 64)  // 48: what the surface uses unset
let offset = GlassOptics.refractionOffset(distance: field.distance, nx: field.nx, ny: field.ny, refraction: travel)
let glass = GlassOptics.materialColor(SIMD3(1, 1, 1), material: .regular, dark: true)
let menu = GlassOptics.legibilityColor(glass, dark: true, amount: GlassOptics.menuLegibility)   // the menu panel's backing
let label = GlassOptics.menuForeground(tint: SIMD3(0, 0.478, 1), material: .regular, dark: false)  // #111113 on a light menu tinted blue

var state = GlassSpring.State(value: 0, target: 1)
state = GlassSpring.release.step(state, dt: 1 / 60)     // closed form, frame-rate independent
```

## Options and theming

Every option inherits through the environment, from the scene or any ancestor:

```swift
VStack { … }
    .glassMaterial(.regular)
    .glassAppearance(.dark)
    .glassRefraction(40)
    .glassMotion(.reduced)
```

- **Material.** `.clear` refracts sharply with a light tone curve. `.regular` frosts the backdrop (a σ 7.33 blur brightened, or deepened in dark appearance, by a wide fill blur) and keeps text legible over busy content. Menus default to `.regular` and add the legibility backing, so the text of an untinted menu, Regular or `.clear`, keeps at least 5.2:1 over any backdrop, even plain white or black, highlighted items included, and a menu tinted with a system color at least 5.0:1 (destructive items, in a deep or a pale red, at least 5.0:1 in light and 4.5:1 in dark).
- **Appearance.** `.automatic` follows the color scheme. `.light` and `.dark` force the tone curves and rim strength, and set the content's color scheme, so `.primary` and `.secondary` adapt.
- **Radius.** Numbers are continuous-corner radii; `.capsule` and `.circle` round fully.
- **Refraction.** Points of optical travel at the rim; 0 turns refraction off. Unset on the surface, the scene and the environment, it follows the surface's resting size, `min(60, 0.75 × shorter side)` (`GlassOptics.defaultRefraction(width:height:)`, [`spec/OPTICS.md` §2](../spec/OPTICS.md#2-refraction)): cards and panels get the full 60, while a 44 pt button travels 33, a 52 pt toolbar 39 and a 62 pt tab bar 46.5, so small controls never pull in backdrop from far outside their outline. It is measured before press and stretch, so a press doesn't change it, and the tab lens takes the size of the tab it rests at. A value you set, on the surface, with `GlassScene(refraction:)` or with `.glassRefraction(_:)`, is used as given at every size; `.glassRefraction(nil)` restores the size-based default. The slider and switch lenses use 5 and 7.75. Small controls (shorter than the 20 pt bevel) refract across their whole face.
- **Tint.** Colored glass transmits the backdrop's luminance through the pigment. Text and icons on tinted glass (surfaces, buttons, bars, menu triggers) default to white or `#111113`, whichever has the higher contrast with the tinted glass; menu items compare the tint mixed toward the menu's legibility backing (`GlassOptics.menuForeground`). On sliders and switches, `tint` colors the fill and track.
- **Motion.** `.full`, `.reduced` (no grow, stretch or overshoot; the light still fades, and the tab lens, the switch thumb and the thumb's lift into glass move as critically damped transitions) and `.none` (no transitions). The system Reduce Motion setting always applies on top.
- **Foreground.** Text and icons on glass default to `#111113` (light) and white (dark); a forced `.light` or `.dark` appearance also sets their color scheme. Set your own `foregroundStyle` inside a surface to override.
- **Right to left.** Every component mirrors in right-to-left layouts (`layoutDirection`, Arabic, Hebrew): glass refracts what is under its mirrored position, toolbar clusters and menus mirror with their items, the tab lens follows the finger and the arrow keys in visual order, sliders fill from the right and `GlassProgressiveBlur(edge: .leading)` blurs toward the leading (right) edge.

## Accessibility

- **Reduce Transparency** replaces the material with an opaque fill (`#F2F2F7` at 94% light, `#1C1C1E` at 94% dark) and keeps the rim light.
- **Increase Contrast** switches to the Regular material and adds a one-point inner border (black 18% light, white 28% dark).
- **Reduce Motion** removes grow, stretch and overshoot; springs become critically damped crossfades.
- **Dynamic Type** scales button, toolbar and tab bar heights and labels; toolbars and tab bars stop growing (with their labels) at the largest sizes, and a long press on a toolbar item or tab shows it in the Large Content Viewer.
- **VoiceOver.** Buttons, tabs, switches, sliders and menu items keep their roles, labels, values, selected states and actions. Icon-only buttons use their title as the label. iOS tab bars carry the tab bar trait and the selected tab the selected trait; checked menu items are selected. Menus are modal and close with the escape gesture. Glass layers are hidden from accessibility.
- **Keyboard (macOS).** Buttons and switches take focus (with Full Keyboard Access); tab bars and sliders respond to the arrow keys; menus to the arrow keys, Return and Escape (disabled items are skipped); switches to Space; a focused `GlassScrollView` to the arrows, Page Up and Down, Space, Home and End. Focused buttons and tab bars draw a ring in the glass outline's shape.
- **Haptics (iOS).** Tab selection and stepped sliders play selection feedback; switches play a light impact.

## Performance

- Each glass surface renders one clipped copy of the backdrop plus one fragment-shader pass over its own padded bounds. Cost scales with the glass area, not the screen.
- The Regular material adds one SwiftUI GPU blur of that copy and a 28-tap fill kernel in the shader.
- Springs run on a display link only while something moves; idle glass costs nothing per frame. On macOS the link follows the screen of the key window, so glass animates at that display's refresh rate (120 Hz on ProMotion). On ProMotion iPhones, apps get more than 60 Hz from a display link only with `CADisableMinimumFrameDurationOnPhone` set to `YES` in their Info.plist; set it so the glass springs run at 120 Hz like system scrolling (iPad needs nothing). Spring values are observed with Observation, so only the glass layers and transformed content redraw during an animation, never the backdrop or your views.
- `GlassScrollView` moves its content with an offset; scrolling redraws the offset, not the rows.
- Prefer a few large surfaces to many tiny overlapping ones, keep heavy backdrops (hundreds of views) lean because each copy instantiates them, and use `GlassProgressiveBlur` sparingly (it renders six blurred copies of its strip).

## How it works

### One copy of the backdrop per surface

SwiftUI's `layerEffect` (iOS 17, macOS 14) runs a Metal shader over a view's own pixels; it cannot read what lies beneath a view. Two designs can refract a backdrop:

1. **One shader pass over the scene's content**, with every surface's geometry as uniforms.
2. **One copy of the content per surface**, aligned to the visible content, with the shader applied to the copy.

LiquidGlass uses **per-surface copies**, after building and measuring both:

| | One pass over the content | Copy per surface (chosen) |
| --- | --- | --- |
| Geometry during SwiftUI animations | Preferences report the final layout, so glass lags or jumps while views animate | `visualEffect` reads render-time geometry: exact during drags, offsets, layout animations and transitions |
| Overlap and z-order | All glass is drawn beneath all surface content; a menu over a toolbar shows the toolbar's icons through the menu | Glass follows the view tree: a menu covers what is under it |
| State duplication | None | Content renders again in each copy; `@State` inside the content is per copy (pass state in from outside) |
| Animation sync | — | Copies share the transaction, so SwiftUI animations stay in sync |
| Cost | One full-screen pass | One pass per surface, clipped to its padded bounds |

Each copy measures its own on-screen frame at render time with `visualEffect` and moves (and under an ancestor's scale, rescales) the backdrop back into place, so the glass refracts what is exactly beneath it while it moves. The press growth and drag stretch are evaluated in the shader instead of as transforms, so the rim stays one device pixel wide while the glass grows. The glass layer outputs only the glass (`rim + glass·(1 − rim.a)` without the base term) and SwiftUI composites it over the visible backdrop, which is the spec's composite exactly.

### Touches

Interactive glass follows the finger without owning the touch. On iOS 18 and later a UIKit long-press recognizer with no minimum duration runs simultaneously with every other recognizer, never cancels or delays touches, and ends the press when an enclosing scroll view starts to pan; on iOS 17 and macOS, simultaneous SwiftUI drag gestures do the same job. Buttons keep their own taps and a scroll that starts on glass still scrolls (the Studio UI tests check both). Sliders and the tab lens take a drag only once the finger moves sideways; a vertical swipe that starts on them scrolls. On macOS, hover lights glass at the 0.35 level.

### Colors and blur, measured

- In SwiftUI's default (non-linear) rendering mode, layer effects receive **premultiplied, gamma-encoded sRGB** colors, on iOS 18 and macOS 15, on screen and in `ImageRenderer` (measured by rendering known colors through probe shaders). The shader unpremultiplies, runs the tone math on sRGB values and returns premultiplied sRGB. Under `drawingGroup(colorMode: .linear)` shaders would receive linear values; don't put a scene in a linear drawing group.
- SwiftUI's `blur(radius:)` measured as a Gaussian with σ ≈ 0.947 · radius (fitting blurred step edges on iOS and macOS), so the frost uses radius 7.33 / 0.947. `blur(radius:opaque: true)` did not blur at all on iOS 18, so the library never uses it.
- The Regular material's wide fill blur (σ 23 light, 21.8 dark) is the σ 7.33 frost sampled by a two-ring Gauss–Laguerre kernel with σ' = √(σ² − 7.33²), as OPTICS §3 allows; it preserves the kernel's variance and is exact on smooth backdrops.

## Limitations

- **UIKit/AppKit-backed views in the backdrop** don't appear in glass (see [What can go in GlassContent](#what-can-go-in-glasscontent)). Use `GlassScrollView` to scroll a backdrop.
- **Rotation.** Inside a `rotationEffect` (or a 3D transform) the refraction is misaligned: the backdrop copy tracks translation and scale, not rotation.
- **Simultaneous animated `scaleEffect` and `offset` on an ancestor.** SwiftUI's render-time geometry can disagree with the drawn transform while both animate together; the refraction is offset for the duration of that animation and correct when it settles. Animating either alone, layout, position or frame is exact.
- **Glass doesn't refract other glass** or foreground views, only the scene's backdrop. The WPF port also refracts enclosing glass and progressive blurs, and the Compose port a progressive blur beneath the glass ([COMPONENTS.md](../spec/COMPONENTS.md#behavior-every-port-implements)).
- **The backdrop must lay out identically in every copy**: no `GeometryReader`-driven state written back into the content, no dependence on safe-area insets, no randomness.
- **Very large backdrops** (thousands of views) are instantiated once per surface.
- **Linear color pipelines** (`drawingGroup(colorMode: .linear)`) are unsupported.

## Liquid Glass Studio

The demo app, in [`platforms/apple/Studio`](../platforms/apple/Studio), runs on iPhone, iPad and Mac:

1. **Hero:** a wallpaper under a draggable Regular glass card, a floating toolbar with the appearance switch, Share (the system share sheet) and a menu with checked appearance items, and the tab bar.
2. **Controls:** buttons in every size, tinted and icon-only, a toolbar, sliders, switches, a tab bar and menus, in a native `ScrollView` over a wallpaper (two columns on the Mac and on iPad in landscape). Content fades out under the status bar and above the tab bar; on iOS a haze over a progressive blur of the wallpaper keeps the status bar legible.
3. **Materials lab:** live material, appearance, refraction, radius, tint and wallpaper controls for a draggable lens, on a Regular glass panel with Regular pickers.
4. **Progressive blur:** a `GlassScrollView` of stories with blurred top and bottom edges under a Regular toolbar, with a Filter menu of story categories.

The floating tab bar is dark glass over the wallpapers, which all darken toward the bottom, so its labels read in either appearance; over the stories it is Regular glass in the app's appearance. Section headers drawn on a wallpaper sit on a soft dark haze.

<p align="center">
  <img src="images/apple/ios-controls.png" width="32%" alt="Controls, scrolled down: icon buttons, a toolbar, sliders and switches and a segmented tab bar over a wallpaper, above a glass tab bar on iPhone">
  <img src="images/apple/ios-lab.png" width="32%" alt="Materials lab: a draggable Clear glass card over the Dunes wallpaper, with material, appearance, refraction, radius, tint and wallpaper controls on iPhone">
  <img src="images/apple/ios-blur.png" width="32%" alt="Progressive blur: a journal of stories scrolling under blurred edges, with Regular glass toolbar buttons and a tab bar on iPhone">
</p>

![Liquid Glass Studio on macOS](images/apple/macos-controls.png)

Open `platforms/apple/Studio/LiquidGlassStudio.xcodeproj` in Xcode, pick the *LiquidGlassStudio* scheme and run on an iOS Simulator or *My Mac*; both build with the project's ad-hoc signing. To run on an iPhone or iPad, select the *LiquidGlassStudio* target, open **Signing & Capabilities**, turn on **Automatically manage signing** and choose your team (the project's manual ad-hoc identity can't sign for devices). On iPhone the Studio runs in portrait; on iPad and Mac in any orientation and window size. Launch arguments `-page hero|controls|lab|blur` and `-appearance automatic|light|dark` choose where it starts, and `-scroll end` or a fraction of the way down, such as `-scroll 0.76` (the iPhone Controls shot above), where the Controls page opens; `-AppleTextDirection YES -NSForceRightToLeftWritingDirection YES` shows it right to left. A prebuilt macOS build is on the website's downloads page. Unzip it and move *Liquid Glass Studio* to Applications first. It is ad-hoc signed and not notarized, so macOS blocks its first launch: open it once, then open System Settings › Privacy & Security and click Open Anyway (on macOS 14, Control-click › Open also works). Or, once it is in Applications, run `xattr -dr com.apple.quarantine "/Applications/Liquid Glass Studio.app"`.

The Xcode project is generated from `project.yml` with [XcodeGen](https://github.com/yonaskolb/XcodeGen); after adding files run `platforms/apple/scripts/generate-project.sh`. The app icon is drawn by `platforms/apple/Studio/Scripts/make-app-icon.swift`.

## Building and testing

```sh
# Unit, GPU and render tests (macOS and iOS Simulator)
xcodebuild test -scheme LiquidGlass -destination 'platform=macOS'
xcodebuild test -scheme LiquidGlass -destination 'platform=iOS Simulator,name=iPhone 16 Pro'

# The Studio app and its UI tests
xcodebuild test -project platforms/apple/Studio/LiquidGlassStudio.xcodeproj -scheme LiquidGlassStudio \
  -destination 'platform=iOS Simulator,name=iPhone 16 Pro'

# Release build of the macOS app, ad-hoc signed and zipped into artifacts/apple/, with LICENSE and NOTICE.md in the
# app's Resources and at the top of the zip
xcodebuild build -project platforms/apple/Studio/LiquidGlassStudio.xcodeproj -scheme LiquidGlassStudio \
  -configuration Release -destination 'generic/platform=macOS' -derivedDataPath DerivedData
platforms/apple/scripts/package-macos.sh DerivedData/Build/Products/Release
```

- `OpticsVectorTests` checks the Swift port against every category of [`spec/vectors.json`](../spec/vectors.json) at 1e-4: shape parameters, all 640 field samples, the outline, edge profile, default refraction, refraction offset, tone curves, material color, tint, frost, the legibility backing of menus (the amount 0.6, the 8% highlight, the backing colors, the mix, `menuForeground` for untinted and tinted Regular and Clear menus, and the label floors over a 6 × 6 × 6 grid of backdrops with and without the highlight: 6.51:1 and 8.74:1 over white and black, 5.24:1 untinted and 5.07:1 for the system tints of either appearance), all 328 rim samples, all 63 composited glass pixels (six of them menu glass with the backing at 0.6), the six springs, rubber band, stretch limit, press scale, glow and progressive weights.
- `ShaderVectorTests` runs the Metal functions the glass shader uses (`LiquidGlassCore.h`) on the GPU in a compute kernel and checks the same vectors at the GPU tolerance, 2e-3, the menu label floors included (the shader's menu glass over the same grid of backdrops).
- `RenderTests` renders real `GlassScene`s through SwiftUI and compares every pixel of a surface (and two around it) with the model: Clear and Regular, light and dark, tinted, at 1x and 2x, off center left to right and right to left, over layered, text, narrow and empty backdrops, under an ancestor's clip, the Reduce Transparency and no-scene fallbacks and degenerate sizes, and the refraction by size and its precedence (surface, environment, scene). It also checks that Regular glass frosts text smoothly (also over a `GlassScrollView`), that backdrop copies render as copies, that labels follow forced appearances and tints, that menu items of untinted Regular and Clear menus, light and dark, over white and over black keep at least 5.2:1 on plain glass and on a highlighted row (where a Regular surface without the backing gives less than 2.5:1), that menus tinted `systemBlue` and `systemYellow`, Regular and Clear, light and dark, with the colors resolved in light and dark mode, take `menuForeground`, match the model with their highlighted rows and keep at least 5.0:1, that a highlighted row mixes the label color in at 8% (fitted over the row to within half a percent, which tells it apart from the system label color at 85%), that under Reduce Transparency the menu's opaque fill mixes toward the backing too, with labels far above 5:1, and a fill tinted with the dark-appearance `systemYellow` keeps 5.0:1, and that bars, menus and progressive blurs mirror right to left.
- `BehaviorTests` covers what isn't a picture: the tab lens and switch thumb under drags, right to left and reduced motion, the lens's resting size and how refraction resolves, the press glow spring, menu keyboard navigation, the menu backing's amount through the morph (0 at the trigger, 0.6 open, never more while the spring overshoots), drag intent (sideways drags commit, vertical ones scroll), `GlassScrollPosition` after cancelled drags, keyboard and trackpad momentum, label contrast on tints, control sizes (icon menu circles, hidden-label switches, bars at accessibility sizes and in tall rows) and the initializers.
- `StudioUITests` drives the demo: navigation, menus (open, outside tap, choose), the stories filter, switches and sliders inside a scroll view, scrolls that start on a glass button, a slider and a tab bar (the value and the selection stay), `GlassScrollView` scrolling and a held press-and-drag.

CI ([`.github/workflows/apple.yml`](../.github/workflows/apple.yml)) runs the package tests on macOS and the iOS Simulator, builds the [Hello, Glass example](../examples/swiftui/HelloGlass) for both, builds the Studio app for macOS in Release, runs its UI tests and uploads the packaged app.

## FAQ

**Why not iOS 26's `glassEffect`?** It needs iOS 26 and macOS 26 and its look is not under your control. LiquidGlass runs on iOS 17 and macOS 14, matches the other platforms' ports pixel for pixel in its model, and every constant is documented in `spec/OPTICS.md`. The two can coexist.

**My glass shows nothing (or only the rim).** The backdrop is probably UIKit/AppKit-backed (a `List`, `ScrollView`, `Map`…), or the surface isn't inside a `GlassScene` (outside a scene, glass falls back to the system material with the rim light).

**The glass shows the wrong part of the backdrop.** Check that the backdrop doesn't depend on safe-area insets or state that differs between copies, and that the surface isn't rotated.

**Can I put glass in a native `ScrollView` or `List` row?** Yes: glass anywhere in the scene's foreground works, including in scroll views. Only the *backdrop* has to be pure SwiftUI.

**How do I make a sheet or popover glass?** Presentations render in their own window, outside your scene. Put a `GlassScene` inside the presented view with its own backdrop, or present your own overlay inside the scene's foreground.

**Does it work on visionOS, tvOS or watchOS?** Not yet; the package targets iOS and macOS.
