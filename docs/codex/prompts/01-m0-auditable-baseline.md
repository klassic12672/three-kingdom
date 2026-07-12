# Prompt Package: M0 Auditable Baseline

This package addresses the current lack of HEAD, tracked files, origin, and same-revision evidence. It deliberately separates local reconciliation from external repository creation and hosted CI.

## Authorization boundary

Running this prompt authorizes local analysis and file edits only. It does not authorize creating a remote repository, choosing a remote URL, committing, pushing, changing hosted settings, or using credentials unless the user explicitly adds that authorization.

## Prompt

~~~text
/goal

Prepare the repository for M0-A: an auditable baseline and unsigned same-SHA CI.

Read AGENTS.md, the Codex M0 audit supplied in this task, docs/MASTER_PLAN.md,
docs/ROADMAP.md, docs/plans/README.md, SP-00 through SP-03, docs/SIMULATION.md,
docs/CONTENT_PIPELINE.md, docs/RELEASE.md, and the relevant implementation/tests.

Use these subagents:
- project-architect: read-only reconciliation and ADR check;
- build-release-engineer: repository/build/manifest scope;
- verification-reviewer: final read-only evidence review.
Wait for the architecture result before editing. The main agent owns roadmap and shared documentation edits.

Required local outcomes:
1. Reconcile the documented SP-03 status with the active milestone without erasing truthful local implementation history.
2. Uncheck or qualify SP-03 visual criteria that lack rendering and interaction evidence.
3. Update save documentation to the implemented schema and migration chain.
4. Resolve the build-manifest checksum naming/contract conflict with tests and documentation. Stop for an ADR if the public contract must change.
5. Audit all untracked files, ignored artifacts, LFS coverage, secret patterns, machine-local paths, and generated outputs before any baseline commit.
6. Define how validation behaves when HEAD is absent. Do not create a circular flow that prevents establishing the first safe commit.
7. Run local validation, Release tests, Godot import, applicable exports, LFS checks available before HEAD, and focused manifest tests.
8. Produce a baseline readiness report listing files intended for the first commit, excluded files, test results, known external gates, and any user decisions.

Do not:
- invent or configure origin;
- commit or push without explicit user authorization;
- claim clean-checkout, hosted, native-Windows, signed, notarized, or same-SHA evidence;
- mark M0, SP-01, SP-02, or SP-03 complete;
- add downstream gameplay.

If the user explicitly supplies remote/commit/push authorization in this task, create the smallest reviewable baseline commit, push exactly that SHA, and report the SHA and remote. Otherwise stop at baseline readiness.

Definition of done:
- local contradictions in scope are corrected or explicitly blocked by a decision;
- the intended baseline is clean of secrets, generated outputs, and machine-local paths;
- local checks are green or failures are precisely explained;
- no external evidence is fabricated;
- the next authorized Git/hosted action is explicit.
~~~
