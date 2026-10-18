package io.github.rookasrudzianskas.liquidglass.internal

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.isSpecified
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.graphics.layer.GraphicsLayer
import androidx.compose.ui.graphics.layer.drawLayer
import io.github.rookasrudzianskas.liquidglass.glassPath
import io.github.rookasrudzianskas.liquidglass.optics.GlowParams
import io.github.rookasrudzianskas.liquidglass.optics.legibilityBacking
import io.github.rookasrudzianskas.liquidglass.optics.legibilityColor
import io.github.rookasrudzianskas.liquidglass.optics.luma
import io.github.rookasrudzianskas.liquidglass.optics.shapeParams
import io.github.rookasrudzianskas.liquidglass.optics.tintBody

/**
 * The fallback material (spec/COMPONENTS.md, Behavior 8) for devices or places
 * without the GPU glass path: an optional platform blur of the backdrop, a
 * translucent fill and the rim light, all clipped to the continuous outline.
 *
 * Draws into a scope whose origin is the surface's top-left corner and whose
 * size is the surface's visual size. [blurredBackdrop], when given, is the
 * scene content already blurred, positioned by [backdropOffset]. [legibility]
 * mixes the glass toward the legibility backing, as the shader does (menus).
 */
internal fun DrawScope.drawFallbackGlass(
    size: Size,
    radiusDp: Float?,
    material: Float,
    dark: Boolean,
    tint: Color,
    opacity: Float,
    contrast: Boolean,
    glowCenter: Offset,
    glow: GlowParams,
    glowAmount: Float,
    legibility: Float,
    blurredBackdrop: GraphicsLayer?,
    backdropOffset: Offset,
) {
    if (size.width <= 0f || size.height <= 0f || opacity <= 0f) return
    val radiusPx = radiusDp?.let { it * density }
    val outline = glassPath(size.width, size.height, radiusPx)
    val regular = material == ShaderMaterial.REGULAR
    val solid = material == ShaderMaterial.SOLID
    clipPath(outline) {
        if (blurredBackdrop != null && !solid) {
            drawContext.transform.translate(backdropOffset.x, backdropOffset.y)
            drawLayer(blurredBackdrop)
            drawContext.transform.translate(-backdropOffset.x, -backdropOffset.y)
        }
        drawRect(fallbackGlassFill(material, dark, tint, blurred = blurredBackdrop != null, legibility), alpha = opacity)
        val glowRadiusPx = glow.radius * density
        if (glowAmount > 0f && glowRadiusPx > 0f) {
            val alpha = glow.alpha * glowAmount * opacity
            drawCircle(
                brush = Brush.radialGradient(listOf(Color.White.copy(alpha = alpha), Color.Transparent), glowCenter, glowRadiusPx),
                radius = glowRadiusPx,
                center = glowCenter,
            )
        }
        if (contrast) {
            drawPath(outline, if (dark) Color.White.copy(alpha = 0.28f) else Color.Black.copy(alpha = 0.18f), style = Stroke(2f * density), alpha = opacity)
        }
        drawFallbackRim(outline, size, radiusPx, dark, tint, opacity, regular || solid)
    }
}

/** Rim light approximated with gradient strokes: specular top and bottom, diffuse glow, side shade. */
private fun DrawScope.drawFallbackRim(outline: Path, size: Size, radiusPx: Float?, dark: Boolean, tint: Color, opacity: Float, frosted: Boolean) {
    val extent = shapeParams(size.width, size.height, radiusPx).extent
    val k = if (dark) 0.82f else 1f
    val highlight = if (tint.isSpecified) Color(0.45f + 0.55f * tint.red, 0.45f + 0.55f * tint.green, 0.45f + 0.55f * tint.blue) else Color.White
    // Fraction of the height over which the corner turns from horizontal to vertical.
    val v = (extent * 0.62f / size.height).coerceIn(0.02f, 0.49f)
    val vertical = Brush.verticalGradient(
        0f to highlight.copy(alpha = k),
        v to Color.Transparent,
        1f - v to Color.Transparent,
        1f to highlight.copy(alpha = k),
    )
    // Specular: about 0.9 dp inside the outline (the stroke is centered on it and clipped).
    drawPath(outline, vertical, style = Stroke(1.8f * density), alpha = opacity)
    // Diffuse: a soft band 4.5 dp deep.
    drawPath(outline, vertical, style = Stroke(9f * density), alpha = opacity * (if (dark) 0.07f else if (frosted) 0.16f else 0.12f))
    // Side shade.
    val h = (extent * 0.55f / size.width).coerceIn(0.02f, 0.49f)
    val shade = (if (dark) 0.6f else 0.48f) * 0.5f
    val horizontal = Brush.horizontalGradient(
        0f to Color.Black.copy(alpha = shade),
        h to Color.Transparent,
        1f - h to Color.Transparent,
        1f to Color.Black.copy(alpha = shade),
    )
    drawPath(outline, horizontal, style = Stroke(1.2f * density), alpha = opacity)
}

/**
 * The fill drawn for the material, with [legibility] of the backing (spec/OPTICS.md §3). A translucent fill stands
 * in for the GPU glass, which is opaque, so the backing is folded into it ([withLegibility]). The Reduce
 * Transparency fill is the shader's own solid material: as in the shader, its color is mixed toward the backing
 * and it keeps its opacity, so the fallback and the GPU draw the same solid glass.
 */
internal fun fallbackGlassFill(material: Float, dark: Boolean, tint: Color, blurred: Boolean, legibility: Float): Color {
    val fill = fallbackFill(material, dark, tint, blurred)
    if (material != ShaderMaterial.SOLID || legibility <= 0f) return withLegibility(fill, dark, legibility)
    val rgb = legibilityColor(floatArrayOf(fill.red, fill.green, fill.blue), dark, legibility)
    return Color(rgb[0], rgb[1], rgb[2], fill.alpha)
}

/**
 * The translucent fill that stands in for the material. The Reduce Transparency fill is the shader's solid
 * material: `#F2F2F7` / `#1C1C1E` at 94%, tinted with Regular's body, which a tint makes `0.78 + 0.22 · 0.94` opaque.
 */
internal fun fallbackFill(material: Float, dark: Boolean, tint: Color, blurred: Boolean): Color {
    if (material == ShaderMaterial.SOLID) {
        val fill = if (dark) Color(0xFF1C1C1E) else Color(0xFFF2F2F7)
        if (!tint.isSpecified) return fill.copy(alpha = SOLID_OPACITY)
        val body = tintBody(regular = true)
        return tinted(fill, tint, regular = true).copy(alpha = body + (1f - body) * SOLID_OPACITY)
    }
    val regular = material == ShaderMaterial.REGULAR
    val base = when {
        regular && dark -> Color(0xFF1C1C1E).copy(alpha = if (blurred) 0.5f else 0.72f)
        regular -> Color(0xFFF7F7FA).copy(alpha = if (blurred) 0.55f else 0.78f)
        dark -> Color(0xFF000000).copy(alpha = if (blurred) 0.12f else 0.28f)
        else -> Color(0xFFFFFFFF).copy(alpha = if (blurred) 0.16f else 0.3f)
    }
    if (!tint.isSpecified) return base
    return tinted(base, tint, regular).copy(alpha = tintBody(regular))
}

/**
 * [fill] with the legibility backing (spec/OPTICS.md §3) folded in: the glass a fill
 * makes over any backdrop, mixed toward the backing by [amount], is itself a fill
 * over that backdrop, `(1 − amount)·fill + amount·backing` with the backdrop showing
 * through by `(1 − amount)(1 − fill.alpha)`. So a fallback menu reads like a GPU one.
 */
internal fun withLegibility(fill: Color, dark: Boolean, amount: Float): Color {
    if (amount <= 0f) return fill
    val backing = legibilityBacking(dark)
    val a = fill.alpha
    val alpha = 1f - (1f - amount) * (1f - a)
    fun channel(c: Float, b: Float) = (((1f - amount) * a * c + amount * b) / alpha).coerceIn(0f, 1f)
    return Color(channel(fill.red, backing[0]), channel(fill.green, backing[1]), channel(fill.blue, backing[2]), alpha)
}

/** Opacity of the Reduce Transparency fill (spec/OPTICS.md §8). */
private const val SOLID_OPACITY = 0.94f

/** Tint transmits the luminance of what lies beneath, as in spec/OPTICS.md §3. */
private fun tinted(under: Color, tint: Color, regular: Boolean): Color {
    val body = tintBody(regular)
    val l = luma(under.red, under.green, under.blue)
    val f = body + (1f - body) * l
    return Color((tint.red * f).coerceIn(0f, 1f), (tint.green * f).coerceIn(0f, 1f), (tint.blue * f).coerceIn(0f, 1f))
}
