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

/** Anything registered with a scene that the content layer renders. */
internal interface SceneParticipant {
    /** Nesting depth among glass surfaces; deeper participants draw on top. */
    val depth: Int
}

/** A surface that the content layer renders. */
internal interface SceneSurface : SceneParticipant {
    /** Geometry and optics this frame, or null when not visible. */
    fun frame(content: LayoutCoordinates, density: Float): SurfaceFrame?

    /** Told whether the scene draws this surface (otherwise it draws its own fallback). */
    fun onSceneRenderingChanged()
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
    private val orderOf = HashMap<SceneParticipant, Long>()

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
        surfaceList.forEach { it.onSceneRenderingChanged() }
    }

    fun detachContent(node: GlassContentNode) {
        if (content !== node) return
        content = null
        surfaceList.forEach { it.onSceneRenderingChanged() }
    }

    fun register(surface: SceneSurface) {
        orderOf[surface] = order++
        surfaceList += surface
        invalidate()
    }

    fun unregister(surface: SceneSurface) {
        orderOf.remove(surface)
        surfaceList -= surface
        invalidate()
    }

    fun register(blur: SceneBlur) {
        orderOf[blur] = order++
        blurList += blur
        invalidate()
    }

    fun unregister(blur: SceneBlur) {
        orderOf.remove(blur)
        blurList -= blur
        invalidate()
    }

    /** Surfaces bottom to top: shallower first, then in registration order. */
    fun surfaces(): List<SceneSurface> = surfaceList.sortedWith(compareBy({ it.depth }, { orderOf[it] ?: 0L }))

    fun blurs(): List<SceneBlur> = blurList

    /** Asks the content layer to redraw (geometry changed without a snapshot write). */
    fun invalidate() {
        content?.invalidateGlass()
    }
}

internal val LocalGlassScene = staticCompositionLocalOf<GlassSceneState?> { null }
