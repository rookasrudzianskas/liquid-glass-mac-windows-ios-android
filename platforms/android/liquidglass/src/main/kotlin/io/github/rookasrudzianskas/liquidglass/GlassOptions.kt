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
import io.github.rookasrudzianskas.liquidglass.optics.defaultRefraction

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
    /**
     * The full optical travel at the rim (60 dp): what a surface that sets no
     * refraction uses once its shorter side is 80 dp or more. See [refraction].
     */
    public val Refraction: Dp = DEFAULT_REFRACTION.dp

    /**
     * The refraction a surface of this resting size uses when neither it nor its
     * scene sets one: `min(60 dp, 0.75 × the shorter side)` (spec/OPTICS.md §2).
     * A 34 dp button gets 25.5 dp, a 52 dp toolbar 39 dp, a 62 dp tab bar 46.5 dp;
     * anything 80 dp or more on both sides gets the full [Refraction]. Surfaces
     * measure it on their layout size, so a press or stretch does not change it.
     */
    public fun refraction(width: Dp, height: Dp): Dp = defaultRefraction(width.value, height.value).dp

    /** Corner radius of a plain [GlassSurface]. */
    public val SurfaceRadius: Dp = 16.dp

    /** Corner radius of a [GlassMenu] panel. */
    public val MenuRadius: Dp = 28.dp

    /** Text and icons on light glass. */
    public val LightContentColor: Color = Color(0xFF111113)

    /** Text and icons on dark glass. */
    public val DarkContentColor: Color = Color(0xFFFFFFFF)

    /** Maximum number of surfaces one scene refracts on the GPU at once; more fall back to the fallback material. */
    public const val MaxSurfacesPerScene: Int = 16

    /** Default depth of the [GlassProgressiveBlur] band from its edge (80 dp): its height at the top or bottom, its width at the start or end. */
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
