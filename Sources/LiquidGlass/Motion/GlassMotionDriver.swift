import QuartzCore
import SwiftUI
#if canImport(UIKit)
import UIKit
#elseif canImport(AppKit)
import AppKit
#endif

/// Something the motion driver advances once per display frame.
@MainActor
protocol GlassAnimating: AnyObject {
    /// Advances by `dt` seconds. Returns `true` while it still needs frames.
    func advance(by dt: Double) -> Bool
}

/// Advances glass springs on the display's refresh, and only while something
/// is moving: the display link is paused whenever every client has settled.
@MainActor
final class GlassMotionDriver: NSObject {
    static let shared = GlassMotionDriver()

    private var clients: [ObjectIdentifier: GlassAnimating] = [:]
    private var link: CADisplayLink?
    private var lastTimestamp: CFTimeInterval?

    /// Starts advancing `client` until it reports that it has settled.
    func run(_ client: GlassAnimating) {
        clients[ObjectIdentifier(client)] = client
        if link == nil { start() }
        link?.isPaused = false
    }

    /// Stops advancing `client`.
    func stop(_ client: GlassAnimating) {
        clients[ObjectIdentifier(client)] = nil
        if clients.isEmpty { pause() }
    }

    private func start() {
        #if canImport(UIKit)
        let link = CADisplayLink(target: self, selector: #selector(tick(_:)))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 60, maximum: 120, preferred: 120)
        #else
        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        let link = screen.displayLink(target: self, selector: #selector(tick(_:)))
        #endif
        link.add(to: .main, forMode: .common)
        self.link = link
    }

    private func pause() {
        link?.isPaused = true
        lastTimestamp = nil
    }

    @objc private func tick(_ link: CADisplayLink) {
        // The analytic springs are stable across dropped frames, so a long
        // frame advances them by its real duration instead of a capped one.
        let dt = lastTimestamp.map { max(0, link.timestamp - $0) } ?? 1.0 / 60.0
        lastTimestamp = link.timestamp
        for (id, client) in clients where !client.advance(by: dt) {
            clients[id] = nil
        }
        if clients.isEmpty { pause() }
    }
}
