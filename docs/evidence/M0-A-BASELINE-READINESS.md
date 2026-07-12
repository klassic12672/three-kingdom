# M0-A Baseline Readiness Evidence

| Field | Value |
|---|---|
| Evidence date | 2026-07-12 |
| Package | M0-A — auditable baseline readiness |
| Active milestone | M0 — Source of truth and toolchain |
| Target plan | SP-00 — Repository, toolchain, CI, and packaging |
| Host | macOS 26.5.1 (25F80), Apple Silicon arm64 |
| Git state | Unborn `main`; no `HEAD`; no commit SHA exists |
| Confirmed origin | `https://github.com/klassic12672/three-kingdom.git` |
| Authorization boundary | Local edits and verification only; no staging, commit, push, hosted CI, or credentials |
| Result | Ready for user review and separate authorization of the first baseline Git actions |

## Gate and dependency state

M0 remains active and SP-00 remains in progress with no dependencies. SP-01 and SP-02 have local implementations but still lack same-revision macOS/Windows evidence and cannot satisfy their dependency gates. SP-03 implementation exists locally ahead of its M2 gate; SP-01/SP-02 remain unverified, and rendering, map-mode presentation, interaction, and visual acceptance remain unchecked.

This report is pre-commit evidence. It is not clean-checkout, same-SHA, hosted, native-Windows, signed, notarized, or release-ready evidence.

## Contract and documentation reconciliation

- Build-manifest schema 2 preserves `contentManifestChecksum` as the canonical top-level built-in pack checksum and adds `contentRegistryChecksum` for the aggregate validated load order.
- The canonical top-level pack checksum is `6c527133073ffece29d4d75f7372cc783f2855f6354ed5be9eb1a6c971936449`.
- The aggregate registry checksum is `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d`.
- Save documentation now matches implemented schema 3 and the tested 1→2→3 migration chain.
- The roadmap now records explicitly authorized baseline Git actions as the next work before hosted same-SHA CI.
- SP-03 status distinguishes local implementation from its blocked milestone/dependency state. Unsupported rendering and map-mode presentation criteria are unchecked.
- The project-architect review found no ADR trigger. An ADR is required if a later change removes or redefines `contentManifestChecksum` or changes a locked master-plan contract or gate.

## Intended first-commit inventory

The intended baseline is exactly every path returned by:

```bash
git ls-files --cached --others --exclude-standard
```

After adding this report, that set contains 195 files. There are no exceptions within the returned set. The top-level inventory is:

| Path | Files | Purpose |
|---|---:|---|
| `.codex/` | 7 | Project-scoped agent configuration |
| `.editorconfig` | 1 | Shared editor formatting |
| `.gitattributes` | 1 | Text normalization and Git LFS rules |
| `.github/` | 2 | CI and signed-release workflow definitions |
| `.gitignore` | 1 | Generated, local, and credential exclusions |
| `.vscode/` | 3 | Shared editor recommendations, settings, and tasks |
| `AGENTS.md` | 1 | Repository operating guidance |
| `Directory.Build.props` | 1 | Shared .NET build configuration |
| `Directory.Packages.props` | 1 | Central package versions |
| `NuGet.Config` | 1 | Package source configuration |
| `README.md` | 1 | Repository entry point |
| `ThreeKingdom.slnx` | 1 | Repository solution |
| `artifacts/` | 1 | Tracked `.gitkeep` only |
| `build/` | 3 | Version and toolchain pins plus guidance |
| `data/` | 18 | Authored content, schemas, localization, research, and provenance |
| `docs/` | 40 | Master plan, roadmap, plans, guides, prompts, and this evidence report |
| `game/` | 18 | Godot project, presentation source, scene, presets, and placeholders |
| `global.json` | 1 | .NET SDK pin |
| `scripts/` | 23 | Validation, build, import, export, smoke, packaging, and signing entry points |
| `src/` | 37 | Simulation, application, content, and platform assemblies |
| `tests/` | 23 | Repository, simulation, and content tests |
| `tools/` | 10 | Content-pipeline and simulation operator tools |

The only candidate files reported by MIME inspection as `application/octet-stream` are eight one-byte, newline-only `.gitkeep` placeholders. No unexplained binary source is included.

## Excluded local and generated material

The following remain in the working directory but are ignored and are not part of the intended baseline:

- `.DS_Store` files;
- `artifacts/*` except `artifacts/.gitkeep`, including approximately 603 MB of exports, derived templates, normalized content, and local reports;
- `game/.godot/`, approximately 274 MB of editor/import state;
- `game/generated/*.json`, including generated geography and build manifests;
- all `bin/`, `obj/`, and test result directories;
- local logs, temporary files, editor state, environment files, signing material, and credential paths covered by `.gitignore`.

Repository validation enumerates cached files plus nonignored untracked files with NUL-safe Git output. Ignored untracked outputs are excluded, while cached files remain auditable even if they match an ignore rule.

## Security, path, and LFS audit

The integrated validator passed against the real candidate tree and found:

- no credential or signing-material filenames;
- no high-confidence private-key, GitHub, AWS, Google, Stripe, or Slack token signatures;
- no macOS-user, Linux-home, Windows-user, or local-file-URI machine-local absolute paths;
- all required Git LFS patterns present;
- no malformed, missing, size-mismatched, or checksum-mismatched working-tree LFS pointer objects.

Because `HEAD` does not exist, history-dependent `git lfs fsck` is correctly deferred. Pre-HEAD validation requires each pointer's OID and size, checks object byte count, and recomputes SHA-256. Tests also prove that `git lfs fsck` becomes mandatory and reports corruption once a committed `HEAD` exists.

## Automated and local platform verification

All commands ran from the repository root on the host above.

| Command | Result | Key evidence | Elapsed |
|---|---|---|---:|
| `dotnet test tests/Repository.Tests/Repository.Tests.csproj -c Release --filter FullyQualifiedName~BuildManifestTests` | Pass | 2/2 tests | 3.28 s |
| `dotnet test tests/Repository.Tests/Repository.Tests.csproj -c Release --filter FullyQualifiedName~RepositoryValidatorTests` | Pass | 14/14 tests, including pre-HEAD corruption and size validation | 6.20 s |
| `dotnet test tests/Simulation.Core.Tests/Simulation.Core.Tests.csproj -c Release --filter FullyQualifiedName~SaveStoreTests` | Pass | 11/11 tests | 1.88 s |
| `./scripts/preflight.sh --require-templates` | Pass | .NET 10.0.301, Godot 4.6.1 .NET, matching templates, Git LFS | 0.44 s |
| `./scripts/validate.sh` | Pass | Corrected tree: 32 records, 70 translations, registry checksum `e937297a…672d` | 4.61 s |
| `./scripts/test.sh Release` | Pass | Corrected tree: build 0 warnings/errors; tests 31 content + 52 simulation + 17 repository = 100 | 10.21 s |
| `./scripts/import.sh` | Pass | Godot headless import completed | 3.57 s |
| `./scripts/export.sh macos development` | Pass | macOS arm64 development app and schema-2 sidecar manifest | 19.84 s |
| `./scripts/export.sh windows development` | Pass | Windows x64 development export and schema-2 sidecar manifest | 18.59 s |
| `./scripts/smoke.sh macos` | Pass | Native arm64 headless launch, manifest log, geography checksum, clean exit | 2.39 s |

An initial focused BuildManifest test run failed because the implementation assumed loaded manifest paths were absolute. `ContentPackLoader` intentionally retains portable diagnostic paths. Selection was corrected to the normalized `content-manifest.json` path. Final-tree validation also rejected machine-local path syntax in an early draft of this report; the report was corrected before the final validation and Release runs above. No gate, checksum, or validation expectation was weakened.

The closeout review later found that pre-HEAD LFS validation checked only pointer syntax and object existence. The validator now requires pointer size, verifies object byte count, recomputes SHA-256, and has explicit unborn-HEAD tests for corrupted objects, size mismatch, and missing or malformed size fields. This report was returned to a non-ready state until the corrected focused and full gates passed.

## Local artifact identities

These artifacts are ignored development outputs and remain non-release evidence because their manifest Git field is `uncommitted`.

| Artifact | Architecture | SHA-256 |
|---|---|---|
| `artifacts/exports/macos-arm64-development/build-manifest.json` | manifest | `5f890c5c5e7bdf4dc78ebcac92208cdc0f37ea68b412255b87a3ab3c555af0b9` |
| macOS app executable | Mach-O arm64 | `cb84927715e50725c9bb24babacfc25fce33b1c46b0db3e1c005f47e96c25c9e` |
| `artifacts/exports/windows-x64-development/build-manifest.json` | manifest | `1c14c73df081d00675ff8d392628d66b13fe3536e9a45783530d8102f9e8b50c` |
| `artifacts/exports/windows-x64-development/ThreeKingdom.exe` | PE32+ x86-64 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| `artifacts/exports/windows-x64-development/ThreeKingdom.pck` | Godot pack | `405285d2c4f9e798ca072b2d95b924f9f0624e83639b59cb8f9ec0e31bacf4e2` |

Both sidecar manifests use schema 2, report project version `0.1.0`, contain the two expected content checksums, and explicitly report `gitCommit: uncommitted`. The Windows executable was cross-exported on macOS and was not launched on Windows hardware.

## Unavailable evidence and unchanged acceptance state

| Evidence | State | Reason / next action |
|---|---|---|
| Commit SHA and clean checkout | Unavailable | No `HEAD`; requires explicit staging/commit authorization |
| Hosted macOS/Windows CI | Unavailable | Requires pushing an authorized exact SHA |
| SP-01 cross-platform golden checksum | Unavailable | Requires same-SHA hosted jobs |
| SP-02 cross-platform registry/load-order result | Unavailable | Requires same-SHA hosted jobs |
| Native Windows smoke | Unavailable | Cross-export is not a Windows launch |
| Signed/notarized macOS package | Unavailable | Requires separate credentials and authorization |
| Windows Authenticode/timestamp verification | Unavailable | Requires Windows signing credentials and authorization |
| SP-03 rendering/interaction evidence | Unavailable | Headless M0 smoke is not visual acceptance |
| Hosted 15-minute CI budget | Unavailable | No hosted run exists |

No M0, SP-00, SP-01, SP-02, or SP-03 external acceptance criterion was newly checked by this package.

## Next authorized action

The confirmed remote is recorded, but no Git mutation beyond local file edits was authorized. The next package requires explicit authorization to:

1. review and stage exactly the 195-file candidate set defined above;
2. create the first baseline commit and record its SHA;
3. push that exact SHA to the confirmed origin;
4. collect unsigned same-SHA macOS and Windows hosted CI evidence.

Signing, notarization, publishing, credentials, and native Windows hardware remain separate authorization boundaries.
