# SP-04C0 Exact-SHA Hosted Evidence — `7d4612d`

| Field | Value |
|---|---|
| Evidence date | 2026-07-15 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04C0 — descriptor, agency, and typed kinship |
| Exact commit | `7d4612d21784ceebbcd574ea00231785b9408036` |
| Parent commit | `bad40d4c7d431480bde3fb313dec639dd7f3ac3a` |
| Commit tree | `e32c8d9c5324b300888419d67b2d5b87dcf3ead1` |
| Commit subject | `feat: add SP-04C0 character descriptors and typed kinship` |
| Parent-to-commit binary-diff SHA-256 | `77f1f01df363ec70808f41de6de200943bf6bdf0c78af0b5757d69a0d8a9aaf9` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29401495613](https://github.com/klassic12672/three-kingdom/actions/runs/29401495613), attempt 1 |
| Overall result | **Pass — SP-04C0 criterion C13 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the previously locally verified C0 package. It covers character-v2 descriptors, conditions, typed kinship, authored v1/v2 normalization, override lineage, schema-6 migration, validation, complete tests, Godot import, native development export, automated smoke, manifests, artifact upload, and static artifact inspection.

It does not establish later career/resource/marriage/lifecycle/succession packages, physical Windows behavior, signing, Steam, release readiness, or local wall-clock performance on hosted runners. Full SP-04 acceptance criteria remain unchecked.

## Candidate and remote identity

- The index contained exactly the audited 25-file C0 package. `git diff --cached --check`, `git lfs fsck`, and the credential/machine-path scan passed before commit.
- Exact-commit local `./scripts/validate.sh` and `./scripts/test.sh Release` passed before push.
- A fresh remote guard found `origin/main` at the exact approved parent. One normal non-force push advanced `refs/heads/main` from `bad40d4` to `7d4612d`; no branch, tag, pull request, merge, release, workflow dispatch, signing, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `7d4612d21784ceebbcd574ea00231785b9408036`.

## Hosted run and jobs

Run 29401495613 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T08:38:09Z` through `2026-07-15T08:41:34Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87306847695](https://github.com/klassic12672/three-kingdom/actions/runs/29401495613/job/87306847695) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `08:38:14Z` / `08:41:33Z` | Pass |
| Windows x64 | [87306847655](https://github.com/klassic12672/three-kingdom/actions/runs/29401495613/job/87306847655) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1` | `08:38:12Z` / `08:41:18Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true` and logged the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 204 passed | 204 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass | Pass |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source soak assertion plus both hosted 204-test Core passes establish checksum `37504979fcaa25789cc9e12af7084c351d115c1195054e062ca7f7ea6ba943dd`; CI does not print that checksum directly.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded through authenticated GitHub access and extracted successfully. File counts matched upload logs, manifests matched the smoke output, and executables matched the required architectures.

| Platform | Artifact | ID | API/upload ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29401495613/artifacts/8337294755) | 8337294755 | `476c74909402895069fbf3cb8186a82edab004d1379637b55b06173cd33bf515` | 67,751,175 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29401495613/artifacts/8337289531) | 8337289531 | `c645372f2305497b8b186da110d943e2df0cd963a08e0bc7153d546f52f92e7d` | 73,070,075 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `c3eff6239b8a04b5f7ca9353afa69052b2a08d50097b10a956b64ec0431b04e3` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `5d93aaca9dce3557e446bfb4c8a61f4b60aa31978bb1c475d6fb4d465fdf1e58` |
| macOS PCK | 1,624,060 | `a092176aad44384555c2d53398d32f94c465933c789210d4e4eef0400a60fbc8` |
| Windows `build-manifest.json` | 503 | `dca461f4fc24d92076ff48145463e7d8cfa36380d63b59fd4009735379f9cbe9` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `6b4889309052706f98955910b44a1ac9defea8ee1feee3687a90856333c78ead` |

The only workflow annotations were non-failing Node.js 20 deprecation warnings from `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. They did not affect job conclusions or artifacts.

## Decision

SP-04C0 criterion C13 passes at exact SHA `7d4612d21784ceebbcd574ea00231785b9408036`. C0 now has local and hosted macOS arm64/Windows x64 evidence. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and later SP-04 packages remain required.
