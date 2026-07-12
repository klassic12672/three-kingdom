# M0/M1 Exact-SHA Evidence — `7f62a97`

| Field | Value |
|---|---|
| Evidence date | 2026-07-12 |
| Target milestones | M0 — Source of truth and toolchain; M1 — Headless simulation foundation |
| Target plans | SP-00, SP-01, and SP-02 |
| Exact commit | `7f62a97cf880ae6ded8e47af8737a11e53479977` |
| Commit subject | `Fix Windows CI repository cleanup` |
| Approved origin | `https://github.com/klassic12672/three-kingdom.git` |
| Hosted run | [CI run 29180111376](https://github.com/klassic12672/three-kingdom/actions/runs/29180111376) |
| Overall result | **Pass — revised M0 and complete M1 gates are supported; M2/SP-03 may activate** |

## Purpose and scope

This report records auditable hosted macOS arm64 and Windows x64 evidence for the exact cleanup-fix revision above, plus independent inspection of both unsigned development artifacts. It assesses revised M0 under [ADR-0001](../adr/0001-mac-first-development-deferred-physical-windows-verification.md) and the SP-01/SP-02 evidence needed to close M1.

The local working tree already contained unstaged documentation changes during collection. Those changes do not affect the immutable hosted run or artifacts and are not represented as clean-checkout evidence. No file was staged or committed, no workflow was rerun, no signing credential was used, and nothing was published.

## Preflight

| Check | Result | Evidence |
|---|---|---|
| Local `HEAD` and branch | Pass | Exact SHA above on `main`. |
| Working tree | Qualified | Dirty with pre-existing documentation changes; hosted evidence remains exact-SHA evidence. |
| Configured origin | Pass | Fetch and push URL matched the approved origin. |
| Fresh remote `main` query | Pass | `git ls-remote` returned the exact SHA above. |
| Commit metadata | Pass | Tree `af6162599d9e3cafda04edc4932ee47f41be9f85`; parent `1ab375a5e812c14eba4eca4cc121e604ade73f47`; author/committer time `2026-07-12T13:42:45+09:00`. |
| Hosted run identity | Pass | `push` event on `main`, attempt 1, exact head SHA, status `completed`, conclusion `success`. |
| Hosted run timing | Pass | Started `2026-07-12T04:42:49Z`; completed by API update `2026-07-12T04:45:30Z`. |

The connected GitHub integration supplied job logs and artifact downloads. Public GitHub Actions API metadata independently supplied run/job timestamps and URLs.

## Hosted CI jobs

| Platform | Job | Runner | Started / completed | Conclusion |
|---|---|---|---|---|
| macOS arm64 | [86616090942](https://github.com/klassic12672/three-kingdom/actions/runs/29180111376/job/86616090942), `macOS arm64 build, export, and smoke` | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion passed | `04:42:53Z` / `04:44:47Z` | **Success** |
| Windows x64 | [86616090950](https://github.com/klassic12672/three-kingdom/actions/runs/29180111376/job/86616090950), `Windows x64 build, export, and smoke` | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1`; Windows x64 export target | `04:42:52Z` / `04:45:30Z` | **Success** |

Both jobs used runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching `4.6.1` templates, and Git LFS.

### Step results

| Stage | macOS arm64 | Windows x64 |
|---|---|---|
| Checkout exact revision | Success | Success |
| Validate, restore, build, test | Success; 0 build warnings/errors | Success; 0 build warnings/errors |
| Headless Godot import | Success | Success |
| Development export | macOS arm64 app produced | Windows x64 executable produced |
| Automated native smoke | Embedded manifest/geography markers and clean success | Embedded manifest/geography markers and clean success |
| Artifact upload | Success; 206 files | Success; 202 files |

The macOS import log emitted a nonfatal Godot `EditorSettings` Android SDK-path error after import completed. The step remained successful and export/smoke passed; this is disclosed as a limitation.

### Tests and checksums

Each job ran the full suite in its test step and again because export repeats the Release gate:

| Assembly | macOS per pass | Windows per pass |
|---|---:|---:|
| `Game.Content.Tests` | 31 passed, 0 failed/skipped | 31 passed, 0 failed/skipped |
| `Simulation.Core.Tests` | 52 passed, 0 failed/skipped | 52 passed, 0 failed/skipped |
| `Repository.Tests` | 18 passed, 0 failed/skipped | 18 passed, 0 failed/skipped |
| Total | 101 per pass; 202 executions/job | 101 per pass; 202 executions/job |

| Evidence | macOS arm64 | Windows x64 | Assessment |
|---|---|---|---|
| SP-01 ten-year/1,000-entity checksum | `105da5fd449cc2d00ba1bf979642b22107db5b236eab30baac437f1b9b8bf088` | Same | Pass, assertion-derived |
| Canonical content-pack checksum | `6c527133073ffece29d4d75f7372cc783f2855f6354ed5be9eb1a6c971936449` | Same | Pass, directly logged |
| SP-02 aggregate registry checksum | `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d` | Same | Pass, directly logged |
| Geography smoke checksum | `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d` | Same | Pass, directly logged |
| Applicable content diagnostics | 0 | 0 | Pass; validator reported 32 valid records and 70 translations |

The simulation checksum literal is not printed by xUnit. At this exact source, `TenYearThousandEntitySoak_CompletesWithoutInvariantFailure` runs ten years with 1,000 entities and seed `20260712`, then asserts that checksum. Both platforms passed all 52 simulation tests twice. A local exact-HEAD operator run independently produced 1,044 turns, final date `0200-01-03`, and the same checksum. The cross-platform claim is thus exact-source assertion plus complete hosted passes, not a direct hosted checksum line.

The simulation suite also covers command/event-only mutation, deterministic ordering and random streams, deterministic background commits, tier conservation, save/load, interrupted-write/corrupt-save recovery, migrations, failed-migration preservation, and contract compatibility. The content suite covers schemas/validation, Korean/English coverage, deterministic built-in/mod order and overrides, conflict handling, invalid-pack isolation, provenance/source requirements, save compatibility, and absence of runtime-code mods.

## Artifact inspection

Both artifacts were downloaded through the GitHub integration into a temporary directory outside the repository, hashed as received, extracted, and inventoried.

| Platform | Artifact | ID | Created / expires | Size | GitHub digest | Local archive SHA-256 |
|---|---|---:|---|---:|---|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29180111376/artifacts/8256131824) | 8256131824 | `2026-07-12T04:44:43Z` / `2026-10-10T04:42:49Z` | 67,528,514 | `7ec657f8e89f6cea1a6bbc4feb54292849aba109d61ea5c20c2a85b622ecd55f` | Same |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29180111376/artifacts/8256135950) | 8256135950 | `2026-07-12T04:45:22Z` / `2026-10-10T04:42:49Z` | 72,848,272 | `4b8116511be3b3f869b7ef3658b853d07d355f0696bc48aba6c9fd19f606bd2c` | Same |

The downloaded ZIP bytes matched GitHub's digest for both artifacts; no alternate archive representation was observed.

### Relevant inventory

| Platform | File | Bytes | SHA-256 |
|---|---|---:|---|
| macOS | `build-manifest.json` sidecar | 488 | `f9b251be3e3fb55f1b63891db0df429fb1ab030e7049516ee8a9a835e8469462` |
| macOS | `ThreeKingdom.command` launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS | app executable, Mach-O arm64 | 95,978,608 | `0cc0b534be511821810cbf2a87edd88fac8f52f4167e9dbe8e315466bb4df44e` |
| macOS | app PCK | 52,012 | `7f78cfa34663e8c2c65fd20ce1ef7a2d86c624f69bfe7fb47067944c9793e460` |
| Windows | `build-manifest.json` sidecar | 503 | `9d86a47c0038fc605d679b44f29906df2dc5abe9636adc669ee3283dc483a2b1` |
| Windows | `ThreeKingdom.exe`, PE32+ x86-64 | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows | `ThreeKingdom.console.exe`, PE32+ x86-64 | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows | `ThreeKingdom.pck` | 53,724 | `5d8959bb43e9589fac1070dc34f412e0ade1195c2917ca51a98aec6ec86d0596` |

### Embedded and sidecar manifests

Each PCK contains `res://generated/build-manifest.json`. Hosted native smoke emitted that embedded file as `BUILD_MANIFEST`; string inspection of each extracted PCK located the same manifest. Sorted-key compact hosted JSON matched sorted-key compact sidecar JSON byte-for-byte.

| Platform | Comparison | Canonical manifest SHA-256 |
|---|---|---|
| macOS arm64 | Match | `e519116304f95a2de97f808fd3551ec6d6f4629e179f127e9d32946023a8a4a0` |
| Windows x64 | Match | `27beee4c4ca8ef2957d82423d68cf3dca4c0b782750f5a04e3072aadf74917f6` |

The downloaded macOS app was additionally copied to temporary staging, stripped of quarantine metadata, ad-hoc signed for local execution only, and launched natively. It emitted the same manifest/checksum and exited cleanly. This is not Developer ID signing evidence.

| Field | macOS | Windows |
|---|---|---|
| Schema / project | `2` / `0.1.0` | Same |
| Git commit | Exact `7f62a97…` | Exact `7f62a97…` |
| Godot / .NET | `4.6.1.stable.mono.official.14d19694e` / `10.0.301` | Same |
| Platform / architecture | `macos` / `arm64` | `windows` / `x86_64` |
| Configuration | `Development` | `Development` |
| Content checksums | Expected values above | Expected values above |

The Windows workflow/preset/artifact/PE identify Windows x64; its tested manifest literal is `x86_64`, the project's x64 synonym. Neither manifest contains `uncommitted` or another SHA.

## Gate assessment

### M0 and SP-00

Previously checked documentation, pinned-tool, LFS, export-preset, native development-Mac smoke, fail-closed workflow, provenance-register, and credential/path criteria remain supported by earlier evidence. This exact revision supplies the missing successful clean-checkout hosted macOS/Windows validation, build, test, import, export, automated smoke, manifests, and artifacts.

Result: **SP-00 Complete; M0 Complete.**

### M1, SP-01, and SP-02

SP-01's complete suite passed on both platforms, including soak, save/load, migrations, deterministic ordering, tier conservation, and compatibility. SP-02's complete suite and content validation passed on both platforms with the same registry/load-order checksum and zero applicable diagnostics. Closing SP-00/SP-01 removes their dependency blocks.

Result: **SP-01 Complete; SP-02 Complete; M1 Complete. M2 and SP-03 may become Active.**

## Explicitly deferred evidence

ADR-0001 leaves physical Windows smoke/input/display/save/playtesting at M4, and Developer ID signing/notarization, Authenticode, Steam, install/update, and release certification at SP-15. This report also does not claim SP-03 rendering, modes, picking, route highlighting, localization, label overlap, or interactive visual acceptance.

## Limitations

- The local documentation tree was dirty and is not clean-checkout evidence.
- The SP-01 checksum is assertion-derived, not directly printed in hosted logs.
- The macOS import contains the nonfatal Android SDK-path message noted above.
- The Windows manifest literal is `x86_64`; plan language uses `x64`.
- Artifacts are unsigned development builds and expire on `2026-10-10`; recorded hashes preserve their audit identity.

## Relationship to the historical `1ab375a` report

[M0-EXACT-SHA-1ab375a.md](M0-EXACT-SHA-1ab375a.md) remains the failed-baseline account: macOS passed, while Windows failed before import, export, smoke, and upload. No `7f62a97` result is applied retroactively. This separate report records the cleanup fix and first complete passing same-SHA hosted artifact set.
