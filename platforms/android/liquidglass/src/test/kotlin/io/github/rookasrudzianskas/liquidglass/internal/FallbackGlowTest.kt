package io.github.rookasrudzianskas.liquidglass.internal

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import org.junit.Assert.assertEquals
import org.junit.Test

/** The own fallback draws the glass inside the drag offset and press scale: its glow must still land under the pointer. */
class FallbackGlowTest {

    /** What the fallback's transforms do to a point drawn at [drawn]: inset, then scale about the center, then the drag. */
    private fun onScreen(drawn: Offset, local: Rect, offset: Offset, sx: Float, sy: Float): Offset = Offset(
        offset.x + local.center.x + (drawn.x + local.left - local.center.x) * sx,
        offset.y + local.center.y + (drawn.y + local.top - local.center.y) * sy,
    )

    @Test
    fun glowStaysUnderThePointerWhilePressedAndDragged() {
        val local = Rect(-6f, -4f, 126f, 48f)
        for ((pointer, offset, scale) in listOf(
            Triple(Offset(30f, 20f), Offset.Zero, 1f to 1f),
            Triple(Offset(30f, 20f), Offset.Zero, 1.16f to 1.16f),
            Triple(Offset(110f, 6f), Offset(12f, -3f), 1.2f to 0.9f),
            Triple(Offset(-2f, 44f), Offset(-8f, 5f), 0.95f to 1.1f),
        )) {
            val drawn = fallbackGlowCenter(pointer, local, offset, scale.first, scale.second)
            val shown = onScreen(drawn, local, offset, scale.first, scale.second)
            assertEquals("x for $pointer", pointer.x, shown.x, 1e-3f)
            assertEquals("y for $pointer", pointer.y, shown.y, 1e-3f)
        }
    }

    @Test
    fun atRestTheGlowIsThePointerInTheInsetGlass() {
        val local = Rect(10f, 5f, 90f, 45f)
        assertEquals(Offset(20f, 15f), fallbackGlowCenter(Offset(30f, 20f), local, Offset.Zero, 1f, 1f))
    }
}
