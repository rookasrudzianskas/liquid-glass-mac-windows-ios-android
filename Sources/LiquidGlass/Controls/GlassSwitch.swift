import SwiftUI

/// A switch whose thumb lifts into clear glass while it is held.
///
/// Tap to toggle, or drag the thumb across. The thumb travels on the
/// measured switch spring (response 0.47 s, damping 0.7) and the track takes
/// `tint` (the accent color by default) when on. It is a `Toggle` underneath,
/// so VoiceOver, keyboard focus and `labelsHidden()` behave as usual.
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
        HStack {
            configuration.label
            Spacer(minLength: 8)
            GlassSwitchControl(
                isOn: configuration.$isOn, tint: tint, appearance: appearance,
                interactive: interactive, motion: motion
            )
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
    @GestureState private var tracking = false
    @Environment(\.glassMotion) private var inheritedMotion
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.layoutDirection) private var layoutDirection

    private struct DragState {
        var start: Double
        var current: Double
        var moved = false
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
        .environment(\.layoutDirection, .leftToRight)
        .contentShape(Capsule())
        .gesture(
            DragGesture(minimumDistance: 0)
                .updating($tracking) { _, state, _ in state = true }
                .onChanged { value in
                    if drag == nil {
                        drag = DragState(start: isOn ? 1 : 0, current: isOn ? 1 : 0)
                        thumb.setPressed(interactive, motion: resolvedMotion)
                    }
                    let delta = Double(value.translation.width) * (rtl ? -1 : 1)
                    drag?.moved = drag!.moved || abs(delta) > 3
                    if drag!.moved {
                        drag?.current = min(1, max(0, drag!.start + delta / Double(travel)))
                    }
                }
                .onEnded { _ in finish(motion: resolvedMotion, commit: true) }
        )
        .onChange(of: tracking) { _, isTracking in
            if !isTracking { finish(motion: resolvedMotion, commit: false) }
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

    /// Ends a press: a tap toggles, a drag commits the side the thumb is on.
    private func finish(motion: GlassMotion, commit: Bool) {
        guard let state = drag else { return }
        drag = nil
        thumb.setPressed(false, motion: motion)
        if !commit {
            thumb.setPosition(isOn ? 1 : 0, animated: true, motion: motion)
            return
        }
        let target = state.moved ? state.current >= 0.5 : !isOn
        // Continue from where the finger left the thumb.
        thumb.setPosition(state.current, animated: false, motion: motion)
        if target != isOn {
            isOn = target
        } else {
            thumb.setPosition(isOn ? 1 : 0, animated: true, motion: motion)
        }
    }
}
