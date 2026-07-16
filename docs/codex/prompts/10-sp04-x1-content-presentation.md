# Prompt Package: SP-04X1 Minimal Content and Character Inspector

Use after SP-04G is accepted. This package proves a small bilingual SP-04 vertical slice; it is not a substitute for SP-11, SP-12, or SP-13.

## Authorization boundary

The user has authorized local SP-04 implementation, commits, and ordinary non-force pushes to the approved `origin/main`. Verify remote, branch, tree, and divergence before each commit or push.

No force push, rebase, branch, tag, pull request, release, workflow dispatch/rerun, signing, Steam, deployment, publishing, credential, or remote-setting action is authorized.

## Prompt

~~~text
/goal

Complete SP-04X1 — a small source-backed/custom bilingual content fixture and one minimal knowledge-safe character inspector — with local visual and exact-SHA hosted evidence.

Ponytail routing

- Invoke `ponytail:ponytail` in full mode before selecting content records,
  screens, controls, or assets.
- Invoke `ponytail:ponytail-review` after integration if available.
- Use native Godot controls and the existing Main scene before adding components,
  navigation frameworks, design systems, assets, or dependencies.
- If a UI element is not needed to prove an SP-04 acceptance criterion, skip it.

Dependency gate

- SP-04G must be accepted.
- This is not the SP-13 roster, SP-12 creator, or full SP-11 interface.

Read AGENTS.md, source-of-truth plans, relevant SP-11/SP-12/SP-13 boundaries,
applicable ADRs, CHARACTERS, CONTENT_PIPELINE, DEVELOPMENT, current content/
localization/provenance rules, application queries, and Godot scene/scripts.

Use:

- project-architect, read-only, for the vertical boundary;
- content-engineer for a small assigned content/localization scope;
- godot-presentation-engineer for one assigned inspector;
- verification-reviewer, read-only, after integration.

The main agent owns shared contracts, integration, documentation, Git, evidence,
and completion claims.

Before editing

1. Choose the smallest fixture that exercises historical and custom persistence:
   target 6–10 characters unless a specific required relationship needs more.
2. Reuse existing content schemas. Add schema fields only if an explicit SP-04
   requirement cannot be represented.
3. Define one inspector with four sections: identity, relationships, household,
   and succession. Avoid a new campaign shell or screen framework.
4. Select at most one essential succession action flow if a read-only inspector
   cannot prove required player interaction.
5. Add no portrait or other asset unless it is necessary and fully sourced.

Required outcome

Content:

1. Add a small representative 191 historical fixture and one custom-authored
   character/family example using ordinary SP-02/SP-04 contracts.
2. Record sources, confidence, disputes, and historical-versus-Romance
   classification for historical assertions.
3. Provide complete Korean and English localization.
4. Prove stable-ID isolation, validation, and save persistence.

Presentation:

5. Build one native Godot character inspector consuming only observer-filtered
   Game.Application models.
6. Show the minimum useful identity, known relationships/memories,
   family/household roles, and succession state.
7. Clearly distinguish family, household, retinue/employer, designation, claim,
   support, legal successor, regency requirement, and controlled character when
   those values are present.
8. Keep political marriage and romance distinct and never present coercion as
   successful romance.
9. Hide unknown/private information and use localized systemic fallback text.
10. Support Korean/English switching, text expansion, keyboard focus, readable
    contrast, scalable text, and non-color state indicators for this inspector.
11. Preserve inspector selection across refresh when valid; do not persist direct
    domain-object references.

Non-goals

- No full command center, relationship graph, family management suite, tutorial,
  design system, tactical HUD, character creator, generated roster, faction
  founding, or complete 191 roster.
- No “all accepted commands” UI.
- No AI-assisted asset unless required provenance and human review already exist.
- Never introduce explicit sexual content.

Required tests and evidence

- Content/reference/source/localization/ID/save validation.
- Observer-leak and bilingual inspector tests.
- One action-flow invalidation test only if that flow is included.
- Godot import and automated smoke.
- Local Apple Silicon rendered review in Korean and English, with revision-bound
  screenshots for the inspector's required states.

Verification and closeout

1. Run focused checks, then:

   ./scripts/validate.sh
   ./scripts/test.sh Release
   ./scripts/import.sh
   git diff --check
   git lfs fsck

2. Run the local macOS development export/smoke and required visual review.
3. Obtain independent review and fix validated in-scope issues.
4. Commit, re-fetch, and ordinarily push `main`.
5. Require exact-SHA hosted macOS/Windows tests, import, export, smoke, manifests,
   and artifacts.
6. Because X1 changes presentation/content, download and inspect both artifacts.
7. Add exact-SHA and local visual evidence, mark X1 accepted, commit, and push.

Definition of done

- The small historical/custom fixture validates and persists in both languages.
- One knowledge-safe bilingual inspector proves the required SP-04 presentation.
- Local visual and hosted evidence pass; the tree is clean.
- Next package: SP-04X2. Do not begin it here.
~~~
