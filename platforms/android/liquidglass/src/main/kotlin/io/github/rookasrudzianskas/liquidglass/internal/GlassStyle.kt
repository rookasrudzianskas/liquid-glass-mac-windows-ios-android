package io.github.rookasrudzianskas.liquidglass.internal

import android.app.UiModeManager
import android.content.Context
import android.database.ContentObserver
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import androidx.annotation.RequiresApi
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.platform.LocalContext
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassMotion
import io.github.rookasrudzianskas.liquidglass.optics.DEFAULT_REFRACTION

/**
 * Glass defaults in effect at a point of the composition: set by the nearest
 * [io.github.rookasrudzianskas.liquidglass.GlassScene] and read by every surface.
 */
@Immutable
internal data class GlassStyle(
    val material: GlassMaterial = GlassMaterial.Clear,
    val appearance: GlassAppearance = GlassAppearance.Automatic,
    /** The system dark theme, used when [appearance] is automatic. */
    val systemDark: Boolean = false,
    /** Refraction in dp. */
    val refraction: Float = DEFAULT_REFRACTION,
    val motion: GlassMotion = GlassMotion.Full,
    /** The system asked for less animation (animator duration scale 0). */
    val systemReduceMotion: Boolean = false,
    val reduceTransparency: Boolean = false,
    val increaseContrast: Boolean = false,
) {
    fun isDark(override: GlassAppearance?): Boolean = when (override ?: appearance) {
        GlassAppearance.Automatic -> systemDark
        GlassAppearance.Light -> false
        GlassAppearance.Dark -> true
    }

    fun motion(override: GlassMotion?): GlassMotion {
        val requested = override ?: motion
        return if (systemReduceMotion && requested == GlassMotion.Full) GlassMotion.Reduced else requested
    }

    /** The material actually drawn: Increase Contrast always uses Regular. */
    fun material(override: GlassMaterial?): GlassMaterial =
        if (increaseContrast) GlassMaterial.Regular else override ?: material
}

internal val LocalGlassStyle = staticCompositionLocalOf { GlassStyle() }

/** True while the system "Remove animations" setting is on (animator duration scale 0). */
@Composable
internal fun rememberSystemReduceMotion(): Boolean {
    val context = LocalContext.current
    val resolver = context.contentResolver
    var reduced by remember { mutableStateOf(readAnimatorScale(context) == 0f) }
    DisposableEffect(resolver) {
        val observer = object : ContentObserver(Handler(Looper.getMainLooper())) {
            override fun onChange(selfChange: Boolean) {
                reduced = readAnimatorScale(context) == 0f
            }
        }
        resolver.registerContentObserver(Settings.Global.getUriFor(Settings.Global.ANIMATOR_DURATION_SCALE), false, observer)
        reduced = readAnimatorScale(context) == 0f
        onDispose { resolver.unregisterContentObserver(observer) }
    }
    return reduced
}

private fun readAnimatorScale(context: Context): Float =
    Settings.Global.getFloat(context.contentResolver, Settings.Global.ANIMATOR_DURATION_SCALE, 1f)

/**
 * True when the user asked for more contrast: Android 14's contrast level at
 * medium or above, or "High contrast text".
 */
@Composable
internal fun rememberSystemIncreaseContrast(): Boolean {
    val context = LocalContext.current
    val resolver = context.contentResolver
    var increased by remember { mutableStateOf(readIncreaseContrast(context)) }
    DisposableEffect(resolver) {
        val update = { increased = readIncreaseContrast(context) }
        val observer = object : ContentObserver(Handler(Looper.getMainLooper())) {
            override fun onChange(selfChange: Boolean) = update()
        }
        resolver.registerContentObserver(Settings.Secure.getUriFor(HIGH_TEXT_CONTRAST), false, observer)
        val contrast = if (Build.VERSION.SDK_INT >= 34) ContrastListener.register(context, update) else null
        update()
        onDispose {
            resolver.unregisterContentObserver(observer)
            contrast?.invoke()
        }
    }
    return increased
}

/** Settings.Secure.ACCESSIBILITY_HIGH_TEXT_CONTRAST_ENABLED, readable but not public API. */
private const val HIGH_TEXT_CONTRAST = "high_text_contrast_enabled"

private fun readIncreaseContrast(context: Context): Boolean {
    val highText = Settings.Secure.getInt(context.contentResolver, HIGH_TEXT_CONTRAST, 0) == 1
    val level = if (Build.VERSION.SDK_INT >= 34) ContrastListener.level(context) else 0f
    return highText || level >= 0.5f
}

@RequiresApi(34)
private object ContrastListener {
    fun level(context: Context): Float = context.getSystemService(UiModeManager::class.java)?.contrast ?: 0f

    /** Registers [onChange] for contrast changes and returns the unregistration. */
    fun register(context: Context, onChange: () -> Unit): () -> Unit {
        val manager = context.getSystemService(UiModeManager::class.java) ?: return {}
        val listener = UiModeManager.ContrastChangeListener { onChange() }
        manager.addContrastChangeListener(context.mainExecutor, listener)
        return { manager.removeContrastChangeListener(listener) }
    }
}
