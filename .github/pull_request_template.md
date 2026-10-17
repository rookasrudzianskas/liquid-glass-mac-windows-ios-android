## What changed

<!-- What this pull request changes and why. Link the issue it fixes, if any. -->

## How it was verified

<!-- Tests run, devices or emulators used, and before/after screenshots for anything visible. -->

## Checklist

- [ ] Tests pass for every platform this touches (see [CONTRIBUTING.md](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/CONTRIBUTING.md#tests-per-platform)).
- [ ] A change to the optical model starts in `spec/`, and `spec/vectors.json` is regenerated with `node spec/generate-vectors.mjs`.
- [ ] `node scripts/sync-skill-references.mjs --check` and `node scripts/check-plugin.mjs` pass.
- [ ] Public API changes update the platform guide in `docs/`, its code samples, the matching skill and `CHANGELOG.md`.
