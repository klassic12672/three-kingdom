# Milestone Roadmap

| Field | Value |
|---|---|
| Master-plan version | 0.2.0 |
| Updated | 2026-07-16 |
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

SP-04D0's immutable practice/proposal/betrothal/union/romance/history contracts, exact causal and eligibility validation, bounded canonical queries, and authenticated schema-9-to-10 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04D0-EXACT-SHA-f7fef24.md) at `f7fef247178776d7c6fb1c4bed56f09dece76ff4`. D0 adds no marriage workflow, household decision, relationship effect, lifecycle, succession, content, or presentation behavior. The full SP-04 criteria remain unchecked, and the three-second turn budget remains unmet.

SP-04D1's participant-issued political proposal/response/withdrawal, betrothal cancellation, adult fulfillment, deterministic causal-group retention, registered command/event, pending replay, and authenticated schema-10-to-11 package has passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04D1-EXACT-SHA-653ce71.md) at `653ce71d24bd81435ded9e65022dc29afd8f4810`. D1 adds no adult romance progression, household decision or movement, relationship consequence, lifecycle, succession, content, UI, AI, or platform behavior; the full SP-04 criteria and three-second turn budget remain unchecked.

SP-04D2's mutual-consent adult romance invitation/acceptance, one-step progression, completion/ending, deterministic race handling, bounded retention, legacy-route compatibility, and authenticated schema-11-to-12 package have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04D2-EXACT-SHA-62a5007.md) at `62a50075ca86b3466cca9c05825d4374e6cac366`. The package adds no proposal/union, relationship or memory effect, household decision/movement, coercion consequence, lifecycle, succession, content, UI, AI, or platform behavior. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04D3's narrow head-authorized household decisions, exact-custodian coerced political unions, harmful target-to-actor memories with zero attraction, system-authoritative condition changes, atomic marriage-lifecycle invalidation, internal death preview, and authenticated schema-12-to-13 compatibility package have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04D3-EXACT-SHA-93d3881.md) at `93d38810a87707a7c4c98c7392e2a2f20dc030fb`. Public death, succession/inheritance, later character packages, the full SP-04 criteria, and the three-second turn budget remain pending.

SP-04E0's reserved-system establishment of current legal-adoptive parentage, complete retained-marriage preflight, action-local parent bounds, and authenticated vocabulary-only schema-13-to-14 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E0-EXACT-SHA-30fd0ad.md) at `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed`. It adds no guardianship, residence/family movement, effective-dated adoption history, pregnancy/birth, education, coming of age, inheritance, succession, content, UI, or platform behavior. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04E1's bounded primary-guardianship state/query, reserved-system establishment workflow, and authenticated schema-14-to-15 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E1-EXACT-SHA-97b607a.md) at `97b607ae8df77dbd5c6fa5ab6b544000208cdb0e`. It grants no education, residence, custody, inheritance, succession, adult-regency, co-guardian, consent, content, UI, AI, battle, or platform behavior. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04E2's reserved-system primary-guardianship ending and atomic replacement workflow, exact state/race revalidation, and authenticated vocabulary-only schema-15-to-16 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E2-EXACT-SHA-7491da8.md) at `7491da89985fedb18e423082a2fd9187b8899e52`. The package reuses guardianship-v1 state, changes no other subsystem, and adds no automatic birthday/death termination, authority/consent, education, pregnancy/birth, inheritance, succession, content, or presentation behavior. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04E3's deterministic exact-birthday coming-of-age command/event, atomic `WardCameOfAge` primary-guardianship closure, and authenticated vocabulary-only schema-16-to-17 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E3-EXACT-SHA-59588be.md) at `59588be9d277dc4c4cb7ec98ef99e33591b0eeda`. The package adds no adult flag, birth, education, mutable ability/trait, public death, inheritance, succession, content, or presentation behavior. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04E4's active-only, explicit-role, adult union-linked pregnancy registration and authenticated schema-17-to-18 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E4-EXACT-SHA-177346b.md) at `177346b7358e84da358f3bfac8057b6ea70ed412`. The package adds no conception scheduler, birth, child creation, pregnancy loss, death integration, inheritance, education, reproductive descriptors, content, UI, or AI. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04E5's deterministic due/overdue pregnancy resolution, generated-child insertion, biological parent links, parental placement/trait validation, and authenticated vocabulary-only schema-18-to-19 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E5-EXACT-SHA-4b28fb7.md) at `4b28fb74bed9181ce021e1c5e32ef9d039b4e2e1`. The package adds no automatic scheduling or naming, pregnancy loss, twins, education, public death, inheritance, content, UI, or AI. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04E6's source-backed primary-guardian education attainment, runtime character-v3 effective abilities, authored v1/v2 separation, and authenticated schema-19-to-20 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04E6-EXACT-SHA-0928e44.md) at `0928e4484ef41da5ac31f5683af5347800a92dec`. The package adds no education scheduler/progress/levels, mutable definitions, relationship effects, public death, inheritance, succession, content, UI, or AI. The full SP-04 criteria and three-second turn budget remain unchecked.

SP-04F0's restricted public-death action/outcome, atomic character/marriage/guardianship/pregnancy lifecycle resolution, explicit household-head/custodian/career/retinue fail-closed seams, dead-owned wealth/estate preservation, and authenticated vocabulary-only schema-20-to-21 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F0-EXACT-SHA-783ccfb.md) at `783ccfb61357248158cf287ee69ba27b56c38f4a`. Inheritance, successor selection, career death closure, household-head/captive disposition, player continuity, full SP-04 acceptance, and the three-second budget remain open.

SP-04F1's atomic career-death proposal invalidation and service closure, retained-retinue preservation, household-head/current-custodian blockers, and authenticated structural schema-21-to-22 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F1-EXACT-SHA-23045a0.md) at `23045a06a39361ecf8d2ef341cc0458590322f0a`. Inheritance, successor selection, retinue succession, household-head/captive disposition, player continuity, full SP-04 acceptance, and the three-second budget remain open.

SP-04F2's non-head custodian-death custody release, canonical death-v3 evidence, unchanged household-head blocker, and authenticated structural schema-22-to-23 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F2-EXACT-SHA-ab8917a.md) at `ab8917a95ea064911a584cd640647374745fd2c7`. Reassignment, inheritance, successor selection, retinue succession, household-head replacement, player continuity, full SP-04 acceptance, and the three-second budget remain open.

SP-04F3's exact reserved-system household-head death handoff, unchanged ordinary death-v3 contract, mandatory head-change-v1 evidence, and vocabulary-only schema-23-to-24 migration have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F3-EXACT-SHA-72e5bd3.md) at `72e5bd34f41f068c2e07a580e02522f8222eca30`. Its 50 focused cases and zero-warning full Release gate pass 904 Simulation.Core, 73 Game.Content, 6 Game.Application, and 18 repository tests; validation, diff, LFS, focused formatter checks, and independent architecture/schema/verification reviews also pass. Automatic selection, legal succession, inheritance, claims, regency, retinue succession, player continuity, full SP-04 acceptance, and the three-second budget remain open.

SP-04F4's character-issued explicit heir designation/replacement/revocation intent, exact-current concurrency, bounded topology/overflow-safe lifecycle evidence, schema-25 structural persistence, frozen schema-24 F3 authentication, and deterministic participant-death ordering have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F4-EXACT-SHA-ebde538.md) at `ebde5387ac2d7398105f11043d9cdaeb2c2ae187`. Its 95 focused cases and zero-warning full Release gate pass 1,005 Simulation.Core, 73 Game.Content, 6 Game.Application, and 18 repository tests; both hosted platforms repeat the complete suite during native export/smoke. A designation grants no legal status and is not consumed by F3, inheritance, claims, regency, retinues, or player continuity. Full SP-04 acceptance and the three-second budget remain open.

SP-04F5's query-only pairwise legal-candidate evaluator, explicit transient rule inputs, typed biological/legal-adoptive/legacy-unknown descendant paths, current-F4-designation basis, canonical issue evidence, and schema/system/persistence neutrality have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F5-EXACT-SHA-bb9dcff.md) at `bb9dcffab8f517e7a2f9cb0b47072bb096fc7725`. Its 105 focused cases and zero-warning full Release gate pass 1,015 Simulation.Core, 73 Game.Content, 6 Game.Application, and 18 repository tests; both hosted platforms repeat the complete suite during native export/smoke. F5 adds no precedence, claim, selection, mutation, persisted law, inheritance, regency, or player continuity. Full SP-04 acceptance and the three-second budget remain open.

SP-04F6's query-only caller-bounded candidate-set enumeration has passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F6-EXACT-SHA-7e96930.md) at `7e96930d103081e981c0cf1a06736223e222bc07`. It scans every authoritative profile through the accepted F5 rule, returns each eligible candidate once with every recognized basis, orders complete results by canonical ID without implying precedence, and fails closed with an exact total and no partial set when the caller maximum is exceeded. Its 115 focused cases and zero-warning full Release gate pass 1,025 Simulation.Core, 73 Game.Content, 6 Game.Application, and 18 repository tests; both hosted platforms repeat the complete suite during native export/smoke. Schema 25, succession snapshot/system v1, persistence, commands/events, and F4/F5 behavior remain unchanged. Full SP-04 acceptance and the three-second budget remain open.

SP-04F7's neutral character-issued personal succession claims have passing [exact-SHA hosted macOS arm64/Windows x64 evidence](evidence/SP-04F7-EXACT-SHA-62e03b6.md) at `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4`. F7 adds exact assertion/withdrawal, one active ordered-pair claim, 64/64 active capacities, 32 recent withdrawn records per subject with checked folding, subject-bounded authoritative queries, and no eligibility, precedence, selection, or downstream effect. Save schema 26 advances succession snapshot/system to v2 with an authenticated exact-F6 schema-25 migration and frozen fixture from `7e96930d103081e981c0cf1a06736223e222bc07`. Its 167 focused cases and zero-warning full Release gate pass 1,077 Simulation.Core, 73 Game.Content, 6 Game.Application, and 18 repository tests; both hosted platforms repeat the complete suite during native export/smoke. Full SP-04 acceptance and the three-second budget remain open.

SP-04F8's neutral character-issued succession support is locally verified. It adds exact declaration/replacement/withdrawal, one active record per ordered subject/supporter pair, 64/64 active capacities, 32 recent terminal records per subject with checked replaced/withdrawn folding, bounded authoritative queries, and no support strength, eligibility, precedence, selection, or downstream effect. Save schema 27 advances succession snapshot/system to v3 with an authenticated exact-F7 schema-26 migration and frozen fixture from `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4`. Its 216 focused cases and zero-warning full Release gate pass 1,126 Simulation.Core, 73 Game.Content, 6 Game.Application, and 18 repository tests. Exact-SHA hosted macOS arm64/Windows x64 acceptance, full SP-04 acceptance, and the three-second budget remain open.

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
9. Preserve SP-04D0's immutable marriage foundation, authenticated schema-9-to-10 migration, independent-remediation record, and exact-SHA hosted evidence at `f7fef247178776d7c6fb1c4bed56f09dece76ff4`.
10. Preserve SP-04D1's political proposal/response/betrothal/union workflow, causal-group retention, authenticated schema-10-to-11 compatibility package, and exact-SHA hosted evidence at `653ce71d24bd81435ded9e65022dc29afd8f4810`.
11. Preserve SP-04D2's adult non-explicit mutual-consent romance workflow, authenticated schema-11-to-12 compatibility package, and exact-SHA hosted evidence at `62a50075ca86b3466cca9c05825d4374e6cac366`.
12. Preserve SP-04D3's household/coercion/condition-lifecycle package, authenticated schema-12-to-13 compatibility, independent-remediation record, and exact-SHA hosted evidence at `93d38810a87707a7c4c98c7392e2a2f20dc030fb`.
13. Preserve SP-04E0's legal-adoptive-parent establishment, authenticated vocabulary-only schema-13-to-14 compatibility, independent-remediation record, and exact-SHA hosted evidence at `30fd0ad5f9a47eb15c0af27360ae31d72414a8ed`.
14. Preserve SP-04E1's primary-guardianship establishment, authenticated schema-14-to-15 compatibility, independent-remediation record, and exact-SHA hosted evidence at `97b607ae8df77dbd5c6fa5ab6b544000208cdb0e`.
15. Preserve SP-04E2's guardianship termination/replacement and schema-15-to-16 compatibility package with exact-SHA hosted evidence at `7491da89985fedb18e423082a2fd9187b8899e52`.
16. Preserve SP-04E3's deterministic coming of age, atomic guardianship closure, schema-16-to-17 compatibility, independent-remediation record, and exact-SHA hosted evidence at `59588be9d277dc4c4cb7ec98ef99e33591b0eeda`.
17. Preserve SP-04E4's active-only pregnancy registration, schema-17-to-18 compatibility, independent-remediation record, and exact-SHA hosted evidence at `177346b7358e84da358f3bfac8057b6ea70ed412`.
18. Preserve SP-04E5's deterministic pregnancy birth resolution, generated-child insertion, schema-18-to-19 compatibility, independent-remediation record, and exact-SHA hosted evidence at `4b28fb74bed9181ce021e1c5e32ef9d039b4e2e1`.
19. Preserve SP-04E6's primary-guardian education attainment, runtime-v3/authored-v1-v2 separation, schema-19-to-20 compatibility, independent-remediation record, and exact-SHA hosted evidence at `0928e4484ef41da5ac31f5683af5347800a92dec`.
20. Complete — SP-04F0 restricted public death is accepted at exact SHA `783ccfb61357248158cf287ee69ba27b56c38f4a` with hosted macOS arm64/Windows x64 evidence.
21. Complete — SP-04F1 career-death obligation closure is accepted at exact SHA `23045a06a39361ecf8d2ef341cc0458590322f0a` with hosted macOS arm64/Windows x64 evidence.
22. Complete — SP-04F2 custodian-death custody release is accepted at exact SHA `ab8917a95ea064911a584cd640647374745fd2c7` with hosted macOS arm64/Windows x64 evidence.
23. Complete — SP-04F3 exact household-head death handoff is accepted at exact SHA `72e5bd34f41f068c2e07a580e02522f8222eca30` with hosted macOS arm64/Windows x64 evidence, without treating the supplied replacement as a legal heir.
24. Complete — SP-04F4 explicit personal heir designation is accepted at exact SHA `ebde5387ac2d7398105f11043d9cdaeb2c2ae187` with hosted macOS arm64/Windows x64 evidence, without granting the designation legal status.
25. Complete — SP-04F5 pairwise legal-candidate eligibility is accepted at exact SHA `bb9dcffab8f517e7a2f9cb0b47072bb096fc7725` with hosted macOS arm64/Windows x64 evidence, without adding precedence, selection, claims, or a persisted law.
26. Complete — SP-04F6 caller-bounded candidate-set enumeration is accepted at exact SHA `7e96930d103081e981c0cf1a06736223e222bc07` with hosted macOS arm64/Windows x64 evidence, without adding precedence, selection, claims, mutation, or persistence.
27. Complete — SP-04F7 neutral personal succession claims are accepted at exact SHA `62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4` with hosted macOS arm64/Windows x64 evidence, without adding claim strength, precedence, selection, resolution, or downstream effects.
28. In progress — SP-04F8 neutral explicit succession support is locally verified; the implementation commit and exact-SHA hosted macOS arm64/Windows x64 evidence remain pending.

M0, M1, and SP-03 are complete. M2 remains Active and is not complete. SP-04 is Active: SP-04A through SP-04F7 have passing exact-SHA hosted macOS arm64/Windows x64 evidence at their accepted revisions, while SP-04F8 is locally verified with hosted acceptance pending. SP-05 remains blocked, and later SP-04 packages remain pending.

See the [subsystem plan index](plans/README.md) for dependency status.
