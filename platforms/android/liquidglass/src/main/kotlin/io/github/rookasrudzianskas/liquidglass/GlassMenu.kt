package io.github.rookasrudzianskas.liquidglass

import androidx.activity.compose.BackHandler
import androidx.activity.compose.LocalOnBackPressedDispatcherOwner
import androidx.compose.foundation.background
import androidx.compose.ui.layout.layout
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.focusable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.CompositionLocalContext
import androidx.compose.runtime.currentCompositionLocalContext
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.InputMode
import androidx.compose.ui.platform.LocalInputModeManager
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Outline
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.layout.LayoutCoordinates
import androidx.compose.ui.layout.findRootCoordinates
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.paneTitle
import androidx.compose.ui.semantics.collapse
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.onClick
import androidx.compose.ui.semantics.toggleableState
import androidx.compose.ui.state.ToggleableState
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntRect
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.unit.toSize
import androidx.compose.ui.window.Popup
import androidx.compose.ui.window.PopupPositionProvider
import androidx.compose.ui.window.PopupProperties
import io.github.rookasrudzianskas.liquidglass.internal.FrameAnimator
import io.github.rookasrudzianskas.liquidglass.internal.GlassDynamics
import io.github.rookasrudzianskas.liquidglass.internal.GlassOverlay
import io.github.rookasrudzianskas.liquidglass.internal.GlassSurfaceParams
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassFade
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassOverlayLevel
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassScene
import io.github.rookasrudzianskas.liquidglass.internal.currentGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.OVERLAY_LEVEL
import io.github.rookasrudzianskas.liquidglass.internal.glassSurface
import io.github.rookasrudzianskas.liquidglass.optics.SpringSpec
import io.github.rookasrudzianskas.liquidglass.optics.SpringValue
import io.github.rookasrudzianskas.liquidglass.optics.mix
import kotlin.math.roundToInt

/**
 * A menu that grows out of its trigger: a Regular glass panel morphing from the
 * trigger's outline on the morph spring, covering it, and collapsing back
 * into it when dismissed. Items highlight with the foreground color at 8%.
 * Tapping outside, Back and Escape dismiss it; choosing an item runs its
 * action and dismisses it.
 *
 * Inside a [GlassScene] the panel is drawn above the whole scene and
 * refracts it. Outside a scene it opens in a popup with the fallback material.
 *
 * ```
 * var open by remember { mutableStateOf(false) }
 * GlassMenu(
 *     expanded = open,
 *     onDismissRequest = { open = false },
 *     trigger = { GlassButton(Icons.Default.MoreVert, "More", onClick = { open = true }) },
 * ) {
 *     GlassMenuItem("Share", onClick = { share() }, icon = Icons.Default.Share)
 *     GlassMenuDivider()
 *     GlassMenuItem("Delete", onClick = { delete() }, destructive = true)
 * }
 * ```
 *
 * @param expanded whether the menu is open.
 * @param onDismissRequest called when the menu asks to close (outside tap, Back, Escape, an item chosen).
 * @param trigger the control that opens the menu; the panel grows out of its bounds.
 * @param modifier modifier for the panel.
 * @param material the panel's material; Regular by default.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the panel's glass.
 * @param shape the panel's outline; 28 dp continuous corners by default.
 * @param motion overrides the scene's motion.
 * @param content the menu items: [GlassMenuItem], [GlassMenuDivider] or any composable.
 */
@Composable
public fun GlassMenu(
    expanded: Boolean,
    onDismissRequest: () -> Unit,
    trigger: @Composable () -> Unit,
    modifier: Modifier = Modifier,
    material: GlassMaterial = GlassMaterial.Regular,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    shape: GlassShape = GlassShape(GlassDefaults.MenuRadius),
    motion: GlassMotion? = null,
    content: @Composable ColumnScope.() -> Unit,
) {
    val scene = LocalGlassScene.current
    val style = currentGlassStyle()
    val density = LocalDensity.current.density
    val scope = rememberCoroutineScope()
    val menu = remember { MenuState() }
    val animator = remember(scope) { FrameAnimator(scope, menu::step) }
    val dismiss by rememberUpdatedState(onDismissRequest)
    val toolbar = LocalGlassToolbarClusters.current
    val panel: @Composable () -> Unit = {
        MenuPanel(menu, menu.expanded, { dismiss() }, modifier, material, appearance, tint, shape, content)
    }
    val locals = currentCompositionLocalContext
    SideEffect {
        menu.motion = style.motion(motion)
        menu.animator = animator
        menu.density = density
        menu.panel = panel
        menu.locals = locals
        menu.toolbar = toolbar
        menu.open(expanded)
    }
    val visible by remember(menu) { derivedStateOf { menu.visible } }
    val present = expanded || visible
    // Keyboard users get focus back on the trigger when the menu closes.
    val inputMode = LocalInputModeManager.current
    val triggerFocus = remember { FocusRequester() }
    var wasExpanded by remember { mutableStateOf(expanded) }
    LaunchedEffect(expanded) {
        if (wasExpanded && !expanded && inputMode.inputMode == InputMode.Keyboard) triggerFocus.requestFocus()
        wasExpanded = expanded
    }

    Box(
        Modifier
            .onGloballyPositioned { menu.trigger = it }
            .focusRequester(triggerFocus)
            .semantics {
                if (expanded) {
                    collapse {
                        dismiss()
                        true
                    }
                }
            },
    ) {
        CompositionLocalProvider(LocalGlassFade provides menu.triggerFade) {
            Box(Modifier.graphicsLayer { alpha = menu.triggerFade() }) { trigger() }
        }
        if (present && scene == null) {
            Popup(
                popupPositionProvider = remember(menu) { MenuPopupPosition(menu) },
                onDismissRequest = { dismiss() },
                properties = PopupProperties(focusable = true),
            ) {
                // Kept inside the window's margins, like the in-scene panel.
                Box(
                    Modifier.layout { measurable, constraints ->
                        val inset = 2 * PanelMargin.roundToPx()
                        val placeable = measurable.measure(
                            constraints.copy(
                                minWidth = 0,
                                minHeight = 0,
                                maxWidth = maxOf(0, constraints.maxWidth - inset),
                                maxHeight = maxOf(0, constraints.maxHeight - inset),
                            ),
                        )
                        layout(placeable.width, placeable.height) { placeable.place(0, 0) }
                    },
                ) { panel() }
            }
        }
    }
    if (!present || scene == null) return
    // Menus open in the outermost scene so they draw above every nested scene's glass.
    val host = scene.root
    val overlay = remember(menu, host) {
        GlassOverlay(menu) {
            val captured = menu.locals ?: return@GlassOverlay
            CompositionLocalProvider(captured) {
                CompositionLocalProvider(LocalGlassOverlayLevel provides OVERLAY_LEVEL, LocalGlassScene provides host) {
                    MenuOverlay(menu, expanded = menu.expanded, onDismiss = { dismiss() }, panel = menu.panel)
                }
            }
        }
    }
    DisposableEffect(host, overlay) {
        host.overlays += overlay
        onDispose { host.overlays -= overlay }
    }
}

/**
 * A menu row: an optional icon and a label, 44 dp tall (more with large fonts).
 * Choosing it runs [onClick] and closes the menu. With a keyboard, the focused
 * row is highlighted and ringed.
 *
 * @param text the label.
 * @param onClick the action.
 * @param modifier modifier for the row.
 * @param icon optional leading icon.
 * @param enabled whether the item can be chosen.
 * @param destructive draws the item in red, for actions like Delete.
 * @param checked null for an action; true or false for a choice, which shows a check mark when
 *   true and is announced as checked or not checked.
 */
@Composable
public fun GlassMenuItem(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    icon: ImageVector? = null,
    enabled: Boolean = true,
    destructive: Boolean = false,
    checked: Boolean? = null,
) {
    val dismiss = LocalMenuDismiss.current
    val source = remember { MutableInteractionSource() }
    val pressed by source.collectIsPressedAsState()
    val focused by source.collectIsFocusedAsState()
    val foreground = LocalGlassContentColor.current
    val color = if (destructive) DestructiveColor else foreground
    Row(
        modifier
            .fillMaxWidth()
            .heightIn(min = 44.dp)
            .background(if (pressed || focused) foreground.copy(alpha = 0.08f) else Color.Transparent, ItemShape)
            .then(if (focused) Modifier.border(2.dp, FocusRingColor, ItemShape) else Modifier)
            .semantics { if (checked != null) toggleableState = ToggleableState(checked) }
            .clickable(source, indication = null, enabled = enabled, role = if (checked != null) Role.Checkbox else Role.Button) {
                onClick()
                dismiss()
            }
            .padding(horizontal = 14.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        val shown = color.copy(alpha = if (enabled) 1f else 0.4f)
        if (icon != null) GlassIcon(icon, contentDescription = null, tint = shown, size = 20.dp)
        // Long labels and large fonts wrap: the panel grows to fit its items, up to the safe width.
        GlassText(text, Modifier.weight(1f).padding(vertical = 10.dp), style = ItemTextStyle, color = shown)
        if (checked == true) GlassIcon(CheckMark, contentDescription = null, tint = shown, size = 18.dp)
    }
}

/** A thin separator between groups of [GlassMenuItem]s. */
@Composable
public fun GlassMenuDivider() {
    val foreground = LocalGlassContentColor.current
    Box(
        Modifier
            .padding(horizontal = 14.dp, vertical = 4.dp)
            .fillMaxWidth()
            .height(1.dp)
            .background(foreground.copy(alpha = 0.12f)),
    )
}

/** Dismisses the enclosing menu; provided to its items. */
private val LocalMenuDismiss = staticCompositionLocalOf<() -> Unit> { {} }

/** Full-scene overlay: an outside-tap catcher under the panel, positioned against the trigger. */
@Composable
private fun MenuOverlay(menu: MenuState, expanded: Boolean, onDismiss: () -> Unit, panel: @Composable () -> Unit) {
    val insets = WindowInsets.safeDrawing
    val density = LocalDensity.current
    val dismissLabel = stringResource(R.string.liquidglass_dismiss_menu)
    Box(Modifier.fillMaxSize()) {
        if (expanded) {
            Spacer(
                Modifier
                    .fillMaxSize()
                    .clearAndSetSemantics {
                        contentDescription = dismissLabel
                        onClick(dismissLabel) {
                            onDismiss()
                            true
                        }
                    }
                    .pointerInput(Unit) {
                        awaitEachGesture {
                            awaitFirstDown().consume()
                            onDismiss()
                        }
                    },
            )
        }
        Layout(content = panel) { measurables, constraints ->
            val margin = PanelMargin.roundToPx()
            val top = insets.getTop(density) + margin
            val bottom = constraints.maxHeight - insets.getBottom(density) - margin
            val left = insets.getLeft(density, layoutDirection) + margin
            val right = constraints.maxWidth - insets.getRight(density, layoutDirection) - margin
            // The panel fits the safe area; taller content scrolls inside it.
            val placeable = measurables.first().measure(Constraints(maxWidth = maxOf(0, right - left), maxHeight = maxOf(0, bottom - top)))
            layout(constraints.maxWidth, constraints.maxHeight) {
                // The trigger is located against this layout's own coordinates, known while placing.
                val anchor = coordinates?.let(menu::triggerIn) ?: Rect.Zero
                placeable.place(placePanel(anchor, IntSize(placeable.width, placeable.height), left, top, right, bottom, constraints.maxWidth))
            }
        }
    }
}

/**
 * Covers the trigger: aligned to its top (or bottom when there is no room) and
 * to its nearer side, kept inside the safe area widened to include the trigger.
 */
private fun placePanel(anchor: Rect, size: IntSize, safeLeft: Int, safeTop: Int, safeRight: Int, safeBottom: Int, width: Int): IntOffset {
    val left = minOf(safeLeft, anchor.left.roundToInt())
    val top = minOf(safeTop, anchor.top.roundToInt())
    val right = maxOf(safeRight, anchor.right.roundToInt())
    val bottom = maxOf(safeBottom, anchor.bottom.roundToInt())
    val alignStart = anchor.center.x <= width / 2f
    val x = if (alignStart) anchor.left.roundToInt() else anchor.right.roundToInt() - size.width
    val y = if (anchor.top + size.height <= bottom) anchor.top.roundToInt() else anchor.bottom.roundToInt() - size.height
    return IntOffset(x.coerceIn(left, maxOf(left, right - size.width)), y.coerceIn(top, maxOf(top, bottom - size.height)))
}

@Composable
private fun MenuPanel(
    menu: MenuState,
    expanded: Boolean,
    onDismiss: () -> Unit,
    modifier: Modifier,
    material: GlassMaterial,
    appearance: GlassAppearance?,
    tint: Color,
    shape: GlassShape,
    content: @Composable ColumnScope.() -> Unit,
) {
    val params = GlassSurfaceParams(
        shape = shape,
        material = material,
        appearance = appearance,
        tint = tint,
        refraction = Dp.Unspecified,
        interactive = false,
        motion = null,
        opacity = 1f,
        interactionSource = null,
    )
    val dynamics = remember(menu, shape) {
        GlassDynamics(
            opacity = { (menu.morph / 0.15f).coerceIn(0f, 1f) },
            bounds = { size -> menu.morphRect(size) },
            radius = { menu.morphRadius(shape) },
        )
    }
    val focus = remember { FocusRequester() }
    val menuTitle = stringResource(R.string.liquidglass_menu)
    if (expanded && LocalOnBackPressedDispatcherOwner.current != null) BackHandler(onBack = onDismiss)
    LaunchedEffect(expanded) { if (expanded) focus.requestFocus() }
    ProvideGlassContentColor(appearance, tint) {
        // The panel sees its call site's locals; it is not part of a toolbar even when its trigger is.
        CompositionLocalProvider(LocalMenuDismiss provides onDismiss, LocalGlassToolbarItem provides false, LocalGlassToolbarClusters provides null) {
            Column(
                modifier
                    // As wide as its widest item (at least MinMenuWidth), up to the space it is given.
                    .widthIn(min = MinMenuWidth)
                    .width(IntrinsicSize.Max)
                    .onGloballyPositioned { menu.panelCoordinates = it }
                    .glassSurface(params, dynamics)
                    .graphicsLayer {
                        // Items appear once the glass has mostly grown, clipped to its morphing outline.
                        alpha = ((menu.morph - 0.35f) / 0.65f).coerceIn(0f, 1f)
                        clip = true
                        this.shape = MorphShape(menu, shape)
                    }
                    .semantics { paneTitle = menuTitle }
                    .focusRequester(focus)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.key == Key.Escape && event.type == KeyEventType.KeyUp) {
                            onDismiss()
                            true
                        } else {
                            false
                        }
                    }
                    .verticalScroll(rememberScrollState())
                    .padding(MenuInset),
                content = content,
            )
        }
    }
}

/** The clip of the menu items: the morphing glass outline. */
private class MorphShape(private val menu: MenuState, private val shape: GlassShape) : Shape {
    override fun createOutline(size: Size, layoutDirection: LayoutDirection, density: Density): Outline {
        val rect = menu.morphRect(size)
        val radius = menu.morphRadius(shape)
        val path = glassPath(rect.width, rect.height, if (radius == Dp.Unspecified) null else with(density) { radius.toPx() })
        path.translate(rect.topLeft)
        return Outline.Generic(path)
    }
}

/** Popup placement outside a scene: the same rule as the in-scene overlay. */
private class MenuPopupPosition(private val menu: MenuState) : PopupPositionProvider {
    override fun calculatePosition(anchorBounds: IntRect, windowSize: IntSize, layoutDirection: LayoutDirection, popupContentSize: IntSize): IntOffset {
        // anchorBounds are the trigger's; the panel covers the toolbar cluster around it when there is one.
        val inTrigger = menu.anchorInTrigger() ?: Rect(0f, 0f, anchorBounds.width.toFloat(), anchorBounds.height.toFloat())
        val anchor = inTrigger.translate(anchorBounds.left.toFloat(), anchorBounds.top.toFloat())
        return placePanel(anchor, popupContentSize, 0, 0, windowSize.width, windowSize.height, windowSize.width)
    }
}

/**
 * Open/close state of one menu: the morph spring (0 = the trigger's outline,
 * 1 = the panel) and the coordinates it morphs between.
 */
private class MenuState {
    var motion = GlassMotion.Full
    var animator: FrameAnimator? = null
    var trigger: LayoutCoordinates? = null
    var panelCoordinates: LayoutCoordinates? = null

    /** The toolbar the trigger is an item of, if any. */
    var toolbar: ToolbarClusters? = null

    /** Pixels per dp. */
    var density = 1f

    private val spring = SpringValue()
    private val morphState = mutableFloatStateOf(0f)
    private val expandedState = mutableStateOf(false)
    private val panelState = mutableStateOf<@Composable () -> Unit>({})
    private val localsState = mutableStateOf<CompositionLocalContext?>(null)

    val morph: Float get() = morphState.floatValue
    val expanded: Boolean get() = expandedState.value

    /** The menu is on screen: open, or still collapsing into the trigger. */
    val visible: Boolean get() = expandedState.value || morphState.floatValue > 0f

    /** The panel content, kept current by [GlassMenu] for the scene overlay. */
    var panel: @Composable () -> Unit
        get() = panelState.value
        set(value) {
            panelState.value = value
        }

    /** Composition locals at the menu's call site, so the overlay panel sees the same theme. */
    var locals: CompositionLocalContext?
        get() = localsState.value
        set(value) {
            localsState.value = value
        }

    /** The trigger fades out (glass and content) as the panel grows over it. */
    val triggerFade: () -> Float = { 1f - (morphState.floatValue * 1.6f).coerceIn(0f, 1f) }

    fun open(open: Boolean) {
        if (expandedState.value == open) return
        expandedState.value = open
        spring.target = if (open) 1f else 0f
        if (motion == GlassMotion.None) {
            spring.snap(spring.target)
            morphState.floatValue = spring.value
        }
        animator?.wake()
    }

    fun step(dt: Float): Boolean {
        when (motion) {
            GlassMotion.None -> spring.snap(spring.target)
            GlassMotion.Reduced -> spring.step(SpringSpec.Morph.critical(), dt)
            GlassMotion.Full -> spring.step(SpringSpec.Morph, dt)
        }
        if (spring.target == 0f && spring.value <= 0.001f) spring.snap(0f)
        // Collapsing may overshoot past the trigger; the glass never inverts.
        morphState.floatValue = if (spring.target == 0f) spring.value.coerceAtLeast(0f) else spring.value
        return spring.isSettled
    }

    /**
     * What the panel grows out of and covers, in the trigger's coordinates: for a
     * toolbar item the whole cluster it sits in (whose glass would otherwise show
     * around the panel), else the trigger itself (null). The cluster is overlapped
     * by [ClusterOverlap], so the capsule's ends stay inside the panel's rounder
     * continuous corners; whatever lies under the panel is frosted away.
     */
    fun anchorInTrigger(): Rect? {
        val trigger = trigger?.takeIf { it.isAttached } ?: return null
        return toolbar?.clusterAround(trigger)?.inflate(ClusterOverlap.value * density)
    }

    /** The anchor's bounds in [host], for placing the panel. */
    fun triggerIn(host: LayoutCoordinates): Rect {
        val trigger = trigger?.takeIf { it.isAttached && host.isAttached } ?: return Rect.Zero
        val anchor = anchorInTrigger() ?: Rect(Offset.Zero, trigger.size.toSize())
        if (trigger.findRootCoordinates() !== host.findRootCoordinates()) {
            // The trigger is in another window (a dialog): place by screen position instead.
            return Rect(trigger.localToScreen(anchor.topLeft) - host.localToScreen(Offset.Zero), anchor.size)
        }
        return Rect(host.localPositionOf(trigger, anchor.topLeft), host.localPositionOf(trigger, anchor.bottomRight))
    }

    /** The glass rectangle inside a panel of [size] pixels: from the trigger's bounds to the full panel. */
    fun morphRect(size: Size): Rect {
        val full = Rect(Offset.Zero, size)
        val from = triggerInPanel() ?: return full
        val m = morphState.floatValue
        return Rect(mix(from.left, full.left, m), mix(from.top, full.top, m), mix(from.right, full.right, m), mix(from.bottom, full.bottom, m))
    }

    /** Corner radius along the morph: from the trigger's capsule radius to the panel's radius. */
    fun morphRadius(shape: GlassShape): Dp {
        if (shape.isCapsule) return Dp.Unspecified
        val from = triggerInPanel() ?: return shape.radius
        return Dp(mix(from.height / 2f / density, shape.radius.value, morphState.floatValue))
    }

    /** The anchor's bounds relative to the panel (screen-based, so it also works across a popup window). */
    private fun triggerInPanel(): Rect? {
        val trigger = trigger?.takeIf { it.isAttached } ?: return null
        val panel = panelCoordinates?.takeIf { it.isAttached } ?: return null
        val anchor = anchorInTrigger() ?: Rect(Offset.Zero, trigger.size.toSize())
        val topLeft = trigger.localToScreen(anchor.topLeft) - panel.localToScreen(Offset.Zero)
        return Rect(topLeft, anchor.size)
    }
}

/** A check mark drawn as a 24-unit vector, so the library needs no icon dependency. */
private val CheckMark: ImageVector = ImageVector.Builder("CheckMark", 24.dp, 24.dp, 24f, 24f).apply {
    path(
        fill = null,
        stroke = SolidColor(Color.Black),
        strokeLineWidth = 2.6f,
        strokeLineCap = StrokeCap.Round,
        strokeLineJoin = StrokeJoin.Round,
    ) {
        moveTo(4.5f, 12.5f)
        lineTo(9.5f, 17.5f)
        lineTo(19.5f, 6.5f)
    }
}.build()

private val MenuInset = 6.dp

/** How far a menu reaches past the toolbar cluster it opens from. */
private val ClusterOverlap = 1.dp
/** The narrowest panel (as on iOS); it grows with its labels. */
private val MinMenuWidth = 220.dp

/** Space kept between the panel and the safe area's edges. */
private val PanelMargin = 8.dp
private val ItemShape = RoundedCornerShape(22.dp)
private val ItemTextStyle = TextStyle(fontSize = 17.sp, fontWeight = FontWeight.Normal)
private val DestructiveColor = Color(0xFFFF3B30)
