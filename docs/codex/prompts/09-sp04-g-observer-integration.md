# Prompt Package: SP-04G Observer Queries and Required Integration

Use after the consolidated SP-04F9 package is accepted. This package implements the knowledge-filtered application surface and only the integration explicitly required by SP-04.

## Authorization boundary

The user has authorized local SP-04 implementation, commits, and ordinary non-force pushes to the approved `origin/main`. Verify remote, branch, tree, and divergence before each commit or push.

No force push, rebase, branch, tag, pull request, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action is authorized.

## Prompt

~~~text
/goal

Complete SP-04G — observer-filtered character/relationship/household/succession queries and the minimum required character-event integration — with exact-SHA hosted closeout.

Ponytail routing

- Invoke `ponytail:ponytail` in full mode before designing query DTOs or integration contracts.
- Invoke `ponytail:ponytail-review` after integration if available.
- Reuse `RelationshipSummaryQuery`, authoritative character queries, existing
  commands/events, and current BattleSetup/BattleResult contracts before adding
  anything new.
- Do not create placeholder political, faction, court, diplomacy, or battle
  subsystems for future consumers.

Dependency gate

- The consolidated F9 succession-completion package must be accepted.
- SP-05, SP-07, and SP-08 remain blocked.

Read AGENTS.md, source-of-truth plans, applicable ADRs, CHARACTERS, SIMULATION,
the relevant boundaries of SP-05/SP-07/SP-08/SP-11, the agent roster, and the
current application queries and character-event code/tests.

Use:

- project-architect, read-only, for knowledge and cross-plan ownership;
- simulation-engineer for assigned core/application work;
- verification-reviewer, read-only, after integration.

Before editing

1. Define a whitelist of fields needed by the SP-04 UI and acceptance tests.
   Do not mirror every authoritative field automatically.
2. Reuse the existing observer/publicity model from relationship and geography
   queries. Add a broader knowledge abstraction only if current rules cannot
   express a required visibility case.
3. Identify relationship consequences that SP-04 explicitly requires and are
   currently missing. Do not add a consequence merely because an event exists.
4. For battle integration, prefer a small mapper/adapter and one round-trip test
   over new persisted handoff state.

Required outcome

1. Implement immutable, observer-filtered `CharacterProfile`, `RelationshipSummary`,
   `HouseholdView`, and `SuccessionView` application models.
2. Expose only the controlled character's known details and public/witnessed
   facts; hide private memories, exact hidden dimensions, private wealth/estate,
   and unobserved succession planning.
3. Return canonical bounded defensive results and localization keys/arguments
   only where presentation needs them.
4. Preserve legal marriage and emotional romance as distinct.
5. Add only missing required relationship/memory consequences, including the
   coercion invariant: no positive romance or attraction.
6. Map character state into the existing battle boundary and accept the required
   wound/death/capture/rescue/shared-memory result through existing registered
   mutation patterns. Do not build tactical behavior.
7. If current BattleSetup/BattleResult contracts cannot carry the SP-04 fields,
   make the smallest additive change owned by the proper layer.
8. Do not persist neutral “future consumer” facts for faction, court, office,
   title, reputation, or diplomacy systems. Their owning plans consume existing
   character events later.
9. Preserve behavior across save/load, tier transition, player transfer, replay,
   and later-day continuation.

Non-goals

- No Godot screens, historical content, full political integration, armies,
  tactical simulation, AI, or generic knowledge framework.
- No universal loyalty score or duplicated relationship query stack.
- No package-specific ten-year soak or 1,000-character fixture unless the full
  suite lacks relevant coverage. Final integrated performance belongs to X2.

Required tests

- Field whitelist and hidden-information negatives.
- Self/public/witnessed/private visibility.
- Defensive bounded query results.
- Marriage/romance divergence and coercion regression.
- Only the required lifecycle relationship consequences.
- Minimal battle adapter/result round-trip.
- Save/load, tier transition, player transfer, replay, and architecture boundaries.

Verification and closeout

1. Run focused tests, then:

   ./scripts/validate.sh
   ./scripts/test.sh Release
   git diff --check
   git lfs fsck

2. Obtain independent review and update documentation honestly.
3. Commit, re-fetch, and ordinarily push `main`.
4. Observe automatic exact-SHA CI only.
5. Record exact SHA, jobs, manifests, artifact names, sizes, and API digests.
   Download full artifacts only for an identity inconsistency or packaging change.
6. Add evidence, mark G accepted, commit the status update, and ordinarily push.

Definition of done

- One small knowledge-filtered application surface supports SP-04 presentation.
- Required relationship and battle-boundary integration works without speculative
  downstream systems.
- Exact-SHA hosted evidence passes and the tree is clean.
- Next package: SP-04X1. Do not begin it here.
~~~
