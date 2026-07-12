# SP-15 — Steam, Release QA, and Crash Reporting

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M0 for packaging groundwork; M4 for public distribution |
| Dependencies | [SP-00](SP-00-repository-toolchain-ci-packaging.md), [SP-11](SP-11-ui-ux-accessibility-tutorial.md), [SP-14](SP-14-art-animation-vfx-audio-provenance.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Ship reliable Steam demo, Early Access, and version 1.0 builds for Windows x64 and Apple Silicon macOS with repeatable packaging, platform integration, crash diagnostics, truthful disclosures, and regression gates.

## Non-goals

- Non-Steam storefronts before version 1.0.
- Linux, Intel Mac, console, or mobile distribution.
- Explicit-content depots or adult DLC through version 1.0.
- Achievements or cloud saves before their underlying systems stabilize.

## Requirements

- Maintain distinct Steam applications/packages as required for base game and demo, with OS-specific depots and private test branches.
- Separate development, QA candidate, demo, Early Access, and release branch promotion; never build unique binaries manually after candidate approval.
- Windows builds target x64; macOS builds target arm64 and are signed/notarized before public promotion.
- Integrate Steam through `Game.Platform.Steam` with a no-Steam fallback for local development and offline play.
- Stage achievements after stable event IDs and cloud saves after stable save/migration behavior; platform failure must not block local saves or core play.
- Generate build manifests, content checksums, changelogs, symbols, provenance reports, localization coverage, and test evidence per candidate.
- Implement structured local logs, crash capture, privacy-respecting user consent, symbolicated diagnostics, and a manual exportable bug-report bundle.
- Autosaves and crash recovery must protect the last valid manual/autosave generations.
- Steam store/Early Access text accurately states supported platforms, content, incompleteness, AI-assisted asset use, and mature themes.
- Public depots, screenshots, trailers, store text, and builds contain no sexually explicit material through version 1.0.
- Maintain release-blocking regression suites for simulation determinism, saves/migrations, scenarios, battles, localization, accessibility, assets, and platform packaging.

## Public contracts

- `IPlatformServices`: initialization, user identity abstraction, overlay availability, achievements, cloud availability, and graceful no-platform behavior.
- `BuildManifest`: contract established by SP-00 and included with diagnostics.
- `CrashReportEnvelope`: game/build/content versions, platform, sanitized logs, stack/native symbols references, save metadata, reproduction notes, and consent.
- `ReleaseCandidateManifest`: artifact checksums, depot targets, tests, disclosures, known issues, migration range, and approval state.
- Platform data never enters authoritative simulation checksums.

## Data flow

```text
Tagged source + validated content/assets + platform secrets
→ CI build/test/export
→ sign/notarize and symbol package
→ private Steam branch
→ regression/manual QA
→ immutable release candidate manifest
→ promotion to demo/EA/release branch
→ crash/bug diagnostics feed next candidate
```

## Implementation workstreams

1. Establish Steam partner/test application configuration, OS depots, packages, private branches, and least-privilege credentials.
2. Implement platform abstraction and local/no-Steam behavior.
3. Automate build, symbol, signing, notarization, upload, branch selection, and release-candidate manifests.
4. Implement structured logs, crash capture, consent, symbolication, bug-report bundle, and save recovery.
5. Build demo packaging and bounded-content rules for M4.
6. Add Early Access disclosures, update procedure, save compatibility matrix, rollback, and known-issue workflow.
7. Stage achievements and cloud saves only after their explicit readiness gates pass.
8. Execute final 1.0 platform, content, store, and regression certification checklist.

## Edge cases and failure handling

- Steam unavailable/offline: game starts, local saves work, and platform features report unavailable without repeated modal errors.
- Cloud conflict, once enabled: preserve both versions and ask the player; never auto-delete a newer local save.
- Failed signing/notarization/upload prevents public promotion and leaves the previous branch untouched.
- A bad public update has a documented rollback to the last approved manifest/build.
- Crash reporting excludes secrets and personally identifying paths where possible and requires user consent before upload/export.
- Save incompatibility blocks promotion unless a tested migration or explicit preserved legacy branch exists.

## Performance budget

- Steam/platform initialization adds less than 2 seconds to normal startup when online and times out gracefully when unavailable.
- Crash logging remains bounded and adds negligible normal-frame overhead.
- Release-build packaging may be lengthy but must be unattended, reproducible, and fail closed.

## Tests

- Steam online/offline/unavailable initialization and overlay tests.
- Windows x64 and macOS arm64 clean-install, update, uninstall/reinstall, and offline tests.
- Signing, notarization, depot routing, branch promotion, and rollback tests.
- Local save, future cloud conflict, migration, corrupt-save, autosave recovery, and legacy-branch tests.
- Crash generation, symbolication, consent, sanitization, and bug-bundle tests.
- Demo content-boundary and save-separation tests.
- Store/build scan for platform claims, localization, AI disclosure, mature content, and absence of explicit assets/text.

## Acceptance criteria

- [ ] The same tagged source creates reproducible Windows and macOS artifacts with manifests/checksums.
- [ ] Private Steam branches install and run correct OS/architecture depots.
- [ ] macOS public candidates pass signing, notarization, Gatekeeper, and Steam overlay checks.
- [ ] The game works offline and without Steam platform services except for optional platform features.
- [ ] Crash/bug bundles are useful, symbolicated where possible, consented, and sanitized.
- [ ] Demo, Early Access, rollback, migration, and version 1.0 procedures are documented and exercised.
- [ ] Store and build disclosures are accurate and no explicit content exists through version 1.0.
- [ ] Every public candidate passes the declared automated and manual regression gates.

## Risks

| Risk | Mitigation |
|---|---|
| Mac support regresses while developing primarily in editor | Run packaged macOS smoke tests continuously and full notarized tests at each candidate. |
| Windows issues appear late | Maintain Windows CI immediately and obtain physical test hardware before M4. |
| Early Access updates break saves | Version schemas/content, test migrations, retain rollback/legacy branches, and publish compatibility notes. |
| Steam/AI/mature-content disclosures are inaccurate | Generate disclosure inputs from content/provenance reports and manually review every candidate/store update. |

## Deferred work

- GOG, Epic, itch.io, or direct sales.
- Steam Workshop.
- Achievements until event contracts stabilize.
- Cloud saves until migration and conflict behavior is proven.
- Any post-1.0 explicit-content proposal.
