# SP-04E4 Exact-SHA Hosted Evidence — `177346b`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04E4 — adult union-linked active pregnancy registration |
| Exact commit | `177346b7358e84da358f3bfac8057b6ea70ed412` |
| Parent commit | `630913346fce4cecf9f6cfcd3af6411ba82e66f9` |
| Commit tree | `3bb8ac4bb96c3b5e11847b96252bc04d83b831da` |
| Commit subject | `feat: add SP-04E4 pregnancy registration` |
| Parent-to-commit diff SHA-256 | `4da1e650a2ff49751cd6b0d0cb96972a1af5181065e24a99edf587161b74f260` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29447393782](https://github.com/klassic12672/three-kingdom/actions/runs/29447393782), attempt 1 |
| Overall result | **Pass — SP-04E4 criterion E415 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified E4 package. It covers `simulation.character_pregnancies@1`; explicit gestational and other biological parent roles; adult, living, exact-active-union registration; one active pregnancy per gestational parent and source union; fixed 280-day expected-birth calculation; stable source-derived IDs; canonical defensive queries; reserved family action authority; canonical priority-then-event-ID race ordering; exact affected IDs and apply-time replanning; rollback and replay; schema-18 active state and diagnostics; authenticated schema-17-to-18 migration from exact E3 source; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and static artifact inspection.

It does not establish conception scheduling, fertility or reproductive descriptors, pregnancy loss, birth, child identity or parentage creation, education, mutable abilities or traits, public death or union-ending integration, inheritance, succession, new content, localization, UI, AI, battle, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second turn budget. Passing the expected-birth date does not resolve the active record. Every full SP-04 acceptance criterion remains unchecked.

## Candidate and remote identity

- The audited commit contains pregnancy-v1 contracts/state/query, deterministic family-action integration, schema-18 persistence and checksums, exact-E3 schema-17 fixture, focused/campaign/save tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The complete 674/71/6/18 suites, zero-warning Release build, pregnancy-focused 16-test slice, formatting, diff check, and LFS check passed before commit.
- The frozen 68,407-byte schema-17 fixture has file SHA-256 `c99c1183f408dfd66eb08015e3b10d4e5b3a2f573c4adb364cd395fb8a1eb9c2` and stored historical checksum `7a680781eceabf8c46554f780aaaca0ef6f781caf3256cac46185f0031e88ea4`. A detached harness compiled against exact accepted E3 production source `59588be9d277dc4c4cb7ec98ef99e33591b0eeda` generated it.
- Independent review found and remediated race-order, event-order wording, apply-time rollback, resolved-gzip, union-query, and isolated historical-property evidence gaps. Architecture re-review also required schema 18 to reject an ended source union or either currently dead parent until a later lifecycle package defines atomic pregnancy handling. Final architecture and verification review found no remaining blocker.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `6309133` to `177346b`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `177346b7358e84da358f3bfac8057b6ea70ed412`.

## Hosted run and jobs

Run 29447393782 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T20:12:06Z` through `2026-07-15T20:17:48Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87461165847](https://github.com/klassic12672/three-kingdom/actions/runs/29447393782/job/87461165847) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `20:12:11Z` / `20:16:02Z` | Pass |
| Windows x64 | [87461165877](https://github.com/klassic12672/three-kingdom/actions/runs/29447393782/job/87461165877) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `20:12:10Z` / `20:17:47Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true`, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 674 passed | 674 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 34.295 ms | Pass; `MAP_MODE_TIMING` 55.811 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source ten-year/1,000-entity soak assertion plus both hosted 674-test Core passes establish checksum `ff5de686f960dc216e853642476c0334f598543c7aa62d5e07530977297bb218`. The local 1,000-character/one-union pregnancy measurement remains a non-threshold observation; CI does not print or assert its observed component checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29447393782/artifacts/8356105938) | 8356105938 | `326031044a8e7e891f89579d62230398558c07ca7004084b3909399ca4431cca` | 68,022,277 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29447393782/artifacts/8356149808) | 8356149808 | `298dfc0ab6605583daa95c5a9ac8c2887ad528f64d444eb8af15fb7c721bbbfc` | 73,341,169 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `8ba14cc51a85afd222979ec60310c396d45cfade225c2d25060fbef22e688336` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `f340c8fc6e7d1ad796a9cfadae940c36c43edf4af1829bcb67185c21cd3e502c` |
| macOS PCK | 1,624,060 | `845ea275dc3df9aa86e2a702bb0fcbfbe9aec6ae76b48c65be36a303124fd5b8` |
| macOS `Simulation.Core.dll` | 1,182,208 | `dcb4cf72ec5ae63904bda4e87c14685e0359193ef9fb21c3bf55b90c6ef133e7` |
| Windows `build-manifest.json` | 503 | `b91509192bd01994f799784bb682c500359ef588fc6c22c726c1577317078f72` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `cc33432bbd02b30664700d6c771f97a1e7883cce0847693bdeaec45d31ae02bf` |
| Windows `Simulation.Core.dll` | 1,182,208 | `a08de84716998d922ff822bf792aac760ad5064005a62cffe9d43e782249db7a` |

The workflow emitted a non-failing Node.js 20 deprecation warning for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. It did not affect job conclusions or artifact integrity.

## Decision

SP-04E4 criterion E415 passes at exact SHA `177346b7358e84da358f3bfac8057b6ea70ed412`. E4 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character pregnancy fixture remains raw component evidence and does not establish the full-SP-04 three-second turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered SP-04 package may begin.
