import CoreGraphics
import Foundation

/// The Liquid Glass optical model on the CPU.
///
/// A line-by-line port of `spec/reference.mjs`, the executable form of
/// `spec/OPTICS.md`. The Metal shader evaluates the same functions per pixel;
/// these versions drive layout, interaction and the unit tests, which check
/// them against `spec/vectors.json`.
///
/// Units are points. Colors are sRGB gamma-encoded, unpremultiplied channels
/// in `0...1` unless a name says otherwise. Coordinates inside a surface are
/// measured from its center with y pointing down.
public enum GlassOptics {
    /// Depth of the refracting bevel inside the outline, in points.
    public static let bevel = 20.0
    /// Default optical travel at the outline, in points.
    public static let defaultRefraction = 60.0
    /// Continuous corners reach this far along each edge, relative to the radius.
    public static let continuousExtent = 1.528
    /// Superellipse exponent of a continuous corner (2 is a circular arc).
    public static let continuousExponent = 3.26
    /// Luma weights of SVG's `feColorMatrix type="saturate"`.
    public static let luma = SIMD3<Double>(0.213, 0.715, 0.072)

    // MARK: Shape

    /// Corner geometry of a glass outline.
    public struct ShapeParameters: Hashable, Sendable {
        /// How far the corner reaches along each edge, in points.
        public var extent: Double
        /// The superellipse exponent of the corner.
        public var exponent: Double
    }

    /// The signed distance field of an outline at one point.
    public struct Field: Hashable, Sendable {
        /// Signed distance to the outline in points, negative inside.
        public var distance: Double
        /// Outward unit normal, x component.
        public var nx: Double
        /// Outward unit normal, y component (y points down).
        public var ny: Double
    }

    /// Corner extent and exponent for a `width × height` outline.
    ///
    /// Large radii blend toward a circular capsule so animating the radius
    /// never jumps (`spec/OPTICS.md` §1).
    public static func shapeParameters(width: Double, height: Double, radius: GlassRadius) -> ShapeParameters {
        let half = max(0, min(width, height) / 2)
        let r: Double
        switch radius {
        case .capsule, .circle: return ShapeParameters(extent: half, exponent: 2)
        case .points(let value): r = max(0, value)
        }
        let raw = continuousExtent * r
        let extent = min(raw, half)
        let t = half > 0 ? smoothstep(0.75, 1, raw / half) : 1
        return ShapeParameters(extent: extent, exponent: mix(continuousExponent, 2, t))
    }

    /// Signed distance (negative inside) and outward normal of the outline at
    /// `(x, y)`, measured from the shape's center with y down.
    ///
    /// Straight edges are exact; superellipse corners use the first-order
    /// distance f / |∇f|, which is exact on the outline and for exponent 2.
    public static func shapeField(x: Double, y: Double, width: Double, height: Double, extent: Double, exponent: Double) -> Field {
        let sx: Double = x < 0 ? -1 : 1
        let sy: Double = y < 0 ? -1 : 1
        let qx = abs(x) - (width / 2 - extent)
        let qy = abs(y) - (height / 2 - extent)
        if qx > 0, qy > 0 {
            let n = exponent
            // Normalize by the larger component so pow() stays in range.
            let k = max(qx, qy)
            let m = k * pow(pow(qx / k, n) + pow(qy / k, n), 1 / n)
            if m < 1e-6 { return Field(distance: -extent, nx: sx * 0.5.squareRoot(), ny: sy * 0.5.squareRoot()) }
            let gx = pow(qx / m, n - 1)
            let gy = pow(qy / m, n - 1)
            let g = (gx * gx + gy * gy).squareRoot()
            return Field(distance: (m - extent) / g, nx: sx * gx / g, ny: sy * gy / g)
        }
        if qx > qy { return Field(distance: qx - extent, nx: sx, ny: 0) }
        return Field(distance: qy - extent, nx: 0, ny: sy)
    }

    /// The outline as a polygon, clockwise from the top edge's start, in a
    /// `width × height` box with its origin at the top-left corner.
    ///
    /// Each corner contributes `segments + 1` points on the parametric
    /// superellipse `x = e·sgn(cos t)|cos t|^(2/n)`, `y = e·sgn(sin t)|sin t|^(2/n)`.
    public static func shapeOutline(width: Double, height: Double, radius: GlassRadius, segments: Int = 24) -> [CGPoint] {
        let params = shapeParameters(width: width, height: height, radius: radius)
        let e = params.extent, n = params.exponent
        let corners: [(cx: Double, cy: Double, a0: Double, a1: Double)] = [
            (width - e, e, -Double.pi / 2, 0),
            (width - e, height - e, 0, Double.pi / 2),
            (e, height - e, Double.pi / 2, Double.pi),
            (e, e, Double.pi, 3 * Double.pi / 2),
        ]
        var points: [CGPoint] = []
        points.reserveCapacity(4 * (segments + 1))
        let count = max(1, segments)
        for corner in corners {
            for i in 0...count {
                let a = corner.a0 + (corner.a1 - corner.a0) * Double(i) / Double(count)
                let c = cos(a), s = sin(a)
                let px = e * sign(c) * pow(abs(c), 2 / n)
                let py = e * sign(s) * pow(abs(s), 2 / n)
                points.append(CGPoint(x: corner.cx + px, y: corner.cy + py))
            }
        }
        return points
    }

    // MARK: Refraction

    /// Displacement weight: 1 on the outline, falling to 0 at ``bevel`` inside.
    public static func edgeProfile(distance: Double, bevel: Double = GlassOptics.bevel) -> Double {
        let depth = sat(-distance / bevel)
        return 1 - sat(((2 - depth) * depth).squareRoot())
    }

    /// Where a glass pixel samples its backdrop, relative to itself.
    public static func refractionOffset(distance: Double, nx: Double, ny: Double, refraction: Double = defaultRefraction) -> CGVector {
        let e = edgeProfile(distance: distance)
        return CGVector(dx: -nx * e * refraction, dy: -ny * e * refraction)
    }

    /// Antialiased coverage of the outline; `pixel` is one device pixel in points.
    public static func coverage(distance: Double, pixel: Double) -> Double {
        sat(0.5 - distance / pixel)
    }

    // MARK: Color

    /// Luma of an sRGB color with the SVG saturate weights.
    public static func luma(_ c: SIMD3<Double>) -> Double {
        luma.x * c.x + luma.y * c.y + luma.z * c.z
    }

    /// SVG `feColorMatrix type="saturate"`.
    public static func saturate(_ c: SIMD3<Double>, _ s: Double) -> SIMD3<Double> {
        SIMD3(
            sat((0.213 + 0.787 * s) * c.x + (0.715 - 0.715 * s) * c.y + (0.072 - 0.072 * s) * c.z),
            sat((0.213 - 0.213 * s) * c.x + (0.715 + 0.285 * s) * c.y + (0.072 - 0.072 * s) * c.z),
            sat((0.213 - 0.213 * s) * c.x + (0.715 - 0.715 * s) * c.y + (0.072 + 0.928 * s) * c.z)
        )
    }

    /// Clear material: lifts shadows, rolls off highlights, adds a faint veil.
    public static func clearTone(_ v: Double, dark: Bool, nested: Bool = false) -> Double {
        let lift = dark ? 0.15 : 0.18
        let rolloff = dark ? 0.2 : 0
        let tone = v + lift * pow(1 - v, 3) - rolloff * pow(v, 3)
        let veil = dark ? 0.38 * v + 0.2 : 0.78 * v + 0.26
        let weight = dark ? 0.14 : 0.06
        let layerLight = dark && nested ? 0.08 * (1 - v) : 0
        return sat((1 - weight) * tone + weight * veil + layerLight)
    }

    /// Regular material: the calibrated per-channel curve, before saturation.
    public static func regularTone(_ v: Double, dark: Bool) -> Double {
        let m = RegularConstants(dark: dark)
        return sat(m.toneWeight * (m.a + m.b * v + m.q * v * v) + (1 - m.toneWeight) * v)
    }

    /// The full material color response on an unpremultiplied sRGB color.
    public static func materialColor(_ c: SIMD3<Double>, material: GlassMaterial, dark: Bool, nested: Bool = false) -> SIMD3<Double> {
        switch material {
        case .regular:
            let toned = SIMD3(regularTone(c.x, dark: dark), regularTone(c.y, dark: dark), regularTone(c.z, dark: dark))
            return saturate(toned, RegularConstants(dark: dark).chroma)
        case .clear:
            return SIMD3(clearTone(c.x, dark: dark, nested: nested), clearTone(c.y, dark: dark, nested: nested), clearTone(c.z, dark: dark, nested: nested))
        }
    }

    /// How opaque the pigment of tinted glass is.
    public static func tintBody(_ material: GlassMaterial) -> Double {
        material == .regular ? 0.78 : 0.68
    }

    /// Colored glass transmits luminance through the pigment.
    public static func tintColor(_ c: SIMD3<Double>, tint: SIMD3<Double>, material: GlassMaterial) -> SIMD3<Double> {
        let body = tintBody(material)
        let l = luma(c)
        let t = tint * (body + (1 - body) * l)
        return SIMD3(sat(t.x), sat(t.y), sat(t.z))
    }

    /// Gaussian σ of the Regular material's frost blur, in points.
    public static let frostSigma = 7.33

    /// Gaussian σ of the Regular material's wide fill blur, in points.
    public static func fillSigma(dark: Bool) -> Double { RegularConstants(dark: dark).fillSigma }

    /// Regular glass refracts a frosted backdrop: a σ 7.33 blur, brightened
    /// (light) or deepened (dark) by a wide fill blur. `frost` and `fill` are
    /// the two blurred backdrop colors at the same point.
    public static func frostColor(frost: SIMD3<Double>, fill: SIMD3<Double>, dark: Bool) -> SIMD3<Double> {
        let opacity = RegularConstants(dark: dark).fillOpacity
        let blended = dark ? pointwiseMin(frost, fill) : pointwiseMax(frost, fill)
        return opacity * blended + (1 - opacity) * frost
    }

    // MARK: Rim light

    /// The rim overlay painted over the glass: a specular line along the top
    /// and bottom, a soft inner diffuse glow, and a shade on the sides.
    /// - Returns: Premultiplied RGBA.
    public static func rimOverlay(distance d: Double, nx: Double, ny: Double, dark: Bool, pixel: Double, tint: SIMD3<Double>? = nil) -> SIMD4<Double> {
        let aa = max(pixel, 1e-6)
        let k: Double = dark ? 1 : 0
        let cover = sat(0.5 - d / aa)
        let spread = 1.08
        let directional = pow(sat((abs(ny) - cos(spread)) / (1 - cos(spread))), 1.25)
        let diffuseDir = pow(sat((abs(ny) - cos(spread * 0.65)) / (1 - cos(spread * 0.65))), 1.25)
        let stroke = sat((d + 0.9) / aa + 0.5) * sat(0.5 - d / aa)
        var specular = directional * stroke
        specular = specular / (1 + 0.5 * (1 - specular))
        let diffuse = diffuseDir * 0.18 * pow(1 - sat(-d / 4.5), 2) * cover
        let highlight = sat(specular * mix(1, 0.82, k) + diffuse * mix(1, 0.4, k))
        let adjusted = d - 0.5
        let falloff = 1 - sat(-adjusted / 0.5)
        let ramp = mix(falloff > 0 ? 1 : 0, falloff, 0.75)
        let outline = ramp * sat((adjusted + 0.5) / aa + 0.5) * sat(-adjusted / aa + 0.5)
        let cs = cos(0.98)
        let w1 = sat((nx - cs) / (1 - cs)) * outline
        let w2 = sat((-nx - cs) / (1 - cs)) * outline
        let shade = (w1 / (1 + 0.4 * (1 - w1)) + w2 / (1 + 0.4 * (1 - w2))) * mix(0.48, 0.6, k)
        let alpha = highlight + shade * (1 - highlight)
        if let tint {
            let rgb = highlight * (0.45 + 0.55 * tint)
            return SIMD4(rgb.x, rgb.y, rgb.z, alpha)
        }
        let light = mix(1, 0.82, k) * highlight
        return SIMD4(light, light, light, alpha)
    }

    // MARK: One glass pixel

    /// Composites one pixel of a glass surface.
    ///
    /// - Parameters:
    ///   - x: Pixel center relative to the surface center, in points.
    ///   - y: Pixel center relative to the surface center, y down.
    ///   - sample: The backdrop color (unpremultiplied RGBA) at the pixel
    ///     offset by `(dx, dy)`. Regular glass samples its frosted backdrop.
    ///   - base: The unrefracted backdrop at the pixel, premultiplied.
    /// - Returns: The premultiplied result.
    public static func glassPixel(
        x: Double, y: Double, width: Double, height: Double, radius: GlassRadius,
        material: GlassMaterial = .clear, dark: Bool = false, refraction: Double = defaultRefraction,
        tint: SIMD3<Double>? = nil, pixel: Double = 1.0 / 3.0, nested: Bool = false,
        sample: (_ dx: Double, _ dy: Double) -> SIMD4<Double>, base: SIMD4<Double>
    ) -> SIMD4<Double> {
        let params = shapeParameters(width: width, height: height, radius: radius)
        let f = shapeField(x: x, y: y, width: width, height: height, extent: params.extent, exponent: params.exponent)
        let cover = coverage(distance: f.distance, pixel: pixel)
        let o = refractionOffset(distance: f.distance, nx: f.nx, ny: f.ny, refraction: refraction)
        let src = sample(o.dx, o.dy)
        var rgb = materialColor(SIMD3(src.x, src.y, src.z), material: material, dark: dark, nested: nested)
        var a = src.w
        if let tint {
            rgb = tintColor(rgb, tint: tint, material: material)
            let body = tintBody(material)
            a = body + (1 - body) * a
        }
        let ga = a * cover
        let glass = SIMD4(rgb.x * ga, rgb.y * ga, rgb.z * ga, ga) + base * (1 - ga)
        let rim = rimOverlay(distance: f.distance, nx: f.nx, ny: f.ny, dark: dark, pixel: pixel, tint: tint)
        return rim + glass * (1 - rim.w)
    }

    // MARK: Interaction

    /// iOS-style rubber band: follows a short drag, then resists.
    public static func rubberBand(_ offset: Double, limit: Double, coefficient: Double = 0.55) -> Double {
        if limit <= 0 { return 0 }
        let m = abs(offset)
        return sign(offset) * limit * (1 - 1 / (m * coefficient / limit + 1))
    }

    /// How far a held surface may stretch toward the pointer before resisting.
    public static func stretchLimit(width: Double, height: Double) -> Double {
        let size = max(width, height)
        return size <= 72 ? 4 + 0.15 * size : min(10, 3 + 0.04 * size)
    }

    /// Press and drag response of a `width × height` surface.
    ///
    /// `press` is the press spring's value (0…1), `tx`/`ty` the drag spring
    /// values and `vx`/`vy` their velocities. `carriedX`/`carriedY` is the
    /// velocity of the surface itself while the application drags it.
    /// Volume is preserved: stretching one axis narrows the other.
    public static func pressScale(
        width: Double, height: Double, press: Double,
        tx: Double = 0, ty: Double = 0, vx: Double = 0, vy: Double = 0,
        carriedX: Double = 0, carriedY: Double = 0, reduced: Bool = false
    ) -> CGSize {
        if reduced { return CGSize(width: 1, height: 1) }
        let size = max(width, height)
        let grow = min(0.16, 8 / size) * press
        let cap = size <= 72 ? 0.2 : 0.12
        let ex = min(cap, 0.5 * abs(tx) / width + 0.00007 * abs(vx) + 0.00005 * abs(carriedX))
        let ey = min(cap, 0.5 * abs(ty) / height + 0.00007 * abs(vy) + 0.00005 * abs(carriedY))
        return CGSize(
            width: (1 + grow) * (1 + ex) / (1 + ey).squareRoot(),
            height: (1 + grow) * (1 + ey) / (1 + ex).squareRoot()
        )
    }

    /// The light that blooms under the finger of an interactive surface.
    public struct Glow: Hashable, Sendable {
        /// Radius at which the light falls to zero, in points.
        public var radius: Double
        /// Peak alpha of the white light.
        public var alpha: Double
        /// Glow level while a pointer hovers (pointer devices).
        public var hover: Double
    }

    /// Glow parameters for a `width × height` surface.
    public static func glow(width: Double, height: Double, dark: Bool) -> Glow {
        Glow(radius: clamp(max(width, height) * 0.9, 36, 180), alpha: dark ? 0.16 : 0.3, hover: 0.35)
    }

    // MARK: Progressive blur

    /// Number of blur levels above the sharp backdrop.
    public static let progressiveLevels = 6

    /// Gaussian σ of progressive blur level `level` (0 is sharp).
    public static func progressiveLevelBlur(level: Int, blur: Double) -> Double {
        blur * pow(Double(level) / Double(progressiveLevels), 2)
    }

    /// Weight of blur level `level` at depth `u` (0 = sharp side, 1 = the edge).
    /// The weights of all levels sum to 1.
    public static func progressiveWeight(level: Int, u: Double) -> Double {
        let s = sat(u)
        let eased = s * s * (3 - 2 * s)
        return max(0, 1 - abs(Double(progressiveLevels) * eased - Double(level)))
    }

    // MARK: Helpers

    static func clamp(_ x: Double, _ lo: Double, _ hi: Double) -> Double { min(hi, max(lo, x)) }
    static func sat(_ x: Double) -> Double { clamp(x, 0, 1) }
    static func mix(_ a: Double, _ b: Double, _ t: Double) -> Double { a + (b - a) * t }
    static func smoothstep(_ e0: Double, _ e1: Double, _ x: Double) -> Double {
        let t = sat((x - e0) / (e1 - e0))
        return t * t * (3 - 2 * t)
    }
    static func sign(_ x: Double) -> Double { x > 0 ? 1 : x < 0 ? -1 : 0 }

    /// The Regular material's calibrated constants (`spec/OPTICS.md` §3).
    struct RegularConstants {
        let fillSigma, fillOpacity, a, b, q, toneWeight, chroma: Double
        init(dark: Bool) {
            if dark {
                (fillSigma, fillOpacity, a, b, q, toneWeight, chroma) = (21.8, 0.65, 0.105, 0.83, -0.2091, 0.9, 1.0648)
            } else {
                (fillSigma, fillOpacity, a, b, q, toneWeight, chroma) = (23, 0.34, 0.36, 0.67, -0.1113, 0.8, 1.1031)
            }
        }
    }
}
