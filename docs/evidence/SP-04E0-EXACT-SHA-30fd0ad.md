# SP-04E0 Exact-SHA Hosted Evidence — `30fd0ad`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04E0 — legal-adoptive parent establishment |
| Exact commit | `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed` |
| Parent commit | `0a0ae128e03e6f16cf3d003071977c95d0af41d1` |
| Commit tree | `72ab6c23ab752c083d656819182dcf612a2b292c` |
| Commit subject | `feat: add SP-04E0 legal adoption` |
| Parent-to-commit diff SHA-256 | `143943cb40691fb4473ac17b41d3b87a3477dbc1f166830d9d8e8d3c91e66104` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29436287227](https://github.com/klassic12672/three-kingdom/actions/runs/29436287227), attempt 1 |
| Overall result | **Pass — SP-04E0 criterion E013 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified E0 package. It covers one reserved-system action that establishes current legal-adoptive parentage; exact expected-current revalidation; action-local parent/child bounds; retained-marriage preflight; atomic apply-time replan; pending replay; defensive payload ownership; current schema-14 saves; authenticated vocabulary-only schema-13-to-14 migration; the exact-D3 schema-13 fixture; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and static artifact inspection.

It does not establish adoption revocation, effective-dated adoption history, guardianship, residence or family/household movement, consent effects, pregnancy, birth, education, coming of age, public death, inheritance, succession, claims, content, localization, UI, AI, battle, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second turn budget. Every full SP-04 acceptance criterion remains unchecked.

## Candidate and remote identity

- The audited E0 commit contains the family-action contracts and registry entry, legal-adoption domain and campaign integration, schema-14 compatibility, the exact-D3 schema-13 fixture, focused and compatibility tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The complete 539/71/6/18 suites, zero-warning Release build, focused 48-test family/schema-13 slice, touched-file formatting, diff check, and LFS check passed before commit.
- The frozen 52,467-byte schema-13 fixture has file SHA-256 `f54e0bb16eb827b2b739384a27325dd0a279cd43f18d612085c247fd3aa1fca5` and stored historical checksum `540dac5d9065bbb1b66a484c550eadf4e60de7be92acacb5c716bfcf7f4956c4`. A detached harness compiled against exact accepted D3 source `93d38810a87707a7c4c98c7392e2a2f20dc030fb` generated it.
- Independent architecture and adversarial verification identified and remediated mutable caller ownership of the expected-parent-link list and expanded stale-event, same-turn race, complete sibling-category, checksum, diagnostic, and exhaustive schema-13 rejection coverage. Final re-review found no remaining correctness or package-boundary blocker.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `0a0ae12` to `30fd0ad`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed`.

## Hosted run and jobs

Run 29436287227 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T17:23:27Z` through `2026-07-15T17:28:33Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87423660284](https://github.com/klassic12672/three-kingdom/actions/runs/29436287227/job/87423660284) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `17:23:31Z` / `17:27:51Z` | Pass |
| Windows x64 | [87423660397](https://github.com/klassic12672/three-kingdom/actions/runs/29436287227/job/87423660397) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `17:23:32Z` / `17:28:32Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true`, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 539 passed | 539 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_READY` in 63.689 ms | Pass; `MAP_MODE_READY` in 45.839 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source family performance assertion plus both hosted 539-test Core passes establish checksum `fa8e08d551b8e3fe3e5b3d066b418aed6e2d68526c36320b2b8f0c5bf7d94e8f`; CI does not print that checksum directly.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29436287227/artifacts/8351627173) | 8351627173 | `e8c5fe6ae9e7bd63b4da8a1c000e1ef3fbcf15547fcc63eaca3121b2bfc0aead` | 67,985,571 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29436287227/artifacts/8351646793) | 8351646793 | `d64312fe01f6f455271e863cb2c9bf9d6ea95d0fd595eaa6ea40e7ad1c0f705b` | 73,304,515 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `88b5e5f2b2d05145a75b40fc9ad7919f5723b56bea16e51834e1d6c658ca5de9` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `cfca256dc4395487689c852c13a6c047bce1e9b4cab4e5950bb0301732ff4b40` |
| macOS PCK | 1,624,060 | `5e53d37d1b2ad5b1d11f6e5e5826fad8505adbf33313e27cebdb9b28c4ff5aae` |
| macOS `Simulation.Core.dll` | 1,079,808 | `3e9f91cec92f1511082627e4aa9c650c07ddc6baa5a83c3300630195f015279a` |
| Windows `build-manifest.json` | 503 | `9a16b8e52b0e38bb13b6f0b33e691c2727a2374cb861bc95c09797f185ff9fd9` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `22d565e68e7982ec3201652aa82be22506be58918f7433083476b334bcb888bc` |
| Windows `Simulation.Core.dll` | 1,079,808 | `5384dd9e8e97c0a299631f54611b6bd6f4c92ac7f3c8695c0cae69e1aab8406e` |

The workflow emitted non-failing Node.js 20 deprecation warnings for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. They did not affect job conclusions or artifact integrity.

## Decision

SP-04E0 criterion E013 passes at exact SHA `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed`. E0 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/64-adoption fixture remains raw component evidence and does not establish the full-SP-04 three-second turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered SP-04E package may begin.
