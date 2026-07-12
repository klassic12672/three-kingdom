# Tests

Tests enforce assembly direction, repository invariants, build-manifest shape, documentation links, and LFS pointer integrity.

`Simulation.Core.Tests` covers calendar chronology, ID validation, command/event ordering, random-stream isolation, canonical checksums, simulation tiers, background commit ordering, save atomicity/recovery/migration, and ten-, fifty-, and hundred-year synthetic soaks.

`Game.Content.Tests` covers published schemas, golden pack checksums, deterministic dependency/override resolution, localization and glossary validation, research/provenance policy, typed ranges/dates/references, data-only mod boundaries, and save compatibility for added, removed, or changed packs.

SP-03 coverage spans both suites: the content tests load the bilingual 191 slice and map-mode query inputs, while simulation tests cover graph integrity, path budgets, movement, seasonal blocking, interception, retreat, reinforcement, shared supply capacity, political-state independence, fog of war, battle descriptors, and persisted geography checksums.
