# Milestone Roadmap

| Field | Value |
|---|---|
| Master-plan version | 0.2.0 |
| Updated | 2026-07-15 |
| Active milestone | **M2 — 191 campaign slice** |
| Scheduling policy | Milestone gates; no public dates before M4 |

## Status legend

- **Active**: the only milestone currently accepting implementation work.
- **Planned**: defined but blocked by earlier gates.
- **Complete**: every exit criterion is verified.

## Milestones

| ID | Milestone | Status | Exit gate summary |
|---|---|---|---|
| M0 | Source of truth and toolchain | **Complete** | Documentation hierarchy, pinned tools, LFS, native macOS local smoke, and exact-SHA hosted macOS/Windows CI/export/automated smoke |
| M1 | Headless simulation foundation | **Complete** | Deterministic commands/events, calendar, stable IDs, validated content, saves/migrations, and ten-year synthetic soak |
| M2 | 191 campaign slice | **Active** | Bounded Central Plains campaign with character play, routes, politics, economy, diplomacy, supply, and faction founding |
| M3 | Tactical battle slice | Planned | RTWP battle from 18 through 54 units with morale, formations, ranged combat, rout, pursuit, and campaign result round-trip |
| M4 | Integrated vertical slice and public demo | Planned | One uninterrupted 191 loop, physical Windows validation, cross-platform demo, onboarding, and performance budgets met |
| M5 | Initial paid Early Access | Planned | Full 191 campaign, world topology, 300–400 historical characters, generated fill, Korean/English, and core combined systems |
| M6 | Early Access expansion | Planned | Regional depth, roster growth, 관도대전, 적벽대전, expanded systems, mod documentation, AI and polish |
| M7 | Version 1.0 | Planned | Three bookmarks, full map, 800–1,000 historical characters, complete localization, regression, migration, and release QA |

## M0 exit criteria

- [x] Master plan and subplan structure defined.
- [x] Godot 4.6.1 .NET and .NET 10 LTS pinned and installed.
- [x] Git LFS patterns and repository conventions configured.
- [x] Exact-SHA macOS and Windows CI runners pass validation, build, tests, import, export, automated smoke, manifest checks, and artifact upload.
- [x] Godot export presets created for Windows x64 and macOS arm64.
- [x] Apple Silicon macOS development export launches locally and records its manifest.
- [x] Signing/notarization and Authenticode workflows are configured, secret-gated, and fail closed; production credential verification is deferred to SP-15.
- [x] Historical-source and AI-asset provenance registers established.

Physical Windows testing and production signing are not M0 exit criteria under [ADR-0001](adr/0001-mac-first-development-deferred-physical-windows-verification.md).

Exact SHA `7f62a97cf880ae6ded8e47af8737a11e53479977` passed both hosted jobs and artifact/manifest inspection. See the [passing report](evidence/M0-EXACT-SHA-7f62a97.md). The earlier [`1ab375a` report](evidence/M0-EXACT-SHA-1ab375a.md) remains the historical failed attempt.

## M1 exit criteria

- [x] Deterministic commands/events, calendar, stable IDs, ordered resolution, isolated random streams, and simulation tiers have automated coverage.
- [x] A ten-year/1,000-entity soak completes with checksum `105da5fd449cc2d00ba1bf979642b22107db5b236eab30baac437f1b9b8bf088` on both hosted platforms.
- [x] Save/load, recovery, migrations, failed-migration preservation, and contract compatibility pass.
- [x] Content schemas, validation, stable IDs, sources/provenance, and Korean/English localization coverage pass.
- [x] Built-in/mod order, overrides, conflicts, save compatibility, and registry checksum pass on both hosted platforms with zero applicable diagnostics.

The simulation checksum is established by the exact-source assertion plus complete hosted suite passes; the registry checksum is directly logged. See the [passing report](evidence/M0-EXACT-SHA-7f62a97.md).

SP-04B-L exact SHA `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a` passed [hosted macOS arm64 and Windows x64 validation, build, complete tests, import, native export, automated smoke, manifest inspection, and artifact verification](evidence/SP-04B-EXACT-SHA-ff7420f.md). This establishes SP-04 criterion B15 only. SP-04 and M2 remain Active, SP-05 remains blocked, and the full SP-04 acceptance criteria remain unchecked.

SP-04C0 descriptor, agency, typed-kinship, authored v1/v2 normalization, and schema-6 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04C0-EXACT-SHA-7d4612d.md) at `7d4612d21784ceebbcd574ea00231785b9408036`. SP-04 and M2 remain Active, SP-05 remains blocked, and the full SP-04 acceptance criteria remain unchecked.

SP-04C1's bounded retinue, patronage, recommendation, employment-history, registered character-action, generic-memory, and schema-7 package has passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04C1-EXACT-SHA-d5d2705.md) at `d5d2705d3516c67a06e127dcfa867a854b37a21f`. Personal resources and every later marriage, lifecycle, succession, faction, battle, content, AI, and presentation package were still pending at C1 acceptance; the full SP-04 three-second turn budget remains unmet.

SP-04C2's abstract personal-wealth accounts, atomic registered transfers, bounded ledger/history, and authenticated schema-7-to-8 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04C2-EXACT-SHA-e2d9590.md) at `e2d9590afc409da30aef86226a8d90a0023fbda3`. Opaque estate holdings are reserved for C3; economy, land, household/faction treasuries, and later character packages remain outside C2. The full SP-04 three-second turn budget remains unmet.

SP-04C3's opaque owner-independent estate identities, defensive character-ownership queries, dead-owner preservation seam, accepted 64-holding bound, and authenticated schema-8-to-9 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04C3-EXACT-SHA-7b9f795.md) at `7b9f795320e5f4c14aa7e14185e7ba035fdf6847`. Physical/economic estates and inheritance behavior remain outside C3. The full SP-04 three-second turn budget remains unmet.

SP-04D0's immutable practice/proposal/betrothal/union/romance/history contracts, exact causal and eligibility validation, bounded canonical queries, and authenticated schema-9-to-10 migration are locally verified. D0 adds no marriage workflow, household decision, relationship effect, lifecycle, succession, content, or presentation behavior. Exact-SHA hosted macOS arm64/Windows x64 evidence remains pending, the full SP-04 criteria remain unchecked, and the three-second turn budget remains unmet.

## Platform verification gates

- Through M3, Apple Silicon macOS is the primary interactive and visual development platform.
- Every accepted development revision must keep hosted Windows build/test/export/automated-smoke coverage healthy.
- Before M4 closes or a public demo ships, a physical Windows x64 machine must pass native smoke, input/display, packaged save, and representative playtesting checks.
- Before any public promotion, SP-15 must verify macOS signing/notarization, Windows Authenticode, Steam behavior, clean installs/updates, and release-candidate artifacts.

## Scheduling rule

Before M4, estimates are internal ranges only. After M4, record measured velocity across at least three completed work packages before publishing any Early Access window. If a gate cannot be met solo, reduce content breadth or presentation cost before altering core product pillars.

## Current work order

1. Preserve the completed SP-03 functional-map baseline established by the accepted Later Han Apple Silicon [working-tree presentation evidence](evidence/SP-03-later-han-map-working-tree-2026-07-13/README.md) and exact-SHA [hosted macOS/Windows evidence](evidence/SP-03-EXACT-SHA-f91dfce.md).
2. Keep detailed cartographic refinement deferred to pre–Early Access work.
3. Preserve the accepted SP-04A foundations for save integrity/recovery, typed-content validation/schema coverage, authoritative query naming, and evidence traceability at exact revision `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea`, supported by the [passing hosted macOS arm64/Windows x64 report](evidence/SP-04A-EXACT-SHA-eaa3aaf.md).
4. Preserve the locally verified SP-04B-L bounded directional relationship/memory kernel, schema-5 migration chain, observer-filtered summaries, bounded-history evidence, local performance results, and accepted exact-SHA SP-04B-H hosted evidence at `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a`.
5. Preserve the SP-04C0 character-v2 descriptor/condition/typed-kinship contracts, owning-pack override lineage, strict authored v1/v2 normalization, authenticated schema-5-to-6 migration, and exact-SHA hosted evidence at `7d4612d21784ceebbcd574ea00231785b9408036`.
6. Preserve SP-04C1's bounded career/social-action kernel, atomic generic-memory consequences, authenticated schema-6-to-7 migration, independent-remediation record, and exact-SHA hosted evidence at `d5d2705d3516c67a06e127dcfa867a854b37a21f`.
7. Preserve SP-04C2's sparse personal-wealth accounts, registered atomic transfers, bounded ledger/history, authenticated schema-7-to-8 migration, independent-remediation record, and exact-SHA hosted evidence at `e2d9590afc409da30aef86226a8d90a0023fbda3`.
8. Preserve SP-04C3's opaque owner-independent estate identities, dead-owner persistence seam, accepted 64-holding bound, authenticated schema-8-to-9 migration, and exact-SHA hosted evidence at `7b9f795320e5f4c14aa7e14185e7ba035fdf6847`.
9. Preserve the locally verified SP-04D0 immutable marriage foundation, authenticated schema-9-to-10 migration, and independent-remediation record while its exact-SHA hosted gate remains pending.
10. Implement political proposal, response, betrothal, and union workflow as SP-04D1 without importing adult romance progression or household effects.

M0, M1, and SP-03 are complete. M2 remains Active and is not complete. SP-04 is Active: SP-04A, SP-04B-L, SP-04C0, SP-04C1, SP-04C2, and SP-04C3 have passing exact-SHA hosted macOS arm64/Windows x64 evidence at their accepted revisions; SP-04D0 is locally verified with hosted evidence pending. SP-05 remains blocked, and later SP-04 packages remain pending.

See the [subsystem plan index](plans/README.md) for dependency status.
