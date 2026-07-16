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
    private const int Schema23DeathContractVersion = 3;
    private static readonly DeathDiagnosticVersions Schema24DeathDiagnosticVersions = new(
        Death: 3,
        ConditionChange: 1,
        HouseholdHeadChange: 1,
        MarriageLifecycleChangeSet: 1,
        MarriageSnapshot: 2,
        MarriageState: 1,
        MarriageInvitationState: 1,
        MarriageRouteState: 2,
        MarriagePractice: 1,
        GuardianshipSnapshot: 1,
        GuardianshipState: 1,
        PregnancySnapshot: 1,
        PregnancyState: 1,
        CareerDeathChange: 1,
        CareerState: 1);
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
            new SaveMigrationV17ToV18(),
            new SaveMigrationV18ToV19(),
            new SaveMigrationV19ToV20(),
            new SaveMigrationV20ToV21(),
            new SaveMigrationV21ToV22(),
            new SaveMigrationV22ToV23(),
            new SaveMigrationV23ToV24(),
            new SaveMigrationV24ToV25(),
            new SaveMigrationV25ToV26(),
            new SaveMigrationV26ToV27(),
            new SaveMigrationV27ToV28(),
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
        if (schemaVersion is < 1 or > 27)
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
        if (schemaVersion == 25)
        {
            UpgradeSchema25SuccessionSnapshotForCurrentDto(compatible);
        }
        else if (schemaVersion == 26)
        {
            UpgradeSchema26SuccessionSnapshotForCurrentDto(compatible);
        }
        else if (schemaVersion == 27)
        {
            UpgradeSchema27SuccessionSnapshotForCurrentDto(compatible);
        }

        if (schemaVersion is >= 5 and < 7
            && compatible["relationships"] is JsonObject relationships)
        {
            UpgradeLegacyRelationshipSnapshot(relationships);
        }

        return compatible.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} is missing its authoritative snapshot.");
    }

    private static void UpgradeSchema25SuccessionSnapshotForCurrentDto(
        JsonObject compatible)
    {
        JsonObject successions = compatible["characterSuccessions"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Save schema 25 is missing its character-succession snapshot.");
        successions["contractVersion"] = CharacterSuccessionContractVersions.Snapshot;
        successions["claims"] = new JsonArray();
        successions["claimHistory"] = new JsonArray();
        successions["supports"] = new JsonArray();
        successions["supportHistory"] = new JsonArray();
        AddEmptySuccessionResolutionState(successions);

        JsonArray systemVersions = compatible["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Save schema 25 is missing system-version data.");
        JsonObject[] successionVersions = systemVersions
            .OfType<JsonObject>()
            .Where(version => IsSystemId(
                version,
                CharacterSuccessionSystem.SystemId))
            .ToArray();
        if (successionVersions.Length != 1)
        {
            throw new SaveCompatibilityException(
                "Save schema 25 must contain exactly one character-succession system version.");
        }

        successionVersions[0]["version"] = CharacterSuccessionSystem.Version;
    }

    private static void UpgradeSchema26SuccessionSnapshotForCurrentDto(
        JsonObject compatible)
    {
        JsonObject successions = compatible["characterSuccessions"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Save schema 26 is missing its character-succession snapshot.");
        successions["contractVersion"] = CharacterSuccessionContractVersions.Snapshot;
        successions["supports"] = new JsonArray();
        successions["supportHistory"] = new JsonArray();
        AddEmptySuccessionResolutionState(successions);

        JsonArray systemVersions = compatible["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Save schema 26 is missing system-version data.");
        JsonObject[] successionVersions = systemVersions
            .OfType<JsonObject>()
            .Where(version => IsSystemId(
                version,
                CharacterSuccessionSystem.SystemId))
            .ToArray();
        if (successionVersions.Length != 1)
        {
            throw new SaveCompatibilityException(
                "Save schema 26 must contain exactly one character-succession system version.");
        }

        successionVersions[0]["version"] = CharacterSuccessionSystem.Version;
    }

    private static void UpgradeSchema27SuccessionSnapshotForCurrentDto(
        JsonObject compatible)
    {
        JsonObject successions = compatible["characterSuccessions"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Save schema 27 is missing its character-succession snapshot.");
        successions["contractVersion"] = CharacterSuccessionContractVersions.Snapshot;
        AddEmptySuccessionResolutionState(successions);

        JsonArray systemVersions = compatible["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Save schema 27 is missing system-version data.");
        JsonObject[] successionVersions = systemVersions
            .OfType<JsonObject>()
            .Where(version => IsSystemId(
                version,
                CharacterSuccessionSystem.SystemId))
            .ToArray();
        if (successionVersions.Length != 1)
        {
            throw new SaveCompatibilityException(
                "Save schema 27 must contain exactly one character-succession system version.");
        }

        successionVersions[0]["version"] = CharacterSuccessionSystem.Version;
    }

    private static void AddEmptySuccessionResolutionState(JsonObject successions)
    {
        successions["resolutions"] = new JsonArray();
        successions["resolutionHistory"] = new JsonObject
        {
            ["contractVersion"] = CharacterSuccessionContractVersions.ResolutionHistory,
            ["foldedSelectedCount"] = 0L,
            ["foldedDisputedCount"] = 0L,
            ["foldedNoSuccessorCount"] = 0L,
            ["earliestDate"] = null,
            ["latestDate"] = null,
        };
        successions["campaignContinuity"] = null;
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

        if (schemaVersion < 25
            && (snapshot.ContainsKey("characterSuccessions")
                || systemVersions.Any(version => IsSystemId(
                    version,
                    CharacterSuccessionSystem.SystemId))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 25 character-succession data.");
        }

        if (schemaVersion == 25)
        {
            JsonObject characterSuccessions = RequireHistoricalObject(
                snapshot,
                "characterSuccessions",
                schemaVersion,
                "snapshot.characterSuccessions");
            ValidateCharacterSuccessionSnapshotShape(
                characterSuccessions,
                "Save schema 25",
                expectedSnapshotVersion: 1,
                requireClaims: false,
                requireSupports: false,
                requireResolutions: false);
            if (characterSuccessions.ContainsKey("claims")
                || characterSuccessions.ContainsKey("claimHistory"))
            {
                throw new SaveCompatibilityException(
                    "Save schema 25 unexpectedly contains schema 26 succession-claim snapshot data.");
            }

            JsonObject[] successionSystemVersions = systemVersions
                .Where(version => IsSystemId(
                    version,
                    CharacterSuccessionSystem.SystemId))
                .ToArray();
            if (successionSystemVersions.Length != 1
                || !IsSystemVersion(
                    successionSystemVersions[0],
                    CharacterSuccessionSystem.SystemId,
                    1))
            {
                throw new SaveCompatibilityException(
                    $"Save schema 25 requires exactly one '{CharacterSuccessionSystem.SystemId}@1' system-version registration.");
            }
        }
        else if (schemaVersion == 26)
        {
            JsonObject characterSuccessions = RequireHistoricalObject(
                snapshot,
                "characterSuccessions",
                schemaVersion,
                "snapshot.characterSuccessions");
            ValidateCharacterSuccessionSnapshotShape(
                characterSuccessions,
                "Save schema 26",
                expectedSnapshotVersion: 2,
                requireClaims: true,
                requireSupports: false,
                requireResolutions: false);
            if (characterSuccessions.ContainsKey("supports")
                || characterSuccessions.ContainsKey("supportHistory"))
            {
                throw new SaveCompatibilityException(
                    "Save schema 26 unexpectedly contains schema 27 succession-support snapshot data.");
            }

            JsonObject[] successionSystemVersions = systemVersions
                .Where(version => IsSystemId(
                    version,
                    CharacterSuccessionSystem.SystemId))
                .ToArray();
            if (successionSystemVersions.Length != 1
                || !IsSystemVersion(
                    successionSystemVersions[0],
                    CharacterSuccessionSystem.SystemId,
                    2))
            {
                throw new SaveCompatibilityException(
                $"Save schema 26 requires exactly one '{CharacterSuccessionSystem.SystemId}@2' system-version registration.");
            }
        }
        else if (schemaVersion == 27)
        {
            JsonObject characterSuccessions = RequireHistoricalObject(
                snapshot,
                "characterSuccessions",
                schemaVersion,
                "snapshot.characterSuccessions");
            ValidateCharacterSuccessionSnapshotShape(
                characterSuccessions,
                "Save schema 27",
                expectedSnapshotVersion: 3,
                requireClaims: true,
                requireSupports: true,
                requireResolutions: false);
            if (characterSuccessions.ContainsKey("resolutions")
                || characterSuccessions.ContainsKey("resolutionHistory")
                || characterSuccessions.ContainsKey("campaignContinuity"))
            {
                throw new SaveCompatibilityException(
                    "Save schema 27 unexpectedly contains schema 28 succession-resolution snapshot data.");
            }

            JsonObject[] successionSystemVersions = systemVersions
                .Where(version => IsSystemId(
                    version,
                    CharacterSuccessionSystem.SystemId))
                .ToArray();
            if (successionSystemVersions.Length != 1
                || !IsSystemVersion(
                    successionSystemVersions[0],
                    CharacterSuccessionSystem.SystemId,
                    3))
            {
                throw new SaveCompatibilityException(
                    $"Save schema 27 requires exactly one '{CharacterSuccessionSystem.SystemId}@3' system-version registration.");
            }
        }

        if (schemaVersion < 18
            && (snapshot.ContainsKey("characterPregnancies")
                || systemVersions.Any(version => IsSystemId(
                    version,
                    CharacterPregnancySystem.SystemId))))
        {
            throw new SaveCompatibilityException(
                $"Schema {schemaVersion} unexpectedly contains schema 18 character-pregnancy data.");
        }

        if (schemaVersion >= 18)
        {
            ValidateCharacterPregnancySnapshotShape(
                RequireHistoricalObject(
                    snapshot,
                    "characterPregnancies",
                    schemaVersion,
                    "snapshot.characterPregnancies"),
                $"Save schema {schemaVersion}");
            JsonObject[] pregnancySystemVersions = systemVersions
                .Where(version => IsSystemId(
                    version,
                    CharacterPregnancySystem.SystemId))
                .ToArray();
            if (pregnancySystemVersions.Length != 1
                || !IsSystemVersion(
                    pregnancySystemVersions[0],
                    CharacterPregnancySystem.SystemId,
                    CharacterPregnancySystem.Version))
            {
                throw new SaveCompatibilityException(
                    $"Save schema {schemaVersion} requires exactly one '{CharacterPregnancySystem.SystemId}@{CharacterPregnancySystem.Version}' system-version registration.");
            }
        }

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

        if (schemaVersion is >= 10 and <= 27)
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
            else if (schemaVersion == 16)
            {
                RejectE3Discriminators(pendingCommands, "snapshot pending commands");
                RejectE3Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE3Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 17)
            {
                RejectE4Discriminators(pendingCommands, "snapshot pending commands");
                RejectE4Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE4Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 18)
            {
                RejectE5Discriminators(pendingCommands, "snapshot pending commands");
                RejectE5Discriminators(diagnosticCommands, "diagnostic commands");
                RejectE5Discriminators(diagnosticEvents, "diagnostic events");
            }
            else if (schemaVersion == 19)
            {
                RejectE6Discriminators(source, "save payload");
            }
            else if (schemaVersion == 20)
            {
                RejectF0Discriminators(source, "save payload");
            }
            else if (schemaVersion == 21)
            {
                RejectF1Discriminators(source, "save payload");
            }
            else if (schemaVersion == 22)
            {
                ValidateSchema22CharacterDeathDiagnostics(diagnosticEvents);
                RejectF2Discriminators(source, "save payload");
            }
            else if (schemaVersion == 23)
            {
                ValidateSchema23CharacterDeathDiagnostics(diagnosticEvents);
                RejectF3Discriminators(source, "save payload");
            }
            else if (schemaVersion == 24)
            {
                ValidateSchema24CharacterDeathDiagnostics(source);
                RejectF4Discriminators(source, "save payload");
            }
            else if (schemaVersion == 25)
            {
                ValidateSchema24CharacterDeathDiagnostics(source);
                ValidateCurrentCharacterSuccessionDiagnostics(source);
                RejectF7Discriminators(source, "save payload");
            }
            else if (schemaVersion == 26)
            {
                ValidateSchema24CharacterDeathDiagnostics(source);
                ValidateCurrentCharacterSuccessionDiagnostics(source);
                RejectF8Discriminators(source, "save payload");
            }
            else
            {
                ValidateSchema24CharacterDeathDiagnostics(source);
                ValidateCurrentCharacterSuccessionDiagnostics(source);
                RejectF9Discriminators(source, "save payload");
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
            schemaVersion >= 20
                ? CharacterContractVersions.Snapshot
                : schemaVersion >= 6
                ? CharacterContractVersions.PreviousSnapshot
                : CharacterContractVersions.LegacySnapshot,
            schemaVersion >= 6
                ? CharacterContractVersions.Definition
                : CharacterContractVersions.LegacyDefinition,
            schemaVersion >= 20
                ? CharacterContractVersions.State
                : schemaVersion >= 6
                ? CharacterContractVersions.PreviousState
                : CharacterContractVersions.LegacyState,
            requireVersionTwoFields: schemaVersion >= 6,
            requireEducationAttainments: schemaVersion >= 20);
        if (!systemVersions.Any(version => IsSystemVersion(
                version,
                "simulation.characters",
                schemaVersion >= 20
                    ? CharacterContractVersions.Snapshot
                    : schemaVersion >= 6
                    ? CharacterContractVersions.PreviousSnapshot
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
            || snapshot["characterGuardianships"] is not JsonObject characterGuardianships
            || snapshot["characterPregnancies"] is not JsonObject characterPregnancies
            || snapshot["characterSuccessions"] is not JsonObject characterSuccessions)
        {
            throw new SaveCompatibilityException(
                "Current save schema is missing required character, relationship, career, character-resource, character-estate-holding, character-marriage, character-guardianship, character-pregnancy, or character-succession snapshot data.");
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
            CharacterContractVersions.Definition,
            CharacterContractVersions.State,
            requireVersionTwoFields: true,
            requireEducationAttainments: true);
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
        ValidateCharacterPregnancySnapshotShape(
            characterPregnancies,
            "Current save schema");
        ValidateCharacterSuccessionSnapshotShape(
            characterSuccessions,
            "Current save schema");
        ValidateCurrentCharacterDeathDiagnostics(source);
        ValidateCurrentCharacterSuccessionDiagnostics(source);
        ValidateRetainedSuccessionDeathDiagnosticsMatchSnapshot(source);
        ValidateSuccessionDeathDiagnosticSemanticEvidence(source);

        if (snapshot["systemVersions"] is not JsonArray systemVersions
            || !systemVersions.Any(IsCurrentCharacterSystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required 'simulation.characters@{CharacterContractVersions.Snapshot}' system-version data.");
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

        if (!systemVersions.Any(IsCurrentCharacterPregnancySystemVersion))
        {
            throw new SaveCompatibilityException(
                $"Current save schema is missing required '{CharacterPregnancySystem.SystemId}@{CharacterPregnancySystem.Version}' system-version data.");
        }

        if (systemVersions.Count(IsCurrentCharacterSuccessionSystemVersion) != 1
            || systemVersions.OfType<JsonObject>().Count(version => IsSystemId(
                version,
                CharacterSuccessionSystem.SystemId)) != 1)
        {
            throw new SaveCompatibilityException(
                $"Current save schema requires exactly one supported '{CharacterSuccessionSystem.SystemId}@{CharacterSuccessionSystem.Version}' system-version entry.");
        }
    }

    private static void ValidateCurrentCharacterDeathDiagnostics(JsonNode? node) =>
        ValidateCharacterDeathDiagnostics(
            node,
            CurrentDeathDiagnosticVersions(),
            requireReleasedCustodyChanges: true,
            "Current");

    private static void ValidateSchema24CharacterDeathDiagnostics(JsonNode? node) =>
        ValidateCharacterDeathDiagnostics(
            node,
            Schema24DeathDiagnosticVersions,
            requireReleasedCustodyChanges: true,
            "Schema 24");

    private static void ValidateSchema22CharacterDeathDiagnostics(JsonNode? node) =>
        ValidateCharacterDeathDiagnostics(
            node,
            CurrentDeathDiagnosticVersions() with { Death = 2 },
            requireReleasedCustodyChanges: false,
            "Schema 22");

    private static void ValidateSchema23CharacterDeathDiagnostics(JsonNode? node) =>
        ValidateCharacterDeathDiagnostics(
            node,
            CurrentDeathDiagnosticVersions() with
            {
                Death = Schema23DeathContractVersion,
            },
            requireReleasedCustodyChanges: true,
            "Schema 23");

    private static void ValidateCharacterDeathDiagnostics(
        JsonNode? node,
        DeathDiagnosticVersions versions,
        bool requireReleasedCustodyChanges,
        string description)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                ValidateCharacterDeathDiagnostics(
                    item,
                    versions,
                    requireReleasedCustodyChanges,
                    description);
            }

            return;
        }

        if (node is not JsonObject value)
        {
            return;
        }

        string? discriminator = value["$type"]?.GetValue<string>();
        if (discriminator == "resolve_household_head_death.v1"
            && (value["characterId"] is not JsonObject
                || !IsCurrentCharacterCondition(value["expectedCurrent"])
                || value["householdId"] is not JsonObject
                || value["replacementHeadCharacterId"] is not JsonObject))
        {
            throw new SaveCompatibilityException(
                $"{description} save schema contains incomplete household-head death action diagnostics.");
        }

        if (discriminator == "resolve_character_succession_death.v1"
            && (value["characterId"] is not JsonObject
                || !IsCurrentCharacterCondition(value["expectedCurrent"])
                || value["rule"] is not JsonObject rule
                || !IsCurrentSuccessionResolutionRule(rule)
                || value["expectedResolutionStateId"] is not JsonObject
                || !HasNullableObject(value, "householdId")
                || !HasNullableObject(value, "replacementHeadCharacterId")
                || (value["householdId"] is null)
                    != (value["replacementHeadCharacterId"] is null)
                || !HasNullableObject(value, "regentCharacterId")))
        {
            throw new SaveCompatibilityException(
                $"{description} save schema contains incomplete succession-death action diagnostics.");
        }

        if (discriminator == "character_condition_action_resolved.v1")
        {
            string? actionType = value["action"]?["$type"]?.GetValue<string>();
            string? outcomeType = value["outcome"]?["$type"]?.GetValue<string>();
            if ((actionType == "resolve_household_head_death.v1"
                    || outcomeType == "household_head_death_resolved.v1")
                && (actionType != "resolve_household_head_death.v1"
                    || outcomeType != "household_head_death_resolved.v1"))
            {
                throw new SaveCompatibilityException(
                    $"{description} save schema contains incomplete or mismatched household-head death action diagnostics.");
            }

            if ((actionType == "resolve_character_succession_death.v1"
                    || outcomeType
                        == "character_succession_death_resolved.v1")
                && (actionType
                        != "resolve_character_succession_death.v1"
                    || outcomeType
                        != "character_succession_death_resolved.v1"))
            {
                throw new SaveCompatibilityException(
                    $"{description} save schema contains incomplete or mismatched succession-death action diagnostics.");
            }
        }

        if (discriminator == "character_death_resolved.v1")
        {
            if (value["death"] is not JsonObject death
                || !IsCharacterDeathChange(
                    death,
                    versions,
                    requireReleasedCustodyChanges))
            {
                throw new SaveCompatibilityException(
                    $"{description} save schema contains incomplete or unsupported character-death diagnostics.");
            }
        }

        if (discriminator == "household_head_death_resolved.v1")
        {
            if (value["death"] is not JsonObject death
                || !IsCharacterDeathChange(
                    death,
                    versions,
                    requireReleasedCustodyChanges)
                || value["householdHeadChange"] is not JsonObject headChange
                || !IsHouseholdHeadChange(
                    headChange,
                    versions.HouseholdHeadChange))
            {
                throw new SaveCompatibilityException(
                    $"{description} save schema contains incomplete or unsupported household-head death diagnostics.");
            }
        }

        if (discriminator == "character_succession_death_resolved.v1")
        {
            if (value["death"] is not JsonObject death
                || !IsCharacterDeathChange(
                    death,
                    versions,
                    requireReleasedCustodyChanges)
                || !HasNullableHouseholdHeadChange(
                    value,
                    "householdHeadChange",
                    versions.HouseholdHeadChange)
                || value["succession"] is not JsonObject succession
                || !IsCurrentSuccessionResolution(succession))
            {
                throw new SaveCompatibilityException(
                    $"{description} save schema contains incomplete or unsupported succession-death diagnostics.");
            }
        }

        foreach ((_, JsonNode? child) in value)
        {
            ValidateCharacterDeathDiagnostics(
                child,
                versions,
                requireReleasedCustodyChanges,
                description);
        }
    }

    private static bool HasNullableHouseholdHeadChange(
        JsonObject value,
        string property,
        int expectedVersion) =>
        value.ContainsKey(property)
        && (value[property] is null
            || value[property] is JsonObject change
                && IsHouseholdHeadChange(change, expectedVersion));

    private static bool IsHouseholdHeadChange(
        JsonObject headChange,
        int expectedVersion) =>
        HasVersion(headChange, expectedVersion)
        && headChange["changeId"] is JsonObject
        && headChange["householdId"] is JsonObject
        && headChange["previousHeadCharacterId"] is JsonObject
        && headChange["currentHeadCharacterId"] is JsonObject
        && headChange["resolutionDate"] is JsonObject
        && HasLong(headChange, "resolutionTurnIndex")
        && headChange["sourceCommandId"] is JsonObject
        && headChange["sourceEventId"] is JsonObject;

    private static bool IsCharacterDeathChange(
        JsonObject death,
        DeathDiagnosticVersions versions,
        bool requireReleasedCustodyChanges)
    {
        return HasVersion(death, versions.Death)
            && death["deathId"] is JsonObject
            && death["conditionChange"] is JsonObject conditionChange
            && ContainsOnlyConditionChanges(
                new JsonArray(conditionChange.DeepClone()),
                versions.ConditionChange)
            && (requireReleasedCustodyChanges
                ? death["releasedCustodyChanges"] is JsonArray releasedChanges
                    && ContainsOnlyConditionChanges(
                        releasedChanges,
                        versions.ConditionChange)
                : !death.ContainsKey("releasedCustodyChanges"))
            && death["marriageChanges"] is JsonObject marriageChanges
            && IsCharacterMarriageLifecycleChanges(marriageChanges, versions)
            && death["endedGuardianships"] is JsonArray endedGuardianships
            && ContainsOnlyGuardianships(endedGuardianships, versions)
            && death["removedPregnancies"] is JsonArray removedPregnancies
            && ContainsOnlyPregnancies(removedPregnancies, versions)
            && death["careerChanges"] is JsonObject careerChanges
            && HasVersion(careerChanges, versions.CareerDeathChange)
            && careerChanges["invalidatedProposals"] is JsonArray invalidatedProposals
            && careerChanges["endedRetinueMemberships"] is JsonArray endedMemberships
            && careerChanges["endedPatronageBonds"] is JsonArray endedBonds
            && careerChanges["endedEmploymentTenures"] is JsonArray endedTenures
            && ContainsOnlyCareerStateRecords(invalidatedProposals, versions.CareerState)
            && ContainsOnlyCareerStateRecords(endedMemberships, versions.CareerState)
            && ContainsOnlyCareerStateRecords(endedBonds, versions.CareerState)
            && ContainsOnlyCareerStateRecords(endedTenures, versions.CareerState)
            && death["resolutionDate"] is JsonObject
            && HasLong(death, "resolutionTurnIndex")
            && death["sourceCommandId"] is JsonObject
            && death["sourceEventId"] is JsonObject;
    }

    private static bool IsCharacterMarriageLifecycleChanges(
        JsonObject changes,
        DeathDiagnosticVersions versions)
    {
        if (!HasVersion(changes, versions.MarriageLifecycleChangeSet)
            || !HasDefinedEnum<CharacterMarriageLifecycleReason>(changes, "reason")
            || changes["invalidatedProposals"] is not JsonArray invalidatedProposals
            || changes["invalidatedBetrothals"] is not JsonArray invalidatedBetrothals
            || changes["endedUnions"] is not JsonArray endedUnions
            || changes["cancelledInvitations"] is not JsonArray cancelledInvitations
            || changes["invalidatedRomanceRoutes"] is not JsonArray invalidatedRomanceRoutes)
        {
            return false;
        }

        JsonObject snapshot = new()
        {
            ["contractVersion"] = versions.MarriageSnapshot,
            ["practices"] = new JsonArray(),
            ["proposals"] = invalidatedProposals.DeepClone(),
            ["betrothals"] = invalidatedBetrothals.DeepClone(),
            ["unions"] = endedUnions.DeepClone(),
            ["invitations"] = cancelledInvitations.DeepClone(),
            ["romanceRoutes"] = invalidatedRomanceRoutes.DeepClone(),
            ["history"] = new JsonArray(),
        };
        try
        {
            ValidateCharacterMarriageSnapshotShape(
                snapshot,
                "Character-death marriage lifecycle changes",
                versions.MarriageSnapshot,
                requireInvitations: true,
                allowVersionTwoRoutes: true,
                versions.MarriageInvitationState,
                versions.MarriagePractice,
                versions.MarriageState,
                versions.MarriageRouteState);
            return true;
        }
        catch (SaveCompatibilityException)
        {
            return false;
        }
    }

    private static bool ContainsOnlyGuardianships(
        JsonArray guardianships,
        DeathDiagnosticVersions versions)
    {
        JsonObject snapshot = new()
        {
            ["contractVersion"] = versions.GuardianshipSnapshot,
            ["guardianships"] = guardianships.DeepClone(),
        };
        try
        {
            ValidateCharacterGuardianshipSnapshotShape(
                snapshot,
                "Character-death guardianship changes",
                versions.GuardianshipSnapshot,
                versions.GuardianshipState);
            return true;
        }
        catch (SaveCompatibilityException)
        {
            return false;
        }
    }

    private static bool ContainsOnlyPregnancies(
        JsonArray pregnancies,
        DeathDiagnosticVersions versions)
    {
        JsonObject snapshot = new()
        {
            ["contractVersion"] = versions.PregnancySnapshot,
            ["activePregnancies"] = pregnancies.DeepClone(),
        };
        try
        {
            ValidateCharacterPregnancySnapshotShape(
                snapshot,
                "Character-death pregnancy changes",
                versions.PregnancySnapshot,
                versions.PregnancyState);
            return true;
        }
        catch (SaveCompatibilityException)
        {
            return false;
        }
    }

    private static bool ContainsOnlyConditionChanges(
        JsonArray records,
        int expectedChangeVersion) =>
        records.All(item => item is JsonObject record
            && HasVersion(record, expectedChangeVersion)
            && record["changeId"] is JsonObject
            && record["characterId"] is JsonObject
            && IsCurrentCharacterCondition(record["previousCondition"])
            && IsCurrentCharacterCondition(record["currentCondition"])
            && record["resolutionDate"] is JsonObject
            && record["resolutionTurnIndex"] is JsonValue
            && record["sourceCommandId"] is JsonObject);

    private static bool IsCurrentCharacterCondition(JsonNode? node) =>
        node is JsonObject condition
        && HasDefinedEnum<CharacterVitalStatus>(condition, "vitalStatus")
        && HasDefinedEnum<CharacterHealthStatus>(condition, "healthStatus")
        && HasBoolean(condition, "isIncapacitated")
        && HasDefinedEnum<CharacterCustodyStatus>(condition, "custodyStatus")
        && HasNullableObject(condition, "custodianId");

    private static bool ContainsOnlyCareerStateRecords(
        JsonArray records,
        int expectedStateVersion) =>
        records.All(item => item is JsonObject record
            && HasVersion(record, expectedStateVersion));

    private static DeathDiagnosticVersions CurrentDeathDiagnosticVersions() => new(
        CharacterConditionContractVersions.Death,
        CharacterConditionContractVersions.Change,
        CharacterConditionContractVersions.HouseholdHeadChange,
        CharacterMarriageContractVersions.LifecycleChangeSet,
        CharacterMarriageContractVersions.Snapshot,
        CharacterMarriageContractVersions.State,
        CharacterMarriageContractVersions.RomanceInvitationState,
        CharacterMarriageContractVersions.RomanceRouteState,
        CharacterMarriageContractVersions.Practice,
        CharacterGuardianshipContractVersions.Snapshot,
        CharacterGuardianshipContractVersions.State,
        CharacterPregnancyContractVersions.Snapshot,
        CharacterPregnancyContractVersions.State,
        CareerContractVersions.DeathChange,
        CareerContractVersions.State);

    private readonly record struct DeathDiagnosticVersions(
        int Death,
        int ConditionChange,
        int HouseholdHeadChange,
        int MarriageLifecycleChangeSet,
        int MarriageSnapshot,
        int MarriageState,
        int MarriageInvitationState,
        int MarriageRouteState,
        int MarriagePractice,
        int GuardianshipSnapshot,
        int GuardianshipState,
        int PregnancySnapshot,
        int PregnancyState,
        int CareerDeathChange,
        int CareerState);

    private static void ValidateCurrentCharacterSuccessionDiagnostics(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                ValidateCurrentCharacterSuccessionDiagnostics(item);
            }

            return;
        }

        if (node is not JsonObject value)
        {
            return;
        }

        if (value["payload"] is JsonObject deathDiagnosticPayload
            && deathDiagnosticPayload["$type"]?.GetValue<string>()
                == "character_condition_action_resolved.v1"
            && deathDiagnosticPayload["action"]?["$type"]?.GetValue<string>()
                == "resolve_character_succession_death.v1"
            && !IsExactCharacterSuccessionDeathEventDiagnostic(
                value,
                deathDiagnosticPayload))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains succession-death diagnostics that are not bound to their outer event.");
        }

        if (value["payload"] is JsonObject diagnosticPayload
            && diagnosticPayload["$type"]?.GetValue<string>()
                == "character_succession_claim_action_resolved.v1"
            && !IsExactCharacterSuccessionClaimEventDiagnostic(
                value,
                diagnosticPayload))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains succession-claim diagnostics that are not bound to their outer event.");
        }

        if (value["payload"] is JsonObject supportDiagnosticPayload
            && supportDiagnosticPayload["$type"]?.GetValue<string>()
                == "character_succession_support_action_resolved.v1"
            && !IsExactCharacterSuccessionSupportEventDiagnostic(
                value,
                supportDiagnosticPayload))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains succession-support diagnostics that are not bound to their outer event.");
        }

        string? discriminator = value["$type"]?.GetValue<string>();
        if (discriminator == "character_succession_action.v1"
            && value["action"] is not JsonObject)
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete character-succession command diagnostics.");
        }

        if (discriminator == "designate_heir.v1"
            && (!HasObject(value, "heirCharacterId")
                || !HasNullableObject(value, "expectedCurrentDesignationId")))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete designate-heir action diagnostics.");
        }

        if (discriminator == "revoke_heir_designation.v1"
            && !HasObject(value, "expectedCurrentDesignationId"))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete revoke-heir-designation action diagnostics.");
        }

        if (discriminator == "character_succession_action_resolved.v1")
        {
            if (!HasObject(value, "actingCharacterId")
                || value["action"] is not JsonObject action
                || value["outcome"] is not JsonObject outcome
                || !IsExactCharacterSuccessionDiagnosticPair(
                    value["actingCharacterId"]!,
                    action,
                    outcome))
            {
                throw new SaveCompatibilityException(
                    "Current save schema contains incomplete or mismatched character-succession action diagnostics.");
            }
        }

        if (discriminator == "heir_designated.v1"
            && (value["currentDesignation"] is not JsonObject current
                || !IsCurrentHeirDesignation(current)
                || !IsActiveHeirDesignation(current)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete heir-designated outcome diagnostics.");
        }

        if (discriminator == "heir_designation_replaced.v1"
            && (value["previousDesignation"] is not JsonObject previous
                || !IsCurrentHeirDesignation(previous)
                || !IsTerminalHeirDesignation(
                    previous,
                    HeirDesignationStatus.Replaced)
                || value["currentDesignation"] is not JsonObject replacement
                || !IsCurrentHeirDesignation(replacement)
                || !IsActiveHeirDesignation(replacement)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete heir-designation-replaced outcome diagnostics.");
        }

        if (discriminator == "heir_designation_revoked.v1"
            && (value["previousDesignation"] is not JsonObject revoked
                || !IsCurrentHeirDesignation(revoked)
                || !IsTerminalHeirDesignation(
                    revoked,
                    HeirDesignationStatus.Revoked)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete heir-designation-revoked outcome diagnostics.");
        }

        if (discriminator == "character_succession_claim_action.v1"
            && value["action"] is not JsonObject)
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-claim command diagnostics.");
        }

        if (discriminator == "assert_succession_claim.v1"
            && !HasObject(value, "subjectCharacterId"))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete assert-succession-claim action diagnostics.");
        }

        if (discriminator == "withdraw_succession_claim.v1"
            && (!HasObject(value, "subjectCharacterId")
                || !HasObject(value, "expectedCurrentClaimId")))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete withdraw-succession-claim action diagnostics.");
        }

        if (discriminator == "character_succession_claim_action_resolved.v1"
            && (!HasObject(value, "actingCharacterId")
                || value["action"] is not JsonObject claimAction
                || value["outcome"] is not JsonObject claimOutcome
                || !IsExactCharacterSuccessionClaimDiagnosticPair(
                    value["actingCharacterId"]!,
                    claimAction,
                    claimOutcome)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete or mismatched succession-claim action diagnostics.");
        }

        if (discriminator == "succession_claim_asserted.v1"
            && (value["currentClaim"] is not JsonObject asserted
                || !IsActiveSuccessionClaim(asserted)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-claim-asserted outcome diagnostics.");
        }

        if (discriminator == "succession_claim_withdrawn.v1"
            && (value["previousClaim"] is not JsonObject withdrawn
                || !IsWithdrawnSuccessionClaim(withdrawn)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-claim-withdrawn outcome diagnostics.");
        }

        if (discriminator == "character_succession_support_action.v1"
            && value["action"] is not JsonObject)
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-support command diagnostics.");
        }

        if (discriminator == "declare_succession_support.v1"
            && (!HasObject(value, "subjectId")
                || !HasObject(value, "supportedCandidateId")
                || !HasNullableObject(value, "expectedCurrentSupportId")))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete declare-succession-support action diagnostics.");
        }

        if (discriminator == "withdraw_succession_support.v1"
            && (!HasObject(value, "subjectId")
                || !HasObject(value, "expectedCurrentSupportId")))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete withdraw-succession-support action diagnostics.");
        }

        if (discriminator == "character_succession_support_action_resolved.v1"
            && (!HasObject(value, "actingCharacterId")
                || value["action"] is not JsonObject supportAction
                || value["outcome"] is not JsonObject supportOutcome
                || !IsExactCharacterSuccessionSupportDiagnosticPair(
                    value["actingCharacterId"]!,
                    supportAction,
                    supportOutcome)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete or mismatched succession-support action diagnostics.");
        }

        if (discriminator == "succession_support_declared.v1"
            && (value["currentSupport"] is not JsonObject declared
                || !IsActiveSuccessionSupport(declared)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-support-declared outcome diagnostics.");
        }

        if (discriminator == "succession_support_replaced.v1"
            && (value["previousSupport"] is not JsonObject replaced
                || !IsTerminalSuccessionSupport(
                    replaced,
                    SuccessionSupportStatus.Replaced)
                || value["currentSupport"] is not JsonObject supportReplacement
                || !IsActiveSuccessionSupport(supportReplacement)
                || !HasExactSuccessionSupportReplacementCausality(
                    replaced,
                    supportReplacement)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-support-replaced outcome diagnostics.");
        }

        if (discriminator == "succession_support_withdrawn.v1"
            && (value["previousSupport"] is not JsonObject supportWithdrawn
                || !IsTerminalSuccessionSupport(
                    supportWithdrawn,
                    SuccessionSupportStatus.Withdrawn)))
        {
            throw new SaveCompatibilityException(
                "Current save schema contains incomplete succession-support-withdrawn outcome diagnostics.");
        }

        foreach ((_, JsonNode? child) in value)
        {
            ValidateCurrentCharacterSuccessionDiagnostics(child);
        }
    }

    private static void ValidateRetainedSuccessionDeathDiagnosticsMatchSnapshot(
        JsonObject source)
    {
        if (source["snapshot"]?["characterSuccessions"]?["resolutions"]
                is not JsonArray resolutions
            || source["diagnosticEvents"] is not JsonArray diagnosticEvents)
        {
            return;
        }

        Dictionary<string, JsonObject> retained = resolutions
            .OfType<JsonObject>()
            .Where(item => item["resolutionId"] is not null)
            .ToDictionary(
                item => item["resolutionId"]!.ToJsonString(),
                item => item,
                StringComparer.Ordinal);
        foreach (JsonObject diagnostic in diagnosticEvents.OfType<JsonObject>())
        {
            if (diagnostic["payload"] is not JsonObject payload
                || payload["$type"]?.GetValue<string>()
                    != "character_condition_action_resolved.v1"
                || payload["action"]?["$type"]?.GetValue<string>()
                    != "resolve_character_succession_death.v1"
                || payload["outcome"]?["succession"]
                    is not JsonObject diagnosticResolution
                || diagnosticResolution["resolutionId"] is not JsonNode
                    resolutionId
                || !retained.TryGetValue(
                    resolutionId.ToJsonString(),
                    out JsonObject? retainedResolution))
            {
                continue;
            }

            if (!JsonNode.DeepEquals(
                    diagnosticResolution,
                    retainedResolution))
            {
                throw new SaveCompatibilityException(
                    "Current save schema contains retained succession-death diagnostics that do not exactly match the authoritative resolution snapshot.");
            }
        }
    }

    private static void ValidateSuccessionDeathDiagnosticSemanticEvidence(
        JsonObject source)
    {
        if (source["snapshot"] is not JsonObject snapshotNode
            || source["diagnosticEvents"] is not JsonArray diagnosticEvents)
        {
            return;
        }

        JsonObject[] resolutionNodes = diagnosticEvents
            .OfType<JsonObject>()
            .Where(diagnostic =>
                diagnostic["payload"] is JsonObject payload
                && payload["$type"]?.GetValue<string>()
                    == "character_condition_action_resolved.v1"
                && payload["action"]?["$type"]?.GetValue<string>()
                    == "resolve_character_succession_death.v1"
                && payload["outcome"]?["succession"] is JsonObject)
            .Select(diagnostic =>
                diagnostic["payload"]!["outcome"]!["succession"]!
                    .AsObject())
            .ToArray();
        if (resolutionNodes.Length == 0)
        {
            return;
        }

        try
        {
            WorldSnapshot snapshot = snapshotNode.Deserialize<WorldSnapshot>(
                SimulationJson.CreateOptions())
                ?? throw new JsonException(
                    "Succession diagnostic snapshot is empty.");
            IReadOnlyDictionary<EntityId, CharacterDefinition> definitions =
                snapshot.Characters.CharacterDefinitions.ToDictionary(
                    item => item.Id);
            IReadOnlyDictionary<EntityId, CharacterGuardianshipState>
                guardianships = snapshot.CharacterGuardianships.Guardianships
                    .ToDictionary(item => item.GuardianshipId);
            foreach (JsonObject resolutionNode in resolutionNodes)
            {
                SuccessionResolutionState resolution =
                    resolutionNode.Deserialize<SuccessionResolutionState>(
                        SimulationJson.CreateOptions())
                    ?? throw new JsonException(
                        "Succession diagnostic resolution is empty.");
                if (!IsSemanticallyValidDiagnosticResolution(
                        resolution,
                        definitions,
                        guardianships))
                {
                    throw new SaveCompatibilityException(
                        "Current save schema contains succession-death diagnostics with invalid frozen candidate or regency evidence.");
                }
            }
        }
        catch (SaveCompatibilityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or SimulationValidationException
            or OverflowException)
        {
            throw new SaveCompatibilityException(
                $"Current save schema contains malformed succession-death semantic evidence: {exception.Message}");
        }
    }

    private static bool IsSemanticallyValidDiagnosticResolution(
        SuccessionResolutionState resolution,
        IReadOnlyDictionary<EntityId, CharacterDefinition> definitions,
        IReadOnlyDictionary<EntityId, CharacterGuardianshipState>
            guardianships)
    {
        if (!IsSemanticallyValidDiagnosticRule(resolution.Rule)
            || resolution.DisputedCandidates is null)
        {
            return false;
        }

        Dictionary<SuccessionLegalBasis, int> precedence =
            resolution.Rule.LegalBasisPrecedence
                .Select((basis, index) => (basis, index))
                .ToDictionary(item => item.basis, item => item.index);
        List<SuccessionResolutionCandidate> candidateList = [];
        if (resolution.SelectedCandidate is not null)
        {
            candidateList.Add(resolution.SelectedCandidate);
        }

        candidateList.AddRange(resolution.DisputedCandidates);
        SuccessionResolutionCandidate[] candidates =
            candidateList.ToArray();
        if (candidates.Select(item => item.CandidateCharacterId)
                .Distinct()
                .Count() != candidates.Length
            || resolution.DisputedCandidates
                .Select(item => item.CandidateCharacterId)
                .SequenceEqual(resolution.DisputedCandidates
                    .Select(item => item.CandidateCharacterId)
                    .Order()) == false
            || candidates.Any(candidate =>
                !IsSemanticallyValidDiagnosticCandidate(
                    candidate,
                    resolution.SubjectCharacterId,
                    resolution.Rule,
                    resolution.ResolutionDate,
                    definitions,
                    precedence)))
        {
            return false;
        }

        SuccessionResolutionCandidate? selected =
            resolution.SelectedCandidate;
        SuccessionRegencyReason expectedReasons =
            SuccessionRegencyReason.None;
        if (selected is not null
            && selected.CandidateAge
                < CharacterMarriageLimits.MinimumAdultAge)
        {
            expectedReasons |= SuccessionRegencyReason.Minor;
        }

        if (selected?.CandidateCondition.IsIncapacitated == true
            && resolution.Rule.CreatesRegencyForIncapacitatedSuccessor)
        {
            expectedReasons |= SuccessionRegencyReason.Incapacitated;
        }

        if (expectedReasons == SuccessionRegencyReason.None)
        {
            return resolution.Regency is null;
        }

        if (resolution.Regency is not SuccessionRegencyHook regency
            || selected is null
            || regency.SuccessorCharacterId
                != selected.CandidateCharacterId
            || regency.Reasons != expectedReasons
            || regency.SourceCustodianCharacterId
                != (selected.CandidateCondition.CustodyStatus
                        == CharacterCustodyStatus.Free
                    ? null
                    : selected.CandidateCondition.CustodianId))
        {
            return false;
        }

        if (regency.RegentCharacterId is EntityId regentId
            && (!definitions.TryGetValue(
                    regentId,
                    out CharacterDefinition? regent)
                || regentId == resolution.SubjectCharacterId
                || regentId == selected.CandidateCharacterId
                || CalculateDiagnosticAge(
                    regent.BirthDate,
                    resolution.ResolutionDate)
                    < CharacterMarriageLimits.MinimumAdultAge))
        {
            return false;
        }

        if ((regency.SourceGuardianshipId is null)
            != (regency.SourceGuardianCharacterId is null))
        {
            return false;
        }

        if (regency.SourceGuardianshipId is EntityId guardianshipId
            && (regency.SourceGuardianCharacterId is not EntityId guardianId
                || !guardianships.TryGetValue(
                    guardianshipId,
                    out CharacterGuardianshipState? guardianship)
                || guardianship.WardCharacterId
                    != selected.CandidateCharacterId
                || guardianship.GuardianCharacterId != guardianId
                || guardianship.EstablishedDate.CompareTo(
                    resolution.ResolutionDate) > 0
                || guardianship.EstablishedTurnIndex
                    > resolution.ResolutionTurnIndex
                || guardianship.EndDate is CampaignDate endDate
                    && endDate.CompareTo(resolution.ResolutionDate) < 0
                || guardianship.EndTurnIndex is long endTurnIndex
                    && endTurnIndex < resolution.ResolutionTurnIndex))
        {
            return false;
        }

        return true;
    }

    private static bool IsSemanticallyValidDiagnosticCandidate(
        SuccessionResolutionCandidate candidate,
        EntityId subjectCharacterId,
        SuccessionResolutionRule rule,
        CampaignDate resolutionDate,
        IReadOnlyDictionary<EntityId, CharacterDefinition> definitions,
        IReadOnlyDictionary<SuccessionLegalBasis, int> precedence)
    {
        if (candidate is null
            || candidate.CandidateCharacterId == subjectCharacterId
            || !definitions.TryGetValue(
                candidate.CandidateCharacterId,
                out CharacterDefinition? definition)
            || candidate.CandidateAge != CalculateDiagnosticAge(
                definition.BirthDate,
                resolutionDate)
            || candidate.CandidateAge
                < rule.CandidateEligibility.MinimumCandidateAge
            || candidate.CandidateCondition is null
            || candidate.CandidateCondition.VitalStatus
                != CharacterVitalStatus.Alive
            || candidate.CandidateCondition.IsIncapacitated
                && !rule.CandidateEligibility
                    .AllowsIncapacitatedCandidates
            || !rule.CandidateEligibility.AllowedCustodyStatuses.Contains(
                candidate.CandidateCondition.CustodyStatus)
            || (candidate.CandidateCondition.CustodyStatus
                    == CharacterCustodyStatus.Free)
                != (candidate.CandidateCondition.CustodianId is null)
            || candidate.LegalBases is not { Count: > 0 }
            || candidate.ActiveSupportIds is null
            || candidate.ActiveSupportIds.Any(item => !item.IsValid)
            || candidate.ActiveSupportIds.Distinct().Count()
                != candidate.ActiveSupportIds.Count
            || !candidate.ActiveSupportIds.SequenceEqual(
                candidate.ActiveSupportIds.Order())
            || candidate.ActiveClaimId is EntityId claimId
                && !claimId.IsValid)
        {
            return false;
        }

        foreach (SuccessionLegalBasisEvidence evidence
                 in candidate.LegalBases)
        {
            if (!IsSemanticallyValidDiagnosticBasis(
                    evidence,
                    precedence)
                || evidence.DescendantGeneration
                    > rule.CandidateEligibility
                        .MaximumDescendantGeneration
                || evidence.CollateralDistance
                    > rule.MaximumCollateralDistance)
            {
                return false;
            }
        }

        int expectedPrecedence = candidate.LegalBases.Min(
            item => precedence[item.Basis]);
        int expectedDistance = candidate.LegalBases
            .Where(item => precedence[item.Basis] == expectedPrecedence)
            .Select(item => item.DescendantGeneration
                ?? item.CollateralDistance
                ?? 0)
            .Min();
        return candidate.LegalBasisPrecedenceIndex == expectedPrecedence
            && candidate.KinshipDistance == expectedDistance
            && DiagnosticSerializedEquals(
                candidate,
                candidate.Canonicalize());
    }

    private static bool IsSemanticallyValidDiagnosticRule(
        SuccessionResolutionRule? rule)
    {
        if (rule is null
            || rule.ContractVersion
                != CharacterSuccessionContractVersions.ResolutionRule
            || rule.CandidateEligibility is null
            || rule.CandidateEligibility.ContractVersion
                != CharacterSuccessionContractVersions
                    .CandidateEligibilityRule
            || rule.CandidateEligibility.AllowedBases
                is not { Count: > 0 }
            || rule.CandidateEligibility.AllowedCustodyStatuses
                is not { Count: > 0 }
            || rule.LegalBasisPrecedence is null
            || rule.AllowedCollateralKinds is null
            || rule.CandidateEligibility.MaximumDescendantGeneration
                is < 1
                    or > CharacterSuccessionLimits
                        .MaximumEvaluatedDescendantGeneration
            || rule.CandidateEligibility.MinimumCandidateAge is < 0
                or > CharacterSuccessionLimits
                    .MaximumConfiguredMinimumCandidateAge
            || rule.CandidateEligibility.AllowedBases.Any(
                item => !Enum.IsDefined(item))
            || rule.CandidateEligibility.AllowedBases.Distinct().Count()
                != rule.CandidateEligibility.AllowedBases.Count
            || rule.CandidateEligibility.AllowedCustodyStatuses.Any(
                item => !Enum.IsDefined(item))
            || rule.CandidateEligibility.AllowedCustodyStatuses
                    .Distinct()
                    .Count()
                != rule.CandidateEligibility.AllowedCustodyStatuses.Count
            || rule.AllowedCollateralKinds.Any(
                item => !Enum.IsDefined(item))
            || rule.AllowedCollateralKinds.Distinct().Count()
                != rule.AllowedCollateralKinds.Count
            || !Enum.IsDefined(rule.ContestResolutionMode)
            || !Enum.IsDefined(rule.NoAcceptedSuccessorBehavior)
            || rule.MaximumCandidates is < 1
                or > CharacterSuccessionLimits.MaximumResolutionCandidates
            || rule.MaximumDisputedCandidates is < 1
                or > CharacterSuccessionLimits.MaximumDisputedCandidates
            || rule.MaximumDisputedCandidates > rule.MaximumCandidates
            || (rule.AllowedCollateralKinds.Count == 0
                ? rule.MaximumCollateralDistance != 0
                : rule.MaximumCollateralDistance is < 2
                    or > CharacterSuccessionLimits
                        .MaximumCollateralDistance)
            || !DiagnosticSerializedEquals(rule, rule.Canonicalize()))
        {
            return false;
        }

        HashSet<SuccessionLegalBasis> expectedBases =
            rule.CandidateEligibility.AllowedBases
                .Select(ToDiagnosticLegalBasis)
                .ToHashSet();
        if (rule.IncludesPrincipalSpouse)
        {
            expectedBases.Add(SuccessionLegalBasis.PrincipalSpouse);
        }

        foreach (ParentChildLinkKind kind
                 in rule.AllowedCollateralKinds)
        {
            expectedBases.Add(ToDiagnosticCollateralLegalBasis(kind));
        }

        return rule.LegalBasisPrecedence.All(Enum.IsDefined)
            && rule.LegalBasisPrecedence.Distinct().Count()
                == rule.LegalBasisPrecedence.Count
            && rule.LegalBasisPrecedence.Count == expectedBases.Count
            && rule.LegalBasisPrecedence.ToHashSet()
                .SetEquals(expectedBases);
    }

    private static SuccessionLegalBasis ToDiagnosticLegalBasis(
        SuccessionCandidateBasis basis) => basis switch
        {
            SuccessionCandidateBasis.ActiveDesignation =>
                SuccessionLegalBasis.ActiveDesignation,
            SuccessionCandidateBasis.BiologicalDescendant =>
                SuccessionLegalBasis.BiologicalDescendant,
            SuccessionCandidateBasis.LegalAdoptiveDescendant =>
                SuccessionLegalBasis.LegalAdoptiveDescendant,
            SuccessionCandidateBasis.UnspecifiedLegacyDescendant =>
                SuccessionLegalBasis.UnspecifiedLegacyDescendant,
            _ => throw new ArgumentOutOfRangeException(nameof(basis)),
        };

    private static SuccessionLegalBasis ToDiagnosticCollateralLegalBasis(
        ParentChildLinkKind kind) => kind switch
        {
            ParentChildLinkKind.Biological =>
                SuccessionLegalBasis.BiologicalCollateral,
            ParentChildLinkKind.LegalAdoptive =>
                SuccessionLegalBasis.LegalAdoptiveCollateral,
            ParentChildLinkKind.UnspecifiedLegacy =>
                SuccessionLegalBasis.UnspecifiedLegacyCollateral,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static bool IsSemanticallyValidDiagnosticBasis(
        SuccessionLegalBasisEvidence evidence,
        IReadOnlyDictionary<SuccessionLegalBasis, int> precedence)
    {
        if (evidence is null
            || !precedence.ContainsKey(evidence.Basis))
        {
            return false;
        }

        return evidence.Basis switch
        {
            SuccessionLegalBasis.ActiveDesignation =>
                evidence.SourceDesignationId is EntityId designationId
                && designationId.IsValid
                && evidence.DescendantGeneration is null
                && evidence.CollateralDistance is null
                && evidence.SourceMarriageUnionId is null
                && evidence.SharedAncestorCharacterId is null,
            SuccessionLegalBasis.BiologicalDescendant
                or SuccessionLegalBasis.LegalAdoptiveDescendant
                or SuccessionLegalBasis.UnspecifiedLegacyDescendant =>
                    evidence.DescendantGeneration is >= 1
                    && evidence.CollateralDistance is null
                    && evidence.SourceDesignationId is null
                    && evidence.SourceMarriageUnionId is null
                    && evidence.SharedAncestorCharacterId is null,
            SuccessionLegalBasis.PrincipalSpouse =>
                evidence.SourceMarriageUnionId is EntityId unionId
                && unionId.IsValid
                && evidence.DescendantGeneration is null
                && evidence.CollateralDistance is null
                && evidence.SourceDesignationId is null
                && evidence.SharedAncestorCharacterId is null,
            SuccessionLegalBasis.BiologicalCollateral
                or SuccessionLegalBasis.LegalAdoptiveCollateral
                or SuccessionLegalBasis.UnspecifiedLegacyCollateral =>
                    evidence.CollateralDistance is >= 2
                    && evidence.SharedAncestorCharacterId
                        is EntityId ancestorId
                    && ancestorId.IsValid
                    && evidence.DescendantGeneration is null
                    && evidence.SourceDesignationId is null
                    && evidence.SourceMarriageUnionId is null,
            _ => false,
        };
    }

    private static int CalculateDiagnosticAge(
        CampaignDate birthDate,
        CampaignDate currentDate)
    {
        int age = currentDate.Year - birthDate.Year;
        if (currentDate.Month < birthDate.Month
            || currentDate.Month == birthDate.Month
                && currentDate.Day < birthDate.Day)
        {
            age--;
        }

        return age;
    }

    private static bool IsExactCharacterSuccessionDiagnosticPair(
        JsonNode actingCharacterId,
        JsonObject action,
        JsonObject outcome)
    {
        string? actionType = action["$type"]?.GetValue<string>();
        string? outcomeType = outcome["$type"]?.GetValue<string>();
        if (actionType == "designate_heir.v1"
            && action["heirCharacterId"] is JsonObject heirCharacterId
            && action.ContainsKey("expectedCurrentDesignationId"))
        {
            if (outcomeType == "heir_designated.v1"
                && action["expectedCurrentDesignationId"] is null
                && outcome["currentDesignation"] is JsonObject current)
            {
                return IsActiveHeirDesignation(current)
                    && !JsonNode.DeepEquals(actingCharacterId, heirCharacterId)
                    && JsonNode.DeepEquals(
                        actingCharacterId,
                        current["designatorCharacterId"])
                    && JsonNode.DeepEquals(
                        heirCharacterId,
                        current["heirCharacterId"]);
            }

            if (outcomeType == "heir_designation_replaced.v1"
                && action["expectedCurrentDesignationId"] is JsonObject expectedCurrent
                && outcome["previousDesignation"] is JsonObject previous
                && outcome["currentDesignation"] is JsonObject replacement)
            {
                return IsTerminalHeirDesignation(
                        previous,
                        HeirDesignationStatus.Replaced)
                    && IsActiveHeirDesignation(replacement)
                    && !JsonNode.DeepEquals(actingCharacterId, heirCharacterId)
                    && !JsonNode.DeepEquals(
                        previous["heirCharacterId"],
                        replacement["heirCharacterId"])
                    && !JsonNode.DeepEquals(
                        previous["designationId"],
                        replacement["designationId"])
                    && JsonNode.DeepEquals(
                        actingCharacterId,
                        previous["designatorCharacterId"])
                    && JsonNode.DeepEquals(
                        actingCharacterId,
                        replacement["designatorCharacterId"])
                    && JsonNode.DeepEquals(
                        heirCharacterId,
                        replacement["heirCharacterId"])
                    && JsonNode.DeepEquals(
                        expectedCurrent,
                        previous["designationId"])
                    && HasExactReplacementCausality(previous, replacement);
            }
        }

        return actionType == "revoke_heir_designation.v1"
            && action["expectedCurrentDesignationId"] is JsonObject expectedCurrentId
            && outcomeType == "heir_designation_revoked.v1"
            && outcome["previousDesignation"] is JsonObject revoked
            && IsTerminalHeirDesignation(revoked, HeirDesignationStatus.Revoked)
            && JsonNode.DeepEquals(
                actingCharacterId,
                revoked["designatorCharacterId"])
            && JsonNode.DeepEquals(expectedCurrentId, revoked["designationId"]);
    }

    private static bool IsActiveHeirDesignation(JsonObject designation) =>
        IsCurrentHeirDesignation(designation)
        && HasEnumValue(
            designation,
            "status",
            HeirDesignationStatus.Active)
        && designation["resolutionDate"] is null
        && designation["resolutionTurnIndex"] is null
        && designation["resolutionCommandId"] is null
        && designation["resolutionEventId"] is null;

    private static bool IsExactCharacterSuccessionClaimDiagnosticPair(
        JsonNode actingCharacterId,
        JsonObject action,
        JsonObject outcome)
    {
        string? actionType = action["$type"]?.GetValue<string>();
        string? outcomeType = outcome["$type"]?.GetValue<string>();
        if (action["subjectCharacterId"] is not JsonObject subjectCharacterId)
        {
            return false;
        }

        if (actionType == "assert_succession_claim.v1"
            && outcomeType == "succession_claim_asserted.v1"
            && outcome["currentClaim"] is JsonObject asserted)
        {
            return IsActiveSuccessionClaim(asserted)
                && !JsonNode.DeepEquals(actingCharacterId, subjectCharacterId)
                && JsonNode.DeepEquals(
                    actingCharacterId,
                    asserted["claimantCharacterId"])
                && JsonNode.DeepEquals(
                    subjectCharacterId,
                    asserted["subjectCharacterId"]);
        }

        return actionType == "withdraw_succession_claim.v1"
            && action["expectedCurrentClaimId"] is JsonObject expectedCurrentClaimId
            && outcomeType == "succession_claim_withdrawn.v1"
            && outcome["previousClaim"] is JsonObject withdrawn
            && IsWithdrawnSuccessionClaim(withdrawn)
            && !JsonNode.DeepEquals(actingCharacterId, subjectCharacterId)
            && JsonNode.DeepEquals(
                actingCharacterId,
                withdrawn["claimantCharacterId"])
            && JsonNode.DeepEquals(
                subjectCharacterId,
                withdrawn["subjectCharacterId"])
            && JsonNode.DeepEquals(
                expectedCurrentClaimId,
                withdrawn["claimId"]);
    }

    private static bool IsExactCharacterSuccessionSupportDiagnosticPair(
        JsonNode actingCharacterId,
        JsonObject action,
        JsonObject outcome)
    {
        string? actionType = action["$type"]?.GetValue<string>();
        string? outcomeType = outcome["$type"]?.GetValue<string>();
        if (action["subjectId"] is not JsonObject subjectId)
        {
            return false;
        }

        if (actionType == "declare_succession_support.v1"
            && action["supportedCandidateId"] is JsonObject supportedCandidateId
            && action.ContainsKey("expectedCurrentSupportId"))
        {
            if (outcomeType == "succession_support_declared.v1"
                && action["expectedCurrentSupportId"] is null
                && outcome["currentSupport"] is JsonObject declared)
            {
                return IsActiveSuccessionSupport(declared)
                    && AreDistinct(
                        actingCharacterId,
                        subjectId,
                        supportedCandidateId)
                    && JsonNode.DeepEquals(
                        actingCharacterId,
                        declared["supporterId"])
                    && JsonNode.DeepEquals(subjectId, declared["subjectId"])
                    && JsonNode.DeepEquals(
                        supportedCandidateId,
                        declared["supportedCandidateId"]);
            }

            if (outcomeType == "succession_support_replaced.v1"
                && action["expectedCurrentSupportId"] is JsonObject expectedCurrent
                && outcome["previousSupport"] is JsonObject previous
                && outcome["currentSupport"] is JsonObject replacement)
            {
                return IsTerminalSuccessionSupport(
                        previous,
                        SuccessionSupportStatus.Replaced)
                    && IsActiveSuccessionSupport(replacement)
                    && AreDistinct(
                        actingCharacterId,
                        subjectId,
                        supportedCandidateId)
                    && !JsonNode.DeepEquals(
                        previous["supportedCandidateId"],
                        replacement["supportedCandidateId"])
                    && JsonNode.DeepEquals(
                        actingCharacterId,
                        previous["supporterId"])
                    && JsonNode.DeepEquals(
                        actingCharacterId,
                        replacement["supporterId"])
                    && JsonNode.DeepEquals(subjectId, previous["subjectId"])
                    && JsonNode.DeepEquals(subjectId, replacement["subjectId"])
                    && JsonNode.DeepEquals(
                        supportedCandidateId,
                        replacement["supportedCandidateId"])
                    && JsonNode.DeepEquals(
                        expectedCurrent,
                        previous["supportId"])
                    && HasExactSuccessionSupportReplacementCausality(
                        previous,
                        replacement);
            }
        }

        return actionType == "withdraw_succession_support.v1"
            && action["expectedCurrentSupportId"] is JsonObject expectedCurrentSupportId
            && outcomeType == "succession_support_withdrawn.v1"
            && outcome["previousSupport"] is JsonObject withdrawn
            && IsTerminalSuccessionSupport(
                withdrawn,
                SuccessionSupportStatus.Withdrawn)
            && !JsonNode.DeepEquals(actingCharacterId, subjectId)
            && JsonNode.DeepEquals(
                actingCharacterId,
                withdrawn["supporterId"])
            && JsonNode.DeepEquals(subjectId, withdrawn["subjectId"])
            && JsonNode.DeepEquals(
                expectedCurrentSupportId,
                withdrawn["supportId"]);
    }

    private static bool AreDistinct(
        JsonNode first,
        JsonNode second,
        JsonNode third) =>
        !JsonNode.DeepEquals(first, second)
        && !JsonNode.DeepEquals(first, third)
        && !JsonNode.DeepEquals(second, third);

    private static bool IsCurrentSuccessionClaim(JsonObject claim) =>
        HasVersion(claim, CharacterSuccessionContractVersions.ClaimState)
        && HasObject(claim, "claimId")
        && HasObject(claim, "subjectCharacterId")
        && HasObject(claim, "claimantCharacterId")
        && !JsonNode.DeepEquals(
            claim["subjectCharacterId"],
            claim["claimantCharacterId"])
        && HasDefinedEnum<SuccessionClaimOrigin>(claim, "origin")
        && HasObject(claim, "assertedDate")
        && HasLong(claim, "assertedTurnIndex")
        && HasObject(claim, "sourceCommandId")
        && HasObject(claim, "sourceEventId")
        && HasDefinedEnum<SuccessionClaimStatus>(claim, "status")
        && HasNullableObject(claim, "withdrawalDate")
        && HasNullableLong(claim, "withdrawalTurnIndex")
        && HasNullableObject(claim, "withdrawalCommandId")
        && HasNullableObject(claim, "withdrawalEventId");

    private static bool IsActiveSuccessionClaim(JsonObject claim) =>
        IsCurrentSuccessionClaim(claim)
        && HasExactSuccessionClaimCausality(claim)
        && HasEnumValue(claim, "status", SuccessionClaimStatus.Active)
        && claim["withdrawalDate"] is null
        && claim["withdrawalTurnIndex"] is null
        && claim["withdrawalCommandId"] is null
        && claim["withdrawalEventId"] is null;

    private static bool IsWithdrawnSuccessionClaim(JsonObject claim) =>
        IsCurrentSuccessionClaim(claim)
        && HasExactSuccessionClaimCausality(claim)
        && HasEnumValue(claim, "status", SuccessionClaimStatus.Withdrawn)
        && claim["withdrawalDate"] is JsonObject
        && claim["withdrawalTurnIndex"] is JsonValue
        && claim["withdrawalCommandId"] is JsonObject
        && claim["withdrawalEventId"] is JsonObject
        && !JsonNode.DeepEquals(
            claim["sourceCommandId"],
            claim["withdrawalCommandId"])
        && !JsonNode.DeepEquals(
            claim["sourceEventId"],
            claim["withdrawalEventId"]);

    private static bool IsActiveSuccessionSupport(JsonObject support) =>
        IsCurrentSuccessionSupport(support)
        && HasExactSuccessionSupportCausality(support)
        && HasEnumValue(support, "status", SuccessionSupportStatus.Active)
        && support["resolutionDate"] is null
        && support["resolutionTurnIndex"] is null
        && support["resolutionCommandId"] is null
        && support["resolutionEventId"] is null;

    private static bool IsTerminalSuccessionSupport(
        JsonObject support,
        SuccessionSupportStatus expectedStatus) =>
        IsCurrentSuccessionSupport(support)
        && HasExactSuccessionSupportCausality(support)
        && HasEnumValue(support, "status", expectedStatus)
        && support["resolutionDate"] is JsonObject
        && support["resolutionTurnIndex"] is JsonValue
        && support["resolutionCommandId"] is JsonObject
        && support["resolutionEventId"] is JsonObject
        && !JsonNode.DeepEquals(
            support["sourceCommandId"],
            support["resolutionCommandId"])
        && !JsonNode.DeepEquals(
            support["sourceEventId"],
            support["resolutionEventId"]);

    private static bool IsExactCharacterSuccessionClaimEventDiagnostic(
        JsonObject campaignEvent,
        JsonObject payload)
    {
        if (campaignEvent["eventId"] is not JsonObject eventIdNode
            || campaignEvent["causalId"] is not JsonObject causalIdNode
            || campaignEvent["resolutionDate"] is not JsonObject resolutionDateNode
            || payload["outcome"] is not JsonObject outcome)
        {
            return false;
        }

        try
        {
            EntityId eventId = eventIdNode.Deserialize<EntityId>(CanonicalJson.Options);
            EntityId causalId = causalIdNode.Deserialize<EntityId>(CanonicalJson.Options);
            CampaignDate resolutionDate = resolutionDateNode.Deserialize<CampaignDate>(
                CanonicalJson.Options);
            if (eventId != CharacterSuccessionIds.DeriveClaimActionEventId(
                    resolutionDate,
                    causalId))
            {
                return false;
            }

            string? outcomeType = outcome["$type"]?.GetValue<string>();
            if (outcomeType == "succession_claim_asserted.v1"
                && outcome["currentClaim"] is JsonObject asserted)
            {
                return JsonNode.DeepEquals(asserted["sourceCommandId"], causalIdNode)
                    && JsonNode.DeepEquals(asserted["sourceEventId"], eventIdNode)
                    && JsonNode.DeepEquals(asserted["assertedDate"], resolutionDateNode);
            }

            return outcomeType == "succession_claim_withdrawn.v1"
                && outcome["previousClaim"] is JsonObject withdrawn
                && JsonNode.DeepEquals(withdrawn["withdrawalCommandId"], causalIdNode)
                && JsonNode.DeepEquals(withdrawn["withdrawalEventId"], eventIdNode)
                && JsonNode.DeepEquals(withdrawn["withdrawalDate"], resolutionDateNode);
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or SimulationValidationException
            or OverflowException)
        {
            return false;
        }
    }

    private static bool IsExactCharacterSuccessionDeathEventDiagnostic(
        JsonObject campaignEvent,
        JsonObject payload)
    {
        if (campaignEvent["eventId"] is not JsonObject eventIdNode
            || campaignEvent["causalId"] is not JsonObject causalIdNode
            || campaignEvent["resolutionDate"] is not JsonObject resolutionDateNode
            || payload["action"] is not JsonObject actionNode
            || payload["outcome"] is not JsonObject outcomeNode
            || outcomeNode["death"] is not JsonObject deathNode
            || outcomeNode["succession"] is not JsonObject successionNode)
        {
            return false;
        }

        try
        {
            EntityId eventId = eventIdNode.Deserialize<EntityId>(
                CanonicalJson.Options);
            EntityId causalId = causalIdNode.Deserialize<EntityId>(
                CanonicalJson.Options);
            CampaignDate resolutionDate = resolutionDateNode.Deserialize<CampaignDate>(
                CanonicalJson.Options);
            CampaignEvent typedEvent = campaignEvent.Deserialize<CampaignEvent>(
                SimulationJson.CreateOptions())
                ?? throw new JsonException(
                    "Succession-death diagnostic event is empty.");
            if (eventId != CharacterConditionIds.DeriveActionEventId(
                    resolutionDate,
                    causalId)
                || typedEvent.Phase != ResolutionPhase.Commands
                || typedEvent.CausalId != causalId
                || typedEvent.Payload
                    is not CharacterConditionActionResolvedEventPayload typedPayload
                || typedPayload.ActingActorId
                    != CharacterConditionSystem.AuthoritativeActorId
                || typedPayload.RelationshipMemoryConsequence is not null
                || typedPayload.Action
                    is not ResolveCharacterSuccessionDeathAction action
                || typedPayload.Outcome
                    is not CharacterSuccessionDeathResolvedOutcome outcome
                || typedEvent.AffectedIds is null
                || !typedEvent.AffectedIds.SequenceEqual(
                    WorldState.GetCharacterConditionActionAffectedIds(
                        typedPayload,
                        eventId)))
            {
                return false;
            }

            CharacterDeathChange death = outcome.Death;
            SuccessionResolutionState succession = outcome.Succession;
            if (action.CharacterId != death.ConditionChange.CharacterId
                || action.CharacterId != succession.SubjectCharacterId
                || action.ExpectedCurrent != death.ConditionChange.PreviousCondition
                || death.DeathId != succession.DeathId
                || death.SourceCommandId != causalId
                || death.SourceEventId != eventId
                || death.ConditionChange.SourceCommandId != causalId
                || succession.SourceCommandId != causalId
                || succession.SourceEventId != eventId
                || death.ResolutionDate != resolutionDate
                || death.ConditionChange.ResolutionDate != resolutionDate
                || succession.ResolutionDate != resolutionDate
                || death.ResolutionTurnIndex
                    != death.ConditionChange.ResolutionTurnIndex
                || death.ResolutionTurnIndex
                    != succession.ResolutionTurnIndex
                || death.DeathId != CharacterConditionIds.DeriveDeathId(
                    eventId,
                    action.CharacterId)
                || succession.ResolutionId
                    != CharacterSuccessionIds.DeriveResolutionId(
                        eventId,
                        action.CharacterId)
                || !DiagnosticSerializedEquals(
                    action.Rule.Canonicalize(),
                    succession.Rule)
                || action.RegentCharacterId
                    != succession.Regency?.RegentCharacterId
                || !IsExactSuccessionResolutionStatus(succession)
                || !IsExactSuccessionInheritanceBinding(
                    succession,
                    action.CharacterId)
                || !DiagnosticSerializedEquals(
                    ResolveDiagnosticCampaignContinuity(succession),
                    succession.CurrentCampaignContinuity))
            {
                return false;
            }

            if (action.HouseholdId is null)
            {
                return action.ReplacementHeadCharacterId is null
                    && outcome.HouseholdHeadChange is null;
            }

            return action.ReplacementHeadCharacterId is EntityId replacementId
                && outcome.HouseholdHeadChange is HouseholdHeadChange headChange
                && headChange.HouseholdId == action.HouseholdId
                && headChange.PreviousHeadCharacterId == action.CharacterId
                && headChange.CurrentHeadCharacterId == replacementId
                && headChange.SourceCommandId == causalId
                && headChange.SourceEventId == eventId
                && headChange.ResolutionDate == resolutionDate
                && headChange.ResolutionTurnIndex
                    == succession.ResolutionTurnIndex;
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or SimulationValidationException
            or OverflowException)
        {
            return false;
        }
    }

    private static bool IsExactSuccessionInheritanceBinding(
        SuccessionResolutionState resolution,
        EntityId subjectCharacterId)
    {
        if (resolution.Inheritance is null
            || resolution.Inheritance.EstateTransfers is null)
        {
            return false;
        }

        EntityId? selectedCharacterId =
            resolution.SelectedCandidate?.CandidateCharacterId;
        if (selectedCharacterId is null)
        {
            return resolution.Inheritance.WealthTransfer is null
                && resolution.Inheritance.EstateTransfers.Count == 0;
        }

        if (resolution.Inheritance.WealthTransfer is WealthTransferredOutcome wealth
            && (wealth.Transfer.SourceCharacterId != subjectCharacterId
                || wealth.Transfer.RecipientCharacterId != selectedCharacterId
                || wealth.Transfer.ResolutionDate != resolution.ResolutionDate
                || wealth.Transfer.ResolutionTurnIndex
                    != resolution.ResolutionTurnIndex
                || wealth.Transfer.SourceCommandId
                    != resolution.SourceCommandId))
        {
            return false;
        }

        SuccessionEstateTransfer[] estateTransfers =
            resolution.Inheritance.EstateTransfers.ToArray();
        return estateTransfers.Select(item => item.EstateId).Distinct().Count()
                == estateTransfers.Length
            && estateTransfers.Select(item => item.EstateId).SequenceEqual(
                estateTransfers.Select(item => item.EstateId).Order())
            && estateTransfers.All(transfer =>
                transfer.PreviousOwnerCharacterId == subjectCharacterId
                && transfer.CurrentOwnerCharacterId == selectedCharacterId);
    }

    private static bool IsExactSuccessionResolutionStatus(
        SuccessionResolutionState resolution) =>
        resolution.Status switch
        {
            SuccessionResolutionStatus.Selected =>
                resolution.SelectedCandidate is not null
                && resolution.DisputedCandidates is { Count: 0 }
                && resolution.EligibleCandidateCount >= 1,
            SuccessionResolutionStatus.Disputed =>
                resolution.SelectedCandidate is null
                && resolution.DisputedCandidates is { Count: >= 2 }
                && resolution.EligibleCandidateCount
                    >= resolution.DisputedCandidates.Count,
            SuccessionResolutionStatus.NoSuccessor =>
                resolution.SelectedCandidate is null
                && resolution.DisputedCandidates is { Count: 0 }
                && resolution.EligibleCandidateCount == 0,
            _ => false,
        };

    private static PlayerCampaignContinuityState?
        ResolveDiagnosticCampaignContinuity(
            SuccessionResolutionState resolution)
    {
        PlayerCampaignContinuityState? previous =
            resolution.PreviousCampaignContinuity;
        if (previous?.Status != PlayerCampaignContinuityStatus.Active
            || previous.ControlledCharacterId
                != resolution.SubjectCharacterId)
        {
            return previous;
        }

        if (resolution.Status == SuccessionResolutionStatus.Selected)
        {
            return new(
                CharacterSuccessionContractVersions.CampaignContinuity,
                PlayerCampaignContinuityStatus.Active,
                resolution.SelectedCandidate!.CandidateCharacterId,
                resolution.ResolutionDate,
                resolution.ResolutionTurnIndex,
                resolution.SourceCommandId,
                resolution.SourceEventId);
        }

        return new(
            CharacterSuccessionContractVersions.CampaignContinuity,
            resolution.Rule.NoAcceptedSuccessorBehavior
                == SuccessionNoAcceptedSuccessorBehavior.EndCampaign
                    ? PlayerCampaignContinuityStatus.Ended
                    : PlayerCampaignContinuityStatus
                        .ContinueWithoutControlledCharacter,
            null,
            resolution.ResolutionDate,
            resolution.ResolutionTurnIndex,
            resolution.SourceCommandId,
            resolution.SourceEventId);
    }

    private static bool DiagnosticSerializedEquals<T>(T left, T right)
    {
        JsonSerializerOptions options = SimulationJson.CreateOptions();
        return StringComparer.Ordinal.Equals(
            JsonSerializer.Serialize(left, options),
            JsonSerializer.Serialize(right, options));
    }

    private static bool IsExactCharacterSuccessionSupportEventDiagnostic(
        JsonObject campaignEvent,
        JsonObject payload)
    {
        if (campaignEvent["eventId"] is not JsonObject eventIdNode
            || campaignEvent["causalId"] is not JsonObject causalIdNode
            || campaignEvent["resolutionDate"] is not JsonObject resolutionDateNode
            || payload["outcome"] is not JsonObject outcome)
        {
            return false;
        }

        try
        {
            EntityId eventId = eventIdNode.Deserialize<EntityId>(CanonicalJson.Options);
            EntityId causalId = causalIdNode.Deserialize<EntityId>(CanonicalJson.Options);
            CampaignDate resolutionDate = resolutionDateNode.Deserialize<CampaignDate>(
                CanonicalJson.Options);
            if (eventId != CharacterSuccessionIds.DeriveSupportActionEventId(
                    resolutionDate,
                    causalId))
            {
                return false;
            }

            CampaignEvent typedEvent = campaignEvent.Deserialize<CampaignEvent>(
                SimulationJson.CreateOptions())
                ?? throw new JsonException(
                    "Succession-support diagnostic event is empty.");
            if (typedEvent.Phase != ResolutionPhase.Commands
                || typedEvent.Payload
                    is not CharacterSuccessionSupportActionResolvedEventPayload
                        typedPayload
                || typedEvent.AffectedIds is null
                || !typedEvent.AffectedIds.SequenceEqual(
                    WorldState.GetCharacterSuccessionSupportActionAffectedIds(
                        typedPayload)))
            {
                return false;
            }

            string? outcomeType = outcome["$type"]?.GetValue<string>();
            if (outcomeType == "succession_support_declared.v1"
                && outcome["currentSupport"] is JsonObject declared)
            {
                return JsonNode.DeepEquals(declared["sourceCommandId"], causalIdNode)
                    && JsonNode.DeepEquals(declared["sourceEventId"], eventIdNode)
                    && JsonNode.DeepEquals(declared["declaredDate"], resolutionDateNode);
            }

            if (outcomeType == "succession_support_replaced.v1"
                && outcome["previousSupport"] is JsonObject replaced
                && outcome["currentSupport"] is JsonObject replacement)
            {
                return JsonNode.DeepEquals(
                        replaced["resolutionCommandId"],
                        causalIdNode)
                    && JsonNode.DeepEquals(
                        replaced["resolutionEventId"],
                        eventIdNode)
                    && JsonNode.DeepEquals(
                        replaced["resolutionDate"],
                        resolutionDateNode)
                    && JsonNode.DeepEquals(
                        replacement["sourceCommandId"],
                        causalIdNode)
                    && JsonNode.DeepEquals(
                        replacement["sourceEventId"],
                        eventIdNode)
                    && JsonNode.DeepEquals(
                        replacement["declaredDate"],
                        resolutionDateNode);
            }

            return outcomeType == "succession_support_withdrawn.v1"
                && outcome["previousSupport"] is JsonObject withdrawn
                && JsonNode.DeepEquals(
                    withdrawn["resolutionCommandId"],
                    causalIdNode)
                && JsonNode.DeepEquals(
                    withdrawn["resolutionEventId"],
                    eventIdNode)
                && JsonNode.DeepEquals(
                    withdrawn["resolutionDate"],
                    resolutionDateNode);
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or SimulationValidationException
            or OverflowException)
        {
            return false;
        }
    }

    private static bool HasExactSuccessionClaimCausality(JsonObject claim)
    {
        try
        {
            EntityId claimId = claim["claimId"]!.Deserialize<EntityId>(
                CanonicalJson.Options);
            EntityId subjectCharacterId = claim["subjectCharacterId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            EntityId claimantCharacterId = claim["claimantCharacterId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            CampaignDate assertedDate = claim["assertedDate"]!
                .Deserialize<CampaignDate>(CanonicalJson.Options);
            long assertedTurnIndex = claim["assertedTurnIndex"]!.GetValue<long>();
            EntityId sourceCommandId = claim["sourceCommandId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            EntityId sourceEventId = claim["sourceEventId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            if (assertedTurnIndex < 0
                || sourceEventId != CharacterSuccessionIds.DeriveClaimActionEventId(
                    assertedDate,
                    sourceCommandId)
                || claimId != CharacterSuccessionIds.DeriveClaimId(
                    sourceEventId,
                    subjectCharacterId,
                    claimantCharacterId))
            {
                return false;
            }

            if (HasEnumValue(claim, "status", SuccessionClaimStatus.Active))
            {
                return true;
            }

            CampaignDate withdrawalDate = claim["withdrawalDate"]!
                .Deserialize<CampaignDate>(CanonicalJson.Options);
            long withdrawalTurnIndex = claim["withdrawalTurnIndex"]!.GetValue<long>();
            EntityId withdrawalCommandId = claim["withdrawalCommandId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            EntityId withdrawalEventId = claim["withdrawalEventId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            return withdrawalDate.CompareTo(assertedDate) >= 0
                && withdrawalTurnIndex >= assertedTurnIndex
                && withdrawalEventId == CharacterSuccessionIds.DeriveClaimActionEventId(
                    withdrawalDate,
                    withdrawalCommandId);
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return false;
        }
    }

    private static bool HasExactSuccessionSupportCausality(JsonObject support)
    {
        try
        {
            EntityId supportId = support["supportId"]!.Deserialize<EntityId>(
                CanonicalJson.Options);
            EntityId subjectId = support["subjectId"]!.Deserialize<EntityId>(
                CanonicalJson.Options);
            EntityId supporterId = support["supporterId"]!.Deserialize<EntityId>(
                CanonicalJson.Options);
            EntityId supportedCandidateId = support["supportedCandidateId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            CampaignDate declaredDate = support["declaredDate"]!
                .Deserialize<CampaignDate>(CanonicalJson.Options);
            long declaredTurnIndex = support["declaredTurnIndex"]!.GetValue<long>();
            EntityId sourceCommandId = support["sourceCommandId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            EntityId sourceEventId = support["sourceEventId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            if (declaredTurnIndex < 0
                || subjectId == supporterId
                || subjectId == supportedCandidateId
                || supporterId == supportedCandidateId
                || sourceEventId
                    != CharacterSuccessionIds.DeriveSupportActionEventId(
                        declaredDate,
                        sourceCommandId)
                || supportId != CharacterSuccessionIds.DeriveSupportId(
                    sourceEventId,
                    subjectId,
                    supporterId,
                    supportedCandidateId))
            {
                return false;
            }

            if (HasEnumValue(support, "status", SuccessionSupportStatus.Active))
            {
                return true;
            }

            CampaignDate resolutionDate = support["resolutionDate"]!
                .Deserialize<CampaignDate>(CanonicalJson.Options);
            long resolutionTurnIndex = support["resolutionTurnIndex"]!.GetValue<long>();
            EntityId resolutionCommandId = support["resolutionCommandId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            EntityId resolutionEventId = support["resolutionEventId"]!
                .Deserialize<EntityId>(CanonicalJson.Options);
            return resolutionDate.CompareTo(declaredDate) >= 0
                && resolutionTurnIndex >= declaredTurnIndex
                && resolutionEventId
                    == CharacterSuccessionIds.DeriveSupportActionEventId(
                        resolutionDate,
                        resolutionCommandId);
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return false;
        }
    }

    private static bool HasExactSuccessionSupportReplacementCausality(
        JsonObject previous,
        JsonObject replacement) =>
        JsonNode.DeepEquals(previous["subjectId"], replacement["subjectId"])
        && JsonNode.DeepEquals(previous["supporterId"], replacement["supporterId"])
        && JsonNode.DeepEquals(
            previous["resolutionDate"],
            replacement["declaredDate"])
        && JsonNode.DeepEquals(
            previous["resolutionTurnIndex"],
            replacement["declaredTurnIndex"])
        && JsonNode.DeepEquals(
            previous["resolutionCommandId"],
            replacement["sourceCommandId"])
        && JsonNode.DeepEquals(
            previous["resolutionEventId"],
            replacement["sourceEventId"]);

    private static bool IsTerminalHeirDesignation(
        JsonObject designation,
        HeirDesignationStatus expectedStatus) =>
        IsCurrentHeirDesignation(designation)
        && HasEnumValue(designation, "status", expectedStatus)
        && designation["resolutionDate"] is JsonObject
        && designation["resolutionTurnIndex"] is JsonValue
        && designation["resolutionCommandId"] is JsonObject
        && designation["resolutionEventId"] is JsonObject
        && !JsonNode.DeepEquals(
            designation["sourceCommandId"],
            designation["resolutionCommandId"])
        && !JsonNode.DeepEquals(
            designation["sourceEventId"],
            designation["resolutionEventId"]);

    private static bool HasExactReplacementCausality(
        JsonObject previous,
        JsonObject replacement) =>
        JsonNode.DeepEquals(previous["resolutionDate"], replacement["establishedDate"])
        && JsonNode.DeepEquals(
            previous["resolutionTurnIndex"],
            replacement["establishedTurnIndex"])
        && JsonNode.DeepEquals(
            previous["resolutionCommandId"],
            replacement["sourceCommandId"])
        && JsonNode.DeepEquals(
            previous["resolutionEventId"],
            replacement["sourceEventId"]);

    private static bool HasEnumValue<T>(
        JsonObject value,
        string property,
        T expected)
        where T : struct, Enum
    {
        if (value[property] is not JsonValue enumValue)
        {
            return false;
        }

        try
        {
            return enumValue.TryGetValue(out int actual)
                && actual == Convert.ToInt32(expected);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or OverflowException)
        {
            return false;
        }
    }

    private static void ValidateCharacterSuccessionSnapshotShape(
        JsonObject characterSuccessions,
        string context,
        int expectedSnapshotVersion = CharacterSuccessionContractVersions.Snapshot,
        bool requireClaims = true,
        bool requireSupports = true,
        bool requireResolutions = true)
    {
        if (!HasVersion(
                characterSuccessions,
                expectedSnapshotVersion)
            || characterSuccessions["designations"] is not JsonArray designations
            || characterSuccessions["history"] is not JsonArray history
            || (requireClaims
                && (characterSuccessions["claims"] is not JsonArray
                    || characterSuccessions["claimHistory"] is not JsonArray))
            || (requireSupports
                && (characterSuccessions["supports"] is not JsonArray
                    || characterSuccessions["supportHistory"] is not JsonArray))
            || (requireResolutions
                && (characterSuccessions["resolutions"] is not JsonArray
                    || characterSuccessions["resolutionHistory"] is not JsonObject
                    || !characterSuccessions.ContainsKey("campaignContinuity"))))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character-succession snapshot data.");
        }

        if (designations.Any(node => node is not JsonObject designation
                || !IsCurrentHeirDesignation(designation)))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or malformed heir-designation record data.");
        }

        foreach (JsonNode? node in history)
        {
            if (node is not JsonObject aggregate
                || !HasVersion(aggregate, CharacterSuccessionContractVersions.State)
                || !HasObject(aggregate, "designatorCharacterId")
                || !HasLong(aggregate, "foldedReplacedCount")
                || !HasLong(aggregate, "foldedRevokedCount")
                || !HasObject(aggregate, "earliestDate")
                || !HasObject(aggregate, "latestDate"))
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or malformed heir-designation history data.");
            }
        }

        if (!requireClaims)
        {
            return;
        }

        JsonArray claims = characterSuccessions["claims"]!.AsArray();
        foreach (JsonNode? node in claims)
        {
            if (node is not JsonObject claim
                || !HasVersion(claim, 1)
                || !HasObject(claim, "claimId")
                || !HasObject(claim, "subjectCharacterId")
                || !HasObject(claim, "claimantCharacterId")
                || JsonNode.DeepEquals(
                    claim["subjectCharacterId"],
                    claim["claimantCharacterId"])
                || !HasDefinedEnum<SuccessionClaimOrigin>(claim, "origin")
                || !HasObject(claim, "assertedDate")
                || !HasLong(claim, "assertedTurnIndex")
                || !HasObject(claim, "sourceCommandId")
                || !HasObject(claim, "sourceEventId")
                || !HasDefinedEnum<SuccessionClaimStatus>(claim, "status")
                || !HasNullableObject(claim, "withdrawalDate")
                || !HasNullableLong(claim, "withdrawalTurnIndex")
                || !HasNullableObject(claim, "withdrawalCommandId")
                || !HasNullableObject(claim, "withdrawalEventId")
                || !HasExactSuccessionClaimTerminalShape(claim))
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or malformed succession-claim record data.");
            }
        }

        JsonArray claimHistory = characterSuccessions["claimHistory"]!.AsArray();
        foreach (JsonNode? node in claimHistory)
        {
            if (node is not JsonObject aggregate
                || !HasVersion(aggregate, 1)
                || !HasObject(aggregate, "subjectCharacterId")
                || !HasLong(aggregate, "foldedWithdrawnCount")
                || !HasObject(aggregate, "earliestDate")
                || !HasObject(aggregate, "latestDate"))
            {
                throw new SaveCompatibilityException(
                $"{context} contains missing, null, or malformed succession-claim history data.");
            }
        }

        if (!requireSupports)
        {
            return;
        }

        JsonArray supports = characterSuccessions["supports"]!.AsArray();
        foreach (JsonNode? node in supports)
        {
            if (node is not JsonObject support
                || !IsCurrentSuccessionSupport(support)
                || !HasExactSuccessionSupportTerminalShape(support))
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or malformed succession-support record data.");
            }
        }

        JsonArray supportHistory = characterSuccessions["supportHistory"]!.AsArray();
        foreach (JsonNode? node in supportHistory)
        {
            if (node is not JsonObject aggregate
                || !HasVersion(
                    aggregate,
                    CharacterSuccessionContractVersions.SupportHistory)
                || !HasObject(aggregate, "subjectId")
                || !HasLong(aggregate, "foldedReplacedCount")
                || !HasLong(aggregate, "foldedWithdrawnCount")
                || !HasObject(aggregate, "earliestDate")
                || !HasObject(aggregate, "latestDate"))
            {
                throw new SaveCompatibilityException(
                $"{context} contains missing, null, or malformed succession-support history data.");
            }
        }

        if (!requireResolutions)
        {
            return;
        }

        JsonObject resolutionHistory = characterSuccessions["resolutionHistory"]!.AsObject();
        if (!HasVersion(
                resolutionHistory,
                CharacterSuccessionContractVersions.ResolutionHistory)
            || !HasLong(resolutionHistory, "foldedSelectedCount")
            || !HasLong(resolutionHistory, "foldedDisputedCount")
            || !HasLong(resolutionHistory, "foldedNoSuccessorCount")
            || !HasNullableObject(resolutionHistory, "earliestDate")
            || !HasNullableObject(resolutionHistory, "latestDate"))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or malformed succession-resolution history data.");
        }

        if (resolutionHistory["foldedSelectedCount"]!.GetValue<long>() < 0
            || resolutionHistory["foldedDisputedCount"]!.GetValue<long>() < 0
            || resolutionHistory["foldedNoSuccessorCount"]!.GetValue<long>() < 0
            || (resolutionHistory["earliestDate"] is null)
                != (resolutionHistory["latestDate"] is null))
        {
            throw new SaveCompatibilityException(
                $"{context} contains impossible succession-resolution history data.");
        }

        JsonArray resolutions = characterSuccessions["resolutions"]!.AsArray();
        if (resolutions.Any(item => item is not JsonObject resolution
                || !IsCurrentSuccessionResolution(resolution)))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or malformed succession-resolution record data.");
        }

        if (characterSuccessions["campaignContinuity"] is JsonNode continuity
            && (continuity is not JsonObject continuityObject
                || !IsCurrentCampaignContinuity(continuityObject)))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or malformed player campaign-continuity data.");
        }
    }

    private static bool IsCurrentSuccessionResolution(JsonObject resolution) =>
        HasVersion(resolution, CharacterSuccessionContractVersions.Resolution)
        && HasObject(resolution, "resolutionId")
        && HasObject(resolution, "subjectCharacterId")
        && HasObject(resolution, "deathId")
        && HasDefinedEnum<SuccessionResolutionStatus>(resolution, "status")
        && HasNullableSuccessionCandidate(resolution, "selectedCandidate")
        && resolution["disputedCandidates"] is JsonArray disputedCandidates
        && disputedCandidates.All(item => item is JsonObject candidate
            && IsCurrentSuccessionCandidate(candidate))
        && HasInt(resolution, "eligibleCandidateCount")
        && resolution["rule"] is JsonObject rule
        && IsCurrentSuccessionResolutionRule(rule)
        && resolution["inheritance"] is JsonObject inheritance
        && IsCurrentSuccessionInheritance(inheritance)
        && HasNullableSuccessionRegency(resolution, "regency")
        && HasNullableCampaignContinuity(
            resolution,
            "previousCampaignContinuity")
        && HasNullableCampaignContinuity(
            resolution,
            "currentCampaignContinuity")
        && HasObject(resolution, "resolutionDate")
        && HasLong(resolution, "resolutionTurnIndex")
        && HasObject(resolution, "sourceCommandId")
        && HasObject(resolution, "sourceEventId");

    private static bool IsCurrentSuccessionResolutionRule(JsonObject rule) =>
        HasVersion(rule, CharacterSuccessionContractVersions.ResolutionRule)
        && rule["candidateEligibility"] is JsonObject eligibility
        && HasVersion(
            eligibility,
            CharacterSuccessionContractVersions.CandidateEligibilityRule)
        && eligibility["allowedBases"] is JsonArray
        && HasInt(eligibility, "maximumDescendantGeneration")
        && HasInt(eligibility, "minimumCandidateAge")
        && HasBoolean(eligibility, "allowsIncapacitatedCandidates")
        && eligibility["allowedCustodyStatuses"] is JsonArray
        && rule["legalBasisPrecedence"] is JsonArray
        && HasBoolean(rule, "includesPrincipalSpouse")
        && rule["allowedCollateralKinds"] is JsonArray
        && HasInt(rule, "maximumCollateralDistance")
        && HasDefinedEnum<SuccessionContestResolutionMode>(
            rule,
            "contestResolutionMode")
        && HasInt(rule, "maximumCandidates")
        && HasInt(rule, "maximumDisputedCandidates")
        && HasBoolean(rule, "createsRegencyForIncapacitatedSuccessor")
        && HasDefinedEnum<SuccessionNoAcceptedSuccessorBehavior>(
            rule,
            "noAcceptedSuccessorBehavior");

    private static bool HasNullableSuccessionCandidate(
        JsonObject value,
        string property) =>
        value.ContainsKey(property)
        && (value[property] is null
            || value[property] is JsonObject candidate
                && IsCurrentSuccessionCandidate(candidate));

    private static bool IsCurrentSuccessionCandidate(JsonObject candidate) =>
        HasVersion(
            candidate,
            CharacterSuccessionContractVersions.ResolutionCandidate)
        && HasObject(candidate, "candidateCharacterId")
        && HasInt(candidate, "candidateAge")
        && IsCurrentCharacterCondition(candidate["candidateCondition"])
        && candidate["legalBases"] is JsonArray legalBases
        && legalBases.All(item => item is JsonObject basis
            && HasVersion(
                basis,
                CharacterSuccessionContractVersions.ResolutionCandidate)
            && HasDefinedEnum<SuccessionLegalBasis>(basis, "basis")
            && HasNullableInt(basis, "descendantGeneration")
            && HasNullableInt(basis, "collateralDistance")
            && HasNullableObject(basis, "sourceDesignationId")
            && HasNullableObject(basis, "sourceMarriageUnionId")
            && HasNullableObject(basis, "sharedAncestorCharacterId"))
        && HasNullableObject(candidate, "activeClaimId")
        && candidate["activeSupportIds"] is JsonArray
        && HasInt(candidate, "legalBasisPrecedenceIndex")
        && HasInt(candidate, "kinshipDistance");

    private static bool IsCurrentSuccessionInheritance(JsonObject inheritance) =>
        HasVersion(
            inheritance,
            CharacterSuccessionContractVersions.Inheritance)
        && inheritance.ContainsKey("wealthTransfer")
        && (inheritance["wealthTransfer"] is null
            || inheritance["wealthTransfer"] is JsonObject wealthTransfer
                && IsCurrentSuccessionWealthTransfer(wealthTransfer))
        && inheritance["estateTransfers"] is JsonArray estateTransfers
        && estateTransfers.All(item => item is JsonObject transfer
            && HasVersion(
                transfer,
                CharacterSuccessionContractVersions.Inheritance)
            && HasObject(transfer, "estateId")
            && HasObject(transfer, "previousOwnerCharacterId")
            && HasObject(transfer, "currentOwnerCharacterId"));

    private static bool IsCurrentSuccessionWealthTransfer(JsonObject value)
    {
        if (!HasVersion(value, CharacterResourceContractVersions.Outcome)
            || value["transfer"] is not JsonObject transfer
            || !HasLong(value, "sourceWealthAfter")
            || !HasLong(value, "recipientWealthAfter")
            || value["outgoingEntry"] is not JsonObject outgoing
            || value["incomingEntry"] is not JsonObject incoming
            || !HasVersion(transfer, CharacterResourceContractVersions.State)
            || !HasObject(transfer, "transferId")
            || !HasObject(transfer, "sourceCharacterId")
            || !HasObject(transfer, "recipientCharacterId")
            || !HasLong(transfer, "amount")
            || !HasObject(transfer, "resolutionDate")
            || !HasLong(transfer, "resolutionTurnIndex")
            || !HasObject(transfer, "sourceCommandId")
            || !HasObject(transfer, "sourceEventId")
            || !IsCurrentSuccessionWealthLedgerEntry(outgoing)
            || !IsCurrentSuccessionWealthLedgerEntry(incoming))
        {
            return false;
        }

        try
        {
            WealthTransferredOutcome outcome =
                value.Deserialize<WealthTransferredOutcome>(
                    CanonicalJson.Options)
                ?? throw new JsonException(
                    "Succession wealth-transfer outcome is empty.");
            WealthTransferRecord record = outcome.Transfer;
            WealthLedgerEntry outgoingEntry = outcome.OutgoingEntry;
            WealthLedgerEntry incomingEntry = outcome.IncomingEntry;
            return record.SourceCharacterId != record.RecipientCharacterId
                && record.Amount > 0
                && outcome.SourceWealthAfter == 0
                && outcome.RecipientWealthAfter >= record.Amount
                && record.SourceEventId
                    == CharacterResourceIds.DeriveActionEventId(
                        record.ResolutionDate,
                        record.SourceCommandId)
                && record.TransferId
                    == CharacterResourceIds.DeriveWealthTransferId(
                        record.SourceEventId)
                && IsExactSuccessionWealthLedgerEntry(
                    outgoingEntry,
                    record,
                    WealthLedgerDirection.Outgoing)
                && IsExactSuccessionWealthLedgerEntry(
                    incomingEntry,
                    record,
                    WealthLedgerDirection.Incoming);
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return false;
        }
    }

    private static bool IsCurrentSuccessionWealthLedgerEntry(JsonObject entry) =>
        HasVersion(entry, CharacterResourceContractVersions.State)
        && HasObject(entry, "entryId")
        && HasObject(entry, "transferId")
        && HasObject(entry, "characterId")
        && HasObject(entry, "counterpartyCharacterId")
        && HasDefinedEnum<WealthLedgerDirection>(entry, "direction")
        && HasLong(entry, "amount")
        && HasObject(entry, "resolutionDate")
        && HasLong(entry, "resolutionTurnIndex")
        && HasObject(entry, "sourceCommandId")
        && HasObject(entry, "sourceEventId");

    private static bool IsExactSuccessionWealthLedgerEntry(
        WealthLedgerEntry entry,
        WealthTransferRecord transfer,
        WealthLedgerDirection direction)
    {
        EntityId expectedCharacterId = direction == WealthLedgerDirection.Outgoing
            ? transfer.SourceCharacterId
            : transfer.RecipientCharacterId;
        EntityId expectedCounterpartyId =
            direction == WealthLedgerDirection.Outgoing
                ? transfer.RecipientCharacterId
                : transfer.SourceCharacterId;
        return entry.Direction == direction
            && entry.TransferId == transfer.TransferId
            && entry.CharacterId == expectedCharacterId
            && entry.CounterpartyCharacterId == expectedCounterpartyId
            && entry.Amount == transfer.Amount
            && entry.ResolutionDate == transfer.ResolutionDate
            && entry.ResolutionTurnIndex == transfer.ResolutionTurnIndex
            && entry.SourceCommandId == transfer.SourceCommandId
            && entry.SourceEventId == transfer.SourceEventId
            && entry.EntryId
                == CharacterResourceIds.DeriveWealthLedgerEntryId(
                    transfer.TransferId,
                    entry.CharacterId,
                    direction);
    }

    private static bool HasNullableSuccessionRegency(
        JsonObject value,
        string property) =>
        value.ContainsKey(property)
        && (value[property] is null
            || value[property] is JsonObject regency
                && HasVersion(
                    regency,
                    CharacterSuccessionContractVersions.Regency)
                && HasObject(regency, "successorCharacterId")
                && HasInt(regency, "reasons")
                && HasNullableObject(regency, "regentCharacterId")
                && HasNullableObject(regency, "sourceGuardianshipId")
                && HasNullableObject(
                    regency,
                    "sourceGuardianCharacterId")
                && HasNullableObject(
                    regency,
                    "sourceCustodianCharacterId"));

    private static bool HasNullableCampaignContinuity(
        JsonObject value,
        string property) =>
        value.ContainsKey(property)
        && (value[property] is null
            || value[property] is JsonObject continuity
                && IsCurrentCampaignContinuity(continuity));

    private static bool IsCurrentCampaignContinuity(JsonObject continuity) =>
        HasVersion(
            continuity,
            CharacterSuccessionContractVersions.CampaignContinuity)
        && HasDefinedEnum<PlayerCampaignContinuityStatus>(
            continuity,
            "status")
        && HasNullableObject(continuity, "controlledCharacterId")
        && HasObject(continuity, "resolutionDate")
        && HasLong(continuity, "resolutionTurnIndex")
        && HasObject(continuity, "sourceCommandId")
        && HasObject(continuity, "sourceEventId");

    private static bool HasExactSuccessionClaimTerminalShape(JsonObject claim)
    {
        bool isActive = HasEnumValue(claim, "status", SuccessionClaimStatus.Active);
        bool isWithdrawn = HasEnumValue(
            claim,
            "status",
            SuccessionClaimStatus.Withdrawn);
        bool hasWithdrawalEvidence = claim["withdrawalDate"] is JsonObject
            && claim["withdrawalTurnIndex"] is JsonValue
            && claim["withdrawalCommandId"] is JsonObject
            && claim["withdrawalEventId"] is JsonObject;
        bool hasNoWithdrawalEvidence = claim["withdrawalDate"] is null
            && claim["withdrawalTurnIndex"] is null
            && claim["withdrawalCommandId"] is null
            && claim["withdrawalEventId"] is null;
        return isActive ? hasNoWithdrawalEvidence : isWithdrawn && hasWithdrawalEvidence;
    }

    private static bool HasExactSuccessionSupportTerminalShape(JsonObject support)
    {
        bool isActive = HasEnumValue(
            support,
            "status",
            SuccessionSupportStatus.Active);
        bool isTerminal = HasEnumValue(
                support,
                "status",
                SuccessionSupportStatus.Replaced)
            || HasEnumValue(
                support,
                "status",
                SuccessionSupportStatus.Withdrawn);
        bool hasResolutionEvidence = support["resolutionDate"] is JsonObject
            && support["resolutionTurnIndex"] is JsonValue
            && support["resolutionCommandId"] is JsonObject
            && support["resolutionEventId"] is JsonObject;
        bool hasNoResolutionEvidence = support["resolutionDate"] is null
            && support["resolutionTurnIndex"] is null
            && support["resolutionCommandId"] is null
            && support["resolutionEventId"] is null;
        return isActive ? hasNoResolutionEvidence : isTerminal && hasResolutionEvidence;
    }

    private static bool IsCurrentSuccessionSupport(JsonObject support) =>
        HasVersion(support, CharacterSuccessionContractVersions.SupportState)
        && HasObject(support, "supportId")
        && HasObject(support, "subjectId")
        && HasObject(support, "supporterId")
        && HasObject(support, "supportedCandidateId")
        && !JsonNode.DeepEquals(support["subjectId"], support["supporterId"])
        && !JsonNode.DeepEquals(support["subjectId"], support["supportedCandidateId"])
        && !JsonNode.DeepEquals(
            support["supporterId"],
            support["supportedCandidateId"])
        && HasObject(support, "declaredDate")
        && HasLong(support, "declaredTurnIndex")
        && HasObject(support, "sourceCommandId")
        && HasObject(support, "sourceEventId")
        && HasDefinedEnum<SuccessionSupportStatus>(support, "status")
        && HasNullableObject(support, "resolutionDate")
        && HasNullableLong(support, "resolutionTurnIndex")
        && HasNullableObject(support, "resolutionCommandId")
        && HasNullableObject(support, "resolutionEventId");

    private static bool IsCurrentHeirDesignation(JsonObject designation) =>
        HasVersion(designation, CharacterSuccessionContractVersions.State)
        && HasObject(designation, "designationId")
        && HasObject(designation, "designatorCharacterId")
        && HasObject(designation, "heirCharacterId")
        && !JsonNode.DeepEquals(
            designation["designatorCharacterId"],
            designation["heirCharacterId"])
        && HasObject(designation, "establishedDate")
        && HasLong(designation, "establishedTurnIndex")
        && HasObject(designation, "sourceCommandId")
        && HasObject(designation, "sourceEventId")
        && HasDefinedEnum<HeirDesignationStatus>(designation, "status")
        && HasNullableObject(designation, "resolutionDate")
        && HasNullableLong(designation, "resolutionTurnIndex")
        && HasNullableObject(designation, "resolutionCommandId")
        && HasNullableObject(designation, "resolutionEventId");

    private static void ValidateCharacterPregnancySnapshotShape(
        JsonObject characterPregnancies,
        string context,
        int expectedSnapshotVersion = CharacterPregnancyContractVersions.Snapshot,
        int expectedStateVersion = CharacterPregnancyContractVersions.State)
    {
        if (!HasVersion(
                characterPregnancies,
                expectedSnapshotVersion)
            || characterPregnancies["activePregnancies"] is not JsonArray activePregnancies)
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character-pregnancy snapshot data.");
        }

        foreach (JsonNode? node in activePregnancies)
        {
            if (node is not JsonObject pregnancy
                || !HasVersion(
                    pregnancy,
                    expectedStateVersion)
                || !HasObject(pregnancy, "pregnancyId")
                || !HasObject(pregnancy, "gestationalParentCharacterId")
                || !HasObject(pregnancy, "otherBiologicalParentCharacterId")
                || !HasObject(pregnancy, "sourceUnionId")
                || !HasObject(pregnancy, "startDate")
                || !HasObject(pregnancy, "expectedBirthDate")
                || !HasLong(pregnancy, "startTurnIndex")
                || !HasObject(pregnancy, "sourceCommandId")
                || !HasObject(pregnancy, "sourceEventId"))
            {
                throw new SaveCompatibilityException(
                    $"{context} contains missing, null, or malformed character-pregnancy record data.");
            }
        }
    }

    private static void ValidateCharacterGuardianshipSnapshotShape(
        JsonObject characterGuardianships,
        string context,
        int expectedSnapshotVersion = CharacterGuardianshipContractVersions.Snapshot,
        int expectedStateVersion = CharacterGuardianshipContractVersions.State)
    {
        if (!HasVersion(
                characterGuardianships,
                expectedSnapshotVersion)
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
                    expectedStateVersion)
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
        int expectedSnapshotVersion,
        int expectedDefinitionVersion,
        int expectedStateVersion,
        bool requireVersionTwoFields,
        bool requireEducationAttainments)
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
            || version != expectedSnapshotVersion
            || requiredArrays.Any(property => characters[property] is not JsonArray))
        {
            throw new SaveCompatibilityException(
                $"{context} contains missing, null, or unsupported character snapshot fields.");
        }

        ValidateVersionedCharacterEntries(
            characters,
            context,
            "identityDefinitions",
            expectedDefinitionVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "characterDefinitions",
            expectedDefinitionVersion,
            requireVersionTwoFields
                ? ["structuredName", "contentOrigin", "flawIds"]
                : []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "familyDefinitions",
            expectedDefinitionVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "householdDefinitions",
            expectedDefinitionVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "characterStates",
            expectedStateVersion,
            requireEducationAttainments
                ? ["parentLinks", "condition", "educationAttainments"]
                : requireVersionTwoFields ? ["parentLinks", "condition"] : []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "familyStates",
            expectedStateVersion,
            []);
        ValidateVersionedCharacterEntries(
            characters,
            context,
            "householdStates",
            expectedStateVersion,
            []);

        ValidateCommonCharacterEntryShape(characters, context);

        if (requireVersionTwoFields)
        {
            ValidateCharacterV2EntryShape(characters, context);
            if (requireEducationAttainments)
            {
                ValidateCharacterEducationEntryShape(characters, context);
            }
        }
        else
        {
            RejectLegacyCharacterV2Fields(characters, context);
        }
    }

    private static void ValidateCharacterEducationEntryShape(
        JsonObject characters,
        string context)
    {
        foreach (JsonObject state in characters["characterStates"]!.AsArray().OfType<JsonObject>())
        {
            JsonArray attainments = RequireCharacterArray(
                state,
                "educationAttainments",
                context,
                "character education-attainment collection");
            foreach (JsonNode? node in attainments)
            {
                if (node is not JsonObject attainment)
                {
                    throw MalformedCharacterData(context, "character education attainment");
                }

                RequireCharacterInteger(
                    attainment,
                    "contractVersion",
                    context,
                    "character education attainment");
                if (attainment["contractVersion"]!.GetValue<int>()
                    != CharacterEducationContractVersions.Attainment)
                {
                    throw new SaveCompatibilityException(
                        $"{context} contains an unsupported character education-attainment version.");
                }

                RequireCharacterObject(attainment, "attainmentId", context, "character education attainment");
                RequireCharacterObject(attainment, "wardCharacterId", context, "character education attainment");
                RequireCharacterObject(attainment, "teacherCharacterId", context, "character education attainment");
                RequireCharacterObject(attainment, "primaryGuardianshipId", context, "character education attainment");
                RequireCharacterObject(attainment, "abilityId", context, "character education attainment");
                RequireCharacterObject(attainment, "resolutionDate", context, "character education attainment");
                RequireCharacterInteger(attainment, "resolutionTurnIndex", context, "character education attainment");
                RequireCharacterObject(attainment, "sourceCommandId", context, "character education attainment");
                RequireCharacterObject(attainment, "sourceEventId", context, "character education attainment");
            }
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
        bool allowVersionTwoRoutes,
        int expectedInvitationStateVersion = CharacterMarriageContractVersions.RomanceInvitationState,
        int expectedPracticeVersion = CharacterMarriageContractVersions.Practice,
        int expectedStateVersion = CharacterMarriageContractVersions.State,
        int expectedRomanceRouteStateVersion = CharacterMarriageContractVersions.RomanceRouteState)
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
                        expectedInvitationStateVersion)
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
                || !HasVersion(practice, expectedPracticeVersion)
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
                || !HasVersion(proposal, expectedStateVersion)
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
                || !HasVersion(betrothal, expectedStateVersion)
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
                || !HasVersion(union, expectedStateVersion)
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
                || routeVersion != expectedStateVersion
                    && (!allowVersionTwoRoutes
                        || routeVersion
                            != expectedRomanceRouteStateVersion)
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
            if (routeVersion == expectedStateVersion)
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
                || !HasVersion(history, expectedStateVersion)
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

    private static bool HasDefinedEnum<TEnum>(JsonObject value, string property)
        where TEnum : struct, Enum =>
        value[property] is JsonValue enumValue
        && enumValue.TryGetValue(out int actual)
        && Enum.IsDefined(typeof(TEnum), actual);

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

    private static bool IsCurrentCharacterPregnancySystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            CharacterPregnancySystem.SystemId,
            CharacterPregnancySystem.Version);

    private static bool IsCurrentCharacterSuccessionSystemVersion(JsonNode? node) =>
        node is JsonObject systemVersion
        && IsSystemVersion(
            systemVersion,
            CharacterSuccessionSystem.SystemId,
            CharacterSuccessionSystem.Version);

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

    private static void RejectE4Discriminators(JsonNode node, string description)
    {
        if (ContainsE4Discriminator(node) || ContainsE4Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 17 unexpectedly contains schema 18 character-pregnancy data in {description}.");
        }
    }

    private static void RejectE5Discriminators(JsonNode node, string description)
    {
        if (ContainsE5Discriminator(node) || ContainsE5Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 18 unexpectedly contains schema 19 pregnancy-birth data in {description}.");
        }
    }

    private static void RejectE6Discriminators(JsonNode node, string description)
    {
        if (ContainsE6Discriminator(node) || ContainsE6Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 19 unexpectedly contains schema 20 character-education data in {description}.");
        }
    }

    private static void RejectF0Discriminators(JsonNode node, string description)
    {
        if (ContainsF0Discriminator(node) || ContainsF0Property(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 20 unexpectedly contains schema 21 character-death data in {description}.");
        }
    }

    private static void RejectF1Discriminators(JsonNode node, string description)
    {
        if (ContainsF1PropertyOrVersion(node) || ContainsF1CareerEndReason(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 21 unexpectedly contains schema 22 career-death data in {description}.");
        }
    }

    private static void RejectF2Discriminators(JsonNode node, string description)
    {
        if (ContainsF2PropertyOrVersion(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 22 unexpectedly contains schema 23 custodian-death release data in {description}.");
        }
    }

    private static void RejectF3Discriminators(JsonNode node, string description)
    {
        if (ContainsF3PropertyOrDiscriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 23 unexpectedly contains schema 24 household-head death data in {description}.");
        }
    }

    private static void RejectF4Discriminators(JsonNode node, string description)
    {
        if (ContainsF4PropertyOrDiscriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 24 unexpectedly contains schema 25 character-succession data in {description}.");
        }
    }

    private static void RejectF7Discriminators(JsonNode node, string description)
    {
        if (ContainsF7PropertyOrDiscriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 25 unexpectedly contains schema 26 succession-claim data in {description}.");
        }
    }

    private static void RejectF8Discriminators(JsonNode node, string description)
    {
        if (ContainsF8PropertyOrDiscriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 26 unexpectedly contains schema 27 succession-support data in {description}.");
        }
    }

    private static void RejectF9Discriminators(JsonNode node, string description)
    {
        if (ContainsF9PropertyOrDiscriminator(node))
        {
            throw new SaveCompatibilityException(
                $"Save schema 27 unexpectedly contains schema 28 succession-resolution data in {description}.");
        }
    }

    private static bool ContainsF9PropertyOrDiscriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            string? discriminator = value["$type"]?.GetValue<string>();
            if (discriminator is "resolve_character_succession_death.v1"
                    or "character_succession_death_resolved.v1"
                || value.ContainsKey("expectedResolutionStateId")
                || value.ContainsKey("resolutions")
                || value.ContainsKey("resolutionHistory")
                || value.ContainsKey("campaignContinuity")
                || value.ContainsKey("succession")
                || value.ContainsKey("selectedCandidate")
                || value.ContainsKey("disputedCandidates")
                || value.ContainsKey("candidateEligibility")
                || value.ContainsKey("legalBasisPrecedence")
                || value.ContainsKey("includesPrincipalSpouse")
                || value.ContainsKey("allowedCollateralKinds")
                || value.ContainsKey("maximumCollateralDistance")
                || value.ContainsKey("candidateAge")
                || value.ContainsKey("candidateCondition")
                || value.ContainsKey("legalBases")
                || value.ContainsKey("activeClaimId")
                || value.ContainsKey("activeSupportIds")
                || value.ContainsKey("legalBasisPrecedenceIndex")
                || value.ContainsKey("kinshipDistance")
                || value.ContainsKey("inheritance")
                || value.ContainsKey("wealthTransfer")
                || value.ContainsKey("estateTransfers")
                || value.ContainsKey("previousOwnerCharacterId")
                || value.ContainsKey("currentOwnerCharacterId")
                || value.ContainsKey("regency")
                || value.ContainsKey("regentCharacterId")
                || value.ContainsKey("successorCharacterId")
                || value.ContainsKey("sourceGuardianshipId")
                || value.ContainsKey("sourceGuardianCharacterId")
                || value.ContainsKey("sourceCustodianCharacterId")
                || value.ContainsKey("controlledCharacterId")
                || value.ContainsKey("foldedSelectedCount")
                || value.ContainsKey("foldedDisputedCount")
                || value.ContainsKey("foldedNoSuccessorCount")
                || value.ContainsKey("contestResolutionMode")
                || value.ContainsKey("maximumDisputedCandidates")
                || value.ContainsKey(
                    "createsRegencyForIncapacitatedSuccessor")
                || value.ContainsKey("noAcceptedSuccessorBehavior")
                || value.ContainsKey("previousCampaignContinuity")
                || value.ContainsKey("currentCampaignContinuity"))
            {
                return true;
            }

            return value.Any(property => ContainsF9PropertyOrDiscriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF9PropertyOrDiscriminator);
    }

    private static bool ContainsF8PropertyOrDiscriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            string? discriminator = value["$type"]?.GetValue<string>();
            if (discriminator is "character_succession_support_action.v1"
                    or "character_succession_support_action_resolved.v1"
                    or "declare_succession_support.v1"
                    or "withdraw_succession_support.v1"
                    or "succession_support_declared.v1"
                    or "succession_support_replaced.v1"
                    or "succession_support_withdrawn.v1"
                || value.ContainsKey("supports")
                || value.ContainsKey("supportHistory")
                || value.ContainsKey("supportId")
                || value.ContainsKey("supporterId")
                || value.ContainsKey("supportedCandidateId")
                || value.ContainsKey("expectedCurrentSupportId")
                || value.ContainsKey("currentSupport")
                || value.ContainsKey("previousSupport")
                || value.ContainsKey("declaredDate")
                || value.ContainsKey("declaredTurnIndex"))
            {
                return true;
            }

            return value.Any(property => ContainsF8PropertyOrDiscriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF8PropertyOrDiscriminator);
    }

    private static bool ContainsF7PropertyOrDiscriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            string? discriminator = value["$type"]?.GetValue<string>();
            if (discriminator is "character_succession_claim_action.v1"
                    or "character_succession_claim_action_resolved.v1"
                    or "assert_succession_claim.v1"
                    or "withdraw_succession_claim.v1"
                    or "succession_claim_asserted.v1"
                    or "succession_claim_withdrawn.v1"
                || value.ContainsKey("claimHistory")
                || value.ContainsKey("claimId")
                || value.ContainsKey("claimantCharacterId")
                || value.ContainsKey("expectedCurrentClaimId")
                || value.ContainsKey("currentClaim")
                || value.ContainsKey("previousClaim")
                || value.ContainsKey("assertedDate")
                || value.ContainsKey("assertedTurnIndex")
                || value.ContainsKey("withdrawalDate")
                || value.ContainsKey("withdrawalTurnIndex")
                || value.ContainsKey("withdrawalCommandId")
                || value.ContainsKey("withdrawalEventId")
                || value.ContainsKey("foldedWithdrawnCount"))
            {
                return true;
            }

            return value.Any(property => ContainsF7PropertyOrDiscriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF7PropertyOrDiscriminator);
    }

    private static bool ContainsF4PropertyOrDiscriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            string? discriminator = value["$type"]?.GetValue<string>();
            if (discriminator is "character_succession_action.v1"
                    or "character_succession_action_resolved.v1"
                    or "designate_heir.v1"
                    or "revoke_heir_designation.v1"
                    or "heir_designated.v1"
                    or "heir_designation_replaced.v1"
                    or "heir_designation_revoked.v1"
                || value.ContainsKey("characterSuccessions")
                || value.ContainsKey("designations")
                || value.ContainsKey("expectedCurrentDesignationId")
                || value.ContainsKey("currentDesignation")
                || value.ContainsKey("previousDesignation")
                || value.ContainsKey("designationId")
                || value.ContainsKey("designatorCharacterId")
                || value.ContainsKey("heirCharacterId")
                || value.ContainsKey("resolutionEventId")
                || value.ContainsKey("foldedReplacedCount")
                || value.ContainsKey("foldedRevokedCount"))
            {
                return true;
            }

            return value.Any(property => ContainsF4PropertyOrDiscriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF4PropertyOrDiscriminator);
    }

    private static bool ContainsF3PropertyOrDiscriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            string? discriminator = value["$type"]?.GetValue<string>();
            if (discriminator is "resolve_household_head_death.v1"
                    or "household_head_death_resolved.v1"
                || value.ContainsKey("replacementHeadCharacterId")
                || value.ContainsKey("householdHeadChange")
                || value.ContainsKey("previousHeadCharacterId")
                || value.ContainsKey("currentHeadCharacterId"))
            {
                return true;
            }

            return value.Any(property => ContainsF3PropertyOrDiscriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF3PropertyOrDiscriminator);
    }

    private static bool ContainsF2PropertyOrVersion(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("releasedCustodyChanges")
                || value["$type"]?.GetValue<string>() == "character_death_resolved.v1"
                    && value["death"] is JsonObject death
                    && death["contractVersion"]?.GetValue<int>() != 2)
            {
                return true;
            }

            return value.Any(property => ContainsF2PropertyOrVersion(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF2PropertyOrVersion);
    }

    private static bool ContainsF1PropertyOrVersion(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("careerChanges")
                || value.ContainsKey("endedRetinueMemberships")
                || value.ContainsKey("endedPatronageBonds")
                || value.ContainsKey("endedEmploymentTenures")
                || value["$type"]?.GetValue<string>() == "character_death_resolved.v1"
                    && value["death"] is JsonObject death
                    && (death["contractVersion"]?.GetValue<int>() != 1
                        || death.ContainsKey("invalidatedProposals")))
            {
                return true;
            }

            return value.Any(property => ContainsF1PropertyOrVersion(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF1PropertyOrVersion);
    }

    private static bool ContainsF1CareerEndReason(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if ((value.ContainsKey("membershipId")
                    || value.ContainsKey("bondId")
                    || value.ContainsKey("tenureId"))
                && value["endReason"] is JsonValue reason
                && (reason.TryGetValue(out int numeric)
                        && numeric is >= (int)CareerServiceEndReason.LeaderDied
                            and <= (int)CareerServiceEndReason.EmployerDied
                    || reason.TryGetValue(out string? text)
                        && text is "LeaderDied"
                            or "MemberDied"
                            or "PatronDied"
                            or "BeneficiaryDied"
                            or "EmployeeDied"
                            or "EmployerDied"))
            {
                return true;
            }

            return value.Any(property => ContainsF1CareerEndReason(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF1CareerEndReason);
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

    private static bool ContainsE4Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "register_active_pregnancy.v1"
                    or "active_pregnancy_registered.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE4Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE4Discriminator);
    }

    private static bool ContainsE4Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("pregnancy")
                || value.ContainsKey("pregnancyId")
                || value.ContainsKey("gestationalParentCharacterId")
                || value.ContainsKey("otherBiologicalParentCharacterId")
                || value.ContainsKey("sourceUnionId")
                || value.ContainsKey("expectedCurrentPregnancyId")
                || value.ContainsKey("expectedBirthDate"))
            {
                return true;
            }

            return value.Any(property => ContainsE4Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE4Property);
    }

    private static bool ContainsE5Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "resolve_pregnancy_birth.v1"
                    or "pregnancy_birth_resolved.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE5Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE5Discriminator);
    }

    private static bool ContainsE5Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("expectedPregnancyId")
                || value.ContainsKey("newborn")
                || value.ContainsKey("birth")
                || value.ContainsKey("birthId")
                || value.ContainsKey("resolvedPregnancy")
                || value.ContainsKey("childDefinition")
                || value.ContainsKey("childState")
                || value.ContainsKey("inheritedTraitIds"))
            {
                return true;
            }

            return value.Any(property => ContainsE5Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE5Property);
    }

    private static bool ContainsE6Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "complete_primary_guardian_education.v1"
                    or "primary_guardian_education_completed.v1")
            {
                return true;
            }

            return value.Any(property => ContainsE6Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE6Discriminator);
    }

    private static bool ContainsE6Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("educationAttainments")
                || value.ContainsKey("attainment")
                || value.ContainsKey("attainmentId")
                || value.ContainsKey("teacherCharacterId")
                || value.ContainsKey("primaryGuardianshipId")
                || value.ContainsKey("expectedPrimaryGuardianshipId"))
            {
                return true;
            }

            return value.Any(property => ContainsE6Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsE6Property);
    }

    private static bool ContainsF0Discriminator(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value["$type"] is JsonValue discriminator
                && discriminator.TryGetValue(out string? type)
                && type is "resolve_character_death.v1"
                    or "character_death_resolved.v1")
            {
                return true;
            }

            return value.Any(property => ContainsF0Discriminator(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF0Discriminator);
    }

    private static bool ContainsF0Property(JsonNode? node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("death")
                || value.ContainsKey("deathId")
                || value.ContainsKey("conditionChange")
                || value.ContainsKey("endedGuardianships")
                || value.ContainsKey("removedPregnancies"))
            {
                return true;
            }

            return value.Any(property => ContainsF0Property(property.Value));
        }

        return node is JsonArray array && array.Any(ContainsF0Property);
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
            CharacterContractVersions.PreviousSnapshot,
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
                ContractVersion = CharacterContractVersions.PreviousState,
                ParentLinks = state.ParentIds
                    .Select(parentId => new CharacterParentLink(parentId, ParentChildLinkKind.UnspecifiedLegacy))
                    .ToArray(),
                Condition = CharacterConditionState.Default,
            }).ToArray(),
            legacy.FamilyStates.Select(state => state with
            {
                ContractVersion = CharacterContractVersions.PreviousState,
            }).ToArray(),
            legacy.HouseholdStates.Select(state => state with
            {
                ContractVersion = CharacterContractVersions.PreviousState,
            }).ToArray()).Canonicalize();

        snapshot["characters"] = JsonSerializer.SerializeToNode(
            migratedCharacters,
            SimulationJson.CreateOptions());
        foreach (JsonObject state in snapshot["characters"]!["characterStates"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            state.Remove("educationAttainments");
        }

        JsonObject characterVersion = systemVersions
            .OfType<JsonObject>()
            .SingleOrDefault(node => IsCharacterSystemVersion(node, CharacterContractVersions.LegacySnapshot))
            ?? throw new SaveCompatibilityException(
                "Schema 5 is missing required 'simulation.characters@1' system-version data.");
        characterVersion["version"] = CharacterContractVersions.PreviousSnapshot;

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
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
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
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
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
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV17ToV18 : ISaveMigration
{
    public int FromSchemaVersion => 17;

    public int ToSchemaVersion => 18;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException(
                "Schema 17 save is missing snapshot or system-version data.");
        }

        if (snapshot.ContainsKey("characterPregnancies")
            || systemVersions.Any(node =>
                node?["systemId"]?.GetValue<string>()
                    == CharacterPregnancySystem.SystemId))
        {
            throw new SaveCompatibilityException(
                "Schema 17 unexpectedly contains schema 18 character-pregnancy data.");
        }

        snapshot["characterPregnancies"] = JsonSerializer.SerializeToNode(
            CharacterPregnancyWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = CharacterPregnancySystem.SystemId,
            ["version"] = CharacterPregnancySystem.Version,
        });

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 18 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV18ToV19 : ISaveMigration
{
    public int FromSchemaVersion => 18;

    public int ToSchemaVersion => 19;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot)
        {
            throw new SaveCompatibilityException(
                "Schema 18 save is missing authoritative snapshot data.");
        }

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 19 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV19ToV20 : ISaveMigration
{
    public int FromSchemaVersion => 19;

    public int ToSchemaVersion => 20;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["characters"] is not JsonObject characters
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException(
                "Schema 19 save is missing character or system-version data.");
        }

        characters["contractVersion"] = CharacterContractVersions.Snapshot;
        AdvanceStateVersions(
            characters,
            "characterStates",
            addEducationAttainments: true);
        AdvanceStateVersions(characters, "familyStates", addEducationAttainments: false);
        AdvanceStateVersions(characters, "householdStates", addEducationAttainments: false);
        UpgradeEmbeddedBirthChildStates(source["diagnosticEvents"]);

        JsonObject characterSystemVersion = systemVersions
            .OfType<JsonObject>()
            .SingleOrDefault(node =>
                node["systemId"]?.GetValue<string>() == "simulation.characters"
                && node["version"]?.GetValue<int>() == CharacterContractVersions.PreviousSnapshot)
            ?? throw new SaveCompatibilityException(
                $"Schema 19 is missing required 'simulation.characters@{CharacterContractVersions.PreviousSnapshot}' system-version data.");
        characterSystemVersion["version"] = CharacterContractVersions.Snapshot;

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated schema 20 snapshot is empty.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static void AdvanceStateVersions(
        JsonObject characters,
        string property,
        bool addEducationAttainments)
    {
        if (characters[property] is not JsonArray states)
        {
            throw new SaveCompatibilityException(
                $"Schema 19 character snapshot is missing '{property}' data.");
        }

        foreach (JsonObject state in states.OfType<JsonObject>())
        {
            if (state["contractVersion"]?.GetValue<int>()
                != CharacterContractVersions.PreviousState)
            {
                throw new SaveCompatibilityException(
                    $"Schema 19 character snapshot contains unsupported '{property}' state data.");
            }

            state["contractVersion"] = CharacterContractVersions.State;
            if (addEducationAttainments)
            {
                state["educationAttainments"] = new JsonArray();
            }
        }
    }

    private static void UpgradeEmbeddedBirthChildStates(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                UpgradeEmbeddedBirthChildStates(item);
            }

            return;
        }

        if (node is not JsonObject value)
        {
            return;
        }

        if (value["$type"]?.GetValue<string>() == "pregnancy_birth_resolved.v1"
            && value["birth"] is JsonObject birth
            && birth["childState"] is JsonObject childState)
        {
            if (childState["contractVersion"]?.GetValue<int>()
                != CharacterContractVersions.PreviousState)
            {
                throw new SaveCompatibilityException(
                    "Schema 19 pregnancy-birth diagnostics contain an unsupported child-state version.");
            }

            childState["contractVersion"] = CharacterContractVersions.State;
            childState["educationAttainments"] = new JsonArray();
        }

        foreach ((_, JsonNode? child) in value)
        {
            UpgradeEmbeddedBirthChildStates(child);
        }
    }
}

public sealed class SaveMigrationV20ToV21 : ISaveMigration
{
    public int FromSchemaVersion => 20;

    public int ToSchemaVersion => 21;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        WorldSnapshot snapshot = (source["snapshot"] as JsonObject)?.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                "Schema 20 save is missing its authoritative snapshot.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            snapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV21ToV22 : ISaveMigration
{
    private const int Schema22DeathContractVersion = 2;

    public int FromSchemaVersion => 21;

    public int ToSchemaVersion => 22;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        UpgradeCharacterDeathChanges(source["diagnosticEvents"]);
        WorldSnapshot snapshot = (source["snapshot"] as JsonObject)?.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                "Schema 21 save is missing its authoritative snapshot.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            snapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static void UpgradeCharacterDeathChanges(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                UpgradeCharacterDeathChanges(item);
            }

            return;
        }

        if (node is not JsonObject value)
        {
            return;
        }

        if (value["$type"]?.GetValue<string>() == "character_death_resolved.v1")
        {
            if (value["death"] is not JsonObject death
                || death["contractVersion"]?.GetValue<int>() != 1
                || death.ContainsKey("careerChanges"))
            {
                throw new SaveCompatibilityException(
                    "Schema 21 character-death diagnostics contain unsupported death-change data.");
            }

            death["contractVersion"] = Schema22DeathContractVersion;
            death["careerChanges"] = new JsonObject
            {
                ["contractVersion"] = CareerContractVersions.DeathChange,
                ["invalidatedProposals"] = new JsonArray(),
                ["endedRetinueMemberships"] = new JsonArray(),
                ["endedPatronageBonds"] = new JsonArray(),
                ["endedEmploymentTenures"] = new JsonArray(),
            };
        }

        foreach ((_, JsonNode? child) in value.ToArray())
        {
            UpgradeCharacterDeathChanges(child);
        }
    }
}

public sealed class SaveMigrationV22ToV23 : ISaveMigration
{
    private const int Schema22DeathContractVersion = 2;
    private const int Schema23DeathContractVersion = 3;

    public int FromSchemaVersion => 22;

    public int ToSchemaVersion => 23;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        UpgradeCharacterDeathChanges(source["diagnosticEvents"]);
        WorldSnapshot snapshot = (source["snapshot"] as JsonObject)?.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                "Schema 22 save is missing its authoritative snapshot.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            snapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }

    private static void UpgradeCharacterDeathChanges(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                UpgradeCharacterDeathChanges(item);
            }

            return;
        }

        if (node is not JsonObject value)
        {
            return;
        }

        if (value["$type"]?.GetValue<string>() == "character_death_resolved.v1")
        {
            if (value["death"] is not JsonObject death
                || death["contractVersion"]?.GetValue<int>()
                    != Schema22DeathContractVersion
                || death.ContainsKey("releasedCustodyChanges"))
            {
                throw new SaveCompatibilityException(
                    "Schema 22 character-death diagnostics contain unsupported death-change data.");
            }

            death["contractVersion"] = Schema23DeathContractVersion;
            death["releasedCustodyChanges"] = new JsonArray();
        }

        foreach ((_, JsonNode? child) in value.ToArray())
        {
            UpgradeCharacterDeathChanges(child);
        }
    }
}

public sealed class SaveMigrationV23ToV24 : ISaveMigration
{
    public int FromSchemaVersion => 23;

    public int ToSchemaVersion => 24;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        WorldSnapshot snapshot = (source["snapshot"] as JsonObject)?.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                "Schema 23 save is missing its authoritative snapshot.");
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            snapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV24ToV25 : ISaveMigration
{
    public int FromSchemaVersion => 24;

    public int ToSchemaVersion => 25;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        JsonObject snapshot = source["snapshot"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 24 save is missing its authoritative snapshot.");
        JsonArray systemVersions = snapshot["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Schema 24 save is missing system-version data.");
        if (snapshot.ContainsKey("characterSuccessions")
            || systemVersions.OfType<JsonObject>().Any(version =>
                version["systemId"]?.GetValue<string>()
                    == CharacterSuccessionSystem.SystemId))
        {
            throw new SaveCompatibilityException(
                "Schema 24 save unexpectedly contains character-succession data.");
        }

        snapshot["characterSuccessions"] = new JsonObject
        {
            ["contractVersion"] = 1,
            ["designations"] = new JsonArray(),
            ["history"] = new JsonArray(),
        };
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = CharacterSuccessionSystem.SystemId,
            ["version"] = 1,
        });

        WorldSnapshot migratedSnapshot =
            SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(snapshot, ToSchemaVersion);
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV25ToV26 : ISaveMigration
{
    public int FromSchemaVersion => 25;

    public int ToSchemaVersion => 26;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        JsonObject snapshot = source["snapshot"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 25 save is missing its authoritative snapshot.");
        JsonObject successions = snapshot["characterSuccessions"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 25 save is missing its character-succession snapshot.");
        JsonArray systemVersions = snapshot["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Schema 25 save is missing system-version data.");
        JsonObject[] successionVersions = systemVersions
            .OfType<JsonObject>()
            .Where(version => version["systemId"]?.GetValue<string>()
                == CharacterSuccessionSystem.SystemId)
            .ToArray();
        if (successions["contractVersion"]?.GetValue<int>() != 1
            || successions.ContainsKey("claims")
            || successions.ContainsKey("claimHistory")
            || successionVersions.Length != 1
            || successionVersions[0]["version"]?.GetValue<int>() != 1)
        {
            throw new SaveCompatibilityException(
                "Schema 25 save contains incompatible character-succession version data.");
        }

        successions["contractVersion"] = 2;
        successions["claims"] = new JsonArray();
        successions["claimHistory"] = new JsonArray();
        successionVersions[0]["version"] = 2;

        WorldSnapshot migratedSnapshot =
            SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
                snapshot,
                ToSchemaVersion);
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV26ToV27 : ISaveMigration
{
    public int FromSchemaVersion => 26;

    public int ToSchemaVersion => 27;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        JsonObject snapshot = source["snapshot"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 26 save is missing its authoritative snapshot.");
        JsonObject successions = snapshot["characterSuccessions"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 26 save is missing its character-succession snapshot.");
        JsonArray systemVersions = snapshot["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Schema 26 save is missing system-version data.");
        JsonObject[] successionVersions = systemVersions
            .OfType<JsonObject>()
            .Where(version => version["systemId"]?.GetValue<string>()
                == CharacterSuccessionSystem.SystemId)
            .ToArray();
        if (successions["contractVersion"]?.GetValue<int>() != 2
            || successions.ContainsKey("supports")
            || successions.ContainsKey("supportHistory")
            || successionVersions.Length != 1
            || successionVersions[0]["version"]?.GetValue<int>() != 2)
        {
            throw new SaveCompatibilityException(
                "Schema 26 save contains incompatible character-succession version data.");
        }

        successions["contractVersion"] = 3;
        successions["supports"] = new JsonArray();
        successions["supportHistory"] = new JsonArray();
        successionVersions[0]["version"] = 3;

        WorldSnapshot migratedSnapshot =
            SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
                snapshot,
                ToSchemaVersion);
        source["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            migratedSnapshot,
            ToSchemaVersion).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV27ToV28 : ISaveMigration
{
    public int FromSchemaVersion => 27;

    public int ToSchemaVersion => 28;

    public JsonObject Migrate(JsonObject source)
    {
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, FromSchemaVersion);
        JsonObject snapshot = source["snapshot"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 27 save is missing its authoritative snapshot.");
        JsonObject successions = snapshot["characterSuccessions"] as JsonObject
            ?? throw new SaveCompatibilityException(
                "Schema 27 save is missing its character-succession snapshot.");
        JsonArray systemVersions = snapshot["systemVersions"] as JsonArray
            ?? throw new SaveCompatibilityException(
                "Schema 27 save is missing system-version data.");
        JsonObject[] successionVersions = systemVersions
            .OfType<JsonObject>()
            .Where(version => version["systemId"]?.GetValue<string>()
                == CharacterSuccessionSystem.SystemId)
            .ToArray();
        if (successions["contractVersion"]?.GetValue<int>() != 3
            || successions.ContainsKey("resolutions")
            || successions.ContainsKey("resolutionHistory")
            || successions.ContainsKey("campaignContinuity")
            || successionVersions.Length != 1
            || successionVersions[0]["version"]?.GetValue<int>() != 3)
        {
            throw new SaveCompatibilityException(
                "Schema 27 save contains incompatible character-succession version data.");
        }

        successions["contractVersion"] = CharacterSuccessionContractVersions.Snapshot;
        successions["resolutions"] = new JsonArray();
        successions["resolutionHistory"] = new JsonObject
        {
            ["contractVersion"] = CharacterSuccessionContractVersions.ResolutionHistory,
            ["foldedSelectedCount"] = 0L,
            ["foldedDisputedCount"] = 0L,
            ["foldedNoSuccessorCount"] = 0L,
            ["earliestDate"] = null,
            ["latestDate"] = null,
        };
        successions["campaignContinuity"] = null;
        successionVersions[0]["version"] = CharacterSuccessionSystem.Version;

        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException(
                "Migrated schema 28 snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}
