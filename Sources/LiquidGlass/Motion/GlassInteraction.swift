import Observation
import SwiftUI

/// One spring-driven value.
struct SpringChannel {
    var state: GlassSpring.State
    var spring: GlassSpring

    init(_ value: Double, _ spring: GlassSpring) {
        state = GlassSpring.State(value: value)
        self.spring = spring
    }

    var value: Double { state.value }
    var velocity: Double { state.velocity }
    var isSettled: Bool { state.isSettled }

    /// Retargets the channel; `motion` decides how it gets there.
    mutating func set(_ target: Double, _ spring: GlassSpring, motion: GlassMotion) {
        switch motion {
        case .full: self.spring = spring
        case .reduced: self.spring = spring.critical
        case .none: self.spring = spring
            state = GlassSpring.State(value: target)
            return
        }
        state.target = target
    }

    mutating func jump(to value: Double) {
        state = GlassSpring.State(value: value)
    }

    mutating func advance(by dt: Double) {
        state = spring.step(state, dt: dt)
    }
}

/// The elastic, illuminated response of one interactive surface
/// (`spec/OPTICS.md` §6): it grows under a press, stretches toward a drag with
/// constant volume, settles with gel-like overshoot on release, and lights up
/// under the finger or pointer.
///
/// Gesture callbacks set targets; the ``GlassMotionDriver`` advances the
/// springs while anything moves, and views observe only the presentation
/// values.
@MainActor
@Observable
final class GlassInteraction: GlassAnimating {
    /// Press-and-drag scale of the outline and its content.
    private(set) var scale = CGSize(width: 1, height: 1)
    /// Drag translation toward the finger, in points.
    private(set) var translation = CGSize.zero
    /// Glow level, 0…1.
    private(set) var glow: Double = 0
    /// Where the light sits, in the surface's local points.
    private(set) var glowPoint = CGPoint.zero
    /// Whether a finger, pointer or key holds the surface.
    private(set) var isPressed = false

    /// The surface's layout size, kept current by its glass layer.
    @ObservationIgnored var size = CGSize(width: 1, height: 1)
    /// The resolved motion level.
    @ObservationIgnored var motion = GlassMotion.full

    @ObservationIgnored private var tx = SpringChannel(0, .track)
    @ObservationIgnored private var ty = SpringChannel(0, .track)
    @ObservationIgnored private var press = SpringChannel(0, .press)
    @ObservationIgnored private var light = SpringChannel(0, .glow)
    @ObservationIgnored private var pointer: Pointer?
    @ObservationIgnored private var hovering = false
    @ObservationIgnored private var keyboard = false
    @ObservationIgnored private var carried = CGVector.zero

    private struct Pointer {
        var start: CGPoint
        var local: CGPoint
        var global: CGPoint?
        var lastOrigin: CGPoint?
    }

    // MARK: Input

    /// A finger or pointer moved while pressing, in the surface's local points.
    func pointerChanged(local: CGPoint) {
        if pointer == nil {
            pointer = Pointer(start: local, local: local)
            carried = .zero
            glowPoint = local
            press.set(1, .press, motion: motion)
            light.set(1, .press, motion: motion)
            let follow = GlassSpring.follow(width: size.width, height: size.height)
            tx.set(tx.state.target, follow, motion: motion)
            ty.set(ty.state.target, follow, motion: motion)
        } else {
            pointer?.local = local
        }
        wake()
    }

    /// The same pointer in a coordinate space that does not move with the
    /// surface, so a surface the application drags stretches with its speed.
    func pointerChanged(global: CGPoint) {
        pointer?.global = global
    }

    /// The press ended or was cancelled.
    func pointerEnded() {
        guard pointer != nil else { return }
        pointer = nil
        release()
    }

    /// A pointer hovers at `location`, or left (`nil`). Pointer devices only.
    func hover(_ location: CGPoint?) {
        hovering = location != nil
        if let location, pointer == nil, !keyboard, location != glowPoint { glowPoint = location }
        if pointer == nil, !keyboard, light.state.target != (hovering ? Self.hoverLevel : 0) {
            light.set(hovering ? Self.hoverLevel : 0, .glow, motion: motion)
            wake()
        }
    }

    /// A key press (Space or Return) or an accessibility activation.
    func keyboardPress(_ down: Bool) {
        guard pointer == nil, down != keyboard else { return }
        keyboard = down
        if down {
            glowPoint = CGPoint(x: size.width / 2, y: size.height / 2)
            press.set(1, .press, motion: motion)
            light.set(1, .press, motion: motion)
        } else {
            release()
        }
        wake()
    }

    private func release() {
        press.set(0, .release, motion: motion)
        tx.set(0, .release, motion: motion)
        ty.set(0, .release, motion: motion)
        light.set(hovering ? Self.hoverLevel : 0, .glow, motion: motion)
        wake()
    }

    /// The glow level under a hovering pointer.
    private static let hoverLevel = 0.35

    private func wake() {
        GlassMotionDriver.shared.run(self)
        publish()
    }

    // MARK: Frames

    func advance(by dt: Double) -> Bool {
        let width = max(size.width, 1), height = max(size.height, 1)
        if var p = pointer {
            let small = max(width, height) <= 72
            // Controls give a little under the finger; larger surfaces only
            // at their edges. Local coordinates move with the surface, so a
            // surface the application moves with the pointer doesn't stretch
            // from the offset, only from its speed.
            let ox = small ? p.local.x - p.start.x : p.local.x - min(max(p.local.x, 0), width)
            let oy = small ? p.local.y - p.start.y : p.local.y - min(max(p.local.y, 0), height)
            let limit = GlassOptics.stretchLimit(width: width, height: height)
            tx.state.target = GlassOptics.rubberBand(ox, limit: limit)
            ty.state.target = GlassOptics.rubberBand(oy, limit: limit)
            if p.local != glowPoint { glowPoint = p.local }
            if let global = p.global {
                let origin = CGPoint(x: global.x - p.local.x, y: global.y - p.local.y)
                if let last = p.lastOrigin, dt > 0 {
                    let rate = min(1, dt * 12)
                    carried.dx += ((origin.x - last.x) / dt - carried.dx) * rate
                    carried.dy += ((origin.y - last.y) / dt - carried.dy) * rate
                }
                p.lastOrigin = origin
                pointer = p
            }
        } else {
            let decay = exp(-dt * 10)
            carried.dx *= decay
            carried.dy *= decay
            if abs(carried.dx) < 0.5, abs(carried.dy) < 0.5 { carried = .zero }
        }
        if motion == .none {
            tx.jump(to: tx.state.target)
            ty.jump(to: ty.state.target)
            press.jump(to: press.state.target)
            light.jump(to: light.state.target)
        } else {
            tx.advance(by: dt)
            ty.advance(by: dt)
            press.advance(by: dt)
            light.advance(by: dt)
        }
        publish()
        let moving = !(tx.isSettled && ty.isSettled && press.isSettled && light.isSettled)
        return pointer != nil || keyboard || moving || carried != .zero
    }

    /// Writes the presentation values, touching only those that changed so
    /// observers redraw only when something visible moves.
    private func publish() {
        let elastic = motion == .full
        let width = max(size.width, 1), height = max(size.height, 1)
        let newScale = GlassOptics.pressScale(
            width: width, height: height, press: press.value,
            tx: tx.value, ty: ty.value, vx: tx.velocity, vy: ty.velocity,
            carriedX: carried.dx, carriedY: carried.dy, reduced: !elastic
        )
        let newTranslation = elastic ? CGSize(width: tx.value, height: ty.value) : .zero
        if newScale != scale { scale = newScale }
        if newTranslation != translation { translation = newTranslation }
        let newGlow = max(0, light.value)
        if newGlow != glow { glow = newGlow }
        let pressed = pointer != nil || keyboard
        if pressed != isPressed { isPressed = pressed }
    }
}
