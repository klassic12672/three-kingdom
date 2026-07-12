# Codex Development Kit

This directory contains reusable prompts and agent routing for executing the approved game plans. It does not replace the master plan, roadmap, SP plans, ADRs, tests, or platform evidence.

## Files

- [Agent roster](AGENT_ROSTER.md): main-agent responsibilities, custom subagents, routing, and concurrency rules.
- [Subplan catalog](SUBPLAN_CATALOG.md): dependency-aware routing from SP-00 through SP-15.
- [Package planner](prompts/00-package-planner.md): turn a plan or audit finding into one bounded work package.
- [M0 auditable baseline](prompts/01-m0-auditable-baseline.md): establish repository provenance and reconcile the current audit.
- [M0 external evidence](prompts/02-m0-external-evidence.md): collect same-SHA CI, native smoke, and signing evidence.
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

The audit makes this the safe order:

1. M0 auditable baseline.
2. M0 unsigned same-SHA hosted evidence.
3. Remaining M0 native smoke and signing evidence.
4. SP-01 and SP-02 same-SHA cross-platform evidence.
5. SP-03 presentation correction and interactive evidence.
6. Only then select later work according to the active roadmap and dependency gates.

Do not launch the full roadmap as one goal. Each task should produce one reviewable outcome with one clear completion test.
