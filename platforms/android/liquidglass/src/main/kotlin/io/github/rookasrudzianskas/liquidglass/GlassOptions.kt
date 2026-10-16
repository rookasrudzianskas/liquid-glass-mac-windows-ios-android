package io.github.rookasrudzianskas.liquidglass

import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.internal.rememberSystemIncreaseContrast
import io.github.rookasrudzianskas.liquidglass.optics.DEFAULT_REFRACTION

/** The two Liquid Glass materials. */
public enum class GlassMaterial {
    /** Transparent glass: the backdrop stays sharp and is only toned and refracted. */
    Clear,

    /** Frosted glass: the backdrop is blurred, brightened (light) or deepened (dark), then refracted. */
    Regular,
}

/** Which tone curves, rim light and foreground colors the glass uses. */
public enum class GlassAppearance {
    /** Follows the system dark theme (`isSystemInDarkTheme()`). */
    Automatic,

    /** Light glass with dark foreground content. */
    Light,

    /** Dark glass with light foreground content. */
    Dark,
}

/**
 * How much glass animates. The system "Remove animations" setting
 * (animator duration scale 0) always lowers [Full] to [Reduced].
 */
public enum class GlassMotion {
    /** Press grow, drag stretch, gel overshoot and glow. */
    Full,

    /** No grow, stretch or overshoot: springs become critically damped crossfades. */
    Reduced,

    /** No transitions at all: every value jumps to its target. */
    None,
}

/** Defaults shared by every Liquid Glass component, taken from spec/OPTICS.md. */
public object GlassDefaults {
    /** Optical travel at the rim of a surface. */
    public val Refraction: Dp = DEFAULT_REFRACTION.dp

    /** Corner radius of a plain [GlassSurface]. */
    public val SurfaceRadius: Dp = 16.dp

    /** Corner radius of a [GlassMenu] panel. */
    public val MenuRadius: Dp = 28.dp

    /** Text and icons on light glass. */
    public val LightContentColor: Color = Color(0xFF111113)

    /** Text and icons on dark glass. */
    public val DarkContentColor: Color = Color(0xFFFFFFFF)

    /** Fill and track accent of value controls when no tint is given. */
    public val ControlAccent: Color = Color(0xFF8E8E93)

    /** Maximum number of surfaces one scene refracts on the GPU at once; more fall back to the fallback material. */
    public const val MaxSurfacesPerScene: Int = 16

    /** Default height of the [GlassProgressiveBlur] band. */
    public val ProgressiveBlurSize: Dp = 80.dp

    /** Default strongest blur (Gaussian σ at the edge) of [GlassProgressiveBlur]. */
    public val ProgressiveBlurRadius: Dp = 20.dp

    /** Labels on glass controls: 17 sp semibold. */
    public val LabelTextStyle: TextStyle = TextStyle(fontSize = 17.sp, fontWeight = FontWeight.SemiBold)

    /** Foreground color for content on glass of the given darkness. */
    public fun contentColor(dark: Boolean): Color = if (dark) DarkContentColor else LightContentColor

    /**
     * Whether the system asks for more contrast (Android 14's contrast level at
     * medium or above, or "High contrast text"); the default of
     * [GlassScene]'s `increaseContrast`. Updates when the setting changes.
     */
    @Composable
    public fun systemIncreaseContrast(): Boolean = rememberSystemIncreaseContrast()
}
