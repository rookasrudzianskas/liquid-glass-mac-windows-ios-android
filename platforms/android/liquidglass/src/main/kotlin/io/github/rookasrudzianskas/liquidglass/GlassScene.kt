package io.github.rookasrudzianskas.liquidglass

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.ProvidableCompositionLocal
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.key
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.unit.Dp
import io.github.rookasrudzianskas.liquidglass.internal.GlassContentElement
import io.github.rookasrudzianskas.liquidglass.internal.GlassSceneState
import io.github.rookasrudzianskas.liquidglass.internal.GlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassOverlayLevel
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassScene
import io.github.rookasrudzianskas.liquidglass.internal.LocalGlassStyle
import io.github.rookasrudzianskas.liquidglass.internal.OVERLAY_LEVEL
import io.github.rookasrudzianskas.liquidglass.internal.rememberSystemReduceMotion

/**
 * Foreground color for text and icons on glass: `#111113` on light glass and
 * white on dark glass (spec/OPTICS.md §8). Every glass component provides it
 * for its content; read it for your own labels.
 */
public val LocalGlassContentColor: ProvidableCompositionLocal<Color> =
    compositionLocalOf { GlassDefaults.LightContentColor }

/**
 * The region glass may refract.
 *
 * Put the backdrop in a [GlassContent] (photos, gradients, any Compose UI
 * including scrolling lists) and glass surfaces anywhere else inside the scene:
 * at any depth, moving, scaling or animating, each refracts the content at its
 * exact position. Surface children (labels, icons) draw above the glass and are
 * not refracted.
 *
 * ```
 * GlassScene {
 *     GlassContent { Image(painterResource(R.drawable.wallpaper), null, Modifier.fillMaxSize()) }
 *     GlassButton(onClick = {}, Modifier.align(Alignment.Center)) { GlassText("Hello") }
 * }
 * ```
 *
 * Glass is drawn on the GPU with AGSL on Android 13 and newer. On older
 * versions surfaces draw the fallback material: the platform blur (Android 12)
 * or a translucent fill, plus the rim light.
 *
 * The options below are inherited by every surface in the scene; each
 * component can override them.
 *
 * @param modifier modifier for the scene's box.
 * @param material default material of surfaces in the scene.
 * @param appearance light or dark glass; [GlassAppearance.Automatic] follows `isSystemInDarkTheme()`.
 * @param refraction default optical travel at the rim of surfaces.
 * @param motion how much glass animates; the system "Remove animations" setting always reduces it.
 * @param reduceTransparency replaces the material with an opaque fill (`#F2F2F7` / `#1C1C1E` at 94%), keeping the rim light.
 * @param increaseContrast adds a 1 dp inner border and uses the Regular material. Follows the system contrast setting by default.
 * @param content the scene: one [GlassContent] plus any glass in a [BoxScope].
 */
@Composable
public fun GlassScene(
    modifier: Modifier = Modifier,
    material: GlassMaterial = GlassMaterial.Clear,
    appearance: GlassAppearance = GlassAppearance.Automatic,
    refraction: Dp = GlassDefaults.Refraction,
    motion: GlassMotion = GlassMotion.Full,
    reduceTransparency: Boolean = false,
    increaseContrast: Boolean = GlassDefaults.systemIncreaseContrast(),
    content: @Composable BoxScope.() -> Unit,
) {
    val style = GlassStyle(
        material = material,
        appearance = appearance,
        systemDark = isSystemInDarkTheme(),
        refraction = refraction.value,
        motion = motion,
        systemReduceMotion = rememberSystemReduceMotion(),
        reduceTransparency = reduceTransparency,
        increaseContrast = increaseContrast,
    )
    val parent = LocalGlassScene.current
    val scene = remember(parent) { GlassSceneState(parent) }
    CompositionLocalProvider(
        LocalGlassStyle provides style,
        LocalGlassScene provides scene,
        LocalGlassContentColor provides GlassDefaults.contentColor(style.isDark(null)),
    ) {
        Box(modifier) {
            content()
            GlassOverlayHost(scene)
        }
    }
}

/**
 * The backdrop layer of the enclosing [GlassScene]: everything glass in the
 * scene refracts. Any Compose UI works, including `LazyColumn` and other
 * scrolling content, video or animation; the glass follows it every frame.
 *
 * Use one per scene, below the glass (first in the scene's content). Glass
 * placed inside the content itself cannot refract the layer it belongs to and
 * draws the fallback material instead. Outside a scene this is a plain [Box].
 *
 * @param modifier modifier for the content box; usually `Modifier.fillMaxSize()`.
 * @param content the backdrop.
 */
@Composable
public fun GlassContent(modifier: Modifier = Modifier, content: @Composable BoxScope.() -> Unit) {
    val scene = LocalGlassScene.current
    if (scene == null) {
        Box(modifier, content = content)
        return
    }
    Box(modifier.graphicsLayer().then(GlassContentElement(scene)), content = content)
}

/** Draws scene overlays (open menus) above everything else in the scene. */
@Composable
private fun BoxScope.GlassOverlayHost(scene: GlassSceneState) {
    if (scene.overlays.isEmpty()) return
    CompositionLocalProvider(LocalGlassOverlayLevel provides OVERLAY_LEVEL) {
        Box(Modifier.matchParentSize()) {
            for (overlay in scene.overlays) key(overlay.key) { overlay.content() }
        }
    }
}

/**
 * A scene private to one control (slider and switch thumbs): its own content
 * layer, with the style of the enclosing scene.
 */
@Composable
internal fun GlassLocalScene(modifier: Modifier, content: @Composable BoxScope.() -> Unit) {
    val parent = LocalGlassScene.current
    val scene = remember(parent) { GlassSceneState(parent) }
    CompositionLocalProvider(LocalGlassScene provides scene) {
        Box(modifier, content = content)
    }
}
