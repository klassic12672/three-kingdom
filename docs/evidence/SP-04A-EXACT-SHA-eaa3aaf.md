# SP-04A Exact-SHA Hosted Evidence — `eaa3aaf`

| Field | Value |
|---|---|
| Evidence date | 2026-07-15 |
| Target milestone | M2 — 191 campaign slice |
| Target plan/package | SP-04 — Characters, family, marriage, and succession; SP-04A foundations |
| Exact commit | `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea` |
| Parent commit | `4e6e83cb5a8f70b33e109d84782ef16681bd6e20` |
| Commit tree | `5df587b63ee16bed267a562de32cd5eb13b6b2e6` |
| Commit subject | `feat: add SP-04A character foundations and save schema 4` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29384539707](https://github.com/klassic12672/three-kingdom/actions/runs/29384539707), attempt 1 |
| Overall result | **Pass — SP-04A exact-SHA hosted macOS/Windows evidence is supported; full SP-04 and M2 remain Active** |

## Purpose and scope

This report records clean-checkout hosted macOS arm64 and Windows x64 validation for the accepted SP-04A revision above, plus independent inspection of both unsigned development artifacts. The jobs establish exact checkout with LFS, repository/content validation, Release build and complete tests, Godot import, native development export, automated smoke, build manifests, and artifact upload on both hosted release-target architectures.

This evidence does not relabel the earlier working-tree checks as clean-checkout evidence. In particular, the disqualifying pre-remediation `GeographyTests.PathfindingUsesRoutesOnlyAndMeetsInteractionBudget` failure at the unchanged `<50 ms` threshold remains historical failure evidence. No workflow was rerun, no gate was changed, and the signed release workflow was not triggered.

## Push and run identity

| Check | Result | Evidence |
|---|---|---|
| Local candidate | Pass | Clean `main` checkout at the exact commit above; parent-to-commit diff SHA-256 `6c3044d2c84e6d0d1f36beaaeca521cb9620e1444988ff9a6cf130322c3a51a9`. |
| Pre-push remote boundary | Pass | Fresh `git ls-remote` showed `origin/main` at the exact parent above. |
| Push operation | Pass | Normal non-force push of the exact commit to `refs/heads/main`; no branch, tag, pull request, merge, amend, or release operation. |
| Post-push remote identity | Pass | Both `git ls-remote` and the GitHub ref API returned the exact commit above. |
| Hosted run identity | Pass | Push event on `main`, attempt 1, exact `headSha`, status `completed`, conclusion `success`. |
| Hosted run timing | Pass | Started `2026-07-15T02:37:28Z`; API update completed `2026-07-15T02:40:32Z`. |

## Hosted jobs

| Platform | Job | Runner | Started / completed | Conclusion |
|---|---|---|---|---|
| macOS arm64 | [87254962501](https://github.com/klassic12672/three-kingdom/actions/runs/29384539707/job/87254962501), `macOS arm64 build, export, and smoke` | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion passed | `02:37:34Z` / `02:39:47Z` | **Success** |
| Windows x64 | [87254962519](https://github.com/klassic12672/three-kingdom/actions/runs/29384539707/job/87254962519), `Windows x64 build, export, and smoke` | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1`; x86-64 export inspected | `02:37:32Z` / `02:40:31Z` | **Success** |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`.

### Per-platform results

| Stage | macOS arm64 | Windows x64 |
|---|---|---|
| Exact checkout and LFS | Pass; exact SHA logged, clean checkout, `lfs: true` | Same |
| Repository/content validation | Pass; 1,295 records, 2,820 translations, registry checksum below | Same |
| Release build | Pass; zero warnings and zero errors | Same |
| `Game.Content.Tests` | 66 passed, zero failed/skipped per pass | Same |
| `Simulation.Core.Tests` | 107 passed, zero failed/skipped per pass | Same |
| `Repository.Tests` | 18 passed, zero failed/skipped per pass | Same |
| Complete tests | 191 per pass; export repeated the gate for 382 successful test executions | Same |
| Headless Godot import | Pass | Pass |
| Native development export | Pass; Mach-O 64-bit arm64 application | Pass; PE32+ x86-64 executables |
| Automated smoke | Pass; exact embedded manifest/checksum logged and native launch succeeded | Same |
| Artifact upload | Pass; 206 files | Pass; 202 files |

The only workflow annotation was GitHub's non-failing Node.js 20 deprecation warning for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. It did not alter either successful job result.

## Checksums and manifests

Both smoke logs and both downloaded sidecars recorded:

| Field | macOS arm64 | Windows x64 |
|---|---|---|
| Manifest schema / project | `2` / `0.1.0` | Same |
| Git commit | Exact `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea` | Same |
| Godot / .NET | `4.6.1.stable.mono.official.14d19694e` / `10.0.301` | Same |
| Platform / architecture | `macos` / `arm64` | `windows` / `x86_64` |
| Configuration | `Development` | Same |
| Content-manifest checksum | `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef` | Same |
| Content-registry/geography checksum | `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` | Same |
| Sidecar manifest SHA-256 | `c2d65e446f78f3b5f52213b42f2a377ab114dd1e185161bf22f211294d0bbc59` | `e48bbc022bebe463aa01144b41cadb5cf7bceb1e364daefa59a379cfc2a54b69` |

The source top-level content-manifest checksum matched both build manifests. Each sidecar's exact 488-byte or 503-byte sequence occurred once in its platform PCK at byte offset 112, so the embedded and sidecar manifests are byte-identical. Neither manifest contains `uncommitted` or another source revision.

## Artifact inspection

Both artifacts were downloaded through the authenticated GitHub Actions API into temporary storage outside the repository. Each ZIP passed `unzip -tq`, and its locally recomputed SHA-256 exactly matched both the Actions upload log and GitHub artifact API digest.

| Platform | Artifact | ID | Created / expires | Size | SHA-256 |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29384539707/artifacts/8330956943) | 8330956943 | `2026-07-15T02:39:39Z` / `2026-10-13T02:37:28Z` | 67,683,100 | `2256359e5a469f5a45f4f61dcd13859dc8007fc9bbcd0985177a8879bd1a9ac1` |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29384539707/artifacts/8330965775) | 8330965775 | `2026-07-15T02:40:20Z` / `2026-10-13T02:37:28Z` | 73,002,008 | `fc07eca4f5099c3ecf4b2721ec00b49d49b8ff2e5d59f9d3302cf23183812935` |

Relevant extracted inventory:

| Platform | File | Bytes | SHA-256 |
|---|---|---:|---|
| macOS | `build-manifest.json` | 488 | `c2d65e446f78f3b5f52213b42f2a377ab114dd1e185161bf22f211294d0bbc59` |
| macOS | `ThreeKingdom.command` | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS | application executable, Mach-O 64-bit arm64 | 95,978,608 | `4eb843554aa0dc4923bf08c49275db8e5fe5514c59db53ba154a8b373b8c93e8` |
| macOS | application PCK | 1,624,060 | `e34fe16e2d34e9ee51b0cb16fdc51ae5637d72c3f4f2c35d86bbb73890f13f51` |
| Windows | `build-manifest.json` | 503 | `e48bbc022bebe463aa01144b41cadb5cf7bceb1e364daefa59a379cfc2a54b69` |
| Windows | `ThreeKingdom.exe`, PE32+ x86-64 | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows | `ThreeKingdom.console.exe`, PE32+ x86-64 | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows | `ThreeKingdom.pck` | 1,678,620 | `9a664a5e8dec0dbd5b380c2074f6ce44be3f04dc6fef873a704c26a76b89b082` |

## Gate assessment and limitations

The exact accepted revision supplies SP-04A criterion A13: the same SHA passed hosted macOS arm64 and Windows x64 validation, build, complete tests, import, export, automated smoke, manifest inspection, and artifact upload. Combined with the preserved local evidence for A01–A12, this supports SP-04A's bounded package closeout.

This does not mark full SP-04 or M2 complete. Real schema-1/2 field compatibility remains unverified; physical Windows x64 smoke/input/display/packaged-save/playtesting remains an M4 gate; signing, notarization, Authenticode, Steam, install/update, and release-candidate evidence remains an SP-15 gate. Relationships, memories, marriage, succession, bounded history, battle integration, historical rosters, presentation, and player-knowledge-filtered queries remain later SP-04 work. The artifacts are unsigned expiring development builds and are not release-ready.
