# Prompt Package: SP-04X1 Bounded Content and Character Presentation

Use only after SP-04G has accepted exact-SHA hosted evidence. This package proves the SP-04 systems with a bounded source-backed 191 content slice, a custom-content fixture, bilingual localization, and the minimum required Godot character/family/relationship/succession presentation.

## Authorization boundary

The user has authorized local implementation, commits, and ordinary non-force pushes to the approved `origin/main` for remaining SP-04 work. Verify remote, branch, tree, and divergence before each commit or push.

No force push, rebase, branch, tag, pull request, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action is authorized. Do not add AI-assisted shipped assets unless provenance and human review are complete.

## Prompt

~~~text
/goal

Complete SP-04X1 — bounded historical/custom character content, Korean/English localization, and the minimum usable character/family/relationship/marriage/succession presentation — as one larger vertical package through exact-SHA hosted evidence and visual closeout.

Keep content, localization, application consumption, and presentation together because this package must prove an actual bilingual SP-04 vertical slice. Do not split individual panels or content records into separate acceptance packages merely for convenience.

Dependency gate

- SP-04G must be accepted with exact-SHA hosted evidence.
- Observer-filtered application models must be stable; presentation must not read authoritative simulation state directly.
- This package is bounded SP-04/M2 proof, not the SP-13 300–400-character Early Access roster or the complete SP-11 interface.

Read:

1. AGENTS.md
2. docs/MASTER_PLAN.md
3. docs/ROADMAP.md
4. docs/plans/README.md
5. docs/plans/SP-04-characters-family-marriage-succession.md
6. relevant boundaries in SP-11, SP-12, and SP-13
7. applicable ADRs
8. docs/CHARACTERS.md
9. docs/CONTENT_PIPELINE.md
10. docs/DEVELOPMENT.md
11. docs/codex/AGENT_ROSTER.md
12. docs/codex/prompts/04-subplan-implementation.md
13. docs/codex/prompts/05-verification-closeout.md
14. Current content schemas/loaders, localization, provenance registers, Game.Application queries, Godot scenes/scripts, import/export/smoke, and tests

Use:

- project-architect, read-only, for vertical boundary, dependency, and ADR review;
- content-engineer for assigned schemas, source-backed records, localization, and provenance;
- godot-presentation-engineer for assigned scenes, UI scripts, interaction, accessibility, and visual evidence;
- simulation-engineer only if a proven defect in the accepted application contract blocks the vertical slice;
- verification-reviewer, read-only, after integration.

Sequence stable contracts first. Assign non-overlapping paths and one writer per shared file. The main agent owns shared contracts, integration, plan/roadmap status, final verification, Git, hosted evidence, and completion claims.

Before editing:

1. Verify accepted G evidence and a safe repository state.
2. Define the smallest source-backed 191 character/family/household set that exercises identity, kinship, marriage, relationship, succession, and differing knowledge states. Do not confuse this with the full SP-13 roster.
3. Define a separate deterministic custom-authored content fixture or pack using ordinary SP-02/SP-04 contracts. Do not implement the SP-12 creation editor.
4. Define the exact screens, interactions, localization keys, resolutions/scales, and visual evidence needed for SP-04 acceptance.
5. Stop if required assets would lack rights/provenance or if the requested UI would require implementing blocked SP-11 systems.

Required outcome

Content and localization:

1. Add a bounded representative 191 historical character slice with structured names, courtesy names where supported, origins, dates, culture/location references, abilities/aptitudes/traits/flaws/ambitions/reputations, typed family links, households, and enough scenario state to exercise accepted SP-04 behavior.
2. Record sources, confidence, disputes, and historical-versus-Romance classification for every historical assertion.
3. Add a separate custom-authored character/family/household fixture or optional pack proving custom origin, stable IDs, localization, save persistence, and no collision with historical IDs.
4. Supply complete nonempty Korean and English localization for every player-facing record and UI string.
5. Validate content manifests, references, overrides, source coverage, localization coverage, chronology, kinship, and scenario consistency.
6. Do not add AI-assisted art, portraits, or text without the required provenance record and human review.

Presentation:

7. Build the minimum coherent SP-04 character interface consuming only immutable observer-filtered Game.Application models.
8. Show character identity, age/condition, abilities/aptitudes/traits/flaws/ambitions/reputation, known relationships/memories, family/household, marriage/romance status, resources/estates at permitted visibility, guardianship/children/education, succession designation/claims/support/resolution/inheritance/regency, and player-continuity status where known.
9. Provide usable submission paths for already accepted SP-04 player commands where the player has authority; invalidated commands show precise current feedback and refresh.
10. Visibly distinguish family, household, retinue, employer, succession claim, support, designation, legal successor, regent, and controlled character.
11. Present political marriage and adult romance as separate states and never frame coercion as successful romance.
12. Handle unknown/private information without leakage and provide localized systemic fallback text when bespoke scene content is absent.
13. Support Korean/English switching, font fallback, text expansion, scalable UI, keyboard/mouse focus, readable contrast, non-color indicators, and required window/resolution behavior for these panels.
14. Preserve UI state safely across refresh, save/load, player transfer, and resolution changes without storing direct domain-object references.

Package boundary and non-goals

- Do not build the full SP-11 campaign shell, tutorial, accessibility certification, or tactical HUD.
- Do not implement SP-12's character creator, generation archetypes, start packages, or faction founding.
- Do not attempt SP-13's complete 191 roster or later bookmarks.
- Do not implement faction, court, diplomacy, economy, army, AI, or tactical gameplay.
- Do not expose hidden authoritative data to simplify UI.
- Never introduce explicit sexual content.

Required acceptance matrix

Cover at least:

- dependency/ADR and cross-layer ownership safety;
- historical source/provenance/classification completeness;
- deterministic custom-content load/save/ID isolation;
- Korean/English content and UI coverage;
- content/schema/reference/chronology/kinship validation;
- observer-safe display for every SP-04 view section;
- command submission, invalidation feedback, and refresh;
- explicit role/state distinctions;
- marriage/romance divergence and coercion framing;
- unknown/private fallback and no-leak tests;
- localization expansion, scaling, focus, contrast, and non-color indicators;
- save/load/player-transfer UI refresh behavior;
- automated scene/import tests plus manual rendered interaction evidence;
- exact-SHA hosted builds, exports, smoke, manifests, and artifacts.

Verification

Run focused content/application/presentation checks while iterating, then:

./scripts/validate.sh
./scripts/test.sh Release
./scripts/import.sh
git diff --check
git lfs fsck

Also run:

- content normalization, source, provenance, localization, and schema reports;
- Korean and English UI tests at the declared resolutions/scales;
- observer-leak and command-feedback tests;
- local macOS development export and automated smoke;
- interactive Apple Silicon macOS rendering and interaction review for every checked visual criterion;
- screenshots or other repository-approved visual evidence with exact revision/tool/platform identity;
- focused formatter checks.

Do not mark a visual, interaction, localization-layout, or accessibility criterion complete from compilation/import alone.

Closeout

1. Obtain verification-reviewer audit, including content, localization, knowledge filtering, and visual evidence.
2. Fix validated in-scope defects and rerun affected evidence.
3. Update SP-04, ROADMAP, index, CHARACTERS, content, and presentation docs to the demonstrated local status.
4. Commit the intentional X1 package, re-fetch, and ordinarily push main.
5. Observe automatic exact-SHA CI only.
6. Require hosted macOS arm64/Windows x64 validation, complete tests, import, native export, automated smoke, manifests, and artifacts for the implementation SHA.
7. Authenticate run/job/artifact identities and inspect both artifacts.
8. Add exact-SHA hosted evidence and the revision-bound local visual evidence index, mark X1 accepted only, commit evidence/status docs, and ordinarily push main.

Definition of done

- A bounded historical and custom character slice validates, localizes, saves, and renders through the normal SP-04 stack.
- The minimum SP-04 character/family/relationship/succession interface is knowledge-safe and usable in Korean and English.
- Required local visual evidence and exact-SHA hosted macOS/Windows evidence pass and are committed.
- The tree is clean and synchronized.
- Only integrated performance, compatibility audit, any evidence-backed residual SP-04 defects, and final closeout remain.
- The next package is SP-04X2; do not start it here.
~~~
