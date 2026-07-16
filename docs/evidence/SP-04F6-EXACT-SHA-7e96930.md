# SP-04F6 Exact-SHA Hosted Evidence — `7e96930`

| Field | Value |
|---|---|
| Evidence date | 2026-07-16 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04F6 — caller-bounded candidate-set enumeration |
| Exact commit | `7e96930d103081e981c0cf1a06736223e222bc07` |
| Parent commit | `c8ccc88dd22b0916d6d565cfe649406d3c3af509` |
| Commit tree | `0d54050a64e201d6f2bc2d500bca1adfb1b61daa` |
| Commit subject | `feat(simulation): add bounded succession candidate enumeration` |
| Parent-to-commit diff SHA-256 | `26254f042c10f071dee6f7fb091d9874c4bf9ed78074bdc0a7bb8fc86d3d36de` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29474962692](https://github.com/klassic12672/three-kingdom/actions/runs/29474962692), attempt 1 |
| Overall result | **Pass — SP-04F6 criterion F613 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified F6 package. It covers one query-only `FindEligibleCandidates` operation over an explicit succession subject, the accepted transient F5 eligibility rule, and a caller-selected positive maximum; complete enumeration of every current authoritative F5-eligible candidate; unique entries retaining every recognized basis; canonical serialization order; controlled invalid inputs; complete empty sets; caller-bounded retained entries; fail-closed overflow with an exact total and no partial set; deterministic defensive results; query purity; complete tests; Godot import; native development export; automated smoke; manifests; artifact upload; and authenticated static artifact inspection.

It does not establish precedence, seniority, primogeniture, legitimacy, claims, deterministic successor selection or resolution, death/incapacity integration, inheritance, wealth or estate transfer, spouse or collateral rules, missing-heir fallback, regency, household/office/title/faction effects, retinue succession, disputed or political support, player-character transfer, content, localization, UI, AI, battle integration, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second campaign-turn budget. Canonical character-ID order is serialization only, and the supplied eligibility rule remains transient rather than a persisted or universal cultural law. Full SP-04 acceptance remains unchecked.

## Candidate and remote identity

- The audited commit contains the version-3 authoritative query surface, version-1 transient F6 candidate-set contracts, canonical whole-roster enumeration through F5, bounded retained-response behavior, exact overflow counting, condition/designation/depth/multi-basis/purity regressions, a 1,000-character component fixture, and same-package documentation. Save schema 25, succession snapshot/system version 1, every F4 persisted/action contract, F5 evaluation contracts and behavior, commands, events, migrations, checksum inputs, and random streams remain unchanged.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The zero-warning Release build and complete 1,025/73/6/18 suites passed before commit. Independent architecture, compatibility, and verification reviews found no remaining local blocker and separately confirmed the 115/115 succession-focused slice.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `c8ccc88` to `7e96930`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Local, remote, workflow, manifest, and artifact identities all resolve to `7e96930d103081e981c0cf1a06736223e222bc07`.

## Hosted run and jobs

Run 29474962692 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-16T05:50:20Z` through `2026-07-16T05:56:27Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87545885883](https://github.com/klassic12672/three-kingdom/actions/runs/29474962692/job/87545885883) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `05:50:24Z` / `05:56:26Z` | Pass |
| Windows x64 | [87545885983](https://github.com/klassic12672/three-kingdom/actions/runs/29474962692/job/87545885983) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260714.173.1` | `05:50:24Z` / `05:55:31Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used LFS, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 1,025 passed | 1,025 passed |
| `Game.Content.Tests` | 73 passed | 73 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass; `MAP_MODE_TIMING` 50.722 ms | Pass; `MAP_MODE_TIMING` 47.958 ms |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The local 1,000-character/999-candidate measurement remains a non-threshold observation; CI executes that test but does not print or assert its observed component timings or checksum.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded and extracted through authenticated GitHub access. The immutable API digests matched the workflow upload logs, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures. Because `gh run download` extracted the archives directly, this report does not claim an independent local ZIP digest.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29474962692/artifacts/8366287704) | 8366287704 | `6a92e9581d93c24a441cd6025322c2aa5d4633a67ca10878b30e223230e9f6f9` | 68,103,040 | 2026-10-14 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29474962692/artifacts/8366271157) | 8366271157 | `b762781a500ae535807524e9adc83b3a46a94eb41079338efeb0a167cb24dc97` | 73,421,951 | 2026-10-14 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `ec8431f4016a304e212d9c54c893e497f46a60efcc49508a90e9859253536bc8` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `e8cd93a8093679ae301d642bf8379f07da09d1974aeb616e7d1395eef411175b` |
| macOS PCK | 1,624,060 | `8efbb5e4d68609e21c32bd2bdcaf1ce70972edd2434444b5a361a57de6860df6` |
| macOS `Simulation.Core.dll` | 1,401,856 | `21b161a7bb3e6894cfaab583c5a892ff56019b5fc76b351bbf57691dcf73f739` |
| Windows `build-manifest.json` | 503 | `7399068ebed0bdf8bfa530d3985f65a8a4c1af5b5cc8ae1d2d015e280fda8f59` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `491be4ae2df755a3f7768ccc62605f26a63e8638462129a46a2e091b9ad6d5c7` |
| Windows `Simulation.Core.dll` | 1,401,856 | `2e933ef4cf5a77a5f23521003ceb7585cfc3f601d090f62c5f2ea4bf810a93e5` |

The upload action emitted a non-failing Node.js action deprecation warning that `actions/upload-artifact@v4` targets deprecated Node.js 20 and was forced to run on Node.js 24. It did not affect either job conclusion or artifact integrity.

## Decision

SP-04F6 criterion F613 passes at exact SHA `7e96930d103081e981c0cf1a06736223e222bc07`. F6 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/999-candidate fixture remains raw component evidence and does not establish the full-SP-04 three-second campaign-turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and the next dependency-ordered succession package may begin.
