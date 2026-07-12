#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLATFORM="${1:-macos}"

if [[ "$PLATFORM" != "macos" ]]; then
  printf 'smoke.sh supports native macOS smoke runs; use smoke.ps1 on Windows.\n' >&2
  exit 2
fi

APP="$ROOT/artifacts/exports/macos-arm64-development/ThreeKingdom.app"
[[ -d "$APP" ]] || "$ROOT/scripts/export.sh" macos development
STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT
STAGED_APP="$STAGING/ThreeKingdom.app"
ditto --norsrc "$APP" "$STAGED_APP"
xattr -cr "$STAGED_APP"
codesign --force --deep --sign - "$STAGED_APP"
codesign --verify --deep --strict "$STAGED_APP"

EXECUTABLE="$STAGED_APP/Contents/MacOS/Three Kingdoms Grand Strategy"
if [[ ! -x "$EXECUTABLE" ]]; then
  EXECUTABLE="$(find "$STAGED_APP/Contents/MacOS" -maxdepth 1 -type f -perm -111 | head -n 1)"
fi
[[ -n "$EXECUTABLE" && -x "$EXECUTABLE" ]] || { printf 'No executable found in %s\n' "$STAGED_APP" >&2; exit 1; }
"$EXECUTABLE" --headless -- --smoke-test
printf 'macOS arm64 smoke build launched successfully.\n'
