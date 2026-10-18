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
  # Launched through Launch Services, the app comes to the front and its window is key, so the shot shows the
  # active window (colored traffic lights) rather than an inactive one.
  open -n -F "$app" --args -page "$page" -appearance "$appearance"
  sleep "${WAIT:-4}"
  pid=$(pgrep -n -f "$app/Contents/MacOS/")
  window=$(swift "$here/window-id.swift" "$pid")
  screencapture -x -o -l "$window" "$out/mac-$page-$appearance.png"
  kill "$pid"; while kill -0 "$pid" 2>/dev/null; do sleep 0.2; done
  echo "$out/mac-$page-$appearance.png"
done
