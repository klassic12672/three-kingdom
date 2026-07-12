# Agent and Subagent Roster

## Main agent

Use the normal Codex task as the integration coordinator. It owns:

- interpreting the user request and active milestone;
- choosing one package;
- resolving architecture and product decisions;
- assigning non-overlapping work;
- integrating all edits;
- running final verification;
- asking before external actions; and
- making the final completion claim.

Do not delegate final integration, plan status changes, or milestone closure.

## Project-scoped subagents

Codex loads the executable definitions from ../../.codex/agents/.

| Agent | Mode | Use for | Do not use for |
|---|---|---|---|
| project-architect | Read-only | dependency analysis, package design, ADR detection, contract tracing | implementation |
| simulation-engineer | Write within assigned scope | deterministic simulation, saves, campaign systems, battles, AI | Godot presentation or release operations |
| content-engineer | Write within assigned scope | schemas, localization, mods, scenarios, historical content, provenance | unsourced content or runtime-code mods |
| godot-presentation-engineer | Write within assigned scope | scenes, UI, maps, tactical presentation, accessibility, visual checks | authoritative domain rules |
| build-release-engineer | Write within assigned scope | Git/LFS, CI, manifests, exports, signing workflows, Steam boundary | unsanctioned external actions or credentials |
| verification-reviewer | Read-only | independent acceptance, diff, test, evidence, and documentation review | implementation or status editing |

Built-in explorer and worker agents remain useful for short generic tasks. Prefer the project roles when repository-specific invariants matter.

## Routing by work

| Work | Lead subagent | Supporting subagent |
|---|---|---|
| Plan or ADR boundary | project-architect | verification-reviewer |
| Simulation, saves, campaign rules, tactical state, AI | simulation-engineer | verification-reviewer |
| Data, localization, scenarios, history, mods | content-engineer | verification-reviewer |
| Godot map, UI, tactical visuals, accessibility | godot-presentation-engineer | verification-reviewer |
| Repository, CI, packaging, platform, release | build-release-engineer | verification-reviewer |
| Cross-layer vertical slice | main agent | one specialist per non-overlapping layer |

## Concurrency rules

- Use at most three subagents plus the main agent at one time.
- Prefer parallel read-only exploration, tests, research, and review.
- Give each writing agent explicit file or directory ownership.
- Do not assign two writers to the same public contracts, schemas, roadmap, solution files, Godot project files, or save format.
- When one package spans simulation, content, and presentation, define contracts sequentially before parallel layer implementation.
- Wait for all requested subagents, reconcile their findings, and verify the integrated tree.

## Reusable delegation instruction

~~~text
Use bounded subagents for this package.

1. Spawn project-architect to confirm dependency gates, contracts, scope, and ADR triggers. Read-only.
2. Spawn only the specialist agents needed for non-overlapping assigned scopes.
3. Spawn verification-reviewer after integration. Read-only.

Wait for each required result before making the next dependency-sensitive edit.
The main agent owns shared contracts, integration, final tests, documentation status, and all external-action decisions.
Return distilled findings rather than raw logs.
~~~
