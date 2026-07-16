using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionResolutionSchemaMigrationTests
{
    private const int FrozenLength = 14_957;
    private const string FrozenFileSha256 =
        "5b9b61b468d6b521f243182b359a1b16c7ab38c3698796bd694418e08a9b3913";
    private const string FrozenChecksum =
        "927a855b94dc56580166f94dce120707b5d17b8f24cc2e678f9052298dd2075c";
    private const string FrozenSourceRevision =
        "c946c8739d29e9f484bc921223e47cb5f24e38ab";
    private static readonly EntityId Subject =
        new("character:fixture/f8-subject");
    private static readonly EntityId Supporter =
        new("character:fixture/f8-supporter");
    private static readonly EntityId SchemaSubject =
        new("character:test/f9-schema-subject");
    private static readonly EntityId SchemaSuccessor =
        new("character:test/f9-schema-successor");
    private static readonly EntityId SchemaAdultRegent =
        new("character:test/f9-schema-adult-regent");
    private static readonly EntityId SchemaChildRegent =
        new("character:test/f9-schema-child-regent");

    [Fact]
    public void F912_ExactF8Schema27AuthenticatesMigratesMinimallyAndContinues()
    {
        string path = FixturePath();
        byte[] sourceBytes = File.ReadAllBytes(path);
        Assert.Equal(FrozenLength, sourceBytes.Length);
        Assert.Equal(
            FrozenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        JsonObject source = JsonNode.Parse(sourceBytes)!.AsObject();
        Assert.Equal(
            $"sp04f8@{FrozenSourceRevision}",
            source["gameVersion"]!.GetValue<string>());
        Assert.Equal(27, source["schemaVersion"]!.GetValue<int>());
        Assert.Equal(FrozenChecksum, source["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(source, 27);
        JsonObject original = (JsonObject)source.DeepClone();
        WorldSnapshot historical =
            SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
                source["snapshot"]!.AsObject(),
                27);
        Assert.Equal(
            FrozenChecksum,
            SimulationChecksum.ComputeForSaveSchema(historical, 27).Value);

        JsonObject migrated = new SaveSchemaRegistry().MigrateToCurrent(source);

        Assert.True(JsonNode.DeepEquals(original, source));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
        Assert.Equal(28, migrated["schemaVersion"]!.GetValue<int>());
        JsonObject beforeSnapshot = original["snapshot"]!.AsObject();
        JsonObject afterSnapshot = migrated["snapshot"]!.AsObject();
        AssertUnchangedSnapshotProperties(beforeSnapshot, afterSnapshot);
        JsonObject beforeSuccession = beforeSnapshot["characterSuccessions"]!.AsObject();
        JsonObject afterSuccession = afterSnapshot["characterSuccessions"]!.AsObject();
        Assert.Equal(3, beforeSuccession["contractVersion"]!.GetValue<int>());
        Assert.Equal(4, afterSuccession["contractVersion"]!.GetValue<int>());
        foreach (string property in new[]
                 {
                     "designations",
                     "history",
                     "claims",
                     "claimHistory",
                     "supports",
                     "supportHistory",
                 })
        {
            Assert.True(JsonNode.DeepEquals(
                beforeSuccession[property],
                afterSuccession[property]));
        }

        Assert.Empty(afterSuccession["resolutions"]!.AsArray());
        Assert.Null(afterSuccession["campaignContinuity"]);
        JsonObject history = afterSuccession["resolutionHistory"]!.AsObject();
        Assert.Equal(
            CharacterSuccessionContractVersions.ResolutionHistory,
            history["contractVersion"]!.GetValue<int>());
        Assert.Equal(0, history["foldedSelectedCount"]!.GetValue<long>());
        Assert.Equal(0, history["foldedDisputedCount"]!.GetValue<long>());
        Assert.Equal(0, history["foldedNoSuccessorCount"]!.GetValue<long>());
        Assert.Null(history["earliestDate"]);
        Assert.Null(history["latestDate"]);
        Assert.Equal(3, SuccessionSystemVersion(beforeSnapshot));
        Assert.Equal(4, SuccessionSystemVersion(afterSnapshot));
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticCommands"],
            migrated["diagnosticCommands"]));
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticEvents"],
            migrated["diagnosticEvents"]));

        SaveEnvelope envelope = migrated.Deserialize<SaveEnvelope>(
            CanonicalJson.Options)!;
        Assert.Equal(
            migrated["checksum"]!.GetValue<string>(),
            SimulationChecksum.Compute(envelope.Snapshot).Value);
        CampaignSimulation simulation = new(WorldState.Restore(envelope.Snapshot));
        Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        Assert.False(simulation.World.CharacterSuccessions.TryGetCurrentSupport(
            Subject,
            Supporter,
            out _));
        Assert.Empty(simulation.World.CharacterSuccessions.Resolutions);
        Assert.Null(simulation.World.CharacterSuccessions.CampaignContinuity);
    }

    [Theory]
    [InlineData("resolve_character_succession_death.v1")]
    [InlineData("character_succession_death_resolved.v1")]
    public void F913_Schema27RejectsEveryF9Discriminator(string discriminator)
    {
        JsonObject source = ReadFixture();
        source["f9Probe"] = new JsonObject { ["$type"] = discriminator };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
    }

    [Theory]
    [InlineData("expectedResolutionStateId")]
    [InlineData("resolutions")]
    [InlineData("resolutionHistory")]
    [InlineData("campaignContinuity")]
    [InlineData("succession")]
    [InlineData("selectedCandidate")]
    [InlineData("disputedCandidates")]
    [InlineData("candidateEligibility")]
    [InlineData("legalBasisPrecedence")]
    [InlineData("includesPrincipalSpouse")]
    [InlineData("allowedCollateralKinds")]
    [InlineData("maximumCollateralDistance")]
    [InlineData("candidateAge")]
    [InlineData("candidateCondition")]
    [InlineData("legalBases")]
    [InlineData("activeClaimId")]
    [InlineData("activeSupportIds")]
    [InlineData("legalBasisPrecedenceIndex")]
    [InlineData("kinshipDistance")]
    [InlineData("inheritance")]
    [InlineData("wealthTransfer")]
    [InlineData("estateTransfers")]
    [InlineData("previousOwnerCharacterId")]
    [InlineData("currentOwnerCharacterId")]
    [InlineData("regency")]
    [InlineData("regentCharacterId")]
    [InlineData("successorCharacterId")]
    [InlineData("sourceGuardianshipId")]
    [InlineData("sourceGuardianCharacterId")]
    [InlineData("sourceCustodianCharacterId")]
    [InlineData("controlledCharacterId")]
    [InlineData("foldedSelectedCount")]
    [InlineData("foldedDisputedCount")]
    [InlineData("foldedNoSuccessorCount")]
    [InlineData("contestResolutionMode")]
    [InlineData("maximumDisputedCandidates")]
    [InlineData("createsRegencyForIncapacitatedSuccessor")]
    [InlineData("noAcceptedSuccessorBehavior")]
    [InlineData("previousCampaignContinuity")]
    [InlineData("currentCampaignContinuity")]
    public void F913_Schema27RejectsEveryUniqueF9PropertyIncludingNull(
        string property)
    {
        JsonObject source = ReadFixture();
        source["f9Probe"] = new JsonObject { [property] = null };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(source));
    }

    [Fact]
    public void F914_CurrentSchemaRejectsMissingResolutionFieldsAndOldSystemVersion()
    {
        foreach (string scenario in new[]
                 {
                     "missing-resolutions",
                     "null-history",
                     "missing-continuity",
                     "old-system",
                 })
        {
            JsonObject current = new SaveSchemaRegistry().MigrateToCurrent(ReadFixture());
            JsonObject snapshot = current["snapshot"]!.AsObject();
            JsonObject successions = snapshot["characterSuccessions"]!.AsObject();
            switch (scenario)
            {
                case "missing-resolutions":
                    successions.Remove("resolutions");
                    break;
                case "null-history":
                    successions["resolutionHistory"] = null;
                    break;
                case "missing-continuity":
                    successions.Remove("campaignContinuity");
                    break;
                case "old-system":
                    SuccessionSystem(snapshot)["version"] = 3;
                    break;
            }

            Assert.Throws<SaveCompatibilityException>(() =>
                new SaveSchemaRegistry().MigrateToCurrent(current));
        }
    }

    [Theory]
    [InlineData("missing-resolution-inheritance")]
    [InlineData("missing-continuity-event")]
    [InlineData("missing-action-state")]
    [InlineData("mismatched-outcome")]
    [InlineData("mismatched-action-character")]
    [InlineData("mismatched-causal-id")]
    [InlineData("mismatched-rule")]
    [InlineData("mismatched-continuity-source")]
    [InlineData("mismatched-candidate-age")]
    [InlineData("malformed-wealth-transfer")]
    public void F914_CurrentSchemaRejectsIncompleteResolutionStateAndDiagnostics(
        string scenario)
    {
        JsonObject current = CurrentResolvedSave();
        JsonObject snapshot = current["snapshot"]!.AsObject();
        JsonObject successions = snapshot["characterSuccessions"]!.AsObject();
        JsonObject diagnostic = current["diagnosticEvents"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_condition_action_resolved.v1");
        JsonObject payload = diagnostic["payload"]!.AsObject();
        switch (scenario)
        {
            case "missing-resolution-inheritance":
                successions["resolutions"]!.AsArray()[0]!
                    .AsObject()
                    .Remove("inheritance");
                break;
            case "missing-continuity-event":
                successions["campaignContinuity"]!
                    .AsObject()
                    .Remove("sourceEventId");
                break;
            case "missing-action-state":
                payload["action"]!.AsObject()
                    .Remove("expectedResolutionStateId");
                break;
            case "mismatched-outcome":
                payload["outcome"]!.AsObject()["$type"] =
                    "household_head_death_resolved.v1";
                break;
            case "mismatched-action-character":
                payload["action"]!.AsObject()["characterId"] =
                    successions["resolutions"]!.AsArray()[0]!
                        ["selectedCandidate"]!["candidateCharacterId"]!
                        .DeepClone();
                break;
            case "mismatched-causal-id":
                diagnostic["causalId"] = JsonSerializer.SerializeToNode(
                    new EntityId("command:test/f9-schema-other"),
                    CanonicalJson.Options);
                break;
            case "mismatched-rule":
                payload["action"]!["rule"]!["maximumCandidates"] = 17;
                break;
            case "mismatched-continuity-source":
                payload["outcome"]!["succession"]!
                    ["currentCampaignContinuity"]!["sourceEventId"] =
                    JsonSerializer.SerializeToNode(
                        new EntityId("event:test/f9-schema-other"),
                        CanonicalJson.Options);
                break;
            case "mismatched-candidate-age":
                payload["outcome"]!["succession"]!
                    ["selectedCandidate"]!["candidateAge"] = 99;
                break;
            case "malformed-wealth-transfer":
                payload["outcome"]!["succession"]!["inheritance"]!
                    ["wealthTransfer"] = new JsonObject();
                break;
        }

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Fact]
    public void F914_CurrentSchemaRejectsSameTypeWealthLedgerTampering()
    {
        JsonObject current = CurrentResolvedSave(withWealth: true);
        JsonObject diagnostic = current["diagnosticEvents"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_condition_action_resolved.v1");
        JsonObject transfer = diagnostic["payload"]!["outcome"]!
            ["succession"]!["inheritance"]!["wealthTransfer"]!.AsObject();
        transfer["outgoingEntry"]!["counterpartyCharacterId"] =
            transfer["transfer"]!["sourceCharacterId"]!.DeepClone();

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Fact]
    public void F914_CurrentSchemaRejectsFoldedDiagnosticCandidateTampering()
    {
        JsonObject current = CurrentResolvedSave();
        JsonObject successions =
            current["snapshot"]!["characterSuccessions"]!.AsObject();
        JsonObject resolution =
            Assert.Single(successions["resolutions"]!.AsArray())!.AsObject();
        JsonObject history = successions["resolutionHistory"]!.AsObject();
        history["foldedSelectedCount"] = 1;
        history["earliestDate"] =
            resolution["resolutionDate"]!.DeepClone();
        history["latestDate"] =
            resolution["resolutionDate"]!.DeepClone();
        successions["resolutions"] = new JsonArray();
        JsonObject diagnostic = current["diagnosticEvents"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_condition_action_resolved.v1");
        diagnostic["payload"]!["outcome"]!["succession"]!
            ["selectedCandidate"]!["candidateAge"] = 99;
        WorldSnapshot snapshot = current["snapshot"]!
            .Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())!;
        current["checksum"] =
            SimulationChecksum.Compute(snapshot).Value;

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Fact]
    public void F914_CurrentSchemaRejectsFoldedMatchedInvalidRuleTampering()
    {
        JsonObject current = CurrentResolvedSave();
        JsonObject successions =
            current["snapshot"]!["characterSuccessions"]!.AsObject();
        JsonObject resolution =
            Assert.Single(successions["resolutions"]!.AsArray())!.AsObject();
        JsonObject history = successions["resolutionHistory"]!.AsObject();
        history["foldedSelectedCount"] = 1;
        history["earliestDate"] =
            resolution["resolutionDate"]!.DeepClone();
        history["latestDate"] =
            resolution["resolutionDate"]!.DeepClone();
        successions["resolutions"] = new JsonArray();
        JsonObject diagnostic = current["diagnosticEvents"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["payload"]?["$type"]?.GetValue<string>()
                == "character_condition_action_resolved.v1");
        diagnostic["payload"]!["action"]!["rule"]!
            ["includesPrincipalSpouse"] = true;
        diagnostic["payload"]!["outcome"]!["succession"]!["rule"]!
            ["includesPrincipalSpouse"] = true;
        WorldSnapshot snapshot = current["snapshot"]!
            .Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())!;
        current["checksum"] =
            SimulationChecksum.Compute(snapshot).Value;

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveSchemaRegistry().MigrateToCurrent(current));
    }

    [Theory]
    [InlineData("living-subject")]
    [InlineData("self-candidate")]
    [InlineData("unreachable-continuity")]
    [InlineData("missing-estate")]
    [InlineData("missing-guardianship")]
    [InlineData("regent-is-subject")]
    [InlineData("regent-is-child")]
    public void F914_RestoreRejectsCrossRecordSemanticTampering(string scenario)
    {
        bool minor = scenario is "missing-guardianship"
            or "regent-is-subject"
            or "regent-is-child";
        WorldSnapshot snapshot = CurrentResolvedSave(
                withEstate: scenario == "missing-estate",
                minorSuccession: minor)
            ["snapshot"]!
            .Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())!;
        SuccessionResolutionState resolution =
            Assert.Single(snapshot.CharacterSuccessions.Resolutions);
        switch (scenario)
        {
            case "living-subject":
                snapshot = snapshot with
                {
                    Characters = snapshot.Characters with
                    {
                        CharacterStates = snapshot.Characters.CharacterStates
                            .Select(state => state.CharacterId == SchemaSubject
                                ? state with
                                {
                                    Condition =
                                        CharacterConditionState.Default,
                                }
                                : state)
                            .ToArray(),
                    },
                };
                break;
            case "self-candidate":
                resolution = resolution with
                {
                    SelectedCandidate = resolution.SelectedCandidate! with
                    {
                        CandidateCharacterId = SchemaSubject,
                    },
                    PreviousCampaignContinuity = null,
                    CurrentCampaignContinuity = null,
                };
                snapshot = snapshot with
                {
                    CharacterSuccessions = snapshot.CharacterSuccessions with
                    {
                        Resolutions = [resolution],
                        CampaignContinuity = null,
                    },
                };
                break;
            case "unreachable-continuity":
                snapshot = snapshot with
                {
                    CharacterSuccessions = snapshot.CharacterSuccessions with
                    {
                        CampaignContinuity = snapshot.CharacterSuccessions
                            .CampaignContinuity! with
                        {
                            SourceCommandId = new(
                                "command:test/f9-schema-unreachable"),
                            SourceEventId = new(
                                "event:test/f9-schema-unreachable"),
                        },
                    },
                };
                break;
            case "missing-estate":
                resolution = resolution with
                {
                    Inheritance = resolution.Inheritance with
                    {
                        EstateTransfers =
                        [
                            Assert.Single(
                                resolution.Inheritance.EstateTransfers) with
                            {
                                EstateId = new(
                                    "estate:test/f9-schema-missing"),
                            },
                        ],
                    },
                };
                snapshot = ReplaceResolution(snapshot, resolution);
                break;
            case "missing-guardianship":
                resolution = resolution with
                {
                    Regency = resolution.Regency! with
                    {
                        SourceGuardianshipId = new(
                            "guardianship:test/f9-schema-missing"),
                        SourceGuardianCharacterId = SchemaAdultRegent,
                    },
                };
                snapshot = ReplaceResolution(snapshot, resolution);
                break;
            case "regent-is-subject":
                resolution = resolution with
                {
                    Regency = resolution.Regency! with
                    {
                        RegentCharacterId = SchemaSubject,
                    },
                };
                snapshot = ReplaceResolution(snapshot, resolution);
                break;
            case "regent-is-child":
                resolution = resolution with
                {
                    Regency = resolution.Regency! with
                    {
                        RegentCharacterId = SchemaChildRegent,
                    },
                };
                snapshot = ReplaceResolution(snapshot, resolution);
                break;
        }

        Assert.Throws<SimulationValidationException>(() =>
            WorldState.Restore(snapshot));
    }

    [Fact]
    public void F914_UnorderedCommandRuleLoadsAgainstCanonicalResolutionRule()
    {
        JsonObject current = CurrentResolvedSave(unorderedRule: true);

        JsonObject validated =
            new SaveSchemaRegistry().MigrateToCurrent(current);
        SaveEnvelope envelope = validated.Deserialize<SaveEnvelope>(
            CanonicalJson.Options)!;
        _ = WorldState.Restore(envelope.Snapshot);
    }

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
                $"Snapshot property '{property}' changed during schema 27 to 28 migration.");
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

    private static WorldSnapshot ReplaceResolution(
        WorldSnapshot snapshot,
        SuccessionResolutionState resolution) => snapshot with
        {
            CharacterSuccessions = snapshot.CharacterSuccessions with
            {
                Resolutions = [resolution],
            },
        };

    private static JsonObject CurrentResolvedSave(
        bool withWealth = false,
        bool withEstate = false,
        bool minorSuccession = false,
        bool unorderedRule = false)
    {
        CampaignDate date = new(200, 1, 1);
        EntityId subject = SchemaSubject;
        EntityId successor = SchemaSuccessor;
        CharacterDefinition[] definitions = new[]
            {
                subject,
                successor,
                SchemaAdultRegent,
                SchemaChildRegent,
            }
            .Select(id =>
            {
                EntityId nameKey =
                    new($"loc:{id.Value.Replace(':', '/')}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    id == subject
                        ? new CampaignDate(160, 1, 1)
                        : id == successor
                            ? minorSuccession
                                ? new CampaignDate(190, 1, 1)
                                : new CampaignDate(180, 1, 1)
                            : id == SchemaAdultRegent
                                ? new CampaignDate(170, 1, 1)
                                : new CampaignDate(195, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(id),
                    null,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states =
        [
            new(
                CharacterContractVersions.State,
                subject,
                [],
                [],
                CharacterConditionState.Default,
                []),
            new(
                CharacterContractVersions.State,
                successor,
                [subject],
                [new(subject, ParentChildLinkKind.Biological)],
                CharacterConditionState.Default,
                []),
            new(
                CharacterContractVersions.State,
                SchemaAdultRegent,
                [],
                [],
                CharacterConditionState.Default,
                []),
            new(
                CharacterContractVersions.State,
                SchemaChildRegent,
                [],
                [],
                CharacterConditionState.Default,
                []),
        ];
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
        CharacterSuccessionWorldSnapshot successions =
            CharacterSuccessionWorldSnapshot.Empty with
            {
                CampaignContinuity = new(
                    CharacterSuccessionContractVersions.CampaignContinuity,
                    PlayerCampaignContinuityStatus.Active,
                    subject,
                    date.AddDays(-1),
                    0,
                    new("command:test/f9-schema-continuity"),
                    new("event:test/f9-schema-continuity")),
            };
        CharacterResourceWorldSnapshot resources = withWealth
            ? new(
                CharacterResourceContractVersions.Snapshot,
                [
                    new(
                        CharacterResourceContractVersions.State,
                        CharacterResourceIds.DeriveWealthAccountId(subject),
                        subject,
                        10),
                    new(
                        CharacterResourceContractVersions.State,
                        CharacterResourceIds.DeriveWealthAccountId(successor),
                        successor,
                        1),
                ],
                [],
                [])
            : CharacterResourceWorldSnapshot.Empty;
        CharacterEstateHoldingWorldSnapshot estates = withEstate
            ? new(
                CharacterEstateHoldingContractVersions.Snapshot,
                [
                    new(
                        CharacterEstateHoldingContractVersions.State,
                        new("estate:test/f9-schema"),
                        subject),
                ])
            : CharacterEstateHoldingWorldSnapshot.Empty;
        CampaignSimulation simulation = new(WorldState.Create(
            date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            resources,
            estates,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            successions));
        SuccessionResolutionRule rule = new(
            CharacterSuccessionContractVersions.ResolutionRule,
            new(
                CharacterSuccessionContractVersions.CandidateEligibilityRule,
                [SuccessionCandidateBasis.BiologicalDescendant],
                8,
                0,
                AllowsIncapacitatedCandidates: true,
                unorderedRule
                    ? Enum.GetValues<CharacterCustodyStatus>()
                        .Reverse()
                        .ToArray()
                    : Enum.GetValues<CharacterCustodyStatus>()),
            [SuccessionLegalBasis.BiologicalDescendant],
            IncludesPrincipalSpouse: false,
            AllowedCollateralKinds: [],
            MaximumCollateralDistance: 0,
            SuccessionContestResolutionMode.ResolveByStableId,
            MaximumCandidates: 16,
            MaximumDisputedCandidates: 8,
            CreatesRegencyForIncapacitatedSuccessor: true,
            SuccessionNoAcceptedSuccessorBehavior.EndCampaign);
        EntityId commandId = new("command:test/f9-schema-resolution");
        EntityId expected =
            simulation.World.GetCharacterSuccessionResolutionStateId(
                subject,
                rule,
                date,
                simulation.World.Calendar.TurnIndex,
                regentCharacterId:
                    minorSuccession ? SchemaAdultRegent : null);
        CampaignCommand command = CampaignCommand.Create(
            commandId,
            CharacterConditionSystem.AuthoritativeActorId,
            date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterSuccessionDeathAction(
                    subject,
                    CharacterConditionState.Default,
                    rule,
                    expected,
                    null,
                    null,
                    minorSuccession ? SchemaAdultRegent : null)));
        CommandValidationResult validation = simulation.Submit(command);
        Assert.True(
            validation.IsValid,
            string.Join(
                "; ",
                validation.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message}")));
        Assert.Single(simulation.ResolveTurn());
        return JsonSerializer.SerializeToNode(
            SaveEnvelope.Create("test", [], simulation),
            CanonicalJson.Options)!.AsObject();
    }

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "save-schema-27-f8-history-backed.json");
}
