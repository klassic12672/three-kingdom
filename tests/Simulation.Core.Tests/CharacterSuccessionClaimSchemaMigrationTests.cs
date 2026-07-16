using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionClaimSchemaMigrationTests
{
    private const int FrozenLength = 21_419;
    private const string FrozenFileSha256 =
        "ad85ff638f72b743fb9033c64642a49f7157cfd7e3206241813822f8ef9af6c1";
    private const string FrozenChecksum =
        "7042138074e069633eda94539f22b0759bcb462383f4e12bcdb43545e3c37bec";
    private const string FrozenSourceRevision =
        "7e96930d103081e981c0cf1a06736223e222bc07";
    private static readonly EntityId Claimant =
        new("character:fixture/f3-replacement");
    private static readonly EntityId Subject = new("character:fixture/f3-captive");

    [Fact]
    public void F711_ExactF6Schema25AuthenticatesMigratesMinimallyAndContinues()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(FrozenFileSha256, Convert.ToHexStringLower(
            SHA256.HashData(sourceBytes)));
        JsonObject source = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(
            $"sp04f6@{FrozenSourceRevision}",
            source["gameVersion"]!.GetValue<string>());
        Assert.Equal(25, source["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, source["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, 25);
        JsonObject original = (JsonObject)source.DeepClone();
        WorldSnapshot historical = SaveSchemaRegistry
            .DeserializeHistoricalSnapshotForChecksum(
                source["snapshot"]!.AsObject(),
                25);
        Assert.Equal(
            FrozenChecksum,
            SimulationChecksum.ComputeForSaveSchema(historical, 25).Value);

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(source);

        Assert.True(JsonNode.DeepEquals(original, source));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        Assert.Equal(SaveEnvelope.CurrentSchemaVersion,
            migrated["schemaVersion"]!.GetValue<int>());
        JsonObject beforeSnapshot = original["snapshot"]!.AsObject();
        JsonObject afterSnapshot = migrated["snapshot"]!.AsObject();
        AssertUnchangedSnapshotProperties(beforeSnapshot, afterSnapshot);
        JsonObject beforeSuccession = beforeSnapshot["characterSuccessions"]!.AsObject();
        JsonObject afterSuccession = afterSnapshot["characterSuccessions"]!.AsObject();
        Assert.Equal(1, beforeSuccession["contractVersion"]!.GetValue<int>());
        Assert.Equal(4, afterSuccession["contractVersion"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(
            beforeSuccession["designations"],
            afterSuccession["designations"]));
        Assert.True(JsonNode.DeepEquals(
            beforeSuccession["history"],
            afterSuccession["history"]));
        Assert.Empty(afterSuccession["claims"]!.AsArray());
        Assert.Empty(afterSuccession["claimHistory"]!.AsArray());
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
        Assert.Equal(1, SuccessionSystemVersion(beforeSnapshot));
        Assert.Equal(4, SuccessionSystemVersion(afterSnapshot));

        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            CanonicalJson.Options)!;
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        CampaignEvent pendingReplacement = Assert.Single(simulation.ResolveTurn());
        Assert.IsType<CharacterSuccessionActionResolvedEventPayload>(
            pendingReplacement.Payload);

        SuccessionClaimState asserted = AssertClaim(
            simulation,
            "post-migration-assert");
        CampaignCommand withdrawal = CampaignCommand.Create(
            new EntityId("command:fixture/f7-post-migration-withdraw"),
            Claimant,
            simulation.World.Calendar.Date,
            new CharacterSuccessionClaimActionCommandPayload(
                new WithdrawSuccessionClaimAction(Subject, asserted.ClaimId)));
        Assert.True(simulation.Submit(withdrawal).IsValid);
        Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        Assert.False(simulation.World.CharacterSuccessions.TryGetActiveClaim(
            Subject,
            Claimant,
            out _));
        Assert.Equal(
            SuccessionClaimStatus.Withdrawn,
            Assert.Single(simulation.World.CharacterSuccessions
                .GetRecentClaimRecordsForSubject(Subject)).Status);
    }

    [Theory]
    [InlineData("character_succession_claim_action.v1")]
    [InlineData("character_succession_claim_action_resolved.v1")]
    [InlineData("assert_succession_claim.v1")]
    [InlineData("withdraw_succession_claim.v1")]
    [InlineData("succession_claim_asserted.v1")]
    [InlineData("succession_claim_withdrawn.v1")]
    public void F712_Schema25RejectsEveryF7Discriminator(string discriminator)
    {
        JsonObject source = ReadFixture();
        source["f7Probe"] = new JsonObject { ["$type"] = discriminator };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
    }

    [Theory]
    [InlineData("claimHistory")]
    [InlineData("claimId")]
    [InlineData("claimantCharacterId")]
    [InlineData("expectedCurrentClaimId")]
    [InlineData("currentClaim")]
    [InlineData("previousClaim")]
    [InlineData("assertedDate")]
    [InlineData("assertedTurnIndex")]
    [InlineData("withdrawalDate")]
    [InlineData("withdrawalTurnIndex")]
    [InlineData("withdrawalCommandId")]
    [InlineData("withdrawalEventId")]
    [InlineData("foldedWithdrawnCount")]
    public void F712_Schema25RejectsEveryUniqueF7PropertyIncludingNull(
        string property)
    {
        JsonObject source = ReadFixture();
        source["f7Probe"] = new JsonObject { [property] = null };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
    }

    [Fact]
    public void F712_Schema25RejectsClaimArraysAndFutureSuccessionVersions()
    {
        foreach (string scenario in new[]
                 {
                     "claims",
                     "claim-history",
                     "snapshot-version",
                     "system-version",
                 })
        {
            JsonObject source = ReadFixture();
            JsonObject snapshot = source["snapshot"]!.AsObject();
            JsonObject succession = snapshot["characterSuccessions"]!.AsObject();
            switch (scenario)
            {
                case "claims":
                    succession["claims"] = null;
                    break;
                case "claim-history":
                    succession["claimHistory"] = null;
                    break;
                case "snapshot-version":
                    succession["contractVersion"] = 2;
                    break;
                case "system-version":
                    SuccessionSystem(snapshot)["version"] = 2;
                    break;
            }

            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(source));
        }
    }

    [Fact]
    public void F713_CurrentSchemaRejectsMissingMalformedAndCrossPairedClaimData()
    {
        foreach (string scenario in new[]
                 {
                     "missing-claims",
                     "null-history",
                     "old-system",
                     "withdrawn-without-evidence",
                     "cross-pair",
                 })
        {
            JsonObject current = CurrentClaimSave();
            JsonObject snapshot = current["snapshot"]!.AsObject();
            JsonObject succession = snapshot["characterSuccessions"]!.AsObject();
            switch (scenario)
            {
                case "missing-claims":
                    succession.Remove("claims");
                    break;
                case "null-history":
                    succession["claimHistory"] = null;
                    break;
                case "old-system":
                    SuccessionSystem(snapshot)["version"] = 1;
                    break;
                case "withdrawn-without-evidence":
                    succession["claims"]![0]!["status"] =
                        (int)SuccessionClaimStatus.Withdrawn;
                    break;
                case "cross-pair":
                    JsonObject payload = CurrentClaimEventPayload(current);
                    payload["action"]!["$type"] = "withdraw_succession_claim.v1";
                    payload["action"]!["expectedCurrentClaimId"] =
                        payload["outcome"]!["currentClaim"]!["claimId"]!.DeepClone();
                    break;
            }

            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(current));
        }
    }

    [Theory]
    [InlineData("assertion-command")]
    [InlineData("assertion-date")]
    [InlineData("withdrawal-command")]
    [InlineData("withdrawal-date")]
    public void F713_CurrentSchemaRejectsInternallyCoherentClaimDiagnosticsNotBoundToOuterEvent(
        string scenario)
    {
        bool withdrawal = scenario.StartsWith("withdrawal", StringComparison.Ordinal);
        JsonObject current = CurrentClaimSave(withdrawal);
        JsonObject payload = CurrentClaimEventPayload(current);
        JsonObject claim = payload["outcome"]![withdrawal
            ? "previousClaim"
            : "currentClaim"]!.AsObject();
        EntityId subjectCharacterId = claim["subjectCharacterId"]!
            .Deserialize<EntityId>(CanonicalJson.Options);
        EntityId claimantCharacterId = claim["claimantCharacterId"]!
            .Deserialize<EntityId>(CanonicalJson.Options);
        if (!withdrawal)
        {
            EntityId commandId = scenario == "assertion-command"
                ? new EntityId("command:test/f7-forged-assertion")
                : claim["sourceCommandId"]!.Deserialize<EntityId>(CanonicalJson.Options);
            CampaignDate date = scenario == "assertion-date"
                ? new CampaignDate(201, 1, 1)
                : claim["assertedDate"]!.Deserialize<CampaignDate>(CanonicalJson.Options);
            EntityId eventId = CharacterSuccessionIds.DeriveClaimActionEventId(
                date,
                commandId);
            claim["assertedDate"] = SerializeNode(date);
            claim["sourceCommandId"] = SerializeNode(commandId);
            claim["sourceEventId"] = SerializeNode(eventId);
            claim["claimId"] = SerializeNode(CharacterSuccessionIds.DeriveClaimId(
                eventId,
                subjectCharacterId,
                claimantCharacterId));
        }
        else
        {
            EntityId commandId = scenario == "withdrawal-command"
                ? new EntityId("command:test/f7-forged-withdrawal")
                : claim["withdrawalCommandId"]!.Deserialize<EntityId>(CanonicalJson.Options);
            CampaignDate date = scenario == "withdrawal-date"
                ? new CampaignDate(202, 1, 1)
                : claim["withdrawalDate"]!.Deserialize<CampaignDate>(CanonicalJson.Options);
            claim["withdrawalDate"] = SerializeNode(date);
            claim["withdrawalCommandId"] = SerializeNode(commandId);
            claim["withdrawalEventId"] = SerializeNode(
                CharacterSuccessionIds.DeriveClaimActionEventId(date, commandId));
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Fact]
    public void F713_CorruptSchema25ChecksumFailsBeforeMigrationAndPreservesSource()
    {
        JsonObject source = ReadFixture();
        source["checksum"] = new string('0', 64);
        JsonObject original = (JsonObject)source.DeepClone();

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
        Assert.True(JsonNode.DeepEquals(original, source));
    }

    private static SuccessionClaimState AssertClaim(
        CampaignSimulation simulation,
        string suffix)
    {
        CampaignCommand assertion = CampaignCommand.Create(
            new EntityId($"command:fixture/f7-{suffix}"),
            Claimant,
            simulation.World.Calendar.Date,
            new CharacterSuccessionClaimActionCommandPayload(
                new AssertSuccessionClaimAction(Subject)));
        Assert.True(simulation.Submit(assertion).IsValid);
        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        CharacterSuccessionClaimActionResolvedEventPayload payload = Assert.IsType<
            CharacterSuccessionClaimActionResolvedEventPayload>(
            Assert.Single(events, item =>
                item.Payload is CharacterSuccessionClaimActionResolvedEventPayload).Payload);
        return Assert.IsType<SuccessionClaimAssertedOutcome>(payload.Outcome).CurrentClaim;
    }

    private static JsonObject CurrentClaimSave(bool withdrawal = false)
    {
        SaveEnvelope migrated = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture())
            .Deserialize<SaveEnvelope>(CanonicalJson.Options)!;
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        SuccessionClaimState claim = AssertClaim(simulation, "current-shape");
        if (withdrawal)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId("command:fixture/f7-current-shape-withdraw"),
                Claimant,
                simulation.World.Calendar.Date,
                new CharacterSuccessionClaimActionCommandPayload(
                    new WithdrawSuccessionClaimAction(Subject, claim.ClaimId)));
            Assert.True(simulation.Submit(command).IsValid);
            Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload);
        }

        return JsonSerializer.SerializeToNode(
            SaveEnvelope.Create("test", [], simulation),
            CanonicalJson.Options)!.AsObject();
    }

    private static JsonNode SerializeNode<T>(T value) =>
        JsonSerializer.SerializeToNode(value, CanonicalJson.Options)!;

    private static JsonObject CurrentClaimEventPayload(JsonObject source) =>
        source["diagnosticEvents"]!.AsArray()
            .Select(item => item!.AsObject())
            .Last(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_succession_claim_action_resolved.v1")["payload"]!
            .AsObject();

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
                $"Snapshot property '{property}' changed during schema 25 to 26 migration.");
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
        "save-schema-25-f6-history-backed.json");
}
