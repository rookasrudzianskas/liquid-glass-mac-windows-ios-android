import LiquidGlass
import SwiftUI

@main
struct LiquidGlassStudioApp: App {
    var body: some Scene {
        WindowGroup {
            StudioRoot()
        }
        #if os(macOS)
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1180, height: 820)
        #endif
    }
}

/// One glass scene for the whole app: each page supplies its backdrop and
/// its foreground, and the tab bar floats over all of them, so its lens
/// keeps moving smoothly between pages.
struct StudioRoot: View {
    // `-page lab -appearance dark` launch arguments choose where to start.
    @State private var page = StudioPage(rawValue: UserDefaults.standard.string(forKey: "page") ?? "") ?? .hero
    @State private var appearance = GlassAppearance(rawValue: UserDefaults.standard.string(forKey: "appearance") ?? "") ?? .automatic
    @State private var lab = LabSettings()
    @State private var stories = GlassScrollPosition()
    @State private var storyFilter: String?

    var body: some View {
        GlassScene(appearance: appearance) {
            GlassContent {
                backdrop
                    .id(page)
                    .transition(.opacity)
            }
            .ignoresSafeArea()

            ZStack(alignment: .bottom) {
                foreground
                    .id(page)
                    .transition(.opacity)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                tabBar
            }
        }
        .animation(.easeInOut(duration: 0.3), value: page)
        .preferredColorScheme(appearance.colorScheme)
        #if os(macOS)
        .frame(minWidth: 760, minHeight: 620)
        #endif
    }

    @ViewBuilder
    private var backdrop: some View {
        switch page {
        case .hero: Wallpaper.aurora.view
        case .controls: Wallpaper.midnight.view
        case .lab: lab.wallpaper.view
        case .blur: StoriesBackdrop(position: stories, filter: storyFilter)
        }
    }

    @ViewBuilder
    private var foreground: some View {
        switch page {
        case .hero: HeroPage(appearance: $appearance, page: $page)
        case .controls: ControlsPage()
        case .lab: LabPage(settings: lab)
        case .blur: StoriesPage(position: stories, filter: $storyFilter)
        }
    }

    private var tabBar: some View {
        // Over the scrolling stories the bars frost, so text never shows through.
        GlassTabBar(selection: $page, material: page == .blur ? .regular : nil) {
            GlassTab("Hero", systemImage: "sparkles", value: StudioPage.hero)
            GlassTab("Controls", systemImage: "slider.horizontal.3", value: StudioPage.controls)
            GlassTab("Lab", systemImage: "drop", value: StudioPage.lab)
            GlassTab("Blur", systemImage: "text.below.photo", value: StudioPage.blur)
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

enum StudioPage: String, CaseIterable, Hashable {
    case hero, controls, lab, blur
}

extension GlassAppearance {
    /// The color scheme the app shows for this appearance.
    var colorScheme: ColorScheme? {
        switch self {
        case .automatic: nil
        case .light: .light
        case .dark: .dark
        }
    }
}
