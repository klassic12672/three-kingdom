# Content, Localization, Modding, and Research Pipeline

SP-02 provides a data-only content pipeline in `Game.Content`. Authored packs contain UTF-8 JSON and CSV; they cannot load assemblies, execute scripts, or register runtime code.

## Pack layout

A built-in pack uses `data/content-manifest.json`. Development mods are discovered below `data/mods/**/content-manifest.json`. Each manifest records:

- a stable namespaced pack ID and semantic version;
- minimum game and content-schema versions;
- required or optional pack dependencies;
- built-in/release flags and deterministic load priority;
- every authored file's kind and SHA-256;
- authorship, license, rights holder, and source/asset registers;
- a canonical pack checksum.

Manifest file paths are portable relative paths: use ASCII segments and forward slashes, with no `.` or `..` segments. Canonical checksums always use invariant formatting, normalized forward slashes, ordinal ordering, and LF separators on every operating system.

All built-in packs load first. Mods then load in deterministic installed-dependency order, priority, and ordinal pack ID. Dependency cycles, missing required packs, incompatible versions, and changed checksums block the affected pack. An invalid mod is omitted without mutating the built-in registry.

Content files may be only `records`, `overrides`, `localization`, `glossary`, `sources`, or `provenance`. There is deliberately no script or assembly file kind.

## Records and overrides

Every `ContentRecord` has a contract version, stable `EntityId`, record type, one of the five governed content tags, classification, source IDs, localization keys, release flag, and typed JSON data. The initial semantic validators cover synthetic regions, characters, dated events, research claims, assets, cross-record references, ranges, and proleptic dates.

A mod cannot repeat a record to replace it. It must author an override document naming the target ID and each JSON-pointer field. IDs, schema versions, and record types cannot be overridden. Two packs changing the same field at the same priority are both rejected unless dependency/priority ordering makes the authority unambiguous.

Published JSON Schemas are under `data/schemas/`. The content-record schema uses discriminated, closed version-1 payload definitions for `character_world`, `character_definition`, `family_definition`, `household_definition`, and `character_identity_definition`; unrelated record types retain their existing generic data envelope. Runtime DTO normalization keeps authored schema evolution separate from consumers.

SP-04A adds a typed `CharacterContentLoader` over resolved registry records. It recognizes `character_world`, `character_definition`, `family_definition`, `household_definition`, and `character_identity_definition`, requires their closed typed data, and requires `data.references` plus envelope `localizationKeys` to canonically and exactly cover the typed references and names the loader consumes. Character-world and state authored contracts remain limited to v1/v2; both normalize to runtime character-v3 with empty education-attainment collections, while definitions remain authored/runtime v2. The unchanged published schema rejects authored runtime-v3 tags and `educationAttainments`, so acquired state cannot enter through content. Each candidate pack independently validates every typed record it authors or overrides, including unselected definitions, canonical per-kind identity lists, nested world-state collections, and non-empty Korean and English names. The complete post-override record envelope is validated before candidate-registry construction, and mistyped reference values become diagnostics rather than escaping the load boundary. Resolved-world graph validation then checks cross-record types, references, and state semantics separately. Invalid owning packs and mods receive deterministic record/world diagnostics and roll back atomically to the previously valid registry. The existing generic `character` record remains unchanged. The current fictional household fixture is test-owned and does not change the built-in manifest or registry checksum. See [the character foundation guide](CHARACTERS.md).

## Localization and glossary

Localization uses UTF-8 RFC 4180 CSV with this header:

```text
key,locale,text,context,variables,review_state,source_content_ids,release_marked
```

Use `|` between multiple variables or content IDs. Keys are namespaced IDs. Supported launch locales are `ko-KR` and `en-US`. Release-marked records require both locales, and release rows must be approved. Development gaps are warnings.

Validation rejects undeclared/unused variables, unbalanced message braces, plural/select messages without an `other` branch, malformed `[b]`, `[i]`, or `[color]` markup, duplicate key/locale rows, invalid UTF-8, and broken content references.

The bilingual glossary uses:

```text
term_id,ko-KR,en-US,notes,review_state
```

Both launch-language terms are mandatory.

## Research and provenance

Historical, inferred, and Romance records require source references. Disputed records require at least two sources so conflicts remain visible. `SourceReference` records preserve claim, location, confidence, notes, citation, and source tier. The rules in [Historical and Content Research Rules](content/README.md) remain authoritative.

Release assets require rights status, final SHA-256, content links, reviewer, and human approval. Offline-AI assets additionally require model/service version, date, input sources, prompt/brief, edits, and commercial-rights evidence. Live-generated, unreviewed, incomplete, or sexually explicit pre-1.0 assets are rejected from release packs.

## Commands

Run the repository-integrated validator:

```bash
./scripts/validate.sh
```

Run content operations directly:

```bash
dotnet run --project tools/Tools.ContentPipeline -- content validate
dotnet run --project tools/Tools.ContentPipeline -- content normalize --output artifacts/content
dotnet run --project tools/Tools.ContentPipeline -- content report --output artifacts/content-report.json
dotnet run --project tools/Tools.ContentPipeline -- content fixtures --output artifacts/development-fixture.json
```

Diagnostics are deterministic and include severity, code, file, JSON/row path, record ID, message, and remediation. Normalization emits ordered records, translations, glossary, enabled manifests, validation report, and aggregate registry checksum.

`ContentRegistry` uses frozen dictionaries for constant-time runtime lookup. It exports exact pack ID/version/checksum references for `SaveEnvelope`; added packs are compatible, while removed or changed required packs block load without rewriting the save. Build-manifest schema 2 stores two distinct values: `contentManifestChecksum` is the canonical checksum declared by the top-level built-in pack, while `contentRegistryChecksum` covers the aggregate validated load order. Ignored schema-1 development exports are non-evidence and must be regenerated.
