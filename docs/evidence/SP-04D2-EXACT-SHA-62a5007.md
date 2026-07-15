# SP-04D2 Exact-SHA Hosted Evidence — `62a5007`

| Field | Value |
|---|---|
| Evidence date | 2026-07-15 |
| Target milestone | M2 — 191 campaign slice |
| Target package | SP-04D2 — mutual-consent adult romance workflow |
| Exact commit | `62a50075ca86b3466cca9c05825d4374e6cac366` |
| Parent commit | `ad5138edd2f44b8f2256b2ecfb7f9962cc7f3858` |
| Commit tree | `a0ee69f21861df47293ca6ce9cf325d8d9fd81f2` |
| Commit subject | `feat: add SP-04D2 mutual-consent romance workflow` |
| Parent-to-commit diff SHA-256 | `aa1c73bb6814040a88e192c03ce2b895c3c6b4faf7bd0f0a2dd44cfde2d6358d` |
| Approved origin/ref | `https://github.com/klassic12672/three-kingdom.git`, `refs/heads/main` |
| Hosted run | [CI run 29425995954](https://github.com/klassic12672/three-kingdom/actions/runs/29425995954), attempt 1 |
| Overall result | **Pass — SP-04D2 criterion D214 is supported at the exact SHA; full SP-04 and M2 remain Active** |

## Boundary

This report records clean-checkout hosted macOS arm64 and Windows x64 evidence for the locally verified D2 package. It covers participant-issued adult non-explicit romance offer, recipient acceptance/refusal, initiator withdrawal, expected-level progression, completion, and either-participant ending; explicit bilateral consent; authoritative eligibility revalidation; stable event-order race handling; coercive-causality exclusion for positive actions; bounded invitation and route retention; legacy route progression; snapshot, checksum, pending replay, save/recovery, schema-12 current saves, authenticated schema-11-to-12 migration, historical schema-11 checksums, complete tests, Godot import, native development export, automated smoke, manifests, artifact upload, and static artifact inspection.

It does not establish romantic marriage proposals or unions, relationship or memory effects, household decisions or movement, conflict or coercion consequences, route invalidation from condition/lifecycle mutation, third-party authority, lifecycle, inheritance or succession resolution, faction/court/diplomacy integration, content, localization, scenes, UI, battle, AI, physical Windows behavior, signing, Steam, release readiness, or the full SP-04 three-second turn budget. Every full SP-04 acceptance criterion remains unchecked.

## Candidate and remote identity

- The audited D2 commit contains the mutual-consent romance action/outcome workflow, invitation and route-v2 causal state, legacy route behavior, schema-12 save compatibility, an exact-D1 schema-11 fixture, focused and compatibility tests, and same-package documentation.
- Local validation retained 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The complete 469/71/6/18 suites, zero-warning Release build, focused 296-test marriage/save/ten-year-soak slice, touched-project formatting, diff check, and LFS check passed before commit.
- The frozen 325,473-byte schema-11 fixture has file SHA-256 `ce6f737a9e3a608dfaaaeaf422f74e134a8fa7073ad4026a9aa1354007174d14` and stored historical checksum `9c5dc3195649bfde2626f95c7cf2573d4acbc4c2a081b9af0ac9d30c74f9c8fb` from exact D1 revision `653ce71d24bd81435ded9e65022dc29afd8f4810`.
- Independent architecture and adversarial verification identified and remediated coercive command reuse on legacy nonterminal advances, incomplete route-v2 last-positive-progress causality, uncontrolled duplicate historical system registration, and race tests that did not force both hashed-event orderings. Final re-review found no remaining correctness or package-boundary blocker.
- `origin/main` was the exact approved parent before one normal non-force push advanced it from `ad5138e` to `62a5007`. No branch, tag, pull request, merge, release, workflow dispatch, signing, Steam, or publishing action occurred.
- Fresh local, remote, workflow, manifest, and artifact identities all resolve to `62a50075ca86b3466cca9c05825d4374e6cac366`.

## Hosted run and jobs

Run 29425995954 was triggered by the authorized push to `main`, used attempt 1, and completed successfully from `2026-07-15T14:58:25Z` through `2026-07-15T15:04:19Z`.

| Platform | Job | Runner | Started / completed | Result |
|---|---|---|---|---|
| macOS arm64 | [87388419713](https://github.com/klassic12672/three-kingdom/actions/runs/29425995954/job/87388419713) | macOS 15.7.7 build 24G720; `macos-15-arm64` image `20260706.0213.1`; native arm64 assertion | `14:58:29Z` / `15:04:19Z` | Pass |
| Windows x64 | [87388419548](https://github.com/klassic12672/three-kingdom/actions/runs/29425995954/job/87388419548) | Windows Server 2025 `10.0.26100`; `windows-2025-vs2026` image `20260628.158.1` | `14:58:28Z` / `15:03:06Z` | Pass |

Both jobs used Actions runner `2.335.1`, .NET SDK `10.0.301`, Godot `4.6.1.stable.mono.official.14d19694e`, matching export templates, and Git LFS `3.7.1`. Checkout used `lfs: true`, a clean depth-one fetch, and the exact target SHA.

## Validation, tests, import, export, and smoke

| Stage | macOS arm64 | Windows x64 |
|---|---:|---:|
| Repository/content validation | 1,295 records, 2,820 translations, registry checksum below | Same |
| `Simulation.Core.Tests` | 469 passed | 469 passed |
| `Game.Content.Tests` | 71 passed | 71 passed |
| `Game.Application.Tests` | 6 passed | 6 passed |
| `Repository.Tests` | 18 passed | 18 passed |
| Build | Zero warnings/errors | Zero warnings/errors |
| Headless Godot import | Pass | Pass |
| Native development export | Mach-O 64-bit arm64 | PE32+ x86-64 |
| Automated smoke and clean exit | Pass | Pass |
| Artifact upload | 206 files | 202 files |

Export repeats the complete validation/build/test gate, so each hosted platform recorded two successful complete suite executions. The exact-source soak assertion plus both hosted 469-test Core passes establish checksum `ba4eccd512e7bf699c3360032f2a5f007b362cc16ff718a487a6d082357e65b2`; CI does not print that checksum directly.

Both smoke manifests record:

- content manifest checksum `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef`;
- content registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; and
- schema 2, project version `0.1.0`, exact Git SHA, pinned toolchain, `Development` configuration, and the expected platform/architecture.

## Artifact identities

Both artifacts were downloaded through authenticated GitHub access. The downloaded ZIP SHA-256 values exactly matched the immutable API/upload digests, extraction succeeded, file counts matched upload logs, manifests matched smoke output, and executables matched the required architectures.

| Platform | Artifact | ID | API/upload and downloaded ZIP SHA-256 | Size | Expires |
|---|---|---:|---|---:|---|
| macOS arm64 | [macos-arm64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29425995954/artifacts/8347439561) | 8347439561 | `ef57e301aa43889c9ca4a394bddb796236e8604e5ecbafde4a525ba7a79c3cdf` | 67,941,939 | 2026-10-13 |
| Windows x64 | [windows-x64-development-unsigned](https://github.com/klassic12672/three-kingdom/actions/runs/29425995954/artifacts/8347405039) | 8347405039 | `1572d172315dcc756d9efdf390484f092d415f6aacf01ba91904d6e0bb594b64` | 73,260,859 | 2026-10-13 |

| File | Bytes | SHA-256 |
|---|---:|---|
| macOS `build-manifest.json` | 488 | `e5d6b555b139f216514c3694d15d4c74d0a35c1bd46cef4e4a082eeec844d474` |
| macOS launcher | 470 | `16779aaff50f905f52f7fe9fcf792a6ba93e48e6ea737e403fccea3311d90dfc` |
| macOS arm64 executable | 95,978,608 | `79ce160c95381b33febe71d404305e77080e42b87c28a77239c02890da2e242e` |
| macOS PCK | 1,624,060 | `c8169139fc7dcf19f569888daa27f4beaeecbce578a3006f05ea68ae05973f64` |
| macOS `Simulation.Core.dll` | 959,488 | `8887c42541297207cf2f450ef0646fb69708d2e09f68922dd335293b58dd9b68` |
| Windows `build-manifest.json` | 503 | `8c399629cf2e3ae7fe71a72fe95d479fc3b0b10344a86700d956cd052e63c757` |
| Windows GUI executable | 100,801,024 | `f7676bbd43ff38cc10debecde6c002fef9a5c8bfab4c5c1d73ce5dbba0f8a235` |
| Windows console executable | 50,176 | `8994307fb9b522fc0f6fa0157fa6a11a4baaf88c454ad5e8582b98b250d51d1a` |
| Windows PCK | 1,678,620 | `7504268e12c1deca35b63ffd0bd5fc7afa062db60d1dd802a4288fb8d6476110` |
| Windows `Simulation.Core.dll` | 959,488 | `48201476e0f9cad8d06190968609d089bd73507a65b4c617432f7dabbeff4442` |

The workflow emitted non-failing Node.js 20 deprecation warnings for `actions/upload-artifact@v4`, which GitHub forced to Node.js 24. They did not affect job conclusions or artifact integrity.

## Decision

SP-04D2 criterion D214 passes at exact SHA `62a50075ca86b3466cca9c05825d4374e6cac366`. D2 now has local and hosted macOS arm64/Windows x64 evidence. The local 1,000-character/200-completed-route performance fixture remains raw component evidence and does not establish the full-SP-04 three-second turn budget. Physical Windows remains an M4 gate; signing and Steam remain SP-15 gates. SP-04 and M2 remain Active, SP-05 remains blocked, and SP-04D3 household decisions, conflict, coercion effects, and atomic condition/lifecycle integration is the next package.
