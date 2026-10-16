import SwiftUI

/// A slider whose thumb lifts into clear glass while you drag it.
///
/// The track is a 6-point capsule filled with `tint` (the accent color by
/// default) up to the value. Press anywhere on the track to jump there and
/// keep dragging. With a `step`, the value snaps and touch devices play a
/// selection haptic per step. VoiceOver and keyboard users get a standard
/// adjustable slider.
///
/// ```swift
/// GlassSlider(value: $volume, in: 0...100) { Text("Volume") }
/// ```
public struct GlassSlider<Value: BinaryFloatingPoint, Label: View>: View where Value.Stride: BinaryFloatingPoint {
    @Binding private var value: Value
    private let range: ClosedRange<Value>
    private let step: Value.Stride?
    private let tint: Color?
    private let appearance: GlassAppearance?
    private let interactive: Bool
    private let motion: GlassMotion?
    private let label: Label
    private let onEditingChanged: (Bool) -> Void

    @State private var thumb = GlassThumbMotion()
    @State private var editing = false
    @GestureState private var dragging = false
    @Environment(\.glassMotion) private var inheritedMotion
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.layoutDirection) private var layoutDirection
    @Environment(\.isEnabled) private var isEnabled

    /// Creates a slider.
    /// - Parameters:
    ///   - value: The selected value.
    ///   - range: The values the slider spans.
    ///   - step: The distance between valid values, or `nil` for continuous.
    ///   - tint: The fill color; `nil` uses the accent color.
    ///   - appearance: The appearance of the lifted glass; `nil` inherits.
    ///   - interactive: Whether the thumb lifts into glass while held.
    ///   - motion: Overrides the scene's motion level.
    ///   - label: Describes the slider for accessibility.
    ///   - onEditingChanged: Called when a drag begins (`true`) and ends (`false`).
    public init(
        value: Binding<Value>, in range: ClosedRange<Value> = 0...1, step: Value.Stride? = nil,
        tint: Color? = nil, appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil,
        @ViewBuilder label: () -> Label, onEditingChanged: @escaping (Bool) -> Void = { _ in }
    ) {
        self._value = value
        self.range = range
        self.step = step
        self.tint = tint
        self.appearance = appearance
        self.interactive = interactive
        self.motion = motion
        self.label = label()
        self.onEditingChanged = onEditingChanged
    }

    public var body: some View {
        let metrics = GlassThumbMetrics.slider
        let resolvedMotion = (motion ?? inheritedMotion).resolved(reduceMotion: reduceMotion)
        GeometryReader { proxy in
            let size = proxy.size
            let travel = max(1, size.width - metrics.size.width)
            let fraction = self.fraction
            let rtl = layoutDirection == .rightToLeft
            let x = metrics.size.width / 2 + travel * (rtl ? 1 - fraction : fraction)
            let track = SliderTrack(size: size, fill: x, rtl: rtl, tint: tint ?? .accentColor)
            ZStack(alignment: .topLeading) {
                track
                GlassThumbLayer(
                    size: size, center: CGPoint(x: x, y: size.height / 2), metrics: metrics,
                    motion: thumb, appearance: appearance, track: track
                )
            }
            // Positions are computed for the layout direction above, so the
            // internals lay out left to right.
            .environment(\.layoutDirection, .leftToRight)
            .contentShape(Rectangle())
            .gesture(
                DragGesture(minimumDistance: 0)
                    .updating($dragging) { _, state, _ in state = true }
                    .onChanged { drag in
                        if !editing {
                            editing = true
                            onEditingChanged(true)
                            thumb.setPressed(interactive, motion: resolvedMotion)
                        }
                        let raw = (drag.location.x - metrics.size.width / 2) / travel
                        set(fraction: Double(rtl ? 1 - raw : raw))
                    }
                    .onEnded { _ in endEditing(motion: resolvedMotion) }
            )
            .onChange(of: x, initial: true) { _, newX in thumb.track(x: newX) }
        }
        .frame(height: Self.controlHeight)
        .frame(minWidth: 80)
        .onChange(of: dragging) { _, isDragging in
            if !isDragging { endEditing(motion: resolvedMotion) }
        }
        .sensoryFeedback(.selection, trigger: stepIndex)
        .accessibilityRepresentation {
            if let step {
                Slider(value: $value, in: range, step: step) { label }
            } else {
                Slider(value: $value, in: range) { label }
            }
        }
        #if os(macOS)
        .focusable(isEnabled)
        .onKeyPress(.leftArrow) { nudge(-1) }
        .onKeyPress(.rightArrow) { nudge(1) }
        #endif
    }

    #if os(macOS)
    static var controlHeight: CGFloat { 28 }
    #else
    static var controlHeight: CGFloat { 44 }
    #endif

    private var fraction: Double {
        let span = Double(range.upperBound - range.lowerBound)
        guard span > 0 else { return 0 }
        return min(1, max(0, Double(value - range.lowerBound) / span))
    }

    /// The index of the current step, which changes once per step for haptics.
    private var stepIndex: Int {
        guard let step, step > 0 else { return 0 }
        return Int((Double(value - range.lowerBound) / Double(step)).rounded())
    }

    private func set(fraction: Double) {
        let clamped = min(1, max(0, fraction))
        var newValue = range.lowerBound + Value(clamped) * (range.upperBound - range.lowerBound)
        if let step, step > 0 {
            let steps = (Double(newValue - range.lowerBound) / Double(step)).rounded()
            newValue = min(range.upperBound, range.lowerBound + Value(steps * Double(step)))
        }
        if newValue != value { value = newValue }
    }

    private func endEditing(motion: GlassMotion) {
        guard editing else { return }
        editing = false
        thumb.setPressed(false, motion: motion)
        onEditingChanged(false)
    }

    #if os(macOS)
    private func nudge(_ direction: Double) -> KeyPress.Result {
        let span = Double(range.upperBound - range.lowerBound)
        let increment = step.map { Double($0) } ?? span / 100
        set(fraction: fraction + direction * increment / max(span, .ulpOfOne))
        return .handled
    }
    #endif
}

extension GlassSlider where Label == EmptyView {
    /// Creates a slider without a label. Give it an `accessibilityLabel`.
    public init(
        value: Binding<Value>, in range: ClosedRange<Value> = 0...1, step: Value.Stride? = nil,
        tint: Color? = nil, appearance: GlassAppearance? = nil, interactive: Bool = true, motion: GlassMotion? = nil,
        onEditingChanged: @escaping (Bool) -> Void = { _ in }
    ) {
        self.init(
            value: value, in: range, step: step, tint: tint, appearance: appearance, interactive: interactive,
            motion: motion, label: { EmptyView() }, onEditingChanged: onEditingChanged
        )
    }
}

/// The slider's track and fill.
private struct SliderTrack: View {
    let size: CGSize
    /// Where the fill ends, in local points.
    let fill: CGFloat
    let rtl: Bool
    let tint: Color

    var body: some View {
        let height: CGFloat = 6
        ZStack(alignment: .leading) {
            Capsule().fill(Color(white: 0.5, opacity: 0.3))
            Capsule().fill(tint)
                .frame(width: max(height, rtl ? size.width - fill : fill))
                .frame(maxWidth: .infinity, alignment: rtl ? .trailing : .leading)
        }
        .frame(width: size.width, height: height)
        .frame(width: size.width, height: size.height)
    }
}
