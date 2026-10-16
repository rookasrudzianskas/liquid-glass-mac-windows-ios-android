package io.github.rookasrudzianskas.liquidglass.internal

import androidx.compose.foundation.interaction.HoverInteraction
import androidx.compose.foundation.interaction.InteractionSource
import androidx.compose.foundation.interaction.PressInteraction
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.GraphicsLayerScope
import androidx.compose.ui.graphics.colorspace.ColorSpaces
import androidx.compose.ui.graphics.isSpecified
import androidx.compose.ui.graphics.drawscope.ContentDrawScope
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.input.pointer.PointerEvent
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.PointerEventType
import androidx.compose.ui.input.pointer.PointerId
import androidx.compose.ui.input.pointer.PointerType
import androidx.compose.ui.input.pointer.SuspendingPointerInputModifierNode
import androidx.compose.ui.input.pointer.changedToDownIgnoreConsumed
import androidx.compose.ui.layout.LayoutCoordinates
import androidx.compose.ui.layout.Measurable
import androidx.compose.ui.layout.MeasureResult
import androidx.compose.ui.layout.MeasureScope
import androidx.compose.ui.layout.findRootCoordinates
import androidx.compose.ui.node.CompositionLocalConsumerModifierNode
import androidx.compose.ui.node.DelegatingNode
import androidx.compose.ui.node.DrawModifierNode
import androidx.compose.ui.node.GlobalPositionAwareModifierNode
import androidx.compose.ui.node.LayoutModifierNode
import androidx.compose.ui.node.ModifierNodeElement
import androidx.compose.ui.node.ObserverModifierNode
import androidx.compose.ui.node.TraversableNode
import androidx.compose.ui.node.currentValueOf
import androidx.compose.ui.node.invalidateDraw
import androidx.compose.ui.node.observeReads
import androidx.compose.ui.node.requireDensity
import androidx.compose.ui.node.traverseAncestors
import androidx.compose.ui.platform.InspectorInfo
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.toSize
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassMotion
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.optics.SpringSpec
import io.github.rookasrudzianskas.liquidglass.optics.SpringValue
import io.github.rookasrudzianskas.liquidglass.optics.HOVER_GLOW
import io.github.rookasrudzianskas.liquidglass.optics.glow
import io.github.rookasrudzianskas.liquidglass.optics.pressScale
import io.github.rookasrudzianskas.liquidglass.optics.rubberBand
import io.github.rookasrudzianskas.liquidglass.optics.stretchLimit
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

/** The options of one glass surface, as given to `Modifier.liquidGlass`. */
@Immutable
internal data class GlassSurfaceParams(
    val shape: GlassShape,
    val material: GlassMaterial?,
    val appearance: GlassAppearance?,
    val tint: Color,
    val refraction: Dp,
    val interactive: Boolean,
    val motion: GlassMotion?,
    val opacity: Float,
    val interactionSource: InteractionSource?,
)

/**
 * Values the library's own components animate on a surface without
 * recomposing: read while the scene draws, so snapshot state read here
 * redraws the glass directly.
 */
internal class GlassDynamics(
    /** Multiplies the surface opacity. */
    val opacity: () -> Float = { 1f },
    /** The glass rectangle inside the node, in its local pixels; null keeps the node's bounds. */
    val bounds: ((Size) -> Rect)? = null,
    /** Overrides the corner radius in dp ([Dp.Unspecified] for a capsule); null keeps the shape's. */
    val radius: (() -> Dp)? = null,
    /** When set, a container forwards pointer input through it instead of the surface observing its own. */
    val pointer: GlassPointerHandle? = null,
)

/**
 * Lets a container drive an interactive surface's press, drag and glow, for
 * surfaces that are siblings of the content they respond to (toolbar clusters).
 * Positions are in the surface's local pixels.
 */
internal class GlassPointerHandle {
    internal var node: GlassSurfaceNode? = null

    fun down(position: Offset) = node?.pointerDown(position)

    fun move(position: Offset) = node?.pointerMove(position)

    fun up() = node?.pointerUp()
}

/** Multiplies the opacity of all glass below it in the composition (a menu's trigger fades as the menu takes over). */
internal val LocalGlassFade = staticCompositionLocalOf<(() -> Float)?> { null }

/** Raises surfaces composed in a scene overlay (menus) above everything else. */
internal val LocalGlassOverlayLevel = staticCompositionLocalOf { 0 }

internal const val OVERLAY_LEVEL: Int = 1000

internal class GlassSurfaceElement(
    private val params: GlassSurfaceParams,
    private val dynamics: GlassDynamics?,
) : ModifierNodeElement<GlassSurfaceNode>() {
    override fun create(): GlassSurfaceNode = GlassSurfaceNode(params, dynamics)

    override fun update(node: GlassSurfaceNode) = node.update(params, dynamics)

    override fun InspectorInfo.inspectableProperties() {
        name = "liquidGlass"
        properties["shape"] = params.shape
        properties["material"] = params.material
        properties["appearance"] = params.appearance
        properties["tint"] = params.tint
        properties["refraction"] = params.refraction
        properties["interactive"] = params.interactive
        properties["motion"] = params.motion
        properties["opacity"] = params.opacity
    }

    override fun equals(other: Any?): Boolean =
        other is GlassSurfaceElement && other.params == params && other.dynamics === dynamics

    override fun hashCode(): Int = 31 * params.hashCode() + System.identityHashCode(dynamics)
}

/** Traversal key shared by every glass surface node, used to find nesting. */
internal object GlassSurfaceKey

/**
 * A glass surface: registers its geometry and optics with the nearest scene
 * every frame, animates press, drag and glow on the spec springs, and draws the
 * fallback material itself when no scene can render it.
 */
internal class GlassSurfaceNode(
    private var params: GlassSurfaceParams,
    private var dynamics: GlassDynamics?,
) : DelegatingNode(),
    LayoutModifierNode,
    DrawModifierNode,
    GlobalPositionAwareModifierNode,
    CompositionLocalConsumerModifierNode,
    ObserverModifierNode,
    TraversableNode,
    SceneSurface {

    override val traverseKey: Any get() = GlassSurfaceKey
    override val shouldAutoInvalidate: Boolean get() = false

    private var scene: GlassSceneState? = null
    private var style = GlassStyle()
    private var overlayLevel = 0
    private var fade: (() -> Float)? = null
    private var insideContent = false
    private var nested = false
    override var depth: Int = 0
        private set

    private var coordinates: LayoutCoordinates? = null
    private var lastRootBounds = Rect.Zero

    // Springs, advanced on frames only while something moves.
    private val press = SpringValue()
    private val glowSpring = SpringValue()
    private val dragX = SpringValue()
    private val dragY = SpringValue()
    private var animator: FrameAnimator? = null
    private var interactions: Job? = null

    // Published values, read while drawing and in the layer block.
    private val scaleX = mutableFloatStateOf(1f)
    private val scaleY = mutableFloatStateOf(1f)
    private val offsetX = mutableFloatStateOf(0f)
    private val offsetY = mutableFloatStateOf(0f)
    private val glowAmount = mutableFloatStateOf(0f)
    private val glowX = mutableFloatStateOf(0f)
    private val glowY = mutableFloatStateOf(0f)

    // Pointer tracking.
    private var trackedPointer: PointerId? = null
    private var downPosition = Offset.Zero
    private var pressed = false
    private var hovered = false

    private val layerBlock: GraphicsLayerScope.() -> Unit = {
        scaleX = this@GlassSurfaceNode.scaleX.floatValue
        scaleY = this@GlassSurfaceNode.scaleY.floatValue
        translationX = offsetX.floatValue * density
        translationY = offsetY.floatValue * density
    }

    init {
        delegate(SuspendingPointerInputModifierNode {
            awaitPointerEventScope {
                while (true) {
                    val event = awaitPointerEvent(PointerEventPass.Initial)
                    if (params.interactive) onPointerEvent(event)
                }
            }
        })
    }

    fun update(params: GlassSurfaceParams, dynamics: GlassDynamics?) {
        val sourceChanged = params.interactionSource != this.params.interactionSource
        this.params = params
        if (dynamics !== this.dynamics) {
            this.dynamics?.pointer?.let { if (it.node === this) it.node = null }
            dynamics?.pointer?.node = this
        }
        this.dynamics = dynamics
        if (sourceChanged && isAttached) collectInteractions()
        if (!params.interactive && (pressed || hovered)) {
            pressed = false
            hovered = false
            retarget()
        }
        invalidateDraw()
        scene?.invalidate()
    }

    override fun onAttach() {
        readLocals()
        resolveNesting()
        scene?.register(this)
        dynamics?.pointer?.node = this
        collectInteractions()
    }

    override fun onDetach() {
        dynamics?.pointer?.let { if (it.node === this) it.node = null }
        animator = null
        scene?.unregister(this)
        coordinates = null
        lastRootBounds = Rect.Zero
        trackedPointer = null
        pressed = false
        hovered = false
        // Nodes are reused (lazy lists): come back at rest.
        for (spring in listOf(press, glowSpring, dragX, dragY)) spring.snap(0f)
        scaleX.floatValue = 1f
        scaleY.floatValue = 1f
        offsetX.floatValue = 0f
        offsetY.floatValue = 0f
        glowAmount.floatValue = 0f
    }

    override fun onObservedReadsChanged() {
        val previous = scene
        readLocals()
        if (previous !== scene) {
            previous?.unregister(this)
            resolveNesting()
            scene?.register(this)
        }
        invalidateDraw()
        scene?.invalidate()
    }

    /**
     * Depth among surfaces of the same scene, and whether this surface sits
     * inside its own scene's content (where it cannot refract the layer it
     * belongs to). Content of an outer scene is fine: nested scenes are its content.
     */
    private fun resolveNesting() {
        var surfaces = 0
        var inContent = false
        traverseAncestors(GlassSurfaceKey) { surfaces++; true }
        traverseAncestors(GlassContentKey) { node ->
            if ((node as GlassContentNode).scene === scene) inContent = true
            !inContent
        }
        nested = surfaces > 0
        depth = overlayLevel + surfaces
        insideContent = inContent
    }

    private fun readLocals() {
        observeReads {
            scene = currentValueOf(LocalGlassScene)
            style = currentValueOf(LocalGlassStyle)
            overlayLevel = currentValueOf(LocalGlassOverlayLevel)
            fade = currentValueOf(LocalGlassFade)
        }
    }

    override fun onSceneRenderingChanged() = invalidateDraw()

    /** The scene's content layer draws this glass; otherwise the node draws its own fallback. */
    private val renderedByScene: Boolean get() = scene?.hasContent == true && !insideContent && sameWindow

    /** Whether the last layout put this surface in the scene content's window. */
    private var sameWindow = true

    // --- Layout and geometry --------------------------------------------------------

    override fun MeasureScope.measure(measurable: Measurable, constraints: Constraints): MeasureResult {
        val placeable = measurable.measure(constraints)
        return layout(placeable.width, placeable.height) { placeable.placeWithLayer(0, 0, layerBlock = layerBlock) }
    }

    override fun onGloballyPositioned(coordinates: LayoutCoordinates) {
        this.coordinates = coordinates
        val inWindow = scene?.renders(coordinates) ?: true
        if (inWindow != sameWindow) {
            sameWindow = inWindow
            invalidateDraw()
        }
        val rootBounds = coordinates.findRootCoordinates().localBoundingBoxOf(coordinates, clipBounds = false)
        if (rootBounds != lastRootBounds) {
            lastRootBounds = rootBounds
            scene?.invalidate()
        }
    }

    override fun frame(content: LayoutCoordinates, density: Float): SurfaceFrame? {
        if (!renderedByScene) return null
        val coordinates = coordinates?.takeIf { it.isAttached } ?: return null
        val size = coordinates.size.toSize()
        if (size.width <= 0f || size.height <= 0f || !content.isAttached) return null
        val box = content.localBoundingBoxOf(coordinates, clipBounds = false)
        val clipped = content.localBoundingBoxOf(coordinates, clipBounds = true)
        if (clipped.isEmpty) return null
        val kx = box.width / size.width
        val ky = box.height / size.height
        val local = dynamics?.bounds?.invoke(size) ?: Rect(Offset.Zero, size)
        val sx = scaleX.floatValue
        val sy = scaleY.floatValue
        val center = Offset(
            box.left + local.center.x * kx + offsetX.floatValue * density * kx,
            box.top + local.center.y * ky + offsetY.floatValue * density * ky,
        )
        val halfW = local.width * kx * sx / 2f
        val halfH = local.height * ky * sy / 2f
        val bounds = Rect(center.x - halfW, center.y - halfH, center.x + halfW, center.y + halfH)
        // Off the content layer entirely: nothing to draw, and no GPU slot used.
        if (!bounds.overlaps(Rect(Offset.Zero, content.size.toSize()))) return null
        val clip = if (clipped == box) INFINITE else clipped
        val dark = style.isDark(params.appearance)
        val widthDp = local.width / density
        val heightDp = local.height / density
        val glowAt = Offset(box.left + glowX.floatValue * kx, box.top + glowY.floatValue * ky)
        return SurfaceFrame(
            bounds = bounds,
            clip = clip,
            radius = radiusDp(),
            material = materialCode(),
            dark = dark,
            nested = nested,
            opacity = opacity(),
            contrast = style.increaseContrast,
            tint = params.tint.inSrgb(),
            refraction = if (params.refraction == Dp.Unspecified) style.refraction else params.refraction.value,
            glowX = (glowAt.x - center.x) / density,
            glowY = (glowAt.y - center.y) / density,
            glowRadius = glow(widthDp, heightDp, dark).radius,
            glowAmount = glowAmount.floatValue,
        )
    }

    private fun opacity(): Float =
        (params.opacity * (dynamics?.opacity?.invoke() ?: 1f) * (fade?.invoke() ?: 1f)).coerceIn(0f, 1f)

    private fun radiusDp(): Float? {
        val override = dynamics?.radius?.invoke()
        if (override != null) return if (override == Dp.Unspecified) null else override.value
        return params.shape.radiusDp
    }

    private fun materialCode(): Float = when {
        style.reduceTransparency -> ShaderMaterial.SOLID
        style.material(params.material) == GlassMaterial.Regular -> ShaderMaterial.REGULAR
        else -> ShaderMaterial.CLEAR
    }

    // --- Drawing ----------------------------------------------------------------------

    override fun ContentDrawScope.draw() {
        if (!renderedByScene) drawOwnFallback()
        drawContent()
    }

    /** Fallback material for glass no scene renders (outside a scene, or inside its content). */
    private fun ContentDrawScope.drawOwnFallback() {
        val local = dynamics?.bounds?.invoke(size) ?: Rect(Offset.Zero, size)
        val opacity = opacity()
        if (opacity <= 0f) return
        val dark = style.isDark(params.appearance)
        translate(offsetX.floatValue * density, offsetY.floatValue * density) {
            scale(scaleX.floatValue, scaleY.floatValue, pivot = local.center) {
                translate(local.left, local.top) {
                    drawFallbackGlass(
                        size = local.size,
                        radiusDp = radiusDp(),
                        material = materialCode(),
                        dark = dark,
                        tint = params.tint.inSrgb(),
                        opacity = opacity,
                        contrast = style.increaseContrast,
                        glowCenter = Offset(glowX.floatValue - local.left, glowY.floatValue - local.top),
                        glow = glow(local.width / density, local.height / density, dark),
                        glowAmount = glowAmount.floatValue,
                        blurredBackdrop = null,
                        backdropOffset = Offset.Zero,
                    )
                }
            }
        }
    }

    // --- Interaction --------------------------------------------------------------

    private fun collectInteractions() {
        interactions?.cancel()
        val source = params.interactionSource ?: return
        interactions = coroutineScope.launch {
            source.interactions.collect { interaction ->
                when (interaction) {
                    is PressInteraction.Press -> {
                        moveGlow(interaction.pressPosition)
                        setPressed(true)
                    }
                    is PressInteraction.Release, is PressInteraction.Cancel -> setPressed(false)
                    is HoverInteraction.Enter -> setHovered(true)
                    is HoverInteraction.Exit -> setHovered(false)
                }
            }
        }
    }

    private fun onPointerEvent(event: PointerEvent) {
        if (dynamics?.pointer != null) return
        val tracked = trackedPointer
        if (tracked == null) {
            val down = event.changes.firstOrNull { it.changedToDownIgnoreConsumed() }
            if (down != null) {
                trackedPointer = down.id
                pointerDown(down.position)
                return
            }
            val mouse = event.changes.firstOrNull { it.type == PointerType.Mouse && !it.pressed }
            if (mouse != null) {
                // The glow follows the mouse; hover itself comes from the interaction source when there is one.
                if (event.type == PointerEventType.Enter || event.type == PointerEventType.Move) moveGlow(mouse.position)
                if (params.interactionSource == null) {
                    when (event.type) {
                        PointerEventType.Enter, PointerEventType.Move -> setHovered(true)
                        PointerEventType.Exit -> setHovered(false)
                    }
                }
            }
            return
        }
        val change = event.changes.firstOrNull { it.id == tracked }
        if (change == null || !change.pressed) {
            trackedPointer = null
            pointerUp()
        } else {
            pointerMove(change.position)
        }
    }

    internal fun pointerDown(position: Offset) {
        if (!params.interactive) return
        downPosition = position
        moveGlow(position)
        if (params.interactionSource == null) setPressed(true)
    }

    internal fun pointerMove(position: Offset) {
        if (!params.interactive) return
        moveGlow(position)
        if (!pressed) return
        val density = requireDensity().density
        val size = coordinates?.size ?: return
        val limit = stretchLimit(size.width / density, size.height / density)
        dragX.target = rubberBand((position.x - downPosition.x) / density, limit)
        dragY.target = rubberBand((position.y - downPosition.y) / density, limit)
        wake()
    }

    internal fun pointerUp() {
        if (params.interactionSource == null) setPressed(false)
    }

    private fun moveGlow(position: Offset) {
        glowX.floatValue = position.x
        glowY.floatValue = position.y
    }

    private fun setPressed(value: Boolean) {
        if (pressed == value) return
        pressed = value
        retarget()
    }

    private fun setHovered(value: Boolean) {
        if (hovered == value) return
        hovered = value
        retarget()
    }

    private fun retarget() {
        press.target = if (pressed) 1f else 0f
        glowSpring.target = if (pressed) 1f else if (hovered) HOVER_GLOW else 0f
        if (!pressed) {
            dragX.target = 0f
            dragY.target = 0f
        }
        wake()
    }

    private fun wake() {
        if (!isAttached) return
        val animator = animator ?: FrameAnimator(coroutineScope, ::step).also { animator = it }
        animator.wake()
    }

    /** Advances every spring by [dt] seconds and publishes the result. Returns true once all are at rest. */
    private fun step(dt: Float): Boolean {
        val motion = style.motion(params.motion)
        val density = requireDensity().density
        val size = coordinates?.size
        val w = (size?.width ?: 0) / density
        val h = (size?.height ?: 0) / density
        if (motion == GlassMotion.None) {
            press.snap(press.target)
            glowSpring.snap(glowSpring.target)
            dragX.snap(dragX.target)
            dragY.snap(dragY.target)
        } else {
            val reduced = motion == GlassMotion.Reduced
            fun SpringSpec.forMotion() = if (reduced) critical() else this
            press.step((if (press.target > press.value) SpringSpec.Press else SpringSpec.Release).forMotion(), dt)
            glowSpring.step(SpringSpec.Glow, dt)
            val drag = if (pressed) SpringSpec.trackWhileDragging(w, h) else SpringSpec.Release.forMotion()
            dragX.step(drag, dt)
            dragY.step(drag, dt)
        }
        val reduced = motion != GlassMotion.Full
        val s = pressScale(w, h, press.value, dragX.value, dragY.value, dragX.velocity, dragY.velocity, reduced)
        scaleX.floatValue = s.sx
        scaleY.floatValue = s.sy
        offsetX.floatValue = if (reduced) 0f else dragX.value
        offsetY.floatValue = if (reduced) 0f else dragY.value
        glowAmount.floatValue = glowSpring.value
        return press.isSettled && glowSpring.isSettled && dragX.isSettled && dragY.isSettled
    }

    private companion object {
        val INFINITE = Rect(-1e7f, -1e7f, 1e7f, 1e7f)
    }
}

/** The surface modifier: a layer for press scale and drag, plus the glass node. */
internal fun Modifier.glassSurface(params: GlassSurfaceParams, dynamics: GlassDynamics? = null): Modifier =
    this then GlassSurfaceElement(params, dynamics)

/** The optics run on sRGB values; tints given in another color space are converted first. */
private fun Color.inSrgb(): Color = if (isSpecified) convert(ColorSpaces.Srgb) else this
