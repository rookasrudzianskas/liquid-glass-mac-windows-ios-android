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
///
/// On macOS the link follows the screen of the key window (glass animates
/// where the user interacts), so springs tick at that display's refresh
/// rate, 120 Hz on ProMotion, and move with the window to another screen.
@MainActor
final class GlassMotionDriver: NSObject {
    static let shared = GlassMotionDriver()

    private var clients: [ObjectIdentifier: GlassAnimating] = [:]
    private var link: CADisplayLink?
    private var lastTimestamp: CFTimeInterval?
    #if !canImport(UIKit)
    private var screenNumber: NSNumber?
    private var observers: [NSObjectProtocol] = []
    #endif

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
        // iPhone apps get more than 60 Hz only with
        // CADisableMinimumFrameDurationOnPhone in their Info.plist.
        let link = CADisplayLink(target: self, selector: #selector(tick(_:)))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 60, maximum: 120, preferred: 120)
        #else
        observeScreens()
        guard let screen = Self.activeScreen else { return }
        let link = screen.displayLink(target: self, selector: #selector(tick(_:)))
        screenNumber = Self.number(of: screen)
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

    #if !canImport(UIKit)
    /// The screen of the window the user works in.
    private static var activeScreen: NSScreen? {
        NSApp?.keyWindow?.screen ?? NSApp?.mainWindow?.screen ?? NSScreen.main ?? NSScreen.screens.first
    }

    private static func number(of screen: NSScreen) -> NSNumber? {
        screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber
    }

    private func observeScreens() {
        guard observers.isEmpty else { return }
        let center = NotificationCenter.default
        let names: [(Notification.Name, Bool)] = [
            (NSWindow.didBecomeKeyNotification, false),
            (NSWindow.didChangeScreenNotification, false),
            // Displays were added, removed or reconfigured: the link's screen
            // may be gone or have a new refresh rate.
            (NSApplication.didChangeScreenParametersNotification, true),
        ]
        for (name, always) in names {
            observers.append(center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.followActiveScreen(force: always) }
            })
        }
    }

    /// Moves the link to the active screen when that screen changed.
    private func followActiveScreen(force: Bool) {
        guard let link, let screen = Self.activeScreen else { return }
        guard force || Self.number(of: screen) != screenNumber else { return }
        let paused = link.isPaused
        link.invalidate()
        self.link = nil
        lastTimestamp = nil
        start()
        self.link?.isPaused = paused
    }
    #endif
}
