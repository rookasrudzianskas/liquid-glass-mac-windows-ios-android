package io.github.rookasrudzianskas.liquidglass.internal

import android.os.Build
import androidx.annotation.RequiresApi
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asComposeRenderEffect
import androidx.compose.ui.graphics.drawscope.ContentDrawScope
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.clipRect
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.layer.GraphicsLayer
import androidx.compose.ui.graphics.layer.drawLayer
import androidx.compose.ui.node.DrawModifierNode
import androidx.compose.ui.node.ModifierNodeElement
import androidx.compose.ui.node.TraversableNode
import androidx.compose.ui.node.invalidateDraw
import androidx.compose.ui.node.requireGraphicsContext
import androidx.compose.ui.node.requireLayoutCoordinates
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
internal class GlassContentNode(scene: GlassSceneState) : Modifier.Node(), DrawModifierNode, TraversableNode {

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

    /** The Android 13 effect builder, typed loosely so older runtimes never resolve RuntimeShader. */
    private var effects: Any? = null

    fun update(scene: GlassSceneState) {
        if (scene === this.scene) return
        this.scene.detachContent(this)
        this.scene = scene
        scene.attachContent(this)
        invalidateDraw()
    }

    /** Called by the scene when surface geometry changes outside snapshot state. */
    fun invalidateGlass() {
        if (isAttached) invalidateDraw()
    }

    override fun onAttach() = scene.attachContent(this)

    override fun onDetach() {
        scene.detachContent(this)
        val graphics = requireGraphicsContext()
        listOfNotNull(backdropLayer, bandedLayer, glassLayer, fallbackLayer).forEach(graphics::releaseGraphicsLayer)
        bandLayers.values.forEach { layers -> layers.forEach(graphics::releaseGraphicsLayer) }
        backdropLayer = null
        bandedLayer = null
        glassLayer = null
        fallbackLayer = null
        bandLayers.clear()
        effects = null
    }

    override fun ContentDrawScope.draw() {
        val coordinates = requireLayoutCoordinates()
        val surfaces = scene.surfaces().mapNotNull { it.frame(coordinates, density) }
        val blurs = scene.blurs().mapNotNull { owner -> owner.frame(coordinates, density)?.let { owner to it } }
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
        val fallback: List<SurfaceFrame> = if (Build.VERSION.SDK_INT >= 33) {
            val builder = (effects as? GlassEffectBuilder) ?: GlassEffectBuilder().also { effects = it }
            glass.renderEffect = builder.build(surfaces, size, density)?.asComposeRenderEffect()
            if (surfaces.size > MAX_SURFACES) surfaces.subList(MAX_SURFACES, surfaces.size) else emptyList()
        } else {
            surfaces
        }
        drawLayer(glass)
        if (fallback.isEmpty()) return
        // The fallback material over the platform blur of the backdrop.
        val frosted = fallbackLayer ?: graphics.createGraphicsLayer().also { fallbackLayer = it }
        frosted.record { drawLayer(source) }
        frosted.renderEffect = gaussian(FALLBACK_BLUR_SIGMA * density).asComposeRenderEffect()
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
                layer.renderEffect = progressiveLevelEffect(
                    level = level,
                    sigmaPx = sigma,
                    startX = center.x - halfX - left,
                    startY = center.y - halfY - top,
                    endX = center.x + halfX - left,
                    endY = center.y + halfY - top,
                ).asComposeRenderEffect()
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
        val glowRadius = glow(b.width / density, b.height / density, frame.dark).radius * density
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
                    glowRadiusPx = glowRadius,
                    glowAmount = frame.glowAmount,
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
