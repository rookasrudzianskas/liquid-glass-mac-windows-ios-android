#!/usr/bin/env bash
# Builds the release artifacts of the Windows port into artifacts/windows (gitignored):
#   nuget/LiquidGlassEngineering.Core.1.0.0.nupkg (+ .snupkg)
#   nuget/LiquidGlassEngineering.Wpf.1.0.0.nupkg (+ .snupkg)
#   LiquidGlassStudio-win-x64.zip                 framework-dependent (needs the .NET 10 Desktop Runtime)
#   LiquidGlassStudio-win-arm64.zip               framework-dependent, Arm64
#   LiquidGlassStudio-win-x64-self-contained.zip  single file, no runtime needed
# Runs on macOS, Linux or Windows (WPF builds anywhere with EnableWindowsTargeting).
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
repo="$(cd "$here/../.." && pwd)"
out="$repo/artifacts/windows"
dotnet="$here/scripts/dotnet.sh"
work="$(mktemp -d -t liquid-glass-publish.XXXXXX)"
trap 'rm -rf "$work"' EXIT

rm -rf "$out/nuget" "$out"/LiquidGlassStudio-*.zip
mkdir -p "$out/nuget"
"$dotnet" pack "$here/src/LiquidGlass.Core/LiquidGlass.Core.csproj" -c Release -o "$out/nuget" -v quiet
"$dotnet" pack "$here/src/LiquidGlass.Wpf/LiquidGlass.Wpf.csproj" -c Release -o "$out/nuget" -v quiet

publish() {
  local name="$1"; shift
  "$dotnet" publish "$here/src/LiquidGlass.Studio/LiquidGlass.Studio.csproj" -c Release -o "$work/$name" -v quiet "$@"
  rm -f "$work/$name"/*.pdb
  (cd "$work/$name" && zip -qr -9 "$out/$name.zip" .)
}
publish LiquidGlassStudio-win-x64 -r win-x64 --self-contained false
publish LiquidGlassStudio-win-arm64 -r win-arm64 --self-contained false
publish LiquidGlassStudio-win-x64-self-contained -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

ls -la "$out" "$out/nuget"
