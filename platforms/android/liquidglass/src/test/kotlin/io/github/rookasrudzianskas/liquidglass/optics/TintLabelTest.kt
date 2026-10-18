package io.github.rookasrudzianskas.liquidglass.optics

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** Labels on tinted glass take white or #111113, whichever contrasts more with the tinted glass (spec/OPTICS.md §8). */
class TintLabelTest {

    private fun dark(hex: Long, regular: Boolean): Boolean = darkLabelOnTint(
        ((hex shr 16) and 0xFF) / 255f,
        ((hex shr 8) and 0xFF) / 255f,
        (hex and 0xFF) / 255f,
        regular,
    )

    @Test
    fun lightTintsTakeTheDarkLabel() {
        // Green, orange (light and dark system variants), yellow, mint and cyan.
        for (tint in listOf(0x30D158L, 0x34C759L, 0xFF9500L, 0xFF9F0AL, 0xFFCC00L, 0x00C7BEL, 0x32ADE6L)) {
            assertTrue("#%06X on Clear".format(tint), dark(tint, regular = false))
            assertTrue("#%06X on Regular".format(tint), dark(tint, regular = true))
        }
    }

    @Test
    fun saturatedTintsTakeWhite() {
        // Blue (light and dark), red, purple, indigo and pink.
        for (tint in listOf(0x0A84FFL, 0x007AFFL, 0xFF3B30L, 0xAF52DEL, 0x5856D6L, 0xFF2D55L)) {
            assertFalse("#%06X on Clear".format(tint), dark(tint, regular = false))
            assertFalse("#%06X on Regular".format(tint), dark(tint, regular = true))
        }
        assertTrue(dark(0xFFFFFFL, regular = false))
        assertFalse(dark(0x000000L, regular = true))
    }

    /** The chosen label always has the higher contrast ratio with the glass, across grays and hue ramps. */
    @Test
    fun theChosenLabelHasTheHigherContrast() {
        val darkLabel = relativeLuminance(17f / 255f, 17f / 255f, 19f / 255f)
        for (regular in listOf(false, true)) {
            val body = tintBody(regular)
            val pass = body + (1 - body) * 0.5f
            for (step in 0..40) {
                val v = step / 40f
                for (tint in listOf(floatArrayOf(v, v, v), floatArrayOf(v, 0.4f, 1 - v), floatArrayOf(1f, v, 0f))) {
                    val glass = relativeLuminance(tint[0] * pass, tint[1] * pass, tint[2] * pass)
                    val expected = (glass + 0.05f) / (darkLabel + 0.05f) > 1.05f / (glass + 0.05f)
                    assertEquals("tint ${tint.toList()} regular=$regular", expected, darkLabelOnTint(tint[0], tint[1], tint[2], regular))
                }
            }
        }
    }

    /**
     * Menu labels (spec/OPTICS.md §3) are chosen against the tint mixed toward the legibility backing, so they
     * follow the appearance too: the chosen label always has the higher contrast with that glass.
     */
    @Test
    fun menuLabelsHaveTheHigherContrastWithTheBackedTint() {
        val ink = floatArrayOf(17f / 255f, 17f / 255f, 19f / 255f)
        val white = floatArrayOf(1f, 1f, 1f)
        for (regular in listOf(false, true)) {
            val body = tintBody(regular)
            val pass = body + (1 - body) * 0.5f
            for (dark in listOf(false, true)) {
                for (step in 0..40) {
                    val v = step / 40f
                    for (tint in listOf(floatArrayOf(v, v, v), floatArrayOf(v, 0.4f, 1 - v), floatArrayOf(1f, v, 0f))) {
                        val glass = legibilityColor(FloatArray(3) { tint[it] * pass }, dark, MENU_LEGIBILITY)
                        val label = menuForeground(tint, regular, dark)
                        val other = if (label[0] > 0.5f) ink else white
                        assertTrue(
                            "tint ${tint.toList()} regular=$regular dark=$dark",
                            contrastRatio(label, glass) >= contrastRatio(other, glass),
                        )
                    }
                }
            }
        }
        // A light blue panel takes #111113 where the tint alone would take white; a dark yellow one white.
        val blue = floatArrayOf(0f, 0x7A / 255f, 1f)
        val yellow = floatArrayOf(1f, 0xCC / 255f, 0f)
        assertFalse(dark(0x007AFFL, regular = true))
        assertEquals(ink.toList(), menuForeground(blue, regular = true, dark = false).toList())
        assertTrue(dark(0xFFCC00L, regular = true))
        assertEquals(white.toList(), menuForeground(yellow, regular = true, dark = true).toList())
    }

    @Test
    fun relativeLuminanceMatchesWcag() {
        assertEquals(1f, relativeLuminance(1f, 1f, 1f), 1e-6f)
        assertEquals(0f, relativeLuminance(0f, 0f, 0f), 1e-6f)
        // #808080: 0.2158605 (WCAG 2 sRGB transfer).
        assertEquals(0.2158605f, relativeLuminance(128f / 255f, 128f / 255f, 128f / 255f), 1e-5f)
    }
}
