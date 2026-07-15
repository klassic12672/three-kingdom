# Character, Family, and Household Foundation

SP-04A establishes the first deterministic character-world slice and has accepted exact-SHA hosted macOS arm64/Windows x64 evidence at `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea`. SP-04B-L adds the bounded relationship/memory kernel and has passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04B-EXACT-SHA-ff7420f.md) at `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a`. SP-04 remains Active and is not complete.

## Package boundary

The package provides versioned character, family, household, and identity definitions; immutable scenario state; read-only queries; content loading; and save persistence. It deliberately adds no character mutation commands or events. Relationships, memories, marriage, births, death, inheritance, succession, simulation tiers, battle integration, historical rosters, and UI remain deferred.

## Contracts and queries

All character contracts are in `Simulation.Core` and use version 1:

| Contract | Purpose |
|---|---|
| `CharacterIdentityDefinition` | Stable ability, aptitude, trait, ambition, or reputation ID plus localization key |
| `CharacterDefinition` | Stable character identity, localized name key, birth date, and typed identity-definition references |
| `FamilyDefinition`, `HouseholdDefinition` | Stable localized family and household identities |
| `CharacterState` | Parent references |
| `FamilyState` | Family membership |
| `HouseholdState` | Household membership and head |
| `CharacterWorldSnapshot` | Canonical authoritative subsystem snapshot |
| `AuthoritativeCharacterProfile`, `AuthoritativeHouseholdView` | Defensive, unfiltered simulation read models |

`WorldState.Characters` is the authoritative `CharacterWorldState`; `IWorldQuery.Characters` exposes it through `IAuthoritativeCharacterWorldQuery`. These results are deliberately named as authoritative and unfiltered. The SP-04 names `CharacterProfile` and `HouseholdView` remain reserved for later observer-aware, player-knowledge-filtered queries in `Game.Application`.

Authoritative profiles derive parent and child lists, nullable family and household membership, and age from the campaign date. Age calculation uses `CampaignDate` only and changes deterministically on the birthday. Family and household membership are separate: a person may share one without sharing the other.

## Determinism and validation

Top-level definition and state input order is normalized by stable `EntityId` order. Nested identity, parent, family-member, and household-member lists must already be unique and ordinal-canonical in authoritative state. Captured snapshots and query results are defensive copies.

Construction rejects:

- unsupported snapshot, definition, or state versions;
- invalid or duplicate global definition IDs and duplicate states;
- missing or mistyped ability, aptitude, trait, ambition, and reputation definitions;
- dangling, self-referential, cyclic, or chronologically impossible parentage;
- characters born after the current campaign date;
- dangling or duplicate family and household members;
- membership in multiple families or multiple households;
- household heads who are not members; and
- non-canonical nested ID ordering.

## Content loading and localization

`CharacterContentLoader` reads resolved registry records of these types:

- `character_world`
- `character_definition`
- `family_definition`
- `household_definition`
- `character_identity_definition`

The published content-record schema defines closed version-1 payloads for all five types. The loader requires the same contract versions, unique ordinal-canonical per-kind identity lists, exact canonical agreement between typed references and `data.references`, exact declaration of consumed name keys in `localizationKeys`, and non-empty `ko-KR` and `en-US` text. Every typed record authored or overridden by a candidate pack is validated even when no world selects it; the complete post-override envelope is checked before candidate-registry construction, and mistyped reference values produce diagnostics. Resolved-world graph validation separately checks selected types, references, and state. Null nested parent/member lists produce deterministic owning-world diagnostics. Invalid candidates roll back without changing the earlier registry. The pre-existing generic `character` record and built-in pack are unchanged.

Registry construction validates age-independent character-world structure using the latest selected birth date. The real campaign start date remains authoritative when `WorldState` is created, because `character_world` does not itself own a scenario date.

Automated coverage uses only fictional, test-owned content: four characters, one family, two independent households, one parent/child relationship, five identity-definition kinds, and Korean/English names. No historical assertion or shipped content was added.

## Persistence and compatibility

`WorldSnapshot` contract version remains 1 and now has an additive default-empty `Characters` property. Newly captured state registers `simulation.characters@1`, and `SimulationChecksum` canonicalizes the character snapshot.

`SaveEnvelope` schema 4 adds the character subsystem. Current saves must explicitly contain the complete character snapshot plus `simulation.characters@1`; omission, null, partial state, and a missing version are rejected before DTO deserialization. The raw-shape boundary also rejects malformed required envelope/world data. For legacy schemas it validates common canonicalized snapshot collections before deserialization, keeps geography absent in schemas 1 and 2, requires the schema-3 geography shape, and keeps character state absent through schema 3. Explicit-null legacy data therefore becomes a controlled compatibility diagnostic, allowing autosave recovery to continue to the newest valid generation without changing any candidate source. Schema 3 is authenticated against its repository-history-backed checksum contract; schema-1/2 fixtures and checksum shapes are synthetic/inferred from migration contracts and remain field-unverified. Migration 3→4 rejects pre-existing character data, injects `CharacterWorldSnapshot.Empty`, registers the system version, and computes the schema-4 checksum. The full 1→2→3→4 chain remains forward-only and never rewrites the source save. Character validation and recognized nonfatal DTO-construction failures are normalized to save-compatibility diagnostics. A legacy standalone version-1 `WorldSnapshot` may omit `simulation.characters@1` only when its complete character snapshot is valid and empty; partial-null and nonempty cases fail deliberately, and capturing restored empty state writes the current system version.

## Verification

Focused commands:

```bash
dotnet test tests/Simulation.Core.Tests/Simulation.Core.Tests.csproj -c Release --filter FullyQualifiedName~Character
dotnet test tests/Simulation.Core.Tests/Simulation.Core.Tests.csproj -c Release --filter FullyQualifiedName~Save
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj -c Release --filter FullyQualifiedName~Character
```

Performance evidence was collected on 2026-07-14 from an uncommitted local working tree based on `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`, using macOS 26.5.1 build 25F80 on arm64 and .NET SDK 10.0.301:

```bash
dotnet test tests/Simulation.Core.Tests/Simulation.Core.Tests.csproj -c Release --filter FullyQualifiedName~CharacterWorldStateConstructsAndQueriesOneThousandCharacters --logger "console;verbosity=detailed"
```

The run constructed 1,000 characters and one household in 23.608 ms, then performed 1,000 profile lookups and one household lookup in 5.674 ms. The test asserts counts and correctness, not wall-clock thresholds. The full relationship/memory campaign-turn budget remains unchecked because those systems are outside SP-04A.

The pre-remediation candidate remains disqualified because `GeographyTests.PathfindingUsesRoutesOnlyAndMeetsInteractionBudget` later failed its unchanged `<50 ms` threshold; its earlier audit is not relabeled as passing. Corrected-tree evidence collected on 2026-07-15 passed:

- `./scripts/validate.sh`: 1,295 records and 2,820 translations valid; registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0` (unchanged because the fixture is test-owned);
- 39 `SaveStoreTests`, including three schema-specific explicit-null recovery and byte-preservation cases;
- focused closeout: 36 Simulation.Core character, 29 Game.Content character, 3 published-schema, and 1 architecture-boundary tests passed;
- `./scripts/test.sh Release`: 107 Simulation.Core, 66 Game.Content, and 18 repository tests passed with zero failures;
- the ten-year/1,000-entity synthetic soak with the new authoritative subsystem: checksum `cc6cba9f2b5408921fdbcd15a8d5494ca2351e73d7f3052f16702a09639af702`;
- `git diff --check`; and
- `git lfs fsck`.

These are local macOS results from an uncommitted tree, not clean-checkout, same-revision hosted, or cross-platform evidence. Nothing was staged or committed. Subsequent exact-SHA hosted macOS arm64/Windows x64 verification passed at `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea`; that later evidence does not relabel the historical working-tree results. Physical Windows packaged-save evidence remains an M4 gate, and production signing remains an SP-15 gate. No push or hosted action was authorized.

## SP-04B-L relationship and memory kernel

SP-04B-L adds independent directional affection, trust, respect, attraction, obligation, fear, resentment, rivalry, and compatibility state without a universal loyalty score. `RelationshipActionCommandPayload` uses the issuing character as the subject and names one different target; `RelationshipActionResolvedEventPayload` is the only persistent mutation path. Nonzero attraction changes require both participants to be at least 18 on the exact resolution date. Reciprocal changes require a separate command.

Every accepted action creates one deterministic consequential memory containing subject, target, canonical witnesses, authoritative date/turn, opaque meaning ID, initial severity, publicity, derived decay, applied impact, and source event. Private, Participants, Witnessed, and Public visibility is enforced by `RelationshipSummaryQuery` in `Game.Application`. Subjects receive exact detailed dimensions and their active retained memories; other existing observers receive only visible active memories and no exact dimensions. Archived and distant summaries are subject-only.

History is bounded to 64 detailed links and 128 archived summaries per subject, 16 detailed memories per link, and one fixed-size distant aggregate. Retention uses effective severity/recorded turn/ID for memories and recorded importance/last-change turn/ID for links; evicted state folds into checked fixed-size summaries. Relationship IDs hash the ordered subject/target sequence, and memory IDs hash the resolution date/command ID using exact versioned UTF-8 framing. Relationship source-event IDs frame both the authoritative date and command identity, so reusing a command ID on a later date cannot alias the earlier causal event.

`RelationshipWorldSnapshot` version 1 is a separate default-empty `WorldSnapshot` subsystem registered as `simulation.relationships@1`. Save schema 5 includes it in canonical checksums. Schema 4 is authenticated before the explicit 4→5 migration injects empty relationship state and recomputes the checksum; geography, character data, source files, and autosave candidates remain unchanged.

SP-04B-L local verification and exact-SHA hosted macOS arm64/Windows x64 verification passed. The hosted evidence establishes the unchanged suites, deterministic assertions, import/export/smoke path, and artifact manifests on both hosted release-target architectures; it does not convert the recorded Apple Silicon wall-clock measurements into hosted performance evidence. The package-specific matrix, focused commands, raw local performance measurements, checksums, and limitations are recorded in the [SP-04 plan](plans/SP-04-characters-family-marriage-succession.md). Marriage, romance, household expansion, birth, death, inheritance, succession, content, UI, battle integration, and platform behavior remain deferred.
