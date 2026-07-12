# Prompt Package: Implement the Next Subplan Slice

Use for any unblocked SP plan after the package planner has produced an approved package contract.

## Prompt

~~~text
/goal

Implement this approved work package:
<PASTE THE PACKAGE CONTRACT>

Target plan:
<SP FILE>

Read AGENTS.md, the master plan, roadmap, subplan index, target plan,
applicable ADRs, and only the subsystem guides/contracts needed for the package.

Before editing:
1. confirm the active milestone and dependencies;
2. compare the package contract with the current tree;
3. identify any user decision or ADR trigger;
4. assign one write owner per path and public contract.

Use the project agents named in the approved package. Prefer read-only parallel
exploration, research, tests, and review. Sequence dependency-sensitive contract
work before parallel layer implementation. The main agent owns shared contracts,
integration, documentation status, and final verification.

Implementation rules:
- stay inside outcome and non-goals;
- preserve compatibility, determinism, localization, provenance, and architecture invariants;
- add focused tests with behavior;
- update docs when behavior, schema, commands, or evidence changes;
- do not fix unrelated findings unless they block the package;
- do not perform Git, hosted, credentialed, publishing, or release actions without explicit authorization.

Verification:
- run focused tests while iterating;
- run ./scripts/validate.sh and ./scripts/test.sh Release before completion;
- run every additional visual, import, export, soak, content, performance, save,
  platform, or manual check named in the package contract;
- classify missing external evidence as unavailable, not passed.

Finish with:
- outcome delivered;
- changed files by layer;
- tests and evidence with exact commands;
- compatibility and performance notes;
- verified and unverified acceptance criteria;
- residual risks and next smallest package.
~~~
