#!/usr/bin/env bash
# CI only: selects the newest released (non-beta) Xcode installed on the runner.
set -euo pipefail
newest="$(ls -d /Applications/Xcode_*.app 2>/dev/null | grep -vi -e beta -e rc | sort -V | tail -1 || true)"
if [ -n "$newest" ]; then sudo xcode-select -s "$newest/Contents/Developer"; fi
xcodebuild -version
