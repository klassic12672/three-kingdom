using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterCareerDeathSchemaMigrationTests
{
    private const int FrozenLength = 34_795;
    private const string FrozenFileSha256 =
        "8214d8f3430e48a09bc215a2dfc34608924472e44622885250351350a4581686";
    private const string FrozenChecksum =
        "a108ed5ad5932992db2c2adfac6a9987320f3ab2493a2719d4035c05701b39e2";
    private static readonly EntityId CareerTarget =
        new("character:fixture/f0-career-target");

    [Fact]
    public void F115_ExactF0Schema21MigratesDeathDiagnosticsStructurallyAndContinues()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(FrozenFileSha256, Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject frozen = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(21, frozen["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 21);
        JsonObject original = (JsonObject)frozen.DeepClone();
        JsonObject expected = (JsonObject)original.DeepClone();
        expected["schemaVersion"] = SaveEnvelope.CurrentSchemaVersion;
        JsonObject expectedDeath = HistoricalDeath(expected);
        expectedDeath["contractVersion"] = CharacterConditionContractVersions.Death;
        expectedDeath["careerChanges"] = EmptyCareerChanges();
        expectedDeath["releasedCustodyChanges"] = new JsonArray();

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(frozen);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, migrated["checksum"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expected, migrated));
        Assert.True(JsonNode.DeepEquals(original, frozen));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        CharacterDeathChange historicalDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                envelope.DiagnosticEvents.Single(item =>
                    item.Payload is CharacterConditionActionResolvedEventPayload
                    {
                        Outcome: CharacterDeathResolvedOutcome,
                    }).Payload).Outcome).Death;
        Assert.Equal(CharacterConditionContractVersions.Death, historicalDeath.ContractVersion);
        Assert.Empty(historicalDeath.ReleasedCustodyChanges);
        Assert.Empty(historicalDeath.CareerChanges.InvalidatedProposals);
        Assert.Empty(historicalDeath.CareerChanges.EndedRetinueMemberships);
        Assert.Empty(historicalDeath.CareerChanges.EndedPatronageBonds);
        Assert.Empty(historicalDeath.CareerChanges.EndedEmploymentTenures);
        Assert.Equal(FrozenChecksum, SimulationChecksum.Compute(envelope.Snapshot).Value);
        Assert.Equal(FrozenChecksum, SimulationChecksum.ComputeForSaveSchema(
            envelope.Snapshot,
            21).Value);

        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        CharacterDeathChange careerDeath = SubmitCareerTargetDeath(simulation, "migration-continuation");
        Assert.Single(careerDeath.CareerChanges.InvalidatedProposals);
        Assert.Single(careerDeath.CareerChanges.EndedRetinueMemberships);
        Assert.Single(careerDeath.CareerChanges.EndedPatronageBonds);
        Assert.Single(careerDeath.CareerChanges.EndedEmploymentTenures);
    }

    [Fact]
    public void F114_CurrentSchemaPendingAndResolvedCareerDeathRoundTripThroughSaveStore()
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(SimulationJson.CreateOptions())!;
        CampaignSimulation original = new(WorldState.Restore(migrated.Snapshot));
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f1-save-{Guid.NewGuid():N}");
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

            CharacterDeathChange death = SubmitCareerTargetDeath(original, "save-roundtrip");
            Assert.NotEmpty(death.CareerChanges.InvalidatedProposals);
            string resolvedPath = Path.Combine(directory, "resolved.save.gz");
            store.SaveAtomic(resolvedPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope resolved = store.Load(resolvedPath);
            CharacterDeathChange loadedDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
                Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                    resolved.DiagnosticEvents.Single(item =>
                        item.Payload is CharacterConditionActionResolvedEventPayload
                        {
                            Outcome: CharacterDeathResolvedOutcome outcome,
                        } && outcome.Death.ConditionChange.CharacterId == CareerTarget).Payload).Outcome).Death;
            Assert.Equal(Serialize(death), Serialize(loadedDeath));
            Assert.DoesNotContain(
                resolved.Snapshot.Careers.RetinueMemberships,
                item => item.IsActive && item.MemberCharacterId == CareerTarget);
            Assert.DoesNotContain(
                resolved.Snapshot.Careers.PatronageBonds,
                item => item.IsActive && item.BeneficiaryCharacterId == CareerTarget);
            Assert.DoesNotContain(
                resolved.Snapshot.Careers.EmploymentTenures,
                item => item.IsActive && item.EmployeeCharacterId == CareerTarget);
            _ = WorldState.Restore(resolved.Snapshot);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("careerChanges-null")]
    [InlineData("death-v2")]
    [InlineData("invalidated-proposals-direct")]
    [InlineData("ended-memberships")]
    [InlineData("ended-bonds")]
    [InlineData("ended-tenures")]
    [InlineData("reason-5")]
    [InlineData("reason-6")]
    [InlineData("reason-7")]
    [InlineData("reason-8")]
    [InlineData("reason-9")]
    [InlineData("reason-10")]
    public void F116_Schema21RejectsEveryIsolatedF1InjectionWithoutChangingSource(
        string mutation)
    {
        JsonObject source = ReadFixture();
        JsonObject death = HistoricalDeath(source);
        if (mutation == "careerChanges-null")
        {
            death["careerChanges"] = null;
        }
        else if (mutation == "death-v2")
        {
            death["contractVersion"] = 2;
        }
        else if (mutation == "invalidated-proposals-direct")
        {
            death["invalidatedProposals"] = null;
        }
        else if (mutation == "ended-memberships")
        {
            source["endedRetinueMemberships"] = null;
        }
        else if (mutation == "ended-bonds")
        {
            source["endedPatronageBonds"] = null;
        }
        else if (mutation == "ended-tenures")
        {
            source["endedEmploymentTenures"] = null;
        }
        else
        {
            int reason = int.Parse(
                mutation.AsSpan("reason-".Length),
                System.Globalization.CultureInfo.InvariantCulture);
            source["snapshot"]!["careers"]!["retinueMemberships"]![0]!["endReason"] = reason;
        }

        JsonObject tampered = (JsonObject)source.DeepClone();
        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(tampered, source));
    }

    [Fact]
    public void F116_Schema21ChecksumCorruptionAndCurrentDeathShapeFailClosed()
    {
        JsonObject corrupted = ReadFixture();
        corrupted["checksum"] = new string('0', 64);
        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(corrupted));

        JsonObject current = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture());
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            death => death["contractVersion"] = 1,
            death => death["careerChanges"] = null,
            death => death["careerChanges"]!["contractVersion"] = 2,
            death => death["careerChanges"]!["endedRetinueMemberships"] = null,
            death => death["careerChanges"]!["invalidatedProposals"]!.AsArray().Add(
                new JsonObject { ["contractVersion"] = 2 }),
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

    private static CharacterDeathChange SubmitCareerTargetDeath(
        CampaignSimulation simulation,
        string suffix)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            CareerTarget,
            out AuthoritativeCharacterProfile? profile));
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/f1-{suffix}"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterDeathAction(CareerTarget, profile.Condition)));
        CommandValidationResult validation = simulation.Submit(command);
        Assert.True(
            validation.IsValid,
            string.Join("; ", validation.Issues.Select(item => item.Message)));
        return Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).Death;
    }

    private static JsonObject HistoricalDeath(JsonObject source) => source["diagnosticEvents"]!
        .AsArray()
        .OfType<JsonObject>()
        .Single(item => item["payload"]?["outcome"]?["$type"]?.GetValue<string>()
            == "character_death_resolved.v1")
        ["payload"]!["outcome"]!["death"]!.AsObject();

    private static JsonObject EmptyCareerChanges() => new()
    {
        ["contractVersion"] = CareerContractVersions.DeathChange,
        ["invalidatedProposals"] = new JsonArray(),
        ["endedRetinueMemberships"] = new JsonArray(),
        ["endedPatronageBonds"] = new JsonArray(),
        ["endedEmploymentTenures"] = new JsonArray(),
    };

    private static JsonObject ReadFixture() => JsonNode.Parse(
        File.ReadAllBytes(FixturePath()))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-21-history-backed.json");

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
