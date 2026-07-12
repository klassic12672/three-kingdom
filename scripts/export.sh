#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLATFORM="${1:-}"
FLAVOR="${2:-development}"
GODOT_COMMAND="${GODOT_BIN:-godot}"
if command -v "$GODOT_COMMAND" >/dev/null 2>&1; then
  GODOT_COMMAND="$(realpath "$(command -v "$GODOT_COMMAND")")"
fi

case "$PLATFORM:$FLAVOR" in
  windows:development)
    PRESET="Windows x64 Development"
    OUTPUT_DIR="$ROOT/artifacts/exports/windows-x64-development"
    OUTPUT="$OUTPUT_DIR/ThreeKingdom.exe"
    ARCHITECTURE="x86_64"
    CONFIGURATION="Development"
    EXPORT_FLAG="--export-debug"
    ;;
  windows:release)
    PRESET="Windows x64 Release"
    OUTPUT_DIR="$ROOT/artifacts/exports/windows-x64-release"
    OUTPUT="$OUTPUT_DIR/ThreeKingdom.exe"
    ARCHITECTURE="x86_64"
    CONFIGURATION="Release"
    EXPORT_FLAG="--export-release"
    ;;
  macos:development)
    PRESET="macOS arm64 Development"
    OUTPUT_DIR="$ROOT/artifacts/exports/macos-arm64-development"
    OUTPUT="$OUTPUT_DIR/ThreeKingdom.app"
    ARCHITECTURE="arm64"
    CONFIGURATION="Development"
    EXPORT_FLAG="--export-debug"
    ;;
  macos:release)
    PRESET="macOS arm64 Release"
    OUTPUT_DIR="$ROOT/artifacts/exports/macos-arm64-release"
    OUTPUT="$OUTPUT_DIR/ThreeKingdom.app"
    ARCHITECTURE="arm64"
    CONFIGURATION="Release"
    EXPORT_FLAG="--export-release"
    ;;
  *)
    printf 'usage: %s {windows|macos} {development|release}\n' "$0" >&2
    exit 2
    ;;
esac

"$ROOT/scripts/preflight.sh" --require-templates
if [[ "$PLATFORM" == "macos" ]]; then
  "$ROOT/scripts/prepare-macos-arm64-template.sh"
fi
"$ROOT/scripts/test.sh" Release
"$ROOT/scripts/import.sh"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet run --project "$ROOT/tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj" --configuration Release --no-build -- manifest \
  --repository-root "$ROOT" \
  --platform "$PLATFORM" \
  --architecture "$ARCHITECTURE" \
  --configuration "$CONFIGURATION" \
  --output "$ROOT/game/generated/build-manifest.json"

"$GODOT_COMMAND" --headless --path "$ROOT/game" "$EXPORT_FLAG" "$PRESET" "$OUTPUT"
if [[ "$PLATFORM" == "macos" ]]; then
  APP_EXECUTABLE="$(find "$OUTPUT/Contents/MacOS" -maxdepth 1 -type f -perm -111 | head -n 1)"
  [[ -n "$APP_EXECUTABLE" ]] || { printf 'No macOS executable was produced.\n' >&2; exit 1; }
  [[ "$(lipo -archs "$APP_EXECUTABLE")" == "arm64" ]] || { printf 'macOS export is not arm64-only.\n' >&2; exit 1; }
fi
cp "$ROOT/game/generated/build-manifest.json" "$OUTPUT_DIR/build-manifest.json"
printf 'Exported %s to %s\n' "$PRESET" "$OUTPUT"
