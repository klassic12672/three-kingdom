# SP-13 — Scenarios and Historical Content Pipeline

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | [SP-02](SP-02-content-localization-modding-research.md), [SP-03](SP-03-campaign-map-regions-routes-supply.md), [SP-04](SP-04-characters-family-marriage-succession.md), [SP-05](SP-05-factions-court-emperor-diplomacy-espionage.md), [SP-06](SP-06-population-economy-administration-technology.md), [SP-07](SP-07-armies-recruitment-equipment-logistics.md), [SP-08](SP-08-tactical-runtime-command-morale-formations.md), [SP-09](SP-09-siege-naval-shoreline-combined-battles.md), [SP-10](SP-10-strategic-tactical-ai-simulation-tiers.md), [SP-11](SP-11-ui-ux-accessibility-tutorial.md), [SP-12](SP-12-custom-generated-characters-faction-founding.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Author source-backed historical bookmarks that initialize the same complete game simulation with different dates, world states, pressures, and strategic tastes without forcing historical outcomes.

## Non-goals

- Separate game modes or scenario-exclusive core rules.
- Mandatory named battles or scripted territorial transfers.
- Completing all post-1.0 bookmark ideas before release.
- Using generated characters to replace known curated historical actors.

## Requirements

- A scenario defines date, calendar state, map/control/claims, characters/families/locations, offices/titles, factions/subfactions, courts/emperor, armies/fleets, economy/population, diplomacy, intelligence, technologies, and active pressures.
- All scenarios use the same systems, command contracts, unification objective, and victory rules.
- Conditional event pressures validate prerequisites at resolution time and allow alternative outcomes.
- Support three history settings:
  - Historical pressure: stronger historically grounded tendencies and conditional event weighting.
  - Dynamic history: normal character/AI interests without privileged outcome protection.
  - Free sandbox: opening state remains historical while optional scripted event chains are disabled.
- Canonical data is historical-core; disputed, inferred, Romance, and fictional content is visibly tagged.
- The initial vertical slice and Early Access bookmark are 군웅할거 after the anti-Dong Zhuo coalition's effective collapse.
- Early Access starts with approximately 300–400 curated historical characters and generated systemic fill.
- During Early Access add 관도대전, then 적벽대전, alongside the systems/content each requires.
- Version 1.0 contains all three bookmarks and approximately 800–1,000 curated historical characters.
- Bookmark-specific pressures never terminate the campaign after their named crisis; play continues toward unification.
- Scenario validation produces political, geographic, chronological, roster, military, economic, localization, and source-coverage reports.

## Public contracts

- `ScenarioDefinition`: scenario ID/version, date, required manifests, history settings, `imperial_core`, initial-state record references, pressures, and player-start rules.
- `ScenarioOverlay`: initial mutable state applied to immutable base content.
- `HistoricalPressure`: prerequisites, weighting/settings behavior, expiration, commands/events, and alternative outcomes.
- `ScenarioValidationReport`: coverage and consistency results by subsystem.
- Scenario loading produces the initial `WorldSnapshot`; subsequent play uses ordinary `CampaignCommand`/`CampaignEvent` contracts.

## Data flow

```text
Historical research + base content + scenario overlay + selected history setting
→ cross-system validation
→ initial WorldSnapshot and player-start options
→ ordinary campaign simulation
→ conditional pressures evaluated through normal commands/events
→ emergent continuation toward unification
```

## Implementation workstreams

1. Define scenario, overlay, pressure, history-setting, start-option, and validation schemas.
2. Build content reports for dates, location, office/title conflicts, allegiance, claims/control, armies, economy, sources, and localization.
3. Author a small synthetic scenario to prove deterministic initialization and alternative outcomes.
4. Research and author the bounded 191 Central Plains vertical slice.
5. Expand 191 into the full Early Access bookmark and curated roster.
6. Author 관도대전 with Yuan/Cao internal blocs, imperial politics, logistics, defections, and multi-power opportunities.
7. Author 적벽대전 with Jing succession, surrender/resistance, Sun court debate, fleet/port state, coalition formation, and combined warfare.
8. Grow regional content and roster to the 1.0 targets while retaining source/review status.

## Edge cases and failure handling

- A pressure whose prerequisites cease to exist expires or branches; it never resurrects dead characters or transfers unavailable territory.
- Historical death/lifespan settings are scenario options where supported and do not override active player choices without declared rules.
- A custom/generated faction may alter political balance but cannot corrupt required scenario entities or IDs.
- Missing optional Romance packs disable those pressures cleanly.
- An incomplete development scenario cannot be marked release-ready until every required subsystem/source/localization report passes.
- Bookmark dates with disputed chronology record the chosen snapshot and alternatives rather than claiming false precision.

## Performance budget

- Validate and initialize the full projected 1.0 scenario in under 30 seconds on the development Mac.
- Scenario selection displays summary/start options within 2 seconds after validated metadata is indexed.
- Conditional pressure evaluation remains within the overall campaign-turn budget through indexed prerequisites.

## Tests

- Scenario schema, overlay, manifest, source, localization, and initial-state consistency tests.
- Deterministic initialization/checksum tests across platforms.
- Historical-pressure prerequisite, expiration, alternative outcome, and sandbox-disable tests.
- Custom faction and generated-character compatibility tests.
- Named-crisis continuation tests proving the campaign remains playable toward unification.
- Long simulation tests from each base bookmark under all history settings.

## Acceptance criteria

- [ ] The same systems and victory rules run in every bookmark.
- [ ] The bounded 191 slice initializes deterministically from validated data.
- [ ] Historical pressures branch or expire based on world state without forcing outcomes.
- [ ] All three history settings produce their defined behavior.
- [ ] Early Access 191 and version 1.0 content/roster gates are measurable through reports.
- [ ] 관도대전 and 적벽대전 remain complete campaigns after their named conflicts.
- [ ] Every release scenario passes sources, chronology, localization, and cross-system validation.

## Risks

| Risk | Mitigation |
|---|---|
| Historical research expands without end | Define bookmark-specific required entities/claims and confidence thresholds; defer peripheral detail by milestone. |
| Events recreate history despite changed conditions | Resolve every pressure through current prerequisites, actors, resources, and ordinary commands/events. |
| Multiple bookmarks multiply maintenance | Share immutable base records, overlays, validators, and automated long-run tests. |
| Regional content is uneven at Early Access | Disclose coverage, use generated systemic fill, and prioritize functional parity before flavor volume. |

## Deferred work

- 황건적의 난, 반동탁연합군, 삼국정립, 이릉대전, 오장원의 지는 별, 이궁지쟁, and 고평릉사변 bookmarks.
- Public scenario editor.
- Additional hypothetical start dates.
- Full literary Romance campaign pack.
