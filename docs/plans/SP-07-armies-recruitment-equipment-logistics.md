# SP-07 — Armies, Recruitment, Equipment, and Logistics

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-03](SP-03-campaign-map-regions-routes-supply.md), [SP-04](SP-04-characters-family-marriage-succession.md), [SP-05](SP-05-factions-court-emperor-diplomacy-espionage.md), [SP-06](SP-06-population-economy-administration-technology.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Create persistent armies whose composition, command relationships, recruitment, equipment, readiness, ammunition, transport, and supply determine campaign and tactical capability.

## Non-goals

- Instant recruitment or replenishment detached from people/equipment.
- More than three generals or eighteen units in one army.
- Tactical combat resolution implementation, owned by SP-08/SP-09.
- Individually equipping every soldier.

## Requirements

- An army contains up to three general-led contingents; each general commands up to six units.
- Preserve contingent ownership and identity when armies combine, split, reinforce, retreat, or change allegiance.
- Units have persistent type, culture, home/recruitment source, manpower, experience, morale baseline, cohesion, equipment, ammunition, formation knowledge, and service obligations.
- Unit categories include line/shield infantry, spear/polearm infantry, assault infantry, archers, crossbowmen, light/shock cavalry, horse archers, heavy hybrid cavalry, siege units, naval infantry, ships, militia, and regional units.
- Generals define normal, penalized, forbidden, and unlockable recruitment access by unit category/culture/institution.
- Recruitment consumes eligible population, equipment, money/supplies, authority, location capacity, and time.
- Replacement troops require compatible pools and reduce experience/cohesion until integrated.
- Readiness reflects mobilization, command changes, training, supply, fatigue, losses, weather, and recent movement.
- Track food, fodder, ammunition types, siege stores, transport capacity, and local resupply permissions.
- Support army stances such as march, forced march, encamp, defend, raid, conceal/ambush, besiege, and naval transport.
- Reinforcement eligibility depends on routes, command/coalition relationship, readiness, timing, and battlefield capacity.

## Public contracts

- Extends `EntityId` for armies, contingents, units, equipment sets, ammunition loads, recruitment orders, supply trains, and transports.
- Recruitment, organization, equipment, stance, movement-support, resupply, and reinforcement actions extend `CampaignCommand`.
- Muster, readiness, shortage, attrition, reinforcement, disbandment, and allegiance outcomes extend `CampaignEvent`.
- `ArmyState`, `ContingentState`, and `UnitState` persist in `WorldSnapshot`.
- `ArmyBattleContribution` transforms eligible campaign units into `BattleSetup` entries without losing persistent IDs.
- `BattleResult` updates manpower, morale, cohesion, experience, equipment, ammunition, capture, and commander state.

## Data flow

```text
Population + equipment + authority + general permissions + location
→ recruitment and training
→ persistent units/contingents
→ army organization, stance, movement, readiness, and supply
→ BattleSetup
→ BattleResult
→ replacements, recovery, capture, disbandment, or continued campaign service
```

## Implementation workstreams

1. Define army, contingent, persistent unit, equipment, ammunition, readiness, and transport state.
2. Implement general command capacity and recruitment permission evaluation.
3. Implement recruitment/training queues, population/equipment consumption, replacement integration, and disbandment returns.
4. Implement army composition, split/merge, command changes, allegiance changes, and persistent identity.
5. Implement stores, supply consumption, resupply, transport, shortage, attrition, encampment, and stances.
6. Implement reinforcement eligibility/timing and campaign-to-battle adapters.
7. Build army organization, recruitment, supply, and readiness UI queries for the 191 slice.

## Edge cases and failure handling

- A general's death/capture transfers the contingent according to chain of command, relationship, and battle result; units do not vanish.
- Defection may transfer some or all of a contingent based on loyalty and preparation.
- An invalid recruitment order reserves nothing and reports missing population, equipment, institution, permission, or funds.
- Interrupted recruitment returns defined recoverable resources and leaves already trained manpower in an explicit state.
- Over-capacity armies cannot add units but may temporarily carry disorganized survivors pending reorganization.
- Naval transport requires valid ports/ferries, capacity, and route; armies cannot embark from arbitrary shoreline positions.

## Performance budget

- Army organization and readiness queries return within 50 ms for normal UI use.
- Daily supply/readiness processing remains within the overall campaign-turn budget.
- Transforming 54 active units plus reinforcement candidates into `BattleSetup` completes within 200 ms excluding scene loading.

## Tests

- Three-general/eighteen-unit capacity and contingent identity tests.
- Recruitment permission, population/equipment consumption, training, cancellation, and replacement tests.
- Split, merge, commander death, defection, capture, and disbandment tests.
- Food/fodder/ammunition consumption, resupply, shortage, attrition, and forced-march tests.
- Land, river, coastal, and naval transport eligibility tests.
- Reinforcement timing and campaign/battle round-trip tests.

## Acceptance criteria

- [ ] Armies enforce three generals, six units per general, and eighteen total units.
- [ ] Units persist across campaigns and battles with manpower, experience, equipment, and identity intact.
- [ ] General-specific recruitment permissions and unlocks produce clear valid/invalid outcomes.
- [ ] Recruitment/replacement consumes real population, resources, equipment, location capacity, and time.
- [ ] Supply, transport, readiness, stances, and route access affect operational capability.
- [ ] Eligible multi-faction reinforcements enter `BattleSetup` deterministically.
- [ ] `BattleResult` updates the same persistent units without duplication or loss.

## Risks

| Risk | Mitigation |
|---|---|
| Logistics overwhelms players | Provide delegation, forecast days of supply, warnings, and clear route visualization. |
| Unit restrictions feel arbitrary | Show exact cultural, general, institution, technology, and equipment reasons. |
| Persistent units snowball permanently | Model replacements, officer turnover, equipment loss, fatigue, and institutional catch-up. |
| Army split/merge creates state bugs | Preserve stable unit/contingent IDs and test every ownership transition. |

## Deferred work

- Detailed individual weapon inventories.
- Mercenary markets beyond scenario-specific contracts.
- Advanced fleet construction and ship refitting, expanded in SP-09.
- Automated army templates beyond basic saved compositions.
