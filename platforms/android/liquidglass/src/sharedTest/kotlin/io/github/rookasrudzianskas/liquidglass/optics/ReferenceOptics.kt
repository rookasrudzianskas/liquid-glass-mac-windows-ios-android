package io.github.rookasrudzianskas.liquidglass.optics

import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow
import kotlin.math.sqrt

// The per-pixel half of spec/reference.mjs, on the CPU: the reference the
// tests hold the AGSL shader to. GoldenVectorsTest checks it against
// spec/vectors.json; GlassRenderingTest checks the GPU output against it.
// Shared by the unit and instrumented test source sets.

/** Depth of the refracting bevel inside the outline, in dp. */
internal const val BEVEL: Float = 20f

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

/**
 * Composites one pixel of a glass surface, exactly as the shader does.
 * [x], [y] is the pixel center relative to the surface center (y down);
 * [sample] returns the unpremultiplied backdrop r, g, b, a at the pixel offset
 * by (dx, dy) (Regular glass samples its frosted backdrop); [base] is the
 * unrefracted backdrop at the pixel, premultiplied. [legibility] mixes the glass
 * toward the legibility backing ([MENU_LEGIBILITY] on menu panels, 0 elsewhere).
 * Returns premultiplied r, g, b, a.
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
    legibility: Float = 0f,
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
    if (legibility > 0f) rgb = legibilityColor(rgb, dark, legibility)
    val ga = a * cover
    val glass = floatArrayOf(rgb[0] * ga, rgb[1] * ga, rgb[2] * ga, ga)
    for (i in 0..3) glass[i] += base[i] * (1f - ga)
    val rim = rimOverlay(f.distance, f.nx, f.ny, dark, pixel, tint)
    return FloatArray(4) { rim[it] + glass[it] * (1f - rim[3]) }
}

/**
 * The system colors spec/generate-vectors.mjs holds tinted menus to (legibility.worstCase.systemTintsWithHighlight),
 * gamma-encoded sRGB: blue, green, red, orange, yellow, purple, pink, indigo, teal and mint, in their light-appearance
 * variants and then their dark-appearance ones.
 */
internal val SYSTEM_TINTS: List<FloatArray> =
    listOf(
        0x007AFF, 0x34C759, 0xFF3B30, 0xFF9500, 0xFFCC00, 0xAF52DE, 0xFF2D55, 0x5856D6, 0x5AC8FA, 0x00C7BE,
        0x0A84FF, 0x30D158, 0xFF453A, 0xFF9F0A, 0xFFD60A, 0xBF5AF2, 0xFF375F, 0x5E5CE6, 0x64D2FF, 0x66D4CF,
    ).map { rgb ->
        floatArrayOf(((rgb shr 16) and 0xFF) / 255f, ((rgb shr 8) and 0xFF) / 255f, (rgb and 0xFF) / 255f)
    }

/** The menus' contrast floors (spec/OPTICS.md §3), highlighted rows included: untinted, and tinted with [SYSTEM_TINTS]. */
internal const val UNTINTED_MENU_FLOOR: Float = 5.2f
internal const val TINTED_MENU_FLOOR: Float = 5.0f

/** [glass] with the menu row highlight: [label] mixed in by [MENU_HIGHLIGHT] (spec/OPTICS.md §3). */
internal fun highlighted(glass: FloatArray, label: FloatArray): FloatArray = FloatArray(3) { mix(glass[it], label[it], MENU_HIGHLIGHT) }

/** WCAG 2 contrast ratio of two gamma-encoded sRGB colors (r, g, b), lighter over darker. */
internal fun contrastRatio(a: FloatArray, b: FloatArray): Float {
    val x = relativeLuminance(a[0], a[1], a[2]) + 0.05f
    val y = relativeLuminance(b[0], b[1], b[2]) + 0.05f
    return max(x, y) / min(x, y)
}

/** Weight of blur level [level] at depth [u] (0 = sharp side, 1 = the edge). Weights sum to 1. */
internal fun progressiveWeight(level: Int, u: Float): Float =
    max(0f, 1f - abs(PROGRESSIVE_LEVELS * progressiveEase(sat(u)) - level))

private const val SQRT1_2: Float = 0.70710678f
