# SP-04C3 Exact-SHA Hosted Evidence — `7b9f795`

| Field | Value |
|---|---|
| Evidence date | 2026-07-15 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04C3 — opaque character-estate holdings |
| Exact commit | `7b9f795320e5f4c14aa7e14185e7ba035fdf6847` |
| Parent commit | `dd1a65904713d297e094e956bc232ecc1093bb33` |
| Commit tree | `708ad91bafbd484ff371ed60b4f7a2f0c0b048c9` |
| Commit subject | `feat: add SP-04C3 estate holdings` |
| Parent-to-commit binary-diff SHA-256 | `0c5a5f12f247308d02f1ec7d0bedd71100998c8a6e288ffa36fe56eb6f9288e9` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29410724965](https://github.com/klassic12672/three-kingdom/actions/runs/29410724965), attempt 1 |
| Overall result | **Pass — SP-04C3 criterion C316 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified C3 package. It covers the separate version-1 estate-holding subsystem, owner-independent `estate:` identities, exact owner validation and 64-holding bound, canonical defensive queries, dead-owner persistence, schema-9 current saves, authenticated schema-8-to-9 migration, historical checksums, complete tests, Godot import, native development export, automated smoke, manifests, artifact upload, and static artifact inspection.

It does not establish estate location, acreage, legal control, land grants, claims, value, yield, rent, tax, production, administration, household/family/faction/court ownership, marriage or romance, lifecycle mutation, inheritance or succession resolution, content, UI, battle, AI, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second turn budget. C3 registers no estate command, event, public mutator, history, cleanup, or automatic inheritance rule. Every full SP-04 acceptance criterion remains unchecked.

## Candidate and remote identity

- The audited 16-file C3 commit contains only estate contracts/state, simulation/save/checksum integration, the literal exact-C2 schema-8 fixture, focused and compatibility tests, the soak golden, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The complete 312/71/6/18 suites, zero-warning Release build, C3-touched-file formatting, diff check, and LFS check passed before commit.
- Independent architecture review approved the separate subsystem without an ADR. Adversarial verification identified and remediated one query-eligibility coupling and missing checksum/migration assertions; focused regressions and final re-review found no open code issue.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `dd1a659` to `7b9f795`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `7b9f795320e5f4c14aa7e14185e7ba035fdf6847`.

## Hosted run and jobs

Run 29410724965 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T11:10:49Z` through `2026-07-15T11:15:57Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87336907116](https://github.com/klassic12672/three-kingdom/actions/runs/29410724965/job/87336907116) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `11:10:52Z` / `11:14:25Z` | Pass |
| Windows x64 | [87336907163](https://github.com/klassic12672/three-kingdom/actions/runs/29410724965/job/87336907163) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1` | `11:10:52Z` / `11:15:56Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true`, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 312 passed | 312 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass | Pass |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source soak assertion plus both hosted 312-test Core passes establish checksum `798a96c57375fb3012c55175195430604a66a658fdf786d7fd0f0ba4e96cce9b`; CI does not print that checksum directly.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded through authenticated GitHub access and extracted successfully. File counts matched upload logs, manifests matched the smoke output, and executables matched the required architectures.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29410724965/artifacts/8341046027) | 8341046027 | `f5753846d0aaebe35fee047705e367f16f5b3765867e2fbc3847a7a633e931f9` | 67,851,141 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29410724965/artifacts/8341081212) | 8341081212 | `5ce4a60a73acb2fd7c0c4e085d72b669b8517d0aad1e7a67291ebc996c881024` | 73,170,044 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `26249aee8cf5d84ff066fefc6d04a20ad96c447bc525b22654fcdf857e616105` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `cc94d21de85945fed34be2218a895fdeab36f3f5d980911f466cacb3dfffcccf` |
| macOS PCK | 1,624,060 | `f67057d1e638acd39b619fb27d88236979370a12c45fdfaa333a5a9723137616` |
| Windows `build-manifest.json` | 503 | `03b4210889fa6a2573624791f02d583e23d4c1d6a9bdab89d3dfcd7d5e279838` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `6944251220841ba336369be434f85730fabf2d5e67cf7aa039b709dc865ad4bb` |

The workflow emitted non-failing Node.js 20 deprecation warnings for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. They did not affect job conclusions or artifact integrity.

## Decision

SP-04C3 criterion C316 passes at exact SHA `7b9f795320e5f4c14aa7e14185e7ba035fdf6847`. C3 now has local and hosted macOS arm64/Windows x64 evidence. The local 8,000-holding fixture checksum remains `c033184371cf80b022a174acc5fa799345fa5eca9a89feec90f75b8a4c5bc83a`, but its raw timings do not establish the full-SP-04 three-second turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and SP-04D household/marriage work is the next package.
