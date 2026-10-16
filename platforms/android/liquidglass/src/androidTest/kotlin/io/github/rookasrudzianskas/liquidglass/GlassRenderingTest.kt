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
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import io.github.rookasrudzianskas.liquidglass.optics.glassPixel
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.math.abs
import kotlin.math.sqrt

/**
 * Renders glass on the device GPU and checks the pixels against the Kotlin
 * reference ([glassPixel], itself checked against spec/vectors.json).
 * Scenes run at density 1, so one dp is one pixel, over a uniform backdrop
 * (where refraction cannot move the color and only tone, tint, coverage and
 * rim light show). Tolerance is 2/255: the readback is 8-bit (measured worst
 * case on an Android 17 emulator: 1.3/255).
 */
@RunWith(AndroidJUnit4::class)
class GlassRenderingTest {

    private val backdrop = floatArrayOf(0.5f, 0.5f, 0.6f, 1f)
    private val backdropColor = Color(backdrop[0], backdrop[1], backdrop[2])

    /** Composes [content] in a SceneWidth × SceneHeight box at density 1, lets it settle, and copies its pixels from the window. */
    private fun capture(content: @Composable () -> Unit): Bitmap {
        ActivityScenario.launch(ComponentActivity::class.java).use { scenario ->
            var bounds = Rect.Zero
            scenario.onActivity { activity ->
                activity.setContent {
                    CompositionLocalProvider(LocalDensity provides Density(1f)) {
                        Box(Modifier.size(SceneWidth.dp, SceneHeight.dp).onGloballyPositioned { bounds = it.boundsInWindow() }) { content() }
                    }
                }
            }
            awaitFrames(scenario, SettleFrames)
            val bitmap = Bitmap.createBitmap(SceneWidth, SceneHeight, Bitmap.Config.ARGB_8888)
            val done = CountDownLatch(1)
            var result = -1
            scenario.onActivity { activity ->
                val area = android.graphics.Rect(bounds.left.toInt(), bounds.top.toInt(), bounds.left.toInt() + SceneWidth, bounds.top.toInt() + SceneHeight)
                PixelCopy.request(activity.window, area, bitmap, { code -> result = code; done.countDown() }, Handler(Looper.getMainLooper()))
            }
            assertTrue("PixelCopy timed out", done.await(5, TimeUnit.SECONDS))
            assertEquals("PixelCopy failed", PixelCopy.SUCCESS, result)
            return bitmap
        }
    }

    /** Waits for [count] frames to be drawn, so composition, layout and the glass effect have settled. */
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
        assertTrue("frames never arrived", done.await(10, TimeUnit.SECONDS))
    }

    private fun Bitmap.color(x: Int, y: Int): Color = Color(getPixel(x, y))

    /** The test surface (Width × Height at Left, Top) in a scene over the uniform backdrop. */
    @Composable
    private fun SingleSurface(material: GlassMaterial, appearance: GlassAppearance, tint: Color = Color.Unspecified) {
        GlassScene(appearance = appearance, increaseContrast = false) {
            GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
            Box(
                Modifier
                    .offset(Left.dp, Top.dp)
                    .size(Width.dp, Height.dp)
                    .liquidGlass(GlassShape(Radius.dp), material = material, appearance = appearance, tint = tint),
            )
        }
    }

    private fun assertMatchesReference(pixels: Bitmap, regular: Boolean, dark: Boolean, tint: FloatArray?) {
        var worst = 0f
        var where = ""
        // Every pixel of three rows (the top rim, the middle, near the bottom rim) and two columns (left rim, middle).
        val samples = buildList {
            for (x in Left - 2 until Left + Width + 2) {
                add(x to Top)
                add(x to Top + Height / 2)
                add(x to Top + Height - 3)
            }
            for (y in Top - 2 until Top + Height + 2) {
                add(Left to y)
                add(Left + Width / 2 to y)
            }
        }
        for ((px, py) in samples) {
            val x = px + 0.5f - (Left + Width / 2f)
            val y = py + 0.5f - (Top + Height / 2f)
            val expected = glassPixel(
                x = x, y = y, width = Width.toFloat(), height = Height.toFloat(), radius = Radius.toFloat(),
                regular = regular, dark = dark, refraction = 60f, pixel = 1f, tint = tint,
                base = backdrop, sample = { _, _ -> backdrop },
            )
            val actual = pixels.color(px, py)
            val channels = floatArrayOf(actual.red, actual.green, actual.blue)
            for (c in 0..2) {
                val error = abs(channels[c] - expected[c])
                if (error > worst) {
                    worst = error
                    where = "($px, $py) channel $c: expected ${expected[c]}, got ${channels[c]}"
                }
            }
        }
        Log.i("GlassRenderingTest", "regular=$regular dark=$dark tint=${tint != null}: max error ${worst * 255}/255 over ${samples.size} pixels")
        assertTrue("max error ${worst * 255}/255 at $where", worst <= Tolerance)
    }

    @Test
    fun clearLightMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val image = capture { SingleSurface(GlassMaterial.Clear, GlassAppearance.Light) }
        assertMatchesReference(image, regular = false, dark = false, tint = null)
    }

    @Test
    fun clearDarkMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val image = capture { SingleSurface(GlassMaterial.Clear, GlassAppearance.Dark) }
        assertMatchesReference(image, regular = false, dark = true, tint = null)
    }

    @Test
    fun regularLightMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val image = capture { SingleSurface(GlassMaterial.Regular, GlassAppearance.Light) }
        assertMatchesReference(image, regular = true, dark = false, tint = null)
    }

    @Test
    fun regularDarkMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val image = capture { SingleSurface(GlassMaterial.Regular, GlassAppearance.Dark) }
        assertMatchesReference(image, regular = true, dark = true, tint = null)
    }

    @Test
    fun tintedRegularMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val tint = floatArrayOf(0f, 0.478f, 1f)
        val image = capture { SingleSurface(GlassMaterial.Regular, GlassAppearance.Light, Color(tint[0], tint[1], tint[2])) }
        assertMatchesReference(image, regular = true, dark = false, tint = tint)
    }

    @Test
    fun moreSurfacesThanTheGpuLimitAllDraw() {
        val count = GlassDefaults.MaxSurfacesPerScene + 4
        val image = capture {
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                for (i in 0 until count) {
                    Box(
                        Modifier
                            .offset((10 + (i % 5) * 60).dp, (10 + (i / 5) * 50).dp)
                            .size(48.dp, 40.dp)
                            .liquidGlass(GlassShape(12.dp), material = GlassMaterial.Regular),
                    )
                }
            }
        }
        for (i in 0 until count) {
            val center = image.color(10 + (i % 5) * 60 + 24, 10 + (i / 5) * 50 + 20)
            val changed = abs(center.red - backdropColor.red) + abs(center.green - backdropColor.green) + abs(center.blue - backdropColor.blue)
            assertTrue("surface $i draws no glass", changed > 0.05f)
        }
    }

    @Test
    fun surfaceOutsideASceneDrawsTheFallback() {
        val image = capture {
            Box(Modifier.fillMaxSize().background(backdropColor)) {
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular))
            }
        }
        val center = image.color(Left + Width / 2, Top + Height / 2)
        val rim = image.color(Left + Width / 2, Top)
        assertTrue("fallback fill missing", abs(center.red - backdropColor.red) > 0.05f)
        assertTrue("fallback rim missing", rim.red > center.red + 0.05f)
    }

    @Test
    fun glassInADialogInsideASceneDrawsItsOwnFallback() {
        // The dialog is another window: its glass must not join the scene's layer (bounds across windows throw).
        var drawn = false
        capture {
            GlassScene {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Dialog(onDismissRequest = {}) {
                    GlassSurface(Modifier.size(120.dp, 60.dp).drawBehind { drawn = true }, material = GlassMaterial.Regular) {}
                }
            }
        }
        assertTrue("dialog glass never drew", drawn)
    }

    @Test
    fun progressiveBlurSoftensTowardTheEdge() {
        assumeTrue(Build.VERSION.SDK_INT >= 31)
        val image = capture {
            GlassScene {
                GlassContent(Modifier.fillMaxSize()) {
                    // Vertical stripes, 4 px wide.
                    Row(Modifier.fillMaxSize()) {
                        repeat(SceneWidth / 4) { i -> Box(Modifier.size(4.dp, SceneHeight.dp).background(if (i % 2 == 0) Color.Black else Color.White)) }
                    }
                }
                GlassProgressiveBlur(GlassEdge.Top, Modifier.align(Alignment.TopCenter).fillMaxWidth(), size = 100.dp, blur = 20.dp)
            }
        }
        fun contrast(y: Int): Float {
            val row = (40 until SceneWidth - 40).map { image.color(it, y).red }
            val mean = row.average().toFloat()
            return sqrt(row.map { (it - mean) * (it - mean) }.average().toFloat())
        }
        val edge = contrast(2)
        val sharp = contrast(98)
        val below = contrast(150)
        assertTrue("edge not blurred: $edge vs $below", edge < below * 0.2f)
        assertTrue("sharp side blurred: $sharp vs $below", sharp > below * 0.8f)
    }

    private companion object {
        const val SceneWidth = 320
        const val SceneHeight = 260
        const val Left = 60
        const val Top = 50
        const val Width = 200
        const val Height = 120
        const val Radius = 24
        const val Tolerance = 2f / 255f
        const val SettleFrames = 12
    }
}
