# Prompt Package: SP-04X2 Integrated Verification and Final Closeout

Use only after SP-04X1 has accepted exact-SHA hosted and local visual evidence. This final package is an evidence-driven completion audit, integrated performance/compatibility pass, bounded remediation package, and SP-04 closeout.

## Authorization boundary

The user has authorized local SP-04 remediation, commits, and ordinary non-force pushes to the approved `origin/main`. Verify remote identity, branch, tree, and divergence before each commit or push.

No force push, rebase, branch, tag, pull request, release, workflow dispatch/rerun, signing, notarization, Authenticode, Steam, deployment, publishing, credential, or remote-setting action is authorized. Physical Windows remains an M4 gate; production signing and Steam remain SP-15 gates.

## Prompt

~~~text
/goal

Complete SP-04X2 — full requirement-by-requirement SP-04 audit, integrated performance and compatibility verification, bounded remediation of proven SP-04 gaps, exact-SHA hosted evidence, and final SP-04 closeout — as one final package.

Do not declare completion from the existence of prior package evidence. Reconstruct every SP-04 requirement and acceptance criterion and prove it against current code, tests, runtime behavior, content, presentation, saves, performance, and exact-SHA evidence. Do not split routine residual fixes into new micro-packages; integrate evidence-backed SP-04 corrections here unless a locked decision or genuinely independent major subsystem makes that unsafe.

Dependency gate

- SP-04F9, F10, F11, G, and X1 must each be accepted with exact-SHA hosted evidence.
- X1 must also have revision-bound Apple Silicon visual/interaction/localization evidence.
- SP-01 and SP-02 remain complete; SP-05 remains blocked until this package legitimately closes SP-04.

Read:

1. AGENTS.md
2. docs/MASTER_PLAN.md
3. docs/ROADMAP.md
4. docs/plans/README.md
5. the complete docs/plans/SP-04-characters-family-marriage-succession.md
6. all applicable ADRs
7. docs/CHARACTERS.md
8. docs/SIMULATION.md
9. docs/CONTENT_PIPELINE.md
10. docs/DEVELOPMENT.md
11. docs/codex/AGENT_ROSTER.md
12. docs/codex/prompts/05-verification-closeout.md
13. Every accepted SP-04 exact-SHA evidence report
14. The complete current SP-04 implementation, schemas, migrations, content, UI, tests, CI, and relevant artifacts

Use:

- project-architect, read-only, for the full dependency/contract/ADR/completion audit;
- verification-reviewer, read-only, for independent requirement-to-evidence mapping and final diff review;
- simulation-engineer, content-engineer, or godot-presentation-engineer only for bounded, non-overlapping remediation proven necessary by the audit.

The main agent owns the completion matrix, remediation scope, shared contracts, integration, final verification, status changes, Git, hosted evidence, and the SP-04 completion claim.

Phase 1 — prove the remaining scope before editing

Build a completion matrix covering every item in:

- SP-04 goal, requirements, public contracts, data flow, seven workstreams, edge cases, performance budget, test list, and seven acceptance criteria;
- MASTER_PLAN character/political requirements applicable to SP-04;
- original user package/decomposition and execution requirements;
- every unproven limitation recorded by accepted SP-04 packages;
- save schema and compatibility promises;
- knowledge filtering, localization, historical sourcing, provenance, UI, and platform-evidence rules.

For every item record:

1. authoritative implementation evidence;
2. automated test evidence and why it covers the requirement;
3. required manual, visual, performance, hosted, or artifact evidence;
4. status: proven, contradicted, incomplete, weak/indirect, or missing;
5. smallest in-scope remediation when not proven.

Do not edit until this matrix identifies the bounded X2 remediation scope. Stop for an ADR or user decision if completion requires changing a locked product/architecture decision or importing a genuinely blocked subsystem.

Required integrated outcomes

At minimum, prove or remediate:

1. Historical and custom characters persist with structured identity, abilities, relationships, memories, family, household, and retinue.
2. Family, household, retinue, employer, faction/subfaction placeholders, and imperial allegiance boundaries are not conflated.
3. The nine relationship dimensions and bounded consequential memories produce distinct effects without one universal loyalty score.
4. Political marriage and adult non-explicit romance both function and can diverge emotionally.
5. Both romance participants are at least 18 and able to consent; childhood betrothal remains political-only.
6. Coercive household actions never create positive romance or attraction and produce appropriate negative/neutral consequences.
7. Children, parentage, adoption, guardianship, education, coming of age, death, succession, inheritance, and regency resolve deterministically.
8. Pregnancy/death, simultaneous deaths, missing heirs, extinct families, captivity, stale commands, and disputed succession have deterministic tested behavior.
9. Player-character succession preserves the same campaign when a valid successor exists and follows explicit scenario rules otherwise.
10. Observer-filtered CharacterProfile, RelationshipSummary, HouseholdView, and SuccessionView do not leak hidden information.
11. Character event integration covers campaign consequences and the bounded battle capture/death/rescue/shared-memory round-trip without outward architecture violations.
12. Bounded historical/custom content, Korean/English localization, sources/classification/provenance, and required UI/visual behavior are complete for the SP-04 M2 slice.
13. Save/load, recovery, pending commands, replay, tier transitions, schema raw validation, every forward migration, frozen fixtures, checksums, and later-day continuation cover the final contracts.
14. Stable IDs, canonical ordering, defensive copies, checked capacities, bounded detailed histories/folding, deterministic input ordering, and exact causality hold across every SP-04 subsystem.

Performance proof

Create or use a representative integrated fixture with 1,000 historical plus generated characters and realistic bounded SP-04 state. Measure and enforce the documented budgets on the M2 Pro development Mac:

- routine integrated SP-04 processing within the overall 3-second campaign-turn budget;
- detailed social-graph/character view queries for one character within 100 ms;
- bounded memory/history storage with no unbounded collection growth.

The fixture must exercise relationships/memories, career/retinue, resources/estates, marriage/romance, family/guardianship/education, succession/inheritance/regency, observer queries, and relevant event integration rather than timing an empty or synthetic shortcut.

Record machine, OS, architecture, SDK, configuration, fixture identity, counts, commands, warm-up policy, repetitions, observed results, and checksum. Do not weaken an existing threshold. Hosted CI may run correctness coverage without pretending shared-runner wall-clock results are local performance proof.

Required final verification

Run the narrowest remediation checks while editing, then at minimum:

./scripts/validate.sh
./scripts/test.sh Release
./scripts/import.sh
git diff --check
git lfs fsck

Also run:

- every SP-04 focused test slice and architecture test;
- complete save raw-shape, fixture, migration, recovery, replay, and checksum coverage;
- exact ten-year/1,000-entity soak;
- the integrated performance suite;
- local macOS development export and automated smoke;
- X1 bilingual visual/interaction regressions affected by X2;
- formatter checks for all changed source.

Independent review

Have project-architect and verification-reviewer independently audit:

- every completion-matrix row;
- the complete X2 diff;
- exact test/evidence coverage;
- compatibility and architecture;
- hidden information, localization, content sourcing, and visual claims;
- secrets, machine-local paths, generated files, LFS, and unrelated edits;
- whether all seven SP-04 acceptance criteria are actually proven.

Resolve every validated SP-04 blocker. Leave SP-04 Active if any required evidence remains missing, weak, wrong-revision, or unavailable.

Git and exact-SHA hosted closeout

1. Update implementation and documentation to a locally verified candidate without marking SP-04 complete prematurely.
2. Commit only the intentional final implementation/remediation package.
3. Re-fetch and ordinarily push main to the approved origin.
4. Observe the automatically triggered CI; do not dispatch or rerun.
5. Require the exact implementation SHA to pass clean-checkout hosted macOS arm64 and Windows x64 validation, complete tests, import, native development export, automated smoke, manifest checks, and artifact upload.
6. Authenticate run, job, runner, SHA, test, checksum, smoke, manifest, artifact, digest, size, architecture, and representative file identities.
7. Download and inspect both artifacts through authenticated existing access.
8. Add the final exact-SHA hosted evidence report and completion matrix.
9. Only if every SP-04 criterion is proven, mark SP-04 Complete, update ROADMAP/plan index/CHARACTERS/SIMULATION and unblock SP-05.
10. Commit the final evidence/status closeout and ordinarily push main.

Do not require or claim:

- physical Windows hardware evidence before M4;
- signing, notarization, Authenticode, Steam, clean-install/update, or release-candidate certification before SP-15;
- the SP-13 Early Access roster, later bookmarks, full SP-11 interface, SP-12 character creator/faction founding, SP-05 political systems, or SP-08 tactical runtime;
- evidence from a different revision.

Definition of done

- Every explicit SP-04 requirement and all seven acceptance criteria have direct authoritative evidence.
- Integrated local performance budgets pass without weakened thresholds.
- Save/migration/determinism/content/localization/knowledge/UI/visual compatibility is verified at the required scope.
- The exact final implementation SHA passes hosted macOS arm64/Windows x64 and artifact inspection.
- Final completion evidence and status documentation are committed and pushed to origin/main.
- The tree is clean and synchronized.
- SP-04 is marked Complete and SP-05 is unblocked only if the audit proves all of the above.
- If any item remains unproven, report it precisely and leave SP-04 Active; do not redefine completion.
~~~
