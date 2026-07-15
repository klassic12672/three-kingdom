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
    public void SchemaFour_SaveLoad_RoundTripsCharacterState()
    {
        SaveEnvelope expected = CreateEnvelope(CreateCharacterSimulation());
        string path = Path.Combine(directory, "characters.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        Assert.Equal(4, actual.SchemaVersion);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Snapshot.Characters, CanonicalJson.Options),
            JsonSerializer.Serialize(actual.Snapshot.Characters, CanonicalJson.Options));
        Assert.Equal(expected.Checksum, actual.Checksum);
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
    [InlineData("missing-system-version")]
    public void SchemaFour_RequiresCompleteCharacterDataWithoutChangingSource(string mutation)
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
            case "missing-system-version":
                RemoveSystemVersion(snapshot, "simulation.characters");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-four-{mutation}.save.gz");
        WriteJsonGzip(path, current);
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
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaThree_MigratesEmptyCharactersAndRecomputesChecksumWithoutOverwritingSource()
    {
        string path = Path.Combine(directory, "schema-three.save.gz");
        WriteFrozenHistoricalFixture(path, 3);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion("simulation.characters", 1));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
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
        historicalShape.Remove("characters");
        if (schemaVersion < 3)
        {
            historicalShape.Remove("geography");
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(historicalShape, CanonicalJson.Options);
        string independentlyComputed = Convert.ToHexStringLower(SHA256.HashData(bytes));

        Assert.Equal(schemaVersion < 3 ? FrozenSchemaOneTwoChecksum : FrozenSchemaThreeChecksum, stored);
        Assert.Equal(stored, independentlyComputed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
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
        // Schema 1/2 are synthetic fixtures inferred from the registered migration contracts.
        string fileName = schemaVersion switch
        {
            1 => "save-schema-1-inferred.json",
            2 => "save-schema-2-inferred.json",
            3 => "save-schema-3-history-backed.json",
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion)),
        };
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private sealed class SimulatedInterruptionException : Exception;
}
