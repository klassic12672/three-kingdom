#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMPLATE_VERSION="4.6.1.stable.mono"
if [[ -n "${GODOT_EXPORT_TEMPLATES_DIR:-}" ]]; then
  TEMPLATE_DIR="$GODOT_EXPORT_TEMPLATES_DIR"
else
  TEMPLATE_DIR="$HOME/Library/Application Support/Godot/export_templates/$TEMPLATE_VERSION"
fi
SOURCE="$TEMPLATE_DIR/macos.zip"
OUTPUT="$ROOT/artifacts/templates/macos-arm64.zip"

[[ "$(uname -s)" == "Darwin" ]] || { printf 'The arm64 template must be prepared on macOS.\n' >&2; exit 1; }
command -v lipo >/dev/null 2>&1 || { printf 'lipo is required; install Xcode command-line tools.\n' >&2; exit 1; }
[[ -f "$SOURCE" ]] || { printf 'Matching macOS template is missing: %s\n' "$SOURCE" >&2; exit 1; }

if [[ -f "$OUTPUT" && "$OUTPUT" -nt "$SOURCE" ]]; then
  printf 'Using existing derived macOS arm64 template: %s\n' "$OUTPUT"
  exit 0
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
unzip -q "$SOURCE" -d "$WORK"
MACOS_DIR="$WORK/macos_template.app/Contents/MacOS"

lipo -thin arm64 "$MACOS_DIR/godot_macos_debug.universal" -output "$MACOS_DIR/godot_macos_debug.arm64"
lipo -thin arm64 "$MACOS_DIR/godot_macos_release.universal" -output "$MACOS_DIR/godot_macos_release.arm64"
rm "$MACOS_DIR/godot_macos_debug.universal" "$MACOS_DIR/godot_macos_release.universal"
chmod +x "$MACOS_DIR/godot_macos_debug.arm64" "$MACOS_DIR/godot_macos_release.arm64"
xattr -cr "$WORK/macos_template.app"

mkdir -p "$(dirname "$OUTPUT")"
rm -f "$OUTPUT"
(
  cd "$WORK"
  zip -qry "$OUTPUT" macos_template.app
)

printf 'Derived macOS arm64 template from Godot %s: %s\n' "$TEMPLATE_VERSION" "$OUTPUT"
