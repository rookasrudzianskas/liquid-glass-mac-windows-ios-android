import LiquidGlass
import SwiftUI

/// A vivid wallpaper under a draggable Regular glass card, a floating
/// toolbar and the tab bar.
struct HeroPage: View {
    @Binding var appearance: GlassAppearance
    @Binding var page: StudioPage

    @State private var offset = CGSize.zero

    var body: some View {
        VStack(spacing: 0) {
            toolbar
            // The card floats in the middle; at the largest text sizes, when
            // it no longer fits above the tab bar, it scrolls instead.
            ViewThatFits(in: .vertical) {
                VStack(spacing: 0) {
                    Spacer(minLength: 24)
                    draggableCard
                    Spacer(minLength: StudioLayout.tabBarClearance)
                }
                ScrollView {
                    card
                        .padding(.top, 24)
                        .padding(.bottom, StudioLayout.tabBarClearance)
                }
                .scrollIndicators(.hidden)
            }
        }
        .padding(.horizontal, StudioLayout.pagePadding)
    }

    private var draggableCard: some View {
        card
            .offset(offset)
            .gesture(
                DragGesture()
                    .onChanged { offset = $0.translation }
                    .onEnded { _ in
                        // Let the card drift home on a gel-like spring.
                        withAnimation(.spring(duration: 0.6, bounce: 0.3)) { offset = .zero }
                    }
            )
    }

    private var toolbar: some View {
        GlassToolbar {
            GlassButton("Studio", systemImage: "drop.fill") { page = .hero }
            GlassToolbarSpacer()
            GlassButton(icon: appearanceSymbol, label: "Appearance") { cycleAppearance() }
            ShareButton()
            GlassMenu(icon: "ellipsis", label: "More") {
                GlassMenuItem("Controls", systemImage: "slider.horizontal.3") { page = .controls }
                GlassMenuItem("Materials lab", systemImage: "drop") { page = .lab }
                GlassMenuItem("Progressive blur", systemImage: "text.below.photo") { page = .blur }
                GlassMenuDivider()
                GlassMenuItem("Light", systemImage: "sun.max", checked: appearance == .light) { appearance = .light }
                GlassMenuItem("Dark", systemImage: "moon", checked: appearance == .dark) { appearance = .dark }
                GlassMenuItem("Match system", systemImage: "circle.lefthalf.filled", checked: appearance == .automatic) { appearance = .automatic }
            }
        }
        .padding(.top, 8)
    }

    private var card: some View {
        GlassSurface(material: .regular, radius: 34, interactive: true) {
            VStack(alignment: .leading, spacing: 14) {
                HStack {
                    Image(systemName: "drop.fill")
                        .font(.title2.weight(.semibold))
                    Spacer()
                    Text("SwiftUI · Metal")
                        .font(.caption.weight(.semibold))
                        .padding(.horizontal, 10)
                        .padding(.vertical, 5)
                        .background(Capsule().fill(.foreground.opacity(0.08)))
                }
                Text("Liquid Glass")
                    .font(.system(size: 40, weight: .bold, design: .rounded))
                    .minimumScaleFactor(0.7)
                    .lineLimit(1)
                Text("Refraction, frost and light that follow your finger. Drag the card, press it, let it go.")
                    .font(.callout)
                    .opacity(0.75)
                    .fixedSize(horizontal: false, vertical: true)
                HStack(spacing: 10) {
                    GlassButton("Controls", systemImage: "arrow.forward", prominent: true) { page = .controls }
                    GlassButton("Lab", systemImage: "drop") { page = .lab }
                }
                .padding(.top, 4)
            }
            .padding(26)
            .frame(maxWidth: 360)
        }
        .accessibilityLabel("Liquid Glass card")
    }

    private var appearanceSymbol: String {
        switch appearance {
        case .automatic: "circle.lefthalf.filled"
        case .light: "sun.max"
        case .dark: "moon"
        }
    }

    private func cycleAppearance() {
        switch appearance {
        case .automatic: appearance = .dark
        case .dark: appearance = .light
        case .light: appearance = .automatic
        }
    }
}
