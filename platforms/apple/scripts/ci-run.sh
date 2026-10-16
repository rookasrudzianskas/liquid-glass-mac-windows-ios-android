#!/usr/bin/env bash
# Runs a build command, keeping its log; on failure, writes the errors and
# failing tests to the GitHub step summary so they're readable without logs.
# Usage: ci-run.sh "<title>" <command...>
set -uo pipefail
title="$1"; shift
log="$(mktemp)"
"$@" 2>&1 | tee "$log"
status=${PIPESTATUS[0]}
if [ "$status" -ne 0 ] && [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### $title failed (exit $status)"
    echo '```'
    grep -E "error:|failed|FAILED|Fatal|XCTAssert" "$log" | head -60
    echo '```'
  } >> "$GITHUB_STEP_SUMMARY"
fi
exit "$status"
