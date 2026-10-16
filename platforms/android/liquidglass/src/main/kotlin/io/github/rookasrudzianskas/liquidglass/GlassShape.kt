package io.github.rookasrudzianskas.liquidglass

import androidx.compose.runtime.Immutable
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Outline
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.optics.shapeOutline

/**
 * The Liquid Glass outline: a rectangle with continuous (superellipse) corners,
 * as defined in spec/OPTICS.md §1. Clips, borders and hit shapes built from it
 * match the glass exactly.
 *
 * A numeric [radius] gives continuous corners whose diagonal inset equals a
 * circular corner of that radius; large radii blend smoothly into a capsule, so
 * animating the radius never jumps. [Capsule] and [Circle] are fully rounded.
 */
@Immutable
public class GlassShape private constructor(
    /** Corner radius, or [Dp.Unspecified] for a fully rounded capsule. */
    public val radius: Dp,
) : Shape {

    /** True for [Capsule] and [Circle]: the ends are fully rounded whatever the size. */
    public val isCapsule: Boolean get() = radius == Dp.Unspecified

    /** The corner radius in dp for the shader, or null for a capsule. */
    internal val radiusDp: Float? get() = if (isCapsule) null else radius.value

    override fun createOutline(size: Size, layoutDirection: LayoutDirection, density: Density): Outline {
        val radiusPx = radiusDp?.let { with(density) { it.dp.toPx() } }
        return Outline.Generic(glassPath(size.width, size.height, radiusPx))
    }

    override fun equals(other: Any?): Boolean = other is GlassShape && other.radius == radius

    override fun hashCode(): Int = radius.hashCode()

    override fun toString(): String = if (isCapsule) "GlassShape.Capsule" else "GlassShape(radius=$radius)"

    /** The capsule and circle shapes, and the factory for continuous corners. */
    public companion object {
        /** Fully rounded ends: the default for buttons, toolbars, tab bars and switches. */
        public val Capsule: GlassShape = GlassShape(Dp.Unspecified)

        /** A circle when the surface is square (icon-only buttons); a capsule otherwise. */
        public val Circle: GlassShape = Capsule

        /** Continuous corners of the given [radius] (nonnegative). */
        public operator fun invoke(radius: Dp): GlassShape {
            require(radius.value.isFinite() && radius >= 0.dp) { "radius must be finite and nonnegative, was $radius" }
            return GlassShape(radius)
        }
    }
}

/** Builds the continuous-corner outline of a `width × height` box (pixels), y down. */
internal fun glassPath(width: Float, height: Float, radiusPx: Float?, segments: Int = 24): Path {
    val path = Path()
    if (width <= 0f || height <= 0f) return path
    val points = shapeOutline(width, height, radiusPx, segments)
    path.moveTo(points[0], points[1])
    var i = 2
    while (i < points.size) {
        path.lineTo(points[i], points[i + 1])
        i += 2
    }
    path.close()
    return path
}
