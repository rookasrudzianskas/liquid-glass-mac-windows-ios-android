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
/// ```
///
/// A prominent button tints its glass with `tint` (the accent color by
/// default) and draws its label in white. Inside a ``GlassToolbar`` buttons
/// drop their own glass and share the toolbar's.
public struct GlassButton<Label: View>: View {
    private let action: () -> Void
    private let label: Label
    private let iconOnly: Bool
    private let radius: GlassRadius?
    private let material: GlassMaterial?
    private let tint: Color?
    private let prominent: Bool

    @State private var interaction = GlassInteraction()
    @GestureState private var tracking = false
    @Environment(\.glassToolbarItem) private var toolbarItem
    @Environment(\.isEnabled) private var isEnabled

    /// Creates a button with a custom label.
    /// - Parameters:
    ///   - radius: The outline; `nil` is a capsule.
    ///   - material: Clear or Regular glass; `nil` inherits the scene's.
    ///   - tint: Colors the glass. Tinted buttons draw their label in white.
    ///   - prominent: Tints the glass with the accent color when `tint` is `nil`.
    public init(
        radius: GlassRadius? = nil, material: GlassMaterial? = nil, tint: Color? = nil, prominent: Bool = false,
        action: @escaping () -> Void, @ViewBuilder label: () -> Label
    ) {
        self.action = action
        self.label = label()
        self.iconOnly = false
        self.radius = radius
        self.material = material
        self.tint = tint
        self.prominent = prominent
    }

    public var body: some View {
        let resolvedTint = tint ?? (prominent ? Color.accentColor : nil)
        let style = GlassButtonStyle(
            interaction: interaction, iconOnly: iconOnly, radius: radius ?? (iconOnly ? .circle : .capsule),
            material: material, tint: resolvedTint, toolbarItem: toolbarItem
        )
        Button(action: action) { label }
            .buttonStyle(style)
            .opacity(isEnabled ? 1 : 0.45)
            .modifier(GlassPressGestures(model: toolbarItem || !isEnabled ? nil : interaction, tracking: $tracking))
    }
}

extension GlassButton where Label == SwiftUI.Label<Text, Image?> {
    /// Creates a button with a title and an optional SF Symbol.
    public init(
        _ titleKey: LocalizedStringKey, systemImage: String? = nil,
        material: GlassMaterial? = nil, tint: Color? = nil, prominent: Bool = false,
        action: @escaping () -> Void
    ) {
        self.init(material: material, tint: tint, prominent: prominent, action: action) {
            SwiftUI.Label { Text(titleKey) } icon: { systemImage.map { Image(systemName: $0) } }
        }
    }

    /// Creates a button with a title string and an optional SF Symbol.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, systemImage: String? = nil,
        material: GlassMaterial? = nil, tint: Color? = nil, prominent: Bool = false,
        action: @escaping () -> Void
    ) {
        self.init(material: material, tint: tint, prominent: prominent, action: action) {
            SwiftUI.Label { Text(title) } icon: { systemImage.map { Image(systemName: $0) } }
        }
    }

    /// Creates an icon-only circular button. `label` names it for VoiceOver
    /// and tooltips.
    public init(
        icon systemImage: String, label titleKey: LocalizedStringKey,
        material: GlassMaterial? = nil, tint: Color? = nil, prominent: Bool = false,
        action: @escaping () -> Void
    ) {
        self.action = action
        self.label = SwiftUI.Label { Text(titleKey) } icon: { Image(systemName: systemImage) }
        self.iconOnly = true
        self.radius = .circle
        self.material = material
        self.tint = tint
        self.prominent = prominent
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
    let radius: GlassRadius
    let material: GlassMaterial?
    let tint: Color?
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
        let label = configuration.label
            .labelStyle(GlassButtonLabelStyle(iconOnly: style.iconOnly))
            .font(GlassControlMetrics.font(controlSize))
            .lineLimit(1)
            .padding(.horizontal, style.iconOnly ? 0 : GlassControlMetrics.horizontalPadding(controlSize))
            .frame(minWidth: height, minHeight: height)
            .frame(width: style.iconOnly ? height : nil)
        Group {
            if style.toolbarItem {
                label
                    .background {
                        GlassShape(radius: style.radius)
                            .fill(.foreground.opacity(configuration.isPressed || hovering || isFocused ? GlassControlMetrics.highlightOpacity : 0))
                    }
                    .contentShape(GlassShape(radius: style.radius))
                    .onHover { hovering = $0 }
            } else {
                GlassSurfaceCore(
                    options: GlassSurfaceOptions(
                        material: style.material, appearance: nil, radius: style.radius, refraction: nil,
                        tint: style.tint, interactive: true, motion: nil
                    ),
                    interaction: style.interaction,
                    handlesInput: false,
                    content: label.modifier(OnTintForeground(tinted: style.tint != nil))
                )
                .overlay {
                    if isFocused {
                        GlassShape(radius: style.radius)
                            .stroke(Color.accentColor.opacity(0.6), lineWidth: 3)
                            .padding(-3)
                    }
                }
            }
        }
        .focusEffectDisabled()
        .onChange(of: configuration.isPressed) { _, pressed in
            if !style.toolbarItem { style.interaction.keyboardPress(pressed) }
        }
    }
}

/// Draws content in white on tinted glass; otherwise keeps the glass's own
/// foreground color.
struct OnTintForeground: ViewModifier {
    let tinted: Bool

    func body(content: Content) -> some View {
        if tinted {
            content.foregroundStyle(.white)
        } else {
            content
        }
    }
}

/// Lays out a button label: icon and title side by side, or the icon alone.
private struct GlassButtonLabelStyle: LabelStyle {
    let iconOnly: Bool

    func makeBody(configuration: Configuration) -> some View {
        if iconOnly {
            configuration.icon
        } else {
            HStack(spacing: GlassControlMetrics.labelSpacing) {
                configuration.icon
                configuration.title
            }
        }
    }
}
