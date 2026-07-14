# SP-03 Later Han Hierarchy and Coordinate-Backed Stylized Map — macOS Working-Tree Evidence

## Result

The uncommitted SP-03 working tree loads 13 region-tier records, 99 ordinary 군/국 or capital-equivalent district-tier records, and 1,160 county-rank localities. It preserves the existing eight strategic stops and ten movement/supply routes. The former circular test layout is replaced by an attributed coordinate snapshot, explicit low-confidence inferred anchors, a project-authored projection, rivers, mountains, convex presentation hulls, and semantic zoom.

This is dirty-working-tree evidence from the development Apple Silicon Mac. On 2026-07-14, the user product-accepted this functional map baseline and deferred detailed cartographic refinement until pre–Early Access work. This report is still not exact-SHA, clean-checkout, hosted, exported-build, physical-Windows, or release evidence. At collection time SP-03 and M2 remained active pending the applicable gates.

## Later exact-SHA reconciliation

The accepted working tree was committed as `f91dfce730f1e116bd17321e8a0a654a69823c69`. That exact revision subsequently passed hosted macOS arm64 and Windows x64 validation, build, 113/113 tests, import, native development export, automated smoke, manifest checks, and artifact upload in [CI run 29297543256](https://github.com/klassic12672/three-kingdom/actions/runs/29297543256). Both hosted artifacts reproduce this report's content pack checksum `f6024dea...` and registry/geography checksum `b04754a...`; see the [exact-SHA hosted report](../SP-03-EXACT-SHA-f91dfce.md).

That later evidence does not retroactively make these captures clean-checkout, hosted, exported-build, or Windows visual evidence. This report remains the local Apple Silicon visual, interaction, performance, and product-acceptance record; the exact-SHA report supplies the accepted-revision portability and artifact identities. Taken together they justify SP-03 completion while M2 remains Active and SP-04 package planning becomes next.

## Revision and environment

| Field | Evidence |
|---|---|
| Date | 2026-07-13 (Asia/Seoul) |
| Branch / HEAD | `main` / `7941ced08bb5f7e55e499715aaad23615881a742` |
| Tree | Dirty, uncommitted SP-03 package; not attributable to HEAD alone |
| OS / CPU | macOS 26.5.1 build 25F80 / Apple Silicon arm64 |
| Godot | `4.6.1.stable.mono.official.14d19694e` |
| .NET | SDK `10.0.301` |
| Content pack | `core:base` version `0.2.0` |
| Pack checksum | `f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef` |
| Registry/artifact checksum | `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` |

## Source and generated-content identity

| Artifact | SHA-256 | Purpose |
|---|---|---|
| Corrected hierarchy snapshot | `84af7a6a127f50087dad15e51a7f5c117191d5d1da46d10c7201374528f13bcb` | Portable 13/99/1,160 name and containment rows |
| Audited workbook | `4f6509b8d4d64b58319cd17f0617a5460fa2e6b2c89759eb7ae4ecdab7a35cf0` | Human-reviewable source audit; not a runtime dependency |
| DILA upstream TEI | `6fcc9f650b0737f4379f58d605cb65de5ce08680de8ab5631dbc1427f3552efb` | Input at commit `385e3f557285d7a60346f85d698193e19b6cea2f`; not copied into the repository |
| Portable location snapshot | `b787034fc2257b4ecbdcfdefcfccd3c4266208152701446c127cd219a9366811` | CC BY-SA 3.0 attribution, source IDs, confidence, and direct/inferred placement status |
| Generated hierarchy records | `2629ccd14bf36ff9197a7a375d8725574fd75be6bf6ca85e29cc1941f4f28a5a` | Versioned authored records consumed by the content loader |
| Generated 191 scenario | `d6be0aaf61ba68c0097450db4d3ba340498df023a0b232767b902a82c63a7d00` | Existing strategic graph rebased onto new map anchors |
| Runtime geography artifact | `bf3d64168dc8cf04280029847d5794ab47521f2ba091992f94c33fc7b79c89d3` | Normalized Godot input; embeds the registry checksum above |
| Generated build manifest | `1313e662042a19fc1dd59e86d12946383f0ed430487709576d5bfe686eefcf91` | macOS arm64 Development artifact identity |

The DILA importer produced 1,272 anchors: 938 direct matches, 8 child-disambiguated DILA matches, 212 parent-disambiguated DILA matches, 4 child-centroid inferences, 2 descendant-centroid inferences, and 108 deterministic parent inferences. The last three categories are low-confidence presentation anchors and are not claimed as exact historical locations. The snapshot records DILA attribution, CC BY-SA 3.0 terms, pinned commit, file checksum, source place IDs, and coordinate certainty. CHGIS/TGAZ data is excluded because its published non-commercial restriction is incompatible with the intended commercial game.

The hierarchy remains an early-140s gameplay baseline, not a synchronized 191 census. Dependent-state 도위부/부도위 sections remain excluded. Eight Korean readings marked provisional remain non-release records and localization entries.

## Supplied visual references

The eleven user-supplied maps were visually consulted only for broad 주 and 군/국 sanity checking. Their rights were not established, so no pixels, borders, labels, or traced geometry were copied into the repository. Identity of the unshipped inputs reviewed:

| File | SHA-256 |
|---|---|
| `4ju_jonpal_(1).jpg` | `21167e52f30bf9af7c66582468bcf36c6b9d79cb6c328613b3e2a2499e62f8ee` |
| `yangju2_jonpal.jpg` | `8a25c6ac5fa09efefe5e6cbe29e2df7c5d9255d7b87802ea6ea9858e6ec69526` |
| `20130924155609.jpg` | `b24fd5ad8887fcf0a648322f6101df13a04e49ecb17b4076f8246482f7d875b1` |
| `kyoju_jonpal_(1).jpg` | `e37ceff96f56e827afd2f0f2e68646a494cb09cdb0cef36b50f58dda19568b19` |
| `hyungju_jonpal.jpg` | `91f0d3760e055ef027cc0e4950f84a84ef9652929c53919c9212b62fe981d6e3` |
| `saye_jonpal.jpg` | `1e7daa2abb06fb39c2df19686d7bee0237dd5d3ee5e37d3af62d198aeb193061` |
| `yangju1-jonpal_(1).jpg` | `6fc108f4075091fa29a413c3c5560397bac6545f8d5bfd1b26aa683a0cf2f82d` |
| `yunju_jonpal.jpg` | `062c93ed5479c41b19382109a7dca5051119dd1d0c4e9f6977b7c1c174c51c5f` |
| `byungju_jonpal.jpg` | `79b568fc4b064da575c847daf520575a393b9e3f414cc8bed60c8a0ff2b0f753` |
| `giju_jonpal.jpg` | `8bfd5af910e01dc1469615a607bd2050d2a3cc8db51776574096a249f7e0345c` |
| `yuju_jonpal.jpg` | `bfc3eff4de5f8ff404f6a262bc992d9eda35ece36cda1b545c30b54d3820e429` |

The supplied Naver viewer URL could not be fetched through the automated safe-open path; no claim relies on it.

## Deterministic generation and mechanics regression

The following repeat sequence produced byte-identical location snapshot, hierarchy, scenario, localization, and content-manifest hashes:

```sh
dotnet run --project tools/Tools.ContentPipeline -c Release --no-build -- later-han import-locations --dila-xml "$DILA_TEI"
dotnet run --project tools/Tools.ContentPipeline -c Release --no-build -- later-han generate
```

Focused tests assert the exact hierarchy counts and parent links, five required source references, DILA commit/checksum/license identity, explicit inferred placements, Luoyang's direct coordinate, eight non-release provisional rows, content-pack version `0.2.0`, and an exact snapshot of every route ID, capacity, traversal cost, and supply throughput. No movement, supply, command, event, world-snapshot, or save-schema implementation changed in this refinement.

## Visual artifacts and human inspection

The 1280×720 PNGs below were captured from the native macOS renderer after the coordinate-backed replacement:

| Artifact | SHA-256 | What it establishes |
|---|---|---|
| [01-administration-overview-en.png](01-administration-overview-en.png) | `2acafe2d318a17fa444fae57e787727cc7e0193f79ded1ca05417dd253b8dc26` | English 13-region overview; recognizable west/east/north/south relationships and non-circular territories |
| [02-administration-central-ko.png](02-administration-central-ko.png) | `14a27a8eccce34004a7d7fda9cd10248c12f7284389c3676d87d2aad053ebc97` | Korean district tier at 2.4×; 군/국 labels, new anchors, stops, rivers, and containment hulls |
| [03-localities-central-en.png](03-localities-central-en.png) | `9d382a1a047b4a9680bbe517912dcff69830ccbe39e622cdadc99c568b1c6b12` | English county-rank tier at 4.8×; dense locality points and collision-rejected labels |
| [04-supply-route-en.png](04-supply-route-en.png) | `bc76f9bf30ca308c060c5afdf0f27a69280bdf0f9707ac44f800cb089ccee8b3` | Selected Xingyang–Yingyin route retains capacity 10,000, traversal 2,200, throughput 7,200, and effective throughput 6,912 |

Agent visual inspection confirmed broad province orientation against the supplied references, removal of the circular layout, route visibility, Korean and English rendering, top-bar clipping, and selection/inspector rendering. This does not satisfy the remaining human play-review gate. The dense 4.8× tier is usable for targeted inspection but still needs human review for label density and navigation feel.

## Performance

Two native-arm64 renderer benchmarks exercised all nine map modes. After the initial frame, the overview ranged from 1.417 to 32.594 ms and the 4.8× county tier ranged from 24.055 to 37.470 ms. Both are below the SP-03 100 ms map-mode budget. Initialization and capture timing are not treated as steady mode-change performance.

## Verification commands and results

```sh
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~GeographicContentTests
./scripts/validate.sh
./scripts/test.sh Release
./scripts/import.sh
dotnet run --project tools/Tools.ContentPipeline -c Release --no-build -- content geography --output game/generated/geography-191.json
dotnet run --project tools/Tools.ContentPipeline -c Release --no-build -- manifest --platform macos --architecture arm64 --configuration Development --output game/generated/build-manifest.json
/usr/bin/arch -arm64 "$GODOT_REAL" --headless --path game -- --smoke-test
```

Final results:

- focused `GeographicContentTests`: 8/8 pass;
- content validation: 1,295 records, 2,820 translations, 1 pack, zero errors, zero warnings;
- Release build: pass with zero warnings and zero errors;
- `Game.Content.Tests`: 37/37 pass;
- `Simulation.Core.Tests`: 58/58 pass;
- `Repository.Tests`: 18/18 pass;
- full Release total: 113/113 pass;
- Godot import: pass;
- native-arm64 headless smoke: pass with matching manifest/registry/geography checksums.

The first full run after the source change failed only the three expected golden checksum assertions. Those exact identities were refreshed; the unchanged 58-test simulation suite passed in both runs. A dummy-renderer capture attempt failed because headless Godot has no viewport texture; the final visual artifacts use the native macOS renderer.

## Residual gates

- The 108 parent-inferred county anchors and six centroid-inferred higher-tier anchors remain explicitly low-confidence and are candidates for future source refinement.
- Convex hulls are stylized containment cues, not historical borders.
- The eight provisional Korean readings still need human language review before release marking.
- Broader multi-window cartographic polish and play review are deliberately deferred to pre–Early Access refinement rather than treated as an SP-03 closeout gate.
- At collection time no accepted revision or exact-SHA hosted evidence existed. Commit `f91dfce...` and its later [hosted report](../SP-03-EXACT-SHA-f91dfce.md) close that historical gap.
- Physical Windows visual/input review remains an M4 gate under ADR-0001, not an SP-03 working-tree gate.
- At collection time M2 and SP-03 were not complete. SP-03 subsequently completed from the combined local and exact-SHA hosted evidence; M2 remains Active.
