using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionSchemaMigrationTests
{
    private const int FrozenLength = 15_228;
    private const string FrozenFileSha256 =
        "7f6ebd4b11ce0f5d9bd2ad8154a32750090bb235a35808565fe3c7df6daec10c";
    private const string FrozenChecksum =
        "adf2a49ac7aca33d0ca1a794724b06bdcae8cbd144c19aaafcbef5e8a2fc607f";
    private static readonly EntityId Designator =
        new("character:fixture/f3-replacement");
    private static readonly EntityId Heir = new("character:fixture/f3-captive");
    private static readonly EntityId AlternateHeir =
        new("character:fixture/f3-detained");

    [Fact]
    public void F411_ExactF3Schema24AuthenticatesMigratesStructurallyAndContinues()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(FrozenFileSha256, Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject source = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(24, source["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, source["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, 24);
        JsonObject original = (JsonObject)source.DeepClone();

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(source);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion,
            migrated["schemaVersion"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(original, source));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        JsonObject migratedSnapshot = migrated["snapshot"]!.AsObject();
        JsonObject succession = migratedSnapshot["characterSuccessions"]!.AsObject();
        Assert.Equal(CharacterSuccessionContractVersions.Snapshot,
            succession["contractVersion"]!.GetValue<int>());
        Assert.Empty(succession["designations"]!.AsArray());
        Assert.Empty(succession["history"]!.AsArray());
        Assert.Empty(succession["claims"]!.AsArray());
        Assert.Empty(succession["claimHistory"]!.AsArray());
        Assert.Empty(succession["supports"]!.AsArray());
        Assert.Empty(succession["supportHistory"]!.AsArray());
        Assert.Empty(succession["resolutions"]!.AsArray());
        Assert.Null(succession["campaignContinuity"]);
        Assert.Single(migratedSnapshot["systemVersions"]!.AsArray(), node =>
            node!["systemId"]!.GetValue<string>() == CharacterSuccessionSystem.SystemId
            && node["version"]!.GetValue<int>() == CharacterSuccessionSystem.Version);
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticCommands"],
            migrated["diagnosticCommands"]));
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticEvents"],
            migrated["diagnosticEvents"]));
        Assert.Contains(
            migrated["diagnosticEvents"]!.AsArray(),
            node => node!["payload"]?["outcome"]?["$type"]?.GetValue<string>()
                == "household_head_death_resolved.v1");

        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        Assert.Equal(
            FrozenChecksum,
            SimulationChecksum.ComputeForSaveSchema(envelope.Snapshot, 24).Value);
        Assert.Equal(
            Designator,
            envelope.Snapshot.Characters.HouseholdStates.Single().HeadCharacterId);
        Assert.Equal(
            new EntityId("character:fixture/f3-head"),
            Assert.Single(envelope.Snapshot.CharacterResources.Accounts).CharacterId);
        Assert.Equal(
            new EntityId("character:fixture/f3-head"),
            Assert.Single(envelope.Snapshot.CharacterEstateHoldings.Holdings).OwnerCharacterId);

        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:fixture/f4-after-migration"),
            Designator,
            simulation.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(Heir, null)));
        Assert.True(simulation.Submit(command).IsValid);
        HeirDesignatedOutcome outcome = Assert.IsType<HeirDesignatedOutcome>(Assert.IsType<
            CharacterSuccessionActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        Assert.Equal(Heir, outcome.CurrentDesignation.HeirCharacterId);
        Assert.Equal(
            new EntityId("character:fixture/f3-head"),
            Assert.Single(simulation.World.CharacterResources.Accounts).CharacterId);
        Assert.Equal(
            new EntityId("character:fixture/f3-head"),
            Assert.Single(simulation.World.CharacterEstateHoldings.Holdings).OwnerCharacterId);
    }

    [Fact]
    public void F411_CorruptSchema24ChecksumRejectsWithoutMutation()
    {
        JsonObject source = ReadFixture();
        source["checksum"] = new string('0', 64);
        JsonObject tampered = (JsonObject)source.DeepClone();

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));

        Assert.True(JsonNode.DeepEquals(tampered, source));
    }

    [Theory]
    [InlineData("snapshot-property")]
    [InlineData("system-version")]
    [InlineData("command-discriminator")]
    [InlineData("event-discriminator")]
    [InlineData("action-discriminator")]
    [InlineData("outcome-discriminator")]
    [InlineData("expected-current-null")]
    [InlineData("current-designation-null")]
    [InlineData("previous-designation-null")]
    [InlineData("designation-id-null")]
    [InlineData("designator-id-null")]
    [InlineData("heir-id-null")]
    [InlineData("folded-replaced-null")]
    [InlineData("folded-revoked-null")]
    [InlineData("designations-null")]
    [InlineData("designations-empty")]
    [InlineData("resolution-event-null")]
    public void F412_Schema24RejectsEveryIsolatedF4InjectionWithoutMutation(
        string mutation)
    {
        JsonObject source = ReadFixture();
        JsonObject snapshot = source["snapshot"]!.AsObject();
        switch (mutation)
        {
            case "snapshot-property":
                snapshot["characterSuccessions"] = null;
                break;
            case "system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterSuccessionSystem.SystemId,
                    ["version"] = CharacterSuccessionSystem.Version,
                });
                break;
            case "command-discriminator":
                source["$type"] = "character_succession_action.v1";
                break;
            case "event-discriminator":
                source["$type"] = "character_succession_action_resolved.v1";
                break;
            case "action-discriminator":
                source["$type"] = "designate_heir.v1";
                break;
            case "outcome-discriminator":
                source["$type"] = "heir_designated.v1";
                break;
            case "expected-current-null":
                source["expectedCurrentDesignationId"] = null;
                break;
            case "current-designation-null":
                source["currentDesignation"] = null;
                break;
            case "previous-designation-null":
                source["previousDesignation"] = null;
                break;
            case "designation-id-null":
                source["designationId"] = null;
                break;
            case "designator-id-null":
                source["designatorCharacterId"] = null;
                break;
            case "heir-id-null":
                source["heirCharacterId"] = null;
                break;
            case "folded-replaced-null":
                source["foldedReplacedCount"] = null;
                break;
            case "folded-revoked-null":
                source["foldedRevokedCount"] = null;
                break;
            case "designations-null":
                source["designations"] = null;
                break;
            case "designations-empty":
                source["designations"] = new JsonArray();
                break;
            case "resolution-event-null":
                source["resolutionEventId"] = null;
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
    [InlineData("missing-snapshot")]
    [InlineData("null-snapshot")]
    [InlineData("missing-system-version")]
    [InlineData("missing-action")]
    [InlineData("missing-outcome")]
    [InlineData("cross-pair-revoke")]
    [InlineData("missing-heir")]
    [InlineData("missing-expected-current")]
    [InlineData("missing-current-designation")]
    [InlineData("null-current-designation")]
    [InlineData("wrong-designation-version")]
    [InlineData("missing-resolution-field")]
    [InlineData("malformed-history")]
    [InlineData("missing-command-action")]
    public void F413_CurrentSchemaRejectsIncompleteOrMismatchedSuccessionData(
        string mutation)
    {
        JsonObject current = ResolvedCurrentSave();
        _ = new SaveSchemaRegistry().MigrateToCurrent(
            (JsonObject)current.DeepClone());
        JsonObject snapshot = current["snapshot"]!.AsObject();
        JsonObject payload = CurrentSuccessionPayload(current);
        JsonObject action = payload["action"]!.AsObject();
        JsonObject outcome = payload["outcome"]!.AsObject();
        JsonObject designation = outcome["currentDesignation"]!.AsObject();
        switch (mutation)
        {
            case "missing-snapshot":
                snapshot.Remove("characterSuccessions");
                break;
            case "null-snapshot":
                snapshot["characterSuccessions"] = null;
                break;
            case "missing-system-version":
                JsonNode systemVersion = snapshot["systemVersions"]!.AsArray()
                    .Single(node => node!["systemId"]!.GetValue<string>()
                        == CharacterSuccessionSystem.SystemId)!;
                snapshot["systemVersions"]!.AsArray().Remove(systemVersion);
                break;
            case "missing-action":
                payload.Remove("action");
                break;
            case "missing-outcome":
                payload.Remove("outcome");
                break;
            case "cross-pair-revoke":
                action["$type"] = "revoke_heir_designation.v1";
                action.Remove("heirCharacterId");
                action["expectedCurrentDesignationId"] = designation["designationId"]!.DeepClone();
                break;
            case "missing-heir":
                action.Remove("heirCharacterId");
                break;
            case "missing-expected-current":
                action.Remove("expectedCurrentDesignationId");
                break;
            case "missing-current-designation":
                outcome.Remove("currentDesignation");
                break;
            case "null-current-designation":
                outcome["currentDesignation"] = null;
                break;
            case "wrong-designation-version":
                designation["contractVersion"] = 2;
                break;
            case "missing-resolution-field":
                designation.Remove("resolutionEventId");
                break;
            case "malformed-history":
                snapshot["characterSuccessions"]!["history"]!.AsArray().Add(
                    new JsonObject());
                break;
            case "missing-command-action":
                CurrentSuccessionCommandPayload(current).Remove("action");
                break;
            default:
                throw new InvalidOperationException();
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Theory]
    [InlineData("designated-expected-current")]
    [InlineData("designated-actor-mismatch")]
    [InlineData("designated-heir-mismatch")]
    [InlineData("designated-wrong-status")]
    [InlineData("replacement-expected-mismatch")]
    [InlineData("replacement-no-op")]
    [InlineData("replacement-causality")]
    [InlineData("replacement-wrong-status")]
    [InlineData("replacement-terminal-source-reused")]
    [InlineData("replacement-same-designation-id")]
    [InlineData("revocation-expected-mismatch")]
    [InlineData("revocation-wrong-status")]
    public void F413_CurrentSchemaRejectsImpossibleSuccessionDiagnosticSemantics(
        string mutation)
    {
        string lifecycle = mutation.StartsWith("replacement", StringComparison.Ordinal)
            ? "replacement"
            : mutation.StartsWith("revocation", StringComparison.Ordinal)
                ? "revocation"
                : "designated";
        JsonObject current = ResolvedCurrentSave(lifecycle);
        _ = new SaveSchemaRegistry().MigrateToCurrent(
            (JsonObject)current.DeepClone());
        JsonObject payload = CurrentSuccessionPayload(current);
        JsonObject action = payload["action"]!.AsObject();
        JsonObject outcome = payload["outcome"]!.AsObject();
        switch (mutation)
        {
            case "designated-expected-current":
                action["expectedCurrentDesignationId"] =
                    outcome["currentDesignation"]!["designationId"]!.DeepClone();
                break;
            case "designated-actor-mismatch":
                payload["actingCharacterId"] = action["heirCharacterId"]!.DeepClone();
                break;
            case "designated-heir-mismatch":
                action["heirCharacterId"] = JsonSerializer.SerializeToNode(
                    AlternateHeir,
                    SimulationJson.CreateOptions());
                break;
            case "designated-wrong-status":
                outcome["currentDesignation"]!["status"] =
                    (int)HeirDesignationStatus.Revoked;
                break;
            case "replacement-expected-mismatch":
                action["expectedCurrentDesignationId"] =
                    outcome["currentDesignation"]!["designationId"]!.DeepClone();
                break;
            case "replacement-no-op":
                outcome["previousDesignation"]!["heirCharacterId"] =
                    outcome["currentDesignation"]!["heirCharacterId"]!.DeepClone();
                break;
            case "replacement-causality":
                outcome["currentDesignation"]!["sourceEventId"] =
                    outcome["previousDesignation"]!["sourceEventId"]!.DeepClone();
                break;
            case "replacement-wrong-status":
                outcome["previousDesignation"]!["status"] =
                    (int)HeirDesignationStatus.Revoked;
                break;
            case "replacement-terminal-source-reused":
                outcome["previousDesignation"]!["resolutionEventId"] =
                    outcome["previousDesignation"]!["sourceEventId"]!.DeepClone();
                break;
            case "replacement-same-designation-id":
                outcome["currentDesignation"]!["designationId"] =
                    outcome["previousDesignation"]!["designationId"]!.DeepClone();
                break;
            case "revocation-expected-mismatch":
                action["expectedCurrentDesignationId"] =
                    outcome["previousDesignation"]!["sourceCommandId"]!.DeepClone();
                break;
            case "revocation-wrong-status":
                outcome["previousDesignation"]!["status"] =
                    (int)HeirDesignationStatus.Replaced;
                break;
            default:
                throw new InvalidOperationException();
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Fact]
    public void F410_CurrentPendingAndResolvedSavesRoundTripAndReplayExactly()
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(SimulationJson.CreateOptions())!;
        CampaignSimulation pendingSimulation = new(WorldState.Restore(migrated.Snapshot));
        CampaignDate scheduledDate = pendingSimulation.World.Calendar.Date.AddDays(2);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:fixture/f4-pending"),
            Designator,
            scheduledDate,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(Heir, null)));
        Assert.True(pendingSimulation.Submit(command).IsValid);
        SaveEnvelope pending = SaveStoreRoundTrip(
            SaveEnvelope.Create("test", [], pendingSimulation),
            "pending");
        Assert.Single(pending.Snapshot.PendingCommands);
        Assert.Empty(pending.Snapshot.CharacterSuccessions.Designations);

        CampaignSimulation first = new(WorldState.Restore(pending.Snapshot));
        CampaignSimulation second = new(WorldState.Restore(pending.Snapshot));
        string firstEvents = Serialize(first.ResolveTurn());
        string secondEvents = Serialize(second.ResolveTurn());

        Assert.Equal(firstEvents, secondEvents);
        Assert.Equal(
            SimulationChecksum.Compute(first.World.CaptureSnapshot()),
            SimulationChecksum.Compute(second.World.CaptureSnapshot()));
        Assert.True(first.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? designation));
        Assert.Equal(scheduledDate, designation.EstablishedDate);
        SaveEnvelope resolved = SaveStoreRoundTrip(
            SaveEnvelope.Create("test", [], first),
            "resolved");
        Assert.Equal(
            SimulationChecksum.Compute(first.World.CaptureSnapshot()),
            SimulationChecksum.Compute(resolved.Snapshot));
        Assert.Single(resolved.Snapshot.CharacterSuccessions.Designations);
    }

    private static JsonObject ResolvedCurrentSave(string lifecycle = "designated")
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(SimulationJson.CreateOptions())!;
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:fixture/f4-current-shape"),
            Designator,
            simulation.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(Heir, null)));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());
        HeirDesignationState current = Assert.Single(
            simulation.World.CharacterSuccessions.Designations);
        if (lifecycle is "replacement" or "revocation")
        {
            ICharacterSuccessionAction action = lifecycle == "replacement"
                ? new DesignateHeirAction(AlternateHeir, current.DesignationId)
                : new RevokeHeirDesignationAction(current.DesignationId);
            CampaignCommand terminal = CampaignCommand.Create(
                new EntityId($"command:fixture/f4-current-{lifecycle}"),
                Designator,
                simulation.World.Calendar.Date,
                new CharacterSuccessionActionCommandPayload(action));
            Assert.True(simulation.Submit(terminal).IsValid);
            Assert.Single(simulation.ResolveTurn());
        }

        return JsonNode.Parse(Serialize(SaveEnvelope.Create("test", [], simulation)))!
            .AsObject();
    }

    private static JsonObject CurrentSuccessionPayload(JsonObject source) =>
        source["diagnosticEvents"]!.AsArray().Select(item => item!.AsObject())
            .Last(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_succession_action_resolved.v1")["payload"]!.AsObject();

    private static JsonObject CurrentSuccessionCommandPayload(JsonObject source) =>
        source["diagnosticCommands"]!.AsArray().Select(item => item!.AsObject())
            .Last(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_succession_action.v1")["payload"]!.AsObject();

    private static SaveEnvelope RoundTrip(SaveEnvelope envelope) =>
        JsonSerializer.Deserialize<SaveEnvelope>(
            Serialize(envelope),
            SimulationJson.CreateOptions())!;

    private static SaveEnvelope SaveStoreRoundTrip(
        SaveEnvelope envelope,
        string suffix)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f4-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, $"{suffix}.save.gz");
            SaveStore store = new();
            store.SaveAtomic(path, envelope);
            Assert.True(new FileInfo(path).Length > 0);
            return store.Load(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonObject ReadFixture() => JsonNode.Parse(
        File.ReadAllBytes(FixturePath()))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-24-f3-history-backed.json");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());
}
