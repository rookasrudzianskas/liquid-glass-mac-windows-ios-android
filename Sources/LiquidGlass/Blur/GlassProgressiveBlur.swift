import SwiftUI

/// A blur that grows toward an edge of the scene's backdrop.
///
/// Six blur levels over the sharp backdrop (`spec/OPTICS.md` §7): level `i`
/// has σ = `blur`·(i/6)² and fades in with the smoothstep of the depth `u`,
/// which runs from 0 on the sharp side to 1 at the edge, so the weights sum
/// to 1 at every depth. Place it in a scene's foreground along the edge it
/// blurs, usually under a toolbar:
///
/// ```swift
/// VStack {
///     GlassProgressiveBlur(edge: .top).ignoresSafeArea()
///     Spacer()
/// }
/// ```
///
/// It fills the width (or height) it is offered and is `size` points deep.
/// It never takes touches. Outside a scene it falls back to the platform
/// material under the same fade.
public struct GlassProgressiveBlur: View {
    private let edge: Edge
    private let size: CGFloat
    private let blur: Double

    @Environment(\.glassBackdrop) private var backdrop
    @Environment(\.layoutDirection) private var layoutDirection

    /// Creates a progressive blur.
    /// - Parameters:
    ///   - edge: The edge the blur grows toward.
    ///   - size: How far the blur reaches from the edge, in points. Default 80.
    ///   - blur: The Gaussian σ at the edge, in points. Default 20.
    public init(edge: Edge = .top, size: CGFloat = 80, blur: Double = 20) {
        self.edge = edge
        self.size = max(0, size)
        self.blur = max(0, blur)
    }

    public var body: some View {
        let vertical = edge == .top || edge == .bottom
        Group {
            if let backdrop {
                ZStack {
                    ForEach(1...GlassOptics.progressiveLevels, id: \.self) { level in
                        blurLevel(level, backdrop: backdrop)
                    }
                }
                // The levels are placed in left-to-right coordinates; the
                // gradients below resolve the edge for the layout direction.
                .environment(\.layoutDirection, .leftToRight)
            } else {
                Rectangle()
                    .fill(.ultraThinMaterial)
                    .mask(LinearGradient(colors: [.clear, .black], startPoint: sharpSide, endPoint: edgePoint))
            }
        }
        .frame(maxWidth: vertical ? .infinity : nil, maxHeight: vertical ? nil : .infinity)
        .frame(width: vertical ? nil : size, height: vertical ? size : nil)
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }

    /// One blur level: a copy of the backdrop, blurred beyond the strip so
    /// its edges stay true, faded in by its share of the depth.
    private func blurLevel(_ level: Int, backdrop: GlassBackdrop) -> some View {
        let sigma = GlassOptics.progressiveLevelBlur(level: level, blur: blur)
        let margin = CGFloat((3 * sigma).rounded(.up))
        return GeometryReader { proxy in
            Color.clear
                .frame(width: proxy.size.width + 2 * margin, height: proxy.size.height + 2 * margin)
                .overlay(alignment: .topLeading) { GlassBackdropCopy(backdrop: backdrop) }
                .clipped()
                // Flattened first, so the blur sees one image of the backdrop.
                .compositingGroup()
                .blur(radius: GlassRendering.blurRadius(forSigma: sigma), opaque: false)
                .offset(x: -margin, y: -margin)
                .frame(width: proxy.size.width, height: proxy.size.height, alignment: .topLeading)
                .clipped()
                .mask(LinearGradient(stops: Self.stops(level: level), startPoint: sharpSide, endPoint: edgePoint))
        }
        // Fill the strip even where it extends under the safe area.
        .ignoresSafeArea()
    }

    /// Alpha of level `i` when drawn over the levels below it, so the stack
    /// composites to Σ wᵢ·levelᵢ: `sat(6·ease(u) − (i − 1))`.
    static func stops(level: Int, samples: Int = 48) -> [Gradient.Stop] {
        (0...samples).map { index in
            let u = Double(index) / Double(samples)
            let eased = u * u * (3 - 2 * u)
            let alpha = min(1, max(0, Double(GlassOptics.progressiveLevels) * eased - Double(level - 1)))
            return Gradient.Stop(color: .black.opacity(alpha), location: u)
        }
    }

    /// Gradient points are fixed left and right, while `.leading` and
    /// `.trailing` follow the layout direction.
    private var edgePoint: UnitPoint {
        let rtl = layoutDirection == .rightToLeft
        switch edge {
        case .top: return .top
        case .bottom: return .bottom
        case .leading: return rtl ? .trailing : .leading
        case .trailing: return rtl ? .leading : .trailing
        }
    }

    private var sharpSide: UnitPoint {
        UnitPoint(x: 1 - edgePoint.x, y: 1 - edgePoint.y)
    }
}
