# SP-04E2 Exact-SHA Hosted Evidence — `7491da8`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04E2 — primary-guardianship termination and replacement |
| Exact commit | `7491da89985fedb18e423082a2fd9187b8899e52` |
| Parent commit | `e3097dce2a313fc26dea81054f00d9dd4d751df8` |
| Commit tree | `b510c1065ef4ce15930dfddc83b7cac1b3d80b22` |
| Commit subject | `feat: add SP-04E2 guardianship lifecycle` |
| Parent-to-commit diff SHA-256 | `e206b98b9f60a6175ca5b5ab22e03750b2f096f6d04548c8e2327eced2fa9471` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29441422674](https://github.com/klassic12672/three-kingdom/actions/runs/29441422674), attempt 1 |
| Overall result | **Pass — SP-04E2 criterion E214 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified E2 package. It covers reserved-system primary-guardianship ending and atomic replacement; exact active-record, participant, condition, retained-capacity, collision, affected-ID, and apply-time revalidation; deterministic end/end, end/replace, and replace/replace races; complete rollback; pending replay; unchanged non-guardianship subsystems; current schema-16 saves; authenticated schema-15-to-16 migration from exact E1 source; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and static artifact inspection.

It does not establish automatic birthday/death termination, guardian authority over education/residence/custody/inheritance, character- or household-issued authority or consent, co-guardians, adult regency, pregnancy, birth, education, coming of age, public death, inheritance, succession, claims, content, localization, UI, AI, battle, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second turn budget. Every full SP-04 acceptance criterion remains unchecked.

## Candidate and remote identity

- The audited E2 commit contains the guardianship end/replace actions and outcomes, atomic lifecycle integration, schema-16 vocabulary compatibility, exact-E1 schema-15 fixture, focused/campaign/save tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The complete 602/71/6/18 suites, zero-warning Release build, focused 301-test family/guardianship/save slice, touched-file formatting, diff check, and LFS check passed before commit.
- The frozen 61,267-byte schema-15 fixture has file SHA-256 `51a6ecf8e0556af35cbe4010645925d50bc965ca4a2d07ea05c8c9c486538fc3` and stored historical checksum `ed398b879de95b9065bffb367fb3a7b830c40d6a8e1f29d3a0a64ffb414e5b86`. A detached harness compiled against exact accepted E1 production source `97b607ae8df77dbd5c6fa5ab6b544000208cdb0e` generated it.
- Independent architecture and verification review selected the bounded guardianship-lifecycle seam. Verification found an exact-JSON evidence gap; literal discriminator/property and legacy replacement-rejection coverage remediated it. The final reviewer reran 46 narrow tests and the complete 602-test Core suite and found no remaining correctness or package-boundary blocker.
- One local wrapper invocation failed before build or tests when `dotnet restore` terminated with a segmentation fault. No source changed, and the immediate unchanged-tree rerun passed the complete validation/build/test gates. The failed attempt remains failure evidence and is not relabeled as passing.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `e3097dc` to `7491da8`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `7491da89985fedb18e423082a2fd9187b8899e52`.

## Hosted run and jobs

Run 29441422674 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T18:39:21Z` through `2026-07-15T18:44:45Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87441114380](https://github.com/klassic12672/three-kingdom/actions/runs/29441422674/job/87441114380) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `18:39:24Z` / `18:42:47Z` | Pass |
| Windows x64 | [87441114405](https://github.com/klassic12672/three-kingdom/actions/runs/29441422674/job/87441114405) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `18:39:24Z` / `18:44:44Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true`, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 602 passed | 602 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 31.471 ms | Pass; `MAP_MODE_TIMING` 53.076 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source ten-year/1,000-entity soak assertion plus both hosted 602-test Core passes establish checksum `95d559c0ebcf51f854ad563a12c00a4ab49a68c38c69fa6508c523e9a7b83e1d`. The local 1,000-character/64-starting-guardianship component measurement remains a non-threshold local observation; CI does not print or assert its observed checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29441422674/artifacts/8353668235) | 8353668235 | `7e26f7b71eb5e07e881a9c7cfb19c2c99970713124a69792f1029e84ec604c90` | 68,004,470 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29441422674/artifacts/8353719816) | 8353719816 | `392610c33b3b52c53ab4f2286bf3359247dff6fd15b0efdfdde7a3ee1d7e708f` | 73,323,368 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `8c59ca09cdae1235ce9f3ed9136528a357618fa9108de0e8b52337408b88df6b` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `8b2063d608511978adef05b3545fb94be478a20e7ec571ab0f3c3e665c2e7e31` |
| macOS PCK | 1,624,060 | `041e4c01d34cab70cad2173c71bafa7762ab2ecfc54e9cfb1a023a86cc88b93e` |
| macOS `Simulation.Core.dll` | 1,133,056 | `2d2c83f4fead907a0c72dc5819ec898e6b163ce638f9b1368411eb839c7571c9` |
| Windows `build-manifest.json` | 503 | `ebfe7745c42148eb8e915eda2b83d2a261dfd030a78b48fd104a3192b0b11320` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `85d3363b6787f16c686c3949d8c6c892dd0f340cccc72158a456e3fdcdfb3096` |
| Windows `Simulation.Core.dll` | 1,133,056 | `ef347ae193c5645a301c27161c810b8c6f36645584a9f8064d9b01cb6f2b9e7f` |

The workflow emitted non-failing Node.js 20 deprecation warnings for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. They did not affect job conclusions or artifact integrity.

## Decision

SP-04E2 criterion E214 passes at exact SHA `7491da89985fedb18e423082a2fd9187b8899e52`. E2 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/64-starting-guardianship fixture remains raw component evidence and does not establish the full-SP-04 three-second turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered SP-04 package may begin.
