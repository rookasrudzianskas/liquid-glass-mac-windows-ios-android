package io.github.rookasrudzianskas.liquidglass

import android.graphics.Bitmap
import android.graphics.RuntimeShader
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import android.view.Choreographer
import android.view.PixelCopy
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ShaderBrush
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import io.github.rookasrudzianskas.liquidglass.optics.glassPixel
import io.github.rookasrudzianskas.liquidglass.optics.sat
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Assume.assumeTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * The reference cases of [GlassRenderingTest] at a real phone density (2.625,
 * a 420 dpi screen) instead of 1, so every dp-to-pixel conversion runs on the
 * GPU: the frost σ, the one-pixel antialiasing and rim widths, the bevel and its
 * compression. Layout rounds the surface to whole pixels, so the reference is
 * evaluated for the bounds the surface actually got, in dp, at pixel = 1/density.
 */
@RunWith(AndroidJUnit4::class)
class GlassDensityTest {

    private val backdrop = floatArrayOf(0.5f, 0.5f, 0.6f, 1f)
    private val backdropColor = Color(backdrop[0], backdrop[1], backdrop[2])

    /** rgb = (0.5 + x/400, 0.5 + y/400, 0.6) with x, y in dp from [GradientCenter] of the scene. */
    private fun gradientAt(x: Float, y: Float): FloatArray =
        floatArrayOf(sat(0.5f + (x - GradientCenter) / 400f), sat(0.5f + (y - GradientCenter) / 400f), 0.6f, 1f)

    @Test
    fun clearLightMatchesReference() = check(GlassMaterial.Clear, GlassAppearance.Light)

    @Test
    fun regularDarkMatchesReference() = check(GlassMaterial.Regular, GlassAppearance.Dark)

    @Test
    fun tintedClearMatchesReference() = check(GlassMaterial.Clear, GlassAppearance.Light, tint = floatArrayOf(0f, 0.478f, 1f))

    @Test
    fun clearOverAGradientMatchesReference() = check(GlassMaterial.Clear, GlassAppearance.Light, gradient = true)

    @Test
    fun regularOverAGradientMatchesReference() = check(GlassMaterial.Regular, GlassAppearance.Light, gradient = true)

    private fun check(material: GlassMaterial, appearance: GlassAppearance, tint: FloatArray? = null, gradient: Boolean = false) {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        ActivityScenario.launch(ComponentActivity::class.java).use { scenario ->
            var scene = Rect.Zero
            var surface = Rect.Zero
            scenario.onActivity { activity ->
                activity.setContent {
                    CompositionLocalProvider(LocalDensity provides Density(DENSITY)) {
                        Box(Modifier.size(SceneWidth.dp, SceneHeight.dp).onGloballyPositioned { scene = it.boundsInWindow() }) {
                            GlassScene(appearance = appearance, increaseContrast = false) {
                                GlassContent(Modifier.fillMaxSize()) {
                                    Box(Modifier.fillMaxSize().then(if (gradient) Modifier.background(gradientBrush()) else Modifier.background(backdropColor)))
                                }
                                Box(
                                    Modifier
                                        .offset(Left.dp, Top.dp)
                                        .size(Width.dp, Height.dp)
                                        .onGloballyPositioned { surface = it.boundsInWindow() }
                                        .liquidGlass(
                                            GlassShape(Radius.dp),
                                            material = material,
                                            appearance = appearance,
                                            tint = tint?.let { Color(it[0], it[1], it[2]) } ?: Color.Unspecified,
                                        ),
                                )
                            }
                        }
                    }
                }
            }
            awaitFrames(scenario, 12)
            val deadline = SystemClock.uptimeMillis() + 8_000L
            var failure: String?
            do {
                failure = difference(copy(scenario, scene), surface.translate(-scene.left, -scene.top), material == GlassMaterial.Regular, appearance == GlassAppearance.Dark, tint, gradient)
                if (failure == null) return
                awaitFrames(scenario, 6)
            } while (SystemClock.uptimeMillis() < deadline)
            fail(failure)
        }
    }

    /** The gradient, drawn exactly by a runtime shader in the scene's pixels. */
    private fun gradientBrush(): ShaderBrush {
        val shader = RuntimeShader(
            """
            uniform float uDensity;
            uniform float uCenter;
            half4 main(float2 p) {
              float2 d = p / uDensity - uCenter;
              return half4(half(clamp(0.5 + d.x / 400.0, 0.0, 1.0)), half(clamp(0.5 + d.y / 400.0, 0.0, 1.0)), 0.6, 1.0);
            }
            """.trimIndent(),
        )
        shader.setFloatUniform("uDensity", DENSITY)
        shader.setFloatUniform("uCenter", GradientCenter)
        return object : ShaderBrush() {
            override fun createShader(size: Size): android.graphics.Shader = shader
        }
    }

    /** Null when the rim rows and columns of the surface at [bounds] (scene pixels) match the reference within 2/255. */
    private fun difference(pixels: Bitmap, bounds: Rect, regular: Boolean, dark: Boolean, tint: FloatArray?, gradient: Boolean): String? {
        val left = bounds.left.roundToInt()
        val top = bounds.top.roundToInt()
        val right = bounds.right.roundToInt()
        val bottom = bounds.bottom.roundToInt()
        val widthDp = (right - left) / DENSITY
        val heightDp = (bottom - top) / DENSITY
        val centerX = (left + right) / 2f
        val centerY = (top + bottom) / 2f
        val samples = buildList {
            for (x in left - 3 until right + 3) {
                add(x to top)
                add(x to top + 1)
                add(x to (top + bottom) / 2)
                add(x to bottom - 2)
            }
            for (y in top - 3 until bottom + 3) {
                add(left to y)
                add(left + 1 to y)
                add((left + right) / 2 to y)
            }
        }
        var worst = 0f
        var where = ""
        for ((px, py) in samples) {
            // Pixel centers, in dp from the surface center and from the scene origin.
            val x = (px + 0.5f - centerX) / DENSITY
            val y = (py + 0.5f - centerY) / DENSITY
            val sceneX = (px + 0.5f) / DENSITY
            val sceneY = (py + 0.5f) / DENSITY
            val backdropAt = { dx: Float, dy: Float -> if (gradient) gradientAt(sceneX + dx, sceneY + dy) else backdrop }
            val expected = glassPixel(
                x = x, y = y, width = widthDp, height = heightDp, radius = Radius.toFloat(),
                regular = regular, dark = dark, refraction = 60f, pixel = 1f / DENSITY, tint = tint,
                base = backdropAt(0f, 0f), sample = backdropAt,
            )
            val actual = Color(pixels.getPixel(px, py))
            val channels = floatArrayOf(actual.red, actual.green, actual.blue)
            for (c in 0..2) {
                val error = abs(channels[c] - expected[c])
                if (error > worst) {
                    worst = error
                    where = "($px, $py) channel $c: expected ${expected[c]}, got ${channels[c]}"
                }
            }
        }
        Log.i("GlassDensityTest", "density=$DENSITY regular=$regular dark=$dark tint=${tint != null} gradient=$gradient: max error ${worst * 255}/255 over ${samples.size} pixels")
        return if (worst <= Tolerance) null else "max error ${worst * 255}/255 at $where"
    }

    private fun copy(scenario: ActivityScenario<ComponentActivity>, bounds: Rect): Bitmap {
        val width = bounds.width.roundToInt()
        val height = bounds.height.roundToInt()
        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        val done = CountDownLatch(1)
        var result = -1
        scenario.onActivity { activity ->
            val area = android.graphics.Rect(bounds.left.roundToInt(), bounds.top.roundToInt(), bounds.left.roundToInt() + width, bounds.top.roundToInt() + height)
            PixelCopy.request(activity.window, area, bitmap, { code -> result = code; done.countDown() }, Handler(Looper.getMainLooper()))
        }
        assertTrue("PixelCopy timed out", done.await(30, TimeUnit.SECONDS))
        assertEquals("PixelCopy failed", PixelCopy.SUCCESS, result)
        return bitmap
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
        assertTrue("frames never arrived", done.await(30, TimeUnit.SECONDS))
    }

    private companion object {
        /** A 420 dpi phone. The scene (272 dp, 714 px) fits a 720 px wide screen. */
        const val DENSITY = 2.625f
        const val SceneWidth = 272
        const val SceneHeight = 220
        const val Left = 66
        const val Top = 60
        const val Width = 140
        const val Height = 90
        const val Radius = 24
        const val GradientCenter = 136f
        const val Tolerance = 2f / 255f
    }
}
