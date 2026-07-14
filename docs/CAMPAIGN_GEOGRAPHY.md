# Campaign Geography, Movement, and Supply

SP-03 implements route-based campaign geography in `Simulation.Core`, content loading in `Game.Content`, and an illustrated Godot view in `Game.Presentation`. The simulation never stores Godot nodes, scene paths, vectors, or resources.

## Content model

The campaign hierarchy combines two authored layers. `data/authored/later-han-geography.json` contains 13 region-tier records, 99 ordinary 군/국 or capital-equivalent district-tier records, and 1,160 county-rank locality records. `data/authored/geography-191.json` contains the bounded Central Plains gameplay state: eight strategic stops, ten routes, two armies, and their political and supply overlays. Its `geography_scenario` references the complete hierarchy while movement remains limited to the authored route graph.

The hierarchy is deterministically generated from `data/research/later-han-administrative-units.json`, the repository snapshot of the corrected source audit. Map anchors come from the separately licensed `data/research/later-han-locations.json` snapshot: traditional-name matches against DILA Place Authority data are tagged as direct or disambiguated, while missing locations are tagged as low-confidence parent/descendant inferences. Stable IDs derive from audited source-cell identity rather than translated names. The source is an early-140s Later Han register used as a broad gameplay baseline, not a synchronized or survey-accurate map of 191. Dependent-state 도위부/부도위 sections are excluded. Eight provisional Korean readings remain non-release records and localization entries until human review; all other retained hierarchy rows have release-marked Korean and English labels.

Regenerate the static content after changing the audited snapshot or layout rules:

```sh
dotnet run --project tools/Tools.ContentPipeline -- later-han generate
```

To reproduce the location snapshot, obtain the DILA Authority Databases source at pinned commit `385e3f557285d7a60346f85d698193e19b6cea2f`, then run:

```sh
dotnet run --project tools/Tools.ContentPipeline -- later-han import-locations --dila-xml /path/to/Buddhist_Studies_Place_Authority.xml
```

The importer requires TEI SHA-256 `6fcc9f650b0737f4379f58d605cb65de5ce08680de8ab5631dbc1427f3552efb`. DILA attribution and CC BY-SA 3.0 terms are recorded in the snapshot and source register. CHGIS/TGAZ data is not used because its published non-commercial restriction is incompatible with the intended commercial game. User-supplied historical map images are unshipped visual sanity references only; no pixels or borders are copied or traced.

The generator also rebases the existing strategic stops onto matching Later Han localities and updates the versioned content manifest. It does not edit route identities, endpoints, capacity, traversal cost, permitted modes, modifiers, throughput, commands, events, saves, or supply algorithms. The content pipeline constructs a `GeographicGraphDefinition` and rejects the pack when it finds:

- missing or duplicate IDs;
- a district without a region or locality without a district;
- duplicate stop coordinates;
- dangling, self-connected, zero-capacity, or modeless routes;
- water routes without suitable ports/ferries and water transport;
- invalid scenario control, claim, intelligence, store, disruption, or army overlays.

Universal `Region`, `District`, and `Locality` mechanics use content-provided name and administrative-label localization keys. The imported Later Han layer uses 주-level regions, 군/국 or capital-equivalent districts, and retained county-rank terms such as 현, 후국, 읍, 공국, and 도. The universal contracts can still support other governments later without hard-coding Han terminology into containment mechanics.

`./scripts/build.sh` and `scripts/build.ps1` validate the source pack and emit `game/generated/geography-191.json`. Godot exports include this normalized runtime artifact; the editor can fall back to the validated source data during development.

## Route graph and movement

Administrative containment and route connectivity are separate. Armies can move only across a contiguous ordered route plan whose edges permit their transport mode. Deterministic pathfinding orders equal-cost alternatives by stable route ID.

`MovementOrderPayload` records the army, route plan, transport mode, stance, departure, and fallback. Each campaign day advances integer route progress. Capacity, disruption, control, weather, and seasonal closure can block the edge. `Wait` retains the order, `Stop` cancels it at the last valid stop, and `Reroute` requests a deterministic permitted path.

Opposing movement ranges that cross on the same edge emit one stable `InterceptionEventPayload`; scouting and stance select the interceptor with stable-ID tie breaking. Retreat selects the first edge of the best permitted path. Reinforcement queries return armies ordered by route cost and ID.

## Supply and political state

Stops store supplies and daily production. Supply orders traverse an explicit chain and transfer no more than the smallest remaining daily throughput after route capacity, season/weather modifiers, disruption, control, and transport permissions. Competing same-day transfers share that capacity. Armies track carried supply and daily demand; daily events record consumption and shortage.

Each stop independently stores actual controller, legal appointee, local acceptance, claims, occupation, and per-observer intelligence. Passage permission belongs to route state and does not transfer administrative control.

`GetCampaignMapPresentation` is the engine-independent, deterministic, non-mutating read model used by Godot. It reveals controllers from Rumored intelligence; legal appointees, claims, occupation, population, and culture from Observed; and acceptance, stores, production, stationed demand, and shortages only at Current. Route dynamic values require Current intelligence at both endpoints. Known null/empty values remain distinguishable from hidden values: for example, a known null controller is Uncontrolled, an observed empty claim list is None, and a hidden value is Unknown.

Scenario content may provide explicit observer/counterparty diplomacy categories. The schema-1 runtime artifact carries this as an additive default-empty `diplomaticRelations` field, preserving deserialization of earlier artifacts. Presentation distinguishes self, friendly, neutral, hostile, uncontrolled, and unknown rather than treating every non-player controller as hostile.

## Presentation and battle handoff

`CampaignMapView` draws a geographically plausible stylized overview with a project-authored land silhouette, projection, river bands, mountain decoration, typed routes, stops, bilingual labels, selection, and deterministic label placement. Anchor candidates derive from the attributed DILA snapshot; the projected coordinates and convex containment hulls remain gameplay presentation geometry, not surveyed historical borders. The old circular containment layout has been removed.

The map uses semantic zoom: the overview emphasizes region names, the middle tier reveals district boundaries and labels, and close zoom reveals county-rank points and labels. Mouse-wheel zoom and middle-button drag pan the map; launch arguments `--map-zoom=` and `--map-center=x,y` support repeatable visual evidence. Geometry is clipped below the top bar and labels use deterministic collision rejection.

All nine map modes remain backed by the same observer-filtered presentation query and do not mutate or rebuild simulation state. Static topology, geometry, terrain, route types, and localized names come from the validated graph; all dynamic/hidden presentation values come from `CampaignMapPresentationState`.

Supply rendering separates stored quantity from route capacity, raw throughput, observer-usable effective throughput, availability, and disruption. Stop inspectors also expose known production, stationed demand, and shortage. Administrative containment hulls remain visually distinct from route connectivity, and stop/route/region/district/locality picking uses deterministic hit ordering.

`BattleLocationDescriptor` provides terrain, weather, elevation, fronts, river/coast flags, and reinforcement routes for either a stop or route encounter. It contains no Godot references and can be serialized in saves, replays, tests, or a later `BattleSetup` contract.

## Verification

```bash
dotnet run --project tools/Tools.ContentPipeline -- later-han generate
./scripts/validate.sh
./scripts/test.sh Release
dotnet run --project tools/Tools.ContentPipeline -- content geography --output game/generated/geography-191.json
./scripts/import.sh
```

If direct macOS launch through a shell symlink stalls before `_Ready()`, resolve the application-bundle executable and force the native architecture:

```sh
GODOT_REAL="$(realpath "$(command -v godot)")"
/usr/bin/arch -arm64 "$GODOT_REAL" --headless --path game -- --smoke-test
```

Automated coverage includes exact imported hierarchy counts and containment, source/release metadata, bilingual labels, the versioned content pack, an exact snapshot of all ten strategic route mechanics, graph failures, all map-mode query inputs, explicit diplomacy, supply presentation inputs, known-information thresholds, deterministic/non-mutating queries, deterministic paths, movement, seasonal blocking, interception, retreat, reinforcement, supply bottlenecks and shared daily capacity, port transport, independent political state, fog of war, battle descriptors, snapshot persistence, and fail-safe content/save compatibility.

The expanded hierarchy and stylized-map Apple Silicon results are recorded in the [Later Han working-tree evidence report](evidence/SP-03-later-han-map-working-tree-2026-07-13/README.md). Exact implementation SHA `f91dfce730f1e116bd17321e8a0a654a69823c69` passed the corresponding [hosted macOS arm64 and Windows x64 gates](evidence/SP-03-EXACT-SHA-f91dfce.md) with matching content identities. The older [presentation report](evidence/SP-03-presentation-working-tree-2026-07-12/README.md) remains historical evidence for the preceding bounded test layout and must not be used as visual evidence for the replacement map.
