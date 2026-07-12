# Subsystem Plan Index

Master plan: [version 0.1.0](../MASTER_PLAN.md)  
Active milestone: **M0 — Source of truth and toolchain**

Subplans elaborate the master plan but may not redefine it. SP-00 remains in progress. SP-01 and SP-02 have been implemented locally ahead of their external dependency gates, but they cannot close M1 or unblock downstream plans until SP-00 and their cross-platform checksum runs are verified. SP-03 implementation also exists locally ahead of its M2 gate; the plan and its presentation acceptance remain dependency-blocked.

| ID | Subplan | First milestone | Status | Dependencies |
|---|---|---|---|---|
| SP-00 | [Repository, toolchain, CI, and packaging](SP-00-repository-toolchain-ci-packaging.md) | M0 | **In progress** | None |
| SP-01 | [Simulation, calendar, determinism, and saves](SP-01-simulation-calendar-determinism-saves.md) | M1 | **Implemented locally; same-revision hosted cross-platform verification pending** | SP-00 |
| SP-02 | [Content, localization, modding, and research](SP-02-content-localization-modding-research.md) | M1 | **Implemented locally; same-revision hosted cross-platform verification pending** | SP-01 |
| SP-03 | [Campaign map, regions, routes, and supply](SP-03-campaign-map-regions-routes-supply.md) | M2 | **Implemented locally ahead of gate; plan/acceptance blocked** | SP-01, SP-02 |
| SP-04 | [Characters, family, marriage, and succession](SP-04-characters-family-marriage-succession.md) | M2 | Planned/blocked | SP-01, SP-02 |
| SP-05 | [Factions, court, emperor, diplomacy, and espionage](SP-05-factions-court-emperor-diplomacy-espionage.md) | M2 | Planned/blocked | SP-03, SP-04 |
| SP-06 | [Population, economy, administration, and technology](SP-06-population-economy-administration-technology.md) | M2 | Planned/blocked | SP-03, SP-04, SP-05 |
| SP-07 | [Armies, recruitment, equipment, and logistics](SP-07-armies-recruitment-equipment-logistics.md) | M2 | Planned/blocked | SP-03, SP-04, SP-05, SP-06 |
| SP-08 | [Tactical runtime, command, morale, and formations](SP-08-tactical-runtime-command-morale-formations.md) | M3 | Planned/blocked | SP-01, SP-07 |
| SP-09 | [Siege, naval, shoreline, and combined battles](SP-09-siege-naval-shoreline-combined-battles.md) | M5 | Planned/blocked | SP-08 |
| SP-10 | [Strategic/tactical AI and simulation tiers](SP-10-strategic-tactical-ai-simulation-tiers.md) | M2 | Planned/blocked | SP-01, SP-03–SP-09 |
| SP-11 | [UI, UX, accessibility, and tutorial](SP-11-ui-ux-accessibility-tutorial.md) | M2 | Planned/blocked | Relevant active systems |
| SP-12 | [Custom/generated characters and faction founding](SP-12-custom-generated-characters-faction-founding.md) | M2 | Planned/blocked | SP-02, SP-04–SP-07 |
| SP-13 | [Scenarios and historical content pipeline](SP-13-scenarios-historical-content-pipeline.md) | M2 | Planned/blocked | SP-02–SP-12 |
| SP-14 | [Art, animation, VFX, audio, and provenance](SP-14-art-animation-vfx-audio-provenance.md) | M3 | Planned/blocked | SP-08, SP-11, SP-13 |
| SP-15 | [Steam, release QA, and crash reporting](SP-15-steam-release-qa-crash-reporting.md) | M0 | Planned/blocked | SP-00, SP-11, SP-14 |

## Authoring rules

- Start new plans from [SUBPLAN_TEMPLATE.md](SUBPLAN_TEMPLATE.md).
- Link every dependency and applicable [ADR](../adr/README.md).
- Keep deferred features separate from acceptance criteria.
- Update this index whenever plan status or dependency gates change.
- A contradiction with the master plan requires an ADR and master-plan revision before implementation.
