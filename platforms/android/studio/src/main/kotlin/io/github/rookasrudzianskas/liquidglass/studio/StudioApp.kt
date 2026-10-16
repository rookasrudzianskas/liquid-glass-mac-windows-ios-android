package io.github.rookasrudzianskas.liquidglass.studio

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Build
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.Star
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassTab
import io.github.rookasrudzianskas.liquidglass.GlassTabBar

/** The four pages, in tab order. */
enum class StudioPage(val title: String) { Hero("Hero"), Controls("Controls"), Lab("Lab"), Blur("Blur") }

/** Choices shared by every page: appearance and wallpaper (null = the page's own pairing). */
class StudioSettings(appearance: GlassAppearance, wallpaper: Wallpaper?) {
    var appearance by mutableStateOf(appearance)
    var wallpaper by mutableStateOf(wallpaper)
}

/**
 * Liquid Glass Studio. One [GlassScene] hosts every page: the page's backdrop
 * goes in [GlassContent], its glass above, and the tab bar refracts whichever
 * page is showing.
 */
@Composable
fun StudioApp(launch: StudioLaunch = StudioLaunch()) {
    var page by rememberSaveable { mutableIntStateOf(launch.page.ordinal) }
    val settings = remember { StudioSettings(launch.appearance, launch.wallpaper) }
    val systemDark = isSystemInDarkTheme()
    val dark = when (settings.appearance) {
        GlassAppearance.Automatic -> systemDark
        GlassAppearance.Light -> false
        GlassAppearance.Dark -> true
    }
    val current = StudioPage.entries[page]
    val wallpaper = settings.wallpaper ?: Wallpaper.pairedWith(current, dark)
    SystemBarIcons(lightContent = if (current == StudioPage.Blur) dark else !wallpaper.lightTop)

    GlassScene(Modifier.fillMaxSize(), appearance = settings.appearance) {
        GlassContent(Modifier.fillMaxSize()) {
            when (current) {
                StudioPage.Blur -> JournalList(dark)
                else -> WallpaperBackdrop(wallpaper)
            }
        }
        when (current) {
            StudioPage.Hero -> HeroPage(settings)
            StudioPage.Controls -> ControlsPage()
            StudioPage.Lab -> LabPage(launch)
            StudioPage.Blur -> BlurPage()
        }
        StudioTabBar(page) { page = it }
    }
}

@Composable
private fun BoxScope.StudioTabBar(page: Int, onPage: (Int) -> Unit) {
    val tabs = remember {
        listOf(
            GlassTab(StudioPage.Hero.title, Icons.Filled.Home),
            GlassTab(StudioPage.Controls.title, Icons.Filled.Build),
            GlassTab(StudioPage.Lab.title, Icons.Filled.Star),
            GlassTab(StudioPage.Blur.title, Icons.AutoMirrored.Filled.List),
        )
    }
    GlassTabBar(
        tabs = tabs,
        selectedIndex = page,
        onSelectedIndexChange = onPage,
        material = GlassMaterial.Regular,
        modifier = Modifier
            .align(Alignment.BottomCenter)
            .windowInsetsPadding(WindowInsets.navigationBars)
            .padding(start = 20.dp, end = 20.dp, bottom = 12.dp)
            .fillMaxWidth(),
    )
}

/** Keeps the status and navigation bar icons legible over the current backdrop. */
@Composable
private fun SystemBarIcons(lightContent: Boolean) {
    val view = LocalView.current
    if (view.isInEditMode) return
    SideEffect {
        val window = (view.context as? Activity)?.window ?: return@SideEffect
        val controller = WindowCompat.getInsetsController(window, view)
        controller.isAppearanceLightStatusBars = !lightContent
        controller.isAppearanceLightNavigationBars = !lightContent
    }
}
