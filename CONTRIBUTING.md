# Contributing

Thanks for helping. Liquid Glass Engineering is one optical model in four places: the specification in `spec/` and the SwiftUI, Jetpack Compose and WPF ports that reproduce it. A change is done when every port that it touches still matches the specification, its tests pass and the docs say what the code does.

- [Toolchains](#toolchains)
- [How a change flows](#how-a-change-flows)
- [Tests per platform](#tests-per-platform)
- [Agent skills](#agent-skills)
- [Docs and screenshots](#docs-and-screenshots)
- [Commits and pull requests](#commits-and-pull-requests)
- [Releasing](#releasing)

## Toolchains

| Part | Toolchain |
| --- | --- |
| Apple (`Package.swift`, `Sources/`, `Tests/`, `platforms/apple/`) | Xcode 16 or later (iOS 18 / macOS 15 SDK; the deployment targets stay iOS 17 and macOS 14). When Xcode is not the selected developer directory, prefix commands with `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`. `swift build` alone does not compile the Metal shaders; use `xcodebuild`. |
| Android (`platforms/android/`) | JDK 17 and the Android SDK (platform 35); set `ANDROID_HOME`, or `sdk.dir` in `platforms/android/local.properties`. The Gradle wrapper is checked in. |
| Windows (`platforms/windows/`) | .NET 10 SDK (`global.json`). `scripts/dotnet.sh` builds on macOS and Linux too; the WPF tests and the Studio need Windows. |
| Website and scripts (`website/`, `scripts/`, `spec/`) | Node 22 (20.19 or later). The scripts use only Node built-ins. |

## How a change flows

1. **Specification first.** Anything that changes how glass looks or moves starts in [`spec/OPTICS.md`](spec/OPTICS.md) and [`spec/reference.mjs`](spec/reference.mjs) (and [`spec/shaders/liquid-glass.glsl`](spec/shaders/liquid-glass.glsl) for shader code). Component names, options and sizes live in [`spec/COMPONENTS.md`](spec/COMPONENTS.md).
2. **Regenerate the golden vectors** and commit them with the change: `node spec/generate-vectors.mjs`. Never edit `spec/vectors.json` by hand.
3. **Update every port** until its tests reproduce the new vectors (1e-4 on the CPU, 2e-3 on the GPU).
4. **Refresh the skills' copies** of the spec, `LICENSE` and `NOTICE.md`: `node scripts/sync-skill-references.mjs`. The copies are `skills/liquid-glass/references/optics.md`, everything in `skills/liquid-glass-port/references/`, and the `LICENSE` and `NOTICE.md` in the plugin folder and in every skill folder (all under `plugins/liquid-glass/`); never edit them directly. The other reference files (each platform skill's `api.md` and `recipes.md`, and the `liquid-glass` skill's `design.md`) are written by hand and change with the API or guidance they describe.
5. **Update the guides** in `docs/` and the code samples in them.

CI checks steps 2 and 4 on every pull request:

```sh
node spec/generate-vectors.mjs && git diff --exit-code spec/vectors.json
node scripts/sync-skill-references.mjs --check
node scripts/check-plugin.mjs
```

## Tests per platform

**Apple**, from the repository root:

```sh
xcodebuild test -scheme LiquidGlass -destination 'platform=macOS'
xcodebuild test -scheme LiquidGlass -destination 'platform=iOS Simulator,name=iPhone 16 Pro'
xcodebuild test -project platforms/apple/Studio/LiquidGlassStudio.xcodeproj -scheme LiquidGlassStudio \
  -destination 'platform=iOS Simulator,name=iPhone 16 Pro'
```

The Studio's Xcode project is generated from `project.yml`; after adding files run `platforms/apple/scripts/generate-project.sh`.

**Android**, in `platforms/android`:

```sh
./gradlew :liquidglass:testDebugUnitTest          # Kotlin optics vs spec/vectors.json
./gradlew :liquidglass:connectedDebugAndroidTest  # GPU output vs the reference (device or emulator, Android 13+)
./gradlew lint
./gradlew :studio:assembleRelease
```

**Windows**, in `platforms/windows`:

```sh
scripts/dotnet.sh build LiquidGlass.sln -c Release
scripts/dotnet.sh test tests/LiquidGlass.Core.Tests      # anywhere
scripts/dotnet.sh test tests/LiquidGlass.Wpf.Tests       # Windows only (WPF, Direct3D 11)
```

On macOS and Linux the [Windows workflow](.github/workflows/windows.yml) is the place to run the WPF tests: it builds, tests, compiles the shader with fxc and captures the Studio on a Windows runner. After editing `Shaders/*.fx` or `LiquidGlass.hlsli`, refresh `Shaders/Compiled/GlassSurface.ps` and its `.sha256` from that workflow's fxc output.

**Website**, in `website`:

```sh
npm ci
npm run dev        # local server
npm run build      # collects the downloads if missing, then typecheck, build, prerender and link check
npm run lint
```

The full commands for every platform, including packaging, are in the guides: [Apple](docs/apple.md#building-and-testing), [Android](docs/android.md#building-and-testing) and [Windows](docs/windows.md#building-and-testing).

## Agent skills

The skills live in `plugins/liquid-glass/skills/<name>/SKILL.md`. The frontmatter `name` must equal the folder name (lowercase letters, digits and hyphens) and the `description` must stay within 1024 characters. Code in a skill must compile against the current library: when an API changes, update the skill that documents it in the same pull request. `node scripts/check-plugin.mjs` validates the marketplace, the plugin manifest, every skill's frontmatter, the skills each one names and every relative link.

## Docs and screenshots

The guides in `docs/` are published on the website as they are, so relative links must resolve in the repository. Screenshots go in `docs/images/<platform>/` and come from Liquid Glass Studio, opened on a given page and appearance with its launch options (see each guide), so they line up across platforms.

## Commits and pull requests

- One logical change per commit. Start the summary with the area it touches: `Apple:`, `Android:`, `Windows:`, `Website:`, `Spec:`, `Skills:`, `CI:` or `Docs:`.
- Pull requests describe what changed and how it was verified, with before and after screenshots for anything visible. The [template](.github/pull_request_template.md) lists the checks.
- Keep the public API stable. A change to it updates the platform guide, its samples, the skill and `CHANGELOG.md`.
- Don't commit build outputs: `artifacts/` and the website's `public/downloads`, `public/maven` and `public/nuget` are generated.
- Report bugs with the [issue templates](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/issues/new/choose), and security problems as described in [SECURITY.md](SECURITY.md).

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).

## Releasing

For maintainers. A release is a semver tag without a `v` prefix (`1.0.0`): Swift Package Manager resolves versions from tags, and JitPack serves the tag name as the version. The release checks of `website/scripts/deploy.sh --strict` look for that tag on GitHub, so the tag is pushed before the website is deployed:

1. `CHANGELOG.md` has the release date (`## [1.0.0] - YYYY-MM-DD`), committed and pushed to `main`, and the latest run of every workflow on `main` is green.
2. Tag that commit and push the tag: `git tag -a 1.0.0 -m "Liquid Glass Engineering 1.0.0"` and `git push origin 1.0.0`.
3. With the tagged commit checked out and a clean working tree, build the release artifacts into `artifacts/` with each platform's packaging commands ([Apple](docs/apple.md#building-and-testing), [Android](docs/android.md#building-and-testing), [Windows](docs/windows.md#building-and-testing)) and deploy the website with `website/scripts/deploy.sh --strict`. It restores the earlier releases' packages first (see below), so the checkout needs network access. Check that `https://liquid-glass-engineering.vercel.app/downloads/manifest.json` reports the tagged `commit` with `modified: false`, that every required download is available, and that `platforms/windows/scripts/check-feed.sh` passes (the feed lists this release's packages and still serves every earlier release).
4. Publish a GitHub release from the tag with the `CHANGELOG.md` notes and the files of `website/public/downloads/` (the Studio apps, the AAR, the NuGet packages, the source and skill zips, and `SHA256SUMS`). Later deploys restore this release's packages from these assets and verify them with its `SHA256SUMS`, so the release must keep the `.nupkg` files, `liquid-glass-compose-<version>.aar` and `SHA256SUMS` under their original names.
5. Request `https://jitpack.io/com/github/rookasrudzianskas/liquid-glass-mac-windows-ios-android/1.0.0/` and wait until the build log shows the library published. Then check that a scratch Swift package with `from: "1.0.0"` resolves.

Never move a pushed tag: Swift Package Manager and JitPack keep what it first pointed to. A code fix after step 2 ships as the next patch version; artifacts and the website can be rebuilt and redeployed from the same tag.

**Earlier versions stay in the feeds.** The NuGet feed (`/nuget`) and the Maven repository (`/maven`) are built from `artifacts/windows/nuget` and `artifacts/android/maven`, and list every version found there: `index.json`, the registrations and `maven-metadata.xml` name them all, oldest first, with the newest as the latest. Projects pinned to an earlier version resolve it from these feeds, so every deploy must keep every released version. The scripts keep them:

- `platforms/windows/scripts/package.sh` replaces only the packages of the version it builds (`Directory.Build.props`), and the Gradle publication adds the new version next to the ones already in `artifacts/android/maven`; `scripts/build-downloads.mjs` writes `maven-metadata.xml` from the versions in the tree.
- `artifacts/` is gitignored, so a fresh clone or worktree starts without the earlier versions. `website/scripts/deploy.sh` passes `--restore-released` to `scripts/build-downloads.mjs`, which first adds what `artifacts/` lacks for every published GitHub release other than this version: the release's `.nupkg` assets, checked against its `SHA256SUMS` and GitHub's digest, and the Compose library's Maven files from the live site's `/maven`, checked against their `.sha256` and `.sha512` files and the Gradle module's hashes, with the AAR checked against the release's `SHA256SUMS`. It never overwrites a file in `artifacts/`, and stops when a release cannot be fetched or verified, or when a package already there is not the released file.
- With `--strict`, the deploy also fails when a published release would be missing from either feed, or when `artifacts/` holds a version that is neither this release nor a published one (an old local build: delete it).
- The website's downloads carry only the newest version of each package; the earlier ones stay in the feeds and on their GitHub releases.
