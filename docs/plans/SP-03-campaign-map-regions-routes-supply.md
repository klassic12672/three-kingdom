# SP-03 — Campaign Map, Regions, Routes, and Supply

## Metadata

| Field | Value |
|---|---|
| Status | **Active — engine/content implementation present; presentation acceptance in progress** |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-01](SP-01-simulation-calendar-determinism-saves.md), [SP-02](SP-02-content-localization-modding-research.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Create the authoritative route-based campaign geography and an illustrated 2.5D map that makes movement, administration, claims, supply, and regional differences legible.

## Non-goals

- A seamless or freely traversable 3D campaign world.
- Tactical battle terrain generation beyond producing `BattleSetup` location descriptors.
- Final 1.0 map content during the M2 slice.
- Assuming every culture used Han administrative terminology.

## Requirements

- Represent geography through universal `Region`, `District`, `Locality`, `Route`, and `RouteStop` entities.
- Display localized historical labels such as 주, 군, 국, 도위부, 현, 연맹권, 소국, 국읍, 읍락, 부, 성, 곡, and 사출도 according to government/content data.
- Separate administrative containment from graph connectivity.
- Route types include roads, mountain paths, rivers, coastal lanes, open-sea lanes, frontier trails, and seasonal passages.
- Stops include settlements, ports, ferries, bridges, passes, gates, forts, watchtowers, camps, depots, and natural battlefields.
- Every route has capacity, traversal cost, permitted transport modes, weather/season modifiers, supply throughput, and control state.
- Track actual controller, legal appointee, local acceptance, claims, occupation, and intelligence independently.
- Armies move by route progress and may intercept, reinforce, retreat, or be blocked at stops.
- Supply originates from stores/production and flows through controlled or permitted routes subject to capacity and disruption.
- Produce map modes for political control, claims, administration, diplomacy, supply, population, culture, intelligence, and routes.
- Produce deterministic location/front descriptors for `BattleSetup`.

## Public contracts

- Extends `EntityId` for all geographic records.
- `MovementOrder` is a typed `CampaignCommand` containing actor/army, planned route, stance, departure, and fallback behavior.
- `MovementEvent`, `InterceptionEvent`, `ControlChangedEvent`, and `SupplyTransferredEvent` are typed `CampaignEvent` payloads.
- `GeographicContext` query supplies location, adjacency, routes, terrain, weather, ownership, claims, and battle-front possibilities.
- `BattleLocationDescriptor` contributes terrain/front data to `BattleSetup` without embedding Godot scene references.
- Geographic and route state persists inside `WorldSnapshot` and `SaveEnvelope`.

## Data flow

```text
Validated map content + scenario ownership/claims
→ geographic graph registry
→ movement and supply commands
→ daily capacity/control/weather resolution
→ movement, encounter, shortage, and control events
→ campaign map queries and BattleSetup descriptors
```

## Implementation workstreams

1. Define universal geographic entities, localized label strategy, graph validation, and scenario-state overlays.
2. Implement movement progress, route permissions, interception, reinforcement range, retreat, and stop occupation.
3. Implement stores, supply demand, route throughput, disruption, depots, ports, and transport modes.
4. Implement control, legal appointment, local acceptance, claims, and intelligence visibility as separate state.
5. Build the illustrated 2.5D map renderer, selection/picking, route visualization, labels, and map modes.
6. Implement deterministic battle-location/front descriptors.
7. Author the bounded 191 Central Plains map slice and test fixtures.

## Edge cases and failure handling

- Graph validation rejects dangling routes, impossible containment, duplicate stop placement, and routes without valid transport modes.
- Armies whose planned route becomes invalid stop at the last valid position and request/recompute orders; they never teleport.
- Opposing movements on the same edge resolve by deterministic encounter rules using progress, scouting, and stance.
- Contested or allied stops may permit passage without transferring administrative control.
- Seasonal closure queues or redirects orders according to the order's declared fallback.
- Supply cannot exceed the minimum capacity of its traversed route chain.

## Performance budget

- Path queries for normal player interaction return within 50 ms in the vertical slice and 150 ms on the projected full map.
- Map-mode changes update visible layers within 100 ms without rebuilding authoritative simulation data.
- Daily world movement/supply resolution remains inside the overall 3-second turn target.

## Tests

- Graph integrity, containment, localized label, and route-mode validation.
- Deterministic path, movement, interception, retreat, and reinforcement tests.
- Supply capacity, disruption, depot, port, and seasonal closure tests.
- Distinct controller/appointee/acceptance/claim state tests.
- Fog-of-war queries that expose only known information.
- Visual picking, label overlap, route highlighting, and map-mode manual tests.

## Acceptance criteria

- [x] The 191 slice loads from content data.
- [ ] The 191 slice renders as an illustrated 2.5D route map.
- [x] Armies traverse routes/stops and cannot move solely through territorial adjacency.
- [x] Interceptions, reinforcements, retreat, and route invalidation resolve deterministically.
- [x] Supply throughput responds to capacity, control, transport, season, and disruption.
- [x] Political control, legal appointment, local acceptance, and claims remain independent.
- [ ] Every required map mode presents correct known information.
- [x] Valid encounter locations produce engine-independent `BattleSetup` geography.

The bounded 191 fixture, authoring contract, runtime behavior, presentation modes, and verification commands are documented in the [campaign geography guide](../CAMPAIGN_GEOGRAPHY.md).

The Godot project and C# presentation assembly compile successfully and the scene imports the `CampaignMapView` type. On the current development Mac, direct editor/game launch stalled inside Godot's `.NET: Initializing module` step before project code executed, including from a clean copied project cache. Rendering, map-mode presentation, interactive picking, and label-overlap acceptance therefore remain unchecked until runtime visual evidence exists; this does not invalidate the separately automated engine/content results. SP-01 and SP-02 are complete, so M2 and this plan are active. The next package must recheck or resolve the stall, render the route map, verify modes, test picking/highlighting, review localized labels/overlap, and capture interactive visual evidence.

## Risks

| Risk | Mitigation |
|---|---|
| Full map research and authoring overwhelms solo development | Prove schemas and tooling on the bounded 191 slice, then expand region by region. |
| Route graph feels too abstract | Use terrain relief, visible progress, stops, local art, and meaningful alternate routes. |
| Supply calculations dominate turn time | Cache static reachability, resolve flows in batches, and profile before adding detail. |
| Local terminology becomes misleading | Separate universal mechanics from displayed historical labels and cite disputed classifications. |

## Deferred work

- Complete full-world geography and bespoke regional art.
- Advanced trade-flow simulation.
- Dynamic construction of entirely new permanent roads or canals.
- Tactical terrain generation implementation, owned by battle plans.
