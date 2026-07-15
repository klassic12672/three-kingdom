using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class SaveStoreTests : IDisposable
{
    private const string FrozenSchemaOneTwoChecksum = "852cbd1b26d70d7270ac7ffc6eb1f59992d82b46be31ecee5a6c5873460e6a8e";
    private const string FrozenSchemaThreeChecksum = "6fb3219946ab9328e0a2ace7e6f1c90cab8b46ff592e8cecde2e0b9b8f3e362c";
    private const string FrozenSchemaFourChecksum = "48b94dad9d4dda78591243341afa16ece40e0ed157368f84c1189641684ecd3e";
    // Reconstructed literally from the exact schema-4 serializer contract at eaa3aaf.
    // Unlike the inferred schema-1/2 fixtures, this contains nonempty character history.
    private const string FrozenSchemaFourFixture = """{"schemaVersion":4,"contractVersion":2,"gameVersion":"0.1.0","createdUtc":"2026-07-15T00:00:00+00:00","contentManifests":[{"packId":{"value":"base:synthetic"},"version":"1.0.0","checksum":"sha256:abc","requiredForSimulation":true}],"seed":99,"snapshot":{"contractVersion":1,"calendar":{"date":{"year":191,"month":7,"day":14},"turnIndex":0,"daysInCurrentTurn":3},"rootSeed":99,"randomStreams":[],"entities":[],"pendingCommands":[],"systemVersions":[{"systemId":"simulation.calendar","version":1},{"systemId":"simulation.synthetic_entities","version":1},{"systemId":"simulation.command_events","version":1},{"systemId":"simulation.geography","version":1},{"systemId":"simulation.characters","version":1}],"lastEventDate":null,"lastEventPhase":null,"lastEventPriority":null,"lastEventId":null,"geography":{"graph":{"regions":[],"districts":[],"localities":[],"stops":[],"routes":[]},"season":0,"weather":0,"locations":[],"routes":[],"armies":[]},"characters":{"contractVersion":1,"identityDefinitions":[{"contractVersion":1,"id":{"value":"ability:synthetic/command"},"kind":0,"nameKey":{"value":"loc:ability/synthetic_command"}}],"characterDefinitions":[{"contractVersion":1,"id":{"value":"character:synthetic/adult"},"nameKey":{"value":"loc:character/synthetic_adult"},"birthDate":{"year":160,"month":1,"day":1},"abilityIds":[{"value":"ability:synthetic/command"}],"aptitudeIds":[],"traitIds":[],"ambitionIds":[],"reputationIds":[]}],"familyDefinitions":[],"householdDefinitions":[],"characterStates":[{"contractVersion":1,"characterId":{"value":"character:synthetic/adult"},"parentIds":[]}],"familyStates":[],"householdStates":[]}},"diagnosticCommands":[],"diagnosticEvents":[],"checksum":"48b94dad9d4dda78591243341afa16ece40e0ed157368f84c1189641684ecd3e"}""";
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"three-kingdom-tests-{Guid.NewGuid():N}");

    public SaveStoreTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void SaveLoad_RoundTripsSnapshotAndChecksumExactly()
    {
        CampaignSimulation simulation = CreateSimulation();
        SaveEnvelope expected = CreateEnvelope(simulation);
        string path = Path.Combine(directory, "campaign.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        Assert.Equal(expected.Checksum, actual.Checksum);
        Assert.Equal(
            SimulationChecksum.Compute(expected.Snapshot),
            SimulationChecksum.Compute(actual.Snapshot));
        Assert.Equal(expected.Seed, actual.Seed);
    }

    [Fact]
    public void SchemaFive_SaveLoad_RoundTripsCharacterAndRelationshipState()
    {
        SaveEnvelope expected = CreateEnvelope(CreateCharacterSimulation());
        string path = Path.Combine(directory, "characters.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        Assert.Equal(5, actual.SchemaVersion);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Snapshot.Characters, CanonicalJson.Options),
            JsonSerializer.Serialize(actual.Snapshot.Characters, CanonicalJson.Options));
        Assert.Equal(expected.Checksum, actual.Checksum);
        Assert.Empty(actual.Snapshot.Relationships.Subjects);
        Assert.True(WorldState.Restore(actual.Snapshot).Characters.TryGetCharacterProfile(
            new EntityId("character:synthetic/child"),
            out AuthoritativeCharacterProfile? profile));
        Assert.Equal(21, profile.Age);
    }

    [Theory]
    [InlineData("missing-characters")]
    [InlineData("null-characters")]
    [InlineData("partial-characters")]
    [InlineData("null-character-state")]
    [InlineData("missing-character-system-version")]
    [InlineData("missing-relationships")]
    [InlineData("null-relationships")]
    [InlineData("partial-relationships")]
    [InlineData("null-relationship-subject")]
    [InlineData("missing-relationship-system-version")]
    public void SchemaFive_RequiresCompleteCharacterAndRelationshipDataWithoutChangingSource(string mutation)
    {
        JsonObject current = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateSimulation()),
            CanonicalJson.Options)!.AsObject();
        JsonObject snapshot = current["snapshot"]!.AsObject();
        switch (mutation)
        {
            case "missing-characters":
                snapshot.Remove("characters");
                break;
            case "null-characters":
                snapshot["characters"] = null;
                break;
            case "partial-characters":
                snapshot["characters"]!.AsObject().Remove("characterStates");
                break;
            case "null-character-state":
                snapshot["characters"]!["characterStates"]!.AsArray().Add(null);
                break;
            case "missing-character-system-version":
                RemoveSystemVersion(snapshot, "simulation.characters");
                break;
            case "missing-relationships":
                snapshot.Remove("relationships");
                break;
            case "null-relationships":
                snapshot["relationships"] = null;
                break;
            case "partial-relationships":
                snapshot["relationships"]!.AsObject().Remove("subjects");
                break;
            case "null-relationship-subject":
                snapshot["relationships"]!["subjects"]!.AsArray().Add(null);
                break;
            case "missing-relationship-system-version":
                RemoveSystemVersion(snapshot, "simulation.relationships");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-five-{mutation}.save.gz");
        WriteJsonGzip(path, current);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFive_RejectsImpossibleRelationshipImpactWithoutChangingSource()
    {
        CampaignSimulation simulation = CreateCharacterSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/impossible-persisted-impact"),
            new EntityId("character:synthetic/child"),
            simulation.World.Calendar.Date,
            new RelationshipActionCommandPayload(
                new EntityId("character:synthetic/parent"),
                new RelationshipImpact(1, 0, 0, 0, 0, 0, 0, 0, 0),
                new EntityId("memory_meaning:test/persisted-impact"),
                10,
                MemoryPublicity.Private,
                0,
                []));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());

        JsonObject invalid = JsonSerializer.SerializeToNode(
            CreateEnvelope(simulation),
            CanonicalJson.Options)!.AsObject();
        JsonObject persistedMemory = invalid["snapshot"]!["relationships"]!["subjects"]![0]!
            ["detailedRelationships"]![0]!["memories"]![0]!.AsObject();
        persistedMemory["appliedImpact"]!["affection"] = int.MaxValue;
        WorldSnapshot invalidSnapshot = invalid["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        invalid["checksum"] = SimulationChecksum.Compute(invalidSnapshot).Value;
        string path = Path.Combine(directory, "schema-five-impossible-relationship-impact.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void InterruptedAtomicWrite_PreservesLastValidSave()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "campaign.save.gz");
        SaveEnvelope original = CreateEnvelope(CreateSimulation());
        store.SaveAtomic(path, original);
        byte[] originalBytes = File.ReadAllBytes(path);

        CampaignSimulation changed = CreateSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:changed"),
            changed.World.Entities[0].Id,
            changed.World.Calendar.Date,
            new AdjustResourcesCommandPayload(changed.World.Entities[0].Id, 0, 500, 0));
        Assert.True(changed.Submit(command).IsValid);
        changed.ResolveTurn();
        SaveEnvelope replacement = CreateEnvelope(changed);

        Assert.Throws<SimulatedInterruptionException>(() =>
            store.SaveAtomic(path, replacement, _ => throw new SimulatedInterruptionException()));

        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal(original.Checksum, store.Load(path).Checksum);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void CorruptPrimary_RemainsUntouchedAndRecoversNewestValidGeneration()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "autosave.save.gz");
        SaveEnvelope first = CreateEnvelope(CreateSimulation());
        store.SaveAutosave(path, first);

        CampaignSimulation changed = CreateSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:autosave/second"),
            changed.World.Entities[0].Id,
            changed.World.Calendar.Date,
            new AdjustResourcesCommandPayload(changed.World.Entities[0].Id, 0, 1, 0));
        Assert.True(changed.Submit(command).IsValid);
        changed.ResolveTurn();
        store.SaveAutosave(path, CreateEnvelope(changed));

        byte[] corrupt = [0x00, 0x01, 0x02, 0x03];
        File.WriteAllBytes(path, corrupt);
        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(first.Checksum, recovered.Envelope.Checksum);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.NotNull(recovered.RecoveryDiagnostic);
        Assert.Equal(corrupt, File.ReadAllBytes(path));
    }

    [Fact]
    public void MalformedCharacterPrimary_RemainsUntouchedAndRecoversNewestValidGeneration()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "malformed-character-autosave.save.gz");
        SaveEnvelope validGeneration = CreateEnvelope(CreateSimulation());
        store.SaveAutosave(path, validGeneration);
        store.SaveAutosave(path, CreateEnvelope(CreateSimulation()));

        JsonObject malformed = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterSimulation()),
            CanonicalJson.Options)!.AsObject();
        JsonArray states = malformed["snapshot"]!["characters"]!["characterStates"]!.AsArray();
        states.Add(states[0]!.DeepClone());
        WriteJsonGzip(path, malformed);
        byte[] malformedBytes = File.ReadAllBytes(path);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(validGeneration.Checksum, recovered.Envelope.Checksum);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.Contains("simulation validation", recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(malformedBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void MalformedRelationshipPrimary_RemainsUntouchedAndRecoversNewestValidGeneration()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "malformed-relationship-autosave.save.gz");
        SaveEnvelope validGeneration = CreateEnvelope(CreateSimulation());
        store.SaveAutosave(path, validGeneration);
        store.SaveAutosave(path, CreateEnvelope(CreateSimulation()));

        JsonObject malformed = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateSimulation()),
            CanonicalJson.Options)!.AsObject();
        malformed["snapshot"]!["relationships"]!["subjects"]!.AsArray().Add(null);
        WriteJsonGzip(path, malformed);
        byte[] malformedBytes = File.ReadAllBytes(path);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(validGeneration.Checksum, recovered.Envelope.Checksum);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.Contains("simulation validation", recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(malformedBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("contentManifests")]
    [InlineData("pendingCommands")]
    [InlineData("nullManifestEntry")]
    public void NullRequiredSaveData_RemainsUntouchedAndRecoversNewestValidGeneration(string mutation)
    {
        SaveStore store = new();
        string path = Path.Combine(directory, $"null-required-{mutation}.save.gz");
        SaveEnvelope oldest = CreateEnvelope(CreateSimulation());
        SaveEnvelope newestValid = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(1) };
        SaveEnvelope replacedPrimary = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(2) };
        store.SaveAutosave(path, oldest);
        store.SaveAutosave(path, newestValid);
        store.SaveAutosave(path, replacedPrimary);

        JsonObject malformed = JsonSerializer.SerializeToNode(
            replacedPrimary,
            CanonicalJson.Options)!.AsObject();
        switch (mutation)
        {
            case "contentManifests":
                malformed["contentManifests"] = null;
                break;
            case "pendingCommands":
                malformed["snapshot"]!["pendingCommands"] = null;
                break;
            case "nullManifestEntry":
                malformed["contentManifests"]!.AsArray()[0] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        WriteJsonGzip(path, malformed);
        byte[] malformedBytes = File.ReadAllBytes(path);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(newestValid.CreatedUtc, recovered.Envelope.CreatedUtc);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.Contains(Path.GetFileName(path), recovered.RecoveryDiagnostic, StringComparison.Ordinal);
        Assert.Equal(malformedBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("birthDate", "year")]
    [InlineData("entityId", "Entity IDs")]
    public void ConstructorBackedCharacterValueFailure_RecoversNewestValidGeneration(
        string mutation,
        string expectedCause)
    {
        SaveStore store = new();
        string path = Path.Combine(directory, $"constructor-backed-{mutation}.save.gz");
        SaveEnvelope oldest = CreateEnvelope(CreateCharacterSimulation());
        SaveEnvelope newestValid = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(1) };
        SaveEnvelope replacedPrimary = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(2) };
        store.SaveAutosave(path, oldest);
        store.SaveAutosave(path, newestValid);
        store.SaveAutosave(path, replacedPrimary);

        JsonObject malformed = JsonSerializer.SerializeToNode(
            replacedPrimary,
            CanonicalJson.Options)!.AsObject();
        JsonObject definition = malformed["snapshot"]!["characters"]!["characterDefinitions"]![0]!.AsObject();
        if (mutation == "birthDate")
        {
            definition["birthDate"]!["year"] = 0;
        }
        else
        {
            definition["id"]!["value"] = "not-an-entity-id";
        }

        WriteJsonGzip(path, malformed);
        byte[] malformedBytes = File.ReadAllBytes(path);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(newestValid.CreatedUtc, recovered.Envelope.CreatedUtc);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.Contains(Path.GetFileName(path), recovered.RecoveryDiagnostic, StringComparison.Ordinal);
        Assert.Contains("invalid serialized data", recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedCause, recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(malformedBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaOne_MigratesForwardWithoutOverwritingSource()
    {
        string path = Path.Combine(directory, "schema-one.save.gz");
        WriteFrozenHistoricalFixture(path, 1);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.DiagnosticEvents);
        Assert.Empty(migrated.Snapshot.Geography.Graph.Routes);
        Assert.Empty(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaTwo_MigratesEmptyGeographyWithoutOverwritingSource()
    {
        string path = Path.Combine(directory, "schema-two.save.gz");
        WriteFrozenHistoricalFixture(path, 2);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.Snapshot.Geography.Graph.Routes);
        Assert.Empty(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaThree_MigratesEmptyCharactersAndRelationshipsWithoutOverwritingSource()
    {
        string path = Path.Combine(directory, "schema-three.save.gz");
        WriteFrozenHistoricalFixture(path, 3);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion("simulation.characters", 1));
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion("simulation.relationships", 1));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFour_AuthenticatesNonemptyCharactersAndMigratesEmptyRelationshipsWithoutChangingSource()
    {
        string path = Path.Combine(directory, "schema-four.save.gz");
        WriteFrozenHistoricalFixture(path, 4);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        CharacterDefinition character = Assert.Single(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Equal(new EntityId("character:synthetic/adult"), character.Id);
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion("simulation.relationships", 1));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFourMigrationPreservesNonemptyGeographyAndCharacterData()
    {
        CharacterWorldSnapshot characters = CreateCharacterSimulation().World.CaptureSnapshot().Characters;
        WorldState world = WorldState.Create(
            new CampaignDate(191, 7, 14),
            99,
            [new SyntheticEntitySnapshot(GeographyFixture.Actor, SimulationTier.Full, 1, 1, 1, [])],
            GeographyFixture.Snapshot(),
            characters);
        JsonObject schemaFour = JsonSerializer.SerializeToNode(
            CreateEnvelope(new CampaignSimulation(world)),
            CanonicalJson.Options)!.AsObject();
        JsonObject snapshot = schemaFour["snapshot"]!.AsObject();
        snapshot.Remove("relationships");
        RemoveSystemVersion(snapshot, "simulation.relationships");
        schemaFour["schemaVersion"] = 4;
        WorldSnapshot historical = snapshot.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        schemaFour["checksum"] = SimulationChecksum.ComputeForSaveSchema(historical, 4).Value;
        string path = Path.Combine(directory, "schema-four-nonempty-geography-characters.save.gz");
        WriteJsonGzip(path, schemaFour);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(
            JsonSerializer.Serialize(historical.Geography, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Geography, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.Characters, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Characters, CanonicalJson.Options));
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CorruptedHistoricalSnapshotFailsAuthenticationWithoutOverwritingSource(int schemaVersion)
    {
        JsonObject historical = CreateHistoricalFixture(schemaVersion);
        historical["snapshot"]!["rootSeed"] = 100UL;
        string path = Path.Combine(directory, $"corrupt-schema-{schemaVersion}.save.gz");
        WriteJsonGzip(path, historical);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => new SaveStore().Load(path));

        Assert.Contains("checksum", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void FrozenLegacyFixturesRetainEraSpecificChecksums(int schemaVersion)
    {
        JsonObject historical = CreateHistoricalFixture(schemaVersion);
        string stored = historical["checksum"]!.GetValue<string>();
        WorldSnapshot snapshot = historical["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        WorldSnapshot canonical = snapshot with
        {
            RandomStreams = snapshot.RandomStreams.OrderBy(item => item.Context, StringComparer.Ordinal).ToArray(),
            Entities = snapshot.Entities.OrderBy(item => item.Id).Select(item => item.Canonicalize()).ToArray(),
            PendingCommands = snapshot.PendingCommands
                .OrderBy(command => command, CommandComparer.Instance)
                .Select(command => command with { Validation = CommandValidationResult.Valid })
                .ToArray(),
            SystemVersions = snapshot.SystemVersions.OrderBy(item => item.SystemId, StringComparer.Ordinal).ToArray(),
            Geography = snapshot.Geography.Canonicalize(),
            Characters = snapshot.Characters.Canonicalize(),
        };
        JsonObject historicalShape = JsonSerializer.SerializeToNode(canonical, CanonicalJson.Options)!.AsObject();
        historicalShape.Remove("relationships");
        if (schemaVersion < 4)
        {
            historicalShape.Remove("characters");
        }

        if (schemaVersion < 3)
        {
            historicalShape.Remove("geography");
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(historicalShape, CanonicalJson.Options);
        string independentlyComputed = Convert.ToHexStringLower(SHA256.HashData(bytes));

        string frozen = schemaVersion switch
        {
            < 3 => FrozenSchemaOneTwoChecksum,
            3 => FrozenSchemaThreeChecksum,
            4 => FrozenSchemaFourChecksum,
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion)),
        };
        Assert.Equal(frozen, stored);
        Assert.Equal(stored, independentlyComputed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LegacyExplicitNullRequiredData_FailsDeliberatelyAndRecoversWithoutChangingCandidates(
        int schemaVersion)
    {
        SaveStore store = new();
        string path = Path.Combine(directory, $"null-schema-{schemaVersion}.save.gz");
        SaveEnvelope oldest = CreateEnvelope(CreateSimulation());
        SaveEnvelope newestValid = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(1) };
        SaveEnvelope replacedPrimary = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(2) };
        store.SaveAutosave(path, oldest);
        store.SaveAutosave(path, newestValid);
        store.SaveAutosave(path, replacedPrimary);

        JsonObject malformed = CreateHistoricalFixture(schemaVersion);
        switch (schemaVersion)
        {
            case 1:
                malformed["snapshot"]!["randomStreams"] = null;
                break;
            case 2:
                malformed["snapshot"]!["entities"]![0]!["pendingWork"] = null;
                break;
            case 3:
                malformed["snapshot"]!["geography"]!["graph"] = null;
                break;
            case 4:
                malformed["snapshot"]!["characters"] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        WriteJsonGzip(path, malformed);
        string[] candidatePaths = [path, path + ".1", path + ".2"];
        Dictionary<string, byte[]> candidateBytes = candidatePaths.ToDictionary(
            candidate => candidate,
            File.ReadAllBytes,
            StringComparer.Ordinal);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(() => store.Load(path));
        Assert.Contains($"schema {schemaVersion}", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("malformed required historical snapshot data", exception.Message, StringComparison.OrdinalIgnoreCase);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(newestValid.CreatedUtc, recovered.Envelope.CreatedUtc);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.Contains(Path.GetFileName(path), recovered.RecoveryDiagnostic, StringComparison.Ordinal);
        Assert.Contains($"schema {schemaVersion}", recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
        foreach (string candidate in candidatePaths)
        {
            Assert.Equal(candidateBytes[candidate], File.ReadAllBytes(candidate));
        }
    }

    [Fact]
    public void FailedMigration_DoesNotOverwriteSource()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject invalid = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        invalid["schemaVersion"] = 1;
        string path = Path.Combine(directory, "invalid-schema-one.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void PartialSchemaThreeCharacterData_FailsMigrationWithoutOverwritingSource()
    {
        JsonObject partial = CreateHistoricalFixture(3);
        partial["snapshot"]!["characters"] = JsonSerializer.SerializeToNode(
            CharacterWorldSnapshot.Empty,
            CanonicalJson.Options);
        string path = Path.Combine(directory, "partial-schema-three.save.gz");
        WriteJsonGzip(path, partial);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => new SaveStore().Load(path));

        Assert.Equal("Schema 3 unexpectedly contains schema 4 character data.", exception.Message);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("system-version")]
    [InlineData("system-version-wrong-version")]
    public void SchemaFour_RejectsUnexpectedRelationshipDataWithoutOverwritingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(4);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        if (mutation == "snapshot")
        {
            snapshot["relationships"] = JsonSerializer.SerializeToNode(
                RelationshipWorldSnapshot.Empty,
                CanonicalJson.Options);
        }
        else
        {
            snapshot["systemVersions"]!.AsArray().Add(new JsonObject
            {
                ["systemId"] = "simulation.relationships",
                ["version"] = mutation == "system-version" ? 1 : 999,
            });
        }

        string path = Path.Combine(directory, $"schema-four-unexpected-relationships-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => new SaveStore().Load(path));

        Assert.Equal("Schema 4 unexpectedly contains schema 5 relationship data.", exception.Message);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void NewerSchema_IsRejectedWithoutOverwritingSource()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject future = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        future["schemaVersion"] = SaveEnvelope.CurrentSchemaVersion + 1;
        string path = Path.Combine(directory, "future-schema.save.gz");
        WriteJsonGzip(path, future);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingCharacters_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("characters");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode characterVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>() == "simulation.characters")!;
        versions.Remove(characterVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException("Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.Characters.Profiles);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion("simulation.characters", 1));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithExplicitEmptyCharacters_Restores()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot legacy = WithoutCharacterSystemVersion(current) with
        {
            Characters = CharacterWorldSnapshot.Empty,
        };

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.Characters.Profiles);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion("simulation.characters", 1));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullCharacters_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterSystemVersion(current) with
        {
            Characters = CharacterWorldSnapshot.Empty with { CharacterDefinitions = null! },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains("complete, valid, empty character snapshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithNonemptyCharacters_FailsDeliberately()
    {
        WorldSnapshot current = CreateCharacterSimulation().World.CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterSystemVersion(current);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains("complete, valid, empty character snapshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingRelationships_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("relationships");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode relationshipVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>() == "simulation.relationships")!;
        versions.Remove(relationshipVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException("Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.Relationships.Subjects);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion("simulation.relationships", 1));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullRelationships_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutRelationshipSystemVersion(current) with
        {
            Relationships = RelationshipWorldSnapshot.Empty with { Subjects = null! },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains("complete, valid, empty relationship snapshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRequiredManifest_BlocksLoadWithPreciseList()
    {
        SaveEnvelope envelope = CreateEnvelope(CreateSimulation());
        string path = Path.Combine(directory, "content.save.gz");
        new SaveStore().SaveAtomic(path, envelope);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(() =>
            new SaveStore().Load(path, []));

        Assert.Contains("base:synthetic@1.0.0 (sha256:abc)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOptionalPresentationManifest_MayBeSubstituted()
    {
        SaveEnvelope envelope = CreateEnvelope(CreateSimulation()) with
        {
            ContentManifests =
            [
                new(new EntityId("presentation:portraits"), "1.0.0", "sha256:optional", false),
            ],
        };
        string path = Path.Combine(directory, "optional-content.save.gz");
        new SaveStore().SaveAtomic(path, envelope);

        SaveEnvelope loaded = new SaveStore().Load(path, []);

        Assert.Equal(envelope.Checksum, loaded.Checksum);
    }

    [Fact]
    public void UnknownRequiredEnvelopeData_IsNotSilentlyDiscarded()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject json = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        json["futureRequiredState"] = new JsonObject { ["value"] = 42 };
        string path = Path.Combine(directory, "future-data.save.gz");
        WriteJsonGzip(path, json);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
    }

    [Fact]
    public void IncompatibleSnapshotSystemVersion_BlocksSave()
    {
        SaveEnvelope envelope = CreateEnvelope(CreateSimulation());
        SaveEnvelope incompatible = envelope with
        {
            Snapshot = envelope.Snapshot with
            {
                SystemVersions = [new SystemVersion("simulation.calendar", 999)],
            },
        };
        incompatible = incompatible with
        {
            Checksum = SimulationChecksum.Compute(incompatible.Snapshot).Value,
        };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveStore().SaveAtomic(Path.Combine(directory, "incompatible.save.gz"), incompatible));
    }

    [Fact]
    public void DiagnosticHistory_IsBounded()
    {
        CampaignSimulation simulation = CreateSimulation();
        for (int index = 0; index < 300; index++)
        {
            CampaignCommand invalid = CampaignCommand.Create(
                new EntityId($"command:invalid/{index:D4}"),
                new EntityId("actor:missing"),
                simulation.World.Calendar.Date,
                new ChangeSimulationTierCommandPayload(simulation.World.Entities[0].Id, SimulationTier.Full));
            Assert.False(simulation.Submit(invalid).IsValid);
        }

        SaveEnvelope envelope = CreateEnvelope(simulation);

        Assert.Equal(256, envelope.DiagnosticCommands.Count);
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    private static CampaignSimulation CreateSimulation() => new(SyntheticSimulation.CreateWorld(5, 99));

    private static CampaignSimulation CreateCharacterSimulation()
    {
        CharacterIdentityDefinition[] identities =
        [
            Identity("ability:synthetic/command", CharacterIdentityKind.Ability),
            Identity("ambition:synthetic/stewardship", CharacterIdentityKind.Ambition),
            Identity("aptitude:synthetic/cavalry", CharacterIdentityKind.Aptitude),
            Identity("reputation:synthetic/reliable", CharacterIdentityKind.Reputation),
            Identity("trait:synthetic/calm", CharacterIdentityKind.Trait),
        ];
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            identities,
            [
                Definition("character:synthetic/child", "loc:character/synthetic_child", new CampaignDate(170, 7, 13)),
                Definition("character:synthetic/parent", "loc:character/synthetic_parent", new CampaignDate(145, 1, 1)),
            ],
            [new FamilyDefinition(CharacterContractVersions.Definition, new EntityId("family:synthetic/test"), new EntityId("loc:family/synthetic_test"))],
            [new HouseholdDefinition(CharacterContractVersions.Definition, new EntityId("household:synthetic/test"), new EntityId("loc:household/synthetic_test"))],
            [
                new CharacterState(CharacterContractVersions.State, new EntityId("character:synthetic/child"), [new EntityId("character:synthetic/parent")]),
                new CharacterState(CharacterContractVersions.State, new EntityId("character:synthetic/parent"), []),
            ],
            [new FamilyState(CharacterContractVersions.State, new EntityId("family:synthetic/test"), [new EntityId("character:synthetic/child"), new EntityId("character:synthetic/parent")])],
            [new HouseholdState(CharacterContractVersions.State, new EntityId("household:synthetic/test"), new EntityId("character:synthetic/parent"), [new EntityId("character:synthetic/child"), new EntityId("character:synthetic/parent")])]);
        WorldState world = WorldState.Create(
            new CampaignDate(191, 7, 14),
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters);
        return new CampaignSimulation(world);
    }

    private static CharacterIdentityDefinition Identity(string id, CharacterIdentityKind kind) => new(
        CharacterContractVersions.Definition,
        new EntityId(id),
        kind,
        new EntityId($"loc:{id.Replace(':', '/')}"));

    private static CharacterDefinition Definition(string id, string nameKey, CampaignDate birthDate) => new(
        CharacterContractVersions.Definition,
        new EntityId(id),
        new EntityId(nameKey),
        birthDate,
        [new EntityId("ability:synthetic/command")],
        [new EntityId("aptitude:synthetic/cavalry")],
        [new EntityId("trait:synthetic/calm")],
        [new EntityId("ambition:synthetic/stewardship")],
        [new EntityId("reputation:synthetic/reliable")]);

    private static SaveEnvelope CreateEnvelope(CampaignSimulation simulation) => SaveEnvelope.Create(
        "0.1.0",
        [new ContentManifestReference(new EntityId("base:synthetic"), "1.0.0", "sha256:abc", true)],
        simulation,
        DateTimeOffset.Parse("2026-07-12T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static JsonObject CreateHistoricalFixture(int schemaVersion)
    {
        return JsonNode.Parse(ReadFrozenHistoricalFixture(schemaVersion))?.AsObject()
            ?? throw new InvalidDataException($"Frozen schema-{schemaVersion} fixture is empty.");
    }

    private static void RemoveSystemVersion(JsonObject snapshot, string systemId)
    {
        JsonArray versions = snapshot["systemVersions"]!.AsArray();
        JsonNode version = versions.Single(node => node!["systemId"]!.GetValue<string>() == systemId)!;
        versions.Remove(version);
    }

    private static WorldSnapshot WithoutCharacterSystemVersion(WorldSnapshot snapshot) => snapshot with
    {
        SystemVersions = snapshot.SystemVersions
            .Where(version => version.SystemId != "simulation.characters")
            .ToArray(),
    };

    private static WorldSnapshot WithoutRelationshipSystemVersion(WorldSnapshot snapshot) => snapshot with
    {
        SystemVersions = snapshot.SystemVersions
            .Where(version => version.SystemId != "simulation.relationships")
            .ToArray(),
    };

    private static void WriteJsonGzip(string path, JsonObject json)
    {
        using FileStream file = File.Create(path);
        using GZipStream gzip = new(file, CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(gzip, json, CanonicalJson.Options);
    }

    private static void WriteFrozenHistoricalFixture(string path, int schemaVersion)
    {
        byte[] json = Encoding.UTF8.GetBytes(ReadFrozenHistoricalFixture(schemaVersion));
        using FileStream file = File.Create(path);
        using GZipStream gzip = new(file, CompressionLevel.SmallestSize);
        gzip.Write(json);
    }

    private static string ReadFrozenHistoricalFixture(int schemaVersion)
    {
        // Schema 3 is reconstructed from the exact schema-3 contract at 4e6e83c.
        // Schema 4 is reconstructed from the exact schema-4 contract at eaa3aaf.
        // Schema 1/2 are synthetic fixtures inferred from the registered migration contracts.
        string fileName = schemaVersion switch
        {
            1 => "save-schema-1-inferred.json",
            2 => "save-schema-2-inferred.json",
            3 => "save-schema-3-history-backed.json",
            4 => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion)),
        };
        return schemaVersion == 4
            ? FrozenSchemaFourFixture
            : File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private sealed class SimulatedInterruptionException : Exception;
}
