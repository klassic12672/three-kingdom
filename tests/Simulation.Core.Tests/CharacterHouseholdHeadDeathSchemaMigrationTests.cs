using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterHouseholdHeadDeathSchemaMigrationTests
{
    private const int FrozenLength = 15_227;
    private const string FrozenFileSha256 =
        "e2712c9a95618867d2543f57aeedc76f2a5b1843ecaa6b78e1072a5dd6b58588";
    private const string FrozenChecksum =
        "1f40e30dbaec836ac8258efbb4cf610a40968548bd2deb30abb621eb91314299";
    private static readonly EntityId Head = new("character:fixture/f3-head");
    private static readonly EntityId Replacement = new("character:fixture/f3-replacement");
    private static readonly EntityId Household = new("household:fixture/f3-primary");

    [Fact]
    public void F311_ExactF2Schema23MigratesVocabularyOnlyAndContinuesHeadDeath()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(FrozenFileSha256, Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject frozen = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(23, frozen["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 23);
        JsonObject original = (JsonObject)frozen.DeepClone();
        JsonObject expected = (JsonObject)original.DeepClone();
        expected["schemaVersion"] = 24;

        JsonObject schema24 = new SaveMigrationV23ToV24().Migrate(
            (JsonObject)frozen.DeepClone());

        Assert.Equal(24, schema24["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, schema24["checksum"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expected, schema24));
        Assert.True(JsonNode.DeepEquals(original, frozen));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(frozen);
        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated["schemaVersion"]!.GetValue<int>());
        JsonObject expectedCurrent = (JsonObject)original.DeepClone();
        expectedCurrent["schemaVersion"] = SaveEnvelope.CurrentSchemaVersion;
        AddExpectedSuccession(expectedCurrent);
        Assert.True(JsonNode.DeepEquals(expectedCurrent, migrated));
        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        Assert.Empty(envelope.Snapshot.CharacterSuccessions.Designations);
        Assert.Empty(envelope.Snapshot.CharacterSuccessions.History);
        CharacterDeathChange historical = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                Assert.Single(envelope.DiagnosticEvents).Payload).Outcome).Death;
        Assert.Equal(3, historical.ReleasedCustodyChanges.Count);
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        Assert.Equal(FrozenChecksum, SimulationChecksum.ComputeForSaveSchema(
            envelope.Snapshot,
            23).Value);

        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        Assert.Single(simulation.ResolveTurn());
        CampaignDate date = simulation.World.Calendar.Date;
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:fixture/f3-head-death"),
            CharacterConditionSystem.AuthoritativeActorId,
            date,
            new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                Household,
                Replacement)));
        Assert.True(simulation.Submit(command).IsValid);
        HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
            HouseholdHeadDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        Assert.Equal(Household, outcome.HouseholdHeadChange.HouseholdId);
        Assert.Equal(Replacement, outcome.HouseholdHeadChange.CurrentHeadCharacterId);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
        Assert.Equal(Replacement, simulation.World.Characters.Households.Single(
            item => item.HouseholdId == Household).HeadCharacterId);
        Assert.Equal(Head, Assert.Single(simulation.World.CharacterResources.Accounts).CharacterId);
        Assert.Equal(Head, Assert.Single(simulation.World.CharacterEstateHoldings.Holdings).OwnerCharacterId);
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
    public void F311_CorruptSchema23ChecksumRejectsWithoutMutatingSource()
    {
        JsonObject source = ReadFixture();
        source["checksum"] = new string('0', 64);
        JsonObject tampered = (JsonObject)source.DeepClone();

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));

        Assert.True(JsonNode.DeepEquals(tampered, source));
    }

    [Theory]
    [InlineData("action-discriminator")]
    [InlineData("outcome-discriminator")]
    [InlineData("replacement-null")]
    [InlineData("head-change-null")]
    [InlineData("previous-head-root")]
    [InlineData("current-head-root")]
    public void F312_Schema23RejectsEveryIsolatedF3InjectionWithoutMutation(string mutation)
    {
        JsonObject source = ReadFixture();
        JsonObject diagnosticCommand = source["diagnosticCommands"]!.AsArray()[0]!.AsObject();
        JsonObject diagnosticAction = diagnosticCommand["payload"]!["action"]!.AsObject();
        JsonObject diagnosticOutcome = source["diagnosticEvents"]!.AsArray()[0]!
            ["payload"]!["outcome"]!.AsObject();
        switch (mutation)
        {
            case "action-discriminator":
                diagnosticAction["$type"] = "resolve_household_head_death.v1";
                break;
            case "outcome-discriminator":
                diagnosticOutcome["$type"] = "household_head_death_resolved.v1";
                break;
            case "replacement-null":
                diagnosticAction["replacementHeadCharacterId"] = null;
                break;
            case "head-change-null":
                diagnosticOutcome["householdHeadChange"] = null;
                break;
            case "previous-head-root":
                source["previousHeadCharacterId"] = null;
                break;
            case "current-head-root":
                source["currentHeadCharacterId"] = null;
                break;
            default:
                throw new InvalidOperationException();
        }

        JsonObject tampered = (JsonObject)source.DeepClone();
        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(tampered, source));
    }

    [Theory]
    [InlineData("missing-head-change")]
    [InlineData("null-head-change")]
    [InlineData("empty-head-change")]
    [InlineData("wrong-head-change-version")]
    [InlineData("missing-current-head")]
    [InlineData("missing-action")]
    [InlineData("ordinary-outcome")]
    [InlineData("ordinary-action")]
    [InlineData("incomplete-death-condition")]
    [InlineData("wrong-marriage-version")]
    [InlineData("wrong-marriage-reason")]
    [InlineData("malformed-marriage-entry")]
    [InlineData("malformed-guardianship-entry")]
    [InlineData("malformed-pregnancy-entry")]
    public void F313_CurrentSchemaRejectsIncompleteOrMismatchedHeadDeathDiagnostics(
        string mutation)
    {
        JsonObject current = ResolvedCurrentSave();
        JsonObject payload = CurrentHeadDeathPayload(current);
        JsonObject action = payload["action"]!.AsObject();
        JsonObject outcome = payload["outcome"]!.AsObject();
        JsonObject headChange = outcome["householdHeadChange"]!.AsObject();
        JsonObject death = outcome["death"]!.AsObject();
        JsonObject marriageChanges = death["marriageChanges"]!.AsObject();
        switch (mutation)
        {
            case "missing-head-change":
                outcome.Remove("householdHeadChange");
                break;
            case "null-head-change":
                outcome["householdHeadChange"] = null;
                break;
            case "empty-head-change":
                outcome["householdHeadChange"] = new JsonObject();
                break;
            case "wrong-head-change-version":
                headChange["contractVersion"] = 2;
                break;
            case "missing-current-head":
                headChange.Remove("currentHeadCharacterId");
                break;
            case "missing-action":
                payload.Remove("action");
                break;
            case "ordinary-outcome":
                outcome["$type"] = "character_death_resolved.v1";
                outcome.Remove("householdHeadChange");
                break;
            case "ordinary-action":
                action["$type"] = "resolve_character_death.v1";
                action.Remove("householdId");
                action.Remove("replacementHeadCharacterId");
                break;
            case "incomplete-death-condition":
                death["conditionChange"]!["currentCondition"]!
                    .AsObject().Remove("isIncapacitated");
                break;
            case "wrong-marriage-version":
                marriageChanges["contractVersion"] = 2;
                break;
            case "wrong-marriage-reason":
                marriageChanges["reason"] = 99;
                break;
            case "malformed-marriage-entry":
                marriageChanges["invalidatedProposals"]!.AsArray().Add(new JsonObject());
                break;
            case "malformed-guardianship-entry":
                death["endedGuardianships"]!.AsArray().Add(new JsonObject());
                break;
            case "malformed-pregnancy-entry":
                death["removedPregnancies"]!.AsArray().Add(new JsonObject());
                break;
            default:
                throw new InvalidOperationException();
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    private static JsonObject ResolvedCurrentSave()
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(SimulationJson.CreateOptions())!;
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        Assert.Single(simulation.ResolveTurn());
        CampaignDate date = simulation.World.Calendar.Date;
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:fixture/f3-current-shape"),
            CharacterConditionSystem.AuthoritativeActorId,
            date,
            new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                Household,
                Replacement)));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());
        return JsonNode.Parse(JsonSerializer.Serialize(
            SaveEnvelope.Create("test", [], simulation),
            SimulationJson.CreateOptions()))!.AsObject();
    }

    private static JsonObject CurrentHeadDeathPayload(JsonObject source) =>
        source["diagnosticEvents"]!.AsArray().Select(item => item!.AsObject())
            .Single(item => item["payload"]?["outcome"]?["$type"]?.GetValue<string>()
                == "household_head_death_resolved.v1")["payload"]!.AsObject();

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId characterId) => simulation.World.Characters.Profiles.Single(
        item => item.CharacterId == characterId);

    private static JsonObject ReadFixture() => JsonNode.Parse(
        File.ReadAllBytes(FixturePath()))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-23-history-backed.json");
}
