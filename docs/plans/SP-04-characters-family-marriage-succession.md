# SP-04 — Characters, Family, Marriage, and Succession

## Metadata

| Field | Value |
|---|---|
| Status | Planned/ready — dependencies complete; next package to plan |
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
