# Security policy

## Supported versions

| Version | Supported |
| --- | --- |
| 1.0.x | Yes |
| Earlier builds | No |

Fixes are released as a new patch version of the libraries (Swift package, `liquid-glass-compose`, `LiquidGlassEngineering.Wpf` and `LiquidGlassEngineering.Core`), the Liquid Glass Studio apps and the website downloads.

## Reporting a vulnerability

Please report vulnerabilities privately, not in a public issue: use **[Report a vulnerability](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/security/advisories/new)** on the repository's **Security** tab, which sends a private advisory to the maintainer. If you cannot use GitHub's advisory form, ask for a private contact with the [private contact request](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/issues/new?template=private_contact.yml) issue form, without any details of the problem, and the maintainer replies with a private way to send the report.

Include what is affected (library, platform and version, Studio app, website or a download), how to reproduce it and the impact you see. The maintainer aims to acknowledge a report within a week. Once a fix is released, the advisory is published with credit to the reporter, unless you prefer otherwise.

## How the repository is checked

- **Code scanning:** CodeQL analyses the Swift, Kotlin, C#, TypeScript and JavaScript code and the GitHub Actions workflows on every push to `main`, on pull requests and weekly ([`.github/workflows/codeql.yml`](.github/workflows/codeql.yml)).
- **Dependencies:** Dependabot alerts and security updates watch the website's npm packages, the .NET projects' NuGet packages, the GitHub Actions and the Android libraries the release builds ship with, which the Android workflow submits to GitHub's dependency graph. Version updates arrive monthly ([`.github/dependabot.yml`](.github/dependabot.yml)).
- **Secrets:** secret scanning with push protection keeps credentials out of the repository.

## Scope

In scope: the libraries in this repository, the Liquid Glass Studio apps, the scripts that build the release artifacts, and the files served from `liquid-glass-engineering.vercel.app` (downloads, the Maven repository and the NuGet feed), including their integrity: every download is listed with its SHA-256 checksum in `downloads/SHA256SUMS`.

Rendering glitches, crashes from invalid input to a public API and performance problems are bugs rather than vulnerabilities; please report them with the [bug report template](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/issues/new/choose).
