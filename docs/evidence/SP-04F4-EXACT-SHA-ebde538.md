# SP-04F4 Exact-SHA Hosted Evidence — `ebde538`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F4 — explicit personal heir designation |
| Exact commit | `ebde5387ac2d7398105f11043d9cdaeb2c2ae187` |
| Parent commit | `3f3ae026cc7a94af3da754613e3ba291f49afee6` |
| Commit tree | `ea967e83542da653cd35f4edeadcd805832f48b7` |
| Commit subject | `feat(simulation): add explicit heir designation lifecycle` |
| Parent-to-commit diff SHA-256 | `5f85207cd3ef33ac354e8f94b91eafd2e691872cd4a8997326a3965fbf6d70b5` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29470789978](https://github.com/klassic12672/three-kingdom/actions/runs/29470789978), attempt 1 |
| Overall result | **Pass — SP-04F4 criterion F415 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F4 package. It covers character-issued explicit heir designation, replacement, and revocation intent; exact-current concurrency; living/capable/free designator validation without an invented adulthood rule; deliberate living nominee permissions; stable lifecycle identity and terminal evidence; bounded topology-safe retention; overflow-safe folded history; global command/event role cardinality; retained-identity collision rejection; deterministic same-day action/death ordering; defensive authoritative queries; schema-25 persistence; authenticated structural schema-24-to-25 migration from exact F3 history; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish automatic or legal successor selection, legal eligibility or precedence, claims, inheritance, wealth or estate transfer, household-head inference, missing-heir fallback, regency, office/title/faction effects, retinue succession, disputed succession, political support, player-character transfer, relationship or memory effects, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. A designation is retained personal intent only. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains the new action/outcome/state/query contracts, atomic succession candidate, exact identity and lifecycle validation, one-active/32-terminal retention, topology and global event-role invariants, schema-25 persistence/migration, exact-F3 schema-24 fixture, campaign/schema/performance regressions, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 1,005/73/6/18 suites passed before commit. Independent architecture, schema/content, and verification reviews found no remaining local blocker and separately confirmed the 95/95 succession-focused slice.
- The frozen 15,228-byte schema-24 fixture has file SHA-256 `7f6ebd4b11ce0f5d9bd2ad8154a32750090bb235a35808565fe3c7df6daec10c` and stored historical checksum `adf2a49ac7aca33d0ca1a794724b06bdcae8cbd144c19aaafcbef5e8a2fc607f`. A temporary public-workflow test harness compiled in a detached worktree at exact accepted F3 implementation `72e5bd34f41f068c2e07a580e02522f8222eca30` generated it; that generator and worktree were removed.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `3f3ae02` to `ebde538`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `ebde5387ac2d7398105f11043d9cdaeb2c2ae187`.

## Hosted run and jobs

Run 29470789978 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T04:12:08Z` through `2026-07-16T04:18:16Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87533426637](https://github.com/klassic12672/three-kingdom/actions/runs/29470789978/job/87533426637) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `04:12:12Z` / `04:16:31Z` | Pass |
| Windows x64 | [87533426622](https://github.com/klassic12672/three-kingdom/actions/runs/29470789978/job/87533426622) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `04:12:12Z` / `04:18:15Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 1,005 passed | 1,005 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 35.195 ms | Pass; `MAP_MODE_TIMING` 54.240 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/200-designation measurement remains a non-threshold observation; CI executes that test but does not print or assert its observed component timings or checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29470789978/artifacts/8364754481) | 8364754481 | `285d9b7f04f7416b7616915cecbd06f7e20afe98a505428504ecbac80cf31686` | 68,090,236 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29470789978/artifacts/8364777806) | 8364777806 | `453525bebec7aef37b45d5ef307696cd648e4b19a592fcc799eba7dc80ae20eb` | 73,409,156 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `93a229df75df7c8a6cc6d2339b695cf0add047b606bfc87d2303045c01822168` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `abb7eee48b1e8046870b7c1c171ffd03893e999dff8ebfeb77c023f81d7288b6` |
| macOS PCK | 1,624,060 | `adbb95c7e08d6d909533c4ecd79f762fd60712c1571a56e775c6df38a94edfd1` |
| macOS `Simulation.Core.dll` | 1,369,600 | `96c0435ed5cdcd992e31e39831794d8d50b4d2e08b2518a0c6137c0cb9b79f60` |
| Windows `build-manifest.json` | 503 | `311127c16f71bd92927d080b65fe12508361742c7832e3eab8f7dec7475f0131` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `8eb85ff7c0089359c874d8d12d7f32bd5c3ce904d339d68f0cc00e3bf4b6b405` |
| Windows `Simulation.Core.dll` | 1,369,600 | `142221034645c2f0717254881be9ca0940f7e9ca5026c3211fd44862650d2247` |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact integrity.

## Decision

SP-04F4 criterion F415 passes at exact SHA `ebde5387ac2d7398105f11043d9cdaeb2c2ae187`. F4 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/200-designation fixture remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered legal-succession package may begin.
