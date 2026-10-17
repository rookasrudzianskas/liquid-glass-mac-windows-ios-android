#!/usr/bin/env bash
# Checks that the project's NuGet feed serves the packages this repository builds. For every package in the folder
# (default artifacts/windows/nuget), the feed must list its version, ship the same lib/ target frameworks, and have been
# packed from a commit whose package sources match those of the local package's commit: the library project (and
# Core's, for the WPF package), Directory.Build.props, global.json, the package icon, LICENSE and NOTICE.md. The feed is
# deployed with the website (website/scripts/deploy.sh), so this tells whether that deploy is behind the code; it exits
# 1 when it is.
# Usage: platforms/windows/scripts/check-feed.sh [package-folder] [feed-index-url]
set -euo pipefail
repo="$(cd "$(dirname "$0")/../../.." && pwd)"
packages="$(cd "${1:-$repo/artifacts/windows/nuget}" && pwd)"
index="${2:-https://liquid-glass-engineering.vercel.app/nuget/index.json}"
shared=(platforms/windows/Directory.Build.props platforms/windows/global.json platforms/windows/packaging LICENSE NOTICE.md
  platforms/windows/src/LiquidGlass.Core)
work="$(mktemp -d -t liquid-glass-feed.XXXXXX)"
trap 'rm -rf "$work"' EXIT

nuspec() { unzip -p "$1" '*.nuspec' | tr -d '\r'; }
field() { sed -n "s#.*<$1>\([^<]*\)</$1>.*#\1#p" | head -n 1; }
commit_of() { sed -n 's#.*<repository [^>]*commit="\([0-9a-f]*\)".*#\1#p' | head -n 1; }
frameworks() { unzip -Z1 "$1" | sed -n 's#^lib/\([^/]*\)/.*#\1#p' | sort -u | paste -sd ' ' -; }
have_commit() { git -C "$repo" cat-file -e "$1^{commit}" 2> /dev/null || git -C "$repo" fetch -q --depth=1 origin "$1" 2> /dev/null; }

# The flat container ("PackageBaseAddress") named by the service index.
base="$(curl -fsSL "$index" | tr -d '\n' | grep -o '{[^{}]*"PackageBaseAddress/3.0.0"[^{}]*}' | grep -o '"@id": *"[^"]*"' | sed 's#.*"\([^"]*\)"$#\1#' | head -n 1)"
[ -n "$base" ] || { echo "$index names no PackageBaseAddress"; exit 1; }
echo "feed: $index"

shopt -s nullglob
local_packages=("$packages"/*.nupkg)
[ "${#local_packages[@]}" -gt 0 ] || { echo "no packages in $packages"; exit 1; }
stale=0
for package in "${local_packages[@]}"; do
  id="$(nuspec "$package" | field id)"
  version="$(nuspec "$package" | field version)"
  built="$(nuspec "$package" | commit_of)"
  lower="$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')"
  lower_version="$(printf '%s' "$version" | tr '[:upper:]' '[:lower:]')"
  sources=("${shared[@]}")
  [ "$id" = LiquidGlassEngineering.Core ] || sources+=(platforms/windows/src/LiquidGlass.Wpf)
  echo
  echo "$id $version, packed here from ${built:0:7}"
  if ! curl -fsSL "$base$lower/index.json" | grep -q "\"$lower_version\""; then
    echo "  STALE: the feed does not list version $version"; stale=1; continue
  fi
  served="$work/$lower.$lower_version.nupkg"
  curl -fsSL -o "$served" "$base$lower/$lower_version/$lower.$lower_version.nupkg"
  deployed="$(nuspec "$served" | commit_of)"
  echo "  feed: packed from ${deployed:0:7}, lib/ $(frameworks "$served")"
  echo "  here: lib/ $(frameworks "$package")"
  if [ "$(frameworks "$served")" != "$(frameworks "$package")" ]; then
    echo "  STALE: the target frameworks differ"; stale=1
  fi
  if [ -z "$deployed" ] || [ -z "$built" ]; then
    echo "  STALE: a package does not record the commit it was packed from"; stale=1; continue
  fi
  if [ "$deployed" != "$built" ]; then
    if ! have_commit "$deployed" || ! have_commit "$built"; then
      echo "  STALE: cannot compare ${deployed:0:7} with ${built:0:7} (commit not found)"; stale=1; continue
    fi
    changed="$(git -C "$repo" diff --name-only "$deployed" "$built" -- "${sources[@]}")"
    if [ -n "$changed" ]; then
      echo "  STALE: $(printf '%s\n' "$changed" | wc -l | tr -d ' ') package source file(s) changed since the feed's build, for example:"
      printf '%s\n' "$changed" | head -n 5 | sed 's#^#    #'
      stale=1; continue
    fi
  fi
  echo "  current"
done
echo
if [ "$stale" -ne 0 ]; then
  echo "The feed is behind these packages: deploy the website with website/scripts/deploy.sh after scripts/package.sh."
  exit 1
fi
echo "The feed serves these packages."
