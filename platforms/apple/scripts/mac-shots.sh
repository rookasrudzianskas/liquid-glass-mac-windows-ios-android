#!/usr/bin/env bash
# Launches the macOS Studio build on each page and captures its window.
# Usage: platforms/apple/scripts/mac-shots.sh <out-dir> [appearance] [configuration]
set -euo pipefail
out="$1"; appearance="${2:-automatic}"; configuration="${3:-Debug}"
root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/DerivedData/Build/Products/$configuration/Liquid Glass Studio.app"
mkdir -p "$out"
here="$(cd "$(dirname "$0")" && pwd)"
for page in ${PAGES:-hero controls lab blur}; do
  "$app/Contents/MacOS/Liquid Glass Studio" -page "$page" -appearance "$appearance" &
  pid=$!
  sleep "${WAIT:-4}"
  window=$(swift "$here/window-id.swift" "$pid")
  screencapture -x -o -l "$window" "$out/mac-$page-$appearance.png"
  kill "$pid"; wait "$pid" 2>/dev/null || true
  echo "$out/mac-$page-$appearance.png"
done
