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

`SimulationChecksum.Compute` canonicalizes entity, pending-command, random-stream, system-version, geography, character-world, relationship-world, career-world, character-resource-world, character-estate-holding-world, character-marriage-world, and character-guardianship-world order before SHA-256 hashing. Presentation state and command-validation diagnostics are excluded.

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

`SaveEnvelope` schema 18 records game/schema/contract versions, content manifests, root seed, a complete world snapshot including geography, character-v2, relationship-v2, career-v1, character-resource-v1, character-estate-holding-v1, character-marriage-v2, character-guardianship-v1, and character-pregnancy-v1 state, bounded command/event diagnostics, and the authoritative checksum. Before DTO use, current-schema loads require the envelope collections plus calendar, random-stream, entity, command, system-version, geography, character, relationship, career, resource, estate, marriage, guardianship, and pregnancy snapshot fields to have their expected non-null JSON shapes. Character definitions/states must expose their required v2 nested descriptor, flaw, typed-link, and condition data; relationship memories must expose their v2 causal-source identity; career, resource, estate, marriage, and guardianship records retain their accepted exact shapes; active pregnancies require exact participant, union, date/turn, and source identity fields. D1/D2/D3/E0/E1/E2/E3/E4 pending commands and diagnostics use explicit outer and nested political-marriage, romance, condition, household, coercion, lifecycle, relationship-memory consequence, legal-adoptive-parent, primary-guardianship establishment/end/replacement, coming-of-age, and pregnancy-registration discriminators. Null entries, unknown discriminators, unsupported nested versions, inconsistent causal evidence, and invalid lifecycle combinations are rejected deliberately. `SaveStore` serializes with `System.Text.Json` and compresses using `GZipStream`.

Writes are created and flushed in a same-directory temporary file, parsed again, then atomically moved into place. Autosaves retain numbered generations (`.1`, `.2`, `.3` by default). A corrupt or simulation-invalid primary is never changed during load; snapshot validation failures are normalized at the save boundary so recovery checks generations newest-first and returns the source path and diagnostic.

Required simulation content is matched by pack ID, version, and checksum. Missing required manifests block loading with a precise list. Missing optional presentation manifests may be substituted by the owning presentation subsystem.

Schema migrations are explicit, forward-only, one-version steps. Before DTO deserialization or checksum canonicalization, schema-specific raw-shape validation requires the common legacy snapshot objects and collections that canonicalization dereferences. Schemas 1 and 2 require geography to remain absent, schema 3 requires its complete geography shape, schemas 1 through 3 require character state to remain absent, schemas 1 through 4 require relationship state to remain absent, schemas 1 through 6 require career state to remain absent, schemas 1 through 7 require character-resource state to remain absent, schemas 1 through 8 require character-estate-holding state to remain absent, schemas 1 through 9 require character-marriage state to remain absent, schemas 1 through 14 require character-guardianship state to remain absent, and schemas 1 through 17 require character-pregnancy state to remain absent. Schemas 10 and 11 require exactly one character-marriage-v1 system registration; schemas 12 through 17 require marriage-v2; schemas 15 through 17 also require exactly one guardianship-v1 registration and complete guardianship-v1 state. The existing D1 through E3 vocabulary exclusions remain unchanged. Schema 17 permits E3 but forbids E4 pregnancy state, system registration, action/outcome discriminators, and E4-only properties even when explicitly null. Malformed explicit-null legacy data and duplicate historical system registrations are rejected as controlled compatibility failures so recovery can continue without changing any candidate generation.

Before mutation, schemas 3–17 are integrity-checked against frozen canonical shapes. Existing schema 3–16 fixture identities remain unchanged. Schema 17 is a 68,407-byte literal nonempty save generated by a detached harness compiled against exact accepted E3 production source `59588be9d277dc4c4cb7ec98ef99e33591b0eeda`; it retains rich prior history, pre-E4 family diagnostics and pending work, and an E3 coming-of-age event with `WardCameOfAge` guardianship closure. Its stored checksum is `7a680781eceabf8c46554f780aaaca0ef6f781caf3256cac46185f0031e88ea4`, and file SHA-256 is `c99c1183f408dfd66eb08015e3b10d4e5b3a2f573c4adb364cd395fb8a1eb9c2`. Schema 1→17 behavior remains as previously accepted. The authenticated 17→18 step adds only empty pregnancy state and its system registration after rejecting all E4 injection. Every migration preserves source bytes, recomputes the destination checksum when required, and the complete 1→18 chain is tested from literal frozen inputs with corruption and source-byte preservation negatives.

The schema-3 fixture is reconstructed from the exact schema-3 contract and serializer at baseline `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`; the schema-4 and schema-5 fixtures use their accepted historical serializer contracts and nonempty character data. No repository commit used schema 1 or 2 as its current save schema, and no retained pre-geography save exists. Their fixtures therefore remain explicitly synthetic/inferred and do not establish field compatibility. Any externally retained schema-1/2 save must be checked before that compatibility is treated as field-proven.

`WorldSnapshot` remains contract version 1 through additive default-empty subsystem properties. A schema-18 save must contain complete character, relationship, career, resource, estate, marriage, guardianship, and pregnancy objects plus all eight exact subsystem registrations; omission, null, partial fields, or a missing, duplicate, or incompatible system version are rejected. Constructor-backed DTO failures such as an invalid character birth date or `EntityId` are normalized to compatibility diagnostics at the read boundary so autosave recovery can continue without catching fatal runtime failures. Default-empty exceptions apply only to legacy standalone snapshots whose omitted subsystem state is complete, valid, and empty; partial-null or nonempty state without its registered system version fails deliberately. A current capture writes all eight registered versions. See [the character guide](CHARACTERS.md) for the full contracts and validation boundaries.
