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
    @ScaledMetric(relativeTo: .body) private var typeScale: CGFloat = 1

    /// Creates a toolbar.
    /// - Parameters:
    ///   - material: Clear or Regular glass; `nil` inherits the scene's.
    ///   - appearance: Light or dark glass; `nil` inherits.
    ///   - tint: Colors the glass.
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
        .modifier(OnTintForeground(tinted: options.tint != nil))
        .padding(inset)
        .frame(minHeight: (GlassControlMetrics.toolbarHeight * typeScale).rounded())
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
                .onChange(of: clusters, initial: true) { _, rects in layout.clusters = rects }
            }
        }
        .coordinateSpace(.named(space))
        .contentShape(Rectangle())
        .simultaneousGesture(
            DragGesture(minimumDistance: 0, coordinateSpace: .named(space))
                .updating($tracking) { _, state, _ in state = true }
                .onChanged { layout.pointerChanged(at: $0.location, enabled: options.interactive) }
                .onEnded { _ in layout.pointerEnded() }
        )
        .simultaneousGesture(
            DragGesture(minimumDistance: 0, coordinateSpace: .global)
                .onChanged { layout.pointerChanged(global: $0.location) }
        )
        .onContinuousHover(coordinateSpace: .named(space)) { phase in
            switch phase {
            case .active(let location): layout.hover(at: location, enabled: options.interactive)
            case .ended: layout.hover(at: nil, enabled: options.interactive)
            }
        }
        .onChange(of: tracking) { _, isTracking in
            if !isTracking { layout.pointerEnded() }
        }
        .accessibilityElement(children: .contain)
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
            case .fixed: Color.clear.frame(width: length)
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
