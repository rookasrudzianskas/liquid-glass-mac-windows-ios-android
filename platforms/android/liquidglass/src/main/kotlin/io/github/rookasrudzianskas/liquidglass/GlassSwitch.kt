package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.border
import androidx.compose.foundation.focusable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.isSpecified
import androidx.compose.ui.graphics.lerp
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.disabled
import androidx.compose.ui.semantics.onClick
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.toggleableState
import androidx.compose.ui.state.ToggleableState
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.internal.ControlThumb
import io.github.rookasrudzianskas.liquidglass.internal.FrameAnimator
import io.github.rookasrudzianskas.liquidglass.internal.GlassHaptics
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.ThumbState
import io.github.rookasrudzianskas.liquidglass.internal.platformAccent
import io.github.rookasrudzianskas.liquidglass.optics.SpringSpec
import io.github.rookasrudzianskas.liquidglass.optics.SpringValue
import kotlin.math.abs

/**
 * A Liquid Glass switch: a 54 × 24 dp track with a 32 × 20 dp thumb that moves
 * on the measured switch spring (response 0.47 s, damping 0.7). Pressed, the
 * thumb lifts into Clear glass (growing 9 × 5.5 dp per side, refraction
 * 7.75 dp) that magnifies the track; drag it across to toggle, or tap.
 * Toggling plays a haptic. It occupies 54 × 44 dp, the extra height being the
 * touch target.
 *
 * @param checked whether the switch is on.
 * @param onCheckedChange called with the new state when the user toggles it.
 * @param modifier modifier for the switch.
 * @param enabled whether the switch responds.
 * @param tint color of the track when on; unspecified uses the platform accent.
 * @param appearance light or dark glass for the thumb; null inherits the scene's.
 * @param motion overrides the scene's motion.
 */
@Composable
public fun GlassSwitch(
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    tint: Color = Color.Unspecified,
    appearance: GlassAppearance? = null,
    motion: GlassMotion? = null,
) {
    val style = LocalGlassStyle.current
    val dark = style.isDark(appearance)
    val onColor = if (tint.isSpecified) tint else platformAccent(dark)
    val density = LocalDensity.current.density
    val rtl = LocalLayoutDirection.current == LayoutDirection.Rtl
    val view = LocalView.current
    val scope = rememberCoroutineScope()
    val thumb = remember { ThumbState(springPosition = true) }
    val track = remember { SwitchTrack() }
    val animator = remember(scope) {
        FrameAnimator(scope) { dt ->
            val thumbDone = thumb.step(dt)
            track.step(dt, thumb.motion) && thumbDone
        }
    }
    val isChecked by rememberUpdatedState(checked)
    val onChange by rememberUpdatedState(onCheckedChange)
    val interactionSource = remember { MutableInteractionSource() }
    val focused by interactionSource.collectIsFocusedAsState()

    fun toggle(to: Boolean) {
        if (to == isChecked) return
        GlassHaptics.toggle(view, to)
        onChange(to)
    }

    SideEffect {
        thumb.motion = style.motion(motion)
        thumb.animator = animator
        if (!track.dragging) {
            track.position = if (checked) 1f else 0f
            thumb.moveTo(track.thumbLeft(rtl))
        }
    }

    Box(
        modifier
            .size(TrackWidth, SwitchHeight)
            .semantics(mergeDescendants = true) {
                role = Role.Switch
                toggleableState = ToggleableState(checked)
                if (enabled) {
                    onClick {
                        toggle(!isChecked)
                        true
                    }
                } else {
                    disabled()
                }
            }
            .focusable(enabled, interactionSource)
            .then(if (focused) Modifier.border(2.dp, FocusRingColor, GlassShape.Capsule) else Modifier)
            .onKeyEvent { event ->
                val activates = event.key == Key.Enter || event.key == Key.Spacebar || event.key == Key.NumPadEnter
                if (!enabled || !activates || event.type != KeyEventType.KeyUp) return@onKeyEvent false
                toggle(!isChecked)
                true
            }
            .pointerInput(enabled, rtl) {
                if (!enabled) return@pointerInput
                awaitEachGesture {
                    val down = awaitFirstDown()
                    thumb.press(true)
                    val start = if (isChecked) 1f else 0f
                    var moved = false
                    var cancelled = false
                    while (true) {
                        val change = awaitPointerEvent().changes.firstOrNull { it.id == down.id } ?: break
                        if (!change.pressed) break
                        // A scroll that claims the finger cancels the press instead of toggling.
                        val dy = abs(change.position.y - down.position.y)
                        if (!moved && (change.isConsumed || dy > viewConfiguration.touchSlop)) {
                            cancelled = true
                            break
                        }
                        val dx = (change.position.x - down.position.x) / density * (if (rtl) -1f else 1f)
                        if (!moved && abs(dx) > DragThreshold) moved = true
                        if (moved) {
                            change.consume()
                            track.dragging = true
                            track.position = (start + dx / Travel.value).coerceIn(0f, 1f)
                            thumb.moveTo(track.thumbLeft(rtl))
                            animator.wake()
                        }
                    }
                    thumb.press(false)
                    val target = when {
                        cancelled -> isChecked
                        moved -> track.position >= 0.5f
                        else -> !isChecked
                    }
                    track.dragging = false
                    track.position = if (target) 1f else 0f
                    thumb.moveTo(track.thumbLeft(rtl))
                    animator.wake()
                    toggle(target)
                }
            },
    ) {
        GlassLocalScene(
            Modifier
                .matchParentSize()
                .layout { measurable, constraints ->
                    // The scene reaches past the track so the grown thumb stays inside it.
                    val pad = SwitchOverhang.roundToPx()
                    val placeable = measurable.measure(Constraints.fixed(constraints.maxWidth + 2 * pad, constraints.maxHeight))
                    layout(constraints.maxWidth, constraints.maxHeight) { placeable.place(-pad, 0) }
                },
        ) {
            GlassContent(Modifier.fillMaxSize()) {
                Canvas(Modifier.fillMaxSize()) {
                    val pad = SwitchOverhang.toPx()
                    val height = TrackHeight.toPx()
                    val top = (size.height - height) / 2f
                    val color = lerp(if (dark) OffTrackDark else OffTrackLight, onColor, track.on)
                    drawRoundRect(color, Offset(pad, top), Size(TrackWidth.toPx(), height), CornerRadius(height / 2f))
                }
            }
            ControlThumb(
                state = thumb,
                width = ThumbWidth,
                height = ThumbHeight,
                growX = 9.dp,
                growY = 5.5.dp,
                refraction = 7.75.dp,
                appearance = appearance,
                left = { SwitchOverhang.value + thumb.x },
                top = (SwitchHeight - ThumbHeight).value / 2f,
            )
        }
    }
}

/** Switch presentation: where the thumb is (0 off, 1 on) and how far the track color has turned on. */
private class SwitchTrack {
    var position = 0f
    var dragging = false
    private val onSpring = SpringValue()
    private val onState = mutableFloatStateOf(0f)
    private var started = false

    val on: Float get() = onState.floatValue

    fun thumbLeft(rtl: Boolean): Float {
        val p = if (rtl) 1f - position else position
        return ThumbInset.value + p * Travel.value
    }

    /** Crossfades the track color toward the side the thumb is on. Returns true at rest. */
    fun step(dt: Float, motion: GlassMotion): Boolean {
        val target = if (position >= 0.5f) 1f else 0f
        if (!started || motion == GlassMotion.None) {
            started = true
            onSpring.snap(target)
        } else {
            onSpring.target = target
            onSpring.step(TrackFade, dt)
        }
        onState.floatValue = onSpring.value
        return onSpring.isSettled
    }
}

private val TrackWidth = 54.dp
private val TrackHeight = 24.dp
private val SwitchHeight = 44.dp
private val ThumbWidth = 32.dp
private val ThumbHeight = 20.dp
private val ThumbInset = 2.dp
private val Travel = TrackWidth - ThumbWidth - ThumbInset * 2
private val SwitchOverhang = 12.dp

/** Finger travel (dp) before a press becomes a drag. */
private const val DragThreshold = 3f

/** Track color crossfade, about 200 ms. */
private val TrackFade = SpringSpec(0.3f, 0f)

private val OffTrackLight = Color(0x52787880)
private val OffTrackDark = Color(0x66787880)
