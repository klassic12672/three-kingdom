using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterCustodianDeathSchemaMigrationTests
{
    private const int FrozenLength = 42_125;
    private const string FrozenFileSha256 =
        "13b11725a2705bdb2e3da36552a679ef2a272edc7a4ea10879d97a85d0c84d79";
    private const string FrozenChecksum =
        "002d05e1a0f467b2c92d24d4a8ad55f4ab324467955a7f524b33a6899e4d2705";
    private static readonly EntityId Custodian =
        new("character:fixture/f2-custodian");
    private static readonly EntityId Detained =
        new("character:fixture/f2-detained");
    private static readonly EntityId Captive =
        new("character:fixture/f2-captive");
    private static readonly EntityId Hostage =
        new("character:fixture/f2-hostage");
    private static readonly EntityId UnrelatedDependent =
        new("character:fixture/f2-unrelated-dependent");
    private static readonly EntityId UnrelatedCustodian =
        new("character:fixture/f2-unrelated-custodian");

    [Fact]
    public void F215_ExactF1Schema22MigratesStructurallyAndContinuesCustodianDeath()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(FrozenFileSha256, Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject frozen = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(22, frozen["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 22);
        JsonObject original = (JsonObject)frozen.DeepClone();
        JsonNode originalCareerChanges = HistoricalDeath(original)["careerChanges"]!.DeepClone();
        JsonObject expected = (JsonObject)original.DeepClone();
        expected["schemaVersion"] = SaveEnvelope.CurrentSchemaVersion;
        JsonObject expectedDeath = HistoricalDeath(expected);
        expectedDeath["contractVersion"] = CharacterConditionContractVersions.Death;
        expectedDeath["releasedCustodyChanges"] = new JsonArray();
        AddExpectedSuccession(expected);
        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(frozen);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated["schemaVersion"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(expected, migrated));
        Assert.True(JsonNode.DeepEquals(original, frozen));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        Assert.True(JsonNode.DeepEquals(
            originalCareerChanges,
            HistoricalDeath(migrated)["careerChanges"]));
        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        CharacterDeathChange historical = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(envelope.DiagnosticEvents.Single(
                item => item.Payload is CharacterConditionActionResolvedEventPayload
                {
                    Outcome: CharacterDeathResolvedOutcome,
                }).Payload).Outcome).Death;
        Assert.Empty(historical.ReleasedCustodyChanges);
        Assert.Single(historical.CareerChanges.InvalidatedProposals);
        Assert.Single(historical.CareerChanges.EndedRetinueMemberships);
        Assert.Single(historical.CareerChanges.EndedPatronageBonds);
        Assert.Single(historical.CareerChanges.EndedEmploymentTenures);
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        Assert.Equal(FrozenChecksum, SimulationChecksum.ComputeForSaveSchema(
            envelope.Snapshot,
            22).Value);
        Assert.Empty(envelope.Snapshot.CharacterSuccessions.Designations);
        Assert.Empty(envelope.Snapshot.CharacterSuccessions.History);

        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        CharacterDeathChange pendingDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).Death;
        Assert.Empty(pendingDeath.ReleasedCustodyChanges);
        CharacterDeathChange custodianDeath = SubmitCustodianDeath(
            simulation,
            "migration-continuation");
        Assert.Equal(3, custodianDeath.ReleasedCustodyChanges.Count);
        Assert.Equal(
            new[] { Detained, Captive, Hostage }.Order(),
            custodianDeath.ReleasedCustodyChanges.Select(item => item.CharacterId).Order());
        Assert.All(
            new[] { Detained, Captive, Hostage },
            id => Assert.Equal(
                CharacterCustodyStatus.Free,
                Profile(simulation, id).Condition.CustodyStatus));
        Assert.Equal(
            CharacterCustodyStatus.Detained,
            Profile(simulation, UnrelatedDependent).Condition.CustodyStatus);
        Assert.Equal(
            UnrelatedCustodian,
            Profile(simulation, UnrelatedDependent).Condition.CustodianId);
    }

    private static void AddExpectedSuccession(JsonObject expected)
    {
        JsonObject snapshot = expected["snapshot"]!.AsObject();
        snapshot["characterSuccessions"] = JsonSerializer.SerializeToNode(
            CharacterSuccessionWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        snapshot["systemVersions"]!.AsArray().Add(new JsonObject
        {
            ["systemId"] = CharacterSuccessionSystem.SystemId,
            ["version"] = CharacterSuccessionSystem.Version,
        });
        expected["checksum"] = SimulationChecksum.Compute(
            snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())!).Value;
    }

    [Fact]
    public void F215_Schema21To22To23FreezesEachHistoricalDeathVersion()
    {
        JsonObject schema21 = ReadSchema21Fixture();
        JsonObject original = (JsonObject)schema21.DeepClone();

        JsonObject schema22 = new SaveMigrationV21ToV22().Migrate(
            (JsonObject)schema21.DeepClone());

        Assert.Equal(22, schema22["schemaVersion"]!.GetValue<int>());
        JsonObject death22 = HistoricalDeath(schema22);
        Assert.Equal(2, death22["contractVersion"]!.GetValue<int>());
        Assert.False(death22.ContainsKey("releasedCustodyChanges"));
        Assert.Empty(death22["careerChanges"]!["invalidatedProposals"]!.AsArray());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(schema22, 22);
        JsonNode careerChanges = death22["careerChanges"]!.DeepClone();

        JsonObject schema23 = new SaveMigrationV22ToV23().Migrate(
            (JsonObject)schema22.DeepClone());

        Assert.Equal(23, schema23["schemaVersion"]!.GetValue<int>());
        JsonObject death23 = HistoricalDeath(schema23);
        Assert.Equal(3, death23["contractVersion"]!.GetValue<int>());
        Assert.Empty(death23["releasedCustodyChanges"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(careerChanges, death23["careerChanges"]));
        Assert.True(JsonNode.DeepEquals(original, schema21));
    }

    [Fact]
    public void F214_CurrentPendingAndResolvedCustodianDeathRoundTripThroughSaveStore()
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(SimulationJson.CreateOptions())!;
        CampaignSimulation original = new(WorldState.Restore(migrated.Snapshot));
        _ = Assert.Single(original.ResolveTurn());
        Assert.True(original.Submit(CustodianDeathCommand(original, "pending-save")).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f2-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            SaveStore store = new();
            string pendingPath = Path.Combine(directory, "pending.save.gz");
            store.SaveAtomic(pendingPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope pending = store.Load(pendingPath);
            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
            Assert.Single(pending.Snapshot.PendingCommands);
            CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));
            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
            CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
                Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(first).Payload).Outcome).Death;
            Assert.Equal(3, death.ReleasedCustodyChanges.Count);

            string resolvedPath = Path.Combine(directory, "resolved.save.gz");
            store.SaveAtomic(resolvedPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope resolved = store.Load(resolvedPath);
            CharacterDeathChange loaded = Assert.IsType<CharacterDeathResolvedOutcome>(
                Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                    resolved.DiagnosticEvents.Single(item =>
                        item.Payload is CharacterConditionActionResolvedEventPayload
                        {
                            Outcome: CharacterDeathResolvedOutcome outcome,
                        } && outcome.Death.ConditionChange.CharacterId == Custodian).Payload).Outcome).Death;
            Assert.Equal(Serialize(death), Serialize(loaded));
            WorldState restored = WorldState.Restore(resolved.Snapshot);
            Assert.All(
                new[] { Detained, Captive, Hostage },
                id => Assert.Equal(
                    CharacterCustodyStatus.Free,
                    restored.Characters.Profiles.Single(item => item.CharacterId == id)
                        .Condition.CustodyStatus));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("release-null")]
    [InlineData("release-empty")]
    [InlineData("release-entry")]
    [InlineData("release-root")]
    [InlineData("death-v3")]
    public void F216_Schema22RejectsEveryIsolatedF2InjectionWithoutMutation(
        string mutation)
    {
        JsonObject source = ReadFixture();
        JsonObject death = HistoricalDeath(source);
        switch (mutation)
        {
            case "release-null":
                death["releasedCustodyChanges"] = null;
                break;
            case "release-empty":
                death["releasedCustodyChanges"] = new JsonArray();
                break;
            case "release-entry":
                death["releasedCustodyChanges"] = new JsonArray(
                    new JsonObject { ["contractVersion"] = 1 });
                break;
            case "release-root":
                source["releasedCustodyChanges"] = new JsonArray();
                break;
            case "death-v3":
                death["contractVersion"] = CharacterConditionContractVersions.Death;
                break;
        }

        JsonObject tampered = (JsonObject)source.DeepClone();
        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(tampered, source));
    }

    [Fact]
    public void F216_Schema22ChecksumCorruptionAndCurrentDeathShapeFailClosed()
    {
        JsonObject corrupted = ReadFixture();
        corrupted["checksum"] = new string('0', 64);
        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(corrupted));

        JsonObject current = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture());
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            death => death["contractVersion"] = 2,
            death => death.Remove("releasedCustodyChanges"),
            death => death["releasedCustodyChanges"] = null,
            death => death["releasedCustodyChanges"] = new JsonObject(),
            death => death["releasedCustodyChanges"] = new JsonArray((JsonNode?)null),
            death => death["releasedCustodyChanges"] = new JsonArray(
                new JsonObject { ["contractVersion"] = 2 }),
            death => death["releasedCustodyChanges"] = new JsonArray(
                new JsonObject { ["contractVersion"] = 1 }),
        })
        {
            JsonObject invalid = (JsonObject)current.DeepClone();
            mutate(HistoricalDeath(invalid));
            JsonObject tampered = (JsonObject)invalid.DeepClone();
            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(invalid));
            Assert.True(JsonNode.DeepEquals(tampered, invalid));
        }
    }

    [Fact]
    public void F216_CurrentDeathRejectsIncompleteOrUnsupportedReleaseConditions()
    {
        JsonObject current = CurrentSaveWithCustodianDeath();
        JsonObject release = CustodianDeath(current)["releasedCustodyChanges"]!
            .AsArray()[0]!.AsObject();
        Assert.True(JsonNode.DeepEquals(
            current,
            new SaveSchemaRegistry().MigrateToCurrent(current)));

        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            change => change["previousCondition"] = new JsonObject(),
            change => change["currentCondition"] = new JsonObject(),
            change => change["previousCondition"]!["vitalStatus"] = 99,
            change => change["previousCondition"]!["healthStatus"] = 99,
            change => change["previousCondition"]!["isIncapacitated"] = 0,
            change => change["previousCondition"]!["custodyStatus"] = 99,
            change => change["previousCondition"]!.AsObject().Remove("custodianId"),
            change => change["currentCondition"]!["vitalStatus"] = 99,
            change => change["currentCondition"]!["healthStatus"] = 99,
            change => change["currentCondition"]!["isIncapacitated"] = 0,
            change => change["currentCondition"]!["custodyStatus"] = 99,
            change => change["currentCondition"]!.AsObject().Remove("custodianId"),
        })
        {
            JsonObject invalid = (JsonObject)current.DeepClone();
            mutate(CustodianDeath(invalid)["releasedCustodyChanges"]!
                .AsArray()[0]!.AsObject());
            JsonObject tampered = (JsonObject)invalid.DeepClone();
            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(invalid));
            Assert.True(JsonNode.DeepEquals(tampered, invalid));
        }

        Assert.NotNull(release["previousCondition"]!["custodianId"]);
        Assert.Null(release["currentCondition"]!["custodianId"]);
    }

    private static CharacterDeathChange SubmitCustodianDeath(
        CampaignSimulation simulation,
        string suffix)
    {
        CampaignCommand command = CustodianDeathCommand(simulation, suffix);
        CommandValidationResult validation = simulation.Submit(command);
        Assert.True(
            validation.IsValid,
            string.Join("; ", validation.Issues.Select(item => item.Message)));
        return Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).Death;
    }

    private static JsonObject CurrentSaveWithCustodianDeath()
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(SimulationJson.CreateOptions())!;
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        _ = Assert.Single(simulation.ResolveTurn());
        _ = SubmitCustodianDeath(simulation, "condition-shape");
        return JsonSerializer.SerializeToNode(
            SaveEnvelope.Create(
                "test",
                [],
                simulation,
                new DateTimeOffset(200, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            SimulationJson.CreateOptions())!.AsObject();
    }

    private static CampaignCommand CustodianDeathCommand(
        CampaignSimulation simulation,
        string suffix) => CampaignCommand.Create(
        new EntityId($"command:test/f2-{suffix}"),
        CharacterConditionSystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
            Custodian,
            Profile(simulation, Custodian).Condition)));

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId id)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            id,
            out AuthoritativeCharacterProfile? profile));
        return profile;
    }

    private static JsonObject HistoricalDeath(JsonObject source) => source["diagnosticEvents"]!
        .AsArray()
        .OfType<JsonObject>()
        .Single(item => item["payload"]?["outcome"]?["$type"]?.GetValue<string>()
            == "character_death_resolved.v1")
        ["payload"]!["outcome"]!["death"]!.AsObject();

    private static JsonObject CustodianDeath(JsonObject source) => source["diagnosticEvents"]!
        .AsArray()
        .OfType<JsonObject>()
        .Select(item => item["payload"]?["outcome"]?["death"])
        .OfType<JsonObject>()
        .Single(death => death["releasedCustodyChanges"] is JsonArray { Count: > 0 });

    private static JsonObject ReadFixture() => JsonNode.Parse(
        File.ReadAllBytes(FixturePath()))!.AsObject();

    private static JsonObject ReadSchema21Fixture() => JsonNode.Parse(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-21-history-backed.json")))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-22-history-backed.json");

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
