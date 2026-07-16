using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterObserverIntegrationSchemaMigrationTests : IDisposable
{
    private const int FrozenLength = 16_294;
    private const string FrozenFileSha256 =
        "f603c2ae99bf836b612edf077d9f157c9faff1a03fc85ca54482fc7a780a0ba8";
    private const string FrozenChecksum =
        "7ee3c84b8a29fac72f20f1a2f776073ea33e9ae32abdcc11d26879a3d7b1215f";
    private const string FrozenSourceRevision =
        "d64ab315a96fda15a2802ebcc687bbf50924fa2c";
    private static readonly EntityId OtherCharacter =
        new("character:fixture/g-other");
    private static readonly EntityId Successor =
        new("character:fixture/g-successor");
    private static readonly EntityId Subject =
        new("character:fixture/g-subject");
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"three-kingdom-g-schema-{Guid.NewGuid():N}");

    [Fact]
    public void G_Schema28FixtureAuthenticatesMigratesVocabularyOnlyAndContinues()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(
            FrozenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject source = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(
            $"sp04f9@{FrozenSourceRevision}",
            source["gameVersion"]!.GetValue<string>());
        Assert.Equal(28, source["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, source["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, 28);
        JsonObject original = (JsonObject)source.DeepClone();
        WorldSnapshot historical =
            SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
                source["snapshot"]!.AsObject(),
                28);
        Assert.Equal(
            FrozenChecksum,
            SimulationChecksum.ComputeForSaveSchema(historical, 28).Value);

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(source);

        Assert.True(JsonNode.DeepEquals(original, source));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        Assert.Equal(
            SaveEnvelope.CurrentSchemaVersion,
            migrated["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, migrated["checksum"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            original["snapshot"],
            migrated["snapshot"]));
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticCommands"],
            migrated["diagnosticCommands"]));
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticEvents"],
            migrated["diagnosticEvents"]));

        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        Assert.Equal(
            FrozenChecksum,
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        Assert.Equal(
            Successor,
            simulation.World.CharacterSuccessions.CampaignContinuity!
                .ControlledCharacterId);
        Assert.Equal(
            Subject,
            Assert.Single(simulation.World.CharacterSuccessions.Resolutions)
                .SubjectCharacterId);
        CharacterConditionChangedOutcome outcome = Assert.IsType<
            CharacterConditionChangedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        Assert.Equal(OtherCharacter, outcome.Change.CharacterId);
        Assert.True(outcome.Change.CurrentCondition.IsIncapacitated);
    }

    [Theory]
    [InlineData("discriminator")]
    [InlineData("resulting-health")]
    [InlineData("resulting-incapacitated")]
    public void G_Schema28RejectsSchema29VocabularyIncludingExplicitNull(
        string mutation)
    {
        JsonObject source = ReadFixture();
        source["gProbe"] = mutation switch
        {
            "discriminator" => new JsonObject
            {
                ["$type"] = "apply_character_wound.v1",
            },
            "resulting-health" => new JsonObject
            {
                ["resultingHealthStatus"] = null,
            },
            "resulting-incapacitated" => new JsonObject
            {
                ["resultingIncapacitated"] = null,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
        JsonObject original = (JsonObject)source.DeepClone();

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(original, source));
    }

    [Fact]
    public void G_WoundPendingResolvedSaveLoadAndReplayRoundTrip()
    {
        Directory.CreateDirectory(directory);
        CampaignSimulation original = CreateCurrentSimulation();
        CharacterConditionState expected = CurrentCondition(original, OtherCharacter);
        CampaignCommand wound = CampaignCommand.Create(
            new EntityId("command:test/g-wound-round-trip"),
            CharacterConditionSystem.AuthoritativeActorId,
            original.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new ApplyCharacterWoundAction(
                    OtherCharacter,
                    expected,
                    CharacterHealthStatus.Injured,
                    ResultingIncapacitated: true)));
        Assert.True(original.Submit(wound).IsValid);

        SaveStore store = new();
        string pendingPath = Path.Combine(directory, "pending.save.gz");
        store.SaveAtomic(
            pendingPath,
            SaveEnvelope.Create("test", [], original));
        SaveEnvelope pending = store.Load(pendingPath);
        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
        Assert.IsType<ApplyCharacterWoundAction>(Assert.IsType<
            CharacterConditionActionCommandPayload>(
                Assert.Single(pending.Snapshot.PendingCommands).Payload).Action);
        CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));

        IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
        IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();
        Assert.Equal(Serialize(first), Serialize(second));
        CharacterConditionChangedOutcome outcome = Assert.IsType<
            CharacterConditionChangedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(first).Payload).Outcome);
        Assert.Equal(CharacterHealthStatus.Injured,
            outcome.Change.CurrentCondition.HealthStatus);
        Assert.True(outcome.Change.CurrentCondition.IsIncapacitated);

        string resolvedPath = Path.Combine(directory, "resolved.save.gz");
        store.SaveAtomic(
            resolvedPath,
            SaveEnvelope.Create("test", [], original));
        SaveEnvelope resolved = store.Load(resolvedPath);
        Assert.Contains(
            resolved.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterConditionActionResolvedEventPayload
            {
                Action: ApplyCharacterWoundAction,
                Outcome: CharacterConditionChangedOutcome,
                RelationshipMemoryConsequence: null,
            });
        CampaignSimulation continued = new(WorldState.Restore(resolved.Snapshot));
        Assert.Empty(continued.ResolveTurn());
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
    }

    [Fact]
    public void G_WoundDiagnosticsAllowInvalidReplayAndIndependentRetentionEviction()
    {
        CampaignSimulation simulation = CreateCurrentSimulation();
        CharacterConditionState expected = CurrentCondition(simulation, OtherCharacter);
        ApplyCharacterWoundAction action = new(
            OtherCharacter,
            expected,
            CharacterHealthStatus.Injured,
            ResultingIncapacitated: true);
        CampaignCommand wound = CampaignCommand.Create(
            new EntityId("command:test/g-wound-retention"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(action));
        Assert.True(simulation.Submit(wound).IsValid);
        _ = simulation.ResolveTurn();

        Assert.False(simulation.Submit(wound).IsValid);
        JsonObject replaySave = JsonSerializer.SerializeToNode(
            SaveEnvelope.Create("test", [], simulation),
            CanonicalJson.Options)!.AsObject();
        _ = new SaveSchemaRegistry().MigrateToCurrent(replaySave);

        for (int index = 0; index < 255; index++)
        {
            CampaignCommand stale = CampaignCommand.Create(
                new EntityId($"command:test/g-wound-retention-{index:D3}"),
                CharacterConditionSystem.AuthoritativeActorId,
                simulation.World.Calendar.Date,
                new CharacterConditionActionCommandPayload(action));
            Assert.False(simulation.Submit(stale).IsValid);
        }

        Assert.DoesNotContain(
            simulation.RecentCommands,
            item => item.CommandId == wound.CommandId
                && item.Validation.IsValid);
        JsonObject evictedSave = JsonSerializer.SerializeToNode(
            SaveEnvelope.Create("test", [], simulation),
            CanonicalJson.Options)!.AsObject();
        _ = new SaveSchemaRegistry().MigrateToCurrent(evictedSave);
    }

    [Theory]
    [InlineData("missing-health")]
    [InlineData("null-incapacitated")]
    [InlineData("invalid-transition")]
    [InlineData("mismatched-command-action")]
    [InlineData("duplicate-valid-command")]
    [InlineData("mismatched-outcome")]
    [InlineData("null-change")]
    [InlineData("mismatched-condition")]
    [InlineData("mismatched-marriage-reason")]
    [InlineData("affected-ids")]
    public void G_CurrentSchemaRejectsMalformedOrInexactWoundDiagnostics(
        string mutation)
    {
        CampaignSimulation simulation = CreateCurrentSimulation();
        CharacterConditionState expected = CurrentCondition(simulation, OtherCharacter);
        CampaignCommand wound = CampaignCommand.Create(
            new EntityId("command:test/g-wound-tamper"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new ApplyCharacterWoundAction(
                    OtherCharacter,
                    expected,
                    CharacterHealthStatus.Injured,
                    ResultingIncapacitated: true)));
        Assert.True(simulation.Submit(wound).IsValid);
        _ = simulation.ResolveTurn();
        JsonObject save = JsonSerializer.SerializeToNode(
            SaveEnvelope.Create("test", [], simulation),
            CanonicalJson.Options)!.AsObject();
        JsonArray diagnosticCommands = save["diagnosticCommands"]!.AsArray();
        JsonObject diagnosticCommand = diagnosticCommands
            .OfType<JsonObject>()
            .Single(item => item["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "apply_character_wound.v1");
        JsonObject action = diagnosticCommand["payload"]!["action"]!.AsObject();
        JsonObject campaignEvent = save["diagnosticEvents"]!.AsArray()
            .OfType<JsonObject>()
            .Single(item => item["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "apply_character_wound.v1");
        JsonObject payload = campaignEvent["payload"]!.AsObject();

        switch (mutation)
        {
            case "missing-health":
                action.Remove("resultingHealthStatus");
                break;
            case "null-incapacitated":
                action["resultingIncapacitated"] = null;
                break;
            case "invalid-transition":
                action["resultingHealthStatus"] =
                    (int)CharacterHealthStatus.Healthy;
                break;
            case "mismatched-command-action":
                action["resultingHealthStatus"] =
                    (int)CharacterHealthStatus.Critical;
                action["resultingIncapacitated"] = true;
                break;
            case "duplicate-valid-command":
                diagnosticCommands.Add(diagnosticCommand.DeepClone());
                break;
            case "mismatched-outcome":
                payload["outcome"]!["$type"] = "character_death_resolved.v1";
                break;
            case "null-change":
                payload["outcome"]!["change"] = null;
                break;
            case "mismatched-condition":
                payload["outcome"]!["change"]!["currentCondition"]![
                    "healthStatus"] = (int)CharacterHealthStatus.Healthy;
                break;
            case "mismatched-marriage-reason":
                payload["outcome"]!["marriageChanges"]!["reason"] =
                    (int)CharacterMarriageLifecycleReason.CharacterDied;
                break;
            case "affected-ids":
                campaignEvent["affectedIds"] = new JsonArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(save));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CampaignSimulation CreateCurrentSimulation()
    {
        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(
            ReadFixture());
        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        _ = simulation.ResolveTurn();
        return simulation;
    }

    private static CharacterConditionState CurrentCondition(
        CampaignSimulation simulation,
        EntityId characterId)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile));
        return profile.Condition;
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, CanonicalJson.Options);

    private static JsonObject ReadFixture() =>
        JsonNode.Parse(File.ReadAllBytes(FixturePath()))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-28-f9-history-backed.json");
}
