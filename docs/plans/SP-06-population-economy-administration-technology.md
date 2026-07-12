# SP-06 — Population, Economy, Administration, and Technology

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-03](SP-03-campaign-map-regions-routes-supply.md), [SP-04](SP-04-characters-family-marriage-succession.md), [SP-05](SP-05-factions-court-emperor-diplomacy-espionage.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Make territory valuable through people, production, institutions, local cooperation, delegated government, and long-term progression rather than uniform building slots or abstract income alone.

## Non-goals

- Individual civilian simulation.
- A modern industrial supply-chain economy.
- Identical administration for every culture.
- Technology dominated by repeated small percentage bonuses.

## Requirements

- Track locality-level registered households, refugees, military-age population, local elites, cultural groups, skilled labor, public order, health, and food security at an aggregate level.
- Recruitment, casualties, migration, captivity, famine, raids, taxation, and settlement change real population pools.
- Model agriculture, commerce, crafts/resources, treasury, stored grain/supplies, and transport constraints.
- Territory provides limited value without an accepted administrator, functioning institutions, local cooperation, and connected routes.
- Support direct rule, appointed administrators, personal estates, land grants, hereditary expectations, autonomous tributaries, and recognized local rulers.
- Administrative titles use the geographic system's localized names while mapping to universal authority responsibilities.
- Buildings/institutions require location, labor, resources, time, policy permission, and maintenance where applicable.
- Separate family legacy progression from faction/subfaction institutional research.
- Research branches unlock formations, units, equipment, buildings, institutions, policies, perks, and further research.
- Distinguish technology, institution, building, policy, doctrine, perk, and tradition.
- Historical families may have unique legacy branches; custom families choose and develop equivalent archetypes without permanent inferiority.

## Public contracts

- Extends `EntityId` for population groups, economic stores, administrators, estates, buildings, institutions, research nodes, policies, doctrines, perks, and traditions.
- Administrative, economic, construction, tax, migration, grant, policy, and research actions extend `CampaignCommand`.
- Production, shortage, migration, unrest, construction, appointment, grant, and unlock results extend `CampaignEvent`.
- `LocalityEconomyView`, `PopulationView`, `AdministrationView`, and `ProgressionView` expose knowledge-appropriate read models.
- Population/economy/technology state persists in `WorldSnapshot` and supplies recruitment/logistics consumers.

## Data flow

```text
Population + land/resources + administrator + institutions + routes + policies
→ daily/turn production and demand
→ stores, taxes, migration, order, construction, and research
→ recruitment/supply capacity and political satisfaction
→ campaign events and player-facing reports
```

## Implementation workstreams

1. Define aggregate population groups, household accounting, demographics, migration, public order, and health/food state.
2. Implement production, consumption, local stores, taxation, trade hooks, treasury, and shortages.
3. Implement administrator authority, competence, corruption/loyalty hooks, local acceptance, delegation, estates, and land grants.
4. Implement buildings/institutions, prerequisites, construction queues, damage, maintenance, and capture behavior.
5. Implement family legacy and faction/subfaction research graphs with typed unlocks.
6. Connect population/economy to recruitment, supply, war damage, refugees, occupation, and subfaction power.
7. Build locality, administration, construction, and progression reports for the 191 slice.

## Edge cases and failure handling

- Population values cannot become negative; unresolved fractional changes use deterministic remainder accounting.
- Occupation does not instantly convert acceptance, taxation, institutions, or local elites.
- Administrator death/capture leaves a vacancy and defined interim authority rather than deleting queues/resources.
- Territory transfer specifies ownership of local stores, personal estates, debts, and ongoing construction.
- Research unlocks remain tied to their owning family/faction/subfaction and do not duplicate on allegiance change without an explicit transfer rule.
- A missing required institution disables dependent capability with a clear explanation instead of deleting progress.

## Performance budget

- Batch-resolve locality production/population using data-oriented arrays inside the overall 3-second turn budget.
- Opening an economy or technology view returns within 100 ms from cached summaries.
- Research prerequisite evaluation is cached and invalidated only by relevant changes.

## Tests

- Population conservation, migration, casualty, refugee, famine, and recovery tests.
- Production, consumption, tax, store, transport, and shortage tests.
- Administrator vacancy, competence, land grant, inheritance expectation, autonomy, and occupation tests.
- Construction prerequisite, capture, damage, cancellation, and completion tests.
- Family versus faction/subfaction research ownership and unlock tests.
- Long-war depopulation and post-war restoration soak scenarios.

## Acceptance criteria

- [ ] Localities contain persistent population and stores that warfare and policy can change.
- [ ] Recruitment and supply draw from real local/regional capacity.
- [ ] Administration, acceptance, institutions, and connections determine usable territorial value.
- [ ] Land grants empower officers/subfactions and create meaningful revocation/inheritance consequences.
- [ ] Family legacy and faction/subfaction technology are distinct and data-driven.
- [ ] Research unlocks typed capabilities rather than relying primarily on flat modifiers.
- [ ] Economy and population processing meet campaign performance targets.

## Risks

| Risk | Mitigation |
|---|---|
| Economic detail creates repetitive micromanagement | Delegate routine policy, surface exceptions, and make administrators meaningful. |
| Population data implies false precision | Use households/groups, uncertainty where appropriate, and documented historical estimates. |
| Technology becomes ahistorical progression fantasy | Gate content by date, culture, institutions, teachers, captured knowledge, and scenario configuration. |
| Land grants produce unstoppable vassals | Balance satisfaction and administrative value against autonomy, inheritance, and military power. |

## Deferred work

- Fully simulated trade markets and price arbitrage.
- Epidemic system beyond initial health hooks.
- Detailed religion/philosophy institutions.
- Post-1.0 economic content for later bookmarks.
