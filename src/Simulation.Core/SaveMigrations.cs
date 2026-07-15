using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core;

public interface ISaveMigration
{
    int FromSchemaVersion { get; }

    int ToSchemaVersion { get; }

    JsonObject Migrate(JsonObject source);
}

public sealed class SaveSchemaRegistry
{
    private readonly IReadOnlyDictionary<int, ISaveMigration> migrations;

    public SaveSchemaRegistry(IEnumerable<ISaveMigration>? migrations = null)
    {
        ISaveMigration[] registered = (migrations ??
        [
            new SaveMigrationV1ToV2(),
            new SaveMigrationV2ToV3(),
            new SaveMigrationV3ToV4(),
        ]).ToArray();
        if (registered.Any(item => item.ToSchemaVersion != item.FromSchemaVersion + 1))
        {
            throw new ArgumentException("Save migrations must advance exactly one schema version.", nameof(migrations));
        }

        this.migrations = registered.ToDictionary(item => item.FromSchemaVersion);
    }

    public JsonObject MigrateToCurrent(JsonObject source)
    {
        JsonObject working = (JsonObject)source.DeepClone();
        int schemaVersion = GetSchemaVersion(working);
        if (schemaVersion > SaveEnvelope.CurrentSchemaVersion)
        {
            throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} is newer than supported schema {SaveEnvelope.CurrentSchemaVersion}.");
        }

        if (schemaVersion < SaveEnvelope.CurrentSchemaVersion)
        {
            try
            {
                // Validate the original shape once; intermediate migrations intentionally replace checksums.
                ValidateHistoricalSourceChecksum(working, schemaVersion);
            }
            catch (Exception exception) when (SaveDataExceptionPolicy.IsRecoverableDataFailure(exception))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} could not be authenticated before migration.",
                    exception);
            }
        }

        while (schemaVersion < SaveEnvelope.CurrentSchemaVersion)
        {
            if (!migrations.TryGetValue(schemaVersion, out ISaveMigration? migration))
            {
                throw new SaveCompatibilityException($"No migration is registered from save schema {schemaVersion}.");
            }

            try
            {
                working = migration.Migrate(working);
            }
            catch (Exception exception) when (SaveDataExceptionPolicy.IsRecoverableDataFailure(exception))
            {
                throw new SaveCompatibilityException(
                    $"Migration from schema {migration.FromSchemaVersion} to {migration.ToSchemaVersion} failed.",
                    exception);
            }

            schemaVersion = GetSchemaVersion(working);
            if (schemaVersion != migration.ToSchemaVersion)
            {
                throw new SaveCompatibilityException(
                    $"Migration from schema {migration.FromSchemaVersion} did not produce schema {migration.ToSchemaVersion}.");
            }
        }

        ValidateCurrentSaveShape(working);
        return working;
    }

    private static int GetSchemaVersion(JsonObject source) => source["schemaVersion"]?.GetValue<int>()
        ?? throw new SaveCompatibilityException("Save is missing required 'schemaVersion' data.");

    private static void ValidateHistoricalSourceChecksum(JsonObject source, int schemaVersion)
    {
        if (schemaVersion is < 1 or > 3)
        {
            throw new SaveCompatibilityException($"Save schema {schemaVersion} has no historical checksum contract.");
        }

        ValidateHistoricalSourceShape(source, schemaVersion);
        string storedChecksum = source["checksum"]?.GetValue<string>()
            ?? throw new SaveCompatibilityException($"Save schema {schemaVersion} is missing its checksum.");
        WorldSnapshot snapshot = source["snapshot"]?.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException($"Save schema {schemaVersion} is missing its authoritative snapshot.");
        string actualChecksum = SimulationChecksum.ComputeForSaveSchema(snapshot, schemaVersion).Value;
        if (!StringComparer.Ordinal.Equals(storedChecksum, actualChecksum))
        {
            throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} checksum does not match its authoritative snapshot "
                + $"(stored {storedChecksum}, actual {actualChecksum}).");
        }
    }

    private static void ValidateHistoricalSourceShape(JsonObject source, int schemaVersion)
    {
        JsonObject snapshot = RequireHistoricalObject(source, "snapshot", schemaVersion, "snapshot");
        _ = RequireHistoricalObject(snapshot, "calendar", schemaVersion, "snapshot.calendar");
        _ = RequireHistoricalObjectArray(snapshot, "randomStreams", schemaVersion, "snapshot.randomStreams");

        IReadOnlyList<JsonObject> entities = RequireHistoricalObjectArray(
            snapshot,
            "entities",
            schemaVersion,
            "snapshot.entities");
        for (int index = 0; index < entities.Count; index++)
        {
            _ = RequireHistoricalObjectArray(
                entities[index],
                "pendingWork",
                schemaVersion,
                $"snapshot.entities[{index}].pendingWork");
        }

        _ = RequireHistoricalObjectArray(snapshot, "pendingCommands", schemaVersion, "snapshot.pendingCommands");
        _ = RequireHistoricalObjectArray(snapshot, "systemVersions", schemaVersion, "snapshot.systemVersions");

        if (snapshot.ContainsKey("characters"))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 4 character data.");
        }

        if (schemaVersion < 3)
        {
            if (snapshot.ContainsKey("geography"))
            {
                throw new SaveCompatibilityException(
                    $"Schema {schemaVersion} unexpectedly contains schema 3 geography data.");
            }

            return;
        }

        ValidateHistoricalGeographyShape(
            RequireHistoricalObject(snapshot, "geography", schemaVersion, "snapshot.geography"),
            schemaVersion);
    }

    private static void ValidateHistoricalGeographyShape(JsonObject geography, int schemaVersion)
    {
        JsonObject graph = RequireHistoricalObject(
            geography,
            "graph",
            schemaVersion,
            "snapshot.geography.graph");
        _ = RequireHistoricalObjectArray(graph, "regions", schemaVersion, "snapshot.geography.graph.regions");
        _ = RequireHistoricalObjectArray(graph, "districts", schemaVersion, "snapshot.geography.graph.districts");
        _ = RequireHistoricalObjectArray(graph, "localities", schemaVersion, "snapshot.geography.graph.localities");

        IReadOnlyList<JsonObject> stops = RequireHistoricalObjectArray(
            graph,
            "stops",
            schemaVersion,
            "snapshot.geography.graph.stops");
        for (int index = 0; index < stops.Count; index++)
        {
            _ = RequireHistoricalArray(
                stops[index],
                "battleFronts",
                schemaVersion,
                $"snapshot.geography.graph.stops[{index}].battleFronts");
        }

        IReadOnlyList<JsonObject> graphRoutes = RequireHistoricalObjectArray(
            graph,
            "routes",
            schemaVersion,
            "snapshot.geography.graph.routes");
        for (int index = 0; index < graphRoutes.Count; index++)
        {
            JsonObject route = graphRoutes[index];
            _ = RequireHistoricalArray(
                route,
                "permittedModes",
                schemaVersion,
                $"snapshot.geography.graph.routes[{index}].permittedModes");
            _ = RequireHistoricalObjectArray(
                route,
                "modifiers",
                schemaVersion,
                $"snapshot.geography.graph.routes[{index}].modifiers");
        }

        IReadOnlyList<JsonObject> locations = RequireHistoricalObjectArray(
            geography,
            "locations",
            schemaVersion,
            "snapshot.geography.locations");
        for (int index = 0; index < locations.Count; index++)
        {
            JsonObject location = locations[index];
            _ = RequireHistoricalObjectArray(
                location,
                "claims",
                schemaVersion,
                $"snapshot.geography.locations[{index}].claims");
            _ = RequireHistoricalObjectArray(
                location,
                "intelligence",
                schemaVersion,
                $"snapshot.geography.locations[{index}].intelligence");
        }

        IReadOnlyList<JsonObject> routes = RequireHistoricalObjectArray(
            geography,
            "routes",
            schemaVersion,
            "snapshot.geography.routes");
        for (int index = 0; index < routes.Count; index++)
        {
            _ = RequireHistoricalArray(
                routes[index],
                "permittedFactionIds",
                schemaVersion,
                $"snapshot.geography.routes[{index}].permittedFactionIds");
        }

        IReadOnlyList<JsonObject> armies = RequireHistoricalObjectArray(
            geography,
            "armies",
            schemaVersion,
            "snapshot.geography.armies");
        for (int index = 0; index < armies.Count; index++)
        {
            _ = RequireHistoricalArray(
                armies[index],
                "plannedRouteIds",
                schemaVersion,
                $"snapshot.geography.armies[{index}].plannedRouteIds");
        }
    }

    private static JsonObject RequireHistoricalObject(
        JsonObject parent,
        string property,
        int schemaVersion,
        string path)
    {
        if (parent[property] is not JsonObject value)
        {
            throw MalformedHistoricalShape(schemaVersion, path);
        }

        return value;
    }

    private static JsonArray RequireHistoricalArray(
        JsonObject parent,
        string property,
        int schemaVersion,
        string path)
    {
        if (parent[property] is not JsonArray value)
        {
            throw MalformedHistoricalShape(schemaVersion, path);
        }

        return value;
    }

    private static IReadOnlyList<JsonObject> RequireHistoricalObjectArray(
        JsonObject parent,
        string property,
        int schemaVersion,
        string path)
    {
        JsonArray array = RequireHistoricalArray(parent, property, schemaVersion, path);
        if (array.Any(node => node is not JsonObject))
        {
            throw MalformedHistoricalShape(schemaVersion, path);
        }

        return array.Select(node => (JsonObject)node!).ToArray();
    }

    private static SaveCompatibilityException MalformedHistoricalShape(int schemaVersion, string path) =>
        new($"Save schema {schemaVersion} contains malformed required historical snapshot data at '{path}'.");

    private static void ValidateCurrentSaveShape(JsonObject source)
    {
        string[] requiredEnvelopeArrays =
        [
            "contentManifests",
            "diagnosticCommands",
            "diagnosticEvents",
        ];
        if (requiredEnvelopeArrays.Any(property => source[property] is not JsonArray))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains missing or null required envelope collections.");
        }

        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["characters"] is not JsonObject characters)
        {
            throw new SaveCompatibilityException("Current save schema is missing required 'snapshot.characters' data.");
        }

        string[] requiredSnapshotArrays =
        [
            "randomStreams",
            "entities",
            "pendingCommands",
            "systemVersions",
        ];
        if (requiredSnapshotArrays.Any(property => snapshot[property] is not JsonArray)
            || snapshot["calendar"] is not JsonObject
            || snapshot["geography"] is not JsonObject)
        {
            throw new SaveCompatibilityException(
                "Current save schema contains missing or null required snapshot fields.");
        }

        string[] requiredArrays =
        [
            "identityDefinitions",
            "characterDefinitions",
            "familyDefinitions",
            "householdDefinitions",
            "characterStates",
            "familyStates",
            "householdStates",
        ];
        if (characters["contractVersion"] is not JsonValue contractVersion
            || !contractVersion.TryGetValue(out int version)
            || version != CharacterContractVersions.Snapshot
            || requiredArrays.Any(property => characters[property] is not JsonArray))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains missing, null, or unsupported character snapshot fields.");
        }

        if (snapshot["systemVersions"] is not JsonArray systemVersions
            || !systemVersions.Any(IsCurrentCharacterSystemVersion))
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required 'simulation.characters@1' system-version data.");
        }
    }

    private static bool IsCurrentCharacterSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && systemVersion["systemId"] is JsonValue systemIdValue
        && systemIdValue.TryGetValue(out string? systemId)
        && StringComparer.Ordinal.Equals(systemId, "simulation.characters")
        && systemVersion["version"] is JsonValue versionValue
        && versionValue.TryGetValue(out int version)
        && version == CharacterContractVersions.Snapshot;
}

public sealed class SaveMigrationV1ToV2 : ISaveMigration
{
    public int FromSchemaVersion => 1;

    public int ToSchemaVersion => 2;

    public JsonObject Migrate(JsonObject source)
    {
        if (source.ContainsKey("diagnosticEvents"))
        {
            throw new SaveCompatibilityException("Schema 1 unexpectedly contains schema 2 diagnostic event data.");
        }

        source["diagnosticEvents"] = new JsonArray();
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV2ToV3 : ISaveMigration
{
    public int FromSchemaVersion => 2;

    public int ToSchemaVersion => 3;

    public JsonObject Migrate(JsonObject source)
    {
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException("Schema 2 save is missing snapshot system versions.");
        }

        if (snapshot.ContainsKey("geography")
            || systemVersions.Any(node => node?["systemId"]?.GetValue<string>() == "simulation.geography"))
        {
            throw new SaveCompatibilityException("Schema 2 unexpectedly contains schema 3 geography data.");
        }

        snapshot["geography"] = System.Text.Json.JsonSerializer.SerializeToNode(
            GeographicWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.geography",
            ["version"] = 1,
        });
        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated geography snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(migratedSnapshot, ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV3ToV4 : ISaveMigration
{
    public int FromSchemaVersion => 3;

    public int ToSchemaVersion => 4;

    public JsonObject Migrate(JsonObject source)
    {
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException("Schema 3 save is missing snapshot system versions.");
        }

        if (snapshot.ContainsKey("characters")
            || systemVersions.Any(node => node?["systemId"]?.GetValue<string>() == "simulation.characters"))
        {
            throw new SaveCompatibilityException("Schema 3 unexpectedly contains schema 4 character data.");
        }

        snapshot["characters"] = JsonSerializer.SerializeToNode(
            CharacterWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.characters",
            ["version"] = 1,
        });
        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated character snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}
