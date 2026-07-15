# SP-04 — Characters, Family, Marriage, and Succession

## Metadata

| Field | Value |
|---|---|
| Status | Active — SP-04A/SP-04B/SP-04C0/SP-04C1/SP-04C2 exact-SHA hosted verified; later packages pending |
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
