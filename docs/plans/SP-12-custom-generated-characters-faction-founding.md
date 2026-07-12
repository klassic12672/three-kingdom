# SP-12 — Custom/Generated Characters and Faction Founding

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-02](SP-02-content-localization-modding-research.md), [SP-04](SP-04-characters-family-marriage-succession.md), [SP-05](SP-05-factions-court-emperor-diplomacy-espionage.md), [SP-06](SP-06-population-economy-administration-technology.md), [SP-07](SP-07-armies-recruitment-equipment-logistics.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Let players create plausible original characters, assemble generated companions, begin from multiple social positions, and found a unique faction without requiring historically empty territory.

## Non-goals

- Giving custom factions unrestricted elite characters, territory, money, and legitimacy simultaneously.
- Generating characters without families, origins, ambitions, relationships, or reasons for being unaffiliated.
- Making custom families permanently weaker than famous historical families.
- Sexually explicit character generation or content.

## Requirements

- Support player-authored identity, appearance parameters, culture/origin, family, background, abilities, aptitudes, traits, flaws, ambitions, relationships, and starting history.
- Provide generated talent archetypes:
  - 장수형: command/martial strengths with administrative/diplomatic tradeoffs.
  - 모사형: strategy, schemes, intelligence, or diplomacy with weaker direct combat.
  - 관료형: administration, law, logistics, economy, and public order.
  - 다재다능형: multiple high specialties plus a meaningful weakness/cost.
  - 팔방미인형: broad competence and social adaptability without an era-defining peak.
- Separate capability archetype from background such as local magnate, fallen scholar family, official, wandering knight, merchant, refugee leader, Yellow Turban remnant, maritime group, frontier military household, imperial relative, or non-Han/local leader.
- Every generated character has a home locality, culture, age, family/patron context, ambition, personality, at least one relationship, reputation, flaw, and reason for current status.
- Use a shared creation budget covering character talent, followers, troops, treasury, territory, local support, official recognition, prestige, and claims.
- Support start packages: wandering army, local magnate, imperial appointee, rebel/restoration force, displaced household, frontier leader, and subordinate officer with retinue.
- Founding a faction requires a legal/political/military path such as appointment, rebellion, recognized local control, occupation, coalition recognition, or a raised banner in weakly controlled territory.
- Generate plausible pre-bookmark histories for custom characters in later scenarios, including employers, offices, relationships, marriages, children, battles, grudges, and reasons for current position.
- Generated characters fill systemic roles in incomplete regions without overwriting curated historical slots.

## Public contracts

- Custom/generated records use the standard `EntityId`, character/family schemas, content tags, and localization keys from SP-02/SP-04.
- `CreationPackage`, `TalentArchetype`, `BackgroundDefinition`, `CreationBudget`, and `GeneratedHistory` are validated content/runtime contracts.
- Character creation, companion selection, raise-banner, independence, and faction-foundation actions extend `CampaignCommand`.
- Generated/founding outcomes extend `CampaignEvent` and persist in `WorldSnapshot`/`SaveEnvelope`.
- A custom-content `ContentManifest` records player-created definitions separately from the save's resulting runtime characters.

## Data flow

```text
Player choices or deterministic generation seed
→ archetype + background + scenario constraints + creation budget
→ validated character/family/relationship/history records
→ scenario placement and start package
→ normal character/political/economic/army simulation
→ possible faction-founding commands and events
```

## Implementation workstreams

1. Define archetypes, backgrounds, capability budgets, rarity, flaws, and scenario constraints.
2. Build deterministic generated identity, family, personality, ambition, relationship, and history generators.
3. Build custom-character editor with validation, presets, appearance preview hooks, and bilingual naming fields.
4. Implement companion selection and shared creation-budget UI.
5. Implement start packages, initial authority/resources, weak-control placement, and faction-founding paths.
6. Implement later-bookmark history generation and consistency validation.
7. Integrate generated characters into AI, succession, recruitment, regional role filling, and save/mod content.

## Edge cases and failure handling

- Creation cannot produce impossible kinship, age, office, title, culture/location, or relationship combinations.
- Historical IDs/names are not overwritten; collisions receive deterministic custom namespaces.
- A start package invalidated by scenario data offers only compatible placements rather than silently changing the world.
- Generated history cannot claim mutually exclusive offices or presence in simultaneous distant events.
- Creation budgets reject overspend and show exact costs/tradeoffs.
- Randomization always exposes a stable seed and permits reroll without mutating scenario content.

## Performance budget

- Generate and validate one detailed character in under 100 ms and a 100-character regional filler pool in under 2 seconds.
- Creation-screen previews update within 100 ms excluding first-time art loading.
- Later-bookmark history generation remains deterministic and bounded by a configurable event count.

## Tests

- Archetype range, weakness, rarity, budget, and no-dominant-build tests.
- Background/location/culture/office compatibility tests.
- Kinship, age, adulthood, relationship, and generated-history consistency tests.
- Deterministic seed and cross-platform output tests.
- Start-package resource, territory, authority, and faction-founding tests.
- Custom-content save/load, missing-mod, and ID collision tests.

## Acceptance criteria

- [ ] Players can create and play an original character with structured world relationships.
- [ ] All five talent archetypes are mechanically distinct and budget-balanced.
- [ ] Generated characters possess plausible origins, ambitions, relationships, flaws, and political context.
- [ ] Start packages create different viable paths without inventing universally empty territory.
- [ ] A landless custom character can gain followers, secure a base, and found a faction in the 191 slice.
- [ ] Later-bookmark generated histories are deterministic and internally consistent.
- [ ] Custom/generated content follows age, non-explicit-content, localization, and provenance rules.

## Risks

| Risk | Mitigation |
|---|---|
| Generated characters feel interchangeable | Combine capability, background, ambition, memory, family, relationships, and regional context. |
| Point-buy creates obvious optimal builds | Price combinations, require tradeoffs, vary start resources, and test automated build searches. |
| Custom starts break historical balance | Use scenario-aware budgets, placement rules, and explicit sandbox difficulty modifiers. |
| Generated histories contradict curated data | Reserve historical slots and validate dates, locations, offices, and relationships against scenario records. |

## Deferred work

- Sharing custom characters through Steam Workshop.
- Full portrait-layer editor beyond initial appearance parameters.
- Post-1.0 custom scenario editor.
- Generated voice acting.
