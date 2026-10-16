import SwiftUI

/// Everything the glass shader needs to know about one surface, resolved
/// against the environment.
struct GlassRenderStyle: Equatable {
    var material: GlassMaterial
    var dark: Bool
    var radius: GlassRadius
    var refraction: Double
    /// sRGB tint, gamma-encoded.
    var tint: SIMD3<Float>?
    var nested: Bool
    var increaseContrast: Bool
    var reduceTransparency: Bool
    var pixel: Double
}

/// The moving parts of a surface's outline, in its local points.
struct GlassOutlineState: Equatable {
    /// The outline before press and drag; `nil` means the surface's bounds.
    var rect: CGRect?
    var scale = CGSize(width: 1, height: 1)
    var translation = CGSize.zero
    var glow: Double = 0
    var glowPoint = CGPoint.zero
    var opacity: Double = 1
}

enum GlassRendering {
    static let library = ShaderLibrary.bundle(.module)

    /// SwiftUI's `blur(radius:)` is a Gaussian with σ ≈ 0.947 · radius
    /// (measured on iOS 18 and macOS 15 by fitting blurred step edges).
    static func blurRadius(forSigma sigma: Double) -> CGFloat {
        CGFloat(sigma / 0.947)
    }

    /// The fill kernel's σ over the frost layer: σ_fill² = σ_frost² + σ²,
    /// so sampling the σ 7.33 result with σ adds up to the spec's fill blur.
    static func fillKernelSigma(dark: Bool) -> Double {
        let fill = GlassOptics.fillSigma(dark: dark), frost = GlassOptics.frostSigma
        return (fill * fill - frost * frost).squareRoot()
    }

    /// How far the glass layer extends past the surface on every side: room
    /// for press growth, drag stretch, refraction reaching past small shapes,
    /// and the frost blur's edge.
    static func padding(size: CGSize, refraction: Double, frosted: Bool, interactive: Bool, extra: CGFloat = 0) -> CGFloat {
        let width = Double(size.width), height = Double(size.height)
        let longest = max(width, height, 1)
        var growth = 0.0
        if interactive {
            let grow = min(0.16, 8 / longest)
            let cap = longest <= 72 ? 0.2 : 0.12
            let limit = GlassOptics.stretchLimit(width: width, height: height)
            growth = longest * ((1 + grow) * (1 + cap) - 1) / 2 + limit
        }
        let reach = min(40, max(0, refraction - min(width, height)))
        let blur = frosted ? 3 * GlassOptics.frostSigma + 4 : 0
        return CGFloat((growth + reach + blur + 2).rounded(.up)) + extra
    }
}

/// The optical layer of one glass surface: a copy of the scene's backdrop,
/// frosted for the Regular material, run through the glass shader. It is
/// drawn beneath the surface's content and extends `padding` points past the
/// surface so the outline can grow and stretch.
struct GlassLayer: View {
    let size: CGSize
    let style: GlassRenderStyle
    let outline: GlassOutlineState
    let padding: CGFloat
    /// Views drawn over the backdrop copy, in the surface's local points, so
    /// the glass refracts them too (a slider's track under its thumb).
    let localBackdrop: AnyView?

    @Environment(\.glassBackdrop) private var backdrop

    var body: some View {
        let layer = CGSize(width: size.width + 2 * padding, height: size.height + 2 * padding)
        optics(layer: layer)
            .frame(width: layer.width, height: layer.height)
            .offset(x: -padding, y: -padding)
            .allowsHitTesting(false)
            .accessibilityHidden(true)
    }

    @ViewBuilder
    private func optics(layer: CGSize) -> some View {
        if let backdrop {
            if style.reduceTransparency {
                Color.clear
                    .frame(width: layer.width, height: layer.height)
                    .layerEffect(shader(layer: layer, mode: 1), maxSampleOffset: .zero)
            } else {
                copy(of: backdrop, layer: layer)
                    .layerEffect(shader(layer: layer, mode: 0), maxSampleOffset: .zero)
            }
        } else {
            fallback(layer: layer)
        }
    }

    private var frosted: Bool { style.material == .regular && !style.reduceTransparency }

    @ViewBuilder
    private func copy(of backdrop: GlassBackdrop, layer: CGSize) -> some View {
        let clipped = Color.clear
            .frame(width: layer.width, height: layer.height)
            .overlay(alignment: .topLeading) {
                GlassBackdropCopy(backdrop: backdrop)
            }
            .overlay(alignment: .topLeading) {
                if let localBackdrop {
                    localBackdrop
                        .frame(width: size.width, height: size.height)
                        .offset(x: padding, y: padding)
                }
            }
            .clipped()
        if frosted {
            clipped.blur(radius: GlassRendering.blurRadius(forSigma: GlassOptics.frostSigma), opaque: false)
        } else {
            clipped
        }
    }

    /// Without a scene there is no backdrop to refract: the platform material
    /// stands in for the glass and the shader paints the rim light over it.
    private func fallback(layer: CGSize) -> some View {
        let rect = outlineRect
        return ZStack(alignment: .topLeading) {
            GlassShape(radius: style.radius)
                .fill(style.material == .regular ? Material.regular : Material.thin)
                .frame(width: rect.width, height: rect.height)
                .scaleEffect(x: outline.scale.width, y: outline.scale.height)
                .offset(x: rect.minX + padding + outline.translation.width, y: rect.minY + padding + outline.translation.height)
                .opacity(outline.opacity)
            Color.clear
                .frame(width: layer.width, height: layer.height)
                .layerEffect(shader(layer: layer, mode: 2), maxSampleOffset: .zero)
        }
    }

    private var outlineRect: CGRect {
        outline.rect ?? CGRect(origin: .zero, size: size)
    }

    private func shader(layer: CGSize, mode: Float) -> Shader {
        let rect = outlineRect
        let center = CGPoint(x: rect.midX + padding + outline.translation.width, y: rect.midY + padding + outline.translation.height)
        let glow = GlassOptics.glow(width: rect.width, height: rect.height, dark: style.dark)
        let margin: Float = frosted ? Float(3 * GlassOptics.frostSigma) : 0.5
        let tint = style.tint ?? .zero
        return GlassRendering.library.liquidGlass(
            .float4(center.x, center.y, rect.width, rect.height),
            .float4(style.radius.shaderValue, Float(style.refraction), Float(outline.scale.width), Float(outline.scale.height)),
            .float4(style.material == .regular ? 1 : 0, style.dark ? 1 : 0, style.nested ? 1 : 0, Float(style.pixel)),
            .float4(tint.x, tint.y, tint.z, style.tint == nil ? 0 : 1),
            .float4(Float(outline.glowPoint.x + padding), Float(outline.glowPoint.y + padding), Float(glow.radius), Float(outline.glow)),
            .float4(Float(outline.opacity), style.increaseContrast ? 1 : 0, mode, frosted ? Float(GlassRendering.fillKernelSigma(dark: style.dark)) : 0),
            .float4(margin, margin, Float(layer.width) - margin, Float(layer.height) - margin)
        )
    }
}
