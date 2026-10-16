#!/usr/bin/env bash
# Runs the .NET SDK used by the Windows port, installing it into ~/.dotnet first if it is missing
# (Microsoft's official dotnet-install.sh, no sudo). Usage: scripts/dotnet.sh <dotnet arguments>
set -euo pipefail
root="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ ! -x "$root/dotnet" ]; then
  installer="$(mktemp -t dotnet-install.XXXXXX)"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  bash "$installer" --channel 10.0 --install-dir "$root" >&2
  rm -f "$installer"
fi
export DOTNET_ROOT="$root" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
exec "$root/dotnet" "$@"
