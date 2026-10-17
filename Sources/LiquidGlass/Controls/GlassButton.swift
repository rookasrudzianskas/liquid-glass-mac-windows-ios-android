import SwiftUI

/// A push button on interactive glass.
///
/// Size follows SwiftUI's `controlSize` (`.small`, `.regular`, `.large`):
/// 34 / 44 / 52 points tall on iOS and 22 / 28 / 34 on macOS, scaling with
/// Dynamic Type. The outline is a capsule; icon-only buttons are circles.
///
/// ```swift
/// GlassButton("Play", systemImage: "play.fill") { player.play() }
/// GlassButton(icon: "plus", label: "Add") { add() }
/// GlassButton("Buy", prominent: true) { buy() }
///     .controlSize(.large)
/// GlassButton("Delete", systemImage: "trash", role: .destructive) { delete() }
/// ```
///
/// A prominent button tints its glass with `tint` (the accent color by
/// default). On tinted glass the label is white or `#111113`, whichever
/// contrasts more with the tinted glass: white on blue or red, dark on
/// yellow or mint. A destructive button draws its label in red unless it is
/// tinted. Inside a ``GlassToolbar`` buttons drop their own glass and
/// share the toolbar's.
public struct GlassButton<Label: View>: View {
    private let action: () -> Void
    private let label: Label
    private let role: ButtonRole?
    private let iconOnly: Bool
    /// The tooltip of an icon-only button: its otherwise invisible title.
    private let help: Text?
    private let radius: GlassRadius?
    private let material: GlassMaterial?
    private let appearance: GlassAppearance?
    private let refraction: Double?
    private let tint: Color?
    private let prominent: Bool
    private let interactive: Bool
    private let motion: GlassMotion?

    @State private var interaction = GlassInteraction()
    @GestureState private var tracking = false
    @Environment(\.glassToolbarItem) private var toolbarItem
    @Environment(\.isEnabled) private var isEnabled

    /// Creates a button with a custom label.
    /// - Parameters:
    ///   - role: The button's role, as for `Button`; `.destructive` draws an
    ///     untinted label in red.
    ///   - radius: The outline; `nil` is a capsule.
    ///   - material: Clear or Regular glass; `nil` inherits the scene's.
    ///   - appearance: Light or dark glass; `nil` inherits.
    ///   - refraction: Optical travel at the rim in points; `nil` inherits.
    ///   - tint: Colors the glass. The label turns white or dark, whichever
    ///     contrasts more with the tinted glass.
    ///   - prominent: Tints the glass with the accent color when `tint` is `nil`.
    ///   - interactive: Press grow, drag stretch and glow. Default `true`.
    ///   - motion: Overrides the scene's motion level.
    public init(
        role: ButtonRole? = nil, radius: GlassRadius? = nil, material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil, refraction: Double? = nil, tint: Color? = nil, prominent: Bool = false,
        interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void, @ViewBuilder label: () -> Label
    ) {
        self.init(
            iconOnly: false, help: nil, role: role, radius: radius, material: material, appearance: appearance,
            refraction: refraction, tint: tint, prominent: prominent, interactive: interactive, motion: motion,
            action: action, label: label()
        )
    }

    /// The designated initializer: `iconOnly` makes a circle of the button's
    /// height around a label shown icon-only.
    init(
        iconOnly: Bool, help: Text?, role: ButtonRole?, radius: GlassRadius?, material: GlassMaterial?,
        appearance: GlassAppearance?, refraction: Double?, tint: Color?, prominent: Bool,
        interactive: Bool, motion: GlassMotion?, action: @escaping () -> Void, label: Label
    ) {
        self.action = action
        self.label = label
        self.role = role
        self.iconOnly = iconOnly
        self.help = help
        self.radius = radius
        self.material = material
        self.appearance = appearance
        self.refraction = refraction
        self.tint = tint
        self.prominent = prominent
        self.interactive = interactive
        self.motion = motion
    }

    public var body: some View {
        let resolvedTint = tint ?? (prominent ? Color.accentColor : nil)
        let style = GlassButtonStyle(
            interaction: interaction, iconOnly: iconOnly,
            options: GlassSurfaceOptions(
                material: material, appearance: appearance, radius: radius ?? (iconOnly ? .circle : .capsule),
                refraction: refraction, tint: resolvedTint, interactive: interactive, motion: motion
            ),
            toolbarItem: toolbarItem
        )
        Button(role: role, action: action) { label }
            .buttonStyle(style)
            .modifier(OptionalHelp(text: help))
            .modifier(ToolbarLargeContent(enabled: toolbarItem) { label })
            .opacity(isEnabled ? 1 : 0.45)
            .modifier(GlassPressGestures(model: toolbarItem || !isEnabled || !interactive ? nil : interaction, tracking: $tracking))
    }
}

extension GlassButton where Label == SwiftUI.Label<Text, Image?> {
    /// Creates a button with a title and an optional SF Symbol.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    ) {
        self.init(
            iconOnly: false, help: nil, role: role, radius: nil, material: material, appearance: appearance,
            refraction: refraction, tint: tint, prominent: prominent, interactive: interactive, motion: motion,
            action: action, label: SwiftUI.Label { Text(titleKey) } icon: { systemImage.map { Image(systemName: $0) } }
        )
    }

    /// Creates a button with a title string and an optional SF Symbol.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, systemImage: String? = nil, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    ) {
        self.init(
            iconOnly: false, help: nil, role: role, radius: nil, material: material, appearance: appearance,
            refraction: refraction, tint: tint, prominent: prominent, interactive: interactive, motion: motion,
            action: action, label: SwiftUI.Label { Text(title) } icon: { systemImage.map { Image(systemName: $0) } }
        )
    }

    /// Creates an icon-only circular button. `label` names it for VoiceOver
    /// and tooltips.
    public init(
        icon systemImage: String, label titleKey: LocalizedStringKey, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    ) {
        self.init(
            iconOnly: true, help: Text(titleKey), role: role, radius: .circle, material: material, appearance: appearance,
            refraction: refraction, tint: tint, prominent: prominent, interactive: interactive, motion: motion,
            action: action, label: SwiftUI.Label { Text(titleKey) } icon: { Image(systemName: systemImage) }
        )
    }

    /// Creates an icon-only circular button named by a string, for example
    /// a title computed at runtime.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        icon systemImage: String, label title: S, role: ButtonRole? = nil,
        material: GlassMaterial? = nil, appearance: GlassAppearance? = nil, refraction: Double? = nil,
        tint: Color? = nil, prominent: Bool = false, interactive: Bool = true, motion: GlassMotion? = nil,
        action: @escaping () -> Void
    ) {
        self.init(
            iconOnly: true, help: Text(title), role: role, radius: .circle, material: material, appearance: appearance,
            refraction: refraction, tint: tint, prominent: prominent, interactive: interactive, motion: motion,
            action: action, label: SwiftUI.Label { Text(title) } icon: { Image(systemName: systemImage) }
        )
    }
}

/// Toolbar items stop growing with the bar at the largest text sizes, so
/// they show in the Large Content Viewer, like the system's bar items.
private struct ToolbarLargeContent<Large: View>: ViewModifier {
    let enabled: Bool
    @ViewBuilder let large: () -> Large

    func body(content: Content) -> some View {
        if enabled {
            content.accessibilityShowsLargeContentViewer { large() }
        } else {
            content
        }
    }
}

/// Platform metrics from `spec/COMPONENTS.md` (Sizing per platform).
enum GlassControlMetrics {
    #if os(macOS)
    static func buttonHeight(_ size: ControlSize) -> CGFloat {
        switch size {
        case .mini, .small: 22
        case .large, .extraLarge: 34
        default: 28
        }
    }
    static func horizontalPadding(_ size: ControlSize) -> CGFloat {
        switch size {
        case .mini, .small: 8
        case .large, .extraLarge: 12
        default: 10
        }
    }
    static func font(_ size: ControlSize) -> Font {
        switch size {
        case .mini, .small: .system(size: 11, weight: .medium)
        case .large, .extraLarge: .system(size: 15, weight: .medium)
        default: .system(size: 13, weight: .medium)
        }
    }
    static let labelSpacing: CGFloat = 4
    /// A pointer drags controls at once: nothing scrolls by dragging.
    static let dragCommitDistance: CGFloat = 3
    static let toolbarHeight: CGFloat = 34
    static let toolbarInset: CGFloat = 3
    static func tabBarHeight(_ size: ControlSize) -> CGFloat {
        size == .small || size == .mini ? 24 : 28
    }
    #else
    static func buttonHeight(_ size: ControlSize) -> CGFloat {
        switch size {
        case .mini, .small: 34
        case .large, .extraLarge: 52
        default: 44
        }
    }
    static func horizontalPadding(_ size: ControlSize) -> CGFloat {
        switch size {
        case .mini, .small: 12
        case .large, .extraLarge: 22
        default: 16
        }
    }
    static func font(_ size: ControlSize) -> Font {
        switch size {
        case .mini, .small: .subheadline.weight(.semibold)
        default: .body.weight(.semibold)
        }
    }
    static let labelSpacing: CGFloat = 6
    /// How far a finger moves sideways before a slider or tab lens follows
    /// it, so a vertical swipe still scrolls.
    static let dragCommitDistance: CGFloat = 8
    static let toolbarHeight: CGFloat = 52
    static let toolbarInset: CGFloat = 4
    static func tabBarHeight(_ size: ControlSize) -> CGFloat {
        size == .small || size == .mini ? 40 : 62
    }
    #endif

    /// The highlight of a pressed, hovered or focused item on glass: the
    /// foreground color at 8%.
    static let highlightOpacity = 0.08
}

/// Renders a ``GlassButton``: the label on its own interactive glass, or as a
/// plain item when it sits in a toolbar.
struct GlassButtonStyle: ButtonStyle {
    let interaction: GlassInteraction
    let iconOnly: Bool
    let options: GlassSurfaceOptions
    let toolbarItem: Bool

    func makeBody(configuration: Configuration) -> some View {
        GlassButtonBody(configuration: configuration, style: self)
    }
}

private struct GlassButtonBody: View {
    let configuration: ButtonStyleConfiguration
    let style: GlassButtonStyle

    @Environment(\.controlSize) private var controlSize
    @Environment(\.isFocused) private var isFocused
    @ScaledMetric(relativeTo: .body) private var typeScale: CGFloat = 1
    @State private var hovering = false

    var body: some View {
        let height = (GlassControlMetrics.buttonHeight(controlSize) * typeScale).rounded()
        let radius = style.options.radius
        let label = configuration.label
            .labelStyle(GlassButtonLabelStyle(iconOnly: style.iconOnly))
            .font(GlassControlMetrics.font(controlSize))
            .lineLimit(1)
            .padding(.horizontal, style.iconOnly ? 0 : GlassControlMetrics.horizontalPadding(controlSize))
            .frame(minWidth: height, minHeight: height)
            .frame(width: style.iconOnly ? height : nil)
            .modifier(DestructiveForeground(enabled: configuration.role == .destructive && style.options.tint == nil))
        Group {
            if style.toolbarItem {
                label
                    .background {
                        GlassShape(radius: radius)
                            .fill(.foreground.opacity(configuration.isPressed || hovering || isFocused ? GlassControlMetrics.highlightOpacity : 0))
                    }
                    .contentShape(GlassShape(radius: radius))
                    .onHover { hovering = $0 }
            } else {
                GlassSurfaceCore(
                    options: style.options,
                    interaction: style.interaction,
                    handlesInput: false,
                    tintsContent: true,
                    content: label
                )
                .overlay {
                    if isFocused {
                        GlassShape(radius: radius)
                            .stroke(Color.accentColor.opacity(0.6), lineWidth: 3)
                            .padding(-3)
                    }
                }
            }
        }
        .focusEffectDisabled()
        .onChange(of: configuration.isPressed) { _, pressed in
            if !style.toolbarItem, style.options.interactive { style.interaction.keyboardPress(pressed) }
        }
    }
}

/// Draws a destructive button's label in red.
private struct DestructiveForeground: ViewModifier {
    let enabled: Bool

    func body(content: Content) -> some View {
        if enabled { content.foregroundStyle(.red) } else { content }
    }
}

/// Adds a tooltip when there is one.
struct OptionalHelp: ViewModifier {
    let text: Text?

    func body(content: Content) -> some View {
        if let text { content.help(text) } else { content }
    }
}

/// Lays out a button label: icon and title side by side, or the icon alone.
private struct GlassButtonLabelStyle: LabelStyle {
    let iconOnly: Bool

    func makeBody(configuration: Configuration) -> some View {
        if iconOnly {
            // The system style keeps the title as the accessibility label.
            SwiftUI.Label(configuration).labelStyle(.iconOnly)
        } else {
            HStack(spacing: GlassControlMetrics.labelSpacing) {
                configuration.icon
                configuration.title
            }
        }
    }
}
