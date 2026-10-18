package io.github.rookasrudzianskas.liquidglass.optics

import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassDefaults
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.float
import kotlinx.serialization.json.floatOrNull
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import kotlin.math.abs

/**
 * Checks the Kotlin optics against spec/vectors.json, the golden data every
 * platform port reproduces (tolerance 1e-4 on CPU code, per spec/OPTICS.md).
 */
class GoldenVectorsTest {

    private val vectors: JsonObject = run {
        val path = System.getProperty("liquidglass.vectors")
            ?: error("liquidglass.vectors is not set; run the tests through Gradle")
        Json.parseToJsonElement(File(path).readText()).jsonObject
    }

    private fun list(name: String): List<JsonObject> = vectors.getValue(name).jsonArray.map { it.jsonObject }

    private fun JsonObject.f(name: String): Float = getValue(name).jsonPrimitive.float
    private fun JsonObject.b(name: String): Boolean = getValue(name).jsonPrimitive.boolean
    private fun JsonObject.floats(name: String): FloatArray = getValue(name).jsonArray.map { it.jsonPrimitive.float }.toFloatArray()
    private fun JsonObject.floatsOrNull(name: String): FloatArray? = (get(name) as? JsonArray)?.map { it.jsonPrimitive.float }?.toFloatArray()

    /** Radius field: a number, or "capsule" / "circle" (both fully round, null here). */
    private fun JsonObject.radius(): Float? = getValue("radius").jsonPrimitive.floatOrNull

    private fun close(expected: Float, actual: Float, what: String, tolerance: Float = TOLERANCE) {
        assertTrue("$what: expected $expected, got $actual", abs(expected - actual) <= tolerance)
    }

    private fun close(expected: FloatArray, actual: FloatArray, what: String) {
        assertEquals("$what: length", expected.size, actual.size)
        for (i in expected.indices) close(expected[i], actual[i], "$what[$i]")
    }

    @Test
    fun constantsMatch() {
        val constants = vectors.getValue("constants").jsonObject
        assertEquals(constants.f("bevel"), BEVEL)
        assertEquals(constants.f("defaultRefraction"), DEFAULT_REFRACTION)
        assertEquals(constants.f("continuousExtent"), CONTINUOUS_EXTENT)
        assertEquals(constants.f("continuousExponent"), CONTINUOUS_EXPONENT)
        close(floatArrayOf(LUMA_R, LUMA_G, LUMA_B), constants.floats("luma"), "luma")

        val materials = vectors.getValue("materials").jsonObject
        val regular = materials.getValue("regular").jsonObject
        assertEquals(regular.f("blur"), FROST_SIGMA)
        assertEquals(regular.f("tintBody"), tintBody(regular = true))
        assertEquals(materials.getValue("clear").jsonObject.f("tintBody"), tintBody(regular = false))
        for (dark in listOf(false, true)) {
            val spec = regular.getValue(if (dark) "dark" else "light").jsonObject
            assertEquals(spec.f("fillSigma"), frostFillSigma(dark))
            assertEquals(spec.f("fillOpacity"), frostFillOpacity(dark))
            assertEquals(spec.f("chroma"), regularChroma(dark))
        }
    }

    @Test
    fun shapeParamsMatch() {
        val cases = list("shapeParams")
        for (c in cases) {
            val p = shapeParams(c.f("width"), c.f("height"), c.radius())
            close(c.f("extent"), p.extent, "extent $c")
            close(c.f("exponent"), p.exponent, "exponent $c")
        }
        assertEquals(8, cases.size)
    }

    @Test
    fun shapeFieldMatches() {
        val cases = list("shapeField")
        for (c in cases) {
            val p = shapeParams(c.f("width"), c.f("height"), c.radius())
            val f = shapeField(c.f("x"), c.f("y"), c.f("width"), c.f("height"), p.extent, p.exponent)
            close(c.f("distance"), f.distance, "distance $c")
            close(c.f("nx"), f.nx, "nx $c")
            close(c.f("ny"), f.ny, "ny $c")
        }
        assertEquals(640, cases.size)
    }

    @Test
    fun outlineMatches() {
        val outline = vectors.getValue("outline").jsonObject
        val expected = outline.getValue("points").jsonArray.flatMap { point -> point.jsonArray.map { it.jsonPrimitive.float } }
        val actual = shapeOutline(outline.f("width"), outline.f("height"), outline.radius(), outline.getValue("segments").jsonPrimitive.int)
        close(expected.toFloatArray(), actual, "outline")
    }

    @Test
    fun edgeProfileAndRefractionMatch() {
        for (c in list("edgeProfile")) close(c.f("value"), edgeProfile(c.f("distance")), "edgeProfile $c")
        for (c in list("refractionOffset")) {
            val (dx, dy) = refractionOffset(c.f("distance"), c.f("nx"), c.f("ny"), c.f("refraction"))
            close(c.f("dx"), dx, "dx $c")
            close(c.f("dy"), dy, "dy $c")
        }
    }

    @Test
    fun defaultRefractionMatches() {
        val cases = list("defaultRefraction")
        for (c in cases) {
            val width = c.f("width")
            val height = c.f("height")
            close(c.f("value"), defaultRefraction(width, height), "defaultRefraction $c")
            close(c.f("value"), defaultRefraction(height, width), "defaultRefraction transposed $c")
            close(c.f("value"), GlassDefaults.refraction(width.dp, height.dp).value, "GlassDefaults.refraction $c")
        }
        assertEquals(9, cases.size)
    }

    @Test
    fun toneCurvesMatch() {
        for (c in list("tone")) {
            val v = c.f("v")
            close(c.f("clearLight"), clearTone(v, dark = false), "clearLight $v")
            close(c.f("clearDark"), clearTone(v, dark = true), "clearDark $v")
            close(c.f("clearDarkNested"), clearTone(v, dark = true, nested = true), "clearDarkNested $v")
            close(c.f("regularLight"), regularTone(v, dark = false), "regularLight $v")
            close(c.f("regularDark"), regularTone(v, dark = true), "regularDark $v")
        }
    }

    @Test
    fun materialColorMatches() {
        val cases = list("materialColor")
        for (c in cases) {
            val regular = c.getValue("material").jsonPrimitive.content == "regular"
            close(c.floats("value"), materialColor(c.floats("color"), regular, c.b("dark")), "materialColor $c")
        }
        assertEquals(24, cases.size)
    }

    @Test
    fun tintColorMatches() {
        for (c in list("tintColor")) {
            val regular = c.getValue("material").jsonPrimitive.content == "regular"
            close(c.floats("value"), tintColor(c.floats("color"), c.floats("tint"), regular), "tintColor $c")
        }
    }

    @Test
    fun frostColorMatches() {
        for (c in list("frostColor")) {
            close(c.floats("value"), frostColor(c.floats("frost"), c.floats("fill"), c.b("dark")), "frostColor $c")
        }
    }

    @Test
    fun legibilityMatches() {
        val legibility = vectors.getValue("legibility").jsonObject
        assertEquals(legibility.f("menuAmount"), MENU_LEGIBILITY)
        assertEquals(legibility.f("highlight"), MENU_HIGHLIGHT)
        val backing = legibility.getValue("backing").jsonObject
        close(backing.floats("light"), legibilityBacking(dark = false), "backing light")
        close(backing.floats("dark"), legibilityBacking(dark = true), "backing dark")
        val cases = legibility.getValue("cases").jsonArray.map { it.jsonObject }
        for (c in cases) {
            close(c.floats("value"), legibilityColor(c.floats("color"), c.b("dark"), c.f("amount")), "legibilityColor $c")
        }
        assertEquals(6, cases.size)
        val labels = legibility.getValue("menuForeground").jsonArray.map { it.jsonObject }
        for (c in labels) {
            val regular = c.getValue("material").jsonPrimitive.content == "regular"
            close(c.floats("value"), menuForeground(c.floatsOrNull("tint"), regular, c.b("dark")), "menuForeground $c")
        }
        assertEquals(8, labels.size)
        // The worst cases: untinted Regular menus, white labels over white in dark, #111113 over black in light.
        val worst = legibility.getValue("worstCase").jsonObject
        val white = floatArrayOf(1f, 1f, 1f)
        val black = floatArrayOf(0f, 0f, 0f)
        val ink = floatArrayOf(17f / 255f, 17f / 255f, 19f / 255f)
        val darkOverWhite = contrastRatio(legibilityColor(materialColor(white, regular = true, dark = true), true, MENU_LEGIBILITY), white)
        val lightOverBlack = contrastRatio(legibilityColor(materialColor(black, regular = true, dark = false), false, MENU_LEGIBILITY), ink)
        close(worst.f("darkOverWhite"), darkOverWhite, "darkOverWhite", tolerance = 1e-3f)
        close(worst.f("lightOverBlack"), lightOverBlack, "lightOverBlack", tolerance = 1e-3f)
        // Without the backing, the same glass gives 1.8:1 and 2.1:1.
        assertTrue(contrastRatio(materialColor(white, regular = true, dark = true), white) < 2f)
        assertTrue(contrastRatio(materialColor(black, regular = true, dark = false), ink) < 2.2f)
        // Over a 6 x 6 x 6 grid of backdrops, highlighted rows included: untinted menus, Regular or Clear, in both
        // appearances, and menus tinted with the system colors keep 5.2:1.
        val untinted = listOf(true, false).minOf { regular -> listOf(false, true).minOf { dark -> menuFloor(regular, dark, null) } }
        val tinted = SYSTEM_TINTS.minOf { tint -> listOf(true, false).minOf { regular -> listOf(false, true).minOf { dark -> menuFloor(regular, dark, tint) } } }
        close(worst.f("untintedWithHighlight"), untinted, "untintedWithHighlight", tolerance = 1e-3f)
        close(worst.f("systemTintsWithHighlight"), tinted, "systemTintsWithHighlight", tolerance = 1e-3f)
        assertTrue("menu labels keep 5.2:1 (untinted $untinted, system tints $tinted)", untinted >= 5.2f && tinted >= 5.2f)
    }

    /**
     * The lowest contrast of a menu's labels ([menuForeground]) with its glass, plain and with the row highlight,
     * over a 6 x 6 x 6 grid of backdrops, as spec/generate-vectors.mjs measures it.
     */
    private fun menuFloor(regular: Boolean, dark: Boolean, tint: FloatArray?): Float {
        val label = menuForeground(tint, regular, dark)
        val steps = List(6) { it / 5f }
        var lowest = Float.MAX_VALUE
        for (r in steps) for (g in steps) for (b in steps) {
            var rgb = materialColor(floatArrayOf(r, g, b), regular, dark)
            if (tint != null) rgb = tintColor(rgb, tint, regular)
            rgb = legibilityColor(rgb, dark, MENU_LEGIBILITY)
            lowest = minOf(lowest, contrastRatio(rgb, label), contrastRatio(highlighted(rgb, label), label))
        }
        return lowest
    }

    @Test
    fun rimOverlayMatches() {
        val cases = list("rimOverlay")
        for (c in cases) {
            val value = rimOverlay(c.f("distance"), c.f("nx"), c.f("ny"), c.b("dark"), c.f("pixel"), c.floatsOrNull("tint"))
            close(c.floats("value"), value, "rimOverlay $c")
        }
        assertEquals(328, cases.size)
    }

    @Test
    fun glassPixelMatches() {
        val cases = vectors.getValue("glassPixel").jsonObject.getValue("cases").jsonArray.map { it.jsonObject }
        for (c in cases) {
            val x = c.f("x")
            val y = c.f("y")
            val value = glassPixel(
                x = x,
                y = y,
                width = c.f("width"),
                height = c.f("height"),
                radius = c.radius(),
                regular = c.getValue("material").jsonPrimitive.content == "regular",
                dark = c.b("dark"),
                refraction = c.f("refraction"),
                pixel = c.f("pixel"),
                tint = c.floatsOrNull("tint"),
                legibility = c["legibility"]?.jsonPrimitive?.float ?: 0f,
                base = linearBackdrop(x, y),
                sample = { dx, dy -> linearBackdrop(x + dx, y + dy) },
            )
            close(c.floats("value"), value, "glassPixel $c")
        }
        assertEquals(63, cases.size)
        // Six cases are open menu panels, with the menus' full backing.
        assertEquals(6, cases.count { it.containsKey("legibility") })
        assertTrue(cases.filter { it.containsKey("legibility") }.all { it.f("legibility") == MENU_LEGIBILITY })
    }

    /** The backdrop spec/vectors.json uses: rgb = (0.5 + px/400, 0.5 + py/400, 0.6), alpha 1. */
    private fun linearBackdrop(px: Float, py: Float): FloatArray = floatArrayOf(sat(0.5f + px / 400f), sat(0.5f + py / 400f), 0.6f, 1f)

    @Test
    fun springsMatch() {
        val springs = vectors.getValue("springs").jsonObject
        val named = mapOf(
            "press" to SpringSpec.Press,
            "release" to SpringSpec.Release,
            "glow" to SpringSpec.Glow,
            "morph" to SpringSpec.Morph,
            // The layout spring (a surface flowing to a new size) has no component using it yet.
            "layout" to SpringSpec(0.4f, 0.16f),
            "track" to SpringSpec.Track,
        )
        assertEquals(named.keys, springs.keys)
        for ((name, spec) in named) {
            val c = springs.getValue(name).jsonObject
            assertEquals(c.f("duration"), spec.duration)
            assertEquals(c.f("bounce"), spec.bounce)
            val every = c.getValue("sampleEvery").jsonPrimitive.int
            val dt = c.f("dt")
            val spring = SpringValue(c.f("from"))
            spring.target = c.f("to")
            val samples = mutableListOf<Float>()
            for (frame in 1..90) {
                spring.step(spec, dt)
                if (frame % every == 0) samples += spring.value
            }
            close(c.floats("values"), samples.toFloatArray(), "spring $name")
        }
    }

    @Test
    fun switchSpringIsTheMeasuredResponse() {
        // Response 0.47 s with damping ratio 0.7, as in the web library's switch.
        val (stiffness, damping) = springCoefficients(SpringSpec.Switch)
        val omega = 2 * Math.PI / 0.47
        close((omega * omega).toFloat(), stiffness.toFloat(), "stiffness", tolerance = 1e-2f)
        close((2 * 0.7 * omega).toFloat(), damping.toFloat(), "damping")
    }

    @Test
    fun interactionMatches() {
        for (c in list("rubberBand")) close(c.f("value"), rubberBand(c.f("offset"), c.f("limit")), "rubberBand $c")
        for (c in list("stretchLimit")) close(c.f("value"), stretchLimit(c.f("width"), c.f("height")), "stretchLimit $c")
        for (c in list("pressScale")) {
            val s = pressScale(c.f("width"), c.f("height"), c.f("press"), c.f("tx"), c.f("ty"), c.f("vx"), c.f("vy"), c.b("reduced"))
            close(c.f("sx"), s.sx, "sx $c")
            close(c.f("sy"), s.sy, "sy $c")
        }
        for (c in list("glow")) {
            val g = glow(c.f("width"), c.f("height"), c.b("dark"))
            close(c.f("radius"), g.radius, "glow radius $c")
            close(c.f("alpha"), g.alpha, "glow alpha $c")
            close(c.f("hover"), g.hover, "glow hover $c")
        }
    }

    @Test
    fun progressiveBlurMatches() {
        val progressive = vectors.getValue("progressive").jsonObject
        assertEquals(PROGRESSIVE_LEVELS, progressive.getValue("levels").jsonPrimitive.int)
        val blur = progressive.floats("blurForLevelsAt20")
        close(blur, FloatArray(PROGRESSIVE_LEVELS + 1) { progressiveLevelBlur(it, 20f) }, "level blur")
        for (c in progressive.getValue("weights").jsonArray.map { it.jsonObject }) {
            val u = c.f("u")
            val weights = FloatArray(PROGRESSIVE_LEVELS + 1) { progressiveWeight(it, u) }
            close(c.floats("weights"), weights, "weights at $u")
            close(1f, weights.sum(), "weights sum at $u")
        }
    }

    @Test
    fun progressiveOverPaintingReproducesTheWeights() {
        // Painting level i with progressiveOverAlpha over levels below it must
        // give every level exactly its progressiveWeight.
        for (step in 0..200) {
            val u = step / 200f
            var remaining = 1f
            val effective = FloatArray(PROGRESSIVE_LEVELS + 1)
            for (level in PROGRESSIVE_LEVELS downTo 0) {
                val alpha = progressiveOverAlpha(level, u)
                effective[level] = alpha * remaining
                remaining *= 1f - alpha
            }
            for (level in 0..PROGRESSIVE_LEVELS) close(progressiveWeight(level, u), effective[level], "level $level at $u")
        }
    }

    private companion object {
        const val TOLERANCE = 1e-4f
    }
}
