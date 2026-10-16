package io.github.rookasrudzianskas.liquidglass.studio

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Menu
import androidx.compose.material.icons.filled.Search
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassEdge
import io.github.rookasrudzianskas.liquidglass.GlassProgressiveBlur
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer

private class Entry(val title: String, val body: String, val wallpaper: Wallpaper)

private val Entries = listOf(
    Entry("Light that bends", "Glass draws the backdrop from deeper inside near its rim, the way a thick lens magnifies toward its edge.", Wallpaper.Aurora),
    Entry("Two materials", "Clear glass keeps the backdrop sharp and only tones it. Regular glass frosts it first, then refracts the frost.", Wallpaper.Dunes),
    Entry("A rim of light", "A specular line runs along the top and bottom of every outline, with a soft glow inside and a shade on the sides.", Wallpaper.Ocean),
    Entry("Elastic by nature", "Press a surface and it catches your finger; let go and it settles with a gel-like overshoot.", Wallpaper.Midnight),
    Entry("Continuous corners", "Superellipse corners meet the straight edges with continuous curvature and blend into capsules as they grow.", Wallpaper.Dunes),
    Entry("Edges that soften", "Six blur levels mixed by depth fade scrolling content into the toolbar and tab bar above it.", Wallpaper.Aurora),
    Entry("One optical model", "The same functions, constants and golden vectors drive SwiftUI, Compose, WPF and the web playground.", Wallpaper.Ocean),
    Entry("Built for touch", "Haptics, TalkBack roles, font scaling and reduce motion come with every control.", Wallpaper.Midnight),
)

/** The scrolling article list behind the progressive blur page: the backdrop the glass refracts. */
@Composable
fun JournalList(dark: Boolean) {
    val background = if (dark) Color.Black else Color(0xFFF2F2F7)
    val card = if (dark) Color(0xFF1C1C1E) else Color.White
    val text = if (dark) Color.White else Color(0xFF111113)
    val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val bottom = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()
    LazyColumn(
        Modifier.fillMaxSize().background(background),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = top + 76.dp, bottom = bottom + TabBarClearance + 16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        item { GlassText("Journal", Modifier.padding(start = 4.dp, bottom = 4.dp), style = StudioType.Title, color = text) }
        items(Entries + Entries) { entry ->
            Column(
                Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(26.dp))
                    .background(card),
            ) {
                Image(
                    painter = painterResource(entry.wallpaper.small),
                    contentDescription = null,
                    modifier = Modifier.fillMaxWidth().aspectRatio(16f / 9f),
                    contentScale = ContentScale.Crop,
                )
                Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    GlassText(entry.title, style = StudioType.Heading, color = text)
                    GlassText(entry.body, style = StudioType.Body, color = text.copy(alpha = 0.7f))
                }
            }
        }
    }
}

/** Progressive blur: the journal scrolls under blurred top and bottom edges and a floating glass toolbar. */
@Composable
fun BoxScope.BlurPage() {
    val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val bottom = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()
    GlassProgressiveBlur(GlassEdge.Top, Modifier.align(Alignment.TopCenter), size = top + 96.dp, blur = 18.dp)
    GlassProgressiveBlur(GlassEdge.Bottom, Modifier.align(Alignment.BottomCenter), size = bottom + 120.dp, blur = 18.dp)
    GlassToolbar(
        Modifier
            .align(Alignment.TopCenter)
            .statusBarsPadding()
            .padding(horizontal = 16.dp, vertical = 8.dp)
            .fillMaxWidth(),
    ) {
        GlassButton(Icons.Filled.Menu, "Sections", onClick = {})
        GlassToolbarSpacer()
        GlassButton(Icons.Filled.Search, "Search", onClick = {})
        GlassButton(Icons.Filled.Add, "New entry", onClick = {})
    }
}
