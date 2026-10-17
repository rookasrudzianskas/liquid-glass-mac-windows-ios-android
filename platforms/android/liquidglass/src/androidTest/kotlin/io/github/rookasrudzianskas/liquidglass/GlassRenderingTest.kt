package io.github.rookasrudzianskas.liquidglass

import android.graphics.Bitmap
import android.content.res.Configuration
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import android.graphics.RuntimeShader
import android.view.Choreographer
import android.view.PixelCopy
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.PressInteraction
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.draw.scale
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ShaderBrush
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import io.github.rookasrudzianskas.liquidglass.internal.GlassDynamics
import io.github.rookasrudzianskas.liquidglass.internal.GlassSurfaceParams
import io.github.rookasrudzianskas.liquidglass.internal.glassSurface
import io.github.rookasrudzianskas.liquidglass.optics.defaultRefraction
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
import kotlin.math.sqrt

/**
 * Renders glass on the device GPU, through the library's own scene, and checks
 * the pixels against the Kotlin reference ([glassPixel], itself checked against
 * spec/vectors.json). Scenes run at density 1, so one dp is one pixel. Over a
 * uniform backdrop only tone, tint, coverage and rim light show; over the
 * linear gradient of the vectors' glassPixel cases refraction moves the color
 * too, so the displacement, edge profile and its filtering are covered end to
 * end. Tolerance is 2/255: the backdrop and the readback are 8-bit. The shader
 * functions themselves are checked against the vectors at 2e-3 by
 * [io.github.rookasrudzianskas.liquidglass.internal.ShaderVectorTest].
 */
@RunWith(AndroidJUnit4::class)
class GlassRenderingTest {

    private val backdrop = floatArrayOf(0.5f, 0.5f, 0.6f, 1f)
    private val backdropColor = Color(backdrop[0], backdrop[1], backdrop[2])

    /**
     * Composes [content] in a SceneWidth × SceneHeight box at density 1 and copies
     * its pixels from the window until [check] passes (returns null). A slow GPU
     * (a software-rendered CI emulator compiling the shader) may show a few frames
     * without the effect, so the copy is retried for up to [Patience] ms before the
     * test fails with the last message.
     */
    private fun assertRendered(content: @Composable () -> Unit, check: (Bitmap) -> String?) {
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
            val deadline = SystemClock.uptimeMillis() + Patience
            var failure: String?
            do {
                failure = check(copy(scenario, bounds))
                if (failure == null) return
                awaitFrames(scenario, RetryFrames)
            } while (SystemClock.uptimeMillis() < deadline)
            fail(failure)
        }
    }

    private fun copy(scenario: ActivityScenario<ComponentActivity>, bounds: Rect): Bitmap {
        val bitmap = Bitmap.createBitmap(SceneWidth, SceneHeight, Bitmap.Config.ARGB_8888)
        val done = CountDownLatch(1)
        var result = -1
        scenario.onActivity { activity ->
            val area = android.graphics.Rect(bounds.left.toInt(), bounds.top.toInt(), bounds.left.toInt() + SceneWidth, bounds.top.toInt() + SceneHeight)
            PixelCopy.request(activity.window, area, bitmap, { code -> result = code; done.countDown() }, Handler(Looper.getMainLooper()))
        }
        // The first copy on a cold software-rendered emulator waits for every shader to compile.
        assertTrue("PixelCopy timed out", done.await(Slow, TimeUnit.SECONDS))
        assertEquals("PixelCopy failed", PixelCopy.SUCCESS, result)
        return bitmap
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
        assertTrue("frames never arrived", done.await(Slow, TimeUnit.SECONDS))
    }

    private fun Bitmap.color(x: Int, y: Int): Color = Color(getPixel(x, y))

    /** The test surface (Width × Height at Left, Top) in a scene over the uniform backdrop, or over the linear [gradient]. */
    @Composable
    private fun SingleSurface(material: GlassMaterial, appearance: GlassAppearance, tint: Color = Color.Unspecified, gradient: Boolean = false) {
        GlassScene(appearance = appearance, increaseContrast = false) {
            GlassContent(Modifier.fillMaxSize()) {
                Box(Modifier.fillMaxSize().then(if (gradient) Modifier.background(LinearBackdropBrush) else Modifier.background(backdropColor)))
            }
            Box(
                Modifier
                    .offset(Left.dp, Top.dp)
                    .size(Width.dp, Height.dp)
                    .liquidGlass(GlassShape(Radius.dp), material = material, appearance = appearance, tint = tint),
            )
        }
    }

    /** The uniform backdrop, unpremultiplied (it is opaque). */
    private val uniformBackdrop: (Float, Float) -> FloatArray = { _, _ -> backdrop }

    /**
     * The backdrop of the vectors' glassPixel cases, around the test surface's
     * center: rgb = (0.5 + x/400, 0.5 + y/400, 0.6), with x, y in pixels from
     * the center of the surface at Left, Top.
     */
    private val linearBackdrop: (Float, Float) -> FloatArray = { x, y -> floatArrayOf(sat(0.5f + x / 400f), sat(0.5f + y / 400f), 0.6f, 1f) }

    /** [linearBackdrop] around a surface centered at ([centerX], [centerY]) in the scene instead. */
    private fun linearBackdropAround(centerX: Float, centerY: Float): (Float, Float) -> FloatArray {
        val dx = centerX - (Left + Width / 2f)
        val dy = centerY - (Top + Height / 2f)
        return { x, y -> linearBackdrop(x + dx, y + dy) }
    }

    /**
     * Null when every sampled pixel of a surface (width × height at left, top,
     * with [radius] corners, null for a capsule) is within [Tolerance] of the
     * reference, else where it is worst. [refraction] defaults to the travel of a
     * surface that sets none, measured on this size. [backdropAt] gives the backdrop the glass
     * refracts and [baseAt] what lies beneath it (the backdrop, or lower glass),
     * both relative to the surface center.
     */
    private fun differenceFromReference(
        pixels: Bitmap,
        regular: Boolean,
        dark: Boolean,
        tint: FloatArray?,
        left: Int = Left,
        top: Int = Top,
        width: Int = Width,
        height: Int = Height,
        radius: Float? = Radius.toFloat(),
        refraction: Float = defaultRefraction(width.toFloat(), height.toFloat()),
        nested: Boolean = false,
        backdropAt: (Float, Float) -> FloatArray = uniformBackdrop,
        baseAt: (Float, Float) -> FloatArray = backdropAt,
    ): String? {
        var worst = 0f
        var where = ""
        // Every pixel of three rows (the top rim, the middle, near the bottom rim) and two columns (left rim, middle).
        val samples = buildList {
            for (x in left - 2 until left + width + 2) {
                add(x to top)
                add(x to top + height / 2)
                add(x to top + height - 3)
            }
            for (y in top - 2 until top + height + 2) {
                add(left to y)
                add(left + width / 2 to y)
            }
        }
        for ((px, py) in samples) {
            val x = px + 0.5f - (left + width / 2f)
            val y = py + 0.5f - (top + height / 2f)
            val expected = glassPixel(
                x = x, y = y, width = width.toFloat(), height = height.toFloat(), radius = radius,
                regular = regular, dark = dark, refraction = refraction, pixel = 1f, tint = tint, nested = nested,
                base = baseAt(x, y), sample = { dx, dy -> backdropAt(x + dx, y + dy) },
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
        Log.i("GlassRenderingTest", "regular=$regular dark=$dark tint=${tint != null} at ($left, $top): max error ${worst * 255}/255 over ${samples.size} pixels")
        return if (worst <= Tolerance) null else "max error ${worst * 255}/255 at $where"
    }

    private fun Color.differsFrom(other: Color, by: Float = 0.05f): Boolean =
        abs(red - other.red) + abs(green - other.green) + abs(blue - other.blue) > by

    @Test
    fun clearLightMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Clear, GlassAppearance.Light) }) { differenceFromReference(it, regular = false, dark = false, tint = null) }
    }

    @Test
    fun clearDarkMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Clear, GlassAppearance.Dark) }) { differenceFromReference(it, regular = false, dark = true, tint = null) }
    }

    @Test
    fun regularLightMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Regular, GlassAppearance.Light) }) { differenceFromReference(it, regular = true, dark = false, tint = null) }
    }

    @Test
    fun regularDarkMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Regular, GlassAppearance.Dark) }) { differenceFromReference(it, regular = true, dark = true, tint = null) }
    }

    @Test
    fun tintedRegularMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val tint = floatArrayOf(0f, 0.478f, 1f)
        assertRendered({ SingleSurface(GlassMaterial.Regular, GlassAppearance.Light, Color(tint[0], tint[1], tint[2])) }) {
            differenceFromReference(it, regular = true, dark = false, tint = tint)
        }
    }

    @Test
    fun clearOverAGradientMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Clear, GlassAppearance.Light, gradient = true) }) {
            differenceFromReference(it, regular = false, dark = false, tint = null, backdropAt = linearBackdrop)
        }
    }

    @Test
    fun clearDarkOverAGradientMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Clear, GlassAppearance.Dark, gradient = true) }) {
            differenceFromReference(it, regular = false, dark = true, tint = null, backdropAt = linearBackdrop)
        }
    }

    @Test
    fun regularOverAGradientMatchesReference() {
        // A Gaussian blur leaves a linear gradient as it is, so the frost is the gradient itself.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        assertRendered({ SingleSurface(GlassMaterial.Regular, GlassAppearance.Light, gradient = true) }) {
            differenceFromReference(it, regular = true, dark = false, tint = null, backdropAt = linearBackdrop)
        }
    }

    @Test
    fun tintedClearOverAGradientMatchesReference() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val tint = floatArrayOf(0f, 0.478f, 1f)
        assertRendered({ SingleSurface(GlassMaterial.Clear, GlassAppearance.Light, Color(tint[0], tint[1], tint[2]), gradient = true) }) {
            differenceFromReference(it, regular = false, dark = false, tint = tint, backdropAt = linearBackdrop)
        }
    }

    @Test
    fun lightAndDarkRegularGlassInOneSceneMatchReference() {
        // The sharp pass and both frost passes are built from one shader in the same frame.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            GlassScene(increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                for ((x, appearance) in listOf(PairLeft to GlassAppearance.Light, PairRight to GlassAppearance.Dark)) {
                    Box(
                        Modifier
                            .offset(x.dp, Top.dp)
                            .size(PairWidth.dp, Height.dp)
                            .liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular, appearance = appearance),
                    )
                }
                Box(
                    Modifier
                        .offset(PairLeft.dp, (Top + Height + 20).dp)
                        .size(PairWidth.dp, 50.dp)
                        .liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Clear, appearance = GlassAppearance.Light),
                )
            }
        }
        assertRendered(scene) { image ->
            differenceFromReference(image, regular = true, dark = false, tint = null, left = PairLeft, width = PairWidth)
                ?: differenceFromReference(image, regular = true, dark = true, tint = null, left = PairRight, width = PairWidth)
                ?: differenceFromReference(image, regular = false, dark = false, tint = null, left = PairLeft, top = Top + Height + 20, width = PairWidth, height = 50)
        }
    }

    @Test
    fun backdropDrawnByTheContentModifierIsRefracted() {
        // GlassContent(Modifier.background(…)) must refract exactly like a backdrop drawn by the content lambda.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val half = SceneWidth / 2
        val stripes = Modifier.drawBehind {
            for (i in 0 until (size.width / 8f).toInt() + 1) {
                drawRect(StripeColors[i % StripeColors.size], topLeft = Offset(i * 8f, 0f), size = Size(8f, size.height))
            }
        }
        @Composable
        fun Half(drawnByModifier: Boolean) {
            Box(Modifier.size(half.dp, SceneHeight.dp)) {
                GlassScene(Modifier.fillMaxSize(), appearance = GlassAppearance.Light, increaseContrast = false) {
                    if (drawnByModifier) {
                        GlassContent(Modifier.fillMaxSize().background(backdropColor).then(stripes)) {}
                    } else {
                        GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor).then(stripes)) }
                    }
                    Box(Modifier.offset(10.dp, 40.dp).size(140.dp, 90.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular))
                    Box(Modifier.offset(10.dp, 150.dp).size(140.dp, 70.dp).liquidGlass(GlassShape(Radius.dp), tint = Color(0xFF0A84FF)))
                }
            }
        }
        val scene = @Composable {
            Row {
                Half(drawnByModifier = true)
                Half(drawnByModifier = false)
            }
        }
        assertRendered(scene) { image ->
            var worst = 0f
            var where = ""
            for (y in listOf(40, 41, 85, 128, 150, 151, 185, 218)) {
                for (x in 6 until 154) {
                    val a = image.color(x, y)
                    val b = image.color(x + half, y)
                    val error = maxOf(abs(a.red - b.red), abs(a.green - b.green), abs(a.blue - b.blue))
                    if (error > worst) {
                        worst = error
                        where = "($x, $y)"
                    }
                }
            }
            val glass = image.color(80, 85)
            val bare = image.color(80, 5)
            when {
                worst > Tolerance -> "modifier-drawn backdrop differs by ${worst * 255}/255 at $where"
                !glass.differsFrom(bare) -> "no glass over the modifier-drawn backdrop"
                else -> null
            }
        }
    }

    @Test
    fun surfaceOffTheContentLayerDrawsItsOwnFallback() {
        // The content covers the top 120 px only: glass wholly below it draws the fallback; glass across its edge keeps the GPU glass.
        val sceneColor = Color(0.2f, 0.3f, 0.25f)
        val scene = @Composable {
            GlassScene(Modifier.fillMaxSize().background(sceneColor), appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxWidth().height(120.dp)) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(Left.dp, 80.dp).size(Width.dp, 80.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular))
                Box(Modifier.offset(Left.dp, 170.dp).size(Width.dp, 80.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular))
            }
        }
        assertRendered(scene) { image ->
            val center = image.color(Left + Width / 2, 210)
            val rim = image.color(Left + Width / 2, 170)
            val straddling = image.color(Left + Width / 2, 100)
            val expected = glassPixel(
                x = 0.5f, y = 100.5f - 120f, width = Width.toFloat(), height = 80f, radius = Radius.toFloat(),
                regular = true, dark = false, refraction = 60f, pixel = 1f, tint = null,
                base = backdrop, sample = { _, _ -> backdrop },
            )
            val straddlingError = maxOf(abs(straddling.red - expected[0]), abs(straddling.green - expected[1]), abs(straddling.blue - expected[2]))
            when {
                !center.differsFrom(sceneColor) -> "glass below the content draws nothing"
                rim.red <= center.red + 0.05f -> "glass below the content has no rim"
                Build.VERSION.SDK_INT >= 33 && straddlingError > Tolerance -> "glass across the content's edge lost the GPU glass: off by ${straddlingError * 255}/255"
                else -> null
            }
        }
    }

    @Test
    fun partlyClippedGlassGrowsPastItsRestingEdges() {
        // A 60 px circle with its left 20 px clipped by a container, its glass grown 4 px per side
        // past the node, as a press grows it (system reduce motion, on in CI, turns press growth off,
        // so the growth is set directly). It shows above, below and to the right; nothing shows left
        // of the container.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val container = Offset(80f, 60f)
        val grown = GlassDynamics(bounds = { size -> Rect(-4f, -4f, size.width + 4f, size.height + 4f) })
        val params = GlassSurfaceParams(
            shape = GlassShape.Circle,
            material = null,
            appearance = null,
            tint = Color.Unspecified,
            refraction = Dp.Unspecified,
            interactive = false,
            motion = null,
            opacity = 1f,
            interactionSource = null,
        )
        val scene = @Composable {
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(container.x.dp, container.y.dp).size(160.dp, 120.dp).clipToBounds()) {
                    Box(Modifier.offset((-20).dp, 30.dp).size(60.dp, 60.dp).glassSurface(params, grown))
                }
            }
        }
        assertRendered(scene) { image ->
            val cx = (container.x - 20f + 30f).toInt()
            val top = (container.y + 30f).toInt()
            val bottom = top + 60
            val right = (container.x - 20f + 60f).toInt()
            // The rim light brightens the grown outline above and below, and its side shade darkens it beside.
            fun strongest(points: List<Pair<Int, Int>>): Float = points.maxOf { (x, y) ->
                val c = image.color(x, y)
                abs(c.red - backdropColor.red) + abs(c.green - backdropColor.green) + abs(c.blue - backdropColor.blue)
            }
            val above = strongest((top - 4 until top).map { cx to it })
            val below = strongest((bottom until bottom + 4).map { cx to it })
            // The side shade sits on the outline itself, half a pixel either side of the grown edge.
            val beside = strongest((right until right + 6).map { it to top + 30 })
            val outside = strongest((container.x.toInt() - 6 until container.x.toInt()).map { it to top + 30 })
            when {
                above <= 0.15f -> "growth above the resting edge is clipped"
                below <= 0.15f -> "growth below the resting edge is clipped"
                beside <= 0.15f -> "growth beside the resting edge is clipped"
                outside > 0.02f -> "glass leaks past the container's clip"
                else -> null
            }
        }
    }

    @Test
    fun glassAddedToAParentLaterStaysBelowItsChild() {
        // The parent gains its glass after the child registered; the red child must still draw above it.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            var parentGlass by remember { mutableStateOf(false) }
            LaunchedEffect(Unit) {
                repeat(4) { withFrameNanos { } }
                parentGlass = true
            }
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(
                    Modifier
                        .offset(Left.dp, 40.dp)
                        .size(170.dp, 170.dp)
                        .then(if (parentGlass) Modifier.liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular) else Modifier),
                ) {
                    Box(Modifier.align(Alignment.Center).size(90.dp).liquidGlass(GlassShape(Radius.dp), tint = Color.Red))
                }
            }
        }
        assertRendered(scene) { image ->
            val child = image.color(Left + 85, 40 + 85)
            val parent = image.color(Left + 20, 40 + 85)
            when {
                !parent.differsFrom(backdropColor, by = 0.02f) -> "the parent's glass never appeared"
                child.red < child.green + 0.3f || child.red < child.blue + 0.3f -> "the child is covered by its parent's glass: $child"
                else -> null
            }
        }
    }

    @Test
    fun sceneAppearanceChangeReachesGlassOnScreen() {
        // The scene turns dark after the glass has drawn light: the glass on screen must follow.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            var appearance by remember { mutableStateOf(GlassAppearance.Light) }
            LaunchedEffect(Unit) {
                repeat(4) { withFrameNanos { } }
                appearance = GlassAppearance.Dark
            }
            GlassScene(appearance = appearance, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular))
            }
        }
        assertRendered(scene) { differenceFromReference(it, regular = true, dark = true, tint = null) }
    }

    @Test
    fun reduceTransparencyChangeReachesGlassOnScreen() {
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            var reduce by remember { mutableStateOf(false) }
            LaunchedEffect(Unit) {
                repeat(4) { withFrameNanos { } }
                reduce = true
            }
            GlassScene(appearance = GlassAppearance.Light, reduceTransparency = reduce, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).liquidGlass(GlassShape(Radius.dp)))
            }
        }
        assertRendered(scene) { image ->
            // The opaque fill #F2F2F7 at 94% over the backdrop.
            val expected = floatArrayOf(0.94902f, 0.94902f, 0.96863f).mapIndexed { i, c -> c * 0.94f + backdrop[i] * 0.06f }
            val center = image.color(Left + Width / 2, Top + Height / 2)
            val error = maxOf(abs(center.red - expected[0]), abs(center.green - expected[1]), abs(center.blue - expected[2]))
            if (error > Tolerance) "reduce transparency did not reach the glass: off by ${error * 255}/255" else null
        }
    }

    @Test
    fun increaseContrastChangeReachesGlassOnScreen() {
        // Increase contrast turns Clear glass Regular and adds the inner border.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            var contrast by remember { mutableStateOf(false) }
            LaunchedEffect(Unit) {
                repeat(4) { withFrameNanos { } }
                contrast = true
            }
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = contrast) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).liquidGlass(GlassShape(Radius.dp)))
            }
        }
        assertRendered(scene) { image ->
            val expected = glassPixel(
                x = 0.5f, y = 0.5f, width = Width.toFloat(), height = Height.toFloat(), radius = Radius.toFloat(),
                regular = true, dark = false, refraction = 60f, pixel = 1f, tint = null,
                base = backdrop, sample = { _, _ -> backdrop },
            )
            val center = image.color(Left + Width / 2, Top + Height / 2)
            val error = maxOf(abs(center.red - expected[0]), abs(center.green - expected[1]), abs(center.blue - expected[2]))
            if (error > Tolerance) "increase contrast did not reach the glass: off by ${error * 255}/255" else null
        }
    }

    @Test
    fun glassFollowsAnAncestorLayerTranslation() {
        // graphicsLayer(translationX = …) moves the glass without a relayout.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            var shift by remember { mutableStateOf(0f) }
            LaunchedEffect(Unit) {
                repeat(4) { withFrameNanos { } }
                shift = -40f
            }
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).graphicsLayer(translationX = shift)) {
                    Box(Modifier.fillMaxSize().liquidGlass(GlassShape(Radius.dp)))
                }
            }
        }
        assertRendered(scene) { differenceFromReference(it, regular = false, dark = false, tint = null, left = Left - 40) }
    }

    @Test
    fun glassUnderAnAncestorScaleIsAScaledCopy() {
        // Modifier.scale(0.8f → 0.5f): the glass follows without a relayout, and its corners and refraction scale too.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            var factor by remember { mutableStateOf(0.8f) }
            LaunchedEffect(Unit) {
                repeat(4) { withFrameNanos { } }
                factor = 0.5f
            }
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(LinearBackdropBrush)) }
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).scale(factor)) {
                    Box(Modifier.fillMaxSize().liquidGlass(GlassShape(Radius.dp)))
                }
            }
        }
        assertRendered(scene) {
            differenceFromReference(
                it, regular = false, dark = false, tint = null,
                left = Left + Width / 4, top = Top + Height / 4, width = Width / 2, height = Height / 2,
                radius = Radius / 2f, refraction = 30f, backdropAt = linearBackdrop,
            )
        }
    }

    @Test
    fun unsetRefractionFollowsTheSurfaceSize() {
        // A 120 × 34 capsule (a small button) over the gradient travels 0.75 × 34 = 25.5, not the full 60.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(LinearBackdropBrush)) }
                Box(Modifier.offset(SmallLeft.dp, SmallTop.dp).size(SmallWidth.dp, SmallHeight.dp).liquidGlass(GlassShape.Capsule))
            }
        }
        assertRendered(scene) { image ->
            fun differenceAt(refraction: Float) = differenceFromReference(
                image, regular = false, dark = false, tint = null,
                left = SmallLeft, top = SmallTop, width = SmallWidth, height = SmallHeight,
                radius = null, refraction = refraction, backdropAt = linearBackdrop,
            )
            when {
                differenceAt(60f) == null -> "the small capsule refracts the full 60"
                else -> differenceAt(25.5f)
            }
        }
    }

    @Test
    fun unsetRefractionIsMeasuredOnTheRestingSize() {
        // The capsule's glass grown 12 px per side, as a press grows it: still 25.5 (its resting 34),
        // not the 43.5 its grown 58 would give.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val grown = GlassDynamics(bounds = { size -> Rect(-12f, -12f, size.width + 12f, size.height + 12f) })
        val params = GlassSurfaceParams(
            shape = GlassShape.Capsule,
            material = null,
            appearance = null,
            tint = Color.Unspecified,
            refraction = Dp.Unspecified,
            interactive = false,
            motion = null,
            opacity = 1f,
            interactionSource = null,
        )
        val scene = @Composable {
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(LinearBackdropBrush)) }
                Box(Modifier.offset(SmallLeft.dp, SmallTop.dp).size(SmallWidth.dp, SmallHeight.dp).glassSurface(params, grown))
            }
        }
        assertRendered(scene) {
            differenceFromReference(
                it, regular = false, dark = false, tint = null,
                left = SmallLeft - 12, top = SmallTop - 12, width = SmallWidth + 24, height = SmallHeight + 24,
                radius = null, refraction = 25.5f, backdropAt = linearBackdrop,
            )
        }
    }

    @Test
    fun explicitRefractionIsUsedAsGiven() {
        // The scene sets 50: the unset capsule uses it as given (not 25.5); a capsule that sets 60 keeps its own.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val upperTop = 50
        val lowerTop = 150
        val scene = @Composable {
            GlassScene(appearance = GlassAppearance.Light, refraction = 50.dp, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(LinearBackdropBrush)) }
                Box(Modifier.offset(SmallLeft.dp, upperTop.dp).size(SmallWidth.dp, SmallHeight.dp).liquidGlass(GlassShape.Capsule))
                Box(Modifier.offset(SmallLeft.dp, lowerTop.dp).size(SmallWidth.dp, SmallHeight.dp).liquidGlass(GlassShape.Capsule, refraction = 60.dp))
            }
        }
        val centerX = SmallLeft + SmallWidth / 2f
        assertRendered(scene) { image ->
            differenceFromReference(
                image, regular = false, dark = false, tint = null,
                left = SmallLeft, top = upperTop, width = SmallWidth, height = SmallHeight, radius = null, refraction = 50f,
                backdropAt = linearBackdropAround(centerX, upperTop + SmallHeight / 2f),
            )?.let { "scene refraction: $it" } ?: differenceFromReference(
                image, regular = false, dark = false, tint = null,
                left = SmallLeft, top = lowerTop, width = SmallWidth, height = SmallHeight, radius = null, refraction = 60f,
                backdropAt = linearBackdropAround(centerX, lowerTop + SmallHeight / 2f),
            )?.let { "surface refraction: $it" }
        }
    }

    @Test
    fun clearGlassOverRegularGlassKeepsItsSideShade() {
        // A Clear capsule inside a Regular card composites over the card, its rim's side shade included.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val capsuleLeft = Left + 40
        val capsuleTop = Top + 38
        val capsuleWidth = 120
        val capsuleHeight = 44
        val scene = @Composable {
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular)) {
                    Box(
                        Modifier
                            .offset((capsuleLeft - Left).dp, (capsuleTop - Top).dp)
                            .size(capsuleWidth.dp, capsuleHeight.dp)
                            .liquidGlass(GlassShape.Capsule, material = GlassMaterial.Clear),
                    )
                }
            }
        }
        // The card at a point given relative to the capsule's center.
        val dx = capsuleLeft + capsuleWidth / 2f - (Left + Width / 2f)
        val dy = capsuleTop + capsuleHeight / 2f - (Top + Height / 2f)
        val card: (Float, Float) -> FloatArray = { x, y ->
            glassPixel(
                x = x + dx, y = y + dy, width = Width.toFloat(), height = Height.toFloat(), radius = Radius.toFloat(),
                regular = true, dark = false, refraction = 60f, pixel = 1f, tint = null,
                base = backdrop, sample = { _, _ -> backdrop },
            )
        }
        assertRendered(scene) {
            differenceFromReference(
                it, regular = false, dark = false, tint = null,
                left = capsuleLeft, top = capsuleTop, width = capsuleWidth, height = capsuleHeight,
                radius = null, nested = true, baseAt = card,
            )
        }
    }

    @Test
    fun fadedOutGlassLeavesItsGpuSlotToVisibleGlass() {
        // Two invisible surfaces (opacity 0, like a tab bar's resting lens) come first; the 16 visible ones all get the GPU glass.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val visible = GlassDefaults.MaxSurfacesPerScene
        val scene = @Composable {
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                repeat(2) { Box(Modifier.offset(0.dp, 0.dp).size(48.dp, 40.dp).liquidGlass(GlassShape(12.dp), opacity = 0f)) }
                for (i in 0 until visible) {
                    Box(Modifier.offset((10 + (i % 5) * 60).dp, (10 + (i / 5) * 50).dp).size(48.dp, 40.dp).liquidGlass(GlassShape(12.dp)))
                }
            }
        }
        val expected = glassPixel(
            x = 0.5f, y = 0.5f, width = 48f, height = 40f, radius = 12f,
            regular = false, dark = false, refraction = defaultRefraction(48f, 40f), pixel = 1f, tint = null,
            base = backdrop, sample = { _, _ -> backdrop },
        )
        assertRendered(scene) { image ->
            (0 until visible).firstNotNullOfOrNull { i ->
                val center = image.color(10 + (i % 5) * 60 + 24, 10 + (i / 5) * 50 + 20)
                val error = maxOf(abs(center.red - expected[0]), abs(center.green - expected[1]), abs(center.blue - expected[2]))
                if (error > Tolerance) "surface $i is not GPU glass: off by ${error * 255}/255" else null
            }
        }
    }

    @Test
    fun nonInteractiveGlassIgnoresItsInteractionSource() {
        // interactive = false: a press from the interaction source neither grows nor lights the glass.
        assumeTrue(Build.VERSION.SDK_INT >= 33)
        val scene = @Composable {
            val source = remember { MutableInteractionSource() }
            LaunchedEffect(source) {
                repeat(2) { withFrameNanos { } }
                source.emit(PressInteraction.Press(Offset(Width / 2f, Height / 2f)))
            }
            GlassScene(appearance = GlassAppearance.Light, increaseContrast = false) {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Box(
                    Modifier
                        .offset(Left.dp, Top.dp)
                        .size(Width.dp, Height.dp)
                        .liquidGlass(GlassShape(Radius.dp), interactive = false, interactionSource = source),
                )
            }
        }
        assertRendered(scene) { differenceFromReference(it, regular = false, dark = false, tint = null) }
    }

    @Test
    fun glassOutsideASceneFollowsTheSystemDarkTheme() {
        // No scene: inherited appearance follows the system's night mode, like explicit dark glass.
        val night = Configuration(InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration).apply {
            uiMode = (uiMode and Configuration.UI_MODE_NIGHT_MASK.inv()) or Configuration.UI_MODE_NIGHT_YES
        }
        val scene = @Composable {
            CompositionLocalProvider(LocalConfiguration provides night) {
                Box(Modifier.fillMaxSize().background(backdropColor)) {
                    Box(Modifier.offset(PairLeft.dp, Top.dp).size(PairWidth.dp, Height.dp).liquidGlass(GlassShape(Radius.dp)))
                    Box(Modifier.offset(PairRight.dp, Top.dp).size(PairWidth.dp, Height.dp).liquidGlass(GlassShape(Radius.dp), appearance = GlassAppearance.Dark))
                    Box(Modifier.offset(PairLeft.dp, (Top + Height + 10).dp).size(PairWidth.dp, 60.dp).liquidGlass(GlassShape(Radius.dp), appearance = GlassAppearance.Light))
                }
            }
        }
        assertRendered(scene) { image ->
            val inherited = image.color(PairLeft + PairWidth / 2, Top + Height / 2)
            val dark = image.color(PairRight + PairWidth / 2, Top + Height / 2)
            val light = image.color(PairLeft + PairWidth / 2, Top + Height + 40)
            when {
                !dark.differsFrom(light, by = 0.03f) -> "dark and light fallback glass look the same"
                inherited.differsFrom(dark, by = 3 * Tolerance) -> "glass outside a scene ignores the system dark theme: $inherited vs dark $dark"
                else -> null
            }
        }
    }

    @Test
    fun moreSurfacesThanTheGpuLimitAllDraw() {
        val count = GlassDefaults.MaxSurfacesPerScene + 4
        val scene = @Composable {
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
        assertRendered(scene) { image ->
            (0 until count).firstOrNull { i ->
                val center = image.color(10 + (i % 5) * 60 + 24, 10 + (i / 5) * 50 + 20)
                abs(center.red - backdropColor.red) + abs(center.green - backdropColor.green) + abs(center.blue - backdropColor.blue) <= 0.05f
            }?.let { "surface $it draws no glass" }
        }
    }

    @Test
    fun surfaceOutsideASceneDrawsTheFallback() {
        val scene = @Composable {
            Box(Modifier.fillMaxSize().background(backdropColor)) {
                Box(Modifier.offset(Left.dp, Top.dp).size(Width.dp, Height.dp).liquidGlass(GlassShape(Radius.dp), material = GlassMaterial.Regular))
            }
        }
        assertRendered(scene) { image ->
            val center = image.color(Left + Width / 2, Top + Height / 2)
            val rim = image.color(Left + Width / 2, Top)
            when {
                abs(center.red - backdropColor.red) <= 0.05f -> "fallback fill missing"
                rim.red <= center.red + 0.05f -> "fallback rim missing"
                else -> null
            }
        }
    }

    @Test
    fun glassInADialogInsideASceneDrawsItsOwnFallback() {
        // The dialog is another window: its glass must not join the scene's layer (bounds across windows throw).
        var drawn = false
        val scene = @Composable {
            GlassScene {
                GlassContent(Modifier.fillMaxSize()) { Box(Modifier.fillMaxSize().background(backdropColor)) }
                Dialog(onDismissRequest = {}) {
                    GlassSurface(Modifier.size(120.dp, 60.dp).drawBehind { drawn = true }, material = GlassMaterial.Regular) {}
                }
            }
        }
        assertRendered(scene) { if (drawn) null else "dialog glass never drew" }
    }

    @Test
    fun progressiveBlurSoftensTowardTheEdge() {
        assumeTrue(Build.VERSION.SDK_INT >= 31)
        val scene = @Composable {
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
        assertRendered(scene) { image ->
            fun contrast(y: Int): Float {
                val row = (40 until SceneWidth - 40).map { image.color(it, y).red }
                val mean = row.average().toFloat()
                return sqrt(row.map { (it - mean) * (it - mean) }.average().toFloat())
            }
            val edge = contrast(2)
            val sharp = contrast(98)
            val below = contrast(150)
            when {
                edge >= below * 0.2f -> "edge not blurred: $edge vs $below"
                sharp <= below * 0.8f -> "sharp side blurred: $sharp vs $below"
                else -> null
            }
        }
    }

    private companion object {
        const val SceneWidth = 320
        const val SceneHeight = 260
        const val Left = 60
        const val Top = 50
        const val Width = 200
        const val Height = 120
        const val Radius = 24

        /** A small button's capsule (120 × 34), centered on the test surface and the gradient. */
        const val SmallWidth = 120
        const val SmallHeight = 34
        const val SmallLeft = Left + (Width - SmallWidth) / 2
        const val SmallTop = Top + (Height - SmallHeight) / 2

        /** Two surfaces side by side. */
        const val PairLeft = 10
        const val PairRight = 170
        const val PairWidth = 140

        val StripeColors = listOf(Color(0xFFE53935), Color(0xFFFDD835), Color(0xFF43A047), Color(0xFF1E88E5), Color(0xFF8E24AA))

        /**
         * The linear backdrop of the glassPixel vectors, drawn exactly (a runtime
         * shader is neither dithered nor interpolated), centered on the test surface.
         */
        val LinearBackdropBrush: ShaderBrush by lazy {
            val shader = RuntimeShader(
                """
                uniform float2 uCenter;
                half4 main(float2 p) {
                  float2 d = p - uCenter;
                  return half4(half(clamp(0.5 + d.x / 400.0, 0.0, 1.0)), half(clamp(0.5 + d.y / 400.0, 0.0, 1.0)), 0.6, 1.0);
                }
                """.trimIndent(),
            )
            shader.setFloatUniform("uCenter", Left + Width / 2f, Top + Height / 2f)
            object : ShaderBrush() {
                override fun createShader(size: Size): android.graphics.Shader = shader
            }
        }
        const val Tolerance = 2f / 255f
        const val SettleFrames = 12
        const val RetryFrames = 6
        const val Patience = 8_000L

        /** Seconds to wait for a frame or a pixel copy before giving up. */
        const val Slow = 30L
    }
}
