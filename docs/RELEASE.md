# Release Signing and Packaging

Release packages are created only by the scripts in this repository. The scripts require the exact tag matching `v` plus the project version in [`build/version.json`](../build/version.json), unless `ALLOW_UNTAGGED_RELEASE=1` is explicitly set for a non-release rehearsal. Signing is required by default. Setting `REQUIRE_SIGNING=0` produces an artifact whose filename is visibly marked `unsigned`; it must not be published.

Every export embeds and writes build-manifest schema 2 with the project version, Git commit, Godot version, .NET SDK version, platform, architecture, configuration, the canonical top-level pack checksum in `contentManifestChecksum`, and the aggregate validated registry checksum in `contentRegistryChecksum`.

## Verification timing

[ADR-0001](adr/0001-mac-first-development-deferred-physical-windows-verification.md) defers credentialed signing/notarization, physical Windows, Steam, clean-install/update, and release-candidate verification from M0 to M4/SP-15. M0 verifies that the workflows exist, protect secrets, and fail closed; it does not claim that production credentials or physical Windows hardware were exercised.

This deferral does not relax public-release requirements. A public demo or later candidate must satisfy the physical Windows and signed release gates below before promotion.

## macOS arm64

Required secrets and local environment values:

| Name | Purpose |
|---|---|
| `MACOS_SIGNING_IDENTITY` | Developer ID Application certificate common name or hash |
| `MACOS_NOTARY_PROFILE` | `notarytool` keychain profile stored outside the repository |

Run `./scripts/package.sh macos` from an exact release tag. It exports arm64, stages the app outside sync-provider metadata, signs with the hardened runtime, verifies the signature, submits to Apple notarization, staples and validates the ticket, assesses the app with Gatekeeper, then creates the final ZIP and sidecar manifest.

GitHub Actions additionally expects base64-encoded certificate material and temporary-keychain/notary credentials named in [the release workflow](../.github/workflows/release.yml). [`prepare-macos-signing.sh`](../scripts/ci/prepare-macos-signing.sh) imports them only into the ephemeral runner keychain. No certificate, password, API key, or profile is committed.

Godot's [macOS export documentation](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_macos.html) describes the Developer ID and notarization prerequisites. Losing these credentials blocks a release, so maintain encrypted offline backups and renewal records separately.

## Windows x64

Required environment values:

| Name | Purpose |
|---|---|
| `WINDOWS_CERTIFICATE_PATH` | Local path to the Authenticode `.pfx` file |
| `WINDOWS_CERTIFICATE_PASSWORD` | Certificate password |
| `WINDOWS_TIMESTAMP_URL` | RFC 3161 timestamp service; defaults to DigiCert |
| `SIGNTOOL_PATH` | Optional explicit path to `signtool.exe` |

Run `./scripts/package.ps1` from an exact release tag. It exports Windows x64, signs the executable with SHA-256 and an RFC 3161 timestamp, verifies it with `signtool` and `Get-AuthenticodeSignature`, then creates the final ZIP and sidecar manifest. GitHub Actions accepts a base64 certificate through `WINDOWS_CERTIFICATE_BASE64`, writes it only to the ephemeral runner, and fails closed if any signing input is missing.

See Godot's [Windows signing documentation](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_windows.html) for certificate and Windows SDK prerequisites.

## Verification gate

A package is release-ready only when all of the following are recorded for the tag:

- Clean macOS and Windows CI jobs passed.
- Native smoke launch passed on Apple Silicon macOS and Windows x64 hardware.
- The macOS Developer ID signature, notarization ticket, stapling, and Gatekeeper assessment passed.
- The Windows Authenticode signature and timestamp verification passed.
- The sidecar manifest matches the embedded manifest and both expected content checksums.

CI without signing secrets may still produce unsigned development artifacts, but the signed release workflow cannot succeed or mark them release-ready.
