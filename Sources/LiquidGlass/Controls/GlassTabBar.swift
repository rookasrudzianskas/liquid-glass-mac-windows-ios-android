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
    @GestureState private var tracking = false
    @Namespace private var space
    @Environment(\.glassMotion) private var inheritedMotion
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.controlSize) private var controlSize
    @ScaledMetric(relativeTo: .body) private var typeScale: CGFloat = 1

    /// Creates a tab bar.
    /// - Parameters:
    ///   - selection: The selected tab's value.
    ///   - material: Clear or Regular glass; `nil` inherits the scene's.
    ///   - appearance: Light or dark glass; `nil` inherits.
    ///   - tint: Colors the bar's glass.
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
        .modifier(OnTintForeground(tinted: options.tint != nil))
        .padding(inset)
        .frame(height: (GlassControlMetrics.tabBarHeight(controlSize) * typeScale).rounded())
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
                        GlassSurfaceCore(
                            options: GlassSurfaceOptions(
                                material: .clear, appearance: options.appearance, radius: .capsule,
                                refraction: nil, tint: nil, interactive: false, motion: options.motion
                            ),
                            outline: lens.presentation, extraPadding: 12,
                            content: Color.clear.frame(width: proxy.size.width, height: proxy.size.height)
                        )
                    }
                }
                .onChange(of: frames, initial: true) { _, frames in lens.tabs = frames }
                .onChange(of: selected, initial: true) { _, rect in
                    if let rect { lens.select(rect, motion: motion) }
                }
            }
        }
        .coordinateSpace(.named(space))
        .contentShape(Capsule())
        .simultaneousGesture(
            DragGesture(minimumDistance: 0, coordinateSpace: .named(space))
                .updating($tracking) { _, state, _ in state = true }
                .onChanged { value in
                    guard options.interactive else { return }
                    barInteraction.pointerChanged(local: value.location)
                    lens.drag(to: value.location.x, start: value.startLocation.x, motion: motion)
                }
                .onEnded { _ in finishDrag() }
        )
        .onChange(of: tracking) { _, isTracking in
            if !isTracking { finishDrag() }
        }
        #if os(macOS)
        .focusable()
        .focusEffectDisabled()
        .onKeyPress(.leftArrow) { moveSelection(by: -1) }
        .onKeyPress(.rightArrow) { moveSelection(by: 1) }
        #endif
        .accessibilityElement(children: .contain)
        .sensoryFeedback(.selection, trigger: selection)
    }

    private func finishDrag() {
        barInteraction.pointerEnded()
        if let value = lens.endDrag()?.base as? Selection, value != selection { selection = value }
    }

    #if os(macOS)
    private func moveSelection(by step: Int) -> KeyPress.Result {
        let values = lens.tabs.compactMap { $0.value.base as? Selection }
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

    @ObservationIgnored var tabs: [TabFrame] = []
    @ObservationIgnored private var x = SpringChannel(0, .track)
    @ObservationIgnored private var width = SpringChannel(0, .track)
    @ObservationIgnored private var y = SpringChannel(0, .track)
    @ObservationIgnored private var height = SpringChannel(0, .track)
    @ObservationIgnored private var motion = GlassMotion.full
    @ObservationIgnored private var dragging = false
    @ObservationIgnored private var grab = 0.0

    /// Moves the lens to the selected tab's frame.
    func select(_ rect: CGRect, motion: GlassMotion) {
        self.motion = motion
        guard !dragging else { return }
        if !isPlaced || motion != .full {
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
        guard motion != .none, isPlaced, let first = tabs.first, let last = tabs.last else { return }
        let location = Double(location), start = Double(start)
        if !dragging {
            guard abs(location - start) > 4 else { return }
            dragging = true
            // Grab the lens where it was touched; pressing elsewhere pulls it there.
            let current = x.value + width.value / 2
            grab = abs(start - current) < width.value / 2 ? start - current : 0
        }
        let size = nearest(to: location - grab).map { Double($0.rect.width) } ?? width.state.target
        let center = min(max(location - grab, Double(first.rect.minX) + size / 2), Double(last.rect.maxX) - size / 2)
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
