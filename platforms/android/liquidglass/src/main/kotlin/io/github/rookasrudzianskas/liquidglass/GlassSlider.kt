package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.border
import androidx.compose.foundation.focusable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.awaitHorizontalTouchSlopOrCancellation
import androidx.compose.foundation.gestures.horizontalDrag
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.progressSemantics
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.isSpecified
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.pointer.changedToUpIgnoreConsumed
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.layout
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.disabled
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.setProgress
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.internal.ControlThumb
import io.github.rookasrudzianskas.liquidglass.internal.FrameAnimator
import io.github.rookasrudzianskas.liquidglass.internal.GlassHaptics
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.ThumbState
import io.github.rookasrudzianskas.liquidglass.internal.platformAccent
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * A Liquid Glass slider. At rest the thumb is an opaque white capsule; while
 * pressed it lifts into Clear glass (growing 2.5 × 2 dp per side, refraction
 * 5 dp) that magnifies the track beneath it, and stretches with its speed.
 * Drag the thumb, or touch the track to jump there. Keyboard arrows step it,
 * and TalkBack can set it directly.
 *
 * Give every slider a [label]. TalkBack reads the value as a percentage of the
 * range; when the value means something else, describe it with
 * `Modifier.semantics { stateDescription = "60 dp" }`.
 *
 * ```
 * GlassSlider(volume, { volume = it }, label = "Volume")
 * GlassSlider(radius, { radius = it }, Modifier.semantics { stateDescription = "${radius.roundToInt()} dp" },
 *     valueRange = 0f..80f, label = "Corner radius")
 * ```
 *
 * @param value current value, within [valueRange].
 * @param onValueChange called with each new value while dragging.
 * @param modifier modifier for the slider; it fills the width it is given, 44 dp tall.
 * @param enabled whether the slider responds.
 * @param valueRange the range of values.
 * @param steps number of discrete values between the ends (0 for continuous); each is a haptic detent.
 * @param onValueChangeFinished called when a drag or key press ends.
 * @param tint color of the filled track; unspecified uses the platform accent.
 * @param appearance light or dark glass for the thumb; null inherits the scene's.
 * @param motion overrides the scene's motion.
 * @param label what the slider sets, read by accessibility services.
 */
@Composable
public fun GlassSlider(
    value: Float,
    onValueChange: (Float) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    valueRange: ClosedFloatingPointRange<Float> = 0f..1f,
    steps: Int = 0,
    onValueChangeFinished: (() -> Unit)? = null,
    tint: Color = Color.Unspecified,
    appearance: GlassAppearance? = null,
    motion: GlassMotion? = null,
    label: String? = null,
) {
    require(steps >= 0) { "steps must be nonnegative" }
    require(valueRange.endInclusive > valueRange.start) { "valueRange must not be empty" }
    val style = LocalGlassStyle.current
    val dark = style.isDark(appearance)
    val accent = if (tint.isSpecified) tint else platformAccent(dark)
    val density = LocalDensity.current.density
    val rtl = LocalLayoutDirection.current == LayoutDirection.Rtl
    val view = LocalView.current
    val scope = rememberCoroutineScope()
    val thumb = remember { ThumbState(springPosition = false) }
    val animator = remember(scope) { FrameAnimator(scope, thumb::step) }
    val geometry = remember { SliderGeometry() }
    val currentValue by rememberUpdatedState(value)
    val onChange by rememberUpdatedState(onValueChange)
    val onFinished by rememberUpdatedState(onValueChangeFinished)
    val interactionSource = remember { MutableInteractionSource() }
    val focused by interactionSource.collectIsFocusedAsState()

    val span = valueRange.endInclusive - valueRange.start
    val fraction = ((value - valueRange.start) / span).coerceIn(0f, 1f)
    val currentFraction by rememberUpdatedState(fraction)
    val resolvedMotion = style.motion(motion)
    SideEffect {
        thumb.motion = resolvedMotion
        thumb.animator = animator
        geometry.rtl = rtl
        thumb.moveTo(geometry.thumbLeft(fraction))
    }

    fun snapped(raw: Float): Float {
        val clamped = raw.coerceIn(valueRange.start, valueRange.endInclusive)
        if (steps == 0) return clamped
        val interval = span / (steps + 1)
        return valueRange.start + ((clamped - valueRange.start) / interval).roundToInt() * interval
    }

    fun update(raw: Float) {
        val next = snapped(raw)
        if (next == currentValue) return
        val atEnd = next == valueRange.start || next == valueRange.endInclusive
        if (steps > 0 || atEnd) GlassHaptics.tick(view)
        onChange(next)
    }

    val keyStep = if (steps > 0) span / (steps + 1) else span / 100f
    Box(
        modifier
            .fillMaxWidth()
            .height(SliderHeight)
            .onSizeChanged {
                geometry.width = it.width / density
                thumb.moveTo(geometry.thumbLeft(currentFraction))
            }
            .progressSemantics(value, valueRange, steps)
            .semantics {
                if (label != null) contentDescription = label
                if (enabled) {
                    setProgress { target ->
                        update(target)
                        onFinished?.invoke()
                        true
                    }
                } else {
                    disabled()
                }
            }
            .focusable(enabled, interactionSource)
            .then(if (focused) Modifier.border(2.dp, FocusRingColor, GlassShape.Capsule) else Modifier)
            .onKeyEvent { event ->
                if (!enabled || event.type != KeyEventType.KeyDown) return@onKeyEvent false
                val direction = when (event.key) {
                    Key.DirectionUp -> 1
                    Key.DirectionDown -> -1
                    Key.DirectionRight -> if (rtl) -1 else 1
                    Key.DirectionLeft -> if (rtl) 1 else -1
                    else -> return@onKeyEvent false
                }
                update(currentValue + direction * keyStep)
                onFinished?.invoke()
                true
            }
            .pointerInput(enabled, valueRange, steps) {
                if (!enabled) return@pointerInput
                awaitEachGesture {
                    val down = awaitFirstDown(requireUnconsumed = false)
                    thumb.press(true)
                    val downX = down.position.x / density
                    val start = ((currentValue - valueRange.start) / span).coerceIn(0f, 1f)
                    // Grabbing the thumb keeps its offset from the finger; touching the track jumps there.
                    val onThumb = abs(downX - geometry.thumbCenter(start)) <= ThumbGrabRadius
                    val origin = if (onThumb) start else geometry.fractionAt(downX)
                    // Only horizontal drags belong to the slider, so vertical scrolling still works over it.
                    val drag = awaitHorizontalTouchSlopOrCancellation(down.id) { change, _ -> change.consume() }
                    var changed = true
                    if (drag == null) {
                        // A tap on the track jumps there; a scroll that took the finger changes nothing.
                        val tapped = currentEvent.changes.any { it.id == down.id && it.changedToUpIgnoreConsumed() && !it.isConsumed }
                        changed = tapped && !onThumb
                        if (changed) update(valueRange.start + origin * span)
                    } else {
                        GlassHaptics.press(view)
                        if (!onThumb) update(valueRange.start + origin * span)
                        horizontalDrag(drag.id) { change ->
                            change.consume()
                            val moved = (change.position.x / density - downX) / geometry.travel * (if (rtl) -1f else 1f)
                            update(valueRange.start + (origin + moved).coerceIn(0f, 1f) * span)
                        }
                    }
                    thumb.press(false)
                    if (changed) onFinished?.invoke()
                }
            },
    ) {
        GlassLocalScene(
            Modifier
                .matchParentSize()
                .layout { measurable, constraints ->
                    // The scene reaches past the ends so the grown thumb stays inside it.
                    val pad = SliderOverhang.roundToPx()
                    val placeable = measurable.measure(Constraints.fixed(constraints.maxWidth + 2 * pad, constraints.maxHeight))
                    layout(constraints.maxWidth, constraints.maxHeight) { placeable.place(-pad, 0) }
                },
        ) {
            GlassContent(Modifier.fillMaxSize()) {
                Canvas(Modifier.fillMaxSize()) {
                    val pad = SliderOverhang.toPx()
                    val track = TrackHeight.toPx()
                    val top = (size.height - track) / 2f
                    val width = size.width - 2f * pad
                    val radius = CornerRadius(track / 2f)
                    drawRoundRect(TrackColor, Offset(pad, top), Size(width, track), radius)
                    val thumbCenter = (thumb.x + ThumbWidth.value / 2f) * density
                    val fillStart = if (rtl) thumbCenter else 0f
                    val fillEnd = if (rtl) width else thumbCenter
                    if (fillEnd > fillStart) drawRoundRect(accent, Offset(pad + fillStart, top), Size(fillEnd - fillStart, track), radius)
                }
            }
            ControlThumb(
                state = thumb,
                motion = resolvedMotion,
                width = ThumbWidth,
                height = ThumbHeight,
                growX = 2.5.dp,
                growY = 2.dp,
                refraction = 5.dp,
                appearance = appearance,
                left = { SliderOverhang.value + thumb.x },
                top = (SliderHeight - ThumbHeight).value / 2f,
            )
        }
    }
}

/** Where the thumb sits along a slider of the last measured width (dp). */
private class SliderGeometry {
    var width = 0f
    var rtl = false

    /** Distance the thumb's left edge travels end to end. */
    val travel: Float get() = (width - ThumbWidth.value).coerceAtLeast(1f)

    fun thumbLeft(fraction: Float): Float = (if (rtl) 1f - fraction else fraction) * travel

    fun thumbCenter(fraction: Float): Float = thumbLeft(fraction) + ThumbWidth.value / 2f

    /** The value fraction under a finger at [x] dp. */
    fun fractionAt(x: Float): Float {
        val f = ((x - ThumbWidth.value / 2f) / travel).coerceIn(0f, 1f)
        return if (rtl) 1f - f else f
    }
}

private val SliderHeight = 44.dp
private val TrackHeight = 6.dp
private val ThumbWidth = 20.dp
private val ThumbHeight = 16.dp
private val SliderOverhang = 10.dp

/** How close to the thumb (dp) a touch grabs it instead of jumping. */
private const val ThumbGrabRadius = 22f

private val TrackColor = Color(0x4D808080)
