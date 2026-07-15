# SP-04C2 Exact-SHA Hosted Evidence — `e2d9590`

| Field | Value |
|---|---|
| Evidence date | 2026-07-15 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04C2 — personal-wealth kernel |
| Exact commit | `e2d9590afc409da30aef86226a8d90a0023fbda3` |
| Parent commit | `2b61d76378d30ce2e0cddf6f2736952a92742481` |
| Commit tree | `914d6b8f2d625d5528b23bae99fb6c5a553fa20c` |
| Commit subject | `feat: add SP-04C2 personal wealth ledger` |
| Parent-to-commit binary-diff SHA-256 | `dedf850caa752b951b28dc3003cdadd0137bd0d48e0e8cdfdc9ee860312e25a4` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29408583882](https://github.com/klassic12672/three-kingdom/actions/runs/29408583882), attempt 1 |
| Overall result | **Pass — SP-04C2 criterion C214 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified C2 package. It covers abstract personal-wealth accounts, atomic registered transfers, exact stable IDs, bounded two-sided ledger/history, schema-8 current saves, authenticated schema-7-to-8 migration, validation, complete tests, Godot import, native development export, automated smoke, manifests, artifact upload, and static artifact inspection.

It does not establish estate holdings, household/family/faction/court treasuries, debt, currency or commodities, income, spending, taxation, geography, land grants, prices, marriage or romance, lifecycle, inheritance, succession, faction/court, battle, AI, content, UI, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second turn budget. The raw local 1,000-transfer fixture remains above that future budget. Every full SP-04 acceptance criterion remains unchecked.

## Candidate and remote identity

- The audited 18-file C2 commit contains only the resource contracts/state, registered simulation integration, schema-8 migration and frozen schema-7 fixture, checksum/soak changes, tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The complete 280/71/6/18 suites, zero-warning Release build, C2-touched-file formatting, diff check, and LFS check passed before commit.
- Independent architecture and verification review found one campaign-calendar defect and one missing campaign overflow regression. Both were corrected; focused regressions and final re-review found no unresolved correctness, architecture, secret, machine-path, or unrelated-change issue.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `2b61d76` to `e2d9590`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `e2d9590afc409da30aef86226a8d90a0023fbda3`.

## Hosted run and jobs

Run 29408583882 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T10:34:11Z` through `2026-07-15T10:38:25Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87329948409](https://github.com/klassic12672/three-kingdom/actions/runs/29408583882/job/87329948409) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `10:34:15Z` / `10:37:50Z` | Pass |
| Windows x64 | [87329948206](https://github.com/klassic12672/three-kingdom/actions/runs/29408583882/job/87329948206) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1` | `10:34:15Z` / `10:38:24Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true`, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 280 passed | 280 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass | Pass |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source soak assertion plus both hosted 280-test Core passes establish checksum `07507a7058b13603e3ecc377870f9e13551ce5bde237012ba90c377cd5e2f79c`; CI does not print that checksum directly.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded through authenticated GitHub access and extracted successfully. File counts matched upload logs, manifests matched the smoke output, and executables matched the required architectures.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29408583882/artifacts/8340170008) | 8340170008 | `155ee759c473d8ea2b6abe7c1935c828e82a4317893829d4bb25dc8313abca26` | 67,846,113 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29408583882/artifacts/8340183666) | 8340183666 | `8376304699ab07a1bcd11a5b90a2e40eb0f3a76311500007ab120d7112fce921` | 73,165,051 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `56e47f528466358f8e90a7f5aa63af8d2f495cb6b580be5124d62c149fdd27be` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `fd4d3ae81e770fd344376df2af2313ce3f2d62d24f18cf200ddac62cf00b94ec` |
| macOS PCK | 1,624,060 | `c9849fd4dda9f9f6ad2e18afc99712c493abe74b16f6fdc4be3e4fda5a0790d6` |
| Windows `build-manifest.json` | 503 | `16c07509cdb151985b37c3ed957dbc450c1d62c5537cc0799070ed0c4ea25ac1` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `20db10cb99cb85d1dbb0a5689cff34513d686b5739e4d656a703c2c372fa4642` |

The workflow emitted non-failing Node.js 20 deprecation warnings for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. They did not affect job conclusions or artifact integrity.

## Decision

SP-04C2 criterion C214 passes at exact SHA `e2d9590afc409da30aef86226a8d90a0023fbda3`. C2 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-transfer fixture checksum remains `989c242311a374c57ea4c306219d08aac12f2b0111f44d1d4df659c1cc657bf0`, but its recorded 8.077-second direct transfer-processing measurement does not satisfy the later full-SP-04 three-second budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and SP-04C3 opaque estate holdings is the next package.
