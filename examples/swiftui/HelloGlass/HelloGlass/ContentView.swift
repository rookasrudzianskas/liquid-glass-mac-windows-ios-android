import LiquidGlass
import SwiftUI

/// One screen of Liquid Glass over the aurora wallpaper: a toolbar, a glass
/// card with two buttons and a switch, and a floating tab bar.
struct ContentView: View {
    enum Tab: Hashable {
        case home, explore, settings
    }

    @State private var tab = Tab.home
    @State private var frosted = true
    /// The color scheme chosen with the toolbar's appearance button; `nil`
    /// follows the system until then.
    @State private var scheme: ColorScheme?

    @Environment(\.colorScheme) private var colorScheme
    @Environment(\.openURL) private var openURL

    private static let guide = URL(string: "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/docs/apple.md")!

    var body: some View {
        GlassScene {
            // What the glass refracts. It runs under the bars and the safe
            // area; the foreground below still respects the safe area.
            GlassContent {
                Image(.aurora)
                    .resizable()
                    .scaledToFill()
                    .frame(minWidth: 0, maxWidth: .infinity, minHeight: 0, maxHeight: .infinity)
                    .clipped()
                    .accessibilityHidden(true)
            }
            .ignoresSafeArea()

            VStack(spacing: 0) {
                toolbar
                Spacer(minLength: 24)
                card
                Spacer(minLength: 24)
                tabBar
            }
            .padding(.horizontal, 20)
        }
        .preferredColorScheme(scheme)
    }

    private var toolbar: some View {
        GlassToolbar {
            GlassButton(
                icon: colorScheme == .dark ? "sun.max" : "moon",
                label: colorScheme == .dark ? "Light appearance" : "Dark appearance"
            ) {
                scheme = colorScheme == .dark ? .light : .dark
            }
            GlassToolbarSpacer()
            GlassButton(icon: "person.crop.circle", label: "Profile") { tab = .settings }
        }
        .padding(.top, 8)
    }

    private var card: some View {
        GlassSurface(material: frosted ? .regular : .clear, radius: 32) {
            VStack(alignment: .leading, spacing: 16) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Hello, Glass")
                        .font(.largeTitle.bold())
                    Text("Native glass over a live backdrop.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
                .accessibilityElement(children: .combine)
                HStack(spacing: 12) {
                    GlassButton("Get started", prominent: true) { tab = .explore }
                    GlassButton("Learn more") { openURL(Self.guide) }
                }
                GlassSwitch("Frosted", isOn: $frosted)
            }
            .padding(24)
            .frame(maxWidth: 360, alignment: .leading)
        }
    }

    private var tabBar: some View {
        GlassTabBar(selection: $tab) {
            GlassTab("Home", systemImage: "house", value: Tab.home)
            GlassTab("Explore", systemImage: "safari", value: Tab.explore)
            GlassTab("Settings", systemImage: "gearshape", value: Tab.settings)
        }
        #if os(macOS)
        .fixedSize()
        .padding(.bottom, 20)
        #else
        .frame(maxWidth: 400)
        .padding(.bottom, 4)
        #endif
    }
}

#Preview {
    ContentView()
}
