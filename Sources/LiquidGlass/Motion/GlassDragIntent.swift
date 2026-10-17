import CoreGraphics

/// Decides whether a touch on a horizontal control (a slider, a tab bar's
/// lens) drags the control or belongs to an enclosing scroll view.
///
/// The drag commits once the finger has moved `distance` points, more
/// sideways than up or down; a touch that moves mostly vertically first is
/// left to the scroll view for the rest of its life. With `distance` 0 the
/// first location commits (pointer input, where nothing scrolls by dragging).
@MainActor
final class GlassDragIntent {
    private enum Phase {
        case idle
        case pending(start: CGPoint)
        case committed(start: CGPoint)
        case declined
    }

    let distance: CGFloat
    private var phase = Phase.idle

    init(distance: CGFloat) {
        self.distance = distance
    }

    /// Whether the touch drags the control.
    var isCommitted: Bool {
        if case .committed = phase { return true }
        return false
    }

    /// Where an undecided touch began: a touch that ends here was a tap.
    var pendingStart: CGPoint? {
        if case .pending(let start) = phase { return start }
        return nil
    }

    /// Feeds the touch's current location. Returns where the touch began
    /// once the drag has committed (on this call or an earlier one), and
    /// `nil` while it is undecided or was declined.
    func update(_ location: CGPoint) -> CGPoint? {
        switch phase {
        case .idle:
            phase = .pending(start: location)
            return update(location)
        case .pending(let start):
            let dx = abs(location.x - start.x), dy = abs(location.y - start.y)
            if distance <= 0 || (dx >= distance && dx > dy) {
                phase = .committed(start: start)
                return start
            }
            if max(dx, dy) >= distance { phase = .declined }
            return nil
        case .committed(let start):
            return start
        case .declined:
            return nil
        }
    }

    /// The touch ended; the next one decides afresh.
    func reset() {
        phase = .idle
    }
}
