import Foundation

/// A damped spring described by its perceptual duration and bounce.
///
/// The parameterization matches `spec/OPTICS.md` §6 (and SwiftUI's
/// `Spring(duration:bounce:)`): `stiffness = (2π / duration)²` and
/// `damping = 4π(1 − bounce) / duration` for nonnegative bounce. Springs are
/// solved in closed form by ``step(_:dt:)``, so a dropped frame never changes
/// the trajectory.
public struct GlassSpring: Hashable, Sendable {
    /// Perceptual duration in seconds.
    public var duration: Double
    /// Bounce in `-1 < bounce < 1`; 0 is critically damped.
    public var bounce: Double

    public init(duration: Double, bounce: Double) {
        self.duration = duration
        self.bounce = bounce
    }

    /// Finger down: the material catches it.
    public static let press = GlassSpring(duration: 0.24, bounce: 0.2)
    /// Finger up: gel-like overshoot back to rest.
    public static let release = GlassSpring(duration: 0.5, bounce: 0.42)
    /// Light settling in and out.
    public static let glow = GlassSpring(duration: 0.42, bounce: 0)
    /// Glass growing out of another shape (menus).
    public static let morph = GlassSpring(duration: 0.42, bounce: 0.18)
    /// A surface flowing to a new size.
    public static let layout = GlassSpring(duration: 0.4, bounce: 0.16)
    /// Selection lenses and drag follow.
    public static let track = GlassSpring(duration: 0.18, bounce: 0)

    /// The track spring of a surface being dragged: larger glass is heavier
    /// and follows more slowly.
    public static func follow(width: Double, height: Double) -> GlassSpring {
        GlassSpring(duration: track.duration + 0.0005 * max(width, height), bounce: 0)
    }

    /// The same spring without overshoot, used when motion is reduced.
    public var critical: GlassSpring { GlassSpring(duration: duration, bounce: min(bounce, 0)) }

    /// Spring constant per unit mass.
    public var stiffness: Double {
        let omega = 2 * Double.pi / duration
        return omega * omega
    }

    /// Damping coefficient per unit mass.
    public var damping: Double {
        bounce >= 0 ? 4 * Double.pi * (1 - bounce) / duration : 4 * Double.pi / (duration * (1 + bounce))
    }

    /// The state of one animated value.
    public struct State: Hashable, Sendable {
        public var value: Double
        public var velocity: Double
        public var target: Double

        public init(value: Double, velocity: Double = 0, target: Double? = nil) {
            self.value = value
            self.velocity = velocity
            self.target = target ?? value
        }

        /// Whether the value rests on its target.
        public var isSettled: Bool { value == target && velocity == 0 }
    }

    /// Advances a spring by `dt` seconds in closed form (`springStep` in
    /// `spec/reference.mjs`). Values within 1e-4 (and velocities within 1e-3)
    /// of rest, relative to the motion's scale, snap to the target.
    public func step(_ state: State, dt: Double) -> State {
        if (state.value == state.target && state.velocity == 0) || !(dt > 0) { return state }
        let omega = stiffness.squareRoot()
        let zeta = damping / (2 * omega)
        let x0 = state.value - state.target, v0 = state.velocity
        var x: Double, v: Double
        if zeta < 1 {
            let wd = omega * (1 - zeta * zeta).squareRoot()
            let decay = exp(-zeta * omega * dt)
            let a = x0, b = (v0 + zeta * omega * x0) / wd
            let c = cos(wd * dt), s = sin(wd * dt)
            x = decay * (a * c + b * s)
            v = decay * ((b * wd - zeta * omega * a) * c - (a * wd + zeta * omega * b) * s)
        } else if zeta == 1 {
            let decay = exp(-omega * dt)
            let b = v0 + omega * x0
            x = decay * (x0 + b * dt)
            v = decay * (b - omega * (x0 + b * dt))
        } else {
            let root = omega * (zeta * zeta - 1).squareRoot()
            let r1 = -zeta * omega + root, r2 = -zeta * omega - root
            let c2 = (v0 - r1 * x0) / (r2 - r1), c1 = x0 - c2
            let e1 = exp(r1 * dt), e2 = exp(r2 * dt)
            x = c1 * e1 + c2 * e2
            v = c1 * r1 * e1 + c2 * r2 * e2
        }
        let scale = max(1, abs(state.target), abs(x0))
        if abs(x) < 1e-4 * scale, abs(v) < 1e-3 * scale { return State(value: state.target, velocity: 0, target: state.target) }
        return State(value: state.target + x, velocity: v, target: state.target)
    }
}
