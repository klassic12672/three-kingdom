# SP-04F3 Exact-SHA Hosted Evidence — `72e5bd3`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F3 — exact household-head death handoff |
| Exact commit | `72e5bd34f41f068c2e07a580e02522f8222eca30` |
| Parent commit | `524937a6b91ca1ae1518b5724b9358ed7db840ed` |
| Commit tree | `4dee2dada71872e8db0b1e2ac758460cc5d017f9` |
| Commit subject | `feat: hand off household heads on death` |
| Parent-to-commit diff SHA-256 | `e587cdbac7a987b0fa9771d6170452fb4bc4f21800829d949aa8b9b64d3fcf45` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29466701169](https://github.com/klassic12672/three-kingdom/actions/runs/29466701169), attempt 1 |
| Overall result | **Pass — SP-04F3 criterion F315 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F3 package. It covers the reserved-system exact household-head death action; mandatory composite death-v3 plus head-change-v1 evidence; caller-supplied distinct, born, living same-household replacement validation; unchanged ordinary household-head death blocker; exact membership, wealth, and estate preservation; composition with F0–F2 custody, marriage, guardianship, pregnancy, and career death behavior; later-candidate rollback; deterministic replacement-death, replacement-expulsion, competing-handoff, and independent-household races; current and later-day replay; save schema 24; authenticated vocabulary-only schema-23-to-24 migration from exact F2 history; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish automatic successor selection, legal heir designation, claims, precedence, household vacancy or dissolution, inheritance, wealth or estate transfer, regency, offices or titles, disputed succession, retinue succession, player-character transfer, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. The supplied replacement remains an exact household-pointer handoff, not an inferred legal heir. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains the new action/outcome/head-change vocabulary, one atomic character candidate for target death plus F2 custody releases and the head pointer, unchanged downstream lifecycle planners, schema-24 persistence and compatibility validation, the exact-F2 schema-23 fixture, focused campaign/schema/performance tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 904/73/6/18 suites passed before commit. Independent architecture, schema, and verification re-reviews found no remaining local blocker; verification separately reran 50/50 F3-focused tests and 904/904 complete Simulation.Core tests.
- The frozen 15,227-byte schema-23 fixture has file SHA-256 `e2712c9a95618867d2543f57aeedc76f2a5b1843ecaa6b78e1072a5dd6b58588` and stored historical checksum `1f40e30dbaec836ac8258efbb4cf610a40968548bd2deb30abb621eb91314299`. A temporary public-workflow test harness compiled in a detached worktree at exact accepted F2 implementation `ab8917a95ea064911a584cd640647374745fd2c7` generated it; that generator and worktree were removed.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `524937a` to `72e5bd3`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `72e5bd34f41f068c2e07a580e02522f8222eca30`.

## Hosted run and jobs

Run 29466701169 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T02:29:19Z` through `2026-07-16T02:36:30Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87521260074](https://github.com/klassic12672/three-kingdom/actions/runs/29466701169/job/87521260074) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `02:29:23Z` / `02:34:05Z` | Pass |
| Windows x64 | [87521260063](https://github.com/klassic12672/three-kingdom/actions/runs/29466701169/job/87521260063) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1` | `02:29:27Z` / `02:36:29Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 904 passed | 904 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 32.648 ms | Pass; `MAP_MODE_TIMING` 57.608 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/200-household-head-death measurement remains a non-threshold observation; CI executes that test but does not print or assert its observed component timings or checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29466701169/artifacts/8363280714) | 8363280714 | `04791f6653598dd463dffc1e28d202390aa65c11e896ecb6a05c49fd4ea7effc` | 68,062,555 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29466701169/artifacts/8363312960) | 8363312960 | `b9ff0d0f303dec01a5b746e4aac7adb17f2e09d9ea1988125eac1b2f693d671d` | 73,381,457 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `408368a4a8f1b860ddc30fa30620131d5cff2aeac35e5a0025c8ce9eced60972` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `018ecbea5dc0fa3205d53142f835f83f69a3fbd1578e2fc971f40c2a38fd296e` |
| macOS PCK | 1,624,060 | `6c492823c67d4bfebd71572cf1a5555589bb7b9bca79965979c6048726e12fb2` |
| macOS `Simulation.Core.dll` | 1,297,408 | `c2b65a53307af2ab968887e0a0148b967dce36ddee900cab6ce07d6b8b5c50fc` |
| Windows `build-manifest.json` | 503 | `ef1b0695213ced38c3e374fe229ffe569d1f15bbb90cfa3d6f022f2d48509a51` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `99e93b9e08cfa6810bc8ee5a69b8e24cb80507b1d98613032e99f5ed8890b5e6` |
| Windows `Simulation.Core.dll` | 1,297,408 | `1a6cde2e5f0c980fda04558937cc14b81d4b2e537d5a13a604002050e4eab236` |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact integrity.

## Decision

SP-04F3 criterion F315 passes at exact SHA `72e5bd34f41f068c2e07a580e02522f8222eca30`. F3 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/200-household-head-death fixture remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered SP-04 package may begin.
