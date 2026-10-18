package io.github.rookasrudzianskas.liquidglass

import io.github.rookasrudzianskas.liquidglass.optics.MENU_LEGIBILITY
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A menu panel's legibility backing fades in with its morph, so the glass leaves the trigger as the
 * trigger's own glass and returns to it, as in the SwiftUI and WPF ports.
 */
class MenuLegibilityTest {

    @Test
    fun theBackingFollowsTheMorph() {
        assertEquals(0f, menuLegibility(0f), 0f)
        assertEquals(MENU_LEGIBILITY / 2f, menuLegibility(0.5f), 1e-6f)
        assertEquals(MENU_LEGIBILITY, menuLegibility(1f), 0f)
    }

    @Test
    fun theBackingNeverPassesTheMenuAmount() {
        // The morph spring overshoots past 1 when it opens; collapsing never goes below 0.
        for (morph in listOf(1.05f, 1.18f, 2f)) assertEquals(MENU_LEGIBILITY, menuLegibility(morph), 0f)
        assertEquals(0f, menuLegibility(-0.1f), 0f)
        var last = 0f
        for (i in 0..100) {
            val amount = menuLegibility(i / 100f)
            assertTrue("not monotonic at ${i / 100f}", amount >= last)
            last = amount
        }
    }
}
