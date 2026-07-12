# Three Kingdoms Codex Guidance

## Source of truth

Read only the material needed for the task, in this order:

1. docs/MASTER_PLAN.md for locked product and architecture decisions.
2. docs/ROADMAP.md for the active milestone and exit gates.
3. docs/plans/README.md and the target SP plan for dependencies and acceptance criteria.
4. Applicable ADRs under docs/adr/.
5. Subsystem guides and implementation.

Plans do not prove completion. Code, automated results, platform evidence, and manual evidence do.

## Work selection

- Implement one independently verifiable work package at a time.
- State the target milestone, SP plan, dependency state, and package boundary before editing.
- Do not begin a blocked plan merely because some implementation already exists.
- Distinguish implemented locally, verified on one platform, verified on both platforms, and milestone complete.
- If the requested change contradicts a locked decision or milestone rule, stop and propose an ADR before implementation.
- Use the prompt packages under docs/codex/prompts/ for planning, implementation, evidence collection, and closeout.

## Architecture and content invariants

- Simulation.Core is deterministic pure .NET and never references Godot or Steam.
- Presentation and platform assemblies depend inward; domain code does not depend outward.
- Persistent simulation mutation goes through registered commands and events.
- Stable IDs are namespaced and never silently reused.
- Saves, public contracts, content manifests, and schemas require explicit versioning and compatibility coverage.
- Player-facing text uses localization keys and must work in Korean and English.
- Historical content records sources, confidence, disputes, and history-versus-Romance classification.
- AI-assisted shipped assets require complete provenance and human review. Live generation is out of scope.
- Never introduce explicit sexual content.

## Verification

Use the narrowest relevant checks while iterating, then run the repository gates before declaring a package complete:

~~~bash
./scripts/validate.sh
./scripts/test.sh Release
~~~

Run import, export, smoke, soak, content, visual, performance, or platform checks when the target plan requires them. Do not check a visual or cross-platform criterion from compile-only or single-platform evidence.

## Documentation and evidence

- Update documentation in the same package when behavior, schema, commands, status, or acceptance evidence changes.
- Keep unchecked any criterion whose required evidence is missing.
- Record commit SHA, platform, command, result, and artifact/checksum identity for external verification.
- Never describe an uncommitted tree as a clean-checkout or same-revision result.
- Do not weaken validation to make a gate pass.

## Git and external actions

- Preserve unrelated user changes.
- Do not create or choose a remote, push, open a pull request, publish, sign, notarize, upload to Steam, or use credentials without explicit user authorization.
- Never commit secrets, certificates, tokens, notarization profiles, Steam credentials, or machine-local paths.
- Avoid parallel worktrees until the repository has a real baseline commit.

## Subagents

- The main agent owns package scope, architectural decisions, integration, final edits, and completion claims.
- Delegate only bounded independent work. Prefer subagents for exploration, contract tracing, test analysis, historical research, visual review, and log analysis.
- At most one agent owns writes to a given file or public contract.
- Do not run parallel write-heavy agents against shared project, solution, roadmap, schema, save, or contract files.
- Wait for delegated results, reconcile conflicts, then verify the integrated tree.
- Project-scoped roles are defined in .codex/agents/ and routed in docs/codex/AGENT_ROSTER.md.
