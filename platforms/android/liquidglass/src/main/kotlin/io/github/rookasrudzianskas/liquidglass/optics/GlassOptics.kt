package io.github.rookasrudzianskas.liquidglass.optics

import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow
import kotlin.math.sign
import kotlin.math.sin
import kotlin.math.sqrt

// Kotlin port of spec/reference.mjs, the canonical optical model shared by every
// Liquid Glass Engineering platform. Geometry is in dp, colors are sRGB
// gamma-encoded and unpremultiplied unless a name says otherwise. The AGSL
// shader in GlassShaders.kt evaluates the per-pixel functions on the GPU; these
// CPU versions feed its uniforms and are checked against spec/vectors.json.

/** Depth of the refracting bevel inside the outline, in dp. */
internal const val BEVEL: Float = 20f

/** Default optical travel at the outline, in dp. */
internal const val DEFAULT_REFRACTION: Float = 60f

/** Continuous corners reach this far along each edge, relative to the radius. */
internal const val CONTINUOUS_EXTENT: Float = 1.528f

/** Superellipse exponent of a continuous corner (2 is a circular arc). */
internal const val CONTINUOUS_EXPONENT: Float = 3.26f

internal fun sat(x: Float): Float = x.coerceIn(0f, 1f)

internal fun mix(a: Float, b: Float, t: Float): Float = a + (b - a) * t

internal fun smoothstep(edge0: Float, edge1: Float, x: Float): Float {
    val t = sat((x - edge0) / (edge1 - edge0))
    return t * t * (3f - 2f * t)
}

// ---------------------------------------------------------------------------
// Shape

/** Corner geometry of an outline: the corner [extent] along each edge and the superellipse [exponent]. */
internal data class ShapeParams(val extent: Float, val exponent: Float)

/**
 * Corner geometry of a `width × height` outline. A null [radius] is a capsule
 * (also used for circles). Large radii blend toward a circular capsule so
 * animating the radius never jumps.
 */
internal fun shapeParams(width: Float, height: Float, radius: Float?): ShapeParams {
    val half = max(0f, min(width, height) / 2f)
    if (radius == null) return ShapeParams(half, 2f)
    require(radius.isFinite() && radius >= 0f) { "radius must be finite and nonnegative" }
    val raw = CONTINUOUS_EXTENT * radius
    val extent = min(raw, half)
    val t = if (half > 0f) smoothstep(0.75f, 1f, raw / half) else 1f
    return ShapeParams(extent, mix(CONTINUOUS_EXPONENT, 2f, t))
}

/** Signed distance (negative inside) and outward unit normal of the outline at a point. */
internal data class ShapeField(val distance: Float, val nx: Float, val ny: Float)

/**
 * Signed distance and normal at (x, y), measured from the shape's center with
 * y pointing down. Straight edges are exact; superellipse corners use the
 * first-order distance f / |∇f|.
 */
internal fun shapeField(x: Float, y: Float, width: Float, height: Float, extent: Float, exponent: Float): ShapeField {
    val sx = if (x < 0f) -1f else 1f
    val sy = if (y < 0f) -1f else 1f
    val qx = abs(x) - (width / 2f - extent)
    val qy = abs(y) - (height / 2f - extent)
    if (qx > 0f && qy > 0f) {
        val n = exponent
        // Normalize by the larger component so pow() stays well inside float range.
        val k = max(qx, qy)
        val m = k * ((qx / k).pow(n) + (qy / k).pow(n)).pow(1f / n)
        if (m < 1e-6f) return ShapeField(-extent, sx * SQRT1_2, sy * SQRT1_2)
        val gx = (qx / m).pow(n - 1f)
        val gy = (qy / m).pow(n - 1f)
        val g = sqrt(gx * gx + gy * gy)
        return ShapeField((m - extent) / g, sx * gx / g, sy * gy / g)
    }
    if (qx > qy) return ShapeField(qx - extent, sx, 0f)
    return ShapeField(qy - extent, 0f, sy)
}

/**
 * Outline polygon, clockwise from the top edge's start, in a box whose origin
 * is its top-left corner (y down): `segments + 1` points per corner.
 * Returned as interleaved x, y pairs.
 */
internal fun shapeOutline(width: Float, height: Float, radius: Float?, segments: Int = 24): FloatArray {
    val (extent, exponent) = shapeParams(width, height, radius)
    // Double precision keeps cos(π/2)^(2/n) from leaking float noise into the corners.
    val e = extent.toDouble()
    val n = exponent.toDouble()
    val w = width.toDouble()
    val h = height.toDouble()
    val points = FloatArray(4 * (segments + 1) * 2)
    var index = 0
    val corners = arrayOf(
        doubleArrayOf(w - e, e, -PI / 2, 0.0),
        doubleArrayOf(w - e, h - e, 0.0, PI / 2),
        doubleArrayOf(e, h - e, PI / 2, PI),
        doubleArrayOf(e, e, PI, 3 * PI / 2),
    )
    for ((cx, cy, a0, a1) in corners) {
        for (i in 0..segments) {
            val a = a0 + (a1 - a0) * i / segments
            val c = cos(a)
            val s = sin(a)
            points[index++] = (cx + e * sign(c) * abs(c).pow(2 / n)).toFloat()
            points[index++] = (cy + e * sign(s) * abs(s).pow(2 / n)).toFloat()
        }
    }
    return points
}

// ---------------------------------------------------------------------------
// Refraction

/** Displacement weight: 1 on the outline, falling to 0 at [BEVEL] inside. */
internal fun edgeProfile(distance: Float, bevel: Float = BEVEL): Float {
    val depth = sat(-distance / bevel)
    return 1f - sat(sqrt((2f - depth) * depth))
}

/** Where a glass pixel samples its backdrop, relative to itself, in dp. */
internal fun refractionOffset(distance: Float, nx: Float, ny: Float, refraction: Float = DEFAULT_REFRACTION): Pair<Float, Float> {
    val e = edgeProfile(distance)
    return Pair(-nx * e * refraction, -ny * e * refraction)
}

/** Antialiased coverage of the outline; [pixel] is one device pixel in dp. */
internal fun coverage(distance: Float, pixel: Float): Float = sat(0.5f - distance / pixel)

// ---------------------------------------------------------------------------
// Color

internal const val LUMA_R: Float = 0.213f
internal const val LUMA_G: Float = 0.715f
internal const val LUMA_B: Float = 0.072f

internal fun luma(r: Float, g: Float, b: Float): Float = LUMA_R * r + LUMA_G * g + LUMA_B * b

/** SVG feColorMatrix type="saturate". */
internal fun saturate(rgb: FloatArray, s: Float): FloatArray {
    val (r, g, b) = rgb
    return floatArrayOf(
        sat((0.213f + 0.787f * s) * r + (0.715f - 0.715f * s) * g + (0.072f - 0.072f * s) * b),
        sat((0.213f - 0.213f * s) * r + (0.715f + 0.285f * s) * g + (0.072f - 0.072f * s) * b),
        sat((0.213f - 0.213f * s) * r + (0.715f - 0.715f * s) * g + (0.072f + 0.928f * s) * b),
    )
}

/** Clear material: lift shadows, roll off highlights, add a faint veil. */
internal fun clearTone(v: Float, dark: Boolean, nested: Boolean = false): Float {
    val lift = if (dark) 0.15f else 0.18f
    val rolloff = if (dark) 0.2f else 0f
    val inv = 1f - v
    val tone = v + lift * inv * inv * inv - rolloff * v * v * v
    val veil = if (dark) 0.38f * v + 0.2f else 0.78f * v + 0.26f
    val weight = if (dark) 0.14f else 0.06f
    val layerLight = if (dark && nested) 0.08f * inv else 0f
    return sat((1f - weight) * tone + weight * veil + layerLight)
}

/** Regular material: the calibrated per-channel curve, before saturation. */
internal fun regularTone(v: Float, dark: Boolean): Float {
    val a = if (dark) 0.105f else 0.36f
    val b = if (dark) 0.83f else 0.67f
    val q = if (dark) -0.2091f else -0.1113f
    val weight = if (dark) 0.9f else 0.8f
    return sat(weight * (a + b * v + q * v * v) + (1f - weight) * v)
}

/** Saturation boost the Regular material applies after its tone curve. */
internal fun regularChroma(dark: Boolean): Float = if (dark) 1.0648f else 1.1031f

/** Full material color response on an unpremultiplied sRGB color. */
internal fun materialColor(rgb: FloatArray, regular: Boolean, dark: Boolean, nested: Boolean = false): FloatArray =
    if (regular) {
        saturate(floatArrayOf(regularTone(rgb[0], dark), regularTone(rgb[1], dark), regularTone(rgb[2], dark)), regularChroma(dark))
    } else {
        floatArrayOf(clearTone(rgb[0], dark, nested), clearTone(rgb[1], dark, nested), clearTone(rgb[2], dark, nested))
    }

/** How much of the pigment colored glass shows regardless of the light behind it. */
internal fun tintBody(regular: Boolean): Float = if (regular) 0.78f else 0.68f

/** Colored glass transmits luminance through the pigment. */
internal fun tintColor(rgb: FloatArray, tint: FloatArray, regular: Boolean): FloatArray {
    val body = tintBody(regular)
    val l = luma(rgb[0], rgb[1], rgb[2])
    return FloatArray(3) { sat(tint[it] * (body + (1f - body) * l)) }
}

/** Regular glass: blend the σ 7.33 [frost] with the wide [fill] blur at the same point. */
internal fun frostColor(frost: FloatArray, fill: FloatArray, dark: Boolean): FloatArray {
    val opacity = frostFillOpacity(dark)
    return FloatArray(3) {
        val blended = if (dark) min(frost[it], fill[it]) else max(frost[it], fill[it])
        opacity * blended + (1f - opacity) * frost[it]
    }
}

/** Gaussian σ of the Regular frost, in dp. */
internal const val FROST_SIGMA: Float = 7.33f

/** Gaussian σ of the wide fill blur, in dp. */
internal fun frostFillSigma(dark: Boolean): Float = if (dark) 21.8f else 23f

/** Weight of the fill blur in the Regular frost. */
internal fun frostFillOpacity(dark: Boolean): Float = if (dark) 0.65f else 0.34f

// ---------------------------------------------------------------------------
// Rim light

/**
 * The rim overlay painted over the glass: a specular line along the top and
 * bottom, a soft inner diffuse glow, and a shade on the sides.
 * Returns premultiplied r, g, b, a.
 */
internal fun rimOverlay(distance: Float, nx: Float, ny: Float, dark: Boolean, pixel: Float, tint: FloatArray? = null): FloatArray {
    val d = distance
    val aa = max(pixel, 1e-6f)
    val k = if (dark) 1f else 0f
    val cover = sat(0.5f - d / aa)
    val spread = 1.08f
    val directional = sat((abs(ny) - cos(spread)) / (1f - cos(spread))).pow(1.25f)
    val diffuseDir = sat((abs(ny) - cos(spread * 0.65f)) / (1f - cos(spread * 0.65f))).pow(1.25f)
    val stroke = sat((d + 0.9f) / aa + 0.5f) * sat(0.5f - d / aa)
    var specular = directional * stroke
    specular /= 1f + 0.5f * (1f - specular)
    val inner = 1f - sat(-d / 4.5f)
    val diffuse = diffuseDir * 0.18f * inner * inner * cover
    val highlight = sat(specular * mix(1f, 0.82f, k) + diffuse * mix(1f, 0.4f, k))
    val adjusted = d - 0.5f
    val falloff = 1f - sat(-adjusted / 0.5f)
    val ramp = mix(if (falloff > 0f) 1f else 0f, falloff, 0.75f)
    val outline = ramp * sat((adjusted + 0.5f) / aa + 0.5f) * sat(-adjusted / aa + 0.5f)
    val cs = cos(0.98f)
    val w1 = sat((nx - cs) / (1f - cs)) * outline
    val w2 = sat((-nx - cs) / (1f - cs)) * outline
    val shade = (w1 / (1f + 0.4f * (1f - w1)) + w2 / (1f + 0.4f * (1f - w2))) * mix(0.48f, 0.6f, k)
    val alpha = highlight + shade * (1f - highlight)
    if (tint != null) {
        return floatArrayOf(
            highlight * (0.45f + 0.55f * tint[0]),
            highlight * (0.45f + 0.55f * tint[1]),
            highlight * (0.45f + 0.55f * tint[2]),
            alpha,
        )
    }
    val light = mix(1f, 0.82f, k) * highlight
    return floatArrayOf(light, light, light, alpha)
}

// ---------------------------------------------------------------------------
// One glass pixel

/**
 * Composites one pixel of a glass surface, exactly as the shader does.
 * [x], [y] is the pixel center relative to the surface center (y down);
 * [sample] returns the unpremultiplied backdrop r, g, b, a at the pixel offset
 * by (dx, dy) (Regular glass samples its frosted backdrop); [base] is the
 * unrefracted backdrop at the pixel, premultiplied. Returns premultiplied r, g, b, a.
 */
internal fun glassPixel(
    x: Float,
    y: Float,
    width: Float,
    height: Float,
    radius: Float?,
    regular: Boolean,
    dark: Boolean,
    refraction: Float,
    pixel: Float,
    tint: FloatArray?,
    nested: Boolean = false,
    base: FloatArray,
    sample: (dx: Float, dy: Float) -> FloatArray,
): FloatArray {
    val (extent, exponent) = shapeParams(width, height, radius)
    val f = shapeField(x, y, width, height, extent, exponent)
    val cover = coverage(f.distance, pixel)
    val (dx, dy) = refractionOffset(f.distance, f.nx, f.ny, refraction)
    val src = sample(dx, dy)
    var rgb = materialColor(src, regular, dark, nested)
    var a = src[3]
    if (tint != null) {
        rgb = tintColor(rgb, tint, regular)
        val body = tintBody(regular)
        a = body + (1f - body) * a
    }
    val ga = a * cover
    val glass = floatArrayOf(rgb[0] * ga, rgb[1] * ga, rgb[2] * ga, ga)
    for (i in 0..3) glass[i] += base[i] * (1f - ga)
    val rim = rimOverlay(f.distance, f.nx, f.ny, dark, pixel, tint)
    return FloatArray(4) { rim[it] + glass[it] * (1f - rim[3]) }
}

// ---------------------------------------------------------------------------
// Interaction

/** Largest share of its size an interactive surface grows under a press. */
internal fun pressGrow(width: Float, height: Float): Float = min(0.16f, 8f / max(width, height))

/** How far a held surface may stretch toward the pointer before resisting, in dp. */
internal fun stretchLimit(width: Float, height: Float): Float {
    val size = max(width, height)
    return if (size <= 72f) 4f + 0.15f * size else min(10f, 3f + 0.04f * size)
}

/** iOS-style rubber band: follows a short drag, then resists. */
internal fun rubberBand(offset: Float, limit: Float, coefficient: Float = 0.55f): Float {
    if (limit <= 0f) return 0f
    val m = abs(offset)
    return sign(offset) * limit * (1f - 1f / (m * coefficient / limit + 1f))
}

/** Horizontal and vertical scale of a pressed, dragged surface. */
internal data class PressScale(val sx: Float, val sy: Float)

/**
 * Press and drag response. [press] is the press spring's value (0..1),
 * [tx]/[ty] the drag spring values and [vx]/[vy] their velocities (dp/s).
 * Volume is preserved: stretching one axis narrows the other.
 */
internal fun pressScale(
    width: Float,
    height: Float,
    press: Float,
    tx: Float = 0f,
    ty: Float = 0f,
    vx: Float = 0f,
    vy: Float = 0f,
    reduced: Boolean = false,
): PressScale {
    if (reduced || width <= 0f || height <= 0f) return PressScale(1f, 1f)
    val size = max(width, height)
    val grow = min(0.16f, 8f / size) * press
    val cap = if (size <= 72f) 0.2f else 0.12f
    val ex = min(cap, 0.5f * abs(tx) / width + 0.00007f * abs(vx))
    val ey = min(cap, 0.5f * abs(ty) / height + 0.00007f * abs(vy))
    return PressScale(
        sx = (1f + grow) * (1f + ex) / sqrt(1f + ey),
        sy = (1f + grow) * (1f + ey) / sqrt(1f + ex),
    )
}

/** Light that blooms under the finger of an interactive surface. */
internal data class GlowParams(val radius: Float, val alpha: Float, val hover: Float)

internal fun glow(width: Float, height: Float, dark: Boolean): GlowParams =
    GlowParams(radius = (max(width, height) * 0.9f).coerceIn(36f, 180f), alpha = if (dark) 0.16f else 0.3f, hover = HOVER_GLOW)

/** Glow target while a mouse hovers an interactive surface. */
internal const val HOVER_GLOW: Float = 0.35f

// ---------------------------------------------------------------------------
// Progressive blur

/** Number of blur levels above the sharp backdrop. */
internal const val PROGRESSIVE_LEVELS: Int = 6

private fun ease(u: Float): Float = u * u * (3f - 2f * u)

/** Gaussian σ of blur level [level] (0 is sharp) for a total [blur]. */
internal fun progressiveLevelBlur(level: Int, blur: Float): Float {
    val t = level.toFloat() / PROGRESSIVE_LEVELS
    return blur * t * t
}

/** Weight of blur level [level] at depth [u] (0 = sharp side, 1 = the edge). Weights sum to 1. */
internal fun progressiveWeight(level: Int, u: Float): Float =
    max(0f, 1f - abs(PROGRESSIVE_LEVELS * ease(sat(u)) - level))

/**
 * Coverage of level [level] when the levels are painted bottom-up with source-over:
 * painting level i at this alpha over levels 0..i-1 reproduces [progressiveWeight] exactly.
 */
internal fun progressiveOverAlpha(level: Int, u: Float): Float =
    sat(PROGRESSIVE_LEVELS * ease(sat(u)) - level + 1f)

private const val SQRT1_2: Float = 0.70710678f
