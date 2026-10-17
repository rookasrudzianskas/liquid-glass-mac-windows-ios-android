# Working in this repository

A short map for anyone changing code here. [CONTRIBUTING.md](CONTRIBUTING.md) has the full workflow and [README.md](README.md) the overview.

## Ground truth

- [`spec/OPTICS.md`](spec/OPTICS.md) defines how glass renders and moves, with every constant. [`spec/reference.mjs`](spec/reference.mjs) is its executable form and [`spec/shaders/liquid-glass.glsl`](spec/shaders/liquid-glass.glsl) its shader form.
- [`spec/vectors.json`](spec/vectors.json) is generated from the reference by `node spec/generate-vectors.mjs`; never edit it by hand. Every port's tests reproduce it (1e-4 on the CPU, 2e-3 on the GPU).
- [`spec/COMPONENTS.md`](spec/COMPONENTS.md) fixes the component names, shared options, sizes and behaviors of every port.

When the spec and a port disagree, the spec wins: change the spec first, regenerate the vectors, then update every port.

## Layout

| Path | What | Guide |
| --- | --- | --- |
| `Package.swift`, `Sources/LiquidGlass/`, `Tests/` | SwiftUI and Metal library | [docs/apple.md](docs/apple.md) |
| `platforms/apple/` | Liquid Glass Studio for iOS and macOS, CI scripts | [docs/apple.md](docs/apple.md) |
| `platforms/android/` | Compose and AGSL library (`liquidglass`), Studio (`studio`) | [docs/android.md](docs/android.md) |
| `platforms/windows/` | WPF and HLSL library (`LiquidGlass.Core`, `LiquidGlass.Wpf`), Studio, tests, CI tools | [docs/windows.md](docs/windows.md) |
| `plugins/liquid-glass/` | The plugin and its skills; `skills/*/references/`, `LICENSE` and `NOTICE.md` there are generated copies | [CONTRIBUTING.md](CONTRIBUTING.md#agent-skills) |
| `website/` | The website (Vite and React); it publishes `docs/` and `spec/` | |
| `scripts/` | Release downloads, skill reference sync, plugin checks | |

## Checks

Run the checks for every part a change touches before committing:

```sh
node spec/generate-vectors.mjs && git diff --exit-code spec/vectors.json
node scripts/sync-skill-references.mjs --check
node scripts/check-plugin.mjs
```

Platform tests are listed in [CONTRIBUTING.md](CONTRIBUTING.md#tests-per-platform). The Apple package needs `xcodebuild` (the bare `swift build` does not compile Metal); the WPF tests need Windows, where CI runs them.

## Conventions

- Keep the public API stable. If it changes, update the guide in `docs/`, its code samples, the matching skill and `CHANGELOG.md` in the same change.
- Code samples in `docs/` and in the skills must compile against the current library.
- Commit summaries start with the area: `Apple:`, `Android:`, `Windows:`, `Website:`, `Spec:`, `Skills:`, `CI:` or `Docs:`.
- Build outputs (`artifacts/`, the website's `public/downloads`, `public/maven`, `public/nuget`) are never committed.
