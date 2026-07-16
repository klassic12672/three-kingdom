# Runtime Assemblies

This directory contains the non-Godot runtime boundaries. Dependencies point toward `Simulation.Core`; that project must remain deterministic and free of Godot and Steam references.

`Simulation.Core` now contains the SP-01 versioned domain contracts, deterministic world kernel, canonical checksum, snapshot/save primitives, and synthetic fixtures. Persistence and runner usage are documented in the [simulation guide](../docs/SIMULATION.md).

`Game.Content` contains the SP-02 strict authored contracts, semantic validation, deterministic data-mod resolver, frozen runtime registry, localization/glossary pipeline, source records, and asset provenance policy. See the [content pipeline guide](../docs/CONTENT_PIPELINE.md).

`Simulation.Core` and `Game.Content` now also contain the SP-03 geographic graph, scenario overlays, path queries, movement/interception/retreat events, route-limited supply, fog-aware political queries, and battle-location descriptors. See the [campaign geography guide](../docs/CAMPAIGN_GEOGRAPHY.md).

`Simulation.Core` contains the accepted SP-04 character, relationship, household, marriage, lifecycle, and succession state plus the narrow registered wound action and schema-29 compatibility boundary. `Game.Application` contains observer-filtered `CharacterProfile`, reused `RelationshipSummary`, `HouseholdView`, and `SuccessionView` queries plus the three-type character battle-contribution adapter; it adds no final battle or tactical subsystem. See the [character guide](../docs/CHARACTERS.md).
