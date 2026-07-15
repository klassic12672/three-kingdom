# SP-04B Exact-SHA Hosted Evidence — `ff7420f`

| Field | Value |
|---|---|
| Evidence date | 2026-07-15 |
| Target milestone | M2 — 191 campaign slice |
| Target plan/package | SP-04 — Characters, family, marriage, and succession; SP-04B-H hosted evidence for SP-04B-L |
| Exact commit | `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a` |
| Parent commit | `3b3202eb11b9c6a3bdb9434e7744ccb5c14c52d1` |
| Commit tree | `b3639a61a37a4568364022ac1fe1b1a763938498` |
| Commit subject | `Enhance SaveStoreTests and TierAndSoakTests for Schema 5 and Relationships` |
| Approved parent-to-commit binary-diff SHA-256 | `5193499e2ab385f2b7adfe0ebd59411766a32655350c61eb01ff8cb194d1aa95` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29394613777](https://github.com/klassic12672/three-kingdom/actions/runs/29394613777), attempt 1 |
| Overall result | **Pass — SP-04B criterion B15 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Purpose and boundary

This report records exact-SHA hosted macOS arm64 and Windows x64 evidence for the already locally verified SP-04B-L relationship/memory kernel. It covers clean checkout with LFS, repository/content validation, Release build and complete tests, Godot import, native development export, automated smoke, build manifests, artifact upload, archive integrity, and static artifact inspection.

This package changes no implementation, test, workflow, script, dependency, schema, checksum, artifact, or performance threshold. It does not relabel the recorded local Apple Silicon wall-clock measurements as hosted performance. It also does not establish physical Windows behavior, packaged-save hardware testing, signing, release readiness, full SP-04 completion, SP-05 unblocking, or M2 completion.

## Candidate, remote guard, and push

| Check | Result | Evidence |
|---|---|---|
| Immutable local identity | Pass | Branch `main`; `HEAD`, parent, tree, subject, and binary-diff SHA-256 exactly matched the approved identities above. |
| Clean and safe candidate | Pass | Worktree and index were clean; `git diff --check HEAD^ HEAD`, `git lfs fsck`, the introduced-path/secret/machine-path scan, and `./scripts/validate.sh` passed. Validation reported 1,295 records, 2,820 translations, and registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; the tree remained clean afterward. |
| Origin identity | Pass | Configured `origin` exactly matched the approved URL. |
| Pre-push remote boundary | Pass | A fresh `git ls-remote` showed `origin/main` at the exact approved parent. |
| Push operation | Pass | One normal non-force remote update fast-forwarded the exact target commit to `refs/heads/main`; no branch, tag, pull request, merge, amend, force push, release, or workflow dispatch occurred. An earlier malformed local refspec invocation was rejected before any remote update; a fresh guard confirmed the remote was still at the approved parent before the transmitted push. |
| Post-push remote identity | Pass | Fresh `git ls-remote` and the GitHub ref API both resolved `refs/heads/main` to the exact target SHA. |

## Hosted run and jobs

Run 29394613777 was triggered by `push` on `main`, used attempt 1, and had the exact target `head_sha`. It started at `2026-07-15T06:34:34Z` and completed successfully at `2026-07-15T06:38:28Z`. No rerun, cancellation, or manual dispatch occurred.

| Platform | Job | Runner | Started / completed | Conclusion |
|---|---|---|---|---|
| macOS arm64 | [87285167104](https://github.com/klassic12672/three-kingdom/actions/runs/29394613777/job/87285167104), `macOS arm64 build, export, and smoke` | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native `arm64` assertion passed | `06:34:38Z` / `06:37:14Z` | **Success** |
| Windows x64 | [87285167156](https://github.com/klassic12672/three-kingdom/actions/runs/29394613777/job/87285167156), `Windows x64 build, export, and smoke` | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1`; amd64 runtime and x86-64 export inspection | `06:34:37Z` / `06:38:27Z` | **Success** |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, export templates `4.6.1.stable.mono`, and Git LFS `3.7.1`. Checkout logged `lfs: true`, fetched LFS objects, and logged the exact target SHA after checkout.

The macOS job reached `./scripts/ci/install-godot.sh`, the native arm64 assertion, `./scripts/test.sh Release`, `./scripts/import.sh`, `./scripts/export.sh macos development && ./scripts/smoke.sh macos`, and artifact upload. The Windows job reached the corresponding PowerShell install, test, import, `./scripts/export.ps1 -Platform windows -Flavor development; ./scripts/smoke.ps1`, and artifact-upload commands.

## Per-platform results and test counts

| Stage | macOS arm64 | Windows x64 |
|---|---|---|
| Exact checkout and LFS | Pass; exact SHA logged and LFS fetched/materialized | Pass; same |
| Repository/content validation | Pass twice; 1,295 records, 2,820 translations, registry checksum below | Pass twice; same |
| Release build | Pass twice; zero warnings and zero errors | Pass twice; zero warnings and zero errors |
| `Simulation.Core.Tests` | 158 passed, zero failed/skipped per pass | Same |
| `Game.Content.Tests` | 66 passed, zero failed/skipped per pass | Same |
| `Game.Application.Tests` | 6 passed, zero failed/skipped per pass | Same |
| `Repository.Tests` | 18 passed, zero failed/skipped per pass | Same |
| Complete tests | 248 per pass; export repeated the gate for 496 successful executions | Same |
| Headless Godot import | Pass | Pass |
| Native development export | Pass; Mach-O 64-bit arm64 | Pass; PE32+ x86-64 |
| Automated smoke | Pass; manifest/checksum markers logged and clean exit | Same |
| Artifact upload | Pass; 206 files | Pass; 202 files |

The exact unfiltered suites provide the SP-04B-L relationship and memory contract, command/event causality, revalidation/cancellation, actor-authority, stable-ID, snapshot, checksum, JSON, and bounded-history coverage. The five observer-query tests cover the publicity matrix, decay, subject-only archive/distant history, existing-observer rules, empty self-summary, and defensive copies; the sixth Game.Application test records the local-only large-fixture measurements without a hosted wall-clock assertion. `SaveStoreTests` cover schema-5 round trips and required shape, authenticated schema-4 to schema-5 migration, the complete schema-1 through schema-5 chain, malformed relationship recovery, byte preservation, and rejected unexpected legacy relationship data.

## Checksum provenance

| Identity | Value | Provenance |
|---|---|---|
| Content manifest | `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef` | Directly logged in both smoke manifests and read from both downloaded sidecars. |
| Content registry | `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` | Directly logged by validation on both jobs and in both smoke manifests/sidecars. |
| Ten-year/1,000-entity soak | `8430e2054d15fdb9a6e0c54a88b20de3b34dbca7ba80030b30676041773e7155` | Not printed by CI. Established by the exact-source golden assertion in `TierAndSoakTests` plus both complete 158-test hosted Simulation.Core passes. |

## Artifact and manifest inspection

Both unsigned development artifacts were downloaded through the authenticated GitHub Actions API into temporary storage outside the repository. Each ZIP passed `unzip -tq`; its locally recomputed SHA-256 matched the Actions upload log and GitHub artifact API digest.

| Platform | Artifact | ID | Created / expires | Size | API and local ZIP SHA-256 |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29394613777/artifacts/8334612828) | 8334612828 | `2026-07-15T06:37:06Z` / `2026-10-13T06:34:34Z` | 67,723,665 | `195a5c056037ce638bc481586e2409073b3fa361e87b494f092088e858c30cdc` |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29394613777/artifacts/8334635243) | 8334635243 | `2026-07-15T06:38:19Z` / `2026-10-13T06:34:34Z` | 73,042,574 | `d55e0161a936315f0cc3ed522568a4d5b6ada7e5c5267281793833e9f0cd86ab` |

Relevant extracted inventory:

| Platform | File | Bytes | SHA-256 |
|---|---|---:|---|
| macOS | `build-manifest.json` | 488 | `23928d0639a310aff91f1838b8fe9a84a5fa238bd6fa87e8250d87153e79d18d` |
| macOS | `ThreeKingdom.command` | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS | application executable, Mach-O 64-bit arm64 | 95,978,608 | `edce4c5b7f8b141c6a3f94d12cff486c0ac8b07c756ffd33d2b76f783cebd6ab` |
| macOS | application PCK | 1,624,060 | `0233383f65c338bdba5f66d95751b08832e7dbd82f86d8cb78c907f2dbb40186` |
| Windows | `build-manifest.json` | 503 | `5b732be844273d06c412030b9a1147dbd0b20264d82b7799caada2d45ac15aa1` |
| Windows | `ThreeKingdom.exe`, PE32+ GUI x86-64 | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows | `ThreeKingdom.console.exe`, PE32+ console x86-64 | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows | `ThreeKingdom.pck` | 1,678,620 | `9886df1f7258fe33ba62832980f418a1d9e5cabbe06cc82dccedb991b00126e5` |

Both sidecars use build-manifest schema 2 and project version `0.1.0`; record the exact target Git SHA, pinned Godot and .NET versions, `Development` configuration, expected content checksums, and `macos`/`arm64` or `windows`/`x86_64` respectively. Each exact sidecar byte sequence occurs once in its PCK at byte offset 112, establishing embedded-versus-sidecar byte identity.

Both smoke runs logged `BUILD_MANIFEST`, `GEOGRAPHY_CHECKSUM`, `MAP_MODE_TIMING`, and the platform launch-success marker. The smoke scripts fail on nonzero exit, so the successful workflow steps establish a clean exit. The logged map-mode timings are uncontrolled hosted smoke markers, not SP-04B-H performance evidence.

The only workflow annotations were one non-failing Node.js 20 deprecation warning per artifact-upload job because GitHub forced `actions/upload-artifact@v4` onto Node.js 24. Upload logs also contained non-failing Node deprecation notices. They did not alter the successful job conclusions or artifact identities.

## Stage classification and B15 decision

| Stage | Classification |
|---|---|
| Local identity, clean-tree, diff, LFS, safety, and origin guards | **Pass** |
| Remote parent guard, normal fast-forward push, and post-push exact ref | **Pass** |
| Exact-SHA push-event run identity and both required jobs | **Pass** |
| Validation, build, complete tests, import, native export, and automated smoke on both hosted platforms | **Pass** |
| Artifact upload/API identity, download, local digest, archive integrity, executable architecture, manifest fields, and embedded byte identity | **Pass** |
| Hosted SP-04B wall-clock performance acceptance | **Unavailable / not required**; local Apple Silicon measurements remain local evidence |
| Physical Windows smoke, input/display, packaged-save, and representative playtesting | **Unavailable / deferred to M4** |
| Developer ID signing/notarization, Authenticode, Steam, clean install/update, and release-candidate certification | **Unavailable / deferred to SP-15** |

**B15 decision: Pass.** The same accepted revision, `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a`, passed hosted macOS arm64 and Windows x64 verification with the required artifact inspections. This supports only SP-04B criterion B15. B16 and later SP-04 systems remain deferred; all full SP-04 acceptance criteria remain unchecked; SP-04 and M2 remain Active; SP-05 remains blocked. The historical SP-04A evidence, its disqualified pre-remediation result, and its schema-1/2 field-compatibility disclosure remain unchanged.
