# SP-00 — Repository, Toolchain, CI, and Packaging

## Metadata

| Field | Value |
|---|---|
| Status | **In progress — local macOS path verified; external gates remain** |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M0 |
| Dependencies | None |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Establish a reproducible solo-development environment that builds, tests, packages, and smoke-runs the same Godot C# project on the Apple Silicon development Mac and Windows x64 CI/test hardware.

## Non-goals

- Gameplay, simulation, content, or presentation implementation.
- Linux, Intel Mac, console, mobile, or web exports.
- Steam achievements or cloud saves beyond reserving a platform boundary.
- Publishing a public build.

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
→ sign/notarize where applicable
→ smoke run
→ immutable build artifact + build manifest
```

## Implementation workstreams

1. Install and record Godot 4.6.1 .NET, matching templates, .NET 10 LTS, Git LFS, VS Code extensions, Xcode/command-line tools, and Windows signing tooling.
2. Create the repository layout and empty projects without adding gameplay code.
3. Configure `.gitignore`, `.gitattributes`, version pins, dependency lock behavior, and deterministic build metadata.
4. Add headless local scripts for validation, build, test, and export; scripts must fail on the first failed stage.
5. Configure macOS and Windows CI using identical commands to local development.
6. Configure macOS arm64 signing/notarization and Windows x64 signing as optional release stages gated by secrets.
7. Produce and manually launch empty platform smoke builds.

## Edge cases and failure handling

- A missing pinned SDK or export template produces an actionable preflight error before compilation.
- CI without signing secrets still produces unsigned internal artifacts but cannot mark them release-ready.
- A Git LFS pointer without its object fails asset validation rather than entering a build.
- Godot patch upgrades occur only on a dedicated branch and require clean import, tests, exports, and smoke runs.
- Platform-specific plugins must support Windows x64 and macOS arm64 or remain isolated behind optional adapters.

## Performance budget

- Clean CI restore/import/build should complete within 15 minutes per platform before substantial game assets exist.
- Incremental local C# compilation should target less than 10 seconds during M0–M2.
- Empty debug builds should start within 5 seconds on the development Mac and Windows test machine.

## Tests

- Confirm pinned version commands match recorded values.
- Clone into a clean directory and complete restore/build/test/export without manual editor state.
- Launch exported Windows and macOS builds and verify build-manifest display/logging.
- Verify unsigned release attempts fail closed when signing is required.
- Verify a missing LFS object and a broken documentation link fail CI.

## Acceptance criteria

- [x] Pinned Godot 4.6.1 .NET and .NET 10 SDK are documented and machine-checked.
- [x] Repository layout and empty assemblies match the master-plan boundaries.
- [x] Git LFS and ignore rules cover expected generated and binary files.
- [ ] macOS and Windows CI pass from a clean checkout.
- [ ] Windows x64 and macOS arm64 smoke builds launch successfully.
- [ ] A macOS build is signed and notarized; Windows signing has a documented verified path.
- [x] The intended baseline candidate set contains no secrets or machine-local absolute paths.
- [ ] No secrets or machine-local absolute paths are committed in the exact-SHA clean checkout.

### Implementation verification (2026-07-11)

- macOS arm64 headless import, development export, ad-hoc staging, launch, manifest logging, and clean exit pass locally.
- Windows x64 development export passes locally using the pinned templates; native launch remains for Windows CI/test hardware.
- Signed/notarized macOS and signed Windows workflows are implemented and fail closed, but require external credentials and a release tag before they can be marked verified.

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
- Crash-symbol hosting and public release branches, covered by [SP-15](SP-15-steam-release-qa-crash-reporting.md).
