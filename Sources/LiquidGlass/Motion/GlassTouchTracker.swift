#if canImport(UIKit)
import SwiftUI
import UIKit

/// Tracks a finger on glass without competing for it.
///
/// SwiftUI's `DragGesture(minimumDistance: 0)` claims the touch on newer
/// systems, so a scroll that starts on a glass control wouldn't scroll. This
/// recognizer runs alongside every other one (buttons and scroll views
/// included), never cancels or delays their touches, and ends the press as
/// soon as an enclosing scroll view starts to pan, unless the press has
/// claimed the touch (a control being dragged sideways), which stops the
/// scroll instead.
@available(iOS 18.0, *)
struct GlassTouchTracker<Space: CoordinateSpaceProtocol>: UIGestureRecognizerRepresentable {
    /// The coordinate space `changed` reports the local location in.
    let space: Space
    /// The finger moved: its location in `space` and in the global space.
    let changed: (_ local: CGPoint, _ global: CGPoint) -> Void
    /// The finger lifted (`true`), or the touch was taken over or cancelled.
    let ended: (_ completed: Bool) -> Void
    /// Whether the press owns the touch, so a scroll that starts is stopped.
    let claimsTouch: () -> Bool

    func makeCoordinator(converter: CoordinateSpaceConverter) -> Coordinator {
        Coordinator()
    }

    func updateUIGestureRecognizer(_ recognizer: UILongPressGestureRecognizer, context: Context) {
        context.coordinator.claimsTouch = claimsTouch
    }

    func makeUIGestureRecognizer(context: Context) -> UILongPressGestureRecognizer {
        let recognizer = UILongPressGestureRecognizer()
        recognizer.minimumPressDuration = 0
        recognizer.allowableMovement = .greatestFiniteMagnitude
        recognizer.cancelsTouchesInView = false
        recognizer.delaysTouchesBegan = false
        recognizer.delaysTouchesEnded = false
        recognizer.delegate = context.coordinator
        context.coordinator.claimsTouch = claimsTouch
        return recognizer
    }

    func handleUIGestureRecognizerAction(_ recognizer: UILongPressGestureRecognizer, context: Context) {
        switch recognizer.state {
        case .began, .changed:
            changed(context.converter.location(in: space), context.converter.location(in: .global))
        case .ended:
            ended(true)
        default:
            ended(false)
        }
    }

    @MainActor
    final class Coordinator: NSObject, UIGestureRecognizerDelegate {
        private weak var tracker: UIGestureRecognizer?
        var claimsTouch: () -> Bool = { false }

        func gestureRecognizer(
            _ gestureRecognizer: UIGestureRecognizer,
            shouldRecognizeSimultaneouslyWith other: UIGestureRecognizer
        ) -> Bool {
            // Follow an enclosing scroll view's pan so the press ends when
            // the content starts to scroll.
            if other.view is UIScrollView, other is UIPanGestureRecognizer {
                tracker = gestureRecognizer
                other.removeTarget(self, action: #selector(scrollPanChanged(_:)))
                other.addTarget(self, action: #selector(scrollPanChanged(_:)))
            }
            return true
        }

        @objc private func scrollPanChanged(_ pan: UIPanGestureRecognizer) {
            guard pan.state == .began, let tracker, tracker.state == .began || tracker.state == .changed else { return }
            // Toggling isEnabled cancels a recognizer: the scroll's, when the
            // press owns the touch; otherwise the tracker's, which reports
            // `ended`.
            let cancelled = claimsTouch() ? pan : tracker
            cancelled.isEnabled = false
            cancelled.isEnabled = true
        }
    }
}
#endif
