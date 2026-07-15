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
            new SaveMigrationV4ToV5(),
            new SaveMigrationV5ToV6(),
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

    internal static void ValidateHistoricalSourceChecksum(JsonObject source, int schemaVersion)
    {
        if (schemaVersion is < 1 or > 5)
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
        IReadOnlyList<JsonObject> systemVersions = RequireHistoricalObjectArray(
            snapshot,
            "systemVersions",
            schemaVersion,
            "snapshot.systemVersions");

        if (schemaVersion < 5
            && (snapshot.ContainsKey("relationships")
                || systemVersions.Any(version => IsSystemId(version, "simulation.relationships"))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 5 relationship data.");
        }

        if (schemaVersion == 5)
        {
            ValidateRelationshipSnapshotShape(
                RequireHistoricalObject(snapshot, "relationships", schemaVersion, "snapshot.relationships"),
                $"Save schema {schemaVersion}");
            if (!systemVersions.Any(version => IsSystemVersion(
                    version,
                    "simulation.relationships",
                    RelationshipContractVersions.Snapshot)))
            {
                throw new SaveCompatibilityException(
                    "Save schema 5 is missing required 'simulation.relationships@1' system-version data.");
            }
        }

        if (schemaVersion < 4 && snapshot.ContainsKey("characters"))
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

        }
        else
        {
            ValidateHistoricalGeographyShape(
                RequireHistoricalObject(snapshot, "geography", schemaVersion, "snapshot.geography"),
                schemaVersion);
        }

        if (schemaVersion < 4)
        {
            return;
        }

        JsonObject characters = RequireHistoricalObject(
            snapshot,
            "characters",
            schemaVersion,
            "snapshot.characters");
        ValidateCharacterSnapshotShape(
            characters,
            $"Save schema {schemaVersion}",
            CharacterContractVersions.LegacySnapshot,
            requireVersionTwoFields: false);
        if (!systemVersions.Any(version => IsSystemVersion(
                version,
                "simulation.characters",
                CharacterContractVersions.LegacySnapshot)))
        {
            throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} is missing required 'simulation.characters@1' system-version data.");
        }
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
            || snapshot["characters"] is not JsonObject characters
            || snapshot["relationships"] is not JsonObject relationships)
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required character or relationship snapshot data.");
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

        ValidateCharacterSnapshotShape(
            characters,
            "Current save schema",
            CharacterContractVersions.Snapshot,
            requireVersionTwoFields: true);
        ValidateRelationshipSnapshotShape(relationships, "Current save schema");

        if (snapshot["systemVersions"] is not JsonArray systemVersions
            || !systemVersions.Any(IsCurrentCharacterSystemVersion))
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required 'simulation.characters@2' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentRelationshipSystemVersion))
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required 'simulation.relationships@1' system-version data.");
        }
    }

    private static void ValidateCharacterSnapshotShape(
        JsonObject characters,
        string context,
        int expectedVersion,
        bool requireVersionTwoFields)
    {
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
            || version != expectedVersion
            || requiredArrays.Any(property => characters[property] is not JsonArray))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character snapshot fields.");
        }

        ValidateVersionedCharacterEntries(
            characters,
            context,
            "identityDefinitions",
            expectedVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "characterDefinitions",
            expectedVersion,
            requireVersionTwoFields
                ? ["structuredName", "contentOrigin", "flawIds"]
                : []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "familyDefinitions",
            expectedVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "householdDefinitions",
            expectedVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "characterStates",
            expectedVersion,
            requireVersionTwoFields ? ["parentLinks", "condition"] : []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "familyStates",
            expectedVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "householdStates",
            expectedVersion,
            []);

        ValidateCommonCharacterEntryShape(characters, context);

        if (requireVersionTwoFields)
        {
            ValidateCharacterV2EntryShape(characters, context);
        }
        else
        {
            RejectLegacyCharacterV2Fields(characters, context);
        }
    }

    private static void ValidateCommonCharacterEntryShape(JsonObject characters, string context)
    {
        foreach (JsonObject identity in characters["identityDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(identity, "id", context, "identity definition");
            RequireCharacterInteger(identity, "kind", context, "identity definition");
            RequireCharacterObject(identity, "nameKey", context, "identity definition");
        }

        foreach (JsonObject definition in characters["characterDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(definition, "id", context, "character definition");
            RequireCharacterObject(definition, "nameKey", context, "character definition");
            RequireCharacterObject(definition, "birthDate", context, "character definition");
            RequireCharacterArray(definition, "abilityIds", context, "character definition");
            RequireCharacterArray(definition, "aptitudeIds", context, "character definition");
            RequireCharacterArray(definition, "traitIds", context, "character definition");
            RequireCharacterArray(definition, "ambitionIds", context, "character definition");
            RequireCharacterArray(definition, "reputationIds", context, "character definition");
        }

        foreach (JsonObject definition in characters["familyDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(definition, "id", context, "family definition");
            RequireCharacterObject(definition, "nameKey", context, "family definition");
        }

        foreach (JsonObject definition in characters["householdDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(definition, "id", context, "household definition");
            RequireCharacterObject(definition, "nameKey", context, "household definition");
        }

        foreach (JsonObject state in characters["characterStates"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(state, "characterId", context, "character state");
            RequireCharacterArray(state, "parentIds", context, "character state");
        }

        foreach (JsonObject state in characters["familyStates"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(state, "familyId", context, "family state");
            RequireCharacterArray(state, "memberIds", context, "family state");
        }

        foreach (JsonObject state in characters["householdStates"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterObject(state, "householdId", context, "household state");
            RequireCharacterObject(state, "headCharacterId", context, "household state");
            RequireCharacterArray(state, "memberIds", context, "household state");
        }
    }

    private static void ValidateCharacterV2EntryShape(JsonObject characters, string context)
    {
        foreach (JsonObject definition in characters["characterDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            RequireCharacterArray(definition, "flawIds", context, "character definition");
            RequireCharacterNullableObject(definition, "cultureId", context, "character definition");
            RequireCharacterNullableObject(definition, "originLocationId", context, "character definition");

            JsonObject structuredName = RequireCharacterObject(
                definition,
                "structuredName",
                context,
                "character definition");
            RequireCharacterObject(structuredName, "primaryNameKey", context, "structured character name");
            RequireCharacterNullableObject(
                structuredName,
                "courtesyNameKey",
                context,
                "structured character name");

            JsonObject contentOrigin = RequireCharacterObject(
                definition,
                "contentOrigin",
                context,
                "character definition");
            RequireCharacterInteger(contentOrigin, "originKind", context, "character content origin");
            RequireCharacterNullableInteger(
                contentOrigin,
                "historicalClassification",
                context,
                "character content origin");
            RequireCharacterObject(contentOrigin, "recordId", context, "character content origin");
            RequireCharacterNullableObject(contentOrigin, "owningPackId", context, "character content origin");
            RequireCharacterArray(
                contentOrigin,
                "appliedOverridePackIds",
                context,
                "character content origin");
            RequireCharacterArray(contentOrigin, "sourceIds", context, "character content origin");
        }

        foreach (JsonObject state in characters["characterStates"]!.AsArray().OfType<JsonObject>())
        {
            JsonArray parentLinks = RequireCharacterArray(state, "parentLinks", context, "character state");
            foreach (JsonNode? node in parentLinks)
            {
                if (node is not JsonObject parentLink)
                {
                    throw MalformedCharacterData(context, "parent link");
                }

                RequireCharacterObject(parentLink, "parentCharacterId", context, "parent link");
                RequireCharacterInteger(parentLink, "kind", context, "parent link");
            }

            JsonObject condition = RequireCharacterObject(state, "condition", context, "character state");
            RequireCharacterInteger(condition, "vitalStatus", context, "character condition");
            RequireCharacterInteger(condition, "healthStatus", context, "character condition");
            RequireCharacterBoolean(condition, "isIncapacitated", context, "character condition");
            RequireCharacterInteger(condition, "custodyStatus", context, "character condition");
            RequireCharacterNullableObject(condition, "custodianId", context, "character condition");
        }
    }

    private static void RejectLegacyCharacterV2Fields(JsonObject characters, string context)
    {
        foreach (JsonObject identity in characters["identityDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            if (identity["kind"] is not JsonValue kindValue
                || !kindValue.TryGetValue(out int kind)
                || kind is < (int)CharacterIdentityKind.Ability or > (int)CharacterIdentityKind.Reputation)
            {
                throw new SaveCompatibilityException(
                    $"{context} character contract v1 contains a non-v1 identity kind.");
            }
        }

        string[] definitionFields =
        [
            "structuredName",
            "contentOrigin",
            "cultureId",
            "originLocationId",
            "flawIds",
        ];
        foreach (JsonObject definition in characters["characterDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            if (definitionFields.Any(definition.ContainsKey))
            {
                throw new SaveCompatibilityException(
                    $"{context} character contract v1 unexpectedly contains contract-v2 descriptor data.");
            }
        }

        foreach (JsonObject state in characters["characterStates"]!.AsArray().OfType<JsonObject>())
        {
            if (state.ContainsKey("parentLinks") || state.ContainsKey("condition"))
            {
                throw new SaveCompatibilityException(
                    $"{context} character contract v1 unexpectedly contains contract-v2 state data.");
            }
        }
    }

    private static void ValidateVersionedCharacterEntries(
        JsonObject characters,
        string context,
        string property,
        int expectedVersion,
        IReadOnlyList<string> requiredV2Properties)
    {
        JsonArray entries = characters[property]!.AsArray();
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JsonObject entry
                || entry["contractVersion"] is not JsonValue versionValue
                || !versionValue.TryGetValue(out int version)
                || version != expectedVersion
                || requiredV2Properties.Any(required => entry[required] is null))
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or unsupported character data at '{property}[{index}]'.");
            }
        }
    }

    private static void ValidateRelationshipSnapshotShape(JsonObject relationships, string context)
    {
        if (relationships["contractVersion"] is not JsonValue contractVersion
            || !contractVersion.TryGetValue(out int version)
            || version != RelationshipContractVersions.Snapshot
            || relationships["subjects"] is not JsonArray subjects
            || subjects.Any(node => node is not JsonObject))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported relationship snapshot fields.");
        }

        foreach (JsonObject subject in subjects.OfType<JsonObject>())
        {
            if (subject["detailedRelationships"] is not JsonArray detailedRelationships
                || subject["archivedRelationships"] is not JsonArray archivedRelationships
                || subject["distantHistory"] is not JsonObject)
            {
                throw MalformedRelationshipData(context);
            }

            foreach (JsonNode? node in detailedRelationships)
            {
                if (node is not JsonObject relationship
                    || relationship["dimensions"] is not JsonObject
                    || relationship["memories"] is not JsonArray memories
                    || relationship["foldedMemories"] is not JsonObject)
                {
                    throw MalformedRelationshipData(context);
                }

                foreach (JsonNode? memoryNode in memories)
                {
                    if (memoryNode is not JsonObject memory
                        || memory["witnessIds"] is not JsonArray
                        || memory["appliedImpact"] is not JsonObject)
                    {
                        throw MalformedRelationshipData(context);
                    }
                }
            }

            foreach (JsonNode? node in archivedRelationships)
            {
                if (node is not JsonObject relationship
                    || relationship["dimensions"] is not JsonObject
                    || relationship["foldedMemories"] is not JsonObject)
                {
                    throw MalformedRelationshipData(context);
                }
            }
        }
    }

    private static JsonObject RequireCharacterObject(
        JsonObject owner,
        string property,
        string context,
        string description)
    {
        if (owner[property] is JsonObject value)
        {
            return value;
        }

        throw MalformedCharacterData(context, description);
    }

    private static JsonArray RequireCharacterArray(
        JsonObject owner,
        string property,
        string context,
        string description)
    {
        if (owner[property] is JsonArray value)
        {
            return value;
        }

        throw MalformedCharacterData(context, description);
    }

    private static void RequireCharacterInteger(
        JsonObject owner,
        string property,
        string context,
        string description)
    {
        if (owner[property] is not JsonValue value || !value.TryGetValue(out int _))
        {
            throw MalformedCharacterData(context, description);
        }
    }

    private static void RequireCharacterBoolean(
        JsonObject owner,
        string property,
        string context,
        string description)
    {
        if (owner[property] is not JsonValue value || !value.TryGetValue(out bool _))
        {
            throw MalformedCharacterData(context, description);
        }
    }

    private static void RequireCharacterNullableInteger(
        JsonObject owner,
        string property,
        string context,
        string description)
    {
        if (!owner.ContainsKey(property)
            || (owner[property] is not null
                && (owner[property] is not JsonValue value || !value.TryGetValue(out int _))))
        {
            throw MalformedCharacterData(context, description);
        }
    }

    private static void RequireCharacterNullableObject(
        JsonObject owner,
        string property,
        string context,
        string description)
    {
        if (!owner.ContainsKey(property)
            || (owner[property] is not null && owner[property] is not JsonObject))
        {
            throw MalformedCharacterData(context, description);
        }
    }

    private static SaveCompatibilityException MalformedCharacterData(string context, string description) =>
        new(
            $"{context} contains missing, null, or malformed required {description} data.");

    private static SaveCompatibilityException MalformedRelationshipData(string context) =>
        new(
            $"{context} contains missing, null, or malformed required nested relationship data.");

    private static bool IsCurrentCharacterSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && systemVersion["systemId"] is JsonValue systemIdValue
        && systemIdValue.TryGetValue(out string? systemId)
        && StringComparer.Ordinal.Equals(systemId, "simulation.characters")
        && systemVersion["version"] is JsonValue versionValue
        && versionValue.TryGetValue(out int version)
        && version == CharacterContractVersions.Snapshot;

    private static bool IsCurrentRelationshipSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(systemVersion, "simulation.relationships", RelationshipContractVersions.Snapshot);

    private static bool IsSystemVersion(JsonObject systemVersion, string systemId, int expectedVersion) =>
        IsSystemId(systemVersion, systemId)
        && systemVersion["version"] is JsonValue versionValue
        && versionValue.TryGetValue(out int version)
        && version == expectedVersion;

    private static bool IsSystemId(JsonObject systemVersion, string systemId) =>
        systemVersion["systemId"] is JsonValue systemIdValue
        && systemIdValue.TryGetValue(out string? actualSystemId)
        && StringComparer.Ordinal.Equals(actualSystemId, systemId);
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
            CharacterWorldSnapshot.LegacyV1Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.characters",
            ["version"] = 1,
        });
        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated character snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(migratedSnapshot, ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV4ToV5 : ISaveMigration
{
    public int FromSchemaVersion => 4;

    public int ToSchemaVersion => 5;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException("Schema 4 save is missing snapshot system versions.");
        }

        if (snapshot.ContainsKey("relationships")
            || systemVersions.Any(node => node?["systemId"]?.GetValue<string>() == "simulation.relationships"))
        {
            throw new SaveCompatibilityException("Schema 4 unexpectedly contains schema 5 relationship data.");
        }

        snapshot["relationships"] = JsonSerializer.SerializeToNode(
            RelationshipWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.relationships",
            ["version"] = 1,
        });
        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated relationship snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(migratedSnapshot, ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV5ToV6 : ISaveMigration
{
    public int FromSchemaVersion => 5;

    public int ToSchemaVersion => 6;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["characters"] is not JsonObject characterNode
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException("Schema 5 save is missing character or system-version data.");
        }

        CharacterWorldSnapshot legacy = characterNode.Deserialize<CharacterWorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Schema 5 character snapshot is empty.");
        CharacterWorldSnapshot migratedCharacters = new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            legacy.IdentityDefinitions.Select(definition => definition with
            {
                ContractVersion = CharacterContractVersions.Definition,
            }).ToArray(),
            legacy.CharacterDefinitions.Select(definition => definition with
            {
                ContractVersion = CharacterContractVersions.Definition,
                StructuredName = new StructuredCharacterName(definition.NameKey, null),
                ContentOrigin = CharacterContentOrigin.LegacyUnknown(definition.Id),
                CultureId = null,
                OriginLocationId = null,
                FlawIds = [],
            }).ToArray(),
            legacy.FamilyDefinitions.Select(definition => definition with
            {
                ContractVersion = CharacterContractVersions.Definition,
            }).ToArray(),
            legacy.HouseholdDefinitions.Select(definition => definition with
            {
                ContractVersion = CharacterContractVersions.Definition,
            }).ToArray(),
            legacy.CharacterStates.Select(state => state with
            {
                ContractVersion = CharacterContractVersions.State,
                ParentLinks = state.ParentIds
                    .Select(parentId => new CharacterParentLink(parentId, ParentChildLinkKind.UnspecifiedLegacy))
                    .ToArray(),
                Condition = CharacterConditionState.Default,
            }).ToArray(),
            legacy.FamilyStates.Select(state => state with
            {
                ContractVersion = CharacterContractVersions.State,
            }).ToArray(),
            legacy.HouseholdStates.Select(state => state with
            {
                ContractVersion = CharacterContractVersions.State,
            }).ToArray()).Canonicalize();

        snapshot["characters"] = JsonSerializer.SerializeToNode(
            migratedCharacters,
            SimulationJson.CreateOptions());

        JsonObject characterVersion = systemVersions
            .OfType<JsonObject>()
            .SingleOrDefault(node => IsCharacterSystemVersion(node, CharacterContractVersions.LegacySnapshot))
            ?? throw new SaveCompatibilityException(
                "Schema 5 is missing required 'simulation.characters@1' system-version data.");
        characterVersion["version"] = CharacterContractVersions.Snapshot;

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 6 snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static bool IsCharacterSystemVersion(JsonObject node, int version) =>
        node["systemId"]?.GetValue<string>() == "simulation.characters"
        && node["version"]?.GetValue<int>() == version;
}
