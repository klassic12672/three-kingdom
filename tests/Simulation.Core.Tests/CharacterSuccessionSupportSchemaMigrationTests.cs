using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionSupportSchemaMigrationTests
{
    private const int FrozenLength = 23_266;
    private const string FrozenFileSha256 =
        "7292ba5554ced7bb98515ed7a99768121309966510e1225ab29f2c2f95920c7b";
    private const string FrozenChecksum =
        "ac324073ec1dbfe151894083064f14e1fc64864a34f4dc7db6710ce4d8951f8d";
    private const string FrozenSourceRevision =
        "62e03b6e7d4a3965de4c97ed9b92e03b6d40fbe4";
    private static readonly EntityId Subject =
        new("character:fixture/f3-captive");
    private static readonly EntityId Supporter =
        new("character:fixture/f3-replacement");
    private static readonly EntityId FirstCandidate =
        new("character:fixture/f3-detained");
    private static readonly EntityId SecondCandidate =
        new("character:fixture/f3-hostage");

    [Fact]
    public void F812_ExactF7Schema26AuthenticatesMigratesMinimallyAndContinues()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(
            FrozenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject source = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(
            $"sp04f7@{FrozenSourceRevision}",
            source["gameVersion"]!.GetValue<string>());
        Assert.Equal(26, source["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, source["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, 26);
        JsonObject original = (JsonObject)source.DeepClone();
        WorldSnapshot historical =
            SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
                source["snapshot"]!.AsObject(),
                26);
        Assert.Equal(
            FrozenChecksum,
            SimulationChecksum.ComputeForSaveSchema(historical, 26).Value);

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(source);

        Assert.True(JsonNode.DeepEquals(original, source));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        Assert.Equal(28, migrated["schemaVersion"]!.GetValue<int>());
        JsonObject beforeSnapshot = original["snapshot"]!.AsObject();
        JsonObject afterSnapshot = migrated["snapshot"]!.AsObject();
        AssertUnchangedSnapshotProperties(beforeSnapshot, afterSnapshot);
        JsonObject beforeSuccession = beforeSnapshot["characterSuccessions"]!.AsObject();
        JsonObject afterSuccession = afterSnapshot["characterSuccessions"]!.AsObject();
        Assert.Equal(2, beforeSuccession["contractVersion"]!.GetValue<int>());
        Assert.Equal(4, afterSuccession["contractVersion"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(
            beforeSuccession["designations"],
            afterSuccession["designations"]));
        Assert.True(JsonNode.DeepEquals(
            beforeSuccession["history"],
            afterSuccession["history"]));
        Assert.True(JsonNode.DeepEquals(
            beforeSuccession["claims"],
            afterSuccession["claims"]));
        Assert.True(JsonNode.DeepEquals(
            beforeSuccession["claimHistory"],
            afterSuccession["claimHistory"]));
        Assert.Empty(afterSuccession["supports"]!.AsArray());
        Assert.Empty(afterSuccession["supportHistory"]!.AsArray());
        Assert.Empty(afterSuccession["resolutions"]!.AsArray());
        Assert.Null(afterSuccession["campaignContinuity"]);
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticCommands"],
            migrated["diagnosticCommands"]));
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticEvents"],
            migrated["diagnosticEvents"]));
        Assert.Equal(2, SuccessionSystemVersion(beforeSnapshot));
        Assert.Equal(4, SuccessionSystemVersion(afterSnapshot));

        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            CanonicalJson.Options)!;
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);

        SuccessionSupportState declared = DeclareSupport(
            simulation,
            "post-migration-declare",
            FirstCandidate,
            null);
        SuccessionSupportState replacement = DeclareSupport(
            simulation,
            "post-migration-replace",
            SecondCandidate,
            declared.SupportId);
        CampaignCommand withdrawal = CampaignCommand.Create(
            new EntityId("command:fixture/f8-post-migration-withdraw"),
            Supporter,
            simulation.World.Calendar.Date,
            new CharacterSuccessionSupportActionCommandPayload(
                new WithdrawSuccessionSupportAction(
                    Subject,
                    replacement.SupportId)));
        Assert.True(simulation.Submit(withdrawal).IsValid);
        Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        Assert.False(simulation.World.CharacterSuccessions.TryGetCurrentSupport(
            Subject,
            Supporter,
            out _));
    }

    [Theory]
    [InlineData("character_succession_support_action.v1")]
    [InlineData("character_succession_support_action_resolved.v1")]
    [InlineData("declare_succession_support.v1")]
    [InlineData("withdraw_succession_support.v1")]
    [InlineData("succession_support_declared.v1")]
    [InlineData("succession_support_replaced.v1")]
    [InlineData("succession_support_withdrawn.v1")]
    public void F813_Schema26RejectsEveryF8Discriminator(string discriminator)
    {
        JsonObject source = ReadFixture();
        source["f8Probe"] = new JsonObject { ["$type"] = discriminator };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
    }

    [Theory]
    [InlineData("supports")]
    [InlineData("supportHistory")]
    [InlineData("supportId")]
    [InlineData("supporterId")]
    [InlineData("supportedCandidateId")]
    [InlineData("expectedCurrentSupportId")]
    [InlineData("currentSupport")]
    [InlineData("previousSupport")]
    [InlineData("declaredDate")]
    [InlineData("declaredTurnIndex")]
    public void F813_Schema26RejectsEveryUniqueF8PropertyIncludingNull(
        string property)
    {
        JsonObject source = ReadFixture();
        source["f8Probe"] = new JsonObject { [property] = null };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
    }

    [Fact]
    public void F813_Schema26RejectsSupportArraysAndFutureSuccessionVersions()
    {
        foreach (string scenario in new[]
                 {
                     "supports",
                     "support-history",
                     "snapshot-version",
                     "system-version",
                 })
        {
            JsonObject source = ReadFixture();
            JsonObject snapshot = source["snapshot"]!.AsObject();
            JsonObject succession = snapshot["characterSuccessions"]!.AsObject();
            switch (scenario)
            {
                case "supports":
                    succession["supports"] = null;
                    break;
                case "support-history":
                    succession["supportHistory"] = null;
                    break;
                case "snapshot-version":
                    succession["contractVersion"] = 3;
                    break;
                case "system-version":
                    SuccessionSystem(snapshot)["version"] = 3;
                    break;
            }

            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(source));
        }
    }

    [Fact]
    public void F814_CurrentSchemaRejectsMissingMalformedAndCrossPairedSupportData()
    {
        foreach (string scenario in new[]
                 {
                     "missing-supports",
                     "null-history",
                     "old-system",
                     "terminal-without-evidence",
                     "cross-pair",
                     "affected-ids",
                 })
        {
            JsonObject current = CurrentSupportSave();
            JsonObject snapshot = current["snapshot"]!.AsObject();
            JsonObject succession = snapshot["characterSuccessions"]!.AsObject();
            switch (scenario)
            {
                case "missing-supports":
                    succession.Remove("supports");
                    break;
                case "null-history":
                    succession["supportHistory"] = null;
                    break;
                case "old-system":
                    SuccessionSystem(snapshot)["version"] = 2;
                    break;
                case "terminal-without-evidence":
                    succession["supports"]![0]!["status"] =
                        (int)SuccessionSupportStatus.Withdrawn;
                    break;
                case "cross-pair":
                    CurrentSupportEvent(current)["action"]!["supportedCandidateId"] =
                        SerializeNode(SecondCandidate);
                    break;
                case "affected-ids":
                    CurrentSupportCampaignEvent(current)["affectedIds"] =
                        new JsonArray();
                    break;
            }

            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(current));
        }
    }

    [Theory]
    [InlineData("source-command")]
    [InlineData("source-date")]
    public void F814_CurrentSchemaRejectsCoherentSupportDiagnosticsNotBoundToOuterEvent(
        string scenario)
    {
        JsonObject current = CurrentSupportSave();
        JsonObject payload = CurrentSupportEvent(current);
        JsonObject support = payload["outcome"]!["currentSupport"]!.AsObject();
        EntityId subjectId = support["subjectId"]!
            .Deserialize<EntityId>(CanonicalJson.Options);
        EntityId supporterId = support["supporterId"]!
            .Deserialize<EntityId>(CanonicalJson.Options);
        EntityId candidateId = support["supportedCandidateId"]!
            .Deserialize<EntityId>(CanonicalJson.Options);
        EntityId commandId = scenario == "source-command"
            ? new EntityId("command:test/f8-forged-support")
            : support["sourceCommandId"]!.Deserialize<EntityId>(
                CanonicalJson.Options);
        CampaignDate date = scenario == "source-date"
            ? new CampaignDate(201, 1, 1)
            : support["declaredDate"]!.Deserialize<CampaignDate>(
                CanonicalJson.Options);
        EntityId eventId = CharacterSuccessionIds.DeriveSupportActionEventId(
            date,
            commandId);
        support["declaredDate"] = SerializeNode(date);
        support["sourceCommandId"] = SerializeNode(commandId);
        support["sourceEventId"] = SerializeNode(eventId);
        support["supportId"] = SerializeNode(CharacterSuccessionIds.DeriveSupportId(
            eventId,
            subjectId,
            supporterId,
            candidateId));

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Theory]
    [InlineData("replacement", "event-id")]
    [InlineData("replacement", "affected-ids")]
    [InlineData("withdrawal", "event-id")]
    [InlineData("withdrawal", "affected-ids")]
    public void F814_ReplacementAndWithdrawalDiagnosticsRequireExactOuterBinding(
        string outcome,
        string tamper)
    {
        JsonObject current = CurrentSupportSave(outcome);
        _ = new SaveSchemaRegistry().MigrateToCurrent(
            (JsonObject)current.DeepClone());
        JsonObject campaignEvent = CurrentSupportCampaignEvent(current);
        if (tamper == "event-id")
        {
            campaignEvent["eventId"] = SerializeNode(
                new EntityId($"event:test/f8-forged-{outcome}"));
        }
        else
        {
            campaignEvent["affectedIds"] = new JsonArray();
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    private static SuccessionSupportState DeclareSupport(
        CampaignSimulation simulation,
        string suffix,
        EntityId candidateId,
        EntityId? expectedCurrentSupportId)
    {
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:fixture/f8-{suffix}"),
            Supporter,
            simulation.World.Calendar.Date,
            new CharacterSuccessionSupportActionCommandPayload(
                new DeclareSuccessionSupportAction(
                    Subject,
                    candidateId,
                    expectedCurrentSupportId)));
        Assert.True(simulation.Submit(command).IsValid);
        CharacterSuccessionSupportActionResolvedEventPayload payload = Assert.IsType<
            CharacterSuccessionSupportActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        return payload.Outcome switch
        {
            SuccessionSupportDeclaredOutcome declared => declared.CurrentSupport,
            SuccessionSupportReplacedOutcome replaced => replaced.CurrentSupport,
            _ => throw new InvalidOperationException(),
        };
    }

    private static JsonObject CurrentSupportSave(string outcome = "declaration")
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(CanonicalJson.Options)!;
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        _ = simulation.ResolveTurn();
        SuccessionSupportState current = DeclareSupport(
            simulation,
            "current-shape",
            FirstCandidate,
            null);
        if (outcome == "replacement")
        {
            _ = DeclareSupport(
                simulation,
                "current-shape-replacement",
                SecondCandidate,
                current.SupportId);
        }
        else if (outcome == "withdrawal")
        {
            CampaignCommand withdrawal = CampaignCommand.Create(
                new EntityId("command:fixture/f8-current-shape-withdrawal"),
                Supporter,
                simulation.World.Calendar.Date,
                new CharacterSuccessionSupportActionCommandPayload(
                    new WithdrawSuccessionSupportAction(
                        Subject,
                        current.SupportId)));
            Assert.True(simulation.Submit(withdrawal).IsValid);
            Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload);
        }
        else
        {
            Assert.Equal("declaration", outcome);
        }

        return JsonSerializer.SerializeToNode(
            SaveEnvelope.Create("test", [], simulation),
            CanonicalJson.Options)!.AsObject();
    }

    private static JsonObject CurrentSupportCampaignEvent(JsonObject source) =>
        source["diagnosticEvents"]!.AsArray()
            .Select(item => item!.AsObject())
            .Last(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_succession_support_action_resolved.v1");

    private static JsonObject CurrentSupportEvent(JsonObject source) =>
        CurrentSupportCampaignEvent(source)["payload"]!.AsObject();

    private static JsonNode SerializeNode<T>(T value) =>
        JsonSerializer.SerializeToNode(value, CanonicalJson.Options)!;

    private static void AssertUnchangedSnapshotProperties(
        JsonObject before,
        JsonObject after)
    {
        foreach ((string property, JsonNode? value) in before)
        {
            if (property is "characterSuccessions" or "systemVersions")
            {
                continue;
            }

            Assert.True(
                JsonNode.DeepEquals(value, after[property]),
                $"Snapshot property '{property}' changed during schema 26 to 27 migration.");
        }
    }

    private static int SuccessionSystemVersion(JsonObject snapshot) =>
        SuccessionSystem(snapshot)["version"]!.GetValue<int>();

    private static JsonObject SuccessionSystem(JsonObject snapshot) =>
        snapshot["systemVersions"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["systemId"]!.GetValue<string>()
                == CharacterSuccessionSystem.SystemId);

    private static JsonObject ReadFixture() => JsonNode.Parse(
        File.ReadAllBytes(FixturePath()))!.AsObject();

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-26-f7-history-backed.json");
}
