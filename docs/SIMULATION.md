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

`SimulationChecksum.Compute` canonicalizes entity, pending-command, random-stream, system-version, geography, character-world, relationship-world, career-world, character-resource-world, character-estate-holding-world, and character-marriage-world order before SHA-256 hashing. Presentation state and command-validation diagnostics are excluded.

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

`SaveEnvelope` schema 12 records game/schema/contract versions, content manifests, root seed, a complete world snapshot including geography, character-v2, relationship-v2, career-v1, character-resource-v1, character-estate-holding-v1, and character-marriage-v2 state, bounded command/event diagnostics, and the authoritative checksum. Before DTO use, current-schema loads require the envelope collections plus calendar, random-stream, entity, command, system-version, geography, character, relationship, career, resource, estate, and marriage snapshot fields to have their expected non-null JSON shapes. Character definitions/states must expose their required v2 nested descriptor, flaw, typed-link, and condition data; relationship memories must expose their v2 causal-source identity; career records must expose their v1 nested principal, proposal, service, and history data; resource records must expose their v1 account, ledger, transfer, and folded-history data; estate records must expose versioned `estate:` and owner IDs; marriage records must expose versioned practice, proposal, political-betrothal, union, romance invitation, v1/v2 adult non-explicit romance route, consent, and folded-history data. D1/D2 pending commands and diagnostics use explicit outer and nested political-marriage and romance action/outcome discriminators. Null entries, unknown discriminators, unsupported nested versions, and inconsistent v2 route causality are rejected deliberately. `SaveStore` serializes with `System.Text.Json` and compresses using `GZipStream`.

Writes are created and flushed in a same-directory temporary file, parsed again, then atomically moved into place. Autosaves retain numbered generations (`.1`, `.2`, `.3` by default). A corrupt or simulation-invalid primary is never changed during load; snapshot validation failures are normalized at the save boundary so recovery checks generations newest-first and returns the source path and diagnostic.

Required simulation content is matched by pack ID, version, and checksum. Missing required manifests block loading with a precise list. Missing optional presentation manifests may be substituted by the owning presentation subsystem.

Schema migrations are explicit, forward-only, one-version steps. Before DTO deserialization or checksum canonicalization, schema-specific raw-shape validation requires the common legacy snapshot objects and collections that canonicalization dereferences. Schemas 1 and 2 require geography to remain absent, schema 3 requires its complete geography shape, schemas 1 through 3 require character state to remain absent, schemas 1 through 4 require relationship state to remain absent, schemas 1 through 6 require career state to remain absent, schemas 1 through 7 require character-resource state to remain absent, schemas 1 through 8 require character-estate-holding state to remain absent, and schemas 1 through 9 require character-marriage state to remain absent. Schemas 4 and 5 require character contract v1; schema 5 additionally requires relationship contract v1; schema 6 requires character contract v2 and relationship contract v1; schema 7 requires character v2, relationship v2, and career v1; schema 8 additionally requires character-resource v1; schema 9 additionally requires character-estate-holding v1; schemas 10 and 11 require exactly one character-marriage-v1 system registration. Schema 10 forbids D1 discriminators; schema 11 permits D1 but forbids D2 discriminators, invitations, route-v2 state, and marriage system/snapshot v2. Malformed explicit-null legacy data and duplicate historical system registrations are rejected as controlled compatibility failures so recovery can continue without changing any candidate generation.

Before mutation, schemas 3–11 are integrity-checked against frozen canonical shapes. Existing schema 3–10 fixture identities remain unchanged. Schema 11 is a 325,473-byte literal nonempty marriage-v1 save generated from detached exact D1 revision `653ce71d24bd81435ded9e65022dc29afd8f4810`; it contains active and ended legacy romance routes plus nonempty D1 pending and diagnostic data, has stored checksum `9c5dc3195649bfde2626f95c7cf2573d4acbc4c2a081b9af0ac9d30c74f9c8fb`, and file SHA-256 `ce6f737a9e3a608dfaaaeaf422f74e134a8fa7073ad4026a9aa1354007174d14`. Schema 1→11 behavior remains as previously accepted. The authenticated 11→12 step adds an empty invitation collection, advances the marriage snapshot and `simulation.character_marriages` registration to v2, preserves all v1 routes and D1 payloads without synthesizing causal fields, and recomputes the current checksum. It rejects D2 nested discriminators and future marriage state before mutation. Every migration preserves source bytes, recomputes the destination checksum when required, and the complete 1→12 chain is tested from literal frozen inputs with corruption and source-byte preservation negatives.

The schema-3 fixture is reconstructed from the exact schema-3 contract and serializer at baseline `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`; the schema-4 and schema-5 fixtures use their accepted historical serializer contracts and nonempty character data. No repository commit used schema 1 or 2 as its current save schema, and no retained pre-geography save exists. Their fixtures therefore remain explicitly synthetic/inferred and do not establish field compatibility. Any externally retained schema-1/2 save must be checked before that compatibility is treated as field-proven.

`WorldSnapshot` remains contract version 1 through additive default-empty subsystem properties. A schema-12 save must contain complete `snapshot.characters`, `snapshot.relationships`, `snapshot.careers`, `snapshot.characterResources`, `snapshot.characterEstateHoldings`, and `snapshot.characterMarriages` objects plus `simulation.characters@2`, `simulation.relationships@2`, `simulation.character_careers@1`, `simulation.character_resources@1`, `simulation.character_estate_holdings@1`, and `simulation.character_marriages@2`; omission, null, partial fields, or a missing, duplicate, or incompatible system version are rejected. Constructor-backed DTO failures such as an invalid character birth date or `EntityId` are normalized to compatibility diagnostics at the read boundary so autosave recovery can continue without catching fatal runtime failures. Default-empty exceptions apply only to legacy standalone snapshots whose omitted subsystem state is complete, valid, and empty; partial-null or nonempty state without its registered system version fails deliberately. A current capture writes all six registered versions. See [the character guide](CHARACTERS.md) for descriptor, typed kinship, condition, relationship, career, personal-wealth, opaque estate-holding, political-marriage, adult-romance, bounded-history, observer-query, and validation contracts.
