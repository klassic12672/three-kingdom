# SP-01 — Simulation, Calendar, Determinism, and Saves

## Metadata

| Field | Value |
|---|---|
| Status | **Complete** |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M1 |
| Dependencies | [SP-00](SP-00-repository-toolchain-ci-packaging.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Create the pure .NET, headless, deterministic simulation kernel used by campaign, AI, saves, replays, and tactical battle integration.

## Non-goals

- Game-specific character, economy, diplomacy, map, or battle rules beyond synthetic fixtures.
- Godot nodes, UI, rendering, input, or Steam integration.
- Multiplayer lockstep or network rollback.

## Requirements

- Represent every persistent entity with a namespaced immutable `EntityId`.
- Alternate campaign turns between three and four days while resolving authoritative state in daily ordered phases.
- Accept state mutations only through validated `CampaignCommand` instances.
- Emit immutable ordered `CampaignEvent` records for authoritative outcomes.
- Isolate deterministic random streams by system and context so adding presentation calls cannot change results.
- Define Full, Reduced, and Aggregate simulation tiers and deterministic promotion/demotion hooks.
- Preserve conservation invariants when entities change tier.
- Separate read-only queries from commands and state mutation.
- Capture a complete `WorldSnapshot` without Godot resources or unmanaged references.
- Persist a `SaveEnvelope` containing schema/game version, content manifests, seed, snapshot, and recent command/event diagnostics.
- Serialize with `System.Text.Json`, compress with `GZipStream`, use atomic temporary-file replacement, and keep rotating autosave generations.
- Provide explicit forward migrations from each publicly released schema; never silently discard unknown required data.

## Public contracts

- `EntityId`: string-backed namespaced ID with validated syntax and ordinal comparison.
- `CampaignCommand`: command ID, issuing actor, issued date, command type, payload, and validation result.
- `CampaignEvent`: event ID, causal command/event, resolution date/phase, affected IDs, and typed payload.
- `WorldSnapshot`: calendar, deterministic seeds/stream states, entities, pending work, and system versions.
- `SaveEnvelope`: metadata plus compressed snapshot and bounded recent command/event history.
- `SimulationChecksum`: canonical hash excluding presentation-only and diagnostic fields.

All contracts are versioned. Payload polymorphism uses an explicit registered type discriminator rather than assembly-qualified CLR names.

## Data flow

```text
Commands from player/AI
→ validate against read-only state
→ order by date, phase, priority, and stable ID
→ resolve deterministic system logic
→ emit events
→ apply events to authoritative world
→ compute queries/checksum/snapshot
→ persist through SaveEnvelope when requested
```

## Implementation workstreams

1. Define IDs, calendar, ordered phases, deterministic collections, RNG streams, and error types.
2. Implement command validation, event production/application, and canonical checksums.
3. Implement synthetic entity stores and tier transition protocol with conservation accounting.
4. Implement snapshot creation/restoration and atomic compressed saves.
5. Implement schema registry and sample migrations.
6. Build headless CLI runners for replay, checksum comparison, soak simulation, and save inspection.
7. Add background calculation scheduling whose results commit only through deterministic ordered events.

## Edge cases and failure handling

- Duplicate IDs, unregistered payload types, invalid dates, and out-of-order events fail validation before mutation.
- Corrupt saves remain untouched, report a diagnostic, and offer the last valid autosave.
- Missing required content manifests block load with a precise list; optional missing presentation content may be substituted by its owning subsystem.
- A failed migration never overwrites the source save.
- Clock changes and leap-year handling use a project-owned proleptic calendar implementation rather than local OS culture/time zones.
- Commands whose actors die, defect, or lose authority before resolution produce defined cancellation or reassessment events.

## Performance budget

- Synthetic 1,000-character world: normal turn resolution at or below 3 seconds on the M2 Pro by M4.
- Snapshot creation and save/load at or below 5 seconds by M4.
- Checksum calculation adds no more than 10% to headless turn time in validation builds and is optional in release builds.
- Recent diagnostic history remains bounded and cannot grow saves indefinitely.

## Tests

- Calendar alternation, date arithmetic, and long-range chronology tests.
- Identical seed/command replay checksum tests across macOS and Windows.
- Random-stream isolation and collection-order fuzz tests.
- Tier transition conservation and pending-event preservation tests.
- Atomic-save interruption, corrupt-save recovery, and migration tests.
- Ten-, fifty-, and hundred-year synthetic soak runs.

## Acceptance criteria

- [x] A headless synthetic world runs ten years without invariant failure.
- [x] Windows and macOS produce identical checksums for the same replay.
- [x] All persistent mutation occurs through registered commands/events.
- [x] Full, Reduced, and Aggregate tier transitions preserve declared totals and pending work.
- [x] Save/load round-trips state and checksum exactly.
- [x] Interrupted writes and failed migrations preserve the last valid save.
- [x] Public contracts have versioning and compatibility tests.

The golden test passed for historical SHA `1ab375a5e812c14eba4eca4cc121e604ade73f47`, but that Windows job later failed in SP-00 cleanup; see the [historical report](../evidence/M0-EXACT-SHA-1ab375a.md). Cleanup-fix SHA `7f62a97cf880ae6ded8e47af8737a11e53479977` passed all 52 simulation tests on both hosted platforms, including the exact-source assertion for `105da5fd449cc2d00ba1bf979642b22107db5b236eab30baac437f1b9b8bf088`. With SP-00 complete, SP-01 is complete. See the [passing report](../evidence/M0-EXACT-SHA-7f62a97.md) and [simulation guide](../SIMULATION.md).

## Risks

| Risk | Mitigation |
|---|---|
| Hidden nondeterminism from hash iteration or threads | Canonical ordering, deterministic collections, and cross-platform replay tests from M1 onward. |
| Save schema freezes early design | Version all payloads and require migrations instead of serializing runtime classes directly. |
| Full snapshots become too large | Measure first; add chunking/delta autosaves only through a later ADR if budgets fail. |
| Tier transitions create visible discontinuities | Require conservation ledgers and compare outcomes against full-simulation fixtures. |

## Deferred work

- Cloud synchronization and conflict resolution.
- Player-facing replay viewer.
- Delta or journal-only save formats.
- Multiplayer determinism requirements.
