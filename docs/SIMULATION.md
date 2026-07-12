# Simulation and Save Foundation

SP-01 provides a Godot-free deterministic campaign kernel in `Simulation.Core` and a headless operator tool in `Tools.Simulation`.

## Deterministic model

- `EntityId` accepts lowercase namespaced IDs such as `character:liu_bei`; comparison is ordinal and IDs are immutable.
- `CampaignDate` implements a project-owned proleptic Gregorian calendar without `DateTime`, culture, time-zone, or operating-system calendar behavior.
- Campaign turns alternate between three and four daily steps. Each day resolves ordered phases, then priority, then stable ID.
- Domain changes enter through validated `CampaignCommand` values and commit through immutable `CampaignEvent` values.
- Commands and events use explicit JSON discriminators (`$type`); assembly-qualified CLR type names are never persisted.
- Random streams derive isolated state from root seed, system, and context. Reading one stream cannot advance another.
- Full, Reduced, and Aggregate tier changes carry a conservation ledger and preserve resources and pending work.
- Background work may calculate concurrently, but its proposed events are sorted before the authoritative commit phase.

Use `SimulationJson.CreateOptions()` when an external tool reads or writes simulation contracts. It rejects unmapped fields so new required data is not silently discarded.

## Checksums and replay

`SimulationChecksum.Compute` canonicalizes entity, pending-command, random-stream, and system-version order before SHA-256 hashing. Presentation state and command-validation diagnostics are excluded.

The ten-year, 1,000-entity fixture is a cross-platform golden replay. Its seed and checksum are fixed in `TierAndSoakTests`; both macOS and Windows CI run that test.

Headless commands:

```bash
dotnet run --project tools/Tools.Simulation -- soak --years 10 --entities 1000 --seed 20260712
dotnet run --project tools/Tools.Simulation -- replay --input replay.json
dotnet run --project tools/Tools.Simulation -- checksum --save campaign.save.gz
dotnet run --project tools/Tools.Simulation -- compare --left a.save.gz --right b.save.gz
dotnet run --project tools/Tools.Simulation -- inspect --save campaign.save.gz
```

A replay JSON document contains `initialSnapshot` and `commands`, using the same camel-case contract shape and explicit payload discriminators as saves.

## Save behavior

`SaveEnvelope` schema 3 records game/schema/contract versions, content manifests, root seed, a complete world snapshot including geography state, bounded command/event diagnostics, and the authoritative checksum. `SaveStore` serializes with `System.Text.Json` and compresses using `GZipStream`.

Writes are created and flushed in a same-directory temporary file, parsed again, then atomically moved into place. Autosaves retain numbered generations (`.1`, `.2`, `.3` by default). A corrupt primary is never changed during load; recovery checks generations newest-first and returns the source path and diagnostic.

Required simulation content is matched by pack ID, version, and checksum. Missing required manifests block loading with a precise list. Missing optional presentation manifests may be substituted by the owning presentation subsystem.

Schema migrations are explicit, forward-only, one-version steps. Schema 1 to 2 adds diagnostic event history; schema 2 to 3 adds empty geography state and its system version while recomputing the authoritative checksum. Both migrations are registered and tested, and loading or migration never rewrites the source file.
