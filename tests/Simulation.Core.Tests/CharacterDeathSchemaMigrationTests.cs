using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterDeathSchemaMigrationTests
{
    private const int FrozenLength = 16_755;
    private const string FrozenFileSha256 =
        "94830ad5b8a6d1862a2cca7e838f69148adeafa591a9709862169959e148ca1e";
    private const string FrozenChecksum =
        "9ad3e044f7491d4eca1f9e6afa5916ff2d3039efd687cfc944b1f10c82b6ef51";
    private static readonly EntityId DeathCandidate =
        new("character:fixture/e6-death-candidate");

    [Fact]
    public void F008_ExactE6Schema20PreservesVocabularyStepThenMigratesToCurrent()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(FrozenFileSha256, Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject frozen = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(20, frozen["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 20);
        JsonObject original = (JsonObject)frozen.DeepClone();

        JsonObject schema21 = new SaveMigrationV20ToV21().Migrate(
            (JsonObject)frozen.DeepClone());

        Assert.Equal(21, schema21["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, schema21["checksum"]!.GetValue<string>());
        JsonObject expectedSchema21 = (JsonObject)original.DeepClone();
        expectedSchema21["schemaVersion"] = 21;
        Assert.True(JsonNode.DeepEquals(expectedSchema21, schema21));

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(frozen);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated["schemaVersion"]!.GetValue<int>());
        JsonObject expectedCurrent = (JsonObject)original.DeepClone();
        expectedCurrent["schemaVersion"] = SaveEnvelope.CurrentSchemaVersion;
        AddExpectedSuccession(expectedCurrent);
        Assert.True(JsonNode.DeepEquals(expectedCurrent, migrated));
        Assert.True(JsonNode.DeepEquals(original, frozen));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        WorldSnapshot snapshot = migrated["snapshot"]!.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())!;
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(snapshot).Value);
        Assert.Equal(FrozenChecksum, SimulationChecksum.ComputeForSaveSchema(snapshot, 20).Value);
        Assert.Empty(snapshot.CharacterSuccessions.Designations);
        Assert.Empty(snapshot.CharacterSuccessions.History);
        Assert.Equal(CharacterContractVersions.Snapshot, snapshot.Characters.ContractVersion);
        Assert.Contains(
            snapshot.Characters.CharacterStates,
            item => item.EducationAttainments is { Count: 1 });
        Assert.Single(snapshot.PendingCommands);
        Assert.Single(snapshot.CharacterPregnancies.ActivePregnancies);
        Assert.Single(snapshot.CharacterGuardianships.Guardianships);
        Assert.Single(snapshot.Careers.Recommendations);
        Assert.Single(snapshot.CharacterResources.Accounts);
        Assert.Single(snapshot.CharacterEstateHoldings.Holdings);
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
    public void F009_MigratedRichSchema20ContinuesEducationThenExecutesPublicDeath()
    {
        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture());
        WorldSnapshot snapshot = migrated["snapshot"]!.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())!;
        CampaignSimulation simulation = new(WorldState.Restore(snapshot));

        CampaignEvent education = Assert.Single(simulation.ResolveTurn());
        Assert.IsType<PrimaryGuardianEducationCompletedOutcome>(
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(education.Payload).Outcome);
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            DeathCandidate,
            out AuthoritativeCharacterProfile? beforeProfile));
        string resources = Serialize(simulation.World.CharacterResources.CaptureSnapshot());
        string estates = Serialize(simulation.World.CharacterEstateHoldings.CaptureSnapshot());
        string career = Serialize(simulation.World.Careers.CaptureSnapshot());
        CampaignCommand death = CampaignCommand.Create(
            new EntityId("command:test/f0-schema20-continuation-death"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterDeathAction(DeathCandidate, beforeProfile.Condition)));

        Assert.True(simulation.Submit(death).IsValid);
        CharacterDeathChange resolvedDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).Death;

        Assert.Equal(DeathCandidate, resolvedDeath.ConditionChange.CharacterId);
        Assert.Empty(resolvedDeath.EndedGuardianships);
        Assert.Empty(resolvedDeath.RemovedPregnancies);
        Assert.Equal(resources, Serialize(simulation.World.CharacterResources.CaptureSnapshot()));
        Assert.Equal(estates, Serialize(simulation.World.CharacterEstateHoldings.CaptureSnapshot()));
        Assert.Equal(career, Serialize(simulation.World.Careers.CaptureSnapshot()));
        Assert.Equal(
            CharacterVitalStatus.Dead,
            simulation.World.Characters.Profiles.Single(
                item => item.CharacterId == DeathCandidate).Condition.VitalStatus);
        Assert.Contains(
            simulation.World.Characters.Households.Single().MemberIds,
            id => id == DeathCandidate);
    }

    [Theory]
    [InlineData("action-discriminator", "resolve_character_death.v1")]
    [InlineData("outcome-discriminator", "character_death_resolved.v1")]
    [InlineData("death", null)]
    [InlineData("deathId", null)]
    [InlineData("conditionChange", null)]
    [InlineData("endedGuardianships", null)]
    [InlineData("removedPregnancies", null)]
    public void F010_Schema20RejectsEveryIsolatedF0VocabularyInjection(
        string injection,
        string? discriminator)
    {
        JsonObject source = ReadFixture();
        JsonObject original = (JsonObject)source.DeepClone();
        if (injection == "action-discriminator")
        {
            source["snapshot"]!["pendingCommands"]![0]!["payload"]!["action"]!["$type"] =
                discriminator;
        }
        else if (injection == "outcome-discriminator")
        {
            source["diagnosticEvents"]![0]!["payload"]!["outcome"]!["$type"] =
                discriminator;
        }
        else
        {
            source[injection] = null;
        }

        JsonObject tampered = (JsonObject)source.DeepClone();
        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(tampered, source));
        Assert.False(JsonNode.DeepEquals(original, source));
    }

    [Fact]
    public void F011_Schema20CorruptionFailsAuthenticationWithoutChangingTheFixture()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        JsonObject source = ReadFixture();
        source["checksum"] = new string('0', 64);
        JsonObject tampered = (JsonObject)source.DeepClone();

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(tampered, source));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    private static JsonObject ReadFixture() => JsonNode.Parse(
        File.ReadAllBytes(FixturePath()))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-20-history-backed.json");

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
