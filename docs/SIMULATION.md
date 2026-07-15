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

`SimulationChecksum.Compute` canonicalizes entity, pending-command, random-stream, system-version, geography, character-world, and relationship-world order before SHA-256 hashing. Presentation state and command-validation diagnostics are excluded.

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

`SaveEnvelope` schema 5 records game/schema/contract versions, content manifests, root seed, a complete world snapshot including geography, character, and relationship state, bounded command/event diagnostics, and the authoritative checksum. Before DTO use, current-schema loads require the envelope collections plus calendar, random-stream, entity, command, system-version, geography, character, and relationship snapshot fields to have their expected non-null JSON shapes; null manifest/command/entity/relationship entries are rejected deliberately. `SaveStore` serializes with `System.Text.Json` and compresses using `GZipStream`.

Writes are created and flushed in a same-directory temporary file, parsed again, then atomically moved into place. Autosaves retain numbered generations (`.1`, `.2`, `.3` by default). A corrupt or simulation-invalid primary is never changed during load; snapshot validation failures are normalized at the save boundary so recovery checks generations newest-first and returns the source path and diagnostic.

Required simulation content is matched by pack ID, version, and checksum. Missing required manifests block loading with a precise list. Missing optional presentation manifests may be substituted by the owning presentation subsystem.

Schema migrations are explicit, forward-only, one-version steps. Before DTO deserialization or checksum canonicalization, schema-specific raw-shape validation requires the common legacy snapshot objects and collections that canonicalization dereferences. Schemas 1 and 2 require geography to remain absent, schema 3 requires its complete geography object/collection shape, schemas 1 through 3 require character state to remain absent, and schemas 1 through 4 require relationship state to remain absent. Malformed explicit-null legacy data is therefore rejected as a controlled compatibility failure so recovery can continue without changing the primary or any candidate generation. Before any migration mutates a document, schema 3 is integrity-checked against its repository-history-backed canonical snapshot shape, schema 4 is checked against its nonempty history-backed character fixture and exact pre-relationship checksum shape, and schemas 1 and 2 are checked against frozen shapes inferred from their registered migration contracts; a missing or mismatched legacy checksum is rejected rather than replaced. Schema 1 to 2 adds diagnostic event history; schema 2 to 3 adds empty geography state; schema 3 to 4 adds empty character state; schema 4 to 5 adds empty relationship state. Subsystem migrations register their system version and recompute the destination checksum. The complete chain is tested from literal frozen JSON inputs with corrupted-source negatives, and loading or migration never rewrites the source file.

The schema-3 fixture is reconstructed from the exact schema-3 contract and serializer at baseline `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`; no retained schema-3 save blob exists in the repository. No repository commit used schema 1 or 2 as its current save schema, and no retained pre-geography save exists. Their fixtures therefore remain explicitly synthetic/inferred, omit geography and characters as required by the migration contracts, and do not establish field compatibility. Any externally retained schema-1/2 save must be checked before that compatibility is treated as field-proven.

`WorldSnapshot` remains contract version 1 through additive default-empty subsystem properties. A schema-5 save must contain complete `snapshot.characters` and `snapshot.relationships` objects plus `simulation.characters@1` and `simulation.relationships@1`; omission, null, partial fields, or a missing system version are rejected before deserialization. Constructor-backed DTO failures such as an invalid character birth date or `EntityId` are normalized to compatibility diagnostics at the read boundary so autosave recovery can continue without catching fatal runtime failures. Default-empty exceptions apply only to legacy standalone snapshots whose omitted subsystem snapshots are complete, valid, and empty; partial-null or nonempty state without its registered system version fails deliberately. A current capture writes both registered versions. See [the character guide](CHARACTERS.md) for character and SP-04B-L relationship contracts, bounded history, observer queries, and validation.
