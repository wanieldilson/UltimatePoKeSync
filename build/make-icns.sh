#!/usr/bin/env bash
# Builds the macOS icon from src/UltimatePoKeSync.App/Assets/app-icon.png.
#
#   build/make-icns.sh <output.icns>
#
# macOS only: sips and iconutil are Apple tooling. The release workflow calls this on its
# macOS runner, which is also the only place the app bundle is assembled.

set -euo pipefail

output="${1:?usage: make-icns.sh <output.icns>}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source="$root/src/UltimatePoKeSync.App/Assets/app-icon.png"

[ -f "$source" ] || { echo "missing $source" >&2; exit 1; }

workspace="$(mktemp -d)"
trap 'rm -rf "$workspace"' EXIT
iconset="$workspace/icon.iconset"
mkdir -p "$iconset"

# The names are fixed: iconutil looks for exactly these.
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$source" --out "$iconset/icon_${size}x${size}.png" >/dev/null
  retina=$((size * 2))
  sips -z "$retina" "$retina" "$source" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done

mkdir -p "$(dirname "$output")"
iconutil --convert icns "$iconset" --output "$output"
echo "wrote $output"
