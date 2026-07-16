# SP-04F2 Exact-SHA Hosted Evidence — `ab8917a`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F2 — custodian-death custody release |
| Exact commit | `ab8917a95ea064911a584cd640647374745fd2c7` |
| Parent commit | `8f23cc4b06990c3c6aa415f59d52d6e166dbb080` |
| Commit tree | `725cd862c84b619bf44cb37b5fd3bcbdcadc6ccc` |
| Commit subject | `feat: release custody on SP-04F2 custodian death` |
| Parent-to-commit diff SHA-256 | `4dc5cbe928aac432034f9b9039c1d05e1035bdb8604dba3fa4e0e1aec0a7d2cc` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29463067007](https://github.com/klassic12672/three-kingdom/actions/runs/29463067007), attempt 1 |
| Overall result | **Pass — SP-04F2 criterion F218 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F2 package. It covers non-household-head custodian death; deterministic release of every other living Detained, Captive, or Hostage dependent to Free/no custodian; canonical defensive death-v3 release evidence using condition-change v1; target and unrelated-custody preservation; five-plan aggregate validation and fixed commit order; affected-ID and exact-replan tamper rejection; priority, submission-order, simultaneous-death, dependent-mutation, and later-day replay races; save schema 23; authenticated structural schema-22-to-23 migration from the exact F1 source; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish custody reassignment, household-head replacement, household or family movement, inheritance, heir designation, claims, regency, offices or titles, disputed succession, retinue succession, player-character transfer, cause-of-death taxonomy, automatic mortality, relationship or memory effects, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains death-change v3, the canonical `ReleasedCustodyChanges` evidence list, one aggregate character update for target death plus dependent releases, unchanged downstream marriage/guardianship/pregnancy/career planning and commit order, schema-23 persistence/checksums, the exact-F1 schema-22 fixture, custody/death/campaign/schema tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 854/73/6/18 suites passed before commit. Independent architecture, simulation, schema/content, and verification reviews found no remaining local blocker; verification separately reran 24/24 F2-focused tests, 87/87 death/migration tests, and 854/854 complete Simulation.Core tests.
- The frozen 42,125-byte schema-22 fixture has file SHA-256 `13b11725a2705bdb2e3da36552a679ef2a272edc7a4ea10879d97a85d0c84d79` and stored historical checksum `002d05e1a0f467b2c92d24d4a8ad55f4ab324467955a7f524b33a6899e4d2705`. A temporary test-only public-workflow/`SaveStore` generator compiled in a detached worktree at exact accepted F1 implementation `23045a06a39361ecf8d2ef341cc0458590322f0a` generated it; that temporary generator was not retained.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `8f23cc4` to `ab8917a`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `ab8917a95ea064911a584cd640647374745fd2c7`.

## Hosted run and jobs

Run 29463067007 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T01:01:53Z` through `2026-07-16T01:08:21Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87510437865](https://github.com/klassic12672/three-kingdom/actions/runs/29463067007/job/87510437865) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `01:01:57Z` / `01:08:21Z` | Pass |
| Windows x64 | [87510437820](https://github.com/klassic12672/three-kingdom/actions/runs/29463067007/job/87510437820) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `01:02:06Z` / `01:08:06Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 854 passed | 854 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 37.383 ms | Pass; `MAP_MODE_TIMING` 51.454 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/200-death/600-release measurement remains a non-threshold observation; CI executes that test but does not print or assert its observed component timings or checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29463067007/artifacts/8361995626) | 8361995626 | `c6a55bd35923db4ec1dcbbf1b9130234eb6259221f37b2221c6078c99c12f08b` | 68,058,069 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29463067007/artifacts/8361991273) | 8361991273 | `cb801bf4555fb8839ea637533bd2574d731eaa4c4d3a5927c7f873ba37997218` | 73,376,955 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `15dc1943ce115714c3572499801041b4ebb6f029f04c809ec7e91fec4f9ae512` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `25ca6c5cc0fc703200135b01896b9ef179f43f3fd4de054709a497753bb78372` |
| macOS PCK | 1,624,060 | `9b927b831addad5e9eb39c61df80fa010ea9c9620536ca7b8b105ee7bd3dd18b` |
| macOS `Simulation.Core.dll` | 1,281,024 | `ff79e19a19fea67b4c2edf9a17a9852ac0f328b260b8dce03f44110e6063e593` |
| Windows `build-manifest.json` | 503 | `dc431b02330478f757dae90c05a3fd68945015cd910da9cc465cfaa292ff92f8` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `99334c9ab19462a3c6f83b350cac84eb2606043994f93baf5cdb4eda515af8e7` |
| Windows `Simulation.Core.dll` | 1,281,024 | `f3f8504dfa30c48db0587d98b380f7148a339f952af06123b50047931be6de74` |

The workflow emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. The macOS exporter also emitted the known non-failing Godot Android-SDK `EditorSettings` diagnostic while completing a successful native macOS export and smoke launch. Neither affected job conclusions or artifact integrity.

## Decision

SP-04F2 criterion F218 passes at exact SHA `ab8917a95ea064911a584cd640647374745fd2c7`. F2 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/200-death/600-release fixture remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered SP-04 package may begin.
