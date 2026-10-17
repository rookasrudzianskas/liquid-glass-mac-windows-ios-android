package io.github.rookasrudzianskas.liquidglass

import android.graphics.Bitmap
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.view.Choreographer
import android.view.PixelCopy
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Assume.assumeTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

/**
 * Temporary probe: on the Android 14 CI emulator the left of two side-by-side scenes draws its
 * content's first column across its whole width. Each case lays scenes ("G" with glass, "S" without,
 * "-" an empty slot) in a row or a column of 160 x 260 slots and reports, per slot, whether row 5
 * shows the stripes ("ok") or what it shows instead.
 */
@RunWith(AndroidJUnit4::class)
class ContentHalvesProbeTest {

    @Test fun probe1SingleAtLeft() = probe("G-")

    @Test fun probe2SingleAtRight() = probe("-G")

    @Test fun probe3Three() = probe("GGG")

    @Test fun probe4PlainThenGlass() = probe("SG")

    @Test fun probe5GlassThenPlain() = probe("GS")

    @Test fun probe6Stacked() = probe("GG", vertical = true)

    private fun probe(slots: String, vertical: Boolean = false) {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val stripes = Modifier.drawBehind {
            for (i in 0 until (size.width / 8f).toInt() + 1) {
                drawRect(Stripes[i % Stripes.size], topLeft = Offset(i * 8f, 0f), size = Size(8f, size.height))
            }
        }

        @Composable
        fun Slot(kind: Char) {
            Box(Modifier.size(SlotWidth.dp, SlotHeight.dp)) {
                if (kind == '-') return@Box
                GlassScene(Modifier.fillMaxSize(), appearance = GlassAppearance.Light, increaseContrast = false) {
                    GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(Backdrop).then(stripes)) }
                    if (kind == 'G') {
                        Box(Modifier.offset(10.dp, 40.dp).size(140.dp, 90.dp).liquidGlass(GlassShape(24.dp), material = GlassMaterial.Regular))
                        Box(Modifier.offset(10.dp, 150.dp).size(140.dp, 70.dp).liquidGlass(GlassShape(24.dp), tint = Color(0xFF0A84FF)))
                    }
                }
            }
        }
        val width = if (vertical) SlotWidth else SlotWidth * slots.length
        val height = if (vertical) SlotHeight * slots.length else SlotHeight
        ActivityScenario.launch(ComponentActivity::class.java).use { scenario ->
            var bounds = Rect.Zero
            scenario.onActivity { activity ->
                activity.setContent {
                    CompositionLocalProvider(LocalDensity provides Density(1f)) {
                        Box(Modifier.size(width.dp, height.dp).onGloballyPositioned { bounds = it.boundsInWindow() }) {
                            if (vertical) Column { slots.forEach { Slot(it) } } else Row { slots.forEach { Slot(it) } }
                        }
                    }
                }
            }
            awaitFrames(scenario, 12)
            Thread.sleep(1500)
            awaitFrames(scenario, 6)
            var image: Bitmap? = null
            repeat(10) { if (image == null) image = copy(scenario, bounds, width, height) }
            val pixels = image ?: run { fail("no PixelCopy"); return }
            fun hex(p: Int) = "%06x".format(p and 0xFFFFFF)
            fun transitions(x0: Int, y: Int): String {
                var last = -1
                return buildList {
                    for (x in 0 until 48) {
                        val p = pixels.getPixel(x0 + x, y) and 0xFFFFFF
                        if (p != last) add("$x:${hex(p)}")
                        last = p
                    }
                }.joinToString(",")
            }
            val report = slots.indices.joinToString(" ") { i ->
                val x0 = if (vertical) 0 else i * SlotWidth
                val y0 = if (vertical) i * SlotHeight else 0
                val row5 = transitions(x0, y0 + 5)
                val glass = hex(pixels.getPixel(x0 + 80, y0 + 85)) + "/" + hex(pixels.getPixel(x0 + 80, y0 + 185))
                "${slots[i]}$i=" + when {
                    slots[i] == '-' -> "empty:" + hex(pixels.getPixel(x0 + 80, y0 + 5))
                    row5 == Expected -> "ok,$glass"
                    else -> "BAD[$row5],$glass"
                }
            }
            val message = "api=${Build.VERSION.SDK_INT} ${if (vertical) "col" else "row"} $slots: $report"
            Log.i("GlassRenderingTest", "probe $message")
            fail(message)
        }
    }

    private fun copy(scenario: ActivityScenario<ComponentActivity>, bounds: Rect, width: Int, height: Int): Bitmap? {
        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        val done = CountDownLatch(1)
        var result = -1
        scenario.onActivity { activity ->
            val area = android.graphics.Rect(bounds.left.toInt(), bounds.top.toInt(), bounds.left.toInt() + width, bounds.top.toInt() + height)
            PixelCopy.request(activity.window, area, bitmap, { code -> result = code; done.countDown() }, Handler(Looper.getMainLooper()))
        }
        assertTrue(done.await(30, TimeUnit.SECONDS))
        return bitmap.takeIf { result == PixelCopy.SUCCESS }
    }

    private fun awaitFrames(scenario: ActivityScenario<ComponentActivity>, count: Int) {
        val done = CountDownLatch(1)
        scenario.onActivity {
            var left = count
            val callback = object : Choreographer.FrameCallback {
                override fun doFrame(frameTimeNanos: Long) {
                    if (--left <= 0) done.countDown() else Choreographer.getInstance().postFrameCallback(this)
                }
            }
            Choreographer.getInstance().postFrameCallback(callback)
        }
        assertTrue(done.await(30, TimeUnit.SECONDS))
    }

    private companion object {
        const val SlotWidth = 160
        const val SlotHeight = 260
        const val Expected = "0:e53935,8:fdd835,16:43a047,24:1e88e5,32:8e24aa,40:e53935"
        val Backdrop = Color(0.5f, 0.5f, 0.6f)
        val Stripes = listOf(Color(0xFFE53935), Color(0xFFFDD835), Color(0xFF43A047), Color(0xFF1E88E5), Color(0xFF8E24AA))
    }
}
