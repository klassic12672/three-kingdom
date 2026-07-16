# Prompt Package: SP-04F9 Succession Completion

Use after SP-04F8 is accepted. This replaces the former F9, F10, and F11 prompts with one vertical package: successor resolution, inheritance, the minimum regency hook, and player-character continuity.

## Authorization boundary

The user has authorized local SP-04 implementation, commits, and ordinary non-force pushes to the approved `origin/main`. Verify the remote, branch, tree, and divergence before each commit or push.

No force push, rebase, branch, tag, pull request, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action is authorized.

## Prompt

~~~text
/goal

Complete SP-04F9 — deterministic succession resolution, inheritance, minimal regency hooks, and player-character continuity — as one vertical package with one necessary save-schema migration and one exact-SHA hosted closeout.

Ponytail routing

- Invoke the Ponytail plugin's `ponytail:ponytail` skill in full mode before fixing the package boundary and before adding any new abstraction, persisted collection, public contract, or test fixture.
- Invoke `ponytail:ponytail-review` after integration if that skill is available.
- If a Ponytail skill is unavailable, apply its rules directly: reuse existing contracts, avoid speculative future-consumer state, prefer one workflow and one migration, and add only tests that prove required behavior.
- Do not split this package into F10/F11 or other micro-packages unless an accepted ADR or unavoidable ownership conflict makes one atomic implementation impossible.

Dependency gate

- Active milestone: M2.
- Target: docs/plans/SP-04-characters-family-marriage-succession.md.
- F8 must be accepted at exact SHA `c946c8739d29e9f484bc921223e47cb5f24e38ab`.
- SP-05 remains blocked and must not become a dependency.

Read AGENTS.md, MASTER_PLAN, ROADMAP, the plan index, SP-04, applicable ADRs,
CHARACTERS, SIMULATION, DEVELOPMENT, the agent roster, the generic implementation
and closeout prompts, and current succession/death/resource/estate/household/
career/guardianship/application/save code and tests.

Use:

- project-architect, read-only, for dependency, ownership, atomicity, and ADR review;
- simulation-engineer for the assigned deterministic implementation;
- verification-reviewer, read-only, after integration.

The main agent owns shared contracts, schema decisions, integration, documentation,
Git, hosted evidence, and completion claims. One writer owns each shared file.

Before editing

1. Verify HEAD/origin and preserve unrelated work.
2. Trace F4 designation, F5/F6 eligibility, F7 claims, F8 support, kinship,
   marriage, character conditions, wealth, estates, household, career/retinue,
   guardianship, and current player/application state.
3. State the smallest rule input and one end-to-end workflow needed to satisfy
   SP-04. Do not persist general-purpose law configuration unless more than one
   current rule set actually needs it.
4. Prefer extending the existing succession subsystem and death workflow over
   creating new single-use subsystems or interface layers.
5. Stop for an ADR only if a locked decision truly changes.

Required outcome

Implement the shortest correct path that:

1. Evaluates current legal candidates from explicit rule input, designation,
   kinship/adoption, claims, and support without conflating those facts.
2. Deterministically selects one successor, records a bounded disputed result,
   or records no successor, including spouse/collateral, missing-heir,
   simultaneous-death, and extinct-family cases required by SP-04.
3. Uses canonical precedence and a deterministic final tie-break.
4. Revalidates the complete expected state and commits through registered
   commands/events with no partial mutation.
5. Consumes one exact resolution once and conserves personal wealth while
   transferring opaque estate ownership using existing resource/estate patterns.
6. Applies only necessary current household and retinue/service consequences.
   Personal service is not silently hereditary.
7. Does not create persisted office/title/faction/court “handoff” queues.
   Existing succession/consequence events are enough until their owning plans exist.
8. Records only the minimum regency evidence needed when a successor is a minor
   or policy-defined incapacitated character. Full political regent authority,
   appointment, replacement politics, and AI belong to SP-05.
9. Transfers player control exactly once to a valid accepted successor while
   preserving the same campaign. An explicit scenario rule handles no-successor
   terminal or continuation behavior.
10. Keeps successor, regent, guardian, custodian, household head, employer,
    retinue leader, and controlled character distinct.
11. Handles stale, duplicate, replayed, reordered, simultaneous, and save/load
    cases deterministically.
12. Adds only necessary canonical defensive queries and bounded history.
13. Covers checksum, save/load, recovery, replay, later-day continuation, and one
    authenticated F8-to-current forward migration if persisted state changes.

Non-goals

- No factions, court, offices, titles, diplomacy, political economy, AI, battle
  simulation, observer UI, historical content, or presentation.
- No speculative generic law engine, workflow framework, event bus, policy
  registry, or future-consumer persistence.
- No separate performance fixture for each internal step.
- Never introduce explicit sexual content.

Required tests

- Candidate/rule, precedence, selected/disputed/no-successor matrices.
- Claims/support overlap and spouse/collateral/missing-heir behavior.
- Wealth conservation, estate ownership, household/retinue consequences.
- Minor/incapacitated successor regency-hook behavior.
- Player transfer and explicit no-successor scenario behavior.
- Simultaneous death, stale/duplicate/replay, atomicity, save/recovery/migration,
  checksum, and continuation.
- One representative integrated succession workload may record raw local timing;
  the final threshold belongs to X2.

Verification

Run focused tests while iterating, then:

./scripts/validate.sh
./scripts/test.sh Release
git diff --check
git lfs fsck

The full test script already runs the repository soak; do not add a separate
duplicate soak invocation unless the changed path is not covered.

Closeout

1. Obtain independent review and fix validated in-scope findings.
2. Update documentation to the strongest demonstrated status.
3. Commit the implementation, re-fetch, and ordinarily push `main`.
4. Observe automatic CI; do not dispatch or rerun it.
5. Require exact-SHA macOS arm64 and Windows x64 CI success, including tests,
   import, native export, smoke, manifests, and artifact upload.
6. For this simulation package, record run/job/SHA, manifest, artifact name,
   size, and API digest. Download full artifacts only if CI identity is
   inconsistent or the package changes packaging/export behavior.
7. Add exact-SHA evidence, mark F9 accepted, commit the evidence/status update,
   and ordinarily push `main`.

Definition of done

- Succession resolution, inheritance, minimal regency evidence, and player
  continuity work together through one deterministic path.
- Required compatibility and exact-SHA hosted evidence pass.
- The tree is clean and synchronized.
- Observer queries/integration, bounded content/UI, integrated performance, and
  final SP-04 closeout remain open.
- Next package: SP-04G. Do not begin it here.
~~~
