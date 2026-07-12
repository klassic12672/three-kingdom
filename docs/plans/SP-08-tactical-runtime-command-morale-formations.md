# SP-08 — Tactical Runtime, Command, Morale, and Formations

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M3 |
| Dependencies | [SP-01](SP-01-simulation-calendar-determinism-saves.md), [SP-07](SP-07-armies-recruitment-equipment-logistics.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Deliver readable real-time-with-pause tactical battles where formations, morale, cohesion, fatigue, command, positioning, and combined arms matter more than rapid frontal annihilation.

## Non-goals

- Individually simulated soldiers or one AI agent per visible figure.
- Turn-based tactical combat.
- More than 54 simultaneously active units.
- Naval, shoreline, and full siege-front behavior beyond extension hooks for SP-09.
- Supernatural hero abilities that bypass core tactical rules.

## Requirements

- Run authoritative tactical simulation at a fixed 20 Hz independent of rendered frame rate.
- Support pause and 0.25x, 0.5x, 1x, and 2x time scales with orders issuable while paused.
- Represent each unit as a formation entity with footprint, facing, ranks, cohesion, formation integrity, manpower, morale, fatigue, ammunition, experience, and command state.
- Render soldiers as instanced low-poly visuals driven by formation state, equipment, faction color, flags, combat animation, casualties, panic, and rout.
- Provide select, move, face, attack, charge, withdraw, hold, formation, ranged/melee stance, ammunition, target priority, fire-at-will, and delegation commands.
- Use formation-footprint pathfinding, collision avoidance, frontage, turning costs, terrain costs, and friendly passage rules.
- Frontal infantry combat produces gradual casualties, fatigue, morale pressure, and cohesion loss.
- Charges calculate mass, speed, cohesion, formation, weapons, target readiness, terrain, and direction.
- Flank and rear attacks apply significant morale/cohesion shock and increased casualties.
- Broken units leave player control, choose escape behavior, and become vulnerable to pursuit; eligible units may rally in safety or under command support.
- Hybrid units pay time/cohesion costs when switching ranged/melee role or ammunition.
- Formations define movement, turning, frontage, protection, charge, missile, and terrain behavior and may require unit/general/technology unlocks.
- Generals control up to six-unit contingents and can receive delegated objectives and standing orders.
- Produce a deterministic `BattleResult` from the final authoritative tactical state.

## Public contracts

- Consumes `BattleSetup` and returns `BattleResult` without direct mutation of campaign state.
- `TacticalCommand` uses stable participant/unit IDs and records issue tick, issuer, type, targets, and parameters.
- `TacticalEvent` records authoritative combat, morale, command, objective, casualty, capture, and withdrawal outcomes.
- `UnitBattleState` mirrors required persistent campaign attributes while owning battle-only position/formation state.
- `FormationDefinition`, `AmmunitionDefinition`, and `TacticalAbilityDefinition` originate in validated content packs.
- Replays store `BattleSetup`, deterministic battle seed, ordered tactical commands, and result checksum.

## Data flow

```text
BattleSetup + content definitions
→ tactical state initialization
→ player/AI TacticalCommands
→ fixed-tick movement/combat/morale/command resolution
→ TacticalEvents and presentation snapshots
→ objectives, withdrawal, surrender, or battle end
→ deterministic BattleResult + optional replay
```

## Implementation workstreams

1. Define tactical state, fixed-tick scheduler, command queue, deterministic battle RNG, and replay/checksum format.
2. Implement formation footprints, facing, movement, turning, navigation, collision, terrain, and deployment.
3. Implement melee engagement, casualties, fatigue, cohesion, morale states, rout, rally, withdrawal, and pursuit.
4. Implement charge and directionality, including bracing and flank/rear effects.
5. Implement ranged fire, ammunition, fire-at-will, target priorities, hybrid stance switching, and friendly-fire rules.
6. Implement formations, unlock validation, moving formation integrity, contingent/general command, delegation, alerts, and automatic pause hooks.
7. Implement instanced soldier/flag presentation, animation sharing, LOD, casualty density, and state interpolation.
8. Implement battle-end logic, captures, aftermath window, `BattleResult`, and campaign round-trip.

## Edge cases and failure handling

- Invalid or late commands fail with a tactical reason and do not partially mutate state.
- Path failure leaves the unit at its last valid position and requests a new path; units never teleport or collapse their footprint.
- Units overlapping after terrain/destruction changes separate through deterministic recovery rules.
- General death/capture breaks or transfers command according to `BattleSetup` chain of command.
- A routing unit trapped by terrain, walls, water, or enemies may surrender, scatter, or suffer pursuit losses.
- Battle termination distinguishes organized withdrawal, rout, surrender, objective completion, and mutual disengagement.
- Rendered soldier loss is cosmetic sampling of authoritative manpower; visual figures cannot change simulation outcomes.

## Performance budget

- Target 60 FPS at 1920x1080 medium on the M2 Pro in normal battles.
- Never remain below 30 FPS in a 54-unit stress battle.
- Tactical simulation tick completes within 50 ms at the 54-unit cap; target average below 20 ms.
- Battle working set remains at or below 6 GB.
- `BattleSetup` initialization excluding asset loading completes within 500 ms.

## Tests

- Fixed-tick determinism and replay checksum tests across Windows/macOS.
- Movement, turning, collision, terrain, and formation-integrity tests.
- Frontal infantry casualty/morale calibration tests.
- Charge, brace, flank, rear, fatigue, rout, rally, surrender, and pursuit tests.
- Ranged ammunition, fire-at-will, target priority, friendly fire, and hybrid switching tests.
- General death, command transfer, delegation, withdrawal, and multi-contingent tests.
- 18-, 36-, and 54-unit performance scenarios.

## Acceptance criteria

- [ ] Battles run in real time with pause and all required speed controls.
- [ ] Formation entities remain authoritative while instanced soldiers accurately communicate their state.
- [ ] Frontal infantry combat is materially less lethal than successful flank/rear shock and pursuit.
- [ ] Morale loss can remove player control, create rout, and permit rally where valid.
- [ ] Ranged/hybrid units support required toggles, ammunition, priorities, and stance changes.
- [ ] Generals command/delegate six-unit contingents within an eighteen-unit army structure.
- [ ] A 54-unit battle meets deterministic, memory, and frame-rate gates.
- [ ] `BattleResult` round-trips persistent units without duplication or loss.

## Risks

| Risk | Mitigation |
|---|---|
| Formation pathfinding is unstable or costly | Start with simple convex footprints, bounded avoidance, authored lanes, and stress tests before terrain complexity. |
| Visual soldiers imply unsupported individual simulation | Keep UI/animations formation-centric and synchronize visible density with authoritative manpower. |
| Morale outcomes feel opaque | Expose current pressures, attack direction, command, fatigue, and recent shocks in readable tooltips/alerts. |
| Pause-and-play becomes micromanagement-heavy | Delegate contingents, add standing orders, automatic pause conditions, and actionable alerts. |

## Deferred work

- Full naval, shoreline, amphibious, and siege fronts in [SP-09](SP-09-siege-naval-shoreline-combined-battles.md).
- Cinematic duel presentation beyond localized battlefield interactions.
- Controller support.
- More than 54 active units.
