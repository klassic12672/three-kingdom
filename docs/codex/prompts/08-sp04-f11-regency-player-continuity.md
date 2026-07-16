# Prompt Package: SP-04F11 Regency and Player-Character Continuity

Use only after SP-04F10 is accepted with exact-SHA hosted evidence. This package closes the authoritative regency hook and application-level player-character transfer boundary.

## Authorization boundary

The user has authorized local implementation, commits, and ordinary non-force pushes to the approved `origin/main` for the remaining SP-04 packages. Verify remote identity, branch, working tree, and divergence before every commit or push.

Force pushes, rebases, branches, tags, pull requests, releases, workflow dispatches/reruns, signing, Steam, deployment, publishing, credentials, and remote-setting changes remain unauthorized.

## Prompt

~~~text
/goal

Complete SP-04F11 — deterministic regency and player-character campaign continuity — as one larger independently verifiable package, through implementation, compatibility, documentation, immutable commit, ordinary origin/main push, exact-SHA hosted macOS arm64/Windows x64 evidence, and evidence closeout.

Treat regency establishment/lifecycle and player transfer/end-or-continue behavior as one vertical package because they jointly determine continuity after death or incapacity. Do not split them into micro-packages for convenience.

Dependency gate

- SP-04F10 must be accepted with exact-SHA hosted evidence.
- F9 resolution and F10 consequences must be authoritative and consumable.
- SP-05 remains blocked; F11 must not invent court, faction, office, title, or diplomacy authority.
- Presentation beyond the minimum application behavior needed to verify continuity belongs to X1.

Read:

1. AGENTS.md
2. docs/MASTER_PLAN.md
3. docs/ROADMAP.md
4. docs/plans/README.md
5. docs/plans/SP-04-characters-family-marriage-succession.md
6. applicable ADRs
7. docs/CHARACTERS.md
8. docs/SIMULATION.md
9. docs/DEVELOPMENT.md
10. docs/codex/AGENT_ROSTER.md
11. docs/codex/prompts/04-subplan-implementation.md
12. docs/codex/prompts/05-verification-closeout.md
13. Current character-condition, guardianship, coming-of-age, succession, inheritance, command/event, save, and Game.Application implementations/tests

Use:

- project-architect, read-only, to decide the simulation/application/save ownership boundary and detect ADR needs;
- simulation-engineer for deterministic simulation, application, and save work within the assigned paths;
- verification-reviewer, read-only, after integration.

The main agent owns public contracts, cross-layer ownership, schema decisions, integration, documentation, Git, evidence, and completion claims.

Before editing:

1. Verify the accepted F10 dependency and current remote/tree state.
2. Prove where player-control state belongs. Do not put UI preference into authoritative simulation or leave campaign-critical control state unsaved.
3. Define explicit scenario-rule inputs for no-successor behavior; do not hard-code one universal game-over policy.
4. Define regency eligibility and lifecycle without borrowing authority concepts from blocked SP-05.
5. State the acceptance matrix, migration effect, and non-goals before implementation.

Required outcome

Implement a deterministic continuity workflow that:

1. Establishes a bounded neutral regency when the accepted successor is a minor or is under a policy-defined qualifying incapacity.
2. Uses explicit regent eligibility, precedence, expected-current-state, and source resolution rather than inferred court/faction power.
3. Keeps regent, guardian, household head, custodian, employer, retinue leader, and legal successor as distinct roles.
4. Ends, replaces, or continues regency deterministically on coming of age, recovery, death, captivity/custody changes, displacement, or a new accepted succession.
5. Prevents cycles, self-regency, stale replacement, duplicate active regencies, and unbounded history.
6. Records neutral regency authority/handoff facts for future political consumers without implementing those consumers.
7. Adds a versioned Game.Application player-campaign/control state whose active controlled character is explicit and persisted through the appropriate save/application contract.
8. Transfers control exactly once when the controlled character dies and an accepted valid successor exists.
9. Defines behavior when the successor is a minor, incapacitated, captive, or otherwise constrained without silently transferring control to the regent.
10. Applies an explicit scenario rule when no valid successor exists: terminal campaign outcome or a defined continuation choice. The same inputs must yield the same result.
11. Handles simultaneous deaths, dead successors, disputed/unresolved successions, stale choices, save/load during pending transfer, replay, and repeated processing deterministically.
12. Preserves campaign date, seed, commands, world identity, and unrelated state across player transfer.
13. Exposes defensive application queries for current player character, continuity status/options, and active regency without hidden mutable references.
14. Provides exact command/event or application-transition causality, stable IDs where persisted, checksums where authoritative, save compatibility, recovery, and later-day continuation.

Package boundary and non-goals

- Do not build the final character/succession screens; X1 owns presentation.
- Do not implement court appointment, faction leadership, office/title powers, diplomacy, AI regent behavior, economy, or battle systems.
- Do not equate player control with ownership of a faction or household.
- Do not transfer control to an arbitrary character when the explicit scenario rule does not allow it.
- Do not weaken adult-romance consent rules for a minor player successor or regent.
- Never introduce explicit sexual content.

Required acceptance matrix

Cover at least:

- dependency/ADR and simulation/application ownership safety;
- exact regency policy/state/lifecycle/action/outcome/query contracts;
- minor/incapacity establishment and explicit regent eligibility;
- coming-of-age/recovery/death/custody/replacement/end behavior;
- role separation and cycle/stale/duplicate/capacity behavior;
- player-control state and save ownership;
- valid-successor transfer exactly once;
- constrained-successor behavior distinct from regent control;
- no-successor terminal/choice scenario rules;
- simultaneous death, disputed result, stale choice, replay, and pending-save cases;
- defensive queries, stable causality, checksums, migration, recovery, and continuation;
- representative continuity/regency workload and raw local performance;
- focused/full gates, independent review, and exact-SHA hosted evidence.

Verification

Run focused tests during implementation, then:

./scripts/validate.sh
./scripts/test.sh Release
git diff --check
git lfs fsck

Also run:

- succession/inheritance/regency/condition/coming-of-age/guardianship focused suites;
- Game.Application player-continuity and save/load suites;
- simultaneous-death, pending-transfer, replay, recovery, and scenario-rule matrices;
- historical raw-shape, frozen-fixture, migration, checksum, and complete forward-chain tests if persistence changes;
- ten-year deterministic soak;
- representative local Apple Silicon performance measurement;
- focused formatter and architecture-boundary checks.

Closeout

1. Obtain verification-reviewer acceptance and resolve validated in-scope issues.
2. Update SP-04, ROADMAP, index, CHARACTERS, SIMULATION, and application/save docs to the demonstrated status.
3. Commit the intentional implementation package, re-fetch, and ordinarily push main.
4. Observe automatically triggered CI only.
5. Require exact implementation-SHA hosted macOS arm64/Windows x64 validation, tests, import, native export, smoke, manifests, and artifact upload.
6. Authenticate and inspect all run/job/artifact identities.
7. Add exact-SHA evidence, mark F11 accepted only, commit evidence/status docs, and ordinarily push main.

Definition of done

- Regency lifecycle and player-character continuity are deterministic, persisted, and independently verified.
- A valid successor preserves campaign continuity; no-successor behavior follows explicit scenario rules.
- The implementation SHA has passing exact-SHA hosted macOS arm64/Windows x64 and artifact evidence.
- Evidence closeout is committed and pushed; the tree is clean and synchronized.
- Observer-filtered queries/integration, content/UI, final performance, and SP-04 closeout remain open.
- The next package is SP-04G; do not start it here.
~~~
