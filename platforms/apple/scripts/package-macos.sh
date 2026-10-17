#!/usr/bin/env bash
# Ad-hoc signs a Release build of Liquid Glass Studio for macOS and zips it
# for the website's downloads.
# Usage: package-macos.sh <Release products directory>
set -euo pipefail
products="$1"
root="$(git rev-parse --show-toplevel)"
out="$root/artifacts/apple"
app="$products/Liquid Glass Studio.app"
[ -d "$app" ] || { echo "missing $app" >&2; exit 1; }
mkdir -p "$out"
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
rm -f "$out/LiquidGlassStudio-macOS.zip"
ditto -c -k --keepParent "$app" "$out/LiquidGlassStudio-macOS.zip"
version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app/Contents/Info.plist")"
cat > "$out/README.txt" <<TEXT
Liquid Glass Studio $version for macOS 14 or later (Apple silicon and Intel)

The demo of the LiquidGlass Swift package: native Liquid Glass in SwiftUI
and Metal. Source: platforms/apple/Studio in
https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android

The app is ad-hoc signed, not notarized, so macOS blocks its first launch.
On macOS 15 or later: open it once, then open System Settings > Privacy &
Security and click Open Anyway next to "Liquid Glass Studio". On macOS 14:
Control-click (or right-click) the app, choose Open, then confirm. Or, on
any version, run:

  xattr -dr com.apple.quarantine "/Applications/Liquid Glass Studio.app"

MIT License.
TEXT
ls -l "$out"
