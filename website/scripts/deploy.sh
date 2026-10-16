#!/usr/bin/env bash
# Build the downloads and deploy the website to production on Vercel.
#
#   website/scripts/deploy.sh            # tolerant: missing artifacts show as unavailable
#   website/scripts/deploy.sh --strict   # release: fail if any required artifact is missing
#
# Needs Node 22 on PATH and an authenticated Vercel CLI. Always deploys to the
# boringai team (project liquid-glass-engineering).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
vercel=(npx --yes vercel@latest)

node "$repo/scripts/build-downloads.mjs" "$@"

cd "$repo/website"
"${vercel[@]}" pull --yes --environment=production --scope boringai
"${vercel[@]}" build --prod --scope boringai
"${vercel[@]}" deploy --prebuilt --prod --scope boringai
