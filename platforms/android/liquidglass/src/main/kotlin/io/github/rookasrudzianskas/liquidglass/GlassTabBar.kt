package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.isSpecified
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.internal.FrameAnimator
import io.github.rookasrudzianskas.liquidglass.internal.GlassDynamics
import io.github.rookasrudzianskas.liquidglass.internal.GlassHaptics
import io.github.rookasrudzianskas.liquidglass.internal.GlassSurfaceParams
import io.github.rookasrudzianskas.liquidglass.internal.currentGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.glassSurface
import io.github.rookasrudzianskas.liquidglass.optics.SpringSpec
import io.github.rookasrudzianskas.liquidglass.optics.SpringValue
import io.github.rookasrudzianskas.liquidglass.optics.pressScale
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * One item of a [GlassTabBar].
 *
 * @property label the tab's name, shown under the icon and read by accessibility services.
 * @property icon the tab's icon, or null for a label-only tab.
 */
@Immutable
public class GlassTab(public val label: String, public val icon: ImageVector? = null) {
    override fun equals(other: Any?): Boolean = other is GlassTab && other.label == label && other.icon == icon

    override fun hashCode(): Int = 31 * label.hashCode() + (icon?.hashCode() ?: 0)

    override fun toString(): String = "GlassTab($label)"
}

/**
 * A Liquid Glass tab bar: a 62 dp capsule of icon-and-label tabs with a glass
 * lens marking the selection. The lens slides to a newly selected tab on the
 * track spring, lifting into Clear glass and stretching with its speed; drag
 * it across the bar to choose a tab. Arrow keys move the selection, and the
 * focused tab shows a focus ring. Unless the scene sets a refraction, the bar
 * and the lens each follow their resting size ([GlassDefaults.refraction]):
 * 46.5 dp for the 62 dp bar, 40.5 dp for its 54 dp lens.
 *
 * ```
 * var tab by remember { mutableIntStateOf(0) }
 * GlassTabBar(
 *     tabs = listOf(GlassTab("Home", Icons.Default.Home), GlassTab("Search", Icons.Default.Search)),
 *     selectedIndex = tab,
 *     onSelectedIndexChange = { tab = it },
 * )
 * ```
 *
 * @param tabs the tabs, laid out at equal widths.
 * @param selectedIndex index of the selected tab.
 * @param onSelectedIndexChange called when the user picks a tab.
 * @param modifier modifier for the bar; it spans the width it is given.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the bar's glass.
 * @param selectedContentColor icon and label color of the selected tab; unspecified uses the glass foreground.
 * @param unselectedContentColor icon and label color of the other tabs; unspecified uses the glass
 *   foreground at 85%, which keeps the labels legible over mid-tone backdrops.
 * @param interactive whether the bar's glass grows and lights up under the finger.
 * @param motion overrides the scene's motion.
 */
@Composable
public fun GlassTabBar(
    tabs: List<GlassTab>,
    selectedIndex: Int,
    onSelectedIndexChange: (Int) -> Unit,
    modifier: Modifier = Modifier,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    selectedContentColor: Color = Color.Unspecified,
    unselectedContentColor: Color = Color.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
) {
    require(tabs.isNotEmpty()) { "GlassTabBar needs at least one tab" }
    val style = currentGlassStyle()
    val density = LocalDensity.current.density
    val view = LocalView.current
    val rtl = LocalLayoutDirection.current == LayoutDirection.Rtl
    val scope = rememberCoroutineScope()
    val lens = remember { TabLens() }
    val animator = remember(scope) { FrameAnimator(scope, lens::step) }
    val onSelect by rememberUpdatedState(onSelectedIndexChange)
    val selected by rememberUpdatedState(selectedIndex)
    val focus = remember(tabs.size) { List(tabs.size) { FocusRequester() } }
    val sources = remember(tabs.size) { List(tabs.size) { MutableInteractionSource() } }
    SideEffect {
        lens.motion = style.motion(motion)
        lens.count = tabs.size
        lens.animator = animator
        lens.select(selectedIndex.coerceIn(0, tabs.lastIndex))
    }

    val lensParams = GlassSurfaceParams(
        shape = GlassShape.Capsule,
        material = GlassMaterial.Clear,
        appearance = appearance,
        tint = Color.Unspecified,
        refraction = Dp.Unspecified,
        interactive = false,
        motion = motion,
        opacity = 1f,
        interactionSource = null,
    )
    ProvideGlassContentColor(appearance, tint) {
        val contentColor = LocalGlassContentColor.current
        val selectedColor = if (selectedContentColor.isSpecified) selectedContentColor else contentColor
        val unselectedColor = if (unselectedContentColor.isSpecified) unselectedContentColor else contentColor.copy(alpha = UnselectedAlpha)
        val dark = style.isDark(appearance)
        Box(
            modifier
                .heightIn(min = TabBarHeight)
                .liquidGlass(GlassShape.Capsule, material, appearance, tint, interactive = interactive, motion = motion)
                .onSizeChanged { lens.layout(it.width / density, it.height / density, TabBarInset.value, rtl, density) }
                .pointerInput(lens) {
                    awaitEachGesture {
                        val down = awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                        lens.press(true)
                        var dragging = false
                        while (true) {
                            val change = awaitPointerEvent(PointerEventPass.Initial).changes.firstOrNull { it.id == down.id } ?: break
                            if (!change.pressed) break
                            if (!dragging && abs(change.position.x - down.position.x) > viewConfiguration.touchSlop) dragging = true
                            if (dragging && lens.dragTo(change.position.x / density)) GlassHaptics.tick(view)
                        }
                        if (dragging) {
                            val index = lens.endDrag()
                            if (index != selected) onSelect(index)
                        }
                        lens.press(false)
                    }
                },
        ) {
            Box(Modifier.matchParentSize().glassSurface(lensParams, lens.dynamics))
            Row(
                Modifier
                    .drawBehind {
                        // At rest the lens is a soft fill; it gives way to glass as the lens lifts.
                        val alpha = (if (dark) 0.14f else 0.09f) * (1f - lens.lift.coerceIn(0f, 1f))
                        if (alpha > 0f) {
                            val r = lens.rect()
                            drawRoundRect(contentColor.copy(alpha = alpha), r.topLeft, r.size, CornerRadius(r.height / 2f))
                        }
                    }
                    .padding(TabBarInset)
                    .selectableGroup()
                    .onKeyEvent { event ->
                        if (event.type != KeyEventType.KeyDown) return@onKeyEvent false
                        val step = when (event.key) {
                            Key.DirectionLeft -> if (rtl) 1 else -1
                            Key.DirectionRight -> if (rtl) -1 else 1
                            else -> return@onKeyEvent false
                        }
                        val next = (selectedIndex + step).coerceIn(0, tabs.lastIndex)
                        if (next != selectedIndex) {
                            onSelect(next)
                            focus[next].requestFocus()
                        }
                        true
                    },
                verticalAlignment = Alignment.CenterVertically,
            ) {
                val shown = lens.shownIndex
                tabs.forEachIndexed { index, tab ->
                    val color = if (index == shown) selectedColor else unselectedColor
                    val focused by sources[index].collectIsFocusedAsState()
                    Column(
                        Modifier
                            .weight(1f)
                            .heightIn(min = TabBarHeight - TabBarInset * 2)
                            .then(if (focused) Modifier.border(2.dp, FocusRingColor, GlassShape.Capsule) else Modifier)
                            .focusRequester(focus[index])
                            .selectable(
                                selected = index == selectedIndex,
                                interactionSource = sources[index],
                                indication = null,
                                role = Role.Tab,
                                onClick = {
                                    if (index != selectedIndex) {
                                        GlassHaptics.tick(view)
                                        onSelect(index)
                                    }
                                },
                            )
                            // Neighboring labels keep a gap at large font sizes, and ellipsize inside it.
                            .padding(horizontal = TabCellPadding),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(2.dp, Alignment.CenterVertically),
                    ) {
                        if (tab.icon != null) GlassIcon(tab.icon, contentDescription = null, tint = color, size = 24.dp)
                        GlassText(tab.label, style = if (tab.icon != null) TabLabelStyle else TabTextOnlyStyle, color = color, maxLines = 1)
                    }
                }
            }
        }
    }
}

/**
 * The selection lens: its position follows the selected tab (or the finger)
 * on the track spring, and it lifts into glass while pressed or moving.
 * Geometry is in dp, relative to the bar.
 */
private class TabLens {
    var motion = GlassMotion.Full
    var count = 1
    var animator: FrameAnimator? = null

    private var barWidth = 0f
    private var barHeight = 0f
    private var inset = 0f
    private var rtl = false
    private var selected = 0
    private var pressed = false
    private var dragging = false
    private var laidOut = false

    /** Lens slot position: 0 at the first tab, count - 1 at the last. */
    private val slot = SpringValue()
    private val liftSpring = SpringValue()

    private val slotState = mutableFloatStateOf(0f)
    private val slotVelocity = mutableFloatStateOf(0f)
    private val liftState = mutableFloatStateOf(0f)
    private val shownState = mutableIntStateOf(0)

    val lift: Float get() = liftState.floatValue

    /** The tab drawn as selected: the one under the lens while dragging. */
    val shownIndex: Int get() = shownState.intValue

    private val tabWidth: Float get() = if (count > 0) (barWidth - 2 * inset) / count else 0f

    val dynamics = GlassDynamics(
        opacity = { liftState.floatValue.coerceIn(0f, 1f) },
        bounds = { _ -> rect() },
        // An unset refraction follows the lens at rest, one tab wide, not the bar or the lifted lens.
        restingSize = { _ -> Size(tabWidth * density, (barHeight - 2 * inset).coerceAtLeast(0f) * density) },
    )

    /** Pixels per dp of the last layout. */
    private var density = 1f

    fun layout(width: Float, height: Float, inset: Float, rtl: Boolean, density: Float) {
        barWidth = width
        barHeight = height
        this.inset = inset
        this.rtl = rtl
        this.density = density
        laidOut = true
    }

    /** Moves the selection to [index]: animated once laid out, immediately before. */
    fun select(index: Int) {
        if (index == selected && (laidOut || slot.value == index.toFloat())) return
        selected = index
        shownState.intValue = index
        if (dragging) return
        if (!laidOut || motion == GlassMotion.None) slot.snap(index.toFloat()) else slot.target = index.toFloat()
        publish()
        animator?.wake()
    }

    fun press(down: Boolean) {
        pressed = down
        animator?.wake()
    }

    /** Moves the lens under the finger ([x] in dp from the bar's left). Returns true when another tab comes under it. */
    fun dragTo(x: Float): Boolean {
        dragging = true
        val width = tabWidth
        if (width <= 0f) return false
        val fromStart = if (rtl) barWidth - x else x
        val position = ((fromStart - inset - width / 2f) / width).coerceIn(0f, (count - 1).toFloat())
        slot.target = position
        animator?.wake()
        val under = position.roundToInt()
        if (under == shownState.intValue) return false
        shownState.intValue = under
        return true
    }

    /**
     * Ends a drag and returns the tab under the lens. The lens heads back to the
     * current selection; it moves on once the new index arrives through [select],
     * so a rejected change snaps back.
     */
    fun endDrag(): Int {
        dragging = false
        val index = slot.target.roundToInt().coerceIn(0, count - 1)
        slot.target = selected.toFloat()
        shownState.intValue = selected
        animator?.wake()
        return index
    }

    /** The lens rectangle in pixels of the bar, scaled by its press and speed. */
    fun rect(): Rect {
        val width = tabWidth
        val height = barHeight - 2 * inset
        if (width <= 0f || height <= 0f) return Rect.Zero
        val reduced = motion != GlassMotion.Full
        val s = pressScale(width, height, liftState.floatValue, vx = slotVelocity.floatValue * width, reduced = reduced)
        val left = inset + slotState.floatValue * width
        val centerFromStart = left + width / 2f
        val centerX = if (rtl) barWidth - centerFromStart else centerFromStart
        val centerY = barHeight / 2f
        val halfW = width * s.sx / 2f
        val halfH = height * s.sy / 2f
        return Rect((centerX - halfW) * density, (centerY - halfH) * density, (centerX + halfW) * density, (centerY + halfH) * density)
    }

    fun step(dt: Float): Boolean {
        val moving = abs(slot.velocity) * tabWidth > LiftSpeed
        liftSpring.target = if (pressed || dragging || moving) 1f else 0f
        if (motion == GlassMotion.None) {
            slot.snap(slot.target)
            liftSpring.snap(liftSpring.target)
        } else {
            val reduced = motion == GlassMotion.Reduced
            val track = if (dragging) SpringSpec.trackWhileDragging(tabWidth, barHeight) else SpringSpec.Track
            slot.step(track, dt)
            val lift = if (liftSpring.target > liftSpring.value) SpringSpec.Press else SpringSpec.Release
            liftSpring.step(if (reduced) lift.critical() else lift, dt)
        }
        publish()
        // A finger resting on the bar needs no frames once the springs settle; press() and dragTo() wake the loop.
        return slot.isSettled && liftSpring.isSettled
    }

    private fun publish() {
        slotState.floatValue = slot.value
        slotVelocity.floatValue = slot.velocity
        liftState.floatValue = liftSpring.value
    }
}

private val TabBarHeight = 62.dp
private val TabBarInset = 4.dp
private val TabCellPadding = 4.dp

/** Unselected tabs: the spec's 0.85, legible (4.5:1) over mid-tone backdrops where a 62% secondary label is not. */
private const val UnselectedAlpha = 0.85f

/** Lens speed (dp/s) above which it lifts into glass on its own. */
private const val LiftSpeed = 60f

private val TabLabelStyle = TextStyle(fontSize = 10.sp, fontWeight = FontWeight.SemiBold)
private val TabTextOnlyStyle = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.SemiBold)
