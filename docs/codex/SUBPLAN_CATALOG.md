# Subplan Execution Catalog

This is a routing aid, not a status authority. Always read [the roadmap](../ROADMAP.md) and [subplan index](../plans/README.md) before selecting work.

| Plan | Dependency gate | Primary agents | First useful package when unblocked |
|---|---|---|---|
| SP-00 | None; active M0 | build-release-engineer, verification-reviewer | auditable baseline, then same-SHA CI and platform evidence |
| SP-01 | SP-00 | simulation-engineer, verification-reviewer | same-revision cross-platform golden checksum and save compatibility |
| SP-02 | SP-01 | content-engineer, verification-reviewer | same-revision registry/load-order checksum and content diagnostics |
| SP-03 | SP-01, SP-02 | simulation-engineer, godot-presentation-engineer | correct map labels/mode semantics, then interactive visual evidence |
| SP-04 | SP-01, SP-02 | project-architect, simulation-engineer, content-engineer | character/family contracts plus a deterministic synthetic household slice |
| SP-05 | SP-03, SP-04 | project-architect, simulation-engineer | faction/subfaction membership and authority slice before court/diplomacy breadth |
| SP-06 | SP-03, SP-04, SP-05 | simulation-engineer, content-engineer | one locality population/store/production loop with conservation tests |
| SP-07 | SP-03 through SP-06 | simulation-engineer, content-engineer | one persistent army recruitment/supply/battle-round-trip slice |
| SP-08 | SP-01, SP-07 | simulation-engineer, godot-presentation-engineer | fixed-tick headless formation/morale battle before visual scale-up |
| SP-09 | SP-08 | project-architect, simulation-engineer, godot-presentation-engineer | generic front graph with a two-front synthetic battle |
| SP-10 | Applicable SP-01 and SP-03 through SP-09 contracts | project-architect, simulation-engineer | deterministic perceived-information AI for one bounded 191 decision loop |
| SP-11 | Relevant active systems | godot-presentation-engineer, verification-reviewer | campaign shell tokens/input/accessibility plus the active system UI |
| SP-12 | SP-02, SP-04 through SP-07 | content-engineer, simulation-engineer, godot-presentation-engineer | deterministic custom character plus one viable landless start |
| SP-13 | SP-02 through SP-12 as applicable | content-engineer, simulation-engineer | validated synthetic scenario before historical roster scaling |
| SP-14 | SP-08, SP-11, SP-13 | content-engineer, godot-presentation-engineer | approved art/audio budgets and one provenance-complete prototype kit |
| SP-15 | SP-00 groundwork; SP-11 and SP-14 for public release | build-release-engineer, verification-reviewer | local/no-Steam boundary and reproducible private build manifest |

## Package sizing

A package should normally have:

- one player-visible or operator-visible outcome;
- one authoritative processing path;
- one bounded write owner per layer;
- focused unit or integration tests;
- explicit documentation updates;
- exact completion evidence; and
- no unresolved dependency or product decision.

Split a package when it mixes unrelated acceptance criteria, needs two agents to edit the same contracts, requires both implementation and unavailable external credentials, or cannot be reverted and reviewed independently.
