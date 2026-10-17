package io.github.rookasrudzianskas.liquidglass.internal

import android.graphics.BlendMode
import android.graphics.Bitmap
import android.graphics.BitmapShader
import android.graphics.HardwareRenderer
import android.graphics.Paint
import android.graphics.PixelFormat
import android.graphics.RenderNode
import android.graphics.RuntimeShader
import android.graphics.Shader
import android.hardware.HardwareBuffer
import android.media.ImageReader
import android.os.Build
import android.os.SystemClock
import android.util.Log
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import io.github.rookasrudzianskas.liquidglass.optics.shapeParams
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import kotlin.math.abs

/**
 * The AGSL glass program (GLASS_SHADER, the functions the glass effect runs)
 * against spec/vectors.json, evaluated on the device GPU at the spec's GPU
 * tolerance of 2e-3, like the Metal and HLSL ports.
 *
 * Each case is drawn as a one-pixel column of a small test program that calls
 * the shader's own functions; every output is written as a 24-bit fixed-point
 * number across the red, green and blue channels of an 8-bit sRGB target (no
 * color conversion applies there), read back and decoded.
 */
@RunWith(AndroidJUnit4::class)
class ShaderVectorTest {

    private lateinit var vectors: JSONObject

    @Before
    fun setUp() {
        assumeTrue("AGSL needs Android 13", Build.VERSION.SDK_INT >= 33)
        val assets = InstrumentationRegistry.getInstrumentation().context.assets
        vectors = JSONObject(assets.open("vectors.json").bufferedReader().use { it.readText() })
    }

    private fun cases(name: String): List<JSONObject> = vectors.getJSONArray(name).objects()

    private fun JSONArray.objects(): List<JSONObject> = List(length()) { getJSONObject(it) }

    private fun JSONObject.f(name: String): Float = getDouble(name).toFloat()

    private fun JSONObject.floats(name: String): FloatArray = getJSONArray(name).let { a -> FloatArray(a.length()) { a.getDouble(it).toFloat() } }

    /** "capsule" and "circle" are fully round (null). */
    private fun JSONObject.radius(): Float? = if (get("radius") is Number) f("radius") else null

    private fun JSONObject.regular(): Float = if (getString("material") == "regular") 1f else 0f

    private fun JSONObject.flag(name: String): Float = if (getBoolean(name)) 1f else 0f

    /** The vectors write one third of a point as 0.333333; the shader gets the exact value, as on the other ports. */
    private fun JSONObject.pixel(): Float = f("pixel").let { if (abs(it - 1f / 3f) < 1e-5f) 1f / 3f else it }

    /** Tint rgb and whether it is on. */
    private fun JSONObject.tint(): FloatArray = optJSONArray("tint")?.let { floatArrayOf(it.f(0), it.f(1), it.f(2), 1f) } ?: floatArrayOf(0f, 0f, 0f, 0f)

    private fun JSONArray.f(i: Int): Float = getDouble(i).toFloat()

    /** One case of the test program: up to four float4 inputs. */
    private class Inputs(val a: FloatArray, val b: FloatArray = ZERO, val c: FloatArray = ZERO, val d: FloatArray = ZERO)

    /** Evaluates [mode] of the test program for every input; each result is a float4. */
    private fun evaluate(mode: Int, inputs: List<Inputs>): List<FloatArray> {
        val width = inputs.size
        val shader = RuntimeShader(PROGRAM)
        // The glass program declares the content child; this program never samples it.
        shader.setInputShader("content", BitmapShader(Bitmap.createBitmap(1, 1, Bitmap.Config.ARGB_8888), Shader.TileMode.CLAMP, Shader.TileMode.CLAMP))
        val paint = Paint().apply {
            isAntiAlias = false
            isDither = false
            blendMode = BlendMode.SRC
        }
        val node = RenderNode("ShaderVectorTest").apply { setPosition(0, 0, width, ROWS) }
        val canvas = node.beginRecording(width, ROWS)
        inputs.forEachIndexed { i, input ->
            // Setting a uniform makes a new native shader; the recorded draw keeps the one it was drawn with.
            shader.setFloatUniform("uMode", mode.toFloat())
            shader.setFloatUniform("uA", input.a.padded())
            shader.setFloatUniform("uB", input.b.padded())
            shader.setFloatUniform("uC", input.c.padded())
            shader.setFloatUniform("uD", input.d.padded())
            paint.shader = shader
            canvas.drawRect(i.toFloat(), 0f, i + 1f, ROWS.toFloat(), paint)
        }
        node.endRecording()
        val pixels = render(node, width)
        return List(width) { i ->
            FloatArray(4) { component ->
                val o = (component * width + i) * 4
                decode(pixels[o], pixels[o + 1], pixels[o + 2])
            }
        }
    }

    /** Renders [node] on the GPU into an 8-bit RGBA buffer and returns its bytes, row by row (unsigned). */
    private fun render(node: RenderNode, width: Int): IntArray {
        var result: IntArray? = null
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            val usage = HardwareBuffer.USAGE_GPU_SAMPLED_IMAGE or HardwareBuffer.USAGE_GPU_COLOR_OUTPUT or HardwareBuffer.USAGE_CPU_READ_OFTEN
            val reader = ImageReader.newInstance(width, ROWS, PixelFormat.RGBA_8888, 1, usage)
            val renderer = HardwareRenderer()
            try {
                renderer.setSurface(reader.surface)
                renderer.setContentRoot(node)
                renderer.createRenderRequest().setWaitForPresent(true).syncAndDraw()
                val deadline = SystemClock.uptimeMillis() + TIMEOUT_MS
                var image = reader.acquireLatestImage()
                while (image == null && SystemClock.uptimeMillis() < deadline) {
                    SystemClock.sleep(5)
                    image = reader.acquireLatestImage()
                }
                checkNotNull(image) { "the GPU never produced a frame" }
                image.use {
                    val plane = it.planes[0]
                    val buffer = plane.buffer
                    val bytes = IntArray(width * ROWS * 4)
                    for (y in 0 until ROWS) {
                        for (x in 0 until width) {
                            val at = y * plane.rowStride + x * plane.pixelStride
                            for (c in 0..3) bytes[(y * width + x) * 4 + c] = buffer.get(at + c).toInt() and 0xFF
                        }
                    }
                    result = bytes
                }
            } finally {
                renderer.destroy()
                reader.close()
            }
        }
        return checkNotNull(result)
    }

    private var worst = 0f
    private var worstWhere = ""

    private fun close(expected: FloatArray, actual: FloatArray, count: Int, what: String) {
        for (i in 0 until count) {
            val error = abs(expected[i] - actual[i])
            if (error > worst) {
                worst = error
                worstWhere = "$what[$i]"
            }
            assertTrue("$what[$i]: expected ${expected[i]}, got ${actual[i]} (GPU tolerance $TOLERANCE)", error <= TOLERANCE)
        }
    }

    private fun report(name: String, cases: Int) {
        Log.i(TAG, "$name: $cases cases, max error $worst at $worstWhere")
    }

    @Test
    fun shapeFieldMatches() {
        val cases = cases("shapeField")
        val results = evaluate(MODE_SHAPE_FIELD, cases.map { c ->
            val p = shapeParams(c.f("width"), c.f("height"), c.radius())
            Inputs(floatArrayOf(c.f("x"), c.f("y"), c.f("width"), c.f("height")), floatArrayOf(p.extent, p.exponent))
        })
        cases.forEachIndexed { i, c ->
            close(floatArrayOf(c.f("distance"), c.f("nx"), c.f("ny")), results[i], 3, "shapeField[$i] $c")
        }
        assertEquals(640, cases.size)
        report("shapeField", cases.size)
    }

    @Test
    fun edgeProfileAndRefractionOffsetMatch() {
        val edges = cases("edgeProfile")
        val edgeResults = evaluate(MODE_REFRACTION, edges.map { Inputs(floatArrayOf(it.f("distance"), 0f, 0f, 0f)) })
        edges.forEachIndexed { i, c -> close(floatArrayOf(c.f("value")), edgeResults[i], 1, "edgeProfile[$i] $c") }
        val offsets = cases("refractionOffset")
        val offsetResults = evaluate(MODE_REFRACTION, offsets.map { c ->
            Inputs(floatArrayOf(c.f("distance"), c.f("nx"), c.f("ny"), c.f("refraction")))
        })
        offsets.forEachIndexed { i, c ->
            close(floatArrayOf(c.f("dx"), c.f("dy")), floatArrayOf(offsetResults[i][1], offsetResults[i][2]), 2, "refractionOffset[$i] $c")
        }
        report("edgeProfile and refractionOffset", edges.size + offsets.size)
    }

    @Test
    fun toneCurvesAndMaterialColorMatch() {
        val tones = cases("tone")
        // Per level: clear light, clear dark, clear dark nested, regular light, regular dark. Gray stays gray
        // through the Regular saturation, so the Regular material color of a gray is its tone curve.
        val looks = listOf(0f to floatArrayOf(0f, 0f), 0f to floatArrayOf(1f, 0f), 0f to floatArrayOf(1f, 1f), 1f to floatArrayOf(0f, 0f), 1f to floatArrayOf(1f, 0f))
        val toneResults = evaluate(MODE_MATERIAL, tones.flatMap { c ->
            val v = c.f("v")
            looks.map { (material, look) -> Inputs(floatArrayOf(v, v, v, material), look) }
        })
        tones.forEachIndexed { i, c ->
            val expected = floatArrayOf(c.f("clearLight"), c.f("clearDark"), c.f("clearDarkNested"), c.f("regularLight"), c.f("regularDark"))
            for (k in looks.indices) close(floatArrayOf(expected[k]), toneResults[i * looks.size + k], 1, "tone[$i] look $k $c")
        }
        val colors = cases("materialColor")
        val colorResults = evaluate(MODE_MATERIAL, colors.map { c ->
            val rgb = c.floats("color")
            Inputs(floatArrayOf(rgb[0], rgb[1], rgb[2], c.regular()), floatArrayOf(c.flag("dark"), 0f))
        })
        colors.forEachIndexed { i, c -> close(c.floats("value"), colorResults[i], 3, "materialColor[$i] $c") }
        assertEquals(24, colors.size)
        report("tone and materialColor", tones.size * looks.size + colors.size)
    }

    @Test
    fun tintColorMatches() {
        val cases = cases("tintColor")
        val results = evaluate(MODE_TINT, cases.map { c ->
            val rgb = c.floats("color")
            val tint = c.floats("tint")
            Inputs(floatArrayOf(rgb[0], rgb[1], rgb[2], c.regular()), floatArrayOf(tint[0], tint[1], tint[2], 0f))
        })
        cases.forEachIndexed { i, c -> close(c.floats("value"), results[i], 3, "tintColor[$i] $c") }
        report("tintColor", cases.size)
    }

    @Test
    fun rimOverlayMatches() {
        val cases = cases("rimOverlay")
        val results = evaluate(MODE_RIM, cases.map { c ->
            val tint = c.tint()
            Inputs(
                floatArrayOf(c.f("distance"), c.f("nx"), c.f("ny"), c.flag("dark")),
                floatArrayOf(c.pixel(), tint[0], tint[1], tint[2]),
                floatArrayOf(tint[3], 0f, 0f, 0f),
            )
        })
        cases.forEachIndexed { i, c -> close(c.floats("value"), results[i], 4, "rimOverlay[$i] $c") }
        assertEquals(328, cases.size)
        report("rimOverlay", cases.size)
    }

    @Test
    fun glassPixelMatches() {
        val cases = vectors.getJSONObject("glassPixel").getJSONArray("cases").objects()
        val results = evaluate(MODE_GLASS_PIXEL, cases.map { c ->
            val p = shapeParams(c.f("width"), c.f("height"), c.radius())
            Inputs(
                floatArrayOf(c.f("x"), c.f("y"), c.f("width"), c.f("height")),
                floatArrayOf(p.extent, p.exponent, c.regular(), c.flag("dark")),
                floatArrayOf(c.f("refraction"), c.pixel(), 0f, 0f),
                c.tint(),
            )
        })
        cases.forEachIndexed { i, c -> close(c.floats("value"), results[i], 4, "glassPixel[$i] $c") }
        assertEquals(57, cases.size)
        report("glassPixel", cases.size)
    }

    private companion object {
        const val TAG = "ShaderVectorTest"
        const val TOLERANCE = 2e-3f
        const val TIMEOUT_MS = 10_000L

        /** One row per output component. */
        const val ROWS = 4

        const val MODE_SHAPE_FIELD = 0
        const val MODE_REFRACTION = 1
        const val MODE_MATERIAL = 2
        const val MODE_TINT = 3
        const val MODE_RIM = 4
        const val MODE_GLASS_PIXEL = 5

        val ZERO = FloatArray(4)

        /** Outputs are encoded over [-RANGE, RANGE]. */
        const val RANGE = 512f

        fun FloatArray.padded(): FloatArray = if (size == 4) this else FloatArray(4) { getOrElse(it) { 0f } }

        /** Inverse of lgTestEncode: three 8-bit channels back to the value. */
        fun decode(r: Int, g: Int, b: Int): Float {
            val u = (r + (g + b / 255.0) / 255.0) / 255.0
            return (u * 2.0 * RANGE - RANGE).toFloat()
        }

        /**
         * The glass program's functions (everything before its main) and a main
         * that runs one of them on the uniforms and writes component `y` of the
         * result to row `y`.
         */
        val PROGRAM: String = GLASS_SHADER.substringBefore("half4 main(").also {
            check(it.length < GLASS_SHADER.length) { "GLASS_SHADER has no main" }
        } + """
uniform float uMode;
uniform float4 uA;
uniform float4 uB;
uniform float4 uC;
uniform float4 uD;

// The linear backdrop of the glassPixel vectors, relative to the surface center.
float4 lgTestBackdrop(float2 p) {
  return float4(lgSat(0.5 + p.x / 400.0), lgSat(0.5 + p.y / 400.0), 0.6, 1.0);
}

float4 lgTestEvaluate() {
  if (uMode < 0.5) {
    // Shape field: uA = (x, y, width, height), uB = (extent, exponent).
    return float4(lgShapeField(uA.xy, uA.zw, uB.xy), 0.0);
  }
  if (uMode < 1.5) {
    // Edge profile and refraction offset: uA = (distance, nx, ny, refraction).
    return float4(lgEdgeProfile(uA.x), lgRefractionOffset(uA.xyz, uA.w), 0.0);
  }
  if (uMode < 2.5) {
    // Material color: uA = (rgb, material), uB = (dark, nested).
    return float4(lgMaterialColor(uA.rgb, uA.a, uB.x, uB.y), 0.0);
  }
  if (uMode < 3.5) {
    // Tint: uA = (rgb, material), uB = (tint rgb).
    return float4(lgTintColor(uA.rgb, uB.rgb, uA.a), 0.0);
  }
  if (uMode < 4.5) {
    // Rim light: uA = (distance, nx, ny, dark), uB = (pixel, tint rgb), uC.x = tint on.
    return lgRimOverlay(uA.xyz, uA.w, uB.x, uB.yzw, uC.x);
  }
  // Glass pixel over the linear backdrop: uA = (x, y, width, height), uB = (extent, exponent, material, dark),
  // uC = (refraction, pixel), uD = (tint rgb, tint on). Not nested, fully opaque, no contrast border.
  float2 p = uA.xy;
  float3 field = lgShapeField(p, uA.zw, uB.xy);
  float4 src = lgTestBackdrop(p + lgRefractionOffset(field, uC.x));
  return lgCompose(src, lgTestBackdrop(p), field, uB.z, float4(uB.w, 0.0, 1.0, 0.0), uD, uC.y);
}

// A value in [-512, 512] as 24-bit fixed point: whole 1/255 steps in red and green, the rest in blue.
float3 lgTestEncode(float v) {
  float u = clamp((v + $RANGE) / (2.0 * $RANGE), 0.0, 0.99999994);
  float x = u * 255.0;
  float hi = floor(x);
  x = (x - hi) * 255.0;
  float mid = floor(x);
  return float3(hi / 255.0, mid / 255.0, x - mid);
}

half4 main(float2 fragCoord) {
  float4 v = lgTestEvaluate();
  float component = fragCoord.y < 1.0 ? v.x : (fragCoord.y < 2.0 ? v.y : (fragCoord.y < 3.0 ? v.z : v.w));
  return half4(half3(lgTestEncode(component)), 1.0);
}
"""
    }
}
