# Subsystem Plan Index

Master plan: [version 0.2.0](../MASTER_PLAN.md)
Active milestone: **M2 — 191 campaign slice**

Subplans elaborate the master plan but may not redefine it. Exact SHA `7f62a97cf880ae6ded8e47af8737a11e53479977` closes SP-00/M0 and SP-01/SP-02/M1. Exact SHA `f91dfce730f1e116bd17321e8a0a654a69823c69` plus its accepted Apple Silicon presentation record closes SP-03. M2 remains Active.

SP-04A exact SHA `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04A-EXACT-SHA-eaa3aaf.md). SP-04B-L exact SHA `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04B-EXACT-SHA-ff7420f.md). SP-04C0 exact SHA `7d4612d21784ceebbcd574ea00231785b9408036` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04C0-EXACT-SHA-7d4612d.md). SP-04C1 exact SHA `d5d2705d3516c67a06e127dcfa867a854b37a21f` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04C1-EXACT-SHA-d5d2705.md). SP-04C2 exact SHA `e2d9590afc409da30aef86226a8d90a0023fbda3` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04C2-EXACT-SHA-e2d9590.md). SP-04C3 exact SHA `7b9f795320e5f4c14aa7e14185e7ba035fdf6847` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04C3-EXACT-SHA-7b9f795.md). SP-04D0 through SP-04E3 retain the exact-SHA hosted evidence linked from their plan sections.

SP-04E4 exact SHA `177346b7358e84da358f3bfac8057b6ea70ed412` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04E4-EXACT-SHA-177346b.md). SP-04E5 exact SHA `4b28fb74bed9181ce021e1c5e32ef9d039b4e2e1` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04E5-EXACT-SHA-4b28fb7.md). SP-04E6 exact SHA `0928e4484ef41da5ac31f5683af5347800a92dec` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04E6-EXACT-SHA-0928e44.md). SP-04F0 exact SHA `783ccfb61357248158cf287ee69ba27b56c38f4a` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F0-EXACT-SHA-783ccfb.md). SP-04F1 exact SHA `23045a06a39361ecf8d2ef341cc0458590322f0a` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F1-EXACT-SHA-23045a0.md). SP-04F2 exact SHA `ab8917a95ea064911a584cd640647374745fd2c7` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F2-EXACT-SHA-ab8917a.md). SP-04F3 exact SHA `72e5bd34f41f068c2e07a580e02522f8222eca30` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F3-EXACT-SHA-72e5bd3.md). SP-04F4 exact SHA `ebde5387ac2d7398105f11043d9cdaeb2c2ae187` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F4-EXACT-SHA-ebde538.md). SP-04F5 exact SHA `bb9dcffab8f517e7a2f9cb0b47072bb096fc7725` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F5-EXACT-SHA-bb9dcff.md). SP-04F6 exact SHA `7e96930d103081e981c0cf1a06736223e222bc07` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F6-EXACT-SHA-7e96930.md). SP-04F7 exact SHA `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04F7-EXACT-SHA-62e03b6.md). SP-04F8 neutral explicit succession support is locally verified, with its implementation commit and exact-SHA hosted evidence pending. Full SP-04 remains Active and SP-05 remains blocked. Physical Windows and production signing remain M4/SP-15 gates under ADR-0001.

| ID | Subplan | First milestone | Status | Dependencies |
|---|---|---|---|---|
| SP-00 | [Repository, toolchain, CI, and packaging](SP-00-repository-toolchain-ci-packaging.md) | M0 | **Complete** | None |
| SP-01 | [Simulation, calendar, determinism, and saves](SP-01-simulation-calendar-determinism-saves.md) | M1 | **Complete** | SP-00 |
| SP-02 | [Content, localization, modding, and research](SP-02-content-localization-modding-research.md) | M1 | **Complete** | SP-01 |
| SP-03 | [Campaign map, regions, routes, and supply](SP-03-campaign-map-regions-routes-supply.md) | M2 | **Complete** | SP-01, SP-02 |
| SP-04 | [Characters, family, marriage, and succession](SP-04-characters-family-marriage-succession.md) | M2 | **Active — through F7 accepted; F8 hosted pending** | SP-01, SP-02 |
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
| SP-15 | [Steam, release QA, and crash reporting](SP-15-steam-release-qa-crash-reporting.md) | M0 groundwork / M4 validation | Planned/blocked | SP-00, SP-11, SP-14 |

## Authoring rules

- Start new plans from [SUBPLAN_TEMPLATE.md](SUBPLAN_TEMPLATE.md).
- Link every dependency and applicable [ADR](../adr/README.md).
- Keep deferred features separate from acceptance criteria.
- Update this index whenever plan status or dependency gates change.
- A contradiction with the master plan requires an ADR and master-plan revision before implementation.
