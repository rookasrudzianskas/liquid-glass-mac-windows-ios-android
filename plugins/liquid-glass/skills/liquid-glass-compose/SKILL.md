---
name: liquid-glass-compose
description: Builds Liquid Glass interfaces in Jetpack Compose on Android with the io.github.rookasrudzianskas:liquid-glass-compose library, whose GlassScene, GlassContent, GlassSurface, Modifier.liquidGlass, GlassButton, GlassToolbar, GlassTabBar, GlassSlider, GlassSwitch, GlassMenu and GlassProgressiveBlur refract live Compose content through an AGSL RuntimeShader on Android 13+ with Clear and Regular (frosted) materials, rim light and elastic press, and draw a fallback material on Android 7 to 12. Use when writing Jetpack Compose (Android) code for liquid glass, glassmorphism, refraction, frosted or blurred glass top app bars, toolbars, bottom navigation bars, floating tab bars, FABs, buttons, segmented buttons, sliders, switches, dropdown menus, bottom sheets or cards, an Apple-style or iOS 26 Liquid Glass look in an Android app, or when Compose glass looks flat, gray, unrefracted, invisible, misplaced or draws the fallback material.
license: MIT
metadata:
  homepage: https://liquid-glass-engineering.vercel.app
  repository: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android
---

# Liquid Glass for Jetpack Compose

`liquid-glass-compose` renders Liquid Glass natively in Compose: a bevel that bends the content behind it, the Clear and Regular materials, a rim of light and controls that grow, stretch and glow under the finger. One `RenderEffect`, built from an AGSL shader on the scene's content layer, draws every surface of a scene on the GPU. It shares one optical model with the SwiftUI and WPF ports, so the same options give the same pixels.

## When to use

- An Android app written with Jetpack Compose needs glass toolbars, tab bars, buttons, sliders, switches, menus or cards floating over imagery, video, maps or scrolling content.
- Existing Compose glass looks like a gray translucent pill, shows no refraction or is invisible: see [Troubleshooting](#troubleshooting).
- Android Views/XML apps: host the glass screen in a `ComposeView` (`setContent { GlassScene { … } }`) and use this library; existing Views can be the backdrop through `AndroidView` inside `GlassContent` (except `SurfaceView`-backed ones). Flutter, React Native, Compose Multiplatform targets other than Android and other renderers: use the `liquid-glass-port` skill.
- For *where* glass belongs and which material to pick, the `liquid-glass` skill holds the design rules; this skill applies them to Compose.

## Install

```kotlin
// settings.gradle.kts
dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
        maven("https://liquid-glass-engineering.vercel.app/maven")
    }
}
```

```kotlin
// app/build.gradle.kts
dependencies {
    implementation("io.github.rookasrudzianskas:liquid-glass-compose:1.0.0")
    implementation(platform("androidx.compose:compose-bom:2024.12.01"))
    implementation("androidx.activity:activity-compose:1.9.3")         // setContent, enableEdgeToEdge, BackHandler
    implementation("androidx.compose.material:material-icons-core")   // only for Icons.Filled.* in the examples
}
```

Alternative, JitPack (builds `platforms/android` from a release tag; the coordinate resolves only once the `1.0.0` tag is published, so prefer the Maven repository above):

```kotlin
// settings.gradle.kts: repositories { maven("https://jitpack.io") }
implementation("com.github.rookasrudzianskas:liquid-glass-mac-windows-ios-android:1.0.0")
```

| Requirement | Value |
| --- | --- |
| minSdk | 24 (Android 7.0) |
| GPU glass | Android 13 (API 33) and newer: AGSL `RuntimeShader` + `RenderEffect` |
| Below 33 | Fallback material, automatically (see [Fallbacks](#fallbacks)) |
| Built with | Kotlin 2.0.21, Compose UI/Foundation 1.7.6, AGP 8.7.3, compileSdk 35, JVM target 11 |
| Your app | Kotlin 2.0+ with the Compose compiler plugin (`org.jetbrains.kotlin.plugin.compose`), `buildFeatures { compose = true }`, compileSdk 34 or newer |
| Dependencies | Exposed (api): `androidx.compose.ui:ui`, `androidx.compose.foundation:foundation` 1.7.6. Internal only (runtime): `androidx.activity:activity-compose` 1.9.3, so declare it yourself for `setContent`, `enableEdgeToEdge` and `BackHandler`. No Material dependency. No ProGuard/R8 rules needed. |
| Package | `io.github.rookasrudzianskas.liquidglass` (one import per name: `import io.github.rookasrudzianskas.liquidglass.GlassScene`) |

## Mental model: scene, content, surfaces

```
GlassScene(Modifier.fillMaxSize())        a Box: the region glass may refract; holds the defaults
├─ GlassContent(Modifier.fillMaxSize())   ONE backdrop layer, first child. Any Compose UI: Image,
│                                         LazyColumn, Canvas, animation, a Material screen   ← refracted
├─ GlassProgressiveBlur(GlassEdge.Top)    a blur band baked into the backdrop               ← refracted too
├─ GlassToolbar / GlassTabBar / GlassSurface / GlassButton / Modifier.liquidGlass …         ← glass
│     └─ their children                   GlassText, GlassIcon, Rows: drawn on top, NOT refracted
├─ plain composables                      drawn above ALL glass of this scene, NOT refracted
└─ (overlay)                              open GlassMenu panels, above everything
```

1. **Scene** = `GlassScene { }`, a `Box` (`BoxScope` content, place children with `Modifier.align`). Glass sees only its own scene's content. Use one per screen, filling the window.
2. **Content** = `GlassContent { }`, the backdrop. It records its children into a GPU layer and, on Android 13+, paints every glass surface of the scene into that same layer with one `RenderEffect`. That is why glass outside the content's bounds draws nothing, and why a plain composable placed in the scene (outside the content) draws above every glass surface.
3. **Surfaces** = any glass composable or `Modifier.liquidGlass` element elsewhere in the scene, at any depth: in a `Column`, a scrolling list, under `offset`, `Modifier.scale`, `graphicsLayer` scale or translation (lambda or parameter form), animated. Each reports its exact bounds every frame; under a scale its corners, refraction and glow scale too. A surface's children draw in the normal tree above its glass and are not refracted.
4. **Options inherit.** `GlassScene(material, appearance, refraction, motion, reduceTransparency, increaseContrast)` sets the defaults; components override them with nullable parameters (`null` = the scene's) and `refraction = Dp.Unspecified` (the scene's; when the scene sets none either, the surface's resting size decides: `min(60 dp, 0.75 × its shorter side)`, so small controls never smear in backdrop from outside their outline).
5. **Nesting.** Glass that must float above other glass (a tab bar over pages that have their own glass) needs a second scene: put a whole `GlassScene` inside the outer scene's `GlassContent`. The inner scene's output, glass included, becomes the outer backdrop. Nested scenes do not inherit options.

## Minimal example

```kotlin
package com.example.glass   // the app module's namespace, so R needs no import

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Menu
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
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

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()                                  // the backdrop runs under the system bars
        super.onCreate(savedInstanceState)
        setContent { GlassHome() }
    }
}

@Composable
fun GlassHome() {
    GlassScene(Modifier.fillMaxSize()) {
        // 1. The backdrop (any vivid image in res/drawable-nodpi). It must cover everywhere glass appears.
        GlassContent(Modifier.fillMaxSize()) {
            Image(painterResource(R.drawable.wallpaper), contentDescription = null, modifier = Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
        }
        // 2. Glass anywhere else in the scene refracts it.
        GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth()) {
            GlassButton(Icons.Filled.Menu, "Menu", onClick = {})
            GlassToolbarSpacer()                            // flexible gap: two clusters
            GlassButton(Icons.Filled.Share, "Share", onClick = {})
            GlassButton(Icons.Filled.Add, "New", onClick = {})
        }
        GlassSurface(
            Modifier.align(Alignment.Center).padding(24.dp).widthIn(max = 420.dp).fillMaxWidth(),
            shape = GlassShape(32.dp),
            material = GlassMaterial.Regular,               // frosted: it holds text
        ) {
            Column(Modifier.padding(24.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                GlassText("Liquid Glass", style = TextStyle(fontSize = 28.sp, fontWeight = FontWeight.Bold))
                GlassText(
                    "Refraction, frost and rim light over any Compose content.",
                    style = TextStyle(fontSize = 15.sp, lineHeight = 21.sp),
                    color = LocalGlassContentColor.current.copy(alpha = 0.75f),
                )
            }
        }
        GlassButton(
            text = "Play",
            onClick = {},
            icon = Icons.Filled.PlayArrow,
            tint = Color(0xFF0A84FF),                        // the one prominent action
            modifier = Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(bottom = 32.dp),
        )
    }
}
```

## Components

Every signature and default, with snippets: [references/api.md](references/api.md).

| Component | Purpose | Key parameters (default) |
| --- | --- | --- |
| `GlassScene(modifier) { }` | Box: region glass refracts; defaults for everything inside | `material` (`Clear`), `appearance` (`Automatic` = `isSystemInDarkTheme()`), `refraction` (`Dp.Unspecified`: each surface follows its size, `GlassDefaults.refraction(width, height)`), `motion` (`Full`), `reduceTransparency` (false), `increaseContrast` (`GlassDefaults.systemIncreaseContrast()`) |
| `GlassContent(modifier) { }` | The one backdrop layer, first child | `modifier` (use `Modifier.fillMaxSize()`) |
| `Modifier.liquidGlass(…)` | Turns any element into glass; order it like a background | `shape` (`GlassShape(16.dp)`), `material`/`appearance`/`motion` (null = scene), `tint` (`Color.Unspecified`), `refraction` (`Dp.Unspecified` = scene, else `min(60 dp, 0.75 × shorter side)`), `interactive` (false), `opacity` (1f), `interactionSource` (null) |
| `GlassSurface(modifier) { }` | Glass `Box` that provides the right foreground color | as `liquidGlass` without `opacity`/`interactionSource`, plus `contentAlignment` (`Alignment.TopStart`) |
| `GlassText(text)` | Text in the glass foreground | `style` (`GlassDefaults.LabelTextStyle`, 17 sp semibold), `color` (`Unspecified` → `LocalGlassContentColor`), `maxLines` (`Int.MAX_VALUE`) |
| `GlassIcon(imageVector \| painter, contentDescription)` | Icon in the glass foreground | `tint` (`LocalGlassContentColor.current`), `size` (22.dp); `contentDescription` is required (null if decorative) |
| `GlassButton(onClick) { RowScope }` | Interactive capsule with any content; disabled = glass and label at 45% | `enabled` (true), `size` (`GlassButtonSize.Regular` 44 dp; `Small` 34, `Large` 52), `shape` (`Capsule`), `tint`, `material`, `appearance`, `refraction`, `interactive` (true), `motion`, `interactionSource` |
| `GlassButton(text, onClick)` | Text button | `icon` (null), `enabled`, `size`, `material`, `appearance`, `tint`, `interactionSource` |
| `GlassButton(icon, contentDescription, onClick)` | Icon-only circle | `enabled`, `size`, `material`, `appearance`, `tint`, `interactionSource` |
| `GlassToolbar(modifier) { }` | 52 dp floating capsule; its `GlassButton`s become plain items (their own tint, material, appearance and shape are ignored) | `material`, `appearance`, `tint`, `interactive` (true), `motion` |
| `GlassToolbarSpacer()` | Splits toolbar items into separate glass clusters | `minWidth` (8.dp), `flexible` (true) |
| `GlassTabBar(tabs, selectedIndex, onSelectedIndexChange)` | 62 dp capsule with a sliding, draggable glass lens; segmented control if tabs have no icons | `modifier`, `material`, `appearance`, `tint`, `selectedContentColor` (`Unspecified` = foreground), `unselectedContentColor` (`Unspecified` = foreground at 85%), `interactive` (true), `motion` |
| `GlassTab(label, icon)` | One tab | `icon` (null = label-only) |
| `GlassSlider(value, onValueChange)` | 44 dp tall, fills its width; white thumb lifts into glass; disabled = 45% | `enabled` (true), `valueRange` (`0f..1f`), `steps` (0), `onValueChangeFinished` (null), `tint` (platform accent), `appearance`, `motion`, `label` (null; TalkBack name) |
| `GlassSwitch(checked, onCheckedChange)` | 54 × 24 dp track in a 54 × 44 dp touch box; disabled = 45% | `enabled` (true), `tint` (platform accent), `appearance`, `motion`, `label` (null; TalkBack name) |
| `GlassMenu(expanded, onDismissRequest, trigger) { ColumnScope }` | Regular panel that grows out of its trigger (out of the whole cluster for a toolbar item) | `modifier` (the panel: as wide as its widest item, 220 dp to the safe width), `material` (`Regular`), `appearance`, `tint`, `shape` (`GlassShape(GlassDefaults.MenuRadius)` = 28 dp), `motion` |
| `GlassMenuItem(text, onClick)` / `GlassMenuDivider()` | Menu row ≥ 44 dp, label wraps; separator | `icon` (null), `enabled` (true), `destructive` (false), `checked` (null = action, true/false = choice) |
| `GlassProgressiveBlur(edge)` | Progressive edge blur of the backdrop under bars | `modifier`, `size` (`GlassDefaults.ProgressiveBlurSize` = 80.dp), `blur` (`GlassDefaults.ProgressiveBlurRadius` = 20.dp) |
| `GlassShape(radius)`, `GlassShape.Capsule`, `GlassShape.Circle` | Continuous-corner `Shape`, also for `clip`/`border` | `radius` finite, ≥ 0 |
| `GlassDefaults` | Constants | `Refraction` (60.dp, the full travel), `refraction(width, height)` (the size-based default), `SurfaceRadius` (16.dp), `MenuRadius` (28.dp), `MaxSurfacesPerScene` (16), `LightContentColor` (`#111113`), `DarkContentColor` (white), `LabelTextStyle`, `contentColor(dark)`, `systemIncreaseContrast()` |
| `LocalGlassContentColor` | Foreground for your own content on glass | `#111113` on light glass, white on dark |
| Enums | | `GlassMaterial { Clear, Regular }`, `GlassAppearance { Automatic, Light, Dark }`, `GlassMotion { Full, Reduced, None }`, `GlassEdge { Top, Bottom, Start, End }` |

## Recipes

Complete files with imports: [references/recipes.md](references/recipes.md). The core of each:

**App shell with a floating tab bar.** Pages that have glass of their own are a nested scene; the tab bar floats in the outer scene and refracts the whole page, glass included (the Studio app's structure).

```kotlin
GlassScene(Modifier.fillMaxSize()) {
    GlassContent(Modifier.fillMaxSize()) {
        GlassScene(Modifier.fillMaxSize()) {                         // the page and its own glass
            GlassContent(Modifier.fillMaxSize()) { Page(tabs[tab].label) }
            GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth(), material = GlassMaterial.Regular) { /* … */ }
        }
    }
    GlassTabBar(
        tabs, tab, { tab = it },
        Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(start = 20.dp, end = 20.dp, bottom = 12.dp).widthIn(max = 480.dp).fillMaxWidth(),
        material = GlassMaterial.Regular,
    )
}
```

**Toolbar over a photo.** Clear glass keeps the photo vivid; pick the appearance from the image (`Dark` for a photo viewer).

```kotlin
GlassScene(Modifier.fillMaxSize(), appearance = GlassAppearance.Dark) {
    GlassContent(Modifier.fillMaxSize()) { Image(painterResource(R.drawable.photo), "Dunes at sunset", Modifier.fillMaxSize(), contentScale = ContentScale.Crop) }
    GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth()) {
        GlassButton(Icons.AutoMirrored.Filled.ArrowBack, "Back", onClick = onBack)
        GlassToolbarSpacer()
        GlassButton(Icons.Filled.Share, "Share", onClick = {})
        GlassButton(Icons.Filled.Info, "Details", onClick = {})
    }
}
```

**Toolbar with a prominent action.** Toolbar items ignore `tint`; put the one tinted action beside the toolbar as a standalone button.

```kotlin
Row(
    Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth(),
    horizontalArrangement = Arrangement.spacedBy(8.dp),
    verticalAlignment = Alignment.CenterVertically,
) {
    GlassToolbar(Modifier.weight(1f)) { GlassButton(Icons.AutoMirrored.Filled.ArrowBack, "Back", onClick = onBack); GlassToolbarSpacer(); GlassButton(Icons.Filled.Share, "Share", onClick = {}) }
    GlassButton(Icons.Filled.Check, "Done", onClick = onDone, size = GlassButtonSize.Large, tint = Color(0xFF0A84FF))
}
```

**Media card with a slider.** Regular card (it holds text), concentric content, the slider as scrubber. Glass controls on a Regular card are the one allowed nesting: give its `GlassButton`s `material = GlassMaterial.Regular`. Read `LocalGlassContentColor` inside the surface.

```kotlin
GlassSurface(Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(16.dp).widthIn(max = 420.dp).fillMaxWidth(), shape = GlassShape(32.dp), material = GlassMaterial.Regular) {
    val secondary = LocalGlassContentColor.current.copy(alpha = 0.65f)
    Column(Modifier.padding(20.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        GlassText("Midnight Drive", style = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold))
        GlassText("The Glass Hours", style = TextStyle(fontSize = 15.sp), color = secondary)
        GlassSlider(position, { position = it }, tint = Color(0xFF0A84FF), label = "Playback position")
        GlassButton(Icons.Filled.PlayArrow, "Play", onClick = onPlay, size = GlassButtonSize.Large, material = GlassMaterial.Regular, tint = Color(0xFF0A84FF))
    }
}
```

**Settings card with switches.** Compact Regular card over a wallpaper; the switch carries the row label for TalkBack.

```kotlin
GlassSurface(Modifier.align(Alignment.Center).padding(20.dp).widthIn(max = 420.dp).fillMaxWidth(), shape = GlassShape(28.dp), material = GlassMaterial.Regular) {
    Column(Modifier.padding(horizontal = 20.dp, vertical = 16.dp)) {
        Row(Modifier.fillMaxWidth().heightIn(min = 52.dp), verticalAlignment = Alignment.CenterVertically) {
            GlassText("Wi-Fi", Modifier.weight(1f).clearAndSetSemantics {}, style = TextStyle(fontSize = 17.sp))
            GlassSwitch(wifi, { wifi = it }, tint = Color(0xFF30D158), label = "Wi-Fi")
        }
    }
}
```

**Glass menu from a button.** `modifier` styles the panel, so wrap the menu in a `Box` to place its trigger. Keep `open` in `remember`.

```kotlin
var open by remember { mutableStateOf(false) }
Box(Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(16.dp)) {
    GlassMenu(expanded = open, onDismissRequest = { open = false }, trigger = { GlassButton(Icons.Filled.MoreVert, "More", onClick = { open = true }) }) {
        GlassMenuItem("Date modified", onClick = { sort = 1 }, checked = sort == 1)
        GlassMenuDivider()
        GlassMenuItem("Delete", onClick = onDelete, icon = Icons.Filled.Delete, destructive = true)
    }
}
```

**Sheet over the scene.** `ModalBottomSheet` is another window, so build the sheet in the scene. The scrim goes inside `GlassContent` as its last child: it dims the backdrop the sheet refracts, while bars in the scene stay bright (to dim them too, put page and bars in a nested scene inside the outer `GlassContent`, before the scrim). Slide the sheet, never fade it.

```kotlin
var open by remember { mutableStateOf(false) }
BackHandler(open) { open = false }
GlassScene(Modifier.fillMaxSize()) {
    GlassContent(Modifier.fillMaxSize()) {
        Image(painterResource(R.drawable.wallpaper), null, Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
        if (open) Box(Modifier.matchParentSize().background(Color.Black.copy(alpha = 0.3f)).pointerInput(Unit) { detectTapGestures { open = false } })
    }
    AnimatedVisibility(open, Modifier.align(Alignment.BottomCenter), enter = slideInVertically { it }, exit = slideOutVertically { it }) {
        GlassSurface(Modifier.navigationBarsPadding().padding(8.dp).widthIn(max = 480.dp).fillMaxWidth(), shape = GlassShape(32.dp), material = GlassMaterial.Regular) {
            Column(Modifier.padding(24.dp)) { GlassText("Share photo"); /* … */ }
        }
    }
}
```

**Progressive blur over a scrolling list.** The list is the backdrop; blur bands and bars sit in the scene. Include the system bar inset in the band depth and the list padding.

```kotlin
val dark = isSystemInDarkTheme()
val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
GlassScene(Modifier.fillMaxSize()) {
    GlassContent(Modifier.fillMaxSize()) {
        LazyColumn(Modifier.fillMaxSize().background(if (dark) Color.Black else Color(0xFFF2F2F7)), contentPadding = PaddingValues(top = top + 76.dp)) { items(30) { EntryCard(it, dark) } }
    }
    GlassProgressiveBlur(GlassEdge.Top, Modifier.align(Alignment.TopCenter), size = top + 96.dp, blur = 18.dp)
    GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).widthIn(max = 480.dp).fillMaxWidth()) { /* … */ }
}
```

**Custom glass control.** `Modifier.liquidGlass` after the size, `clickable` after it, sharing one `interactionSource` so a scroll that cancels the click also cancels the press.

```kotlin
val source = remember { MutableInteractionSource() }
Box(
    Modifier.size(64.dp).liquidGlass(GlassShape.Circle, interactive = true, interactionSource = source)
        .clickable(source, indication = null, role = Role.Button) { record() },
    contentAlignment = Alignment.Center,
) { GlassIcon(Icons.Filled.Add, contentDescription = "Record") }
```

## Rules and pitfalls

**What goes in the content layer**

- Exactly one `GlassContent` per scene, first child, `Modifier.fillMaxSize()`. Glass is painted inside that layer: a surface partly outside it is cut off at its edge, and one entirely outside it shows only its children, no glass.
- Everything glass should refract goes inside `GlassContent`: images, gradients, `LazyColumn`/`LazyVerticalGrid`, `Canvas` art, animations, whole Material 3 screens. It stays interactive wherever no glass covers it.
- `GlassContent` must paint opaque pixels everywhere glass can sit: give the list or screen a background inside it (`Modifier.background` on the `LazyColumn`, or a `Box(Modifier.fillMaxSize().background(…))` first). Backgrounds of ancestors (a `MaterialTheme` `Surface`, a `Scaffold`, the window) are not part of the layer. Use `ContentScale.Crop` for image backdrops. Over transparent pixels untinted glass draws only its rim.
- `SurfaceView`-backed content (a video player's default surface, map views, camera previews) renders in a separate system surface the layer cannot record, so glass does not see it: switch to a `TextureView`. Media3 `PlayerView`: `app:surface_type="texture_view"`; Media3 Compose: `PlayerSurface(player, surfaceType = SURFACE_TYPE_TEXTURE_VIEW)`; CameraX `PreviewView`: `implementationMode = PreviewView.ImplementationMode.COMPATIBLE`.
- Never put glass inside the `GlassContent` of its own scene: it cannot refract the layer it belongs to and draws the fallback material. Nest a scene instead.
- Plain composables placed in the scene outside `GlassContent` (a solid header, a `Text`, a dimming scrim) draw above every glass surface of that scene and are not refracted. Move them into the content (a scrim as its last child), or nest a scene when glass must float above them.
- `GlassScene` provides `LocalGlassContentColor` to everything inside it, content included. Give text in the content explicit colors (`GlassText(color = …)`), and read `LocalGlassContentColor` inside a surface's content lambda, never above it.
- Material `Text` and `Icon` ignore the glass foreground (they read Material's `LocalContentColor`), so they can come out dark on dark glass. On glass use `GlassText`/`GlassIcon`, pass `color = LocalGlassContentColor.current`, or wrap the surface's content in `CompositionLocalProvider(LocalContentColor provides LocalGlassContentColor.current) { … }`.
- Glass in another window does not refract the scene: `Dialog`, `Popup`, Material `ModalBottomSheet` and `DropdownMenu` draw the fallback material. Use `GlassMenu` (it opens in the scene's overlay, not a window) and build sheets in the scene (recipe above).

**Layout and composition**

- `GlassScene` content is a `BoxScope`: position glass with `Modifier.align(...)`, insets (`statusBarsPadding()`, `navigationBarsPadding()`) and padding. Call `enableEdgeToEdge()` so the backdrop runs under the system bars.
- Material 3 `Scaffold`: never put glass in its `topBar`, `bottomBar` or `floatingActionButton` slots outside a scene (fallback with nothing behind it), and never place a default `Scaffold` in the scene after `GlassContent` (its `containerColor` covers every glass body). Put the `Scaffold` inside `GlassContent` as the backdrop and the glass bars in the scene, or put a `Scaffold(containerColor = Color.Transparent)` in the scene after `GlassContent` with the glass bars in its slots and an empty body (the backdrop stays in `GlassContent`). Pad lists with `WindowInsets` yourself.
- Modifier order: size, place, then `liquidGlass`, then `clickable`, then inner `padding`. `Modifier.padding(16.dp).liquidGlass()` insets the glass; `Modifier.liquidGlass().padding(16.dp)` pads the content.
- Never add `background`, `blur` or `shadow` modifiers to a glass element to strengthen it: they cover or smear the glass. Use `material = GlassMaterial.Regular`, `tint` or `refraction` instead.
- Cap widths for tablets, foldables and landscape: `.widthIn(max = 480.dp).fillMaxWidth()` for toolbars and tab bars, `widthIn(max = 420.dp)` for cards.
- `GlassMenu`'s `modifier` is the panel. Place the menu by wrapping it in a `Box(Modifier.align(...))`, a `Row`, or a `GlassToolbar` (an icon `GlassButton` trigger becomes a toolbar item). `Modifier.align` on the trigger itself has no effect.
- `GlassToolbar` content is a plain lambda, not a `RowScope`: no `weight`; use `GlassToolbarSpacer()` (flexible) for space and give the toolbar `fillMaxWidth()` so spacers can spread. Only `GlassButton`s become borderless items; other composables are placed as they are. Group related actions in one toolbar instead of rows of standalone `GlassButton`s.
- Inside `GlassToolbar` a `GlassButton` keeps only `onClick`, `enabled`, its content and label style: `tint`, `material`, `appearance`, `shape`, `refraction`, `interactive`, `motion` and `size` height are ignored, and it keeps the toolbar's foreground color. A tinted action goes beside the toolbar (recipe above).
- `GlassTabBar` is controlled: update `selectedIndex` in `onSelectedIndexChange` or the lens snaps back. It spans the width it is given; use `fillMaxWidth()` with side padding (20 dp in the Studio) or a fixed `width`. Use 3 to 5 tabs.
- `GlassSlider` applies `fillMaxWidth().height(44.dp)` and `GlassSwitch` applies `size(54.dp, 44.dp)` after your modifier: constrain a slider's width from outside (`Modifier.width(…)` or its parent), and do not try to change either height. Their glass thumbs live in a private scene, so they work on a glass card and outside any scene.
- Nested scenes start from their own parameter defaults: pass every option you set (`material`, `appearance`, `refraction`, `motion`, `reduceTransparency`, `increaseContrast`) to each scene.
- `appearance = GlassAppearance.Automatic` (and null) follows `isSystemInDarkTheme()`, inside a `GlassScene` and outside any (out-of-scene sliders, switches, menu popups and fallback glass included), not your app's own theme switch: pass the resolved appearance when the app overrides the theme. Scene options may change at runtime; glass on screen follows them in the same frame.
- Glass controls on a Regular card are allowed (one level of nesting, as in the `liquid-glass` design rules). Give `GlassButton`s there `material = GlassMaterial.Regular` so they show the card's frost; a Clear button refracts the backdrop directly and shows it sharp through the frost. Plain fills (as on the Studio's Lab page) are the alternative for dense control groups; `GlassSlider`/`GlassSwitch` are designed for cards.

**Limits**

- Up to 16 surfaces per scene on the GPU (`GlassDefaults.MaxSurfacesPerScene`). Counting: 1 per `GlassSurface`, `liquidGlass` element or standalone `GlassButton`; 1 per toolbar cluster (buttons inside a toolbar are free); 1 per `GlassTabBar`, 2 while its lens is lifted (pressed or sliding); 1 per open menu (in the outermost scene); 0 for slider and switch thumbs; 0 for surfaces scrolled or clipped out of view or faded out (`opacity` 0). Extra surfaces draw the fallback material (Regular ones over a platform blur, Clear ones a translucent fill). Surfaces are counted shallowest first, so tab-bar lenses and open menus come last and fall back first: keep the outermost scene at 15 or fewer. Nest scenes to go further.
- Glass follows translation and scale of any ancestor, also a parameter-form `graphicsLayer(scaleX = …)` or `Modifier.scale` changing without a relayout; rotation is not supported (it draws the axis-aligned bounds).
- Alpha on a layer between a scene and its glass (`AnimatedVisibility` fades, `Crossfade`, `AnimatedContent` and `NavHost` default transitions around glass that shares an outer backdrop) fades only the children, never the glass, so labels fade while the glass pops. Fade glass with `Modifier.liquidGlass(opacity = …)`, use slide, scale or expand transitions (the glass follows them), or `EnterTransition.None`/`ExitTransition.None` on the `NavHost`. Fading a whole `GlassScene` (each destination its own scene) fades everything together.
- `interactive = false` makes glass fully static (no grow, stretch or glow), also with an `interactionSource` and in `GlassButton(interactive = false)`; clicks still work.
- `tint = Color.Transparent` is a black tint (any specified color tints fully; alpha is ignored). "No tint" is `Color.Unspecified`.
- `GlassShape` radius must be finite and ≥ 0, `GlassTabBar` needs at least one tab, `GlassSlider` needs `steps >= 0` and a non-empty `valueRange`; otherwise they throw `IllegalArgumentException`.
- Menus are placed inside the outermost scene and its safe drawing insets: make that scene fill the window.

### Fallbacks

| Where | What draws |
| --- | --- |
| Android 13+ inside a scene | The AGSL glass |
| Android 12 inside a scene | Fallback material (Regular surfaces over a platform blur of the content, Clear ones a translucent fill) + rim light; progressive blur works |
| Android 7 to 11 inside a scene | Fallback material (translucent fill) + rim light; progressive blur becomes a fade toward the edge |
| Outside any `GlassScene`, inside its own `GlassContent`, in a `Dialog`/`Popup` window, wholly outside the `GlassContent`'s bounds | Fallback material + rim light (no backdrop); outside a scene `Automatic` follows the system dark theme |
| Beyond 16 surfaces | The extra surfaces get the fallback material (Regular over a platform blur) |

Glass never crashes; where the GPU path is unavailable it draws the fallback material, also for glass placed entirely outside its scene's `GlassContent` bounds. An emulator shows the GPU path only on an Android 13+ image with GPU emulation (`-gpu host` or `swiftshader_indirect`); Android Studio previews and JVM screenshot tools may show the fallback, so judge the look on a device.

**Color and performance**

- Use saturated system hues as light/dark pairs: blue `#007AFF`/`#0A84FF`, green `#34C759`/`#30D158`, red `#FF3B30`/`#FF453A`, orange `#FF9500`/`#FF9F0A`.
- One `RenderEffect` draws all surfaces of a scene: a sharp pass, plus one frost pass per Regular appearance present (light, dark), each adding two blurs of the content layer (σ 7.33 dp and about 23 dp), the passes added together in exact z-order (Clear over Regular keeps its rim and side shade). Scenes with only Clear glass skip them.
- Nothing runs while the scene is still. The effect is rebuilt whenever the content layer redraws: content changes, a surface moving, a spring advancing. A backdrop that animates forever (looping video, infinite transitions) costs a rebuild every frame; keep it as the only such animation on screen.
- Each `GlassProgressiveBlur` band draws six band-sized blur layers: keep bands as shallow as the design allows. Every nested scene records its content into its own layers: nest only where glass must cover glass.
- A long list of glass rows works (offscreen rows are free), but it is a design smell: one glass bar over a solid list is cheaper and calmer.

**Accessibility**

- Roles come built in: buttons `Role.Button`, switches `Role.Switch` with toggle state, sliders expose range and "set progress", tab bars are selectable groups of `Role.Tab`, menu items `Role.Button` or `Role.Checkbox` (`checked`), the open menu is a pane. Glass itself adds no semantics nodes.
- Icon-only `GlassButton` requires `contentDescription`; `GlassIcon` takes `null` only when decorative. Label `GlassSlider` and `GlassSwitch` with `label = "…"` and hide the duplicate row text with `clearAndSetSemantics {}`; give a slider whose value is not a percentage `Modifier.semantics { stateDescription = "60 dp" }`.
- Disabled buttons, sliders and switches dim to 45%, glass included; menu items to 40%.
- Font scaling: labels use `sp` and controls grow with them; menu panels widen to fit and wrap long items, tab labels keep a 4 dp gap and ellipsize. Use `heightIn(min = …)`, never fixed heights, for rows and bars.
- Reduced motion: the system "Remove animations" setting lowers `GlassMotion.Full` to `Reduced` automatically. Use `motion = GlassMotion.None` for screenshot tests.
- Increase contrast follows the system (Android 14 contrast level, "High contrast text") through `GlassDefaults.systemIncreaseContrast()`; it adds a 1 dp border and forces Regular.
- Reduce transparency has no Android system setting: expose an app setting and pass `GlassScene(reduceTransparency = …)` (to every scene) for an opaque fill.
- Keyboard: 2 dp focus rings on buttons, sliders, switches, tabs and menu items (menu items are also highlighted), arrows for tabs and sliders, Enter/Space for switches, Escape and Back close menus. Haptics (press tick, toggle, detents) are built in; do not add your own.
- Menu strings `liquidglass_menu` ("Menu") and `liquidglass_dismiss_menu` ("Close menu") can be overridden or localized in your `res/values*/strings.xml`.

**Design defaults on Android**

- Clear over imagery and media; Regular for panels holding text, cards, menus and bars over text or busy lists.
- Capsules for controls, circles for icon-only buttons, continuous corners (`GlassShape(R)`) for panels; nested shapes concentric: `R − inset` is the cross-platform rule of thumb (the menu is 28 dp with 6 dp inset and 22 dp items). The exact concentric radius of `GlassShape` outlines is `R − inset / 1.528`; Compose has no helper: `GlassShape((r.value - inset.value / 1.528f).dp)`.
- One tinted control per screen. Never tint a toolbar or tab bar.
- Pick the appearance from what lies beneath: the Studio's tab bar uses `GlassAppearance.Dark` over wallpapers that darken toward the bottom. Match the system bar icons to the backdrop, as the Studio does, and after `enableEdgeToEdge()` set `window.isNavigationBarContrastEnforced = false` (API 29+) so 3-button navigation lays no scrim over the backdrop:

```kotlin
val view = LocalView.current
SideEffect {
    val window = (view.context as Activity).window
    WindowCompat.getInsetsController(window, view).apply {
        isAppearanceLightStatusBars = lightBackdropTop          // dark icons over a light backdrop
        isAppearanceLightNavigationBars = lightBackdropBottom
    }
}
```

- Glass needs color and detail behind it. Over a flat fill it reads as a gray pill: use a solid surface there instead.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Gray translucent pill, no bending | Flat backdrop (add color and detail, or use Regular / a progressive blur over plain lists); Android 12 or older; emulator without GPU emulation. Judge on an Android 13+ device. |
| Only a rim, body invisible | Transparent pixels under the glass: give the content an opaque background, `ContentScale.Crop` for images. |
| No glass at all, only its children | The surface lies outside its scene's `GlassContent` bounds. |
| Fallback material instead of refraction | Glass inside its own scene's `GlassContent`, outside any scene, in a `Dialog`/`Popup`/`ModalBottomSheet`, past the 16-surface budget, below API 33, or in a preview. |
| Video, map or camera not refracted | `SurfaceView`-backed content: switch it to a `TextureView`. |
| Glass dimmed or hidden but labels crisp | A scrim, `Scaffold` container or other opaque composable sits in the scene after `GlassContent`: move it into the content. |
| Labels fade but glass pops during transitions | Alpha between the scene and the glass: use `opacity`, slides or scales, or fade the whole scene. |
| Tint has no effect | The `GlassButton` is a toolbar item: move the tinted action beside the toolbar. |
| Glass turns black | `tint = Color.Transparent`: use `Color.Unspecified`. |
| Dark text on dark glass | Material `Text`/`Icon` color, or `LocalGlassContentColor` read outside the surface. |
| Light glass in dark mode | The app overrides the system theme: pass the resolved `appearance`. |
| Glass misplaced or the wrong size | `padding` before `liquidGlass` insets it; rotation draws axis-aligned bounds; `align` on a `GlassMenu` trigger does nothing (wrap it in a `Box`). |
| Tab lens snaps back | `onSelectedIndexChange` does not update `selectedIndex`. |

## Checklist

- [ ] Maven repository added in `settings.gradle.kts`; `io.github.rookasrudzianskas:liquid-glass-compose:1.0.0` and `androidx.activity:activity-compose` in the app module; Compose compiler plugin on; `enableEdgeToEdge()` in the activity.
- [ ] One `GlassScene(Modifier.fillMaxSize())` per screen; its first child is `GlassContent(Modifier.fillMaxSize())` holding everything glass should refract, painted opaque; no glass inside its own content.
- [ ] Glass that must cover other glass sits in an outer scene with the inner scene inside the outer `GlassContent`; options passed to both scenes.
- [ ] No `Dialog`/`Popup`/`ModalBottomSheet` for glass; menus use `GlassMenu`, wrapped in a `Box` to place the trigger; sheets and scrims built in the scene.
- [ ] Bars padded by system bar insets and width-capped; lists padded so content starts below and ends above floating bars.
- [ ] Clear over imagery, Regular for text; capsules, circles, concentric radii; at most one tint, never on a toolbar item; `Color.Unspecified` for no tint.
- [ ] At most 16 surfaces per scene (tab bar = 2, toolbar = 1 per cluster), with headroom for menus.
- [ ] Every icon-only control and every slider/switch has a description; text in the content has explicit colors; `LocalGlassContentColor` read inside surfaces; no Material `Text`/`Icon` with default colors on glass.
- [ ] Ran it on an Android 13+ device or GPU emulator: refraction visible at the rims, rim light crisp, light and dark checked, "Remove animations" and font scale 2.0 tried, and the fallback acceptable on an older API level.

## References

- [references/api.md](references/api.md): every public declaration with exact signatures, defaults, enum cases, composition locals, fixed metrics, the surface budget, resources and preconditions.
- [references/recipes.md](references/recipes.md): seven complete, runnable screens: app shell with a floating tab bar, toolbar over a photo, media card with a slider, settings card with switches, glass menu from a button, progressive blur over a scrolling list, sheet over the scene.
