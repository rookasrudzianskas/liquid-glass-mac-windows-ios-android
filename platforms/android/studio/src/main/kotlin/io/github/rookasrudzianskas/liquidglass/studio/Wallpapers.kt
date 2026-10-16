package io.github.rookasrudzianskas.liquidglass.studio

import androidx.annotation.DrawableRes
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource

/** The shared wallpapers from assets/wallpapers (2048 px for backdrops, 1024 px for thumbnails). */
enum class Wallpaper(val title: String, @DrawableRes val full: Int, @DrawableRes val small: Int, val lightTop: Boolean) {
    Aurora("Aurora", R.drawable.wallpaper_aurora, R.drawable.wallpaper_aurora_small, lightTop = false),
    Dunes("Dunes", R.drawable.wallpaper_dunes, R.drawable.wallpaper_dunes_small, lightTop = true),
    Ocean("Ocean", R.drawable.wallpaper_ocean, R.drawable.wallpaper_ocean_small, lightTop = true),
    Midnight("Midnight", R.drawable.wallpaper_midnight, R.drawable.wallpaper_midnight_small, lightTop = false),
    ;

    companion object {
        /** Each page pairs a light wallpaper with light glass and a dark one with dark glass. */
        fun pairedWith(page: StudioPage, dark: Boolean): Wallpaper = when (page) {
            StudioPage.Hero -> if (dark) Aurora else Ocean
            StudioPage.Controls -> if (dark) Midnight else Dunes
            StudioPage.Lab, StudioPage.Blur -> if (dark) Aurora else Dunes
        }
    }
}

@Composable
fun WallpaperBackdrop(wallpaper: Wallpaper) {
    Image(
        painter = painterResource(wallpaper.full),
        contentDescription = null,
        modifier = Modifier.fillMaxSize(),
        contentScale = ContentScale.Crop,
    )
}
