# SP-04F5 Exact-SHA Hosted Evidence — `bb9dcff`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F5 — pairwise legal-candidate eligibility |
| Exact commit | `bb9dcffab8f517e7a2f9cb0b47072bb096fc7725` |
| Parent commit | `6bb4a18e257d52c4d75d02a4166ae593ee7357a9` |
| Commit tree | `d99f67288c2365adf6f4022468f81efb3b1c9f02` |
| Commit subject | `feat(simulation): add succession candidate eligibility query` |
| Parent-to-commit diff SHA-256 | `c7afb17f7af1dcc449d5bf4a7eeec8dd79240951c7ba2542f19eda0051657404` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29473043674](https://github.com/klassic12672/three-kingdom/actions/runs/29473043674), attempt 1 |
| Overall result | **Pass — SP-04F5 criterion F512 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F5 package. It covers one current-state, query-only `EvaluateCandidate` operation over an explicit subject/candidate pair and transient eligibility rule; versioned rule, request, basis, issue, and result contracts; the current active F4 designation; typed biological, legal-adoptive, and legacy-unknown descendant paths; shortest generation per recognized basis; explicit age, incapacity, and custody policy; canonical controlled issues; deterministic defensive results; query purity; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish candidate-set generation, precedence, seniority, primogeniture, legitimacy, claims, deterministic successor selection or resolution, death/incapacity integration, inheritance, wealth or estate transfer, spouse or collateral rules, missing-heir fallback, regency, household/office/title/faction effects, retinue succession, disputed support, political support, player-character transfer, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. The supplied eligibility rule is transient and creates no persisted or universal cultural law. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains the version-2 authoritative query surface, version-1 transient F5 contracts, canonical evaluator, typed bounded descendant traversal, policy-controlled participant checks, result serialization and purity regressions, 1,000-character component fixture, and same-package documentation. Save schema 25, succession snapshot/system version 1, every F4 persisted/action contract, commands, events, migrations, checksum inputs, and random streams remain unchanged.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 1,015/73/6/18 suites passed before commit. Independent architecture, compatibility, and verification reviews found no remaining local blocker and separately confirmed the 105/105 succession-focused slice.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `6bb4a18` to `bb9dcff`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `bb9dcffab8f517e7a2f9cb0b47072bb096fc7725`.

## Hosted run and jobs

Run 29473043674 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T05:07:01Z` through `2026-07-16T05:14:26Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87539960998](https://github.com/klassic12672/three-kingdom/actions/runs/29473043674/job/87539960998) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `05:07:04Z` / `05:14:25Z` | Pass |
| Windows x64 | [87539960978](https://github.com/klassic12672/three-kingdom/actions/runs/29473043674/job/87539960978) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1` | `05:07:06Z` / `05:13:51Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 1,015 passed | 1,015 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 67.095 ms | Pass; `MAP_MODE_TIMING` 80.583 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/999-pair measurement remains a non-threshold observation; CI executes that test but does not print or assert its observed component timings or checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29473043674/artifacts/8365596036) | 8365596036 | `d51c7fdf1ca6a539451d331764385e8241139836bfd2f97a1a4fb291d40b7f1e` | 68,097,749 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29473043674/artifacts/8365585874) | 8365585874 | `ac8ff430e9e9d27c452306ab65c3d8f380fbd15b1f1233bfb986adc336ba4054` | 73,416,653 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `8fb1a8c993028223da976343005102279e83e8eee76a018cd56da9248da5248b` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `e8ba1019be5f04ac4ca4123726f53d144c0c4e265d43ed7b156297e11efb254d` |
| macOS PCK | 1,624,060 | `161630039032a1d4c0ae33e1523cce61d9ce0431e7945f07e7d9204db95215e5` |
| macOS `Simulation.Core.dll` | 1,389,568 | `3e67c0263615f5ceb524ffefff6655879ce4d7f823a2082707f0852625463248` |
| Windows `build-manifest.json` | 503 | `182ac73ca68caf774311c0510c1d3313f6c3cd75395aa43827bda9be0810fad0` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `2f531907c5c682fe4fc5534669bf851d1df49f70aae7393210513703ed268a5d` |
| Windows `Simulation.Core.dll` | 1,389,568 | `80e4ecad4796c17be713df1738f8555f9fa731302f6488335605e58b5eae488c` |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact integrity.

## Decision

SP-04F5 criterion F512 passes at exact SHA `bb9dcffab8f517e7a2f9cb0b47072bb096fc7725`. F5 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/999-pair fixture remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered legal-succession package may begin.
