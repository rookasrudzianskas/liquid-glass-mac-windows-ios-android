#!/usr/bin/env bash
# Regenerates platforms/apple/Studio/LiquidGlassStudio.xcodeproj from project.yml.
# Uses `xcodegen` from PATH, or $XCODEGEN. Install it with `brew install xcodegen`
# or build it from source (https://github.com/yonaskolb/XcodeGen).
set -euo pipefail
cd "$(dirname "$0")/../Studio"
"${XCODEGEN:-xcodegen}" generate --spec project.yml
