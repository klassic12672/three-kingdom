#!/usr/bin/env bash
set -euo pipefail

: "${RUNNER_TEMP:?RUNNER_TEMP is required}"
VERSION="4.6.1"
TEMPLATE_VERSION="4.6.1.stable.mono"
BASE_URL="https://github.com/godotengine/godot-builds/releases/download/${VERSION}-stable"
WORK="$RUNNER_TEMP/godot-$VERSION"
EDITOR_ARCHIVE="$WORK/editor.zip"
TEMPLATE_ARCHIVE="$WORK/templates.tpz"

mkdir -p "$WORK/editor" "$WORK/template-extract"
curl --fail --location --retry 3 --output "$EDITOR_ARCHIVE" \
  "$BASE_URL/Godot_v${VERSION}-stable_mono_macos.universal.zip"
curl --fail --location --retry 3 --output "$TEMPLATE_ARCHIVE" \
  "$BASE_URL/Godot_v${VERSION}-stable_mono_export_templates.tpz"
unzip -q "$EDITOR_ARCHIVE" -d "$WORK/editor"
unzip -q "$TEMPLATE_ARCHIVE" -d "$WORK/template-extract"

GODOT_PATH="$(find "$WORK/editor" -path '*/Contents/MacOS/Godot' -type f | head -n 1)"
[[ -x "$GODOT_PATH" ]] || { printf 'Godot executable was not found after extraction.\n' >&2; exit 1; }
TEMPLATE_SOURCE="$(find "$WORK/template-extract" -type d -name templates | head -n 1)"
[[ -d "$TEMPLATE_SOURCE" ]] || { printf 'Godot templates were not found after extraction.\n' >&2; exit 1; }
TEMPLATE_DEST="$HOME/Library/Application Support/Godot/export_templates/$TEMPLATE_VERSION"
mkdir -p "$TEMPLATE_DEST"
cp -R "$TEMPLATE_SOURCE/." "$TEMPLATE_DEST/"

if [[ -n "${GITHUB_ENV:-}" ]]; then
  printf 'GODOT_BIN=%s\n' "$GODOT_PATH" >> "$GITHUB_ENV"
  printf 'GODOT_EXPORT_TEMPLATES_DIR=%s\n' "$TEMPLATE_DEST" >> "$GITHUB_ENV"
fi
printf 'Installed Godot %s and matching templates.\n' "$VERSION"
