# Runtime Assemblies

This directory contains the non-Godot runtime boundaries. Dependencies point toward `Simulation.Core`; that project must remain deterministic and free of Godot and Steam references.

`Simulation.Core` now contains the SP-01 versioned domain contracts, deterministic world kernel, canonical checksum, snapshot/save primitives, and synthetic fixtures. Persistence and runner usage are documented in the [simulation guide](../docs/SIMULATION.md).

`Game.Content` contains the SP-02 strict authored contracts, semantic validation, deterministic data-mod resolver, frozen runtime registry, localization/glossary pipeline, source records, and asset provenance policy. See the [content pipeline guide](../docs/CONTENT_PIPELINE.md).

`Simulation.Core` and `Game.Content` now also contain the SP-03 geographic graph, scenario overlays, path queries, movement/interception/retreat events, route-limited supply, fog-aware political queries, and battle-location descriptors. See the [campaign geography guide](../docs/CAMPAIGN_GEOGRAPHY.md).

`Simulation.Core` contains SP-04A character state plus the SP-04B-L directional relationship/memory kernel, bounded history, deterministic IDs, schema-5 persistence, and authenticated migrations. `Game.Application` contains the observer-filtered `RelationshipSummary` query; it exposes exact dimensions and archives only to the subject. See the [character guide](../docs/CHARACTERS.md).
