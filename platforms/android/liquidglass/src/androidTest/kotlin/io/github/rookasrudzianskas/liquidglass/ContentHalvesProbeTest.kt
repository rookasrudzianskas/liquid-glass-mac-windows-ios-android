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
import kotlin.math.abs

/** Temporary probe: which half of backdropDrawnByTheContentModifierIsRefracted's scene differs, and how. */
@RunWith(AndroidJUnit4::class)
class ContentHalvesProbeTest {

    @Test fun probeModLam() = probe(leftModifier = true, rightModifier = false)

    @Test fun probeModLamEdges() = probe(leftModifier = true, rightModifier = false, edges = true)

    @Test fun probeLamMod() = probe(leftModifier = false, rightModifier = true)

    @Test fun probeLamLam() = probe(leftModifier = false, rightModifier = false)

    @Test fun probeModMod() = probe(leftModifier = true, rightModifier = true)

    private fun probe(leftModifier: Boolean, rightModifier: Boolean, edges: Boolean = false) {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val half = 160
        val stripes = Modifier.drawBehind {
            for (i in 0 until (size.width / 8f).toInt() + 1) {
                drawRect(Stripes[i % Stripes.size], topLeft = Offset(i * 8f, 0f), size = Size(8f, size.height))
            }
        }

        @Composable
        fun Half(drawnByModifier: Boolean) {
            Box(Modifier.size(half.dp, 260.dp)) {
                GlassScene(Modifier.fillMaxSize(), appearance = GlassAppearance.Light, increaseContrast = false) {
                    if (drawnByModifier) {
                        GlassContent(Modifier.fillMaxSize().background(Backdrop).then(stripes)) {}
                    } else {
                        GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(Backdrop).then(stripes)) }
                    }
                    Box(Modifier.offset(10.dp, 40.dp).size(140.dp, 90.dp).liquidGlass(GlassShape(24.dp), material = GlassMaterial.Regular))
                    Box(Modifier.offset(10.dp, 150.dp).size(140.dp, 70.dp).liquidGlass(GlassShape(24.dp), tint = Color(0xFF0A84FF)))
                }
            }
        }
        ActivityScenario.launch(ComponentActivity::class.java).use { scenario ->
            var bounds = Rect.Zero
            scenario.onActivity { activity ->
                activity.setContent {
                    CompositionLocalProvider(LocalDensity provides Density(1f)) {
                        Box(Modifier.size(320.dp, 260.dp).onGloballyPositioned { bounds = it.boundsInWindow() }) {
                            Row {
                                Half(leftModifier)
                                Half(rightModifier)
                            }
                        }
                    }
                }
            }
            awaitFrames(scenario, 12)
            Thread.sleep(1500)
            awaitFrames(scenario, 6)
            var image: Bitmap? = null
            repeat(10) { if (image == null) image = copy(scenario, bounds) }
            val pixels = image ?: run { fail("no PixelCopy"); return }
            var worst = 0
            var wx = 0
            var wy = 0
            for (y in listOf(40, 41, 85, 128, 150, 151, 185, 218)) {
                for (x in 0 until half) {
                    val a = pixels.getPixel(x, y)
                    val b = pixels.getPixel(x + half, y)
                    val e = maxOf(abs(((a shr 16) and 255) - ((b shr 16) and 255)), abs(((a shr 8) and 255) - ((b shr 8) and 255)), abs((a and 255) - (b and 255)))
                    if (e > worst) {
                        worst = e
                        wx = x
                        wy = y
                    }
                }
            }
            fun hex(p: Int) = "%06x".format(p and 0xFFFFFF)
            fun row(y: Int, from: Int, to: Int, offset: Int) = (from..to).joinToString(",") { hex(pixels.getPixel(it + offset, y)) }
            // Where the color changes along a row, from x = 0 to 47: "x:color".
            fun transitions(y: Int, offset: Int): String {
                var last = -1
                return buildList {
                    for (x in 0 until 48) {
                        val p = pixels.getPixel(x + offset, y) and 0xFFFFFF
                        if (p != last) add("$x:${hex(p)}")
                        last = p
                    }
                }.joinToString(",")
            }
            val message = if (edges) {
                "L5=${transitions(5, 0)} R5=${transitions(5, half)} L185=${transitions(185, 0)} R185=${transitions(185, half)}"
            } else {
                "api=${Build.VERSION.SDK_INT} win=(${bounds.left.toInt()},${bounds.top.toInt()}) worst=$worst@($wx,$wy) " +
                    "L185[6..14]=${row(185, 6, 14, 0)} R185=${row(185, 6, 14, half)} Lw=${hex(pixels.getPixel(wx, wy))} Rw=${hex(pixels.getPixel(wx + half, wy))}"
            }
            Log.i("GlassRenderingTest", "probe left=$leftModifier right=$rightModifier $message")
            fail(message)
        }
    }

    private fun copy(scenario: ActivityScenario<ComponentActivity>, bounds: Rect): Bitmap? {
        val bitmap = Bitmap.createBitmap(320, 260, Bitmap.Config.ARGB_8888)
        val done = CountDownLatch(1)
        var result = -1
        scenario.onActivity { activity ->
            val area = android.graphics.Rect(bounds.left.toInt(), bounds.top.toInt(), bounds.left.toInt() + 320, bounds.top.toInt() + 260)
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
        val Backdrop = Color(0.5f, 0.5f, 0.6f)
        val Stripes = listOf(Color(0xFFE53935), Color(0xFFFDD835), Color(0xFF43A047), Color(0xFF1E88E5), Color(0xFF8E24AA))
    }
}
