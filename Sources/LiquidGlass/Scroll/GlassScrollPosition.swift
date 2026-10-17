import Observation
import SwiftUI

/// The scroll position of a ``GlassScrollView``, shared by the visible
/// backdrop and every copy of it inside glass.
///
/// Create one with `@State` next to the scene and pass it to the scroll
/// view. Because the copies of a scene's backdrop render the content again,
/// the position lives outside the view, where they all read it:
///
/// ```swift
/// @State private var scroll = GlassScrollPosition()
///
/// GlassScene {
///     GlassContent {
///         GlassScrollView(position: scroll) { ArticleList() }
///     }
///     …
/// }
/// ```
///
/// Read ``offset`` to drive effects; call ``scrollTo(_:animated:)`` or
/// ``scrollTo(edge:animated:)`` to move it.
@MainActor
@Observable
public final class GlassScrollPosition: GlassAnimating {
    /// The content offset in points: how far the content has scrolled from
    /// its top-leading corner. Outside `0...maximum` while rubber-banding.
    public private(set) var offset = CGPoint.zero

    /// The size of the scrolled content.
    public private(set) var contentSize = CGSize.zero
    /// The size of the visible region.
    public private(set) var viewportSize = CGSize.zero
    /// Whether the content is moving: dragged, coasting or settling.
    public private(set) var isScrolling = false

    @ObservationIgnored private var x = ScrollAxis()
    @ObservationIgnored private var y = ScrollAxis()
    @ObservationIgnored var axes: Axis.Set = .vertical

    public init() {}

    /// The largest offset on each axis.
    public var maximumOffset: CGPoint {
        CGPoint(x: max(0, contentSize.width - viewportSize.width), y: max(0, contentSize.height - viewportSize.height))
    }

    /// Scrolls so the content's `point` sits at the viewport's top-leading corner.
    public func scrollTo(_ point: CGPoint, animated: Bool = true) {
        let maximum = maximumOffset
        x.settle(at: min(max(Double(point.x), 0), Double(maximum.x)), animated: animated)
        y.settle(at: min(max(Double(point.y), 0), Double(maximum.y)), animated: animated)
        run()
    }

    /// Scrolls to an edge of the content.
    public func scrollTo(edge: Edge, animated: Bool = true) {
        let maximum = maximumOffset
        switch edge {
        case .top: scrollTo(CGPoint(x: offset.x, y: 0), animated: animated)
        case .bottom: scrollTo(CGPoint(x: offset.x, y: maximum.y), animated: animated)
        case .leading: scrollTo(CGPoint(x: 0, y: offset.y), animated: animated)
        case .trailing: scrollTo(CGPoint(x: maximum.x, y: offset.y), animated: animated)
        }
    }

    // MARK: Input from the scroll view

    func setSizes(content: CGSize, viewport: CGSize) {
        if content != contentSize { contentSize = content }
        if viewport != viewportSize { viewportSize = viewport }
        updateBounds()
        // Keep the offset valid when the content shrinks.
        if !isScrolling {
            x.clamp()
            y.clamp()
            publish()
        }
    }

    private func updateBounds() {
        let maximum = maximumOffset
        x.maximum = axes.contains(.horizontal) ? Double(maximum.x) : 0
        y.maximum = axes.contains(.vertical) ? Double(maximum.y) : 0
        x.limit = Double(viewportSize.width)
        y.limit = Double(viewportSize.height)
    }

    /// A finger touched down: stop coasting where the content is.
    func touchDown() {
        x.catchMotion()
        y.catchMotion()
        run()
    }

    /// The finger lifted. Content it caught past an end without dragging
    /// springs back.
    func touchUp() {
        x.settleIntoRange()
        y.settleIntoRange()
        run()
    }

    /// Moves by `delta` from where the content is headed (keyboard scrolling,
    /// so repeated keys accumulate), animated and kept in range.
    func scroll(byKeyboard delta: CGSize) {
        let maximum = maximumOffset
        x.settle(at: min(max(x.destination + Double(delta.width), 0), Double(maximum.x)), animated: true)
        y.settle(at: min(max(y.destination + Double(delta.height), 0), Double(maximum.y)), animated: true)
        run()
    }

    /// A drag moved the content by `translation` since it began.
    func drag(translation: CGSize) {
        x.drag(Double(translation.width))
        y.drag(Double(translation.height))
        run()
    }

    /// A drag ended with the finger moving at `velocity` (points per second).
    func endDrag(velocity: CGSize) {
        x.release(velocity: -Double(velocity.width))
        y.release(velocity: -Double(velocity.height))
        run()
    }

    /// Scroll-wheel or trackpad deltas, in points (positive scrolls content up/left).
    func scroll(by delta: CGSize, phase: ScrollPhase) {
        x.wheel(Double(delta.width), phase: phase)
        y.wheel(Double(delta.height), phase: phase)
        run()
    }

    enum ScrollPhase {
        /// Fingers on the trackpad, or the system's momentum after them.
        case interacting
        /// The gesture and its momentum ended.
        case ended
        /// A discrete mouse wheel step.
        case step
    }

    private func run() {
        GlassMotionDriver.shared.run(self)
        publish()
    }

    func advance(by dt: Double) -> Bool {
        let movingX = x.advance(by: dt), movingY = y.advance(by: dt)
        publish()
        return movingX || movingY
    }

    private func publish() {
        let point = CGPoint(x: x.offset, y: y.offset)
        if point != offset { offset = point }
        let scrolling = x.isActive || y.isActive
        if scrolling != isScrolling { isScrolling = scrolling }
    }
}

/// Scroll physics along one axis: direct drag with an iOS rubber band past
/// the ends, exponential deceleration, and a critically damped spring back
/// into range.
struct ScrollAxis {
    var offset: Double = 0
    var velocity: Double = 0
    var maximum: Double = 0
    /// Rubber band dimension: the viewport's length.
    var limit: Double = 1

    private enum Mode { case idle, dragging, coasting, settling, wheel }
    private var mode = Mode.idle
    private var dragStart: Double = 0
    /// The un-banded offset a wheel gesture accumulates past the ends.
    private var wheelRaw: Double = 0
    private var spring = SpringChannel(0, ScrollAxis.settle)

    /// UIScrollView's normal deceleration: velocity × 0.998 per millisecond.
    static let decelerationRate = 1000 * log(1 / 0.998)
    /// Settling back into range or to a requested offset (the glow spring:
    /// critically damped, so the content never bounces past its end).
    static let settle = GlassSpring.glow

    var isActive: Bool { mode != .idle }

    /// Where the axis comes to rest: the target it is settling to, or the
    /// offset.
    var destination: Double { mode == .settling ? spring.state.target : offset }

    private func banded(_ raw: Double) -> Double {
        if raw < 0 { return -GlassOptics.rubberBand(-raw, limit: limit) }
        if raw > maximum { return maximum + GlassOptics.rubberBand(raw - maximum, limit: limit) }
        return raw
    }

    /// Inverse of `banded` inside the band, so a drag that starts while
    /// rubber-banding continues from where the content is.
    private func unbanded(_ shown: Double) -> Double {
        func inverse(_ y: Double) -> Double {
            let fraction = min(y / limit, 0.999)
            return limit * fraction / (0.55 * (1 - fraction))
        }
        if shown < 0 { return -inverse(-shown) }
        if shown > maximum { return maximum + inverse(shown - maximum) }
        return shown
    }

    mutating func clamp() {
        offset = min(max(offset, 0), maximum)
        mode = .idle
        velocity = 0
    }

    mutating func catchMotion() {
        switch mode {
        case .coasting, .settling:
            mode = .idle
            velocity = 0
        case .dragging:
            // A drag whose end never arrived (its gesture was cancelled):
            // let go of it before the new touch.
            release(velocity: 0)
        case .idle, .wheel:
            break
        }
    }

    /// Springs back into range if the content rests past an end.
    mutating func settleIntoRange() {
        guard mode == .idle, offset < 0 || offset > maximum else { return }
        velocity = 0
        startSettling(to: offset < 0 ? 0 : maximum)
    }

    mutating func drag(_ translation: Double) {
        if mode != .dragging {
            mode = .dragging
            dragStart = unbanded(offset) + translation
        }
        offset = banded(dragStart - translation)
    }

    mutating func release(velocity v: Double) {
        guard mode == .dragging else { return }
        velocity = v
        if offset < 0 || offset > maximum {
            startSettling(to: offset < 0 ? 0 : maximum)
        } else {
            mode = abs(v) > 1 ? .coasting : .idle
            if mode == .idle { velocity = 0 }
        }
    }

    mutating func wheel(_ delta: Double, phase: GlassScrollPosition.ScrollPhase) {
        switch phase {
        case .step:
            mode = .idle
            velocity = 0
            offset = min(max(offset + delta, 0), maximum)
        case .interacting:
            if mode != .wheel {
                mode = .wheel
                wheelRaw = unbanded(offset)
            }
            wheelRaw += delta
            offset = banded(wheelRaw)
        case .ended:
            guard mode == .wheel else { return }
            velocity = 0
            if offset < 0 || offset > maximum {
                startSettling(to: offset < 0 ? 0 : maximum)
            } else {
                mode = .idle
            }
        }
    }

    mutating func settle(at target: Double, animated: Bool) {
        if animated {
            velocity = 0
            startSettling(to: target)
        } else {
            mode = .idle
            velocity = 0
            offset = target
        }
    }

    private mutating func startSettling(to target: Double) {
        mode = .settling
        spring = SpringChannel(offset, Self.settle)
        spring.state.velocity = velocity
        spring.state.target = target
    }

    /// Advances one frame. Returns whether the axis still moves.
    mutating func advance(by dt: Double) -> Bool {
        switch mode {
        case .idle:
            return false
        case .dragging, .wheel:
            return true
        case .coasting:
            let decay = exp(-Self.decelerationRate * dt)
            offset += velocity * (1 - decay) / Self.decelerationRate
            velocity *= decay
            if offset < 0 || offset > maximum {
                // Coast into the band, then spring back from there.
                startSettling(to: offset < 0 ? 0 : maximum)
            } else if abs(velocity) < 2 {
                mode = .idle
                velocity = 0
                return false
            }
            return true
        case .settling:
            spring.advance(by: dt)
            offset = spring.value
            velocity = spring.velocity
            if spring.isSettled {
                mode = .idle
                velocity = 0
                return false
            }
            return true
        }
    }
}
