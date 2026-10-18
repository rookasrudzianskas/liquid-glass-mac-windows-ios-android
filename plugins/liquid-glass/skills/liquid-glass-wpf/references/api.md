# API reference: LiquidGlassEngineering.Wpf 1.0.0

Every public type of the WPF package (`LiquidGlass.Wpf.dll`, namespace `LiquidGlass.Wpf`) and of the optical model it
depends on (`LiquidGlass.Core.dll`, namespace `LiquidGlass`), as declared in the source. Dependency properties are
listed by their CLR wrapper; the field is the wrapper name plus `Property` (`Radius` → `RadiusProperty`).
"Inherited" means `FrameworkPropertyMetadataOptions.Inherits`: set it on any ancestor (a `Window`, a `Grid`, a
`GlassScene`) and every glass control below picks it up.

## Contents

- [Namespaces and XAML](#namespaces-and-xaml)
- [Enums](#enums)
- [GlassRadius](#glassradius)
- [GlassScene](#glassscene)
- [GlassSurface](#glasssurface)
- [GlassButton](#glassbutton)
- [GlassToolbar and GlassToolbarSpacer](#glasstoolbar-and-glasstoolbarspacer)
- [GlassTabBar and GlassTabBarItem](#glasstabbar-and-glasstabbaritem)
- [GlassSlider](#glassslider)
- [GlassSwitch](#glassswitch)
- [GlassMenu and GlassMenuItem](#glassmenu-and-glassmenuitem)
- [GlassProgressiveBlur](#glassprogressiveblur)
- [GlassGeometry](#glassgeometry)
- [GlassDiagnostics](#glassdiagnostics)
- [Template parts: GlassLens and GlassValueThumb](#template-parts-glasslens-and-glassvaluethumb)
- [Automation peers](#automation-peers)
- [Theme resources and default styles](#theme-resources-and-default-styles)
- [Optical model (LiquidGlass namespace)](#optical-model-liquidglass-namespace)

## Namespaces and XAML

```csharp
using LiquidGlass;      // GlassMaterial, GlassMotion, GlassRadius, GlassRadiusKind, springs and the optical model
using LiquidGlass.Wpf;  // controls, GlassAppearance, GlassRenderMode, GlassButtonSize, GlassEdge, ... GlassDiagnostics
```

The C# fragments below also assume the usual WPF usings (`System`, `System.Windows`, `System.Windows.Controls`,
`System.Windows.Media`). Two need more, marked where they occur: `System.ComponentModel` (`DependencyPropertyDescriptor`)
and `System.Diagnostics` (`Trace`).

```xml
xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
xmlns:lgc="clr-namespace:LiquidGlass;assembly=LiquidGlass.Core"
```

The `lg` namespace (recommended prefix `lg`) exposes the `LiquidGlass.Wpf` types: `{x:Type lg:GlassSurface}` and
`{x:Static lg:GlassAppearance.Dark}` resolve. Core enums and `GlassRadius` still work as plain attribute values,
because the property's type converts them: `Material="Regular"`, `Motion="Reduced"`, `Radius="capsule"`. The WPF
markup compiler does not resolve Core types through `lg`, so `{x:Static lg:GlassMaterial.Regular}` and
`{x:Type lg:GlassRadius}` fail to build (MC3050 / MC3066). To reference a Core type in markup, declare `lgc` as above
(only needed for that) and write `{x:Static lgc:GlassMaterial.Regular}`.

Watch the split: `GlassMaterial` and `GlassMotion` live in `LiquidGlass` (Core); `GlassAppearance` and
`GlassRenderMode` live in `LiquidGlass.Wpf`. C# code that uses both needs both `using` lines.

## Enums

```csharp
// LiquidGlass (Core)
public enum GlassMaterial { Clear, Regular }
public enum GlassMotion { Full, Reduced, None }
public enum GlassRadiusKind { Fixed, Capsule, Circle }

// LiquidGlass.Wpf
public enum GlassAppearance { Automatic, Light, Dark }
public enum GlassRenderMode { Automatic, Shader, Fallback }
public enum GlassButtonSize { Small, Regular, Large }
public enum GlassEdge { Top, Bottom, Left, Right }
public enum GlassMenuPlacement { Bottom, Top }
public enum GlassSpacerSizing { Flexible, Fixed }
public enum GlassShaderSource { Precompiled, CompiledAtRuntime, Unavailable }
public enum GlassLensMode { Shader, Fallback, Opaque, HighContrast }
```

| Enum | Case | Meaning |
| --- | --- | --- |
| `GlassMaterial` | `Clear` | The refracted backdrop with a light tone curve and veil. Default everywhere except menu panels. |
| | `Regular` | Frosted: the backdrop is blurred (σ 7.33) and toned before refraction. |
| `GlassMotion` | `Full` | Springs with grow, stretch and overshoot. Windows' *Animation effects* off turns it into `Reduced`. |
| | `Reduced` | Every transition is kept as a critically damped spring, without grow, stretch or overshoot: the tab lens slides, the switch thumb travels, the menu grows out of its trigger, fluid outlines flow, thumbs crossfade into glass at their resting size. |
| | `None` | No transitions; every change applies immediately. |
| `GlassAppearance` | `Automatic` | Follows the Windows app mode (Settings › Personalization › Colors), live. |
| | `Light` / `Dark` | Light glass with `#111113` foreground / dark glass with white foreground. |
| `GlassRenderMode` | `Automatic` | The pixel shader where it can run (pixel shader 3.0 in hardware, render tier 2, a window that does not render in software, a shader the driver accepted), otherwise the fallback material. |
| | `Shader` | The same as `Automatic` (an alias): glass never forces the shader where it cannot run. Use it to state the intent in a style or to undo an inherited `Fallback`. |
| | `Fallback` | Always the fallback material (platform blur, fitted veil, exact rim light). |
| `GlassButtonSize` | `Small` / `Regular` / `Large` | Heights 24 / 32 / 40 units. |
| `GlassEdge` | `Top`, `Bottom`, `Left`, `Right` | The edge a progressive blur is strongest at. |
| `GlassMenuPlacement` | `Bottom` / `Top` | Below / above the trigger; flips when there is no room. |
| `GlassSpacerSizing` | `Flexible` | Fills the available space, pushing clusters apart. |
| | `Fixed` | A standard 8-unit gap between two clusters. |
| `GlassShaderSource` | `Precompiled` | fxc bytecode embedded in the assembly. |
| | `CompiledAtRuntime` | Compiled from the embedded HLSL with `D3DCompile` (`d3dcompiler_47.dll`). |
| | `Unavailable` | Neither worked; glass draws the fallback material. |
| `GlassLensMode` | `Shader` | The glass pixel shader refracts and tones the backdrop. |
| | `Fallback` | The fallback material. |
| | `Opaque` | Opaque fill with the rim light: transparency reduced, no scene, or glass inside the scene's `Content`. |
| | `HighContrast` | A Windows contrast theme is active: system colors, no transparency. |

## GlassRadius

`[TypeConverter(typeof(GlassRadiusConverter))] public readonly struct GlassRadius : IEquatable<GlassRadius>`:
a corner radius in layout units (continuous superellipse corners), a capsule or a circle. XAML text: `16`,
`capsule`, `circle` (case-insensitive, invariant culture).

```csharp
public static GlassRadius Capsule { get; }
public static GlassRadius Circle { get; }
public GlassRadiusKind Kind { get; }
public double Value { get; }                       // 0 unless Kind == Fixed
public bool IsFullyRounded { get; }                // Capsule or Circle
public static GlassRadius FromValue(double radius) // throws ArgumentOutOfRangeException for negative, NaN, infinity
public static implicit operator GlassRadius(double radius)
public static GlassRadius Parse(string text)       // throws FormatException
public static bool TryParse(string? text, out GlassRadius radius)
public bool Equals(GlassRadius other)
public static bool operator ==(GlassRadius left, GlassRadius right)
public static bool operator !=(GlassRadius left, GlassRadius right)
public override string ToString()                  // "capsule", "circle" or the number

public sealed class GlassRadiusConverter : TypeConverter  // string, double and int → GlassRadius; GlassRadius → string
```

```xml
<lg:GlassSurface Radius="28" />   <lg:GlassSurface Radius="capsule" />   <lg:GlassButton Radius="circle" />
```

```csharp
surface.Radius = 28;                       // implicit from double
surface.Radius = GlassRadius.Capsule;
surface.Radius = GlassRadius.FromValue(slider.Value);
```

Large fixed radii blend into a capsule, so animating the radius never jumps.

## GlassScene

`[ContentProperty(nameof(Children))] public class GlassScene : FrameworkElement`: the region glass may refract. It
draws `Content` once as the backdrop; every glass surface among `Children` (at any depth) refracts it at its exact
on-screen position. `Children` are laid out like a single-cell `Grid`. A third top layer hosts menu panels.
Overrides: `ClipToBounds` defaults to `true`, `SnapsToDevicePixels` defaults to `true`.

```csharp
public GlassScene()
public UIElement? Content { get; set; }          // ContentProperty, default null. The backdrop. Never put glass in it.
public UIElementCollection Children { get; }     // XAML content property
public void InvalidateGlass()                    // re-align all glass after a RenderTransform animated outside the library
public static GlassScene? FindScene(DependencyObject? element)   // nearest enclosing scene, or null
```

Attached, inherited options (set them on the scene, the window or any container):

| Attached property | Type | Default | Accessors |
| --- | --- | --- | --- |
| `Material` | `GlassMaterial` | `Clear` | `GetMaterial(DependencyObject)`, `SetMaterial(DependencyObject, GlassMaterial)` |
| `Appearance` | `GlassAppearance` | `Automatic` | `GetAppearance`, `SetAppearance(DependencyObject, GlassAppearance)` |
| `Motion` | `GlassMotion` | `Full` | `GetMotion`, `SetMotion(DependencyObject, GlassMotion)` |
| `RenderMode` | `GlassRenderMode` | `Automatic` | `GetRenderMode`, `SetRenderMode(DependencyObject, GlassRenderMode)` |
| `ReduceTransparency` | `bool?` | `null` (follow Windows *Transparency effects*) | `GetReduceTransparency`, `SetReduceTransparency(DependencyObject, bool?)` |
| `IncreaseContrast` | `bool` | `false` | `GetIncreaseContrast`, `SetIncreaseContrast(DependencyObject, bool)` |

Read-only attached properties, maintained by each glass control on itself (use them in triggers and bindings):

| Attached property | Type | Accessor | Meaning |
| --- | --- | --- | --- |
| `IsDark` | `bool` | `GetIsDark(DependencyObject)` | The control draws dark glass (appearance resolved against Windows). |
| `ContentForeground` | `Brush` | `GetContentForeground(DependencyObject)` | Default text/icon color: `#111113` on light glass, white on dark glass; on a tint, in either appearance, whichever of the two has the higher WCAG contrast with the tinted glass (`GlassPalette.ForegroundOnTint`: white on blue, red or purple, `#111113` on yellow, mint, green or orange; on a menu and its panel, the tint mixed toward the legibility backing); `SystemColors.ControlTextBrush` in a contrast theme. |
| `IsFocusVisible` | `bool` | `GetIsFocusVisible(DependencyObject)` | Keyboard focus arrived from the keyboard; templates show the focus ring. |

All `Get*`/`Set*` accessors throw `ArgumentNullException` for a null element.

```xml
<lg:GlassScene x:Name="Scene" lg:GlassScene.Appearance="Dark" lg:GlassScene.Motion="Reduced">
  <lg:GlassScene.Content>
    <Image Source="/Assets/wallpaper.jpg" Stretch="UniformToFill" />
  </lg:GlassScene.Content>
  <lg:GlassSurface Width="320" Height="200" />
</lg:GlassScene>
```

```csharp
GlassScene.SetMaterial(window, GlassMaterial.Regular);
GlassScene.SetReduceTransparency(window, true);
window.ClearValue(GlassScene.ReduceTransparencyProperty);    // back to the Windows setting
DependencyPropertyDescriptor.FromProperty(GlassScene.IsDarkProperty, typeof(GlassTabBar))   // using System.ComponentModel;
    .AddValueChanged(tabs, (_, _) => UpdateChrome(GlassScene.GetIsDark(tabs)));
```

## GlassSurface

`public class GlassSurface : ContentControl`: a glass container. Its content draws above glass that refracts the
scene's backdrop. Hit-tests its rounded outline only (clicks in the corners outside it pass through). Not focusable
and not a tab stop by default. Template parts: `PART_Root` (`FrameworkElement`, receives the press transform),
`PART_Lens` (`GlassLens`). Default style: `HorizontalContentAlignment`/`VerticalContentAlignment` = `Stretch`,
`Foreground` bound to `ContentForeground`.

```csharp
public GlassSurface()
public GlassMaterial Material { get; set; }        // inherited (same DP as GlassScene.MaterialProperty), default Clear
public GlassAppearance Appearance { get; set; }    // inherited, default Automatic
public GlassMotion Motion { get; set; }            // inherited, default Full
public GlassRadius Radius { get; set; }            // default GlassRadius.FromValue(16)
public double Refraction { get; set; }             // default NaN: min(60, 0.75 × shorter resting side); else >= 0 and finite, used as given
public Color? Tint { get; set; }                   // default null; alpha ignored, a fully transparent color means no tint
public bool IsInteractive { get; set; }            // default false: press grow, drag stretch, release overshoot, pointer glow
public bool IsFluid { get; set; }                  // default false: outline springs to its new box on layout changes
public bool IsFocusRingVisible { get; set; }       // default false: draws the 2-unit focus ring around the outline
public bool IsPressed { get; }                     // read-only DP IsPressedProperty: a pointer or key holds it down
public GlassLensMode? RenderedMode { get; }        // how the glass draws now; null before the template is applied
public override void OnApplyTemplate()
```

Material, Appearance and Motion set on a surface also apply to all glass inside it (they are inherited). A standalone
surface is an automation Group (`GlassSurfaceAutomationPeer`); a surface inside a control template is not exposed.

```xml
<lg:GlassSurface Material="Regular" Radius="28" Padding="24" Width="380"
                 HorizontalAlignment="Center" VerticalAlignment="Center">
  <StackPanel lg:GlassScene.Material="Clear">   <!-- nested controls back to Clear -->
    <TextBlock Text="Liquid Glass" FontSize="28" FontWeight="SemiBold" />
  </StackPanel>
</lg:GlassSurface>
```

## GlassButton

`public class GlassButton : Button`: a button made of interactive glass. Text buttons are capsules; icon-only buttons
are circles. Everything else is `Button`: `Click`, `Command`, `CommandParameter`, `IsDefault`, `IsCancel`, access keys
(`_Save`). Template part: `PART_Surface` (`GlassSurface`).

```csharp
public GlassButton()
public GlassMaterial Material { get; set; }        // inherited, default Clear
public GlassAppearance Appearance { get; set; }    // inherited, default Automatic
public GlassMotion Motion { get; set; }            // inherited, default Full
public GlassRadius Radius { get; set; }            // default GlassRadius.Capsule (style sets circle when IsIconOnly)
public double Refraction { get; set; }             // default NaN: 24 for a 32-unit button (min(60, 0.75 × shorter side))
public Color? Tint { get; set; }                   // default null
public bool IsInteractive { get; set; }            // default true
public GlassButtonSize Size { get; set; }          // default GlassButtonSize.Regular
public bool IsIconOnly { get; set; }               // default false: square, circular outline
public override void OnApplyTemplate()
```

Default style: font `Segoe UI Variable Text, Segoe UI`, SemiBold (Normal when `Content` is only icon glyphs, that
is private-use characters such as `&#xE734;`, so `FontWeight="Normal"` is not needed), Cursor Hand, focus ring on
keyboard focus, opacity 0.45 when disabled. Inside a `GlassToolbar` a `GlassButton` takes the toolbar button look
(no glass of its own: the cluster is its glass).

| `Size` | Height / MinWidth | FontSize | Padding |
| --- | --- | --- | --- |
| `Small` | 24 | 12 | `10,0` |
| `Regular` | 32 | 14 | `14,0` |
| `Large` | 40 | 15 | `18,0` |
| `IsIconOnly="True"` | Width = Height | (unchanged) | `0`, `Radius` = `circle` |

```xml
<lg:GlassButton Content="Continue" Size="Large" Tint="#0A84FF" Click="OnContinue" />
<lg:GlassButton IsIconOnly="True" Content="&#xE734;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                FontWeight="Normal" FontSize="14" AutomationProperties.Name="Favorite" />
```

```csharp
var share = new GlassButton { Content = "Share", Size = GlassButtonSize.Small };
share.Click += (_, _) => Share();
```

## GlassToolbar and GlassToolbarSpacer

`[ContentProperty(nameof(Items))] public class GlassToolbar : Control`: a floating toolbar. Items between spacers are
grouped into capsule glass clusters (each a `GlassSurface` with `IsFluid = true`, 40 units tall when horizontal) that
spring to their new size when items come and go. Plain `Button`s, `ToggleButton`s and `GlassButton`s inside (a
`GlassMenu`'s trigger too) get the toolbar button style (no glass of their own, a soft highlight in the foreground
color) through implicit styles in the toolbar's resources keyed to exactly those three types: a `RadioButton`,
`RepeatButton` or another `Button` subclass keeps its Windows look unless its style is based on the exported
`ButtonStyle` key (see [Theme resources](#theme-resources-and-default-styles)). There is no `ItemsSource` or
`ItemTemplate`: add elements to `Items`. Pressing anywhere on a cluster presses its glass. Not focusable; a focus scope
(as WPF's `ToolBar`), so a routed command on a toolbar button goes on to the window's focused element;
`KeyboardNavigation.TabNavigation` = `Continue`. Template part: `PART_Host` (`Grid`). UI Automation:
`ToolBar`.

```csharp
public GlassToolbar()
public ObservableCollection<UIElement> Items { get; }   // controls and spacers, in order
public GlassMaterial Material { get; set; }       // inherited, default Clear
public GlassAppearance Appearance { get; set; }   // inherited, default Automatic
public GlassMotion Motion { get; set; }           // inherited, default Full
public GlassRadius Radius { get; set; }           // default GlassRadius.Capsule
public double Refraction { get; set; }            // default NaN: 30 for a 40-unit cluster (min(60, 0.75 × shorter side))
public Color? Tint { get; set; }                  // default null (tints every cluster; rarely wanted)
public bool IsInteractive { get; set; }           // default true
public Orientation Orientation { get; set; }      // default Orientation.Horizontal
public double Inset { get; set; }                 // default 4.0: glass to items
public double Spacing { get; set; }               // default 4.0: between items
public override void OnApplyTemplate()
```

Default style: `HorizontalAlignment="Center"`, `VerticalAlignment="Center"`. Toolbar button style: Height 32,
MinWidth 32, Padding `8,0`, FontSize 14 SemiBold (Normal when the content is only icon glyphs); highlight opacity 0.08
hover, 0.14 checked, 0.16 pressed; 0.45 disabled. The template draws its own 2-unit focus ring in the glass
foreground color (not the window's focus visual), shown for keyboard focus only. In a contrast theme, hover, press
and the checked state are a solid `Highlight` fill with `HighlightText`.

```csharp
public class GlassToolbarSpacer : FrameworkElement
public const double FixedGap = 8;
public GlassSpacerSizing Sizing { get; set; }     // default GlassSpacerSizing.Flexible
```

```xml
<lg:GlassToolbar VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0">
  <Button Content="&#xE72B;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontWeight="Normal" FontSize="15"
          AutomationProperties.Name="Back" ToolTip="Back" />
  <Button Content="Library" />
  <lg:GlassToolbarSpacer />                       <!-- Flexible: leading and trailing clusters -->
  <ToggleButton Content="&#xE734;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontWeight="Normal" FontSize="15"
                AutomationProperties.Name="Favorite" />
</lg:GlassToolbar>
```

```csharp
toolbar.Items.Add(new Button { Content = "Edit" });
toolbar.Items.Insert(2, new GlassToolbarSpacer { Sizing = GlassSpacerSizing.Fixed });
```

## GlassTabBar and GlassTabBarItem

`public class GlassTabBar : ListBox`: a capsule tab bar. A glass lens of the bar's material (a Regular bar has a Regular lens) slides between items on the track
spring, stretches with its speed and can be dragged across the bar. Arrow keys move the selection
(`DirectionalNavigation` = `Contained`); `SelectionMode` = `Single`. `ItemsSource`, `SelectedIndex`,
`SelectedItem`, `SelectionChanged`, `ItemTemplate` and `ItemsPanel` work as on any `ListBox`. `SelectedIndex`
defaults to -1, which draws no selection lens: set it. Once an item is selected, one always is: Ctrl+click on the
selected item and `SelectedIndex = -1` keep the selection, and removing the selected item selects a neighbor. While
dragged, the lens follows the pointer on the track spring lengthened by its size (0.18 s + 0.0005 s per unit of its
longer side, `GlassSprings.TrackWhileDragging`), taking on the size of the item beneath it. Template parts:
`PART_Surface` (`GlassSurface`, the bar), `PART_Lens` (`GlassSurface`, the selection lens, in the bar's material),
`PART_LensHost` (`Canvas`). UI Automation: `Tab` with `TabItem` children and the Selection pattern (single selection,
selection required).

```csharp
public GlassTabBar()
public GlassMaterial Material { get; set; }       // inherited, default Clear; the selection lens takes it too
public GlassAppearance Appearance { get; set; }   // inherited, default Automatic
public GlassMotion Motion { get; set; }           // inherited, default Full
public GlassRadius Radius { get; set; }           // default GlassRadius.Capsule
public double Refraction { get; set; }            // default NaN: bar and lens each follow their size (min(60, 0.75 × shorter side))
public Color? Tint { get; set; }                  // default null; tints the bar and its lens
public bool IsInteractive { get; set; }           // default true
public override void OnApplyTemplate()

public class GlassTabBarItem : ListBoxItem
public GlassTabBarItem()
```

Default styles: bar FontSize 14 SemiBold, Padding 3, `HorizontalAlignment="Center"`, `VerticalAlignment="Center"`,
horizontal `StackPanel` items panel. Item: Height 28, MinWidth 44, Padding `14,0`; label opacity 0.85, 1 when
selected (0.16 s), 0.4 when disabled.

```xml
<lg:GlassTabBar x:Name="Tabs" SelectedIndex="0" VerticalAlignment="Bottom" Margin="0,0,0,24"
                AutomationProperties.Name="Sections">
  <lg:GlassTabBarItem Content="Home" />
  <lg:GlassTabBarItem Content="Explore" />
  <lg:GlassTabBarItem Content="Library" />
</lg:GlassTabBar>
```

```csharp
var tabs = new GlassTabBar { Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" } }, SelectedIndex = 0 };
tabs.SelectionChanged += (_, _) => ShowPage(tabs.SelectedIndex);
```

## GlassSlider

`public class GlassSlider : Slider`: a 6-unit track, an accent fill and a 20 × 16 white thumb that lifts into Clear
glass (refraction 5, growing 2.5 × 2 per side) while dragged; the lifted glass refracts the track. Overrides
`IsMoveToPointEnabled` to default `true` (clicking the track moves the thumb there). All `Slider` behavior applies:
`Minimum`, `Maximum`, `Value`, `SmallChange`, `LargeChange`, `TickFrequency`, `IsSnapToTickEnabled`,
`Orientation`, `IsDirectionReversed`, `ValueChanged`, keyboard steps, UI Automation RangeValue. Template parts:
`PART_Track` (`Track`), `PART_TrackVisual` (`FrameworkElement`), `PART_Fill` (`Border`); the thumb template holds a
`GlassValueThumb` named `PART_Visual`.

```csharp
public GlassSlider()
public Color? Tint { get; set; }    // fill color as given, alpha included; null or A = 0: the Windows accent (Highlight in a contrast theme)
public override void OnApplyTemplate()
```

Default style: MinWidth 80, MinHeight 28 (vertical: MinWidth 28, MinHeight 80); track `#4D808080`; focus ring on
keyboard focus; opacity 0.45 when disabled. No `Material` property: the thumb glass is always Clear. In a contrast
theme the fill is `Highlight`, the rest of the track is `ControlBrush` outlined in `ControlText`, and the thumb is a
solid `ControlText` capsule that does not turn into glass. UI Automation: WPF's own `SliderAutomationPeer`.

```xml
<lg:GlassSlider Minimum="0" Maximum="100" Value="64" AutomationProperties.Name="Volume" />
<lg:GlassSlider Orientation="Vertical" Height="180" Tint="#FF9F0A" AutomationProperties.Name="Warmth" />
```

## GlassSwitch

`public class GlassSwitch : ToggleButton`: a 54 × 24 switch. The 32 × 20 thumb moves on the measured switch spring
(response 0.47 s, damping 0.7) and lifts into Clear glass while pressed (refraction 7.75, growing 9 × 5.5 per side).
Drag the thumb, click, or press Space. `Content` is the label, drawn before the track (column `*`, then the track),
so a stretched switch puts the label left and the track right. `IsChecked`, `Checked`, `Unchecked`, `Command` work as
on `ToggleButton`; `IsChecked = null` parks the thumb in the middle. Under reduced motion the thumb travels with the
same response, critically damped. Template parts: `PART_Track` (`Border`), `PART_Thumb` (`GlassValueThumb`). UI
Automation: a toggle switch (`GlassSwitchAutomationPeer`): ControlType Button with the Toggle pattern, localized
control type "toggle switch" in English and WPF's localized button type in other display languages.

```csharp
public GlassSwitch()
public Color? Tint { get; set; }    // track color when on, alpha included; null or A = 0: the Windows accent color
public override void OnApplyTemplate()
```

Default style: font `Segoe UI Variable Text, Segoe UI`, FontSize 14, `HorizontalContentAlignment="Left"`,
label margin 12 before the track, off track `#66808080`, track color crossfade 200 ms, opacity 0.45 when disabled.
In a contrast theme the track is outlined in `ControlText`: on, a `Highlight` track with a `HighlightText` thumb; off,
a `ControlBrush` track with a `ControlText` thumb.

```xml
<lg:GlassSwitch Content="Wi-Fi" IsChecked="True" />
<lg:GlassSwitch Content="Focus" Tint="#5E5CE6" Checked="OnFocusOn" Unchecked="OnFocusOff" />
```

## GlassMenu and GlassMenuItem

`public class GlassMenu : HeaderedItemsControl`: a glass button (`Header` is its content) that opens a menu. The panel
is a Regular `GlassSurface` (inset 4, min width 160, 6 units from the trigger) that grows out of the trigger on the
morph spring and collapses back into it. Open, its glass mixes 60% toward a solid backing after the material and tint
(`GlassOptics.MenuLegibility` 0.6 toward `GlassPalette.LegibilityBacking`: `#F5F5F7` light, `#1C1C1E` dark), applied
as 0.6 times the open progress clamped to [0, 1], so the backing fades in and out with the morph. Items take
`GlassPalette.MenuForeground`: the appearance's foreground, or on a tint white or `#111113` from the tint mixed toward
the backing (at 0.6, the appearance's foreground for every tint); a highlighted item mixes that color into the glass at
8% (`GlassOptics.MenuHighlight`). Untinted panels, Regular or Clear, keep at least 5.2:1 over any backdrop,
highlighted items included (5.24:1 at worst, white over white on a highlighted Clear item in dark appearance), and
panels tinted with the platform's system colors at least 5.0:1 (5.07:1 at worst, the Apple dark-appearance yellow;
Windows accent colors stay above 5.6:1); very light tints (pure yellow 4.37:1,
white 4.13:1 in dark appearance over white) are not covered. The fallback material paints the backing over its veil
or tint fill at `GlassFallback.LegibilityOpacity`, which keeps its labels at least as legible as the shader's where
they contrast least, and the opaque material (transparency reduced, no scene) paints it over its fill at the same
opacity, so it keeps those floors too (5.11:1 over the system colors, 12.6:1 untinted). Shortcut text is drawn in the
label's color, so the floors hold for it. The trigger and other glass draw without the backing, and it is not an option. It closes when an item is
chosen, on Escape, Alt+F4, a click outside and when the window deactivates; Down or Enter on the trigger opens it with
the first item focused; arrow keys and Tab cycle through items. The panel floats in the enclosing scene's top layer,
clamped inside the scene (it cannot leave the window), and lines up with the trigger's start edge (in right-to-left
layouts too); outside a scene, or when the menu sits in the scene's `Content`, it opens in a `Popup` with the opaque
material. `IsOpen="True"` set before the menu is loaded (in XAML, or from a binding whose source starts true) opens it
once it is loaded. Template part: `PART_Trigger` (`GlassButton`). Not focusable itself (the trigger is). The menu and
its panel are focus scopes, as WPF's menus are: a routed command on an item (`ApplicationCommands.Copy`) that nothing
in the menu handles goes on to the element that has focus in the window (the text box whose selection it copies).

```csharp
public GlassMenu()
public bool IsOpen { get; set; }                     // default false, BindsTwoWayByDefault
public GlassMenuPlacement Placement { get; set; }    // default GlassMenuPlacement.Bottom
public GlassMaterial PanelMaterial { get; set; }     // default GlassMaterial.Regular
public GlassRadius PanelRadius { get; set; }         // default GlassRadius.FromValue(28); capsule and circle work too
public GlassMotion Motion { get; set; }              // inherited, default Full; Reduced: morph critically damped; None: opens and closes at once
public Color? Tint { get; set; }                     // default null; tints the trigger and the panel
public GlassButtonSize Size { get; set; }            // trigger size, default GlassButtonSize.Regular
public bool IsIconOnly { get; set; }                 // circular icon trigger, default false
public event RoutedEventHandler Opened               // OpenedEvent, bubbling, after the menu opened
public event RoutedEventHandler Closed               // ClosedEvent, bubbling, after the menu closed
public void Close()                                  // morph back into the trigger, focus returns to it
public override void OnApplyTemplate()
```

The trigger receives `Header`, `HeaderTemplate`, `Size`, `IsIconOnly`, `Tint`, `FontFamily`, `FontWeight` and
`AutomationProperties.Name` from the menu. Its `FontSize` and `Foreground` are the menu's when the application sets
them (then the trigger and the items share them); otherwise its `FontSize` follows `Size` (12 / 14 / 15) while the
items use the menu's default 14, and its `Foreground` is its own glass's while the items take the panel's (the menu's
default `Foreground`, chosen on the backed glass), which can differ on a tint. An icon-glyph `Header` (only private-use characters) draws at FontWeight Normal automatically.
Items draw in the glass text font (`Segoe UI Variable Text, Segoe UI`) whatever font the trigger uses, so an
icon-only menu's icon font does not reach them; set `FontFamily` on an item, or in `ItemContainerStyle`, to change
it. The panel copies the menu's resolved appearance, motion, render mode, accessibility options, text size and flow
direction. `Separator`s inside a menu get a 1-unit line at 0.14 opacity with margin `10,4`.

Items are `GlassMenuItem` and `Separator` elements declared inside the menu (or added to `Items` in code), or data
objects from `ItemsSource`: each data item gets a `GlassMenuItem` container (its `Content` and `DataContext` are the
item) shown with the menu's `ItemTemplate` or `ItemTemplateSelector` and `ItemStringFormat`. `ItemContainerStyle`
and `ItemContainerStyleSelector` apply to every `GlassMenuItem`; separators and other elements keep their own style.
`DisplayMemberPath` is not passed on to the panel: use an `ItemTemplate`. Items' routed events (`Click`, `Checked`)
bubble through the panel in the scene's top layer, not through the menu: handle them on the item (an `EventSetter`
in `ItemContainerStyle` for data items), or use commands.

```csharp
public class GlassMenuItem : ButtonBase
public GlassMenuItem()
public object? Header { get; set; }             // the label (AddOwner of HeaderedContentControl.HeaderProperty), see below
public object? Icon { get; set; }               // drawn before the label; a glyph string renders in the icon font automatically
public string InputGestureText { get; set; }    // default "": shortcut text after the label, in its color
public bool IsCheckable { get; set; }           // default false: choosing toggles IsChecked
public bool IsChecked { get; set; }             // default false, BindsTwoWayByDefault; shows a check mark (glyph E73E)
public bool StaysOpenOnClick { get; set; }      // default false
public event RoutedEventHandler Checked         // CheckedEvent = MenuItem.CheckedEvent.AddOwner, bubbling
public event RoutedEventHandler Unchecked       // UncheckedEvent = MenuItem.UncheckedEvent.AddOwner, bubbling
```

The template shows `Content`. `Header` fills `Content` and keeps it current when the header changes; `Content` set by
the application takes over from the header. `Checked` and `Unchecked` (the same routed events as WPF's `MenuItem`)
are raised however `IsChecked` changes: a click, UI Automation or code. `InputGestureText` is display only: register
the shortcut yourself with a `KeyBinding` in `Window.InputBindings`.

`Click`, `Command`, `CommandParameter` come from `ButtonBase`; Enter clicks the focused item; hovering focuses it.
Item style: MinHeight 32, Padding `10,6,12,6`, FontWeight Normal, highlight `GlassOptics.MenuHighlight` (0.08) on hover, focus and press,
0.4 opacity when disabled. In a contrast theme, hover, keyboard focus and press are a solid `Highlight` fill with
`HighlightText` (keyboard focus adds a `ControlText` border).

UI Automation: the trigger button, which takes keyboard focus, is a Button with ExpandCollapse and the menu's state;
the `GlassMenu` element itself (`GlassMenuAutomationPeer`) is in the raw view only, out of the control and content
views. The open panel is a Menu named after the menu's automation name or else its current header, raising
MenuOpened and MenuClosed; its items are MenuItems with Invoke and, when checkable, Toggle, and both choose the item
exactly as a click does (toggle, `Click`, command, close unless `StaysOpenOnClick`). On a menu whose label changes,
let the header name it rather than a fixed `AutomationProperties.Name`.

```xml
<lg:GlassMenu Header="Sort" Placement="Bottom">
  <lg:GlassMenuItem Header="Name" IsCheckable="True" IsChecked="True" Click="OnSort" />
  <lg:GlassMenuItem Header="Date modified" IsCheckable="True" Click="OnSort" />
  <Separator />
  <lg:GlassMenuItem Header="Show hidden files" IsCheckable="True" StaysOpenOnClick="True" />
</lg:GlassMenu>

<lg:GlassMenu IsIconOnly="True" Header="&#xE712;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
              AutomationProperties.Name="More">
  <lg:GlassMenuItem Header="Copy" Icon="&#xE8C8;" InputGestureText="Ctrl+C" Command="ApplicationCommands.Copy" />
</lg:GlassMenu>

<!-- Items from data: each becomes a GlassMenuItem showing the item with ItemTemplate. -->
<lg:GlassMenu Header="Open recent" ItemsSource="{Binding RecentFiles}">
  <lg:GlassMenu.ItemTemplate>
    <DataTemplate><TextBlock Text="{Binding Name}" /></DataTemplate>
  </lg:GlassMenu.ItemTemplate>
  <lg:GlassMenu.ItemContainerStyle>
    <Style TargetType="lg:GlassMenuItem">
      <EventSetter Event="Click" Handler="OnOpenRecent" />   <!-- sender's DataContext is the data item -->
    </Style>
  </lg:GlassMenu.ItemContainerStyle>
</lg:GlassMenu>
```

```csharp
menu.IsOpen = true;                        // or menu.Close()
menu.Opened += (_, _) => Log("opened");
```

## GlassProgressiveBlur

`public class GlassProgressiveBlur : FrameworkElement`: progressive edge blur over the scene's backdrop: six blur
levels whose strength rises toward `Edge` across a band `Size` units deep (σ of level *i* = `Blur · (i/6)²`). Glass
above the band refracts the blurred backdrop. Place it among the scene's `Children`, stretched over the area whose
edge should blur (it draws only the band). Not hit-test visible. Desired size is `(0, Size)` for Top/Bottom and
`(Size, 0)` for Left/Right, so leave its alignment at `Stretch`.

```csharp
public GlassProgressiveBlur()
public GlassEdge Edge { get; set; }        // default GlassEdge.Top
public double Size { get; set; }           // default 80 (GlassProgressive.DefaultSize); >= 0 and finite
public double Blur { get; set; }           // default 20 (GlassProgressive.DefaultBlur); 0..MaxBlur, larger throws ArgumentException
public const double MaxBlur = 33;
public UIElement? Source { get; set; }     // default null = the enclosing scene's backdrop; set it to blur an element outside a scene
```

`Blur` renders exactly at every display scale. WPF draws one blur of at most 100 device pixels of radius, so where a
level needs more (its strongest levels on high-DPI displays) it is drawn in several passes whose variances add up to
the level's σ; each pass after the first reads the one before through a brush. `MaxBlur` keeps the strongest level
to a few passes even at 300%.

```xml
<lg:GlassProgressiveBlur Edge="Top" Size="120" Blur="20" />
<lg:GlassProgressiveBlur Edge="Bottom" Size="104" />
```

## GlassGeometry

`public static class GlassGeometry`: continuous-corner outlines as frozen WPF `Geometry`, identical to the glass
outline. Use for clips, borders, focus rings and hit shapes that must match glass.

```csharp
public static Geometry Create(Size size, GlassRadius radius)          // outline of size at the origin
public static Geometry Create(Rect bounds, GlassRadius radius)        // outline filling bounds; Geometry.Empty when empty
public static Geometry Inflate(Rect bounds, GlassRadius radius, double amount)   // grown on every side, corners concentric
public static GlassRadius InflateRadius(GlassRadius radius, double amount)      // radius + amount / 1.528 (fixed radii only)
```

`InflateRadius` with a negative amount gives the exact concentric radius of an element inset inside a
continuous-corner shape: `GlassGeometry.InflateRadius(28, -12)`.

```csharp
artwork.Clip = GlassGeometry.Create(new Rect(artwork.RenderSize), GlassGeometry.InflateRadius(32, -14));
var ring = GlassGeometry.Inflate(new Rect(0, 0, 200, 64), GlassRadius.FromValue(20), 2);
```

## GlassDiagnostics

`public static class GlassDiagnostics`: how glass renders on this machine, for about boxes, logs and support.

```csharp
public static GlassShaderSource ShaderSource { get; }
public static string ShaderStatus { get; }               // one line: how the shader loaded, or why it could not
public static bool CanUseShader(Visual visual)           // pixel shader 3.0 in hardware for that visual's window
public static string Describe()                          // "tier 2, ps_3_0 supported (… slots), shader Precompiled: …"
```

```csharp
var shader = GlassDiagnostics.CanUseShader(this) && GlassScene.GetRenderMode(this) != GlassRenderMode.Fallback;
Status.Text = shader ? "Rendering with the glass shader." : "Rendering the fallback material.";
Trace.WriteLine(GlassDiagnostics.Describe());   // using System.Diagnostics;
```

## Template parts: GlassLens and GlassValueThumb

Only needed when retemplating a control.

`public sealed class GlassLens : FrameworkElement`: the glass of one surface, beneath its content (`PART_Lens` in
the `GlassSurface` template). Holds live `VisualBrush` copies of what lies beneath and runs the shader or draws the
fallback. Not hit-test visible, not focusable. Material, appearance and accessibility options are inherited.

```csharp
public GlassLens()
public GlassRadius Radius { get; set; }          // default GlassRadius.FromValue(16)
public double Refraction { get; set; }           // default NaN: min(60, 0.75 × the lens's shorter resting side)
public Color? Tint { get; set; }                 // default null
public bool IsFocusRingVisible { get; set; }     // default false
public GlassLensMode Mode { get; }               // starts as GlassLensMode.Fallback until loaded
```

`public sealed class GlassValueThumb : FrameworkElement`: the slider/switch thumb (`PART_Visual` in the slider thumb
template, `PART_Thumb` in the switch template). An opaque white capsule with a soft shadow that crossfades into Clear
glass while lifted (growing only under full motion; reduced motion crossfades at the resting size). In a contrast
theme it is a solid system-color capsule.

```csharp
public GlassValueThumb()
public bool IsLifted { get; set; }          // default false
public double GrowX { get; set; }           // default 2.5 (slider); the switch template sets 9
public double GrowY { get; set; }           // default 2 (slider); the switch template sets 5.5
public double LensRefraction { get; set; }  // default 5 (slider); the switch template sets 7.75
public UIElement? Backdrop { get; set; }    // default null: the track the lifted glass refracts
```

`GlassMenu`, `GlassSwitch`, `GlassTabBar` and `GlassValueThumb` are advanced by the library's frame loop through an
internal interface; they have no public `Tick` method.

Named parts per control: `GlassSurface` → `PART_Root`, `PART_Lens`; `GlassButton` → `PART_Surface`; `GlassTabBar` →
`PART_Surface`, `PART_Lens`, `PART_LensHost`; `GlassToolbar` → `PART_Host`; `GlassSlider` → `PART_Track`,
`PART_TrackVisual`, `PART_Fill` (+ `PART_Visual` in the thumb); `GlassSwitch` → `PART_Track`, `PART_Thumb`;
`GlassMenu` → `PART_Trigger`.

## Automation peers

```csharp
public sealed class GlassSurfaceAutomationPeer : FrameworkElementAutomationPeer          // Group; a control element only when named
public GlassSurfaceAutomationPeer(GlassSurface owner)
public sealed class GlassToolbarAutomationPeer : FrameworkElementAutomationPeer          // ToolBar
public GlassToolbarAutomationPeer(GlassToolbar owner)
public sealed class GlassTabBarAutomationPeer : ListBoxAutomationPeer, ISelectionProvider   // Tab; single selection, IsSelectionRequired = true
public GlassTabBarAutomationPeer(GlassTabBar owner)
public sealed class GlassTabBarItemAutomationPeer : ListBoxItemAutomationPeer            // TabItem
public GlassTabBarItemAutomationPeer(object owner, SelectorAutomationPeer selectorAutomationPeer)
public sealed class GlassSwitchAutomationPeer : ToggleButtonAutomationPeer               // Button with Toggle; "toggle switch" in English
public GlassSwitchAutomationPeer(GlassSwitch owner)
public sealed class GlassMenuAutomationPeer : FrameworkElementAutomationPeer, IExpandCollapseProvider   // raw view only
public GlassMenuAutomationPeer(GlassMenu owner)
public ExpandCollapseState ExpandCollapseState { get; }
public void Expand()
public void Collapse()
public sealed class GlassMenuItemAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider, IToggleProvider
public GlassMenuItemAutomationPeer(GlassMenuItem owner)
public ToggleState ToggleState { get; }
public void Invoke()
public void Toggle()                                                                      // pattern exposed only when IsCheckable
```

`GlassButton` and `GlassSlider` use WPF's own `ButtonAutomationPeer` and `SliderAutomationPeer`; a `GlassMenu`'s
trigger button uses an internal `ButtonAutomationPeer` subclass that adds the menu's ExpandCollapse state (it has
keyboard focus, so screen readers announce the change from it), and the open panel and its item host have internal
peers (a Menu; the host adds no element of its own). `GlassSwitchAutomationPeer` reports ControlType Button with the
Toggle pattern; its localized control type is "toggle switch" in English and WPF's localized button type in other
display languages.

## Theme resources and default styles

All default styles live in `Themes/Generic.xaml` (`ThemeInfo(None, SourceAssembly)`); retemplate or restyle as with
any WPF control. Setting `Style` on a control keeps its theme template. Shared resources keyed by
`ComponentResourceKey`:

| Key | Type | Value |
| --- | --- | --- |
| `{ComponentResourceKey TypeInTargetAssembly={x:Type lg:GlassScene}, ResourceId=Font}` | `FontFamily` | `Segoe UI Variable Text, Segoe UI` |
| `{ComponentResourceKey TypeInTargetAssembly={x:Type lg:GlassScene}, ResourceId=IconFont}` | `FontFamily` | `Segoe Fluent Icons, Segoe MDL2 Assets` |
| `{ComponentResourceKey TypeInTargetAssembly={x:Type lg:GlassToolbar}, ResourceId=ButtonStyle}` | `Style` (`ButtonBase`) | the toolbar button style (its template draws its own focus ring) |

Every glass control's style binds `Foreground` to its own `GlassScene.ContentForeground`; a local `Foreground`
overrides it. The `GlassButton`, toolbar button and `GlassMenu` styles set FontWeight Normal on content (or a
header) that is only icon-font glyphs (private-use characters).

## Optical model (LiquidGlass namespace)

`LiquidGlassEngineering.Core` (`netstandard2.0`) is the platform-independent model the controls are built on. App
code rarely needs more than `GlassMaterial`, `GlassMotion` and `GlassRadius`; the rest is listed for completeness,
custom renderers and tests.

### Constants

```csharp
public static class GlassOptics
{
    public const double Bevel = 20;                 // refracting bevel depth inside the outline
    public const double DefaultRefraction = 60;            // the full travel; unset refraction uses GlassRefraction.Default
    public const double ContinuousExtent = 1.528;   // corner reach along each edge, relative to the radius
    public const double ContinuousExponent = 3.26;  // superellipse exponent
    public const double LumaRed = 0.213, LumaGreen = 0.715, LumaBlue = 0.072;   // declared as three constants
    public const double MenuLegibility = 0.6;       // open menu panels mix toward GlassPalette.LegibilityBacking (times the open progress); 0 elsewhere
    public const double MenuHighlight = 0.08;       // a highlighted menu row mixes its label color into the glass
}

public static class GlassMaterials
{
    public const double ClearBlur = 0;
    public const double RegularBlur = 7.33;
    public const double ClearTintBody = 0.68;
    public const double RegularTintBody = 0.78;
    public static ClearToneSpec ClearLight { get; }
    public static ClearToneSpec ClearDark { get; }
    public static RegularToneSpec RegularLight { get; }   // fill σ 23
    public static RegularToneSpec RegularDark { get; }    // fill σ 21.8
    public static ClearToneSpec Clear(bool dark)
    public static RegularToneSpec Regular(bool dark)
    public static double TintBody(GlassMaterial material)
    public static double Blur(GlassMaterial material)
    public static double FillSigmaOverFrost(bool dark)
}
public sealed class ClearToneSpec { double ShadowLift, HighlightRolloff, VeilScale, VeilOffset, VeilWeight { get; } }
public sealed class RegularToneSpec { double FillSigma, FillOpacity, A, B, Q, Chroma, ToneWeight { get; } }

public static class GlassProgressive
{
    public const int Levels = 6;
    public const double DefaultSize = 80;
    public const double DefaultBlur = 20;
    public static double Ease(double u)                        // u²(3 − 2u)
    public static double LevelBlur(int level, double blur)     // blur · (level / 6)²
    public static double Weight(int level, double u)
    public static double LayerOpacity(int level, double u)     // level 1..6, else ArgumentOutOfRangeException
}

public static class GlassControlMotion
{
    public const double SliderThumbWidth = 20, SliderThumbHeight = 16, SliderThumbGrowX = 2.5, SliderThumbGrowY = 2,
        SliderThumbRefraction = 5;
    public const double SwitchTrackWidth = 54, SwitchTrackHeight = 24, SwitchThumbWidth = 32, SwitchThumbHeight = 20,
        SwitchThumbGrowX = 9, SwitchThumbGrowY = 5.5, SwitchThumbRefraction = 7.75;
    public const double PressCrossfadeDuration = 0.2, TapThreshold = 0.08, SwitchResponse = 0.47, SwitchDamping = 0.7;
    public static void SwitchSpringStep(ref double value, ref double velocity, double target, double dt)
    public static double EaseInOut(double x)                   // cubic-bezier(0.42, 0, 0.58, 1)
    public static double TapEnvelope(double seconds)
    public static double TapEnvelopeDuration { get; }
    public static GlassScale ThumbStretch(double speed, double progress)
}
```

(Each constant is declared on its own line in the source; they are grouped here for brevity.)

### Springs

```csharp
public readonly struct SpringOptions : IEquatable<SpringOptions>
{
    public SpringOptions(double duration, double bounce)   // duration > 0 finite; -1 < bounce < 1
    public double Duration { get; }
    public double Bounce { get; }
    public double Stiffness { get; }                       // (2π / duration)²
    public double Damping { get; }
}

public static class GlassSprings
{
    public static SpringOptions Press { get; }     // (0.24, 0.2)
    public static SpringOptions Release { get; }   // (0.5, 0.42)
    public static SpringOptions Glow { get; }      // (0.42, 0)
    public static SpringOptions Morph { get; }     // (0.42, 0.18)
    public static SpringOptions Layout { get; }    // (0.4, 0.16)
    public static SpringOptions Track { get; }     // (0.18, 0)
    public static SpringOptions TrackWhileDragging(double width, double height)   // (0.18 + 0.0005 · max(width, height), 0)
    public static SpringOptions Reduced(SpringOptions spring)   // same duration, bounce 0
}

public readonly struct SpringState
{
    public SpringState(double value, double velocity, double target)
    public double Value { get; }  public double Velocity { get; }  public double Target { get; }
    public bool IsSettled { get; }
}

public static class SpringSolver
{
    public static SpringState Step(SpringState state, SpringOptions options, double dt)   // closed form, any dt
}

public sealed class Spring
{
    public Spring(double value, SpringOptions options)
    public double Value { get; }  public double Velocity { get; }
    public double Target { get; set; }  public SpringOptions Options { get; set; }
    public bool IsSettled { get; }
    public Spring Configure(SpringOptions options)
    public void Animate(double target, SpringOptions options)   // keeps velocity: interruptible
    public void Shift(double delta)
    public void Jump(double value)
    public void Step(double dt)
}
```

Drive a spring from `CompositionTarget.Rendering` with the real frame time, and unsubscribe once it settles (a
permanent handler keeps WPF rendering every frame even when idle):

```csharp
var spring = new Spring(0, GlassSprings.Morph);
spring.Animate(1, GlassSprings.Morph);
TimeSpan? last = null;
void OnFrame(object? sender, EventArgs e)
{
    var now = ((RenderingEventArgs)e).RenderingTime;
    if (now == last) return;                                     // Rendering can fire more than once per frame
    var dt = last is { } previous ? (now - previous).TotalSeconds : 1 / 60.0;
    last = now;
    spring.Step(dt);
    panel.Opacity = spring.Value;
    if (spring.IsSettled) CompositionTarget.Rendering -= OnFrame;
}
CompositionTarget.Rendering += OnFrame;
```

### Shape, refraction, interaction

```csharp
public readonly struct ShapeParameters : IEquatable<ShapeParameters>
{ public ShapeParameters(double extent, double exponent); public double Extent { get; } public double Exponent { get; } }
public readonly struct ShapeSample
{ public ShapeSample(double distance, double normalX, double normalY); public double Distance { get; } public double NormalX { get; } public double NormalY { get; } }

public static class GlassShape
{
    public static ShapeParameters Parameters(double width, double height, GlassRadius radius)
    public static ShapeSample Field(double x, double y, double width, double height, ShapeParameters shape)   // from the center, y down
    public static IReadOnlyList<GlassVector> Outline(double width, double height, GlassRadius radius, int segments = 24)
}

public static class GlassRefraction
{
    public static double EdgeProfile(double distance, double bevel = GlassOptics.Bevel)
    public static double Default(double width, double height)   // min(60, 0.75 × max(0, min(width, height))), the unset refraction
    public static GlassVector Offset(double distance, double normalX, double normalY, double refraction = GlassOptics.DefaultRefraction)
    public static double Coverage(double distance, double pixel)
}

public readonly struct GlassScale { public GlassScale(double sx, double sy); public static GlassScale Identity { get; } public double Sx { get; } public double Sy { get; } }
public readonly struct GlowParameters { public GlowParameters(double radius, double alpha, double hover); public double Radius { get; } public double Alpha { get; } public double Hover { get; } }

public static class GlassInteractionModel
{
    public const double SmallControlSize = 72;
    public static double RubberBand(double offset, double limit, double coefficient = 0.55)
    public static double StretchLimit(double width, double height)
    public static GlassScale PressScale(double width, double height, double press, double tx = 0, double ty = 0,
        double vx = 0, double vy = 0, bool reduced = false)
    public static GlowParameters Glow(double width, double height, bool dark)
}
```

### Color, pixel and fallback

```csharp
public readonly struct Rgb : IEquatable<Rgb>
{ public Rgb(double r, double g, double b); public double R, G, B { get; } public double Luma { get; }
  public static Rgb FromBytes(byte r, byte g, byte b); public Rgb Map(Func<double, double> f); }
public readonly struct Rgba : IEquatable<Rgba>
{ public Rgba(double r, double g, double b, double a); public Rgba(Rgb rgb, double a); public double R, G, B, A { get; } public Rgb Rgb { get; } }
public readonly struct GlassVector : IEquatable<GlassVector> { public GlassVector(double x, double y); public double X { get; } public double Y { get; } }

public static class GlassMaterialOptics
{
    public static double Luma(Rgb color)
    public static Rgb Saturate(Rgb c, double s)
    public static double ClearTone(double v, bool dark, bool nested = false)
    public static double RegularTone(double v, bool dark)
    public static Rgb MaterialColor(Rgb color, GlassMaterial material, bool dark, bool nested = false)
    public static Rgb TintColor(Rgb color, Rgb tint, GlassMaterial material)
    public static double TintAlpha(double alpha, GlassMaterial material)
    public static Rgb FrostColor(Rgb frost, Rgb fill, bool dark)
    public static Rgb LegibilityColor(Rgb color, bool dark, double amount)   // mix(color, LegibilityBacking(dark), amount)
}

public readonly struct GlassSurfaceOptics
{
    public GlassSurfaceOptics(double width, double height, GlassRadius radius, GlassMaterial material = GlassMaterial.Clear,
        bool dark = false, double refraction = GlassOptics.DefaultRefraction, Rgb? tint = null, double pixel = 1.0 / 3, bool nested = false,
        double legibility = 0)   // legibility: GlassOptics.MenuLegibility for an open menu panel
    // Width, Height, Radius, Material, Dark, Refraction, Tint, Pixel, Nested, Legibility { get; }
    // (the 1.0.0 constructor without legibility is kept, hidden from IntelliSense, for compiled callers)
}
public static class GlassPixel
{
    public static Rgba Compose(double x, double y, in GlassSurfaceOptics optics, Func<double, double, Rgba> sample, Rgba backdrop)
    public static double GlowAlpha(double x, double y, double centerX, double centerY, double radius, bool dark, double amount)
}

public static class GlassRimLight
{ public static Rgba Overlay(double distance, double normalX, double normalY, bool dark, double pixel, Rgb? tint = null) }
public static class GlassRimRaster
{ public static RimImage Render(double width, double height, GlassRadius radius, bool dark, double scale, Rgb? tint = null) }
public sealed class RimImage { int PixelWidth, PixelHeight, Padding { get; } double Pixel { get; } int Stride { get; } byte[] Pixels { get; } }

public sealed class FrostFillKernel
{
    public const int Radius = 4;
    public double Sigma { get; }  public double Spacing { get; }  public IReadOnlyList<double> Weights { get; }
    public double Variance { get; }
    public static FrostFillKernel For(bool dark)
}

public static class GlassPalette
{
    public static Rgba ReducedTransparencyLight { get; }   // #F2F2F7 at 0.94
    public static Rgba ReducedTransparencyDark { get; }    // #1C1C1E at 0.94
    public static Rgba ContrastBorderLight { get; }        // black at 0.18
    public static Rgba ContrastBorderDark { get; }         // white at 0.28
    public static Rgb ForegroundLight { get; }             // #111113
    public static Rgb ForegroundDark { get; }              // #FFFFFF
    public static Rgb LegibilityBackingLight { get; }      // #F5F5F7, the menu panel backing
    public static Rgb LegibilityBackingDark { get; }       // #1C1C1E
    public static Rgba ReducedTransparency(bool dark)
    public static Rgba ContrastBorder(bool dark)
    public static Rgb Foreground(bool dark)
    public static Rgb LegibilityBacking(bool dark)
    public static Rgb ForegroundOnTint(Rgb tint, GlassMaterial material)  // white or #111113, the higher WCAG contrast with the tinted glass
    public static Rgb MenuForeground(Rgb? tint, GlassMaterial material, bool dark, double legibility = GlassOptics.MenuLegibility)
        // menu labels: Foreground(dark) untinted; on a tint, white or #111113 by contrast with mix(tint·pass, backing, legibility)
    public static double RelativeLuminance(Rgb color)                      // WCAG 2
}
public readonly struct GlassVeil { public GlassVeil(double gray, double opacity); public double Gray { get; } public double Opacity { get; } public double Apply(double v); }
public static class GlassFallback
{
    public const double TintFillOpacity = 0.9;
    public static GlassVeil Veil(GlassMaterial material, bool dark)   // the veil fitted to the tone curve
    public static Rgb TintFill(Rgb tint, GlassMaterial material)      // tint · (body + (1 − body)·0.5), at TintFillOpacity
    public static double LegibilityOpacity(GlassMaterial material, bool dark, Rgb? tint, double legibility)
        // the 8-bit opacity of the backing over the fill: labels keep at least the shader's contrast where they contrast least
}

public static class GlassMath
{
    public static double Clamp(double x, double lo, double hi)
    public static double Sat(double x)
    public static double Mix(double a, double b, double t)
    public static double SmoothStep(double edge0, double edge1, double x)
}
```

Members written as `double R, G, B { get; }` above are separate properties in the source, compressed here.
