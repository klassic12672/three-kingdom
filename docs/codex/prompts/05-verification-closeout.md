# Prompt Package: Verification and Closeout

Run this in a separate task after implementation and before changing plan status, committing, or merging.

## Prompt

~~~text
Review the completed package against its approved contract and target SP acceptance criteria.
Do not edit files.

Read:
- AGENTS.md;
- the package contract and implementation summary;
- the target plan and applicable ADRs;
- the complete diff or working tree;
- focused and full test results;
- supplied visual, performance, hosted, platform, artifact, and manual evidence.

Spawn verification-reviewer for the acceptance/evidence audit.
Spawn project-architect only if the diff may have changed a public contract,
architecture, product scope, dependency gate, or milestone rule.
Wait for all requested results and consolidate them.

Review for:
1. correctness and regressions;
2. deterministic and save/content compatibility;
3. architecture and scope violations;
4. missing, weak, or wrong-revision tests/evidence;
5. stale or overstated documentation;
6. secrets, machine-local paths, generated output, and LFS problems;
7. unchecked dependencies or ADR requirements;
8. unrelated edits.

Report findings first by severity with file references.
Then provide:
- package contract items: pass/fail/unverified;
- target acceptance criteria: local/hosted/cross-platform/visual/performance/manual/external;
- exact commands and artifact identities reviewed;
- documentation/status changes that are justified;
- documentation/status changes that are not yet justified;
- smallest corrective action.

If there are no findings, say so explicitly, but still list residual external and manual gates.
Do not stage, commit, push, merge, or mark a milestone complete.
~~~
