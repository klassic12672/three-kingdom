#!/usr/bin/env bash
set -euo pipefail

: "${RUNNER_TEMP:?RUNNER_TEMP is required}"
: "${MACOS_CERTIFICATE_BASE64:?MACOS_CERTIFICATE_BASE64 is required}"
: "${MACOS_CERTIFICATE_PASSWORD:?MACOS_CERTIFICATE_PASSWORD is required}"
: "${MACOS_KEYCHAIN_PASSWORD:?MACOS_KEYCHAIN_PASSWORD is required}"
: "${MACOS_NOTARY_APPLE_ID:?MACOS_NOTARY_APPLE_ID is required}"
: "${MACOS_NOTARY_PASSWORD:?MACOS_NOTARY_PASSWORD is required}"
: "${MACOS_TEAM_ID:?MACOS_TEAM_ID is required}"

CERTIFICATE_PATH="$RUNNER_TEMP/developer-id.p12"
KEYCHAIN_PATH="$RUNNER_TEMP/release-signing.keychain-db"
PROFILE="three-kingdom-ci"

printf '%s' "$MACOS_CERTIFICATE_BASE64" | base64 --decode > "$CERTIFICATE_PATH"
security create-keychain -p "$MACOS_KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
security unlock-keychain -p "$MACOS_KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
security import "$CERTIFICATE_PATH" -P "$MACOS_CERTIFICATE_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN_PATH"
security set-key-partition-list -S apple-tool:,apple: -s -k "$MACOS_KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH" >/dev/null
security list-keychains -d user -s "$KEYCHAIN_PATH"
xcrun notarytool store-credentials "$PROFILE" \
  --apple-id "$MACOS_NOTARY_APPLE_ID" \
  --team-id "$MACOS_TEAM_ID" \
  --password "$MACOS_NOTARY_PASSWORD"

if [[ -n "${GITHUB_ENV:-}" ]]; then
  printf 'MACOS_NOTARY_PROFILE=%s\n' "$PROFILE" >> "$GITHUB_ENV"
fi
printf 'Ephemeral macOS signing keychain and notary profile are ready.\n'
