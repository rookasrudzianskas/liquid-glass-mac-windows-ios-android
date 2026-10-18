package io.github.rookasrudzianskas.liquidglass.internal

import androidx.compose.ui.graphics.Color
import io.github.rookasrudzianskas.liquidglass.GlassDefaults
import io.github.rookasrudzianskas.liquidglass.optics.MENU_LEGIBILITY
import io.github.rookasrudzianskas.liquidglass.optics.contrastRatio
import io.github.rookasrudzianskas.liquidglass.optics.legibilityColor
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The fallback material folds the menus' legibility backing (spec/OPTICS.md §3) into its fill,
 * so a fallback menu (below Android 13, outside a scene, beyond the GPU limit) reads like a GPU one.
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

    /** Untinted Regular (and Reduce Transparency) fallback menus, with and without the platform blur, keep their labels at 5:1 over white and black. */
    @Test
    fun fallbackMenusKeepTheirLabelsLegible() {
        for (dark in listOf(false, true)) {
            val label = GlassDefaults.contentColor(dark).let { floatArrayOf(it.red, it.green, it.blue) }
            for (material in listOf(ShaderMaterial.REGULAR, ShaderMaterial.SOLID)) {
                for (blurred in listOf(false, true)) {
                    val fill = withLegibility(fallbackFill(material, dark, Color.Unspecified, blurred), dark, MENU_LEGIBILITY)
                    for (backdrop in backdrops.take(2)) {
                        val contrast = contrastRatio(label, over(fill, backdrop))
                        assertTrue("material $material dark=$dark blurred=$blurred over ${backdrop.toList()}: $contrast", contrast >= 5f)
                    }
                }
            }
        }
    }
}
