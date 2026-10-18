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

// Kotlin port of the parts of spec/reference.mjs the renderer runs on the CPU:
// shape parameters and outlines, material constants, the menus' legibility
// backing (which the fallback material mixes into its fill), interaction
// response and progressive blur levels. Geometry is in dp, colors are sRGB
// gamma-encoded.
// The per-pixel functions run in the AGSL shader (GlassShaders.kt); their CPU
// reference lives with the tests (src/sharedTest), which check both against
// spec/vectors.json.

/** Full optical travel at the outline, in dp: what a surface that sets none uses once its shorter side is 80 dp or more. */
internal const val DEFAULT_REFRACTION: Float = 60f

/**
 * Refraction a surface uses when it sets none, in dp: the full [DEFAULT_REFRACTION],
 * limited for small controls to three quarters of their shorter side so they never
 * pull in backdrop from far outside their outline. Measured on the resting size, so
 * a press does not change it.
 */
internal fun defaultRefraction(width: Float, height: Float): Float =
    min(DEFAULT_REFRACTION, 0.75f * max(0f, min(width, height)))

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
// Color

internal const val LUMA_R: Float = 0.213f
internal const val LUMA_G: Float = 0.715f
internal const val LUMA_B: Float = 0.072f

internal fun luma(r: Float, g: Float, b: Float): Float = LUMA_R * r + LUMA_G * g + LUMA_B * b

/** How much of the pigment colored glass shows regardless of the light behind it. */
internal fun tintBody(regular: Boolean): Float = if (regular) 0.78f else 0.68f

/** WCAG 2 relative luminance of a gamma-encoded sRGB color. */
internal fun relativeLuminance(r: Float, g: Float, b: Float): Float {
    fun linear(c: Float): Float = if (c <= 0.04045f) c / 12.92f else Math.pow((c + 0.055) / 1.055, 2.4).toFloat()
    return 0.2126f * linear(r) + 0.7152f * linear(g) + 0.0722f * linear(b)
}

/**
 * Whether labels on glass tinted with the gamma-encoded sRGB color ([r], [g], [b]) take the dark foreground
 * (`#111113`) rather than white, in either appearance: whichever of the two has the higher WCAG contrast with the
 * tint as the glass draws it over a mid-tone backdrop, `tint · (body + (1 − body)·0.5)` (`spec/OPTICS.md` §8).
 */
internal fun darkLabelOnTint(r: Float, g: Float, b: Float, regular: Boolean): Boolean {
    val body = tintBody(regular)
    val pass = body + (1f - body) * 0.5f
    val glass = relativeLuminance(r * pass, g * pass, b * pass)
    val dark = relativeLuminance(17f / 255f, 17f / 255f, 19f / 255f)
    return (glass + 0.05f) / (dark + 0.05f) > 1.05f / (glass + 0.05f)
}

/**
 * How far a menu panel mixes its glass toward the [legibilityBacking], after the material color and tint
 * (`spec/OPTICS.md` §3, "Legibility backing"): labels on it keep at least 5:1 over any backdrop. Every other
 * surface uses 0.
 */
internal const val MENU_LEGIBILITY: Float = 0.5f

/** The solid backing of the legibility mix, gamma-encoded sRGB: `#F5F5F7` light, `#1C1C1E` dark. */
internal fun legibilityBacking(dark: Boolean): FloatArray =
    if (dark) floatArrayOf(0x1C / 255f, 0x1C / 255f, 0x1E / 255f) else floatArrayOf(0xF5 / 255f, 0xF5 / 255f, 0xF7 / 255f)

/** [rgb] (unpremultiplied sRGB) mixed toward the [legibilityBacking] by [amount]; 0 leaves it unchanged. */
internal fun legibilityColor(rgb: FloatArray, dark: Boolean, amount: Float): FloatArray {
    val backing = legibilityBacking(dark)
    return FloatArray(3) { rgb[it] + (backing[it] - rgb[it]) * amount }
}

/** Gaussian σ of the Regular frost, in dp. */
internal const val FROST_SIGMA: Float = 7.33f

/** Gaussian σ of the wide fill blur, in dp. */
internal fun frostFillSigma(dark: Boolean): Float = if (dark) 21.8f else 23f

/** Weight of the fill blur in the Regular frost. */
internal fun frostFillOpacity(dark: Boolean): Float = if (dark) 0.65f else 0.34f

// ---------------------------------------------------------------------------
// Interaction

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

/** The smoothstep easing that maps depth to blur level. */
internal fun progressiveEase(u: Float): Float = u * u * (3f - 2f * u)

/** Gaussian σ of blur level [level] (0 is sharp) for a total [blur]. */
internal fun progressiveLevelBlur(level: Int, blur: Float): Float {
    val t = level.toFloat() / PROGRESSIVE_LEVELS
    return blur * t * t
}

/**
 * Coverage of level [level] when the levels are painted bottom-up with source-over.
 * Painting level i at this alpha over levels 0..i-1 gives every level exactly the
 * spec's weight `max(0, 1 - |6·ease(u) - i|)`.
 */
internal fun progressiveOverAlpha(level: Int, u: Float): Float =
    sat(PROGRESSIVE_LEVELS * progressiveEase(sat(u)) - level + 1f)
