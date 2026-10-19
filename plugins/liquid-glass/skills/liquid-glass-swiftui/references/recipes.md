# Recipes: Liquid Glass for SwiftUI

Six complete views built with the `LiquidGlass` Swift package (1.0.1, iOS 17+ and macOS 14+). Each file compiles on
its own for iOS and macOS; add it to an app target that depends on the `LiquidGlass` product.

## Contents

1. [App shell: floating tab bar over content](#1-app-shell-floating-tab-bar-over-content)
2. [Toolbar over a photo](#2-toolbar-over-a-photo)
3. [Media card with a slider](#3-media-card-with-a-slider)
4. [Settings card with switches](#4-settings-card-with-switches)
5. [Glass menu from a button](#5-glass-menu-from-a-button)
6. [Progressive blur over a scrolling list](#6-progressive-blur-over-a-scrolling-list)

## Setup shared by every recipe

Add the package (Xcode: **File › Add Package Dependencies…**, paste
`https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android`, from `1.0.1`, product `LiquidGlass`)
and host a recipe in the app's window. Build and run with Xcode 16 or later (the Metal shaders are compiled by
Xcode, not by `swift build`, and the iOS target needs the iOS 18 SDK). For 120 Hz glass on ProMotion iPhones, set
`CADisableMinimumFrameDurationOnPhone` to `YES` in the app's Info.plist.

```swift
import SwiftUI

@main
struct RecipesApp: App {
    var body: some Scene {
        WindowGroup {
            AppShell()                      // or PhotoViewer(), NowPlaying(), SettingsCard(), SortMenu(), ReadingList()
        }
        #if os(macOS)
        .windowStyle(.hiddenTitleBar)       // the backdrop runs under the title bar
        #endif
    }
}
```

Recipes 1 to 5 show pictures as the backdrop. `Image("home")`, `Image("photo")`, `Image("album")` and
`Image("wallpaper")` stand for any vivid images in `Assets.xcassets`; glass needs color and detail behind it, and over
a flat fill it reads as a gray pill. Recipe 6 needs no assets.

Design defaults used throughout: Clear glass over imagery, Regular for compact panels holding text and for bars over
text, buttons grouped into toolbar clusters at the edges, capsules for controls, circles for icon-only buttons,
concentric radii (inner radius = outer radius − padding, as `GlassShape(radius: R).inset(by: i)` computes), at most
one tinted or prominent control per screen. Toolbars and tab bars stop growing with their labels at the largest text
sizes on their own, and labels on tinted glass pick white or dark for contrast, so the recipes set neither.

## 1. App shell: floating tab bar over content

One scene at the app root, as in the demo app: each tab supplies the backdrop, and the tab bar floats over every page,
so its lens glides between pages instead of jumping. The backdrop and the page swap with a crossfade; `.id` and
`.transition` sit inside the `GlassContent` closure because `GlassContent` is not a view. `.id(tab)` rebuilds the
page on every switch, so keep per-tab state (scroll positions, selections) in models or `@State` above the scene.

```swift
import LiquidGlass
import SwiftUI

enum AppTab: String, CaseIterable, Hashable {
    case home, explore, library

    var title: String { rawValue.capitalized }

    var symbol: String {
        switch self {
        case .home: "house"
        case .explore: "safari"
        case .library: "books.vertical"
        }
    }
}

struct AppShell: View {
    @State private var tab = AppTab.home

    var body: some View {
        GlassScene {
            GlassContent {
                Image(tab.rawValue)                     // "home", "explore", "library" in Assets.xcassets
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityHidden(true)
                    .id(tab)
                    .transition(.opacity)
            }
            .ignoresSafeArea()

            ZStack(alignment: .bottom) {
                page
                    .id(tab)
                    .transition(.opacity)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                // Pass `material: .regular` when the page under the bar is text.
                GlassTabBar(selection: $tab) {
                    ForEach(AppTab.allCases, id: \.self) { item in
                        GlassTab(item.title, systemImage: item.symbol, value: item)
                    }
                }
                #if os(macOS)
                .fixedSize()
                .padding(.bottom, 20)
                #else
                .frame(maxWidth: 440)
                .padding(.horizontal, 24)
                .padding(.bottom, 4)
                #endif
            }
        }
        .animation(.easeInOut(duration: 0.3), value: tab)
    }

    private var page: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(tab.title)
                .font(.largeTitle.bold())
            Text("Drag across the bar to carry the lens.")
                .font(.subheadline)
                .opacity(0.85)
        }
        .foregroundStyle(.white)
        .shadow(color: .black.opacity(0.25), radius: 6, y: 2)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .padding(20)
    }
}

#Preview { AppShell() }
```

## 2. Toolbar over a photo

Clear glass over the photo. `GlassButton`s and the `GlassMenu` inside a `GlassToolbar` drop their own glass and share
the cluster's capsule; the flexible `GlassToolbarSpacer` splits the leading and trailing clusters and stretches the
toolbar across the width. The bottom toolbar has no spacer, so it hugs its items and centers. The caption chip uses
the `.liquidGlass` modifier.

```swift
import LiquidGlass
import SwiftUI

struct PhotoViewer: View {
    @State private var favorite = false

    var body: some View {
        // Over a photo known to be dark, pass `appearance: .dark`: the glass turns dark and the toolbar icons, the
        // caption and the menu items light. Add `.environment(\.colorScheme, .dark)` (or `.preferredColorScheme(.dark)`
        // for the status bar too) when plain views over the photo should follow as well.
        GlassScene {
            GlassContent {
                Image("photo")
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityLabel("Photo")
            }
            .ignoresSafeArea()

            VStack(spacing: 12) {
                GlassToolbar {
                    GlassButton(icon: "chevron.left", label: "Back") {}
                    GlassToolbarSpacer()
                    GlassButton(icon: favorite ? "heart.fill" : "heart", label: favorite ? "Unfavorite" : "Favorite") {
                        favorite.toggle()
                    }
                    GlassButton(icon: "square.and.arrow.up", label: "Share") {}
                    GlassMenu(icon: "ellipsis", label: "More") {
                        GlassMenuItem("Duplicate", systemImage: "plus.square.on.square") {}
                        GlassMenuItem("Hide", systemImage: "eye.slash") {}
                        GlassMenuDivider()
                        GlassMenuItem("Delete", systemImage: "trash", role: .destructive) {}
                    }
                }
                .padding(.horizontal, 16)
                .padding(.top, 8)

                Spacer()

                Text("Lake shore, 06:12")
                    .font(.footnote.weight(.semibold))
                    .padding(.horizontal, 14)
                    .padding(.vertical, 8)
                    .liquidGlass(radius: .capsule)

                GlassToolbar {
                    GlassButton("Edit", systemImage: "slider.horizontal.3") {}
                    GlassButton(icon: "info.circle", label: "Info") {}
                    GlassButton(icon: "crop.rotate", label: "Crop") {}
                }
                .padding(.bottom, 8)
            }
        }
    }
}

#Preview { PhotoViewer() }
```

## 3. Media card with a slider

A Regular card because it holds text, over the album art. The artwork is clipped with `GlassShape` concentric to the
card (34 pt card radius − 14 pt padding = 20 pt). The scrubber is a `GlassSlider` whose thumb lifts into glass while
dragged and refracts the track; the play button is the one prominent (accent-tinted) control.

```swift
import LiquidGlass
import SwiftUI

struct NowPlaying: View {
    @State private var position = 83.0
    @State private var playing = false
    @State private var scrubbing = false
    private let duration = 244.0

    var body: some View {
        GlassScene {
            GlassContent {
                Image("album")
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityHidden(true)
            }
            .ignoresSafeArea()

            GlassSurface(material: .regular, radius: 34) {
                VStack(alignment: .leading, spacing: 14) {
                    Color.clear
                        .aspectRatio(1, contentMode: .fit)
                        .overlay { Image("album").resizable().scaledToFill() }
                        .clipShape(GlassShape(radius: 20))      // concentric: 34 − 14
                        .accessibilityHidden(true)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Northern Lights").font(.title3.bold())
                        Text("The Glass Ensemble").opacity(0.7)
                    }
                    .padding(.horizontal, 6)
                    VStack(spacing: 2) {
                        GlassSlider(value: $position, in: 0...duration) {
                            Text("Playback position")
                        } onEditingChanged: { editing in
                            scrubbing = editing
                        }
                        HStack {
                            Text(time(position))
                            Spacer()
                            Text("-" + time(duration - position))
                        }
                        .font(.caption.monospacedDigit())
                        .opacity(scrubbing ? 1 : 0.7)
                    }
                    .padding(.horizontal, 6)
                    HStack(spacing: 20) {
                        GlassButton(icon: "backward.fill", label: "Previous") { position = 0 }
                        GlassButton(icon: playing ? "pause.fill" : "play.fill", label: playing ? "Pause" : "Play", prominent: true) {
                            playing.toggle()
                        }
                        .controlSize(.large)
                        GlassButton(icon: "forward.fill", label: "Next") {}
                    }
                    .frame(maxWidth: .infinity)
                }
                .padding(14)
                .frame(maxWidth: 360)
            }
            .accessibilityLabel("Now playing")
            .padding(20)
        }
    }

    private func time(_ seconds: Double) -> String {
        Duration.seconds(seconds).formatted(.time(pattern: .minuteSecond))
    }
}

#Preview { NowPlaying() }
```

## 4. Settings card with switches

A Regular card of `GlassSwitch` rows (label left, track right) and a brightness `GlassSlider`. A small `GlassTabBar`
acts as a segmented control for the appearance, and two settings drive the scene's own options, so every glass
element follows them (a forced Light or Dark also sets the labels' color scheme). Switch `tint` colors the on-track,
not glass.

```swift
import LiquidGlass
import SwiftUI

struct SettingsCard: View {
    @State private var appearance = GlassAppearance.automatic
    @State private var wifi = true
    @State private var bluetooth = false
    @State private var airplane = false
    @State private var brightness = 0.7
    @State private var reduceMotion = false

    var body: some View {
        GlassScene(appearance: appearance, motion: reduceMotion ? .reduced : nil) {
            GlassContent {
                Image("wallpaper")
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityHidden(true)
            }
            .ignoresSafeArea()

            GlassSurface(material: .regular, radius: 28) {
                VStack(alignment: .leading, spacing: 14) {
                    Text("Quick settings")
                        .font(.headline)
                    GlassTabBar(selection: $appearance) {
                        GlassTab("Auto", value: GlassAppearance.automatic)
                        GlassTab("Light", value: GlassAppearance.light)
                        GlassTab("Dark", value: GlassAppearance.dark)
                    }
                    .controlSize(.small)
                    GlassSwitch(isOn: $wifi) { Label("Wi-Fi", systemImage: "wifi") }
                    GlassSwitch(isOn: $bluetooth) { Label("Bluetooth", systemImage: "antenna.radiowaves.left.and.right") }
                    GlassSwitch(isOn: $airplane, tint: .orange) { Label("Airplane mode", systemImage: "airplane") }
                    Divider().opacity(0.6)
                    HStack(spacing: 12) {
                        Image(systemName: "sun.min")
                        GlassSlider(value: $brightness) { Text("Brightness") }
                        Image(systemName: "sun.max.fill")
                    }
                    GlassSwitch("Reduce motion", isOn: $reduceMotion)
                }
                .padding(20)
                .frame(maxWidth: 380)
            }
            .accessibilityLabel("Quick settings")
            .padding(20)
        }
    }
}

#Preview { SettingsCard() }
```

## 5. Glass menu from a button

Two standalone menus at the top trailing corner: a titled trigger that names the current choice, whose items check it
with `checked:` (a checkmark column, read as selected by VoiceOver), and an icon-only one (the same circle as a
`GlassButton(icon:)`) with an item disabled until there is a selection. Both sit in one foreground container, so
either panel draws above everything else. Opening morphs the trigger's glass into a Regular panel (radius 28) mixed
toward a solid backing (0.6 when open), so its items keep at least 5.2:1 contrast over any backdrop, highlighted items
included (5.0:1 tinted with a system color; a destructive item 5.0:1 in deep red on light menus and 4.5:1 in light
pink on dark ones); it opens below the trigger with trailing edges aligned, flips above when it does not fit below,
and keeps 8 pt from the scene's left and right edges. The panel never scrolls, so keep menus short. Tap outside, press
Escape or choose an item to close it.

```swift
import LiquidGlass
import SwiftUI

enum PhotoSort: String, CaseIterable, Identifiable {
    case newest, oldest, favorites

    var id: Self { self }
    var title: String { rawValue.capitalized }

    var symbol: String {
        switch self {
        case .newest: "clock"
        case .oldest: "clock.arrow.circlepath"
        case .favorites: "heart"
        }
    }
}

struct SortMenu: View {
    @State private var sort = PhotoSort.newest
    @State private var grid = true
    @State private var selecting = false

    var body: some View {
        GlassScene {
            GlassContent {
                Image("photo")
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityHidden(true)
            }
            .ignoresSafeArea()

            VStack {
                HStack(spacing: 12) {
                    Spacer()
                    GlassMenu("Sort: \(sort.title)", systemImage: "arrow.up.arrow.down") {
                        ForEach(PhotoSort.allCases) { option in
                            GlassMenuItem(option.title, systemImage: option.symbol, checked: sort == option) {
                                sort = option
                            }
                        }
                    }
                    GlassMenu(icon: "ellipsis", label: "More") {
                        GlassMenuItem(grid ? "Show as list" : "Show as grid", systemImage: grid ? "list.bullet" : "square.grid.2x2") {
                            grid.toggle()
                        }
                        GlassMenuItem(selecting ? "Done" : "Select", systemImage: "checkmark.circle") {
                            selecting.toggle()
                        }
                        GlassMenuItem("Share selection", systemImage: "square.and.arrow.up") {}
                            .disabled(!selecting)           // dimmed, skipped by the arrow keys
                        GlassMenuDivider()
                        GlassMenuItem("Delete all", systemImage: "trash", role: .destructive) {}
                    }
                }
                Spacer()
            }
            .padding(.horizontal, 20)
            .padding(.top, 8)
        }
    }
}

#Preview { SortMenu() }
```

## 6. Progressive blur over a scrolling list

The list is the backdrop, so it scrolls with `GlassScrollView` (a native `ScrollView` cannot be refracted), and its
`GlassScrollPosition` lives in `@State` beside the scene, where every copy reads it. The backdrop does not see the
safe area, so the list pads itself for the status bar and the toolbar. Both edges blur progressively; the toolbar is
Regular because it floats over text and the blur affects only the backdrop.

```swift
import LiquidGlass
import SwiftUI

struct Article: Identifiable {
    let id: Int
    let title: String
    let summary: String
    let hue: Double
}

struct ReadingList: View {
    @State private var scroll = GlassScrollPosition()
    @Environment(\.colorScheme) private var colorScheme

    private let articles = (0..<14).map { index in
        Article(
            id: index, title: "Story \(index + 1)",
            summary: "Glass bends the content behind it at the rim and blurs it toward the edges of the screen.",
            hue: Double(index) / 14
        )
    }

    var body: some View {
        GlassScene {
            GlassContent {
                GlassScrollView(position: scroll) {
                    VStack(alignment: .leading, spacing: 22) {
                        Text("Reading list").font(.largeTitle.bold())
                        ForEach(articles) { article in
                            HStack(alignment: .top, spacing: 14) {
                                GlassShape(radius: 16)
                                    .fill(Color(hue: article.hue, saturation: 0.65, brightness: 0.95).gradient)
                                    .frame(width: 72, height: 72)
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(article.title).font(.headline)
                                    Text(article.summary).font(.subheadline).foregroundStyle(.secondary)
                                }
                            }
                        }
                    }
                    .frame(maxWidth: 640, alignment: .leading)
                    .padding(.horizontal, 20)
                    .padding(.top, 132)                 // status bar + toolbar (84 is enough on macOS)
                    .padding(.bottom, 120)
                    .frame(maxWidth: .infinity)
                }
                .background(colorScheme == .dark ? Color(white: 0.06) : Color(white: 0.96))
            }
            .ignoresSafeArea()

            VStack(spacing: 0) {
                ZStack(alignment: .top) {
                    GlassProgressiveBlur(edge: .top, size: 128)
                        .ignoresSafeArea(edges: .top)
                    GlassToolbar(material: .regular) {
                        GlassButton(icon: "line.3.horizontal.decrease", label: "Filter") {}
                        GlassToolbarSpacer()
                        GlassButton(icon: "arrow.up", label: "Scroll to top") { scroll.scrollTo(edge: .top) }
                    }
                    .padding(.horizontal, 20)
                    .padding(.top, 8)
                }
                Spacer(minLength: 0)
                GlassProgressiveBlur(edge: .bottom, size: 120)
                    .ignoresSafeArea(edges: .bottom)
            }
        }
    }
}

#Preview { ReadingList() }
```

Read `scroll.offset.y` to drive effects (for example fade in a compact title once it passes 60), and call
`scroll.scrollTo(edge: .bottom)` or `scroll.scrollTo(CGPoint(x: 0, y: 400), animated: true)` to move it.
