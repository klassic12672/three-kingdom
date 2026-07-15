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
            new SaveMigrationV6ToV7(),
            new SaveMigrationV7ToV8(),
            new SaveMigrationV8ToV9(),
            new SaveMigrationV9ToV10(),
            new SaveMigrationV10ToV11(),
            new SaveMigrationV11ToV12(),
            new SaveMigrationV12ToV13(),
            new SaveMigrationV13ToV14(),
            new SaveMigrationV14ToV15(),
            new SaveMigrationV15ToV16(),
            new SaveMigrationV16ToV17(),
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
        if (schemaVersion is < 1 or > 16)
        {
            throw new SaveCompatibilityException($"Save schema {schemaVersion} has no historical checksum contract.");
        }

        ValidateHistoricalSourceShape(source, schemaVersion);
        string storedChecksum = source["checksum"]?.GetValue<string>()
            ?? throw new SaveCompatibilityException($"Save schema {schemaVersion} is missing its checksum.");
        WorldSnapshot snapshot = DeserializeHistoricalSnapshotForChecksum(
            source["snapshot"] as JsonObject
                ?? throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} is missing its authoritative snapshot."),
            schemaVersion);
        string actualChecksum = SimulationChecksum.ComputeForSaveSchema(snapshot, schemaVersion).Value;
        if (!StringComparer.Ordinal.Equals(storedChecksum, actualChecksum))
        {
            throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} checksum does not match its authoritative snapshot "
                + $"(stored {storedChecksum}, actual {actualChecksum}).");
        }
    }

    internal static WorldSnapshot DeserializeHistoricalSnapshotForChecksum(
        JsonObject historicalSnapshot,
        int schemaVersion)
    {
        JsonObject compatible = (JsonObject)historicalSnapshot.DeepClone();
        if (schemaVersion is >= 5 and < 7
            && compatible["relationships"] is JsonObject relationships)
        {
            UpgradeLegacyRelationshipSnapshot(relationships);
        }

        return compatible.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} is missing its authoritative snapshot.");
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

        if (schemaVersion < 15
            && (snapshot.ContainsKey("characterGuardianships")
                || systemVersions.Any(version => IsSystemId(
                    version,
                    CharacterGuardianshipSystem.SystemId))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 15 character-guardianship data.");
        }

        if (schemaVersion >= 15)
        {
            ValidateCharacterGuardianshipSnapshotShape(
                RequireHistoricalObject(
                    snapshot,
                    "characterGuardianships",
                    schemaVersion,
                    "snapshot.characterGuardianships"),
                $"Save schema {schemaVersion}");
            JsonObject[] guardianshipSystemVersions = systemVersions
                .Where(version => IsSystemId(
                    version,
                    CharacterGuardianshipSystem.SystemId))
                .ToArray();
            if (guardianshipSystemVersions.Length != 1
                || !IsSystemVersion(
                    guardianshipSystemVersions[0],
                    CharacterGuardianshipSystem.SystemId,
                    CharacterGuardianshipSystem.Version))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} requires exactly one '{CharacterGuardianshipSystem.SystemId}@{CharacterGuardianshipSystem.Version}' system-version registration.");
            }
        }

        if (schemaVersion < 10
            && (snapshot.ContainsKey("characterMarriages")
                || systemVersions.Any(version => IsSystemId(
                    version,
                    CharacterMarriageSystem.SystemId))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 10 character-marriage data.");
        }

        if (schemaVersion is >= 10 and <= 16)
        {
            ValidateCharacterMarriageSnapshotShape(
                RequireHistoricalObject(
                    snapshot,
                    "characterMarriages",
                    schemaVersion,
                    "snapshot.characterMarriages"),
                $"Save schema {schemaVersion}",
                expectedSnapshotVersion: schemaVersion >= 12
                    ? CharacterMarriageContractVersions.Snapshot
                    : 1,
                requireInvitations: schemaVersion >= 12,
                allowVersionTwoRoutes: schemaVersion >= 12);
            JsonObject[] marriageSystemVersions = systemVersions
                .Where(version => IsSystemId(version, CharacterMarriageSystem.SystemId))
                .ToArray();
            int expectedMarriageSystemVersion = schemaVersion >= 12
                ? CharacterMarriageSystem.Version
                : 1;
            if (marriageSystemVersions.Length != 1
                || !IsSystemVersion(
                    marriageSystemVersions[0],
                    CharacterMarriageSystem.SystemId,
                    expectedMarriageSystemVersion))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} requires exactly one '{CharacterMarriageSystem.SystemId}@{expectedMarriageSystemVersion}' system-version registration.");
            }

            JsonArray pendingCommands = RequireHistoricalArray(
                snapshot,
                "pendingCommands",
                schemaVersion,
                "snapshot.pendingCommands");
            JsonArray diagnosticCommands = RequireHistoricalArray(
                source,
                "diagnosticCommands",
                schemaVersion,
                "diagnosticCommands");
            JsonArray diagnosticEvents = RequireHistoricalArray(
                source,
                "diagnosticEvents",
                schemaVersion,
                "diagnosticEvents");
            if (schemaVersion == 10)
            {
                RejectD1Discriminators(pendingCommands, "snapshot pending commands");
                RejectD1Discriminators(diagnosticCommands, "diagnostic commands");
                RejectD1Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 11)
            {
                RejectD2Discriminators(pendingCommands, "snapshot pending commands");
                RejectD2Discriminators(diagnosticCommands, "diagnostic commands");
                RejectD2Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 12)
            {
                RejectD3Discriminators(pendingCommands, "snapshot pending commands");
                RejectD3Discriminators(diagnosticCommands, "diagnostic commands");
                RejectD3Discriminators(diagnosticEvents, "diagnostic events");
                RejectD3RelationshipSourceKinds(
                    snapshot,
                    "authoritative snapshot");
                RejectD3RelationshipSourceKinds(
                    diagnosticEvents,
                    "diagnostic events");
            }
            else if (schemaVersion == 13)
            {
                RejectE0Discriminators(pendingCommands, "snapshot pending commands");
                RejectE0Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE0Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 14)
            {
                RejectE1Discriminators(pendingCommands, "snapshot pending commands");
                RejectE1Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE1Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 15)
            {
                RejectE2Discriminators(pendingCommands, "snapshot pending commands");
                RejectE2Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE2Discriminators(diagnosticEvents, "diagnostic events");
            }
            else
            {
                RejectE3Discriminators(pendingCommands, "snapshot pending commands");
                RejectE3Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE3Discriminators(diagnosticEvents, "diagnostic events");
            }
        }

        if (schemaVersion < 9
            && (snapshot.ContainsKey("characterEstateHoldings")
            || systemVersions.Any(version => IsSystemId(
                    version,
                    CharacterEstateHoldingSystem.SystemId))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 9 character-estate-holding data.");
        }

        if (schemaVersion >= 9)
        {
            ValidateCharacterEstateHoldingSnapshotShape(
                RequireHistoricalObject(
                    snapshot,
                    "characterEstateHoldings",
                    schemaVersion,
                    "snapshot.characterEstateHoldings"),
                $"Save schema {schemaVersion}");
            if (!systemVersions.Any(version => IsSystemVersion(
                    version,
                    CharacterEstateHoldingSystem.SystemId,
                    CharacterEstateHoldingSystem.Version)))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} is missing required '{CharacterEstateHoldingSystem.SystemId}@{CharacterEstateHoldingSystem.Version}' system-version data.");
            }
        }

        if (schemaVersion < 8
            && (snapshot.ContainsKey("characterResources")
                || systemVersions.Any(version => IsSystemId(
                    version,
                    CharacterResourceSystem.SystemId))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 8 character-resource data.");
        }

        if (schemaVersion >= 8)
        {
            ValidateCharacterResourceSnapshotShape(
                RequireHistoricalObject(
                    snapshot,
                    "characterResources",
                    schemaVersion,
                    "snapshot.characterResources"),
                "Save schema 8");
            if (!systemVersions.Any(version => IsSystemVersion(
                    version,
                    CharacterResourceSystem.SystemId,
                    CharacterResourceSystem.Version)))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} is missing required '{CharacterResourceSystem.SystemId}@{CharacterResourceSystem.Version}' system-version data.");
            }
        }

        if (schemaVersion < 7
            && (snapshot.ContainsKey("careers")
                || systemVersions.Any(version => IsSystemId(
                    version,
                    "simulation.character_careers"))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 7 character-career data.");
        }

        if (schemaVersion >= 7)
        {
            ValidateCareerSnapshotShape(
                RequireHistoricalObject(snapshot, "careers", schemaVersion, "snapshot.careers"),
                $"Save schema {schemaVersion}");
            if (!systemVersions.Any(version => IsSystemVersion(
                    version,
                    "simulation.character_careers",
                    CareerContractVersions.Snapshot)))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} is missing required character-career system-version data.");
            }
        }

        if (schemaVersion < 5
            && (snapshot.ContainsKey("relationships")
                || systemVersions.Any(version => IsSystemId(version, "simulation.relationships"))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 5 relationship data.");
        }

        if (schemaVersion >= 5)
        {
            int expectedRelationshipVersion = schemaVersion >= 7
                ? RelationshipContractVersions.Snapshot
                : RelationshipContractVersions.LegacySnapshot;
            ValidateRelationshipSnapshotShape(
                RequireHistoricalObject(snapshot, "relationships", schemaVersion, "snapshot.relationships"),
                $"Save schema {schemaVersion}",
                expectedRelationshipVersion,
                requireVersionTwoMemoryFields: schemaVersion >= 7);
            if (!systemVersions.Any(version => IsSystemVersion(
                    version,
                    "simulation.relationships",
                    expectedRelationshipVersion)))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} is missing required relationship system-version data.");
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
            schemaVersion >= 6
                ? CharacterContractVersions.Snapshot
                : CharacterContractVersions.LegacySnapshot,
            requireVersionTwoFields: schemaVersion >= 6);
        if (!systemVersions.Any(version => IsSystemVersion(
                version,
                "simulation.characters",
                schemaVersion >= 6
                    ? CharacterContractVersions.Snapshot
                    : CharacterContractVersions.LegacySnapshot)))
        {
            throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} is missing required character system-version data.");
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
            || snapshot["relationships"] is not JsonObject relationships
            || snapshot["careers"] is not JsonObject careers
            || snapshot["characterResources"] is not JsonObject characterResources
            || snapshot["characterEstateHoldings"] is not JsonObject characterEstateHoldings
            || snapshot["characterMarriages"] is not JsonObject characterMarriages
            || snapshot["characterGuardianships"] is not JsonObject characterGuardianships)
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required character, relationship, career, character-resource, character-estate-holding, character-marriage, or character-guardianship snapshot data.");
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
        ValidateRelationshipSnapshotShape(
            relationships,
            "Current save schema",
            RelationshipContractVersions.Snapshot,
            requireVersionTwoMemoryFields: true);
        ValidateCareerSnapshotShape(careers, "Current save schema");
        ValidateCharacterResourceSnapshotShape(characterResources, "Current save schema");
        ValidateCharacterEstateHoldingSnapshotShape(
            characterEstateHoldings,
            "Current save schema");
        ValidateCharacterMarriageSnapshotShape(
            characterMarriages,
            "Current save schema",
            CharacterMarriageContractVersions.Snapshot,
            requireInvitations: true,
            allowVersionTwoRoutes: true);
        ValidateCharacterGuardianshipSnapshotShape(
            characterGuardianships,
            "Current save schema");

        if (snapshot["systemVersions"] is not JsonArray systemVersions
            || !systemVersions.Any(IsCurrentCharacterSystemVersion))
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required 'simulation.characters@2' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentRelationshipSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required 'simulation.relationships@{RelationshipContractVersions.Snapshot}' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentCareerSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required 'simulation.character_careers@{CareerContractVersions.Snapshot}' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentCharacterResourceSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required '{CharacterResourceSystem.SystemId}@{CharacterResourceSystem.Version}' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentCharacterEstateHoldingSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required '{CharacterEstateHoldingSystem.SystemId}@{CharacterEstateHoldingSystem.Version}' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentCharacterMarriageSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required '{CharacterMarriageSystem.SystemId}@{CharacterMarriageSystem.Version}' system-version data.");
        }

        if (!systemVersions.Any(IsCurrentCharacterGuardianshipSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required '{CharacterGuardianshipSystem.SystemId}@{CharacterGuardianshipSystem.Version}' system-version data.");
        }
    }

    private static void ValidateCharacterGuardianshipSnapshotShape(
        JsonObject characterGuardianships,
        string context)
    {
        if (!HasVersion(
                characterGuardianships,
                CharacterGuardianshipContractVersions.Snapshot)
            || characterGuardianships["guardianships"] is not JsonArray guardianships)
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character-guardianship snapshot data.");
        }

        foreach (JsonNode? node in guardianships)
        {
            if (node is not JsonObject guardianship
                || !HasVersion(
                    guardianship,
                    CharacterGuardianshipContractVersions.State)
                || !HasObject(guardianship, "guardianshipId")
                || !HasObject(guardianship, "wardCharacterId")
                || !HasObject(guardianship, "guardianCharacterId")
                || !HasObject(guardianship, "establishedDate")
                || !HasLong(guardianship, "establishedTurnIndex")
                || !HasObject(guardianship, "sourceCommandId")
                || !HasObject(guardianship, "sourceEventId")
                || !HasInt(guardianship, "status")
                || !HasNullableObject(guardianship, "endDate")
                || !HasNullableLong(guardianship, "endTurnIndex")
                || !HasNullableObject(guardianship, "endSourceCommandId")
                || !HasNullableObject(guardianship, "endSourceEventId")
                || !HasNullableInt(guardianship, "endReason"))
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or malformed character-guardianship record data.");
            }
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

    private static void ValidateRelationshipSnapshotShape(
        JsonObject relationships,
        string context,
        int expectedVersion,
        bool requireVersionTwoMemoryFields)
    {
        if (relationships["contractVersion"] is not JsonValue contractVersion
            || !contractVersion.TryGetValue(out int version)
            || version != expectedVersion
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
                        || memory["appliedImpact"] is not JsonObject
                        || memory["contractVersion"]?.GetValue<int>()
                            != (requireVersionTwoMemoryFields
                                ? RelationshipContractVersions.Memory
                                : RelationshipContractVersions.LegacyMemory))
                    {
                        throw MalformedRelationshipData(context);
                    }

                    string[] currentFields =
                    [
                        "sourceEventId",
                        "sourceKind",
                        "identityScheme",
                        "consequenceIndex",
                    ];
                    if (requireVersionTwoMemoryFields)
                    {
                        if (currentFields.Any(field => !memory.ContainsKey(field))
                            || memory.ContainsKey("sourceRelationshipActionEventId"))
                        {
                            throw MalformedRelationshipData(context);
                        }
                    }
                    else if (!memory.ContainsKey("sourceRelationshipActionEventId")
                        || currentFields.Any(memory.ContainsKey))
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

    private static void ValidateCareerSnapshotShape(JsonObject careers, string context)
    {
        string[] requiredArrays =
        [
            "proposals",
            "retinues",
            "retinueMemberships",
            "patronageBonds",
            "recommendations",
            "employmentTenures",
            "history",
        ];
        if (careers["contractVersion"]?.GetValue<int>() != CareerContractVersions.Snapshot
            || requiredArrays.Any(property => careers[property] is not JsonArray))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported career snapshot fields.");
        }

        foreach (string property in requiredArrays)
        {
            foreach (JsonNode? node in careers[property]!.AsArray())
            {
                if (node is not JsonObject entry
                    || entry["contractVersion"]?.GetValue<int>() != CareerContractVersions.State)
                {
                    throw new SaveCompatibilityException(
                        $"{context} contains malformed career data at '{property}'.");
                }
            }
        }
    }

    private static void ValidateCharacterResourceSnapshotShape(
        JsonObject characterResources,
        string context)
    {
        string[] requiredArrays =
        [
            "accounts",
            "ledgerEntries",
            "history",
        ];
        if (characterResources["contractVersion"]?.GetValue<int>()
                != CharacterResourceContractVersions.Snapshot
            || requiredArrays.Any(property => characterResources[property] is not JsonArray))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character-resource snapshot fields.");
        }

        foreach (JsonNode? node in characterResources["accounts"]!.AsArray())
        {
            if (node is not JsonObject account
                || !HasVersion(account, CharacterResourceContractVersions.State)
                || account["accountId"] is not JsonObject
                || account["characterId"] is not JsonObject
                || account["wealth"] is not JsonValue wealth
                || !wealth.TryGetValue(out long _))
            {
                throw MalformedCharacterResourceData(context, "wealth account");
            }
        }

        foreach (JsonNode? node in characterResources["ledgerEntries"]!.AsArray())
        {
            if (node is not JsonObject entry
                || !HasVersion(entry, CharacterResourceContractVersions.State)
                || entry["entryId"] is not JsonObject
                || entry["transferId"] is not JsonObject
                || entry["characterId"] is not JsonObject
                || entry["counterpartyCharacterId"] is not JsonObject
                || entry["direction"] is not JsonValue direction
                || !direction.TryGetValue(out int _)
                || entry["amount"] is not JsonValue amount
                || !amount.TryGetValue(out long _)
                || entry["resolutionDate"] is not JsonObject
                || entry["resolutionTurnIndex"] is not JsonValue turnIndex
                || !turnIndex.TryGetValue(out long _)
                || entry["sourceCommandId"] is not JsonObject
                || entry["sourceEventId"] is not JsonObject)
            {
                throw MalformedCharacterResourceData(context, "wealth-ledger entry");
            }
        }

        foreach (JsonNode? node in characterResources["history"]!.AsArray())
        {
            if (node is not JsonObject aggregate
                || !HasVersion(aggregate, CharacterResourceContractVersions.State)
                || aggregate["characterId"] is not JsonObject
                || !HasLong(aggregate, "foldedIncomingCount")
                || !HasLong(aggregate, "foldedIncomingAmount")
                || !HasLong(aggregate, "foldedOutgoingCount")
                || !HasLong(aggregate, "foldedOutgoingAmount")
                || !HasNullableObject(aggregate, "earliestDate")
                || !HasNullableObject(aggregate, "latestDate"))
            {
                throw MalformedCharacterResourceData(context, "wealth-history aggregate");
            }
        }
    }

    private static void ValidateCharacterEstateHoldingSnapshotShape(
        JsonObject characterEstateHoldings,
        string context)
    {
        if (characterEstateHoldings["contractVersion"]?.GetValue<int>()
                != CharacterEstateHoldingContractVersions.Snapshot
            || characterEstateHoldings["holdings"] is not JsonArray holdings)
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character-estate-holding snapshot fields.");
        }

        foreach (JsonNode? node in holdings)
        {
            if (node is not JsonObject holding
                || !HasVersion(holding, CharacterEstateHoldingContractVersions.State)
                || holding["estateId"] is not JsonObject
                || holding["ownerCharacterId"] is not JsonObject)
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or malformed required character-estate-holding data.");
            }
        }
    }

    private static void ValidateCharacterMarriageSnapshotShape(
        JsonObject characterMarriages,
        string context,
        int expectedSnapshotVersion,
        bool requireInvitations,
        bool allowVersionTwoRoutes)
    {
        string[] requiredArrays =
        [
            "practices",
            "proposals",
            "betrothals",
            "unions",
            "romanceRoutes",
            "history",
        ];
        if (requireInvitations)
        {
            requiredArrays = [.. requiredArrays, "invitations"];
        }

        if (characterMarriages["contractVersion"]?.GetValue<int>()
                != expectedSnapshotVersion
            || requiredArrays.Any(property => characterMarriages[property] is not JsonArray))
        {
            throw MalformedCharacterMarriageData(context, "snapshot fields");
        }

        if (requireInvitations)
        {
            foreach (JsonNode? node in characterMarriages["invitations"]!.AsArray())
            {
                if (node is not JsonObject invitation
                    || !HasVersion(
                        invitation,
                        CharacterMarriageContractVersions.RomanceInvitationState)
                    || !HasObject(invitation, "invitationId")
                    || !HasObject(invitation, "initiatorCharacterId")
                    || !HasObject(invitation, "recipientCharacterId")
                    || !HasObject(invitation, "practiceId")
                    || !HasObject(invitation, "createdDate")
                    || !HasLong(invitation, "createdTurnIndex")
                    || !HasObject(invitation, "sourceCommandId"))
                {
                    throw MalformedCharacterMarriageData(context, "romance invitation");
                }
            }
        }

        foreach (JsonNode? node in characterMarriages["practices"]!.AsArray())
        {
            if (node is not JsonObject practice
                || !HasVersion(practice, CharacterMarriageContractVersions.Practice)
                || !HasObject(practice, "practiceId")
                || !HasInt(practice, "minimumLegalUnionAge")
                || !HasInt(practice, "minimumRomanceAge")
                || !HasInt(practice, "maximumActivePrincipalSpousesPerCharacter")
                || !HasInt(practice, "maximumActiveConcubinageUnionsPerPrincipal")
                || !HasInt(practice, "maximumActiveConcubinageUnionsPerPartner")
                || !HasBoolean(practice, "allowsPoliticalBetrothalBeforeLegalAge")
                || !HasBoolean(practice, "allowsWidowRemarriage")
                || !HasInt(practice, "prohibitedKinship"))
            {
                throw MalformedCharacterMarriageData(context, "practice");
            }
        }

        foreach (JsonNode? node in characterMarriages["proposals"]!.AsArray())
        {
            if (node is not JsonObject proposal
                || !HasVersion(proposal, CharacterMarriageContractVersions.State)
                || !HasObject(proposal, "proposalId")
                || !HasInt(proposal, "kind")
                || !HasInt(proposal, "basis")
                || !HasInt(proposal, "proposedForm")
                || !HasInt(proposal, "consentKind")
                || !HasObject(proposal, "proposerCharacterId")
                || !HasObject(proposal, "recipientCharacterId")
                || !HasNullableObject(proposal, "concubinagePrincipalCharacterId")
                || !HasObject(proposal, "practiceId")
                || !HasObject(proposal, "createdDate")
                || !HasLong(proposal, "createdTurnIndex")
                || !HasObject(proposal, "sourceCommandId")
                || !HasInt(proposal, "status")
                || !HasNullableObject(proposal, "resolutionDate")
                || !HasNullableLong(proposal, "resolutionTurnIndex")
                || !HasNullableObject(proposal, "resolutionCommandId"))
            {
                throw MalformedCharacterMarriageData(context, "proposal");
            }
        }

        foreach (JsonNode? node in characterMarriages["betrothals"]!.AsArray())
        {
            if (node is not JsonObject betrothal
                || !HasVersion(betrothal, CharacterMarriageContractVersions.State)
                || !HasObject(betrothal, "betrothalId")
                || !HasObject(betrothal, "firstCharacterId")
                || !HasObject(betrothal, "secondCharacterId")
                || !HasInt(betrothal, "intendedForm")
                || !HasNullableObject(betrothal, "concubinagePrincipalCharacterId")
                || !HasObject(betrothal, "practiceId")
                || !HasObject(betrothal, "sourceProposalId")
                || !HasObject(betrothal, "startDate")
                || !HasLong(betrothal, "startTurnIndex")
                || !HasInt(betrothal, "status")
                || !HasNullableObject(betrothal, "fulfillmentUnionId")
                || !HasNullableObject(betrothal, "resolutionDate")
                || !HasNullableLong(betrothal, "resolutionTurnIndex")
                || !HasNullableObject(betrothal, "resolutionCommandId"))
            {
                throw MalformedCharacterMarriageData(context, "political-betrothal record");
            }
        }

        foreach (JsonNode? node in characterMarriages["unions"]!.AsArray())
        {
            if (node is not JsonObject union
                || !HasVersion(union, CharacterMarriageContractVersions.State)
                || !HasObject(union, "unionId")
                || !HasObject(union, "firstCharacterId")
                || !HasObject(union, "secondCharacterId")
                || !HasInt(union, "form")
                || !HasNullableObject(union, "concubinagePrincipalCharacterId")
                || !HasInt(union, "basis")
                || !HasInt(union, "consentKind")
                || !HasObject(union, "practiceId")
                || !HasObject(union, "sourceProposalId")
                || !HasObject(union, "startDate")
                || !HasLong(union, "startTurnIndex")
                || !HasInt(union, "status")
                || !HasNullableObject(union, "endDate")
                || !HasNullableLong(union, "endTurnIndex")
                || !HasNullableObject(union, "endCommandId")
                || !HasNullableInt(union, "endReason"))
            {
                throw MalformedCharacterMarriageData(context, "legal-union record");
            }
        }

        foreach (JsonNode? node in characterMarriages["romanceRoutes"]!.AsArray())
        {
            int routeVersion = node?["contractVersion"]?.GetValue<int>() ?? -1;
            if (node is not JsonObject route
                || routeVersion != CharacterMarriageContractVersions.State
                    && (!allowVersionTwoRoutes
                        || routeVersion
                            != CharacterMarriageContractVersions.RomanceRouteState)
                || !HasObject(route, "routeId")
                || !HasObject(route, "firstCharacterId")
                || !HasObject(route, "secondCharacterId")
                || !HasObject(route, "practiceId")
                || !HasInt(route, "progressLevel")
                || !HasObject(route, "startDate")
                || !HasLong(route, "startTurnIndex")
                || !HasObject(route, "sourceCommandId")
                || !HasInt(route, "status")
                || !HasNullableObject(route, "resolutionDate")
                || !HasNullableLong(route, "resolutionTurnIndex")
                || !HasNullableObject(route, "resolutionCommandId"))
            {
                throw MalformedCharacterMarriageData(context, "romance-route record");
            }

            string[] versionTwoFields =
            [
                "sourceInvitationId",
                "invitationInitiatorCharacterId",
                "invitationCreatedDate",
                "invitationCreatedTurnIndex",
                "invitationSourceCommandId",
                "lastPositiveProgressDate",
                "lastPositiveProgressTurnIndex",
                "lastPositiveProgressCommandId",
            ];
            if (routeVersion == CharacterMarriageContractVersions.State)
            {
                if (!allowVersionTwoRoutes
                    && versionTwoFields.Any(route.ContainsKey))
                {
                    throw MalformedCharacterMarriageData(
                        context,
                        "legacy romance-route future fields");
                }
            }
            else if (!HasObject(route, "sourceInvitationId")
                || !HasObject(route, "invitationInitiatorCharacterId")
                || !HasObject(route, "invitationCreatedDate")
                || !HasLong(route, "invitationCreatedTurnIndex")
                || !HasObject(route, "invitationSourceCommandId")
                || !HasObject(route, "lastPositiveProgressDate")
                || !HasLong(route, "lastPositiveProgressTurnIndex")
                || !HasObject(route, "lastPositiveProgressCommandId"))
            {
                throw MalformedCharacterMarriageData(
                    context,
                    "version-2 romance-route evidence");
            }
        }

        foreach (JsonNode? node in characterMarriages["history"]!.AsArray())
        {
            if (node is not JsonObject history
                || !HasVersion(history, CharacterMarriageContractVersions.State)
                || !HasObject(history, "characterId")
                || !HasLong(history, "foldedProposalCount")
                || !HasLong(history, "foldedBetrothalCount")
                || !HasLong(history, "foldedUnionCount")
                || !HasLong(history, "foldedRomanceRouteCount")
                || !HasNullableObject(history, "earliestDate")
                || !HasNullableObject(history, "latestDate"))
            {
                throw MalformedCharacterMarriageData(context, "history aggregate");
            }
        }
    }

    private static bool HasVersion(JsonObject value, int expected) =>
        value["contractVersion"] is JsonValue version
        && version.TryGetValue(out int actual)
        && actual == expected;

    private static bool HasLong(JsonObject value, string property) =>
        value[property] is JsonValue number && number.TryGetValue(out long _);

    private static bool HasInt(JsonObject value, string property) =>
        value[property] is JsonValue number && number.TryGetValue(out int _);

    private static bool HasBoolean(JsonObject value, string property) =>
        value[property] is JsonValue boolean && boolean.TryGetValue(out bool _);

    private static bool HasObject(JsonObject value, string property) =>
        value[property] is JsonObject;

    private static bool HasNullableObject(JsonObject value, string property) =>
        value.ContainsKey(property)
        && (value[property] is null || value[property] is JsonObject);

    private static bool HasNullableLong(JsonObject value, string property) =>
        value.ContainsKey(property)
        && (value[property] is null
            || value[property] is JsonValue number && number.TryGetValue(out long _));

    private static bool HasNullableInt(JsonObject value, string property) =>
        value.ContainsKey(property)
        && (value[property] is null
            || value[property] is JsonValue number && number.TryGetValue(out int _));

    private static SaveCompatibilityException MalformedCharacterResourceData(
        string context,
        string description) =>
        new($"{context} contains missing, null, or malformed required {description} data.");

    private static SaveCompatibilityException MalformedCharacterMarriageData(
        string context,
        string description) =>
        new($"{context} contains missing, null, or malformed required character-marriage {description} data.");

    internal static void UpgradeLegacyRelationshipSnapshot(JsonObject relationships)
    {
        relationships["contractVersion"] = RelationshipContractVersions.Snapshot;
        if (relationships["subjects"] is not JsonArray subjects)
        {
            throw new SaveCompatibilityException(
                "Legacy relationship snapshot is missing subject histories.");
        }

        foreach (JsonObject subject in subjects.OfType<JsonObject>())
        {
            if (subject["detailedRelationships"] is not JsonArray detailed)
            {
                throw new SaveCompatibilityException(
                    "Legacy relationship snapshot contains malformed detailed histories.");
            }

            foreach (JsonObject relationship in detailed.OfType<JsonObject>())
            {
                if (relationship["memories"] is not JsonArray memories)
                {
                    throw new SaveCompatibilityException(
                        "Legacy relationship snapshot contains malformed memories.");
                }

                foreach (JsonObject memory in memories.OfType<JsonObject>())
                {
                    UpgradeLegacyRelationshipMemory(memory);
                }
            }
        }
    }

    internal static void UpgradeLegacyRelationshipMemory(JsonObject memory)
    {
        JsonNode source = memory["sourceRelationshipActionEventId"]?.DeepClone()
            ?? throw new SaveCompatibilityException(
                "Legacy relationship memory is missing its source event.");
        memory["contractVersion"] = RelationshipContractVersions.Memory;
        memory["sourceEventId"] = source;
        memory["sourceKind"] = (int)RelationshipMemorySourceKind.RelationshipAction;
        memory["identityScheme"] =
            (int)RelationshipMemoryIdentityScheme.LegacyRelationshipActionV1;
        memory["consequenceIndex"] = 0;
        memory.Remove("sourceRelationshipActionEventId");
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

    private static bool IsCurrentCareerSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            "simulation.character_careers",
            CareerContractVersions.Snapshot);

    private static bool IsCurrentCharacterResourceSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            CharacterResourceSystem.SystemId,
            CharacterResourceSystem.Version);

    private static bool IsCurrentCharacterEstateHoldingSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            CharacterEstateHoldingSystem.SystemId,
            CharacterEstateHoldingSystem.Version);

    private static bool IsCurrentCharacterMarriageSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            CharacterMarriageSystem.SystemId,
            CharacterMarriageSystem.Version);

    private static bool IsCurrentCharacterGuardianshipSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            CharacterGuardianshipSystem.SystemId,
            CharacterGuardianshipSystem.Version);

    private static bool IsSystemVersion(JsonObject systemVersion, string systemId, int expectedVersion) =>
        IsSystemId(systemVersion, systemId)
        && systemVersion["version"] is JsonValue versionValue
        && versionValue.TryGetValue(out int version)
        && version == expectedVersion;

    private static bool IsSystemId(JsonObject systemVersion, string systemId) =>
        systemVersion["systemId"] is JsonValue systemIdValue
        && systemIdValue.TryGetValue(out string? actualSystemId)
        && StringComparer.Ordinal.Equals(actualSystemId, systemId);

    private static void RejectD1Discriminators(JsonNode node, string description)
    {
        if (ContainsD1Discriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 10 unexpectedly contains schema 11 character-marriage data in {description}.");
        }
    }

    private static void RejectD2Discriminators(JsonNode node, string description)
    {
        if (ContainsD2Discriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 11 unexpectedly contains schema 12 character-marriage data in {description}.");
        }
    }

    private static void RejectD3Discriminators(JsonNode node, string description)
    {
        if (ContainsD3Discriminator(node) || ContainsD3Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 12 unexpectedly contains schema 13 character-condition, household, or coercion data in {description}.");
        }
    }

    private static void RejectD3RelationshipSourceKinds(
        JsonNode node,
        string description)
    {
        if (ContainsPostD2RelationshipSourceKind(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 12 unexpectedly contains a schema 13 relationship-memory source kind in {description}.");
        }
    }

    private static void RejectE0Discriminators(JsonNode node, string description)
    {
        if (ContainsE0Discriminator(node) || ContainsE0Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 13 unexpectedly contains schema 14 character-family data in {description}.");
        }
    }

    private static void RejectE1Discriminators(JsonNode node, string description)
    {
        if (ContainsE1Discriminator(node) || ContainsE1Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 14 unexpectedly contains schema 15 character-guardianship data in {description}.");
        }
    }

    private static void RejectE2Discriminators(JsonNode node, string description)
    {
        if (ContainsE2Discriminator(node) || ContainsE2Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 15 unexpectedly contains schema 16 character-guardianship lifecycle data in {description}.");
        }
    }

    private static void RejectE3Discriminators(JsonNode node, string description)
    {
        if (ContainsE3Discriminator(node) || ContainsE3Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 16 unexpectedly contains schema 17 character coming-of-age data in {description}.");
        }
    }

    private static bool ContainsD1Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && (type is "character_marriage_action.v1"
                    or "character_marriage_action_resolved.v1"
                    or "propose_political_marriage.v1"
                    or "respond_political_marriage_proposal.v1"
                    or "withdraw_political_marriage_proposal.v1"
                    or "cancel_political_betrothal.v1"
                    or "fulfill_political_betrothal.v1"
                    or "marriage_proposal_created.v1"
                    or "marriage_proposal_refused.v1"
                    or "marriage_proposal_withdrawn.v1"
                    or "marriage_proposal_cancelled.v1"
                    or "political_betrothal_accepted.v1"
                    or "direct_political_union_accepted.v1"
                    or "political_betrothal_cancelled.v1"
                    or "political_betrothal_fulfilled.v1"
                    || IsD2Discriminator(type)))
            {
                return true;
            }

            return value.Any(property => ContainsD1Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsD1Discriminator);
    }

    private static bool ContainsD2Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && IsD2Discriminator(type))
            {
                return true;
            }

            return value.Any(property => ContainsD2Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsD2Discriminator);
    }

    private static bool ContainsD3Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && IsD3Discriminator(type))
            {
                return true;
            }

            return value.Any(property => ContainsD3Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsD3Discriminator);
    }

    private static bool ContainsD3Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("relationshipMemoryConsequence"))
            {
                return true;
            }

            return value.Any(property => ContainsD3Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsD3Property);
    }

    private static bool ContainsE0Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "character_family_action.v1"
                    or "character_family_action_resolved.v1"
                    or "establish_legal_adoptive_parent.v1"
                    or "legal_adoptive_parent_established.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE0Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE0Discriminator);
    }

    private static bool ContainsE0Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("expectedCurrentParentLinks")
                || value.ContainsKey("previousParentLinks")
                || value.ContainsKey("currentParentLinks"))
            {
                return true;
            }

            return value.Any(property => ContainsE0Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE0Property);
    }

    private static bool ContainsE1Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "establish_primary_guardianship.v1"
                    or "primary_guardianship_established.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE1Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE1Discriminator);
    }

    private static bool ContainsE1Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("expectedCurrentPrimaryGuardianshipId")
                || value.ContainsKey("guardianship")
                || value.ContainsKey("guardianshipId")
                || value.ContainsKey("wardCharacterId")
                || value.ContainsKey("guardianCharacterId"))
            {
                return true;
            }

            return value.Any(property => ContainsE1Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE1Property);
    }

    private static bool ContainsE2Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "end_primary_guardianship.v1"
                    or "replace_primary_guardianship.v1"
                    or "primary_guardianship_ended.v1"
                    or "primary_guardianship_replaced.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE2Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE2Discriminator);
    }

    private static bool ContainsE2Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("replacementGuardianCharacterId")
                || value.ContainsKey("endedGuardianship")
                || value.ContainsKey("replacementGuardianship")
                || value["$type"]?.GetValue<string>()
                    == "establish_primary_guardianship.v1"
                    && value.ContainsKey("endReason"))
            {
                return true;
            }

            return value.Any(property => ContainsE2Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE2Property);
    }

    private static bool ContainsE3Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "character_coming_of_age.v1"
                    or "character_came_of_age.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE3Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE3Discriminator);
    }

    private static bool ContainsE3Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("expectedActivePrimaryGuardianshipId")
                || value.ContainsKey("endedPrimaryGuardianship"))
            {
                return true;
            }

            return value.Any(property => ContainsE3Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE3Property);
    }

    private static bool ContainsPostD2RelationshipSourceKind(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["sourceKind"] is JsonValue sourceKind
                && sourceKind.TryGetValue(out int kind)
                && kind is not ((int)RelationshipMemorySourceKind.RelationshipAction)
                    and not ((int)RelationshipMemorySourceKind.CharacterAction))
            {
                return true;
            }

            return value.Any(property =>
                ContainsPostD2RelationshipSourceKind(property.Value));
        }

        return node is JsonArray array
            && array.Any(ContainsPostD2RelationshipSourceKind);
    }

    private static bool IsD2Discriminator(string? type) => type is
        "offer_romance_route.v1"
        or "respond_to_romance_invitation.v1"
        or "withdraw_romance_invitation.v1"
        or "advance_romance_route.v1"
        or "end_romance_route.v1"
        or "romance_invitation_created.v1"
        or "romance_invitation_refused.v1"
        or "romance_invitation_withdrawn.v1"
        or "romance_invitation_cancelled.v1"
        or "romance_route_started.v1"
        or "romance_route_advanced.v1"
        or "romance_route_completed.v1"
        or "romance_route_ended.v1";

    private static bool IsD3Discriminator(string? type) => type is
        "character_condition_action.v1"
        or "household_decision.v1"
        or "character_condition_action_resolved.v1"
        or "household_decision_resolved.v1"
        or "incapacitate_character.v1"
        or "restore_character_capacity.v1"
        or "enter_character_custody.v1"
        or "release_character_custody.v1"
        or "character_condition_changed.v1"
        or "expel_household_member.v1"
        or "incorporate_captive_household_member.v1"
        or "household_membership_changed.v1"
        or "impose_coerced_union.v1"
        or "coerced_political_union_imposed.v1";
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
            new RelationshipWorldSnapshot(RelationshipContractVersions.LegacySnapshot, []),
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.relationships",
            ["version"] = 1,
        });
        WorldSnapshot migratedSnapshot = SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
            snapshot,
            ToSchemaVersion);
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

        WorldSnapshot migratedSnapshot = SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
            snapshot,
            ToSchemaVersion);
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static bool IsCharacterSystemVersion(JsonObject node, int version) =>
        node["systemId"]?.GetValue<string>() == "simulation.characters"
        && node["version"]?.GetValue<int>() == version;
}

public sealed class SaveMigrationV6ToV7 : ISaveMigration
{
    public int FromSchemaVersion => 6;

    public int ToSchemaVersion => 7;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["relationships"] is not JsonObject relationships
            || snapshot["systemVersions"] is not JsonArray systemVersions
            || snapshot["pendingCommands"] is not JsonArray pendingCommands
            || source["diagnosticCommands"] is not JsonArray diagnosticCommands
            || source["diagnosticEvents"] is not JsonArray diagnosticEvents)
        {
            throw new SaveCompatibilityException(
                "Schema 6 save is missing relationship, command, event, or system-version data.");
        }

        if (snapshot.ContainsKey("careers")
            || systemVersions.Any(node =>
                node?["systemId"]?.GetValue<string>() == "simulation.character_careers"))
        {
            throw new SaveCompatibilityException(
                "Schema 6 unexpectedly contains schema 7 character-career data.");
        }

        RejectFutureCharacterCommands(pendingCommands, "pending commands");
        RejectFutureCharacterCommands(diagnosticCommands, "diagnostic commands");
        MigrateDiagnosticEvents(diagnosticEvents);

        SaveSchemaRegistry.UpgradeLegacyRelationshipSnapshot(relationships);
        JsonObject relationshipVersion = systemVersions
            .OfType<JsonObject>()
            .SingleOrDefault(node => IsSystemVersion(
                node,
                "simulation.relationships",
                RelationshipContractVersions.LegacySnapshot))
            ?? throw new SaveCompatibilityException(
                "Schema 6 is missing required 'simulation.relationships@1' system-version data.");
        relationshipVersion["version"] = RelationshipContractVersions.Snapshot;

        snapshot["careers"] = JsonSerializer.SerializeToNode(
            CareerWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.character_careers",
            ["version"] = CareerContractVersions.Snapshot,
        });

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 7 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static void RejectFutureCharacterCommands(JsonArray commands, string description)
    {
        foreach (JsonObject command in commands.OfType<JsonObject>())
        {
            string? discriminator = command["payload"]?["$type"]?.GetValue<string>();
            if (StringComparer.Ordinal.Equals(discriminator, "character_action.v1"))
            {
                throw new SaveCompatibilityException(
                    $"Schema 6 unexpectedly contains schema 7 character-action {description}.");
            }
        }
    }

    private static void MigrateDiagnosticEvents(JsonArray events)
    {
        foreach (JsonObject campaignEvent in events.OfType<JsonObject>())
        {
            if (campaignEvent["payload"] is not JsonObject payload)
            {
                throw new SaveCompatibilityException(
                    "Schema 6 diagnostic event contains a malformed payload.");
            }

            string? discriminator = payload["$type"]?.GetValue<string>();
            if (StringComparer.Ordinal.Equals(discriminator, "relationship_action_resolved.v1"))
            {
                if (payload["memory"] is not JsonObject memory)
                {
                    throw new SaveCompatibilityException(
                        "Schema 6 relationship diagnostic event is missing its memory.");
                }

                SaveSchemaRegistry.UpgradeLegacyRelationshipMemory(memory);
                payload["$type"] = "relationship_action_resolved.v2";
            }
            else if (StringComparer.Ordinal.Equals(discriminator, "relationship_action_resolved.v2")
                || StringComparer.Ordinal.Equals(discriminator, "character_action_resolved.v1"))
            {
                throw new SaveCompatibilityException(
                    "Schema 6 unexpectedly contains a schema 7 diagnostic event payload.");
            }
        }
    }

    private static bool IsSystemVersion(JsonObject node, string systemId, int version) =>
        node["systemId"]?.GetValue<string>() == systemId
        && node["version"]?.GetValue<int>() == version;
}

public sealed class SaveMigrationV7ToV8 : ISaveMigration
{
    public int FromSchemaVersion => 7;

    public int ToSchemaVersion => 8;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions
            || snapshot["pendingCommands"] is not JsonArray pendingCommands
            || source["diagnosticCommands"] is not JsonArray diagnosticCommands
            || source["diagnosticEvents"] is not JsonArray diagnosticEvents)
        {
            throw new SaveCompatibilityException(
                "Schema 7 save is missing command, event, or system-version data.");
        }

        if (snapshot.ContainsKey("characterResources")
            || systemVersions.Any(node =>
                node?["systemId"]?.GetValue<string>() == CharacterResourceSystem.SystemId))
        {
            throw new SaveCompatibilityException(
                "Schema 7 unexpectedly contains schema 8 character-resource data.");
        }

        RejectFutureResourceCommands(pendingCommands, "pending commands");
        RejectFutureResourceCommands(diagnosticCommands, "diagnostic commands");
        RejectFutureResourceEvents(diagnosticEvents);

        snapshot["characterResources"] = JsonSerializer.SerializeToNode(
            CharacterResourceWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = CharacterResourceSystem.SystemId,
            ["version"] = CharacterResourceSystem.Version,
        });

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 8 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static void RejectFutureResourceCommands(JsonArray commands, string description)
    {
        foreach (JsonObject command in commands.OfType<JsonObject>())
        {
            string? discriminator = command["payload"]?["$type"]?.GetValue<string>();
            if (StringComparer.Ordinal.Equals(discriminator, "character_resource_action.v1"))
            {
                throw new SaveCompatibilityException(
                    $"Schema 7 unexpectedly contains schema 8 character-resource {description}.");
            }
        }
    }

    private static void RejectFutureResourceEvents(JsonArray events)
    {
        foreach (JsonObject campaignEvent in events.OfType<JsonObject>())
        {
            string? discriminator = campaignEvent["payload"]?["$type"]?.GetValue<string>();
            if (StringComparer.Ordinal.Equals(
                discriminator,
                "character_resource_action_resolved.v1"))
            {
                throw new SaveCompatibilityException(
                    "Schema 7 unexpectedly contains a schema 8 character-resource diagnostic event.");
            }
        }
    }
}

public sealed class SaveMigrationV8ToV9 : ISaveMigration
{
    public int FromSchemaVersion => 8;

    public int ToSchemaVersion => 9;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException(
                "Schema 8 save is missing snapshot or system-version data.");
        }

        if (snapshot.ContainsKey("characterEstateHoldings")
            || systemVersions.Any(node =>
                node?["systemId"]?.GetValue<string>()
                    == CharacterEstateHoldingSystem.SystemId))
        {
            throw new SaveCompatibilityException(
                "Schema 8 unexpectedly contains schema 9 character-estate-holding data.");
        }

        snapshot["characterEstateHoldings"] = JsonSerializer.SerializeToNode(
            CharacterEstateHoldingWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = CharacterEstateHoldingSystem.SystemId,
            ["version"] = CharacterEstateHoldingSystem.Version,
        });

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 9 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV9ToV10 : ISaveMigration
{
    public int FromSchemaVersion => 9;

    public int ToSchemaVersion => 10;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException(
                "Schema 9 save is missing snapshot or system-version data.");
        }

        if (snapshot.ContainsKey("characterMarriages")
            || systemVersions.Any(node =>
                node?["systemId"]?.GetValue<string>()
                    == CharacterMarriageSystem.SystemId))
        {
            throw new SaveCompatibilityException(
                "Schema 9 unexpectedly contains schema 10 character-marriage data.");
        }

        JsonObject legacyMarriages = JsonSerializer.SerializeToNode(
            CharacterMarriageWorldSnapshot.Empty,
            SimulationJson.CreateOptions())!.AsObject();
        legacyMarriages["contractVersion"] = 1;
        legacyMarriages.Remove("invitations");
        snapshot["characterMarriages"] = legacyMarriages;
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = CharacterMarriageSystem.SystemId,
            ["version"] = 1,
        });

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 10 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV10ToV11 : ISaveMigration
{
    public int FromSchemaVersion => 10;

    public int ToSchemaVersion => 11;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot)
        {
            throw new SaveCompatibilityException(
                "Schema 10 save is missing authoritative snapshot data.");
        }

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 11 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV11ToV12 : ISaveMigration
{
    public int FromSchemaVersion => 11;

    public int ToSchemaVersion => 12;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["characterMarriages"] is not JsonObject marriages
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException(
                "Schema 11 save is missing character-marriage or system-version data.");
        }

        if (marriages.ContainsKey("invitations")
            || marriages["contractVersion"]?.GetValue<int>() != 1)
        {
            throw new SaveCompatibilityException(
                "Schema 11 unexpectedly contains schema 12 character-marriage state.");
        }

        JsonObject? marriageVersion = systemVersions
            .OfType<JsonObject>()
            .SingleOrDefault(item =>
                item["systemId"]?.GetValue<string>()
                    == CharacterMarriageSystem.SystemId
                && item["version"]?.GetValue<int>() == 1);
        if (marriageVersion is null)
        {
            throw new SaveCompatibilityException(
                $"Schema 11 is missing '{CharacterMarriageSystem.SystemId}@1'.");
        }

        marriages["contractVersion"] = CharacterMarriageContractVersions.Snapshot;
        marriages["invitations"] = new JsonArray();
        marriageVersion["version"] = CharacterMarriageSystem.Version;

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 12 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV12ToV13 : ISaveMigration
{
    public int FromSchemaVersion => 12;

    public int ToSchemaVersion => 13;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot)
        {
            throw new SaveCompatibilityException(
                "Schema 12 save is missing authoritative snapshot data.");
        }

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 13 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV13ToV14 : ISaveMigration
{
    public int FromSchemaVersion => 13;

    public int ToSchemaVersion => 14;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot)
        {
            throw new SaveCompatibilityException(
                "Schema 13 save is missing authoritative snapshot data.");
        }

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 14 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV14ToV15 : ISaveMigration
{
    public int FromSchemaVersion => 14;

    public int ToSchemaVersion => 15;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException(
                "Schema 14 save is missing snapshot or system-version data.");
        }

        if (snapshot.ContainsKey("characterGuardianships")
            || systemVersions.Any(node =>
                node?["systemId"]?.GetValue<string>()
                    == CharacterGuardianshipSystem.SystemId))
        {
            throw new SaveCompatibilityException(
                "Schema 14 unexpectedly contains schema 15 character-guardianship data.");
        }

        snapshot["characterGuardianships"] = JsonSerializer.SerializeToNode(
            CharacterGuardianshipWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = CharacterGuardianshipSystem.SystemId,
            ["version"] = CharacterGuardianshipSystem.Version,
        });

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 15 snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV15ToV16 : ISaveMigration
{
    public int FromSchemaVersion => 15;

    public int ToSchemaVersion => 16;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot)
        {
            throw new SaveCompatibilityException(
                "Schema 15 save is missing authoritative snapshot data.");
        }

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 16 snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV16ToV17 : ISaveMigration
{
    public int FromSchemaVersion => 16;

    public int ToSchemaVersion => 17;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot)
        {
            throw new SaveCompatibilityException(
                "Schema 16 save is missing authoritative snapshot data.");
        }

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 17 snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}
