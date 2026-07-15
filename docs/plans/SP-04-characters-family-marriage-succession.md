# SP-04 — Characters, Family, Marriage, and Succession

## Metadata

| Field | Value |
|---|---|
| Status | Active — through SP-04E3 exact-SHA hosted; later packages pending |
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

SP-04C0 is accepted at exact revision `7d4612d21784ceebbcd574ea00231785b9408036` with passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04C0-EXACT-SHA-7d4612d.md). It advances the character snapshot, definition, state, and authoritative-query contracts to version 2 and registers `simulation.characters@2`. Character definitions now retain the schema-5 `nameKey` while requiring a matching structured primary name, optional courtesy name, explicit content origin and owning/override-pack lineage, historical classification, source IDs, optional culture and origin IDs, and typed flaws. Character state retains schema-5 `parentIds` while requiring matching biological, legal-adoptive, or unspecified-legacy parent links plus explicit vital, health, incapacity, custody, and custodian state. The authoritative profile returns defensive typed parent and child links and the complete descriptor/condition state.

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
| C13 | The same accepted revision passes deterministic validation on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Hosted macOS arm64/Windows x64 pass at `7d4612d21784ceebbcd574ea00231785b9408036` |

Local verification on 2026-07-15 used macOS 26.5.1 build 25F80 on arm64 and .NET SDK 10.0.301. `./scripts/validate.sh` retained 1,295 records and 2,820 translations with registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` passed 204 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. Focused character construction/validation passed 41 tests; save/recovery passed 82 tests. The 1,000-character fixture constructed in 31.241 ms and performed 1,000 profile lookups plus one household lookup in 6.811 ms. The authoritative ten-year/1,000-entity checksum is `37504979fcaa25789cc9e12af7084c351d115c1195054e062ca7f7ea6ba943dd`. `git diff --check` and `git lfs fsck` passed locally.

These results prove only an uncommitted local working tree. They are not clean-checkout, exact-SHA, hosted, cross-platform, physical-Windows, signing, or release evidence. No files were staged or committed, no remote action ran, and no historical SP-04A/B evidence was relabeled.

Subsequent exact-SHA evidence does not relabel the historical local measurements. Accepted revision `7d4612d21784ceebbcd574ea00231785b9408036` passed hosted macOS arm64 and Windows x64 validation, complete tests, import, native export, automated smoke, manifest inspection, and artifact verification. Physical Windows remains an M4 gate, and signing/Steam remain SP-15 gates.

## Active package: SP-04C1 career and social-action kernel

SP-04C1 is accepted at exact revision `d5d2705d3516c67a06e127dcfa867a854b37a21f` with passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04C1-EXACT-SHA-d5d2705.md). It is bounded to the career/social half of workstream 3: retinue invitations and departures, patronage offers and endings, recommendations, employment offers and endings, bounded history, and their explicit relationship-memory consequences. SP-04A/B/C0 and SP-01/SP-02/SP-03 dependencies are satisfied. This package requires no ADR because it preserves deterministic inward-only domain mutation, stable namespaced IDs, forward-only versioned persistence, bilingual-content boundaries, and the existing non-explicit-content rule.

`CharacterCareerWorldState` is a version-1 default-empty subsystem registered as `simulation.character_careers@1`. Its authoritative query exposes canonical defensive proposals, retinues and memberships, patronage bonds, recommendations, employment tenures, and folded history. Character and household principals are explicit; retinue membership does not imply employment, faction allegiance, office, title, or resource ownership. No global exclusive-retinue rule is invented.

`character_action.v1` registers eleven explicit action variants under one command envelope; `character_action_resolved.v1` is the only persistent mutation path. Submission validates actor agency and preplans the complete outcome. Resolution revalidates eligibility; stale offer responses become explicit invalidation outcomes. Deterministic length-framed IDs bind proposals, services, recommendations, consequences, and events to their causal command/date. Unknown outer or nested discriminators fail deliberately.

Character actions may carry zero or more explicit directional relationship consequences. The event is prepared independently against both career and relationship state, then both candidate states commit only after every validation succeeds. Generic memories use `(sourceEventId, consequenceIndex)` identity and relationship contract v2; legacy relationship-action memories retain their exact v1 IDs and declare their legacy identity scheme. The observer-filtered application summary advances to contract v2 and reports the generalized causal source without revealing exact dimensions to unauthorized observers.

Save schema 7 requires complete character-v2, relationship-v2, and career-v1 state plus all three registered system versions. The authenticated 6→7 migration starts from a literal nonempty schema-6 fixture generated by the accepted SP-04C0 contract, preserves v1 memory IDs while adding source metadata to snapshot and diagnostic events, injects empty career state, recomputes the checksum, and leaves source bytes unchanged. Injected schema-7 career or character-action fields are rejected before migration. The complete 1→7 chain remains forward-only; the pre-existing schema-1/2 compatibility limitation is unchanged.

History, active state, and event workload are explicitly bounded: 8 active proposals per recipient, 64 members per retinue, 16 patronage bonds involving a character, 8 employment tenures per employee, 64 relationship consequences per character action, 32 witnesses per consequence, 64 detailed completed records per category/involved character, and 64 recommendations per involved character. Deterministic retention folds evicted count/date history into checked fixed-size aggregates. Relationship history retains its independent SP-04B limits.

### SP-04C1 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| C101 | M2/SP-04 are Active; SP-01/SP-02/SP-03 and SP-04A/B/C0 dependencies are satisfied; scope stays inside C1 | Source-of-truth and package-boundary review | Local pass |
| C102 | Version-1 proposals, retinues, memberships, patronage, recommendations, employment, history, actions, and outcomes are explicit and defensively queried | Contract review and focused Core tests | Local pass |
| C103 | Registered commands/events enforce actor agency, submission validation, resolution revalidation, deterministic cancellation/invalidation, and exact affected IDs | Campaign integration and negative tests | Local pass |
| C104 | Career and relationship consequences prepare atomically; malformed or overflowed consequences leave both subsystems unchanged | Cross-subsystem rollback tests and implementation review | Local pass |
| C105 | Generic memories have source-event/index identity; legacy relationship memory IDs survive relationship v2; observer filtering remains intact | Golden-ID, migration, relationship, and application-query tests | Local pass |
| C106 | Principal, role, proposal, participant, date/turn, lifecycle, uniqueness, and cross-record invariants reject malformed or non-canonical state | Snapshot/action malformed-state tests | Local pass |
| C107 | Exact active/detail bounds, deterministic eviction, checked folded aggregates, and defensive copies hold | Limit, shuffle, overflow, and copy tests | Local pass |
| C108 | Current schema-7 saves require complete career-v1/relationship-v2 state and all system versions; registered diagnostics round-trip and unknown discriminators fail | Current-save, serialization, and recovery tests | Local pass |
| C109 | Literal nonempty schema-6 history authenticates before 6→7; migration preserves memory IDs/data/source bytes and rejects injected future fields | Frozen fixture, checksum, migration, corruption, and byte-preservation tests | Local pass; schema-1/2 field compatibility remains unverified |
| C110 | Canonical checksum/restore/save/load cover career state and relationship v2; the ten-year golden is updated | Checksum, round-trip, and soak tests | Local pass |
| C111 | 1,000-character/1,000-action fixture records bounded shape, query, checksum, save, and load measurements without a brittle threshold | Raw local Apple Silicon macOS measurement | Local macOS pass; full three-second SP-04 turn budget remains unmet/unproven |
| C112 | Repository validation, complete tests, diff/LFS checks, and C1-touched-file format verification pass; unrelated whole-tree formatter baseline is disclosed | Local repository gates | Local pass |
| C113 | No personal resources, marriage/romance, lifecycle/succession, faction/court, combat, AI, content, UI, platform, or release behavior enters C1 | Diff and architecture review | Local pass after independent review and remediation |
| C114 | The same accepted revision passes deterministic validation on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Hosted macOS arm64/Windows x64 pass at `d5d2705d3516c67a06e127dcfa867a854b37a21f` |

The raw local performance fixture contains 1,000 characters and 1,000 registered actions: 450 offers, 450 acceptances, and 100 recommendations, resulting in 150 active retinue memberships, 150 patronage bonds, 150 employment tenures, 100 recommendations, and 1,000 generic relationship memories. On the final corrected Apple Silicon macOS tree, combined submission and resolution measured 15,649.077 ms, one career query 2.200 ms, snapshot/checksum 80.037 ms, save 285.490 ms, and load 174.532 ms. The save was 387,824 bytes and the checksum was `313d5183aa8b3d60a064076da5a91082217771753c08f958212ef6880a45e55b`. These are raw local measurements and the test asserts shape/correctness only. The full SP-04 three-second turn budget remains unchecked; this result is not relabeled as a pass for that criterion.

Local integrated verification on 2026-07-15 used macOS 26.5.1 build 25F80 on arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retained 1,295 records and 2,820 translations with registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` passed 243 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests; focused runs passed 22 career and 99 save/recovery tests. The authenticated schema-6 fixture checksum is `90c27f2dd9954e0d3d2a304e9b661bf1ef25f2e4f620743d78bc60382d780bd4`, and the current ten-year/1,000-entity golden checksum is `14136a6a415a7c687cc5265709f1bbe7384c67527d9dc328dc111c4d6a523ff2`. `git diff --check`, `git lfs fsck`, and formatter verification over every C1-touched C# file passed. Whole-solution formatter verification separately reports six pre-existing whitespace findings in unchanged `tools/Tools.ContentPipeline/LaterHanLocationImporter.cs`; those findings are outside C1 and were preserved rather than silently folded into this package.

The required independent read-only review initially found five issues: a public direct-mutation bypass, relationship consequences surviving an invalidated response, an unbounded consequence list, omission of a recommendation role from affected IDs, and an inaccurate authentication-order sentence. The corrected tree makes the helper internal, suppresses invalidated consequences through the integrated event path, enforces and tests an exact 64-consequence maximum, includes the optional recommendation role, and fixes the wording. Targeted re-review confirmed all five resolutions and found no new issue in the fix delta.

These results established an uncommitted local working tree only. They were not clean-checkout, exact-SHA, hosted, cross-platform, physical-Windows, signing, Steam, or release evidence; C114 remained pending at that point.

Subsequent exact-SHA evidence does not relabel those historical local measurements. Accepted revision `d5d2705d3516c67a06e127dcfa867a854b37a21f` passed hosted macOS arm64 and Windows x64 validation, complete 243/71/6/18 suites, import, native export, automated smoke, manifest inspection, artifact upload, and static artifact verification. See the [SP-04C1 exact-SHA report](../evidence/SP-04C1-EXACT-SHA-d5d2705.md). Physical Windows remains an M4 gate, signing/Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet.

Personal resources, faction/subfaction/imperial allegiance, offices/titles, marriage/romance, household expansion, birth/death resolution, inheritance/succession, battle, AI, historical/shipped content, UI, platform, signing, and release behavior remained deferred by C1. Consequently every full SP-04 acceptance criterion below remained unchecked and SP-05 remained blocked.

## Active package: SP-04C2 personal-wealth kernel

SP-04C2 implements the personal-resource half of workstream 3 as a separately versioned abstract-wealth subsystem. SP-01/SP-02/SP-03 and SP-04A/B/C0/C1 dependencies are satisfied. The package preserves deterministic pure-.NET domain ownership, registered command/event mutation, stable namespaced IDs, forward-only persistence, and the existing content boundary, so no ADR is required.

`CharacterResourceWorldState` is a version-1 default-empty subsystem registered as `simulation.character_resources@1`. It exposes defensive canonical accounts, recent ledger entries, and folded history through `IAuthoritativeCharacterResourceWorldQuery`. Accounts are sparse and character-owned: absence means zero, positive balances produce exact derived account IDs, and zero-balance rows are removed. The quantity is deliberately abstract wealth, not a named currency, commodity, exchange rate, debt, income, estate valuation, treasury, or economy contract.

`character_resource_action.v1` is the registered outer command; its sole C2 variant is `transfer_wealth.v1`. `character_resource_action_resolved.v1` is the persistent event. The source and recipient must be distinct existing characters who have been born and are alive on the resolution date; the source must be able to act, and the amount must be positive. Resolution revalidates against intervening commands. A successful event atomically debits and credits candidate state with checked conservation; stale insufficient-funds or recipient-overflow outcomes cancel explicitly without partial mutation. Resource calendar state advances for later-day commands.

Versioned length-framed SHA-256 derivation binds account IDs to characters, action events to authoritative dates and commands, transfers to events, and incoming/outgoing entries to transfers and characters. Each success creates an exact two-sided ledger. At most 64 detailed entries remain per involved character; deterministic eviction folds incoming/outgoing counts, amounts, and date range into checked fixed-size history. Snapshot, query, action, outcome, nested record, cross-record, date/turn, ID, canonical-order, retention, and overflow validation reject malformed state before commit.

Save schema 8 requires the complete resource-v1 snapshot and `simulation.character_resources@1` in addition to the existing character-v2, relationship-v2, and career-v1 state. A frozen literal nonempty schema-7 fixture was generated with the exact accepted SP-04C1 binary at `d5d2705d3516c67a06e127dcfa867a854b37a21f`; its authoritative stored checksum is `0c9033c2a0e145a73218aa234f3725878fc7b781b9e6a8e83adad74b10b79d72`, and the checked-in fixture SHA-256 is `34e0c3955df7784d6c4e6b766ce0d0a1d74734552d89ce28e9fb3dd8ce7058a5`. The authenticated 7→8 migration rejects future resource snapshot/system/command/event data, injects empty resource state, recomputes the destination checksum, and preserves source bytes. Current raw-shape validation, diagnostics, recovery, restore, checksum, save/load, and the complete 1→8 chain cover C2.

### SP-04C2 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| C201 | M2/SP-04 are Active; SP-01/SP-02/SP-03 and SP-04A/B/C0/C1 dependencies are satisfied; scope stays inside C2 | Source-of-truth and package-boundary review | Local pass |
| C202 | Version-1 sparse personal-wealth accounts, two-sided ledger, folded history, authoritative query, and exact stable IDs are explicit and defensive | Contract review and focused Core tests | Local pass |
| C203 | Registered commands/events enforce actor/recipient existence, birth, life, agency, distinct parties, positive amount, and exact affected IDs | Campaign integration and negative tests | Local pass |
| C204 | Transfers conserve wealth and commit atomically; stale insufficient funds and recipient overflow cancel without partial state, including later-day campaign resolution | Direct/campaign rollback and calendar tests | Local pass after remediation |
| C205 | Account/event/transfer/entry IDs and source command/event/date/turn/counterparty fields validate exactly | Golden-ID, malformed-state, and event-validation tests | Local pass |
| C206 | The 64-entry per-character bound, deterministic eviction, checked folded aggregate, canonical ordering, overflow rejection, and defensive copies hold | Limit, shuffle, overflow, and copy tests | Local pass |
| C207 | Current schema-8 saves require complete resource-v1 state and system registration; raw nested shape, diagnostics, recovery, and unknown discriminators fail deliberately | Current-save and serialization tests | Local pass |
| C208 | Literal nonempty exact-C1 schema-7 history authenticates before 7→8; migration preserves prior data/source bytes and rejects injected future resource fields | Frozen fixture, checksum, migration, corruption, and byte-preservation tests | Local pass; schema-1/2 field compatibility remains unverified |
| C209 | Canonical checksum/restore/save/load include resource state; the ten-year golden advances to `07507a7058b13603e3ecc377870f9e13551ce5bde237012ba90c377cd5e2f79c` | Checksum, round-trip, and soak tests | Local pass |
| C210 | A 1,000-character/1,000-transfer fixture records bounded shape, query, checksum, save, and load measurements without a brittle threshold | Raw local Apple Silicon macOS measurement | Local macOS pass; full three-second SP-04 turn budget remains unmet/unproven |
| C211 | Repository validation, complete tests, diff/LFS checks, and C2-touched-file format verification pass | Local repository gates | Local pass |
| C212 | No estates, household/family/faction/court treasuries, debt/currency/commodities, income/economy/land, relationship effect, marriage/lifecycle/succession, content, UI, battle, AI, platform, or release behavior enters C2 | Diff and architecture review | Local pass |
| C213 | Independent architecture and verification review finds no unresolved issue after remediation | Review record and focused regressions | Local pass |
| C214 | The same accepted revision passes deterministic validation on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Hosted macOS arm64/Windows x64 pass at `e2d9590afc409da30aef86226a8d90a0023fbda3` |

The final local fixture contains 1,000 characters and plans/applies 1,000 direct transfers, resulting in 1,000 accounts and 2,000 detailed ledger entries. On Apple Silicon macOS, transfer processing measured 8,077.354 ms, query/snapshot JSON 9.857 ms, full-world checksum 91.771 ms, save 196.321 ms, and load 95.900 ms. The snapshot JSON contained 1,462,065 characters; the save was 259,434 bytes with checksum `989c242311a374c57ea4c306219d08aac12f2b0111f44d1d4df659c1cc657bf0`. These are raw correctness-fixture measurements without wall-clock assertions. They do not satisfy or waive the full SP-04 three-second campaign-turn budget.

Independent review found one campaign integration defect and one coverage gap. Later-day resource commands initially prepared outcomes against the correct authoritative date but retained the turn-start resource calendar during commit; the corrected candidate advances date and turn monotonically. Recipient-overflow cancellation initially had direct-state coverage only; the final campaign regression submits two initially valid incoming transfers, verifies one success and one `RecipientOverflow` cancellation, and observes exactly one ledger pair. Final architecture and verification re-review found no remaining issue.

Local integrated verification on 2026-07-15 used macOS 26.5.1 build 25F80 on arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retained 1,295 records and 2,820 translations with registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` built with zero warnings and passed 280 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The current ten-year/1,000-entity golden checksum is `07507a7058b13603e3ecc377870f9e13551ce5bde237012ba90c377cd5e2f79c`; the headless soak completed in 830 ms on the same local machine. Touched-file formatter verification, `git diff --check`, and `git lfs fsck` passed.

The implementation remained uncommitted at this local documentation point. Those results proved only the integrated working tree and did not establish exact-SHA, hosted, or cross-platform evidence.

Subsequent exact-SHA evidence does not relabel the historical local measurements. Accepted revision `e2d9590afc409da30aef86226a8d90a0023fbda3` passed hosted macOS arm64 and Windows x64 validation, two complete 280/71/6/18 suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and static artifact verification. See the [SP-04C2 exact-SHA report](../evidence/SP-04C2-EXACT-SHA-e2d9590.md). Physical Windows remains an M4 gate, signing/Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet.

Opaque character-owned estate holdings are reserved for SP-04C3 so SP-04 can establish inheritance and succession inputs without circularly importing SP-06. SP-06 will later own geography, production, taxation, land grants, and estate economic value. Household/family/faction/court treasuries, debt, currency, commodities, income, spending, prices, relationship effects, marriage/romance, lifecycle, inheritance/succession, content, UI, battle, and AI remain outside C2. Every full SP-04 acceptance criterion below therefore remains unchecked, and SP-05 remains blocked.

## Active package: SP-04C3 opaque character-estate holdings

SP-04C3 implements the estate input half of workstream 3 as a separate version-1, default-empty, state-only subsystem registered as `simulation.character_estate_holdings@1`. SP-01/SP-02/SP-03 and SP-04A/B/C0/C1/C2 dependencies are satisfied. Independent architecture review approved the separate boundary without an ADR because it preserves deterministic pure-.NET ownership, stable namespaced identity, forward-only persistence, and SP-06's ownership of physical/economic estate meaning.

Each `CharacterEstateHoldingState` contains only `EstateId` and `OwnerCharacterId`. `estate:` IDs are globally unique and independent of the owner; separately reconstructed ownership can therefore change without changing estate identity. Persisted owners must be existing characters born by the snapshot date, but dead, incapacitated, captive, or hostage owners remain valid. Holdings are canonical by estate ID, queries and snapshots are defensive, and the accepted workload bound is exactly 64 holdings per character. SP-04F must handle a succession candidate that would exceed this bound explicitly and atomically. C3 adds no command/event, mutator, transfer history, automatic cleanup, or inheritance behavior.

Save schema 9 requires the complete estate snapshot and system registration. The frozen literal 319,764-byte schema-8 fixture was generated from the exact accepted SP-04C2 source/API at `e2d9590afc409da30aef86226a8d90a0023fbda3`; its authoritative stored checksum is `ba485b0efc67e7cff38cf6de4b4536dbda2191ee87f5577ff1ee2d1d0031424f`, and its file SHA-256 is `6419cd00b10697fe04b2f1e32bca8cfdc59b3cfb02f50091113e8735afa55428`. It retains two characters, nonempty relationship/career state, two wealth accounts, 128 ledger entries, two folded histories, and 65 resource command/event diagnostics. The authenticated 8→9 migration rejects injected future estate state, adds only the empty estate snapshot/system version, preserves the complete pre-C3 snapshot, envelope metadata, manifests, diagnostics, and source bytes, then computes the schema-9 checksum.

### SP-04C3 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| C301 | M2/SP-04 are Active; dependencies through accepted C2 `e2d9590` are satisfied; the separate C3 boundary requires no ADR | Source-of-truth and architecture review | Local pass |
| C302 | Version-1 snapshot/state/query contracts contain only owner-independent `EstateId` and `OwnerCharacterId`; empty is the default | Contract and API tests | Local pass |
| C303 | Estate IDs are valid, `estate:`-namespaced, globally unique, immutable across owner replacement, and never silently reused | Invalid-ID, namespace, duplicate, and stable-identity tests | Local pass |
| C304 | Owners exist and are born by the snapshot date; dead/incapacitated/captive owners remain valid; 64 succeeds and 65 fails | Lifecycle, malformed-owner, and boundary tests | Local pass; F owns succession-overflow resolution |
| C305 | Dead-owner holdings persist and remain queryable across restore/save/load until later inheritance resolution | Nonempty current-schema round trip | Local pass |
| C306 | Input order cannot affect snapshot/JSON/checksum; owner or estate changes affect checksum; returned collections are defensive | Shuffle, checksum, copy, and reconstruction tests | Local pass |
| C307 | C3 exposes no runtime mutation, command/event, ownership history, or automatic dead-owner cleanup | Reflection/API and complete-diff review | Local pass |
| C308 | Schema 9 requires complete estate state and exactly one compatible system registration; nonempty checksum/restore/save/load works | Raw-shape, standalone, and round-trip tests | Local pass |
| C309 | Literal exact-C2 schema 8 authenticates before migration and preserves the complete pre-C3 snapshot, envelope metadata, diagnostics, and source bytes | Frozen fixture, full normalized equality, and byte-preservation tests | Local pass; schema-1/2 field compatibility remains unverified |
| C310 | Schema 8 rejects injected C3 data; current missing/partial/duplicate/dangling/wrong-namespace estate data fails without changing source bytes | Future-injection, semantic-corruption, and recovery tests | Local pass |
| C311 | The complete 1→9 migration chain and frozen historical checksums remain valid | Historical migration/corruption tests | Local pass |
| C312 | Stable owner-independent estate identities support later F ownership changes while leaving all physical/economic meaning to SP-06 | Contract seam and architecture review | Local pass |
| C313 | No geography, acreage, control, grants, claims, value, yield, rent, tax, production, treasury, household/family/faction/court ownership, content, UI, battle, or AI enters C3 | Complete diff and dependency review | Local pass |
| C314 | A 1,000-character/8,000-holding fixture records construction, owner queries, snapshot, checksum, save, and load without a brittle threshold | Raw local Apple Silicon macOS measurement | Local macOS pass; full three-second SP-04 budget remains unmet/unproven |
| C315 | Repository validation, complete tests, touched-file formatting, diff, and LFS gates pass | Local repository gates | Local pass |
| C316 | The same accepted revision passes deterministic validation and complete suites on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Hosted macOS arm64/Windows x64 pass at `7b9f795320e5f4c14aa7e14185e7ba035fdf6847` |

The final focused 154-test slice passed. On Apple Silicon macOS, the 1,000-character/8,000-holding fixture measured 68.607 ms construction, 751.881 ms for 1,000 owner queries plus snapshot JSON serialization, 21.056 ms checksum, 325.787 ms save, and 210.018 ms load. The estate JSON contained 1,016,034 characters; the compressed save was 37,677 bytes with checksum `c033184371cf80b022a174acc5fa799345fa5eca9a89feec90f75b8a4c5bc83a`. These raw measurements carry correctness assertions but no wall-clock threshold and do not satisfy or waive the full SP-04 three-second budget.

Adversarial verification identified a read-only owner-query eligibility coupling and missing complete checksum/migration assertions. The final tree applies birth eligibility only while validating persisted owners, proves shuffled and changed owner/identity checksum behavior, rejects semantic save corruption without changing source bytes, and compares the complete normalized pre-C3 world during migration. Architecture and verification re-review report no open code finding.

Local integrated verification on 2026-07-15 used Darwin arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retained 1,295 records and 2,820 translations with registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` built with zero warnings and passed 312 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The ten-year/1,000-entity headless soak completed with checksum `798a96c57375fb3012c55175195430604a66a658fdf786d7fd0f0ba4e96cce9b`. Touched-file formatting, `git diff --check`, and `git lfs fsck` passed. Those results establish the local package tree only and are not relabeled as hosted evidence.

Accepted revision `7b9f795320e5f4c14aa7e14185e7ba035fdf6847` subsequently passed hosted macOS arm64 and Windows x64 validation, two complete 312/71/6/18 suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and static artifact verification. See the [SP-04C3 exact-SHA report](../evidence/SP-04C3-EXACT-SHA-7b9f795.md). Physical Windows remains an M4 gate, signing/Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet.

Physical/economic estate records, geography, production, taxation, grants, marriage/romance, lifecycle mutation, inheritance/succession resolution, content, UI, battle, and AI remain deferred. Every full SP-04 acceptance criterion below remains unchecked, the full three-second budget remains unmet, and SP-05 remains blocked.

## Accepted package: SP-04D0 immutable marriage foundation

SP-04D0 is the state/query/persistence foundation for workstream 4. Dependencies through accepted SP-04C3 revision `7b9f795320e5f4c14aa7e14185e7ba035fdf6847` are satisfied. The boundary preserves pure deterministic .NET, stable namespaced identity, inward dependencies, forward-only persistence, the existing non-explicit-content rule, and `CharacterWorldSnapshot.HouseholdStates` as the sole household residence authority; no ADR is required.

`CharacterMarriageWorldState` is a version-1, default-empty subsystem registered as `simulation.character_marriages@1`. Versioned state covers explicit practices, proposals, political betrothals, legal unions, adult non-explicit romance routes, and checked folded history. Practices explicitly configure legal/romance minimum ages, principal-spouse and concubinage limits, political betrothal before legal age, widow remarriage, and prohibited direct-line/sibling kinship. No rule is inferred from culture, family, or household identity.

Legal unions and romance routes are adult-only. Configured minimum ages are bounded from 18 through 100, and exact birthday-day age calculation is authoritative. Minors may enter only political betrothal state when the selected practice explicitly permits it; betrothal is always political arrangement and never romance. Voluntary paths require life, capacity, and freedom from custody. Coercive classification remains political, never romantic, and produces no D0 relationship effect or scene. Active established unions remain valid across later incapacity/custody until an explicit later-package end.

Persisted IDs, versions, enums, pairs, dates, turns, terminal data, participant/practice references, kinship, roles, and source links are validated before exposure. Every accepted proposal owns exactly one correctly typed outcome; nonaccepted proposals own none. Proposal and romance-route creation commands are unique. Fulfilled betrothals identify one unique exact political-arrangement union and agree with its proposal resolution command/date/turn. All retained turn coordinates are bounded by the authoritative `CampaignCalendar`, and folded history cannot predate its owner.

Eligibility and construction share duplicate-pair, configured form, and global active-legal-relationship constraints. Bounds are 8 active proposals per recipient, 64 active unions plus political betrothals per character, and 64 retained records per category and involved character. Practice caps are themselves bounded to 8 principal spouses and 64 concubinage relationships. Canonical global and per-character queries and captured snapshots are defensive.

D0 registers no command, event, runtime mutator, household membership, household movement, guardianship or authority inference, proposal progression, romance progression, relationship consequence, lifecycle behavior, succession behavior, faction/court/diplomacy integration, content, localization, scene, UI, AI, or platform code. SP-04D1 owns political marriage workflow; D2 owns adult non-explicit romance progression; D3 owns household decisions, conflict, and coercion effects.

Save schema 10 requires complete marriage state and `simulation.character_marriages@1`. The 320,019-byte literal schema-9 fixture retains the complete pre-D0 world, one nonempty estate holding, and 65 command/event diagnostics. Its stored historical checksum is `1ef0f8728311ab217e84d9e6ff432342a7bac85b74aae6eee2cf92159d541684`, and its file SHA-256 is `ab0df6f7740af51bc4eed7d73d97fd5dba38ea273db738c511a289e6bea084ce`. A detached test against the exact accepted C3 binary independently reproduced that checksum. The authenticated 9→10 migration rejects injected future marriage state/system data, adds only the empty marriage snapshot and system registration, computes the current checksum, and preserves the complete source and source bytes. Current raw-shape and semantic-corruption tests cover source-byte preservation; the complete 1→10 chain remains forward-only, with the existing schema-1/2 field-compatibility limitation unchanged.

### SP-04D0 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| D001 | M2/SP-04 are Active; dependencies through accepted C3 are satisfied; D0 remains an immutable foundation and requires no ADR | Source-of-truth and architecture review | Local pass |
| D002 | Version-1 practice/proposal/betrothal/union/romance/history/snapshot/query contracts are explicit, default-empty, canonical, and defensive | Contract/API and focused tests | Local pass |
| D003 | Legal unions and romance are adult-only on exact birthday boundaries; minors enter only explicitly enabled political betrothal state | Age-boundary, minor-state, and practice-policy tests | Local pass |
| D004 | Voluntary eligibility observes life/capacity/custody; coercion remains political and never creates positive romance classification | Condition matrix, coercion, and reflection tests | Local pass; effects deferred to D3 |
| D005 | Accepted proposals own exactly one typed outcome, other statuses own none, and proposal/romance creation commands cannot be reused | Cross-record causal and duplicate-source tests | Local pass |
| D006 | A fulfilled betrothal identifies one unique exact political-arrangement union and common fulfillment resolution | Fulfillment-link, mismatch, missing, and wrong-command tests | Local pass |
| D007 | All retained dates/turns are coherent and bounded by the authoritative calendar; folded history cannot predate the character | Future-coordinate, ordering, birth, and terminal-state tests | Local pass |
| D008 | Eligibility applies the same duplicate, global legal-relationship, and configured union-form limits that state construction enforces | 64-bound, form-limit, duplicate-pair, and category tests | Local pass |
| D009 | Exact active/detail bounds, canonical pairs/order, stable IDs, checksum sensitivity, and defensive query/snapshot copies hold | Boundary, shuffle, checksum, invalid-ID, and copy tests | Local pass |
| D010 | Existing character household state remains the sole residence authority; no duplicate household state or inferred practice enters D0 | API/reflection and architecture review | Local pass |
| D011 | D0 exposes no command/event/public mutator or runtime workflow and imports no later lifecycle/succession/faction/content/UI/platform behavior | Complete diff, dependency, and reflection review | Local pass |
| D012 | Schema 10 requires complete marriage state and exact system registration; nonempty save/load/restore/checksum and semantic corruption are covered | Current raw-shape, round-trip, corruption, and source-byte tests | Local pass |
| D013 | Literal nonempty exact-C3 schema 9 authenticates before migration; 9→10 and 1→10 preserve prior state/diagnostics/source bytes and reject future data | Frozen fixture, exact-C3 binary check, migration, injection, and recovery tests | Local pass; schema-1/2 field compatibility remains unverified |
| D014 | Every marriage record category and consent affects the checksum; shuffled input is invariant; the ten-year golden is updated | Field-sensitivity, canonicalization, and soak tests | Local pass |
| D015 | A 1,000-character fixture records bounded construction/query/snapshot/checksum behavior without a brittle threshold | Raw local Apple Silicon macOS measurement | Local macOS pass; full three-second SP-04 budget remains unmet/unproven |
| D016 | Repository validation, complete Release tests, touched-file formatting, diff, and LFS gates pass | Local repository gates | Local pass |
| D017 | Independent adversarial re-review finds no remaining correctness or boundary blocker | Read-only review and focused rerun | Local pass after remediation |
| D018 | The same accepted revision passes deterministic validation and complete suites on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pass at `f7fef247178776d7c6fb1c4bed56f09dece76ff4` |

Local integrated verification on 2026-07-15 used Darwin 25.5.0 arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retained 1,295 records and 2,820 translations with registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` built with zero warnings and passed 374 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The focused marriage/save slice passed 200 tests. The ten-year/1,000-entity checksum is `0a92aa6dec435a9b33a399898ed7985210d7142dc10027b23a0bc4e392666b36`. Touched-file formatting, `git diff --check`, and `git lfs fsck` passed.

Independent adversarial verification initially found missing proposal/outcome bijection and betrothal fulfillment identity, unbounded future turns, eligibility/state limit mismatches, pre-birth history, and incomplete semantic-save/fixture/checksum evidence. The corrected tree adds the exact causal links and calendar bounds, aligns all legal-relationship eligibility categories, rejects impossible history, authenticates nonempty C3 estate state, and expands current-save and checksum coverage. Final review then corrected the practice-minimum age for coercive unions and prohibited coercive command reuse only for positive romance creation/completion while allowing the same command to end or invalidate a route. Release-focused re-review passed 38/38 marriage tests and found no remaining D0 correctness or boundary blocker.

The final local Release performance fixture contains 1,000 characters, 250 active proposals, and 250 active adult romance routes. Construction measured 38.405 ms, 1,000 aggregate record queries measured 67.561 ms, and snapshot plus full-world checksum measured 191.298 ms; checksum `6542e48b7ccdceee890fc757fd9104c6b4f73164ef4263b7b451947766777e06`. The test asserts shape and correctness, not a wall-clock threshold. These working-tree results are not themselves exact-SHA, hosted, cross-platform, physical-Windows, signing, Steam, release, or full-turn performance evidence. Every full SP-04 acceptance criterion remains unchecked and SP-05 remains blocked.

Subsequent exact-SHA evidence on 2026-07-15 does not retroactively relabel the raw working-tree performance measurements. Accepted revision `f7fef247178776d7c6fb1c4bed56f09dece76ff4` passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and static artifact verification](../evidence/SP-04D0-EXACT-SHA-f7fef24.md). D018 therefore passes at that revision. Physical Windows remains an M4 gate, production signing and Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet.

## Accepted package: SP-04D1 political marriage workflow

SP-04D1 is the registered political-marriage workflow on the accepted D0 foundation. M2 and SP-04 remain Active; dependencies through D0 exact revision `f7fef247178776d7c6fb1c4bed56f09dece76ff4` are satisfied. The package preserves pure deterministic .NET, inward dependencies, explicit practices, stable namespaced identity, registered commands/events, forward-only saves, non-explicit content, and `CharacterWorldSnapshot.HouseholdStates` as the sole residence authority. No ADR is required.

`CharacterMarriageActionCommandPayload` (`character_marriage_action.v1`) carries one of five version-1 actions: propose political marriage, respond to a proposal, withdraw a proposal, cancel a political betrothal, or fulfill a political betrothal. `CharacterMarriageActionResolvedEventPayload` (`character_marriage_action_resolved.v1`) carries one of eight typed outcomes. Length-framed SHA-256 identities bind action events to resolution date/command, proposals to kind/date/command, betrothals to accepted political proposals, and unions to accepted legal proposals. The outer command, event, world-snapshot, marriage snapshot/state/query, and `simulation.character_marriages@1` versions remain unchanged.

D1-created state is always `Political` and `PoliticalArrangement`. A direct legal proposal requires both participants to be adults and currently alive, capable, free, unrelated under the selected practice, and within widow-remarriage, form, mixed-practice, duplicate, and global limits. Political betrothal permits a minor only when the explicit practice permits it. Only the recipient may accept/refuse, only the proposer may withdraw, and either participant may cancel or fulfill an active betrothal. A valid direct acceptance atomically creates one union; a valid betrothal acceptance atomically creates one betrothal. A still-active proposal defeated by an intervening rule becomes typed `Cancelled`; missing/terminal targets and temporarily ineligible fulfillment produce generic command cancellation without ending the betrothal.

Fulfillment atomically creates a second legal proposal already accepted by the fulfillment command, creates the political-arrangement union sourced by that proposal, fulfills the original betrothal, links the union, and shares one command/date/turn across the four-record chain. The source active betrothal is evaluated as the relationship being replaced, so it is not miscounted as a duplicate or extra legal relationship.

Submission validates shape, authority, phase, target, and applicable current rules. Resolution replans against authoritative state, constructs and validates the full candidate, and converts expected races or capacity failures into deterministic cancellation before any event is applied. Apply-time validation independently requires the Commands phase, causal command, deterministic event ID, canonical affected IDs, authoritative date/turn, exact nested action/outcome, and a complete valid candidate. A malformed or injected event leaves state and event-order coordinates unchanged.

Retention is bounded by proposal-root causal groups. An accepted proposal remains with its direct betrothal or union. A fulfilled betrothal links both proposal roots so the proposal/betrothal/proposal/union chain consumes two proposal slots per participant and is retained or folded atomically. Active outcomes pin the complete group; terminal groups are selected by their latest child terminal turn/date and stable group ID. Capacity that cannot preserve a required group cancels before mutation. Folding writes checked `2/1/1` proposal/betrothal/union counts for a fulfillment chain and updates both participants' date range. Independent romance-route retention remains unchanged.

Save schema 11 is required because D1 adds persisted command/action/event/outcome discriminators. The 321,757-byte literal schema-10 fixture was generated from detached exact D0 source `f7fef247178776d7c6fb1c4bed56f09dece76ff4`; it retains nonempty marriage, estate, relationship, career, resource, household, and 65-command/event diagnostic state. Its stored historical checksum is `6e644f0db882a7b7440653060c5b635d6020844a1f032ee05afbe48dd90bce12`, and its file SHA-256 is `05d52f0445c777a186e1a4c0a5eb18638c5b11b4f40a9c9c07bebafeac8f6c0e`. The authenticated 10→11 migration rejects every D1 discriminator in schema-10 pending/diagnostic collections, preserves the snapshot, diagnostics, manifests, and source bytes, and advances only the envelope compatibility contract. Historical checksums now cover schema 10, and the complete 1→11 chain remains forward-only with the existing schema-1/2 field-compatibility limitation.

D1 does not add adult romance progression, romantic scenes or attraction effects, household membership/movement/authority/conflict, coercion consequences, third-party guardian/family/faction/court authority, coerced unions, union ending, lifecycle, succession, content, localization, UI, AI, battle, or platform behavior. D2 owns adult non-explicit romance progression; D3 owns household decisions, conflict, and coercion effects. Every full SP-04 acceptance criterion remains unchecked.

### SP-04D1 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| D101 | M2/SP-04 are Active; D0 is accepted; D1 boundary is unblocked and requires no ADR | Source-of-truth and architecture review | Local pass |
| D102 | Version-1 political action/outcome contracts and outer discriminators are explicit, registered, and platform-neutral | Contract/API/JSON tests | Local pass |
| D103 | D1 construction cannot encode romantic or coerced state and imports no D2/D3 behavior | Workflow negatives and complete-diff review | Local pass |
| D104 | Commands require the Commands phase and exact proposer/recipient/participant authority at submission, resolution, and application | Authority, phase-injection, and target tests | Local pass after remediation |
| D105 | Direct unions use exact adult birthday/capacity/custody rules; minors enter only explicitly enabled political betrothal | Boundary and policy tests | Local pass |
| D106 | Refusal, withdrawal, stale typed cancellation, terminal-target generic cancellation, and same-turn races leave no partial outcome | Concurrent workflow tests | Local pass |
| D107 | Direct acceptance atomically creates exactly one political-arrangement union | Outcome/state/causality tests | Local pass |
| D108 | Betrothal acceptance and either-participant cancellation are atomic and non-romantic | Outcome/authority tests | Local pass |
| D109 | Fulfillment creates the exact second accepted proposal/union and common four-record causality; temporary ineligibility preserves the active betrothal | Fulfillment and cancellation tests | Local pass |
| D110 | Kinship, widow-remarriage, custody/capacity, duplicate, global, form, and mixed-practice limits agree between eligibility and construction | Foundation and mixed-practice race tests | Local pass after remediation |
| D111 | Exact event/causal/affected/nested data, candidate prevalidation, background-injection rejection, and rollback hold | Tamper, phase, overflow, and no-mutation tests | Local pass after remediation |
| D112 | Length-framed SHA-256 identities have exact goldens; shuffled submission produces identical events/checksum | Golden-ID and replay tests | Local pass |
| D113 | The 8/64 bounds, active pinning, terminal selection, causal grouping, checked two-party folding, and saturation cancellation hold | Proposal/betrothal/mixed/fulfillment aging and capacity tests | Local pass after remediation |
| D114 | Snapshot/restore/checksum/current-save/diagnostic/unknown-discriminator coverage includes D1; loaded pending commands resolve identically | Restore/save/load/replay tests | Local pass after remediation |
| D115 | Exact-D0 schema 10 authenticates before 10→11 and preserves nonempty prior state, diagnostics, source, and source bytes | Frozen fixture identity and migration test | Local pass |
| D116 | Schema 10 rejects D1 discriminators; current raw shape, corruption, recovery, future schema, and complete 1→11 chain remain covered | Save/recovery suites | Local pass |
| D117 | Ten-year deterministic soak golden remains exact | Cross-platform assertion in complete Core suite | Pass at `653ce71d24bd81435ded9e65022dc29afd8f4810` |
| D118 | A 1,000-character/250-proposal fixture records bounded workflow, snapshot/checksum, JSON, and gzip behavior without a brittle threshold | Raw local Apple Silicon measurement | Local macOS pass; full SP-04 budget unproven |
| D119 | Independent adversarial and architecture re-review find no remaining correctness or package-boundary blocker | Read-only reviews and focused reruns | Local pass after remediation |
| D120 | Repository validation, complete Release suites, touched-file formatting, diff, and LFS gates pass | Local repository gates | Local pass |
| D121 | The same accepted revision passes deterministic validation and complete suites on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pass at `653ce71d24bd81435ded9e65022dc29afd8f4810` |

Final local verification on 2026-07-15 uses Darwin 25.5.0 arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` builds with zero warnings and passes 407 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The focused marriage/save slice passes 233 tests; focused final marriage review passes 66 tests. The ten-year/1,000-entity checksum remains `0a92aa6dec435a9b33a399898ed7985210d7142dc10027b23a0bc4e392666b36`. Touched-file formatting, `git diff --check`, and `git lfs fsck` pass.

Independent review remediated candidate-capacity turn abort, background-phase event injection, mixed-practice eligibility/construction mismatch, loaded pending-command replay evidence, and two-root fulfillment-chain retention. The final retention regressions prove active-chain pinning, saturated fulfillment cancellation, and ended-chain atomic folding with `2/1/1` counts and union-end ordering. Architecture and verification re-review report no remaining blocker.

The final representative local Release performance run contains 1,000 characters and 250 bounded political proposals. Workflow measured 1,525.199 ms; snapshot plus checksum measured 120.596 ms; snapshot JSON was 930,668 bytes; gzip was 32,440 bytes; checksum was `9610a37a8e68825fa9d907ab58832240f1357b5f3ef227f4d61a57bd86f8d215`. The test asserts shape/correctness, not a wall-clock threshold. These working-tree measurements are not exact-SHA, hosted, cross-platform, physical-Windows, signing, Steam, release, or full-turn performance evidence.

Accepted revision `653ce71d24bd81435ded9e65022dc29afd8f4810` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04D1-EXACT-SHA-653ce71.md). D117 and D121 therefore pass at that revision. Physical Windows remains an M4 gate, production signing and Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet. Every full SP-04 acceptance criterion remains unchecked, SP-05 remains blocked, and the D2 local candidate follows.

## Accepted package: SP-04D2 mutual-consent romance workflow

SP-04D2 is implemented on accepted D1 revision `653ce71d24bd81435ded9e65022dc29afd8f4810`. The dependency is satisfied, M2/SP-04 remain Active, SP-05 remains blocked, and no ADR is required. The package remains deterministic pure .NET and preserves the unchanged outer `character_marriage_action.v1` command plus `character_marriage_action_resolved.v1` event. Five nested actions cover offer, recipient accept/refuse, initiator withdrawal, expected-level advance, and either-participant ending.

An offer requires both distinct participants to meet the explicit practice's current voluntary-romance rules. Acceptance revalidates those rules and is the recipient's affirmative consent; it atomically removes the invitation and creates one route-v2 record at progress 1. Refusal or withdrawal removes the active invitation without retained invitation history. Progression increments exactly once, completes at level 4, and never creates legal state, relationship/memory effects, household state, scenes, content, or presentation. `ExpectedProgressLevel` plus stable event ordering makes repeated and conflicting queued commands deterministic. D2 creates no `Invalidated` outcome; lifecycle/condition integration remains D3 scope. Ending requires only participant authority, not mutual agreement.

Active invitations are unique by canonical pair, bounded to 8 incoming per recipient and 64 involving either participant, and are never folded. Version-2 routes retain immutable invitation/acceptance evidence plus the latest positive progression command/date/turn. Progress 1 must identify acceptance; later progress identifies a distinct uniquely retained advance command. Invitation and route identities use namespaced length-framed SHA-256. Version-1 routes remain version 1, never gain synthesized fields, and remain actionable under their accepted 0→1, 1/2→+1, 3→Completed4, and active4→Completed4 semantics. Existing route retention remains 64 per participant with active pinning, terminal resolution ordering, and atomic checked folding for both participants.

Save schema 12 advances the marriage snapshot/query/system to v2 and adds active invitations while preserving v1 practice/proposal/betrothal/union/history contracts and the outer D1 command/event. The 325,473-byte schema-11 fixture was generated from detached exact D1 source `653ce71d24bd81435ded9e65022dc29afd8f4810`. It contains one active and one ended v1 route, a pending D1 command, and nonempty D1 command/event diagnostics; stored checksum `9c5dc3195649bfde2626f95c7cf2573d4acbc4c2a081b9af0ac9d30c74f9c8fb`; file SHA-256 `ce6f737a9e3a608dfaaaeaf422f74e134a8fa7073ad4026a9aa1354007174d14`. The authenticated 11→12 step rejects D2 discriminators and v2 marriage state, injects empty invitations, advances the exact marriage system registration, preserves legacy routes and all prior state/diagnostics/source bytes, and extends historical checksums through schema 11. Duplicate historical marriage-system registrations fail as controlled compatibility errors.

D2 does not add romantic marriage proposals or unions, attraction/affinity/memory/trauma/conflict effects, household decisions or movement, family/guardian/household/faction/court authority, coerced romance, lifecycle, succession, content, localization, UI, AI, RNG, battle, or platform behavior. D3 remains the owner of household decisions, conflict, coercion effects, and atomic condition/lifecycle integration. Every full SP-04 acceptance criterion remains unchecked.

### SP-04D2 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| D201 | Nested actions/outcomes, invitation/route IDs, and snapshot/query/system v2 are explicit, registered, and round-trip | Contract, golden-ID, and discriminator tests | Local pass |
| D202 | Offer/accept provides adult bilateral consent; refuse/withdraw authority is exact | Workflow, birthday, practice, condition, and authority tests | Local pass |
| D203 | Political betrothal/union and romance may coexist without relationship or household mutation | Divergence workflow tests | Local pass |
| D204 | Expected-level progression advances once, completes only at 4, and either participant may end | Progression/completion/end tests | Local pass |
| D205 | Legacy v1 levels 0–4 remain actionable without version or causal-field synthesis | Explicit v1 progression and migration tests | Local pass after remediation |
| D206 | Same-pair offer, response, repeated advance, advance/end, completion/end, and capacity races use stable event order without partial state | Concurrent command tests | Local pass after remediation |
| D207 | Apply-time replan, exact nested JSON/affected IDs/phase/causality, candidate validation, and rollback hold | Tamper and phase-injection tests | Local pass |
| D208 | Invitation 8/64 bounds and route active pinning/terminal folding are deterministic and atomic | Bound, saturation, retry, and aging tests | Local pass |
| D209 | Coercive commands cannot offer, accept, advance, or complete romance, including legacy routes; ending remains non-positive | Causal-state and command-reuse tests | Local pass after remediation |
| D210 | Current schema-12 saves round-trip v2 state and every pending action; replay events and final checksums are exact | Save/load/replay and semantic-tamper tests | Local pass after remediation |
| D211 | Exact-D1 schema 11 authenticates, migrates legacy routes without rewriting them, rejects D2 data, and preserves source bytes | Frozen fixture and migration/recovery tests | Local pass |
| D212 | Ten-year/1,000-entity deterministic golden is exact | Complete Core suite | Local pass; checksum `ba4eccd512e7bf699c3360032f2a5f007b362cc16ff718a487a6d082357e65b2` |
| D213 | Independent architecture and adversarial review find no remaining correctness or package-boundary blocker | Read-only post-remediation review | Local pass after remediation |
| D214 | Same revision passes repository gates and hosted macOS arm64/Windows x64 evidence | Local gates plus exact-SHA hosted clean-checkout run | Pass at `62a50075ca86b3466cca9c05825d4374e6cac366` |

Final local verification on 2026-07-15 uses Darwin arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` builds with zero warnings and passes 469 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The focused marriage/save/ten-year-soak slice passes 296 tests. Touched-project format verification, `git diff --check`, and `git lfs fsck` pass.

Independent review found and remediated coercive command reuse on legacy nonterminal advances, incomplete v2 last-positive-progress causality, uncontrolled duplicate historical system registration, and race tests that did not force both hashed-event orderings. Final architecture and adversarial re-review report no remaining correctness or package-boundary blocker under the locked decision that D3, not D2, owns atomic route invalidation with condition/lifecycle mutation.

The current raw Release fixture contains 1,000 characters and 200 completed routes. Workflow measured 7,238.257 ms; snapshot plus checksum measured 113.094 ms; snapshot JSON was 1,009,019 bytes; gzip was 48,844 bytes; checksum was `6035ff878b94f261a676af83bb2d293b3585e9eb40f650721399c39f96539af0`. The test asserts shape and correctness, not a wall-clock threshold. These working-tree measurements are not exact-SHA, hosted, cross-platform, physical-Windows, signing, Steam, release, or full-turn performance evidence. The full SP-04 three-second budget remains unmet.

Accepted revision `62a50075ca86b3466cca9c05825d4374e6cac366` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04D2-EXACT-SHA-62a5007.md). D212 and D214 therefore pass at that revision. Physical Windows remains an M4 gate, production signing and Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet. Every full SP-04 acceptance criterion remains unchecked, SP-05 remains blocked, and D3 is the next package.

## Accepted package: SP-04D3 household decisions and lifecycle integration

SP-04D3 is implemented on accepted D2 revision `62a50075ca86b3466cca9c05825d4374e6cac366`. The dependency is satisfied, M2/SP-04 remain Active, SP-05 remains blocked, and no ADR is required. The package remains deterministic pure .NET, uses registered commands and events for persistent mutation, preserves character household state as the sole residence authority, and adds no explicit content.

`character_condition_action.v1` and `character_condition_action_resolved.v1` register system-authoritative incapacity, recovery, custody-entry, and custody-release changes. Every action carries the exact expected current condition. Submission and resolution revalidate character existence, life, authority, transition legality, custodian life/capacity/freedom, and exact current state; a stale or same-turn conflicting action cancels deterministically. Successful apply replaces the existing `CharacterWorldState` contents in place, so subsystem references remain valid, and atomically commits the condition plus the complete marriage-lifecycle change set. Recovery and release do not resurrect invalidated proposals, invitations, or routes.

`household_decision.v1` and `household_decision_resolved.v1` register two deliberately narrow head-authorized decisions. A living, capable, free household head may expel a non-head member. The destination household's living, capable, free head may incorporate a captive or hostage only when that head is the target's exact custodian. The package cannot expel or move a head, create or dissolve households, infer authority from family identity, or change custody as a side effect. Candidate character, household, relationship, and marriage state validates before atomic commit.

`impose_coerced_union.v1` extends the unchanged marriage command/event vocabulary. An adult, living, capable, free exact custodian may impose a legal-age political/coerced union on a living captive or hostage. The action creates one accepted political/coerced proposal and one active political/coerced union without moving either household, accepting romantic consent, or creating a positive romance route. Any active romance route for that exact pair is invalidated. Captivity, household coercion, and coerced union outcomes create a target-to-actor/custodian consequential memory with exact participants-only metadata. Applied attraction is always zero; the deterministic saturation ladder increases resentment, fear, then rivalry before reducing trust, affection, respect, and compatibility, and can end at a zero impact without making any positive dimension increase.

Incapacity or custody invalidates consent-dependent active proposals, removes invitations, and invalidates active romance routes while retaining accepted political betrothals and established coerced unions. The internal death preview is intentionally non-mutating and not a public death command: it demonstrates that a later death event can invalidate all active proposals and betrothals, remove invitations, invalidate active routes, and end every active union with `SpouseDied` in one validated candidate. Public death, widow-history preservation, succession, inheritance, estates, offices, retinues, faction/court effects, and player transfer remain later-package work, primarily SP-04F.

Save schema 13 advances only the envelope vocabulary needed for D3 commands, events, nested outcomes, optional relationship consequences, and the three new relationship-memory source kinds; existing character-v2, relationship-v2, and marriage-v2 snapshot/system versions remain unchanged. The 345,155-byte literal schema-12 fixture was generated by the detached exact D2 binary at `62a50075ca86b3466cca9c05825d4374e6cac366`. It contains one pending D2 command, one active invitation, a fully evidenced completed v2 route, and nonempty diagnostics. Its stored historical checksum is `62988012ca8ed090e62a922a2b3b357ea60d3b044d91a6dcc56b4ea92ad087dd`, and its file SHA-256 is `12ac4db8897a7ddd2f0101085231130ab48057bc22222ee203dcbfd35c1b8061`. The authenticated 12→13 migration preserves all prior state, diagnostics, source, and source bytes while schema 12 rejects every D3 discriminator, source kind, and D3-only property, including an explicit null property. Historical checksum support now extends through schema 12 and the complete 1→13 chain remains forward-only, with the previously disclosed schema-1/2 field-compatibility limitation unchanged.

### SP-04D3 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| D301 | M2/SP-04 are Active; exact D2 is accepted; D3 remains inside household/coercion/condition integration and needs no ADR | Source-of-truth and architecture review | Local pass |
| D302 | Condition and household outer contracts plus every nested action/outcome are versioned, registered, and have stable exact identities | Contract, discriminator, round-trip, and golden-ID tests | Local pass |
| D303 | Condition changes require the reserved system actor, exact expected current state, valid transitions, and deterministic replan | Authority, stale-state, lifecycle, and race tests | Local pass |
| D304 | Incapacity/custody invalidates consent-dependent state atomically while political/coerced established state survives; recovery/release cannot resurrect it | Complete proposal/invitation/route/betrothal/union lifecycle matrix | Local pass |
| D305 | Household heads alone may expel non-heads or incorporate exact-custodian captives/hostages without moving heads or duplicating residence authority | Positive/negative household campaign tests | Local pass |
| D306 | Exact custodians alone may create political/coerced accepted proposals and unions without household movement or positive romance | Coercive-union authority, state, and active-pair route tests | Local pass |
| D307 | Captivity and coercive outcomes create exact target-to-actor harmful memories; attraction remains zero and every positive dimension is nonincreasing | Metadata, source-kind, saturation-ladder, and dimension tests | Local pass after remediation |
| D308 | Internal death preview plans the complete proposal/betrothal/invitation/v1-v2-route/union lifecycle without exposing or applying a public death action | Complete non-mutating preview matrix | Local pass; public death deferred to SP-04F |
| D309 | Apply independently checks phase, causality, nested payloads, affected IDs, candidates, and relationship capacity; malformed or overflowing events roll back every subsystem | Tamper, overflow, and snapshot-equality tests | Local pass after remediation |
| D310 | Same-priority races resolve identically in both event-ID orderings and a save-loaded pending stale command replays deterministically | Ordering and pending replay tests | Local pass |
| D311 | Current schema-13 saves round-trip every D3 action and all new memory source kinds with exact final events and checksums | Current-save, pending-command, source-kind, and semantic-shape tests | Local pass |
| D312 | Exact-D2 schema 12 authenticates before 12→13, preserves rich prior state/diagnostics/source bytes, and rejects all future vocabulary and properties | Frozen fixture, migration, rejection, recovery, and byte-preservation tests | Local pass |
| D313 | Existing bounded proposal/route/memory retention and relationship consequence limits remain exact; capacity failures cannot partially mutate state | Retention and overflow regression tests | Local pass |
| D314 | Ten-year/1,000-entity deterministic golden remains `ba4eccd512e7bf699c3360032f2a5f007b362cc16ff718a487a6d082357e65b2` | Complete Core suite | Local pass |
| D315 | A 1,000-character fixture records coercion/lifecycle workflow, snapshot/checksum, JSON, and gzip behavior without a brittle threshold | Raw local Apple Silicon measurement | Local macOS pass; full SP-04 budget unmet |
| D316 | Independent architecture and verification re-review find no remaining correctness or package-boundary blocker | Read-only reviews and focused reruns | Local pass after remediation |
| D317 | Repository validation, complete Release suites, touched-file formatting, diff, and LFS gates pass | Local repository gates | Local pass |
| D318 | The same accepted revision passes deterministic validation and complete suites on hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pass at `93d38810a87707a7c4c98c7392e2a2f20dc030fb` |

Final local verification on 2026-07-15 uses Darwin arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` builds with zero warnings and passes 490 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The focused D3/save slice passes 203 tests and the complete Core suite passes 490 tests. Touched-file formatting, `git diff --check`, and `git lfs fsck` pass.

Independent review found and remediated missing custody-memory application, schema-12 acceptance of a future explicit-null consequence property, a negative-attraction fallback that violated the exact-zero rule, a relationship limit owned by the career subsystem, and incomplete death/race/save/source-kind/overflow coverage. The corrected candidate uses relationship-owned limits, atomically applies custody memories, keeps attraction exactly zero through every saturation step, strictly rejects the future property in schema 12, and covers both event-ID orderings plus complete D3 persistence and lifecycle matrices. Final architecture and verification re-review report no remaining correctness or package-boundary blocker.

The representative local Release fixture contains 1,000 characters, 200 coerced unions, 200 released captives, and 200 incapacitated characters. Workflow measured 12,802.979 ms; snapshot plus checksum measured 90.260 ms; snapshot JSON was 1,331,895 bytes; gzip was 81,218 bytes; checksum was `9d7bd98955e016f85aa6c0ea8dbeab5387efa97b584ee871603c3533e9a1d374`. The test asserts shape and correctness, not a wall-clock threshold. These working-tree measurements are not exact-SHA, hosted, cross-platform, physical-Windows, signing, Steam, release, or full-turn performance evidence. The full SP-04 three-second budget remains unmet, every full SP-04 acceptance criterion remains unchecked, and SP-05 remains blocked pending all later SP-04 packages.

Accepted revision `93d38810a87707a7c4c98c7392e2a2f20dc030fb` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04D3-EXACT-SHA-93d3881.md). D314 and D318 therefore pass at that revision. Physical Windows remains an M4 gate, production signing and Steam remain SP-15 gates, and the full SP-04 three-second budget remains unmet. Every full SP-04 acceptance criterion remains unchecked, SP-05 remains blocked, and the next dependency-ordered SP-04E package may begin.

## Accepted package: SP-04E0 legal-adoptive parent establishment

SP-04E0 is the first bounded E package on accepted D3 revision `93d38810a87707a7c4c98c7392e2a2f20dc030fb`. It registers one Commands-phase, reserved-system `character_family_action.v1`: `EstablishLegalAdoptiveParentAction`. The action carries the exact canonical current parent links and may append one `LegalAdoptive` link. Its resolved event records a versioned parentage change with exact previous/current links, resolution date/turn, source command, and stable change identity. Successful application replaces the existing `CharacterWorldState` contents in place so all inward subsystem references continue to observe the same authoritative query object.

Both characters must exist, be born, alive, and free. The adopter must be capable, at least 18 on the exact resolution date, and born before the adoptee. Adult adoptees must also be capable; adult adoption is allowed. Self-parentage, an existing link of any kind for the pair, cycles, noncanonical or stale expected links, more than two legal-adoptive parents, more than four total parents, and more than 64 legal-adoptive children for one adopter are rejected. The numeric limits apply only to new runtime actions: previously valid character-v2 snapshots are grandfathered and remain restorable.

E0 establishes current legal parentage only. It does not replace biological or unspecified-legacy links, remove a parent, move family or household membership, establish guardianship, change custody or relationships, grant inheritance or claims, imply consent, or implement pregnancy, birth, education, coming of age, death, or succession. Character-v2 stores no effective-dated adoption record. The change/event source survives only while present in bounded diagnostics; after eviction, the durable fact is the typed current link. Future effective-date-sensitive law must treat links without records as `LegacyUnknown` rather than reconstructing a date.

Before mutation, the complete unchanged marriage snapshot is reconstructed against the candidate parent graph. E0 conservatively rejects any adoption that would make any retained proposal, betrothal, union, invitation, or romance route—including terminal records—violate its practice's direct-line or sibling prohibition. It never rewrites or auto-invalidates marriage history. Apply-time replan, exact payload/affected-ID comparison, and the same full preflight make the operation atomic.

Save schema 14 changes vocabulary only: character, relationship, career, resource, estate, and marriage snapshot/system versions remain unchanged. Schema 13 authenticates with its historical checksum, rejects every E0 outer/nested discriminator and E0-only parent-link property even when explicitly null, and migrates without changing the complete snapshot, diagnostics, manifests, or source bytes. The 52,467-byte schema-13 fixture was generated by a detached test harness compiled against exact accepted D3 production source `93d38810a87707a7c4c98c7392e2a2f20dc030fb`. It contains a pending D3 command, condition/household/coercion diagnostics, all three D3 relationship-memory source kinds, and a grandfathered history-free legal-adoptive link. Its stored checksum is `540dac5d9065bbb1b66a484c550eadf4e60de7be92acacb5c716bfcf7f4956c4`; file SHA-256 is `f54e0bb16eb827b2b739384a27325dd0a279cd43f18d612085c247fd3aa1fca5`.

### SP-04E0 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| E001 | D3 is accepted; adoption-first is dependency-safe and needs no ADR under the establishment-only boundary | Source-of-truth and architecture review | Local pass |
| E002 | Outer/nested action, outcome, change, event, and stable identities are explicit and registered | Contract, round-trip, and golden-ID tests | Local pass |
| E003 | Reserved authority, exact phase/date/turn/event/change/source, and expected-current revalidation hold | Authority, birthday, stale-state, and tamper tests | Local pass |
| E004 | Life/capacity/freedom, birth order, self/duplicate/cycle, and action-local 2/4/64 limits hold without invalidating grandfathered state | Domain and campaign tests | Local pass |
| E005 | One legal-adoptive link and reciprocal child link are added in place without family, household, condition, descriptor, relationship, or marriage mutation | Snapshot/reference/defensive-copy tests | Local pass |
| E006 | Every retained marriage category and terminal state is preflighted for direct-line and sibling restrictions; permissive practices remain valid | Complete marriage-category matrix | Local pass |
| E007 | Apply-time replan, affected IDs, payload equality, rollback, and both hashed same-turn orderings are deterministic | Tamper, race, and checksum tests | Local pass |
| E008 | Pending/current E0 vocabulary round-trips, save-loaded replay is exact, and checksum is order-invariant/mutation-sensitive | Save/replay/checksum tests | Local pass |
| E009 | Exact-D3 schema 13 authenticates before vocabulary-only 13→14, preserves state/source bytes, and rejects E0 data/properties | Frozen fixture, migration, rejection, and corruption tests | Local pass |
| E010 | No guardianship, pregnancy/birth, education, coming-of-age, inheritance, succession, content, UI, AI, battle, or platform surface enters E0 | Reflection and diff review | Local pass |
| E011 | A 1,000-character/64-adoption fixture records raw workflow/checksum/serialization behavior without a brittle threshold | Local Apple Silicon measurement | Local macOS pass; full SP-04 budget unmet |
| E012 | Repository validation, complete Release suites, diff, and LFS gates pass | Local repository gates | Local pass |
| E013 | The same accepted revision passes hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pass at `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed` |

The final focused local Release run passes 48 family/schema-13 tests. Its fictional 1,000-character fixture resolves 64 bounded legal adoptions in 1,070.225 ms, captures and checksums in 7.789 ms, serializes to 881,800 JSON bytes and 23,579 gzip bytes, and produces checksum `fa8e08d551b8e3fe3e5b3d066b418aed6e2d68526c36320b2b8f0c5bf7d94e8f`. Timings are raw Apple Silicon observations without a threshold and do not establish the full SP-04 three-second campaign-turn budget.

Final local repository verification on 2026-07-15 passes the zero-warning Release build and 539 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. Touched-file formatting, `git diff --check`, and `git lfs fsck` pass.

Independent review found that the first action contract retained the caller's mutable expected-parent list after submission. The final contract clones each link into an `Array.AsReadOnly` collection. Post-submit mutation/race/checksum coverage proves pending commands and resolved diagnostics no longer share caller-owned state; a final read-only re-review reports no remaining blocker.

Accepted revision `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04E0-EXACT-SHA-30fd0ad.md). E013 therefore passes at that revision. Every full SP-04 acceptance criterion remains unchecked. Pregnancy/birth, guardianship, education, coming of age, public death, inheritance, succession, content, presentation, and platform work remain later E/F/G/X packages; SP-05 remains blocked.

## Accepted package: SP-04E1 primary-guardianship establishment

SP-04E1 is a bounded guardianship package on accepted E0 revision `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed`. It adds `simulation.character_guardianships@1`, a default-empty version-1 world snapshot, immutable active/ended guardianship records, and defensive authoritative queries for all records, the active primary guardianship of one ward, and all records involving one character. The record retains exact establishment date/turn, source command/event, and stable guardianship identity. Terminal coordinates and reasons are structurally supported for later lifecycle packages, but E1 exposes no ending workflow.

One new nested `EstablishPrimaryGuardianshipAction` reuses the reserved-system, Commands-phase `character_family_action.v1` envelope. Establishment requires exact event identity and expected-current state. Both participants must exist, be born, alive, and distinct. The guardian must be at least 18 on the exact resolution date, older than the ward, capable, and free; the ward must be under 18. Ward incapacity or custody does not block establishment. A guardian may be biological, legal-adoptive, or unrelated. Each ward may have one active primary guardian, and at most 64 retained records may involve any one character.

Persisted records validate historical establishment evidence without reapplying present-day minority, life, capacity, freedom, or custody rules. An active record therefore remains loadable after the ward becomes an adult or either participant's later condition changes. Establishment mutates only the guardianship subsystem: it does not change parentage, family, household, residence, custody, relationships, marriage, claims, inheritance, or succession. It grants no character-issued appointment authority or consent semantics and adds no co-guardians, adult regency, culture-specific adulthood, automatic birthday/death termination, education, pregnancy/birth, content, localization, UI, AI, battle, or platform behavior.

Save schema 15 adds the required `characterGuardianships` snapshot and `simulation.character_guardianships@1` registration while retaining `WorldSnapshot` contract v1 and every earlier subsystem contract. The authenticated 14→15 migration adds only an empty guardianship subsystem after validating the exact schema-14 historical checksum and rejecting E1 state, nested discriminators, and E1-only properties even when explicitly null. The 56,635-byte schema-14 fixture was generated by a detached harness compiled against exact accepted E0 production source `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed`. It retains rich D3 history, one resolved E0 adoption, and one pending E0 adoption; its stored checksum is `8cb144a3b14600bd9916b03cca20644ed69161614d2b6665cdf78fdeadad2b65`, and file SHA-256 is `381c774072a549090dd275f3fefa1bb7a3953a0a74eb3852eeb8323ad0b8f01e`.

### SP-04E1 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| E101 | E0 is accepted; primary-guardianship establishment is the smallest dependency-safe next package and needs no ADR | Source-of-truth and architecture review | Local pass |
| E102 | Version-1 snapshot/state/query/system contracts, nested action/outcome discriminators, and length-framed identities are explicit | Contract, reflection, round-trip, and golden-ID tests | Local pass |
| E103 | Reserved authority, Commands phase, exact date/turn/event/source, expected-current state, affected IDs, and in-place query identity hold | Campaign and domain tests | Local pass |
| E104 | Exact birthday, life, capacity, freedom, identity, biological/adoptive/unrelated, ward-custody, and ward-incapacity rules hold | Eligibility matrix and boundary tests | Local pass |
| E105 | One active guardian per ward and 64 retained records per involved character are exact; persisted history does not depend on current minority or condition | Retention, restore, and historical-state tests | Local pass |
| E106 | Establishment mutates only guardianship state and never infers parentage, family, household, custody, relationship, marriage, or succession effects | Complete snapshot comparison and diff review | Local pass |
| E107 | Apply-time replan, exact payload comparison, rollback, and both guardian assignments under hashed event ordering are deterministic | Tamper and same-turn race tests | Local pass |
| E108 | Pending/current E1 vocabulary, diagnostics, replay, checksum order invariance, and mutation sensitivity round-trip under schema 15 | Save/replay/checksum tests | Local pass |
| E109 | Exact-E0 schema 14 authenticates before 14→15, preserves all prior state/diagnostics/source bytes, and rejects E1 state/vocabulary/properties | Frozen fixture, migration, rejection, corruption, and byte-preservation tests | Local pass |
| E110 | Current and legacy standalone snapshots enforce complete guardianship state/system registration while allowing only complete default-empty legacy omission | Current-shape and standalone-restore tests | Local pass |
| E111 | A 1,000-character/64-guardianship fixture records raw workflow/checksum/serialization behavior without a brittle threshold | Local Apple Silicon measurement | Local macOS pass; full SP-04 budget unmet |
| E112 | Independent verification review finds no remaining correctness or package-boundary blocker | Read-only review and focused rerun | Local pass after calendar remediation |
| E113 | Repository validation, complete Release suites, diff, touched-file formatting, and LFS gates pass | Local repository gates | Local pass |
| E114 | The same accepted revision passes hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pass at `97b607ae8df77dbd5c6fa5ab6b544000208cdb0e` |

The representative local Release fixture contains 1,000 characters and resolves 64 primary guardianships. The latest raw run measured 127.412 ms for workflow and 126.120 ms for snapshot/checksum, serialized to 1,006,377 JSON bytes and 30,190 gzip bytes, and produced checksum `50e4730caedc90084911632a77bdeff94076516321307a7e8bc4daeeb3320db8`. The test asserts shape and correctness, not a wall-clock threshold. These working-tree measurements do not establish the full SP-04 three-second campaign-turn budget.

Final local verification on 2026-07-16 uses Darwin arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` builds with zero warnings and passes 580 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. Touched-file formatting, `git diff --check`, and `git lfs fsck` pass.

Independent review found and remediated a stale local guardianship calendar after eventless turns. The subsystem now advances monotonically with `WorldState`, and a regression rejects both stale direct planning and stale event application without mutation. The final reviewer rerun passes an 85-test guardianship/save/current-schema/soak slice and the complete 580-test Core suite with no remaining correctness or package-boundary blocker.

Accepted revision `97b607ae8df77dbd5c6fa5ab6b544000208cdb0e` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04E1-EXACT-SHA-97b607a.md). E114 therefore passes at that revision. Every full SP-04 acceptance criterion remains unchecked. Pregnancy/birth, education, coming of age, guardianship lifecycle/authority, public death, inheritance, succession, content, presentation, and platform work remain later E/F/G/X packages; SP-05 remains blocked.

## Accepted package: SP-04E2 guardianship termination and replacement

SP-04E2 is a workflow-only extension of the accepted E1 guardianship state. `EndPrimaryGuardianshipAction` requires the ward's exact active record and permits only explicit `Revoked`, or `GuardianUnavailable` when the current guardian is dead, incapacitated, or not free. `ReplacePrimaryGuardianshipAction` requires the same exact active record, a living minor ward, and a different older replacement guardian who is at least 18, alive, capable, and free. Replacement atomically terminalizes the prior record as `Replaced` and creates one active replacement from the same command/event evidence. Capacity, collision, stale-state, invalid-condition, and apply-time failures preserve the complete prior snapshot.

Both actions reuse the reserved-system Commands-phase `character_family_action.v1` envelope and the existing guardianship-v1 snapshot, state, query, ID, and system registration. No parentage, family, household, custody, condition, relationship, marriage, education, claim, inheritance, succession, or other subsystem state is changed. Automatic birthday/death termination, character or household authority/consent, co-guardians, adult regency, residence movement, education authority, pregnancy/birth, and later F behavior remain outside E2. This dependency-safe boundary needs no ADR.

Save schema 16 advances only nested family-action vocabulary. The authenticated 15→16 migration preserves the complete world snapshot, diagnostics, manifests, and source bytes while recomputing the destination checksum. Schema 15 continues to accept all valid active and terminal guardianship-v1 records, including `Replaced`, but rejects E2 actions, outcomes, and E2-only command/event properties even when explicitly null. The 61,267-byte schema-15 fixture was generated by a detached harness compiled against exact accepted E1 production source `97b607ae8df77dbd5c6fa5ab6b544000208cdb0e`. It retains rich prior state, E0 adoption, active and terminal guardianships, and pending/resolved E1 family actions; its stored checksum is `ed398b879de95b9065bffb367fb3a7b830c40d6a8e1f29d3a0a64ffb414e5b86`, and file SHA-256 is `51a6ecf8e0556af35cbe4010645925d50bc965ca4a2d07ea05c8c9c486538fc3`.

### SP-04E2 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| E201 | Exact E1 is accepted; termination/replacement is the smallest dependency-safe next package and needs no ADR | Source-of-truth and architecture review | Local pass |
| E202 | End/replace actions and ended/replaced outcomes are registered with exact round-trip and affected IDs | Contract, reflection, round-trip, and campaign tests | Local pass |
| E203 | End requires exact active state and permits only `Revoked` or valid `GuardianUnavailable` | Domain eligibility and stale-state tests | Local pass |
| E204 | `Replaced`, coming-of-age, ward-death, guardian-death, and unknown explicit reasons are rejected | Reserved-reason matrix | Local pass |
| E205 | Replacement revalidates the ward and replacement guardian and atomically ends/creates exact records | Domain and campaign tests | Local pass |
| E206 | Capacity, ID collision, stale state, and invalid participant failures preserve the prior snapshot | Limit/collision/rollback tests | Local pass |
| E207 | End/end, end/replace, and replace/replace races select one deterministic event-ID winner in both assignments | Same-turn race tests | Local pass |
| E208 | Apply-time payload tampering, affected-ID mismatch, and stale replay roll back | Replan/payload/affected-ID/replay tests | Local pass |
| E209 | Parentage, family, household, condition, relationship, marriage, and all other subsystem snapshots remain identical | Complete snapshot comparison | Local pass |
| E210 | Schema-16 pending actions, outcomes, diagnostics, terminal state, replay, and checksum round-trip exactly | Save/replay/current-schema tests | Local pass |
| E211 | Exact-E1 schema 15 authenticates and migrates byte-preservingly while rejecting E2 vocabulary/properties | Frozen fixture, migration, rejection, and source-byte tests | Local pass |
| E212 | Existing schema-15 active and terminal records remain compatible without tighter persisted-state validation | Historical fixture and restore tests | Local pass |
| E213 | A 1,000-character bounded replacement/end fixture records workflow/checksum/JSON/gzip without a time threshold | Local Apple Silicon measurement | Local macOS pass; full SP-04 budget unmet |
| E214 | Repository gates pass and the same revision passes hosted macOS arm64 and Windows x64 | Local gates plus exact-SHA clean-checkout hosted evidence | Pass at `7491da89985fedb18e423082a2fd9187b8899e52` |
| E215 | No automatic coming age/death, authority/consent, education, pregnancy/birth, inheritance, succession, content, or presentation enters E2 | Reflection and diff review | Local pass |

The representative local Release fixture contains 1,000 characters, 64 active starting guardianships, 32 atomic replacements, and 32 explicit endings. The latest raw run measured 301.142 ms for workflow and 126.468 ms for snapshot/checksum, serialized to 1,015,230 JSON bytes and 35,762 gzip bytes, and produced checksum `962ea2a48bbe1984c8b94b9745c08c3f9984abcba2516d61911291803a805020`. The test asserts exact shape and correctness, not a wall-clock threshold. These working-tree measurements do not establish the full SP-04 three-second campaign-turn budget.

Local verification on 2026-07-16 uses Darwin arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` builds with zero warnings and passes 602 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. The focused family/guardianship/save slice passes 301 tests; touched-file formatting, `git diff --check`, and `git lfs fsck` pass. Independent review found an exact-JSON evidence gap, verified the added literal-property/discriminator and replacement-rejection coverage, reran 46 narrow tests and the full 602-test Core suite, and reports no remaining correctness or package-boundary blocker.

One final wrapper invocation failed before build or tests when `dotnet restore` terminated with a segmentation fault. No source changed; the immediate unchanged-tree rerun completed the validation, zero-warning build, and all four suites above. The failed attempt is retained here and is not relabeled as passing.

Accepted revision `7491da89985fedb18e423082a2fd9187b8899e52` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04E2-EXACT-SHA-7491da8.md). E214 therefore passes at that revision. Every full SP-04 acceptance criterion remains unchecked. Later E/F/G/X packages, physical Windows, signing, and Steam remain open under their existing gates; SP-05 remains blocked.

## Accepted package: SP-04E3 deterministic coming of age

SP-04E3 adds one internal Systems-phase lifecycle transition for a living character whose exact age changes from 17 to 18 on the resolution date. The reserved `system:simulation/character_lifecycle` actor generates a priority-zero `character_coming_of_age.v1` command with a date-and-character-derived ID. Resolution emits one causally linked `character_came_of_age.v1` event with exact affected IDs. External submission of the reserved command and restoration of it as pending work fail closed. The transition is date-derived rather than persistent character state: it fires once on the exact birthday, uses March 1 for a February 29 birth in non-leap years, works on later days inside a multi-day turn, and never backfills a character already 18 or older.

When the character has an active primary guardianship, the same event atomically ends that exact record as `WardCameOfAge`; the event remains observable when no guardianship exists. Commands-phase E2 work resolves before lifecycle closure: a same-day valid revocation or guardian-unavailable ending leaves no record for E3 to close, while a valid replacement made on an earlier day—including an earlier day of the same multi-day turn—is closed on the birthday. A birthday-day replacement remains invalid because E2 requires a minor ward. Apply-time replanning rejects stale state, phase, priority, causal ID, event ID, affected IDs, and payload tampering without mutation. Character, parentage, family, household, condition, relationship, career, resources, estates, marriage, geography, and every other subsystem remain unchanged.

Save schema 17 advances only outer lifecycle command/event vocabulary. Its 16→17 migration authenticates the source, preserves the complete snapshot, diagnostics, manifests, and source bytes, and recomputes the destination checksum. Schema 16 continues to accept E2 terminal reason `WardCameOfAge` as reserved guardianship-v1 state, but rejects E3 command/event discriminators and E3-only properties even when explicitly null. The 64,549-byte schema-16 fixture was generated by a detached harness compiled against exact accepted E2 production source `7491da89985fedb18e423082a2fd9187b8899e52`. It retains rich prior history plus revoked and replaced guardianships; its stored checksum is `aac74df0b0c97f3ec49c6e2aeaef1be742f102a797f7d317b576e1e8ed2da471`, and file SHA-256 is `3f118e24486fc7367dc00f07419abfb19eff13a144daffc878ee73a99277fbe5`.

### SP-04E3 verification matrix

| ID | Observable package criterion | Required evidence | Closeout classification |
|---|---|---|---|
| E301 | Exact E2 is accepted; deterministic coming of age and primary-guardianship closure are the smallest dependency-safe next package and need no ADR | Source-of-truth and architecture review | Local pass |
| E302 | Reserved actor, outer command/event contracts, exact JSON discriminators, IDs, causality, phase, priority, and affected IDs are explicit | Contract, round-trip, golden-ID, and campaign tests | Local pass |
| E303 | External submission and restored pending lifecycle work fail closed while internal generation remains diagnostic and replayable | Submission, restore, save, and replay tests | Local pass |
| E304 | Exactly living 17-to-18 transitions fire once; dead characters and already-adult backfill are skipped | Exact-age and negative tests | Local pass |
| E305 | First day, later multi-day turn dates, and February-29-to-March-1 non-leap behavior use exact calendar age | Calendar boundary tests | Local pass |
| E306 | Active guardianship ends atomically as `WardCameOfAge`; no-guardian, same-day end, and valid earlier-day replacement interactions are deterministic | Domain and campaign sequencing tests | Local pass |
| E307 | Apply-time state, phase, priority, causal/event IDs, affected IDs, and payload are replanned exactly and reject tampering without mutation | Tamper, stale-state, rollback, and replay tests | Local pass |
| E308 | Multiple birthdays and geography events remain deterministic under shuffled character and geography input | Ordering and checksum tests | Local pass |
| E309 | Character and every non-guardianship subsystem remain byte-equivalent | Complete snapshot comparison | Local pass |
| E310 | Current schema-17 diagnostics, checksum, pre-birthday regeneration, and post-birthday nonduplication round-trip | Save/replay/current-schema tests | Local pass |
| E311 | Exact-E2 schema 16 authenticates and migrates byte-preservingly while rejecting E3 vocabulary/properties, including explicit null | Frozen fixture, migration, rejection, corruption, and source-byte tests | Local pass |
| E312 | Existing schema-16 `WardCameOfAge` terminal state remains compatible without adding an adult flag or new subsystem version | Historical fixture and restore tests | Local pass |
| E313 | A 1,000-character birthday batch records raw workflow/checksum/JSON/gzip behavior without a time threshold | Local Apple Silicon measurement | Local macOS pass; full SP-04 budget unmet |
| E314 | Repository gates and independent read-only review find no remaining correctness or package-boundary blocker | Local gates and independent review | Local pass after evidence remediation |
| E315 | The same accepted revision passes hosted macOS arm64 and Windows x64 | Exact-SHA clean-checkout hosted evidence | Pass at `59588be9d277dc4c4cb7ec98ef99e33591b0eeda` |

The representative local Release fixture contains 1,000 characters all turning 18 on the resolved day and emits 1,000 lifecycle events. The latest raw run measured 99.445 ms for workflow and 124.389 ms for snapshot/checksum, serialized to 866,928 JSON bytes and 16,868 gzip bytes, and produced checksum `aa23110c1f48af3ef1242bc06e20ddcd22ef0d82f0414ddf7a7db4813d1b88b8`. The test asserts exact shape and correctness, not a wall-clock threshold. These working-tree measurements do not establish the full SP-04 three-second campaign-turn budget.

Local verification on 2026-07-16 uses Darwin arm64, .NET SDK 10.0.301, and Godot 4.6.1. `./scripts/validate.sh` retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. `./scripts/test.sh Release` builds with zero warnings and passes 625 Simulation.Core, 71 Game.Content, 6 Game.Application, and 18 repository tests. Touched-file formatting, `git diff --check`, and `git lfs fsck` pass. The existing marriage helper now selects its own event on birthday turns. Independent review found and remediated missing exact-birthday replacement evidence and missing schema-15/16 frozen-fixture corruption cases; the expanded focused slice passes 37 tests.

Accepted revision `59588be9d277dc4c4cb7ec98ef99e33591b0eeda` subsequently passed [hosted macOS arm64 and Windows x64 validation, build, two complete suite executions per platform, import, native export, automated smoke, manifest inspection, artifact upload, and authenticated static artifact verification](../evidence/SP-04E3-EXACT-SHA-59588be.md). E315 therefore passes at that revision. Pregnancy/birth, education, mutable abilities or traits, public death, inheritance, succession, culture-specific adulthood, ceremony, content, presentation, physical Windows, signing, and Steam remain outside E3. Every full SP-04 acceptance criterion remains unchecked; later E/F/G/X packages remain open and SP-05 remains blocked.

## Approved package: SP-04E4 adult union-linked pregnancy registration

SP-04E4 adds `simulation.character_pregnancies@1` as the smallest dependency-safe step after accepted E3. D0–D3 already provide retained active adult unions, while birth would also require runtime child identity, names, provenance, parentage/family/household placement, and bounded inherited characteristics. Education requires mutable ability/trait architecture, and public death must reconcile multiple existing subsystems. E4 therefore registers active pregnancy only. The master plan permits this non-explicit family model, no locked decision is contradicted, and no ADR is required.

The active-only `CharacterPregnancyState` records exact explicit gestational and other biological parent roles, the retained source union, start and fixed 280-day expected-birth dates, start turn, and source command/event identities. Both parents must be distinct, known, born, living adults on the action date. The exact source union must be active, match the pair, and have started. One active pregnancy is allowed per gestational parent and per union. Expected-current state must match exactly; E4 success therefore requires null. Stable identity hashes the source event, ordered roles, and union. E4 introduces no sex, gender, fertility, genetic, consent, probability, or automatic-conception inference.

`register_active_pregnancy.v1` and `active_pregnancy_registered.v1` extend the existing reserved-system family envelope. Submission and resolution revalidate authority, Commands phase, date/turn, event identity, participants, union, expected current state, capacity, collision, and calendar range. Apply reconstructs and compares the exact action, outcome, payload, affected IDs, causal ID, and candidate before replacing pregnancy state. Same-parent and same-union races follow the campaign's canonical priority-then-event-ID order and cancel stale losers; equal-priority races choose the first derived family event in either submission order. Independent registrations commute and preserve checksum determinism. Advancing beyond the due date does nothing in E4.

Existing marriage retention pins every active source union through its accepted-proposal causal group, and E4 exposes no union-ending path. Schema 18 fails closed if an active pregnancy is restored against an ended source union or either currently dead parent. The first future public death, annulment, or separation package that can produce either state must atomically define pregnancy handling and, if needed, preserve the terminal union or decouple durable pregnancy evidence. E4 does not change marriage retention for unreachable future state.

Save schema 18 adds the pregnancy snapshot and system registration. The 17→18 migration authenticates exact E3 first, rejects pregnancy state/system/action/outcome/property injection including explicit null, injects the empty v1 subsystem, and preserves prior state, diagnostics, manifests, and source bytes. The 68,407-byte schema-17 fixture was generated by a detached harness compiled against exact E3 production source `59588be9d277dc4c4cb7ec98ef99e33591b0eeda`; it retains rich prior history, pending/resolved pre-E4 family work, and an E3 birthday event with guardianship closure. Its stored checksum is `7a680781eceabf8c46554f780aaaca0ef6f781caf3256cac46185f0031e88ea4`, and file SHA-256 is `c99c1183f408dfd66eb08015e3b10d4e5b3a2f573c4adb364cd395fb8a1eb9c2`.

### SP-04E4 verification matrix

| ID | Observable package criterion | Required evidence | Current classification |
|---|---|---|---|
| E401 | Exact E3 is accepted; pregnancy registration is the smallest dependency-safe next package | Source-of-truth and architecture review | Local pass |
| E402 | Pregnancy-v1 state, snapshot, authoritative query, system registration, action, outcome, and IDs are explicit | Contract, reflection, round-trip, and golden-ID tests | Local pass |
| E403 | Registration requires reserved authority plus exact living adult union participants | Domain and campaign positive/negative tests | Local pass |
| E404 | Gestational role is explicit and role-sensitive; no reproductive descriptor or consent inference enters the contract | Contract, identity, and boundary review | Local pass |
| E405 | Expected birth is exactly start plus 280 days across boundaries; overflow rolls back | Calendar and rollback tests | Local pass |
| E406 | One active pregnancy per gestational parent and source union is enforced | Construction, registration, and race tests | Local pass |
| E407 | Same-parent and same-union races follow canonical priority-then-event-ID order and choose one deterministic winner in both submission orders | Campaign race tests | Local pass |
| E408 | Independent registrations are input-order invariant and checksum deterministic | Shuffle and checksum tests | Local pass |
| E409 | Stale state, replay, ID/affected-ID/payload tampering, collision, and overflow leave all state unchanged | Replanning and rollback tests | Local pass |
| E410 | Queries are canonical, defensive, and exact by gestational parent, union, and involved character | Query tests | Local pass |
| E411 | Character, family, household, marriage, guardianship, relationship, career, resources, estates, geography, and random streams remain unchanged | Complete subsystem comparisons | Local pass |
| E412 | Schema-18 active state, pending action, resolved outcome, diagnostics, checksum, JSON, and gzip round-trip | Save and replay tests | Local pass |
| E413 | Exact-E3 schema 17 authenticates, migrates with empty pregnancy state, and rejects all E4 injection | Frozen fixture, corruption, explicit-null, and source-byte tests | Local pass |
| E414 | A 1,000-character representative-union fixture records workflow/checksum/JSON/gzip measurements | Raw local measurement | Local pass; no threshold |
| E415 | Repository gates, independent review, and exact-SHA hosted macOS arm64/Windows x64 evidence pass | Local gates, review, hosted CI/artifacts | Local gates and review pass; hosted evidence pending |
| E416 | No conception scheduler, birth, child creation, loss, inheritance, education, death, content, localization, UI, or AI enters E4 | Diff and dependency review | Local pass |

Local Release verification on 2026-07-16 passes `./scripts/validate.sh`, a zero-warning build, 674 Simulation.Core tests, 71 Game.Content tests, 6 Game.Application tests, and 18 repository tests. Validation retains 1,295 records and 2,820 translations at registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`. The raw 1,000-character/one-union fixture measured 52.019 ms workflow and 127.504 ms checksum, produced 848,328 JSON bytes and 17,655 gzip bytes, and checksum `46e57978a12f0849515880c1996fe07ead12932135503856522aa7b1ac3cb5ed`; it asserts correctness, not a wall-clock threshold.

Independent review found and remediated incomplete both-submission-order race coverage, imprecise event-ID-only winner wording, missing apply-time identity/affected-ID rollback variants, incomplete resolved-gzip evidence, a missing positive union lookup, and incomplete isolated schema-17 property probes. Architecture re-review also tightened schema-18 restore to reject ended source unions and either currently dead parent until a future lifecycle package defines atomic pregnancy handling. Final architecture and verification review report no remaining correctness, compatibility, or package-boundary blocker.

E4 has no content-authoring boundary and changes no `Game.Content`, `Game.Application`, presentation, or platform project. Exact-SHA hosted evidence remains required before package acceptance. Full SP-04 acceptance, its three-second campaign-turn budget, and later birth/education/death/inheritance/succession packages remain open.

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
