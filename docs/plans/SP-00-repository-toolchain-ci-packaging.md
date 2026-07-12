# SP-00 — Repository, Toolchain, CI, and Packaging

## Metadata

| Field | Value |
|---|---|
| Status | **Complete** |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M0 |
| Dependencies | None |
| Affected ADRs | [ADR-0001](../adr/0001-mac-first-development-deferred-physical-windows-verification.md), [ADR index](../adr/README.md) |

## Goal

Establish a reproducible Mac-first solo-development environment that builds, tests, packages, and smoke-runs the same Godot C# project natively on the Apple Silicon development Mac and continuously through hosted Windows x64 CI.

## Non-goals

- Gameplay, simulation, content, or presentation implementation.
- Linux, Intel Mac, console, mobile, or web exports.
- Steam achievements or cloud saves beyond reserving a platform boundary.
- Publishing a public build.
- Physical Windows manual playtesting and production signing/notarization verification, which are deferred to M4/SP-15 by ADR-0001.

## Requirements

- Pin Godot 4.6.1 .NET and its matching .NET export templates.
- Pin the current .NET 10 LTS SDK patch with `global.json` and commit the generated solution/project files.
- Use VS Code with C# tooling as the documented default editor; do not require a paid IDE.
- Configure Git LFS for source art, textures, meshes, animations, audio, video, and other large binary assets.
- Establish repository directories for assemblies, Godot scenes/assets, authored data, schemas, tools, tests, documentation, build scripts, and distributable output.
- Add Godot, .NET, OS, IDE, build-output, and local-secret ignore rules.
- Provide development and release export presets for Windows x64 and macOS arm64.
- Keep signing identities, notarization credentials, Steam credentials, and certificates outside version control.
- CI must run documentation validation, `dotnet` restore/build/test, Godot headless import, and platform export smoke checks.
- macOS is the primary local interactive platform through M3; hosted Windows CI remains mandatory portability coverage.
- Physical Windows validation is required before M4 closes, not before M0 closes.
- Release packaging must be produced by tagged, auditable scripts rather than undocumented editor clicks.

## Public contracts

SP-00 establishes the physical assembly boundaries named in the master plan. It does not implement domain contracts, but build verification must reserve projects/namespaces for `Simulation.Core`, `Game.Application`, `Game.Content`, `Game.Presentation`, `Game.Platform.Steam`, and `Tools.ContentPipeline`.

Every distributable build writes a versioned machine-readable build manifest containing project version, Git commit, Godot version, .NET SDK version, platform, architecture, build configuration, the canonical top-level content-pack manifest checksum, and the aggregate validated content-registry checksum.

## Data flow

```text
Pinned source + pinned tools + content/assets
→ restore/import
→ compile and test
→ Godot export
→ sign/notarize for authorized release candidates
→ smoke run
→ immutable build artifact + build manifest
```

## Implementation workstreams

1. Install and record Godot 4.6.1 .NET, matching templates, .NET 10 LTS, Git LFS, VS Code extensions, Xcode/command-line tools, and Windows signing tooling.
2. Create the repository layout and empty projects without adding gameplay code.
3. Configure `.gitignore`, `.gitattributes`, version pins, dependency lock behavior, and deterministic build metadata.
4. Add headless local scripts for validation, build, test, and export; scripts must fail on the first failed stage.
5. Configure macOS and Windows CI to run equivalent validation, build, import, export, automated smoke, manifest, and artifact stages.
6. Configure macOS arm64 signing/notarization and Windows x64 signing as optional release stages gated by secrets; defer credentialed verification to SP-15.
7. Produce and manually launch the macOS development export locally, and produce/automatically smoke-run both platform exports in hosted CI.

## Edge cases and failure handling

- A missing pinned SDK or export template produces an actionable preflight error before compilation.
- CI without signing secrets still produces unsigned internal artifacts but cannot mark them release-ready.
- A Git LFS pointer without its object fails asset validation rather than entering a build.
- Godot patch upgrades occur only on a dedicated branch and require clean import, tests, exports, and smoke runs.
- Platform-specific plugins must support Windows x64 and macOS arm64 or remain isolated behind optional adapters.

## Performance budget

- Clean CI restore/import/build should complete within 15 minutes per platform before substantial game assets exist.
- Incremental local C# compilation should target less than 10 seconds during M0–M2.
- Development smoke builds should start within 5 seconds on the development Mac and hosted Windows runner before substantial assets exist.

## Tests

- Confirm pinned version commands match recorded values.
- Clone into a clean directory and complete restore/build/test/export without manual editor state.
- Launch the exported macOS build locally and both exported builds in hosted CI; verify build-manifest display/logging.
- Verify unsigned release attempts fail closed when signing is required.
- Verify a missing LFS object and a broken documentation link fail CI.

## Acceptance criteria

- [x] Pinned Godot 4.6.1 .NET and .NET 10 SDK are documented and machine-checked.
- [x] Repository layout and assembly boundaries match the master plan and architecture tests.
- [x] Git LFS and ignore rules cover expected generated and binary files.
- [x] macOS and Windows CI pass from a clean checkout.
- [x] The Apple Silicon macOS development export launches locally and in hosted CI with matching manifest evidence.
- [x] The Windows x64 hosted job exports, automatically smoke-runs, and uploads matching exact-SHA artifact evidence.
- [x] Signing/notarization and Authenticode workflows are configured, secret-gated, and fail closed; production verification remains an SP-15 gate.
- [x] The intended baseline candidate set contains no secrets or machine-local absolute paths.
- [x] No secrets or machine-local absolute paths are committed in the exact-SHA clean checkout.

### Implementation verification (2026-07-11)

- macOS arm64 headless import, development export, ad-hoc staging, launch, manifest logging, and clean exit pass locally.
- Windows x64 development export passes locally using the pinned templates; automated launch remains for hosted Windows CI and physical validation is deferred to M4.
- Signed/notarized macOS and signed Windows workflows are implemented and fail closed. ADR-0001 defers credentialed verification to SP-15 before public promotion.

### Exact-SHA evidence (2026-07-12)

- Exact SHA `1ab375a5e812c14eba4eca4cc121e604ade73f47` passed an approved-origin fresh clone, LFS pull/fsck, pinned preflight, Release build/tests, Godot import, and exact-SHA credential/path validation.
- Hosted [CI run 29179730891](https://github.com/klassic12672/three-kingdom/actions/runs/29179730891) passed the macOS arm64 job and uploaded a verified unsigned artifact, but the Windows job failed in temporary Git-repository cleanup before import, export, smoke, and upload.
- That Windows hosted automated-smoke/artifact gate remains failed for the historical revision. See the [historical report](../evidence/M0-EXACT-SHA-1ab375a.md).
- Cleanup-fix SHA `7f62a97cf880ae6ded8e47af8737a11e53479977` passed [CI run 29180111376](https://github.com/klassic12672/three-kingdom/actions/runs/29180111376) on hosted macOS arm64 and Windows x64 through validation, build, tests, import, export, native automated smoke, and artifact upload.
- Artifacts `8256131824` and `8256135950` matched GitHub's digests when downloaded; embedded and sidecar manifests match canonically and identify the exact SHA and expected platform/tool/content values. See the [passing report](../evidence/M0-EXACT-SHA-7f62a97.md).
- Physical Windows and production signing remain later M4/SP-15 gates under ADR-0001.

## Risks

| Risk | Mitigation |
|---|---|
| Godot C# regression on Apple Silicon | Pin engine/templates and require migration-branch smoke exports before upgrades. |
| No physical Windows coverage | Acquire or dedicate a Windows x64 machine before M4; use CI only as interim coverage. |
| Large assets overwhelm Git | Enforce LFS patterns and artifact storage before importing production assets. |
| Solo release credentials are lost | Maintain encrypted offline backups and documented renewal procedures. |

## Deferred work

- SteamPipe upload automation beyond a private test application.
- Steam Workshop, achievements, and cloud saves.
- Physical Windows manual validation and production signing/notarization certification, covered by M4 and SP-15.
- Crash-symbol hosting and public release branches, covered by [SP-15](SP-15-steam-release-qa-crash-reporting.md).
