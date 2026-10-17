# Recipes: Liquid Glass for WPF

Seven complete windows built with `LiquidGlassEngineering.Wpf` 1.0.0. Each is a XAML file plus its code-behind in one
WPF project named `GlassRecipes`; set `StartupUri` in `App.xaml` to the window you want to run.

## Contents

1. [App shell: floating tab bar over content](#1-app-shell-floating-tab-bar-over-content)
2. [Toolbar over a photo](#2-toolbar-over-a-photo)
3. [Media card with a slider](#3-media-card-with-a-slider)
4. [Quick-settings card with switches](#4-quick-settings-card-with-switches)
5. [Glass menu from a button](#5-glass-menu-from-a-button)
6. [Progressive blur over a scrolling list](#6-progressive-blur-over-a-scrolling-list)
7. [Draggable glass card](#7-draggable-glass-card)

## Setup shared by every recipe

```
dotnet new wpf -n GlassRecipes
cd GlassRecipes
dotnet nuget add source https://liquid-glass-engineering.vercel.app/nuget/index.json -n liquid-glass
dotnet add package LiquidGlassEngineering.Wpf --version 1.0.0
```

Then replace the generated project file and `App.xaml` with the ones below, add `app.manifest` and the images under
`Assets/`, and add each recipe's two files next to `App.xaml` (the template's `MainWindow` can stay or go). Builds anywhere with
`<EnableWindowsTargeting>true</EnableWindowsTargeting>`; runs on Windows 10 1809+ or Windows 11.

```xml
<!-- GlassRecipes.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <RootNamespace>GlassRecipes</RootNamespace>
    <!-- Per-monitor DPI awareness keeps the one-pixel rim crisp on every monitor. -->
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LiquidGlassEngineering.Wpf" Version="1.0.0" />
  </ItemGroup>
  <ItemGroup>
    <!-- Backdrops: any vivid images; album.jpg should be square. -->
    <Resource Include="Assets\wallpaper.jpg;Assets\photo.jpg;Assets\album.jpg" />
  </ItemGroup>
</Project>
```

```xml
<!-- App.xaml: fonts shared by the recipes -->
<Application x:Class="GlassRecipes.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="ShellWindow.xaml">
  <Application.Resources>
    <FontFamily x:Key="DisplayFont">Segoe UI Variable Display, Segoe UI</FontFamily>
    <FontFamily x:Key="IconFont">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>
  </Application.Resources>
</Application>
```

`app.manifest` (the XML declaration must stay on the first line):

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="GlassRecipes.app" />
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

```csharp
// App.xaml.cs
using System.Windows;

namespace GlassRecipes;

public partial class App : Application
{
}
```

Glass needs color and detail behind it: over a flat fill it reads as a gray pill. Every window turns on
`UseLayoutRounding` so the one-pixel rim lands on device pixels. Design defaults used throughout: Clear over imagery,
Regular for bars over text and for compact cards with a few lines of text (with the controls inside reset to Clear),
solid surfaces for long text, capsules for controls, circles for icon buttons, card radii of 16 to 28, concentric
corners, at most one tinted control per window.

## 1. App shell: floating tab bar over content

The current page is the scene's backdrop, so swapping it swaps what the toolbar and the tab bar refract. Photo pages
get Clear glass; the text page gets Regular, light glass. Both options are inherited, so one call on the scene
restyles every bar.

`ShellWindow.xaml`

```xml
<Window x:Class="GlassRecipes.ShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Glass shell" Width="1100" Height="740" MinWidth="720" MinHeight="520"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="#101014">
  <lg:GlassScene x:Name="Scene">
    <lg:GlassScene.Content>
      <!-- The backdrop: whatever page is showing. -->
      <ContentControl x:Name="PageHost" Focusable="False" />
    </lg:GlassScene.Content>

    <lg:GlassToolbar VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0">
      <Button Content="&#xE700;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Navigation" ToolTip="Navigation" />
      <TextBlock x:Name="PageTitle" FontFamily="{StaticResource DisplayFont}" FontSize="15" FontWeight="SemiBold"
                 VerticalAlignment="Center" Margin="6,0,12,0" />
      <lg:GlassToolbarSpacer />
      <Button Content="&#xE721;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Search" ToolTip="Search" />
      <Button Content="&#xE77B;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Account" ToolTip="Account" />
    </lg:GlassToolbar>

    <lg:GlassTabBar x:Name="Tabs" VerticalAlignment="Bottom" Margin="0,0,0,24" SelectedIndex="0"
                    AutomationProperties.Name="Sections">
      <lg:GlassTabBarItem Content="Home" />
      <lg:GlassTabBarItem Content="Explore" />
      <lg:GlassTabBarItem Content="Library" />
    </lg:GlassTabBar>
  </lg:GlassScene>
</Window>
```

`ShellWindow.xaml.cs`

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiquidGlass;
using LiquidGlass.Wpf;

namespace GlassRecipes;

public partial class ShellWindow : Window
{
    private static readonly string[] Titles = { "Home", "Explore", "Library" };

    public ShellWindow()
    {
        InitializeComponent();
        Tabs.SelectionChanged += (_, _) => ShowPage(Tabs.SelectedIndex);
        ShowPage(Tabs.SelectedIndex);
    }

    private void ShowPage(int index)
    {
        if (index < 0) return;
        PageTitle.Text = Titles[index];
        PageHost.Content = index switch
        {
            0 => Photo("wallpaper.jpg"),
            1 => Photo("photo.jpg"),
            _ => ReadingList(),
        };
        // Clear over imagery, Regular (and light, to match the paper) over text.
        var text = index == 2;
        GlassScene.SetMaterial(Scene, text ? GlassMaterial.Regular : GlassMaterial.Clear);
        GlassScene.SetAppearance(Scene, text ? GlassAppearance.Light : GlassAppearance.Automatic);
    }

    private static UIElement Photo(string name)
    {
        var image = new Image
        {
            Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{name}")),
            Stretch = Stretch.UniformToFill,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    private static UIElement ReadingList()
    {
        var ink = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x19));
        var subtle = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x62));
        // Top and bottom margins let the first and last lines scroll clear of the bars.
        var list = new StackPanel { MaxWidth = 640, Margin = new Thickness(40, 96, 40, 120) };
        for (var i = 1; i <= 14; i++)
        {
            list.Children.Add(new TextBlock
            {
                Text = $"Chapter {i}", FontSize = 22, FontWeight = FontWeights.SemiBold,
                Foreground = ink, Margin = new Thickness(0, 24, 0, 6),
            });
            list.Children.Add(new TextBlock
            {
                Text = "Glass bends the page beneath it: lines near the rim swim inward while the frost keeps every label readable.",
                FontSize = 15, LineHeight = 23, TextWrapping = TextWrapping.Wrap, Foreground = subtle,
            });
        }
        return new ScrollViewer
        {
            Content = list,
            Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF5, 0xF2)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
    }
}
```

## 2. Toolbar over a photo

A top toolbar split into a leading and a trailing cluster by a flexible spacer, an editing toolbar whose fixed spacer
sets Delete apart, and a caption capsule. Toolbar items are plain `Button`/`ToggleButton`s; the toolbar styles them.

`PhotoWindow.xaml`

```xml
<Window x:Class="GlassRecipes.PhotoWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Photo" Width="1100" Height="740" MinWidth="640" MinHeight="480"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="Black">
  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Image Source="/Assets/photo.jpg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality" />
    </lg:GlassScene.Content>

    <lg:GlassToolbar VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0">
      <Button Content="&#xE72B;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Back" ToolTip="Back" Click="OnBack" />
      <Button Content="Library" />
      <lg:GlassToolbarSpacer />
      <ToggleButton x:Name="Favorite" Content="&#xE734;" FontFamily="{StaticResource IconFont}" FontWeight="Normal"
                    FontSize="15" AutomationProperties.Name="Favorite" ToolTip="Favorite" Click="OnFavorite" />
      <Button Content="&#xE72D;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Share" ToolTip="Share" />
      <Button Content="&#xE712;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="More" ToolTip="More" />
    </lg:GlassToolbar>

    <lg:GlassToolbar VerticalAlignment="Bottom" Margin="0,0,0,28">
      <Button Content="&#xE7A8;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Crop" ToolTip="Crop" />
      <Button Content="&#xE7AD;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Rotate" ToolTip="Rotate" />
      <Button Content="&#xE790;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Adjust color" ToolTip="Adjust color" />
      <lg:GlassToolbarSpacer Sizing="Fixed" />
      <Button Content="&#xE74D;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Delete" ToolTip="Delete" />
    </lg:GlassToolbar>

    <lg:GlassSurface Radius="capsule" Height="40" Padding="16,0" HorizontalAlignment="Left"
                     VerticalAlignment="Bottom" Margin="24,0,0,28">
      <TextBlock Text="Lake Bled · 14 June" FontSize="14" FontWeight="SemiBold" VerticalAlignment="Center" />
    </lg:GlassSurface>
  </lg:GlassScene>
</Window>
```

`PhotoWindow.xaml.cs`

```csharp
using System.Windows;
using System.Windows.Automation;

namespace GlassRecipes;

public partial class PhotoWindow : Window
{
    public PhotoWindow()
    {
        InitializeComponent();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Close();

    private void OnFavorite(object sender, RoutedEventArgs e)
    {
        var on = Favorite.IsChecked == true;
        Favorite.Content = on ? "\uE735" : "\uE734";   // filled star / outline star
        AutomationProperties.SetName(Favorite, on ? "Remove from favorites" : "Favorite");
    }
}
```

## 3. Media card with a slider

A compact Regular card over the album art, with everything inside reset to Clear. The artwork sits 14 units inside
a 28-unit card, so `GlassGeometry` clips it to the exact concentric continuous corner (28 − 14 / 1.528 ≈ 18.8). The scrubber is a `GlassSlider` whose
thumb lifts into glass while dragged; the one tinted control is Play.

`PlayerWindow.xaml`

```xml
<Window x:Class="GlassRecipes.PlayerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Now Playing" Width="900" Height="780" MinWidth="520" MinHeight="720"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="#101014">
  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Image Source="/Assets/album.jpg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality" />
    </lg:GlassScene.Content>

    <lg:GlassSurface Material="Regular" Radius="28" Padding="14" Width="380"
                     HorizontalAlignment="Center" VerticalAlignment="Center" AutomationProperties.Name="Now playing">
      <StackPanel lg:GlassScene.Material="Clear">
        <Image x:Name="Artwork" Source="/Assets/album.jpg" Height="352" Stretch="UniformToFill"
               RenderOptions.BitmapScalingMode="HighQuality" />
        <StackPanel Margin="10,16,10,6">
          <TextBlock x:Name="TrackTitle" FontFamily="{StaticResource DisplayFont}" FontSize="22" FontWeight="SemiBold" />
          <TextBlock Text="The Glass Lanterns" FontSize="15" Opacity="0.7" Margin="0,2,0,0" />
          <lg:GlassSlider x:Name="Scrubber" Minimum="0" Maximum="244" SmallChange="5" LargeChange="15"
                          Margin="0,16,0,0" AutomationProperties.Name="Playback position" />
          <Grid Opacity="0.65">
            <TextBlock x:Name="Elapsed" FontSize="12" />
            <TextBlock x:Name="Remaining" FontSize="12" HorizontalAlignment="Right" />
          </Grid>
          <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,10,0,4">
            <lg:GlassButton IsIconOnly="True" Size="Large" Content="&#xE892;" FontFamily="{StaticResource IconFont}"
                            FontWeight="Normal" FontSize="16" Click="OnPrevious" AutomationProperties.Name="Previous" />
            <lg:GlassButton x:Name="PlayButton" IsIconOnly="True" Size="Large" Tint="#FF375F" Margin="20,0"
                            Content="&#xE768;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="16"
                            Click="OnPlay" AutomationProperties.Name="Play" />
            <lg:GlassButton IsIconOnly="True" Size="Large" Content="&#xE893;" FontFamily="{StaticResource IconFont}"
                            FontWeight="Normal" FontSize="16" Click="OnNext" AutomationProperties.Name="Next" />
          </StackPanel>
        </StackPanel>
      </StackPanel>
    </lg:GlassSurface>
  </lg:GlassScene>
</Window>
```

`PlayerWindow.xaml.cs`

```csharp
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using LiquidGlass.Wpf;

namespace GlassRecipes;

public partial class PlayerWindow : Window
{
    private const double CardRadius = 28;
    private const double CardInset = 14;

    private static readonly (string Title, double Seconds)[] Queue =
    {
        ("Midnight Harbour", 244), ("Glass Weather", 198), ("Low Tide", 263),
    };

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _track;

    public PlayerWindow()
    {
        InitializeComponent();
        // Concentric corners: the artwork's outline runs parallel to the card's, 14 units in.
        Artwork.SizeChanged += (_, _) => Artwork.Clip = GlassGeometry.Create(
            new Rect(Artwork.RenderSize), GlassGeometry.InflateRadius(CardRadius, -CardInset));
        Scrubber.ValueChanged += (_, _) => UpdateTimes();
        _clock.Tick += (_, _) =>
        {
            if (Scrubber.Value >= Scrubber.Maximum) Load(_track + 1);
            else Scrubber.Value += 1;
        };
        Load(0);
    }

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        var playing = !_clock.IsEnabled;
        if (playing) _clock.Start(); else _clock.Stop();
        PlayButton.Content = playing ? "\uE769" : "\uE768";   // Pause / Play
        AutomationProperties.SetName(PlayButton, playing ? "Pause" : "Play");
    }

    private void OnPrevious(object sender, RoutedEventArgs e)
    {
        if (Scrubber.Value > 3) Scrubber.Value = 0;
        else Load(_track - 1);
    }

    private void OnNext(object sender, RoutedEventArgs e) => Load(_track + 1);

    private void Load(int index)
    {
        _track = (index + Queue.Length) % Queue.Length;
        TrackTitle.Text = Queue[_track].Title;
        Scrubber.Maximum = Queue[_track].Seconds;
        Scrubber.Value = 0;
        UpdateTimes();
    }

    private void UpdateTimes()
    {
        Elapsed.Text = Format(Scrubber.Value);
        Remaining.Text = "-" + Format(Scrubber.Maximum - Scrubber.Value);
    }

    private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"m\:ss");
}
```

## 4. Quick-settings card with switches

A compact quick-settings panel over a wallpaper: a Regular card of a few stretched `GlassSwitch` rows (label left,
track right) separated by hairlines in the glass foreground color. Two rows drive the scene options directly, and the
slider dims the backdrop the glass refracts. A full settings page (long forms, explanations) belongs on a solid
surface, not glass.

`SettingsWindow.xaml`

```xml
<Window x:Class="GlassRecipes.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Settings" Width="900" Height="720" MinWidth="520" MinHeight="640"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="#101014">
  <Window.Resources>
    <Style x:Key="Row" TargetType="lg:GlassSwitch">
      <Setter Property="Margin" Value="4,11" />
    </Style>
    <Style x:Key="Divider" TargetType="Border">
      <Setter Property="Height" Value="1" />
      <Setter Property="Margin" Value="4,0" />
      <Setter Property="Opacity" Value="0.12" />
      <Setter Property="Background" Value="{Binding Foreground, RelativeSource={RelativeSource AncestorType=lg:GlassSurface}}" />
    </Style>
  </Window.Resources>

  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Grid>
        <Image Source="/Assets/wallpaper.jpg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality" />
        <Rectangle x:Name="Dim" Fill="Black" Opacity="0" />
      </Grid>
    </lg:GlassScene.Content>

    <lg:GlassSurface Material="Regular" Radius="28" Width="400" Padding="20,14,20,20"
                     HorizontalAlignment="Center" VerticalAlignment="Center" AutomationProperties.Name="Quick settings">
      <StackPanel lg:GlassScene.Material="Clear">
        <TextBlock Text="Quick settings" FontFamily="{StaticResource DisplayFont}" FontSize="20" FontWeight="SemiBold"
                   Margin="4,4,0,6" />
        <lg:GlassSwitch Content="Wi-Fi" IsChecked="True" Style="{StaticResource Row}" />
        <Border Style="{StaticResource Divider}" />
        <lg:GlassSwitch Content="Bluetooth" IsChecked="True" Style="{StaticResource Row}" />
        <Border Style="{StaticResource Divider}" />
        <lg:GlassSwitch Content="Focus" Tint="#5E5CE6" Style="{StaticResource Row}" />
        <Border Style="{StaticResource Divider}" />
        <lg:GlassSwitch x:Name="DarkGlass" Content="Always dark glass" Style="{StaticResource Row}" />
        <Border Style="{StaticResource Divider}" />
        <lg:GlassSwitch x:Name="Opaque" Content="Reduce transparency" Style="{StaticResource Row}" />

        <Grid Margin="4,18,4,0">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition />
            <ColumnDefinition Width="Auto" />
          </Grid.ColumnDefinitions>
          <TextBlock Text="&#xE706;" FontFamily="{StaticResource IconFont}" FontSize="12" Opacity="0.7"
                     VerticalAlignment="Center" />
          <lg:GlassSlider x:Name="Brightness" Grid.Column="1" Margin="12,0" Minimum="0.3" Maximum="1" Value="1"
                          SmallChange="0.05" LargeChange="0.1" AutomationProperties.Name="Brightness" />
          <TextBlock Grid.Column="2" Text="&#xE706;" FontFamily="{StaticResource IconFont}" FontSize="16" Opacity="0.7"
                     VerticalAlignment="Center" />
        </Grid>
      </StackPanel>
    </lg:GlassSurface>
  </lg:GlassScene>
</Window>
```

`SettingsWindow.xaml.cs`

```csharp
using System.Windows;
using LiquidGlass.Wpf;

namespace GlassRecipes;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        // The glass options are inherited: set on the window, they reach every glass control in it.
        DarkGlass.Checked += (_, _) => GlassScene.SetAppearance(this, GlassAppearance.Dark);
        DarkGlass.Unchecked += (_, _) => ClearValue(GlassScene.AppearanceProperty);            // follow Windows again
        Opaque.Checked += (_, _) => GlassScene.SetReduceTransparency(this, true);
        Opaque.Unchecked += (_, _) => ClearValue(GlassScene.ReduceTransparencyProperty);       // null: follow Windows
        // Dimming the backdrop dims what the glass refracts; the rim light stays bright.
        Brightness.ValueChanged += (_, e) => Dim.Opacity = 1 - e.NewValue;
    }
}
```

## 5. Glass menu from a button

Two menus floating over a wallpaper: a text trigger whose checkable items behave like radio items, and an icon-only
"More" trigger with icons and shortcut text. The panels grow out of their triggers and float in the scene's top layer,
so the scene covers the whole window. A caption capsule reports what was chosen.

`MenuWindow.xaml`

```xml
<Window x:Class="GlassRecipes.MenuWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Files" Width="1000" Height="700" MinWidth="560" MinHeight="420"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="#101014">
  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Image Source="/Assets/wallpaper.jpg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality" />
    </lg:GlassScene.Content>

    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,20,24,0">
      <!-- No fixed automation name: the header, which changes with the sort order, names the menu. -->
      <lg:GlassMenu x:Name="SortMenu" Header="Sort by name">
        <lg:GlassMenuItem x:Name="SortName" Header="Name" IsCheckable="True" IsChecked="True" Click="OnSort" />
        <lg:GlassMenuItem x:Name="SortDate" Header="Date modified" IsCheckable="True" Click="OnSort" />
        <lg:GlassMenuItem x:Name="SortSize" Header="Size" IsCheckable="True" Click="OnSort" />
        <Separator />
        <lg:GlassMenuItem Header="Show hidden files" IsCheckable="True" StaysOpenOnClick="True" Click="OnAction" />
      </lg:GlassMenu>

      <!-- The icon font styles only the trigger: items draw in the glass text font. -->
      <lg:GlassMenu IsIconOnly="True" Header="&#xE712;" FontFamily="{StaticResource IconFont}"
                    Margin="10,0,0,0" AutomationProperties.Name="More">
        <lg:GlassMenuItem Header="Copy" Icon="&#xE8C8;" InputGestureText="Ctrl+C" Click="OnAction" />
        <lg:GlassMenuItem Header="Rename" Icon="&#xE8AC;" InputGestureText="F2" Click="OnAction" />
        <Separator />
        <lg:GlassMenuItem Header="Delete" Icon="&#xE74D;" InputGestureText="Del" Click="OnAction" />
      </lg:GlassMenu>
    </StackPanel>

    <lg:GlassSurface Radius="capsule" Height="40" Padding="18,0" HorizontalAlignment="Center"
                     VerticalAlignment="Bottom" Margin="0,0,0,28">
      <TextBlock x:Name="Status" Text="Open a menu" FontSize="14" FontWeight="SemiBold" VerticalAlignment="Center" />
    </lg:GlassSurface>
  </lg:GlassScene>
</Window>
```

`MenuWindow.xaml.cs`

```csharp
using System.Windows;
using LiquidGlass.Wpf;

namespace GlassRecipes;

public partial class MenuWindow : Window
{
    public MenuWindow()
    {
        InitializeComponent();
        SortMenu.Opened += (_, _) => Status.Text = "Choose how to sort";
    }

    private void OnSort(object sender, RoutedEventArgs e)
    {
        // Checkable items toggle themselves; keep exactly one sort order checked.
        var chosen = (GlassMenuItem)sender;
        foreach (var item in new[] { SortName, SortDate, SortSize }) item.IsChecked = ReferenceEquals(item, chosen);
        var order = chosen.Header?.ToString()?.ToLowerInvariant();
        SortMenu.Header = "Sort by " + order;
        Status.Text = "Sorted by " + order;
    }

    private void OnAction(object sender, RoutedEventArgs e)
    {
        var item = (GlassMenuItem)sender;
        Status.Text = item.IsCheckable ? $"{item.Header}: {(item.IsChecked ? "on" : "off")}" : $"{item.Header}";
    }
}
```

## 6. Progressive blur over a scrolling list

The `ScrollViewer` is the backdrop, so it keeps scrolling under the bars. Two progressive blurs soften the top and
bottom edges; they come before the bars among the children, so the bars refract the blurred band. The page is light,
so the scene pins light glass, and the bars are Regular because they float over text.

`FeedWindow.xaml`

```xml
<Window x:Class="GlassRecipes.FeedWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Journal" Width="900" Height="780" MinWidth="560" MinHeight="520"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="#F6F5F2">
  <lg:GlassScene lg:GlassScene.Appearance="Light">
    <lg:GlassScene.Content>
      <!-- For a heavy page, CacheMode="BitmapCache" here lets every glass copy reuse one bitmap. -->
      <ScrollViewer x:Name="Scroller" Background="#F6F5F2" VerticalScrollBarVisibility="Hidden"
                    HorizontalScrollBarVisibility="Disabled" PanningMode="VerticalOnly" AutomationProperties.Name="Journal">
        <ItemsControl x:Name="Feed" MaxWidth="680" Margin="32,96,32,112" Focusable="False">
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Grid Margin="0,0,0,28">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="Auto" />
                  <ColumnDefinition />
                </Grid.ColumnDefinitions>
                <Border Width="96" Height="96" CornerRadius="22">
                  <Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                      <GradientStop Color="{Binding From}" Offset="0" />
                      <GradientStop Color="{Binding To}" Offset="1" />
                    </LinearGradientBrush>
                  </Border.Background>
                </Border>
                <StackPanel Grid.Column="1" Margin="18,6,0,0">
                  <TextBlock Text="{Binding Title}" FontFamily="{StaticResource DisplayFont}" FontSize="20"
                             FontWeight="SemiBold" Foreground="#161619" />
                  <TextBlock Text="{Binding Summary}" FontSize="15" LineHeight="22" TextWrapping="Wrap"
                             Foreground="#5A5A62" Margin="0,4,0,0" />
                </StackPanel>
              </Grid>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </lg:GlassScene.Content>

    <lg:GlassProgressiveBlur Edge="Top" Size="120" Blur="20" />
    <lg:GlassProgressiveBlur Edge="Bottom" Size="104" Blur="20" />

    <lg:GlassToolbar Material="Regular" VerticalAlignment="Top" HorizontalAlignment="Stretch" Margin="24,20,24,0">
      <Button Content="&#xE72B;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Back" ToolTip="Back" />
      <TextBlock Text="Journal" FontFamily="{StaticResource DisplayFont}" FontSize="15" FontWeight="SemiBold"
                 VerticalAlignment="Center" Margin="4,0,12,0" />
      <lg:GlassToolbarSpacer />
      <Button Content="&#xE721;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="Search" ToolTip="Search" />
      <Button Content="&#xE712;" FontFamily="{StaticResource IconFont}" FontWeight="Normal" FontSize="15"
              AutomationProperties.Name="More" ToolTip="More" />
    </lg:GlassToolbar>

    <lg:GlassTabBar x:Name="Lists" Material="Regular" VerticalAlignment="Bottom" Margin="0,0,0,26" SelectedIndex="0"
                    AutomationProperties.Name="Lists">
      <lg:GlassTabBarItem Content="Latest" />
      <lg:GlassTabBarItem Content="Saved" />
    </lg:GlassTabBar>
  </lg:GlassScene>
</Window>
```

`FeedWindow.xaml.cs`

```csharp
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace GlassRecipes;

public partial class FeedWindow : Window
{
    private static readonly Story[] Stories =
    {
        new("Light that bends", "Content near a glass edge is drawn from deeper inside, like the rim of a thick lens.", Rgb(0xFF, 0x9F, 0x0A), Rgb(0xFF, 0x37, 0x5F)),
        new("Frost without fog", "Regular glass blurs what lies behind it and lifts it toward white, keeping color but losing detail.", Rgb(0x0A, 0x84, 0xFF), Rgb(0x5E, 0x5C, 0xE6)),
        new("A rim of light", "A specular line runs along the top and bottom of every outline; the sides carry a gentle shade.", Rgb(0x30, 0xD1, 0x58), Rgb(0x0A, 0x84, 0xFF)),
        new("Springs, not timelines", "Every motion is a damped spring, so a press can reverse mid-flight without a jump.", Rgb(0xBF, 0x5A, 0xF2), Rgb(0xFF, 0x37, 0x5F)),
        new("Edges that soften", "Six blur levels rise toward the edge, so rows slide under the bars without clashing.", Rgb(0x64, 0xD2, 0xFF), Rgb(0x30, 0xD1, 0x58)),
        new("Concentric corners", "An inset shape keeps its corner parallel to the one around it, so gaps stay even.", Rgb(0xFF, 0xD6, 0x0A), Rgb(0xFF, 0x9F, 0x0A)),
        new("Glass is a layer", "Controls float on glass; the content beneath stays content, sharp and scrollable.", Rgb(0xFF, 0x37, 0x5F), Rgb(0xBF, 0x5A, 0xF2)),
        new("One model everywhere", "The same shapes, materials and springs on every platform, checked by the same vectors.", Rgb(0x5E, 0x5C, 0xE6), Rgb(0x64, 0xD2, 0xFF)),
    };

    public FeedWindow()
    {
        InitializeComponent();
        Lists.SelectionChanged += (_, _) => ShowList();
        ShowList();
    }

    private void ShowList()
    {
        // "Saved" shows every other story; switching lists starts at the top again.
        Feed.ItemsSource = Lists.SelectedIndex == 1 ? Stories.Where((_, i) => i % 2 == 0).ToArray() : Stories;
        Scroller.ScrollToTop();
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}

public sealed record Story(string Title, string Summary, Color From, Color To);
```

## 7. Draggable glass card

A Regular card the pointer can drag across a wallpaper. `IsInteractive` gives the press, the rubber-band stretch and
the glow; moving the card is app code. The card sits in a `Canvas` without a background (so clicks beside it still
reach the backdrop), and `Canvas.Left`/`Top` change from `PreviewMouse*` handlers that leave presses on the buttons
alone. Moving by layout re-aligns the glass automatically, so nothing calls `InvalidateGlass()`. The card does not set
`IsFluid`: a fluid outline would trail behind the dragged content.

`CardWindow.xaml`

```xml
<Window x:Class="GlassRecipes.CardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf"
        Title="Card" Width="1000" Height="700" MinWidth="560" MinHeight="460"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" Background="#101014">
  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Image Source="/Assets/wallpaper.jpg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality" />
    </lg:GlassScene.Content>

    <Canvas x:Name="Stage">
      <lg:GlassSurface x:Name="Card" Material="Regular" Radius="28" Width="400" Padding="28,26,28,28"
                       IsInteractive="True" Cursor="SizeAll" AutomationProperties.Name="Welcome">
        <StackPanel lg:GlassScene.Material="Clear">
          <TextBlock Text="Liquid Glass" FontFamily="{StaticResource DisplayFont}" FontSize="30" FontWeight="SemiBold" />
          <TextBlock Text="Drag the card: the glass follows, stretches and settles." FontSize="15" Opacity="0.78"
                     TextWrapping="Wrap" Margin="0,8,0,20" />
          <StackPanel Orientation="Horizontal">
            <lg:GlassButton Content="Get started" Size="Large" Tint="#0A84FF" Click="OnStart" />
            <lg:GlassButton Content="Learn more" Size="Large" Margin="10,0,0,0" Click="OnStart" />
          </StackPanel>
        </StackPanel>
      </lg:GlassSurface>
    </Canvas>
  </lg:GlassScene>
</Window>
```

`CardWindow.xaml.cs`

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace GlassRecipes;

public partial class CardWindow : Window
{
    private Point? _grab;                    // where the pointer holds the card, in card coordinates
    private Point _anchor = new(0.5, 0.5);   // the card's center as a fraction of the stage, kept across resizes

    public CardWindow()
    {
        InitializeComponent();
        Stage.SizeChanged += (_, _) => Place();
        Card.SizeChanged += (_, _) => Place();
        Card.PreviewMouseLeftButtonDown += OnCardDown;
        Card.PreviewMouseMove += OnCardMove;
        Card.PreviewMouseLeftButtonUp += OnCardUp;
        Card.LostMouseCapture += (_, _) => _grab = null;
    }

    private void OnStart(object sender, RoutedEventArgs e) => Title = (string)((Button)sender).Content;

    private void OnCardDown(object sender, MouseButtonEventArgs e)
    {
        if (StartsOnButton(e.OriginalSource as DependencyObject)) return;   // buttons keep their clicks
        _grab = e.GetPosition(Card);
        Card.CaptureMouse();
    }

    private void OnCardMove(object sender, MouseEventArgs e)
    {
        if (_grab is not { } grab || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(Stage);
        MoveTo(p.X - grab.X, p.Y - grab.Y);
        _anchor = new Point((Canvas.GetLeft(Card) + Card.ActualWidth / 2) / Stage.ActualWidth,
                            (Canvas.GetTop(Card) + Card.ActualHeight / 2) / Stage.ActualHeight);
    }

    private void OnCardUp(object sender, MouseButtonEventArgs e)
    {
        if (_grab is null) return;
        _grab = null;
        Card.ReleaseMouseCapture();
    }

    private void Place()
    {
        if (Stage.ActualWidth <= 0 || Card.ActualWidth <= 0) return;
        MoveTo(_anchor.X * Stage.ActualWidth - Card.ActualWidth / 2, _anchor.Y * Stage.ActualHeight - Card.ActualHeight / 2);
    }

    private void MoveTo(double x, double y)
    {
        Canvas.SetLeft(Card, Clamp(x, 0, Stage.ActualWidth - Card.ActualWidth));
        Canvas.SetTop(Card, Clamp(y, 0, Stage.ActualHeight - Card.ActualHeight));
    }

    private bool StartsOnButton(DependencyObject? node)
    {
        for (; node is not null && !ReferenceEquals(node, Card);
             node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is ButtonBase) return true;
        return false;
    }

    private static double Clamp(double v, double lo, double hi) => hi < lo ? lo : Math.Min(Math.Max(v, lo), hi);
}
```
