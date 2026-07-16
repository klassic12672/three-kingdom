# Prompt Package: SP-04G Observer Queries and Cross-System Integration

Use only after SP-04F11 has accepted exact-SHA hosted evidence. This package creates the knowledge-filtered application boundary and connects character events to relationship, campaign, and future battle/political consumers without implementing blocked subsystems.

## Authorization boundary

The user has authorized local implementation, commits, and ordinary non-force pushes to the approved `origin/main` for remaining SP-04 work. Verify remote, branch, tree, and divergence before each commit or push.

No force push, rebase, branch, tag, pull request, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action is authorized.

## Prompt

~~~text
/goal

Complete SP-04G — observer-filtered CharacterProfile, HouseholdView, and SuccessionView plus character-event relationship, campaign, political-handoff, and battle-handoff integration — as one larger independently verifiable package through exact-SHA hosted evidence and documentation closeout.

Keep the stable read-model boundary and the event-integration boundary together so presentation receives one coherent, knowledge-safe application surface. Do not subdivide by individual query or event source merely for convenience.

Dependency gate

- SP-04F11 must be accepted with exact-SHA hosted evidence.
- All authoritative SP-04 simulation state through player continuity must be stable.
- SP-05, SP-07, and SP-08 remain blocked. G may define or consume narrow neutral handoff contracts, but it may not implement their faction, army, or tactical systems.

Read:

1. AGENTS.md
2. docs/MASTER_PLAN.md
3. docs/ROADMAP.md
4. docs/plans/README.md
5. docs/plans/SP-04-characters-family-marriage-succession.md
6. relevant portions of SP-05, SP-07, SP-08, and SP-11 for ownership boundaries
7. applicable ADRs
8. docs/CHARACTERS.md
9. docs/SIMULATION.md
10. docs/DEVELOPMENT.md
11. docs/codex/AGENT_ROSTER.md
12. docs/codex/prompts/04-subplan-implementation.md
13. docs/codex/prompts/05-verification-closeout.md
14. Current Game.Application relationship query, authoritative character queries, geography knowledge filtering, character commands/events, and tests

Use:

- project-architect, read-only, for observer/knowledge ownership, cross-plan contract tracing, and ADR detection;
- simulation-engineer for deterministic core and application integration within assigned paths;
- verification-reviewer, read-only, after integration.

The main agent owns application/public contracts, integration seams, shared files, documentation, Git, hosted evidence, and all completion claims.

Before editing:

1. Verify accepted F11 evidence and repository synchronization.
2. Inventory every field in authoritative character, household, relationship, marriage, career, resource, estate, guardianship, pregnancy, succession, regency, inheritance, and player-continuity state.
3. Define an explicit observer/knowledge context. Unknown data must be omitted, summarized, ranged, or marked unknown; never guessed.
4. Trace ownership of BattleSetup/BattleResult and political/faction consumers. Stop for an ADR or user decision if closing SP-04 would require implementing another blocked plan.
5. Publish a field-by-field visibility matrix and event-integration matrix before implementation.

Required outcome

Implement a versioned Game.Application query and integration layer that:

1. Reserves and implements the public `CharacterProfile`, `HouseholdView`, and `SuccessionView` names as immutable observer-filtered models distinct from authoritative simulation reads.
2. Integrates or version-advances `RelationshipSummary` consistently rather than creating a second conflicting relationship query.
3. Uses an explicit observer character and available knowledge/publicity evidence; nonexistent observers and malformed contexts fail deliberately.
4. Prevents leakage of private memories, exact hidden relationship dimensions, secret or nonpublic household facts, hidden custody/health facts, private wealth/estate details, unobserved claims/support, and unavailable succession planning.
5. Exposes the controlled character's own known details and public/witnessed facts according to explicit rules.
6. Returns canonical, bounded, defensive results with stable localization keys/arguments where player-facing labels are required.
7. Produces distinct relationship/memory consequences for accepted marriage, romance, coercion, birth, adoption, guardianship, death, inheritance, succession, regency, capture, rescue, and shared-event inputs where SP-04 owns the consequence.
8. Preserves political marriage and voluntary romance as mechanically distinct, allowing legal union state and emotional relationship state to diverge.
9. Ensures coercive household actions never produce positive romance or attraction progression.
10. Defines narrow versioned character contributions for future BattleSetup and character effects accepted from future BattleResult, including commander descriptors plus wounds, death, capture, rescue, and shared memories, without building tactical simulation.
11. Defines neutral versioned political/campaign handoff facts for reputation, loyalty/allegiance, office/title, court, faction, and diplomacy consumers without mutating blocked subsystem state.
12. Applies accepted external character-result inputs only through registered deterministic commands/events with exact revalidation, causality, affected IDs, replay, and idempotence.
13. Preserves state and knowledge behavior across save/load, tier transitions, player transfer, and later-day continuation.
14. Adds bounded query and integration performance fixtures representative of 1,000 characters.

Package boundary and non-goals

- Do not build Godot character screens; X1 owns presentation.
- Do not author historical/custom content; X1 owns that vertical slice.
- Do not implement factions, courts, diplomacy, offices, titles, armies, tactical combat, AI, or full SP-11 UI.
- Do not expose omniscient authoritative state through application contracts.
- Do not create a universal loyalty score or collapse the nine relationship dimensions.
- Battle and political handoffs must be narrow contracts and tests, not placeholder outward dependencies from Simulation.Core.
- Never introduce explicit sexual content.

Required acceptance matrix

Cover at least:

- dependency, ownership, and ADR safety;
- versioned observer/knowledge context and all four public query models;
- field-by-field visibility and hidden-information negatives;
- own/public/witnessed/private knowledge behavior;
- canonical, bounded, defensive, localized results;
- marriage-versus-romance divergence;
- coercion no-positive-romance invariant;
- relationship/memory consequences for the required character lifecycle events;
- neutral political/campaign handoff contracts;
- future battle contribution/result round-trip contracts and registered mutation;
- stale/duplicate/replay/input-order behavior;
- save/load, tier transition, player transfer, and continuation preservation;
- 1,000-character query/integration performance evidence;
- focused/full gates, independent review, and exact-SHA hosted evidence.

Verification

Run focused tests while iterating, then:

./scripts/validate.sh
./scripts/test.sh Release
git diff --check
git lfs fsck

Additionally run:

- all Game.Application query tests;
- hidden-information/property matrices and defensive-copy tests;
- relationship/memory consequence and coercion regressions;
- character battle contribution/result round-trip tests;
- tier-transition, save/load, replay, player-transfer, and later-day continuation tests;
- architecture tests proving Simulation.Core has no Godot, Steam, application, faction, or tactical outward dependency;
- ten-year deterministic soak;
- local Apple Silicon query/integration performance fixtures;
- focused formatter checks.

Closeout

1. Obtain independent verification review and correct validated in-scope findings.
2. Update plan/roadmap/index and subsystem/application documentation only to demonstrated status.
3. Commit the intentional G implementation, re-fetch, and ordinarily push main.
4. Observe automatic CI without dispatch or rerun.
5. Require exact-SHA hosted macOS arm64/Windows x64 validation, complete tests, import, native export, smoke, manifests, and artifacts.
6. Authenticate run/job/SHA/artifact identities and inspect artifacts.
7. Add exact-SHA evidence, mark G accepted only, commit evidence/status docs, and ordinarily push main.

Definition of done

- Presentation and later consumers have one stable, knowledge-filtered SP-04 application surface.
- Required character lifecycle, relationship, political-handoff, and battle-handoff integration is deterministic and tested without implementing blocked plans.
- Exact-SHA hosted evidence passes on both release-target architectures and is committed.
- The tree is clean and synchronized.
- Historical/custom content, SP-04 presentation, final integrated performance, and full SP-04 acceptance remain open.
- The next package is SP-04X1; do not begin it here.
~~~
