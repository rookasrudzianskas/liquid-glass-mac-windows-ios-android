#!/usr/bin/env bash
# Installs the Studio build on a booted simulator and screenshots each page.
# Usage: platforms/apple/scripts/sim-shots.sh <out-dir> [appearance] [udid]
# ARGS adds launch arguments, for example ARGS="-scroll end" PAGES=controls.
set -euo pipefail
export DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
out="$1"; appearance="${2:-automatic}"; udid="${3:-booted}"
root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/DerivedData/Build/Products/Debug-iphonesimulator/Liquid Glass Studio.app"
bundle=com.rookasrudzianskas.liquidglass.studio
mkdir -p "$out"
xcrun simctl install "$udid" "$app"
for page in ${PAGES:-hero controls lab blur}; do
  xcrun simctl terminate "$udid" "$bundle" >/dev/null 2>&1 || true
  # shellcheck disable=SC2086
  xcrun simctl launch "$udid" "$bundle" -page "$page" -appearance "$appearance" ${ARGS:-} >/dev/null
  sleep "${WAIT:-4}"
  xcrun simctl io "$udid" screenshot "$out/ios-$page-$appearance.png" >/dev/null 2>&1
  echo "$out/ios-$page-$appearance.png"
done
