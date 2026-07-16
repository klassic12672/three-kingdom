# Prompt Package: SP-04F9 Succession Law and Deterministic Resolution

Use after SP-04F8 is accepted with exact-SHA hosted evidence. This is the first of six larger remaining SP-04 packages. It owns succession law, precedence, selection, missing-heir behavior, and neutral disputed outcomes, but not inheritance or player transfer.

## Authorization boundary

The user has authorized local implementation, commits, and ordinary non-force pushes to the existing approved `origin/main` for the remaining SP-04 packages. Before every commit or push, verify the remote, branch, working tree, and absence of unexpected divergence.

This does not authorize force pushes, rebases, new branches, tags, pull requests, releases, workflow dispatches or reruns, signing, notarization, Authenticode, Steam, deployment, publishing, credential changes, or remote-setting changes. Automatic CI caused by an authorized push may be observed and recorded.

## Prompt

~~~text
/goal

Complete SP-04F9 — succession law, precedence, deterministic resolution, and neutral disputed succession — as one larger independently verifiable package, including implementation, compatibility, documentation, an immutable implementation commit, ordinary push to origin/main, exact-SHA hosted macOS arm64/Windows x64 evidence, and evidence closeout.

Do not split this back into designation, law, ranking, selection, fallback, and dispute micro-packages merely for convenience. If architecture review proves the boundary cannot remain atomic without violating a locked decision or ownership boundary, stop before editing and explain the minimum necessary split.

Dependency gate

- Active milestone: M2 — 191 campaign slice.
- Target plan: docs/plans/SP-04-characters-family-marriage-succession.md.
- SP-04F8 must be accepted at exact implementation SHA c946c8739d29e9f484bc921223e47cb5f24e38ab with its hosted evidence and evidence closeout present on origin/main.
- SP-01 and SP-02 must remain complete.
- SP-05 remains blocked and must not become a dependency of this package.

Read in source-of-truth order:

1. AGENTS.md
2. docs/MASTER_PLAN.md
3. docs/ROADMAP.md
4. docs/plans/README.md
5. docs/plans/SP-04-characters-family-marriage-succession.md
6. docs/adr/README.md and applicable ADRs
7. docs/CHARACTERS.md
8. docs/SIMULATION.md
9. docs/DEVELOPMENT.md
10. docs/codex/AGENT_ROSTER.md
11. docs/codex/prompts/04-subplan-implementation.md
12. docs/codex/prompts/05-verification-closeout.md
13. Current succession, character, marriage, family, household, command/event, save, checksum, migration, and test implementations

Use bounded project subagents:

- project-architect, read-only, to confirm the dependency gate, define the law/resolution contract, trace save consequences, and identify ADR triggers;
- simulation-engineer for the assigned deterministic domain/application/save implementation;
- verification-reviewer, read-only, after integration.

The main agent owns package scope, shared/public contracts, schema decisions, integration, plan/roadmap documentation, final tests, Git decisions, hosted evidence, and completion claims. At most one writer owns each file or public contract.

Before editing:

1. Verify HEAD, origin/main, the F8 evidence, and a clean or safely separable tree.
2. State the package boundary, non-goals, affected contracts, expected schema change, and acceptance matrix.
3. Audit F4 designation, F5/F6 eligibility, F7 claims, F8 support, marriage/kinship/adoption state, character conditions, and current death workflows.
4. Decide the smallest explicit versioned representation of succession law or policy that can support deterministic resolution. Do not silently turn F5's transient query rule into persisted law.
5. Stop for an ADR if the required model contradicts a locked product, architecture, save, content, or milestone decision.

Required outcome

Implement one deterministic succession-resolution workflow that:

1. Uses an explicit, versioned law/policy input or persisted policy with a clearly identified owner and source.
2. Supports the accepted biological, legal-adoptive, legacy-unknown, and current-designation bases without conflating them.
3. Defines spouse and collateral participation, descendant depth, age, incapacity, custody, legitimacy/recognition inputs if present, and missing-heir/extinct-family behavior explicitly rather than through hidden defaults.
4. Consumes F7 claims and F8 support as neutral evidence under explicit ranking rules; neither automatically overrides legal eligibility or designation.
5. Produces a canonical ordered candidate assessment with every contributing basis, issue, claim, and support fact retained.
6. Selects exactly one successor, records a no-successor result, or records a bounded neutral disputed result according to the declared rules.
7. Uses deterministic precedence and final tie-breaking. Do not use wall-clock time, collection order, runtime hash order, locale behavior, or untracked randomness.
8. Supports an exact death or qualifying-incapacity trigger without silently treating every temporary condition as permanent succession.
9. Revalidates subject condition, policy, candidate state, designation, claims, support, and expected prior resolution at commit time.
10. Resolves duplicate, stale, simultaneous, replayed, and reordered work deterministically with no partial mutation.
11. Retains bounded recent resolution/dispute evidence and checked folded history with canonical defensive queries.
12. Adds stable namespaced IDs, explicit command/event discriminators, exact affected IDs, checksum coverage, save/load, restore, replay, and later-day continuation.
13. Advances the save schema and succession snapshot/system/query versions only as required, with an authenticated exact-F8 schema-27 fixture and forward-only migration.
14. Rejects F9 vocabulary and shapes from schema 27, including explicit null, and validates the complete current raw shape.

Package boundary and non-goals

- Do not transfer wealth, estates, household leadership, retinues, offices, titles, faction positions, or other inheritance in F9.
- Do not implement regency, player-character transfer, observer-filtered application queries, UI, AI, historical content, or battle integration.
- Do not create SP-05 factions, courts, offices, titles, diplomacy, legitimacy systems, or political strength.
- A disputed result is neutral retained succession evidence, not a completed civil-war system.
- Do not infer a universal historical law. Rules must be explicit, versioned, sourceable by later content, and testable.
- Never introduce explicit sexual content.

Required acceptance matrix

Define and satisfy package criteria covering at least:

- dependency/ADR safety;
- exact versioned policy, candidate-assessment, resolution, dispute, action, outcome, event, and query contracts;
- spouse/collateral/descendant/adoptive/designation participation;
- explicit claims/support treatment and multi-basis overlap;
- death, incapacity, custody, minority, missing-heir, and extinct-family matrices;
- canonical precedence and deterministic tie-breaking;
- selected, disputed, and no-successor outcomes;
- duplicate/stale/simultaneous/replay/input-order behavior;
- bounded retention, overflow, folding, and defensive queries;
- checksums, saves, diagnostics, exact replanning, and continuation;
- historical schema rejection, exact-F8 fixture authentication, migration, and current-schema raw validation;
- a representative 1,000-character succession-resolution performance fixture without a brittle hosted wall-clock assertion;
- focused/full repository gates and independent review;
- exact-SHA hosted macOS arm64/Windows x64 evidence.

Verification

Use narrow tests while iterating, then run at minimum:

./scripts/validate.sh
./scripts/test.sh Release
git diff --check
git lfs fsck

Also run:

- the complete succession-focused Release slice;
- policy/precedence/selection/dispute matrices;
- simultaneous-death/incapacity and stale-resolution regressions;
- save raw-shape, frozen-fixture, migration, recovery, checksum, replay, and continuation tests;
- the ten-year deterministic soak;
- the representative local Apple Silicon performance fixture;
- focused formatter checks for changed C# files.

Closeout

1. Have verification-reviewer audit the integrated diff and matrix; fix validated in-scope findings.
2. Update SP-04, ROADMAP, plan index, CHARACTERS, SIMULATION, and compatibility documentation only to the strongest local status demonstrated.
3. Commit the intentional implementation package with no unrelated changes.
4. Re-fetch and ordinarily push main to the approved origin.
5. Observe the automatically triggered exact-SHA CI. Do not dispatch or rerun workflows.
6. Require successful clean-checkout macOS arm64 and Windows x64 validation, complete tests, import, native development export, automated smoke, manifests, and artifact upload for the exact implementation SHA.
7. Authenticate run/job/SHA identities and inspect artifact names, digests, sizes, manifests, architectures, and representative files.
8. Add an exact-SHA evidence report, mark only F9 accepted, commit the evidence/status documentation, and ordinarily push main.

Definition of done

- F9's complete law/precedence/resolution/dispute boundary is implemented and independently verified.
- The implementation SHA has passing exact-SHA hosted macOS arm64/Windows x64 and artifact evidence.
- Evidence and status documentation are committed and pushed to origin/main.
- The tree is clean and synchronized.
- Inheritance, regency, player continuity, observer queries/integration, content/UI, final performance, and full SP-04 acceptance remain explicitly open.
- The next package is SP-04F10; do not begin it in this task.
~~~
