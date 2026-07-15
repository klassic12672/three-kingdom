# SP-04 — Characters, Family, Marriage, and Succession

## Metadata

| Field | Value |
|---|---|
| Status | Active — SP-04A locally verified; hosted exact-SHA evidence pending |
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

SP-04A is locally verified based on working-tree evidence rooted at `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`. The current remediation addresses the prior findings but does not treat the previous findings-only audit as closed evidence. It adds version-1 character, identity, family, household, state, snapshot, typed-content, authoritative-read-model, checksum, and schema-4 save contracts. Its corrective legacy-save boundary validates schema-specific raw JSON before deserialization and checksum canonicalization so malformed explicit-null schema 1–3 sources fail deliberately and recovery can preserve every candidate file. The omniscient read models are explicitly named `AuthoritativeCharacterProfile` and `AuthoritativeHouseholdView`; the public-contract names `CharacterProfile` and `HouseholdView` remain reserved for the later observer-aware, player-knowledge-filtered application query layer.

The pre-remediation candidate is disqualified: after its initially recorded 2026-07-14 checks, `GeographyTests.PathfindingUsesRoutesOnlyAndMeetsInteractionBudget` failed the unchanged `<50 ms` threshold. That failed audit remains historical failure evidence and is not relabeled as passing. Corrected-tree evidence on 2026-07-15 passed 39 `SaveStoreTests`, the 36/29/3/1 focused simulation-character/content-character/published-schema/architecture checks, `./scripts/validate.sh`, `./scripts/test.sh Release` (107 Simulation.Core, 66 Game.Content, and 18 repository tests), `git diff --check`, and `git lfs fsck`. Validation retained 1,295 records, 2,820 translations, and registry checksum `b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0`; the authoritative soak checksum remains `cc6cba9f2b5408921fdbcd15a8d5494ca2351e73d7f3052f16702a09639af702`. These results justify only “locally verified in an uncommitted working tree”: no staging or commit occurred, and there is no clean-checkout, same-revision hosted, or cross-platform evidence. Hosted macOS/Windows verification remains pending; physical Windows packaged-save evidence remains an M4 gate, and production signing remains an SP-15 gate. See [the character foundation guide](../CHARACTERS.md) for commands and performance measurements.

This package does not implement relationships, memories, marriage, romance, birth progression, death, succession, retinues, battle integration, historical rosters, or presentation. Therefore all full SP-04 acceptance criteria below remain unchecked. Exact-SHA hosted macOS/Windows verification remains pending because no accepted revision has yet supplied that evidence.

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
| A13 | The same accepted revision passes deterministic validation on hosted macOS and Windows | Exact-SHA clean-checkout hosted evidence | Pending external evidence |
| A14 | Relationships, memories, marriage, succession, bounded history, battle integration, and player-knowledge-filtered queries | Later independently verifiable packages | Deferred |

The next independently verifiable package is bounded relationship dimensions and consequential memories, including meaningful-link limits, canonical persistence, and knowledge-filtered read-only summaries. It must not begin until its own mutation commands/events and bounded-history contract are approved.

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
