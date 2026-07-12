# Tools

`Tools.ContentPipeline` owns headless repository/content validation, normalization, deterministic reports/development fixtures, and build-manifest generation. It may reference `Game.Content` but must not become a runtime dependency. See the [content pipeline guide](../docs/CONTENT_PIPELINE.md).

`Tools.Simulation` runs deterministic synthetic soaks and replays, compares checksums, and inspects or recovers saves. See the [simulation guide](../docs/SIMULATION.md).
