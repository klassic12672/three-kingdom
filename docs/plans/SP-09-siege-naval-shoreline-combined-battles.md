# SP-09 — Siege, Naval, Shoreline, and Combined Battles

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M5 |
| Dependencies | [SP-08](SP-08-tactical-runtime-command-morale-formations.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Extend the common tactical runtime so field, siege, river/naval, shoreline, port, and amphibious combat can occur simultaneously on one connected battlefield and affect one another.

## Non-goals

- Separate incompatible minigames for land, naval, and siege combat.
- Fully simulated ship hydrodynamics.
- Every battle containing all possible fronts.
- Resolving an entire multi-turn siege in one tactical session.

## Requirements

- Represent a combined battlefield as connected fronts/zones sharing one clock, participant graph, objectives, command structure, and 54-active-unit cap.
- Supported fronts include open field, siege camp/lines, wall/gate, settlement interior, river/water, shoreline, port, and landing areas.
- Geography determines which fronts exist; scenarios do not force unavailable fronts.
- Ships are formation-scale units with crew, marines, hull condition, morale, maneuver, missile capability, boarding state, fire/flood state, and transport capacity.
- Ports/ferries/landing areas govern embarkation, disembarkation, supply, and reinforcement entry.
- Siege state persists across campaign turns: encirclement, stores, disease/attrition hooks, breaches, engines, works, mines/countermines, camps, and relief routes.
- One-day tactical actions include assaults, sorties, bombardments, fire attacks, landings, naval engagements, relief attempts, and evacuations.
- Cross-front effects include supply interruption, opened reinforcement routes, captured ports/gates, shore support, flooded/changed terrain, fire spread, and morale shocks.
- Allied factions may command different fronts and retain separate objectives, contribution records, and withdrawal decisions.
- Active-unit slots transfer between fronts and reinforcement queues rather than exceeding 54.

## Public contracts

- Extends `BattleSetup` with `BattleFront`, `FrontConnection`, `EntryPoint`, `PersistentSiegeState`, `WaterCondition`, and cross-front objectives.
- Extends `BattleResult` with wall/port/ship/camp state, breaches, fires, siege progress, evacuation, supply-route control, and front-specific contributions.
- `FrontTransferCommand`, `EmbarkCommand`, `LandingCommand`, `BoardCommand`, `SortieCommand`, and siege-engine commands extend the SP-08 tactical command registry.
- Campaign siege actions and persistent works extend `CampaignCommand`/`CampaignEvent` through SP-07 and SP-03 adapters.

## Data flow

```text
Campaign location + armies/fleets + persistent siege state
→ combined BattleSetup with connected fronts and entry points
→ shared fixed-tick tactical resolution
→ front objectives and cross-front effects
→ BattleResult
→ updated siege, route, port, fleet, settlement, and political state
```

## Implementation workstreams

1. Define generic front graph, objective propagation, front transfer, active-slot allocation, and reinforcement queues.
2. Add walls, gates, breaches, siege works, engines, camps, sorties, and settlement objectives.
3. Add ships, water movement, missile exchange, boarding, morale, fire/flood damage, and withdrawal.
4. Add ports, shore batteries, embarkation, landings, amphibious vulnerability, and evacuation.
5. Add campaign-persistent siege state and one-day action/result integration.
6. Add cross-front supply, reinforcement, flooding, fire, capture, and morale effects.
7. Build Red Cliffs/Fancheng-inspired synthetic test maps without scripting historical outcomes.

## Edge cases and failure handling

- A destroyed/captured entry point redirects queued reinforcements only when a valid alternate exists; otherwise they remain outside the battle.
- A ship destroyed during transport resolves survivors, drowning, capture, cargo, and stranded units explicitly.
- Units cannot transfer between fronts without a valid connection and capacity.
- Changing water level or destroyed structures triggers deterministic navigation updates and safe fallback positions.
- A tactical victory may advance but not end a campaign siege; `BattleResult` records the exact persistent change.
- Allied control of a port/gate follows coalition agreements and occupation rules rather than automatically granting it to the human player.

## Performance budget

- Combined 54-unit battles meet SP-08's 30 FPS worst-case floor and 6 GB memory limit.
- Cross-front objective/effect propagation completes inside the fixed 20 Hz tick.
- Loading a combined battlefield completes within 15 seconds on the development Mac SSD by Early Access.

## Tests

- Front connectivity, transfer, active-slot, and reinforcement queue tests.
- Wall, gate, breach, sortie, camp, engine, and persistent siege tests.
- Ship movement, missiles, boarding, fire, sinking, transport, and withdrawal tests.
- Landing, port capture, shore support, evacuation, and amphibious vulnerability tests.
- Cross-front supply, morale, flooding, fire, and entry-point tests.
- Multi-faction contribution, objective, occupation, and independent-withdrawal tests.

## Acceptance criteria

- [ ] Field, siege, water, shoreline, and settlement fronts share one tactical clock and participant structure.
- [ ] Geography/content determines available fronts and connections.
- [ ] Ships, ports, boarding, landings, walls, gates, camps, and relief forces function through the common runtime.
- [ ] Actions on one front produce defined effects on connected fronts.
- [ ] Campaign sieges persist across turns and accept multiple one-day tactical outcomes.
- [ ] Reinforcements obey the 54-active-unit cap and valid entry routes.
- [ ] Combined battles meet performance and campaign round-trip gates.

## Risks

| Risk | Mitigation |
|---|---|
| Combined fronts multiply pathfinding and AI complexity | Build one shared front graph and introduce fronts incrementally: siege, water, then amphibious integration. |
| Naval combat feels like reskinned land combat | Give ships distinct momentum, facing, boarding, fire, crew, and route-control roles without full physics. |
| Large maps become unreadable | Use camera bookmarks, front alerts, delegation, objective panels, and automatic pause. |
| Historical set pieces become scripted puzzles | Generate battles from world geography/state and use historical events only as conditional pressure. |

## Deferred work

- Open-ocean weather simulation beyond scenario-relevant coastal conditions.
- Highly detailed naval construction/refitting.
- Multi-day tactical sessions.
- Scripted cinematic recreations of named battles.
