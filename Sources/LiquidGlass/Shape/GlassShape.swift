import SwiftUI

/// The continuous-corner outline every glass surface uses.
///
/// `GlassShape` draws the same outline the shader renders
/// (`spec/OPTICS.md` §1): straight edges joined by superellipse corners that
/// meet the edges with continuous curvature. Use it to clip, stroke or hit-test
/// content so it lines up with the glass exactly.
///
/// ```swift
/// Image("cover")
///     .clipShape(GlassShape(radius: 24))
/// ```
public struct GlassShape: InsettableShape {
    /// The corner radius.
    public var radius: GlassRadius
    /// How far the outline is inset from the shape's frame, in points.
    public var insetAmount: CGFloat

    /// Creates a continuous-corner shape.
    /// - Parameter radius: A corner radius in points, `.capsule` or `.circle`.
    public init(radius: GlassRadius = 16) {
        self.radius = radius
        self.insetAmount = 0
    }

    public func path(in rect: CGRect) -> Path {
        let frame = rect.insetBy(dx: insetAmount, dy: insetAmount)
        guard frame.width > 0, frame.height > 0 else { return Path() }
        let params = GlassOptics.shapeParameters(width: frame.width, height: frame.height, radius: radius.inset(by: insetAmount))
        // Enough points that the chord error stays far below a device pixel.
        let segments = Int(min(48, max(8, params.extent / 1.5)))
        let points = GlassOptics.shapeOutline(width: frame.width, height: frame.height, radius: radius.inset(by: insetAmount), segments: segments)
        var path = Path()
        path.addLines(points.map { CGPoint(x: $0.x + frame.minX, y: $0.y + frame.minY) })
        path.closeSubpath()
        return path
    }

    public func inset(by amount: CGFloat) -> GlassShape {
        var shape = self
        shape.insetAmount += amount
        return shape
    }

    public var animatableData: CGFloat {
        get { insetAmount }
        set { insetAmount = newValue }
    }
}
