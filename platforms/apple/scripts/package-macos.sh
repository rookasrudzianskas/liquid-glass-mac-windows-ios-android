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
# LICENSE and NOTICE.md (the Glass HQ copyright and permission notice of the
# ported optical model, which its MIT License asks for in all copies) go in
# the app's Resources, before signing, and at the top of the zip.
for file in LICENSE NOTICE.md; do
  cp "$root/$file" "$app/Contents/Resources/$file"
done
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
stage="$(mktemp -d -t liquid-glass-macos.XXXXXX)"
trap 'rm -rf "$stage"' EXIT
ditto --norsrc --noextattr --noqtn "$app" "$stage/Liquid Glass Studio.app"
cp "$root/LICENSE" "$root/NOTICE.md" "$stage/"
rm -f "$out/LiquidGlassStudio-macOS.zip"
# No resource forks, extended attributes or quarantine: the zip holds no
# AppleDouble (._*) or __MACOSX entries.
ditto -c -k --norsrc --noextattr --noqtn "$stage" "$out/LiquidGlassStudio-macOS.zip"
listing="$(unzip -Z1 "$out/LiquidGlassStudio-macOS.zip")"
for entry in LICENSE NOTICE.md "Liquid Glass Studio.app/Contents/Resources/LICENSE" "Liquid Glass Studio.app/Contents/Resources/NOTICE.md"; do
  grep -qxF "$entry" <<<"$listing" || { echo "LiquidGlassStudio-macOS.zip: $entry is missing" >&2; exit 1; }
done
version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app/Contents/Info.plist")"
cat > "$out/README.txt" <<TEXT
Liquid Glass Studio $version for macOS 14 or later (Apple silicon and Intel)

The demo of the LiquidGlass Swift package: native Liquid Glass in SwiftUI
and Metal. Source: platforms/apple/Studio in
https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android

First move "Liquid Glass Studio" to your Applications folder.

The app is ad-hoc signed, not notarized, so macOS blocks its first launch.
Open it once, then open System Settings > Privacy & Security and click
Open Anyway next to "Liquid Glass Studio" (on macOS 14, Control-click the
app, choose Open, then confirm also works). Or, with the app in
Applications, run:

  xattr -dr com.apple.quarantine "/Applications/Liquid Glass Studio.app"

MIT License: see LICENSE and NOTICE.md in the zip (also in the app's
Contents/Resources), which carry the notice of the Glass HQ library the
optical model is ported from.
TEXT
ls -l "$out"
