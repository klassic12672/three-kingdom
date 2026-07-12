# Campaign Geography, Movement, and Supply

SP-03 implements route-based campaign geography in `Simulation.Core`, content loading in `Game.Content`, and an illustrated Godot view in `Game.Presentation`. The simulation never stores Godot nodes, scene paths, vectors, or resources.

## Content model

The bounded 191 Central Plains slice is authored in `data/authored/geography-191.json`. Its `geography_scenario` record names the exact region, district, locality, stop, and route records that belong to the scenario. The content pipeline constructs a `GeographicGraphDefinition` and rejects the pack when it finds:

- missing or duplicate IDs;
- a district without a region or locality without a district;
- duplicate stop coordinates;
- dangling, self-connected, zero-capacity, or modeless routes;
- water routes without suitable ports/ferries and water transport;
- invalid scenario control, claim, intelligence, store, disruption, or army overlays.

Universal `Region`, `District`, and `Locality` mechanics use content-provided name and administrative-label localization keys. This permits labels such as 주, 군, 국, 도위부, 현, 연맹권, 소국, 국읍, 읍락, 부, 성, 곡, and 사출도 without hard-coding one government's terminology into containment mechanics.

`./scripts/build.sh` and `scripts/build.ps1` validate the source pack and emit `game/generated/geography-191.json`. Godot exports include this normalized runtime artifact; the editor can fall back to the validated source data during development.

## Route graph and movement

Administrative containment and route connectivity are separate. Armies can move only across a contiguous ordered route plan whose edges permit their transport mode. Deterministic pathfinding orders equal-cost alternatives by stable route ID.

`MovementOrderPayload` records the army, route plan, transport mode, stance, departure, and fallback. Each campaign day advances integer route progress. Capacity, disruption, control, weather, and seasonal closure can block the edge. `Wait` retains the order, `Stop` cancels it at the last valid stop, and `Reroute` requests a deterministic permitted path.

Opposing movement ranges that cross on the same edge emit one stable `InterceptionEventPayload`; scouting and stance select the interceptor with stable-ID tie breaking. Retreat selects the first edge of the best permitted path. Reinforcement queries return armies ordered by route cost and ID.

## Supply and political state

Stops store supplies and daily production. Supply orders traverse an explicit chain and transfer no more than the smallest remaining daily throughput after route capacity, season/weather modifiers, disruption, control, and transport permissions. Competing same-day transfers share that capacity. Armies track carried supply and daily demand; daily events record consumption and shortage.

Each stop independently stores actual controller, legal appointee, local acceptance, claims, occupation, and per-observer intelligence. Passage permission belongs to route state and does not transfer administrative control. `GeographicContext` applies fog-of-war before presentation receives these fields.

## Presentation and battle handoff

`CampaignMapView` draws relief hints, water, typed routes, stops, bilingual labels, selection, and deterministic label placement. It supports political control, claims, administration, diplomacy, supply, population, culture, intelligence, and route modes without mutating or rebuilding simulation state.

`BattleLocationDescriptor` provides terrain, weather, elevation, fronts, river/coast flags, and reinforcement routes for either a stop or route encounter. It contains no Godot references and can be serialized in saves, replays, tests, or a later `BattleSetup` contract.

## Verification

```bash
./scripts/validate.sh
./scripts/test.sh Release
dotnet run --project tools/Tools.ContentPipeline -- content geography --output game/generated/geography-191.json
./scripts/import.sh
```

Automated coverage includes graph failures, bilingual labels, all map-mode query inputs, deterministic paths, movement, seasonal blocking, interception, retreat, reinforcement, supply bottlenecks and shared daily capacity, port transport, independent political state, fog of war, battle descriptors, snapshot persistence, and simulation checksum replay.
