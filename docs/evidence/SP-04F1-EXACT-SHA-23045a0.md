# SP-04F1 Exact-SHA Hosted Evidence — `23045a0`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F1 — career-death obligation closure |
| Exact commit | `23045a06a39361ecf8d2ef341cc0458590322f0a` |
| Parent commit | `d1f74f1b375c4425adbb49accc48e52f1c40d92c` |
| Commit tree | `d7cd73d63b645855489ecd44aa2872db08d81e82` |
| Commit subject | `feat: close SP-04F1 career obligations on death` |
| Parent-to-commit diff SHA-256 | `cac22b04bbb35f84a91f584bde216e0974e89cd4ac618c062e7964598511efac` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29459667744](https://github.com/klassic12672/three-kingdom/actions/runs/29459667744), attempt 1 |
| Overall result | **Pass — SP-04F1 criterion F118 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F1 package. It covers atomic career-death invalidation and service closure as the fifth public-death candidate; exact role-specific proposal, retinue-membership, patronage, and employment evidence; retained-retinue identity; bounded career-history retention and overflow rollback; affected-ID and event reconstruction validation; priority, submission-order, simultaneous-death, and later-day replay races; save schema 22; authenticated structural schema-21-to-22 migration from the exact F0 source; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish retinue succession or transfer, household-head replacement, captive disposition, inheritance, heir designation, claims, regency, offices or titles, disputed succession, player-character transfer, cause-of-death taxonomy, automatic mortality, relationship or memory effects, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains death-change v2, the career-death change set v1, the six appended terminal reasons, five-candidate atomic public death, schema-22 persistence/checksums, the exact-F0 schema-21 fixture, career/death/campaign/schema tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 831/73/6/18 suites passed before commit. Independent architecture, verification, and schema/content reviews found no remaining local blocker; verification separately reran 40/40 F1-focused tests and 831/831 complete Simulation.Core tests.
- The frozen 34,795-byte schema-21 fixture has file SHA-256 `8214d8f3430e48a09bc215a2dfc34608924472e44622885250351350a4581686` and stored historical checksum `a108ed5ad5932992db2c2adfac6a9987320f3ab2493a2719d4035c05701b39e2`. A temporary test-only public-workflow/`SaveStore` generator compiled in a detached worktree at exact accepted F0 implementation `783ccfb61357248158cf287ee69ba27b56c38f4a` generated it; that temporary generator was not retained.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `d1f74f1` to `23045a0`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `23045a06a39361ecf8d2ef341cc0458590322f0a`.

## Hosted run and jobs

Run 29459667744 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T23:49:08Z` through `2026-07-15T23:55:28Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87500316226](https://github.com/klassic12672/three-kingdom/actions/runs/29459667744/job/87500316226) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `23:49:12Z` / `23:53:02Z` | Pass |
| Windows x64 | [87500316211](https://github.com/klassic12672/three-kingdom/actions/runs/29459667744/job/87500316211) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `23:49:12Z` / `23:55:27Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 831 passed | 831 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 31.970 ms | Pass; `MAP_MODE_TIMING` 55.950 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/200-death career-rich measurement remains a non-threshold observation; CI executes that test but does not print or assert its observed component timings or checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29459667744/artifacts/8360728065) | 8360728065 | `0726f1c386f21276601a1cdd775aee415a7880fe7f4e5a9565a68d2df985a747` | 68,054,929 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29459667744/artifacts/8360766406) | 8360766406 | `43e3c9f333dd6dd2e0254efb0d617e24654b8a971a5923bb5d8cc60681816dfd` | 73,373,829 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `91865419186ec4bc73a07e6eaabd14a505e7dce9440d15924d41857db0e6302a` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `98312c34e03a582ac2ac8a7050a5c2a804c438720d6027d1d3c756139dbec229` |
| macOS PCK | 1,624,060 | `f8eaa80008c572b075e38a110e42a17291b0dab92df02943130735c5f4d37988` |
| macOS `Simulation.Core.dll` | 1,273,344 | `3eb99c529c4207f887d12c6ebd52171350f6fc66bcbd7b1ecda01e3bd6d62181` |
| Windows `build-manifest.json` | 503 | `4fefc255eeffde3fff50586e953a47fa041555ea300ad5cb68a01604fe5d64c5` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `18a8e84e847c70347aea0ca9a4c6960eb775b746db7faae9769e62641dab9b42` |
| Windows `Simulation.Core.dll` | 1,273,344 | `9c0df9c7a69c4a1f105104d21f7d3b9e7793aa6eeda274b6aafead18f28478df` |

The workflow emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect job conclusions or artifact integrity.

## Decision

SP-04F1 criterion F118 passes at exact SHA `23045a06a39361ecf8d2ef341cc0458590322f0a`. F1 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/200-death career-rich fixture remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered SP-04 package may begin.
