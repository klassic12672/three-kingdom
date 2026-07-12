#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLATFORM="${1:-macos}"
REQUIRE_SIGNING="${REQUIRE_SIGNING:-1}"
VERSION="$(sed -n 's/.*"projectVersion"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$ROOT/build/version.json")"
EXPECTED_TAG="v$VERSION"

ACTUAL_TAG="$(git -C "$ROOT" describe --tags --exact-match HEAD 2>/dev/null || true)"
if [[ "${ALLOW_UNTAGGED_RELEASE:-0}" != "1" && "$ACTUAL_TAG" != "$EXPECTED_TAG" ]]; then
  printf 'release packaging requires exact tag %s; current tag is %s.\n' "$EXPECTED_TAG" "${ACTUAL_TAG:-none}" >&2
  exit 1
fi

if [[ "$PLATFORM" != "macos" ]]; then
  printf 'package.sh supports macOS; use package.ps1 on Windows.\n' >&2
  exit 2
fi

"$ROOT/scripts/export.sh" macos release
SOURCE_APP="$ROOT/artifacts/exports/macos-arm64-release/ThreeKingdom.app"
STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT
APP="$STAGING/ThreeKingdom.app"
ditto --norsrc "$SOURCE_APP" "$APP"
xattr -cr "$APP"
PACKAGE="$ROOT/artifacts/ThreeKingdom-$VERSION-macos-arm64"

if [[ "$REQUIRE_SIGNING" == "1" ]]; then
  "$ROOT/scripts/sign-macos.sh" "$APP"
  PACKAGE="$PACKAGE-signed-notarized.zip"
else
  PACKAGE="$PACKAGE-unsigned.zip"
fi

rm -f "$PACKAGE"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$PACKAGE"
cp "$ROOT/artifacts/exports/macos-arm64-release/build-manifest.json" "${PACKAGE%.zip}-build-manifest.json"
printf 'Created auditable release package %s\n' "$PACKAGE"
