#!/usr/bin/env bash
# Prints an xcodebuild destination: "macOS", or the newest available iPhone
# simulator for "iOS Simulator".
set -euo pipefail
case "$1" in
  macOS) echo "platform=macOS" ;;
  "iOS Simulator")
    udid="$(xcrun simctl list devices available --json | python3 -c '
import json, sys
runtimes = json.load(sys.stdin)["devices"]
ios = sorted((r for r in runtimes if "iOS" in r), key=lambda r: [int(p) for p in r.rsplit("iOS-", 1)[-1].split("-") if p.isdigit()])
for runtime in reversed(ios):
    phones = [d for d in runtimes[runtime] if d["name"].startswith("iPhone")]
    if phones:
        print(phones[0]["udid"]); break
')"
    [ -n "$udid" ] || { echo "no iPhone simulator available" >&2; exit 1; }
    echo "platform=iOS Simulator,id=$udid" ;;
  *) echo "unknown platform: $1" >&2; exit 2 ;;
esac
