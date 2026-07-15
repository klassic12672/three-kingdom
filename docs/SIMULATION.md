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

## Character-family birth resolution

The reserved Commands-phase character-family envelope can resolve one exact due or overdue active pregnancy into one generated child. The authoritative caller supplies only a `loc:` primary-name key plus current parental culture/family/household choices and a canonical parental trait subset capped at eight. Core derives collision-checked child and birth IDs, retains the pregnancy's expected date as the child's birth date, creates `Generated/Fictional` provenance, adds exactly two biological parent links, inserts selected memberships, and removes the pregnancy. Character and pregnancy candidates are both replanned and compared before either is committed; failed or stale work changes neither subsystem and consumes no random stream. No automatic scheduling, naming, loss, twins, education, public death, inheritance, content, UI, or AI is included.

## Checksums and replay

`SimulationChecksum.Compute` canonicalizes entity, pending-command, random-stream, system-version, geography, character-world, relationship-world, career-world, character-resource-world, character-estate-holding-world, character-marriage-world, character-guardianship-world, and character-pregnancy-world order before SHA-256 hashing. Presentation state and command-validation diagnostics are excluded.

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

`SaveEnvelope` schema 19 records game/schema/contract versions, content manifests, root seed, a complete world snapshot including geography, character-v2, relationship-v2, career-v1, character-resource-v1, character-estate-holding-v1, character-marriage-v2, character-guardianship-v1, and character-pregnancy-v1 state, bounded command/event diagnostics, and the authoritative checksum. Before DTO use, current-schema loads require the envelope collections plus calendar, random-stream, entity, command, system-version, geography, character, relationship, career, resource, estate, marriage, guardianship, and pregnancy snapshot fields to have their expected non-null JSON shapes. Character definitions/states must expose their required v2 nested descriptor, flaw, typed-link, and condition data; relationship memories must expose their v2 causal-source identity; career, resource, estate, marriage, and guardianship records retain their accepted exact shapes; active pregnancies require exact participant, union, date/turn, and source identity fields. D1/D2/D3/E0/E1/E2/E3/E4/E5 pending commands and diagnostics use explicit outer and nested political-marriage, romance, condition, household, coercion, lifecycle, relationship-memory consequence, legal-adoptive-parent, primary-guardianship establishment/end/replacement, coming-of-age, pregnancy-registration, and pregnancy-birth discriminators. Null entries, unknown discriminators, unsupported nested versions, inconsistent causal evidence, and invalid lifecycle combinations are rejected deliberately. `SaveStore` serializes with `System.Text.Json` and compresses using `GZipStream`.

Writes are created and flushed in a same-directory temporary file, parsed again, then atomically moved into place. Autosaves retain numbered generations (`.1`, `.2`, `.3` by default). A corrupt or simulation-invalid primary is never changed during load; snapshot validation failures are normalized at the save boundary so recovery checks generations newest-first and returns the source path and diagnostic.

Required simulation content is matched by pack ID, version, and checksum. Missing required manifests block loading with a precise list. Missing optional presentation manifests may be substituted by the owning presentation subsystem.

Schema migrations are explicit, forward-only, one-version steps. Before DTO deserialization or checksum canonicalization, schema-specific raw-shape validation requires the common legacy snapshot objects and collections that canonicalization dereferences. Schemas 1 and 2 require geography to remain absent, schema 3 requires its complete geography shape, schemas 1 through 3 require character state to remain absent, schemas 1 through 4 require relationship state to remain absent, schemas 1 through 6 require career state to remain absent, schemas 1 through 7 require character-resource state to remain absent, schemas 1 through 8 require character-estate-holding state to remain absent, schemas 1 through 9 require character-marriage state to remain absent, schemas 1 through 14 require character-guardianship state to remain absent, and schemas 1 through 17 require character-pregnancy state to remain absent. Schemas 10 and 11 require exactly one character-marriage-v1 system registration; schemas 12 through 18 require marriage-v2; schemas 15 through 18 also require exactly one guardianship-v1 registration and complete guardianship-v1 state; schema 18 additionally requires exactly one pregnancy-v1 registration and complete pregnancy-v1 state. The existing D1 through E4 vocabulary exclusions remain unchanged. Schema 17 permits E3 but forbids E4 pregnancy state, system registration, action/outcome discriminators, and E4-only properties even when explicitly null. Schema 18 permits E4 and existing generic generated characters but forbids E5 birth action/outcome discriminators and E5-only properties even when explicitly null. Malformed explicit-null legacy data and duplicate historical system registrations are rejected as controlled compatibility failures so recovery can continue without changing any candidate generation.

Before mutation, schemas 3–18 are integrity-checked against frozen canonical shapes. Existing schema 3–17 fixture identities remain unchanged. Schema 18 is a 74,121-byte literal nonempty save generated by a detached harness compiled against exact accepted E4 implementation source `177346b7358e84da358f3bfac8057b6ea70ed412`; it retains rich E0–E3 history, active pregnancy state, and one pending and one resolved E4 family action. Its stored checksum is `7f0feb49415b8d0074d447381340aea0c09200964f55104034db60c04f70d49e`, and file SHA-256 is `b27ccfdc51704721055161d6e57738e40030ff67d96f928e61bcbf5ed93c9453`. Schema 1→18 behavior remains as previously accepted. The authenticated vocabulary-only 18→19 step preserves the complete snapshot and diagnostics after rejecting all E5 injection. Every migration preserves source bytes, recomputes the destination checksum when required, and the complete 1→19 chain is tested from literal frozen inputs with corruption and source-byte preservation negatives.

The schema-3 fixture is reconstructed from the exact schema-3 contract and serializer at baseline `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`; the schema-4 and schema-5 fixtures use their accepted historical serializer contracts and nonempty character data. No repository commit used schema 1 or 2 as its current save schema, and no retained pre-geography save exists. Their fixtures therefore remain explicitly synthetic/inferred and do not establish field compatibility. Any externally retained schema-1/2 save must be checked before that compatibility is treated as field-proven.

`WorldSnapshot` remains contract version 1 through additive default-empty subsystem properties. A schema-19 save must contain complete character, relationship, career, resource, estate, marriage, guardianship, and pregnancy objects plus all eight exact subsystem registrations; omission, null, partial fields, or a missing, duplicate, or incompatible system version are rejected. Constructor-backed DTO failures such as an invalid character birth date or `EntityId` are normalized to compatibility diagnostics at the read boundary so autosave recovery can continue without catching fatal runtime failures. Default-empty exceptions apply only to legacy standalone snapshots whose omitted subsystem state is complete, valid, and empty; partial-null or nonempty state without its registered system version fails deliberately. A current capture writes all eight registered versions. See [the character guide](CHARACTERS.md) for the full contracts and validation boundaries.
