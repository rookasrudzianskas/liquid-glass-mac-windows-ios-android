import SwiftUI

/// A floating toolbar of glass clusters.
///
/// Items lay out in a row on capsule glass, 52 points tall with a 4-point
/// inset on iOS (34 and 3 on macOS). A ``GlassToolbarSpacer`` splits the row
/// into separate clusters, each on its own capsule. ``GlassButton``s inside a
/// toolbar drop their own glass and highlight with the foreground color at 8%
/// when pressed, hovered or focused; pressing anywhere on a cluster grows it
/// and lights it under the finger.
///
/// ```swift
/// GlassToolbar {
///     GlassButton(icon: "chevron.left", label: "Back") { }
///     GlassToolbarSpacer()
///     GlassButton(icon: "square.and.arrow.up", label: "Share") { }
///     GlassButton(icon: "ellipsis", label: "More") { }
/// }
/// ```
public struct GlassToolbar<Content: View>: View {
    private let options: GlassSurfaceOptions
    private let content: Content

    @State private var layout = ToolbarLayout()
    @GestureState private var tracking = false
    @Namespace private var space

    /// Creates a toolbar.
    /// - Parameters:
    ///   - material: Clear or Regular glass; `nil` inherits the scene's.
    ///   - appearance: Light or dark glass; `nil` inherits. The items take
    ///     its foreground and color scheme.
    ///   - tint: Colors the glass; the items turn white or dark for contrast.
    ///   - interactive: Whether clusters respond to presses. Default `true`.
    ///   - motion: Overrides the scene's motion level.
    public init(
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, tint: Color? = nil,
        interactive: Bool = true, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    ) {
        self.options = GlassSurfaceOptions(
            material: material, appearance: appearance, radius: .capsule, refraction: nil,
            tint: tint, interactive: interactive, motion: motion
        )
        self.content = content()
    }

    public var body: some View {
        let inset = GlassControlMetrics.toolbarInset
        HStack(spacing: GlassToolbarSpacer.itemSpacing) {
            content
        }
        .environment(\.glassToolbarItem, true)
        .modifier(GlassForegroundStyle(options: options))
        .padding(inset)
        .modifier(GlassBarHeight(height: GlassControlMetrics.toolbarHeight, minimum: true))
        .backgroundPreferenceValue(GlassToolbarSpacerKey.self) { spacers in
            GeometryReader { proxy in
                let clusters = Self.clusters(in: proxy.size, spacers: spacers.map { proxy[$0] }, inset: inset)
                ZStack(alignment: .topLeading) {
                    ForEach(clusters.indices, id: \.self) { index in
                        GlassSurfaceCore(
                            options: options, interaction: layout.interaction(at: index), handlesInput: false,
                            content: Color.clear.frame(width: clusters[index].width, height: clusters[index].height)
                        )
                        .offset(x: clusters[index].minX, y: clusters[index].minY)
                    }
                }
                .frame(width: proxy.size.width, height: proxy.size.height, alignment: .topLeading)
                .onChange(of: clusters, initial: true) { _, rects in layout.clusters = rects }
            }
            // Cluster frames come from anchors, which are left to right in
            // every layout direction; so are the offsets placing them.
            .environment(\.layoutDirection, .leftToRight)
        }
        .coordinateSpace(.named(space))
        .contentShape(Rectangle())
        .modifier(GlassTouchInput(
            space: NamedCoordinateSpace.named(space), tracking: $tracking,
            changed: { layout.pointerChanged(at: $0, enabled: options.interactive) },
            changedGlobally: { layout.pointerChanged(global: $0) },
            ended: { _ in layout.pointerEnded() }
        ))
        .onContinuousHover(coordinateSpace: .named(space)) { phase in
            switch phase {
            case .active(let location): layout.hover(at: location, enabled: options.interactive)
            case .ended: layout.hover(at: nil, enabled: options.interactive)
            }
        }
        .accessibilityElement(children: .contain)
        // Bars stop growing at the largest text sizes, like the system's;
        // their items show in the Large Content Viewer instead.
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
    }

    /// The capsule of each cluster: the toolbar's bounds split at its spacers,
    /// keeping the toolbar's inset around every cluster's items.
    static func clusters(in size: CGSize, spacers: [CGRect], inset: CGFloat) -> [CGRect] {
        let gap = GlassToolbarSpacer.itemSpacing
        var rects: [CGRect] = []
        var start: CGFloat = 0
        for spacer in spacers.sorted(by: { $0.minX < $1.minX }) {
            let end = spacer.minX - gap + inset
            if end > start { rects.append(CGRect(x: start, y: 0, width: end - start, height: size.height)) }
            start = spacer.maxX + gap - inset
        }
        if size.width > start { rects.append(CGRect(x: start, y: 0, width: size.width - start, height: size.height)) }
        return rects
    }
}

/// A bar's height from its base metric, scaled with Dynamic Type. The bar
/// caps its Dynamic Type size, so this reads the capped size: the bar stops
/// growing when its labels do.
struct GlassBarHeight: ViewModifier {
    let height: CGFloat
    /// `true` for a minimum height, `false` for an exact one.
    let minimum: Bool

    @ScaledMetric(relativeTo: .body) private var typeScale: CGFloat = 1

    func body(content: Content) -> some View {
        let scaled = (height * typeScale).rounded()
        if minimum {
            content.frame(minHeight: scaled)
        } else {
            content.frame(height: scaled)
        }
    }
}

/// Separates a ``GlassToolbar``'s items into clusters on separate glass.
///
/// A flexible spacer takes the free space in the toolbar (at least `minLength`
/// of visible gap); a fixed one leaves exactly `minLength`.
public struct GlassToolbarSpacer: View {
    /// How a spacer sizes itself.
    public enum Sizing: Hashable, Sendable {
        /// Grows to fill the toolbar's free space.
        case flexible
        /// Keeps its minimum length.
        case fixed
    }

    private let sizing: Sizing
    private let minLength: CGFloat

    /// Space between items inside a cluster.
    static let itemSpacing: CGFloat = 4

    /// Creates a spacer.
    /// - Parameters:
    ///   - sizing: Flexible (default) or fixed.
    ///   - minLength: The smallest visible gap between the clusters, in points.
    public init(_ sizing: Sizing = .flexible, minLength: CGFloat = 8) {
        self.sizing = sizing
        self.minLength = minLength
    }

    public var body: some View {
        // The spacer also covers both clusters' insets and item spacing, so
        // the visible gap between the capsules is at least `minLength`.
        let length = max(0, minLength + 2 * GlassControlMetrics.toolbarInset - 2 * Self.itemSpacing)
        Group {
            switch sizing {
            case .flexible: Spacer(minLength: length)
            // No height of its own: the bar keeps its items' height.
            case .fixed: Color.clear.frame(width: length, height: 0)
            }
        }
        .anchorPreference(key: GlassToolbarSpacerKey.self, value: .bounds) { [$0] }
        .accessibilityHidden(true)
    }
}

struct GlassToolbarSpacerKey: PreferenceKey {
    static let defaultValue: [Anchor<CGRect>] = []

    static func reduce(value: inout [Anchor<CGRect>], nextValue: () -> [Anchor<CGRect>]) {
        value.append(contentsOf: nextValue())
    }
}

/// The toolbar's cluster frames and their interactions, so one gesture on
/// the whole toolbar can press the cluster under the finger.
@MainActor
final class ToolbarLayout {
    var clusters: [CGRect] = []
    private var interactions: [GlassInteraction] = []
    private var pressed: Int?
    private var hovered: Int?

    func interaction(at index: Int) -> GlassInteraction {
        while interactions.count <= index { interactions.append(GlassInteraction()) }
        return interactions[index]
    }

    private func cluster(at point: CGPoint) -> Int? {
        clusters.firstIndex { $0.contains(point) }
    }

    private func local(_ point: CGPoint, in index: Int) -> CGPoint {
        CGPoint(x: point.x - clusters[index].minX, y: point.y - clusters[index].minY)
    }

    func pointerChanged(at point: CGPoint, enabled: Bool) {
        guard enabled else { return }
        if pressed == nil { pressed = cluster(at: point) }
        guard let pressed, pressed < clusters.count else { return }
        interaction(at: pressed).pointerChanged(local: local(point, in: pressed))
    }

    func pointerChanged(global point: CGPoint) {
        if let pressed { interaction(at: pressed).pointerChanged(global: point) }
    }

    func pointerEnded() {
        if let pressed { interaction(at: pressed).pointerEnded() }
        pressed = nil
    }

    func hover(at point: CGPoint?, enabled: Bool) {
        guard enabled else { return }
        let index = point.flatMap { cluster(at: $0) }
        if let hovered, hovered != index { interaction(at: hovered).hover(nil) }
        hovered = index
        if let index, let point { interaction(at: index).hover(local(point, in: index)) }
    }
}
