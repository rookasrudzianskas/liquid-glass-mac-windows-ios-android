import Observation
import SwiftUI

/// Geometry of a value control's thumb (`spec/OPTICS.md` §6), with touch
/// sizes on iOS and the specified desktop sizes on macOS.
struct GlassThumbMetrics {
    /// The thumb's size at rest.
    var size: CGSize
    /// How far the lifted lens grows past the thumb on each side.
    var grow: CGSize
    /// Refraction of the lifted lens.
    var refraction: Double

    #if os(macOS)
    static let slider = GlassThumbMetrics(size: CGSize(width: 20, height: 16), grow: CGSize(width: 2.5, height: 2), refraction: 5)
    static let `switch` = GlassThumbMetrics(size: CGSize(width: 32, height: 20), grow: CGSize(width: 9, height: 5.5), refraction: 7.75)
    #else
    static let slider = GlassThumbMetrics(size: CGSize(width: 36, height: 24), grow: CGSize(width: 5, height: 4), refraction: 5)
    static let `switch` = GlassThumbMetrics(size: CGSize(width: 37, height: 24), grow: CGSize(width: 10, height: 6.5), refraction: 7.75)
    #endif
}

/// How a thumb lifts into glass: a 200 ms ease-in-out crossfade while held,
/// a short pulse for a quick tap, and a stretch along the track with speed.
@MainActor
@Observable
final class GlassThumbMotion: GlassAnimating {
    /// 0 at rest (opaque white thumb), 1 lifted into clear glass.
    private(set) var progress: Double = 0
    /// Stretch along the track, from the thumb's speed.
    private(set) var stretch: Double = 0
    /// The switch's animated position, 0 (off) … 1 (on).
    private(set) var position: Double = 0

    @ObservationIgnored private var motion = GlassMotion.full
    @ObservationIgnored private var pressed = false
    @ObservationIgnored private var pulse = false
    @ObservationIgnored private var elapsed: Double = 0
    @ObservationIgnored private var transition = (time: 0.0, from: 0.0, to: 0.0)
    @ObservationIgnored private var positionSpring = SpringChannel(0, GlassThumbMotion.switchSpring)
    @ObservationIgnored private var lastX: Double?
    @ObservationIgnored private var x: Double = 0
    @ObservationIgnored private var speed: Double = 0
    @ObservationIgnored private var placed = false

    /// The measured switch spring: response 0.47 s, damping ratio 0.7.
    static let switchSpring = GlassSpring(duration: 0.47, bounce: 0.3)

    // Samples of the native short-press envelope (seconds from touch down).
    private static let tapEnvelope: [(Double, Double)] = [
        (0, 0), (0.06, 0), (0.087, 0.120633), (0.103, 0.204144), (0.120, 0.280965), (0.137, 0.334618),
        (0.154, 0.353436), (0.170, 0.333683), (0.187, 0.281121), (0.204, 0.209108), (0.220, 0.133635),
        (0.237, 0.068474), (0.254, 0.022840), (0.270, 0.001445), (0.287, 0),
    ]

    func setPressed(_ down: Bool, motion: GlassMotion) {
        self.motion = motion
        guard down != pressed else { return }
        pressed = down
        // A press released within 80 ms that hasn't lifted halfway plays the tap pulse.
        pulse = !down && elapsed < 0.08 && progress < 0.5
        if down { elapsed = 0 }
        transition = (elapsed, progress, down ? 1 : 0)
        wake()
    }

    /// The thumb's center along the track, for its speed.
    func track(x newX: Double) {
        x = newX
        wake()
    }

    /// Moves the switch to `target` (0 or 1) on the switch spring, or follows
    /// a drag exactly.
    func setPosition(_ target: Double, animated: Bool, motion: GlassMotion) {
        self.motion = motion
        if !placed || !animated || motion != .full {
            positionSpring.jump(to: target)
            placed = true
        } else {
            positionSpring.set(target, Self.switchSpring, motion: motion)
        }
        wake()
    }

    private func wake() {
        GlassMotionDriver.shared.run(self)
        publish()
    }

    func advance(by dt: Double) -> Bool {
        elapsed += dt
        positionSpring.advance(by: dt)
        if motion == .full, let lastX, dt > 0 {
            speed += ((x - lastX) / dt - speed) * min(1, dt * 18)
        } else {
            speed = 0
        }
        lastX = x
        publish()
        let fading = elapsed - transition.time < (pulse ? 0.3 : 0.2)
        return pressed || fading || !positionSpring.isSettled || abs(speed) > 1
    }

    private func publish() {
        let newProgress: Double
        if motion != .full {
            newProgress = pressed ? 1 : 0
        } else if pulse {
            newProgress = Self.sampleTap(elapsed)
        } else {
            let t = min(1, max(0, (elapsed - transition.time) / 0.2))
            newProgress = transition.from + (transition.to - transition.from) * Self.easeInOut(t)
        }
        let newStretch = motion == .full ? min(0.28, abs(speed) * 0.0007) * newProgress : 0
        if newProgress != progress { progress = newProgress }
        if newStretch != stretch { stretch = newStretch }
        if positionSpring.value != position { position = positionSpring.value }
    }

    private static func sampleTap(_ seconds: Double) -> Double {
        for i in 1..<tapEnvelope.count where seconds <= tapEnvelope[i].0 {
            let (t0, p0) = tapEnvelope[i - 1], (t1, p1) = tapEnvelope[i]
            return p0 + (p1 - p0) * max(0, (seconds - t0) / (t1 - t0))
        }
        return 0
    }

    /// `cubic-bezier(0.42, 0, 0.58, 1)`, the native 200 ms interaction curve.
    private static func easeInOut(_ x: Double) -> Double {
        if x <= 0 { return 0 }
        if x >= 1 { return 1 }
        var lo = 0.0, hi = 1.0, t = x
        for _ in 0..<12 {
            let bx = 3 * (1 - t) * (1 - t) * t * 0.42 + 3 * (1 - t) * t * t * 0.58 + t * t * t
            if bx < x { lo = t } else { hi = t }
            t = (lo + hi) / 2
        }
        return 3 * (1 - t) * t * t + t * t * t
    }
}

/// A value control's thumb: an opaque white capsule at rest that lifts into
/// clear glass while held. The lens refracts `track` (drawn in the control's
/// own coordinates) as well as the scene's backdrop.
struct GlassThumbLayer<Track: View>: View {
    /// The control's size; the thumb sits inside it.
    let size: CGSize
    /// The thumb's center in the control's local points.
    let center: CGPoint
    let metrics: GlassThumbMetrics
    let motion: GlassThumbMotion
    let appearance: GlassAppearance?
    let track: Track

    var body: some View {
        let progress = motion.progress
        let along = 1 + motion.stretch, across = 1 / (1 + motion.stretch).squareRoot()
        let width = (metrics.size.width + 2 * metrics.grow.width * progress) * along
        let height = (metrics.size.height + 2 * metrics.grow.height * progress) * across
        let rect = CGRect(x: center.x - width / 2, y: center.y - height / 2, width: width, height: height)
        ZStack(alignment: .topLeading) {
            if progress > 0 {
                GlassSurfaceCore(
                    options: GlassSurfaceOptions(
                        material: .clear, appearance: appearance, radius: .capsule,
                        refraction: metrics.refraction, tint: nil, interactive: false, motion: nil
                    ),
                    outline: rect, opacity: progress,
                    extraPadding: max(metrics.grow.width, metrics.grow.height) * 2 + 4,
                    localBackdrop: AnyView(track),
                    content: Color.clear.frame(width: size.width, height: size.height)
                )
            }
            Capsule()
                .fill(.white)
                .shadow(color: .black.opacity(0.1), radius: 1, y: 1)
                .shadow(color: .black.opacity(0.1), radius: 6, y: 3)
                .frame(width: rect.width, height: rect.height)
                .offset(x: rect.minX, y: rect.minY)
                .opacity(1 - progress)
        }
        .frame(width: size.width, height: size.height, alignment: .topLeading)
        .allowsHitTesting(false)
    }
}
