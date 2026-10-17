package io.github.rookasrudzianskas.liquidglass

import android.view.HapticFeedbackConstants
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.PressInteraction
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Height, padding, label and icon size of a [GlassButton]. Touch sizes from spec/COMPONENTS.md. */
@Immutable
public class GlassButtonSize private constructor(
    /** Minimum height; icon-only buttons are circles of this diameter. Grows with font scale. */
    public val height: Dp,
    /** Horizontal padding of labeled buttons. */
    public val horizontalPadding: Dp,
    /** Label style. */
    public val textStyle: TextStyle,
    /** Icon size. */
    public val iconSize: Dp,
) {
    /** The three touch sizes. */
    public companion object {
        /** 34 dp tall. */
        public val Small: GlassButtonSize = GlassButtonSize(34.dp, 14.dp, TextStyle(fontSize = 15.sp, fontWeight = FontWeight.SemiBold), 18.dp)

        /** 44 dp tall, the default. */
        public val Regular: GlassButtonSize = GlassButtonSize(44.dp, 18.dp, GlassDefaults.LabelTextStyle, 22.dp)

        /** 52 dp tall. */
        public val Large: GlassButtonSize = GlassButtonSize(52.dp, 22.dp, GlassDefaults.LabelTextStyle, 24.dp)
    }

    override fun toString(): String = "GlassButtonSize(height=$height)"
}

/**
 * A Liquid Glass button: an interactive capsule that grows under the finger,
 * stretches toward a drag and lights up where it is touched.
 *
 * Inside a [GlassToolbar] it becomes a toolbar item: no glass of its own, the
 * toolbar's cluster responds instead.
 *
 * ```
 * GlassButton(onClick = { play() }) {
 *     GlassIcon(Icons.Default.PlayArrow, contentDescription = null)
 *     GlassText("Play")
 * }
 * ```
 *
 * @param onClick called when the button is clicked.
 * @param modifier modifier for the button.
 * @param enabled whether the button responds; a disabled button, glass and label, is dimmed to 45%.
 * @param size height, padding and label style.
 * @param shape outline; a capsule by default.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the glass (a prominent button); the label turns white or dark to stay legible.
 * @param refraction optical travel at the rim, used as given; [Dp.Unspecified] takes the scene's, or when the
 *   scene sets none follows the resting size: `min(60 dp, 0.75 × the shorter side)` ([GlassDefaults.refraction]).
 * @param interactive whether the glass grows, stretches and lights up under the finger.
 * @param motion overrides the scene's motion.
 * @param interactionSource source of the button's interactions; the press drives the glass.
 * @param content the label, laid out in a row; use [GlassText] and [GlassIcon].
 */
@Composable
public fun GlassButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    size: GlassButtonSize = GlassButtonSize.Regular,
    shape: GlassShape = GlassShape.Capsule,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
    interactionSource: MutableInteractionSource? = null,
    content: @Composable RowScope.() -> Unit,
) {
    GlassButtonLayout(
        onClick = onClick,
        modifier = modifier,
        enabled = enabled,
        size = size,
        shape = shape,
        material = material,
        appearance = appearance,
        tint = tint,
        refraction = refraction,
        interactive = interactive,
        motion = motion,
        interactionSource = interactionSource,
        iconOnly = false,
        content = content,
    )
}

/**
 * A text [GlassButton], with an optional leading icon.
 *
 * @param text the label.
 * @param onClick called when the button is clicked.
 * @param modifier modifier for the button.
 * @param icon optional icon before the label.
 * @param enabled whether the button responds.
 * @param size height, padding and label style.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the glass.
 * @param interactionSource source of the button's interactions.
 */
@Composable
public fun GlassButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    icon: ImageVector? = null,
    enabled: Boolean = true,
    size: GlassButtonSize = GlassButtonSize.Regular,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    interactionSource: MutableInteractionSource? = null,
) {
    GlassButton(
        onClick = onClick,
        modifier = modifier,
        enabled = enabled,
        size = size,
        material = material,
        appearance = appearance,
        tint = tint,
        interactionSource = interactionSource,
    ) {
        if (icon != null) GlassIcon(icon, contentDescription = null, size = size.iconSize)
        GlassText(text, style = size.textStyle, maxLines = 1)
    }
}

/**
 * An icon-only [GlassButton]: a glass circle (44 dp by default).
 *
 * @param icon the icon.
 * @param contentDescription what the button does, read by accessibility services (an icon-only button needs one).
 * @param onClick called when the button is clicked.
 * @param modifier modifier for the button.
 * @param enabled whether the button responds.
 * @param size circle diameter and icon size.
 * @param material Clear or Regular; null inherits the scene's.
 * @param appearance light or dark; null inherits the scene's.
 * @param tint colors the glass.
 * @param interactionSource source of the button's interactions.
 */
@Composable
public fun GlassButton(
    icon: ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    size: GlassButtonSize = GlassButtonSize.Regular,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    interactionSource: MutableInteractionSource? = null,
) {
    GlassButtonLayout(
        onClick = onClick,
        modifier = modifier,
        enabled = enabled,
        size = size,
        shape = GlassShape.Circle,
        material = material,
        appearance = appearance,
        tint = tint,
        refraction = Dp.Unspecified,
        interactive = true,
        motion = null,
        interactionSource = interactionSource,
        iconOnly = true,
    ) {
        GlassIcon(icon, contentDescription, size = size.iconSize)
    }
}

/** Set by [GlassToolbar]: buttons inside are items on the toolbar's glass. */
internal val LocalGlassToolbarItem = staticCompositionLocalOf { false }

/** Height of items inside a toolbar: the 52 dp capsule minus its 4 dp inset. */
internal val ToolbarItemHeight = 44.dp

@Composable
private fun GlassButtonLayout(
    onClick: () -> Unit,
    modifier: Modifier,
    enabled: Boolean,
    size: GlassButtonSize,
    shape: GlassShape,
    material: GlassMaterial?,
    appearance: GlassAppearance?,
    tint: Color,
    refraction: Dp,
    interactive: Boolean,
    motion: GlassMotion?,
    interactionSource: MutableInteractionSource?,
    iconOnly: Boolean,
    content: @Composable RowScope.() -> Unit,
) {
    val source = interactionSource ?: remember { MutableInteractionSource() }
    val view = LocalView.current
    LaunchedEffect(source, view) {
        source.interactions.collect { if (it is PressInteraction.Press) view.performHapticFeedback(HapticFeedbackConstants.VIRTUAL_KEY) }
    }
    val toolbarItem = LocalGlassToolbarItem.current
    val height = if (toolbarItem) ToolbarItemHeight else size.height
    val focused by source.collectIsFocusedAsState()
    val glass = if (toolbarItem) {
        Modifier
    } else {
        Modifier.liquidGlass(
            shape = shape,
            material = material,
            appearance = appearance,
            tint = tint,
            refraction = refraction,
            interactive = enabled && interactive,
            motion = motion,
            opacity = if (enabled) 1f else DisabledAlpha,
            interactionSource = source,
        )
    }
    val focusRing = if (focused) Modifier.border(2.dp, FocusRingColor, shape) else Modifier
    val padding = if (iconOnly) PaddingValues(0.dp) else PaddingValues(horizontal = if (toolbarItem) 12.dp else size.horizontalPadding)
    val button = @Composable {
        CompositionLocalProvider(LocalGlassToolbarItem provides false) {
            Row(
                modifier = modifier
                    .heightIn(min = height)
                    .widthIn(min = height)
                    .then(glass)
                    .then(focusRing)
                    .clickable(
                        interactionSource = source,
                        indication = null,
                        enabled = enabled,
                        role = Role.Button,
                        onClick = onClick,
                    )
                    .padding(padding)
                    .alpha(if (enabled) 1f else DisabledAlpha),
                horizontalArrangement = Arrangement.spacedBy(6.dp, Alignment.CenterHorizontally),
                verticalAlignment = Alignment.CenterVertically,
                content = content,
            )
        }
    }
    // Toolbar items sit on the toolbar's glass and keep its foreground color.
    if (toolbarItem) button() else ProvideGlassContentColor(appearance, tint, button)
}

/** Keyboard focus ring around glass controls. */
internal val FocusRingColor = Color(0xFF0A84FF)

/** Opacity of a disabled control, glass and content alike (as on Apple platforms and Windows). */
internal const val DisabledAlpha = 0.45f
