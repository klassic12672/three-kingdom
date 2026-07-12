# Prompt Package: Plan One Work Package

Use this in Plan mode before implementing a subplan or audit finding.

## Prompt

~~~text
/plan

Design the next independently verifiable work package for:
<TARGET SP PLAN, MILESTONE GATE, OR AUDIT FINDING>

Read:
- AGENTS.md
- docs/MASTER_PLAN.md
- docs/ROADMAP.md
- docs/plans/README.md
- the target SP plan
- applicable ADRs and subsystem guides

Do not edit files and do not perform external actions.

First establish:
1. the active milestone and target plan status;
2. whether all dependencies are actually verified;
3. contradictions between plans, implementation, tests, and evidence;
4. whether an ADR or user decision is required.

Return one package contract with:
- outcome;
- why this is the next safe package;
- non-goals;
- allowed write paths and shared files owned only by the main agent;
- public contracts and compatibility concerns;
- implementation sequence;
- recommended project subagents and exact bounded assignments;
- automated tests;
- manual, visual, performance, hosted, or platform evidence;
- documentation updates;
- external actions requiring user authorization;
- stop conditions;
- exact definition of done.

Keep the package reviewable. Split it if two writers would need the same contract or if unavailable external evidence is mixed with local implementation.
~~~

## Expected result

Approve or adjust the returned contract, then start a separate task with the generic implementation package or the relevant targeted package.
