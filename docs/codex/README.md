# Codex Development Kit

This directory contains reusable prompts and agent routing for executing the approved game plans. It does not replace the master plan, roadmap, SP plans, ADRs, tests, or platform evidence.

## Files

- [Agent roster](AGENT_ROSTER.md): main-agent responsibilities, custom subagents, routing, and concurrency rules.
- [Subplan catalog](SUBPLAN_CATALOG.md): dependency-aware routing from SP-00 through SP-15.
- [Package planner](prompts/00-package-planner.md): turn a plan or audit finding into one bounded work package.
- [M0 auditable baseline](prompts/01-m0-auditable-baseline.md): establish repository provenance and reconcile the current audit.
- [M0 external evidence](prompts/02-m0-external-evidence.md): collect same-SHA hosted macOS/Windows CI, automated-smoke, manifest, and artifact evidence.
- [SP-03 presentation correction](prompts/03-sp03-presentation-correction.md): implement and verify the geography presentation gaps.
- [Generic subplan implementation](prompts/04-subplan-implementation.md): execute the next package from any unblocked SP plan.
- [Verification and closeout](prompts/05-verification-closeout.md): independently review a package before status changes.

## Operating sequence

1. Read repository [AGENTS guidance](../../AGENTS.md).
2. Use the package planner in Plan mode for a target plan or audit finding.
3. Review the returned package boundary and any required user decisions.
4. Start a new task or worktree for the package. Worktrees are appropriate only after a baseline commit exists.
5. Run the selected goal prompt. Ask explicitly for the bounded subagents named by the prompt.
6. Run the verification-and-closeout prompt in a separate read-only review task.
7. Merge or commit only after the user approves the scope and evidence.
8. Update plan status only to the strongest state actually demonstrated.

## Current order

M0, M1, and SP-03 are complete. SP-04A is locally verified, and exact revision `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea` has passing [hosted macOS arm64/Windows x64 evidence](../evidence/SP-04A-EXACT-SHA-eaa3aaf.md). Full SP-04 remains Active. The current safe order is:

1. Preserve the accepted SP-04A character/family/household foundation boundary, its local working-tree evidence, and its exact-SHA hosted evidence.
2. Select the next independently verifiable SP-04 package only under separate authorization; do not infer full SP-04 completion from SP-04A.
3. Preserve the completed SP-03 baseline and its deferred pre–Early Access refinement boundary.
4. Continue later M2 work according to the active roadmap and dependency gates.
5. Before M4/public demo, collect physical Windows evidence.
6. Before public promotion, complete SP-15 signing/notarization and Steam evidence.

Do not launch the full roadmap as one goal. Each task should produce one reviewable outcome with one clear completion test.
