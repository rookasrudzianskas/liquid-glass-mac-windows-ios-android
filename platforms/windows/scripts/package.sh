#!/usr/bin/env bash
# Builds the release artifacts of the Windows port into artifacts/windows (gitignored):
#   nuget/LiquidGlassEngineering.Core.<version>.nupkg (the libraries carry their PDBs embedded)
#   nuget/LiquidGlassEngineering.Wpf.<version>.nupkg
#   LiquidGlassStudio-win-x64.zip                 framework-dependent (needs the .NET 10 Desktop Runtime, x64)
#   LiquidGlassStudio-win-arm64.zip               framework-dependent (needs the .NET 10 Desktop Runtime, Arm64)
#   LiquidGlassStudio-win-x64-self-contained.zip  single file, no runtime needed
# <version> is Directory.Build.props' Version. Only that version's packages are replaced: the packages of earlier
# versions stay in nuget/, because the website's NuGet feed is built from that folder and must keep serving every
# released version (website/scripts/deploy.sh restores released packages a fresh clone lacks).
# Every build is a continuous-integration build: deterministic, with source paths mapped to /_/ instead of this
# machine's, and Source Link to the commit. Packages and zips carry LICENSE and NOTICE.md; the zips leave out PDBs
# and the libraries' IntelliSense files. Runs on macOS, Linux or Windows (WPF builds anywhere with
# EnableWindowsTargeting); the Windows workflow builds the same artifacts on every push.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
repo="$(cd "$here/../.." && pwd)"
out="$repo/artifacts/windows"
dotnet="$here/scripts/dotnet.sh"
work="$(mktemp -d -t liquid-glass-publish.XXXXXX)"
trap 'rm -rf "$work"' EXIT
ci=(-p:ContinuousIntegrationBuild=true)

version="$(sed -n 's#.*<Version>\([^<]*\)</Version>.*#\1#p' "$here/Directory.Build.props" | head -n 1)"
[ -n "$version" ] || { echo "platforms/windows/Directory.Build.props has no <Version>" >&2; exit 1; }
rm -f "$out"/nuget/*."$version".nupkg "$out"/nuget/*."$version".snupkg "$out"/LiquidGlassStudio-*.zip
mkdir -p "$out/nuget" "$work/nuget"
"$dotnet" pack "$here/src/LiquidGlass.Core/LiquidGlass.Core.csproj" -c Release -o "$work/nuget" -v quiet "${ci[@]}"
"$dotnet" pack "$here/src/LiquidGlass.Wpf/LiquidGlass.Wpf.csproj" -c Release -o "$work/nuget" -v quiet "${ci[@]}"

publish() {
  local name="$1"; shift
  "$dotnet" publish "$here/src/LiquidGlass.Studio/LiquidGlass.Studio.csproj" -c Release -o "$work/$name" -v quiet "${ci[@]}" "$@"
  rm -f "$work/$name"/*.pdb "$work/$name"/*.xml
  for file in LICENSE NOTICE.md; do
    [ -f "$work/$name/$file" ] || { echo "$name: $file is missing from the publish output" >&2; exit 1; }
  done
  (cd "$work/$name" && zip -qr -9 "$out/$name.zip" .)
}
publish LiquidGlassStudio-win-x64 -r win-x64 --self-contained false
publish LiquidGlassStudio-win-arm64 -r win-arm64 --self-contained false
publish LiquidGlassStudio-win-x64-self-contained -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

# The release must not record this machine's paths.
for package in "$work"/nuget/*.nupkg; do
  case "$(basename "$package")" in
    *."$version".nupkg) ;;
    *) echo "$(basename "$package"): not version $version (Directory.Build.props)" >&2; exit 1 ;;
  esac
  if unzip -p "$package" '*.dll' | LC_ALL=C grep -a -q "$repo"; then
    echo "$(basename "$package"): a library records the local path $repo" >&2
    exit 1
  fi
  for file in LICENSE NOTICE.md README.md; do
    unzip -l "$package" "$file" > /dev/null || { echo "$(basename "$package"): $file is missing" >&2; exit 1; }
  done
done

mv -f "$work"/nuget/*.nupkg "$out/nuget/"

ls -la "$out" "$out/nuget"
