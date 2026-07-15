# SP-04 — Characters, Family, Marriage, and Succession

## Metadata

| Field | Value |
|---|---|
| Status | Active — SP-04A/SP-04B exact-SHA hosted verified; SP-04C0 locally verified; later packages pending |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-01](SP-01-simulation-calendar-determinism-saves.md), [SP-02](SP-02-content-localization-modding-research.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Make characters persistent political actors whose abilities, memories, families, households, relationships, ambitions, and succession choices drive campaign outcomes across generations.

## Non-goals

- Sexually explicit content through version 1.0.
- Treating every world character with full-detail simulation at all times.
- Reducing loyalty or relationships to one universally repairable number.
- Guaranteeing that every eligible character is romanceable.

## Requirements

- Support historical and custom characters with structured names, courtesy names, origins, cultures, ages, health, abilities, aptitudes, traits, flaws, reputations, ambitions, offices, titles, wealth, and estates.
- Keep personal retinue, family, household, subfaction, employer, faction, and imperial allegiance distinct.
- Track important bilateral relationships through affection, trust, respect, attraction, obligation, fear, resentment, rivalry, and compatibility.
- Store consequential memories with actor, target, witnesses, date, meaning, severity, publicity, decay/inheritance rules, and source event.
- Limit high-detail active relationships/memories to meaningful links while retaining summarized history for other acquaintances.
- Support principal spouses, concubines, children, widowed relatives, protected dependents, hostages, and household staff.
- Support political marriage, adult non-explicit romance routes, widow remarriage, culturally defined household practices, adoption, guardianship, education, inheritance, and succession.
- Interactive romance requires both characters to be at least 18, eligible, and able to consent under current state.
- Childhood betrothal is political-only until both parties are adults.
- Captivity, coerced marriage, and defeated-household incorporation remain political actions with legitimacy, loyalty, trauma, resentment, family, and diplomatic effects; they never create positive romance scenes.
- Children inherit family identity and possible traits but acquire education, relationships, and abilities through simulation rather than exact cloning.
- On death/incapacity, succession resolves legal rules, designation, adoption, claims, offices, retinues, household interests, and competing political support.

## Public contracts

- Extends `EntityId` for characters, families, households, relationships, memories, ambitions, and succession claims.
- `CharacterActionCommand`, `RelationshipActionCommand`, `MarriageProposalCommand`, `HouseholdDecisionCommand`, and `DesignateHeirCommand` extend `CampaignCommand`.
- Birth, death, marriage, memory, relationship, household, and succession outcomes extend `CampaignEvent`.
- `CharacterProfile`, `RelationshipSummary`, `HouseholdView`, and `SuccessionView` are read-only queries filtered by player knowledge.
- Character/family state persists in `WorldSnapshot`; content origins are recorded through `ContentManifest`.
- Character state contributes commanders and relationship modifiers to `BattleSetup`; `BattleResult` returns wounds, deaths, captures, rescues, and shared memories.

## Data flow

```text
Character/family content + scenario state + campaign events
→ personal/household state
→ actions and proposals
→ eligibility, personality, authority, and relationship resolution
→ memories, relationship changes, births/deaths, claims, and succession events
→ political, administrative, and battle consumers
```

## Implementation workstreams

1. Define character, family, household, ability, aptitude, trait, ambition, and reputation records.
2. Implement relationship dimensions, meaningful-link limits, memory creation/decay/inheritance, and knowledge filtering.
3. Implement retinues, patronage, recommendations, employment history, and personal resources.
4. Implement political marriage, adult romance progression, spouses/concubines, household conflict, and non-explicit scenes.
5. Implement pregnancy/birth abstractions, children, education, guardianship, adoption, and coming of age.
6. Implement death, incapacity, inheritance, claims, heir designation, regency hooks, and disputed succession.
7. Connect campaign/battle events to reputation, memories, loyalty, relationships, and household consequences.

## Active package: SP-04A foundations

SP-04A is locally verified and accepted as exact revision `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea`. It adds version-1 character, identity, family, household, state, snapshot, typed-content, authoritative-read-model, checksum, and schema-4 save contracts. Its corrective legacy-save boundary validates schema-specific raw JSON before deserialization and checksum canonicalization so malformed explicit-null schema 1–3 sources fail deliberately and recovery can preserve every candidate file. The omniscient read models are explicitly named `AuthoritativeCharacterProfile` and `AuthoritativeHouseholdView`; the public-contract names `CharacterProfile` and `HouseholdView` remain reserved for the later observer-aware, player-knowledge-filtered application query layer.

The pre-remediation candidate is disqualified: after its initially recorded 2026-07-14 checks, `GeographyTests.PathfindingUsesRoutesOnlyAndMeetsInteractionBudget` failed the unchanged `<50 ms` threshold. That failed audit remains historical failure evidence and is not relabeled as passing. Corrected-tree evidence on 2026-07-15 passed 39 `SaveStoreTests`, the 36/29/3/1 focused simulation-character/content-character/published-schema/architecture checks, `./scripts/validate.sh`, `./scripts/test.sh Release` (107 Simulation.Core, 66 Game.Content, and 18 repository tests), `git diff --check`, and `git lfs fsck`. Validation retained 1,295 records, 2,820 translations, and registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; the authoritative soak checksum remains `cc6cba9f2b5408921fdbcd15a8d5494ca2351e73d7f3052f16702a09639af702`. These results justify only “locally verified in an uncommitted working tree”: no staging or commit occurred, and there is no clean-checkout, same-revision hosted, or cross-platform evidence. Hosted macOS/Windows verification remains pending; physical Windows packaged-save evidence remains an M4 gate, and production signing remains an SP-15 gate. See [the character foundation guide](../CHARACTERS.md) for commands and performance measurements.

Subsequent exact-SHA evidence on 2026-07-15 does not retroactively relabel that historical working-tree evidence. Accepted revision `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea` passed [hosted macOS arm64 and Windows x64 validation, build, complete tests, import, native export, automated smoke, manifest inspection, and artifact upload](../evidence/SP-04A-EXACT-SHA-eaa3aaf.md). Physical Windows packaged-save evidence remains an M4 gate, and production signing remains an SP-15 gate.

This package does not implement relationships, memories, marriage, romance, birth progression, death, succession, retinues, battle integration, historical rosters, or presentation. Therefore all full SP-04 acceptance criteria below remain unchecked.

### SP-04A request-derived verification matrix

This matrix reconstructs the observable requirements of the authorized SP-04A implementation request. It was recorded during audit closeout and does not claim that a separate pre-implementation package-planning approval occurred. It neither amends the full SP-04 acceptance criteria nor marks SP-04 complete.

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| A01 | M2 is active; SP-01/SP-02 are complete; scope stays within character/family/household foundations | Plan and dependency review | Local pass |
| A02 | Version-1 character, identity, family, household, state, and snapshot contracts are explicit | Contract tests and review | Local pass |
| A03 | Fictional bilingual fixture covers at least three characters, one family/household, a head, parent/child, and all five identity kinds | Focused content tests | Local pass |
| A04 | Construction, typed loading, checksums, owning-pack diagnostics, and rollback are input-order deterministic and reject invalid references/state | Focused simulation/content tests | Local pass |
| A05 | Authoritative queries derive deterministic age and independent family/household membership, return canonical defensive copies, and do not consume names reserved for knowledge-filtered queries | Contract review and focused tests | Local pass |
| A06 | Every owning pack validates authored/overridden typed records independently; resolved graphs honor override order, reject invalid packs atomically, and require Korean/English names plus exact typed-reference metadata | Loader integration tests and repository validation | Local pass |
| A07 | Character state persists in `WorldSnapshot`, schema-4 saves require the character payload, and canonical checksums cover it | Current-schema save tests | Local pass |
| A08 | History-backed schema-3 and inferred schema-1/2 fixtures authenticate before migration; malformed explicit-null and constructor-backed primary saves recover; direct failures are controlled; every candidate source and failed-migration source remains unchanged | Literal fixture, checksum, corruption, recovery, guard, and preservation tests | Local pass; schema-1/2 field compatibility unverified |
| A09 | Published version-1 authoring schemas define all five typed character payloads | Draft 2020-12 evaluator-backed positive/negative tests | Local pass |
| A10 | No character mutation commands/events, historical roster, UI, or built-in content/manifest change enters SP-04A | Diff and architecture review | Local pass |
| A11 | Repository validation, full tests, diff check, and LFS check pass on the integrated tree | Local repository gates | Local pass |
| A12 | A 1,000-character construction/query measurement is recorded without a brittle wall-clock assertion | Detailed focused test on local macOS | Local macOS pass (non-threshold) |
| A13 | The same accepted revision passes deterministic validation on hosted macOS and Windows | Exact-SHA clean-checkout hosted evidence | Hosted macOS arm64/Windows x64 pass at `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea` |
| A14 | Relationships, memories, marriage, succession, bounded history, battle integration, and player-knowledge-filtered queries | Later independently verifiable packages | Deferred |

The approved next package was SP-04B-L: bounded relationship dimensions and consequential memories, including meaningful-link limits, canonical persistence, and knowledge-filtered read-only summaries. Its local implementation and verification are recorded below; hosted acceptance remains a separate package.

## Active package: SP-04B-L directional relationships and memories

SP-04B-L adds a deterministic directional `subject → target` relationship kernel with eight independent `0..100` dimensions, compatibility from `-100..100`, adult-only nonzero attraction changes, one registered `relationship_action.v1` command, and one registered `relationship_action_resolved.v1` event. Every accepted event applies one subject-only impact and creates one causally linked consequential memory. It adds no loyalty aggregate, reciprocal implicit mutation, romance, marriage, succession, content, UI, battle integration, or general character authority over geography/resources.

Memory publicity is observer-filtered in `Game.Application`: the subject receives exact detailed dimensions and active memories, while other existing characters receive only active memories allowed by Private/Participants/Witnessed/Public visibility and never receive exact dimensions. Archived records and the distant aggregate are subject-only. State remains bounded per subject to 64 detailed relationships, 16 detailed memories per relationship, 128 archived summaries, and one fixed-size distant aggregate.

`RelationshipWorldSnapshot` is an independent version-1 subsystem added to `WorldSnapshot` as default-empty state and registered as `simulation.relationships@1`. Save schema 5 requires the complete relationship snapshot. The authenticated 4→5 migration rejects unexpected schema-4 relationship data, injects the empty subsystem, preserves geography/character state, and recomputes the schema-5 checksum. The complete 1→2→3→4→5 chain remains forward-only. The literal schema-4 fixture contains nonempty character history reconstructed from the exact schema-4 contract at `eaa3aaf`; schema-1/2 field compatibility remains unverified as disclosed in the SP-04A record.

SP-04B-L local verification passed, and accepted revision `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a` subsequently passed [exact-SHA hosted macOS arm64/Windows x64 verification](../evidence/SP-04B-EXACT-SHA-ff7420f.md). SP-04 and M2 remain Active; SP-05 remains blocked.

### SP-04B-L verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| B01 | M2/SP-04 are Active; SP-01/SP-02 are complete; no ADR or extra path is required | Source-of-truth and project-architect review | Local pass |
| B02 | Directional dimensions, separate impacts, exact bounds, adult-only attraction, and no universal loyalty score | Contract review and focused tests | Local pass |
| B03 | One registered command/event path revalidates at resolution, cancels invalidated commands, mutates only the subject, and creates one memory | Integration tests and event-causality review | Local pass |
| B04 | Character actors gain relationship authority only; synthetic/geographic/resource actor semantics remain unchanged | Actor-authority integration tests | Local pass |
| B05 | Memories retain canonical participants/witnesses, date/turn, meaning, severity, publicity, decay, impact, and source event | Focused Core/Application tests | Local pass |
| B06 | Relationship and memory IDs use exact invariant SHA-256 framing and reject identity/collision mismatches | Golden-ID and malformed-snapshot tests | Local pass |
| B07 | Exact 64/16/128 limits, deterministic eviction, folded summaries, distant aggregation, checked counters, and defensive copies hold | Bounded-history, overflow, shuffled-input, and copy tests | Local pass |
| B08 | Observer summaries expose exact dimensions/archives only to the subject and visible active memories only to allowed existing observers | Complete publicity matrix and decay tests | Local pass |
| B09 | Relationship state is canonical, checksum-covered, input-order invariant, mutation-sensitive, restorable, and save/load compatible | Checksum, serialization, restore, and save tests | Local pass |
| B10 | Schema 5 and authenticated 4→5 plus complete 1→5 migrations preserve data/source bytes and recover from malformed candidates | Literal fixture, migration, corruption, recovery, and byte-preservation tests | Local pass; schema-1/2 field compatibility unverified |
| B11 | Registered pending/diagnostic payloads round-trip and malformed/null/unsupported/mistyped state fails deliberately | JSON and validation tests | Local pass |
| B12 | 1,000-character/16,000-link/64,000-memory fixture plus 1,000 actions, one query, checksum, save, and load meet local budgets | Raw Apple Silicon macOS measurements | Local macOS pass |
| B13 | Focused suites and repository validation/test/diff/LFS gates pass | Local commands listed below | Local pass |
| B14 | Historical SP-04A evidence, limitations, and disqualified geography result remain unchanged | Documentation and diff audit | Local pass |
| B15 | Same accepted revision passes hosted macOS arm64 and Windows x64 | Exact-SHA hosted verification in SP-04B-H | Hosted macOS arm64/Windows x64 pass at `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a` |
| B16 | Marriage, household expansion, birth/death/succession, battle, content, UI, platform, and release behavior | Later packages | Deferred |

Local performance was measured on 2026-07-15 using macOS 26.5.1 build 25F80 on arm64 and .NET SDK 10.0.301 with:

```bash
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj -c Release --filter FullyQualifiedName~ThousandCharacterRelationshipFixtureRecordsRawLocalPerformance --logger "console;verbosity=detailed" --no-restore
```

The repeatable fictional fixture contained 1,000 characters, 16,000 detailed directional relationships, four initial memories per relationship (64,000 initial memories), and 1,000 relationship actions resolved in one turn. Raw timings were 134.266 ms for turn processing, 0.790 ms for one subject summary, 406.157 ms for snapshot/checksum, 2,501.901 ms for save, and 1,116.485 ms for load. Its post-action checksum was `253e77b8f6b88e95185ff806871ad9f16065c504e75c7329308545dd20fe35a2`. The test asserts fixture shape and correctness but does not add hosted wall-clock assertions.

Focused local results were 54 relationship-filtered Simulation.Core tests, 57 save-filtered Simulation.Core tests, 6 Game.Application tests, and 1 architecture-filtered repository test. `./scripts/validate.sh` retained 1,295 records, 2,820 translations, and registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` passed 158 Simulation.Core, 66 Game.Content, 6 Game.Application, and 18 repository tests. The authoritative ten-year/1,000-entity soak checksum is now `8430e2054d15fdb9a6e0c54a88b20de3b34dbca7ba80030b30676041773e7155` because newly captured worlds include the authoritative relationship subsystem. `git diff --check` and `git lfs fsck` also passed locally.

Exact-SHA hosted verification at `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a` passed the same complete 158/66/6/18 suites on macOS arm64 and Windows x64, plus validation, build, import, native export, automated smoke, manifests, artifact upload, archive integrity, and executable-architecture inspection. The soak checksum is established by its exact-source golden assertion plus both complete hosted Simulation.Core suite passes; it was not printed by CI. The local wall-clock measurements above remain local Apple Silicon evidence and are not relabeled as hosted performance. See the [SP-04B-H evidence report](../evidence/SP-04B-EXACT-SHA-ff7420f.md).

## Active package: SP-04C0 descriptor, agency, and typed kinship

SP-04C0 is locally verified in the current uncommitted working tree. It advances the character snapshot, definition, state, and authoritative-query contracts to version 2 and registers `simulation.characters@2`. Character definitions now retain the schema-5 `nameKey` while requiring a matching structured primary name, optional courtesy name, explicit content origin and owning/override-pack lineage, historical classification, source IDs, optional culture and origin IDs, and typed flaws. Character state retains schema-5 `parentIds` while requiring matching biological, legal-adoptive, or unspecified-legacy parent links plus explicit vital, health, incapacity, custody, and custodian state. The authoritative profile returns defensive typed parent and child links and the complete descriptor/condition state.

Strict authored version-1 records remain accepted and normalize to runtime version 2 without rewriting source packs. Explicit version-2 authoring adds courtesy names, flaws, origin kind, culture/origin references, typed kinship, and condition state; the published JSON schema exposes closed v1/v2 payloads. Original record ownership is preserved through canonical override lineage, and an invalid overriding pack rolls back both record data and lineage. Korean and English text are required for primary and courtesy names. Non-fictional v2 authored characters require source evidence; fictional v2 characters use custom origin, while source-less fictional v1 records retain a narrowly grandfathered authored classification.

Save schema 6 requires character contract v2 and `simulation.characters@2`. The authenticated 5→6 migration first verifies the literal schema-5 checksum/shape, then maps retained names to structured names, adds legacy-unknown provenance, empty flaws, default condition, and `UnspecifiedLegacy` parent links. It never guesses biological kinship. Schema-5 relationship state is preserved, the source file remains byte-identical, and the complete 1→2→3→4→5→6 chain remains forward-only. Contract-v2 fields injected into a v1 historical save are rejected even though the v1 checksum projection omits those fields.

This package adds no character mutation command/event, retinue/resource/employment model, marriage/romance, birth/death resolution, succession, UI, battle integration, historical roster, or shipped content. `CharacterOriginKind.Generated` exists as a versioned runtime value for later work but is not accepted by the authored v2 content boundary. Therefore the full SP-04 acceptance criteria remain unchecked and SP-05 remains blocked.

### SP-04C0 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| C01 | M2/SP-04 are Active; SP-01/SP-02/SP-03 and SP-04A/B dependencies are satisfied; scope stays inside C0 | Source-of-truth and package-boundary review | Local pass |
| C02 | Version-2 structured names, content lineage, classification, culture/origin, flaws, condition, and typed parent/child links are explicit and defensively queried | Contract review and focused simulation tests | Local pass |
| C03 | Runtime rejects null/unsupported/non-canonical descriptor, origin, flaw, condition, custody, and kinship state, including missing custodians and impossible dead/critical combinations | Malformed-state and cross-field validation tests | Local pass |
| C04 | Biological, legal-adoptive, and unspecified-legacy links persist distinctly; retained `parentIds` agree exactly; child links are indexed without inventing consent or spouse semantics | Focused kinship, persistence, and query tests | Local pass |
| C05 | Strict authored v1/v2 payloads normalize to canonical runtime v2; bilingual courtesy names, typed flaws, origin rules, culture/location references, and exact metadata are enforced | Content-loader and published-schema tests | Local pass |
| C06 | Owning-pack and canonical override lineage survive valid overrides, remain input-order deterministic, and roll back atomically with an invalid pack | Content integration and shuffled-input tests | Local pass |
| C07 | Current schema-6 saves require complete v2 character and v1 relationship state and recover from missing/null/unsupported/mistyped input without rewriting candidates | Current-save, recovery, and source-byte tests | Local pass |
| C08 | A history-backed schema-5 fixture authenticates before migration; 5→6 and 1→6 preserve prior data and map legacy parent IDs only to `UnspecifiedLegacy` | Frozen checksum, migration, corruption, and byte-preservation tests | Local pass; schema-1/2 field compatibility remains unverified |
| C09 | Canonical checksums are input-order invariant, mutation-sensitive, and cover every new field; the 10-year/1,000-entity golden is updated | Checksum and soak tests | Local pass |
| C10 | A 1,000-character construction/query measurement records correctness without a brittle wall-clock threshold | Detailed focused test on local Apple Silicon macOS | Local macOS pass (non-threshold) |
| C11 | Repository validation, complete tests, diff check, and LFS check pass on the integrated tree | Local repository gates | Local pass |
| C12 | No command/event, historical roster, shipped content, UI, platform, marriage, birth/death resolution, or succession behavior enters C0 | Diff and architecture review | Local pass |
| C13 | The same accepted revision passes deterministic validation on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pending; no commit or hosted action authorized |

Local verification on 2026-07-15 used macOS 26.5.1 build 25F80 on arm64 and .NET SDK 10.0.301. `./scripts/validate.sh` retained 1,295 records and 2,820 translations with registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` passed 204 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. Focused character construction/validation passed 41 tests; save/recovery passed 82 tests. The 1,000-character fixture constructed in 31.241 ms and performed 1,000 profile lookups plus one household lookup in 6.811 ms. The authoritative ten-year/1,000-entity checksum is `37504979fcaa25789cc9e12af7084c351d115c1195054e062ca7f7ea6ba943dd`. `git diff --check` and `git lfs fsck` passed locally.

These results prove only an uncommitted local working tree. They are not clean-checkout, exact-SHA, hosted, cross-platform, physical-Windows, signing, or release evidence. No files were staged or committed, no remote action ran, and no historical SP-04A/B evidence was relabeled.

## Edge cases and failure handling

- Invalid parentage cycles, duplicate spouses where rules forbid them, impossible ages, and self-relationships fail content validation.
- A proposal invalidated by death, captivity, allegiance change, or marriage resolves as cancelled rather than mutating invalid state.
- Death during pregnancy, regency, missing heirs, simultaneous deaths, and extinct families have deterministic fallback rules.
- A captured household member remains an independent character with relationships and claims; no character becomes an inventory item.
- Player-character death transfers play to a valid chosen/legal successor when available; otherwise the campaign presents a defined end or continuation choice supported by scenario rules.
- Missing nonessential relationship scene content falls back to systemic text without blocking simulation.

## Performance budget

- Routine relationship/memory processing remains inside the overall 3-second campaign-turn budget for 1,000 historical plus generated characters.
- Detailed social-graph queries for one character return within 100 ms.
- Memory storage is bounded through archival summaries and cannot grow without limit.

## Tests

- Age, adulthood, eligibility, kinship, and household validation tests.
- Multi-dimensional relationship and memory creation/decay/inheritance tests.
- Political marriage versus romance-path tests, including cancellation and refusal.
- Hard tests preventing interactive romance below age 18 or during invalid consent states.
- Birth, education, adoption, inheritance, simultaneous death, regency hook, and disputed succession tests.
- Battle capture/death/rescue round-trip tests.
- Save/load and tier-transition preservation tests.

## Acceptance criteria

- [ ] Historical and custom characters persist with structured identity, abilities, relationships, memories, family, and retinue.
- [ ] Relationship dimensions and memories create distinct political consequences without one universal loyalty score.
- [ ] Political marriage and adult non-explicit romance both function and may diverge emotionally.
- [ ] Household, children, education, death, inheritance, and succession resolve deterministically.
- [ ] Coercive household actions never produce positive romance progression.
- [ ] Player-character succession preserves campaign continuity when a valid successor exists.
- [ ] Character processing meets performance and bounded-history requirements.

## Risks

| Risk | Mitigation |
|---|---|
| Relationship simulation becomes an unreadable all-to-all graph | Track high-detail meaningful links and summarize inactive acquaintances. |
| Family systems generate excessive event text | Prioritize events tied to decisions, memories, ambitions, and political consequences. |
| Historical household practices conflict with content boundaries | Separate political/legal modeling from adult romance presentation and enforce hard eligibility validation. |
| Succession destroys player agency arbitrarily | Expose claims/support clearly and provide advance preparation, designation, adoption, and regency tools. |

## Deferred work

- Explicit content of any kind.
- Fully bespoke romance routes for the entire 1.0 roster.
- Genetics beyond bounded inheritable traits and appearance parameters.
- Post-1.0 religious or philosophical household-law expansion.
