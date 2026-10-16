package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.toRect
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.ContentDrawScope
import androidx.compose.ui.layout.LayoutCoordinates
import androidx.compose.ui.node.CompositionLocalConsumerModifierNode
import androidx.compose.ui.node.DrawModifierNode
import androidx.compose.ui.node.GlobalPositionAwareModifierNode
import androidx.compose.ui.node.ModifierNodeElement
import androidx.compose.ui.node.ObserverModifierNode
import androidx.compose.ui.node.currentValueOf
import androidx.compose.ui.node.invalidateDraw
import androidx.compose.ui.node.observeReads
import androidx.compose.ui.platform.InspectorInfo
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.Modifier.Node
import io.github.rookasrudzianskas.liquidglass.internal.BlurFrame
import io.github.rookasrudzianskas.liquidglass.internal.GlassSceneState
import io.github.rookasrudzianskas.liquidglass.internal.GlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassScene
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.SceneBlur

/** The edge of the content a [GlassProgressiveBlur] fades into. */
public enum class GlassEdge {
    /** The top edge. */
    Top,

    /** The bottom edge. */
    Bottom,

    /** The leading edge (left in left-to-right layouts). */
    Start,

    /** The trailing edge (right in left-to-right layouts). */
    End,
}

/**
 * Progressive edge blur over the scene's [GlassContent], as content scrolls
 * under a toolbar or tab bar: sharp on the inner side, blurring smoothly to
 * [blur] at [edge] (spec/OPTICS.md §7, six blur levels mixed by depth).
 *
 * The blur covers this element's bounds, so place it along the matching edge
 * of the scene. It sizes itself to the full length of that edge and [size]
 * deep unless the [modifier] says otherwise. Glass over the band refracts the
 * blurred content.
 *
 * ```
 * GlassScene {
 *     GlassContent { LazyColumn(Modifier.fillMaxSize()) { … } }
 *     GlassProgressiveBlur(GlassEdge.Top, Modifier.align(Alignment.TopCenter), size = 120.dp)
 * }
 * ```
 *
 * The platform blur needs Android 12; older versions fade the band toward the
 * edge instead.
 *
 * @param edge the edge that is blurred most.
 * @param modifier modifier for the band.
 * @param size depth of the band from the edge.
 * @param blur Gaussian σ at the edge.
 */
@Composable
public fun GlassProgressiveBlur(
    edge: GlassEdge,
    modifier: Modifier = Modifier,
    size: Dp = GlassDefaults.ProgressiveBlurSize,
    blur: Dp = GlassDefaults.ProgressiveBlurRadius,
) {
    val extent = when (edge) {
        GlassEdge.Top, GlassEdge.Bottom -> Modifier.fillMaxWidth().height(size)
        GlassEdge.Start, GlassEdge.End -> Modifier.fillMaxHeight().width(size)
    }
    Box(modifier.then(extent).then(ProgressiveBlurElement(edge, blur)))
}

private class ProgressiveBlurElement(private val edge: GlassEdge, private val blur: Dp) : ModifierNodeElement<ProgressiveBlurNode>() {
    override fun create() = ProgressiveBlurNode(edge, blur)

    override fun update(node: ProgressiveBlurNode) = node.update(edge, blur)

    override fun InspectorInfo.inspectableProperties() {
        name = "glassProgressiveBlur"
        properties["edge"] = edge
        properties["blur"] = blur
    }

    override fun equals(other: Any?) = other is ProgressiveBlurElement && other.edge == edge && other.blur == blur

    override fun hashCode() = 31 * edge.hashCode() + blur.hashCode()
}

private class ProgressiveBlurNode(private var edge: GlassEdge, private var blur: Dp) :
    Node(), SceneBlur, GlobalPositionAwareModifierNode, DrawModifierNode, CompositionLocalConsumerModifierNode, ObserverModifierNode {

    override val depth: Int get() = 0
    override val shouldAutoInvalidate: Boolean get() = false

    private var scene: GlassSceneState? = null
    private var style = GlassStyle()
    private var layoutDirection = LayoutDirection.Ltr
    private var coordinates: LayoutCoordinates? = null

    fun update(edge: GlassEdge, blur: Dp) {
        this.edge = edge
        this.blur = blur
        invalidateDraw()
        scene?.invalidate()
    }

    override fun onAttach() {
        readLocals()
        scene?.register(this)
    }

    override fun onDetach() {
        scene?.unregister(this)
        coordinates = null
    }

    override fun onObservedReadsChanged() {
        val previous = scene
        readLocals()
        if (previous !== scene) {
            previous?.unregister(this)
            scene?.register(this)
        }
        invalidateDraw()
        scene?.invalidate()
    }

    private fun readLocals() = observeReads {
        scene = currentValueOf(LocalGlassScene)
        style = currentValueOf(LocalGlassStyle)
        layoutDirection = currentValueOf(LocalLayoutDirection)
    }

    override fun onGloballyPositioned(coordinates: LayoutCoordinates) {
        this.coordinates = coordinates
        scene?.invalidate()
    }

    /** Unit direction from the sharp side toward the blurred edge. */
    private fun toward(): Offset {
        val rtl = layoutDirection == LayoutDirection.Rtl
        return when (edge) {
            GlassEdge.Top -> Offset(0f, -1f)
            GlassEdge.Bottom -> Offset(0f, 1f)
            GlassEdge.Start -> Offset(if (rtl) 1f else -1f, 0f)
            GlassEdge.End -> Offset(if (rtl) -1f else 1f, 0f)
        }
    }

    override fun frame(content: LayoutCoordinates, density: Float): BlurFrame? {
        val coordinates = coordinates?.takeIf { it.isAttached } ?: return null
        if (scene?.hasContent != true || !content.isAttached) return null
        val band = content.localBoundingBoxOf(coordinates, clipBounds = false)
        if (band.isEmpty) return null
        val toward = toward()
        return BlurFrame(band, toward.x, toward.y, blur.value * density, style.isDark(null))
    }

    override fun ContentDrawScope.draw() {
        drawContent()
        if (scene?.hasContent == true) return
        // No content layer to blur: fade toward the edge so the band still reads.
        val toward = toward()
        val half = Offset(size.width / 2f * toward.x, size.height / 2f * toward.y)
        val center = size.toRect().center
        val edgeColor = if (style.isDark(null)) Color.Black.copy(alpha = 0.45f) else Color.White.copy(alpha = 0.6f)
        drawRect(Brush.linearGradient(listOf(Color.Transparent, edgeColor), start = center - half, end = center + half))
    }
}
