# API reference: LiquidGlass 1.0.0 (SwiftUI)

Every public declaration of the `LiquidGlass` module (the package's `Sources/LiquidGlass` folder), copied from the
source with its parameter defaults. Units are points. In every component, an optional option left `nil` inherits
from the enclosing `GlassScene` or from the environment (`.glassMaterial(_:)` and friends); the root defaults are
material `.clear`, appearance `.automatic`, refraction by size (`GlassOptics.defaultRefraction(width:height:)`,
`min(60, 0.75 × shorter side)` of the resting size) and motion `.full`.

```swift
import LiquidGlass   // module and product name; the package is liquid-glass-mac-windows-ios-android
import SwiftUI
```

Platforms: iOS 17+, macOS 14+ (`platforms: [.iOS(.v17), .macOS(.v14)]`), built with Xcode 16 or later: the manifest uses
Swift tools 6.0, so Xcode 15 cannot resolve the package (the iOS code also uses the iOS 18 SDK behind an availability
check). The Metal shaders
compile only in Xcode or `xcodebuild` builds.

## Contents

- [Option types](#option-types): `GlassMaterial`, `GlassAppearance`, `GlassMotion`, `GlassRadius`
- [Environment values and modifiers](#environment-values-and-modifiers)
- [GlassScene and GlassContent](#glassscene-and-glasscontent)
- [GlassSurface and liquidGlass](#glasssurface-and-liquidglass)
- [GlassShape](#glassshape)
- [GlassButton](#glassbutton)
- [GlassToolbar and GlassToolbarSpacer](#glasstoolbar-and-glasstoolbarspacer)
- [GlassTabBar and GlassTab](#glasstabbar-and-glasstab)
- [GlassSlider](#glassslider)
- [GlassSwitch and GlassSwitchStyle](#glassswitch-and-glassswitchstyle)
- [GlassMenu, GlassMenuItem and GlassMenuDivider](#glassmenu-glassmenuitem-and-glassmenudivider)
- [GlassProgressiveBlur](#glassprogressiveblur)
- [GlassScrollView and GlassScrollPosition](#glassscrollview-and-glassscrollposition)
- [GlassSpring](#glassspring)
- [GlassOptics](#glassoptics)
- [Metrics per platform](#metrics-per-platform)
- [System settings, fallbacks and accessibility](#system-settings-fallbacks-and-accessibility)

## Option types

### GlassMaterial

The optical material of a surface.

```swift
public enum GlassMaterial: String, CaseIterable, Hashable, Sendable {
    case clear     // Sharp, transparent glass. The default everywhere except menu panels.
    case regular   // Frosted: σ 7.33 blur brightened (light) or deepened (dark) by a wide fill blur, then refracted.
}
```

```swift
GlassSurface(material: .regular, radius: 28) { Text("Legible over busy content").padding() }
```

### GlassAppearance

Light or dark glass: selects the tone curves, rim strength and the foreground color of surface content (`#111113`
light, white dark).

```swift
public enum GlassAppearance: String, CaseIterable, Hashable, Sendable {
    case automatic   // Follow the environment's color scheme.
    case light       // Always the light material.
    case dark        // Always the dark material.

    /// Resolves the appearance against a color scheme. Returns `true` when the dark material applies.
    public func isDark(in colorScheme: ColorScheme) -> Bool
}
```

An explicit `.light` or `.dark` (on the scene, a surface, a bar or a menu) also sets the `colorScheme` of what is
drawn on that glass: the content of a `GlassSurface`, `.liquidGlass` or `GlassButton`, toolbar items, `GlassTab`
labels, the menu trigger and `GlassMenuItem`s, so `.primary` and `.secondary` match the glass.
`GlassScene(appearance: .dark)` alone therefore gives dark glass with light labels everywhere on glass. Plain views in
the foreground (not on glass) and the status bar keep the SwiftUI color scheme; to switch those too, set the color
scheme on the scene, which `.automatic` glass follows:

```swift
GlassScene { … }                         // dark glass, bars, menus and plain text over a night photo
    .environment(\.colorScheme, .dark)   // or .preferredColorScheme(.dark), which also switches the status bar
```

### GlassMotion

How much glass moves in response to input. The system Reduce Motion setting always applies on top (`.full` behaves
like `.reduced`). Under `.reduced` the tab lens, the switch thumb and the thumbs' lift into glass still move, as
critically damped transitions; under `.none` they jump.

```swift
public enum GlassMotion: String, CaseIterable, Hashable, Sendable {
    case full      // Press grow, volume-preserving drag stretch and gel-like overshoot.
    case reduced   // No grow, stretch or overshoot; the light still fades in and out.
    case none      // No transitions at all: every change applies immediately.

    /// Combines the requested level with the system Reduce Motion setting.
    public func resolved(reduceMotion: Bool) -> GlassMotion
}
```

Every `motion:` parameter is `GlassMotion?`: `motion: .none` is `Optional.none` (inherit) and only warns. Write
`motion: GlassMotion.none`. The `.glassMotion(_:)` modifier takes a non-optional value, so `.glassMotion(.none)` is
right.

```swift
ContentView().glassMotion(.none)                  // UI tests and screenshots
GlassSurface(motion: GlassMotion.none) { … }      // one surface without transitions
```

### GlassRadius

The corner radius of a glass outline. Numbers are continuous ("squircle") corner radii in points; `.capsule` and
`.circle` round the short sides fully (a circle when the outline is square).

```swift
public enum GlassRadius: Hashable, Sendable, ExpressibleByIntegerLiteral, ExpressibleByFloatLiteral {
    case points(Double)   // A continuous corner with this radius in points (nonnegative; negatives clamp to 0).
    case capsule          // Fully rounded short sides.
    case circle           // Identical to `capsule`.

    public init(integerLiteral value: Int)
    public init(floatLiteral value: Double)
}
```

Literals convert (`radius: 24`, `radius: 12.5`); variables and arithmetic do not: write `.points(value)`,
`.points(Double(cgFloat))` or `.points(34 - 14)` (`radius: 34 - 14` is an `Int` and fails to compile).

```swift
GlassSurface(radius: 24) { … }
GlassSurface(radius: .capsule) { … }
GlassSurface(radius: capsule ? .capsule : .points(radius)) { … }
```

## Environment values and modifiers

Inherited defaults for every glass element below the view that sets them. `GlassScene(material:appearance:
refraction:motion:)` sets the same values for its whole tree.

```swift
extension EnvironmentValues {
    public var glassMaterial: GlassMaterial { get set }      // default .clear (menus use .regular regardless)
    public var glassAppearance: GlassAppearance { get set }  // default .automatic
    public var glassRefraction: Double? { get set }          // default nil: each surface's size decides
    public var glassMotion: GlassMotion { get set }          // default .full; Reduce Motion always applies
}

extension View {
    public func glassMaterial(_ material: GlassMaterial) -> some View
    public func glassAppearance(_ appearance: GlassAppearance) -> some View
    public func glassRefraction(_ refraction: Double?) -> some View  // clamped to >= 0; nil restores by-size
    public func glassMotion(_ motion: GlassMotion) -> some View
}
```

```swift
VStack { GlassButton("One") {}; GlassButton("Two") {} }
    .glassMaterial(.regular)
    .glassRefraction(40)

struct Badge: View {
    @Environment(\.glassAppearance) private var appearance    // readable in your own views
    var body: some View { Text(appearance == .dark ? "Dark" : "Light") }
}
```

A component's own non-`nil` parameter beats the environment. A surface's `material` does not change the environment,
so controls inside a Regular card keep the scene's material. A refraction set in the environment (or on the scene)
applies at every size, small controls included; leave it `nil` for the size-based default.

## GlassScene and GlassContent

The region glass may refract. The scene renders its `GlassContent` as the backdrop and lays the foreground over it
like a `ZStack(alignment: .topLeading)` in which each foreground view is proposed the full scene size and centered.
It fills the space it is offered, like `GeometryReader` (inside a `ScrollView`, `List` or lazy stack it collapses to
10 pt, so give it a definite size). Glass at any depth of the foreground (also inside a native `ScrollView`) refracts
the backdrop at its exact on-screen position while it moves, scales or animates. Open `GlassMenu` panels draw in an
overlay above the foreground layer that holds the menu: each top-level foreground view gets its own overlay, so keep
the foreground in one container (`ZStack` or `VStack`) when it holds a menu. The scene sets only the glass defaults,
not `colorScheme` (a forced `appearance` sets the color scheme of what is drawn on glass only). Presentations
(`.sheet`, `.popover`, `.fullScreenCover`) attached inside the foreground inherit the scene's glass environment and
do not fall back reliably: attach them to the scene itself and give the presented view its own `GlassScene`.

```swift
public struct GlassScene<Backdrop: View, Foreground: View>: View {
    /// - material: The default material inside the scene, or `nil` to inherit.
    /// - appearance: The default appearance inside the scene, or `nil` to inherit.
    /// - refraction: The optical travel at the rim of glass inside that sets none, at every size, or `nil` to
    ///   inherit. Unset everywhere, each surface's resting size decides (`GlassOptics.defaultRefraction(width:height:)`).
    /// - motion: The default motion level, or `nil` to inherit.
    /// - content: A `GlassContent` followed by the foreground.
    public init(
        material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil,
        refraction: Double? = nil,
        motion: GlassMotion? = nil,
        @GlassSceneBuilder content: () -> GlassSceneLayers<Backdrop, Foreground>
    )
}

/// The backdrop that glass in a GlassScene refracts. Not a View: only valid as a scene's first element.
public struct GlassContent<Content: View> {
    public init(@ViewBuilder content: () -> Content)

    /// Extends the backdrop under the safe area on `edges`, while the scene's foreground keeps respecting it.
    public func ignoresSafeArea(_ edges: Edge.Set = .all) -> GlassContent
}

/// The backdrop and foreground of a scene, as built by GlassSceneBuilder (no public initializer).
public struct GlassSceneLayers<Backdrop: View, Foreground: View> {}

/// Builds a GlassScene: a GlassContent followed by the foreground views. A fixed sequence only:
/// no `if`, `switch`, `for` or `ForEach` at the top level.
@resultBuilder
public enum GlassSceneBuilder {
    public static func buildBlock<B: View>(_ content: GlassContent<B>) -> GlassSceneLayers<B, EmptyView>
    public static func buildBlock<B: View, F: View>(_ content: GlassContent<B>, _ foreground: F) -> GlassSceneLayers<B, F>
    public static func buildBlock<B: View, F0: View, F1: View, each F: View>(
        _ content: GlassContent<B>, _ f0: F0, _ f1: F1, _ rest: repeat each F
    ) -> GlassSceneLayers<B, TupleView<(F0, F1, repeat each F)>>
}
```

```swift
GlassScene(material: .regular, appearance: .dark) {
    GlassContent {
        Image("aurora").resizable().scaledToFill()
            .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
            .clipped()
    }
    .ignoresSafeArea()                         // backdrop under the bars; foreground keeps the safe area

    ZStack {                                   // one foreground container
        VStack { GlassToolbar { GlassButton(icon: "chevron.left", label: "Back") {} }; Spacer() }
            .padding(.horizontal, 16)
        GlassSurface(radius: 28) { Text("Hello").padding(24) }   // centered
    }
}
.environment(\.colorScheme, .dark)             // plain foreground views turn dark too
```

Rules for the content (it renders once on screen and once more, clipped, inside every surface):

- Pure SwiftUI only (`Image`, shapes, gradients, `Text`, `Canvas`, custom layouts, color and layer effects,
  `GlassScrollView`). UIKit/AppKit-backed views (`ScrollView`, `List`, `Form`, `TextField`, `TextEditor`, `Map`,
  `VideoPlayer`, web views, any `UIViewRepresentable`/`NSViewRepresentable`) are dropped from the copies.
- A pure function of external state: `@State` inside it is per copy, `.onAppear`/`.task` run once per copy, and
  `AsyncImage` downloads once per copy (load images outside and pass an `Image` in).
- Glass shows only this content: glass over a foreground `Map`, `VideoPlayer`, `List` or opaque view shows the
  backdrop hidden beneath it, not that view.
- Lays out without safe-area insets and identically in every copy (no `GeometryReader` write-back, no randomness).
- Modifiers such as `.id`, `.transition`, `.background` go inside the closure; `GlassContent` itself only has
  `ignoresSafeArea(_:)`.
- No glass inside it (in the copies, nested glass renders as its plain content).

## GlassSurface and liquidGlass

A glass container: refracts the scene's backdrop through a continuous-corner outline with a 20-point bevel, tones it
with the material, tints it and paints the rim light. Content draws above the glass, clipped to the outline, in the
glass foreground color, and is not refracted. The surface sizes to its content and adds no padding. Glass renders
whole, rim included, over transparent, narrow or clipped backdrops; an outline thinner than 3 pt (empty text, a first
layout pass) draws no glass.

```swift
public struct GlassSurface<Content: View>: View {
    /// - material: Clear or Regular glass; `nil` inherits the scene's material.
    /// - appearance: Light or dark glass; `nil` inherits.
    /// - radius: The corner radius, `.capsule` or `.circle`. Default 16.
    /// - refraction: Optical travel at the rim in points; `nil` inherits the scene's or, when none is set, follows
    ///   the surface's resting size: 60, or three quarters of the shorter side below 80 points.
    /// - tint: Colors the glass, never the content. The content's default foreground turns white or `#111113`,
    ///   whichever contrasts more with the tinted glass; a `foregroundStyle` you set inside wins.
    /// - interactive: Press grow, drag stretch and glow.
    /// - motion: Overrides the scene's motion level.
    /// - content: The content drawn on the glass.
    public init(
        material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil,
        radius: GlassRadius = 16,
        refraction: Double? = nil,
        tint: Color? = nil,
        interactive: Bool = false,
        motion: GlassMotion? = nil,
        @ViewBuilder content: () -> Content
    )
}

extension View {
    /// Places this view on a glass surface; equivalent to wrapping it in a GlassSurface with the same options.
    public func liquidGlass(
        _ material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil,
        radius: GlassRadius = 16,
        refraction: Double? = nil,
        tint: Color? = nil,
        interactive: Bool = false,
        motion: GlassMotion? = nil
    ) -> some View
}
```

| Parameter | Default | Notes |
| --- | --- | --- |
| `material` | `nil` (scene's, `.clear`) | Increase Contrast forces `.regular` |
| `appearance` | `nil` (scene's, `.automatic`) | explicit values also set the content's `colorScheme` |
| `radius` | `16` | points, `.capsule`, `.circle` |
| `refraction` | `nil` (scene's, else `min(60, 0.75 × shorter side)` of the resting size) | a press or stretch doesn't change it; 0 turns refraction off; surfaces shorter than the 20 pt bevel refract across their whole face |
| `tint` | `nil` | colored glass transmits the backdrop's luminance; clamped to sRGB; the content defaults to white or `#111113`, whichever has the higher WCAG contrast with the tinted glass (white on blue, red, purple; dark on yellow, mint, green, orange); a `foregroundStyle` set inside wins |
| `interactive` | `false` | grows under a press, stretches toward a drag, settles with overshoot, glows under the finger (and hovering pointer, level 0.35); ignored while `.disabled` |
| `motion` | `nil` (scene's, `.full`) | use `GlassMotion.none`, not `.none` |

```swift
GlassSurface(material: .regular, radius: 28, tint: .blue, interactive: true) {
    Label("Now Playing", systemImage: "music.note").padding()   // white on blue glass, automatically
}

Label("Now Playing", systemImage: "music.note")
    .padding()
    .liquidGlass(.regular, radius: .capsule, interactive: true)

// A draggable lens (the demo's Materials lab):
GlassSurface(radius: 48, interactive: true) { Image(systemName: "drop.fill").frame(width: 210, height: 210) }
    .offset(offset)
    .gesture(DragGesture().onChanged { offset = $0.translation })
```

Outside a `GlassScene`, a surface falls back to `Material.regular` (Regular) or `Material.thin` (Clear) in the same
outline with the rim light. Inside a backdrop copy it renders only its content.

## GlassShape

The continuous-corner outline the shader draws: straight edges joined by superellipse corners (exponent 3.26)
whose diagonal inset matches a circular corner of the same radius, blending into a capsule for large radii. Use it to
clip, stroke or hit-test content so it lines up with glass exactly.

```swift
public struct GlassShape: InsettableShape {
    public var radius: GlassRadius          // The corner radius.
    public var insetAmount: CGFloat         // How far the outline is inset from the shape's frame, in points.

    public init(radius: GlassRadius = 16)
    public func path(in rect: CGRect) -> Path
    public func inset(by amount: CGFloat) -> GlassShape   // insets keep the corners concentric (radius − inset)
    public var animatableData: CGFloat { get set }        // the inset amount
}
```

```swift
Image("cover").resizable().scaledToFill()
    .frame(height: 190).frame(maxWidth: .infinity)
    .clipShape(GlassShape(radius: 24))

GlassShape(radius: .capsule).strokeBorder(.white.opacity(0.3), lineWidth: 1)
GlassShape(radius: .points(34 - 14)).fill(.quaternary)   // concentric inside a 34 pt surface padded 14
GlassShape(radius: 34).inset(by: 14).fill(.quaternary)    // the same outline, drawn in the surface's own frame
```

## GlassButton

A SwiftUI `Button` on interactive glass. The outline is a capsule; icon-only buttons are circles. Height follows
`.controlSize` and scales with Dynamic Type. A prominent button tints its glass with the accent color. On tinted and
prominent glass the label is white or `#111113`, whichever has the higher WCAG contrast with the tinted glass (white
on blue, red or purple; dark on yellow, mint or green). `role: .destructive` draws an untinted label in red. Inside a
`GlassToolbar` it drops its own glass and highlights with the foreground color at 8% when pressed, hovered or focused;
there its glass options (`material`, `appearance`, `refraction`, `tint`, `prominent`, `interactive`) are ignored
(put a primary action beside the toolbar). Disabled buttons draw at 45% opacity and stop reacting.

```swift
public struct GlassButton<Label: View>: View {
    /// - role: The button's role, as for `Button`; `.destructive` draws an untinted label in red.
    /// - radius: The outline; `nil` is a capsule.
    /// - material: Clear or Regular glass; `nil` inherits the scene's.
    /// - appearance: Light or dark glass; `nil` inherits.
    /// - refraction: Optical travel at the rim in points; `nil` inherits the scene's or, when none is set, follows
    ///   the button's resting size (33 points for a 44-point button).
    /// - tint: Colors the glass. The label turns white or dark, whichever contrasts more with the tinted glass.
    /// - prominent: Tints the glass with the accent color when `tint` is `nil`.
    /// - interactive: Press grow, drag stretch and glow. Default `true`.
    /// - motion: Overrides the scene's motion level.
    public init(
        role: ButtonRole? = nil, radius: GlassRadius? = nil, material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil, refraction: Double? = nil, tint: Color? = nil, prominent: Bool = false,
        interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void, @ViewBuilder label: () -> Label
    )
}

extension GlassButton where Label == SwiftUI.Label<Text, Image?> {
    /// Creates a button with a title and an optional SF Symbol.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    )

    /// Creates a button with a title string and an optional SF Symbol.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, systemImage: String? = nil, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    )

    /// Creates an icon-only circular button. `label` names it for VoiceOver and tooltips.
    public init(
        icon systemImage: String, label titleKey: LocalizedStringKey, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    )

    /// Creates an icon-only circular button named by a string, for example a title computed at runtime.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        icon systemImage: String, label title: S, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    )
}
```

- `role` comes first in the `label:` initializer and right after `systemImage` / `label` in the others. A custom
  radius needs the `label:` initializer.
- `icon:label:` takes a `LocalizedStringKey` literal or a `String` variable (`GlassButton(icon: "xmark", label: name)`).
- Labels are 17 pt semibold on iOS (`.subheadline` semibold when small) and 13 pt medium on macOS (11 / 15 pt small /
  large). Icon-only titles become the VoiceOver label and the macOS tooltip.
- `.disabled`, `.keyboardShortcut`, `.controlSize`, focus and Space/Return activation work as on any `Button`;
  keyboard focus draws an accent ring in the outline's shape.
- The button applies its font, `.lineLimit(1)`, padding and minimum size to the label, and the glass hugs the label:
  `.font` or `.frame(maxWidth: .infinity)` outside the button has no effect on either. Put them inside the `label:`
  closure (`GlassButton(action: go) { Text("Continue").frame(maxWidth: .infinity) }`).

```swift
GlassButton("Play", systemImage: "play.fill") { play() }
GlassButton(icon: "plus", label: "Add") { add() }                  // circle
GlassButton("Buy", prominent: true) { buy() }.controlSize(.large)  // accent glass, white label
GlassButton("Delete", systemImage: "trash", role: .destructive) { delete() }   // red label
GlassButton("Warning", tint: .yellow) {}                           // dark label on light tints
GlassButton("Frosted", material: .regular) {}
GlassButton("Static", interactive: false) {}                       // no grow, stretch or glow
GlassButton(icon: "xmark", label: name) { close() }                // a String label works too
GlassButton(radius: 12, action: {}) { Text("Custom label") }       // any label, any outline
GlassButton(role: .destructive, radius: 12, action: {}) { Text("Remove") }
GlassButton("Small") {}.controlSize(.small)                        // 34 pt iOS, 22 pt macOS
```

## GlassToolbar and GlassToolbarSpacer

A floating row of items on capsule glass, 52 pt tall with a 4 pt inset on iOS (34 and 3 on macOS), 4 pt between
items. `GlassToolbarSpacer` splits it into clusters, each on its own capsule. Pressing anywhere on a cluster grows it
and lights it under the finger. `GlassButton` and `GlassMenu` inside drop their own glass (and their glass options).
Items take the toolbar's foreground: white or `#111113` for contrast on a `tint`, and under a forced `appearance` its
color scheme. The toolbar caps its own Dynamic Type size at `.xxxLarge`, so it stops growing with its items, and a
long press on an item (button or menu trigger) shows it in the Large Content Viewer. The toolbar hugs its items
unless a flexible spacer stretches it; a fixed spacer never makes it taller. Right to left, the clusters mirror with
their items.

```swift
public struct GlassToolbar<Content: View>: View {
    /// - material: Clear or Regular glass; `nil` inherits the scene's.
    /// - appearance: Light or dark glass; `nil` inherits. The items take its foreground and color scheme.
    /// - tint: Colors the glass; the items turn white or dark for contrast.
    /// - interactive: Whether clusters respond to presses. Default `true`.
    /// - motion: Overrides the scene's motion level.
    public init(
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, tint: Color? = nil,
        interactive: Bool = true, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    )
}

/// Separates a GlassToolbar's items into clusters on separate glass. A flexible spacer takes the free space
/// (at least `minLength` of visible gap); a fixed one leaves exactly `minLength`.
public struct GlassToolbarSpacer: View {
    public enum Sizing: Hashable, Sendable {
        case flexible   // Grows to fill the toolbar's free space.
        case fixed      // Keeps its minimum length.
    }

    /// - sizing: Flexible (default) or fixed.
    /// - minLength: The smallest visible gap between the clusters, in points.
    public init(_ sizing: Sizing = .flexible, minLength: CGFloat = 8)
}
```

```swift
GlassToolbar {
    GlassButton(icon: "chevron.left", label: "Back") {}
    GlassToolbarSpacer()                         // leading cluster | trailing cluster, full width
    GlassButton(icon: "bold", label: "Bold") {}
    GlassButton(icon: "italic", label: "Italic") {}
    GlassToolbarSpacer(.fixed)                   // an 8 pt gap, a third cluster
    GlassMenu(icon: "ellipsis", label: "More") { GlassMenuItem("Rename", systemImage: "pencil") {} }
}
.padding(.horizontal, 16)

GlassToolbar(material: .regular) { … }           // over scrolling text
```

## GlassTabBar and GlassTab

Tabs on a glass capsule with a glass lens of the bar's own material (a Regular bar stays frosted under the
selection) that springs to the selection on the track spring and stretches with its speed; drag across the bar to
carry the lens and release to choose the tab under it. On touch screens the lens follows a finger only once it moves
sideways, so a vertical swipe that starts on the bar scrolls an enclosing scroll view. iOS: 62 pt tall with icon over
label, selected item in the accent color, others at 62% foreground, items fill the width equally; the bar carries
the tab bar trait (`.isTabBar`); `.controlSize(.small)` makes a 40 pt bar with icon beside label (a segmented control).
macOS: 28 pt (24 small), icon (if any) beside the label at 13 pt medium, unselected at 85%, items hug their labels;
when focused it draws a focus ring and the arrow keys move the selection in visual order (right to left too).
Selection plays a haptic on iOS. The bar caps its own Dynamic Type size at `.xxxLarge`, so it stops growing with its
labels, and a long press on a tab shows it in the Large Content Viewer. Tabs take the bar's foreground (white or dark
on a `tint`) and, under a forced `appearance`, its color scheme.

```swift
public struct GlassTabBar<Selection: Hashable, Content: View>: View {
    /// - selection: The selected tab's value.
    /// - material: Clear or Regular glass; `nil` inherits the scene's. The selection lens uses the same material.
    /// - appearance: Light or dark glass; `nil` inherits. The tabs take its foreground and color scheme.
    /// - tint: Colors the bar's glass; the tabs turn white or dark for contrast.
    /// - interactive: Whether the bar responds to presses and lens drags.
    /// - motion: Overrides the scene's motion level.
    /// - content: GlassTab items.
    public init(
        selection: Binding<Selection>, material: GlassMaterial? = nil, appearance: GlassAppearance? = nil,
        tint: Color? = nil, interactive: Bool = true, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    )
}

public struct GlassTab<Value: Hashable>: View {
    /// Creates a tab with a title, an optional SF Symbol and its value.
    public init(_ titleKey: LocalizedStringKey, systemImage: String? = nil, value: Value)

    /// Creates a tab from a title string.
    @_disfavoredOverload
    public init<S: StringProtocol>(_ title: S, systemImage: String? = nil, value: Value)
}
```

- `value` must have exactly the `Selection` type; a tab whose value has another type is never selected.
- The lens takes the bar's `material` and `appearance`; `tint` colors only the bar.
- On iOS cap the floating bar's width (`.frame(maxWidth: 440).padding(.horizontal, 24)`); on macOS use
  `.fixedSize()`.

```swift
enum Page: Hashable { case home, search, library }
@State private var page = Page.home

GlassTabBar(selection: $page) {
    GlassTab("Home", systemImage: "house", value: Page.home)
    GlassTab("Search", systemImage: "magnifyingglass", value: Page.search)
    GlassTab("Library", systemImage: "books.vertical", value: Page.library)
}

GlassTabBar(selection: $settings.material) {          // segmented control inside a panel
    GlassTab("Clear", value: GlassMaterial.clear)
    GlassTab("Regular", value: GlassMaterial.regular)
}
.controlSize(.small)
```

## GlassSlider

A 6 pt capsule track filled with `tint` (the accent color by default) up to the value. The thumb is an opaque white
capsule at rest and lifts into clear glass while held, refracting the track beneath it and stretching with its speed.
Press anywhere on the track to jump there and keep dragging. On touch screens a drag starts only once the finger moves
sideways: a vertical swipe that starts on the slider scrolls an enclosing scroll view and leaves the value alone (a tap
still jumps). 44 pt tall on iOS (28 on macOS), at least 80 pt wide, fills the width it is offered, fills from the
right in right-to-left layouts. With a `step` the value snaps and iOS plays a
selection haptic per step. VoiceOver sees a standard `Slider`; on macOS the arrow keys adjust it when focused (by the
step, or 1% of the range).

```swift
public struct GlassSlider<Value: BinaryFloatingPoint, Label: View>: View where Value.Stride: BinaryFloatingPoint {
    /// - value: The selected value.
    /// - range: The values the slider spans.
    /// - step: The distance between valid values, or `nil` for continuous.
    /// - tint: The fill color; `nil` uses the accent color.
    /// - appearance: The appearance of the lifted glass; `nil` inherits.
    /// - interactive: Whether the thumb lifts into glass while held.
    /// - motion: Overrides the scene's motion level.
    /// - label: Describes the slider for accessibility.
    /// - onEditingChanged: Called when a drag begins (`true`) and ends (`false`).
    public init(
        value: Binding<Value>, in range: ClosedRange<Value> = 0...1, step: Value.Stride? = nil,
        tint: Color? = nil, appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil,
        @ViewBuilder label: () -> Label, onEditingChanged: @escaping (Bool) -> Void = { _ in }
    )
}

extension GlassSlider where Label == EmptyView {
    /// Creates a slider without a label. Give it an `accessibilityLabel`.
    public init(
        value: Binding<Value>, in range: ClosedRange<Value> = 0...1, step: Value.Stride? = nil,
        tint: Color? = nil, appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil,
        onEditingChanged: @escaping (Bool) -> Void = { _ in }
    )
}
```

`Value` is `Double`, `Float` or `CGFloat` (not `Int`). The label is not drawn; it names the slider for VoiceOver, so
show a visible caption or icons beside it. `.disabled(true)` dims the whole slider (track, fill and thumb) to 45%.

```swift
@State private var volume = 40.0

GlassSlider(value: $volume, in: 0...100) { Text("Volume") }
GlassSlider(value: $volume, in: 0...100, step: 10, tint: .orange) { Text("Volume") }
GlassSlider(value: $position, in: 0...duration) {
    Text("Playback position")
} onEditingChanged: { editing in
    scrubbing = editing
}
GlassSlider(value: $level).accessibilityLabel("Level")
```

## GlassSwitch and GlassSwitchStyle

A `Toggle` whose white thumb lifts into clear glass while held. Tap to toggle or drag the thumb across; it travels on
the switch spring (response 0.47 s, damping 0.7). The track takes `tint` (the accent color by default) when on and
gray otherwise. Track 63 × 28 pt with a 37 × 24 pt thumb on iOS, 54 × 24 with 32 × 20 on macOS. The style lays out
`label`, a flexible spacer (at least 8 pt) and the track, so a switch fills its row; `.fixedSize()` makes it compact.
On iOS 18 and macOS 15, `.labelsHidden()` hides the label (it stays the VoiceOver label) and the switch hugs its
track; iOS 17 and macOS 14 don't report hidden labels to toggle styles, so there the label still shows (leave the
title empty and set `accessibilityLabel` instead). iOS plays a light impact on change; macOS toggles with Space when
focused. `.disabled(true)` dims the track and thumb to 45% (the label keeps its own style).

```swift
public struct GlassSwitch<Label: View>: View {
    /// - isOn: Whether the switch is on.
    /// - tint: The track color when on; `nil` uses the accent color.
    /// - appearance: The appearance of the lifted glass; `nil` inherits.
    /// - interactive: Whether the thumb lifts into glass while held.
    /// - motion: Overrides the scene's motion level.
    public init(
        isOn: Binding<Bool>, tint: Color? = nil, appearance: GlassAppearance? = nil,
        interactive: Bool = true, motion: GlassMotion? = nil, @ViewBuilder label: () -> Label
    )
}

extension GlassSwitch where Label == Text {
    /// Creates a switch with a title.
    public init(
        _ titleKey: LocalizedStringKey, isOn: Binding<Bool>, tint: Color? = nil,
        appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil
    )

    /// Creates a switch with a title string.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, isOn: Binding<Bool>, tint: Color? = nil,
        appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil
    )
}

/// The glass switch as a toggle style, for use on any Toggle.
public struct GlassSwitchStyle: ToggleStyle {
    public init(tint: Color? = nil, appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil)
    public func makeBody(configuration: Configuration) -> some View
}
```

```swift
@State private var wifi = true

GlassSwitch("Wi-Fi", isOn: $wifi)
GlassSwitch("AirDrop", isOn: $airdrop, tint: Color(red: 0.204, green: 0.78, blue: 0.349))
GlassSwitch(isOn: $bluetooth) { Label("Bluetooth", systemImage: "antenna.radiowaves.left.and.right") }
GlassSwitch("Capsule", isOn: $capsule).fixedSize()
GlassSwitch("Sync", isOn: $sync).labelsHidden()             // only the track (iOS 18, macOS 15)
Toggle("Bluetooth", isOn: $bluetooth).toggleStyle(GlassSwitchStyle())
```

## GlassMenu, GlassMenuItem and GlassMenuDivider

A menu whose trigger is a `GlassButton` (a plain item inside a `GlassToolbar`); `appearance` and `tint` apply to the
trigger and the panel. Opening morphs the trigger's glass into a Regular panel on the morph spring while the items
condense out of a blur; closing reverses it. As it opens, the panel's glass mixes toward a solid backing, `#F5F5F7`
light and `#1C1C1E` dark, by up to 0.6 (`GlassOptics.menuLegibility`) times the open progress (clamped to 1 while the
spring overshoots), so it leaves the trigger as the trigger's glass and still refracts and frosts at 40% strength when
open. Items take `GlassOptics.menuForeground`: the appearance foreground untinted (`#111113` light, white dark); on a
tinted menu, white or `#111113`, whichever contrasts more with `mix(tint · (body + (1 − body) · 0.5), backing, 0.6)`
(body 0.78 Regular, 0.68 Clear), so a light menu tinted blue has `#111113` items while its trigger keeps a white
label. Untinted menus, Regular or Clear, in both appearances, keep at least 5.2:1 over any backdrop, highlighted items
included (5.24:1 at worst, Clear in dark appearance over white; 6.51:1 white over white on dark Regular without the
highlight, 8.74:1 `#111113` over black in light); menus tinted with the system colors, as they resolve in either
appearance, keep at least 5.0:1 (5.07:1 at worst, the dark-appearance `.yellow` on a highlighted item of dark Regular
over white; 5.26:1 for the light-appearance colors). Very light tints (pure yellow, lime, white) can fall below 4.5:1
over white in dark appearance: tint menus with a deeper color or not at all. No other surface has the backing. The
panel opens below the trigger with trailing edges aligned (the left edges, right to left) and 8 pt from it, flips
above when it does not fit below but fits above, and keeps 8 pt from the scene's left and right edges. It never
scrolls: a menu that fits on neither side runs past the scene's bottom edge. A highlighted item (hovered, focused or
pressed) mixes the label color into the glass at 8% (`GlassOptics.menuHighlight`). Tap outside, press Escape (or the
VoiceOver escape gesture), or choose an item to dismiss; up/down arrows and Return navigate, skipping disabled items.
The panel draws in the enclosing scene's overlay; outside a `GlassScene` the menu falls back to the system `Menu` with
the same items (checked items become toggles there).

```swift
public struct GlassMenu<Label: View, Content: View>: View {
    /// - material: The panel's material; `nil` is Regular. The trigger inherits the scene's.
    /// - appearance: Light or dark glass for the trigger and the panel, and their labels; `nil` inherits.
    /// - radius: The panel's corner radius. Default 28.
    /// - tint: Colors the trigger's and the panel's glass. The trigger's label turns white or dark for contrast with
    ///   the tint; the items take white or #111113 for contrast with the tint mixed toward the backing.
    /// - motion: Overrides the scene's motion level.
    /// - content: GlassMenuItems and GlassMenuDividers.
    /// - label: The trigger's label.
    public init(
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil,
        @ViewBuilder content: () -> Content, @ViewBuilder label: () -> Label
    )
}

extension GlassMenu where Label == SwiftUI.Label<Text, Image?> {
    /// Creates a menu whose trigger shows a title and an optional SF Symbol.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    )

    /// Creates a menu with an icon-only, circular trigger (the icon button circle: 44 pt iOS, 28 pt macOS).
    /// `label` names it for VoiceOver and tooltips.
    public init(
        icon systemImage: String, label titleKey: LocalizedStringKey,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    )

    /// Creates a menu with an icon-only, circular trigger named by a string, for example a title computed at runtime.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        icon systemImage: String, label title: S,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    )
}

/// An item of a GlassMenu. Outside a glass panel (the system menu fallback) it is a plain button, or a Toggle
/// when `checked` is set. A disabled item (`.disabled(true)`) dims and is skipped by the arrow keys and Return.
public struct GlassMenuItem: View {
    /// - role: `.destructive` draws the item in red.
    /// - checked: `true` or `false` for an item that shows a choice, with a checkmark when it is `true`;
    ///   `nil` (the default) for a command.
    /// - action: Runs when the item is chosen; the menu then closes.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil, role: ButtonRole? = nil,
        checked: Bool? = nil, action: @escaping () -> Void
    )

    /// Creates an item from a title string.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, systemImage: String? = nil, role: ButtonRole? = nil,
        checked: Bool? = nil, action: @escaping () -> Void
    )
}

/// A separator between groups of menu items.
public struct GlassMenuDivider: View {
    public init()
}
```

- The trigger's glass uses `material` when set, else the scene's material; the panel uses `material` when set, else
  Regular.
- `radius` is read as points; `.capsule`/`.circle` fall back to 28. Items' highlights are concentric
  (radius − inset: 8 pt iOS, 4 pt macOS).
- Panel metrics: items 44 pt tall, 16 pt padding, 24 pt icon column, 16 pt check column, min width 220 on iOS;
  28 pt, 8 pt, 16 pt, 12 pt, 160 on macOS.
- Title triggers take `LocalizedStringKey` (literals and interpolation work; pass a `String` variable as
  `LocalizedStringKey(name)`); `icon:label:` also takes a `String`.
- Items with `checked:` (`true` or `false`) draw a checkmark in a leading column when `true`; VoiceOver reads them as
  selected (`.isSelected`). Once any item of a panel has `checked:`, every item of that panel keeps the column, so
  icons and titles line up. `ForEach` works inside.
- `.disabled(true)` on an item dims it; the arrow keys and Return skip it.
- Content: only `GlassMenuItem`, `GlassMenuDivider` and `ForEach` of them; about 8 items or fewer (no scrolling), and
  no submenus (a `GlassMenu` inside a panel never opens).
- The panel is built in the scene's overlay with the scene's environment: `.environment(model)`, `.glassAppearance`,
  `.disabled` or `.font` applied between the scene and the menu do not reach the items (a custom row reading an
  environment object injected there crashes). Pass `appearance:` to the menu, put `.disabled` on the items, and
  capture data in item actions.
- Items take the panel's foreground: white or `#111113` for contrast on a `tint`, and the color scheme of a forced
  `appearance`.

```swift
GlassMenu("Sort", systemImage: "arrow.up.arrow.down") {
    GlassMenuItem("Newest", systemImage: "clock") { sort = .newest }
    GlassMenuItem("Oldest", systemImage: "clock.arrow.circlepath") { sort = .oldest }
    GlassMenuDivider()
    GlassMenuItem("Reset", systemImage: "trash", role: .destructive) { reset() }
}

GlassMenu(icon: "ellipsis", label: "More") {
    GlassMenuItem("Duplicate", systemImage: "plus.square.on.square") {}
}

GlassMenu("View", systemImage: "eye") {
    GlassMenuItem("Grid", checked: layout == .grid) { layout = .grid }
    GlassMenuItem("List", checked: layout == .list) { layout = .list }
    GlassMenuDivider()
    GlassMenuItem("Export…", systemImage: "square.and.arrow.up") { export() }
        .disabled(!canExport)
}

GlassMenu {
    GlassMenuItem("Photo", systemImage: "photo") {}
} label: {
    Label("New", systemImage: "plus")
}
```

## GlassProgressiveBlur

A blur that grows toward an edge of the scene's backdrop: six levels over the sharp backdrop, level *i* at
σ = `blur`·(*i*/6)², blended by the smoothstep of the depth so the weights sum to 1. It fills the width (or height)
it is offered and is `size` points deep, never takes touches and is hidden from accessibility. Each of the six levels
is a full copy of the backdrop, so one blur costs about as much as six surfaces: use at most one per edge, 80 to
130 pt deep. It blurs only the backdrop: glass above it still refracts the sharp backdrop. `.leading` and `.trailing`
follow the layout direction (right to left, `.leading` blurs toward the right edge). Outside a scene it falls back to
`.ultraThinMaterial` under the same fade.

```swift
public struct GlassProgressiveBlur: View {
    /// - edge: The edge the blur grows toward.
    /// - size: How far the blur reaches from the edge, in points. Default 80.
    /// - blur: The Gaussian σ at the edge, in points. Default 20.
    public init(edge: Edge = .top, size: CGFloat = 80, blur: Double = 20)   // negatives clamp to 0
}
```

```swift
VStack(spacing: 0) {
    GlassProgressiveBlur(edge: .top, size: 128).ignoresSafeArea(edges: .top)
    Spacer(minLength: 0)
    GlassProgressiveBlur(edge: .bottom, size: 120).ignoresSafeArea(edges: .bottom)
}
```

## GlassScrollView and GlassScrollPosition

A scroll view drawn entirely by SwiftUI, so it can be the backdrop. It moves its content with an offset: drag to
scroll on iOS with `UIScrollView`'s deceleration (× 0.998 per ms) and rubber band past the ends; a cancelled drag
settles back in range. On macOS it follows the trackpad (with the system's momentum) and the mouse wheel, taking only
its own window's wheel events (an AppKit scroll view or text view in front of it scrolls itself), and once focused
scrolls with the arrow keys, Page Up and Down, Space (Shift-Space up), Home and End. A capsule indicator shows while
scrolling. VoiceOver's three-finger scroll pages by 90% of the viewport. Content is laid out at its ideal length
along the scrolling axes, so `LazyVStack` renders every row. Copies inside glass only display the shared position and
take no input.

```swift
public struct GlassScrollView<Content: View>: View {
    /// - axes: The scrollable axes. Default vertical.
    /// - position: The shared scroll position.
    /// - showsIndicators: Whether a scroll indicator appears while scrolling.
    /// - content: The scrolled content.
    public init(
        _ axes: Axis.Set = .vertical, position: GlassScrollPosition, showsIndicators: Bool = true,
        @ViewBuilder content: () -> Content
    )
}

/// The scroll position of a GlassScrollView, shared by the visible backdrop and every copy of it.
/// Create one with @State next to the scene (never inside the content).
@MainActor
@Observable
public final class GlassScrollPosition {
    /// The content offset: how far the content has scrolled from its top-leading corner.
    /// Outside 0...maximumOffset while rubber-banding.
    public private(set) var offset = CGPoint.zero
    /// The size of the scrolled content.
    public private(set) var contentSize = CGSize.zero
    /// The size of the visible region.
    public private(set) var viewportSize = CGSize.zero
    /// Whether the content is moving: dragged, coasting or settling.
    public private(set) var isScrolling = false

    public init()

    /// The largest offset on each axis.
    public var maximumOffset: CGPoint { get }

    /// Scrolls so the content's `point` sits at the viewport's top-leading corner (clamped to the range).
    public func scrollTo(_ point: CGPoint, animated: Bool = true)

    /// Scrolls to an edge of the content.
    public func scrollTo(edge: Edge, animated: Bool = true)
}
```

```swift
@State private var scroll = GlassScrollPosition()

GlassScene {
    GlassContent {
        GlassScrollView(position: scroll) {
            VStack(alignment: .leading, spacing: 24) { ForEach(stories) { StoryCard(story: $0) } }
                .padding(.top, 132)            // backdrops don't see the safe area: room for bars
                .padding(.bottom, 120)
        }
        .background(Color(white: 0.96))        // opaque backdrop
    }
    .ignoresSafeArea()

    VStack {
        GlassToolbar(material: .regular) {
            GlassButton(icon: "arrow.up", label: "Top") { scroll.scrollTo(edge: .top) }
        }
        Spacer()
    }
}

// Reading the position drives effects (it is @Observable):
Text("Stories").opacity(scroll.offset.y > 60 ? 1 : 0)

GlassScrollView(.horizontal, position: carousel, showsIndicators: false) { HStack { … } }
```

## GlassSpring

A damped spring described by its perceptual duration and bounce (the parameterization of SwiftUI's
`Spring(duration:bounce:)`), solved in closed form so a dropped frame never changes the trajectory. The library's
motion uses these presets; use them to match its feel in your own animations or tests.

```swift
public struct GlassSpring: Hashable, Sendable {
    public var duration: Double   // Perceptual duration in seconds.
    public var bounce: Double     // Bounce in -1 < bounce < 1; 0 is critically damped.

    public init(duration: Double, bounce: Double)

    public static let press = GlassSpring(duration: 0.24, bounce: 0.2)     // finger down: the material catches it
    public static let release = GlassSpring(duration: 0.5, bounce: 0.42)   // finger up: gel-like overshoot back to rest
    public static let glow = GlassSpring(duration: 0.42, bounce: 0)        // light settling in and out
    public static let morph = GlassSpring(duration: 0.42, bounce: 0.18)    // glass growing out of another shape (menus)
    public static let layout = GlassSpring(duration: 0.4, bounce: 0.16)    // a surface flowing to a new size
    public static let track = GlassSpring(duration: 0.18, bounce: 0)       // selection lenses and drag follow

    /// The track spring of a surface being dragged: larger glass is heavier and follows more slowly.
    public static func follow(width: Double, height: Double) -> GlassSpring   // duration 0.18 + 0.0005·max(w, h)

    public var critical: GlassSpring { get }   // the same spring without overshoot (reduced motion)
    public var stiffness: Double { get }       // (2π / duration)²
    public var damping: Double { get }         // 4π(1 − bounce) / duration for bounce ≥ 0

    public struct State: Hashable, Sendable {
        public var value: Double
        public var velocity: Double
        public var target: Double
        public init(value: Double, velocity: Double = 0, target: Double? = nil)   // target defaults to value
        public var isSettled: Bool { get }     // value == target && velocity == 0
    }

    /// Advances by `dt` seconds in closed form; values within 1e-4 (velocities 1e-3) of rest snap to the target.
    public func step(_ state: State, dt: Double) -> State
}
```

```swift
var state = GlassSpring.State(value: 0, target: 1)
state = GlassSpring.release.step(state, dt: 1 / 60)

withAnimation(.spring(duration: GlassSpring.release.duration, bounce: GlassSpring.release.bounce)) { offset = .zero }
```

## GlassOptics

The optical model on the CPU: a line-by-line port of the shared JavaScript reference (the `liquid-glass-port` skill's
`references/reference.mjs`), evaluated per pixel by the Metal shader.
For custom renderers, layout that must match the glass outline, and tests. Colors are sRGB gamma-encoded,
unpremultiplied, `0...1`; coordinates inside a surface are measured from its center with y down.

```swift
public enum GlassOptics {
    public static let bevel = 20.0                  // Depth of the refracting bevel inside the outline.
    public static let defaultRefraction = 60.0      // Unset travel of surfaces whose shorter side is >= 80.
    public static let continuousExtent = 1.528      // Corner reach along each edge, relative to the radius.
    public static let continuousExponent = 3.26     // Superellipse exponent of a continuous corner.
    public static let luma = SIMD3<Double>(0.213, 0.715, 0.072)   // SVG saturate luma weights.
    public static let frostSigma = 7.33             // σ of the Regular material's frost blur.
    public static let menuLegibility = 0.6          // Mix toward the legibility backing on open menu panels (0 elsewhere).
    public static let menuHighlight = 0.08          // A highlighted menu row mixes its label color in by this.
    public static let progressiveLevels = 6         // Blur levels above the sharp backdrop.

    // Result types (public fields, no public initializers).
    public struct ShapeParameters: Hashable, Sendable { public var extent: Double; public var exponent: Double }
    public struct Field: Hashable, Sendable { public var distance: Double; public var nx: Double; public var ny: Double }
    public struct Glow: Hashable, Sendable { public var radius: Double; public var alpha: Double; public var hover: Double }

    // Shape
    public static func shapeParameters(width: Double, height: Double, radius: GlassRadius) -> ShapeParameters
    public static func shapeField(x: Double, y: Double, width: Double, height: Double, extent: Double, exponent: Double) -> Field
    public static func shapeOutline(width: Double, height: Double, radius: GlassRadius, segments: Int = 24) -> [CGPoint]

    // Refraction
    public static func defaultRefraction(width: Double, height: Double) -> Double   // min(60, 0.75 · min(w, h))
    public static func edgeProfile(distance: Double, bevel: Double = GlassOptics.bevel) -> Double
    public static func refractionOffset(distance: Double, nx: Double, ny: Double, refraction: Double = defaultRefraction) -> CGVector
    public static func coverage(distance: Double, pixel: Double) -> Double

    // Color
    public static func luma(_ c: SIMD3<Double>) -> Double
    public static func saturate(_ c: SIMD3<Double>, _ s: Double) -> SIMD3<Double>
    public static func clearTone(_ v: Double, dark: Bool, nested: Bool = false) -> Double
    public static func regularTone(_ v: Double, dark: Bool) -> Double
    public static func materialColor(_ c: SIMD3<Double>, material: GlassMaterial, dark: Bool, nested: Bool = false) -> SIMD3<Double>
    public static func tintBody(_ material: GlassMaterial) -> Double            // 0.78 Regular, 0.68 Clear
    public static func tintColor(_ c: SIMD3<Double>, tint: SIMD3<Double>, material: GlassMaterial) -> SIMD3<Double>
    public static func fillSigma(dark: Bool) -> Double                          // 23 light, 21.8 dark
    public static func frostColor(frost: SIMD3<Double>, fill: SIMD3<Double>, dark: Bool) -> SIMD3<Double>
    public static func legibilityBacking(dark: Bool) -> SIMD3<Double>           // #F5F5F7 light, #1C1C1E dark
    public static func legibilityColor(_ c: SIMD3<Double>, dark: Bool, amount: Double) -> SIMD3<Double>  // mix(c, backing, amount)
    public static func menuForeground(tint: SIMD3<Double>?, material: GlassMaterial, dark: Bool,
                                      legibility: Double = menuLegibility) -> SIMD3<Double>  // menu label: #111113 or white

    // Rim light (premultiplied RGBA)
    public static func rimOverlay(distance d: Double, nx: Double, ny: Double, dark: Bool, pixel: Double, tint: SIMD3<Double>? = nil) -> SIMD4<Double>

    // One glass pixel (premultiplied result)
    public static func glassPixel(
        x: Double, y: Double, width: Double, height: Double, radius: GlassRadius,
        material: GlassMaterial = .clear, dark: Bool = false, refraction: Double = defaultRefraction,
        tint: SIMD3<Double>? = nil, pixel: Double = 1.0 / 3.0, nested: Bool = false, legibility: Double = 0,
        sample: (_ dx: Double, _ dy: Double) -> SIMD4<Double>, base: SIMD4<Double>
    ) -> SIMD4<Double>

    // Interaction
    public static func rubberBand(_ offset: Double, limit: Double, coefficient: Double = 0.55) -> Double
    public static func stretchLimit(width: Double, height: Double) -> Double
    public static func pressScale(
        width: Double, height: Double, press: Double,
        tx: Double = 0, ty: Double = 0, vx: Double = 0, vy: Double = 0,
        carriedX: Double = 0, carriedY: Double = 0, reduced: Bool = false
    ) -> CGSize
    public static func glow(width: Double, height: Double, dark: Bool) -> Glow   // radius clamp(0.9·max, 36, 180)

    // Progressive blur
    public static func progressiveLevelBlur(level: Int, blur: Double) -> Double  // blur·(level/6)²
    public static func progressiveWeight(level: Int, u: Double) -> Double
}
```

```swift
let params = GlassOptics.shapeParameters(width: 200, height: 64, radius: .capsule)
let field = GlassOptics.shapeField(x: 90, y: 0, width: 200, height: 64, extent: params.extent, exponent: params.exponent)
let travel = GlassOptics.defaultRefraction(width: 200, height: 64)  // 48: what the surface uses unset
let offset = GlassOptics.refractionOffset(distance: field.distance, nx: field.nx, ny: field.ny, refraction: travel)
let outline = GlassOptics.shapeOutline(width: 200, height: 64, radius: 24)    // polygon, clockwise from the top edge
```

## Metrics per platform

Fixed by the library (sizes scale with Dynamic Type where noted).

| Element | iOS / iPadOS | macOS |
| --- | --- | --- |
| `GlassButton` height small / regular / large (`.mini` = small, `.extraLarge` = large) | 34 / 44 / 52, Dynamic Type | 22 / 28 / 34, Dynamic Type |
| `GlassButton` horizontal padding small / regular / large | 12 / 16 / 22 | 8 / 10 / 12 |
| `GlassToolbar` height, inset, item spacing | 52, 4, 4 (height scales with Dynamic Type up to `.xxxLarge`) | 34, 3, 4 |
| `GlassTabBar` height regular / small | 62 / 40 (height scales with Dynamic Type up to `.xxxLarge`) | 28 / 24 |
| Unset refraction: button small / regular / large, toolbar, tab bar (follows their size) | 25.5 / 33 / 39, 39, 46.5 | 16.5 / 21 / 25.5, 25.5, 21 |
| `GlassSlider` height, thumb, lifted growth per side, lens refraction | 44, 36 × 24, 5 × 4, 5 | 28, 20 × 16, 2.5 × 2, 5 |
| `GlassSwitch` track, thumb, lifted growth per side, lens refraction | 63 × 28, 37 × 24, 10 × 6.5, 7.75 | 54 × 24, 32 × 20, 9 × 5.5, 7.75 |
| `GlassMenu` item height, padding, panel inset, min width, check column | 44, 16, 8, 220, 16 | 28, 8, 4, 160, 12 |
| `GlassMenu` panel legibility backing (mix, light, dark) | 0.6 times the open progress, `#F5F5F7`, `#1C1C1E`; untinted menu text (Regular or Clear) at 5.2:1 or more over any backdrop, system-tinted menu text (either appearance) at 5.0:1 or more, highlighted items included | same |
| Highlight of pressed / hovered / focused items | foreground at 8% (menus: the label color, `menuForeground`) | same |
| Glass foreground color (surface and button content, bar and menu items) | `#111113` light, white dark; on a tint, whichever of the two contrasts more (menu items: with the tint mixed toward the backing) | same |

## System settings, fallbacks and accessibility

| Condition | Behavior |
| --- | --- |
| Reduce Transparency | The material becomes an opaque fill (`#F2F2F7` light, `#1C1C1E` dark, at 94%); the rim light stays |
| Increase Contrast | Regular material plus a 1 pt inner border (black 18% light, white 28% dark) |
| Reduce Motion | `.full` resolves to `.reduced`: no grow, stretch or overshoot; the tab lens, switch thumb and thumb lift move as critically damped transitions |
| `GlassMotion.none` | No transitions at all: the lens and thumbs jump |
| Dynamic Type | Button, toolbar and tab bar heights and labels scale; toolbars and tab bars stop growing, with their labels, at `.xxxLarge`, and a long press on a toolbar item or tab shows it in the Large Content Viewer |
| VoiceOver | Buttons, tabs (`.isSelected`), switches (`Toggle`), sliders (`Slider` representation), menu items (checked ones `.isSelected`) keep roles, labels, values and actions; iOS tab bars have the `.isTabBar` trait; open menus are modal and close with the escape gesture; glass layers are hidden |
| Keyboard (macOS) | Buttons and switches focus with Full Keyboard Access; tab bars (arrows in visual order, with a focus ring) and sliders use arrows; switches Space; menus arrows, Return, Escape (disabled items skipped); a focused `GlassScrollView` arrows, Page Up/Down, Space, Home, End |
| Pointer (macOS, iPad) | Hover lights interactive glass at level 0.35 |
| Haptics (iOS) | Tab selection and stepped sliders: selection feedback; switches: light impact |
| Right to left | Every component mirrors: glass refracts its mirrored position, toolbar clusters and menus mirror, the tab lens and arrow keys follow visual order, sliders fill from the right |
| Frame rate | Springs run on a display link only while something moves. ProMotion iPhones need `CADisableMinimumFrameDurationOnPhone` = `YES` in the app's Info.plist for 120 Hz glass; on macOS the link follows the key window's screen |
| Outside a `GlassScene` | `GlassSurface` and bars: `Material.regular` / `Material.thin` plus rim; `GlassProgressiveBlur`: `.ultraThinMaterial`; `GlassMenu`: system `Menu` |
| Presentation attached inside a scene, without its own scene | Not a reliable fallback: glass draws a misaligned copy of the parent backdrop and a `GlassMenu` opens no panel; give the presented view its own `GlassScene` |
| Disabled `GlassSlider` / `GlassSwitch` | Dimmed to 45% (the slider whole, the switch's track and thumb), like `GlassButton`; menu items dim to 40% |
| Inside `GlassContent` | Glass renders only its content in the copies (glass never refracts glass) |
| `drawingGroup(colorMode: .linear)` | Unsupported: shaders expect SwiftUI's default gamma-encoded pipeline |
| `rotationEffect` / 3D transforms | Refraction misaligned (copies track translation and scale only) |
