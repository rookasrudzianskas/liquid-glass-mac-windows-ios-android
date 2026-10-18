import Observation
import SwiftUI

/// A menu whose Regular glass panel grows out of its trigger.
///
/// The trigger is a ``GlassButton`` (a plain item inside a ``GlassToolbar``).
/// Opening morphs the trigger's glass into the panel on the morph spring
/// while the items condense out of a blur; closing reverses it. The panel's
/// glass mixes halfway toward a solid backing (`#F5F5F7` light, `#1C1C1E`
/// dark, ``GlassOptics/menuLegibility``), so its items keep at least 5:1
/// contrast over any backdrop while it still refracts and frosts. Items
/// highlight with the foreground color at 8%. Tap outside, press Escape, or
/// choose an item to dismiss; the arrow keys and Return navigate the items.
///
/// ```swift
/// GlassMenu(icon: "ellipsis", label: "More") {
///     GlassMenuItem("Duplicate", systemImage: "plus.square.on.square") { duplicate() }
///     GlassMenuDivider()
///     GlassMenuItem("Delete", systemImage: "trash", role: .destructive) { delete() }
/// }
/// ```
///
/// The panel draws in the enclosing ``GlassScene``'s overlay, above the rest
/// of its foreground. Outside a scene, the menu falls back to the system menu.
public struct GlassMenu<Label: View, Content: View>: View {
    private let options: GlassSurfaceOptions
    private let iconOnly: Bool
    /// The tooltip of an icon-only trigger: its otherwise invisible title.
    private let help: Text?
    private let content: Content
    private let label: Label

    @State private var morph = GlassMenuMorph()
    @Namespace private var id
    @Environment(\.glassOverlayAvailable) private var overlayAvailable
    @Environment(\.glassToolbarItem) private var toolbarItem
    @Environment(\.glassMotion) private var inheritedMotion
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    /// Creates a menu with a custom trigger label.
    /// - Parameters:
    ///   - material: The panel's material; `nil` is Regular. The trigger
    ///     inherits the scene's.
    ///   - appearance: Light or dark glass for the trigger and the panel,
    ///     and their labels; `nil` inherits.
    ///   - radius: The panel's corner radius. Default 28.
    ///   - tint: Colors the trigger's and the panel's glass; labels on it
    ///     turn white or dark for contrast.
    ///   - motion: Overrides the scene's motion level.
    ///   - content: ``GlassMenuItem``s and ``GlassMenuDivider``s.
    ///   - label: The trigger's label.
    public init(
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil,
        @ViewBuilder content: () -> Content, @ViewBuilder label: () -> Label
    ) {
        self.options = GlassSurfaceOptions(
            material: material, appearance: appearance, radius: radius, refraction: nil,
            tint: tint, interactive: true, motion: motion
        )
        self.iconOnly = false
        self.help = nil
        self.content = content()
        self.label = label()
    }

    public var body: some View {
        if overlayAvailable {
            glassMenu
        } else {
            Menu { content } label: { label }
        }
    }

    private var resolvedMotion: GlassMotion {
        (options.motion ?? inheritedMotion).resolved(reduceMotion: reduceMotion)
    }

    private var glassMenu: some View {
        trigger
            // A glass trigger becomes the panel: its own glass withdraws as
            // the panel grows out of it.
            .opacity(toolbarItem ? 1 : 1 - morph.progress)
            .blur(radius: toolbarItem ? 0 : morph.progress * 3)
            .anchorPreference(key: GlassOverlayKey.self, value: .bounds) { anchor in
                guard morph.isPresented else { return [] }
                return [GlassOverlayItem(
                    id: id, anchor: anchor,
                    content: { presenter, scene in
                        AnyView(GlassMenuPanel(
                            options: options, presenter: presenter, presenterRadius: toolbarItem ? 16 : presenter.height / 2,
                            scene: scene, morph: morph, dismiss: { morph.close(motion: resolvedMotion) }, content: content
                        ))
                    },
                    dismiss: { morph.close(motion: resolvedMotion) }
                )]
            }
            .accessibilityAddTraits(morph.isOpen ? .isSelected : [])
            .modifier(OptionalHelp(text: help))
    }

    /// The trigger: a glass button with the menu's material, appearance,
    /// tint and motion. An icon-only trigger is the icon button circle (44 pt
    /// on iOS, 28 on macOS); the menu adds its tooltip itself.
    private var trigger: some View {
        GlassButton(
            iconOnly: iconOnly, help: nil, role: nil, radius: iconOnly ? .circle : nil, material: options.material,
            appearance: options.appearance, refraction: nil, tint: options.tint, prominent: false, interactive: true,
            motion: options.motion, action: { morph.open(motion: resolvedMotion) }, label: label
        )
    }
}

extension GlassMenu where Label == SwiftUI.Label<Text, Image?> {
    /// Creates a menu whose trigger shows a title and an optional SF Symbol.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    ) {
        self.init(material: material, appearance: appearance, radius: radius, tint: tint, motion: motion, content: content) {
            SwiftUI.Label { Text(titleKey) } icon: { systemImage.map { Image(systemName: $0) } }
        }
    }

    /// Creates a menu with an icon-only, circular trigger. `label` names it
    /// for VoiceOver and tooltips.
    public init(
        icon systemImage: String, label titleKey: LocalizedStringKey,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    ) {
        self.options = GlassSurfaceOptions(
            material: material, appearance: appearance, radius: radius, refraction: nil,
            tint: tint, interactive: true, motion: motion
        )
        self.iconOnly = true
        self.help = Text(titleKey)
        self.content = content()
        self.label = SwiftUI.Label { Text(titleKey) } icon: { Image(systemName: systemImage) }
    }

    /// Creates a menu with an icon-only, circular trigger named by a string,
    /// for example a title computed at runtime.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        icon systemImage: String, label title: S,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, radius: GlassRadius = 28,
        tint: Color? = nil, motion: GlassMotion? = nil, @ViewBuilder content: () -> Content
    ) {
        self.options = GlassSurfaceOptions(
            material: material, appearance: appearance, radius: radius, refraction: nil,
            tint: tint, interactive: true, motion: motion
        )
        self.iconOnly = true
        self.help = Text(title)
        self.content = content()
        self.label = SwiftUI.Label { Text(title) } icon: { Image(systemName: systemImage) }
    }
}

/// An item of a ``GlassMenu``. Outside a glass panel (for example in the
/// system menu fallback) it is a plain button.
///
/// A disabled item (`.disabled(true)`) dims and is skipped by the arrow keys.
/// Pass `checked` for items that show a choice: they draw a checkmark in a
/// leading column when `true`. Once any item of a panel shows a choice, every
/// item of that panel reserves the column, so icons and titles line up.
public struct GlassMenuItem: View {
    private let title: Text
    private let systemImage: String?
    private let role: ButtonRole?
    private let checked: Bool?
    private let action: () -> Void

    @State private var id = UUID()
    @State private var hovering = false
    @Environment(\.glassMenuPanel) private var panel
    @Environment(\.isEnabled) private var isEnabled

    /// Creates an item.
    /// - Parameters:
    ///   - role: `.destructive` draws the item in red.
    ///   - checked: `true` or `false` for an item that shows a choice, with
    ///     a checkmark when it is `true`; `nil` (the default) for a command.
    ///   - action: Runs when the item is chosen; the menu then closes.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil, role: ButtonRole? = nil,
        checked: Bool? = nil, action: @escaping () -> Void
    ) {
        self.title = Text(titleKey)
        self.systemImage = systemImage
        self.role = role
        self.checked = checked
        self.action = action
    }

    /// Creates an item from a title string.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, systemImage: String? = nil, role: ButtonRole? = nil,
        checked: Bool? = nil, action: @escaping () -> Void
    ) {
        self.title = Text(title)
        self.systemImage = systemImage
        self.role = role
        self.checked = checked
        self.action = action
    }

    public var body: some View {
        if let panel {
            Button(role: role) {
                panel.choose(action)
            } label: {
                HStack(spacing: GlassMenuMetrics.iconSpacing) {
                    if checked != nil || panel.reservesCheckColumn {
                        Image(systemName: "checkmark")
                            .font(GlassMenuMetrics.font.weight(.semibold))
                            .opacity(checked == true ? 1 : 0)
                            .frame(width: GlassMenuMetrics.checkWidth)
                            .accessibilityHidden(true)
                    }
                    if let systemImage {
                        Image(systemName: systemImage).frame(width: GlassMenuMetrics.iconWidth)
                    }
                    title
                    Spacer(minLength: 16)
                }
                .font(GlassMenuMetrics.font)
                .foregroundStyle(role == .destructive ? AnyShapeStyle(Color.red) : AnyShapeStyle(.foreground))
                .opacity(isEnabled ? 1 : 0.4)
                .padding(.horizontal, GlassMenuMetrics.itemPadding)
                .frame(minHeight: GlassMenuMetrics.itemHeight)
                .contentShape(Rectangle())
            }
            .buttonStyle(GlassMenuItemStyle(highlighted: isEnabled && (hovering || panel.highlighted == id), radius: panel.itemRadius))
            .onHover { hovering = $0 }
            .accessibilityAddTraits(checked == true ? .isSelected : [])
            // Disabled items can't be reached with the arrow keys or Return.
            .preference(key: GlassMenuItemKey.self, value: isEnabled ? [GlassMenuItemEntry(id: id, action: action)] : [])
            .preference(key: GlassMenuCheckColumnKey.self, value: checked != nil)
        } else if let checked {
            // The system menu draws a toggle as a checkmark item.
            Toggle(isOn: Binding(get: { checked }, set: { _ in action() })) {
                SwiftUI.Label { title } icon: { systemImage.map { Image(systemName: $0) } }
            }
        } else {
            Button(role: role, action: action) {
                SwiftUI.Label { title } icon: { systemImage.map { Image(systemName: $0) } }
            }
        }
    }
}

/// A separator between groups of menu items.
public struct GlassMenuDivider: View {
    @Environment(\.glassMenuPanel) private var panel

    public init() {}

    public var body: some View {
        if panel != nil {
            Rectangle()
                .fill(.foreground.opacity(0.15))
                .frame(height: 1)
                .padding(.vertical, 4)
                .padding(.horizontal, GlassMenuMetrics.itemPadding)
                .accessibilityHidden(true)
        } else {
            Divider()
        }
    }
}

// MARK: Panel

enum GlassMenuMetrics {
    #if os(macOS)
    static let itemHeight: CGFloat = 28
    static let itemPadding: CGFloat = 8
    static let inset: CGFloat = 4
    static let minWidth: CGFloat = 160
    static let font = Font.system(size: 13)
    static let iconWidth: CGFloat = 16
    static let iconSpacing: CGFloat = 8
    static let checkWidth: CGFloat = 12
    #else
    static let itemHeight: CGFloat = 44
    static let itemPadding: CGFloat = 16
    static let inset: CGFloat = 8
    static let minWidth: CGFloat = 220
    static let font = Font.body
    static let iconWidth: CGFloat = 24
    static let iconSpacing: CGFloat = 12
    static let checkWidth: CGFloat = 16
    #endif
    /// Space between the trigger and the panel.
    static let offset: CGFloat = 8
    /// Space kept between the panel and the scene's edges.
    static let margin: CGFloat = 8
}

/// Opens and closes a menu on the morph spring.
@MainActor
@Observable
final class GlassMenuMorph: GlassAnimating {
    /// 0 is the trigger, 1 the open panel.
    private(set) var progress: Double = 0
    private(set) var isOpen = false

    @ObservationIgnored private var spring = SpringChannel(0, .morph)

    /// Whether the panel is visible, including while it collapses.
    var isPresented: Bool { isOpen || progress > 0.001 }

    func open(motion: GlassMotion) { retarget(1, open: true, motion: motion) }
    func close(motion: GlassMotion) { retarget(0, open: false, motion: motion) }

    private func retarget(_ target: Double, open: Bool, motion: GlassMotion) {
        isOpen = open
        spring.set(target, .morph, motion: motion)
        GlassMotionDriver.shared.run(self)
        publish()
    }

    func advance(by dt: Double) -> Bool {
        spring.advance(by: dt)
        publish()
        return !spring.isSettled
    }

    private func publish() {
        if spring.value != progress { progress = spring.value }
    }
}

/// What items inside an open panel need from it.
struct GlassMenuPanelContext {
    let highlighted: UUID?
    let itemRadius: GlassRadius
    /// Some item of the panel shows a choice, so every item keeps the leading check column (as `UIMenu` and `NSMenu` do).
    var reservesCheckColumn = false
    let choose: (@escaping () -> Void) -> Void
}

private struct GlassMenuPanelKey: EnvironmentKey {
    static let defaultValue: GlassMenuPanelContext? = nil
}

extension EnvironmentValues {
    var glassMenuPanel: GlassMenuPanelContext? {
        get { self[GlassMenuPanelKey.self] }
        set { self[GlassMenuPanelKey.self] = newValue }
    }
}

struct GlassMenuItemEntry: Equatable {
    let id: UUID
    let action: () -> Void

    static func == (lhs: Self, rhs: Self) -> Bool { lhs.id == rhs.id }
}

struct GlassMenuItemKey: PreferenceKey {
    static let defaultValue: [GlassMenuItemEntry] = []

    static func reduce(value: inout [GlassMenuItemEntry], nextValue: () -> [GlassMenuItemEntry]) {
        value.append(contentsOf: nextValue())
    }
}

/// Whether any item of a panel shows a choice (`checked:` set).
struct GlassMenuCheckColumnKey: PreferenceKey {
    static let defaultValue = false

    static func reduce(value: inout Bool, nextValue: () -> Bool) {
        value = value || nextValue()
    }
}

/// The open panel, placed next to its trigger in the scene's coordinates.
struct GlassMenuPanel<Content: View>: View {
    let options: GlassSurfaceOptions
    /// The trigger's frame in the scene.
    let presenter: CGRect
    let presenterRadius: CGFloat
    let scene: CGSize
    let morph: GlassMenuMorph
    let dismiss: () -> Void
    let content: Content

    @State private var items: [GlassMenuItemEntry] = []
    @State private var highlighted: UUID?
    @State private var reservesCheckColumn = false
    @FocusState private var focused: Bool
    @Environment(\.layoutDirection) private var layoutDirection

    private var radius: CGFloat {
        if case .points(let value) = options.radius { return CGFloat(value) }
        return 28
    }

    var body: some View {
        let context = GlassMenuPanelContext(
            highlighted: highlighted,
            itemRadius: .points(max(0, Double(radius - GlassMenuMetrics.inset))),
            reservesCheckColumn: reservesCheckColumn,
            choose: { action in
                action()
                dismiss()
            }
        )
        let placement = GlassMenuPlacement(presenter: presenter, scene: scene, rightToLeft: layoutDirection == .rightToLeft)
        placement {
            VStack(alignment: .leading, spacing: 0) {
                content
            }
            .padding(GlassMenuMetrics.inset)
            .frame(minWidth: GlassMenuMetrics.minWidth, alignment: .leading)
            .fixedSize()
            .environment(\.glassMenuPanel, context)
            // The items draw over the panel's glass, in its foreground.
            .modifier(GlassForegroundStyle(options: options, defaultMaterial: .regular))
            // The items keep the app's layout direction.
            .environment(\.layoutDirection, layoutDirection)
            .onPreferenceChange(GlassMenuItemKey.self) { items = $0 }
            .onPreferenceChange(GlassMenuCheckColumnKey.self) { reservesCheckColumn = $0 }
            .modifier(GlassMenuMorphing(options: options, presenter: presenter, presenterRadius: presenterRadius, radius: radius, morph: morph))
        }
        .frame(width: scene.width, height: scene.height)
        // The trigger's frame and the morph's geometry are left to right in
        // every layout direction, so the placement and the morph are too.
        .environment(\.layoutDirection, .leftToRight)
        .coordinateSpace(.named(GlassMenuPlacement.space))
        .focusable()
        .focused($focused)
        .focusEffectDisabled()
        .onAppear { focused = true }
        .onKeyPress(.escape) {
            dismiss()
            return .handled
        }
        .onKeyPress(.downArrow) { move(by: 1) }
        .onKeyPress(.upArrow) { move(by: -1) }
        .onKeyPress(.return) {
            guard let entry = items.first(where: { $0.id == highlighted }) else { return .ignored }
            context.choose(entry.action)
            return .handled
        }
        .accessibilityAddTraits(.isModal)
        .accessibilityAction(.escape, dismiss)
    }

    private func move(by step: Int) -> KeyPress.Result {
        guard let next = GlassMenuNavigation.item(after: highlighted, by: step, in: items) else { return .ignored }
        highlighted = next
        return .handled
    }
}

/// Arrow-key navigation over an open panel's items. Disabled items publish
/// no entry, so the keys never reach them.
enum GlassMenuNavigation {
    /// The item `step` places from `highlighted`, wrapping around: the first
    /// (down) or last (up) item when nothing is highlighted yet, `nil`
    /// without items.
    static func item(after highlighted: UUID?, by step: Int, in items: [GlassMenuItemEntry]) -> UUID? {
        guard !items.isEmpty else { return nil }
        let index = items.firstIndex { $0.id == highlighted }.map { ($0 + step % items.count + items.count) % items.count }
        return items[index ?? (step > 0 ? 0 : items.count - 1)].id
    }
}

/// Grows the panel's glass out of the trigger's outline and condenses its
/// items out of a blur as the outline opens. The legibility backing fades in
/// with the morph, so the glass leaves the trigger as the trigger's glass and
/// the open panel carries the full ``GlassOptics/menuLegibility``.
private struct GlassMenuMorphing: ViewModifier {
    let options: GlassSurfaceOptions
    let presenter: CGRect
    let presenterRadius: CGFloat
    let radius: CGFloat
    let morph: GlassMenuMorph

    func body(content: Content) -> some View {
        let m = morph.progress
        content
            .opacity(min(1, max(0, m)))
            .blur(radius: max(0, 1 - m) * 6)
            .mask {
                GeometryReader { proxy in
                    let global = proxy.frame(in: .named(GlassMenuPlacement.space))
                    let start = presenter.offsetBy(dx: -global.minX, dy: -global.minY)
                    GlassOutlineShape(rect: Self.mix(start, CGRect(origin: .zero, size: proxy.size), m), radius: currentRadius(m))
                }
            }
            .background {
                GeometryReader { proxy in
                    let global = proxy.frame(in: .named(GlassMenuPlacement.space))
                    let start = presenter.offsetBy(dx: -global.minX, dy: -global.minY)
                    let rect = Self.mix(start, CGRect(origin: .zero, size: proxy.size), m)
                    let union = start.union(CGRect(origin: .zero, size: proxy.size))
                    GlassSurfaceCore(
                        options: GlassSurfaceOptions(
                            material: options.material, appearance: options.appearance, radius: currentRadius(m),
                            refraction: options.refraction, tint: options.tint, interactive: false, motion: options.motion
                        ),
                        defaultMaterial: .regular, outline: rect.offsetBy(dx: -union.minX, dy: -union.minY),
                        restingSize: proxy.size, legibility: GlassOptics.menuLegibility * min(1, max(0, m)),
                        content: Color.clear.frame(width: union.width, height: union.height)
                    )
                    .offset(x: union.minX, y: union.minY)
                }
            }
    }

    /// The corner radius grows from the trigger's to the panel's.
    private func currentRadius(_ m: Double) -> GlassRadius {
        .points(Double(presenterRadius + (radius - presenterRadius) * CGFloat(min(1, max(0, m)))))
    }

    static func mix(_ a: CGRect, _ b: CGRect, _ t: Double) -> CGRect {
        let t = CGFloat(t)
        return CGRect(
            x: a.minX + (b.minX - a.minX) * t, y: a.minY + (b.minY - a.minY) * t,
            width: max(1, a.width + (b.width - a.width) * t), height: max(1, a.height + (b.height - a.height) * t)
        )
    }
}

/// Places the panel below its trigger, trailing edges aligned, flipping
/// above and shifting sideways to stay inside the scene. Laid out left to
/// right; `rightToLeft` puts the trailing edge on the left.
private struct GlassMenuPlacement: Layout {
    let presenter: CGRect
    let scene: CGSize
    let rightToLeft: Bool

    /// The coordinate space the placement and morph measure in.
    static let space = "LiquidGlass.menuPlacement"

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        scene
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        guard let panel = subviews.first else { return }
        let size = panel.sizeThatFits(.unspecified)
        let margin = GlassMenuMetrics.margin, offset = GlassMenuMetrics.offset
        var x = rightToLeft ? presenter.minX : presenter.maxX - size.width
        x = min(max(x, margin), scene.width - margin - size.width)
        let below = presenter.maxY + offset
        let above = presenter.minY - offset - size.height
        let y = below + size.height <= scene.height - margin || above < margin ? below : above
        panel.place(at: CGPoint(x: bounds.minX + x, y: bounds.minY + y), proposal: ProposedViewSize(size))
    }
}

/// An item's highlight: the foreground color at 8% while pressed, hovered
/// or chosen with the keyboard, concentric with the panel.
private struct GlassMenuItemStyle: ButtonStyle {
    let highlighted: Bool
    let radius: GlassRadius

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .background {
                GlassShape(radius: radius)
                    .fill(.foreground.opacity(configuration.isPressed || highlighted ? GlassControlMetrics.highlightOpacity : 0))
            }
    }
}
