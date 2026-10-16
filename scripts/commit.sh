#!/usr/bin/env bash
# Commit (and push) a set of paths as the repository author.
# Usage: scripts/commit.sh "message" <path> [path...]
# Serialized with a lock so several agents can commit their own folders safely.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
message="$1"; shift
[ "$#" -gt 0 ] || { echo "usage: scripts/commit.sh \"message\" <path>..." >&2; exit 2; }
lock=".git/lg-commit.lock"
for _ in $(seq 1 300); do mkdir "$lock" 2>/dev/null && break; sleep 1; done
[ -d "$lock" ] || { echo "could not take $lock" >&2; exit 1; }
trap 'rmdir "$lock" 2>/dev/null || true' EXIT
# Never credit tools or assistants as co-authors.
message="$(printf '%s\n' "$message" | grep -viE '^(co-authored-by|generated with|🤖)' || true)"
git add -A -- "$@"
if git diff --cached --quiet; then echo "nothing to commit for: $*"; exit 0; fi
git -c user.name=rookasrudzianskas -c user.email=rookas.rudzenskas@gmail.com commit -q -m "$message"
git log -1 --format='%h %an <%ae> %s'
if [ "${LG_NO_PUSH:-0}" != "1" ]; then
  for attempt in 1 2 3; do
    if git push -q origin main 2>&1; then echo "pushed"; break; fi
    echo "push attempt $attempt failed" >&2; sleep 5
  done
fi
