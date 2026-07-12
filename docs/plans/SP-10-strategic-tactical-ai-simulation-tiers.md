# SP-10 — Strategic/Tactical AI and Simulation Tiers

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-01](SP-01-simulation-calendar-determinism-saves.md), [SP-03](SP-03-campaign-map-regions-routes-supply.md), [SP-04](SP-04-characters-family-marriage-succession.md), [SP-05](SP-05-factions-court-emperor-diplomacy-espionage.md), [SP-06](SP-06-population-economy-administration-technology.md), [SP-07](SP-07-armies-recruitment-equipment-logistics.md), [SP-08](SP-08-tactical-runtime-command-morale-formations.md), [SP-09](SP-09-siege-naval-shoreline-combined-battles.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Provide deterministic, information-limited AI for characters, administrators, factions, subfactions, diplomacy, operations, and battles while keeping the full world performant through tiered simulation.

## Non-goals

- AI access to hidden authoritative state unavailable to its actors.
- Machine-learning models or online inference.
- Perfect play, identical personalities, or scripted historical outcomes.
- Simulating every distant decision at player-level detail.

## Requirements

- AI decisions submit the same validated `CampaignCommand` and tactical-command contracts used by the player.
- Build decisions from perceived state, intelligence confidence, personality, memories, ambitions, authority, resources, relationships, and risk tolerance.
- Separate character intent, household/subfaction agenda, faction policy, operational planning, and tactical execution.
- Provide delegation for player-owned administrators, generals, contingents, and routine policy using the same AI services.
- Create bounded candidate generation, utility evaluation, plan commitment, reassessment triggers, and fallback behavior.
- Maintain imperfect information, stale reports, scouting, fog of war, and deception.
- Full-tier actors run detailed planning; Reduced actors use compressed but compatible decisions; Aggregate regions use conservation-respecting statistical resolution.
- Promotion from lower tiers reconstructs valid detailed state from preserved aggregates, commitments, relationships, and pending events.
- Tactical AI understands objectives, formations, reserves, flanks, terrain, morale, ammunition, command, reinforcement, withdrawal, and front delegation.
- AI traces explain chosen goals, rejected alternatives, perceived facts, and utility factors in development builds.

## Public contracts

- Consumes read-only knowledge-filtered queries and emits existing `CampaignCommand`/tactical commands.
- `AiDecisionContext`: actor, authority, perceived state, active commitments, budget, deterministic RNG stream, and evaluation deadline.
- `AiPlan`: goal, ordered steps, reserved resources, expiry, reassessment triggers, and fallback.
- `AiTrace`: development-only structured explanation excluded from authoritative checksums and release saves unless diagnostics are enabled.
- `SimulationTierState`: tier, preserved aggregates, promotion data, pending commitments, and last resolved date.

## Data flow

```text
Knowledge-filtered world/tactical query + personality/agenda
→ candidate goals and actions
→ bounded deterministic evaluation
→ plan/command submission
→ normal authoritative validation and resolution
→ observed results, memories, learning counters, and reassessment
```

## Implementation workstreams

1. Define AI context, plans, utility factors, deterministic candidate ordering, tracing, and budgets.
2. Implement character daily-life, relationship, employment, ambition, and household decisions.
3. Implement administrator and subfaction delegation, court proposals, faction strategy, diplomacy, and espionage.
4. Implement operational planning for objectives, routes, supply, concentration, defense, reinforcement, and withdrawal.
5. Implement tactical contingent/general AI and front-level combined battle behavior.
6. Implement Full/Reduced/Aggregate AI adapters, promotion/demotion, and outcome-comparison fixtures.
7. Build automated scenario tournaments, exploit detection, and trace-analysis reports.

## Edge cases and failure handling

- If a plan becomes illegal, the actor reassesses at a defined trigger rather than repeatedly submitting the same invalid command.
- AI computation budgets cap candidates and depth; timeout uses a deterministic best-so-far/fallback action.
- Delegated AI cannot exceed the delegating character's legal authority or known information.
- Aggregate resolution cannot kill, transfer, marry, or appoint protected named characters without emitting equivalent authoritative events.
- Historical tendencies bias goals but never override survival, changed relationships, player actions, or legal validation.
- An AI with no useful action explicitly waits; it does not invent resources or targets.

## Performance budget

- All campaign AI remains inside the overall 3-second normal-turn target by M4.
- Individual interactive recommendations return within 200 ms or run asynchronously without blocking input.
- Tactical AI completes within the remaining 20 Hz tick budget after core simulation, targeting below 15 ms average at 54 units.
- Development traces are bounded and can be disabled independently.

## Tests

- Deterministic candidate ordering and cross-platform command-stream tests.
- Information-boundary tests that fail on hidden-state reads.
- Personality, memory, ambition, authority, and relationship decision fixtures.
- Diplomacy, betrayal, coalition, supply, route, defense, and withdrawal scenarios.
- Tactical formation, reserve, flank, ranged, rout, objective, and combined-front fixtures.
- Full-versus-reduced/aggregate statistical comparison and conservation soak tests.
- Delegation cancellation, authority change, and invalid-plan recovery tests.

## Acceptance criteria

- [ ] AI uses the same command validation and resolution paths as the player.
- [ ] Decisions reflect perceived information and character/faction context rather than hidden truth.
- [ ] Character, political, operational, and tactical layers produce compatible plans without bypassing authority.
- [ ] Player delegation supports routine administration and contingent/front command.
- [ ] Tier changes preserve commitments and do not create impossible named-character outcomes.
- [ ] Development traces make important AI decisions explainable.
- [ ] AI meets campaign and tactical performance budgets.

## Risks

| Risk | Mitigation |
|---|---|
| AI breadth consumes the solo schedule | Build shared candidate/utility/plan infrastructure and implement only actions required by the active vertical slice. |
| Utility AI becomes erratic | Add plan commitment, reassessment triggers, personality bounds, and scenario regression fixtures. |
| Lower simulation tiers visibly cheat | Preserve causal inputs, information, commitments, and compare distributions against full-tier runs. |
| Tactical AI cannot manage combined fronts | Delegate hierarchically by side, front, general, and contingent using shared objectives. |

## Deferred work

- Machine learning or adaptive online models.
- Player-authored AI scripts.
- Multiplayer AI takeover.
- Advanced strategic personalities for post-1.0 bookmarks.
