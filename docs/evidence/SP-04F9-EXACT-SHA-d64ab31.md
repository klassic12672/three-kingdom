# SP-04F9 Exact-SHA Hosted Evidence — `d64ab31`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F9 — deterministic succession resolution, inheritance, minimal regency hooks, and player continuity |
| Exact commit | `d64ab315a96fda15a2802ebcc687bbf50924fa2c` |
| Parent commit | `5794f0136092a745976c50b570c0b8fe8933b5b7` |
| Commit tree | `db3d0603fe0bdbde1f0373f0122556cc41ccce12` |
| Commit subject | `feat(simulation): add deterministic succession resolution` |
| Parent-to-commit diff SHA-256 | `a63f4301577b7bc8e888b509dd282ce6a712dde5e2cdd498cc5cf7db70b33b76` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29496081792](https://github.com/klassic12672/three-kingdom/actions/runs/29496081792), attempt 1 |
| Overall result | **Pass — SP-04F9 criterion F917 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F9 package. It covers one deterministic succession-death workflow; explicit transient precedence over designation, typed descendant/adoption, principal spouse, typed collateral, claim, and support evidence; selected, disputed, and no-successor outcomes; exact-state revalidation; atomic death cleanup; personal-wealth conservation and opaque-estate inheritance; distinct household-head replacement; non-hereditary personal service closure; minimal regency evidence; player continuity; bounded detailed resolution retention and checked folding; save schema 28 and succession snapshot/system v4; authenticated exact-F8 schema-27 migration; raw and restored diagnostic validation; deterministic replay, recovery, continuation, and soak behavior; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish living-incapacity succession, household vacancy or dissolution, inherited personal service, retinue leadership succession, office/title/faction/court effect, political regent authority or lifecycle, observer-filtered application queries, historical content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, the full SP-04 three-second campaign-turn budget, full SP-04 acceptance, or M2 completion.

## Candidate and remote identity

- The audited commit contains the versioned action/outcome/rule/resolution/inheritance/regency/continuity/query contracts, registered atomic workflow, deterministic ranking and retention, schema-27 authentication and 27→28 migration, exact-F8 fixture provenance, integrated tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 1,217/73/6/18 suites passed before commit. The succession-focused slice passed 307/307; independent architecture, simulation-engineer, Ponytail, and final verification reviews approved F901–F916.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `5794f01` to `d64ab31`. No branch, tag, pull request, merge, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `d64ab315a96fda15a2802ebcc687bbf50924fa2c`.

## Hosted run and jobs

Run 29496081792 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T11:53:37Z` through `2026-07-16T12:00:37Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87613234338](https://github.com/klassic12672/three-kingdom/actions/runs/29496081792/job/87613234338) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `11:53:40Z` / `12:00:36Z` | Pass |
| Windows x64 | [87613234387](https://github.com/klassic12672/three-kingdom/actions/runs/29496081792/job/87613234387) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `11:53:40Z` / `12:00:16Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 1,217 passed | 1,217 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 34.758 ms | Pass; `MAP_MODE_TIMING` 54.170 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/100-resolution measurement remains a non-threshold observation; CI executes that test but does not print or assert its component timings.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, extracted file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29496081792/artifacts/8374582862) | 8374582862 | `48b17178a54ff6acb3a274b01722d98f0893f9f8505e8c9aadc824926cbc0fe1` | 68,206,155 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29496081792/artifacts/8374578343) | 8374578343 | `b56942c90134663364330798c2a0d7b992820b80bba212dd2779baeea32a4ccd` | 73,525,071 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `0f77adf4013619031cd290e55b502f966d66531728eb5cc49dbfff1ceda5f823` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `19ed1c601c0778652c19221f43081b647aa915a81b51f0c89d61a38a081607f0` |
| macOS PCK | 1,624,060 | `7ba3e8d70d995a49f5d985532beb73a5718803f0a78609731fff2f5183f02a61` |
| macOS `Simulation.Core.dll` | 1,709,056 | `57dc4e005e859b6140663af4e9f025ca6490b8009970fe30607444ab06e76a31` |
| Windows `build-manifest.json` | 503 | `91087e5ed7cd17e8721f49058c872f58a900c8df3972118cfd1024c243f72c93` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `fa3a7ac230a5effa6708a66676aa94b60dde3ab6d0ed7a5cc0f5ecce05eefe8e` |
| Windows `Simulation.Core.dll` | 1,709,056 | `6c264dc6c6fcd451c83cc6b277c231c0468e235d663f1df2935f6b8899c01939` |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact integrity.

## Decision

SP-04F9 criterion F917 passes at exact SHA `d64ab315a96fda15a2802ebcc687bbf50924fa2c`. F9 now has local and hosted macOS arm64/Windows x64 evidence and is accepted. The local resolution-workflow measurement remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and SP-04G is next but is not started here.
