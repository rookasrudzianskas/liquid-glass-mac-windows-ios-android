# Recipes: Liquid Glass for Jetpack Compose

Seven complete screens built with `io.github.rookasrudzianskas:liquid-glass-compose:1.0.1`. Each file compiles on its own; paste it into an app module whose namespace is `com.example.glass`, so `R` resolves without an import. Or change the `package` line to yours; if it differs from your module's namespace, add `import <your.namespace>.R`.

## Contents

1. [App shell: floating tab bar over content](#1-app-shell-floating-tab-bar-over-content)
2. [Toolbar over a photo](#2-toolbar-over-a-photo)
3. [Media card with a slider](#3-media-card-with-a-slider)
4. [Settings card with switches](#4-settings-card-with-switches)
5. [Glass menu from a button](#5-glass-menu-from-a-button)
6. [Progressive blur over a scrolling list](#6-progressive-blur-over-a-scrolling-list)
7. [Sheet over the scene](#7-sheet-over-the-scene)

## Setup shared by every recipe

```kotlin
// app/build.gradle.kts
dependencies {
    implementation("io.github.rookasrudzianskas:liquid-glass-compose:1.0.1")
    implementation(platform("androidx.compose:compose-bom:2024.12.01"))
    implementation("androidx.compose.material:material-icons-core") // Icons.Filled.* used below
    implementation("androidx.activity:activity-compose:1.9.3")      // setContent, enableEdgeToEdge
}
```

Host any recipe in an edge-to-edge activity so the backdrop runs under the system bars and the bars float over it:

```kotlin
package com.example.glass

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        setContent { AppShell() } // or PhotoViewer(), NowPlaying(), QuickSettings(), SortMenu(), Journal(), ShareSheet()
    }
}
```

The app module also needs the Compose compiler plugin (`id("org.jetbrains.kotlin.plugin.compose")` with Kotlin 2.0+) and `buildFeatures { compose = true }`. The library brings Compose UI and Foundation with it (as `api` dependencies); it uses activity-compose only at runtime, hence the explicit line above.

Recipes 2 to 5 and 7 show a picture as the backdrop: `R.drawable.photo`, `R.drawable.album_art` and `R.drawable.wallpaper` stand for any vivid image in `res/drawable-nodpi/`. Glass needs something with color and detail behind it; over a flat fill it reads as a gray pill.

Design defaults used throughout: Clear glass over imagery, Regular for panels holding text and bars over text, capsules for controls, circles for icon-only buttons, concentric radii (inner radius = outer radius − inset), one tinted control per screen at most, bars capped at 480 dp and cards at 420 dp wide for tablets and landscape.

## 1. App shell: floating tab bar over content

Two nested scenes: the inner one renders the page and the page's own glass (its toolbar), and the whole result is the outer scene's backdrop, so the tab bar refracts the page, toolbar included. The tab bar and the page's toolbar are Regular because they float over text.

```kotlin
package com.example.glass

import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Menu
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Search
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassTab
import io.github.rookasrudzianskas.liquidglass.GlassTabBar
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer

private val Tabs = listOf(GlassTab("Home", Icons.Filled.Home), GlassTab("Search", Icons.Filled.Search), GlassTab("Profile", Icons.Filled.Person))

@Composable
fun AppShell() {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    GlassScene(Modifier.fillMaxSize()) {
        GlassContent(Modifier.fillMaxSize()) {
            // The page is a scene of its own: its backdrop plus its glass.
            GlassScene(Modifier.fillMaxSize()) {
                GlassContent(Modifier.fillMaxSize()) { Page(Tabs[tab].label) }
                GlassToolbar(
                    Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth(),
                    material = GlassMaterial.Regular,                  // over a list of text and gaps
                ) {
                    GlassButton(Icons.Filled.Menu, "Menu", onClick = {})
                    GlassToolbarSpacer()
                    GlassButton(Icons.Filled.Add, "New", onClick = {})
                }
            }
        }
        GlassTabBar(
            tabs = Tabs,
            selectedIndex = tab,
            onSelectedIndexChange = { tab = it },
            modifier = Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(start = 20.dp, end = 20.dp, bottom = 12.dp).widthIn(max = 480.dp).fillMaxWidth(),
            material = GlassMaterial.Regular,
        )
    }
}

/** A scrolling page of colorful tiles, padded so it starts below the toolbar and ends above the tab bar. */
@Composable
private fun Page(title: String) {
    val dark = isSystemInDarkTheme()
    val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val bottom = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()
    LazyColumn(
        Modifier.fillMaxSize().background(if (dark) Color.Black else Color(0xFFF2F2F7)),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = top + 76.dp, bottom = bottom + 96.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        items(24) { i ->
            val hue = (i * 37f) % 360f
            val colors = listOf(Color.hsv(hue, 0.65f, 0.95f), Color.hsv((hue + 50f) % 360f, 0.8f, 0.65f))
            Box(Modifier.fillMaxWidth().height(120.dp).clip(GlassShape(24.dp)).background(Brush.linearGradient(colors)), contentAlignment = Alignment.BottomStart) {
                GlassText("$title ${i + 1}", Modifier.padding(16.dp), color = Color.White)
            }
        }
    }
}
```

Notes:
- Pages without glass of their own need only one scene: put the page straight into the outer `GlassContent`.
- A nested `GlassScene` does not inherit the outer scene's options. Pass every option you set (`material`, `appearance`, `refraction`, `motion`, `reduceTransparency`, `increaseContrast`) to both.
- `GlassText` inside the backdrop gets an explicit `color`: the scene provides the glass foreground color to everything in it, content included.

## 2. Toolbar over a photo

A photo viewer: Clear glass so the image stays vivid, dark appearance because the viewer is dark whatever the system theme. The toolbar splits into a leading cluster (Back) and a trailing cluster with a flexible spacer.

```kotlin
package com.example.glass

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer

@Composable
fun PhotoViewer(onBack: () -> Unit = {}) {
    var favorite by rememberSaveable { mutableStateOf(false) }
    GlassScene(Modifier.fillMaxSize(), appearance = GlassAppearance.Dark) {
        GlassContent(Modifier.fillMaxSize()) {
            Image(
                painter = painterResource(R.drawable.photo),
                contentDescription = "Dunes at sunset",
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Crop,
            )
        }
        GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth()) {
            GlassButton(Icons.AutoMirrored.Filled.ArrowBack, "Back", onClick = onBack)
            GlassToolbarSpacer()
            GlassButton(
                icon = if (favorite) Icons.Filled.Favorite else Icons.Filled.FavoriteBorder,
                contentDescription = if (favorite) "Remove from favorites" else "Add to favorites",
                onClick = { favorite = !favorite },
            )
            GlassButton(Icons.Filled.Share, "Share", onClick = {})
            GlassButton(Icons.Filled.Info, "Details", onClick = {})
        }
        Row(
            Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(16.dp).fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // A static caption chip: glass that does not react to touch.
            GlassSurface(shape = GlassShape.Capsule) {
                GlassText("Dunes · 18:42", Modifier.padding(horizontal = 16.dp, vertical = 11.dp), style = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.Medium))
            }
            GlassButton("Edit", onClick = {}, icon = Icons.Filled.Edit)
        }
    }
}
```

Notes:
- Inside `GlassToolbar`, `GlassButton`s are items on the toolbar's glass (one surface per cluster), not glass of their own: their `tint`, `material`, `appearance`, `shape` and `size` height are ignored. Put a tinted action beside the toolbar as a standalone `GlassButton`.
- Over a bright photo use `GlassAppearance.Light` (or leave `Automatic`); pick the appearance from the backdrop, not the label color.

## 3. Media card with a slider

A Regular card (it holds text) over album art, with a glass slider as the scrubber and a Regular, tinted glass Play button: glass controls inside a Regular card are the one allowed level of nesting. The secondary controls are plain icons. The pause icon is built with `materialIcon`, because `material-icons-core` has no Pause.

```kotlin
package com.example.glass

import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Share
import androidx.compose.material.icons.materialIcon
import androidx.compose.material.icons.materialPath
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassButtonSize
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassIcon
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSlider
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.LocalGlassContentColor

private const val TrackSeconds = 214
private val Accent = Color(0xFF0A84FF)
private val Caption = TextStyle(fontSize = 13.sp, fontWeight = FontWeight.Medium)

@Composable
fun NowPlaying() {
    var playing by rememberSaveable { mutableStateOf(false) }
    var position by rememberSaveable { mutableFloatStateOf(0.35f) }
    GlassScene(Modifier.fillMaxSize()) {
        GlassContent(Modifier.fillMaxSize()) {
            Image(painterResource(R.drawable.album_art), contentDescription = null, modifier = Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
        }
        GlassSurface(
            Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(16.dp).widthIn(max = 420.dp).fillMaxWidth(),
            shape = GlassShape(32.dp),
            material = GlassMaterial.Regular,
        ) {
            // Read the foreground inside the surface: it follows the surface's appearance.
            val secondary = LocalGlassContentColor.current.copy(alpha = 0.65f)
            Column(Modifier.padding(20.dp)) {
                GlassText("Midnight Drive", style = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold))
                GlassText("The Glass Hours", style = TextStyle(fontSize = 15.sp), color = secondary)
                GlassSlider(position, { position = it }, Modifier.padding(top = 6.dp), tint = Accent, label = "Playback position")
                Row(Modifier.fillMaxWidth()) {
                    GlassText(clock(position * TrackSeconds), Modifier.weight(1f), style = Caption, color = secondary)
                    GlassText("-" + clock((1f - position) * TrackSeconds), style = Caption, color = secondary)
                }
                Row(Modifier.fillMaxWidth().padding(top = 8.dp), horizontalArrangement = Arrangement.SpaceEvenly, verticalAlignment = Alignment.CenterVertically) {
                    PanelButton(Icons.Filled.FavoriteBorder, "Favorite") {}
                    // The one prominent action: Regular glass on the Regular card, so it shows the card's frost.
                    GlassButton(
                        icon = if (playing) Pause else Icons.Filled.PlayArrow,
                        contentDescription = if (playing) "Pause" else "Play",
                        onClick = { playing = !playing },
                        size = GlassButtonSize.Large,
                        material = GlassMaterial.Regular,
                        tint = Accent,
                    )
                    PanelButton(Icons.Filled.Share, "Share") {}
                }
            }
        }
    }
}

/** A plain icon control for a glass panel: no glass of its own, the icon in the glass foreground. */
@Composable
private fun PanelButton(icon: ImageVector, label: String, onClick: () -> Unit) {
    Box(
        Modifier.size(44.dp).clip(GlassShape.Circle).clickable(role = Role.Button, onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        GlassIcon(icon, contentDescription = label)
    }
}

private fun clock(seconds: Float): String = "%d:%02d".format(seconds.toInt() / 60, seconds.toInt() % 60)

private val Pause: ImageVector = materialIcon(name = "Filled.Pause") {
    materialPath {
        moveTo(6f, 19f); horizontalLineToRelative(4f); lineTo(10f, 5f); lineTo(6f, 5f); close()
        moveTo(14f, 5f); verticalLineToRelative(14f); horizontalLineToRelative(4f); lineTo(18f, 5f); close()
    }
}
```

Notes:
- `GlassSlider` and `GlassSwitch` carry their own private scene, so their glass thumbs work on a glass card (and anywhere else) and do not count toward the 16-surface budget.
- `tint` on a slider colors the filled track; leave it unspecified for the platform accent (Material You on Android 12+).
- `LocalGlassContentColor` is provided by each surface for its own appearance and tint. Read it inside the surface's content lambda; read outside, it holds the scene's color, which is wrong as soon as the card's appearance differs.
- Glass controls on a Regular card are allowed (one level of nesting). Give `GlassButton`s there `material = GlassMaterial.Regular`: they sample the same frost as the card. A Clear one would refract the album art directly and show it sharp through the frosted card. Plain icons or fills, as on the Studio's Lab page, suit secondary or dense controls.

## 4. Settings card with switches

A Regular panel (text-heavy) over a wallpaper. Rows are at least 52 dp tall; each switch carries its row's label for TalkBack, and the visible text is hidden from it so the label is read once.

```kotlin
package com.example.glass

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSlider
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassSwitch
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.LocalGlassContentColor

private val RowLabel = TextStyle(fontSize = 17.sp, fontWeight = FontWeight.Normal)
private val Green = Color(0xFF30D158)

@Composable
fun QuickSettings() {
    var wifi by rememberSaveable { mutableStateOf(true) }
    var bluetooth by rememberSaveable { mutableStateOf(false) }
    var focus by rememberSaveable { mutableStateOf(true) }
    var brightness by rememberSaveable { mutableFloatStateOf(0.7f) }
    GlassScene(Modifier.fillMaxSize()) {
        GlassContent(Modifier.fillMaxSize()) {
            Image(painterResource(R.drawable.wallpaper), contentDescription = null, modifier = Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
        }
        GlassSurface(
            Modifier.align(Alignment.Center).padding(20.dp).widthIn(max = 420.dp).fillMaxWidth(),
            shape = GlassShape(28.dp),
            material = GlassMaterial.Regular,
        ) {
            Column(Modifier.padding(horizontal = 20.dp, vertical = 16.dp)) {
                GlassText("Quick settings", Modifier.padding(bottom = 4.dp), style = TextStyle(fontSize = 22.sp, fontWeight = FontWeight.Bold))
                SwitchRow("Wi-Fi", wifi) { wifi = it }
                Hairline()
                SwitchRow("Bluetooth", bluetooth) { bluetooth = it }
                Hairline()
                SwitchRow("Focus", focus) { focus = it }
                Hairline()
                GlassText("Brightness", Modifier.padding(top = 12.dp).clearAndSetSemantics {}, style = RowLabel)
                GlassSlider(brightness, { brightness = it }, tint = Color(0xFFFF9F0A), label = "Brightness")
            }
        }
    }
}

@Composable
private fun SwitchRow(label: String, checked: Boolean, onCheckedChange: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth().heightIn(min = 52.dp), verticalAlignment = Alignment.CenterVertically) {
        GlassText(label, Modifier.weight(1f).clearAndSetSemantics {}, style = RowLabel)
        GlassSwitch(checked, onCheckedChange, tint = Green, label = label)
    }
}

/** A separator in the glass foreground color, like GlassMenuDivider. */
@Composable
private fun Hairline() {
    Box(Modifier.fillMaxWidth().height(1.dp).background(LocalGlassContentColor.current.copy(alpha = 0.12f)))
}
```

Notes:
- Long settings screens are reading surfaces: keep them on a solid background and use glass only for the floating bars above them. A compact card like this one over a wallpaper is the right size for glass.
- `GlassSwitch` occupies 54 × 44 dp; the extra height is the touch target, so rows need no extra padding.

## 5. Glass menu from a button

A Regular panel that grows out of its trigger and collapses back into it. `GlassMenu` takes no modifier for the trigger's position (its `modifier` styles the panel), so place it with a wrapper. Choosing an item runs its action, then calls `onDismissRequest`.

```kotlin
package com.example.glass

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassIcon
import io.github.rookasrudzianskas.liquidglass.GlassMenu
import io.github.rookasrudzianskas.liquidglass.GlassMenuDivider
import io.github.rookasrudzianskas.liquidglass.GlassMenuItem
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassText

private val Sorts = listOf("Name", "Date modified", "Size", "Kind")

@Composable
fun SortMenu(onShare: () -> Unit = {}, onDelete: () -> Unit = {}) {
    var open by remember { mutableStateOf(false) }
    var sort by rememberSaveable { mutableIntStateOf(1) }
    GlassScene(Modifier.fillMaxSize()) {
        GlassContent(Modifier.fillMaxSize()) {
            Image(painterResource(R.drawable.wallpaper), contentDescription = null, modifier = Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
        }
        Box(Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(16.dp)) {
            GlassMenu(
                expanded = open,
                onDismissRequest = { open = false },
                trigger = {
                    GlassButton(onClick = { open = true }) {
                        GlassText("Sort by ${Sorts[sort]}")
                        GlassIcon(Icons.Filled.KeyboardArrowDown, contentDescription = null)
                    }
                },
            ) {
                Sorts.forEachIndexed { index, label ->
                    GlassMenuItem(label, onClick = { sort = index }, checked = index == sort)
                }
                GlassMenuDivider()
                GlassMenuItem("Share", onClick = onShare, icon = Icons.Filled.Share)
                GlassMenuItem("Delete", onClick = onDelete, icon = Icons.Filled.Delete, destructive = true)
            }
        }
    }
}
```

Notes:
- In a `GlassToolbar`, put the `GlassMenu` straight in the toolbar's content with an icon `GlassButton` as the trigger: `trigger = { GlassButton(Icons.Filled.MoreVert, "More", onClick = { open = true }) }`. The toolbar positions it.
- Keep `open` in `remember`, not `rememberSaveable`: a menu should not reopen after a configuration change.
- Inside a scene the panel draws in the scene's overlay (the outermost scene when nested) and refracts the whole scene. Outside any scene it opens in a `Popup` with the fallback material.
- The panel brings its own legibility backing (its glass mixed 60% toward `#F5F5F7` / `#1C1C1E`) and picks its label color from it, so the labels of an untinted menu, or one tinted with a system color, read over white photos in dark appearance and over black in light: add no background or scrim to it. Tint with a deep color or not at all; very light tints (pure yellow and white) can fall below 4.5:1. A `destructive` item takes a deep red on light menus (`#560D0A`, at least 5.0:1) and a light pink on dark menus (`#FFEEED`, at least 4.5:1), so it stays readable on any backdrop; the label carries the meaning.
- The panel is as wide as its widest item (220 dp at least, the safe width at most) and long labels wrap; pass `modifier = Modifier.width(280.dp)` to fix it. Items are 44 dp tall with 22 dp corners inside the 28 dp panel (6 dp inset): concentric.

## 6. Progressive blur over a scrolling list

A `LazyColumn` is the backdrop; bands along the top and bottom edges blur it progressively so it fades under the floating toolbar. The bands sit in the scene, not in the content. Text in the content gets explicit colors.

```kotlin
package com.example.glass

import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Menu
import androidx.compose.material.icons.filled.Search
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassEdge
import io.github.rookasrudzianskas.liquidglass.GlassProgressiveBlur
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer

@Composable
fun Journal() {
    val dark = isSystemInDarkTheme()
    val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val bottom = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()
    GlassScene(Modifier.fillMaxSize()) {
        GlassContent(Modifier.fillMaxSize()) {
            LazyColumn(
                Modifier.fillMaxSize().background(if (dark) Color.Black else Color(0xFFF2F2F7)),
                contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = top + 76.dp, bottom = bottom + 24.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp),
            ) {
                items(30) { index -> EntryCard(index, dark) }
            }
        }
        GlassProgressiveBlur(GlassEdge.Top, Modifier.align(Alignment.TopCenter), size = top + 96.dp, blur = 18.dp)
        GlassProgressiveBlur(GlassEdge.Bottom, Modifier.align(Alignment.BottomCenter), size = bottom + 64.dp, blur = 18.dp)
        GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth()) {
            GlassButton(Icons.Filled.Menu, "Sections", onClick = {})
            GlassToolbarSpacer()
            GlassButton(Icons.Filled.Search, "Search", onClick = {})
            GlassButton(Icons.Filled.Add, "New entry", onClick = {})
        }
    }
}

/** A solid card: content is never glass. Radii are concentric: 26 dp card, 18 dp inset, 8 dp image. */
@Composable
private fun EntryCard(index: Int, dark: Boolean) {
    val text = if (dark) Color.White else Color(0xFF111113)
    val hue = (index * 47f) % 360f
    Column(
        Modifier.fillMaxWidth().clip(GlassShape(26.dp)).background(if (dark) Color(0xFF1C1C1E) else Color.White).padding(18.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Box(Modifier.fillMaxWidth().height(150.dp).clip(GlassShape(8.dp)).background(Brush.linearGradient(listOf(Color.hsv(hue, 0.6f, 0.95f), Color.hsv((hue + 40f) % 360f, 0.8f, 0.6f)))))
        GlassText("Entry ${index + 1}", Modifier.padding(top = 8.dp), style = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold), color = text)
        GlassText("Six blur levels mixed by depth fade the list into the toolbar above it.", style = TextStyle(fontSize = 15.sp, lineHeight = 21.sp), color = text.copy(alpha = 0.7f))
    }
}
```

Notes:
- `size` is the band depth from the edge; include the system bar inset so the blur reaches past the bar. A band spans the full length of its edge unless its modifier sizes it.
- Glass over a band refracts the blurred content, which is what makes the toolbar read cleanly over busy rows.
- The blur needs Android 12; Android 7 to 11 fade the band toward the edge instead. Keep bands as shallow as the design allows: each one draws six band-sized blur layers.
- With a floating tab bar at the bottom (recipe 1), add its clearance (about 96 dp) to the list's bottom `contentPadding` and deepen the bottom band to match.

## 7. Sheet over the scene

A share sheet over a photo. Material `ModalBottomSheet` opens in another window, where glass draws the fallback material, so the sheet is a Regular `GlassSurface` in the scene that slides in from the bottom. The dimming scrim is the last child of `GlassContent`: the sheet refracts the dimmed photo, and tapping the scrim closes the sheet.

```kotlin
package com.example.glass

import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer
import io.github.rookasrudzianskas.liquidglass.LocalGlassContentColor

@Composable
fun ShareSheet(onBack: () -> Unit = {}) {
    var open by remember { mutableStateOf(false) }
    BackHandler(enabled = open) { open = false }
    GlassScene(Modifier.fillMaxSize()) {
        GlassContent(Modifier.fillMaxSize()) {
            Image(painterResource(R.drawable.photo), contentDescription = "Dunes at sunset", modifier = Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
            // Part of the backdrop, so it dims what the glass refracts, not the glass itself.
            if (open) {
                Box(
                    Modifier
                        .matchParentSize()
                        .background(Color.Black.copy(alpha = 0.3f))
                        .pointerInput(Unit) { detectTapGestures { open = false } },
                )
            }
        }
        // Stays bright over the scrim. To dim it too, move the photo and this toolbar into a nested scene inside GlassContent, before the scrim.
        GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth()) {
            GlassButton(Icons.AutoMirrored.Filled.ArrowBack, "Back", onClick = onBack)
            GlassToolbarSpacer()
            GlassButton(Icons.Filled.Share, "Share", onClick = { open = true })
        }
        // Slide, never fade: alpha around glass fades only its children.
        AnimatedVisibility(
            visible = open,
            modifier = Modifier.align(Alignment.BottomCenter),
            enter = slideInVertically { it },
            exit = slideOutVertically { it },
        ) {
            GlassSurface(
                Modifier.navigationBarsPadding().padding(8.dp).widthIn(max = 480.dp).fillMaxWidth(),
                shape = GlassShape(32.dp),
                material = GlassMaterial.Regular,
            ) {
                Column(Modifier.padding(24.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    GlassText("Share photo", style = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold))
                    GlassText("Dunes · 18:42", style = TextStyle(fontSize = 15.sp), color = LocalGlassContentColor.current.copy(alpha = 0.65f))
                    Row(Modifier.fillMaxWidth().padding(top = 8.dp), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        GlassButton("Copy link", onClick = { open = false }, modifier = Modifier.weight(1f), material = GlassMaterial.Regular)
                        GlassButton(
                            "Send",
                            onClick = { open = false },
                            modifier = Modifier.weight(1f),
                            icon = Icons.AutoMirrored.Filled.Send,
                            material = GlassMaterial.Regular,
                            tint = Color(0xFF0A84FF),
                        )
                    }
                }
            }
        }
    }
}
```

Notes:
- A scrim placed in the scene after `GlassContent` would be a plain composable above every glass body: it would dim the sheet's glass but not its labels, and the toolbar's glass with it.
- `BackHandler` (from activity-compose) closes the sheet before the activity handles Back.
- Buttons on the sheet are Regular so they show the sheet's frost; the one tinted action is Send.
