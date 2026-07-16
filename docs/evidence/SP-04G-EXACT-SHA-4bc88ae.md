# SP-04G Exact-SHA Hosted Evidence — `4bc88ae`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04G — observer-filtered character/relationship/household/succession queries and minimum character-event integration |
| Exact commit | `4bc88aee38bda5cd67902fbb93f8386f5a87f380` |
| Parent commit | `94a9c3430a9718ca00bfdc07874900fe87a91685` |
| Commit tree | `2fc669e95fb46ba544ad38414fee3bd1eb0d57ff` |
| Commit subject | `feat(simulation): complete SP-04G observer integration` |
| Parent-to-commit diff SHA-256 | `f97bab45a95896c0ad540c7b99d84c7332f3b66c6fd0355b90e2b9750a942fe7` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29501126949](https://github.com/klassic12672/three-kingdom/actions/runs/29501126949), attempt 1 |
| Overall result | **Pass — SP-04G criterion G15 is supported at the exact SHA; SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally reviewed G package. It covers the immutable observer-filtered `CharacterProfile`, reused `RelationshipSummary`, `HouseholdView`, and `SuccessionView` application surface; live controlled-character continuity; self/public/participant/witness/private visibility; bounded defensive results; legal marriage versus emotional romance separation; existing coercion invariants; one narrow character battle-contribution adapter; explicit shared memories; registered wound, capture, rescue, and caller-supplied exact succession-death mapping; fail-closed two-phase death recomputation; save schema 29; authenticated exact-F9 schema-28 migration; exact wound diagnostics; replay, save/restore, tier transition, player transfer, and later-day continuation; complete tests; Godot import; native development export; automated smoke; manifest inspection; and artifact upload/API identity.

It does not establish a final `BattleSetup` or `BattleResult`, tactical behavior, generic knowledge, political/faction/court/diplomacy/office/title integration, armies, AI, Godot character screens, historical content, physical Windows behavior, signing, Steam, release readiness, the full SP-04 three-second campaign-turn budget, full SP-04 acceptance, or M2 completion.

## Candidate and remote identity

- The audited commit contains the frozen observer whitelists, controlled-character facade, three-type battle contribution boundary, retained wound action, schema-28 authentication and 28→29 migration, exact-F9 fixture provenance, integrated tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 1,239/73/18/18 suites passed before commit. The combined succession/G slice passed 329/329; architecture, simulation-engineer, Ponytail full/review, and final independent verification approved G01–G14.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `94a9c34` to `4bc88ae`. No branch, tag, pull request, merge, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `4bc88aee38bda5cd67902fbb93f8386f5a87f380`.

## Hosted run and jobs

Run 29501126949 was triggered automatically by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T13:10:59Z` through `2026-07-16T13:18:26Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87630024985](https://github.com/klassic12672/three-kingdom/actions/runs/29501126949/job/87630024985) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native `uname -m = arm64` assertion | `13:11:04Z` / `13:17:26Z` | Pass |
| Windows x64 | [87630024848](https://github.com/klassic12672/three-kingdom/actions/runs/29501126949/job/87630024848) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `13:11:04Z` / `13:18:25Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 1,239 passed | 1,239 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 18 passed | 18 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | macOS arm64 target | Windows x64 target |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 56.727 ms | Pass; `MAP_MODE_TIMING` 87.407 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions.

## Build-manifest identities

Both smoke manifests record schema 2, project version `0.1.0`, exact Git SHA `4bc88aee38bda5cd67902fbb93f8386f5a87f380`, Godot `4.6.1.stable.mono.official.14d19694e`, .NET SDK `10.0.301`, `Development` configuration, content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`, and content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`.

| Platform | Manifest platform | Manifest architecture | Smoke geography checksum |
|---|---|---|---|
| macOS arm64 | `macos` | `arm64` | `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` |
| Windows x64 | `windows` | `x86_64` | `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` |

## Artifact identities

The immutable artifact API records match the upload logs for name, ID, exact SHA, final size, and run identity. No full artifact was downloaded because there was no identity inconsistency and G changes no packaging workflow or package layout.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29501126949/artifacts/8376636158) | 8376636158 | `35b41d5261bef06003473900170c3973a710578b0a8a3df51ee44bf6b870f246` | 68,240,382 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29501126949/artifacts/8376663172) | 8376663172 | `c9ef77306e93eeb233a5a142dc6d911a19e3e4e90bd3c9c22707e2c7c7868f1d` | 73,559,293 | 2026-10-14 |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact identity.

## Decision

SP-04G criterion G15 passes at exact SHA `4bc88aee38bda5cd67902fbb93f8386f5a87f380`. G now has local and hosted macOS arm64/Windows x64 evidence and is accepted. SP-04 and M2 remain Active, SP-05/SP-07/SP-08 remain blocked, and SP-04X1 is the next package but is not started here.
