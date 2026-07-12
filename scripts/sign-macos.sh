#!/usr/bin/env bash
set -euo pipefail

APP_PATH="${1:-}"
[[ -n "$APP_PATH" && -d "$APP_PATH" ]] || { printf 'usage: %s PATH_TO_APP\n' "$0" >&2; exit 2; }
: "${MACOS_SIGNING_IDENTITY:?MACOS_SIGNING_IDENTITY is required for release signing}"
: "${MACOS_NOTARY_PROFILE:?MACOS_NOTARY_PROFILE is required for notarization}"

command -v codesign >/dev/null 2>&1 || { printf 'codesign is required.\n' >&2; exit 1; }
command -v xcrun >/dev/null 2>&1 || { printf 'Xcode command-line tools are required.\n' >&2; exit 1; }

ARCHIVE_PATH="${APP_PATH%.app}-notarization.zip"
rm -f "$ARCHIVE_PATH"

codesign --force --deep --strict --options runtime --timestamp \
  --sign "$MACOS_SIGNING_IDENTITY" "$APP_PATH"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"

ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$ARCHIVE_PATH"
xcrun notarytool submit "$ARCHIVE_PATH" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
xcrun stapler staple "$APP_PATH"
xcrun stapler validate "$APP_PATH"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"
spctl --assess --type execute --verbose=2 "$APP_PATH"
rm -f "$ARCHIVE_PATH"

printf 'Signed, notarized, stapled, and verified %s\n' "$APP_PATH"
