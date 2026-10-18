package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.Image
import androidx.compose.foundation.interaction.InteractionSource
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Stable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.graphics.isSpecified
import androidx.compose.ui.graphics.colorspace.ColorSpaces
import androidx.compose.ui.graphics.painter.Painter
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.rememberVectorPainter
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.internal.GlassSurfaceParams
import io.github.rookasrudzianskas.liquidglass.internal.currentGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.glassSurface
import io.github.rookasrudzianskas.liquidglass.optics.darkLabelOnTint

/**
 * Makes this element a Liquid Glass surface.
 *
 * Inside a [GlassScene] the glass refracts the scene's [GlassContent] at this
 * element's exact position every frame, then the element's own content (its
 * children) draws above it, unrefracted. Outside a scene, or below Android 13,
 * it draws the fallback material.
 *
 * Interactive glass grows under a press, stretches toward a dragging finger
 * (preserving volume) and blooms with light under the pointer, on the spec
 * springs. It only observes pointer input, so clickable and draggable
 * modifiers keep working; pass their [interactionSource] to drive the press
 * from them instead (for example so a scroll cancels it).
 *
 * Order it like a background, after the modifiers that size and place the
 * element: `Modifier.size(120.dp, 44.dp).liquidGlass(GlassShape.Capsule)`.
 *
 * Glass follows translation and scale of any ancestor; rotation is not supported.
 * Fade glass with [opacity] rather than an ancestor's alpha (the glass is drawn
 * by the scene, beneath the element).
 *
 * @param shape outline of the glass; continuous corners.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the glass (never the content); [Color.Unspecified] for none.
 * @param refraction optical travel at the rim, used as given; [Dp.Unspecified] takes the scene's, or when the
 *   scene sets none follows the resting size: `min(60 dp, 0.75 × the shorter side)` ([GlassDefaults.refraction]).
 * @param interactive press grow, drag stretch and glow.
 * @param motion overrides the scene's motion; system reduce motion still applies.
 * @param opacity multiplies the glass, for fading it in and out.
 * @param interactionSource when given, its press interactions drive the press instead of raw pointer input.
 */
@Stable
public fun Modifier.liquidGlass(
    shape: GlassShape = GlassShape(GlassDefaults.SurfaceRadius),
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = false,
    motion: GlassMotion? = null,
    opacity: Float = 1f,
    interactionSource: InteractionSource? = null,
): Modifier = glassSurface(
    GlassSurfaceParams(
        shape = shape,
        material = material,
        appearance = appearance,
        tint = tint,
        refraction = refraction,
        interactive = interactive,
        motion = motion,
        opacity = opacity,
        interactionSource = interactionSource,
    ),
)

/**
 * A glass container: a [Box] made of Liquid Glass (see [liquidGlass]) whose
 * content gets [LocalGlassContentColor] for its appearance.
 *
 * ```
 * GlassSurface(Modifier.padding(24.dp), material = GlassMaterial.Regular) {
 *     GlassText("Now Playing", Modifier.padding(20.dp))
 * }
 * ```
 *
 * @param modifier modifier for the surface; give it a size or let the content size it.
 * @param shape outline of the glass; 16 dp continuous corners by default.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the glass (never the content); [Color.Unspecified] for none.
 * @param refraction optical travel at the rim, used as given; [Dp.Unspecified] takes the scene's, or when the
 *   scene sets none follows the resting size: `min(60 dp, 0.75 × the shorter side)` ([GlassDefaults.refraction]).
 * @param interactive press grow, drag stretch and glow.
 * @param motion overrides the scene's motion.
 * @param contentAlignment alignment of the content inside the surface.
 * @param content content drawn above the glass.
 */
@Composable
public fun GlassSurface(
    modifier: Modifier = Modifier,
    shape: GlassShape = GlassShape(GlassDefaults.SurfaceRadius),
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = false,
    motion: GlassMotion? = null,
    contentAlignment: Alignment = Alignment.TopStart,
    content: @Composable BoxScope.() -> Unit,
) {
    ProvideGlassContentColor(appearance, tint, material) {
        Box(
            modifier = modifier.liquidGlass(shape, material, appearance, tint, refraction, interactive, motion),
            contentAlignment = contentAlignment,
            content = content,
        )
    }
}

/**
 * Text on glass, colored with [LocalGlassContentColor] and set in the glass
 * label style (17 sp semibold) by default. Font scaling applies.
 *
 * @param text the text.
 * @param modifier modifier for the text.
 * @param style text style; [color] overrides its color.
 * @param color text color; [Color.Unspecified] uses the style's, then [LocalGlassContentColor].
 * @param maxLines maximum number of lines.
 */
@Composable
public fun GlassText(
    text: String,
    modifier: Modifier = Modifier,
    style: TextStyle = GlassDefaults.LabelTextStyle,
    color: Color = Color.Unspecified,
    maxLines: Int = Int.MAX_VALUE,
) {
    val resolved = when {
        color.isSpecified -> color
        style.color.isSpecified -> style.color
        else -> LocalGlassContentColor.current
    }
    BasicText(
        text = text,
        modifier = modifier,
        style = style.copy(color = resolved),
        overflow = TextOverflow.Ellipsis,
        maxLines = maxLines,
    )
}

/**
 * A vector icon on glass, tinted with [LocalGlassContentColor].
 *
 * @param imageVector the icon.
 * @param contentDescription text for accessibility services; null when decorative.
 * @param modifier modifier for the icon.
 * @param tint icon color.
 * @param size icon size.
 */
@Composable
public fun GlassIcon(
    imageVector: ImageVector,
    contentDescription: String?,
    modifier: Modifier = Modifier,
    tint: Color = LocalGlassContentColor.current,
    size: Dp = 22.dp,
) {
    GlassIcon(rememberVectorPainter(imageVector), contentDescription, modifier, tint, size)
}

/**
 * An icon on glass from any [Painter], tinted with [LocalGlassContentColor].
 *
 * @param painter the icon.
 * @param contentDescription text for accessibility services; null when decorative.
 * @param modifier modifier for the icon.
 * @param tint icon color; [Color.Unspecified] draws the painter's own colors.
 * @param size icon size.
 */
@Composable
public fun GlassIcon(
    painter: Painter,
    contentDescription: String?,
    modifier: Modifier = Modifier,
    tint: Color = LocalGlassContentColor.current,
    size: Dp = 22.dp,
) {
    Image(
        painter = painter,
        contentDescription = contentDescription,
        modifier = modifier.size(size),
        contentScale = ContentScale.Fit,
        colorFilter = if (tint.isSpecified) ColorFilter.tint(tint) else null,
    )
}

/**
 * Provides the foreground color for glass of this appearance, material and tint: on a tint, white or `#111113`,
 * whichever has the higher contrast with the tinted glass ([darkLabelOnTint]), as on the other platforms.
 */
@Composable
internal fun ProvideGlassContentColor(
    appearance: GlassAppearance?,
    tint: Color,
    material: GlassMaterial?,
    content: @Composable () -> Unit,
) {
    val style = currentGlassStyle()
    val color = when {
        tint.isSpecified -> {
            val srgb = tint.convert(ColorSpaces.Srgb)
            val regular = style.material(material) == GlassMaterial.Regular
            if (darkLabelOnTint(srgb.red, srgb.green, srgb.blue, regular)) GlassDefaults.LightContentColor else GlassDefaults.DarkContentColor
        }
        else -> GlassDefaults.contentColor(style.isDark(appearance))
    }
    CompositionLocalProvider(LocalGlassContentColor provides color, content = content)
}
