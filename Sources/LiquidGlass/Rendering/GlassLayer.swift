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

    /// Outlines thinner than this, in points, draw no glass. The rim's
    /// specular stroke reaches 0.9 points inside the outline (plus a pixel
    /// of antialiasing) from both sides, so a thinner outline has no face to
    /// refract, and its rim alone would draw a stray line or dot.
    static let minimumSide: CGFloat = 3

    /// The fill kernel's outer ring, in multiples of its σ (see `lgFill`).
    static let fillReach = 2.6131

    /// How far the shader reads from the pixel it shades: the refraction,
    /// scaled by up to 1.5 for press growth and stretch, plus the fill
    /// kernel's outer ring on Regular glass. SwiftUI renders that much of the
    /// layer around what is visible, so a surface clipped by an ancestor (a
    /// scroll view, a mask, the window's edge) still refracts true content
    /// at the clip.
    static func sampleReach(refraction: Double, frosted: Bool, dark: Bool) -> CGSize {
        let fill = frosted ? fillReach * fillKernelSigma(dark: dark) : 0
        let reach = CGFloat((refraction * 1.5 + fill + 2).rounded(.up))
        return CGSize(width: reach, height: reach)
    }
}

/// The optical layer of one glass surface: a copy of the scene's backdrop,
/// frosted for the Regular material, run through the glass shader. It is
/// drawn beneath the surface's content and extends `padding` points past the
/// surface so the outline can grow and stretch.
///
/// The size is animatable. Layout (and so the geometry reader that measures
/// the surface) only knows a surface's final size, while SwiftUI animates the
/// content's frame toward it; animating the size here with the same
/// transaction keeps the outline on the content while it grows or shrinks,
/// instead of jumping to the final outline at once.
struct GlassLayer: View, Animatable {
    var size: CGSize
    let style: GlassRenderStyle
    let outline: GlassOutlineState
    let padding: CGFloat
    /// Views drawn over the backdrop copy, in the surface's local points, so
    /// the glass refracts them too (a slider's track under its thumb).
    let localBackdrop: AnyView?

    @Environment(\.glassBackdrop) private var backdrop

    var animatableData: AnimatablePair<CGFloat, CGFloat> {
        get { AnimatablePair(size.width, size.height) }
        set { size = CGSize(width: newValue.first, height: newValue.second) }
    }

    var body: some View {
        // Placed by a left-to-right parent (see GlassSurfaceCore), so the
        // offset, the outline and the shader's positions are all physical.
        let layer = CGSize(width: size.width + 2 * padding, height: size.height + 2 * padding)
        optics(layer: layer)
            .frame(width: layer.width, height: layer.height)
            .offset(x: -padding, y: -padding)
            .allowsHitTesting(false)
            .accessibilityHidden(true)
    }

    @ViewBuilder
    private func optics(layer: CGSize) -> some View {
        let glass = parameters
        if let backdrop {
            if style.reduceTransparency {
                Self.shaderCanvas
                    .frame(width: layer.width, height: layer.height)
                    .layerEffect(glass.shader(layer: layer, mode: 1), maxSampleOffset: .zero)
            } else {
                let reach = GlassRendering.sampleReach(refraction: style.refraction, frosted: frosted, dark: style.dark)
                copy(of: backdrop, layer: layer)
                    .layerEffect(glass.shader(layer: layer, mode: 0), maxSampleOffset: reach)
            }
        } else {
            fallback(layer: layer)
        }
    }

    private var parameters: GlassShaderParameters {
        GlassShaderParameters(style: style, outline: outline, padding: padding, frosted: frosted)
    }

    private var frosted: Bool { style.material == .regular && !style.reduceTransparency }

    /// Content for a layer whose shader doesn't sample it. SwiftUI skips
    /// layer effects on empty views such as `Color.clear`; the shader
    /// replaces every pixel of this one.
    private static let shaderCanvas = Color.black

    /// The ground under the backdrop copy. SwiftUI runs a layer effect only
    /// where its content draws, so over a transparent or absent backdrop
    /// (letterboxing, a backdrop narrower than the scene, a clear window) the
    /// glass and its rim would be cut off. This draws everywhere, and at
    /// alpha 1/255 the shader still sees an empty backdrop there.
    private static let copyCanvas = Color.black.opacity(1.0 / 255.0)

    @ViewBuilder
    private func copy(of backdrop: GlassBackdrop, layer: CGSize) -> some View {
        let clipped = Self.copyCanvas
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
            // Flattened into one image: the layer effect (and the frost blur)
            // would otherwise run over each drawn piece separately, the
            // canvas and every part of the backdrop, painting the rim once
            // for each.
            .compositingGroup()
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
            Self.shaderCanvas
                .frame(width: layer.width, height: layer.height)
                .layerEffect(parameters.shader(layer: layer, mode: 2), maxSampleOffset: .zero)
        }
    }

    private var outlineRect: CGRect {
        outline.rect ?? CGRect(origin: .zero, size: size)
    }
}

/// The glass shader's arguments for one surface.
struct GlassShaderParameters {
    let style: GlassRenderStyle
    let outline: GlassOutlineState
    let padding: CGFloat
    let frosted: Bool

    /// The shader for a layer of `layer` points: the surface fills it, less
    /// the padding on every side, unless an explicit outline is set.
    func shader(layer: CGSize, mode: Float) -> Shader {
        let rect = outline.rect ?? CGRect(
            x: 0, y: 0, width: max(0, layer.width - 2 * padding), height: max(0, layer.height - 2 * padding)
        )
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
