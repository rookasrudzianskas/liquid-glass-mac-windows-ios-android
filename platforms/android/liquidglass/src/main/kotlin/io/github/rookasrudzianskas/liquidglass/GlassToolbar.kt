package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.layout.Box
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.PointerId
import androidx.compose.ui.input.pointer.changedToDownIgnoreConsumed
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.Placeable
import androidx.compose.ui.layout.SubcomposeLayout
import androidx.compose.ui.layout.layoutId
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.internal.GlassDynamics
import io.github.rookasrudzianskas.liquidglass.internal.GlassPointerHandle
import io.github.rookasrudzianskas.liquidglass.internal.GlassSurfaceParams
import io.github.rookasrudzianskas.liquidglass.internal.glassSurface

/**
 * A floating Liquid Glass toolbar: a 52 dp capsule holding buttons.
 *
 * [GlassButton]s inside become toolbar items on the toolbar's glass. A
 * [GlassToolbarSpacer] splits the items into clusters, each its own glass
 * capsule; flexible spacers share the toolbar's free width. With
 * [interactive] glass, the cluster under the finger grows and lights up.
 *
 * ```
 * GlassToolbar(Modifier.fillMaxWidth().padding(horizontal = 16.dp)) {
 *     GlassButton(Icons.Default.ArrowBack, "Back", onClick = {})
 *     GlassToolbarSpacer()
 *     GlassButton(Icons.Default.Share, "Share", onClick = {})
 *     GlassButton(Icons.Default.MoreVert, "More", onClick = {})
 * }
 * ```
 *
 * @param modifier modifier for the toolbar; `fillMaxWidth()` lets flexible spacers spread the clusters.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the glass of every cluster.
 * @param interactive press grow, drag stretch and glow of the clusters.
 * @param content toolbar items and spacers.
 */
@Composable
public fun GlassToolbar(
    modifier: Modifier = Modifier,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    interactive: Boolean = true,
    content: @Composable () -> Unit,
) {
    val state = remember { ToolbarState() }
    val params = GlassSurfaceParams(
        shape = GlassShape.Capsule,
        material = material,
        appearance = appearance,
        tint = tint,
        refraction = Dp.Unspecified,
        interactive = interactive,
        motion = null,
        opacity = 1f,
        interactionSource = null,
    )
    ProvideGlassContentColor(appearance, tint) {
        SubcomposeLayout(modifier.pointerInput(state) { forwardPointers(state) }) { constraints ->
            val inset = ToolbarInset.roundToPx()
            val items = subcompose(ToolbarSlot.Items) {
                CompositionLocalProvider(LocalGlassToolbarItem provides true, content = content)
            }
            val itemConstraints = Constraints(maxWidth = constraints.maxWidth, maxHeight = constraints.maxHeight)
            // Runs of items between spacers are clusters; adjacent spacers merge into one gap.
            val segments = mutableListOf<ToolbarSegment>()
            for (measurable in items) {
                val marker = measurable.layoutId as? ToolbarSpacerMarker
                val last = segments.lastOrNull()
                if (marker == null) {
                    val placeable = measurable.measure(itemConstraints)
                    if (last is ToolbarSegment.Cluster) last.items += placeable else segments += ToolbarSegment.Cluster(mutableListOf(placeable))
                } else if (last is ToolbarSegment.Gap) {
                    last.minWidth += marker.minWidth.roundToPx()
                    last.flexible = last.flexible || marker.flexible
                } else {
                    segments += ToolbarSegment.Gap(marker.minWidth.roundToPx(), marker.flexible)
                }
            }
            val clusters = segments.filterIsInstance<ToolbarSegment.Cluster>()
            val tallest = clusters.maxOfOrNull { cluster -> cluster.items.maxOf { it.height } } ?: 0
            val height = maxOf(ToolbarHeight.roundToPx(), tallest + 2 * inset)
            val natural = segments.sumOf { segment ->
                when (segment) {
                    is ToolbarSegment.Cluster -> segment.items.sumOf { it.width } + 2 * inset
                    is ToolbarSegment.Gap -> segment.minWidth
                }
            }
            val width = natural.coerceIn(constraints.minWidth, maxOf(constraints.minWidth, constraints.maxWidth))
            val free = (width - natural).coerceAtLeast(0)
            val flexible = segments.count { it is ToolbarSegment.Gap && it.flexible }

            val rects = ArrayList<Rect>(clusters.size)
            var x = if (flexible == 0) free / 2 else 0
            for (segment in segments) {
                when (segment) {
                    is ToolbarSegment.Cluster -> {
                        val clusterWidth = segment.items.sumOf { it.width } + 2 * inset
                        rects += Rect(x.toFloat(), 0f, (x + clusterWidth).toFloat(), height.toFloat())
                        x += clusterWidth
                    }
                    is ToolbarSegment.Gap -> x += segment.minWidth + if (segment.flexible) free / flexible else 0
                }
            }
            state.clusters = rects
            while (state.handles.size < clusters.size) {
                val handle = GlassPointerHandle()
                state.handles += handle
                state.dynamics += GlassDynamics(pointer = handle)
            }
            val glass = subcompose(ToolbarSlot.Glass) {
                for (i in clusters.indices) Box(Modifier.glassSurface(params, state.dynamics[i]))
            }.mapIndexed { i, measurable -> measurable.measure(Constraints.fixed(rects[i].width.toInt(), height)) }

            layout(width, height) {
                glass.forEachIndexed { i, placeable -> placeable.place(rects[i].left.toInt(), 0) }
                clusters.forEachIndexed { i, cluster ->
                    var itemX = rects[i].left.toInt() + inset
                    for (item in cluster.items) {
                        item.place(itemX, (height - item.height) / 2)
                        itemX += item.width
                    }
                }
            }
        }
    }
}

/**
 * Space between toolbar clusters. Flexible spacers share the toolbar's free
 * width; fixed ones stay at [minWidth]. Use only inside [GlassToolbar].
 *
 * @param minWidth the narrowest the gap gets.
 * @param flexible whether the gap grows to fill the toolbar.
 */
@Composable
public fun GlassToolbarSpacer(minWidth: Dp = 8.dp, flexible: Boolean = true) {
    Box(Modifier.layoutId(ToolbarSpacerMarker(minWidth, flexible)))
}

@Immutable
private data class ToolbarSpacerMarker(val minWidth: Dp, val flexible: Boolean)

private enum class ToolbarSlot { Items, Glass }

private sealed interface ToolbarSegment {
    class Cluster(val items: MutableList<Placeable>) : ToolbarSegment
    class Gap(var minWidth: Int, var flexible: Boolean) : ToolbarSegment
}

/** Cluster rectangles from the last layout and the handles that drive their glass. */
private class ToolbarState {
    var clusters: List<Rect> = emptyList()
    val handles = mutableListOf<GlassPointerHandle>()
    val dynamics = mutableListOf<GlassDynamics>()
}

/** Feeds touches to the cluster they land in, without consuming them (the items stay clickable). */
private suspend fun androidx.compose.ui.input.pointer.PointerInputScope.forwardPointers(state: ToolbarState) {
    awaitPointerEventScope {
        var tracked: PointerId? = null
        var cluster = -1
        while (true) {
            val event = awaitPointerEvent(PointerEventPass.Initial)
            if (tracked == null) {
                val down = event.changes.firstOrNull { it.changedToDownIgnoreConsumed() } ?: continue
                cluster = state.clusters.indexOfFirst { it.contains(down.position) }
                if (cluster < 0) continue
                tracked = down.id
                state.handles[cluster].down(down.position - state.clusters[cluster].topLeft)
                continue
            }
            val change = event.changes.firstOrNull { it.id == tracked }
            val handle = state.handles.getOrNull(cluster)
            val origin = state.clusters.getOrNull(cluster)?.topLeft ?: Offset.Zero
            if (change == null || !change.pressed) {
                handle?.up()
                tracked = null
                cluster = -1
            } else {
                handle?.move(change.position - origin)
            }
        }
    }
}

private val ToolbarHeight = 52.dp
private val ToolbarInset = 4.dp
