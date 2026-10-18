# API reference: liquid-glass-compose 1.0.0

Every public declaration of `io.github.rookasrudzianskas:liquid-glass-compose:1.0.0`, as declared in `platforms/android/liquidglass/src/main/kotlin/io/github/rookasrudzianskas/liquidglass/`. The library is built with `explicitApi()`, so this list is the whole surface: everything under `.internal` and `.optics` is `internal` and cannot be called.

All declarations live in one package:

```kotlin
import io.github.rookasrudzianskas.liquidglass.GlassScene   // and so on, one import per name
```

There is no Material dependency. Icons in the snippets come from `androidx.compose.material:material-icons-core` (`Icons.Filled.*`, `Icons.AutoMirrored.Filled.*`).

## Contents

- [Enums](#enums)
- [GlassDefaults](#glassdefaults)
- [GlassShape](#glassshape)
- [LocalGlassContentColor](#localglasscontentcolor)
- [GlassScene](#glassscene)
- [GlassContent](#glasscontent)
- [Modifier.liquidGlass](#modifierliquidglass)
- [GlassSurface](#glasssurface)
- [GlassText](#glasstext)
- [GlassIcon](#glassicon)
- [GlassButtonSize](#glassbuttonsize)
- [GlassButton](#glassbutton)
- [GlassToolbar and GlassToolbarSpacer](#glasstoolbar-and-glasstoolbarspacer)
- [GlassTab and GlassTabBar](#glasstab-and-glasstabbar)
- [GlassSlider](#glassslider)
- [GlassSwitch](#glassswitch)
- [GlassMenu, GlassMenuItem and GlassMenuDivider](#glassmenu-glassmenuitem-and-glassmenudivider)
- [GlassProgressiveBlur](#glassprogressiveblur)
- [Fixed metrics](#fixed-metrics)
- [Surface budget](#surface-budget)
- [Resources](#resources)
- [Preconditions](#preconditions)

## Enums

```kotlin
public enum class GlassMaterial { Clear, Regular }
public enum class GlassAppearance { Automatic, Light, Dark }
public enum class GlassMotion { Full, Reduced, None }
public enum class GlassEdge { Top, Bottom, Start, End }
```

| Enum | Case | Meaning |
| --- | --- | --- |
| `GlassMaterial` | `Clear` | Transparent glass: the backdrop stays sharp and is only toned and refracted. The scene default. |
| | `Regular` | Frosted glass: the backdrop is blurred (σ 7.33 dp mixed with σ 23 dp light / 21.8 dp dark), brightened (light) or deepened (dark), then refracted. The `GlassMenu` default. |
| `GlassAppearance` | `Automatic` | Follows `isSystemInDarkTheme()`, inside a `GlassScene` and outside any (sliders, switches, menu popups and fallback glass there follow the system too). The scene default. |
| | `Light` | Light glass, foreground `#111113`. |
| | `Dark` | Dark glass, foreground white. |
| `GlassMotion` | `Full` | Press grow, drag stretch, gel overshoot and glow. The system "Remove animations" setting (animator duration scale 0) always lowers it to `Reduced`. |
| | `Reduced` | No grow, stretch or overshoot: springs become critically damped crossfades. |
| | `None` | No transitions: every value jumps to its target. Use for screenshot tests. |
| `GlassEdge` | `Top`, `Bottom` | The edge a `GlassProgressiveBlur` band blurs most. |
| | `Start`, `End` | Leading and trailing edges; they follow the layout direction (Start is left in LTR, right in RTL). |

Nullable `material`, `appearance` and `motion` parameters on components mean "inherit the scene's" when null.

## GlassDefaults

Constants shared by every component (values from `spec/OPTICS.md`).

```kotlin
public object GlassDefaults {
    public val Refraction: Dp = DEFAULT_REFRACTION.dp          // 60.dp, the full travel
    public fun refraction(width: Dp, height: Dp): Dp           // min(60.dp, 0.75 × shorter side)
    public val SurfaceRadius: Dp = 16.dp
    public val MenuRadius: Dp = 28.dp
    public val LightContentColor: Color = Color(0xFF111113)
    public val DarkContentColor: Color = Color(0xFFFFFFFF)
    public const val MaxSurfacesPerScene: Int = 16
    public val ProgressiveBlurSize: Dp = 80.dp
    public val ProgressiveBlurRadius: Dp = 20.dp
    public val LabelTextStyle: TextStyle = TextStyle(fontSize = 17.sp, fontWeight = FontWeight.SemiBold)
    public fun contentColor(dark: Boolean): Color = if (dark) DarkContentColor else LightContentColor
    @Composable
    public fun systemIncreaseContrast(): Boolean = rememberSystemIncreaseContrast()
}
```

| Member | Value | Use |
| --- | --- | --- |
| `Refraction` | 60.dp | The full optical travel at the rim: what a surface that sets none uses once its shorter side is 80 dp or more. |
| `refraction(width, height)` | `min(60.dp, 0.75 × min(width, height))` | The refraction a surface of this resting size uses when neither it nor its scene sets one (spec/OPTICS.md §2): 25.5 dp for a 34 dp button, 33 dp for a 44 dp one, 39 dp for the 52 dp toolbar, 46.5 dp for the 62 dp tab bar. |
| `SurfaceRadius` | 16.dp | Corner radius of a plain `GlassSurface` / `Modifier.liquidGlass()`. |
| `MenuRadius` | 28.dp | Corner radius of a `GlassMenu` panel. |
| `LightContentColor` | `#111113` | Text and icons on light glass. |
| `DarkContentColor` | `#FFFFFF` | Text and icons on dark glass. |
| `MaxSurfacesPerScene` | 16 | Surfaces one scene refracts on the GPU at once; more draw the fallback material. |
| `ProgressiveBlurSize` | 80.dp | Default band depth of `GlassProgressiveBlur`. |
| `ProgressiveBlurRadius` | 20.dp | Default strongest blur (Gaussian σ at the edge) of `GlassProgressiveBlur`. |
| `LabelTextStyle` | 17 sp SemiBold | Labels on glass controls; the default style of `GlassText`. |
| `contentColor(dark)` | | The foreground for glass of the given darkness. |
| `systemIncreaseContrast()` | `@Composable` | True when Android 14's contrast level is medium or higher, or "High contrast text" is on; updates live. The default of `GlassScene(increaseContrast)`. |

```kotlin
GlassText("Caption", style = GlassDefaults.LabelTextStyle.copy(fontSize = 13.sp))
val onDark = GlassDefaults.contentColor(dark = true)   // Color.White
```

## GlassShape

The Liquid Glass outline: a `Shape` with continuous (superellipse) corners. Clips, borders and backgrounds built from it line up with the glass exactly. Large radii blend smoothly into a capsule, so animating the radius never jumps.

```kotlin
@Immutable
public class GlassShape private constructor(
    public val radius: Dp,                       // Dp.Unspecified for a capsule
) : Shape {
    public val isCapsule: Boolean                // true for Capsule and Circle
    override fun createOutline(size: Size, layoutDirection: LayoutDirection, density: Density): Outline
    override fun equals(other: Any?): Boolean
    override fun hashCode(): Int
    override fun toString(): String

    public companion object {
        public val Capsule: GlassShape           // fully rounded ends: buttons, toolbars, tab bars, switches
        public val Circle: GlassShape            // = Capsule; a circle on a square element (icon-only buttons)
        public operator fun invoke(radius: Dp): GlassShape   // continuous corners; radius finite and >= 0
    }
}
```

```kotlin
GlassSurface(shape = GlassShape(28.dp)) { /* … */ }
Box(Modifier.size(56.dp).liquidGlass(GlassShape.Circle))
Image(painter, null, Modifier.clip(GlassShape(20.dp)).border(1.dp, Color.White, GlassShape(20.dp)))
```

`GlassShape(-1.dp)` or a non-finite radius throws `IllegalArgumentException`. Concentric nesting: an element inset by `i` inside a surface of radius `R` uses `GlassShape(R - i)`, the cross-platform rule of thumb. The exact concentric radius of these continuous corners (they reach 1.528 × radius along each edge) is `R - i / 1.528`; there is no helper: `GlassShape((r.value - i.value / 1.528f).dp)`.

## LocalGlassContentColor

```kotlin
public val LocalGlassContentColor: ProvidableCompositionLocal<Color> =
    compositionLocalOf { GlassDefaults.LightContentColor }
```

The foreground for text and icons on glass: `#111113` on light glass, white on dark glass. `GlassScene` provides it for the scene's appearance (to everything inside the scene, the content layer included). `GlassSurface`, a standalone `GlassButton`, `GlassToolbar`, `GlassTabBar` and the `GlassMenu` panel provide it again for their own appearance and tint: with a tint, in either appearance, the color is whichever of `#111113` and white has the higher WCAG contrast with the tinted glass, `tint × (body + (1 − body)·0.5)` with body 0.78 Regular and 0.68 Clear (white on blue, red or purple; `#111113` on yellow, mint, green or orange), as on the other platforms. A `GlassButton` inside a `GlassToolbar` does not: it keeps the toolbar's color. `GlassText` and `GlassIcon` read it. Material `Text` and `Icon` do not (they read Material's `LocalContentColor`): pass the color, or provide `LocalContentColor provides LocalGlassContentColor.current` inside the surface.

```kotlin
GlassSurface(material = GlassMaterial.Regular) {
    val secondary = LocalGlassContentColor.current.copy(alpha = 0.65f)   // read inside the surface
    Column(Modifier.padding(16.dp)) {
        GlassText("Subtitle", color = secondary)
        // Your own text. With Material 3 (androidx.compose.material3:material3): Text("Hello", color = LocalGlassContentColor.current)
        BasicText("Hello", style = TextStyle(color = LocalGlassContentColor.current))
    }
}
```

## GlassScene

The region glass may refract: a `Box` holding one `GlassContent` (the backdrop) plus any glass. Its options are inherited by every surface in the scene; each component can override them. Open `GlassMenu` panels draw in the outermost scene's overlay, above everything.

```kotlin
@Composable
public fun GlassScene(
    modifier: Modifier = Modifier,
    material: GlassMaterial = GlassMaterial.Clear,
    appearance: GlassAppearance = GlassAppearance.Automatic,
    refraction: Dp = Dp.Unspecified,
    motion: GlassMotion = GlassMotion.Full,
    reduceTransparency: Boolean = false,
    increaseContrast: Boolean = GlassDefaults.systemIncreaseContrast(),
    content: @Composable BoxScope.() -> Unit,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `modifier` | `Modifier` | Modifier of the scene's `Box`; usually `Modifier.fillMaxSize()`. |
| `material` | `Clear` | Default material of surfaces in the scene. |
| `appearance` | `Automatic` | Light or dark glass; `Automatic` follows `isSystemInDarkTheme()` (not your Material theme). Outside any scene glass also follows it. Every option may change at runtime: glass already on screen follows in the same frame. |
| `refraction` | `Dp.Unspecified` | Optical travel at the rim of surfaces that set none. Unspecified, each follows its resting size, `min(60 dp, 0.75 × its shorter side)`; a value here is used as given by all of them. |
| `motion` | `Full` | How much glass animates; the system "Remove animations" setting always lowers `Full` to `Reduced`. |
| `reduceTransparency` | false | Replaces every material with an opaque fill (`#F2F2F7` light / `#1C1C1E` dark at 94%), keeping the rim light. Android has no system setting for it: expose it in your app. |
| `increaseContrast` | system | Adds a 1 dp inner border (black 18% light, white 28% dark) and forces the Regular material. |
| `content` | | `BoxScope`: one `GlassContent` first, then glass. Use `Modifier.align(...)` to place children. |

A nested `GlassScene` (inside another scene's `GlassContent`) renders its own glass into its content, and the result becomes the outer scene's backdrop. Nested scenes do **not** inherit the outer scene's options: pass every option you set (`material`, `appearance`, `refraction`, `motion`, `reduceTransparency`, `increaseContrast`) to each.

```kotlin
GlassScene(Modifier.fillMaxSize(), appearance = GlassAppearance.Dark) {
    GlassContent(Modifier.fillMaxSize()) { Image(painterResource(R.drawable.photo), null, Modifier.fillMaxSize(), contentScale = ContentScale.Crop) }
    GlassButton(Icons.Filled.Share, "Share", onClick = {}, modifier = Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(16.dp))
}
```

## GlassContent

The backdrop layer of the enclosing scene: everything glass in the scene refracts it. Any Compose UI works, including `LazyColumn` and other scrolling content, animation, Material screens and images; the glass follows it every frame. Outside a scene it is a plain `Box`.

```kotlin
@Composable
public fun GlassContent(modifier: Modifier = Modifier, content: @Composable BoxScope.() -> Unit)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `modifier` | `Modifier` | Usually `Modifier.fillMaxSize()`. The glass is drawn inside this layer, so glass beyond its bounds is cut off, and glass entirely outside it draws the fallback material. |
| `content` | | The backdrop. It must paint opaque pixels wherever glass sits: ancestors' backgrounds are not part of the layer, and over transparent pixels untinted glass draws only its rim. |

One per scene, first in the scene's content. Glass placed inside the content cannot refract the layer it belongs to and draws the fallback material; nest a `GlassScene` there instead.

```kotlin
GlassContent(Modifier.fillMaxSize()) {
    LazyColumn(Modifier.fillMaxSize().background(Color(0xFFF2F2F7))) { items(50) { i -> ArticleRow(i) } }   // your row composable
}
```

## Modifier.liquidGlass

Makes any element a Liquid Glass surface. Inside a scene the glass refracts the scene's content at the element's exact position every frame; the element's children draw above it, unrefracted. Outside a scene, or below Android 13, it draws the fallback material.

```kotlin
@Stable
public fun Modifier.liquidGlass(
    shape: GlassShape = GlassShape(GlassDefaults.SurfaceRadius),
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = false,
    motion: GlassMotion? = null,
    opacity: Float = 1f,
    interactionSource: InteractionSource? = null,
): Modifier
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `shape` | `GlassShape(16.dp)` | Outline, continuous corners. |
| `material` | null (scene's) | `Clear` or `Regular`. |
| `appearance` | null (scene's) | Light or dark. |
| `tint` | `Color.Unspecified` | Colors the glass, never the content. Any specified color tints fully (its alpha is ignored), so use `Color.Unspecified`, never `Color.Transparent`, for "no tint". Converted to sRGB first. |
| `refraction` | `Dp.Unspecified` | Optical travel at the rim, used as given. Unspecified takes the scene's, or when the scene sets none `min(60 dp, 0.75 × the shorter side)` of the element's layout size, so press growth and stretch never change it. |
| `interactive` | false | Press grow (by `min(16%, 8 dp / size)`), drag stretch toward the finger, glow under the pointer (35% on mouse hover). False keeps the glass fully static, also with an `interactionSource`. |
| `motion` | null (scene's) | Overrides the scene's motion; the system setting still applies. |
| `opacity` | 1f | Multiplies the glass, for fading it. Alpha on a layer between the scene and the surface (`AnimatedVisibility` fades, `Crossfade`, `AnimatedContent`, `NavHost` transitions) fades only the children, never the glass; alpha on a layer around the whole `GlassScene` fades everything. |
| `interactionSource` | null | When given and `interactive` is true, its `PressInteraction`s drive the press instead of raw pointer input (so a scroll that cancels the click cancels the press), and its hover interactions drive the hover glow. With `interactive = false` it is ignored. |

Order it like a background: after the modifiers that size and place the element, before `clickable` and inner `padding`. Glass follows translation and scale of any ancestor (layout, `offset`, `graphicsLayer` in lambda or parameter form, `Modifier.scale`, scrolling), and under a scale it is a scaled copy (corners, refraction and glow scale too); rotation is not supported. The modifier only observes pointer input, so `clickable`, `draggable` and `pointerInput` keep working. It does not provide `LocalGlassContentColor`; use `GlassSurface` when the content must follow a per-surface appearance or tint. Do not add `background`, `blur` or `shadow` modifiers to the element to strengthen the glass: they cover or smear it.

Color: the shader converts every sample to gamma-encoded, unpremultiplied sRGB for the tone math and back: the optics run on the sRGB gamut, so on a wide-gamut window the glass shows sRGB colors while pixels no glass term reaches pass through unconverted. Tints given in other color spaces are converted to sRGB.

```kotlin
val source = remember { MutableInteractionSource() }
Box(
    Modifier
        .size(64.dp)
        .liquidGlass(GlassShape.Circle, interactive = true, interactionSource = source)
        .clickable(source, indication = null, role = Role.Button) { onRecord() },
    contentAlignment = Alignment.Center,
) { GlassIcon(Icons.Filled.Add, contentDescription = "Record") }

// Fading glass in and out:
val alpha by animateFloatAsState(if (visible) 1f else 0f, label = "glass")
Box(Modifier.size(200.dp, 56.dp).liquidGlass(GlassShape.Capsule, opacity = alpha))
```

## GlassSurface

A glass container: a `Box` with `Modifier.liquidGlass` applied, whose content gets `LocalGlassContentColor` for the surface's appearance and tint.

```kotlin
@Composable
public fun GlassSurface(
    modifier: Modifier = Modifier,
    shape: GlassShape = GlassShape(GlassDefaults.SurfaceRadius),
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = false,
    motion: GlassMotion? = null,
    contentAlignment: Alignment = Alignment.TopStart,
    content: @Composable BoxScope.() -> Unit,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `modifier` | `Modifier` | Give it a size or let the content size it; place it with `align`, `padding`, `offset`. |
| `shape` | `GlassShape(16.dp)` | Outline. |
| `material`, `appearance` | null (scene's) | |
| `tint` | `Color.Unspecified` | Colors the glass; content turns `#111113` or white, whichever has the higher contrast with the tinted glass. |
| `refraction` | `Dp.Unspecified` | The scene's, else `min(60 dp, 0.75 × the shorter side)`. |
| `interactive` | false | Press grow, drag stretch, glow. |
| `motion` | null | Overrides the scene's motion. |
| `contentAlignment` | `Alignment.TopStart` | Alignment of the content inside the surface. |
| `content` | | `BoxScope`, drawn above the glass. |

`GlassSurface` has no `opacity` or `interactionSource` parameter: use `Modifier.liquidGlass` on a `Box` for those.

```kotlin
GlassSurface(
    Modifier.align(Alignment.Center).padding(24.dp).fillMaxWidth(),
    shape = GlassShape(32.dp),
    material = GlassMaterial.Regular,
) {
    GlassText("Now Playing", Modifier.padding(24.dp))
}
```

## GlassText

Text on glass, colored with `LocalGlassContentColor` and set in the glass label style (17 sp semibold) by default. Font scaling applies; overflow is always an ellipsis.

```kotlin
@Composable
public fun GlassText(
    text: String,
    modifier: Modifier = Modifier,
    style: TextStyle = GlassDefaults.LabelTextStyle,
    color: Color = Color.Unspecified,
    maxLines: Int = Int.MAX_VALUE,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `style` | `GlassDefaults.LabelTextStyle` | Pass your own `TextStyle` for titles and body text (`TextStyle(fontSize = 15.sp)` is regular weight). Alignment, line height and so on come from the style (`style = … .copy(textAlign = TextAlign.Center)`). |
| `color` | `Color.Unspecified` | Resolution: `color` if specified, else `style.color` if specified, else `LocalGlassContentColor.current`. |
| `maxLines` | `Int.MAX_VALUE` | |

```kotlin
GlassText("Liquid Glass", style = TextStyle(fontSize = 28.sp, fontWeight = FontWeight.Bold))
GlassText("Secondary", color = LocalGlassContentColor.current.copy(alpha = 0.6f), maxLines = 1)
```

## GlassIcon

An icon on glass, tinted with `LocalGlassContentColor`. Two overloads.

```kotlin
@Composable
public fun GlassIcon(
    imageVector: ImageVector,
    contentDescription: String?,
    modifier: Modifier = Modifier,
    tint: Color = LocalGlassContentColor.current,
    size: Dp = 22.dp,
)

@Composable
public fun GlassIcon(
    painter: Painter,
    contentDescription: String?,
    modifier: Modifier = Modifier,
    tint: Color = LocalGlassContentColor.current,
    size: Dp = 22.dp,
)
```

`contentDescription` has no default: pass `null` when decorative (for example next to a label). On the `Painter` overload, `tint = Color.Unspecified` draws the painter's own colors.

```kotlin
GlassIcon(Icons.Filled.PlayArrow, contentDescription = null)
GlassIcon(painterResource(R.drawable.logo), contentDescription = "Logo", tint = Color.Unspecified, size = 28.dp)
```

## GlassButtonSize

Height, padding, label style and icon size of a `GlassButton`.

```kotlin
@Immutable
public class GlassButtonSize private constructor(
    public val height: Dp,               // minimum height; icon-only buttons are circles of this diameter; grows with font scale
    public val horizontalPadding: Dp,    // horizontal padding of labeled buttons
    public val textStyle: TextStyle,
    public val iconSize: Dp,
) {
    public companion object {
        public val Small: GlassButtonSize    // 34.dp, 14.dp, TextStyle(fontSize = 15.sp, fontWeight = FontWeight.SemiBold), 18.dp
        public val Regular: GlassButtonSize  // 44.dp, 18.dp, GlassDefaults.LabelTextStyle, 22.dp   (the default)
        public val Large: GlassButtonSize    // 52.dp, 22.dp, GlassDefaults.LabelTextStyle, 24.dp
    }
    override fun toString(): String
}
```

The constructor is private: use the three presets. Read `size.textStyle` / `size.iconSize` when you build custom button content that should match.

## GlassButton

An interactive glass capsule that grows under the finger, stretches toward a drag and lights up where it is touched. Plays a `VIRTUAL_KEY` haptic on press, has the Button role and a 2 dp focus ring (`#0A84FF`) for keyboard focus; a disabled button, glass and content, is drawn at 45%. Inside a `GlassToolbar` it becomes a plain toolbar item: no glass of its own, 44 dp tall, 12 dp horizontal padding, and its `tint`, `material`, `appearance`, `shape`, `refraction`, `interactive`, `motion` and `size` height are ignored; it keeps the toolbar's foreground color. To tint a toolbar action, place a standalone `GlassButton` beside the toolbar. Content is laid out in a `Row` with 6 dp spacing, centered.

### Content overload

```kotlin
@Composable
public fun GlassButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    size: GlassButtonSize = GlassButtonSize.Regular,
    shape: GlassShape = GlassShape.Capsule,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
    interactionSource: MutableInteractionSource? = null,
    content: @Composable RowScope.() -> Unit,
)
```

### Text overload

```kotlin
@Composable
public fun GlassButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    icon: ImageVector? = null,
    enabled: Boolean = true,
    size: GlassButtonSize = GlassButtonSize.Regular,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    interactionSource: MutableInteractionSource? = null,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
)
```

### Icon-only overload (a circle)

```kotlin
@Composable
public fun GlassButton(
    icon: ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    size: GlassButtonSize = GlassButtonSize.Regular,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    interactionSource: MutableInteractionSource? = null,
    refraction: Dp = Dp.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `onClick` | | Called on click. |
| `enabled` | true | Disabled buttons do not respond, do not animate, and dim to 45% (glass and content). |
| `size` | `GlassButtonSize.Regular` | 34 / 44 / 52 dp. |
| `shape` (content overload) | `GlassShape.Capsule` | The icon overload always uses `GlassShape.Circle`. |
| `material`, `appearance` | null (scene's) | |
| `tint` | `Color.Unspecified` | Makes a prominent button: the glass takes the color, and the label turns white or `#111113`, whichever has the higher WCAG contrast with the tinted glass (white on blue, red or purple; dark on yellow, mint, green or orange). |
| `refraction` | `Dp.Unspecified` | The scene's, else the button's size: 25.5 / 33 / 39 dp for the 34 / 44 / 52 dp heights. |
| `interactive` | true | Grow, stretch and glow under the finger. `false` gives a static glass button that still clicks: `GlassButton("Static", onClick = {}, interactive = false)`. |
| `motion` | null | |
| `interactionSource` | null | Source of the button's interactions; its press drives the glass. |
| `icon` (text overload) | null | Leading icon, drawn at `size.iconSize` with no content description. |
| `contentDescription` (icon overload) | required | What the button does, for TalkBack. |
| `content` (content overload) | | `RowScope`; use `GlassText` and `GlassIcon`. |

Overload resolution: `GlassButton("Play", onClick = {})` is the text overload; `GlassButton(Icons.Filled.Add, "Add", onClick = {})` is the icon-only overload; `GlassButton(onClick = {}) { … }` is the content overload.

```kotlin
GlassButton(onClick = {}) { GlassIcon(Icons.Filled.Add, null); GlassText("Add") }
GlassButton("Continue", onClick = {}, size = GlassButtonSize.Large)
GlassButton("Play", onClick = {}, icon = Icons.Filled.PlayArrow, tint = Color(0xFF0A84FF))
GlassButton(Icons.Filled.Share, contentDescription = "Share", onClick = {})
GlassButton("Delete", onClick = {}, enabled = false)
```

## GlassToolbar and GlassToolbarSpacer

A floating 52 dp glass capsule holding buttons. `GlassButton`s inside become toolbar items on the toolbar's glass. A `GlassToolbarSpacer` splits the items into clusters, each its own glass capsule (4 dp inset around its items); flexible spacers share the toolbar's free width; adjacent spacers merge into one gap. Without a flexible spacer, the clusters are centered. With `interactive` glass, the cluster under the finger grows and lights up, without consuming the touch.

```kotlin
@Composable
public fun GlassToolbar(
    modifier: Modifier = Modifier,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
    content: @Composable () -> Unit,
)

@Composable
public fun GlassToolbarSpacer(minWidth: Dp = 8.dp, flexible: Boolean = true)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `modifier` | `Modifier` | `fillMaxWidth()` lets flexible spacers spread the clusters; without it the toolbar wraps its items. |
| `material`, `appearance` | null (scene's) | |
| `tint` | `Color.Unspecified` | Colors every cluster (avoid: every item would look primary). Items' own `tint` is ignored. |
| `interactive` | true | Press grow, drag stretch and glow of the clusters. |
| `motion` | null | |
| `content` | | Plain `@Composable () -> Unit` (not a `RowScope`): items and spacers in order. Any composable is an item (a `GlassMenu` with an icon trigger, a `GlassText` title); items are centered vertically. |
| `minWidth` (spacer) | 8.dp | The narrowest the gap gets. |
| `flexible` (spacer) | true | Whether the gap grows to fill the toolbar. Use `GlassToolbarSpacer(flexible = false)` for a fixed 8 dp gap between two clusters. |

```kotlin
GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).fillMaxWidth()) {
    GlassButton(Icons.AutoMirrored.Filled.ArrowBack, "Back", onClick = {})
    GlassToolbarSpacer()
    GlassButton(Icons.Filled.Share, "Share", onClick = {})
    GlassButton(Icons.Filled.MoreVert, "More", onClick = {})
}
```

## GlassTab and GlassTabBar

A 62 dp glass capsule of equal-width tabs with a glass lens marking the selection. The lens slides to a newly selected tab on the track spring, lifting into glass of the bar's own material (a Regular bar stays frosted under the selection) and stretching with its speed; dragging across the bar moves the lens and selects on release (haptic tick per tab crossed). At rest the lens is a soft fill (foreground at 9% light / 14% dark). Arrow keys move the selection; the focused tab shows a 2 dp focus ring. The bar is a selectable group of `Role.Tab` items.

```kotlin
@Immutable
public class GlassTab(public val label: String, public val icon: ImageVector? = null) {
    override fun equals(other: Any?): Boolean
    override fun hashCode(): Int
    override fun toString(): String
}

@Composable
public fun GlassTabBar(
    tabs: List<GlassTab>,
    selectedIndex: Int,
    onSelectedIndexChange: (Int) -> Unit,
    modifier: Modifier = Modifier,
    material: GlassMaterial? = null,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    selectedContentColor: Color = Color.Unspecified,
    unselectedContentColor: Color = Color.Unspecified,
    interactive: Boolean = true,
    motion: GlassMotion? = null,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `tabs` | | At least one (`require`). `GlassTab(label, icon)`: icon 24 dp over a 10 sp semibold label; with `icon = null` the tab is label-only at 15 sp semibold, which makes a segmented control. The label is what TalkBack reads. |
| `selectedIndex` | | Coerced into range for drawing. The bar is controlled: it only changes when you update this. |
| `onSelectedIndexChange` | | Called with the new index on tap, drag release or arrow key. Not called when the tapped tab is already selected. |
| `modifier` | `Modifier` | The bar spans the width it is given (use `fillMaxWidth()` with side padding, or `width(…)` for a compact bar); height is at least 62 dp. |
| `material`, `appearance` | null (scene's) | Regular over text and busy content; `appearance = GlassAppearance.Dark` over dark imagery. |
| `tint` | `Color.Unspecified` | Colors the bar's glass. |
| `selectedContentColor` | `Color.Unspecified` | Icon and label color of the selected tab; unspecified uses the glass foreground. |
| `unselectedContentColor` | `Color.Unspecified` | Icon and label color of the other tabs; unspecified uses the foreground at 85%, legible over mid-tone backdrops. |
| `interactive` | true | Whether the bar's glass grows and lights up under the finger. |
| `motion` | null | |

```kotlin
val tabs = remember { listOf(GlassTab("Home", Icons.Filled.Home), GlassTab("Search", Icons.Filled.Search), GlassTab("Profile", Icons.Filled.Person)) }
var tab by rememberSaveable { mutableIntStateOf(0) }
GlassTabBar(
    tabs = tabs,
    selectedIndex = tab,
    onSelectedIndexChange = { tab = it },
    modifier = Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(start = 20.dp, end = 20.dp, bottom = 12.dp).fillMaxWidth(),
    material = GlassMaterial.Regular,
)
// Segmented control:
GlassTabBar(listOf(GlassTab("Day"), GlassTab("Week"), GlassTab("Month")), range, { range = it }, Modifier.fillMaxWidth())
```

## GlassSlider

A Liquid Glass slider, 44 dp tall, filling the width it is given. At rest the thumb is an opaque white 20 × 16 dp capsule with a soft shadow; while pressed it lifts into Clear glass (growing 2.5 × 2 dp per side, refraction 5 dp) that magnifies the 6 dp track beneath it, and stretches with its speed. Drag the thumb, or touch the track to jump there. Only horizontal drags belong to the slider, so vertical scrolling over it still works. Keyboard arrows step it (1% of the range, or one step); TalkBack can set it directly. Haptic ticks at each step and at the ends.

```kotlin
@Composable
public fun GlassSlider(
    value: Float,
    onValueChange: (Float) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    valueRange: ClosedFloatingPointRange<Float> = 0f..1f,
    steps: Int = 0,
    onValueChangeFinished: (() -> Unit)? = null,
    tint: Color = Color.Unspecified,
    appearance: GlassAppearance? = null,
    motion: GlassMotion? = null,
    label: String? = null,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `value` | | Current value, within `valueRange`. Controlled: update it in `onValueChange`. |
| `onValueChange` | | Called with each new value while dragging (snapped to steps). |
| `modifier` | `Modifier` | `fillMaxWidth()` and `height(44.dp)` are applied after it; constrain the width with `width(…)` or a parent. TalkBack reads the value as a percentage of the range: when it means something else, add `semantics { stateDescription = "60 dp" }`. |
| `enabled` | true | A disabled slider (track, fill and thumb) is dimmed to 45%. |
| `valueRange` | `0f..1f` | Must not be empty (`require`). |
| `steps` | 0 | Discrete values between the ends (0 = continuous); each is a haptic detent. Must be ≥ 0 (`require`). `valueRange = 0f..5f, steps = 4` gives 0, 1, 2, 3, 4, 5. |
| `onValueChangeFinished` | null | Called when a drag, tap, key press or accessibility action ends. |
| `tint` | `Color.Unspecified` | Color of the filled track; unspecified uses the platform accent (Material You `system_accent1_600` light / `_200` dark on Android 12+, `#007AFF` / `#0A84FF` before). |
| `appearance` | null | Light or dark glass for the thumb. |
| `motion` | null | |
| `label` | null | What the slider sets, read by TalkBack. Give every slider one. |

The thumb lives in a scene private to the slider, so it works on a glass card, outside any scene, and does not count toward the scene's surface budget. It takes the enclosing scene's style, and outside any scene the system's (dark theme, "Remove animations", contrast).

```kotlin
var volume by rememberSaveable { mutableFloatStateOf(0.6f) }
GlassSlider(volume, { volume = it }, tint = Color(0xFF0A84FF), label = "Volume")
GlassSlider(level, { level = it }, Modifier.semantics { stateDescription = "Level ${level.roundToInt()}" }, valueRange = 0f..5f, steps = 4, label = "Level")
```

## GlassSwitch

A Liquid Glass switch: a 54 × 24 dp track with a 32 × 20 dp thumb on the measured switch spring (response 0.47 s, damping 0.7). Pressed, the thumb lifts into Clear glass (growing 9 × 5.5 dp per side, refraction 7.75 dp) that magnifies the track; drag it across to toggle, or tap. It occupies 54 × 44 dp; the extra height is the touch target. `Role.Switch` with toggle state; Enter, Space and NumPadEnter toggle it; `TOGGLE_ON` / `TOGGLE_OFF` haptics on Android 14+.

```kotlin
@Composable
public fun GlassSwitch(
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    tint: Color = Color.Unspecified,
    appearance: GlassAppearance? = null,
    motion: GlassMotion? = null,
    label: String? = null,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `checked` | | Controlled: the thumb returns to `checked` unless you update it, so a rejected change snaps back. |
| `onCheckedChange` | | Called with the new state. |
| `modifier` | `Modifier` | `size(54.dp, 44.dp)` is applied after it. |
| `enabled` | true | A disabled switch (track and thumb) is dimmed to 45%. |
| `tint` | `Color.Unspecified` | Track color when on; unspecified uses the platform accent. The off track is gray (`#787880` at 32% light / 40% dark). |
| `appearance` | null | Light or dark glass for the thumb. |
| `motion` | null | |
| `label` | null | What the switch controls, read by TalkBack with its state ("Wi-Fi, switch, on"). Hide a visible label beside it with `clearAndSetSemantics {}`. |

A vertical scroll that claims the finger cancels the press instead of toggling, so switches work inside scrolling lists. Like the slider, the thumb has its own private scene, and outside any scene it follows the system's dark theme.

```kotlin
var wifi by rememberSaveable { mutableStateOf(true) }
Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
    GlassText("Wi-Fi", Modifier.weight(1f).clearAndSetSemantics {}, style = TextStyle(fontSize = 17.sp))
    GlassSwitch(wifi, { wifi = it }, tint = Color(0xFF30D158), label = "Wi-Fi")
}
```

## GlassMenu, GlassMenuItem and GlassMenuDivider

A menu that grows out of its trigger: a Regular glass panel morphing from the trigger's outline on the morph spring (0.42 s, bounce 0.18), covering it, and collapsing back into it when dismissed. The trigger fades out as the panel grows. Opened from a `GlassToolbar` item, the panel grows out of and covers the item's whole cluster. Tapping outside, Back and Escape dismiss it; choosing an item runs its action and dismisses it. The panel is a pane titled "Menu" for TalkBack, takes focus when it opens and returns focus to the trigger for keyboard users when it closes.

The panel's glass is mixed halfway toward a solid backing, `#F5F5F7` light and `#1C1C1E` dark, after the material color and tint (the legibility backing of `spec/OPTICS.md` §3), so the labels of an untinted Regular panel keep at least 5:1 contrast over any backdrop (5.09:1 white over white in dark, 7.15:1 `#111113` over black in light; Regular glass alone gives 1.8:1 and 2.1:1 there). It still refracts and frosts the backdrop, at half strength. The fallback material folds the same backing into its fill. Only menu panels have it, and it is not an option.

Inside a `GlassScene` the panel is drawn in the **outermost** scene's overlay, above every nested scene's glass, and refracts that scene. It is placed over the trigger (aligned to the trigger's top, or bottom when there is no room, and to its nearer side) and kept inside the safe drawing insets plus an 8 dp margin. Outside a scene it opens in a `Popup` with the fallback material, following the system's dark theme unless you pass `appearance`.

```kotlin
@Composable
public fun GlassMenu(
    expanded: Boolean,
    onDismissRequest: () -> Unit,
    trigger: @Composable () -> Unit,
    modifier: Modifier = Modifier,
    material: GlassMaterial = GlassMaterial.Regular,
    appearance: GlassAppearance? = null,
    tint: Color = Color.Unspecified,
    shape: GlassShape = GlassShape(GlassDefaults.MenuRadius),
    motion: GlassMotion? = null,
    content: @Composable ColumnScope.() -> Unit,
)

@Composable
public fun GlassMenuItem(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    icon: ImageVector? = null,
    enabled: Boolean = true,
    destructive: Boolean = false,
    checked: Boolean? = null,
)

@Composable
public fun GlassMenuDivider()
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `expanded` | | Whether the menu is open. Keep it in `remember`, not `rememberSaveable`. |
| `onDismissRequest` | | Called on outside tap, Back, Escape, TalkBack "collapse" and after an item is chosen. Set `expanded = false` here. |
| `trigger` | | The control that opens the menu, usually a `GlassButton` whose `onClick` sets `expanded = true`. The panel grows out of its bounds. |
| `modifier` | `Modifier` | Modifier of the **panel**, not the trigger. The panel is as wide as its widest item, at least 220 dp and at most the safe area's width minus 8 dp margins (`Modifier.width(280.dp)` fixes it), with a 6 dp inset; taller than the safe area, it scrolls. To place the trigger, wrap the `GlassMenu` in a `Box(Modifier.align(…))` or put it in a `Row` / `GlassToolbar`. |
| `material` | `GlassMaterial.Regular` | The panel's material; the legibility backing applies to either. |
| `appearance` | null | |
| `tint` | `Color.Unspecified` | Colors the panel's glass. |
| `shape` | `GlassShape(28.dp)` | The panel's outline. |
| `motion` | null | `None` opens and closes instantly. |
| `content` | | `ColumnScope`: `GlassMenuItem`, `GlassMenuDivider` or any composable. `LocalGlassContentColor` is the panel's foreground. |
| `text` (item) | | Label, 17 sp regular; long labels and large fonts wrap. |
| `onClick` (item) | | The action; the menu then dismisses itself. |
| `icon` (item) | null | Leading 20 dp icon. |
| `enabled` (item) | true | Disabled items are drawn at 40% alpha and cannot be chosen. |
| `destructive` (item) | false | Draws the item in red (`#FF3B30`), for actions like Delete. |
| `checked` (item) | null | null for an action (`Role.Button`); `true` / `false` makes it a choice (`Role.Checkbox` with toggle state) and shows a check mark when true. |

Items are at least 44 dp tall (more with large fonts), 14 dp horizontal padding, highlighted with the foreground at 8% while pressed or focused (plus a 2 dp focus ring for keyboard focus), in 22 dp corners (concentric with the 28 dp panel and 6 dp inset). `GlassMenuDivider` is a 1 dp line in the foreground at 12%, padded 14 dp horizontally and 4 dp vertically.

```kotlin
var open by remember { mutableStateOf(false) }
GlassMenu(
    expanded = open,
    onDismissRequest = { open = false },
    trigger = { GlassButton(Icons.Filled.MoreVert, "More", onClick = { open = true }) },
) {
    GlassMenuItem("Name", onClick = { sort = 0 }, checked = sort == 0)
    GlassMenuItem("Date", onClick = { sort = 1 }, checked = sort == 1)
    GlassMenuDivider()
    GlassMenuItem("Delete", onClick = { delete() }, icon = Icons.Filled.Delete, destructive = true)
}
```

## GlassProgressiveBlur

Progressive edge blur over the scene's `GlassContent`, as content scrolls under a toolbar or tab bar: sharp on the inner side, blurring smoothly to `blur` at `edge` (six blur levels with σᵢ = blur·(i/6)², mixed by depth). The blur becomes part of the backdrop, so glass over the band refracts the blurred content.

```kotlin
@Composable
public fun GlassProgressiveBlur(
    edge: GlassEdge,
    modifier: Modifier = Modifier,
    size: Dp = GlassDefaults.ProgressiveBlurSize,
    blur: Dp = GlassDefaults.ProgressiveBlurRadius,
)
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `edge` | | The edge blurred most. |
| `modifier` | `Modifier` | Place it along the matching edge of the scene: `Modifier.align(Alignment.TopCenter)` for `Top`, `BottomCenter` for `Bottom`, `CenterStart` / `CenterEnd` for `Start` / `End`. |
| `size` | 80.dp | Depth of the band from the edge (its height at the top or bottom, its width at the start or end). The band spans the full length of the edge (`fillMaxWidth` or `fillMaxHeight`) unless the modifier sizes it. Include the system bar inset: `size = topInset + 96.dp`. |
| `blur` | 20.dp | Gaussian σ at the edge. |

Put it in the scene (a sibling of the glass), not in `GlassContent`. Needs Android 12 for the platform blur; Android 7 to 11 fade the band toward the edge (white at 60% light, black at 45% dark) instead.

```kotlin
val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
GlassScene(Modifier.fillMaxSize()) {
    GlassContent(Modifier.fillMaxSize()) { LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(top = top + 76.dp)) { /* … */ } }
    GlassProgressiveBlur(GlassEdge.Top, Modifier.align(Alignment.TopCenter), size = top + 96.dp, blur = 18.dp)
    GlassToolbar(Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(horizontal = 16.dp, vertical = 8.dp).fillMaxWidth()) { /* … */ }
}
```

## Fixed metrics

Internal constants that shape layout (not configurable; plan around them).

| Component | Metric |
| --- | --- |
| `GlassButton` | Heights 34 / 44 / 52 dp (min; grows with font scale), min width = height, horizontal padding 14 / 18 / 22 dp, icon 18 / 22 / 24 dp, content spacing 6 dp, disabled alpha 0.45 (glass and content), focus ring 2 dp `#0A84FF` |
| `GlassToolbar` | Height 52 dp (or tallest item + 8 dp), cluster inset 4 dp, items 44 dp tall with 12 dp horizontal padding (labeled); unless the scene sets one, cluster refraction 39 dp (0.75 × 52) |
| `GlassTabBar` | Height ≥ 62 dp, inset 4 dp, 4 dp horizontal padding per tab, icon 24 dp, label 10 sp semibold (15 sp label-only, one line), spacing 2 dp, unselected alpha 0.85; unless the scene sets one, refraction 46.5 dp for the bar and 40.5 dp for its 54 dp lens (measured at rest) |
| `GlassSlider` | Height 44 dp, track 6 dp (`#808080` at 30%), thumb 20 × 16 dp, grab radius 22 dp |
| `GlassSwitch` | Box 54 × 44 dp, track 54 × 24 dp, thumb 32 × 20 dp, 2 dp inset |
| `GlassMenu` | Panel 220 dp to the safe width (fits its widest item), 28 dp radius, 6 dp inset; item ≥ 44 dp, 22 dp corners, 17 sp text (wraps), 20 dp icon, 18 dp check mark |
| `GlassText` / `GlassIcon` | 17 sp semibold / 22 dp |

## Surface budget

A scene refracts up to `GlassDefaults.MaxSurfacesPerScene` (16) surfaces on the GPU at once. What counts, per scene:

| Element | Surfaces |
| --- | --- |
| `GlassSurface`, `Modifier.liquidGlass`, standalone `GlassButton` | 1 each |
| `GlassButton` inside a `GlassToolbar` | 0 (the cluster is the glass) |
| `GlassToolbar` | 1 per cluster |
| `GlassTabBar` | 1 (the bar), 2 while the lens is lifted (pressed or sliding) |
| `GlassMenu` while open or closing | 1, in the outermost scene |
| `GlassSlider`, `GlassSwitch` | 0 (their thumbs live in a private scene) |
| Surfaces off the content layer (they draw the fallback material), fully clipped away (scrolled out of a list) or faded out (`opacity` 0) | 0 |

Surfaces are counted in z-order: shallower first, then the order they were attached. Beyond the limit the rest draw the fallback material (Regular ones over a platform blur, Clear ones a translucent fill). Tab-bar lenses (one level deeper than their bar) and open menus (overlay depth) come last, so they are the first to fall back: keep the outermost scene at 15 or fewer surfaces so a menu always gets the GPU path. Nest scenes to go further.

## Resources

The library ships two strings; override them in your app's `res/values*/strings.xml` to change or localize them:

```xml
<string name="liquidglass_menu">Menu</string>                  <!-- announced when a GlassMenu opens -->
<string name="liquidglass_dismiss_menu">Close menu</string>    <!-- the outside-tap area of an open menu -->
```

## Preconditions

| Call | Throws `IllegalArgumentException` when |
| --- | --- |
| `GlassShape(radius)` | `radius` is negative or not finite |
| `GlassTabBar(tabs, …)` | `tabs` is empty |
| `GlassSlider(…)` | `steps < 0`, or `valueRange.endInclusive <= valueRange.start` |

Glass itself never throws: wherever the GPU path cannot run, the fallback material draws, also for glass placed entirely outside its scene's `GlassContent` bounds.
