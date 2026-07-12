# M0 Exact-SHA Evidence — `1ab375a`

| Field | Value |
|---|---|
| Evidence date | 2026-07-12 |
| Target milestone | M0 — Source of truth and toolchain |
| Target plan | SP-00 — Repository, toolchain, CI, and packaging |
| Exact commit | `1ab375a5e812c14eba4eca4cc121e604ade73f47` |
| Approved origin | `https://github.com/klassic12672/three-kingdom.git` |
| Hosted run | [CI run 29179730891](https://github.com/klassic12672/three-kingdom/actions/runs/29179730891) |
| Overall result | **Incomplete — macOS passed; Windows failed before import, export, smoke, and artifact upload** |

> Policy addendum (2026-07-12): [ADR-0001](../adr/0001-mac-first-development-deferred-physical-windows-verification.md) was accepted after this evidence was collected. Physical Windows and production-signing evidence remain unavailable, but they are now M4/SP-15 gates rather than M0 blockers. The failed hosted Windows job remains the M0 blocker recorded by this report.

> Forward link: cleanup-fix SHA `7f62a97cf880ae6ded8e47af8737a11e53479977` has its own [passing exact-SHA report](M0-EXACT-SHA-7f62a97.md). Its results do not change this revision's historical failure.

## Scope and authorization

This report records only evidence produced from or retrieved for the exact commit above. Existing ignored exports whose manifest reported `gitCommit: uncommitted` were not used.

The user authorized retrieval or generation of unsigned same-SHA CI evidence and confirmed the origin. Signing, notarization, Authenticode, publishing, and credential use were not authorized. The signed release workflow was not run. One attempt to rerun the failed hosted job through the connected GitHub integration returned `403 Resource not accessible by integration`; no credential helper, browser session, token, or secret was used to bypass that boundary.

## Prerequisites

| Check | Result | Evidence |
|---|---|---|
| `HEAD` exists and matches the requested SHA | Pass | `git rev-parse HEAD` returned the exact commit above. |
| Origin is user-approved | Pass | User confirmed the approved URL above; the fresh clone used that URL. |
| Working tree was clean before collection | Pass | `git status --porcelain=v1 --untracked-files=all` returned no paths. |
| LFS objects are complete | Pass | Origin clone completed `git lfs pull` and `git lfs fsck`; the repository currently contains no tracked LFS objects. |
| No credential/signing material or machine-local paths are tracked | Pass | Exact-SHA repository validation passed in the origin clone and hosted macOS job; tracked-path review found no prohibited material. |

## Stage 1 — fresh clone

Host: macOS 26.5.1 build 25F80, Apple Silicon arm64.

Commands:

```bash
GIT_LFS_SKIP_SMUDGE=1 git clone --no-checkout https://github.com/klassic12672/three-kingdom.git "$CLONE"
GIT_LFS_SKIP_SMUDGE=1 git -C "$CLONE" checkout --detach 1ab375a5e812c14eba4eca4cc121e604ade73f47
git -C "$CLONE" lfs pull
git -C "$CLONE" lfs fsck
./scripts/preflight.sh --require-templates
./scripts/test.sh Release
./scripts/import.sh
```

| Evidence | Result | Key output / SHA-256 |
|---|---|---|
| Detached exact SHA and clean tree | Pass | Exact commit matched before and after the run; only ignored generated output was produced. |
| LFS pull and fsck | Pass | `Git LFS fsck OK`; zero tracked LFS objects. |
| Pinned preflight | Pass | .NET `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching templates, Git LFS available; log `ce15bdc64c52f4065de60ef0a3107b5263e636ec77256ba0211e87110ea0a2ea`. |
| Release build and tests | Pass | Build: 0 warnings, 0 errors. Tests: 31 content + 52 simulation + 17 repository = 100 passed; log `4c1dca762f2aad922066b59fb843790d21c3c2f79e5af57388658c548369c1f8`. |
| Godot import | Pass | Headless import completed; log `2e75d463d84df7df1657848a7860922f259c306cea9c134b2847d43a99f3c3f4`. |

An earlier isolated clone under the non-canonical macOS `/tmp` alias failed during NuGet restore because the same project appeared through both logical path spellings. A new origin clone under the canonical temporary directory completed without changing scripts, dependencies, checksums, or expectations. The successful origin-clone result above is the authoritative stage result.

## Stage 2 — hosted CI

Run: [CI run 29179730891](https://github.com/klassic12672/three-kingdom/actions/runs/29179730891), event `push`, attempt 1, exact head SHA `1ab375a5e812c14eba4eca4cc121e604ade73f47`, started `2026-07-12T04:27:25Z`.

### macOS arm64 job

| Field | Value |
|---|---|
| Job | [86615145450](https://github.com/klassic12672/three-kingdom/actions/runs/29179730891/job/86615145450) |
| Runner | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; arm64 assertion passed |
| Commands | `./scripts/test.sh Release`; `./scripts/import.sh`; `./scripts/export.sh macos development && ./scripts/smoke.sh macos` |
| Result | **Pass** |
| Key output | 100 tests passed; registry checksum `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d`; import, export, manifest log, geography checksum, and clean smoke exit passed |

### Windows x64 job

| Field | Value |
|---|---|
| Job | [86615145454](https://github.com/klassic12672/three-kingdom/actions/runs/29179730891/job/86615145454) |
| Runner | Microsoft Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1`; x64 workflow target |
| Command reached | `./scripts/test.ps1 -Configuration Release` |
| Result | **Fail** |
| Key output | Content validation and build passed. Game.Content.Tests: 31/31 passed. Simulation.Core.Tests: 52/52 passed. Repository.Tests: 14/17 passed. |
| Failure | Three repository tests threw `UnauthorizedAccessException` while `TemporaryRepository.Dispose()` recursively removed temporary Git object directories. |
| Skipped after failure | Godot import, Windows export, native smoke, artifact upload |
| Next action | Cleanup fix `7f62a97cf880ae6ded8e47af8737a11e53479977` now exists; run the full workflow for that exact SHA in a separate evidence package. This failed revision cannot satisfy the SP-00 hosted CI gate. |

The hosted CI stage is **fail** because both jobs must pass. No script or expectation was weakened, and no Windows artifact identity is claimed.

## Stage 3 — SP-01 deterministic checksum

The exact-SHA `Simulation.Core.Tests` assembly passed all 52 tests on both hosted jobs. That suite includes `TenYearThousandEntitySoak_CompletesWithoutInvariantFailure`, whose fixed command contract is equivalent to:

```bash
dotnet run --project tools/Tools.Simulation --configuration Release --no-build -- soak --years 10 --entities 1000 --seed 20260712
```

| Platform | Hosted evidence | Result | Checksum |
|---|---|---|---|
| macOS arm64 | Job 86615145450, 52/52 simulation tests | Pass | `105da5fd449cc2d00ba1bf979642b22107db5b236eab30baac437f1b9b8bf088` |
| Windows x64 | Job 86615145454, 52/52 simulation tests | Pass | `105da5fd449cc2d00ba1bf979642b22107db5b236eab30baac437f1b9b8bf088` |

An explicit exact-SHA local operator run also reported 1,044 turns, final date `0200-01-03`, the same checksum, and log SHA-256 `87f8c3ac3858a83c625cd6fe355c2c79957d1707de095f0f5cb523262d3cdd77`.

Stage result: **pass on both hosted platforms for the same SHA**. This does not make SP-00 or M0 complete.

## Stage 4 — SP-02 registry and load order

Both hosted jobs printed the same validated registry checksum before tests, and both passed all 31 Game.Content.Tests. The suite covers the golden registry checksum, reversed manifest discovery, built-in-first ordering, dependency-ordered overrides, ambiguous override rejection, and portable manifest paths.

Hosted command path:

```text
./scripts/test.sh Release
./scripts/test.ps1 -Configuration Release
  -> content validation
  -> dotnet test ThreeKingdom.slnx --configuration Release --no-build --no-restore
```

| Platform | Hosted evidence | Result | Registry / diagnostics |
|---|---|---|---|
| macOS arm64 | Job 86615145450, 31/31 content tests | Pass | `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d`; zero diagnostics |
| Windows x64 | Job 86615145454, 31/31 content tests | Pass | `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d`; zero diagnostics |

At this exact revision the validation command calls `WriteDiagnostics` before printing the valid registry summary. Neither hosted job log contains a content warning or error diagnostic, so the hosted diagnostic count is zero on both platforms. The canonical top-level content-manifest checksum is `6c527133073ffece29d4d75f7372cc783f2855f6354ed5be9eb1a6c971936449`.

Explicit exact-SHA local evidence additionally reported `errors=0 warnings=0`, with validation log SHA-256 `90a557257e87bb1aa39cd7d87a43737819e2d3ce1b8910616a84cd308b629e3e`; the ten focused pack-resolution tests passed with log SHA-256 `ce94ae96e1aa07d4ccf0a00d8fcc2f6a19f71545b908520ebfea72f8d25b39a5`.

Stage result: **pass on both hosted platforms for the same SHA**. SP-02 remains dependency-blocked by SP-00 even though this criterion is verified.

## Stage 5 — native smoke and artifacts

### Hosted macOS artifact

| Field | Value |
|---|---|
| Artifact | `macos-arm64-development-unsigned` |
| Artifact ID / URL | [8256039647](https://github.com/klassic12672/three-kingdom/actions/runs/29179730891/artifacts/8256039647) |
| Size | 67,528,492 bytes |
| Uploaded ZIP SHA-256 | `1b84516c3294e77ab49b151f915e5b36edac6eaf329c2ab9efdfb089e677b200` |
| Sidecar manifest SHA-256 | `0bc7bbc9775326ffdc4f5deb058fcaaa23a1ceba8bea7696181e0ba7ec142150` |
| Canonical manifest SHA-256 | `14be334362d38009f4f5f3082e61a4fa79456ea8c55e5d0fd62111b71c9a2992` |
| Hosted executable SHA-256 | `5e40762a1ef4bdb1fb22c26236e906508020d3bc35378345e9aa0582e7076ed6` |
| Architecture | Mach-O arm64 |
| Manifest identity | Schema 2; exact Git SHA; Godot and .NET pins; platform `macos`; architecture `arm64`; both expected content checksums |
| Embedded versus sidecar | Pass; canonical JSON bytes match and hash to the value above |
| Required markers | `BUILD_MANIFEST`, matching `GEOGRAPHY_CHECKSUM`, and successful clean-exit marker present |
| Result | **Pass — unsigned development/native smoke evidence only** |

The GitHub artifact digest matched the freshly downloaded ZIP. The downloaded hosted artifact was separately staged, ad-hoc signed for local execution only, launched natively on Apple Silicon, and reproduced the embedded manifest and geography markers with clean exit. Ad-hoc staging is not Developer ID signing evidence.

### Windows artifact and native smoke

Result: **unavailable**. The Windows hosted job failed before export and artifact upload, so there is no same-SHA hosted Windows artifact, embedded/sidecar comparison, or native smoke log.

A fresh macOS-hosted cross-export produced a PE32+ x86-64 development executable and exact-SHA sidecar, but it is not native Windows evidence. Its local-only identities are:

- cross-export ZIP: `c3d63195b12de799790fe296d6770564f9f659cbce015b5fb3cba24d8af25a72`;
- sidecar manifest: `a8382dc588e91322cc1b8b4582069b16de9ebeddd7527bd4ab2480c4f2f89a31`;
- executable: `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235`;
- PCK: `7073737461a85d334bdfb157d2358c2ae40b58e3e6b0460e22cd9ae4b5291ead`.

These values prove only exact-SHA cross-export structure and are not used to check the native Windows criterion.

## Stage 6 — release signing

| Gate | Result | Reason / next action |
|---|---|---|
| macOS Developer ID signing | Unavailable | Credentials and signing authorization were not supplied. |
| Apple notarization and stapling | Unavailable | Credentials and notarization authorization were not supplied. |
| Windows Authenticode and RFC 3161 timestamp | Unavailable | Certificate, timestamp authorization, and credential use were not supplied. |
| Publishing | Unavailable | Explicitly unauthorized. |

No signing gate was bypassed and no release package was produced.

## Acceptance and status decisions

- M0 remains **Active**.
- SP-00 remains **In progress** because the Windows hosted job and its export/automated-smoke artifact are incomplete.
- The SP-00 exact-SHA clean-checkout credential/path criterion is verified.
- The SP-01 same-revision Windows/macOS golden checksum criterion is verified.
- The SP-02 same-revision Windows/macOS registry/load-order criterion is verified.
- SP-01 and SP-02 remain dependency-blocked by SP-00; these results do not promote M1 or unblock SP-03.
- No physical Windows, signed, notarized, Authenticode, release-ready, publishing, or milestone-complete claim is justified; ADR-0001 defers those platform/release claims without treating them as M0 failures.

## Smallest next action

Exact SHA `1ab375a5e812c14eba4eca4cc121e604ade73f47` permanently records the failed Windows job and cannot close SP-00. Cleanup fix `7f62a97cf880ae6ded8e47af8737a11e53479977` now exists; collect a complete macOS/Windows hosted CI and automated-smoke artifact set for that revision in a separate exact-SHA evidence package. Physical Windows and signing remain later M4/SP-15 authorization and credential gates.
