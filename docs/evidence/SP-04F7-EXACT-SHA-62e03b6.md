# SP-04F7 Exact-SHA Hosted Evidence — `62e03b6`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F7 — neutral personal succession claims |
| Exact commit | `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4` |
| Parent commit | `5cbfd64688769195b4108b9b28c49de99a4af096` |
| Commit tree | `6c8b4ef454b2b6c9abde012369a8145a0f8fe637` |
| Commit subject | `feat(simulation): add bounded personal succession claims` |
| Parent-to-commit diff SHA-256 | `a9847af683cf7f78f360f3e4ad2c0f8f235be980d79e6d8154dab201fff73632` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29480235359](https://github.com/klassic12672/three-kingdom/actions/runs/29480235359), attempt 1 |
| Overall result | **Pass — SP-04F7 criterion F716 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F7 package. It covers character-issued personal succession-claim assertion and exact withdrawal; one active claim per ordered subject/claimant pair; claimant agency and subject-condition rules; stable command, event, and claim identity; deterministic duplicate/stale races and replay; independence from designation and F5/F6 eligibility; retained evidence across later participant death; 64-per-subject and 64-per-claimant active capacities; 32 recent withdrawn records per subject with checked deterministic folding; bounded subject queries; save schema 26 and succession snapshot/system v2; authenticated exact-F6 schema-25 migration; raw diagnostic causality bound to enclosing events; checksum, save, restore, and continuation behavior; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish claim strength, legal precedence, seniority, primogeniture, legitimacy, recognition, political support, successor selection or resolution, missing-heir fallback, inherited claims, spouse or collateral rules, inheritance, wealth or estate transfer, regency, household/office/title/faction/retinue effects, disputed succession, player-character transfer, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains versioned claim/action/outcome/query contracts, the registered claim command/event workflow, deterministic state planning and mutation, bounded retention and queries, schema-25 authentication and 25→26 migration, exact-F6 fixture provenance, component performance coverage, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 1,077/73/6/18 suites passed before commit. Independent architecture, compatibility, simulation-engineer, and verification reviews accepted F701–F715; verification separately reran the 167/167 succession-focused slice.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `5cbfd64` to `62e03b6`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4`.

## Hosted run and jobs

Run 29480235359 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T07:31:57Z` through `2026-07-16T07:37:50Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87562071719](https://github.com/klassic12672/three-kingdom/actions/runs/29480235359/job/87562071719) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260715.0234.1`; native arm64 assertion | `07:32:00Z` / `07:36:44Z` | Pass |
| Windows x64 | [87562071728](https://github.com/klassic12672/three-kingdom/actions/runs/29480235359/job/87562071728) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `07:32:00Z` / `07:37:49Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 1,077 passed | 1,077 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 33.038 ms | Pass; `MAP_MODE_TIMING` 62.477 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/500-active-claim measurement remains a non-threshold observation; CI executes that test but does not print or assert its component timings.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29480235359/artifacts/8368267061) | 8368267061 | `e07d1e33e8e0edcfe7b74a209684ac8c5587dc79c9aa12a260912d48448f21fe` | 68,124,089 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29480235359/artifacts/8368290478) | 8368290478 | `1fe71c11e2ac1af5cde13295625bbe2a1c19ca65b621e1e6dffc05e1aa67fccb` | 73,442,987 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `a20bcda149cf6c951ed26f47cf0a1c8538ca6d02c8fd8f073cacd3823c936491` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `d7452cda7ac8079e51227993e560df24e303270ead5aadc1b1d095d95f19f523` |
| macOS PCK | 1,624,060 | `587ea648839ba60e5edf678a3c68cb97eaa73769e862d94ad1fbf8fdeb1edfe1` |
| macOS `Simulation.Core.dll` | 1,488,384 | `28fec4a567367f57807664005571084c79043714b0616ecfdd0a0cb3781c55d3` |
| Windows `build-manifest.json` | 503 | `b3746fc48de97b695136469480cd8443192259cde9eb8316620795a77ad9e0fa` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `a64ae01dc25d97e68a5ff7efa2232043bafcb93c20f363a52f053193571994af` |
| Windows `Simulation.Core.dll` | 1,488,384 | `219bb66031ee67305ba883556c76d96c8e44dc48d9f4ff4f6362cb63e335c768` |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact integrity.

## Decision

SP-04F7 criterion F716 passes at exact SHA `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4`. F7 now has local and hosted macOS arm64/Windows x64 evidence and is accepted. The local claim-workflow measurement remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered succession package may begin.
