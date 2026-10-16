package io.github.rookasrudzianskas.liquidglass.studio

import android.content.Intent
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassMenu
import io.github.rookasrudzianskas.liquidglass.GlassMenuDivider
import io.github.rookasrudzianskas.liquidglass.GlassMenuItem
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer
import io.github.rookasrudzianskas.liquidglass.LocalGlassContentColor
import kotlin.math.roundToInt

/** Hero: a vivid wallpaper under a draggable Regular glass card, a floating toolbar and the tab bar. */
@Composable
fun BoxScope.HeroPage(settings: StudioSettings) {
    StudioToolbar(settings, Modifier.align(Alignment.TopCenter))
    var offset by remember { mutableStateOf(Offset.Zero) }
    GlassSurface(
        modifier = Modifier
            .align(Alignment.Center)
            .offset { IntOffset(offset.x.roundToInt(), offset.y.roundToInt()) }
            .pointerInput(Unit) {
                detectDragGestures { change, drag ->
                    change.consume()
                    offset += drag
                }
            }
            .width(320.dp),
        shape = GlassShape(34.dp),
        material = GlassMaterial.Regular,
        interactive = true,
    ) {
        Column(Modifier.padding(26.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            GlassText("Liquid Glass", style = StudioType.Title)
            GlassText(
                "Refraction, frost and rim light, drawn live with AGSL over any Compose content.",
                style = StudioType.Body,
                color = LocalGlassContentColor.current.copy(alpha = 0.78f),
            )
            GlassText("Drag me across the waves", style = StudioType.Caption, color = LocalGlassContentColor.current.copy(alpha = 0.6f))
        }
    }
}

/** The floating toolbar: new, favorite, share, and the Studio menu (appearance and wallpaper). */
@Composable
fun StudioToolbar(settings: StudioSettings, modifier: Modifier = Modifier) {
    val context = LocalContext.current
    var favorite by rememberSaveable { mutableStateOf(false) }
    var menuOpen by remember { mutableStateOf(false) }
    GlassToolbar(
        modifier
            .statusBarsPadding()
            .padding(horizontal = 16.dp, vertical = 8.dp)
            .fillMaxWidth(),
    ) {
        GlassButton(Icons.Filled.Add, "New", onClick = {})
        GlassToolbarSpacer()
        GlassButton(if (favorite) Icons.Filled.Favorite else Icons.Filled.FavoriteBorder, "Favorite", onClick = { favorite = !favorite })
        GlassButton(Icons.Filled.Share, "Share", onClick = {
            val send = Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, PROJECT_URL)
            context.startActivity(Intent.createChooser(send, null))
        })
        GlassMenu(
            expanded = menuOpen,
            onDismissRequest = { menuOpen = false },
            trigger = { GlassButton(Icons.Filled.MoreVert, "Studio options", onClick = { menuOpen = true }) },
        ) {
            for ((label, appearance) in listOf("Match system" to GlassAppearance.Automatic, "Light" to GlassAppearance.Light, "Dark" to GlassAppearance.Dark)) {
                GlassMenuItem(label, onClick = { settings.appearance = appearance }, checked = settings.appearance == appearance)
            }
            GlassMenuDivider()
            GlassMenuItem("Paired wallpaper", onClick = { settings.wallpaper = null }, checked = settings.wallpaper == null)
            for (wallpaper in Wallpaper.entries) {
                GlassMenuItem(wallpaper.title, onClick = { settings.wallpaper = wallpaper }, checked = settings.wallpaper == wallpaper)
            }
        }
    }
}

const val PROJECT_URL = "https://liquid-glass-engineering.vercel.app"
