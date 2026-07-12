# Milestone Roadmap

| Field | Value |
|---|---|
| Master-plan version | 0.1.0 |
| Updated | 2026-07-12 |
| Active milestone | **M0 — Source of truth and toolchain** |
| Scheduling policy | Milestone gates; no public dates before M4 |

## Status legend

- **Active**: the only milestone currently accepting implementation work.
- **Planned**: defined but blocked by earlier gates.
- **Complete**: every exit criterion is verified.

## Milestones

| ID | Milestone | Status | Exit gate summary |
|---|---|---|---|
| M0 | Source of truth and toolchain | **Active** | Documentation hierarchy, pinned tools, CI, LFS, signing prerequisites, and empty Windows/macOS smoke builds |
| M1 | Headless simulation foundation | Planned | Deterministic commands/events, calendar, stable IDs, validated content, saves/migrations, and ten-year synthetic soak |
| M2 | 191 campaign slice | Planned | Bounded Central Plains campaign with character play, routes, politics, economy, diplomacy, supply, and faction founding |
| M3 | Tactical battle slice | Planned | RTWP battle from 18 through 54 units with morale, formations, ranged combat, rout, pursuit, and campaign result round-trip |
| M4 | Integrated vertical slice and public demo | Planned | One uninterrupted 191 loop, cross-platform demo, onboarding, and performance budgets met |
| M5 | Initial paid Early Access | Planned | Full 191 campaign, world topology, 300–400 historical characters, generated fill, Korean/English, and core combined systems |
| M6 | Early Access expansion | Planned | Regional depth, roster growth, 관도대전, 적벽대전, expanded systems, mod documentation, AI and polish |
| M7 | Version 1.0 | Planned | Three bookmarks, full map, 800–1,000 historical characters, complete localization, regression, migration, and release QA |

## M0 exit criteria

- [x] Master plan and subplan structure defined.
- [x] Godot 4.6.1 .NET and .NET 10 LTS pinned and installed.
- [x] Git LFS patterns and repository conventions configured.
- [ ] macOS and Windows CI runners execute documentation and empty-project checks.
- [x] Godot export presets created for Windows x64 and macOS arm64.
- [ ] Empty Windows and signed/notarized macOS smoke builds produced.
- [x] Historical-source and AI-asset provenance registers established.

## Scheduling rule

Before M4, estimates are internal ranges only. After M4, record measured velocity across at least three completed work packages before publishing any Early Access window. If a gate cannot be met solo, reduce content breadth or presentation cost before altering core product pillars.

## Current work order

1. After explicit user authorization, review and stage the M0-A candidate set, create the first baseline commit, and push that exact SHA to the confirmed remote.
2. Run SP-00 unsigned hosted macOS and Windows CI for that exact SHA and record native smoke evidence.
3. Collect release-signing/notarization evidence only when separately authorized credentials are available.
4. Run the implemented SP-01 golden checksum on both hosted platforms and record the matching result.
5. Run the implemented SP-02 registry checksum/load-order suite on both hosted platforms.
6. Close the M0 gate before promoting M1 or beginning dependency-gated SP-03 work.

SP-01 and SP-02 are implemented and locally verified, but this does not bypass the active M0 gate. SP-03 implementation also exists locally ahead of its milestone; its dependency and presentation acceptance gates remain blocked.

See the [subsystem plan index](plans/README.md) for dependency status.
