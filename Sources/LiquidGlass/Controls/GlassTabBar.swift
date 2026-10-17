import Observation
import SwiftUI

/// Tabs on a glass capsule with a glass lens that slides to the selection.
///
/// The lens springs between items on the track spring and stretches with
/// its speed; drag across the bar to carry it and release to choose the tab
/// under it. On iOS the bar is 62 points tall with icon and label, and the
/// selected item takes the accent color; on macOS it is 28 points tall with
/// labels, unselected labels at 85% opacity, and the arrow keys move the
/// selection.
///
/// ```swift
/// GlassTabBar(selection: $page) {
///     GlassTab("Home", systemImage: "house", value: Page.home)
///     GlassTab("Search", systemImage: "magnifyingglass", value: Page.search)
/// }
/// ```
public struct GlassTabBar<Selection: Hashable, Content: View>: View {
    @Binding private var selection: Selection
    private let options: GlassSurfaceOptions
    private let content: Content

    @State private var lens = TabLens()
    @State private var barInteraction = GlassInteraction()
    @State private var drag = GlassDragIntent(distance: GlassControlMetrics.dragCommitDistance)
    @GestureState private var tracking = false
    @Namespace private var space
    @Environment(\.glassMotion) private var inheritedMotion
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.controlSize) private var controlSize
    #if os(macOS)
    @FocusState private var focused: Bool
    #endif

    /// Creates a tab bar.
    /// - Parameters:
    ///   - selection: The selected tab's value.
    ///   - material: Clear or Regular glass; `nil` inherits the scene's.
    ///     The selection lens uses the same material.
    ///   - appearance: Light or dark glass; `nil` inherits. The tabs take
    ///     its foreground and color scheme.
    ///   - tint: Colors the bar's glass; the tabs turn white or dark for
    ///     contrast.
    ///   - interactive: Whether the bar responds to presses and lens drags.
    ///   - motion: Overrides the scene's motion level.
    ///   - content: ``GlassTab`` items.
    public init(
        selection: Binding<Selection>, material: GlassMaterial? = nil, appearance: GlassAppearance? = nil,
        tint: Color? = nil, interactive: Bool = true, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    ) {
        self._selection = selection
        self.options = GlassSurfaceOptions(
            material: material, appearance: appearance, radius: .capsule, refraction: nil,
            tint: tint, interactive: interactive, motion: motion
        )
        self.content = content()
    }

    public var body: some View {
        let inset = GlassControlMetrics.toolbarInset
        let motion = (options.motion ?? inheritedMotion).resolved(reduceMotion: reduceMotion)
        HStack(spacing: 0) {
            content
        }
        .environment(\.glassTabContext, GlassTabContext(
            selection: AnyHashable(selection),
            select: { value in
                if let value = value.base as? Selection, value != selection { selection = value }
            }
        ))
        .modifier(GlassForegroundStyle(options: options))
        .padding(inset)
        .modifier(GlassBarHeight(height: GlassControlMetrics.tabBarHeight(controlSize), minimum: false))
        .backgroundPreferenceValue(GlassTabKey.self) { tabs in
            GeometryReader { proxy in
                let frames = tabs.map { TabFrame(value: $0.value, rect: proxy[$0.anchor]) }
                let selected = frames.first { $0.value == AnyHashable(selection) }?.rect
                ZStack(alignment: .topLeading) {
                    GlassSurfaceCore(
                        options: options, interaction: barInteraction, handlesInput: false,
                        content: Color.clear.frame(width: proxy.size.width, height: proxy.size.height)
                    )
                    if lens.isPlaced {
                        // The lens is the bar's own material, so a frosted bar
                        // stays frosted under the selection.
                        GlassSurfaceCore(
                            options: GlassSurfaceOptions(
                                material: options.material, appearance: options.appearance, radius: .capsule,
                                refraction: nil, tint: nil, interactive: false, motion: options.motion
                            ),
                            outline: lens.presentation, restingSize: lens.restingSize, extraPadding: 12,
                            content: Color.clear.frame(width: proxy.size.width, height: proxy.size.height)
                        )
                    }
                }
                .frame(width: proxy.size.width, height: proxy.size.height, alignment: .topLeading)
                .onChange(of: frames, initial: true) { _, frames in lens.tabs = frames }
                .onChange(of: selected, initial: true) { _, rect in
                    if let rect { lens.select(rect, motion: motion) }
                }
            }
            // Tab frames come from anchors, which are left to right in every
            // layout direction, like the lens outline drawn from them.
            .environment(\.layoutDirection, .leftToRight)
        }
        .coordinateSpace(.named(space))
        .contentShape(Capsule())
        // Presses light the bar without taking the touch: a scroll that starts
        // on the bar still scrolls. The lens follows a drag only once it moves
        // sideways, and then keeps the touch.
        .modifier(GlassTouchInput(
            space: NamedCoordinateSpace.named(space), tracking: $tracking,
            changed: { pointerMoved(to: $0, motion: motion) },
            changedGlobally: { _ in },
            ended: { _ in finishDrag() },
            claimsTouch: { drag.isCommitted }
        ))
        #if os(macOS)
        .focusable(interactions: .activate)
        .focused($focused)
        // The bar draws its own focus ring, like a focused glass button.
        .focusEffectDisabled()
        .overlay {
            if focused {
                Capsule()
                    .stroke(Color.accentColor.opacity(0.6), lineWidth: 3)
                    .padding(-3)
                    .allowsHitTesting(false)
            }
        }
        .onKeyPress(.leftArrow) { moveSelection(by: -1) }
        .onKeyPress(.rightArrow) { moveSelection(by: 1) }
        #endif
        .accessibilityElement(children: .contain)
        #if os(iOS)
        .accessibilityAddTraits(.isTabBar)
        #endif
        .sensoryFeedback(.selection, trigger: selection)
        // Like the system's bars: they stop growing at the largest text sizes
        // and their labels show in the Large Content Viewer instead.
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
    }

    private func pointerMoved(to location: CGPoint, motion: GlassMotion) {
        guard options.interactive else { return }
        barInteraction.pointerChanged(local: location)
        if let start = drag.update(location) {
            lens.drag(to: location.x, start: start.x, motion: motion)
        }
    }

    private func finishDrag() {
        barInteraction.pointerEnded()
        let carried = drag.isCommitted
        drag.reset()
        guard carried else { return }
        if let value = lens.endDrag()?.base as? Selection, value != selection { selection = value }
    }

    #if os(macOS)
    /// Moves the selection to the next tab on the left (`-1`) or right (`1`),
    /// in the order the tabs appear in either layout direction.
    private func moveSelection(by step: Int) -> KeyPress.Result {
        let values = lens.valuesInVisualOrder.compactMap { $0.base as? Selection }
        guard let index = values.firstIndex(of: selection) else { return .ignored }
        let next = min(max(index + step, 0), values.count - 1)
        if next != index { selection = values[next] }
        return .handled
    }
    #endif
}

/// One item of a ``GlassTabBar``.
public struct GlassTab<Value: Hashable>: View {
    private let title: Text
    private let systemImage: String?
    private let value: Value

    @Environment(\.glassTabContext) private var context
    @Environment(\.controlSize) private var controlSize

    /// Creates a tab with a title, an optional SF Symbol and its value.
    public init(_ titleKey: LocalizedStringKey, systemImage: String? = nil, value: Value) {
        self.title = Text(titleKey)
        self.systemImage = systemImage
        self.value = value
    }

    /// Creates a tab from a title string.
    @_disfavoredOverload
    public init<S: StringProtocol>(_ title: S, systemImage: String? = nil, value: Value) {
        self.title = Text(title)
        self.systemImage = systemImage
        self.value = value
    }

    public var body: some View {
        let selected = context?.selection == AnyHashable(value)
        label(selected: selected)
            // Like the system's bars: labels stop growing at the largest
            // text sizes and show in the Large Content Viewer instead.
            .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
            .accessibilityShowsLargeContentViewer {
                if let systemImage { Image(systemName: systemImage) }
                title
            }
            .contentShape(Capsule())
            .onTapGesture { context?.select(AnyHashable(value)) }
            .anchorPreference(key: GlassTabKey.self, value: .bounds) { [GlassTabAnchor(value: AnyHashable(value), anchor: $0)] }
            .accessibilityElement(children: .combine)
            .accessibilityAddTraits(selected ? [.isButton, .isSelected] : .isButton)
            .accessibilityAction { context?.select(AnyHashable(value)) }
    }

    @ViewBuilder
    private func label(selected: Bool) -> some View {
        #if os(macOS)
        HStack(spacing: 4) {
            if let systemImage { Image(systemName: systemImage) }
            title
        }
        .font(.system(size: 13, weight: .medium))
        .lineLimit(1)
        .padding(.horizontal, 10)
        .frame(maxHeight: .infinity)
        .opacity(selected ? 1 : 0.85)
        .animation(.easeOut(duration: 0.16), value: selected)
        #else
        Group {
            if controlSize == .small || controlSize == .mini {
                HStack(spacing: 6) {
                    if let systemImage { Image(systemName: systemImage) }
                    title
                }
                .font(.subheadline.weight(.semibold))
            } else {
                VStack(spacing: 2) {
                    if let systemImage {
                        Image(systemName: systemImage)
                            .font(.system(size: 20, weight: .medium))
                            .imageScale(.medium)
                            .frame(height: 24)
                    }
                    title
                        .font(.caption2.weight(.semibold))
                }
            }
        }
        .lineLimit(1)
        .padding(.horizontal, 8)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .foregroundStyle(selected ? AnyShapeStyle(Color.accentColor) : AnyShapeStyle(.foreground.opacity(0.62)))
        #endif
    }
}

// MARK: Plumbing

struct GlassTabContext {
    let selection: AnyHashable
    let select: (AnyHashable) -> Void
}

private struct GlassTabContextKey: EnvironmentKey {
    static let defaultValue: GlassTabContext? = nil
}

extension EnvironmentValues {
    var glassTabContext: GlassTabContext? {
        get { self[GlassTabContextKey.self] }
        set { self[GlassTabContextKey.self] = newValue }
    }
}

struct GlassTabAnchor {
    let value: AnyHashable
    let anchor: Anchor<CGRect>
}

struct GlassTabKey: PreferenceKey {
    static let defaultValue: [GlassTabAnchor] = []

    static func reduce(value: inout [GlassTabAnchor], nextValue: () -> [GlassTabAnchor]) {
        value.append(contentsOf: nextValue())
    }
}

struct TabFrame: Equatable {
    let value: AnyHashable
    let rect: CGRect
}

/// The selection lens: springs for its position and size along the bar,
/// following a drag on the track spring.
@MainActor
@Observable
final class TabLens: GlassAnimating {
    /// The lens outline in the bar's local points, stretched with its speed.
    private(set) var presentation = CGRect.zero
    private(set) var isPlaced = false

    /// The lens's size once its springs settle, unstretched: the tab it is
    /// moving to. Its unset refraction is measured on it.
    var restingSize: CGSize {
        CGSize(width: width.state.target, height: height.state.target)
    }

    @ObservationIgnored var tabs: [TabFrame] = []
    @ObservationIgnored private var x = SpringChannel(0, .track)
    @ObservationIgnored private var width = SpringChannel(0, .track)
    @ObservationIgnored private var y = SpringChannel(0, .track)
    @ObservationIgnored private var height = SpringChannel(0, .track)
    @ObservationIgnored private var motion = GlassMotion.full
    @ObservationIgnored private var dragging = false
    @ObservationIgnored private var grab = 0.0

    /// The tabs' values from left to right, as they appear in either layout
    /// direction (`tabs` is in declaration order).
    var valuesInVisualOrder: [AnyHashable] {
        tabs.sorted { $0.rect.midX < $1.rect.midX }.map(\.value)
    }

    /// Moves the lens to the selected tab's frame: on the track spring, which
    /// is critically damped under reduced motion, or at once without motion.
    func select(_ rect: CGRect, motion: GlassMotion) {
        self.motion = motion
        guard !dragging else { return }
        if !isPlaced || motion == .none {
            x.jump(to: rect.minX); width.jump(to: rect.width)
            y.jump(to: rect.minY); height.jump(to: rect.height)
            isPlaced = rect.width > 0
            publish()
            return
        }
        x.set(rect.minX, .track, motion: motion)
        width.set(rect.width, .track, motion: motion)
        y.set(rect.minY, .track, motion: motion)
        height.set(rect.height, .track, motion: motion)
        GlassMotionDriver.shared.run(self)
    }

    /// Carries the lens with a drag across the bar.
    func drag(to location: CGFloat, start: CGFloat, motion: GlassMotion) {
        self.motion = motion
        // The tabs' frames run in declaration order, which is right to left
        // in a right-to-left layout: the lens stays between the outermost.
        guard motion != .none, isPlaced,
              let lower = tabs.map({ Double($0.rect.minX) }).min(),
              let upper = tabs.map({ Double($0.rect.maxX) }).max() else { return }
        let location = Double(location), start = Double(start)
        if !dragging {
            guard abs(location - start) > 4 else { return }
            dragging = true
            // Grab the lens where it was touched; pressing elsewhere pulls it there.
            let current = x.value + width.value / 2
            grab = abs(start - current) < width.value / 2 ? start - current : 0
        }
        let size = nearest(to: location - grab).map { Double($0.rect.width) } ?? width.state.target
        let center = min(max(location - grab, lower + size / 2), upper - size / 2)
        let follow = GlassSpring.follow(width: size, height: height.value)
        x.set(center - size / 2, follow, motion: motion)
        width.set(size, follow, motion: motion)
        GlassMotionDriver.shared.run(self)
    }

    /// Ends a drag, returning the tab under the lens if the lens was carried.
    func endDrag() -> AnyHashable? {
        guard dragging else { return nil }
        dragging = false
        let choice = nearest(to: x.value + width.value / 2)
        if let choice {
            x.set(choice.rect.minX, .track, motion: motion)
            width.set(choice.rect.width, .track, motion: motion)
            GlassMotionDriver.shared.run(self)
        }
        return choice?.value
    }

    private func nearest(to center: Double) -> TabFrame? {
        tabs.min { abs(Double($0.rect.midX) - center) < abs(Double($1.rect.midX) - center) }
    }

    func advance(by dt: Double) -> Bool {
        x.advance(by: dt); width.advance(by: dt); y.advance(by: dt); height.advance(by: dt)
        publish()
        return dragging || !(x.isSettled && width.isSettled && y.isSettled && height.isSettled)
    }

    private func publish() {
        // The lens stretches along its path with its speed (and less once released).
        let stretch = motion == .full ? min(0.2, abs(x.velocity) * 0.00035) * (dragging ? 1 : 0.6) : 0
        let along = 1 + stretch, across = 1 / (1 + stretch).squareRoot()
        let w = width.value * along, h = height.value * across
        let rect = CGRect(x: x.value + width.value / 2 - w / 2, y: y.value + height.value / 2 - h / 2, width: w, height: h)
        if rect != presentation { presentation = rect }
    }
}
