# Changelog

All notable changes to Liquid Glass Engineering are recorded here. The project follows [Semantic Versioning](https://semver.org); the libraries, the demo apps, the website and the agent skills share one version.

## [1.0.1] - 2026-10-19

A patch release: menus stay legible over any backdrop on every platform, with menu fixes for Apple, Android and Windows, and the Gradle 8.14.6 wrapper.

### Changed

- Menus, on every platform (spec): an open menu panel mixes its glass, after the material color and tint, 60% toward a solid backing (`#F5F5F7` light, `#1C1C1E` dark; OPTICS §3, Legibility backing). The amount is 0.6 times the menu's open progress clamped to [0, 1], so the glass leaves its trigger as the trigger's own glass, the backing fades in as the panel opens, and it never passes 0.6 while the morph spring overshoots; every other surface passes 0. `spec/reference.mjs` (`MENU_LEGIBILITY`, `MENU_HIGHLIGHT`, `menuForeground`), the shader library (`lgLegibility`) and `spec/vectors.json` (the `legibility` section, and the six menu glass pixels at 0.6) carry the rule.
- Menu item labels take `menuForeground`: the appearance foreground on an untinted menu; on a tinted one, white or `#111113`, whichever has the higher WCAG 2 contrast with the tint mixed toward the backing.
- A highlighted row (hover, keyboard focus or press) mixes the label color into the glass at 8%. Android highlighted only pressed and focused rows, and Windows drew a pressed row at 14%.
- The floors: untinted menus, Regular or Clear, keep at least 5.2:1 over any backdrop in both appearances, highlighted rows included (5.24:1 at worst; in 1.0.0, white labels over white in dark appearance had 1.8:1 on plain Regular glass), and menus tinted with the platforms' system colors of either appearance keep at least 5.0:1 (5.07:1 at worst, the dark-appearance yellow `#FFD60A` on a highlighted row of dark Regular glass over white). Very light tints (pure yellow and white, 4.37:1 and 4.13:1 at worst) are not covered, and the guides advise against them.
- Destructive menu items (Apple `role: .destructive`, Android `destructive = true`) keep a floor of their own instead of system red, which fell to about 1.3:1 over the worst backdrops: they take `menuDestructive` on the side of the labels, `#560D0A` (system red darkened) where they are `#111113`, at least 5.0:1 (5.12:1 at worst), and in dark appearance, where they are white, a light pink, `#FFEEED`, so they stay readable on any backdrop, at least 4.5:1 (4.67:1 untinted, 4.52:1 on the system colors), highlighted rows included; the label wording carries the meaning. Over a white backdrop the backed dark glass is a mid gray, so no saturated red reaches 4.5:1 there. Apple adds `GlassOptics.menuDestructive(dark:)`; `spec/vectors.json` adds `legibility.destructive` and its worst cases.
- Apple: `GlassOptics` adds `menuLegibility` (0.6), `menuHighlight` (0.08), `menuForeground(tint:material:dark:legibility:)`, `legibilityBacking(dark:)`, `legibilityColor(_:dark:amount:)` and a `legibility` parameter on `glassPixel`. The Metal library's `lgCompose` and the `liquidGlass` layer effect take the amount, and under Reduce Transparency the opaque fill mixes toward the backing.
- Android: the AGSL shader takes the amount per surface, and the fallback material mixes its fill toward the backing; the tests hold both to the floors.
- Windows: the shader and the fallback material draw the backing. The fallback and the opaque material (transparency effects off, `ReduceTransparency`, a menu outside a scene) paint it over their fill at `GlassFallback.LegibilityOpacity`, the least 8-bit opacity at which labels on a highlighted row keep the shader's contrast where they contrast least, so they keep the shader's floors or more (5.11:1 over the system colors, 5.64:1 over the Windows accents, 12.6:1 untinted in the opaque material). Items take `GlassPalette.MenuForeground`; the trigger keeps its tint's own foreground and takes the menu's `Foreground` only when the application sets one. Shortcut text (`InputGestureText`) is drawn in the label's color instead of at 55% opacity, so the floors hold for it too.
- New in `LiquidGlassEngineering.Core`: `GlassOptics.MenuLegibility` and `MenuHighlight`, `GlassPalette.LegibilityBackingLight`, `LegibilityBackingDark`, `LegibilityBacking(bool)` and `MenuForeground`, `GlassMaterialOptics.LegibilityColor`, `GlassFallback.LegibilityOpacity`, `TintFill` and `TintFillOpacity`, and `GlassSurfaceOptics.Legibility` with a `legibility` constructor parameter (the 1.0.0 constructor stays for compiled callers).
- Documentation: the Apple, Android and WPF guides and the skills describe the menu rule and its floors; the SwiftUI `GlassMenuItem` documentation says a destructive item's highlight mixes the menu's label color, not red, as system menus do; the menu screenshots are retaken on every platform. Install instructions name the published release tag (Swift Package Manager `from:`, with JitPack as the secondary Android route), and shell code blocks carry a language.
- Packages: `LICENSE` and `NOTICE.md` (the Glass HQ copyright and permission notice of the ported optical model) ship inside the Compose AAR's `classes.jar` under `META-INF/liquid-glass-compose/`, so in the Maven and JitPack publications and every app built with the library, the Android Studio APK, and the macOS Studio app (`Contents/Resources`) and zip; the builds fail without them, as the Windows packaging already did.
- Liquid Glass Studio: on Apple platforms the floating tab bar is dark glass over the wallpapers, as on Android, so its labels read in light appearance (4.9:1, was 2.1:1); section headers drawn on a wallpaper sit on a soft dark haze; the Materials lab's wallpaper chips use small thumbnails on Apple and Windows instead of the full wallpaper shrunk on the fly, whose rim lines aliased into specks; the Mac window opens at 1180 × 900; the Controls page takes `-scroll end` or a fraction for screenshots. On Windows, text on glass is no longer dimmed by opacity, section headers sit on a soft haze as on Apple platforms, the tinted buttons with labels use deeper tints (`#0067C0`, `#C42B1C`) and the Materials lab's pickers are Regular glass, so every header and label in the Studio's captures keeps at least 4.5:1 in both appearances (headers 8.9:1 or more; in dark appearance they fell to 2.1:1 to 2.9:1 over the Aurora wallpaper's bright band); in dark appearance each page aligns its wallpaper's deep lower part under the glass, and the Materials lab opens on Midnight. On Android the Controls page ends 4 dp lower, so scrolled to the end the clip line under the status bar falls between rows. The Apple, Android and Windows screenshots in the guides and the README are retaken, and the README's Windows hero is the dark one, matching the iPhone and Android heroes.
- Release tooling keeps every released version in the NuGet feed and the Maven repository: `platforms/windows/scripts/package.sh` replaces only the version it builds, and `website/scripts/deploy.sh` restores earlier releases' packages from their GitHub Releases (the Maven POM and module from the live repository), verified against `SHA256SUMS` and their checksums, before building the feeds; `--strict` and the feed check fail when a released version would drop out. `scripts/set-version.mjs` sets the version and build number in every place that carries them, and CI runs its `--check`; CONTRIBUTING's release steps start with it.
- Website: the social card's domain line, kicker and subtitle read at 4.5:1 or more (a soft dark area behind the domain, halos and a faint band on the card).
- Website: wide docs tables show which side scrolls, wrap their column headings and break code in a cell only after a dot, colon, comma or slash, never inside a name; on phones the home hero fits one screen (375 × 812, 390 × 844 and 360 × 740); the Downloads page and `llms.txt` say JitPack builds the library from the release tag, its first build of a version taking a few minutes. The downloadable source zip is a `git archive` of the release tag, or of HEAD with a warning when the checkout has no such tag.

### Fixed

- Apple: `GlassMenuItem` drew its title and icon in `.foreground`, which resets to the system label color (85% black or white on macOS), so items never took a tinted menu's label color; they now keep the panel's color.
- Android: under Reduce Transparency the fallback material draws the shader's solid glass: a tinted fill was painted at 94% opacity where the shader paints 0.78 + 0.22 × 0.94.
- Windows: a menu opened a second time draws again; its panel came back with no height, so its glass and items did not show.
- Windows: the WPF Hello Glass executable carries version 1.0.1 (it built as 1.0.0, the SDK default), in step with its manifest.
- Docs and website: the Compose install snippets (README, the Android guide, the home page, /downloads and `llms.txt`) add `androidx.compose.material:material-icons-core:1.7.6`, without which the quick start's `Icons.Default.*` does not compile.
- Website: the Playground's Copy icon is no longer squeezed at phone width; install and verify snippets on /downloads, /skills and the home page scroll sideways instead of wrapping mid-word; the Appearance menu in the nav bar has an opaque background, so page text no longer shows through it; the Windows verify command on /downloads, `Get-FileHash LiquidGlassStudio-win-x64.zip`, no longer shows a stray `.\` escape.

### Security

- Gradle wrapper 8.14.6 (was 8.10.2) in `platforms/android` and the Compose Hello Glass example, with the distribution's SHA-256 checksum; the Android Gradle Plugin stays 8.7.3. 8.14.6 is the open-source 8.x release with the fixes for three high-severity Gradle advisories, each affecting `org.gradle:gradle-core` below 8.14.6 and 9.0.0 to 9.8.0 and patched in 8.14.6 and 9.8.1: [GHSA-mvvg-497x-hmj8](https://github.com/gradle/gradle/security/advisories/GHSA-mvvg-497x-hmj8) (the unauthenticated worker-to-daemon channel deserializes untrusted Java objects), [GHSA-xwqc-3h47-hg64](https://github.com/gradle/gradle/security/advisories/GHSA-xwqc-3h47-hg64) (the daemon deserializes a client's message before it verifies the token) and [GHSA-j5m7-59rp-24f5](https://github.com/gradle/gradle/security/advisories/GHSA-j5m7-59rp-24f5) (an SSL connection error did not disable a repository, so dependency resolution could move on to the next one). The first two need a local attacker on the build machine and are not exploitable from the network; the third needs an attacker who controls a repository declared after the disrupted one. They concern building this repository, not the published library.
- Private vulnerability reporting is on: the security policy, the issue chooser and the private contact form send reports to a private advisory first.

### Dependencies and CI

- GitHub Actions: `actions/checkout` v7, `actions/setup-java` v6, `actions/upload-artifact` v7, `actions/download-artifact` v8, `actions/setup-node` v7, `actions/setup-dotnet` v6 and `gradle/actions/setup-gradle` v6.4.0 (pinned to its commit); `setup-gradle` uses the MIT-licensed basic cache provider, and `download-artifact` fails on a digest mismatch.
- Website: TypeScript 7.0.2, the native compiler; react and react-dom 19.3.1, @types/node 22.20.5, @vitejs/plugin-react 6.1.2, marked 18.1.1, oxlint 1.88.0, shiki 4.5.0 and vite 8.3.4.
- Windows tests: xunit.runner.visualstudio 4.0.1 (was 3.1.5) and, for the .NET Framework 4.8 build of the WPF tests, System.Text.Json 10.0.13 (was 8.0.6).
- Dependabot runs monthly. GitHub Actions updates, majors included, arrive as one grouped pull request; website and Windows minor and patch updates arrive as one grouped pull request each, toolchain majors not at all; Android, the library and the Hello Glass example together, takes patch updates only on the 1.0.x line.
- CI: the Windows summary reads test results and hashes release files by literal path; the WPF animation tests advance frames by a fixed 1/60 s step instead of depending on the runner's frame timing, and the three WPF test hosts run one after another.

## [1.0.0] - 2026-10-18

The first release: native Liquid Glass for iOS, macOS, Android and Windows from one optical model.

### Optical model

- `spec/OPTICS.md`: continuous-corner shapes, bevel refraction, the Clear and Regular materials, tint, rim light, the composite, interaction springs, progressive blur and accessibility, ported from the MIT-licensed Glass HQ liquid-glass library.
- `spec/COMPONENTS.md`: the component contract every port implements, with shared options, sizes per platform and the Liquid Glass Studio demo.
- `spec/reference.mjs` (executable reference), `spec/shaders/liquid-glass.glsl` (canonical shader library) and `spec/vectors.json` (golden test vectors every port reproduces).
- Refraction left unset follows the surface's resting size, `min(60, 0.75 × shorter side)`, so small controls never pull backdrop edges in from outside their outline. Labels on tinted glass take white or `#111113`, whichever has the higher WCAG contrast.

### Apple: SwiftUI and Metal

- Swift package `LiquidGlass` for iOS 17+ and macOS 14+: `GlassScene`, `GlassContent`, `GlassSurface` and `.liquidGlass()`, `GlassButton`, `GlassToolbar`, `GlassTabBar`, `GlassSlider`, `GlassSwitch` and `GlassSwitchStyle`, `GlassMenu`, `GlassProgressiveBlur`, `GlassShape`, `GlassScrollView`, `GlassOptics` and `GlassSpring`.
- The Metal shader is checked against the golden vectors on the GPU, and render tests compare a real scene with the model.
- Liquid Glass Studio for iPhone, iPad and Mac, with UI tests.

### Android: Jetpack Compose and AGSL

- `io.github.rookasrudzianskas:liquid-glass-compose` (minSdk 24): GPU glass on Android 13 and later, a fallback material on Android 7 to 12. Served from the project's Maven repository, through JitPack and as an AAR.
- One AGSL pass draws up to 16 surfaces per scene; instrumented tests compare the GPU output with the reference.
- Liquid Glass Studio for Android.

### Windows: WPF and HLSL

- `LiquidGlassEngineering.Wpf` and `LiquidGlassEngineering.Core` for .NET Framework 4.8, .NET 8 and .NET 10 on Windows 10 1809 and later, on the project's NuGet feed.
- A pixel shader 3.0 glass shader, precompiled with a run-time compile path, UI Automation peers, keyboard focus, contrast themes and a fallback material.
- Liquid Glass Studio for Windows on x64 and ARM64.

### Examples

- Hello Glass, the smallest complete app, for SwiftUI (iOS and macOS), Jetpack Compose and WPF: the same screen on every platform, built in CI.

### Website

- Developer guides, an interactive WebGL2 playground, downloads with SHA-256 checksums, the Maven repository and NuGet feed, and `llms.txt`.

### Agent skills

- A plugin marketplace with the `liquid-glass` plugin and its skills: `liquid-glass` (design), `liquid-glass-swiftui`, `liquid-glass-compose`, `liquid-glass-wpf` and `liquid-glass-port`.

[1.0.1]: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/releases/tag/1.0.1
[1.0.0]: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/releases/tag/1.0.0
