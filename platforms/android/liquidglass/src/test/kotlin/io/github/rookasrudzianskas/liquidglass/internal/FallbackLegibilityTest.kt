package io.github.rookasrudzianskas.liquidglass.internal

import androidx.compose.ui.graphics.Color
import io.github.rookasrudzianskas.liquidglass.GlassDefaults
import io.github.rookasrudzianskas.liquidglass.optics.MENU_LEGIBILITY
import io.github.rookasrudzianskas.liquidglass.optics.SYSTEM_TINTS
import io.github.rookasrudzianskas.liquidglass.optics.TINTED_MENU_FLOOR
import io.github.rookasrudzianskas.liquidglass.optics.UNTINTED_MENU_FLOOR
import io.github.rookasrudzianskas.liquidglass.optics.contrastRatio
import io.github.rookasrudzianskas.liquidglass.optics.highlighted
import io.github.rookasrudzianskas.liquidglass.optics.legibilityColor
import io.github.rookasrudzianskas.liquidglass.optics.menuForeground
import io.github.rookasrudzianskas.liquidglass.optics.tintColor
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The fallback material carries the menus' legibility backing (spec/OPTICS.md §3) in its fill (folded into a
 * translucent stand-in, mixed into the Reduce Transparency fill as the shader does), so a fallback menu (below
 * Android 13, outside a scene, beyond the GPU limit) reads like a GPU one.
 */
class FallbackLegibilityTest {

    /** [fill] painted over the opaque [backdrop] with source-over. */
    private fun over(fill: Color, backdrop: FloatArray): FloatArray =
        floatArrayOf(fill.red, fill.green, fill.blue).let { c -> FloatArray(3) { fill.alpha * c[it] + (1f - fill.alpha) * backdrop[it] } }

    private val backdrops = listOf(
        floatArrayOf(1f, 1f, 1f),
        floatArrayOf(0f, 0f, 0f),
        floatArrayOf(0.2f, 0.4f, 0.6f),
        floatArrayOf(0.9f, 0.2f, 0.1f),
    )

    @Test
    fun theFoldedFillIsTheGlassMixedTowardTheBacking() {
        val fills = listOf(
            Color(0xFF1C1C1E).copy(alpha = 0.5f),
            Color(0xFFF7F7FA).copy(alpha = 0.78f),
            Color(0xFFFFFFFF).copy(alpha = 0.16f),
            Color(0xFF000000).copy(alpha = 0.28f),
            Color(0xFF1C1C1E).copy(alpha = 0.94f),
            Color(0.1f, 0.5f, 0.9f, 0.78f),
        )
        for (dark in listOf(false, true)) {
            for (amount in listOf(0.25f, MENU_LEGIBILITY, 1f)) {
                for (fill in fills) {
                    val folded = withLegibility(fill, dark, amount)
                    for (backdrop in backdrops) {
                        val expected = legibilityColor(over(fill, backdrop), dark, amount)
                        val actual = over(folded, backdrop)
                        // An sRGB Color holds 8 bits per channel, so the folded fill is rounded to 1/255.
                        for (i in 0..2) assertEquals("fill $fill dark=$dark amount=$amount over ${backdrop.toList()}[$i]", expected[i], actual[i], 1.5f / 255f)
                    }
                }
            }
        }
    }

    @Test
    fun noBackingLeavesTheFillAsItIs() {
        val fill = Color(0xFFF7F7FA).copy(alpha = 0.55f)
        assertEquals(fill, withLegibility(fill, dark = false, amount = 0f))
    }

    /**
     * Fallback menus keep their labels ([menuForeground]) over white and black, highlighted rows included, at the
     * GPU glass's floors: 5.2:1 untinted, 5.0:1 tinted with the system colors. In both appearances, Regular glass
     * with and without the platform blur, Clear glass (always an unblurred fill) and the Reduce Transparency fill.
     * A fill is affine in the backdrop, so white and black bound every backdrop.
     */
    @Test
    fun fallbackMenusKeepTheirLabelsLegible() {
        val drawn = listOf(ShaderMaterial.REGULAR to true, ShaderMaterial.REGULAR to false, ShaderMaterial.CLEAR to false, ShaderMaterial.SOLID to false)
        for (dark in listOf(false, true)) {
            for ((material, blurred) in drawn) {
                // The Reduce Transparency fill takes Regular's tint body, as in the shader.
                val regular = material != ShaderMaterial.CLEAR
                for (tint in listOf<FloatArray?>(null) + SYSTEM_TINTS) {
                    val label = menuForeground(tint, regular, dark)
                    val tintColor = tint?.let { Color(it[0], it[1], it[2]) } ?: Color.Unspecified
                    val fill = fallbackGlassFill(material, dark, tintColor, blurred, MENU_LEGIBILITY)
                    for (backdrop in backdrops.take(2)) {
                        val glass = over(fill, backdrop)
                        val contrast = minOf(contrastRatio(label, glass), contrastRatio(label, highlighted(glass, label)))
                        val floor = if (tint == null) UNTINTED_MENU_FLOOR else TINTED_MENU_FLOOR
                        assertTrue("material $material dark=$dark blurred=$blurred tint=${tint?.toList()} over ${backdrop.toList()}: $contrast", contrast >= floor)
                    }
                }
            }
        }
    }

    /**
     * The Reduce Transparency fill draws the shader's solid glass over any backdrop: `#F2F2F7` / `#1C1C1E`
     * (tinted with Regular's body), mixed toward the backing, painted at 94%, or `0.78 + 0.22 · 0.94` with a
     * tint, so a fallback menu under Reduce Transparency is the GPU one.
     */
    @Test
    fun theReduceTransparencyFillIsTheShadersSolidGlass() {
        for (dark in listOf(false, true)) {
            val solid = if (dark) floatArrayOf(28f / 255f, 28f / 255f, 30f / 255f) else floatArrayOf(242f / 255f, 242f / 255f, 247f / 255f)
            for (tint in listOf<FloatArray?>(null) + SYSTEM_TINTS) {
                val pigment = tint?.let { Color(it[0], it[1], it[2]) } ?: Color.Unspecified
                val rgb = tint?.let { tintColor(solid, it, regular = true) } ?: solid
                val alpha = if (tint == null) 0.94f else 0.78f + 0.22f * 0.94f
                for (amount in listOf(0f, 0.25f, MENU_LEGIBILITY, 1f)) {
                    val glass = legibilityColor(rgb, dark, amount)
                    val fill = fallbackGlassFill(ShaderMaterial.SOLID, dark, pigment, blurred = false, legibility = amount)
                    for (backdrop in backdrops) {
                        val drawn = over(fill, backdrop)
                        for (i in 0..2) {
                            val expected = alpha * glass[i] + (1f - alpha) * backdrop[i]
                            // An sRGB Color holds 8 bits per channel, alpha included.
                            assertEquals("dark=$dark tint=${tint?.toList()} amount=$amount over ${backdrop.toList()}[$i]", expected, drawn[i], 1.5f / 255f)
                        }
                    }
                }
            }
        }
    }

    /** The untinted labels are the glass foreground. */
    @Test
    fun untintedMenusUseTheGlassForeground() {
        for (dark in listOf(false, true)) {
            val foreground = GlassDefaults.contentColor(dark).let { floatArrayOf(it.red, it.green, it.blue) }
            for (regular in listOf(false, true)) assertEquals(foreground.toList(), menuForeground(null, regular, dark).toList())
        }
    }
}
