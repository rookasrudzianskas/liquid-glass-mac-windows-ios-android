package io.github.rookasrudzianskas.liquidglass.internal

import android.os.Build
import android.view.View
import android.view.ViewTreeObserver
import androidx.annotation.RequiresApi
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.RenderEffect
import androidx.compose.ui.graphics.asComposeRenderEffect
import androidx.compose.ui.graphics.drawscope.ContentDrawScope
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.clipRect
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.layer.GraphicsLayer
import androidx.compose.ui.graphics.layer.drawLayer
import androidx.compose.ui.layout.LayoutCoordinates
import androidx.compose.ui.layout.findRootCoordinates
import androidx.compose.ui.node.DrawModifierNode
import androidx.compose.ui.node.GlobalPositionAwareModifierNode
import androidx.compose.ui.node.ModifierNodeElement
import androidx.compose.ui.node.TraversableNode
import androidx.compose.ui.node.invalidateDraw
import androidx.compose.ui.node.requireGraphicsContext
import androidx.compose.ui.node.requireLayoutCoordinates
import androidx.compose.ui.node.requireView
import androidx.compose.ui.platform.InspectorInfo
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.Modifier
import io.github.rookasrudzianskas.liquidglass.optics.FROST_SIGMA
import io.github.rookasrudzianskas.liquidglass.optics.PROGRESSIVE_LEVELS
import io.github.rookasrudzianskas.liquidglass.optics.glow
import io.github.rookasrudzianskas.liquidglass.optics.progressiveLevelBlur
import kotlin.math.ceil
import kotlin.math.floor

/** Traversal key of the content node, so surfaces can tell they sit inside the content. */
internal object GlassContentKey

internal class GlassContentElement(private val scene: GlassSceneState) : ModifierNodeElement<GlassContentNode>() {
    override fun create(): GlassContentNode = GlassContentNode(scene)

    override fun update(node: GlassContentNode) = node.update(scene)

    override fun InspectorInfo.inspectableProperties() {
        name = "glassContent"
    }

    override fun equals(other: Any?): Boolean = other is GlassContentElement && other.scene === scene

    override fun hashCode(): Int = System.identityHashCode(scene)
}

/**
 * The backdrop of a scene. Records its content into a layer, adds progressive
 * blur bands, and renders every registered surface onto it:
 *
 * - Android 13+: the AGSL glass effect (up to [MAX_SURFACES]; more get the fallback material).
 * - Android 12: the fallback material over the platform blur.
 * - Older: the fallback material over the sharp content.
 */
internal class GlassContentNode(scene: GlassSceneState) : Modifier.Node(), DrawModifierNode, TraversableNode, GlobalPositionAwareModifierNode {

    /** The scene whose backdrop this is. */
    var scene: GlassSceneState = scene
        private set

    override val traverseKey: Any get() = GlassContentKey
    override val shouldAutoInvalidate: Boolean get() = false

    /** The scene content as recorded this frame. */
    private var backdropLayer: GraphicsLayer? = null

    /** The content with progressive blur bands painted over it (only while bands exist). */
    private var bandedLayer: GraphicsLayer? = null

    /** Draws the backdrop through the glass effect. */
    private var glassLayer: GraphicsLayer? = null

    /** The backdrop under the platform blur, for the fallback material. */
    private var fallbackLayer: GraphicsLayer? = null

    private val bandLayers = HashMap<SceneBlur, Array<GraphicsLayer>>()

    /** Each band's level effects, kept while its geometry and strength stay the same. */
    private val bandEffects = HashMap<SceneBlur, BandEffects>()

    /** The platform blur under fallback glass, built for [fallbackBlurDensity]. */
    private var fallbackBlur: RenderEffect? = null
    private var fallbackBlurDensity = Float.NaN

    /** The Android 13 effect builder, typed loosely so older runtimes never resolve RuntimeShader. */
    private var effects: Any? = null

    // Reused every frame.
    private val surfaceFrames = ArrayList<SurfaceFrame>()
    private val blurFrames = ArrayList<Pair<SceneBlur, BlurFrame>>()

    fun update(scene: GlassSceneState) {
        if (scene === this.scene) return
        this.scene.detachContent(this)
        this.scene = scene
        scene.attachContent(this)
        invalidateDraw()
    }

    private var lastRootBounds = Rect.Zero

    /** Surface geometry is relative to the content, so the glass moves when the content itself moves. */
    override fun onGloballyPositioned(coordinates: LayoutCoordinates) {
        val bounds = coordinates.findRootCoordinates().localBoundingBoxOf(coordinates, clipBounds = false)
        if (bounds == lastRootBounds) return
        lastRootBounds = bounds
        invalidateDraw()
    }

    /** The root of this node's window, to tell which surfaces it can render. */
    fun rootCoordinates(): LayoutCoordinates = requireLayoutCoordinates().findRootCoordinates()

    /** Called by the scene when surface geometry changes outside snapshot state. */
    fun invalidateGlass() {
        if (isAttached) invalidateDraw()
    }

    /**
     * Before each draw of the window, checks whether glass moved without a
     * relayout (an ancestor's layer changed) and redraws the glass if so.
     */
    private val preDraw = ViewTreeObserver.OnPreDrawListener {
        if (isAttached && scene.hasParticipants) {
            val coordinates = requireLayoutCoordinates()
            if (coordinates.isAttached && scene.moved(coordinates)) invalidateDraw()
        }
        true
    }
    private var view: View? = null
    private var treeObserver: ViewTreeObserver? = null

    override fun onAttach() {
        scene.attachContent(this)
        val view = requireView()
        this.view = view
        treeObserver = view.viewTreeObserver.also { it.addOnPreDrawListener(preDraw) }
    }

    override fun onDetach() {
        // A tree observer registered before the view attached is merged into the window's and dies.
        val observer = treeObserver?.takeIf { it.isAlive } ?: view?.viewTreeObserver
        if (observer?.isAlive == true) observer.removeOnPreDrawListener(preDraw)
        treeObserver = null
        view = null
        scene.detachContent(this)
        val graphics = requireGraphicsContext()
        listOfNotNull(backdropLayer, bandedLayer, glassLayer, fallbackLayer).forEach(graphics::releaseGraphicsLayer)
        bandLayers.values.forEach { layers -> layers.forEach(graphics::releaseGraphicsLayer) }
        backdropLayer = null
        bandedLayer = null
        glassLayer = null
        fallbackLayer = null
        bandLayers.clear()
        bandEffects.clear()
        fallbackBlur = null
        effects = null
        lastRootBounds = Rect.Zero
        surfaceFrames.clear()
        blurFrames.clear()
    }

    override fun ContentDrawScope.draw() {
        val coordinates = requireLayoutCoordinates()
        val surfaces = surfaceFrames.apply {
            clear()
            for (surface in scene.surfaces()) surface.frame(coordinates, density)?.let(::add)
        }
        val blurs = blurFrames.apply {
            clear()
            for (owner in scene.blurs()) owner.frame(coordinates, density)?.let { add(owner to it) }
        }
        releaseStaleBands(blurs)
        if (Build.VERSION.SDK_INT < 31) {
            drawContent()
            blurs.forEach { (_, blur) -> drawBlurScrim(blur) }
            surfaces.forEach { drawFallback(it, frosted = null) }
            return
        }
        drawWithLayers(surfaces, blurs)
    }

    private fun releaseStaleBands(active: List<Pair<SceneBlur, BlurFrame>>) {
        bandEffects.keys.retainAll { key -> active.any { it.first === key } }
        if (bandLayers.isEmpty()) return
        val stale = bandLayers.keys.filter { key -> active.none { it.first === key } }
        if (stale.isEmpty()) return
        val graphics = requireGraphicsContext()
        stale.forEach { key -> bandLayers.remove(key)?.forEach(graphics::releaseGraphicsLayer) }
    }

    @RequiresApi(31)
    private fun ContentDrawScope.drawWithLayers(surfaces: List<SurfaceFrame>, blurs: List<Pair<SceneBlur, BlurFrame>>) {
        val graphics = requireGraphicsContext()
        val backdrop = backdropLayer ?: graphics.createGraphicsLayer().also { backdropLayer = it }
        backdrop.record { this@drawWithLayers.drawContent() }
        val source = if (blurs.isEmpty()) {
            backdrop
        } else {
            // Progressive blur bands are part of the backdrop the glass refracts.
            val banded = bandedLayer ?: graphics.createGraphicsLayer().also { bandedLayer = it }
            banded.record {
                drawLayer(backdrop)
                blurs.forEach { (owner, blur) -> drawBand(owner, blur, backdrop) }
            }
            banded
        }
        val glass = glassLayer ?: graphics.createGraphicsLayer().also { glassLayer = it }
        glass.record { drawLayer(source) }
        val fallback: List<SurfaceFrame> = when {
            surfaces.isEmpty() -> {
                glass.renderEffect = null
                surfaces
            }
            Build.VERSION.SDK_INT >= 33 -> {
                val builder = (effects as? GlassEffectBuilder) ?: GlassEffectBuilder().also { effects = it }
                glass.renderEffect = builder.build(surfaces, size, density)?.asComposeRenderEffect()
                if (surfaces.size > MAX_SURFACES) surfaces.subList(MAX_SURFACES, surfaces.size) else emptyList()
            }
            else -> surfaces
        }
        // Android 14 runs a layer's effect over the whole clip, not just the layer, and samples the content
        // clamped to its edges there, so an unclipped glass layer paints its edge pixels across the window over
        // whatever was drawn before it (another scene to its left or above, for one). Android 17 bounds the
        // effect to the layer; clipped to it, every version draws the same.
        glass.clip = glass.renderEffect != null
        drawLayer(glass)
        if (fallback.isEmpty()) return
        // The fallback material over the platform blur of the backdrop.
        val frosted = fallbackLayer ?: graphics.createGraphicsLayer().also { fallbackLayer = it }
        frosted.record { drawLayer(source) }
        if (fallbackBlur == null || fallbackBlurDensity != density) {
            fallbackBlurDensity = density
            fallbackBlur = gaussian(FALLBACK_BLUR_SIGMA * density).asComposeRenderEffect()
        }
        frosted.renderEffect = fallbackBlur
        fallback.forEach { drawFallback(it, frosted) }
    }

    /** Paints levels 1…6 of a band over the sharp backdrop, each masked so the levels mix with the spec weights. */
    @RequiresApi(31)
    private fun DrawScope.drawBand(owner: SceneBlur, blur: BlurFrame, sharp: GraphicsLayer) {
        val graphics = requireGraphicsContext()
        val layers = bandLayers.getOrPut(owner) { Array(PROGRESSIVE_LEVELS) { graphics.createGraphicsLayer() } }
        val bounds = Rect(Offset.Zero, size)
        val band = blur.band.intersect(bounds)
        if (band.isEmpty) return
        val halfX = blur.band.width / 2f * blur.towardX
        val halfY = blur.band.height / 2f * blur.towardY
        val center = blur.band.center
        // The level effects depend only on where the band sits in the layer and how strong it is.
        val key = BandKey(blur.band, bounds, blur.towardX, blur.towardY, blur.blurPx)
        val cached = bandEffects[owner]?.takeIf { it.key == key }
        val effects = cached ?: BandEffects(key).also { bandEffects[owner] = it }
        clipRect(band.left, band.top, band.right, band.bottom) {
            for (level in 1..PROGRESSIVE_LEVELS) {
                val sigma = progressiveLevelBlur(level, blur.blurPx)
                val margin = ceil(3f * sigma + 2f)
                val area = blur.band.inflate(margin).intersect(bounds)
                val left = floor(area.left)
                val top = floor(area.top)
                val layer = layers[level - 1]
                layer.topLeft = IntOffset(left.toInt(), top.toInt())
                layer.record(IntSize(ceil(area.right - left).toInt(), ceil(area.bottom - top).toInt())) {
                    translate(-left, -top) { drawLayer(sharp) }
                }
                val effect = effects.levels[level - 1] ?: progressiveLevelEffect(
                    level = level,
                    sigmaPx = sigma,
                    startX = center.x - halfX - left,
                    startY = center.y - halfY - top,
                    endX = center.x + halfX - left,
                    endY = center.y + halfY - top,
                ).asComposeRenderEffect().also { effects.levels[level - 1] = it }
                layer.renderEffect = effect
                drawLayer(layer)
            }
        }
    }

    /** Below Android 12 there is no blur: the band fades toward the edge instead. */
    private fun DrawScope.drawBlurScrim(blur: BlurFrame) {
        val halfX = blur.band.width / 2f * blur.towardX
        val halfY = blur.band.height / 2f * blur.towardY
        val center = blur.band.center
        val edge = if (blur.dark) Color.Black.copy(alpha = 0.45f) else Color.White.copy(alpha = 0.6f)
        drawRect(
            brush = Brush.linearGradient(
                listOf(Color.Transparent, edge),
                start = Offset(center.x - halfX, center.y - halfY),
                end = Offset(center.x + halfX, center.y + halfY),
            ),
            topLeft = blur.band.topLeft,
            size = blur.band.size,
        )
    }

    private fun DrawScope.drawFallback(frame: SurfaceFrame, frosted: GraphicsLayer?) {
        val b = frame.bounds
        clipRect(frame.clip.left, frame.clip.top, frame.clip.right, frame.clip.bottom) {
            translate(b.left, b.top) {
                drawFallbackGlass(
                    size = Size(b.width, b.height),
                    radiusDp = frame.radius,
                    material = frame.material,
                    dark = frame.dark,
                    tint = frame.tint,
                    opacity = frame.opacity,
                    contrast = frame.contrast,
                    glowCenter = Offset(b.width / 2f + frame.glowX * density, b.height / 2f + frame.glowY * density),
                    glow = glow(b.width / density, b.height / density, frame.dark),
                    glowAmount = frame.glowAmount,
                    legibility = frame.legibility,
                    blurredBackdrop = frosted.takeIf { frame.regular },
                    backdropOffset = Offset(-b.left, -b.top),
                )
            }
        }
    }

    private companion object {
        /** Blur of the fallback Regular material, dp: the frost plus a share of the fill. */
        const val FALLBACK_BLUR_SIGMA = FROST_SIGMA * 1.6f
    }
}

/** What a band's level effects are built from: its rectangle in a layer of the given bounds, direction and strength. */
private data class BandKey(val band: Rect, val layer: Rect, val towardX: Float, val towardY: Float, val blurPx: Float)

/** A band's level effects (levels 1…6), built lazily for [key]. */
private class BandEffects(val key: BandKey) {
    val levels = arrayOfNulls<RenderEffect>(PROGRESSIVE_LEVELS)
}
