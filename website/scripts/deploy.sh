#!/usr/bin/env bash
# Build the downloads and deploy the website to production on Vercel.
#
#   website/scripts/deploy.sh            # tolerant: missing artifacts show as unavailable
#   website/scripts/deploy.sh --strict   # release: fail if a required artifact is missing or a
#                                        # release check fails (committed and pushed tree, release
#                                        # tag on origin, committed skills, NuGet frameworks, feeds
#                                        # that serve every published release)
#
# Both deploy to production, so both pass --restore-released: the NuGet feed and the Maven
# repository must keep every released version, and artifacts/ only holds what this checkout
# built. build-downloads.mjs adds the packages of every other published GitHub release that
# artifacts/ lacks (verified against the release's SHA256SUMS) and fails when it cannot.
#
# Needs Node 22 on PATH, network access and an authenticated Vercel CLI. Always deploys to the
# boringai team (project liquid-glass-engineering).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
vercel=(npx --yes vercel@latest)

node "$repo/scripts/build-downloads.mjs" --restore-released "$@"

cd "$repo/website"
"${vercel[@]}" pull --yes --environment=production --scope boringai
"${vercel[@]}" build --prod --scope boringai
"${vercel[@]}" deploy --prebuilt --prod --scope boringai

# The build output is a copy of public/ (downloads included): free the disk.
rm -rf "$repo/website/dist" "$repo/website/.vercel/output"
