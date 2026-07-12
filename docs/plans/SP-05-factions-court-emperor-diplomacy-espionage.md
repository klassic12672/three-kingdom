# SP-05 — Factions, Court, Emperor, Diplomacy, and Espionage

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-03](SP-03-campaign-map-regions-routes-supply.md), [SP-04](SP-04-characters-family-marriage-succession.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Model overlapping political authority so personal networks, subfactions, courts, emperors, diplomacy, and espionage can reshape the unification war without replacing it.

## Non-goals

- A single faction-membership flag that owns every character and territory relationship.
- Automatic obedience to imperial edicts or legal appointments.
- Omniscient diplomacy or guaranteed-correct intelligence.
- A separate short-form coup or court game mode.

## Requirements

- Represent states/major factions, subfactions, retinues, court blocs, coalitions, alliances, vassals, tributaries, and imperial institutions independently.
- Subfactions may hold officers, personal troops, estates/territory, treasury, objectives, succession preferences, and a relationship with the parent faction.
- Track personal service, political membership, military command, territorial appointment, and sovereign recognition separately.
- Courts have offices, councils, agendas, proposals, influence, patronage, and access to the ruler/emperor.
- The emperor is a character plus an institution with trust, custody/location, court access, seal/document authority, and directive credibility.
- Support granting 작위, 관직, 군호, and territorial appointments; grants create legal status/claims but do not transfer actual control.
- Treaty clauses include peace, non-aggression, military/supply access, alliance, coalition goal, war contribution, territory/claim recognition, marriage, hostages, tribute, vassalage, prisoner exchange, separate-peace limits, and guarantors.
- Coalitions define objectives, leadership, contributions, territorial division, captured-person handling, cohesion, and withdrawal rules.
- Espionage uses real characters with cover, access, suspicion, information, loyalties, relationships, and possible double-agent behavior.
- Coups and defections depend on prepared networks, access, troops, gates/stops, documents, and recognition; no single button guarantees success.
- Political and military outcomes generate cross-system memories, prestige, legitimacy, claims, and factional power changes.

## Public contracts

- Extends `EntityId` for factions, subfactions, courts, offices, titles, treaties, clauses, coalitions, imperial documents, spy missions, and claims.
- Diplomacy, appointment, council, edict, coalition, coup, and espionage actions extend `CampaignCommand`.
- Treaty, recognition, appointment, betrayal, intelligence, coup, defection, and legitimacy outcomes extend `CampaignEvent`.
- `PoliticalMembership`, `ServiceRelationship`, `OfficeTenure`, `TerritorialClaim`, and `TreatyAgreement` are independent state contracts.
- `PoliticalSituationView`, `CouncilView`, `DiplomaticOfferView`, and `IntelligenceReport` expose only knowledge available to the player character.
- Coalition and allegiance data populate `BattleSetup`; defections, captures, performance, and withdrawals return through `BattleResult`.

## Data flow

```text
Characters + territories + offices + relationships + intelligence
→ political memberships and agendas
→ proposals, edicts, treaties, plots, and orders
→ authority/access/credibility/interest resolution
→ appointments, agreements, claims, defections, coups, and memories
→ campaign ownership, army, economy, and battle systems
```

## Implementation workstreams

1. Define faction, subfaction, membership, service, treasury, agenda, autonomy, and recognition state.
2. Implement offices, councils, proposals, influence, satisfaction/expectation, patronage, and ruler decisions.
3. Implement emperor custody, edicts, seals, appointments, credibility, compliance, forgery/dispute hooks, and 협천자 consequences.
4. Implement clause-based diplomacy, negotiation evaluation, treaties, coalitions, vassalage, and breach consequences.
5. Implement spy recruitment, cover/access, reports, suspicion, missions, double agency, collusion, and counterintelligence.
6. Implement coups, defections, subfaction rebellion/independence, provincial recognition, and political aftermath.
7. Integrate political structures into war declarations, alliance participation, battle command, captured territory, and peace.

## Edge cases and failure handling

- Conflicting memberships are allowed only when their relationship types permit them; invalid exclusive memberships fail validation.
- Death, deposition, capture, or defection of a negotiator/officeholder triggers defined succession, ratification, or cancellation behavior.
- Treaty clauses specify whether obligations survive ruler/faction succession.
- An edict whose emperor, seal, witnesses, or delivery becomes invalid is disputed rather than silently applied.
- A spy may provide stale, partial, fabricated, or misinterpreted information; reports record confidence and observation date.
- Coalition members may fight on one side while retaining separate objectives and refusing commands outside their obligations.

## Performance budget

- Routine faction/court/diplomacy resolution remains inside the overall 3-second campaign-turn budget.
- Opening a known diplomatic or court view returns within 100 ms from cached read models.
- AI negotiation evaluation uses bounded candidate clauses rather than exhaustive combinatorial search.

## Tests

- Independent service, membership, office, claim, and control state tests.
- Subfaction loyalty, autonomy, succession, rebellion, and transfer tests.
- Imperial grant, compliance, credibility, disputed seal, and forged-document tests.
- Treaty construction, ratification, inheritance, breach, coalition contribution, and peace tests.
- Spy cover, access, uncertainty, double-agent, detection, and collusion tests.
- Multi-faction `BattleSetup` and defection/withdrawal `BattleResult` tests.

## Acceptance criteria

- [ ] Characters can lead loyal retinues while serving within another faction.
- [ ] Subfactions possess meaningful officers, resources, agendas, and paths to autonomy or control.
- [ ] Courts and offices influence decisions rather than acting only as passive bonuses.
- [ ] Imperial grants create legal effects without transferring actual control automatically.
- [ ] Clause-based treaties and coalitions resolve obligations, contributions, breaches, and succession.
- [ ] Espionage is character-based, uncertain, and capable of political/military effects.
- [ ] Political conflict remains integrated with the wider campaign war.

## Risks

| Risk | Mitigation |
|---|---|
| Political layers confuse players | Provide relationship/authority views that answer who serves, controls, appoints, claims, and recognizes whom. |
| Diplomacy search becomes expensive or exploitable | Bound clause sets, use transparent valuations, and test adversarial offers. |
| Emperor control becomes an unconditional snowball bonus | Model credibility, court resistance, competing recognition, escape, and abuse consequences. |
| Spies feel random | Expose confidence, access, risk, and causal factors without revealing hidden truth. |

## Deferred work

- Complete 고평릉사변 and 이궁지쟁 bookmarks.
- Full imperial-document forgery presentation and handwriting/seal minigames.
- Multiplayer diplomatic negotiation.
- Additional post-1.0 institutional government forms.
