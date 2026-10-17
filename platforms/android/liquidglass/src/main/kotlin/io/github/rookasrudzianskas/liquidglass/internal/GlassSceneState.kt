package io.github.rookasrudzianskas.liquidglass.internal

import androidx.compose.runtime.Composable
import androidx.compose.runtime.Stable
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.LayoutCoordinates
import androidx.compose.ui.layout.findRootCoordinates

/**
 * One glass surface as the scene draws it this frame. Rectangles are in pixels
 * of the content layer; optical sizes are in dp.
 */
internal class SurfaceFrame(
    /** The visual outline box, after press scale, drag and morph. */
    val bounds: Rect,
    /** Pixels outside this rectangle are not drawn (clipping ancestors). */
    val clip: Rect,
    /** Corner radius in dp, or null for a capsule. */
    val radius: Float?,
    /** One of [ShaderMaterial]. */
    val material: Float,
    val dark: Boolean,
    val nested: Boolean,
    val opacity: Float,
    val contrast: Boolean,
    /** [Color.Unspecified] for untinted glass. */
    val tint: Color,
    /** Optical travel at the rim, dp. */
    val refraction: Float,
    /** Glow center relative to the surface center, dp. */
    val glowX: Float,
    val glowY: Float,
    /** Glow radius, dp. */
    val glowRadius: Float,
    /** Glow spring value, 0..1 (0.35 while hovered). */
    val glowAmount: Float,
) {
    val regular: Boolean get() = material == ShaderMaterial.REGULAR
}

/** A region of the content that [GlassContentNode] blurs progressively toward one edge. */
internal class BlurFrame(
    /** The band, in content-layer pixels. */
    val band: Rect,
    /** Unit vector from the sharp side toward the blurred edge. */
    val towardX: Float,
    val towardY: Float,
    /** Strongest Gaussian σ at the edge, pixels. */
    val blurPx: Float,
    val dark: Boolean,
)

/** Anything registered with a scene that its content layer renders. */
internal interface SceneParticipant {
    /** Told when the scene gains or loses its content layer (participants draw their own fallback without one). */
    fun onSceneRenderingChanged()
}

/** A surface that the content layer renders. */
internal interface SceneSurface : SceneParticipant {
    /** Nesting depth among glass surfaces; deeper surfaces draw on top. */
    val depth: Int

    /** Recomputes [depth] and nesting from the surface's current ancestors. */
    fun refreshNesting()

    /** Geometry and optics this frame, or null when the scene does not draw it. */
    fun frame(content: LayoutCoordinates, density: Float): SurfaceFrame?
}

/** A progressive blur region that the content layer renders. */
internal interface SceneBlur : SceneParticipant {
    fun frame(content: LayoutCoordinates, density: Float): BlurFrame?
}

/** Content composed in the scene's overlay layer, above everything else (menus). */
internal class GlassOverlay(val key: Any, val content: @Composable () -> Unit)

/**
 * The registry behind a [io.github.rookasrudzianskas.liquidglass.GlassScene]:
 * the content layer, the surfaces and blur regions it renders, and overlays.
 * All access happens on the main thread.
 */
@Stable
internal class GlassSceneState(
    /** The scene this one is nested in (its content, or a control's private scene), if any. */
    private val parent: GlassSceneState? = null,
) {
    /** The outermost scene: menus open there so they draw above all nested glass. */
    val root: GlassSceneState get() = parent?.root ?: this

    private val surfaceList = mutableListOf<SceneSurface>()
    private val blurList = mutableListOf<SceneBlur>()
    private var order = 0L
    private val orderOf = HashMap<SceneSurface, Long>()

    /** [surfaceList] in drawing order, reused every frame. */
    private val sorted = ArrayList<SceneSurface>()
    private val drawingOrder: Comparator<SceneSurface> = compareBy({ it.depth }, { orderOf[it] ?: 0L })

    /**
     * Set when glass is added or removed anywhere above a surface of this scene:
     * nesting depth depends on ancestors, which can change without the surface
     * itself being reattached (glass added to a parent later, for example).
     */
    private var nestingDirty = false

    /** The attached content node, if any. */
    var content: GlassContentNode? = null
        private set

    /** Menus and other content drawn above the whole scene. */
    val overlays = mutableStateListOf<GlassOverlay>()

    /** Whether a content layer exists to render glass. */
    val hasContent: Boolean get() = content != null

    /**
     * Whether the content layer is in the same window as [coordinates]. Glass in
     * another window (a dialog or popup inside the scene) cannot refract it.
     */
    fun renders(coordinates: LayoutCoordinates): Boolean {
        val content = content?.takeIf { it.isAttached } ?: return false
        return content.rootCoordinates() === coordinates.findRootCoordinates()
    }

    fun attachContent(node: GlassContentNode) {
        content = node
        notifyRendering()
    }

    fun detachContent(node: GlassContentNode) {
        if (content !== node) return
        content = null
        notifyRendering()
    }

    private fun notifyRendering() {
        surfaceList.forEach { it.onSceneRenderingChanged() }
        blurList.forEach { it.onSceneRenderingChanged() }
    }

    fun register(surface: SceneSurface) {
        orderOf[surface] = order++
        surfaceList += surface
        markNestingDirty()
    }

    fun unregister(surface: SceneSurface) {
        orderOf.remove(surface)
        surfaceList -= surface
        markNestingDirty()
    }

    /** Glass was added or removed above some surface of this scene: re-resolve nesting before the next frame. */
    fun markNestingDirty() {
        nestingDirty = true
        invalidate()
    }

    fun register(blur: SceneBlur) {
        blurList += blur
        invalidate()
    }

    fun unregister(blur: SceneBlur) {
        blurList -= blur
        invalidate()
    }

    /**
     * Surfaces bottom to top: shallower first, then in registration order. The
     * list is reused; it stays valid until the next call.
     */
    fun surfaces(): List<SceneSurface> {
        if (nestingDirty) {
            // Resolved lazily, when the frame is drawn: during a detach the departing glass is still an ancestor.
            nestingDirty = false
            surfaceList.forEach { it.refreshNesting() }
        }
        sorted.clear()
        sorted.addAll(surfaceList)
        sorted.sortWith(drawingOrder)
        return sorted
    }

    fun blurs(): List<SceneBlur> = blurList

    /** Asks the content layer to redraw (geometry changed without a snapshot write). */
    fun invalidate() {
        content?.invalidateGlass()
    }
}

internal val LocalGlassScene = staticCompositionLocalOf<GlassSceneState?> { null }
