import LiquidGlass
import SwiftUI

/// A scrolling article list as the scene's backdrop. It scrolls with
/// `GlassScrollView`, so the toolbar and the progressive blur refract the
/// stories exactly where they are while they move.
struct StoriesBackdrop: View {
    let position: GlassScrollPosition
    /// The category shown, or `nil` for every story.
    let filter: String?

    @Environment(\.colorScheme) private var colorScheme

    var body: some View {
        GlassScrollView(position: position) {
            VStack(alignment: .leading, spacing: 28) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(filter ?? "Stories")
                        .font(.largeTitle.bold())
                    Text("Scroll: the top and bottom edges blur progressively under the glass.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                ForEach(Story.all.filter { filter == nil || $0.category == filter }) { story in
                    StoryCard(story: story)
                }
            }
            .frame(maxWidth: 640, alignment: .leading)
            .padding(.horizontal, StudioLayout.pagePadding)
            .padding(.top, Self.topInset)
            .padding(.bottom, StudioLayout.tabBarClearance + 48)
            .frame(maxWidth: .infinity)
        }
        .background(colorScheme == .dark ? Color(white: 0.06) : Color(white: 0.96))
    }

    /// Room for the status bar and the floating toolbar. The backdrop lays
    /// out without safe-area insets, so the inset is explicit.
    #if os(macOS)
    static let topInset: CGFloat = 84
    #else
    static let topInset: CGFloat = 132
    #endif
}

/// The glass over the stories: a toolbar and the progressive edges.
struct StoriesPage: View {
    let position: GlassScrollPosition
    @Binding var filter: String?

    /// How far below the top safe area the top blur reaches: past the
    /// toolbar row, so stories passing between and under its clusters blur.
    #if os(macOS)
    static let topBlurDepth: CGFloat = 128
    #else
    static let topBlurDepth: CGFloat = 116
    #endif

    var body: some View {
        GeometryReader { proxy in
            content(safeTop: proxy.safeAreaInsets.top)
        }
    }

    private func content(safeTop: CGFloat) -> some View {
        VStack(spacing: 0) {
            ZStack(alignment: .top) {
                // Measured from the top of the screen, through the status bar
                // and the toolbar row.
                GlassProgressiveBlur(edge: .top, size: safeTop + Self.topBlurDepth)
                    .ignoresSafeArea(edges: .top)
                GlassToolbar(material: .regular) {
                    GlassMenu(icon: "line.3.horizontal.decrease", label: "Filter") {
                        GlassMenuItem("All stories", checked: filter == nil) { show(nil) }
                        GlassMenuDivider()
                        ForEach(Story.categories, id: \.self) { category in
                            GlassMenuItem(category, checked: filter == category) { show(category) }
                        }
                    }
                    GlassToolbarSpacer()
                    GlassButton(icon: "arrow.up", label: "Scroll to top") { position.scrollTo(edge: .top) }
                    GlassMenu(icon: "ellipsis", label: "More") {
                        GlassMenuItem("Scroll to top", systemImage: "arrow.up.to.line") { position.scrollTo(edge: .top) }
                        GlassMenuItem("Scroll to end", systemImage: "arrow.down.to.line") { position.scrollTo(edge: .bottom) }
                    }
                }
                .padding(.horizontal, StudioLayout.pagePadding)
                .padding(.top, 8)
            }
            Spacer(minLength: 0)
            GlassProgressiveBlur(edge: .bottom, size: 120)
                .ignoresSafeArea(edges: .bottom)
        }
    }

    /// Shows one category (or every story) from the top.
    private func show(_ category: String?) {
        filter = category
        position.scrollTo(edge: .top, animated: false)
    }
}

private struct StoryCard: View {
    let story: Story

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Image(story.wallpaper.rawValue)
                .resizable()
                .scaledToFill()
                .frame(height: 190)
                .frame(maxWidth: .infinity)
                .clipShape(GlassShape(radius: 24))
            Text(story.category.uppercased())
                .font(.caption.weight(.semibold))
                .kerning(0.6)
                .foregroundStyle(.secondary)
            Text(story.title)
                .font(.title2.bold())
            Text(story.excerpt)
                .font(.body)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .accessibilityElement(children: .combine)
    }
}

struct Story: Identifiable {
    let id: Int
    let category: String
    let title: String
    let excerpt: String
    let wallpaper: Wallpaper

    /// The categories, in the order the stories first use them.
    static var categories: [String] {
        all.reduce(into: []) { names, story in
            if !names.contains(story.category) { names.append(story.category) }
        }
    }

    static let all: [Story] = [
        Story(id: 0, category: "Optics", title: "Light, bent on purpose",
              excerpt: "A twenty-point bevel pulls the backdrop inward at the rim, so content near the edge looks magnified, the way a thick lens behaves.",
              wallpaper: .aurora),
        Story(id: 1, category: "Materials", title: "A field guide to frosted glass",
              excerpt: "Regular glass blurs what is behind it, then lifts or deepens it with a wider fill, keeping text readable over anything.",
              wallpaper: .ocean),
        Story(id: 2, category: "Shape", title: "Why continuous corners feel calm",
              excerpt: "Superellipse corners meet the edges without a jump in curvature, so the outline reads as one smooth form.",
              wallpaper: .dunes),
        Story(id: 3, category: "Interaction", title: "Designing for the finger",
              excerpt: "Glass grows when pressed, stretches toward a drag while keeping its volume, and settles with a soft overshoot.",
              wallpaper: .midnight),
        Story(id: 4, category: "Motion", title: "Springs you can solve",
              excerpt: "Every animation is a damped spring with a duration and a bounce, solved in closed form so a dropped frame never changes the path.",
              wallpaper: .ocean),
        Story(id: 5, category: "Color", title: "Tint without paint",
              excerpt: "Tinted glass passes the backdrop's luminance through its pigment, so it stays alive instead of turning into a flat fill.",
              wallpaper: .aurora),
        Story(id: 6, category: "Accessibility", title: "Glass that steps aside",
              excerpt: "Reduce Transparency swaps in an opaque fill, Increase Contrast adds a border, and Reduce Motion removes the elasticity.",
              wallpaper: .dunes),
        Story(id: 7, category: "Platforms", title: "One model, four platforms",
              excerpt: "SwiftUI, Jetpack Compose and WPF share the same equations and the same golden test vectors.",
              wallpaper: .midnight),
    ]
}
