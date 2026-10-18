import SwiftUI

/// A switch whose thumb lifts into clear glass while it is held.
///
/// Tap to toggle, or drag the thumb across. The thumb travels on the
/// measured switch spring (response 0.47 s, damping 0.7) and the track takes
/// `tint` (the accent color by default) when on. It is a `Toggle` underneath,
/// so VoiceOver and keyboard focus behave as usual. On iOS 18 and macOS 15,
/// `labelsHidden()` hides the label (it stays the VoiceOver label) and the
/// switch hugs its track; earlier systems don't report hidden labels to
/// styles, so there the label still shows.
///
/// ```swift
/// GlassSwitch("Wi-Fi", isOn: $wifi)
/// ```
public struct GlassSwitch<Label: View>: View {
    @Binding private var isOn: Bool
    private let tint: Color?
    private let appearance: GlassAppearance?
    private let interactive: Bool
    private let motion: GlassMotion?
    private let label: Label

    /// Creates a switch with a custom label.
    /// - Parameters:
    ///   - isOn: Whether the switch is on.
    ///   - tint: The track color when on; `nil` uses the accent color.
    ///   - appearance: The appearance of the lifted glass; `nil` inherits.
    ///   - interactive: Whether the thumb lifts into glass while held.
    ///   - motion: Overrides the scene's motion level.
    public init(
        isOn: Binding<Bool>, tint: Color? = nil, appearance: GlassAppearance? = nil,
        interactive: Bool = true, motion: GlassMotion? = nil, @ViewBuilder label: () -> Label
    ) {
        self._isOn = isOn
        self.tint = tint
        self.appearance = appearance
        self.interactive = interactive
        self.motion = motion
        self.label = label()
    }

    public var body: some View {
        Toggle(isOn: $isOn) { label }
            .toggleStyle(GlassSwitchStyle(tint: tint, appearance: appearance, interactive: interactive, motion: motion))
    }
}

extension GlassSwitch where Label == Text {
    /// Creates a switch with a title.
    public init(
        _ titleKey: LocalizedStringKey, isOn: Binding<Bool>, tint: Color? = nil,
        appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil
    ) {
        self.init(isOn: isOn, tint: tint, appearance: appearance, interactive: interactive, motion: motion) {
            Text(titleKey)
        }
    }

    /// Creates a switch with a title string.
    @_disfavoredOverload
    public init<S: StringProtocol>(
        _ title: S, isOn: Binding<Bool>, tint: Color? = nil,
        appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil
    ) {
        self.init(isOn: isOn, tint: tint, appearance: appearance, interactive: interactive, motion: motion) {
            Text(title)
        }
    }
}

/// The glass switch as a toggle style, for use on any `Toggle`:
/// `Toggle("Wi-Fi", isOn: $wifi).toggleStyle(GlassSwitchStyle())`.
public struct GlassSwitchStyle: ToggleStyle {
    private let tint: Color?
    private let appearance: GlassAppearance?
    private let interactive: Bool
    private let motion: GlassMotion?

    /// Creates the style.
    /// - Parameters:
    ///   - tint: The track color when on; `nil` uses the accent color.
    ///   - appearance: The appearance of the lifted glass; `nil` inherits.
    ///   - interactive: Whether the thumb lifts into glass while held.
    ///   - motion: Overrides the scene's motion level.
    public init(tint: Color? = nil, appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil) {
        self.tint = tint
        self.appearance = appearance
        self.interactive = interactive
        self.motion = motion
    }

    public func makeBody(configuration: Configuration) -> some View {
        let control = GlassSwitchControl(
            isOn: configuration.$isOn, tint: tint, appearance: appearance,
            interactive: interactive, motion: motion
        )
        if #available(iOS 18.0, macOS 15.0, *) {
            GlassSwitchRow(label: configuration.label, control: control)
        } else {
            HStack {
                configuration.label
                Spacer(minLength: 8)
                control
            }
        }
    }
}

/// The label, then the switch at the trailing edge; only the switch, named
/// by the label for VoiceOver, when labels are hidden.
@available(iOS 18.0, macOS 15.0, *)
private struct GlassSwitchRow<Label: View, Control: View>: View {
    let label: Label
    let control: Control

    @Environment(\.labelsVisibility) private var labelsVisibility

    var body: some View {
        if labelsVisibility == .hidden {
            control.accessibilityLabel { _ in label }
        } else {
            HStack {
                label
                Spacer(minLength: 8)
                control
            }
        }
    }
}

/// The track and thumb of a ``GlassSwitch``.
private struct GlassSwitchControl: View {
    @Binding var isOn: Bool
    let tint: Color?
    let appearance: GlassAppearance?
    let interactive: Bool
    let motion: GlassMotion?

    @State private var thumb = GlassThumbMotion()
    @State private var drag: DragState?
    @State private var touching = false
    @GestureState private var tracking = false
    @Environment(\.glassMotion) private var inheritedMotion
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.layoutDirection) private var layoutDirection
    @Environment(\.isEnabled) private var isEnabled

    private struct DragState {
        var start: Double
        var current: Double
    }

    #if os(macOS)
    static let trackSize = CGSize(width: 54, height: 24)
    #else
    static let trackSize = CGSize(width: 63, height: 28)
    #endif

    var body: some View {
        let metrics = GlassThumbMetrics.switch
        let size = Self.trackSize
        let resolvedMotion = (motion ?? inheritedMotion).resolved(reduceMotion: reduceMotion)
        let inset = (size.height - metrics.size.height) / 2
        let travel = size.width - metrics.size.width - 2 * inset
        let rtl = layoutDirection == .rightToLeft
        let position = drag?.current ?? thumb.position
        let x = inset + metrics.size.width / 2 + travel * (rtl ? 1 - position : position)
        let presentedOn = drag.map { $0.current >= 0.5 } ?? isOn
        let track = Capsule()
            .fill(presentedOn ? AnyShapeStyle(tint ?? .accentColor) : AnyShapeStyle(Color(white: 0.5, opacity: 0.4)))
            .animation(resolvedMotion == .none ? nil : .easeInOut(duration: 0.2), value: presentedOn)
            .frame(width: size.width, height: size.height)
        ZStack(alignment: .topLeading) {
            track
            GlassThumbLayer(
                size: size, center: CGPoint(x: x, y: size.height / 2), metrics: metrics,
                motion: thumb, appearance: appearance, track: track
            )
        }
        .frame(width: size.width, height: size.height)
        // Disabled, the track and thumb dim, as a disabled GlassButton does.
        .opacity(isEnabled ? 1 : 0.45)
        .environment(\.layoutDirection, .leftToRight)
        .contentShape(Capsule())
        // A tap toggles; it stays reliable inside scroll views, which may
        // cancel the drag gestures below.
        .onTapGesture { isOn.toggle() }
        // While held, the thumb lifts into glass.
        .modifier(GlassTouchInput(
            space: LocalCoordinateSpace.local, tracking: $tracking,
            changed: { _ in if !touching { touching = true } },
            changedGlobally: { _ in },
            ended: { _ in touching = false }
        ))
        // Dragging carries the thumb; releasing commits the side it is on.
        .simultaneousGesture(
            DragGesture(minimumDistance: 3)
                .onChanged { value in
                    if drag == nil { drag = DragState(start: isOn ? 1 : 0, current: isOn ? 1 : 0) }
                    let delta = Double(value.translation.width) * (rtl ? -1 : 1)
                    drag?.current = min(1, max(0, (drag?.start ?? 0) + delta / Double(travel)))
                }
                .onEnded { _ in commitDrag(motion: resolvedMotion) }
        )
        .onChange(of: touching) { _, isPressed in
            thumb.setPressed(isPressed && interactive, motion: resolvedMotion)
            // A drag the system cancelled (a scroll view took over) returns
            // the thumb; `onEnded` has already run for a completed drag.
            if !isPressed { Task { @MainActor in cancelDrag(motion: resolvedMotion) } }
        }
        .onChange(of: isOn, initial: true) { _, on in
            thumb.setPosition(on ? 1 : 0, animated: true, motion: resolvedMotion)
        }
        .onChange(of: x, initial: true) { _, newX in thumb.track(x: newX) }
        .sensoryFeedback(.impact(weight: .light), trigger: isOn)
        #if os(macOS)
        .focusable()
        .onKeyPress(.space) {
            isOn.toggle()
            return .handled
        }
        #endif
    }

    /// Commits a drag: the switch takes the side the thumb was released on,
    /// continuing from where the finger left the thumb.
    private func commitDrag(motion: GlassMotion) {
        guard let state = drag else { return }
        drag = nil
        thumb.setPosition(state.current, animated: false, motion: motion)
        let target = state.current >= 0.5
        if target != isOn {
            isOn = target
        } else {
            thumb.setPosition(isOn ? 1 : 0, animated: true, motion: motion)
        }
    }

    private func cancelDrag(motion: GlassMotion) {
        guard drag != nil else { return }
        drag = nil
        thumb.setPosition(isOn ? 1 : 0, animated: true, motion: motion)
    }
}
