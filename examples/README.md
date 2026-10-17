# Examples

Hello Glass is the smallest complete Liquid Glass app, written once per platform. Every version builds the same screen, so the screenshots line up side by side. The screen has:

- a Regular glass card over the aurora wallpaper, with a title, a one-line subtitle, a tinted **Get started** button and a glass **Learn more** button;
- a **Frosted** switch that turns the card from Regular into Clear glass and back;
- a glass toolbar with two icon buttons;
- a floating tab bar with Home, Explore and Settings.

<p align="center">
  <img src="swiftui/HelloGlass/screenshot.png" height="420" alt="Hello Glass in SwiftUI on iPhone: a frosted glass card titled Hello, Glass over the aurora wallpaper, with a glass toolbar and tab bar">
  <img src="compose/HelloGlass/screenshot.png" height="420" alt="Hello Glass in Jetpack Compose on Android: the same card, toolbar and tab bar">
</p>
<p align="center">
  <img src="wpf/HelloGlass/screenshot.png" width="640" alt="Hello Glass in WPF on Windows: the same card, toolbar and tab bar in a window">
</p>

| Example | Platform | Build |
| --- | --- | --- |
| [`swiftui/HelloGlass`](swiftui/HelloGlass) | iOS 17+ and macOS 14+, Xcode 16+ | Open `HelloGlass.xcodeproj` and run, or build with `xcodebuild` (see its README). |
| [`compose/HelloGlass`](compose/HelloGlass) | Android 7+ (glass on Android 13+), JDK 17 | `./gradlew :app:assembleDebug` from the example folder (`:app:installDebug` installs it on a connected device). |
| [`wpf/HelloGlass`](wpf/HelloGlass) | Windows 10 1809+; the .NET 10 SDK for the in-repo build (.NET 8 with the published package) | `dotnet run --project examples/wpf/HelloGlass` from the repository root. |

Each example builds against the library source in this repository, so it always matches the code next to it. Its README shows the one change that makes it use the published package instead: the Swift package URL, the Maven coordinates or the NuGet package. CI builds all three on every change.

For a bigger tour of every component, see Liquid Glass Studio in [`platforms/apple/Studio`](../platforms/apple/Studio), [`platforms/android/studio`](../platforms/android/studio) and [`platforms/windows/src/LiquidGlass.Studio`](../platforms/windows/src/LiquidGlass.Studio).
