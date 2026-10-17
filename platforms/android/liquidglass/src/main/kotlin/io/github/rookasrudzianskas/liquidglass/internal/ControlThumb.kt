package io.github.rookasrudzianskas.liquidglass.internal

import android.os.Build
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.absoluteOffset
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassMotion
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.optics.SpringSpec
import io.github.rookasrudzianskas.liquidglass.optics.SpringValue
import kotlin.math.abs
import kotlin.math.min
import kotlin.math.roundToInt
import kotlin.math.sqrt

/**
 * Motion of a slider or switch thumb (spec/OPTICS.md §6): its position (the
 * switch moves on the measured switch spring, a slider follows its value), the
 * lift into glass while pressed, and the stretch along its path with speed.
 * Positions are in dp.
 */
internal class ThumbState(private val springPosition: Boolean) {
    var motion = GlassMotion.Full
    var animator: FrameAnimator? = null

    private val position = SpringValue()
    private val liftSpring = SpringValue()
    private var pressed = false
    private var placed = false
    private var lastX = Float.NaN
    private var speed = 0f

    private val xState = mutableFloatStateOf(0f)
    private val liftState = mutableFloatStateOf(0f)
    private val stretchState = mutableFloatStateOf(0f)

    /** Thumb position along the track, dp. */
    val x: Float get() = xState.floatValue

    /** 0 at rest (opaque face), 1 lifted into glass; may overshoot slightly. */
    val lift: Float get() = liftState.floatValue

    /** Horizontal stretch from speed, 0..0.28. */
    val stretch: Float get() = stretchState.floatValue

    fun moveTo(target: Float) {
        if (!placed || motion == GlassMotion.None) {
            placed = true
            position.snap(target)
            xState.floatValue = target
            lastX = target
        } else {
            position.target = target
        }
        animator?.wake()
    }

    fun press(down: Boolean) {
        if (pressed == down) return
        pressed = down
        animator?.wake()
    }

    fun step(dt: Float): Boolean {
        liftSpring.target = if (pressed) 1f else 0f
        if (motion == GlassMotion.None) {
            position.snap(position.target)
            liftSpring.snap(liftSpring.target)
        } else {
            val reduced = motion == GlassMotion.Reduced
            if (springPosition) position.step(if (reduced) SpringSpec.Switch.critical() else SpringSpec.Switch, dt) else position.snap(position.target)
            val spec = if (liftSpring.target > liftSpring.value) SpringSpec.Press else SpringSpec.Release
            liftSpring.step(if (reduced) spec.critical() else spec, dt)
        }
        val x = position.value
        if (!lastX.isNaN() && dt > 0f) speed += ((x - lastX) / dt - speed) * min(1f, dt * 18f)
        lastX = x
        if (motion != GlassMotion.Full) speed = 0f
        if (abs(speed) < 1f) speed = 0f
        xState.floatValue = x
        liftState.floatValue = liftSpring.value
        stretchState.floatValue = min(0.28f, abs(speed) * 0.0007f) * liftSpring.value.coerceIn(0f, 1f)
        // A finger resting on the thumb needs no frames once the springs settle; press() and moveTo() wake the loop.
        return position.isSettled && liftSpring.isSettled && speed == 0f
    }
}

/**
 * The thumb itself, in the control's local scene: Clear glass grown by
 * [growX] × [growY] per side while lifted, and an opaque white capsule with a
 * soft shadow that fades out as the glass fades in. [left], [top] place the
 * resting thumb (dp from the scene's absolute top-left; the control mirrors it
 * in RTL). Under reduced [motion] the thumb crossfades without growing.
 */
@Composable
internal fun ControlThumb(
    state: ThumbState,
    motion: GlassMotion,
    width: Dp,
    height: Dp,
    growX: Dp,
    growY: Dp,
    refraction: Dp,
    appearance: GlassAppearance?,
    left: () -> Float,
    top: Float,
) {
    val params = remember(refraction, appearance) {
        GlassSurfaceParams(
            shape = GlassShape.Capsule,
            material = GlassMaterial.Clear,
            appearance = appearance,
            tint = Color.Unspecified,
            refraction = refraction,
            interactive = false,
            motion = null,
            opacity = 1f,
            interactionSource = null,
        )
    }
    val grows = motion == GlassMotion.Full
    val density = LocalDensity.current.density
    val dynamics = remember(state, grows, growX, growY, density) {
        GlassDynamics(
            opacity = { state.lift.coerceIn(0f, 1f) },
            bounds = { size ->
                // Reduced motion crossfades at the resting size.
                val lift = if (grows) state.lift else 0f
                val stretch = state.stretch
                val w = (size.width + 2f * growX.value * density * lift) * (1f + stretch)
                val h = (size.height + 2f * growY.value * density * lift) / sqrt(1f + stretch)
                val cx = size.width / 2f
                val cy = size.height / 2f
                Rect(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f)
            },
        )
    }
    val place = Modifier.absoluteOffset { IntOffset((left() * density).roundToInt(), (top * density).roundToInt()) }.size(width, height)
    Box(place.glassSurface(params, dynamics))
    Box(
        place
            .graphicsLayer {
                val lift = if (grows) state.lift else 0f
                val stretch = state.stretch
                scaleX = (width.toPx() + 2f * growX.toPx() * lift) / width.toPx() * (1f + stretch)
                scaleY = (height.toPx() + 2f * growY.toPx() * lift) / height.toPx() / sqrt(1f + stretch)
                alpha = 1f - state.lift.coerceIn(0f, 1f)
                shadowElevation = 3.dp.toPx()
                shape = GlassShape.Capsule
                if (Build.VERSION.SDK_INT >= 28) {
                    ambientShadowColor = ThumbShadow
                    spotShadowColor = ThumbShadow
                }
            }
            .background(Color.White, GlassShape.Capsule),
    )
}

private val ThumbShadow = Color(0x66000000)

/** The platform accent: Material You's accent on Android 12+, system blue before. */
@Composable
internal fun platformAccent(dark: Boolean): Color {
    if (Build.VERSION.SDK_INT >= 31) {
        val context = LocalContext.current
        return Color(context.getColor(if (dark) android.R.color.system_accent1_200 else android.R.color.system_accent1_600))
    }
    return if (dark) Color(0xFF0A84FF) else Color(0xFF007AFF)
}
