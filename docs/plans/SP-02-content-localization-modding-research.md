# SP-02 — Content, Localization, Modding, and Research

## Metadata

| Field | Value |
|---|---|
| Status | **Complete** |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M1 |
| Dependencies | [SP-01](SP-01-simulation-calendar-determinism-saves.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Provide a validated, source-backed, bilingual, data-driven content pipeline that supports built-in content and safe data mods without compiling game code.

## Non-goals

- Runtime scripting mods or arbitrary code execution.
- Steam Workshop integration.
- Completing the historical roster or every scenario during M1.
- Treating Romance material as untagged historical fact.

## Requirements

- Store structured authored content in UTF-8 JSON with published JSON Schemas.
- Use CSV import/export for localization and bulk tabular editing; runtime consumes validated normalized data.
- Give every entity and localization entry a stable namespaced `EntityId`/key.
- Define a `ContentManifest` with pack ID, semantic version, game/schema requirements, dependencies, load priority, checksums, authorship, and provenance summary.
- Validate IDs, references, types, ranges, dates, dependency cycles, localization coverage, content tags, and source references headlessly.
- Load base packs first, then declared mods in deterministic topological order.
- Permit explicit field-level override only; reject ambiguous same-priority conflicts.
- Record `historical`, `disputed`, `inferred`, `romance`, or `fictional` tags and link claims to source records governed by [content rules](../content/README.md).
- Maintain Korean and English terminology/glossary records and require both languages for release-marked content.
- Record offline AI-assisted asset provenance and prohibit assets without human approval from release manifests.
- Detect sexually explicit classifications in pre-1.0 content and reject them from release manifests.

## Public contracts

- `ContentManifest`: authoritative pack metadata and dependency contract.
- `ContentRecord`: stable ID, schema version, content tag, source IDs, localization keys, and typed data.
- `LocalizationEntry`: stable key, locale, text, context, variables, review state, and source content IDs.
- `SourceReference`: source-record ID, claim/location, confidence, and notes.
- `AssetProvenance`: origin, rights, AI-assistance details, edits, reviewer, checksum, and release eligibility.
- `ContentValidationReport`: deterministic errors/warnings with file, JSON path, record ID, and remediation.

`SaveEnvelope` records enabled `ContentManifest` identities and checksums so load can detect missing or incompatible packs.

## Data flow

```text
Research/source records + authored JSON + localization CSV + assets
→ schema and semantic validation
→ deterministic normalization
→ content-pack checksum
→ runtime content registry
→ scenario/simulation consumers
```

## Implementation workstreams

1. Define common record envelope, manifest schema, ID rules, content tags, and source/provenance models.
2. Implement schema generation/storage and semantic validators.
3. Implement deterministic pack discovery, dependency resolution, override rules, and checksum production.
4. Implement Korean/English import, variable validation, glossary checks, and coverage reports.
5. Implement historical-source and asset-provenance registers/templates.
6. Build a CLI that validates, normalizes, reports, and optionally emits development fixtures.
7. Add save compatibility checks for enabled packs and clear diagnostics for missing mods.

## Edge cases and failure handling

- Duplicate IDs in one pack are errors; duplicate overrides across packs require declared override targets.
- Missing Korean or English text is an error for release content and a warning for development-only content.
- Unknown variables, broken markup, or inconsistent plural/select branches fail localization validation.
- Dependency cycles, unsupported schema versions, checksum mismatches, and missing required packs prevent load.
- Removing a mod does not rewrite saves; the load screen reports affected IDs and keeps the save intact.
- Conflicting historical sources are recorded as disputed rather than collapsed into an undocumented choice.

## Performance budget

- Validate an Early Access-scale content set in under 30 seconds on the development Mac.
- Build the normalized runtime registry in under 5 seconds for the vertical slice and under 15 seconds for the projected 1.0 set.
- Runtime lookups by stable ID are effectively constant-time and allocation-free in hot paths.

## Tests

- Golden valid/invalid manifest and record fixtures.
- Dependency ordering, cycle, conflict, and override tests.
- Cross-platform checksum equality tests.
- Localization coverage, variable, encoding, and glossary tests.
- Historical-tag/source-reference and provenance completeness tests.
- Save-load behavior with added, removed, and changed content packs.
- Release-manifest rejection tests for live-generated, unreviewed, or explicit content.

## Acceptance criteria

- [x] JSON schemas and semantic validators cover the initial synthetic content types.
- [x] Pack load order and overrides are deterministic on Windows and macOS.
- [x] Every record has a stable ID, content tag, and required source/provenance fields.
- [x] Korean and English coverage reports identify every missing release string.
- [x] Invalid packs fail independently without corrupting built-in content or saves.
- [x] Content manifests and checksums are stored and verified through saves/builds.
- [x] No runtime-code mod path exists.

The same tests passed for historical SHA `1ab375a5e812c14eba4eca4cc121e604ade73f47`, although that Windows job later failed in SP-00 cleanup; see the [historical report](../evidence/M0-EXACT-SHA-1ab375a.md). Cleanup-fix SHA `7f62a97cf880ae6ded8e47af8737a11e53479977` passed all 31 content tests and validation on both hosted platforms. Both reported registry checksum `e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d`, pack checksum `6c527133073ffece29d4d75f7372cc783f2855f6354ed5be9eb1a6c971936449`, and zero applicable diagnostics. With SP-01 complete, SP-02 is complete. See the [passing report](../evidence/M0-EXACT-SHA-7f62a97.md) and [content guide](../CONTENT_PIPELINE.md).

## Risks

| Risk | Mitigation |
|---|---|
| Schema churn creates costly migrations | Version envelopes and normalize authored data into runtime DTOs instead of binding directly to simulation classes. |
| Solo bilingual content volume becomes unmanageable | Enforce glossary/reuse, coverage dashboards, and milestone-specific release flags. |
| Mods create unstable save dependencies | Store exact manifests/checksums and fail safely without rewriting saves. |
| AI-assisted assets create rights risk | Require provenance and human approval before release eligibility. |

## Deferred work

- Steam Workshop distribution.
- Sandboxed scripting API.
- Additional launch languages.
- Collaborative web-based content editing.
