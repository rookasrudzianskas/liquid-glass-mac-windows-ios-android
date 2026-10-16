package io.github.rookasrudzianskas.liquidglass.internal

import android.graphics.BlendMode
import android.graphics.ColorMatrix
import android.graphics.ColorMatrixColorFilter
import android.graphics.LinearGradient
import android.graphics.RenderEffect
import android.graphics.RuntimeShader
import android.graphics.Shader
import androidx.annotation.RequiresApi
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.isSpecified
import io.github.rookasrudzianskas.liquidglass.optics.FROST_SIGMA
import io.github.rookasrudzianskas.liquidglass.optics.frostFillOpacity
import io.github.rookasrudzianskas.liquidglass.optics.frostFillSigma
import io.github.rookasrudzianskas.liquidglass.optics.progressiveOverAlpha
import io.github.rookasrudzianskas.liquidglass.optics.shapeParams

/**
 * Builds the RenderEffect that draws every glass surface of a scene onto its
 * content layer (Android 13+):
 *
 * ```
 * sharp  = glass shader (Clear and solid glass) on the content
 * frost  = glass shader (Regular glass only) on
 *          mix(blur σ7.33, max|min(blur σ7.33, blur σ23|21.8), fillOpacity)
 * result = frost source-over sharp
 * ```
 *
 * The frost is exact: Skia blurs and blends in the working color space, which
 * is sRGB on ordinary windows, matching SVG's sRGB filter math. A RenderEffect
 * captures its shader's uniforms when created, so a new effect is built per frame.
 */
@RequiresApi(33)
internal class GlassEffectBuilder {
    private val sharp = RuntimeShader(GLASS_SHADER)
    private var frostLight: RuntimeShader? = null
    private var frostDark: RuntimeShader? = null

    private val rect = FloatArray(MAX_SURFACES * 4)
    private val clip = FloatArray(MAX_SURFACES * 4)
    private val shape = FloatArray(MAX_SURFACES * 4)
    private val look = FloatArray(MAX_SURFACES * 4)
    private val tint = FloatArray(MAX_SURFACES * 4)
    private val glow = FloatArray(MAX_SURFACES * 4)

    /** The effect for up to [MAX_SURFACES] [surfaces] on a layer of [size] pixels, or null when there is nothing to draw. */
    fun build(surfaces: List<SurfaceFrame>, size: Size, density: Float): RenderEffect? {
        if (surfaces.isEmpty()) return null
        val count = minOf(surfaces.size, MAX_SURFACES)
        var hasLight = false
        var hasDark = false
        for (i in 0 until count) {
            val s = surfaces[i]
            write(i, s, density)
            if (s.regular) if (s.dark) hasDark = true else hasLight = true
        }
        var effect = RenderEffect.createRuntimeShaderEffect(configure(sharp, ShaderPass.SHARP, count, size, density), "content")
        if (hasLight) {
            val shader = frostLight ?: RuntimeShader(GLASS_SHADER).also { frostLight = it }
            effect = RenderEffect.createBlendModeEffect(effect, frostEffect(shader, ShaderPass.FROST_LIGHT, dark = false, count, size, density), BlendMode.SRC_OVER)
        }
        if (hasDark) {
            val shader = frostDark ?: RuntimeShader(GLASS_SHADER).also { frostDark = it }
            effect = RenderEffect.createBlendModeEffect(effect, frostEffect(shader, ShaderPass.FROST_DARK, dark = true, count, size, density), BlendMode.SRC_OVER)
        }
        return effect
    }

    private fun write(i: Int, s: SurfaceFrame, density: Float) {
        val o = i * 4
        val b = s.bounds
        rect[o] = b.center.x
        rect[o + 1] = b.center.y
        rect[o + 2] = b.width / 2f
        rect[o + 3] = b.height / 2f
        clip[o] = s.clip.left
        clip[o + 1] = s.clip.top
        clip[o + 2] = s.clip.right
        clip[o + 3] = s.clip.bottom
        val params = shapeParams(b.width / density, b.height / density, s.radius)
        shape[o] = params.extent
        shape[o + 1] = params.exponent
        shape[o + 2] = s.refraction
        shape[o + 3] = s.material
        look[o] = if (s.dark) 1f else 0f
        look[o + 1] = if (s.nested) 1f else 0f
        look[o + 2] = s.opacity
        look[o + 3] = if (s.contrast) 1f else 0f
        val tinted = s.tint.isSpecified
        tint[o] = if (tinted) s.tint.red else 0f
        tint[o + 1] = if (tinted) s.tint.green else 0f
        tint[o + 2] = if (tinted) s.tint.blue else 0f
        tint[o + 3] = if (tinted) 1f else 0f
        glow[o] = s.glowX
        glow[o + 1] = s.glowY
        glow[o + 2] = maxOf(s.glowRadius, 1f)
        glow[o + 3] = s.glowAmount
    }

    private fun configure(shader: RuntimeShader, pass: Float, count: Int, size: Size, density: Float): RuntimeShader {
        shader.setFloatUniform("uSize", size.width, size.height)
        shader.setFloatUniform("uDensity", density)
        shader.setFloatUniform("uPass", pass)
        shader.setIntUniform("uCount", count)
        shader.setFloatUniform("uRect", rect)
        shader.setFloatUniform("uClip", clip)
        shader.setFloatUniform("uShape", shape)
        shader.setFloatUniform("uLook", look)
        shader.setFloatUniform("uTint", tint)
        shader.setFloatUniform("uGlow", glow)
        return shader
    }

    private fun frostEffect(shader: RuntimeShader, pass: Float, dark: Boolean, count: Int, size: Size, density: Float): RenderEffect {
        val frost = gaussian(FROST_SIGMA * density)
        val fill = gaussian(frostFillSigma(dark) * density)
        val blended = RenderEffect.createBlendModeEffect(frost, fill, if (dark) BlendMode.DARKEN else BlendMode.LIGHTEN)
        val opacity = frostFillOpacity(dark)
        val frosted = RenderEffect.createBlendModeEffect(fade(frost, 1f - opacity), fade(blended, opacity), BlendMode.PLUS)
        return RenderEffect.createChainEffect(
            RenderEffect.createRuntimeShaderEffect(configure(shader, pass, count, size, density), "content"),
            frosted,
        )
    }

    /** Scales a premultiplied image by [amount] (the matrix runs on unpremultiplied color, so only alpha is scaled). */
    private fun fade(input: RenderEffect, amount: Float): RenderEffect =
        RenderEffect.createColorFilterEffect(ColorMatrixColorFilter(ColorMatrix().apply { setScale(1f, 1f, 1f, amount) }), input)
}

/**
 * A Gaussian blur of standard deviation [sigmaPx]. HWUI converts a blur radius
 * to σ as `0.57735 · radius + 0.5`, so the radius passed is the inverse of that.
 */
@RequiresApi(31)
internal fun gaussian(sigmaPx: Float): RenderEffect {
    val radius = ((sigmaPx - 0.5f) / 0.57735f).coerceAtLeast(0.01f)
    return RenderEffect.createBlurEffect(radius, radius, Shader.TileMode.CLAMP)
}

/**
 * The effect for progressive-blur level [level] (1..6) of a band: the level's
 * blur, masked to the coverage that reproduces the level weights when levels are
 * painted bottom-up (see `progressiveOverAlpha`). Coordinates are those of the
 * level layer, in which the band runs from [startX], [startY] (sharp side) to
 * [endX], [endY] (the edge).
 */
@RequiresApi(31)
internal fun progressiveLevelEffect(level: Int, sigmaPx: Float, startX: Float, startY: Float, endX: Float, endY: Float): RenderEffect {
    val stops = 24
    val colors = IntArray(stops + 1)
    val positions = FloatArray(stops + 1)
    for (i in 0..stops) {
        val u = i.toFloat() / stops
        positions[i] = u
        val alpha = (progressiveOverAlpha(level, u) * 255f + 0.5f).toInt().coerceIn(0, 255)
        colors[i] = alpha shl 24
    }
    val mask = LinearGradient(startX, startY, endX, endY, colors, positions, Shader.TileMode.CLAMP)
    return RenderEffect.createBlendModeEffect(gaussian(sigmaPx), RenderEffect.createShaderEffect(mask), BlendMode.DST_IN)
}
