# SP-03 Exact-SHA Hosted Evidence — `f91dfce`

| Field | Value |
|---|---|
| Evidence date | 2026-07-14 |
| Target milestone | M2 — 191 campaign slice |
| Target plan | SP-03 — Campaign map, regions, routes, and supply |
| Exact commit | `f91dfce730f1e116bd17321e8a0a654a69823c69` |
| Commit tree | `ec6590defe79e5abc9083442254ba353fa07a098` |
| Commit subject | `Implement SP-03 campaign geography and map presentation` |
| Approved origin | `https://github.com/klassic12672/three-kingdom.git` |
| Hosted run | [CI run 29297543256](https://github.com/klassic12672/three-kingdom/actions/runs/29297543256) |
| Overall result | **Pass — SP-03 closeout is supported; M2 remains Active** |

## Purpose and scope

This report records exact-source hosted macOS arm64 and Windows x64 validation for the accepted SP-03 implementation revision, plus independent inspection of both unsigned development artifacts. It closes the accepted-revision gap left by the historical [Later Han working-tree report](SP-03-later-han-map-working-tree-2026-07-13/README.md).

The hosted jobs establish clean-checkout LFS materialization, validation, build, complete tests, Godot import, native development export, automated smoke, manifests, and artifacts on both release-target architectures. The historical report remains the source of local Apple Silicon visual, interaction, performance, and product-acceptance evidence. Hosted automated smoke is not presented as hosted visual or physical-Windows evidence.

## Preflight and push

| Check | Result | Evidence |
|---|---|---|
| Local revision | Pass | `HEAD` and `main` were exactly the commit above; the worktree was clean. |
| Configured origin | Pass | Fetch and push URL matched the approved origin above. |
| Fresh remote state | Pass | After `git fetch --no-tags origin main`, `origin/main` was direct parent `7941ced08bb5f7e55e499715aaad23615881a742`; ahead/behind was `1/0`. |
| Fast-forward boundary | Pass | `git merge-base --is-ancestor origin/main f91dfce...` passed. No force option, amend, replacement commit, remote change, tag, release, or pull request was used. |
| LFS integrity | Pass | Git LFS `3.7.1`; `git lfs fsck` passed, every exact-revision LFS pointer/object was present, and the push uploaded 18/18 objects. |
| Repository-material audit | Pass | Tracked filename/content checks and `./scripts/validate.sh` found no secret, signing material, machine-local path, or unintended generated output. The validator reported 1,295 records, 2,820 translations, and zero diagnostics. |

The authorized push used an explicit normal refspec for the immutable implementation commit:

```sh
git push origin f91dfce730f1e116bd17321e8a0a654a69823c69:refs/heads/main
```

It advanced `origin/main` from `7941ced08bb5f7e55e499715aaad23615881a742` to the exact implementation SHA.

## Hosted run identity

Run `29297543256`, attempt 1, was triggered by a `push` to `main`. It started at `2026-07-14T01:02:29Z`, completed at `2026-07-14T01:05:16Z`, and concluded `success` with head SHA `f91dfce730f1e116bd17321e8a0a654a69823c69`.

| Platform | Job | Runner | Started / completed | Conclusion |
|---|---|---|---|---|
| macOS arm64 | [86974220905](https://github.com/klassic12672/three-kingdom/actions/runs/29297543256/job/86974220905), `macOS arm64 build, export, and smoke` | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion and Mach-O inspection passed | `01:02:33Z` / `01:04:16Z` | **Success** |
| Windows x64 | [86974220894](https://github.com/klassic12672/three-kingdom/actions/runs/29297543256/job/86974220894), `Windows x64 build, export, and smoke` | Windows Server 2025 Datacenter `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1`; x86-64 manifest and PE inspection passed | `01:02:32Z` / `01:05:16Z` | **Success** |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`.

## Per-platform results

| Stage | macOS arm64 | Windows x64 |
|---|---|---|
| Exact checkout and LFS | Pass; exact SHA logged, `lfs: true`, Darwin arm64 LFS fetch succeeded | Pass; exact SHA logged, `lfs: true`, Windows amd64 LFS fetch succeeded |
| Content validation | Pass; 1,295 records, 2,820 translations, 1 pack, zero errors/warnings | Same |
| Release build | Pass; zero warnings and zero errors | Same |
| `Game.Content.Tests` | 37/37 passed, zero failed/skipped per pass | Same |
| `Simulation.Core.Tests` | 58/58 passed, zero failed/skipped per pass | Same |
| `Repository.Tests` | 18/18 passed, zero failed/skipped per pass | Same |
| Complete tests | 113/113 per pass; the export gate repeated the same suite, for 226 successful test executions | Same |
| Headless Godot import | Pass; filesystem, class, and layout imports completed | Pass; filesystem, class, and layout imports completed |
| Native development export | Pass; macOS arm64 application | Pass; Windows x64 executable |
| Automated smoke | Pass; logged build manifest and geography checksum, then launched successfully | Same |
| Artifact upload | Pass | Pass |

The macOS import emitted a nonfatal Android SDK `EditorSettings` diagnostic after the required import phases completed. The import step, export, smoke, upload, and job all remained successful.

## Artifact inspection

Both artifacts were downloaded through the authenticated repository access already used for the authorized push, into temporary storage outside the repository. No credential was printed or persisted. The downloaded ZIP bytes matched the SHA-256 digest reported by GitHub Actions.

| Platform | Artifact | ID | Created / expires | Size | SHA-256 |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29297543256/artifacts/8297334676) | 8297334676 | `2026-07-14T01:04:13Z` / `2026-10-12T01:02:29Z` | 67,642,823 | `bcfbca64d45bf468c370de2dc883645eb8d677b1b71480ad750beb1dc9660259` |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29297543256/artifacts/8297349231) | 8297349231 | `2026-07-14T01:05:12Z` / `2026-10-12T01:02:29Z` | 72,961,713 | `8d8d46022eb521f320e5c8770726115a996493cad12eeb5306cd83d8aabb19c8` |

Relevant extracted inventory:

| Platform | File | Bytes | SHA-256 |
|---|---|---:|---|
| macOS | `build-manifest.json` sidecar | 488 | `6c0af7a784ce5a1f083f23dbf4de747c6bd2db74bb8cf9dba8880d26f15587c5` |
| macOS | `ThreeKingdom.command` launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS | application executable, Mach-O 64-bit arm64 | 95,978,608 | `c10228153cdd8c38b1d3f43476588f92192e85969c726b1c2fe9b75207c87db6` |
| macOS | application PCK | 1,624,060 | `50edfce63e9eb3051bb8a6b1ed63836dc5e3cfc258eb95692ea96aa5a54ae3c0` |
| Windows | `build-manifest.json` sidecar | 503 | `e75835345d6eeb23cc10e894c1c592a0df443e9a834be92ecd2b87eb26f10ac8` |
| Windows | `ThreeKingdom.exe`, PE32+ x86-64 | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows | `ThreeKingdom.console.exe` | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows | `ThreeKingdom.pck` | 1,678,620 | `c6fffe7a968485d5005c054b4cbe062b3e7f4d81bc9da497a5e7b4764682bf2f` |

## Embedded and sidecar manifests

For each artifact, the exact sidecar byte sequence occurs once in the corresponding PCK at byte offset 112. This directly establishes that the embedded and sidecar manifests are byte-identical.

| Field | macOS arm64 | Windows x64 |
|---|---|---|
| Sidecar / embedded identity | Match; SHA-256 `6c0af7a784ce5a1f083f23dbf4de747c6bd2db74bb8cf9dba8880d26f15587c5` | Match; SHA-256 `e75835345d6eeb23cc10e894c1c592a0df443e9a834be92ecd2b87eb26f10ac8` |
| Schema / project | `2` / `0.1.0` | Same |
| Git commit | Exact `f91dfce730f1e116bd17321e8a0a654a69823c69` | Same |
| Godot / .NET | `4.6.1.stable.mono.official.14d19694e` / `10.0.301` | Same |
| Platform / architecture | `macos` / `arm64` | `windows` / `x86_64` |
| Configuration | `Development` | Same |
| Content-manifest checksum | `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef` | Same |
| Content-registry checksum | `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` | Same |

The Windows manifest literal `x86_64` is the project's x64 architecture value. Neither manifest contains `uncommitted` or another source revision.

## Reconciliation with working-tree evidence

The historical Later Han report was collected from a dirty tree based on parent commit `7941ced...`. Its accepted files, visual captures, content pack checksum `f6024dea...`, and registry/geography checksum `b04754a...` were then committed together as exact implementation revision `f91dfce...`. The hosted artifacts identify that exact revision and reproduce both expected content checksums on macOS arm64 and Windows x64.

This does not retroactively relabel the historical captures as clean-checkout or hosted evidence. It gives them an exact committed successor and confirms that the accepted content identity is present in both hosted native artifacts. The local report continues to establish visual, interaction, and performance acceptance; this report establishes accepted-revision hosted portability and artifact identity.

## Gate assessment

All eight SP-03 acceptance criteria were already covered by implementation, automated tests, and the accepted local presentation evidence. This exact revision supplies the remaining hosted macOS/Windows clean-checkout, import, export, automated-smoke, manifest, and artifact evidence.

Result: **SP-03 Complete. M2 remains Active. SP-04 package planning is next.**

Detailed cartographic refinement, broader navigation/play review, and replacement of explicitly low-confidence inferred anchors remain deferred pre–Early Access work rather than SP-03 closeout gates.

## Explicitly deferred evidence

- Physical Windows x64 smoke, input/display, packaged-save compatibility, and representative playtesting remain M4 gates under [ADR-0001](../adr/0001-mac-first-development-deferred-physical-windows-verification.md).
- Developer ID signing/notarization, Authenticode, Steam, clean-install/update, and release-candidate certification remain SP-15 gates.
- The eight provisional Korean readings remain non-release records pending human language review.
- The artifacts above are unsigned expiring development artifacts. They are not release-ready and were not published.
