package io.github.rookasrudzianskas.liquidglass.studio

import android.content.Intent
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassMaterial

/**
 * How the Studio opens. Defaults show the Hero page following the system;
 * intent extras open a given state directly, which keeps screenshots
 * reproducible, for example:
 * `adb shell am start -n io.github.rookasrudzianskas.liquidglass.studio/.MainActivity --es page lab --es appearance dark`
 * (also `--es wallpaper`, `--es material`, `--ez reduceTransparency true`, `--ez increaseContrast true`).
 */
data class StudioLaunch(
    val page: StudioPage = StudioPage.Hero,
    val appearance: GlassAppearance = GlassAppearance.Automatic,
    val wallpaper: Wallpaper? = null,
    val labMaterial: GlassMaterial = GlassMaterial.Clear,
    val reduceTransparency: Boolean = false,
    val increaseContrast: Boolean = false,
) {
    companion object {
        fun from(intent: Intent?): StudioLaunch {
            if (intent == null) return StudioLaunch()
            fun <T : Enum<T>> pick(key: String, values: Array<T>): T? =
                intent.getStringExtra(key)?.let { name -> values.firstOrNull { it.name.equals(name, ignoreCase = true) } }
            return StudioLaunch(
                page = pick("page", StudioPage.entries.toTypedArray()) ?: StudioPage.Hero,
                appearance = pick("appearance", GlassAppearance.entries.toTypedArray()) ?: GlassAppearance.Automatic,
                wallpaper = pick("wallpaper", Wallpaper.entries.toTypedArray()),
                labMaterial = pick("material", GlassMaterial.entries.toTypedArray()) ?: GlassMaterial.Clear,
                reduceTransparency = intent.getBooleanExtra("reduceTransparency", false),
                increaseContrast = intent.getBooleanExtra("increaseContrast", false),
            )
        }
    }
}
