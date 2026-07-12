# Historical and Content Research Rules

This directory governs historical claims, terminology, localization, and asset provenance under [master-plan version 0.1.0](../MASTER_PLAN.md).

The executable authoring contracts and commands are documented in the [content pipeline guide](../CONTENT_PIPELINE.md).

## Source hierarchy

Prefer sources in this order:

1. Contemporaneous or near-contemporaneous primary texts in reliable editions or translations.
2. Modern academic monographs, peer-reviewed research, and university reference works.
3. Specialist reference works with transparent citations.
4. General reference works for orientation only.
5. Romance of the Three Kingdoms and later traditions as explicitly tagged literary material.

Do not silently resolve disputed dates, identities, locations, titles, or institutions. Record the competing claims and the gameplay choice.

## Content tags

Every historical claim or authored event uses one of:

- `historical`: supported by the canonical historical source set.
- `disputed`: sources conflict or identification is uncertain.
- `inferred`: a documented design inference fills a source gap.
- `romance`: derived from the novel or later popular tradition.
- `fictional`: created for gameplay without claiming historicity.

Optional Romance content must never overwrite historical-core data without a visible rules/content setting.

## Citations

Use [SOURCE_TEMPLATE.md](SOURCE_TEMPLATE.md) for each source or tightly related source group. Character, region, scenario, and event records reference source IDs rather than copying unsupported prose into data files.

## Terminology and localization

- Maintain a Korean/English glossary for offices, titles, administrations, formations, units, relationships, and cultural terms.
- Preserve source-language characters where helpful, but expose readable Korean and English display names.
- Distinguish a universal gameplay tier from the locally displayed historical name.
- Treat names, courtesy names, posthumous names, ranks, and titles as structured fields rather than one display string.
- All player-facing text uses localization keys; neither Korean nor English is a fallback for missing release text.

## Relationship-content boundary

- Version 1.0 contains no sexually explicit content.
- Interactive romance requires adult characters aged 18 or older.
- Childhood betrothal, where historically relevant, is political-only.
- Coercive marriage or household actions are political events with consequences and are not presented as successful romance.

## AI-assisted asset provenance

Offline AI assistance is allowed for text, portraits, and non-explicit illustrations only. Record:

- Model or service and version.
- Generation date.
- Input sources and rights status.
- Prompt or production brief.
- Human edits and reviewer.
- Evidence of commercial-use rights.
- Final asset checksum and affected content IDs.

Live generation is prohibited. Assets without complete provenance remain development-only and cannot enter a release build.

Start individual production records from [ASSET_PROVENANCE_TEMPLATE.md](ASSET_PROVENANCE_TEMPLATE.md), then normalize approved fields into the pack's `AssetProvenance` JSON document and CSV register.
