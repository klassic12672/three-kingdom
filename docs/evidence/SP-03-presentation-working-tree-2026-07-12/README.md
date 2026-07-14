# SP-03 Campaign Map Presentation — macOS Working-Tree Evidence

## Result

The bounded 191 campaign map launches and renders on the development Apple Silicon Mac. The final working tree was visually reviewed in all nine required modes, both launch locales, stop/route/area selection states, hover states, and a smaller requested window size. Authoritative presentation values come from the observer-filtered simulation query. No hidden controller, claim, supply, or diplomatic value was observed leaking through the inspected modes.

This is **working-tree evidence**, not exact-SHA, clean-checkout, hosted, exported-build, or cross-platform evidence. It supports the two SP-03 presentation criteria on the development Mac but does not complete M2 or satisfy the accepted-revision hosted Windows gate.

## Revision and environment

| Field | Evidence |
|---|---|
| Date | 2026-07-12 (Asia/Seoul) |
| Branch | `main` |
| HEAD | `7941ced08bb5f7e55e499715aaad23615881a742` |
| Upstream | `origin/main`; ahead 0, behind 0 at verification time |
| Tree | Dirty, uncommitted SP-03 package; therefore not attributable to HEAD alone |
| OS | macOS 26.5.1, build 25F80 |
| CPU | Apple M2 Pro, arm64 |
| Godot | `4.6.1.stable.mono.official.14d19694e` |
| .NET | SDK 10.0.301; `Microsoft.NETCore.App` 10.0.9 |
| Content | 32 records; 278 translations |
| Pack checksum | `4591544aac657902905080c7b242ffe0f9a530c84637bab6a1711fcc2920aef5` |
| Registry/artifact checksum | `edd4b1d5618169436a9e3bc2bff8b47676039ebde09f92e5776b3047b5714061` |

The generated development manifest records the current Git HEAD because the tree is uncommitted. Its content checksums identify the dirty content state, but the manifest must not be treated as an exact-revision manifest.

## Launch-stall diagnosis and recovery

The previously recorded launch stall was reproduced with the shell-visible `godot` symlink:

```sh
godot --verbose --headless --path game -- --smoke-test
```

The process stopped during `.NET: Initializing module` before `_Ready()`. A bounded process sample placed the main thread in `CSharpLanguage::init → GDMono::initialize → OS_MacOS::alert → NSAlert runModal`; interrupting exposed an `Assemblies not found` alert followed by a C# script failure. A clean isolated project copy without `.godot`, `bin`, or `obj` reproduced the same boundary, so repository code and generated cache were not the cause.

Resolving the application-bundle executable and forcing the native architecture restored Godot/.NET resource discovery without a project configuration change:

```sh
GODOT_REAL="$(realpath "$(command -v godot)")"
/usr/bin/arch -arm64 "$GODOT_REAL" --headless --path game -- --smoke-test
```

The recovered command reached `_Ready()`, printed the build manifest and geography checksum, and exited 0. `./scripts/import.sh` also succeeds because it resolves the configured Godot command before launch. No orphan Godot process remained after the bounded checks.

## Final-tree automated and runtime checks

The following command chain was run after the final known-empty/controller formatting correction and final localization/checksum update:

```sh
git diff --check
./scripts/validate.sh
./scripts/test.sh Release
dotnet run --project tools/Tools.ContentPipeline -- content geography --output game/generated/geography-191.json
./scripts/import.sh
```

Results:

- diff check: pass;
- repository/content validation: pass, 32 records, 278 translations, zero reported errors;
- Release build: pass, 0 warnings and 0 errors;
- `Game.Content.Tests`: 34/34 pass;
- `Simulation.Core.Tests`: 58/58 pass;
- `Repository.Tests`: 18/18 pass;
- total: 110/110 pass;
- geography artifact generation: pass;
- headless Godot import: pass;
- recovered native-arm64 app smoke: pass, `_Ready()` reached and registry/artifact checksum matched `edd4b1d5…`.

The additive schema-1 `diplomaticRelations` artifact field has backward-deserialization coverage. Invalid diplomatic relation categories fail normal geography validation. Query tests cover diplomacy categories, supply inputs, intelligence thresholds, deterministic ordering, and non-mutation of simulation snapshots.

## Mode-change performance

Command:

```sh
GODOT_REAL="$(realpath "$(command -v godot)")"
/usr/bin/arch -arm64 "$GODOT_REAL" --headless --path game -- --benchmark-modes
```

The first draw was 33.456 ms. Steady mode changes were:

| Mode | Elapsed ms |
|---|---:|
| Political control | 0.310 |
| Claims | 0.674 |
| Administration | 0.399 |
| Diplomacy | 0.814 |
| Supply | 0.854 |
| Population | 0.619 |
| Culture | 0.504 |
| Intelligence | 23.083 |
| Routes | 1.226 |

All steady changes remained below the SP-03 100 ms target and did not rebuild or mutate authoritative simulation state.

## Interaction checks

Quartz mouse movement and button events were sent to the running final working tree. The application printed these hit results:

```text
MAP_SELECTION stop=stop:year191/xingyang_fort route=- area=- tier=-
MAP_SELECTION stop=- route=route:year191/xingyang_yingyin area=- tier=-
MAP_SELECTION stop=- route=- area=locality:year191/yingyin tier=Locality
MAP_SELECTION stop=- route=- area=region:year191/central_plains tier=Region
MAP_SELECTION stop=- route=- area=district:year191/henan tier=District
```

Manual review also covered mode switching, selected-route emphasis, connected-route emphasis, stop and route hover inspectors, `Escape` clearing, and Korean/English switching. Repeated selection and mode changes produced no unexpected application error.

The actual-window Quartz capture path was intermittently only partially composited when another process held focus. For that reason, the interaction log lines above establish mouse-hit identity, while the clean viewport captures below establish render detail. Partial Quartz captures are retained only as supplemental diagnostic artifacts.

## Final authoritative visual sweep

The `final-01` through `final-12` images are clean viewport captures from the final working tree. They are 1280×720 PNGs. The `final-20` capture requested a 1024×768 window; the project preserved its 16:9 viewport and emitted a physically verified 1024×576 PNG.

| Artifact | SHA-256 | What it establishes |
|---|---|---|
| [final-01-political-uncontrolled-en.png](final-01-political-uncontrolled-en.png) | `a384308f48659a3dba33d09bfcb0d8cef4f92544374989afb5c59a602b2d4d84` | Political mode; known null controller is `Uncontrolled`, not hidden `Unknown` |
| [final-02-claims-known-none-en.png](final-02-claims-known-none-en.png) | `e9fa4310f9e1e0be4a575703127ea7345f5c1266d21655b027b49b6e42667990` | Claims mode; observed empty claims render `None` |
| [final-03-administration-ko.png](final-03-administration-ko.png) | `91be0045df8d42100067b9bf6129016feadd4e11abaa35698434e2d6059386df` | Korean administration labels, appointee, acceptance arc, occupation, selection |
| [final-04-diplomacy-uncontrolled-ko.png](final-04-diplomacy-uncontrolled-ko.png) | `0ed949fce33f7dd15a66e6cb90aa765542b1f070f84a5c321aa708599fd0d617` | Korean diplomacy; self/friendly/neutral/hostile/uncontrolled/unknown legend semantics |
| [final-05-supply-selected-route-en.png](final-05-supply-selected-route-en.png) | `61e0d7302af3687abc4dc33020cd00a38ac17e97d528f8bf2903fa8c5ba5c05e` | Supply capacity 10,000; raw 7,200; effective 6,912; disruption 4%; available yes |
| [final-06-population-en.png](final-06-population-en.png) | `261a564947c43f60b087c0bc2467707e8dd0114ffc3d9cfd080c59c2cfa8540d` | Observer-filtered locality population mode |
| [final-07-culture-ko.png](final-07-culture-ko.png) | `35aa0f17978ce0cd860a403a3eff64e2261c439667c69688916ae2689fedc77a` | Observer-filtered culture mode with Korean labels |
| [final-08-intelligence-rumored-en.png](final-08-intelligence-rumored-en.png) | `339f40bcac475ef3bfcb429b0f6e486211893a39aec734397df5c8efaf6868d0` | Rumored controller knowledge while appointee, acceptance, claims, stores, production, demand, and shortage remain unknown |
| [final-09-routes-selected-ko.png](final-09-routes-selected-ko.png) | `319a608b47e1824cf53e780d7980e22559d36038fd6703b6d588e40edd2d95bd` | Typed/patterned routes, selected route, Korean route labels, hidden dynamic route values |
| [final-10-selected-region-en.png](final-10-selected-region-en.png) | `087ac8e05c633268cd0b33024579cbf2d46e0e8ab9c13f94aef5adbb25c19dbc` | Region selection and containment distinct from connectivity |
| [final-11-selected-district-ko.png](final-11-selected-district-ko.png) | `1013f0e7263ec547b61eca71840172991eb617230d2c6876a24833c786c5ee71` | District selection, containment, Korean administrative terminology |
| [final-12-selected-locality-en.png](final-12-selected-locality-en.png) | `63e8b4c1fdde66cf5cc83c5b9a824e2fa521a3bcf275a60c1fa9b4bd4e088349` | Locality selection and contained-stop count |
| [final-19-actual-window-hover-route-en.png](final-19-actual-window-hover-route-en.png) | `a7e522f5679781d3c9145be94c293c120a02d5e933f8f6713be868577d7d2273` | Clean actual-window route hover and observer-usable supply inspector |
| [final-20-routes-1024x768-en.png](final-20-routes-1024x768-en.png) | `94962a7ab95c89b9a0b5f56433698f4dddbe0fe2cffe06e0295adcc7f63fc6e4` | Readability at requested 1024×768 window / 1024×576 rendered viewport |

The actual macOS window used a Retina 2× backing scale and remained readable. A separate custom in-game UI scale setting does not exist in this slice, so non-default custom UI-scale behavior was not independently verified.

The older `01`–`14`, `current-*`, and non-table `final-*` PNGs predate the final sweep or include Quartz compositor artifacts. They are historical/supplemental and are not used for final-working-tree render assertions. In particular, a filename containing `1024x768` records the requested window size; artifact dimensions must be read from the file metadata.

## Acceptance and limitations

Supported on this working tree and development Mac:

- the bounded 191 slice renders as an illustrated 2.5D route map;
- all nine required modes present observer-filtered authoritative data;
- controller, legal appointee, local acceptance, claims, occupation, diplomacy, intelligence, and supply semantics remain distinct;
- Korean and English map/UI labels render and the reviewed bounded-slice label placement is readable;
- stop, route, locality, district, and region mouse picking succeeds;
- selected and hovered route/stop states are visible;
- steady mode changes meet the 100 ms budget.

Still unsupported by this evidence:

- exact-SHA or clean-checkout attribution;
- hosted macOS/Windows gates for an accepted revision;
- a current export/package validation run;
- physical Windows visual/manual verification, which ADR-0001 does not require through M3;
- non-default custom UI scaling beyond the reviewed Retina backing scale;
- automated pixel/layout/picking assertions; these visual behaviors remain manually evidenced.

At collection time SP-03 remained Active because no accepted-revision hosted gates existed. The coordinate-backed replacement and its later [exact-SHA hosted report](../SP-03-EXACT-SHA-f91dfce.md) subsequently completed SP-03; this report remains historical evidence for the preceding bounded layout and does not establish the replacement map. M2 remains Active and is not complete.
