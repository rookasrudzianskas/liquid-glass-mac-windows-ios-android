---
name: liquid-glass-wpf
description: Builds Liquid Glass interfaces in WPF on Windows 10 and 11 with the LiquidGlassEngineering.Wpf NuGet package, whose GlassScene, GlassSurface, GlassButton, GlassToolbar, GlassTabBar, GlassSlider, GlassSwitch, GlassMenu and GlassProgressiveBlur refract a live backdrop through an HLSL pixel shader with Clear and Regular (frosted) materials, rim light and elastic press. Use when writing XAML or C# for liquid glass, glassmorphism, glass UI, refraction, blur, frosted or acrylic-style glass toolbars, tab bars, buttons, sliders, switches, menus or cards in a WPF app (.NET 8, .NET 10 or .NET Framework 4.8), or when WPF glass looks flat, opaque, misaligned or draws the fallback material.
license: MIT
metadata:
  homepage: https://liquid-glass-engineering.vercel.app
  repository: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android
---

# Liquid Glass for WPF

`LiquidGlassEngineering.Wpf` renders Liquid Glass natively in WPF: a 20-unit bevel that bends the backdrop, the
Clear and Regular materials, a one-pixel rim light and controls that grow, stretch and glow under the pointer. It
shares one optical model with the SwiftUI and Compose ports, so the same options give matching results.

## When to use

- A WPF app (XAML or C#) needs glass toolbars, tab bars, buttons, sliders, switches, menus or cards over imagery,
  video, maps or scrolling content.
- Existing WPF glass looks like plain translucency, draws an opaque fill, or lags behind moving content.
- Glass refracts only what is inside its `GlassScene` in this window. It cannot blur the desktop or other windows
  behind the app. For a Mica or Acrylic window background use DWM (`DWMWA_SYSTEMBACKDROP_TYPE`, Windows 11 22H2+), as
  the demo app does, and keep an opaque `Content` (image, gradient) under the glass.
- Not for WinUI 3, UWP, MAUI or Avalonia: this package is WPF only. Use the `liquid-glass-port` skill there.
- For *where* glass belongs and which material to pick, the `liquid-glass` skill holds the design rules; this skill
  applies them to WPF.

## Install

The packages are served from the project's static NuGet v3 feed (not on nuget.org yet). Add the feed once, then the
package (`LiquidGlassEngineering.Core` comes with it):

```powershell
dotnet nuget add source https://liquid-glass-engineering.vercel.app/nuget/index.json -n liquid-glass
dotnet add package LiquidGlassEngineering.Wpf
```

```xml
<!-- App.csproj -->
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>   <!-- or net10.0-windows, net48 (see below) -->
  <UseWPF>true</UseWPF>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="LiquidGlassEngineering.Wpf" Version="1.0.0" />
  <Resource Include="Assets\*.jpg" />                  <!-- backdrop images as WPF resources -->
</ItemGroup>
```

For a checked-in feed, put a `nuget.config` next to the solution (keep nuget.org in it):

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="liquid-glass" value="https://liquid-glass-engineering.vercel.app/nuget/index.json" />
  </packageSources>
</configuration>
```

If the repository uses `packageSourceMapping`, also map the packages to the feed:
`<packageSource key="liquid-glass"><package pattern="LiquidGlassEngineering.*" /></packageSource>`.

On `net48`, add `<LangVersion>latest</LangVersion>` to the app project: the C# in this skill uses C# 10 syntax
(file-scoped namespaces, lambda discards, target-typed `new`) and .NET Framework projects default to C# 7.3. Records
(recipe 6) also need a polyfill: `namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }`.
To build `net48` off Windows, add a `Microsoft.NETFramework.ReferenceAssemblies` package reference.

| Requirement | Value |
| --- | --- |
| Package targets | `net8.0-windows`, `net10.0-windows`, `net48` (Core is `netstandard2.0`) |
| OS | Windows 10 version 1809 or later, Windows 11 |
| GPU path | Pixel shader 3.0 in hardware (every DirectX 10-class GPU, also WARP), render tier 2, ≥ 1024 instruction slots |
| Otherwise | The fallback material renders automatically (no crash, never invisible) |
| XAML namespace | `xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"` (the controls; Core enums work as attribute values, see [api.md](references/api.md#namespaces-and-xaml) for `x:Static`) |
| C# namespaces | `using LiquidGlass;` (GlassMaterial, GlassMotion, GlassRadius) and `using LiquidGlass.Wpf;` (controls, GlassAppearance, GlassRenderMode) |

## Mental model: scene, content, surfaces

```
GlassScene                      the region glass may refract (a FrameworkElement, clips to bounds)
├─ Content                      ONE UIElement: the backdrop. Image, MediaElement, ScrollViewer, Canvas art...
│                               ← refracted. Stays interactive where nothing covers it.
└─ Children (XAML content)      laid out like a single-cell Grid, drawn over the backdrop
   ├─ GlassProgressiveBlur      blurred band of the backdrop  ← also refracted by glass above it
   ├─ GlassToolbar / GlassTabBar / GlassSurface / GlassButton / GlassMenu ...   ← glass
   │    └─ their own content    labels, icons, nested glass: sharp, on top, NOT refracted
   └─ plain elements            TextBlock, Border, Canvas: drawn over the backdrop, NOT refracted
(top layer)                     GlassMenu panels float here, above all children
```

1. **Scene** = `lg:GlassScene`. Glass sees only what is inside its scene. Use one per screen region (usually one
   per window or page). Glass outside any scene draws the opaque material.
2. **Content** = `GlassScene.Content`, the single backdrop element. Every surface keeps a live `VisualBrush` copy
   of it, aligned to the surface's exact screen position (also while it moves, scales or animates) and extended past
   the outline as far as refraction and frost reach. The pixel shader refracts and tones that copy.
3. **Surfaces** = any glass control among the scene's `Children`, at any depth (inside `Grid`, `Canvas`,
   `StackPanel`, a `ScrollViewer`). A surface's children draw above its glass, unrefracted. Glass inside another
   surface is *nested*: it refracts the backdrop plus the enclosing glass.
4. **Options inherit.** `lg:GlassScene.Material`, `.Appearance`, `.Motion`, `.RenderMode`, `.ReduceTransparency`
   and `.IncreaseContrast` are inherited attached properties: set them on the window, the scene, a panel or a single
   control; everything below follows.

## Minimal example

```xml
<Window x:Class="Demo.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Glass" Width="1000" Height="680" UseLayoutRounding="True">
  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Image Source="/Assets/wallpaper.jpg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality" />
    </lg:GlassScene.Content>

    <lg:GlassToolbar VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0">
      <Button Content="&#xE72B;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Back" ToolTip="Back" />
      <Button Content="Library" />
      <lg:GlassToolbarSpacer />
      <Button Content="&#xE72D;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Share" ToolTip="Share" />
    </lg:GlassToolbar>

    <lg:GlassSurface Material="Regular" Radius="28" Width="380" Padding="28"
                     HorizontalAlignment="Center" VerticalAlignment="Center">
      <StackPanel lg:GlassScene.Material="Clear">
        <TextBlock Text="Liquid Glass" FontSize="32" FontWeight="SemiBold" />
        <TextBlock Text="Refraction, frost and light." Opacity="0.75" Margin="0,6,0,18" TextWrapping="Wrap" />
        <lg:GlassButton Content="Get started" Size="Large" Tint="#0A84FF" HorizontalAlignment="Left" />
      </StackPanel>
    </lg:GlassSurface>

    <lg:GlassTabBar VerticalAlignment="Bottom" Margin="0,0,0,24" SelectedIndex="0" AutomationProperties.Name="Sections">
      <lg:GlassTabBarItem Content="Home" />
      <lg:GlassTabBarItem Content="Explore" />
      <lg:GlassTabBarItem Content="Library" />
    </lg:GlassTabBar>
  </lg:GlassScene>
</Window>
```

The same in C#:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiquidGlass;
using LiquidGlass.Wpf;

namespace Demo;

// MainWindow.xaml.cs; MainWindow.xaml is an empty <Window> with x:Class="Demo.MainWindow".
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var scene = new GlassScene
        {
            Content = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/wallpaper.jpg")), Stretch = Stretch.UniformToFill },
        };
        scene.Children.Add(new GlassSurface
        {
            Material = GlassMaterial.Regular,
            Radius = 28,                                   // double converts to GlassRadius implicitly
            Width = 380,
            Padding = new Thickness(28),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = "Liquid Glass", FontSize = 32 },
        });
        Content = scene;
    }
}
```

## Components

Full signatures, every default and template part: [references/api.md](references/api.md).

| Component (base) | Purpose | Key properties (default) |
| --- | --- | --- |
| `GlassScene` (`FrameworkElement`) | Region glass refracts; holds the backdrop | `Content` (null), `Children`; attached inherited `Material` (`Clear`), `Appearance` (`Automatic`), `Motion` (`Full`), `RenderMode` (`Automatic`), `ReduceTransparency` (`null` = follow Windows), `IncreaseContrast` (false); read-only `IsDark`, `ContentForeground`, `IsFocusVisible`; `InvalidateGlass()`, `FindScene(element)` |
| `GlassSurface` (`ContentControl`) | Glass container for cards, panels, captions, draggable lenses | `Material`, `Appearance`, `Motion` (inherited), `Radius` (16), `Refraction` (NaN: `min(60, 0.75 × shorter side)`), `Tint` (null), `IsInteractive` (false), `IsFluid` (false), `IsFocusRingVisible` (false), read-only `IsPressed`, `RenderedMode` |
| `GlassButton` (`Button`) | Interactive glass button; capsule, or circle when icon-only | `Size` (`Regular` = 32; `Small` 24, `Large` 40), `IsIconOnly` (false), `Radius` (`capsule`), `Refraction` (NaN: `min(60, 0.75 × shorter side)`), `Tint` (null), `IsInteractive` (true), `Material`, `Appearance`, `Motion` (inherited); takes the toolbar button look inside a `GlassToolbar` |
| `GlassToolbar` (`Control`) | Floating toolbar of capsule glass clusters, 40 tall | `Items` (content, `ObservableCollection<UIElement>`; no `ItemsSource`), `Orientation` (`Horizontal`), `Inset` (4), `Spacing` (4), `Radius` (`capsule`), `Refraction` (NaN: `min(60, 0.75 × shorter side)`), `Tint` (null), `IsInteractive` (true), `Material`, `Appearance`, `Motion` |
| `GlassToolbarSpacer` (`FrameworkElement`) | Splits a toolbar into clusters | `Sizing` (`Flexible` fills; `Fixed` = 8-unit gap) |
| `GlassTabBar` (`ListBox`) / `GlassTabBarItem` (`ListBoxItem`) | Capsule tab bar with a sliding, draggable selection lens in the bar's material | `SelectedIndex` (-1: always set it; once an item is selected one always is), `ItemsSource`, `SelectionChanged`, `Radius` (`capsule`), `Refraction` (NaN: `min(60, 0.75 × shorter side)`), `Tint` (null), `IsInteractive` (true), `Material`, `Appearance`, `Motion`; items 28 tall |
| `GlassSlider` (`Slider`) | 6-unit track, accent fill, white thumb lifting into glass while dragged | All `Slider` members; `Tint` (null = Windows accent); `IsMoveToPointEnabled` (true) |
| `GlassSwitch` (`ToggleButton`) | 54 × 24 switch, thumb lifts into glass while pressed; `Content` is the label | `IsChecked`, `Checked`/`Unchecked`; `Tint` (null = Windows accent) |
| `GlassMenu` (`HeaderedItemsControl`) | Glass trigger button whose Regular panel grows out of it, mixed halfway toward a solid backing so items stay legible | `Header`, `IsOpen` (false, two-way), `Placement` (`Bottom`), `PanelMaterial` (`Regular`), `PanelRadius` (28, a `GlassRadius`), `Size` (`Regular`), `IsIconOnly` (false), `Tint` (null), `Motion` (inherited), `Opened`/`Closed`, `Close()`; items are `GlassMenuItem` and `Separator` elements, or data from `ItemsSource` (with `ItemTemplate`, `ItemContainerStyle`) |
| `GlassMenuItem` (`ButtonBase`) | A menu command | `Header` (the label, kept current), `Content`, `Icon` (glyph string ok), `InputGestureText` (""), `IsCheckable` (false), `IsChecked` (false), `StaysOpenOnClick` (false), `Click`, `Command`, `Checked`/`Unchecked`; `Separator` between items |
| `GlassProgressiveBlur` (`FrameworkElement`) | Edge blur band over the backdrop for scrolling content | `Edge` (`Top`), `Size` (80), `Blur` (σ 20, max 33), `Source` (null = scene backdrop) |
| `GlassGeometry` (static) | Exact continuous-corner outlines as frozen `Geometry` | `Create(Size, GlassRadius)`, `Create(Rect, GlassRadius)`, `Inflate(Rect, GlassRadius, double)`, `InflateRadius(GlassRadius, double)` |
| `GlassDiagnostics` (static) | Which render path is active | `ShaderSource`, `ShaderStatus`, `CanUseShader(Visual)`, `Describe()` |
| `GlassRadius` (struct, Core) | Corner radius | XAML `16`, `capsule`, `circle`; C# `28`, `GlassRadius.Capsule`, `GlassRadius.Circle`, `GlassRadius.FromValue(x)` |

`Tint` is `Color?`: XAML `Tint="#0A84FF"`, C# `Tint = Color.FromRgb(0x0A, 0x84, 0xFF)`; `null` (or a fully
transparent color) means untinted. On glass the alpha is ignored. On `GlassSlider` and `GlassSwitch`, `Tint` is the
fill or on-track color as given, alpha included (a fully transparent color falls back to the Windows accent).

## Recipes

Complete, runnable windows (XAML + code-behind, project file, feed) are in
[references/recipes.md](references/recipes.md). `IconFont` below is an app resource:
`<FontFamily x:Key="IconFont">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>` in `App.xaml`. The shape of each:

**App shell with a floating tab bar.** The page is the backdrop; swap it on selection. Clear over imagery,
Regular when the page is text.

```xml
<lg:GlassScene x:Name="Scene">
  <lg:GlassScene.Content><ContentControl x:Name="PageHost" Focusable="False" /></lg:GlassScene.Content>
  <lg:GlassTabBar x:Name="Tabs" VerticalAlignment="Bottom" Margin="0,0,0,24" SelectedIndex="0">
    <lg:GlassTabBarItem Content="Home" /> <lg:GlassTabBarItem Content="Explore" /> <lg:GlassTabBarItem Content="Library" />
  </lg:GlassTabBar>
</lg:GlassScene>
```

```csharp
Tabs.SelectionChanged += (_, _) => ShowPage(Tabs.SelectedIndex);   // PageHost.Content = ...; GlassScene.SetMaterial(Scene, ...)
```

**Toolbar over a photo.** Clusters split by spacers; plain `Button`/`ToggleButton` items, icon glyphs in the icon
font, an automation name on each icon.

```xml
<lg:GlassToolbar VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0">
  <Button Content="&#xE72B;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15" AutomationProperties.Name="Back" />
  <Button Content="Library" />
  <lg:GlassToolbarSpacer />
  <ToggleButton Content="&#xE734;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15" AutomationProperties.Name="Favorite" />
</lg:GlassToolbar>
```

**Media card with a slider.** A Regular card, Clear inside, a `GlassSlider` scrubber, circular transport buttons,
one tinted primary action, artwork clipped concentric to the card.

```xml
<lg:GlassSurface Material="Regular" Radius="28" Padding="14" Width="380" VerticalAlignment="Center">
  <StackPanel lg:GlassScene.Material="Clear">
    <Image x:Name="Artwork" Source="/Assets/album.jpg" Height="352" Stretch="UniformToFill" />
    <lg:GlassSlider x:Name="Scrubber" Minimum="0" Maximum="244" Value="83" AutomationProperties.Name="Playback position" />
    <lg:GlassButton IsIconOnly="True" Size="Large" Tint="#FF375F" Content="&#xE768;" FontFamily="{StaticResource IconFont}"
                    FontWeight="Normal" FontSize="16" AutomationProperties.Name="Play" />
  </StackPanel>
</lg:GlassSurface>
```

```csharp
Artwork.SizeChanged += (_, _) => Artwork.Clip = GlassGeometry.Create(new Rect(Artwork.RenderSize), GlassGeometry.InflateRadius(28, -14));
```

**Quick-settings card with switches.** A compact panel of a few toggles over a wallpaper (a full settings page gets a
solid surface, not glass). Stretched `GlassSwitch` rows put the label left and the track right; wire accessibility
toggles straight to the scene options.

```xml
<lg:GlassSurface Material="Regular" Radius="28" Width="400" Padding="20,12">
  <StackPanel lg:GlassScene.Material="Clear">
    <lg:GlassSwitch Content="Wi-Fi" IsChecked="True" Margin="4,10" />
    <lg:GlassSwitch x:Name="Opaque" Content="Reduce transparency" Margin="4,10" />
  </StackPanel>
</lg:GlassSurface>
```

```csharp
Opaque.Checked += (_, _) => GlassScene.SetReduceTransparency(this, true);
Opaque.Unchecked += (_, _) => ClearValue(GlassScene.ReduceTransparencyProperty);   // back to the Windows setting
```

**Glass menu from a button.** Standalone in the scene; checkable items, a separator, shortcut text (display only:
add a `KeyBinding` for the real shortcut). The header changes with the choice, so it also names the menu.

```xml
<lg:GlassMenu Header="Sort by name">
  <lg:GlassMenuItem Header="Name" IsCheckable="True" IsChecked="True" Click="OnSort" />
  <lg:GlassMenuItem Header="Date modified" IsCheckable="True" Click="OnSort" />
  <Separator />
  <lg:GlassMenuItem Header="Copy" Icon="&#xE8C8;" InputGestureText="Ctrl+C" Click="OnCopy" />
</lg:GlassMenu>
```

**Progressive blur over a scrolling list.** The `ScrollViewer` is the backdrop; blurs come before the bars among
the children; padding keeps the first and last rows clear of the bars.

```xml
<lg:GlassScene lg:GlassScene.Appearance="Light">
  <lg:GlassScene.Content>
    <ScrollViewer Background="#F6F5F2" VerticalScrollBarVisibility="Hidden">
      <ItemsControl x:Name="Feed" Margin="32,96,32,112" />
    </ScrollViewer>
  </lg:GlassScene.Content>
  <lg:GlassProgressiveBlur Edge="Top" Size="120" Blur="20" />
  <lg:GlassProgressiveBlur Edge="Bottom" Size="104" />
  <lg:GlassToolbar Material="Regular" VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0" />
  <lg:GlassTabBar Material="Regular" VerticalAlignment="Bottom" Margin="0,0,0,26" />
</lg:GlassScene>
```

**Draggable glass card.** `IsInteractive` gives the press, stretch and glow; the app moves the card. A `Canvas`
without a background among the children, `Canvas.Left`/`Top` set from `PreviewMouse*` handlers that skip presses on
buttons; the glass re-aligns by itself.

```xml
<Canvas x:Name="Stage">
  <lg:GlassSurface x:Name="Card" Material="Regular" Radius="28" Width="400" Padding="28" IsInteractive="True" Cursor="SizeAll" />
</Canvas>
```

## Rules and pitfalls

**The content layer**

- Put in `Content` whatever glass should bend: images, `MediaElement` video, gradients, art on a `Canvas`, lists in
  a `ScrollViewer`, text. It stays interactive (scrolls, clicks) wherever no glass or other child covers it.
- The backdrop must be inside `<lg:GlassScene.Content>`. Elements written directly inside `<lg:GlassScene>` are
  `Children`: an `<Image>` there is drawn over the backdrop and never refracted.
- Never put glass in `Content`: it would refract itself, so it draws the opaque material. Glass goes in `Children`.
- Only `Content`, progressive blurs and enclosing glass are refracted. A `TextBlock` or `Border` among `Children` is
  not, even right under a surface. Move anything that should show through glass into `Content`.
- Give the backdrop an opaque background (an image, or `Background` on the content root). Glass over a transparent
  backdrop has nothing to refract and reads as a gray smudge; plain flat colors make glass look like a gray pill.
- `HwndHost`-based content (`WebBrowser`, `WindowsFormsHost`, the HWND-hosted WebView2 control) cannot be copied by
  a `VisualBrush` and draws above all WPF content: it cannot be a backdrop or sit under glass. Popups, tooltips and
  context menus are separate windows and are not part of the backdrop either.
- A panel among `Children` with a `Background` (even `Transparent`) swallows input meant for `Content`. Leave layout
  panels without a background.

**Layout**

- Give the scene a bounded slot: the window root or a star-sized `Grid` cell. Put `ScrollViewer`s inside `Content`,
  never around the scene. A scene in a `ScrollViewer` or an auto-sized `StackPanel` grows to its content, so bars
  scroll away and menu panels, clamped to the scene's full height, open off-screen.
- A `GlassSurface` stretches like any `ContentControl`: give it `Width`/`Height` or non-`Stretch` alignment, or it
  fills the scene. `GlassToolbar` and `GlassTabBar` default to centered; set `VerticalAlignment="Top"`/`"Bottom"`.
- Leave `GlassProgressiveBlur` stretched (its desired size is only the band); list it before the glass that floats
  over it.
- Turn on `UseLayoutRounding="True"` on the window. The rim light is one device pixel; surfaces on fractional pixels
  resample the copy and look soft.
- Declare per-monitor DPI awareness (`<dpiAwareness>PerMonitorV2</dpiAwareness>` in `app.manifest`, as the demo app
  does; see the recipes setup). Without it, Windows bitmap-stretches the window on a monitor with a different scale
  and the rim blurs.
- Moving glass via layout (`Margin`, `Canvas.Left`/`Top`, `Grid` changes, scrolling) re-aligns automatically. If you
  animate a `RenderTransform` yourself, call `scene.InvalidateGlass()` every frame (for example from
  `CompositionTarget.Rendering`, unsubscribing when the animation ends) or animate layout properties instead.
- `IsInteractive="True"` on a `GlassSurface` adds press grow, rubber-band stretch and glow, then springs back; it does
  not move the surface. To drag a card, put it in a `Canvas` among the scene's `Children` and update
  `Canvas.Left`/`Top` from `PreviewMouse*` handlers, skipping presses that start on a `ButtonBase` (recipe 7). Buttons,
  toolbars and tab bars are interactive by default; static panels stay non-interactive.
- `IsFluid="True"` springs the outline to the surface's new box after any layout move or resize (toolbar clusters do
  this already). Don't set it on a surface you drag or move by layout animation: the glass trails behind its content.

**Materials, appearance and inheritance**

- Clear over imagery; Regular for menus, sheets, compact cards with a few lines of text, and anything large or over
  busy content. Menu panels default to Regular. Long text, settings pages and tables get a solid surface, not glass.
- Menu panels mix their glass halfway toward a solid backing (`#F5F5F7` light, `#1C1C1E` dark) on their own, so text
  on an untinted Regular menu (the default) keeps at least 5:1 over any backdrop, even white under dark glass, in the
  fallback material too; a Clear panel reaches about 4.9:1. On a tint, items take white or `#111113` from the backed
  glass (at least 4.9:1 with the Windows accent and the usual system colors; very light tints in dark appearance can
  fall lower). Don't add a background to the panel or its items to make them readable; the trigger and other glass
  stay plain glass.
- `Material` is inherited: a Regular card makes every glass control inside it Regular too. Reset nested controls with
  `lg:GlassScene.Material="Clear"` on the card's inner panel (the demo app does this on every card).
- `Appearance="Automatic"` follows the Windows app mode live. Over a backdrop that is clearly light or dark, set
  `lg:GlassScene.Appearance` to match it (light glass over light paper, dark over night imagery).
- Text on glass inherits `GlassScene.ContentForeground` (`#111113` light, white dark; on a tint white or `#111113`,
  whichever contrasts more). Do not force `Foreground` through implicit `TextBlock` styles; set `Foreground` on a
  control only to override deliberately.
- On glass, use `TextBlock` for labels and `Glass*` controls for input. `Label`, `CheckBox`, `RadioButton`, `TextBox`,
  `ComboBox` and a plain `Button` outside a toolbar keep their Windows theme: the system text color (black on dark
  glass) and Aero chrome. If you need one, bind
  `Foreground="{Binding Foreground, RelativeSource={RelativeSource AncestorType=lg:GlassSurface}}"` and give it a
  transparent, borderless template.
- `GlassSurface` ignores `Background`, `BorderBrush` and `BorderThickness`, and its corner property is `Radius` (there
  is no `CornerRadius`). Never put a filled `Border` inside it to fake frost: it covers the glass. Use
  `Material="Regular"`.
- Tint sparingly: one tinted primary action per view. Never tint whole toolbars or tab bars.
- Shapes: capsules for text controls (`GlassButton`, toolbars, tab bars default to it), circles for icon-only
  buttons (`IsIconOnly="True"`), fixed continuous radii of 16 to 28 for panels and cards, by size. Nested corners are
  concentric. The exact concentric radius for library outlines is `R − inset / 1.528`
  (`GlassGeometry.InflateRadius(R, -inset)`; in XAML write the computed number). `R − inset` is the cross-platform
  rule of thumb and gives a slightly tighter inner corner.
- Leave `Refraction` unset: it follows each surface's resting size, `min(60, 0.75 × shorter side)` (24 for a 32-unit
  button, 60 for a card), so small controls never pull in backdrop from far outside their outline. A value you set is
  used as given; very high values make the backdrop unrecognizable. It must be ≥ 0 (or NaN, the unset default).
- A press drives the pointer light to full on the glow spring (0.42 s, no bounce), so it never overshoots.

**Toolbars, tab bars and menus**

- Inside a `GlassToolbar` the cluster is the glass: plain `Button`/`ToggleButton`, a `GlassButton` and a
  `GlassMenu`'s trigger all take the toolbar button look (no glass of their own, through an implicit `GlassButton`
  style in the toolbar's resources); the menu's panel is still glass. Glass controls inside a Regular card are
  allowed nested glass (they refract the card); keep it to one level.
- `GlassToolbar` has no `ItemsSource`: declare items in XAML or add `UIElement`s to `toolbar.Items` in code. Only
  exact `Button`, `ToggleButton` and `GlassButton` get the toolbar look automatically (implicit styles). For
  `RadioButton`, `RepeatButton`, another `Button` subclass or your own `Style`, base the style on the exported one
  (`TargetType` `ButtonBase`): `BasedOn="{StaticResource {ComponentResourceKey TypeInTargetAssembly={x:Type lg:GlassToolbar}, ResourceId=ButtonStyle}}"`.
  For font changes alone, set `FontFamily`/`FontSize`/`FontWeight` inline. A text field in a toolbar needs a
  transparent, borderless template.
- Always set `SelectedIndex` on a `GlassTabBar` (the default -1 draws no lens). Once an item is selected, one always
  is: Ctrl+click on the selected tab and `SelectedIndex = -1` keep the selection, and removing the selected item
  selects a neighbor. `SelectedIndex < 0` remains only before the first selection or when the bar has no items, so a
  guard in `SelectionChanged` is enough. Tab items are 28 tall: use a text label or a single glyph, not an icon above
  a label. Button, toolbar and tab heights are fixed by their styles; change `Size`, or `Height` together with
  `FontSize`, rather than `FontSize` alone.
- Icon glyphs: `FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"` (Windows 10 has only the MDL2 font), FontSize 15
  in toolbars, 14 to 16 on icon buttons, plus `AutomationProperties.Name` and a `ToolTip`. `FontWeight="Normal"` is
  not required: `GlassButton`, `GlassMenu` triggers and toolbar buttons draw content that is only private-use
  characters (`&#xE72B;`) at normal weight automatically (icon fonts have one weight; SemiBold would smear them). Set
  it yourself only for a glyph elsewhere, such as a `TextBlock` or a `GlassTabBarItem`.
- A `GlassMenu` panel lives in the scene's top layer and is clamped inside the scene: it cannot leave the window, so
  make the scene cover the window. Outside a scene it opens in a `Popup` with the opaque material. It lines up with
  the trigger's start edge, also in right-to-left layouts.
- Items draw in the glass text font whatever font the trigger uses: an icon-only menu (`Header="&#xE712;"` with the
  icon font) needs nothing on its items. Set `FontFamily` on an item, or in `ItemContainerStyle`, to change it. `Icon`
  glyph strings already use the icon font. The trigger takes the menu's `Foreground` and `FontSize` when the
  application sets them (items and trigger then share them); otherwise it follows `Size` (12 / 14 / 15) and keeps its
  own glass's foreground while the items take the panel's, which can differ on a tint.
- Menu items are `GlassMenuItem` and `Separator` elements, or data: with `ItemsSource`, each data item gets a
  `GlassMenuItem` container that shows it with `ItemTemplate` (or `ItemTemplateSelector`); `ItemContainerStyle`
  applies to every `GlassMenuItem` (separators keep their own style), for example to set `Command` and
  `CommandParameter`.
- Items' routed events (`Click`, `Checked`) bubble through the panel in the scene's top layer, not through the menu:
  handle them on the item (or in `ItemContainerStyle`), or use commands. `GlassMenuItem.Header` is the label and keeps
  it current when it changes; `Content` set by the application takes over from it.
- `InputGestureText` only displays the shortcut. Register it yourself with a `KeyBinding` in `Window.InputBindings`.
- The menu, its panel and the toolbar are focus scopes, as WPF's menus and toolbars are: a routed `Command`
  (`ApplicationCommands.Copy`) on a `GlassMenuItem` or a toolbar button that nothing in the menu or toolbar handles
  goes on to the element that has focus in the window (the text box whose selection it copies). A routed command on a
  standalone `GlassButton` routes from the button itself, as on any WPF `Button`, and is disabled unless a
  `CommandBinding` on its ancestors handles it; use `Click` or a bound `ICommand` there.
- `IsOpen="True"` set before the menu loads (in XAML, or from a binding whose source starts true) opens it once it is
  loaded.

**Rendering, fallbacks and limits**

- Render path per lens (`surface.RenderedMode`): `Shader` normally; `Fallback` without pixel shader 3.0 in hardware,
  with a render tier below 2 or fewer than 1024 shader instruction slots, with
  `RenderOptions.ProcessRenderMode = SoftwareOnly`, in a software-rendered window, over Remote Desktop that renders
  in software, or after the driver rejected the shader; `Opaque` when transparency effects are off,
  `ReduceTransparency` is true, there is no scene, or the glass sits in `Content`; `HighContrast` under a Windows
  contrast theme. Virtual machines without a GPU usually still run the shader, through WARP.
- `GlassRenderMode.Shader` is the same as `Automatic` (an alias for stating the intent or undoing an inherited
  `Fallback`); only `Fallback` changes the path.
- `RenderTargetBitmap` snapshots: WPF renders them in software, where the shader cannot run, so glass in `Automatic`
  mode is missing from the image. Set `lg:GlassScene.RenderMode="Fallback"` on what you snapshot.
- A shader the graphics driver rejects switches glass to the fallback material in every window of the process; it
  never crashes. The library handles `PixelShader.InvalidPixelShaderEncountered` on each UI thread that shows glass,
  so a rejected pixel shader of the application's own on that thread also switches glass to the fallback (and no
  longer throws).
- Log `GlassDiagnostics.Describe()` when glass looks like plain translucency.
- No fixed surface limit, but each surface costs one live copy of everything beneath it plus a shader pass over its
  area and refraction margin, and only while something beneath or on it changes. Up to 16 surfaces per scene is
  comfortable. Count one per `GlassButton`, `GlassSurface`, toolbar cluster and open menu, and two per `GlassTabBar`
  (bar and lens). Group toolbar items into clusters instead of one glass button each.
- Each `GlassProgressiveBlur` adds six blurred copies of its band (`BlurEffect` radius up to 3 × `Blur`); use at most
  one per edge. `Blur` (0 to `MaxBlur` = 33) renders exactly at every display scale: where one WPF blur pass is not
  wide enough (high-DPI displays), a level uses several passes that read each other through brushes, so the
  strongest levels cost more there.
- Regular glass also blurs its copy (`BlurEffect`, σ 7.33) and samples a 9 × 9 grid; prefer Clear for large glass that
  animates continuously.
- The backdrop is drawn once per surface copy. For a costly `Content` (many elements, much text), set
  `CacheMode="BitmapCache"` on it so copies reuse one bitmap.
- Copies are limited to the GPU's maximum texture size (16384). `GlassProgressiveBlur.Blur` above 33 and a negative
  `Size`, `Refraction` or radius (`PanelRadius` included) throw.

**Accessibility**

- Controls expose UI Automation: Button (`GlassButton`), a toggle switch (`GlassSwitch`: a Button with Toggle),
  RangeValue (slider), Tab/TabItem with Selection, single and required (tab bar), ToolBar, menus (the focused trigger
  button with ExpandCollapse; the open panel a Menu named after the current header, raising MenuOpened and
  MenuClosed) and MenuItem with Invoke and, when checkable, Toggle (both choose the item exactly as a click does).
  Give every icon-only control `AutomationProperties.Name`; on a menu whose label changes, let the header name it
  rather than a fixed name; name standalone surfaces that group content. An `Image` in `GlassScene.Content` is an
  unnamed image to screen readers: draw a decorative wallpaper as a `Rectangle` with an `ImageBrush` fill (no
  automation peer), and name a meaningful picture.
- Keyboard: Tab, Space/Enter, arrows (tabs, slider, menu), Escape (menu). A 2-unit focus ring follows the outline and
  appears only for keyboard focus (`lg:GlassScene.IsFocusVisible`).
- Windows *Transparency effects* off → opaque fill (`#F2F2F7`/`#1C1C1E` at 0.94) with the rim light; override with
  `lg:GlassScene.ReduceTransparency` (`null` follows Windows).
- Windows *Animation effects* off → `Motion="Full"` behaves as `Reduced`. `Reduced` keeps every transition as a
  critically damped spring without grow, stretch or overshoot (the tab lens slides, the switch thumb travels, the
  menu grows out of its trigger, fluid outlines flow, thumbs crossfade into glass at their resting size);
  `lg:GlassScene.Motion="None"` removes transitions entirely.
- `lg:GlassScene.IncreaseContrast="True"` adds a 1-unit inner border and switches to Regular.
- Contrast themes draw system colors automatically, following the theme live: `ControlBrush` glass with
  `ControlTextBrush` border and text; menu items and toolbar buttons show hover, press, keyboard focus (menu items)
  and the checked state (toolbar buttons) as a solid `Highlight` fill with `HighlightText`; slider fills and switches
  that are on use `Highlight`, with the slider's rest and the switch track outlined in `ControlText`; thumbs are solid
  system colors. Never encode meaning only in glass or tint.

## Checklist

- [ ] Feed added, `LiquidGlassEngineering.Wpf` 1.0.0 referenced, `xmlns:lg` declared, both `using` lines in C#
      (`LangVersion` latest on `net48`).
- [ ] One `GlassScene` per glass region, in a bounded slot; its `Content` is the opaque backdrop; no glass inside
      `Content`.
- [ ] Everything that should show through glass is in `Content`; layout panels in `Children` have no background.
- [ ] Surfaces have a size or alignment; bars sit at the top or bottom with margins (about 20 to 28).
- [ ] Clear over imagery, Regular for menus and compact text cards, solid surfaces for long text; nested controls in
      Regular cards reset to Clear.
- [ ] Capsules for controls, circles for icon buttons, concentric corners; at most one tinted control per view.
- [ ] Icon-only controls have `AutomationProperties.Name`; labels readable over the busiest backdrop area.
- [ ] `UseLayoutRounding="True"` and PerMonitorV2 DPI awareness; custom `RenderTransform` animations call
      `InvalidateGlass()`.
- [ ] Ran it: `RenderedMode` is `Shader` (or the fallback is expected), rim crisp, light and dark both checked,
      Transparency effects and Animation effects toggled in Windows settings.

## References

- [references/api.md](references/api.md): every public type, property, default, enum case, template part and the
  optical model, with snippets.
- [references/recipes.md](references/recipes.md): seven complete windows: app shell with a floating tab bar, toolbar
  over a photo, media card with a slider, quick-settings card with switches, glass menu from a button, progressive
  blur over a scrolling list, draggable glass card.
- [Hello Glass](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/tree/main/examples/wpf/HelloGlass):
  a whole runnable app (project file, `App.xaml`, one window): a card whose switch turns its frost on and off, a
  toolbar and a tab bar over a wallpaper, the same screen as the SwiftUI and Compose examples.
