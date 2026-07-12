# Authored Data

Authored JSON, published schemas, localization tables, the content manifest, research records, and provenance registers live here.

- `content-manifest.json` is the built-in pack contract.
- `authored/` contains versioned `ContentRecord` documents.
- `localization/` contains Korean/English RFC 4180 CSV and the bilingual glossary.
- `research/` contains structured source/claim records.
- `provenance/` contains normalized asset provenance plus CSV authoring registers.
- `schemas/` publishes the initial JSON Schema 2020-12 contracts.
- `mods/` is reserved for data-only development packs and contains no runtime-code path.

All files declared by a manifest are SHA-256 checked before parsing. See the [content pipeline guide](../docs/CONTENT_PIPELINE.md) for authoring, validation, normalization, and override rules.
