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
    private const string FrozenSchemaFourChecksum = "48b94dad9d4dda78591243341afa16ece40e0ed157368f84c1189641684ecd3e";
    private const string FrozenSchemaFiveChecksum = "4ef74d59d48b7415cc86a40eca98ca3ac3fdafe5c0a5047bdb3b1ff3d5f3ea14";
    private const string FrozenSchemaSixChecksum = "90c27f2dd9954e0d3d2a304e9b661bf1ef25f2e4f620743d78bc60382d780bd4";
    private const string FrozenSchemaSevenChecksum = "0c9033c2a0e145a73218aa234f3725878fc7b781b9e6a8e83adad74b10b79d72";
    private const string FrozenSchemaEightChecksum = "ba485b0efc67e7cff38cf6de4b4536dbda2191ee87f5577ff1ee2d1d0031424f";
    private const string FrozenSchemaNineChecksum = "1ef0f8728311ab217e84d9e6ff432342a7bac85b74aae6eee2cf92159d541684";
    private const string FrozenSchemaTenChecksum = "6e644f0db882a7b7440653060c5b635d6020844a1f032ee05afbe48dd90bce12";
    private const string FrozenSchemaElevenChecksum = "9c5dc3195649bfde2626f95c7cf2573d4acbc4c2a081b9af0ac9d30c74f9c8fb";
    private const string FrozenSchemaElevenFileSha256 = "ce6f737a9e3a608dfaaaeaf422f74e134a8fa7073ad4026a9aa1354007174d14";
    private const string FrozenSchemaTwelveChecksum = "62988012ca8ed090e62a922a2b3b357ea60d3b044d91a6dcc56b4ea92ad087dd";
    private const string FrozenSchemaTwelveFileSha256 = "12ac4db8897a7ddd2f0101085231130ab48057bc22222ee203dcbfd35c1b8061";
    private const string FrozenSchemaThirteenChecksum = "540dac5d9065bbb1b66a484c550eadf4e60de7be92acacb5c716bfcf7f4956c4";
    private const string FrozenSchemaThirteenFileSha256 = "f54e0bb16eb827b2b739384a27325dd0a279cd43f18d612085c247fd3aa1fca5";
    private const string FrozenSchemaFourteenChecksum = "8cb144a3b14600bd9916b03cca20644ed69161614d2b6665cdf78fdeadad2b65";
    private const string FrozenSchemaFourteenFileSha256 = "381c774072a549090dd275f3fefa1bb7a3953a0a74eb3852eeb8323ad0b8f01e";
    private const string FrozenSchemaFifteenChecksum = "ed398b879de95b9065bffb367fb3a7b830c40d6a8e1f29d3a0a64ffb414e5b86";
    private const string FrozenSchemaFifteenFileSha256 = "51a6ecf8e0556af35cbe4010645925d50bc965ca4a2d07ea05c8c9c486538fc3";
    private const string FrozenSchemaSixteenChecksum = "aac74df0b0c97f3ec49c6e2aeaef1be742f102a797f7d317b576e1e8ed2da471";
    private const string FrozenSchemaSixteenFileSha256 = "3f118e24486fc7367dc00f07419abfb19eff13a144daffc878ee73a99277fbe5";
    private const string FrozenSchemaSeventeenChecksum = "7a680781eceabf8c46554f780aaaca0ef6f781caf3256cac46185f0031e88ea4";
    private const string FrozenSchemaSeventeenFileSha256 = "c99c1183f408dfd66eb08015e3b10d4e5b3a2f573c4adb364cd395fb8a1eb9c2";
    private const string FrozenSchemaEighteenChecksum = "7f0feb49415b8d0074d447381340aea0c09200964f55104034db60c04f70d49e";
    private const string FrozenSchemaEighteenFileSha256 = "b27ccfdc51704721055161d6e57738e40030ff67d96f928e61bcbf5ed93c9453";
    private const string FrozenSchemaNineteenChecksum = "dc49cef5fe5d55a310ec0191189358a188224afd8df33fc689efc4c34edd999c";
    private const string FrozenSchemaNineteenFileSha256 = "d6af4142e95b38fbd47386668de7b85253f345f29699258c0b9dce99b87369f0";
    // Reconstructed literally from the exact schema-4 serializer contract at eaa3aaf.
    // Unlike the inferred schema-1/2 fixtures, this contains nonempty character history.
    private const string FrozenSchemaFourFixture = """{"schemaVersion":4,"contractVersion":2,"gameVersion":"0.1.0","createdUtc":"2026-07-15T00:00:00+00:00","contentManifests":[{"packId":{"value":"base:synthetic"},"version":"1.0.0","checksum":"sha256:abc","requiredForSimulation":true}],"seed":99,"snapshot":{"contractVersion":1,"calendar":{"date":{"year":191,"month":7,"day":14},"turnIndex":0,"daysInCurrentTurn":3},"rootSeed":99,"randomStreams":[],"entities":[],"pendingCommands":[],"systemVersions":[{"systemId":"simulation.calendar","version":1},{"systemId":"simulation.synthetic_entities","version":1},{"systemId":"simulation.command_events","version":1},{"systemId":"simulation.geography","version":1},{"systemId":"simulation.characters","version":1}],"lastEventDate":null,"lastEventPhase":null,"lastEventPriority":null,"lastEventId":null,"geography":{"graph":{"regions":[],"districts":[],"localities":[],"stops":[],"routes":[]},"season":0,"weather":0,"locations":[],"routes":[],"armies":[]},"characters":{"contractVersion":1,"identityDefinitions":[{"contractVersion":1,"id":{"value":"ability:synthetic/command"},"kind":0,"nameKey":{"value":"loc:ability/synthetic_command"}}],"characterDefinitions":[{"contractVersion":1,"id":{"value":"character:synthetic/adult"},"nameKey":{"value":"loc:character/synthetic_adult"},"birthDate":{"year":160,"month":1,"day":1},"abilityIds":[{"value":"ability:synthetic/command"}],"aptitudeIds":[],"traitIds":[],"ambitionIds":[],"reputationIds":[]}],"familyDefinitions":[],"householdDefinitions":[],"characterStates":[{"contractVersion":1,"characterId":{"value":"character:synthetic/adult"},"parentIds":[]}],"familyStates":[],"householdStates":[]}},"diagnosticCommands":[],"diagnosticEvents":[],"checksum":"48b94dad9d4dda78591243341afa16ece40e0ed157368f84c1189641684ecd3e"}""";
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
    public void SchemaTen_RoundTripsNonemptyCharacterResourcesAndDiagnostics()
    {
        CampaignSimulation simulation = CreateCharacterResourceSimulation();
        EntityId child = new("character:synthetic/child");
        EntityId parent = new("character:synthetic/parent");
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/wealth-transfer"),
            child,
            simulation.World.Calendar.Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(parent, 25)));
        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent resolved = Assert.Single(simulation.ResolveTurn());
        Assert.IsType<CharacterResourceActionResolvedEventPayload>(resolved.Payload);
        SaveEnvelope expected = CreateEnvelope(simulation);
        string path = Path.Combine(directory, "character-resources.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        Assert.Equal(75, actual.Snapshot.CharacterResources.Accounts.Single(
            account => account.CharacterId == child).Wealth);
        Assert.Equal(25, actual.Snapshot.CharacterResources.Accounts.Single(
            account => account.CharacterId == parent).Wealth);
        Assert.Equal(2, actual.Snapshot.CharacterResources.LedgerEntries.Count);
        Assert.IsType<CharacterResourceActionCommandPayload>(Assert.Single(actual.DiagnosticCommands).Payload);
        Assert.IsType<CharacterResourceActionResolvedEventPayload>(Assert.Single(actual.DiagnosticEvents).Payload);
        WorldState restored = WorldState.Restore(actual.Snapshot);
        Assert.Equal(75, restored.CharacterResources.GetWealth(child));
        Assert.Equal(25, restored.CharacterResources.GetWealth(parent));
        Assert.Equal(expected.Checksum, actual.Checksum);
    }

    [Fact]
    public void SchemaTen_SaveLoad_RoundTripsCharacterRelationshipCareerAndResourceState()
    {
        SaveEnvelope expected = CreateEnvelope(CreateCharacterSimulation());
        string path = Path.Combine(directory, "characters.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, actual.SchemaVersion);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Snapshot.Characters, CanonicalJson.Options),
            JsonSerializer.Serialize(actual.Snapshot.Characters, CanonicalJson.Options));
        Assert.Equal(expected.Checksum, actual.Checksum);
        Assert.Empty(actual.Snapshot.Relationships.Subjects);
        Assert.Empty(actual.Snapshot.Careers.Proposals);
        Assert.Empty(actual.Snapshot.CharacterResources.Accounts);
        Assert.Empty(actual.Snapshot.CharacterEstateHoldings.Holdings);
        Assert.Empty(actual.Snapshot.CharacterMarriages.Unions);
        Assert.True(WorldState.Restore(actual.Snapshot).Characters.TryGetCharacterProfile(
            new EntityId("character:synthetic/child"),
            out AuthoritativeCharacterProfile? profile));
        Assert.Equal(21, profile.Age);
    }

    [Fact]
    public void SchemaTen_SaveLoad_RoundTripsEstateHeldByDeadCharacter()
    {
        CampaignSimulation simulation = CreateCharacterEstateHoldingSimulation();
        EntityId parent = new("character:synthetic/parent");
        EntityId estate = new("estate:synthetic/family_manor");
        SaveEnvelope expected = CreateEnvelope(simulation);
        string path = Path.Combine(directory, "character-estate-holdings.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        CharacterEstateHoldingState holding = Assert.Single(actual.Snapshot.CharacterEstateHoldings.Holdings);
        Assert.Equal(estate, holding.EstateId);
        Assert.Equal(parent, holding.OwnerCharacterId);
        CharacterState owner = Assert.Single(
            actual.Snapshot.Characters.CharacterStates,
            state => state.CharacterId == parent);
        Assert.Equal(CharacterVitalStatus.Dead, owner.Condition!.VitalStatus);
        WorldState restored = WorldState.Restore(actual.Snapshot);
        Assert.True(restored.CharacterEstateHoldings.TryGetHolding(estate, out CharacterEstateHoldingState? restoredHolding));
        Assert.Equal(parent, restoredHolding.OwnerCharacterId);
        Assert.Equal(expected.Checksum, actual.Checksum);
    }

    [Fact]
    public void SchemaTen_SaveLoad_RoundTripsNonemptyCharacterMarriageFoundation()
    {
        CampaignSimulation simulation = CreateCharacterMarriageSimulation();
        SaveEnvelope expected = CreateEnvelope(simulation);
        string path = Path.Combine(directory, "character-marriages.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        MarriagePracticeState practice = Assert.Single(actual.Snapshot.CharacterMarriages.Practices);
        MarriageProposalState proposal = Assert.Single(actual.Snapshot.CharacterMarriages.Proposals);
        MarriageUnionState union = Assert.Single(actual.Snapshot.CharacterMarriages.Unions);
        Assert.Equal(new EntityId("marriage_practice:synthetic/default"), practice.PracticeId);
        Assert.Equal(MarriageProposalStatus.Accepted, proposal.Status);
        Assert.Equal(MarriageUnionStatus.Active, union.Status);
        Assert.Equal(MarriageBasis.Political, union.Basis);
        Assert.Empty(actual.Snapshot.CharacterMarriages.RomanceRoutes);
        WorldState restored = WorldState.Restore(actual.Snapshot);
        Assert.True(restored.CharacterMarriages.TryGetUnion(union.UnionId, out MarriageUnionState? restoredUnion));
        Assert.Equal(union, restoredUnion);
        Assert.Equal(expected.Checksum, actual.Checksum);
    }

    [Theory]
    [InlineData("missing-characters")]
    [InlineData("null-characters")]
    [InlineData("partial-characters")]
    [InlineData("null-character-state")]
    [InlineData("null-structured-name")]
    [InlineData("missing-courtesy-name-key")]
    [InlineData("missing-origin-kind")]
    [InlineData("missing-historical-classification")]
    [InlineData("missing-owning-pack-id")]
    [InlineData("missing-applied-override-pack-ids")]
    [InlineData("missing-source-ids")]
    [InlineData("missing-culture-id")]
    [InlineData("missing-origin-location-id")]
    [InlineData("null-parent-links")]
    [InlineData("missing-parent-link-kind")]
    [InlineData("missing-condition")]
    [InlineData("missing-education-attainments")]
    [InlineData("null-education-attainments")]
    [InlineData("missing-vital-status")]
    [InlineData("missing-incapacitated")]
    [InlineData("missing-custodian-id")]
    [InlineData("missing-character-system-version")]
    [InlineData("missing-relationships")]
    [InlineData("null-relationships")]
    [InlineData("partial-relationships")]
    [InlineData("null-relationship-subject")]
    [InlineData("missing-relationship-system-version")]
    [InlineData("missing-careers")]
    [InlineData("null-careers")]
    [InlineData("partial-careers")]
    [InlineData("missing-career-system-version")]
    [InlineData("missing-character-resources")]
    [InlineData("null-character-resources")]
    [InlineData("partial-character-resources")]
    [InlineData("missing-character-resource-system-version")]
    [InlineData("missing-character-estate-holdings")]
    [InlineData("null-character-estate-holdings")]
    [InlineData("partial-character-estate-holdings")]
    [InlineData("missing-character-estate-holding-system-version")]
    [InlineData("duplicate-character-estate-holding-system-version")]
    [InlineData("unsupported-character-estate-holding-system-version")]
    [InlineData("missing-character-marriages")]
    [InlineData("null-character-marriages")]
    [InlineData("partial-character-marriages")]
    [InlineData("missing-character-marriage-system-version")]
    [InlineData("duplicate-character-marriage-system-version")]
    [InlineData("unsupported-character-marriage-system-version")]
    [InlineData("missing-character-guardianships")]
    [InlineData("null-character-guardianships")]
    [InlineData("partial-character-guardianships")]
    [InlineData("missing-character-guardianship-system-version")]
    [InlineData("duplicate-character-guardianship-system-version")]
    [InlineData("unsupported-character-guardianship-system-version")]
    [InlineData("missing-character-pregnancies")]
    [InlineData("null-character-pregnancies")]
    [InlineData("partial-character-pregnancies")]
    [InlineData("missing-character-pregnancy-system-version")]
    [InlineData("duplicate-character-pregnancy-system-version")]
    [InlineData("unsupported-character-pregnancy-system-version")]
    public void CurrentSchema_RequiresCompleteCharacterSubsystemDataWithoutChangingSource(
        string mutation)
    {
        JsonObject current = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterSimulation()),
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
            case "null-structured-name":
                snapshot["characters"]!["characterDefinitions"]![0]!["structuredName"] = null;
                break;
            case "missing-courtesy-name-key":
                snapshot["characters"]!["characterDefinitions"]![0]!["structuredName"]!
                    .AsObject().Remove("courtesyNameKey");
                break;
            case "missing-origin-kind":
                snapshot["characters"]!["characterDefinitions"]![0]!["contentOrigin"]!
                    .AsObject().Remove("originKind");
                break;
            case "missing-historical-classification":
                snapshot["characters"]!["characterDefinitions"]![0]!["contentOrigin"]!
                    .AsObject().Remove("historicalClassification");
                break;
            case "missing-owning-pack-id":
                snapshot["characters"]!["characterDefinitions"]![0]!["contentOrigin"]!
                    .AsObject().Remove("owningPackId");
                break;
            case "missing-applied-override-pack-ids":
                snapshot["characters"]!["characterDefinitions"]![0]!["contentOrigin"]!
                    .AsObject().Remove("appliedOverridePackIds");
                break;
            case "missing-source-ids":
                snapshot["characters"]!["characterDefinitions"]![0]!["contentOrigin"]!
                    .AsObject().Remove("sourceIds");
                break;
            case "missing-culture-id":
                snapshot["characters"]!["characterDefinitions"]![0]!.AsObject().Remove("cultureId");
                break;
            case "missing-origin-location-id":
                snapshot["characters"]!["characterDefinitions"]![0]!
                    .AsObject().Remove("originLocationId");
                break;
            case "null-parent-links":
                snapshot["characters"]!["characterStates"]![0]!["parentLinks"] = null;
                break;
            case "missing-parent-link-kind":
                snapshot["characters"]!["characterStates"]![0]!["parentLinks"]![0]!
                    .AsObject().Remove("kind");
                break;
            case "missing-condition":
                snapshot["characters"]!["characterStates"]![0]!.AsObject().Remove("condition");
                break;
            case "missing-education-attainments":
                snapshot["characters"]!["characterStates"]![0]!
                    .AsObject().Remove("educationAttainments");
                break;
            case "null-education-attainments":
                snapshot["characters"]!["characterStates"]![0]!["educationAttainments"] = null;
                break;
            case "missing-vital-status":
                snapshot["characters"]!["characterStates"]![0]!["condition"]!
                    .AsObject().Remove("vitalStatus");
                break;
            case "missing-incapacitated":
                snapshot["characters"]!["characterStates"]![0]!["condition"]!
                    .AsObject().Remove("isIncapacitated");
                break;
            case "missing-custodian-id":
                snapshot["characters"]!["characterStates"]![0]!["condition"]!
                    .AsObject().Remove("custodianId");
                break;
            case "missing-character-system-version":
                RemoveSystemVersion(snapshot, "simulation.characters");
                break;
            case "missing-relationships":
                snapshot.Remove("relationships");
                break;
            case "null-relationships":
                snapshot["relationships"] = null;
                break;
            case "partial-relationships":
                snapshot["relationships"]!.AsObject().Remove("subjects");
                break;
            case "null-relationship-subject":
                snapshot["relationships"]!["subjects"]!.AsArray().Add(null);
                break;
            case "missing-relationship-system-version":
                RemoveSystemVersion(snapshot, "simulation.relationships");
                break;
            case "missing-careers":
                snapshot.Remove("careers");
                break;
            case "null-careers":
                snapshot["careers"] = null;
                break;
            case "partial-careers":
                snapshot["careers"]!.AsObject().Remove("employmentTenures");
                break;
            case "missing-career-system-version":
                RemoveSystemVersion(snapshot, "simulation.character_careers");
                break;
            case "missing-character-resources":
                snapshot.Remove("characterResources");
                break;
            case "null-character-resources":
                snapshot["characterResources"] = null;
                break;
            case "partial-character-resources":
                snapshot["characterResources"]!.AsObject().Remove("ledgerEntries");
                break;
            case "missing-character-resource-system-version":
                RemoveSystemVersion(snapshot, CharacterResourceSystem.SystemId);
                break;
            case "missing-character-estate-holdings":
                snapshot.Remove("characterEstateHoldings");
                break;
            case "null-character-estate-holdings":
                snapshot["characterEstateHoldings"] = null;
                break;
            case "partial-character-estate-holdings":
                snapshot["characterEstateHoldings"]!.AsObject().Remove("holdings");
                break;
            case "missing-character-estate-holding-system-version":
                RemoveSystemVersion(snapshot, CharacterEstateHoldingSystem.SystemId);
                break;
            case "duplicate-character-estate-holding-system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterEstateHoldingSystem.SystemId,
                    ["version"] = CharacterEstateHoldingSystem.Version,
                });
                break;
            case "unsupported-character-estate-holding-system-version":
                JsonObject estateSystemVersion = snapshot["systemVersions"]!.AsArray()
                    .OfType<JsonObject>()
                    .Single(version => version["systemId"]!.GetValue<string>()
                        == CharacterEstateHoldingSystem.SystemId);
                estateSystemVersion["version"] = 999;
                break;
            case "missing-character-marriages":
                snapshot.Remove("characterMarriages");
                break;
            case "null-character-marriages":
                snapshot["characterMarriages"] = null;
                break;
            case "partial-character-marriages":
                snapshot["characterMarriages"]!.AsObject().Remove("unions");
                break;
            case "missing-character-marriage-system-version":
                RemoveSystemVersion(snapshot, CharacterMarriageSystem.SystemId);
                break;
            case "duplicate-character-marriage-system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterMarriageSystem.SystemId,
                    ["version"] = CharacterMarriageSystem.Version,
                });
                break;
            case "unsupported-character-marriage-system-version":
                JsonObject marriageSystemVersion = snapshot["systemVersions"]!.AsArray()
                    .OfType<JsonObject>()
                    .Single(version => version["systemId"]!.GetValue<string>()
                        == CharacterMarriageSystem.SystemId);
                marriageSystemVersion["version"] = 999;
                break;
            case "missing-character-guardianships":
                snapshot.Remove("characterGuardianships");
                break;
            case "null-character-guardianships":
                snapshot["characterGuardianships"] = null;
                break;
            case "partial-character-guardianships":
                snapshot["characterGuardianships"]!.AsObject().Remove("guardianships");
                break;
            case "missing-character-guardianship-system-version":
                RemoveSystemVersion(snapshot, CharacterGuardianshipSystem.SystemId);
                break;
            case "duplicate-character-guardianship-system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterGuardianshipSystem.SystemId,
                    ["version"] = CharacterGuardianshipSystem.Version,
                });
                break;
            case "unsupported-character-guardianship-system-version":
                JsonObject guardianshipSystemVersion = snapshot["systemVersions"]!.AsArray()
                    .OfType<JsonObject>()
                    .Single(version => version["systemId"]!.GetValue<string>()
                        == CharacterGuardianshipSystem.SystemId);
                guardianshipSystemVersion["version"] = 999;
                break;
            case "missing-character-pregnancies":
                snapshot.Remove("characterPregnancies");
                break;
            case "null-character-pregnancies":
                snapshot["characterPregnancies"] = null;
                break;
            case "partial-character-pregnancies":
                snapshot["characterPregnancies"]!.AsObject().Remove("activePregnancies");
                break;
            case "missing-character-pregnancy-system-version":
                RemoveSystemVersion(snapshot, CharacterPregnancySystem.SystemId);
                break;
            case "duplicate-character-pregnancy-system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterPregnancySystem.SystemId,
                    ["version"] = CharacterPregnancySystem.Version,
                });
                break;
            case "unsupported-character-pregnancy-system-version":
                JsonObject pregnancySystemVersion = snapshot["systemVersions"]!.AsArray()
                    .OfType<JsonObject>()
                    .Single(version => version["systemId"]!.GetValue<string>()
                        == CharacterPregnancySystem.SystemId);
                pregnancySystemVersion["version"] = 999;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"current-schema-{mutation}.save.gz");
        WriteJsonGzip(path, current);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaEighteen_MigratedOverduePregnancyResolvesWithExpectedBirthDate()
    {
        JsonObject compatible = CreateHistoricalFixture(18);
        WorldSnapshot historical = compatible["snapshot"]!.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())!;
        CharacterPregnancyState pregnancy = Assert.Single(
            historical.CharacterPregnancies.ActivePregnancies);
        CampaignDate overdueDate = pregnancy.ExpectedBirthDate.AddDays(2);
        historical = historical with
        {
            Calendar = new CampaignCalendar(overdueDate, historical.Calendar.TurnIndex),
            PendingCommands = [],
        };
        compatible["snapshot"] = JsonSerializer.SerializeToNode(
            historical,
            SimulationJson.CreateOptions());
        foreach (JsonObject state in compatible["snapshot"]!["characters"]!["characterStates"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            state.Remove("educationAttainments");
        }
        compatible["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            historical,
            18).Value;
        string path = Path.Combine(directory, "schema-eighteen-overdue-birth.save.gz");
        WriteJsonGzip(path, compatible);
        byte[] sourceBytes = File.ReadAllBytes(path);
        SaveEnvelope migrated = new SaveStore().Load(path);
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/schema-eighteen-overdue-birth"),
            CharacterFamilySystem.AuthoritativeActorId,
            overdueDate,
            new CharacterFamilyActionCommandPayload(
                new ResolvePregnancyBirthAction(
                    pregnancy.PregnancyId,
                    new GeneratedNewbornSpecification(
                        CharacterBirthContractVersions.NewbornSpecification,
                        new EntityId("loc:test/schema_eighteen_overdue_child"),
                        null,
                        null,
                        new EntityId("household:d3/000"),
                        []))),
            ResolutionPhase.Commands);

        Assert.True(simulation.Submit(command).IsValid);
        CharacterBirthChange birth = Assert.IsType<PregnancyBirthResolvedOutcome>(
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).Birth;

        Assert.Equal(pregnancy.ExpectedBirthDate, birth.ChildDefinition.BirthDate);
        Assert.Equal(overdueDate, birth.ResolutionDate);
        Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("guardianCharacterId")]
    [InlineData("sourceEventId")]
    [InlineData("endReason")]
    public void CurrentSchema_RejectsMalformedGuardianshipRecordWithoutChangingSource(
        string removedProperty)
    {
        JsonObject current = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterSimulation()),
            CanonicalJson.Options)!.AsObject();
        EntityId guardian = new("character:synthetic/parent");
        EntityId ward = new("character:synthetic/child");
        CampaignDate establishedDate = new(175, 1, 1);
        EntityId commandId = new("command:test/current-guardianship-shape");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            establishedDate,
            commandId);
        CharacterGuardianshipState guardianship = new(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(eventId, ward, guardian),
            ward,
            guardian,
            establishedDate,
            0,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
        JsonObject record = JsonSerializer.SerializeToNode(
            guardianship,
            CanonicalJson.Options)!.AsObject();
        Assert.True(record.Remove(removedProperty));
        current["snapshot"]!["characterGuardianships"]!["guardianships"]!
            .AsArray().Add(record);
        string path = Path.Combine(
            directory,
            $"current-guardianship-record-{removedProperty}.save.gz");
        WriteJsonGzip(path, current);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("missing-estate-id")]
    [InlineData("missing-owner-character-id")]
    [InlineData("unsupported-contract")]
    public void SchemaTen_RequiresCompleteEstateEntryShapeWithoutChangingSource(string mutation)
    {
        JsonObject current = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterEstateHoldingSimulation()),
            CanonicalJson.Options)!.AsObject();
        JsonObject holding = current["snapshot"]!["characterEstateHoldings"]!["holdings"]![0]!.AsObject();
        switch (mutation)
        {
            case "missing-estate-id":
                holding.Remove("estateId");
                break;
            case "missing-owner-character-id":
                holding.Remove("ownerCharacterId");
                break;
            case "unsupported-contract":
                holding["contractVersion"] = 999;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-ten-estate-{mutation}.save.gz");
        WriteJsonGzip(path, current);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("practice-null")]
    [InlineData("snapshot-missing-invitations")]
    [InlineData("practice-missing-id")]
    [InlineData("proposal-missing-resolution")]
    [InlineData("betrothal-missing-source")]
    [InlineData("betrothal-missing-fulfillment-union")]
    [InlineData("union-missing-consent")]
    [InlineData("route-missing-practice")]
    [InlineData("invitation-missing-source")]
    [InlineData("route-v2-missing-evidence")]
    [InlineData("history-missing-count")]
    public void SchemaTen_RequiresCompleteCharacterMarriageEntryShapesWithoutChangingSource(
        string mutation)
    {
        JsonObject current = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterSimulation()),
            CanonicalJson.Options)!.AsObject();
        JsonObject marriages = current["snapshot"]!["characterMarriages"]!.AsObject();
        CampaignDate date = new(191, 1, 1);
        EntityId first = new("character:synthetic/child");
        EntityId second = new("character:synthetic/parent");
        EntityId practiceId = new("marriage_practice:test/default");
        JsonObject entry;
        switch (mutation)
        {
            case "snapshot-missing-invitations":
                marriages.Remove("invitations");
                break;
            case "practice-null":
                marriages["practices"]!.AsArray().Add(null);
                break;
            case "practice-missing-id":
                entry = JsonSerializer.SerializeToNode(new MarriagePracticeState(
                    CharacterMarriageContractVersions.Practice,
                    practiceId,
                    18,
                    18,
                    1,
                    8,
                    1,
                    true,
                    true,
                    MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings),
                    CanonicalJson.Options)!.AsObject();
                entry.Remove("practiceId");
                marriages["practices"]!.AsArray().Add(entry);
                break;
            case "proposal-missing-resolution":
                entry = JsonSerializer.SerializeToNode(new MarriageProposalState(
                    CharacterMarriageContractVersions.State,
                    new EntityId("marriage_proposal:test/value"),
                    MarriageProposalKind.LegalUnion,
                    MarriageBasis.Political,
                    MarriageUnionForm.PrincipalSpouse,
                    MarriageConsentKind.Voluntary,
                    first,
                    second,
                    null,
                    practiceId,
                    date,
                    0,
                    new EntityId("command:test/proposal"),
                    MarriageProposalStatus.Active,
                    null,
                    null,
                    null), CanonicalJson.Options)!.AsObject();
                entry.Remove("resolutionDate");
                marriages["proposals"]!.AsArray().Add(entry);
                break;
            case "betrothal-missing-source":
            case "betrothal-missing-fulfillment-union":
                entry = JsonSerializer.SerializeToNode(new PoliticalBetrothalState(
                    CharacterMarriageContractVersions.State,
                    new EntityId("political_betrothal:test/value"),
                    first,
                    second,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    practiceId,
                    new EntityId("marriage_proposal:test/value"),
                    date,
                    0,
                    PoliticalBetrothalStatus.Active,
                    null,
                    null,
                    null,
                    null), CanonicalJson.Options)!.AsObject();
                entry.Remove(mutation == "betrothal-missing-source"
                    ? "sourceProposalId"
                    : "fulfillmentUnionId");
                marriages["betrothals"]!.AsArray().Add(entry);
                break;
            case "union-missing-consent":
                entry = JsonSerializer.SerializeToNode(new MarriageUnionState(
                    CharacterMarriageContractVersions.State,
                    new EntityId("marriage_union:test/value"),
                    first,
                    second,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    MarriageBasis.Political,
                    MarriageConsentKind.Voluntary,
                    practiceId,
                    new EntityId("marriage_proposal:test/value"),
                    date,
                    0,
                    MarriageUnionStatus.Active,
                    null,
                    null,
                    null,
                    null), CanonicalJson.Options)!.AsObject();
                entry.Remove("consentKind");
                marriages["unions"]!.AsArray().Add(entry);
                break;
            case "route-missing-practice":
                entry = JsonSerializer.SerializeToNode(new RomanceRouteState(
                    CharacterMarriageContractVersions.State,
                    new EntityId("romance_route:test/value"),
                    first,
                    second,
                    practiceId,
                    0,
                    date,
                    0,
                    new EntityId("command:test/romance"),
                    RomanceRouteStatus.Active,
                    null,
                    null,
                    null), CanonicalJson.Options)!.AsObject();
                entry.Remove("practiceId");
                marriages["romanceRoutes"]!.AsArray().Add(entry);
                break;
            case "invitation-missing-source":
                EntityId invitationCommand = new("command:test/romance-invitation");
                entry = JsonSerializer.SerializeToNode(new RomanceInvitationState(
                    CharacterMarriageContractVersions.RomanceInvitationState,
                    CharacterMarriageIds.DeriveRomanceInvitationId(date, invitationCommand),
                    first,
                    second,
                    practiceId,
                    date,
                    0,
                    invitationCommand), CanonicalJson.Options)!.AsObject();
                entry.Remove("sourceCommandId");
                marriages["invitations"]!.AsArray().Add(entry);
                break;
            case "route-v2-missing-evidence":
                EntityId routeInvitationCommand = new("command:test/route-v2-invitation");
                EntityId routeInvitationId = CharacterMarriageIds.DeriveRomanceInvitationId(
                    date,
                    routeInvitationCommand);
                EntityId acceptanceCommand = new("command:test/route-v2-acceptance");
                entry = JsonSerializer.SerializeToNode(new RomanceRouteState(
                    CharacterMarriageContractVersions.RomanceRouteState,
                    CharacterMarriageIds.DeriveRomanceRouteId(
                        routeInvitationId,
                        acceptanceCommand),
                    first,
                    second,
                    practiceId,
                    1,
                    date,
                    0,
                    acceptanceCommand,
                    RomanceRouteStatus.Active,
                    null,
                    null,
                    null,
                    routeInvitationId,
                    first,
                    date,
                    0,
                    routeInvitationCommand,
                    date,
                    0,
                    acceptanceCommand), CanonicalJson.Options)!.AsObject();
                entry.Remove("sourceInvitationId");
                marriages["romanceRoutes"]!.AsArray().Add(entry);
                break;
            case "history-missing-count":
                entry = JsonSerializer.SerializeToNode(
                    CharacterMarriageHistoryAggregate.Empty(first),
                    CanonicalJson.Options)!.AsObject();
                entry.Remove("foldedUnionCount");
                marriages["history"]!.AsArray().Add(entry);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-ten-marriage-{mutation}.save.gz");
        WriteJsonGzip(path, current);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("future-turn")]
    [InlineData("orphan-accepted-proposal")]
    [InlineData("duplicate-proposal-outcome")]
    [InlineData("v2-level-one-false-progress")]
    [InlineData("v2-progress-reuses-acceptance")]
    public void SchemaTen_RejectsSemanticallyInvalidMarriageStateWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterMarriageSimulation()),
            CanonicalJson.Options)!.AsObject();
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        JsonObject marriages = snapshot["characterMarriages"]!.AsObject();
        switch (mutation)
        {
            case "future-turn":
                marriages["proposals"]![0]!["createdTurnIndex"] =
                    snapshot["calendar"]!["turnIndex"]!.GetValue<long>() + 1;
                break;
            case "orphan-accepted-proposal":
                marriages["unions"] = new JsonArray();
                break;
            case "duplicate-proposal-outcome":
                JsonObject duplicate = marriages["unions"]![0]!.DeepClone().AsObject();
                duplicate["unionId"]!["value"] = "marriage_union:synthetic/duplicate";
                marriages["unions"]!.AsArray().Add(duplicate);
                break;
            case "v2-level-one-false-progress":
            case "v2-progress-reuses-acceptance":
                CampaignDate date = snapshot["calendar"]!["date"]!
                    .Deserialize<CampaignDate>(CanonicalJson.Options);
                EntityId invitationCommand = new("command:synthetic/romance-invitation");
                EntityId invitationId = CharacterMarriageIds.DeriveRomanceInvitationId(
                    date,
                    invitationCommand);
                EntityId acceptanceCommand = new("command:synthetic/romance-acceptance");
                bool levelOne = mutation == "v2-level-one-false-progress";
                RomanceRouteState route = new(
                    CharacterMarriageContractVersions.RomanceRouteState,
                    CharacterMarriageIds.DeriveRomanceRouteId(
                        invitationId,
                        acceptanceCommand),
                    new EntityId("character:synthetic/child"),
                    new EntityId("character:synthetic/peer"),
                    new EntityId("marriage_practice:synthetic/default"),
                    levelOne ? 1 : 2,
                    date,
                    0,
                    acceptanceCommand,
                    RomanceRouteStatus.Active,
                    null,
                    null,
                    null,
                    invitationId,
                    new EntityId("character:synthetic/child"),
                    date,
                    0,
                    invitationCommand,
                    date,
                    0,
                    levelOne
                        ? new EntityId("command:synthetic/false-level-one-progress")
                        : acceptanceCommand);
                marriages["romanceRoutes"]!.AsArray().Add(
                    JsonSerializer.SerializeToNode(route, CanonicalJson.Options));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        WorldSnapshot invalidSnapshot = snapshot.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        invalid["checksum"] = SimulationChecksum.Compute(invalidSnapshot).Value;
        string path = Path.Combine(directory, $"schema-ten-marriage-semantic-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("duplicate-estate")]
    [InlineData("dangling-owner")]
    [InlineData("wrong-estate-namespace")]
    public void SchemaTen_RejectsSemanticallyInvalidEstateStateWithoutChangingSource(string mutation)
    {
        JsonObject current = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateCharacterEstateHoldingSimulation()),
            CanonicalJson.Options)!.AsObject();
        JsonArray holdings = current["snapshot"]!["characterEstateHoldings"]!["holdings"]!.AsArray();
        JsonObject holding = holdings[0]!.AsObject();
        switch (mutation)
        {
            case "duplicate-estate":
                holdings.Add(holding.DeepClone());
                break;
            case "dangling-owner":
                holding["ownerCharacterId"]!["value"] = "character:synthetic/missing";
                break;
            case "wrong-estate-namespace":
                holding["estateId"]!["value"] = "property:synthetic/family_manor";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-ten-estate-semantic-{mutation}.save.gz");
        WriteJsonGzip(path, current);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaTen_RejectsImpossibleRelationshipImpactWithoutChangingSource()
    {
        CampaignSimulation simulation = CreateCharacterSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/impossible-persisted-impact"),
            new EntityId("character:synthetic/child"),
            simulation.World.Calendar.Date,
            new RelationshipActionCommandPayload(
                new EntityId("character:synthetic/parent"),
                new RelationshipImpact(1, 0, 0, 0, 0, 0, 0, 0, 0),
                new EntityId("memory_meaning:test/persisted-impact"),
                10,
                MemoryPublicity.Private,
                0,
                []));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());

        JsonObject invalid = JsonSerializer.SerializeToNode(
            CreateEnvelope(simulation),
            CanonicalJson.Options)!.AsObject();
        JsonObject persistedMemory = invalid["snapshot"]!["relationships"]!["subjects"]![0]!
            ["detailedRelationships"]![0]!["memories"]![0]!.AsObject();
        persistedMemory["appliedImpact"]!["affection"] = int.MaxValue;
        WorldSnapshot invalidSnapshot = invalid["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        invalid["checksum"] = SimulationChecksum.Compute(invalidSnapshot).Value;
        string path = Path.Combine(directory, "schema-six-impossible-relationship-impact.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("missing-source-event")]
    [InlineData("missing-source-kind")]
    [InlineData("missing-identity-scheme")]
    [InlineData("missing-consequence-index")]
    [InlineData("legacy-source-field")]
    [InlineData("unsupported-memory-contract")]
    public void SchemaTen_RequiresCompleteRelationshipMemoryV2ShapeWithoutChangingSource(
        string mutation)
    {
        CampaignSimulation simulation = CreateCharacterSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/current-memory-v2"),
            new EntityId("character:synthetic/child"),
            simulation.World.Calendar.Date,
            new RelationshipActionCommandPayload(
                new EntityId("character:synthetic/parent"),
                new RelationshipImpact(0, 1, 0, 0, 0, 0, 0, 0, 0),
                new EntityId("memory_meaning:test/current_memory_v2"),
                10,
                MemoryPublicity.Private,
                0,
                []));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());
        JsonObject invalid = JsonSerializer.SerializeToNode(
            CreateEnvelope(simulation),
            CanonicalJson.Options)!.AsObject();
        JsonObject memory = invalid["snapshot"]!["relationships"]!["subjects"]![0]!
            ["detailedRelationships"]![0]!["memories"]![0]!.AsObject();
        switch (mutation)
        {
            case "missing-source-event":
                memory.Remove("sourceEventId");
                break;
            case "missing-source-kind":
                memory.Remove("sourceKind");
                break;
            case "missing-identity-scheme":
                memory.Remove("identityScheme");
                break;
            case "missing-consequence-index":
                memory.Remove("consequenceIndex");
                break;
            case "legacy-source-field":
                memory["sourceRelationshipActionEventId"] = memory["sourceEventId"]!.DeepClone();
                break;
            case "unsupported-memory-contract":
                memory["contractVersion"] = 999;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-seven-memory-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaTen_RejectsUnsupportedCareerStateWithoutChangingSource()
    {
        CampaignSimulation simulation = CreateCharacterSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/current-career-state"),
            new EntityId("character:synthetic/child"),
            simulation.World.Calendar.Date,
            new CharacterActionCommandPayload(new PatronageOfferAction(
                new EntityId("character:synthetic/parent"))));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());
        JsonObject invalid = JsonSerializer.SerializeToNode(
            CreateEnvelope(simulation),
            CanonicalJson.Options)!.AsObject();
        invalid["snapshot"]!["careers"]!["proposals"]![0]!["contractVersion"] = 999;
        string path = Path.Combine(directory, "schema-seven-unsupported-career.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("missing-account-id")]
    [InlineData("null-account-wealth")]
    [InlineData("missing-ledger-source-event")]
    [InlineData("unsupported-ledger-contract")]
    public void SchemaTen_RequiresCompleteCharacterResourceEntryShapeWithoutChangingSource(
        string mutation)
    {
        CampaignSimulation simulation = CreateCharacterResourceSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/current-resource-shape"),
            new EntityId("character:synthetic/child"),
            simulation.World.Calendar.Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(
                new EntityId("character:synthetic/parent"),
                1)));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());
        JsonObject invalid = JsonSerializer.SerializeToNode(
            CreateEnvelope(simulation),
            CanonicalJson.Options)!.AsObject();
        JsonObject resources = invalid["snapshot"]!["characterResources"]!.AsObject();
        switch (mutation)
        {
            case "missing-account-id":
                resources["accounts"]![0]!.AsObject().Remove("accountId");
                break;
            case "null-account-wealth":
                resources["accounts"]![0]!["wealth"] = null;
                break;
            case "missing-ledger-source-event":
                resources["ledgerEntries"]![0]!.AsObject().Remove("sourceEventId");
                break;
            case "unsupported-ledger-contract":
                resources["ledgerEntries"]![0]!["contractVersion"] = 999;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-eight-resource-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
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
        Assert.Contains("character", recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(malformedBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void MalformedRelationshipPrimary_RemainsUntouchedAndRecoversNewestValidGeneration()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "malformed-relationship-autosave.save.gz");
        SaveEnvelope validGeneration = CreateEnvelope(CreateSimulation());
        store.SaveAutosave(path, validGeneration);
        store.SaveAutosave(path, CreateEnvelope(CreateSimulation()));

        JsonObject malformed = JsonSerializer.SerializeToNode(
            CreateEnvelope(CreateSimulation()),
            CanonicalJson.Options)!.AsObject();
        malformed["snapshot"]!["relationships"]!["subjects"]!.AsArray().Add(null);
        WriteJsonGzip(path, malformed);
        byte[] malformedBytes = File.ReadAllBytes(path);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(validGeneration.Checksum, recovered.Envelope.Checksum);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.Contains("relationship", recovered.RecoveryDiagnostic, StringComparison.OrdinalIgnoreCase);
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
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
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
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaThree_MigratesEmptyCharactersAndRelationshipsWithoutOverwritingSource()
    {
        string path = Path.Combine(directory, "schema-three.save.gz");
        WriteFrozenHistoricalFixture(path, 3);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                "simulation.characters",
                CharacterContractVersions.Snapshot));
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                "simulation.relationships",
                RelationshipContractVersions.Snapshot));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFour_AuthenticatesNonemptyCharactersAndMigratesEmptyRelationshipsWithoutChangingSource()
    {
        string path = Path.Combine(directory, "schema-four.save.gz");
        WriteFrozenHistoricalFixture(path, 4);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        CharacterDefinition character = Assert.Single(migrated.Snapshot.Characters.CharacterDefinitions);
        Assert.Equal(new EntityId("character:synthetic/adult"), character.Id);
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                "simulation.relationships",
                RelationshipContractVersions.Snapshot));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFive_AuthenticatesAndMigratesDescriptorConditionAndLegacyKinshipWithoutChangingSource()
    {
        string path = Path.Combine(directory, "schema-five.save.gz");
        WriteFrozenHistoricalFixture(path, 5);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                "simulation.characters",
                CharacterContractVersions.Snapshot));
        CharacterDefinition child = Assert.Single(
            migrated.Snapshot.Characters.CharacterDefinitions,
            definition => definition.Id == new EntityId("character:synthetic/child"));
        Assert.Equal(child.NameKey, child.StructuredName!.PrimaryNameKey);
        Assert.Null(child.StructuredName.CourtesyNameKey);
        Assert.Equal(CharacterOriginKind.LegacyUnknown, child.ContentOrigin!.OriginKind);
        Assert.Equal(child.Id, child.ContentOrigin.RecordId);
        Assert.Null(child.CultureId);
        Assert.Null(child.OriginLocationId);
        Assert.Empty(child.FlawIds!);

        CharacterState state = Assert.Single(
            migrated.Snapshot.Characters.CharacterStates,
            item => item.CharacterId == child.Id);
        CharacterParentLink parent = Assert.Single(state.ParentLinks!);
        Assert.Equal(new EntityId("character:synthetic/parent"), parent.ParentCharacterId);
        Assert.Equal(ParentChildLinkKind.UnspecifiedLegacy, parent.Kind);
        Assert.Equal(CharacterConditionState.Default, state.Condition);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaSix_AuthenticatesAndMigratesNonemptyC0StateWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(6);
        Assert.Equal(FrozenSchemaSixChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 6);
        string path = Path.Combine(directory, "schema-six-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 6);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                "simulation.relationships",
                RelationshipContractVersions.Snapshot));
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                "simulation.character_careers",
                CareerContractVersions.Snapshot));
        Assert.Empty(migrated.Snapshot.Careers.Proposals);

        CharacterState child = Assert.Single(
            migrated.Snapshot.Characters.CharacterStates,
            state => state.CharacterId == new EntityId("character:synthetic/child"));
        Assert.Equal(CharacterVitalStatus.Alive, child.Condition!.VitalStatus);
        CharacterParentLink parent = Assert.Single(child.ParentLinks!);
        Assert.Equal(ParentChildLinkKind.UnspecifiedLegacy, parent.Kind);

        ConsequentialMemory memory = Assert.Single(
            Assert.Single(
                Assert.Single(migrated.Snapshot.Relationships.Subjects)
                    .DetailedRelationships).Memories);
        Assert.Equal(
            new EntityId("memory:sha256/3b4399ee0274b525d1912ec06ed2d7865a2de9ede6484ae58fb912c4ce8f1d9f"),
            memory.MemoryId);
        Assert.Equal(RelationshipContractVersions.Memory, memory.ContractVersion);
        Assert.Equal(RelationshipMemorySourceKind.RelationshipAction, memory.SourceKind);
        Assert.Equal(
            RelationshipMemoryIdentityScheme.LegacyRelationshipActionV1,
            memory.IdentityScheme);
        Assert.Equal(0, memory.ConsequenceIndex);

        CampaignCommand diagnosticCommand = Assert.Single(migrated.DiagnosticCommands);
        Assert.IsType<RelationshipActionCommandPayload>(diagnosticCommand.Payload);
        RelationshipActionResolvedEventPayload diagnosticEvent = Assert.IsType<
            RelationshipActionResolvedEventPayload>(Assert.Single(migrated.DiagnosticEvents).Payload);
        Assert.Equal(memory.MemoryId, diagnosticEvent.Memory.MemoryId);
        Assert.Equal(memory.SourceEventId, diagnosticEvent.Memory.SourceEventId);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaSeven_AuthenticatesAndMigratesNonemptyC1StateWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(7);
        Assert.Equal(FrozenSchemaSevenChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 7);
        WorldSnapshot historical = frozen["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        string path = Path.Combine(directory, "schema-seven-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 7);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                CharacterResourceSystem.SystemId,
                CharacterResourceSystem.Version));
        Assert.Empty(migrated.Snapshot.CharacterResources.Accounts);
        Assert.Empty(migrated.Snapshot.CharacterResources.LedgerEntries);
        Assert.Empty(migrated.Snapshot.CharacterResources.History);
        Assert.Equal(
            SerializeCharactersWithoutEducation(historical.Characters),
            SerializeCharactersWithoutEducation(migrated.Snapshot.Characters));
        Assert.Equal(
            JsonSerializer.Serialize(historical.Relationships, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Relationships, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.Careers, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Careers, CanonicalJson.Options));
        Assert.Single(migrated.Snapshot.Careers.RetinueMemberships);
        Assert.Equal(2, migrated.DiagnosticCommands.Count);
        Assert.Equal(2, migrated.DiagnosticEvents.Count);
        Assert.All(migrated.DiagnosticCommands, command =>
            Assert.IsType<CharacterActionCommandPayload>(command.Payload));
        Assert.All(migrated.DiagnosticEvents, campaignEvent =>
            Assert.IsType<CharacterActionResolvedEventPayload>(campaignEvent.Payload));
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaEight_AuthenticatesAndMigratesNonemptyC2StateWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(8);
        Assert.Equal(FrozenSchemaEightChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 8);
        WorldSnapshot historical = frozen["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-eight-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 8);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);
        WorldSnapshot normalizedPreC3 = migrated.Snapshot with
        {
            SystemVersions = migrated.Snapshot.SystemVersions
                .Where(version => version.SystemId != CharacterEstateHoldingSystem.SystemId
                    && version.SystemId != CharacterMarriageSystem.SystemId
                    && version.SystemId != CharacterGuardianshipSystem.SystemId
                    && version.SystemId != CharacterPregnancySystem.SystemId)
                .ToArray(),
            CharacterEstateHoldings = CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriages = CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianships = CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancies = CharacterPregnancyWorldSnapshot.Empty,
        };

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(frozen["contractVersion"]!.GetValue<int>(), migrated.ContractVersion);
        Assert.Equal(frozen["gameVersion"]!.GetValue<string>(), migrated.GameVersion);
        Assert.Equal(
            DateTimeOffset.Parse(
                frozen["createdUtc"]!.GetValue<string>(),
                System.Globalization.CultureInfo.InvariantCulture),
            migrated.CreatedUtc);
        Assert.Equal(frozen["seed"]!.GetValue<ulong>(), migrated.Seed);
        Assert.Equal(
            JsonSerializer.Serialize(frozen["contentManifests"], CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.ContentManifests, CanonicalJson.Options));
        Assert.Equal(
            SerializeWithoutCharacterEducation(historical),
            SerializeWithoutCharacterEducation(normalizedPreC3));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                CharacterEstateHoldingSystem.SystemId,
                CharacterEstateHoldingSystem.Version));
        Assert.Empty(migrated.Snapshot.CharacterEstateHoldings.Holdings);
        Assert.Equal(
            SerializeCharactersWithoutEducation(historical.Characters),
            SerializeCharactersWithoutEducation(migrated.Snapshot.Characters));
        Assert.Equal(
            JsonSerializer.Serialize(historical.Relationships, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Relationships, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.Careers, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Careers, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.CharacterResources, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.CharacterResources, CanonicalJson.Options));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Equal(65, migrated.DiagnosticCommands.Count);
        Assert.Equal(65, migrated.DiagnosticEvents.Count);
        Assert.All(migrated.DiagnosticCommands, command =>
            Assert.IsType<CharacterResourceActionCommandPayload>(command.Payload));
        Assert.All(migrated.DiagnosticEvents, campaignEvent =>
            Assert.IsType<CharacterResourceActionResolvedEventPayload>(campaignEvent.Payload));
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaNine_AuthenticatesAndMigratesExactC3StateWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(9);
        Assert.Equal(FrozenSchemaNineChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 9);
        WorldSnapshot historical = frozen["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-nine-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 9);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);
        WorldSnapshot normalizedPreD0 = migrated.Snapshot with
        {
            SystemVersions = migrated.Snapshot.SystemVersions
                .Where(version => version.SystemId != CharacterMarriageSystem.SystemId
                    && version.SystemId != CharacterGuardianshipSystem.SystemId
                    && version.SystemId != CharacterPregnancySystem.SystemId)
                .ToArray(),
            CharacterMarriages = CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianships = CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancies = CharacterPregnancyWorldSnapshot.Empty,
        };

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(frozen["contractVersion"]!.GetValue<int>(), migrated.ContractVersion);
        Assert.Equal(frozen["gameVersion"]!.GetValue<string>(), migrated.GameVersion);
        Assert.Equal(
            DateTimeOffset.Parse(
                frozen["createdUtc"]!.GetValue<string>(),
                System.Globalization.CultureInfo.InvariantCulture),
            migrated.CreatedUtc);
        Assert.Equal(frozen["seed"]!.GetValue<ulong>(), migrated.Seed);
        Assert.Equal(
            JsonSerializer.Serialize(frozen["contentManifests"], CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.ContentManifests, CanonicalJson.Options));
        Assert.Equal(
            SerializeWithoutCharacterEducation(historical),
            SerializeWithoutCharacterEducation(normalizedPreD0));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Contains(migrated.Snapshot.SystemVersions, version =>
            version == new SystemVersion(
                CharacterMarriageSystem.SystemId,
                CharacterMarriageSystem.Version));
        Assert.Empty(migrated.Snapshot.CharacterMarriages.Practices);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.Proposals);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.Betrothals);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.Unions);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.RomanceRoutes);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.History);
        Assert.Equal(
            JsonSerializer.Serialize(historical.CharacterEstateHoldings, CanonicalJson.Options),
            JsonSerializer.Serialize(
                migrated.Snapshot.CharacterEstateHoldings,
                CanonicalJson.Options));
        CharacterEstateHoldingState preservedHolding = Assert.Single(
            migrated.Snapshot.CharacterEstateHoldings.Holdings);
        Assert.Equal(new EntityId("estate:fixture/leader_manor"), preservedHolding.EstateId);
        Assert.Equal(new EntityId("character:fixture/leader"), preservedHolding.OwnerCharacterId);
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Equal(65, migrated.DiagnosticCommands.Count);
        Assert.Equal(65, migrated.DiagnosticEvents.Count);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaTen_AuthenticatesAndMigratesExactD0StateWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(10);
        Assert.Equal(FrozenSchemaTenChecksum, frozen["checksum"]!.GetValue<string>());
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 10);
        WorldSnapshot historical = frozen["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-ten-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 10);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(frozen["contractVersion"]!.GetValue<int>(), migrated.ContractVersion);
        Assert.Equal(frozen["gameVersion"]!.GetValue<string>(), migrated.GameVersion);
        Assert.Equal(
            DateTimeOffset.Parse(
                frozen["createdUtc"]!.GetValue<string>(),
                System.Globalization.CultureInfo.InvariantCulture),
            migrated.CreatedUtc);
        Assert.Equal(frozen["seed"]!.GetValue<ulong>(), migrated.Seed);
        Assert.Equal(
            JsonSerializer.Serialize(frozen["contentManifests"], CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.ContentManifests, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.CharacterMarriages.Practices, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.CharacterMarriages.Practices, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.CharacterMarriages.Proposals, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.CharacterMarriages.Proposals, CanonicalJson.Options));
        Assert.Equal(
            JsonSerializer.Serialize(historical.CharacterMarriages.Unions, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.CharacterMarriages.Unions, CanonicalJson.Options));
        Assert.Equal(CharacterMarriageContractVersions.Snapshot, migrated.Snapshot.CharacterMarriages.ContractVersion);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.Invitations);
        Assert.Contains(
            migrated.Snapshot.SystemVersions,
            item => item.SystemId == CharacterMarriageSystem.SystemId
                && item.Version == CharacterMarriageSystem.Version);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        Assert.Single(migrated.Snapshot.CharacterMarriages.Practices);
        MarriageProposalState proposal = Assert.Single(
            migrated.Snapshot.CharacterMarriages.Proposals);
        MarriageUnionState union = Assert.Single(migrated.Snapshot.CharacterMarriages.Unions);
        Assert.Equal(MarriageProposalStatus.Accepted, proposal.Status);
        Assert.Equal(MarriageConsentKind.PoliticalArrangement, union.ConsentKind);
        Assert.Equal(proposal.ProposalId, union.SourceProposalId);
        Assert.Single(migrated.Snapshot.CharacterEstateHoldings.Holdings);
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Equal(65, migrated.DiagnosticCommands.Count);
        Assert.Equal(65, migrated.DiagnosticEvents.Count);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaEleven_AuthenticatesExactD1FixtureAndMigratesLegacyRoutesWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(11);
        Assert.Equal(FrozenSchemaElevenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-11-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(325_473, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaElevenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 11);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-eleven-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 11);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(CharacterMarriageContractVersions.Snapshot, migrated.Snapshot.CharacterMarriages.ContractVersion);
        Assert.Empty(migrated.Snapshot.CharacterMarriages.Invitations);
        RomanceRouteState[] routes = migrated.Snapshot.CharacterMarriages.RomanceRoutes.ToArray();
        Assert.Equal(2, routes.Length);
        Assert.All(routes, route =>
        {
            Assert.Equal(CharacterMarriageContractVersions.State, route.ContractVersion);
            Assert.Null(route.SourceInvitationId);
            Assert.Null(route.LastPositiveProgressCommandId);
        });
        Assert.Contains(routes, route => route.Status == RomanceRouteStatus.Active);
        Assert.Contains(routes, route => route.Status == RomanceRouteStatus.Ended);
        Assert.Single(migrated.Snapshot.CharacterMarriages.Unions);
        Assert.Single(migrated.Snapshot.CharacterEstateHoldings.Holdings);
        Assert.IsType<CharacterMarriageActionCommandPayload>(
            Assert.Single(migrated.Snapshot.PendingCommands).Payload);
        Assert.Contains(
            migrated.DiagnosticCommands,
            command => command.Payload is CharacterMarriageActionCommandPayload);
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterMarriageActionResolvedEventPayload);
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        WorldState restored = WorldState.Restore(migrated.Snapshot);
        RomanceRouteState activeRoute = routes.Single(
            route => route.Status == RomanceRouteStatus.Active);
        EntityId commandId = new("command:test/schema11-legacy-route-advance");
        EntityId eventId = CharacterMarriageIds.DeriveActionEventId(
            restored.Calendar.Date,
            commandId);
        CharacterMarriageActionResolvedEventPayload legacyAdvance =
            restored.CharacterMarriages.PlanAction(
                activeRoute.FirstCharacterId,
                new CharacterMarriageActionCommandPayload(
                    new AdvanceRomanceRouteAction(
                        activeRoute.RouteId,
                        activeRoute.ProgressLevel)),
                restored.Calendar.Date,
                restored.Calendar.TurnIndex,
                commandId,
                eventId);
        Assert.IsType<RomanceRouteAdvancedOutcome>(legacyAdvance.Outcome);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaTwelve_AuthenticatesExactD2FixtureAndMigratesVocabularyWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(12);
        Assert.Equal(FrozenSchemaTwelveChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-12-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(345_155, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaTwelveFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 12);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-twelve-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 12);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterGuardianships(migrated.Snapshot));
        Assert.Contains(
            migrated.DiagnosticCommands,
            command => command.Payload is CharacterMarriageActionCommandPayload
            {
                Action: EndRomanceRouteAction,
            });
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterMarriageActionResolvedEventPayload
            {
                Outcome: RomanceRouteEndedOutcome,
            });
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterMarriageActionCommandPayload
            {
                Action: WithdrawRomanceInvitationAction,
            });
        Assert.Single(migrated.Snapshot.CharacterMarriages.Invitations);
        RomanceRouteState versionTwoRoute = Assert.Single(
            migrated.Snapshot.CharacterMarriages.RomanceRoutes,
            route => route.ContractVersion
                == CharacterMarriageContractVersions.RomanceRouteState);
        Assert.Equal(RomanceRouteStatus.Completed, versionTwoRoute.Status);
        Assert.NotNull(versionTwoRoute.SourceInvitationId);
        Assert.NotNull(versionTwoRoute.InvitationInitiatorCharacterId);
        Assert.NotNull(versionTwoRoute.InvitationCreatedDate);
        Assert.NotNull(versionTwoRoute.InvitationCreatedTurnIndex);
        Assert.NotNull(versionTwoRoute.InvitationSourceCommandId);
        Assert.NotNull(versionTwoRoute.LastPositiveProgressDate);
        Assert.NotNull(versionTwoRoute.LastPositiveProgressTurnIndex);
        Assert.NotNull(versionTwoRoute.LastPositiveProgressCommandId);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pending-condition")]
    [InlineData("diagnostic-household")]
    [InlineData("diagnostic-coercion")]
    [InlineData("diagnostic-marriage-consequence-property")]
    [InlineData("relationship-source")]
    public void SchemaTwelve_RejectsD3DiscriminatorsAndRelationshipSourcesWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(12);
        switch (mutation)
        {
            case "pending-condition":
                invalid["snapshot"]!["pendingCommands"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "character_condition_action.v1",
                    },
                });
                break;
            case "diagnostic-household":
                invalid["diagnosticEvents"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "household_decision_resolved.v1",
                    },
                });
                break;
            case "diagnostic-coercion":
                invalid["diagnosticCommands"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "character_marriage_action.v1",
                        ["action"] = new JsonObject
                        {
                            ["$type"] = "impose_coerced_union.v1",
                        },
                    },
                });
                break;
            case "diagnostic-marriage-consequence-property":
                JsonObject marriageEvent = invalid["diagnosticEvents"]!
                    .AsArray()
                    .OfType<JsonObject>()
                    .First(item => item["payload"]?["$type"]?.GetValue<string>()
                        == "character_marriage_action_resolved.v1");
                marriageEvent["payload"]!["relationshipMemoryConsequence"] = null;
                break;
            case "relationship-source":
                invalid["snapshot"]!["relationships"]!["subjects"]![0]![
                    "detailedRelationships"]![0]!["memories"]![0]!["sourceKind"] =
                    (int)RelationshipMemorySourceKind.HouseholdDecision;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-twelve-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaThirteen_AuthenticatesExactD3FixtureAndMigratesVocabularyWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(13);
        Assert.Equal(FrozenSchemaThirteenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-13-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(52_467, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaThirteenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 13);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-thirteen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 13);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterGuardianships(migrated.Snapshot));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterConditionActionCommandPayload);
        Assert.Contains(
            migrated.DiagnosticCommands,
            command => command.Payload is HouseholdDecisionCommandPayload);
        Assert.Contains(
            migrated.DiagnosticCommands,
            command => command.Payload is CharacterMarriageActionCommandPayload
            {
                Action: ImposeCoercedUnionAction,
            });
        RelationshipMemorySourceKind[] sourceKinds = migrated.Snapshot.Relationships.Subjects
            .SelectMany(subject => subject.DetailedRelationships)
            .SelectMany(relationship => relationship.Memories)
            .Select(memory => memory.SourceKind)
            .Distinct()
            .Order()
            .ToArray();
        Assert.Contains(RelationshipMemorySourceKind.CharacterCondition, sourceKinds);
        Assert.Contains(RelationshipMemorySourceKind.HouseholdDecision, sourceKinds);
        Assert.Contains(RelationshipMemorySourceKind.CharacterMarriageAction, sourceKinds);
        CharacterState grandfatheredAdoptee = Assert.Single(
            migrated.Snapshot.Characters.CharacterStates,
            state => state.CharacterId == new EntityId("character:d3/009"));
        CharacterParentLink grandfatheredLink = Assert.Single(
            grandfatheredAdoptee.ParentLinks!,
            link => link.Kind == ParentChildLinkKind.LegalAdoptive);
        Assert.Equal(new EntityId("character:d3/008"), grandfatheredLink.ParentCharacterId);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);

        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        EntityId adoptedCharacterId = new("character:d3/007");
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            adoptedCharacterId,
            out AuthoritativeCharacterProfile? adoptedBefore));
        CampaignCommand adoption = CampaignCommand.Create(
            new EntityId("command:e0/migrated-schema13-adoption"),
            CharacterFamilySystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterFamilyActionCommandPayload(
                new EstablishLegalAdoptiveParentAction(
                    new EntityId("character:d3/008"),
                    adoptedCharacterId,
                    adoptedBefore.ParentLinks)));
        Assert.True(simulation.Submit(adoption).IsValid);
        CampaignEvent adoptionEvent = Assert.Single(
            simulation.ResolveTurn(),
            campaignEvent => campaignEvent.Payload
                is CharacterFamilyActionResolvedEventPayload);
        Assert.IsType<LegalAdoptiveParentEstablishedOutcome>(
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(
                adoptionEvent.Payload).Outcome);
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            adoptedCharacterId,
            out AuthoritativeCharacterProfile? adoptedAfter));
        Assert.Contains(
            adoptedAfter.ParentLinks,
            link => link.ParentCharacterId == new EntityId("character:d3/008")
                && link.Kind == ParentChildLinkKind.LegalAdoptive);

        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pending-command")]
    [InlineData("nested-action")]
    [InlineData("nested-outcome")]
    [InlineData("diagnostic-event")]
    [InlineData("explicit-null-property")]
    [InlineData("previous-links-property")]
    [InlineData("current-links-property")]
    public void SchemaThirteen_RejectsE0VocabularyAndPropertiesWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(13);
        switch (mutation)
        {
            case "pending-command":
                invalid["snapshot"]!["pendingCommands"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "character_family_action.v1",
                    },
                });
                break;
            case "nested-action":
                invalid["diagnosticCommands"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "character_condition_action.v1",
                        ["action"] = new JsonObject
                        {
                            ["$type"] = "establish_legal_adoptive_parent.v1",
                        },
                    },
                });
                break;
            case "nested-outcome":
                invalid["diagnosticEvents"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "character_condition_action_resolved.v1",
                        ["outcome"] = new JsonObject
                        {
                            ["$type"] = "legal_adoptive_parent_established.v1",
                        },
                    },
                });
                break;
            case "diagnostic-event":
                invalid["diagnosticEvents"]!.AsArray().Add(new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["$type"] = "character_family_action_resolved.v1",
                    },
                });
                break;
            case "explicit-null-property":
                invalid["diagnosticCommands"]![0]!["payload"]!["action"]![
                    "expectedCurrentParentLinks"] = null;
                break;
            case "previous-links-property":
                invalid["diagnosticEvents"]![0]!["payload"]![
                    "previousParentLinks"] = null;
                break;
            case "current-links-property":
                invalid["diagnosticEvents"]![0]!["payload"]![
                    "currentParentLinks"] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-thirteen-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFourteen_AuthenticatesExactE0FixtureAndAddsEmptyGuardianshipsWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(14);
        Assert.Equal(FrozenSchemaFourteenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-14-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(56_635, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaFourteenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 14);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-fourteen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 14);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterGuardianships(migrated.Snapshot));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterFamilyActionCommandPayload
            {
                Action: EstablishLegalAdoptiveParentAction,
            });
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterFamilyActionResolvedEventPayload
            {
                Outcome: LegalAdoptiveParentEstablishedOutcome,
            });
        Assert.Empty(migrated.Snapshot.CharacterGuardianships.Guardianships);
        Assert.Contains(
            migrated.Snapshot.SystemVersions,
            version => version == new SystemVersion(
                CharacterGuardianshipSystem.SystemId,
                CharacterGuardianshipSystem.Version));
        Assert.Equal(
            SimulationChecksum.Compute(migrated.Snapshot).Value,
            migrated.Checksum);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("nested-action")]
    [InlineData("nested-outcome")]
    [InlineData("explicit-null-expected")]
    [InlineData("explicit-null-guardianship")]
    [InlineData("snapshot-state")]
    [InlineData("system-version")]
    public void SchemaFourteen_RejectsE1VocabularyStateAndPropertiesWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(14);
        JsonObject familyCommand = invalid["diagnosticCommands"]!.AsArray()
            .OfType<JsonObject>()
            .First(command => command["payload"]?["$type"]?.GetValue<string>()
                == "character_family_action.v1");
        JsonObject familyEvent = invalid["diagnosticEvents"]!.AsArray()
            .OfType<JsonObject>()
            .First(campaignEvent => campaignEvent["payload"]?["$type"]?.GetValue<string>()
                == "character_family_action_resolved.v1");
        switch (mutation)
        {
            case "nested-action":
                familyCommand["payload"]!["action"]!["$type"] =
                    "establish_primary_guardianship.v1";
                break;
            case "nested-outcome":
                familyEvent["payload"]!["outcome"]!["$type"] =
                    "primary_guardianship_established.v1";
                break;
            case "explicit-null-expected":
                familyCommand["payload"]!["action"]![
                    "expectedCurrentPrimaryGuardianshipId"] = null;
                break;
            case "explicit-null-guardianship":
                familyEvent["payload"]!["outcome"]!["guardianship"] = null;
                break;
            case "snapshot-state":
                invalid["snapshot"]!["characterGuardianships"] = new JsonObject
                {
                    ["contractVersion"] = CharacterGuardianshipContractVersions.Snapshot,
                    ["guardianships"] = new JsonArray(),
                };
                break;
            case "system-version":
                invalid["snapshot"]!["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterGuardianshipSystem.SystemId,
                    ["version"] = CharacterGuardianshipSystem.Version,
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-fourteen-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFifteen_AuthenticatesExactE1FixtureAndMigratesVocabularyWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(15);
        Assert.Equal(FrozenSchemaFifteenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-15-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(61_267, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaFifteenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 15);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-fifteen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 15);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterPregnancies(migrated.Snapshot));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterFamilyActionCommandPayload
            {
                Action: EstablishPrimaryGuardianshipAction,
            });
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterFamilyActionResolvedEventPayload
            {
                Outcome: PrimaryGuardianshipEstablishedOutcome,
            });
        Assert.Contains(
            migrated.Snapshot.CharacterGuardianships.Guardianships,
            guardianship => guardianship.Status == CharacterGuardianshipStatus.Active);
        Assert.Contains(
            migrated.Snapshot.CharacterGuardianships.Guardianships,
            guardianship => guardianship.Status == CharacterGuardianshipStatus.Ended
                && guardianship.EndReason == CharacterGuardianshipEndReason.Replaced);
        Assert.Equal(
            SimulationChecksum.Compute(migrated.Snapshot).Value,
            migrated.Checksum);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("nested-action")]
    [InlineData("nested-outcome")]
    [InlineData("nested-replace-action")]
    [InlineData("nested-replace-outcome")]
    [InlineData("explicit-null-replacement-guardian")]
    [InlineData("explicit-null-ended-guardianship")]
    [InlineData("explicit-null-replacement-guardianship")]
    [InlineData("explicit-null-end-reason")]
    public void SchemaFifteen_RejectsE2VocabularyAndPropertiesWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(15);
        JsonObject familyCommand = invalid["diagnosticCommands"]!.AsArray()
            .OfType<JsonObject>()
            .First(command => command["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "establish_primary_guardianship.v1");
        JsonObject familyEvent = invalid["diagnosticEvents"]!.AsArray()
            .OfType<JsonObject>()
            .First(campaignEvent => campaignEvent["payload"]?["outcome"]?["$type"]?.GetValue<string>()
                == "primary_guardianship_established.v1");
        switch (mutation)
        {
            case "nested-action":
                familyCommand["payload"]!["action"]!["$type"] =
                    "end_primary_guardianship.v1";
                break;
            case "nested-outcome":
                familyEvent["payload"]!["outcome"]!["$type"] =
                    "primary_guardianship_ended.v1";
                break;
            case "nested-replace-action":
                familyCommand["payload"]!["action"]!["$type"] =
                    "replace_primary_guardianship.v1";
                break;
            case "nested-replace-outcome":
                familyEvent["payload"]!["outcome"]!["$type"] =
                    "primary_guardianship_replaced.v1";
                break;
            case "explicit-null-replacement-guardian":
                familyCommand["payload"]!["action"]![
                    "replacementGuardianCharacterId"] = null;
                break;
            case "explicit-null-ended-guardianship":
                familyEvent["payload"]!["outcome"]!["endedGuardianship"] = null;
                break;
            case "explicit-null-replacement-guardianship":
                familyEvent["payload"]!["outcome"]!["replacementGuardianship"] = null;
                break;
            case "explicit-null-end-reason":
                familyCommand["payload"]!["action"]!["endReason"] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-fifteen-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaSixteen_AuthenticatesExactE2FixtureAndMigratesVocabularyWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(16);
        Assert.Equal(FrozenSchemaSixteenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-16-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(64_549, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaSixteenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 16);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-sixteen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 16);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterPregnancies(migrated.Snapshot));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Contains(
            migrated.DiagnosticCommands,
            command => command.Payload is CharacterFamilyActionCommandPayload
            {
                Action: EndPrimaryGuardianshipAction,
            });
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterFamilyActionResolvedEventPayload
            {
                Outcome: PrimaryGuardianshipEndedOutcome,
            });
        Assert.Contains(
            migrated.Snapshot.CharacterGuardianships.Guardianships,
            guardianship => guardianship.Status == CharacterGuardianshipStatus.Ended
                && guardianship.EndReason == CharacterGuardianshipEndReason.Revoked);
        Assert.Contains(
            migrated.Snapshot.CharacterGuardianships.Guardianships,
            guardianship => guardianship.Status == CharacterGuardianshipStatus.Ended
                && guardianship.EndReason == CharacterGuardianshipEndReason.Replaced);
        Assert.Equal(
            SimulationChecksum.Compute(migrated.Snapshot).Value,
            migrated.Checksum);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaSixteen_AllowsExistingWardCameOfAgeTerminalReason()
    {
        JsonObject compatible = CreateHistoricalFixture(16);
        JsonObject terminal = compatible["snapshot"]!["characterGuardianships"]![
            "guardianships"]!.AsArray().OfType<JsonObject>().First();
        terminal["endReason"] = (int)CharacterGuardianshipEndReason.WardCameOfAge;
        WorldSnapshot historical = compatible["snapshot"]!.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())!;
        compatible["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            historical,
            16).Value;
        string path = Path.Combine(directory, "schema-sixteen-coming-of-age-reason.save.gz");
        WriteJsonGzip(path, compatible);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Contains(
            migrated.Snapshot.CharacterGuardianships.Guardianships,
            guardianship => guardianship.EndReason
                == CharacterGuardianshipEndReason.WardCameOfAge);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pending-command")]
    [InlineData("diagnostic-command")]
    [InlineData("diagnostic-event")]
    [InlineData("explicit-null-expectation")]
    [InlineData("explicit-null-ended-guardianship")]
    public void SchemaSixteen_RejectsE3VocabularyAndPropertiesWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(16);
        CampaignDate resolutionDate = new(200, 6, 24);
        EntityId characterId = new("character:d3/000");
        EntityId commandId = CharacterComingOfAgeIds.DeriveCommandId(
            resolutionDate,
            characterId);
        CharacterComingOfAgeCommandPayload commandPayload = new(characterId, null);
        CharacterCameOfAgeEventPayload eventPayload = new(characterId, null);
        JsonObject serializedCommandPayload = JsonSerializer.SerializeToNode<ICampaignCommandPayload>(
            commandPayload,
            SimulationJson.CreateOptions())!.AsObject();
        JsonObject serializedEventPayload = JsonSerializer.SerializeToNode<ICampaignEventPayload>(
            eventPayload,
            SimulationJson.CreateOptions())!.AsObject();
        switch (mutation)
        {
            case "pending-command":
                CampaignCommand future = CampaignCommand.Create(
                    commandId,
                    CharacterComingOfAgeSystem.AuthoritativeActorId,
                    resolutionDate,
                    commandPayload,
                    ResolutionPhase.Systems,
                    CharacterComingOfAgeSystem.Priority);
                invalid["snapshot"]!["pendingCommands"]!.AsArray().Add(
                    JsonSerializer.SerializeToNode(future, SimulationJson.CreateOptions()));
                WorldSnapshot snapshot = invalid["snapshot"]!.Deserialize<WorldSnapshot>(
                    SimulationJson.CreateOptions())!;
                invalid["checksum"] = SimulationChecksum.ComputeForSaveSchema(
                    snapshot,
                    16).Value;
                break;
            case "diagnostic-command":
                invalid["diagnosticCommands"]!.AsArray()[0]!["payload"] =
                    serializedCommandPayload;
                break;
            case "diagnostic-event":
                invalid["diagnosticEvents"]!.AsArray()[0]!["payload"] =
                    serializedEventPayload;
                break;
            case "explicit-null-expectation":
                invalid["diagnosticCommands"]!.AsArray()[0]!["payload"]![
                    "expectedActivePrimaryGuardianshipId"] = null;
                break;
            case "explicit-null-ended-guardianship":
                invalid["diagnosticEvents"]!.AsArray()[0]!["payload"]![
                    "endedPrimaryGuardianship"] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-sixteen-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaSeventeen_AuthenticatesExactE3FixtureAndAddsEmptyPregnancies()
    {
        JsonObject frozen = CreateHistoricalFixture(17);
        Assert.Equal(FrozenSchemaSeventeenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-17-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(68_407, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaSeventeenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 17);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-seventeen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 17);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterPregnancies(migrated.Snapshot));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterFamilyActionCommandPayload
            {
                Action: EstablishLegalAdoptiveParentAction,
            });
        CharacterCameOfAgeEventPayload cameOfAge = Assert.IsType<
            CharacterCameOfAgeEventPayload>(migrated.DiagnosticEvents.Single(
                item => item.Payload is CharacterCameOfAgeEventPayload).Payload);
        Assert.Equal(new EntityId("character:d3/009"), cameOfAge.CharacterId);
        Assert.Equal(
            CharacterGuardianshipEndReason.WardCameOfAge,
            cameOfAge.EndedPrimaryGuardianship!.EndReason);
        Assert.Contains(
            migrated.Snapshot.CharacterGuardianships.Guardianships,
            item => item.GuardianshipId
                    == cameOfAge.EndedPrimaryGuardianship.GuardianshipId
                && item.Status == CharacterGuardianshipStatus.Ended
                && item.EndReason == CharacterGuardianshipEndReason.WardCameOfAge);
        Assert.Empty(migrated.Snapshot.CharacterPregnancies.ActivePregnancies);
        Assert.Contains(
            migrated.Snapshot.SystemVersions,
            version => version == new SystemVersion(
                CharacterPregnancySystem.SystemId,
                CharacterPregnancySystem.Version));
        Assert.Equal(
            SimulationChecksum.Compute(migrated.Snapshot).Value,
            migrated.Checksum);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("snapshot-state")]
    [InlineData("snapshot-explicit-null")]
    [InlineData("system-registration")]
    [InlineData("pending-action")]
    [InlineData("diagnostic-action")]
    [InlineData("diagnostic-outcome")]
    [InlineData("explicit-null-expectation")]
    [InlineData("explicit-null-outcome")]
    public void SchemaSeventeen_RejectsAllE4StateVocabularyAndPropertiesWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(17);
        CampaignDate resolutionDate = new(200, 6, 28);
        EntityId firstParent = new("character:d3/000");
        EntityId secondParent = new("character:d3/004");
        EntityId unionId = new(
            "marriage_union:sha256/6295da926eaf162265f5b910b0c54a1107b55627544f40d64768cb060a4e53e4");
        EntityId commandId = new("command:test/schema-seventeen-e4");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            resolutionDate,
            commandId);
        RegisterActivePregnancyAction action = new(
            firstParent,
            secondParent,
            unionId,
            null);
        CharacterPregnancyState pregnancy = new(
            CharacterPregnancyContractVersions.State,
            CharacterPregnancyIds.DerivePregnancyId(
                eventId,
                firstParent,
                secondParent,
                unionId),
            firstParent,
            secondParent,
            unionId,
            resolutionDate,
            resolutionDate.AddDays(CharacterPregnancyLimits.GestationDays),
            14,
            commandId,
            eventId);
        CharacterFamilyActionCommandPayload commandPayload = new(action);
        CharacterFamilyActionResolvedEventPayload eventPayload = new(
            CharacterFamilySystem.AuthoritativeActorId,
            action,
            new ActivePregnancyRegisteredOutcome(pregnancy));
        JsonObject serializedCommandPayload = JsonSerializer.SerializeToNode<
            ICampaignCommandPayload>(
                commandPayload,
                SimulationJson.CreateOptions())!.AsObject();
        JsonObject serializedEventPayload = JsonSerializer.SerializeToNode<
            ICampaignEventPayload>(
                eventPayload,
                SimulationJson.CreateOptions())!.AsObject();
        switch (mutation)
        {
            case "snapshot-state":
                invalid["snapshot"]!["characterPregnancies"] =
                    JsonSerializer.SerializeToNode(
                        new CharacterPregnancyWorldSnapshot(
                            CharacterPregnancyContractVersions.Snapshot,
                            [pregnancy]),
                        SimulationJson.CreateOptions());
                break;
            case "snapshot-explicit-null":
                invalid["snapshot"]!["characterPregnancies"] = null;
                break;
            case "system-registration":
                invalid["snapshot"]!["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterPregnancySystem.SystemId,
                    ["version"] = CharacterPregnancySystem.Version,
                });
                break;
            case "pending-action":
                CampaignCommand pending = CampaignCommand.Create(
                    commandId,
                    CharacterFamilySystem.AuthoritativeActorId,
                    resolutionDate,
                    commandPayload,
                    ResolutionPhase.Commands);
                invalid["snapshot"]!["pendingCommands"]!.AsArray().Add(
                    JsonSerializer.SerializeToNode(
                        pending,
                        SimulationJson.CreateOptions()));
                break;
            case "diagnostic-action":
                invalid["diagnosticCommands"]!.AsArray()[0]!["payload"] =
                    serializedCommandPayload;
                break;
            case "diagnostic-outcome":
                invalid["diagnosticEvents"]!.AsArray()[0]!["payload"] =
                    serializedEventPayload;
                break;
            case "explicit-null-expectation":
                invalid["diagnosticCommands"]!.AsArray()[0]!["payload"]![
                    "expectedCurrentPregnancyId"] = null;
                break;
            case "explicit-null-outcome":
                invalid["diagnosticEvents"]!.AsArray()[0]!["payload"]![
                    "pregnancy"] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-seventeen-e4-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pregnancy", false)]
    [InlineData("pregnancy", true)]
    [InlineData("pregnancyId", false)]
    [InlineData("pregnancyId", true)]
    [InlineData("gestationalParentCharacterId", false)]
    [InlineData("gestationalParentCharacterId", true)]
    [InlineData("otherBiologicalParentCharacterId", false)]
    [InlineData("otherBiologicalParentCharacterId", true)]
    [InlineData("sourceUnionId", false)]
    [InlineData("sourceUnionId", true)]
    [InlineData("expectedCurrentPregnancyId", false)]
    [InlineData("expectedCurrentPregnancyId", true)]
    [InlineData("expectedBirthDate", false)]
    [InlineData("expectedBirthDate", true)]
    public void SchemaSeventeen_RejectsEachIsolatedE4PropertyIncludingExplicitNull(
        string propertyName,
        bool explicitNull)
    {
        JsonObject invalid = CreateHistoricalFixture(17);
        string beforeMutation = JsonSerializer.Serialize(invalid, CanonicalJson.Options);
        Assert.DoesNotContain($"\"{propertyName}\"", beforeMutation, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "register_active_pregnancy.v1",
            beforeMutation,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "active_pregnancy_registered.v1",
            beforeMutation,
            StringComparison.Ordinal);
        JsonObject payload = invalid["diagnosticCommands"]!.AsArray()[0]!["payload"]!
            .AsObject();

        payload[propertyName] = explicitNull
            ? null
            : JsonValue.Create("isolated-e4-property-probe");

        Assert.True(payload.ContainsKey(propertyName));
        string mutated = JsonSerializer.Serialize(invalid, CanonicalJson.Options);
        Assert.Contains($"\"{propertyName}\"", mutated, StringComparison.Ordinal);
        string path = Path.Combine(
            directory,
            $"schema-seventeen-isolated-{propertyName}-{explicitNull}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaEighteen_AuthenticatesExactE4FixtureAndMigratesVocabularyWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(18);
        Assert.Equal(FrozenSchemaEighteenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-18-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(74_121, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaEighteenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 18);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalSnapshot = JsonSerializer.Serialize(
            frozen["snapshot"],
            CanonicalJson.Options);
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string historicalEvents = JsonSerializer.Serialize(
            frozen["diagnosticEvents"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-eighteen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 18);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(
            historicalSnapshot,
            SerializeWithoutCharacterEducation(migrated.Snapshot));
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        Assert.Equal(
            historicalEvents,
            JsonSerializer.Serialize(migrated.DiagnosticEvents, CanonicalJson.Options));
        Assert.Single(migrated.Snapshot.CharacterPregnancies.ActivePregnancies);
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterFamilyActionCommandPayload
            {
                Action: RegisterActivePregnancyAction,
            });
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload
                is CharacterFamilyActionResolvedEventPayload
            {
                Outcome: ActivePregnancyRegisteredOutcome,
            });
        Assert.Equal(
            SimulationChecksum.Compute(migrated.Snapshot).Value,
            migrated.Checksum);
        _ = WorldState.Restore(migrated.Snapshot);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pending-action")]
    [InlineData("diagnostic-action")]
    [InlineData("diagnostic-outcome")]
    public void SchemaEighteen_RejectsE5DiscriminatorsWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(18);
        JsonObject pendingAction = invalid["snapshot"]!["pendingCommands"]!.AsArray()
            .OfType<JsonObject>()
            .First(command => command["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "register_active_pregnancy.v1");
        JsonObject diagnosticAction = invalid["diagnosticCommands"]!.AsArray()
            .OfType<JsonObject>()
            .First(command => command["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "register_active_pregnancy.v1");
        JsonObject diagnosticOutcome = invalid["diagnosticEvents"]!.AsArray()
            .OfType<JsonObject>()
            .First(campaignEvent => campaignEvent["payload"]?["outcome"]?["$type"]?.GetValue<string>()
                == "active_pregnancy_registered.v1");
        switch (mutation)
        {
            case "pending-action":
                pendingAction["payload"]!["action"]!["$type"] =
                    "resolve_pregnancy_birth.v1";
                break;
            case "diagnostic-action":
                diagnosticAction["payload"]!["action"]!["$type"] =
                    "resolve_pregnancy_birth.v1";
                break;
            case "diagnostic-outcome":
                diagnosticOutcome["payload"]!["outcome"]!["$type"] =
                    "pregnancy_birth_resolved.v1";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-eighteen-e5-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("expectedPregnancyId", false)]
    [InlineData("expectedPregnancyId", true)]
    [InlineData("newborn", false)]
    [InlineData("newborn", true)]
    [InlineData("birth", false)]
    [InlineData("birth", true)]
    [InlineData("birthId", false)]
    [InlineData("birthId", true)]
    [InlineData("resolvedPregnancy", false)]
    [InlineData("resolvedPregnancy", true)]
    [InlineData("childDefinition", false)]
    [InlineData("childDefinition", true)]
    [InlineData("childState", false)]
    [InlineData("childState", true)]
    [InlineData("inheritedTraitIds", false)]
    [InlineData("inheritedTraitIds", true)]
    public void SchemaEighteen_RejectsEachIsolatedE5PropertyIncludingExplicitNull(
        string propertyName,
        bool explicitNull)
    {
        JsonObject invalid = CreateHistoricalFixture(18);
        string beforeMutation = JsonSerializer.Serialize(invalid, CanonicalJson.Options);
        Assert.DoesNotContain($"\"{propertyName}\"", beforeMutation, StringComparison.Ordinal);
        JsonObject payload = invalid["diagnosticCommands"]!.AsArray()[0]!["payload"]!
            .AsObject();

        payload[propertyName] = explicitNull
            ? null
            : JsonValue.Create("isolated-e5-property-probe");

        string path = Path.Combine(
            directory,
            $"schema-eighteen-isolated-{propertyName}-{explicitNull}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaEighteen_PreservesExistingGeneratedChildAndV2Memberships()
    {
        JsonObject compatible = CreateHistoricalFixture(18);
        WorldSnapshot historical = compatible["snapshot"]!.Deserialize<WorldSnapshot>(
            SimulationJson.CreateOptions())!;
        EntityId childId = new("character:generated/schema-18-existing");
        EntityId firstParent = new("character:d3/000");
        EntityId secondParent = new("character:d3/004");
        EntityId familyId = new("family:fixture/schema-18-existing");
        EntityId householdId = new("household:d3/000");
        EntityId nameKey = new("loc:fixture/schema_18_existing_child");
        CharacterParentLink[] parentLinks =
        [
            new(firstParent, ParentChildLinkKind.Biological),
            new(secondParent, ParentChildLinkKind.Biological),
        ];
        CharacterWorldSnapshot characters = historical.Characters with
        {
            CharacterDefinitions = historical.Characters.CharacterDefinitions.Append(
                new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    childId,
                    nameKey,
                    new CampaignDate(199, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    new CharacterContentOrigin(
                        CharacterOriginKind.Generated,
                        CharacterHistoricalClassification.Fictional,
                        new EntityId("character_record:fixture/schema-18-existing"),
                        null,
                        [],
                        []),
                    null,
                    null,
                    [])).ToArray(),
            CharacterStates = historical.Characters.CharacterStates.Append(
                new CharacterState(
                    CharacterContractVersions.PreviousState,
                    childId,
                    parentLinks.Select(link => link.ParentCharacterId).Order().ToArray(),
                    parentLinks.OrderBy(link => link.ParentCharacterId).ToArray(),
                    CharacterConditionState.Default)).ToArray(),
            FamilyDefinitions = historical.Characters.FamilyDefinitions.Append(
                new FamilyDefinition(
                    CharacterContractVersions.Definition,
                    familyId,
                    new EntityId("loc:fixture/schema_18_family"))).ToArray(),
            FamilyStates = historical.Characters.FamilyStates.Append(
                new FamilyState(
                    CharacterContractVersions.PreviousState,
                    familyId,
                    new[] { firstParent, childId }.Order().ToArray())).ToArray(),
            HouseholdStates = historical.Characters.HouseholdStates.Select(state =>
                state.HouseholdId == householdId
                    ? state with
                    {
                        MemberIds = state.MemberIds.Append(childId).Order().ToArray(),
                    }
                    : state).ToArray(),
        };
        historical = historical with { Characters = characters.Canonicalize() };
        compatible["snapshot"] = JsonSerializer.SerializeToNode(
            historical,
            SimulationJson.CreateOptions());
        foreach (JsonObject state in compatible["snapshot"]!["characters"]!["characterStates"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            state.Remove("educationAttainments");
        }
        compatible["checksum"] = SimulationChecksum.ComputeForSaveSchema(
            historical,
            18).Value;
        string path = Path.Combine(directory, "schema-eighteen-generated-child.save.gz");
        WriteJsonGzip(path, compatible);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);
        WorldState restored = WorldState.Restore(migrated.Snapshot);

        Assert.True(restored.Characters.TryGetCharacterProfile(
            childId,
            out AuthoritativeCharacterProfile? child));
        Assert.Equal(CharacterOriginKind.Generated, child.ContentOrigin.OriginKind);
        Assert.Equal(CharacterHistoricalClassification.Fictional, child.ContentOrigin.HistoricalClassification);
        Assert.Equal(familyId, child.FamilyId);
        Assert.Equal(householdId, child.HouseholdId);
        Assert.Equal(
            parentLinks.OrderBy(link => link.ParentCharacterId),
            child.ParentLinks);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaNineteen_AuthenticatesExactE5FixtureAndMigratesRuntimeCharactersWithoutChangingSource()
    {
        JsonObject frozen = CreateHistoricalFixture(19);
        Assert.Equal(FrozenSchemaNineteenChecksum, frozen["checksum"]!.GetValue<string>());
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "save-schema-19-history-backed.json");
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        Assert.Equal(17_797, fixtureBytes.Length);
        Assert.Equal(
            FrozenSchemaNineteenFileSha256,
            Convert.ToHexStringLower(SHA256.HashData(fixtureBytes)));
        SaveSchemaRegistry.ValidateHistoricalSourceChecksum(frozen, 19);
        JsonObject original = (JsonObject)frozen.DeepClone();
        string historicalCommands = JsonSerializer.Serialize(
            frozen["diagnosticCommands"],
            CanonicalJson.Options);
        string path = Path.Combine(directory, "schema-nineteen-history-backed.save.gz");
        WriteFrozenHistoricalFixture(path, 19);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        JsonObject expectedSnapshot = original["snapshot"]!.DeepClone().AsObject();
        JsonObject expectedCharacters = expectedSnapshot["characters"]!.AsObject();
        expectedCharacters["contractVersion"] = CharacterContractVersions.Snapshot;
        foreach (JsonObject state in expectedCharacters["characterStates"]!.AsArray()
                     .OfType<JsonObject>())
        {
            state["contractVersion"] = CharacterContractVersions.State;
            state["educationAttainments"] = new JsonArray();
        }
        foreach (JsonObject state in expectedCharacters["familyStates"]!.AsArray()
                     .Concat(expectedCharacters["householdStates"]!.AsArray())
                     .OfType<JsonObject>())
        {
            state["contractVersion"] = CharacterContractVersions.State;
        }
        expectedSnapshot["systemVersions"]!.AsArray()
            .OfType<JsonObject>()
            .Single(item => item["systemId"]!.GetValue<string>() == "simulation.characters")
            ["version"] = CharacterContractVersions.Snapshot;
        JsonNode migratedSnapshot = JsonSerializer.SerializeToNode(
            migrated.Snapshot,
            SimulationJson.CreateOptions())!;
        JsonArray expectedDiagnosticEvents = original["diagnosticEvents"]!
            .DeepClone()
            .AsArray();
        JsonObject expectedBirthChildState = expectedDiagnosticEvents
            .OfType<JsonObject>()
            .Single(campaignEvent => campaignEvent["payload"]?["outcome"]?["$type"]
                ?.GetValue<string>() == "pregnancy_birth_resolved.v1")
            ["payload"]!["outcome"]!["birth"]!["childState"]!.AsObject();
        expectedBirthChildState["contractVersion"] = CharacterContractVersions.State;
        expectedBirthChildState["educationAttainments"] = new JsonArray();
        JsonObject migratedJson = JsonSerializer.SerializeToNode(
            migrated,
            SimulationJson.CreateOptions())!.AsObject();

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.True(JsonNode.DeepEquals(expectedSnapshot, migratedSnapshot));
        foreach (string preservedEnvelopeProperty in new[]
                 {
                     "contractVersion",
                     "gameVersion",
                     "createdUtc",
                     "contentManifests",
                     "seed",
                 })
        {
            Assert.True(JsonNode.DeepEquals(
                original[preservedEnvelopeProperty],
                migratedJson[preservedEnvelopeProperty]));
        }
        Assert.True(JsonNode.DeepEquals(
            original["diagnosticCommands"],
            migratedJson["diagnosticCommands"]));
        Assert.True(JsonNode.DeepEquals(
            expectedDiagnosticEvents,
            migratedJson["diagnosticEvents"]));
        Assert.Equal(CharacterContractVersions.Snapshot, migrated.Snapshot.Characters.ContractVersion);
        Assert.All(migrated.Snapshot.Characters.IdentityDefinitions, item =>
            Assert.Equal(CharacterContractVersions.Definition, item.ContractVersion));
        Assert.All(migrated.Snapshot.Characters.CharacterDefinitions, item =>
            Assert.Equal(CharacterContractVersions.Definition, item.ContractVersion));
        Assert.All(migrated.Snapshot.Characters.CharacterStates, item =>
        {
            Assert.Equal(CharacterContractVersions.State, item.ContractVersion);
            Assert.Empty(item.EducationAttainments!);
        });
        Assert.All(migrated.Snapshot.Characters.FamilyStates, item =>
            Assert.Equal(CharacterContractVersions.State, item.ContractVersion));
        Assert.All(migrated.Snapshot.Characters.HouseholdStates, item =>
            Assert.Equal(CharacterContractVersions.State, item.ContractVersion));
        Assert.Contains(
            migrated.Snapshot.SystemVersions,
            item => item.SystemId == "simulation.characters"
                && item.Version == CharacterContractVersions.Snapshot);
        Assert.Contains(
            migrated.Snapshot.PendingCommands,
            command => command.Payload is CharacterFamilyActionCommandPayload
            {
                Action: ResolvePregnancyBirthAction,
            });
        Assert.Contains(
            migrated.DiagnosticEvents,
            campaignEvent => campaignEvent.Payload is CharacterFamilyActionResolvedEventPayload
            {
                Outcome: PregnancyBirthResolvedOutcome,
            });
        Assert.Equal(
            historicalCommands,
            JsonSerializer.Serialize(migrated.DiagnosticCommands, CanonicalJson.Options));
        PregnancyBirthResolvedOutcome migratedBirth = Assert.IsType<
            PregnancyBirthResolvedOutcome>(Assert.IsType<
                CharacterFamilyActionResolvedEventPayload>(Assert.Single(
                    migrated.DiagnosticEvents).Payload).Outcome);
        Assert.Equal(CharacterContractVersions.State, migratedBirth.Birth.ChildState.ContractVersion);
        Assert.Empty(migratedBirth.Birth.ChildState.EducationAttainments!);
        Assert.Equal(
            FrozenSchemaNineteenChecksum,
            SimulationChecksum.ComputeForSaveSchema(migrated.Snapshot, 19).Value);
        Assert.Equal(SimulationChecksum.Compute(migrated.Snapshot).Value, migrated.Checksum);
        WorldState restored = WorldState.Restore(migrated.Snapshot);
        AuthoritativeCharacterProfile generated = Assert.Single(
            restored.Characters.Profiles,
            item => item.ContentOrigin.OriginKind == CharacterOriginKind.Generated);
        Assert.Empty(generated.EducationAttainments);
        Assert.Equal(
            JsonSerializer.Serialize(original, CanonicalJson.Options),
            JsonSerializer.Serialize(frozen, CanonicalJson.Options));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pending-action")]
    [InlineData("diagnostic-action")]
    [InlineData("diagnostic-outcome")]
    public void SchemaNineteen_RejectsE6DiscriminatorsWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(19);
        JsonObject pendingAction = invalid["snapshot"]!["pendingCommands"]!.AsArray()
            .OfType<JsonObject>()
            .First(command => command["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "resolve_pregnancy_birth.v1");
        JsonObject diagnosticAction = invalid["diagnosticCommands"]!.AsArray()
            .OfType<JsonObject>()
            .First(command => command["payload"]?["action"]?["$type"]?.GetValue<string>()
                == "resolve_pregnancy_birth.v1");
        JsonObject diagnosticOutcome = invalid["diagnosticEvents"]!.AsArray()
            .OfType<JsonObject>()
            .First(campaignEvent => campaignEvent["payload"]?["outcome"]?["$type"]?.GetValue<string>()
                == "pregnancy_birth_resolved.v1");
        switch (mutation)
        {
            case "pending-action":
                pendingAction["payload"]!["action"]!["$type"] =
                    "complete_primary_guardian_education.v1";
                break;
            case "diagnostic-action":
                diagnosticAction["payload"]!["action"]!["$type"] =
                    "complete_primary_guardian_education.v1";
                break;
            case "diagnostic-outcome":
                diagnosticOutcome["payload"]!["outcome"]!["$type"] =
                    "primary_guardian_education_completed.v1";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-nineteen-e6-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("educationAttainments", false)]
    [InlineData("educationAttainments", true)]
    [InlineData("attainment", false)]
    [InlineData("attainment", true)]
    [InlineData("attainmentId", false)]
    [InlineData("attainmentId", true)]
    [InlineData("teacherCharacterId", false)]
    [InlineData("teacherCharacterId", true)]
    [InlineData("primaryGuardianshipId", false)]
    [InlineData("primaryGuardianshipId", true)]
    [InlineData("expectedPrimaryGuardianshipId", false)]
    [InlineData("expectedPrimaryGuardianshipId", true)]
    public void SchemaNineteen_RejectsEachIsolatedE6PropertyIncludingExplicitNull(
        string propertyName,
        bool explicitNull)
    {
        JsonObject invalid = CreateHistoricalFixture(19);
        string beforeMutation = JsonSerializer.Serialize(invalid, CanonicalJson.Options);
        Assert.DoesNotContain($"\"{propertyName}\"", beforeMutation, StringComparison.Ordinal);
        JsonObject target = propertyName == "educationAttainments"
            ? invalid["snapshot"]!["characters"]!["characterStates"]!.AsArray()[0]!.AsObject()
            : invalid["diagnosticCommands"]!.AsArray()[0]!["payload"]!.AsObject();
        target[propertyName] = explicitNull
            ? null
            : JsonValue.Create("isolated-e6-property-probe");
        string path = Path.Combine(
            directory,
            $"schema-nineteen-isolated-{propertyName}-{explicitNull}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("snapshot-version")]
    [InlineData("state-version")]
    [InlineData("system-version")]
    public void SchemaNineteen_RejectsCharacterV3VersionInjectionWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(19);
        switch (mutation)
        {
            case "snapshot-version":
                invalid["snapshot"]!["characters"]!["contractVersion"] =
                    CharacterContractVersions.Snapshot;
                break;
            case "state-version":
                invalid["snapshot"]!["characters"]!["characterStates"]![0]!["contractVersion"] =
                    CharacterContractVersions.State;
                break;
            case "system-version":
                invalid["snapshot"]!["systemVersions"]!.AsArray()
                    .OfType<JsonObject>()
                    .Single(item => item["systemId"]!.GetValue<string>() == "simulation.characters")
                    ["version"] = CharacterContractVersions.Snapshot;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-nineteen-v3-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaNineteen_GeneratedChildMigratesEmptyThenGainsEducationAbility()
    {
        string path = Path.Combine(directory, "schema-nineteen-generated-education.save.gz");
        WriteFrozenHistoricalFixture(path, 19);
        SaveEnvelope migrated = new SaveStore().Load(path);
        CampaignSimulation simulation = new(WorldState.Restore(migrated.Snapshot));
        AuthoritativeCharacterProfile child = Assert.Single(
            simulation.World.Characters.Profiles,
            item => item.ContentOrigin.OriginKind == CharacterOriginKind.Generated);
        EntityId teacher = new("character:fixture/e5-first");
        EntityId ability = new("ability:fixture/e5-teaching");
        CampaignCommand guardianshipCommand = CampaignCommand.Create(
            new EntityId("command:test/schema19-generated-guardian"),
            CharacterFamilySystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterFamilyActionCommandPayload(
                new EstablishPrimaryGuardianshipAction(teacher, child.CharacterId, null)));
        Assert.True(simulation.Submit(guardianshipCommand).IsValid);

        IReadOnlyList<CampaignEvent> firstTurn = simulation.ResolveTurn();
        PrimaryGuardianshipEstablishedOutcome guardianship = Assert.IsType<
            PrimaryGuardianshipEstablishedOutcome>(Assert.IsType<
                CharacterFamilyActionResolvedEventPayload>(firstTurn.Single(item =>
                    item.CausalId == guardianshipCommand.CommandId).Payload).Outcome);
        CampaignCommand educationCommand = CampaignCommand.Create(
            new EntityId("command:test/schema19-generated-education"),
            CharacterFamilySystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterFamilyActionCommandPayload(
                new CompletePrimaryGuardianEducationAction(
                    child.CharacterId,
                    guardianship.Guardianship.GuardianshipId,
                    ability)));
        Assert.True(simulation.Submit(educationCommand).IsValid);
        Assert.Contains(
            simulation.ResolveTurn(),
            item => item.CausalId == educationCommand.CommandId
                && item.Payload is CharacterFamilyActionResolvedEventPayload
                {
                    Outcome: PrimaryGuardianEducationCompletedOutcome,
                });

        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            child.CharacterId,
            out AuthoritativeCharacterProfile? educated));
        Assert.Equal([ability], educated.AbilityIds);
        Assert.Equal(ability, Assert.Single(educated.EducationAttainments).AbilityId);
    }

    [Theory]
    [InlineData("pending-command")]
    [InlineData("diagnostic-command")]
    [InlineData("diagnostic-event")]
    [InlineData("d2-nested")]
    public void SchemaTen_RejectsFutureD1DiscriminatorsWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(10);
        JsonObject future = new()
        {
            ["payload"] = new JsonObject
            {
                ["$type"] = mutation == "diagnostic-event"
                    ? "character_marriage_action_resolved.v1"
                    : "character_marriage_action.v1",
            },
        };
        switch (mutation)
        {
            case "pending-command":
                invalid["snapshot"]!["pendingCommands"]!.AsArray().Add(future);
                break;
            case "diagnostic-command":
                invalid["diagnosticCommands"]!.AsArray().Add(future);
                break;
            case "diagnostic-event":
                invalid["diagnosticEvents"]!.AsArray().Add(future);
                break;
            case "d2-nested":
                invalid["diagnosticCommands"]!.AsArray().Add(new JsonObject
                {
                    ["nested"] = new JsonObject
                    {
                        ["$type"] = "offer_romance_route.v1",
                    },
                });
                break;
        }

        string path = Path.Combine(directory, $"schema-ten-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("pending-action")]
    [InlineData("diagnostic-action")]
    [InlineData("diagnostic-outcome")]
    [InlineData("invitation-state")]
    [InlineData("route-v2")]
    [InlineData("system-v2")]
    public void SchemaEleven_RejectsAllD2DiscriminatorsAndStateWithoutChangingSource(
        string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(11);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        JsonObject marriages = snapshot["characterMarriages"]!.AsObject();
        JsonObject nested = new()
        {
            ["payload"] = new JsonObject
            {
                ["$type"] = "character_marriage_action.v1",
                ["action"] = new JsonObject
                {
                    ["$type"] = mutation == "diagnostic-outcome"
                        ? "advance_romance_route.v1"
                        : "offer_romance_route.v1",
                },
                ["outcome"] = new JsonObject
                {
                    ["$type"] = "romance_route_advanced.v1",
                },
            },
        };
        switch (mutation)
        {
            case "pending-action":
                snapshot["pendingCommands"]!.AsArray().Add(nested);
                break;
            case "diagnostic-action":
                invalid["diagnosticCommands"]!.AsArray().Add(nested);
                break;
            case "diagnostic-outcome":
                invalid["diagnosticEvents"]!.AsArray().Add(nested);
                break;
            case "invitation-state":
                marriages["invitations"] = new JsonArray();
                break;
            case "route-v2":
                marriages["romanceRoutes"]![0]!["contractVersion"] = 2;
                break;
            case "system-v2":
                JsonObject marriageVersion = snapshot["systemVersions"]!.AsArray()
                    .OfType<JsonObject>()
                    .Single(item => item["systemId"]!.GetValue<string>()
                        == CharacterMarriageSystem.SystemId);
                marriageVersion["version"] = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-eleven-future-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaEleven_DuplicateMarriageSystemRegistrationRecoversWithoutChangingPrimary()
    {
        JsonObject invalid = CreateHistoricalFixture(11);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        JsonArray systemVersions = snapshot["systemVersions"]!.AsArray();
        JsonObject marriageVersion = systemVersions
            .OfType<JsonObject>()
            .Single(item => item["systemId"]!.GetValue<string>()
                == CharacterMarriageSystem.SystemId);
        systemVersions.Add(marriageVersion.DeepClone());
        WorldSnapshot historical = SaveSchemaRegistry.DeserializeHistoricalSnapshotForChecksum(
            snapshot,
            11);
        invalid["checksum"] = SimulationChecksum.ComputeForSaveSchema(historical, 11).Value;

        string path = Path.Combine(directory, "schema-eleven-duplicate-marriage-system.save.gz");
        WriteJsonGzip(path, invalid);
        WriteJsonGzip(path + ".1", CreateHistoricalFixture(11));
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveLoadResult recovered = new SaveStore().LoadWithRecovery(path);

        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.NotNull(recovered.RecoveryDiagnostic);
        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, recovered.Envelope.SchemaVersion);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("marriage-snapshot")]
    [InlineData("marriage-system-version")]
    public void SchemaNine_RejectsInjectedSchemaTenDataWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(9);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        if (mutation == "marriage-snapshot")
        {
            snapshot["characterMarriages"] = JsonSerializer.SerializeToNode(
                CharacterMarriageWorldSnapshot.Empty,
                CanonicalJson.Options);
        }
        else
        {
            snapshot["systemVersions"]!.AsArray().Add(new JsonObject
            {
                ["systemId"] = CharacterMarriageSystem.SystemId,
                ["version"] = CharacterMarriageSystem.Version,
            });
        }

        string path = Path.Combine(directory, $"schema-nine-injected-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("estate-snapshot")]
    [InlineData("estate-system-version")]
    public void SchemaEight_RejectsInjectedSchemaNineDataWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(8);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        if (mutation == "estate-snapshot")
        {
            snapshot["characterEstateHoldings"] = JsonSerializer.SerializeToNode(
                CharacterEstateHoldingWorldSnapshot.Empty,
                CanonicalJson.Options);
        }
        else
        {
            snapshot["systemVersions"]!.AsArray().Add(new JsonObject
            {
                ["systemId"] = CharacterEstateHoldingSystem.SystemId,
                ["version"] = CharacterEstateHoldingSystem.Version,
            });
        }

        string path = Path.Combine(directory, $"schema-eight-injected-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("resource-snapshot")]
    [InlineData("resource-system-version")]
    [InlineData("resource-command")]
    [InlineData("resource-event")]
    public void SchemaSeven_RejectsInjectedSchemaEightDataWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(7);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        switch (mutation)
        {
            case "resource-snapshot":
                snapshot["characterResources"] = JsonSerializer.SerializeToNode(
                    CharacterResourceWorldSnapshot.Empty,
                    CanonicalJson.Options);
                break;
            case "resource-system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = CharacterResourceSystem.SystemId,
                    ["version"] = CharacterResourceSystem.Version,
                });
                break;
            case "resource-command":
                invalid["diagnosticCommands"]![0]!["payload"]!["$type"] =
                    "character_resource_action.v1";
                break;
            case "resource-event":
                invalid["diagnosticEvents"]![0]!["payload"]!["$type"] =
                    "character_resource_action_resolved.v1";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-seven-injected-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("career-snapshot")]
    [InlineData("career-system-version")]
    [InlineData("relationship-v2-field")]
    [InlineData("character-command")]
    [InlineData("character-event")]
    public void SchemaSix_RejectsInjectedSchemaSevenDataWithoutChangingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(6);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        switch (mutation)
        {
            case "career-snapshot":
                snapshot["careers"] = JsonSerializer.SerializeToNode(
                    CareerWorldSnapshot.Empty,
                    CanonicalJson.Options);
                break;
            case "career-system-version":
                snapshot["systemVersions"]!.AsArray().Add(new JsonObject
                {
                    ["systemId"] = "simulation.character_careers",
                    ["version"] = CareerContractVersions.Snapshot,
                });
                break;
            case "relationship-v2-field":
                snapshot["relationships"]!["subjects"]![0]!["detailedRelationships"]![0]!
                    ["memories"]![0]!["sourceEventId"] = new JsonObject
                    {
                        ["value"] = "event:injected/schema7",
                    };
                break;
            case "character-command":
                invalid["diagnosticCommands"]![0]!["payload"]!["$type"] =
                    "character_action.v1";
                break;
            case "character-event":
                invalid["diagnosticEvents"]![0]!["payload"]!["$type"] =
                    "character_action_resolved.v1";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string path = Path.Combine(directory, $"schema-six-injected-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFive_RejectsUnauthenticatedV2CharacterFieldsWithoutChangingSource()
    {
        JsonObject invalid = CreateHistoricalFixture(5);
        JsonObject definition = invalid["snapshot"]!["characters"]!["characterDefinitions"]![0]!.AsObject();
        definition["structuredName"] = new JsonObject
        {
            ["primaryNameKey"] = definition["nameKey"]!.DeepClone(),
            ["courtesyNameKey"] = null,
        };
        string path = Path.Combine(directory, "schema-five-injected-v2-field.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => new SaveStore().Load(path));

        Assert.Contains("contract-v2 descriptor data", exception.Message, StringComparison.Ordinal);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFive_RejectsRechecksummedV2FlawKindInLegacyIdentityWithoutChangingSource()
    {
        JsonObject invalid = CreateHistoricalFixture(5);
        invalid["snapshot"]!["characters"]!["identityDefinitions"]![0]!["kind"] =
            (int)CharacterIdentityKind.Flaw;
        WorldSnapshot snapshot = invalid["snapshot"]!.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        invalid["checksum"] = SimulationChecksum.ComputeForSaveSchema(snapshot, 5).Value;
        string path = Path.Combine(directory, "schema-five-v2-flaw-kind.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => new SaveStore().Load(path));

        Assert.Contains("non-v1 identity kind", exception.Message, StringComparison.Ordinal);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaFourMigrationPreservesNonemptyGeographyAndCharacterData()
    {
        CharacterWorldSnapshot characters = CreateCharacterSimulation().World.CaptureSnapshot().Characters;
        WorldState world = WorldState.Create(
            new CampaignDate(191, 7, 14),
            99,
            [new SyntheticEntitySnapshot(GeographyFixture.Actor, SimulationTier.Full, 1, 1, 1, [])],
            GeographyFixture.Snapshot(),
            characters);
        JsonObject schemaFour = JsonSerializer.SerializeToNode(
            CreateEnvelope(new CampaignSimulation(world)),
            CanonicalJson.Options)!.AsObject();
        JsonObject snapshot = schemaFour["snapshot"]!.AsObject();
        snapshot.Remove("relationships");
        RemoveSystemVersion(snapshot, "simulation.relationships");
        snapshot.Remove("careers");
        RemoveSystemVersion(snapshot, "simulation.character_careers");
        snapshot.Remove("characterResources");
        RemoveSystemVersion(snapshot, CharacterResourceSystem.SystemId);
        snapshot.Remove("characterEstateHoldings");
        RemoveSystemVersion(snapshot, CharacterEstateHoldingSystem.SystemId);
        snapshot.Remove("characterMarriages");
        RemoveSystemVersion(snapshot, CharacterMarriageSystem.SystemId);
        snapshot.Remove("characterGuardianships");
        RemoveSystemVersion(snapshot, CharacterGuardianshipSystem.SystemId);
        snapshot.Remove("characterPregnancies");
        RemoveSystemVersion(snapshot, CharacterPregnancySystem.SystemId);
        DowngradeCharactersToLegacy(snapshot);
        schemaFour["schemaVersion"] = 4;
        WorldSnapshot historical = snapshot.Deserialize<WorldSnapshot>(CanonicalJson.Options)!;
        schemaFour["checksum"] = SimulationChecksum.ComputeForSaveSchema(historical, 4).Value;
        string path = Path.Combine(directory, "schema-four-nonempty-geography-characters.save.gz");
        WriteJsonGzip(path, schemaFour);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(
            JsonSerializer.Serialize(historical.Geography, CanonicalJson.Options),
            JsonSerializer.Serialize(migrated.Snapshot.Geography, CanonicalJson.Options));
        Assert.Equal(
            historical.Characters.CharacterDefinitions.Select(item => item.Id),
            migrated.Snapshot.Characters.CharacterDefinitions.Select(item => item.Id));
        Assert.All(migrated.Snapshot.Characters.CharacterStates.SelectMany(item => item.ParentLinks!), link =>
            Assert.Equal(ParentChildLinkKind.UnspecifiedLegacy, link.Kind));
        Assert.Empty(migrated.Snapshot.Relationships.Subjects);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
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
    [InlineData(4)]
    [InlineData(5)]
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
        historicalShape.Remove("characterMarriages");
        historicalShape.Remove("characterEstateHoldings");
        historicalShape.Remove("characterResources");
        historicalShape.Remove("careers");
        historicalShape.Remove("characterGuardianships");
        historicalShape.Remove("characterPregnancies");
        if (schemaVersion < 5)
        {
            historicalShape.Remove("relationships");
        }

        if (schemaVersion < 4)
        {
            historicalShape.Remove("characters");
        }
        else
        {
            StripCharacterV2Fields(historicalShape);
        }

        if (schemaVersion < 3)
        {
            historicalShape.Remove("geography");
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(historicalShape, CanonicalJson.Options);
        string independentlyComputed = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string frozen = schemaVersion switch
        {
            < 3 => FrozenSchemaOneTwoChecksum,
            3 => FrozenSchemaThreeChecksum,
            4 => FrozenSchemaFourChecksum,
            5 => FrozenSchemaFiveChecksum,
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion)),
        };
        Assert.Equal(frozen, stored);
        Assert.Equal(stored, independentlyComputed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
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
            case 4:
                malformed["snapshot"]!["characters"] = null;
                break;
            case 5:
                malformed["snapshot"]!["relationships"] = null;
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

    [Theory]
    [InlineData("character-ability-ids")]
    [InlineData("character-parent-ids")]
    [InlineData("family-member-ids")]
    [InlineData("household-member-ids")]
    [InlineData("relationship-detailed-relationships")]
    public void SchemaFive_NestedNullRequiredDataFailsAndRecoversWithoutChangingCandidates(string mutation)
    {
        SaveStore store = new();
        string path = Path.Combine(directory, $"nested-null-schema-five-{mutation}.save.gz");
        SaveEnvelope oldest = CreateEnvelope(CreateSimulation());
        SaveEnvelope newestValid = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(1) };
        SaveEnvelope replacedPrimary = oldest with { CreatedUtc = oldest.CreatedUtc.AddMinutes(2) };
        store.SaveAutosave(path, oldest);
        store.SaveAutosave(path, newestValid);
        store.SaveAutosave(path, replacedPrimary);

        JsonObject malformed = CreateHistoricalFixture(5);
        JsonObject characters = malformed["snapshot"]!["characters"]!.AsObject();
        switch (mutation)
        {
            case "character-ability-ids":
                characters["characterDefinitions"]![0]!["abilityIds"] = null;
                break;
            case "character-parent-ids":
                characters["characterStates"]![0]!["parentIds"] = null;
                break;
            case "family-member-ids":
                characters["familyStates"]![0]!["memberIds"] = null;
                break;
            case "household-member-ids":
                characters["householdStates"]![0]!["memberIds"] = null;
                break;
            case "relationship-detailed-relationships":
                malformed["snapshot"]!["relationships"]!["subjects"]!.AsArray().Add(new JsonObject
                {
                    ["contractVersion"] = RelationshipContractVersions.State,
                    ["subjectCharacterId"] = JsonSerializer.SerializeToNode(
                        new EntityId("character:synthetic/child"),
                        CanonicalJson.Options),
                    ["detailedRelationships"] = null,
                    ["archivedRelationships"] = new JsonArray(),
                    ["distantHistory"] = JsonSerializer.SerializeToNode(
                        DistantRelationshipHistoryAggregate.Empty,
                        CanonicalJson.Options),
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        WriteJsonGzip(path, malformed);
        string[] candidatePaths = [path, path + ".1", path + ".2"];
        Dictionary<string, byte[]> candidateBytes = candidatePaths.ToDictionary(
            candidate => candidate,
            File.ReadAllBytes,
            StringComparer.Ordinal);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(() => store.Load(path));
        Assert.Contains("schema 5", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required", exception.ToString(), StringComparison.OrdinalIgnoreCase);

        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(newestValid.CreatedUtc, recovered.Envelope.CreatedUtc);
        Assert.Equal(path + ".1", recovered.SourcePath);
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

    [Theory]
    [InlineData("snapshot")]
    [InlineData("system-version")]
    [InlineData("system-version-wrong-version")]
    public void SchemaFour_RejectsUnexpectedRelationshipDataWithoutOverwritingSource(string mutation)
    {
        JsonObject invalid = CreateHistoricalFixture(4);
        JsonObject snapshot = invalid["snapshot"]!.AsObject();
        if (mutation == "snapshot")
        {
            snapshot["relationships"] = JsonSerializer.SerializeToNode(
                RelationshipWorldSnapshot.Empty,
                CanonicalJson.Options);
        }
        else
        {
            snapshot["systemVersions"]!.AsArray().Add(new JsonObject
            {
                ["systemId"] = "simulation.relationships",
                ["version"] = mutation == "system-version" ? 1 : 999,
            });
        }

        string path = Path.Combine(directory, $"schema-four-unexpected-relationships-{mutation}.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => new SaveStore().Load(path));

        Assert.Equal("Schema 4 unexpectedly contains schema 5 relationship data.", exception.Message);
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
            version == new SystemVersion(
                "simulation.characters",
                CharacterContractVersions.Snapshot));
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
            version == new SystemVersion(
                "simulation.characters",
                CharacterContractVersions.Snapshot));
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
    public void LegacyStandaloneSnapshotOmittingRelationships_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("relationships");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode relationshipVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>() == "simulation.relationships")!;
        versions.Remove(relationshipVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException("Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.Relationships.Subjects);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                "simulation.relationships",
                RelationshipContractVersions.Snapshot));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullRelationships_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutRelationshipSystemVersion(current) with
        {
            Relationships = RelationshipWorldSnapshot.Empty with { Subjects = null! },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains("complete, valid, empty relationship snapshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingCharacterResources_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("characterResources");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode resourceVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>() == CharacterResourceSystem.SystemId)!;
        versions.Remove(resourceVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException("Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.CharacterResources.Accounts);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                CharacterResourceSystem.SystemId,
                CharacterResourceSystem.Version));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullCharacterResources_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterResourceSystemVersion(current) with
        {
            CharacterResources = CharacterResourceWorldSnapshot.Empty with { Accounts = null! },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-resource snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithNonemptyCharacterResources_FailsDeliberately()
    {
        WorldSnapshot current = CreateCharacterResourceSimulation().World.CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterResourceSystemVersion(current);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-resource snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingCharacterEstateHoldings_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("characterEstateHoldings");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode estateVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>() == CharacterEstateHoldingSystem.SystemId)!;
        versions.Remove(estateVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException("Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.CharacterEstateHoldings.Holdings);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                CharacterEstateHoldingSystem.SystemId,
                CharacterEstateHoldingSystem.Version));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullCharacterEstateHoldings_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterEstateHoldingSystemVersion(current) with
        {
            CharacterEstateHoldings = CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = null!,
            },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-estate-holding snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithNonemptyCharacterEstateHoldings_FailsDeliberately()
    {
        WorldSnapshot current = CreateCharacterEstateHoldingSimulation().World.CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterEstateHoldingSystemVersion(current);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-estate-holding snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingCharacterMarriages_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("characterMarriages");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode marriageVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>() == CharacterMarriageSystem.SystemId)!;
        versions.Remove(marriageVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException("Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.CharacterMarriages.Unions);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                CharacterMarriageSystem.SystemId,
                CharacterMarriageSystem.Version));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullCharacterMarriages_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterMarriageSystemVersion(current) with
        {
            CharacterMarriages = CharacterMarriageWorldSnapshot.Empty with
            {
                Unions = null!,
            },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-marriage snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingCharacterGuardianships_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(
            current,
            CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("characterGuardianships");
        JsonArray versions = legacyJson["systemVersions"]!.AsArray();
        JsonNode guardianshipVersion = versions.Single(
            node => node!["systemId"]!.GetValue<string>()
                == CharacterGuardianshipSystem.SystemId)!;
        versions.Remove(guardianshipVersion);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException(
                "Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.CharacterGuardianships.Guardianships);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                CharacterGuardianshipSystem.SystemId,
                CharacterGuardianshipSystem.Version));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullCharacterGuardianships_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterGuardianshipSystemVersion(current) with
        {
            CharacterGuardianships = CharacterGuardianshipWorldSnapshot.Empty with
            {
                Guardianships = null!,
            },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-guardianship snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithNonemptyCharacterGuardianships_FailsDeliberately()
    {
        WorldSnapshot current = CreateCharacterSimulation().World.CaptureSnapshot();
        EntityId guardian = new("character:synthetic/parent");
        EntityId ward = new("character:synthetic/child");
        CampaignDate establishedDate = new(175, 1, 1);
        EntityId commandId = new("command:test/legacy-standalone-guardianship");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            establishedDate,
            commandId);
        CharacterGuardianshipState guardianship = new(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(eventId, ward, guardian),
            ward,
            guardian,
            establishedDate,
            0,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
        WorldSnapshot nonempty = current with
        {
            CharacterGuardianships = new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [guardianship]),
        };
        _ = WorldState.Restore(nonempty);
        WorldSnapshot invalid = WithoutCharacterGuardianshipSystemVersion(nonempty);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-guardianship snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotOmittingCharacterPregnancies_RestoresAsEmpty()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        JsonObject legacyJson = JsonSerializer.SerializeToNode(
            current,
            CanonicalJson.Options)!.AsObject();
        legacyJson.Remove("characterPregnancies");
        RemoveSystemVersion(legacyJson, CharacterPregnancySystem.SystemId);
        WorldSnapshot legacy = legacyJson.Deserialize<WorldSnapshot>(CanonicalJson.Options)
            ?? throw new InvalidDataException(
                "Legacy standalone snapshot did not deserialize.");

        WorldState restored = WorldState.Restore(legacy);

        Assert.Empty(restored.CharacterPregnancies.ActivePregnancies);
        Assert.Contains(restored.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                CharacterPregnancySystem.SystemId,
                CharacterPregnancySystem.Version));
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithPartialNullCharacterPregnancies_FailsDeliberately()
    {
        WorldSnapshot current = SyntheticSimulation.CreateWorld(1, 99).CaptureSnapshot();
        WorldSnapshot invalid = WithoutCharacterPregnancySystemVersion(current) with
        {
            CharacterPregnancies = CharacterPregnancyWorldSnapshot.Empty with
            {
                ActivePregnancies = null!,
            },
        };

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-pregnancy snapshot",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyStandaloneSnapshotWithNonemptyCharacterPregnancies_FailsDeliberately()
    {
        JsonObject migratedJson = new SaveSchemaRegistry().MigrateToCurrent(
            CreateHistoricalFixture(17));
        SaveEnvelope migrated = migratedJson.Deserialize<SaveEnvelope>(
            SimulationJson.CreateOptions())!;
        EntityId firstParent = new("character:d3/000");
        EntityId secondParent = new("character:d3/004");
        EntityId unionId = new(
            "marriage_union:sha256/6295da926eaf162265f5b910b0c54a1107b55627544f40d64768cb060a4e53e4");
        CampaignDate startDate = migrated.Snapshot.Calendar.Date;
        EntityId commandId = new("command:test/legacy-standalone-pregnancy");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(startDate, commandId);
        CharacterPregnancyState pregnancy = new(
            CharacterPregnancyContractVersions.State,
            CharacterPregnancyIds.DerivePregnancyId(
                eventId,
                firstParent,
                secondParent,
                unionId),
            firstParent,
            secondParent,
            unionId,
            startDate,
            startDate.AddDays(CharacterPregnancyLimits.GestationDays),
            migrated.Snapshot.Calendar.TurnIndex,
            commandId,
            eventId);
        WorldSnapshot nonempty = migrated.Snapshot with
        {
            CharacterPregnancies = new CharacterPregnancyWorldSnapshot(
                CharacterPregnancyContractVersions.Snapshot,
                [pregnancy]),
        };
        _ = WorldState.Restore(nonempty);
        WorldSnapshot invalid = WithoutCharacterPregnancySystemVersion(nonempty);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(
            () => WorldState.Restore(invalid));

        Assert.Contains(
            "complete, valid, empty character-pregnancy snapshot",
            exception.Message,
            StringComparison.Ordinal);
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
            Identity("flaw:synthetic/stubborn", CharacterIdentityKind.Flaw),
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
                new CharacterState(
                    CharacterContractVersions.State,
                    new EntityId("character:synthetic/child"),
                    [new EntityId("character:synthetic/parent")],
                    [new CharacterParentLink(new EntityId("character:synthetic/parent"), ParentChildLinkKind.Biological)],
                    CharacterConditionState.Default),
                new CharacterState(
                    CharacterContractVersions.State,
                    new EntityId("character:synthetic/parent"),
                    [],
                    [],
                    CharacterConditionState.Default),
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

    private static CampaignSimulation CreateCharacterResourceSimulation()
    {
        WorldSnapshot characterSnapshot = CreateCharacterSimulation().World.CaptureSnapshot();
        EntityId child = new("character:synthetic/child");
        CharacterResourceWorldSnapshot resources = CharacterResourceWorldSnapshot.Empty with
        {
            Accounts =
            [
                new CharacterWealthAccountState(
                    CharacterResourceContractVersions.State,
                    CharacterResourceIds.DeriveWealthAccountId(child),
                    child,
                    100),
            ],
        };
        WorldState world = WorldState.Create(
            characterSnapshot.Calendar.Date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characterSnapshot.Characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            resources);
        return new CampaignSimulation(world);
    }

    private static CampaignSimulation CreateCharacterEstateHoldingSimulation()
    {
        WorldSnapshot characterSnapshot = CreateCharacterSimulation().World.CaptureSnapshot();
        EntityId parent = new("character:synthetic/parent");
        CharacterWorldSnapshot characters = characterSnapshot.Characters with
        {
            CharacterStates = characterSnapshot.Characters.CharacterStates
                .Select(state => state.CharacterId == parent
                    ? state with
                    {
                        Condition = new CharacterConditionState(
                            CharacterVitalStatus.Dead,
                            CharacterHealthStatus.Critical,
                            IsIncapacitated: true,
                            CharacterCustodyStatus.Free,
                            null),
                    }
                    : state)
                .ToArray(),
        };
        CharacterEstateHoldingWorldSnapshot holdings = new(
            CharacterEstateHoldingContractVersions.Snapshot,
            [
                new CharacterEstateHoldingState(
                    CharacterEstateHoldingContractVersions.State,
                    new EntityId("estate:synthetic/family_manor"),
                    parent),
            ]);
        WorldState world = WorldState.Create(
            characterSnapshot.Calendar.Date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            holdings);
        return new CampaignSimulation(world);
    }

    private static CampaignSimulation CreateCharacterMarriageSimulation()
    {
        WorldSnapshot characterSnapshot = CreateCharacterSimulation().World.CaptureSnapshot();
        EntityId first = new("character:synthetic/child");
        EntityId second = new("character:synthetic/peer");
        EntityId practiceId = new("marriage_practice:synthetic/default");
        EntityId proposalId = new("marriage_proposal:synthetic/political");
        CampaignDate date = characterSnapshot.Calendar.Date;
        CharacterWorldSnapshot characters = characterSnapshot.Characters with
        {
            CharacterDefinitions =
            [
                .. characterSnapshot.Characters.CharacterDefinitions,
                Definition(
                    second.Value,
                    "loc:character/synthetic_peer",
                    new CampaignDate(165, 2, 3)),
            ],
            CharacterStates =
            [
                .. characterSnapshot.Characters.CharacterStates,
                new CharacterState(
                    CharacterContractVersions.State,
                    second,
                    [],
                    [],
                    CharacterConditionState.Default),
            ],
        };
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            proposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.Voluntary,
            first,
            second,
            null,
            practiceId,
            date,
            0,
            new EntityId("command:synthetic/marriage_proposal"),
            MarriageProposalStatus.Accepted,
            date,
            0,
            new EntityId("command:synthetic/marriage_response"));
        CharacterMarriageWorldSnapshot marriages = new(
            CharacterMarriageContractVersions.Snapshot,
            [
                new MarriagePracticeState(
                    CharacterMarriageContractVersions.Practice,
                    practiceId,
                    18,
                    18,
                    1,
                    8,
                    1,
                    true,
                    true,
                    MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings),
            ],
            [proposal],
            [],
            [
                new MarriageUnionState(
                    CharacterMarriageContractVersions.State,
                    new EntityId("marriage_union:synthetic/political"),
                    first,
                    second,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    MarriageBasis.Political,
                    MarriageConsentKind.Voluntary,
                    practiceId,
                    proposalId,
                    date,
                    0,
                    MarriageUnionStatus.Active,
                    null,
                    null,
                    null,
                    null),
            ],
            [],
            []);
        WorldState world = WorldState.Create(
            date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            marriages);
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
        [new EntityId("reputation:synthetic/reliable")],
        new StructuredCharacterName(new EntityId(nameKey), null),
        CharacterContentOrigin.LegacyUnknown(new EntityId(id)),
        null,
        null,
        [new EntityId("flaw:synthetic/stubborn")]);

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

    private static WorldSnapshot WithoutRelationshipSystemVersion(WorldSnapshot snapshot) => snapshot with
    {
        SystemVersions = snapshot.SystemVersions
            .Where(version => version.SystemId != "simulation.relationships")
            .ToArray(),
    };

    private static WorldSnapshot WithoutCharacterResourceSystemVersion(WorldSnapshot snapshot) =>
        snapshot with
        {
            SystemVersions = snapshot.SystemVersions
                .Where(version => version.SystemId != CharacterResourceSystem.SystemId)
                .ToArray(),
        };

    private static WorldSnapshot WithoutCharacterEstateHoldingSystemVersion(WorldSnapshot snapshot) =>
        snapshot with
        {
            SystemVersions = snapshot.SystemVersions
                .Where(version => version.SystemId != CharacterEstateHoldingSystem.SystemId)
                .ToArray(),
        };

    private static WorldSnapshot WithoutCharacterMarriageSystemVersion(WorldSnapshot snapshot) =>
        snapshot with
        {
            SystemVersions = snapshot.SystemVersions
                .Where(version => version.SystemId != CharacterMarriageSystem.SystemId)
                .ToArray(),
        };

    private static WorldSnapshot WithoutCharacterGuardianshipSystemVersion(
        WorldSnapshot snapshot) => snapshot with
        {
            SystemVersions = snapshot.SystemVersions
            .Where(version => version.SystemId != CharacterGuardianshipSystem.SystemId)
            .ToArray(),
        };

    private static WorldSnapshot WithoutCharacterPregnancySystemVersion(
        WorldSnapshot snapshot) => snapshot with
        {
            SystemVersions = snapshot.SystemVersions
            .Where(version => version.SystemId != CharacterPregnancySystem.SystemId)
            .ToArray(),
        };

    private static string SerializeWithoutCharacterGuardianships(WorldSnapshot snapshot)
    {
        JsonObject serialized = JsonSerializer.SerializeToNode(
            snapshot,
            CanonicalJson.Options)!.AsObject();
        serialized.Remove("characterGuardianships");
        JsonArray systemVersions = serialized["systemVersions"]!.AsArray();
        JsonNode? guardianshipVersion = systemVersions.SingleOrDefault(node =>
            node?["systemId"]?.GetValue<string>()
                == CharacterGuardianshipSystem.SystemId);
        if (guardianshipVersion is not null)
        {
            systemVersions.Remove(guardianshipVersion);
        }

        serialized.Remove("characterPregnancies");
        JsonNode? pregnancyVersion = systemVersions.SingleOrDefault(node =>
            node?["systemId"]?.GetValue<string>()
                == CharacterPregnancySystem.SystemId);
        if (pregnancyVersion is not null)
        {
            systemVersions.Remove(pregnancyVersion);
        }

        DowngradeSerializedCharactersFromV3(serialized);

        return JsonSerializer.Serialize(serialized, CanonicalJson.Options);
    }

    private static string SerializeWithoutCharacterPregnancies(WorldSnapshot snapshot)
    {
        JsonObject serialized = JsonSerializer.SerializeToNode(
            snapshot,
            CanonicalJson.Options)!.AsObject();
        serialized.Remove("characterPregnancies");
        JsonArray systemVersions = serialized["systemVersions"]!.AsArray();
        JsonNode? pregnancyVersion = systemVersions.SingleOrDefault(node =>
            node?["systemId"]?.GetValue<string>()
                == CharacterPregnancySystem.SystemId);
        if (pregnancyVersion is not null)
        {
            systemVersions.Remove(pregnancyVersion);
        }

        DowngradeSerializedCharactersFromV3(serialized);

        return JsonSerializer.Serialize(serialized, CanonicalJson.Options);
    }

    private static string SerializeWithoutCharacterEducation(WorldSnapshot snapshot)
    {
        JsonObject serialized = JsonSerializer.SerializeToNode(
            snapshot,
            CanonicalJson.Options)!.AsObject();
        DowngradeSerializedCharactersFromV3(serialized);
        return JsonSerializer.Serialize(serialized, CanonicalJson.Options);
    }

    private static string SerializeCharactersWithoutEducation(CharacterWorldSnapshot characters)
    {
        JsonObject serialized = JsonSerializer.SerializeToNode(
            characters,
            CanonicalJson.Options)!.AsObject();
        serialized["contractVersion"] = CharacterContractVersions.PreviousSnapshot;
        DowngradeSerializedCharacterStateCollections(serialized);
        return JsonSerializer.Serialize(serialized, CanonicalJson.Options);
    }

    private static void DowngradeSerializedCharactersFromV3(JsonObject snapshot)
    {
        JsonObject characters = snapshot["characters"]!.AsObject();
        characters["contractVersion"] = CharacterContractVersions.PreviousSnapshot;
        DowngradeSerializedCharacterStateCollections(characters);
        JsonObject version = snapshot["systemVersions"]!.AsArray()
            .OfType<JsonObject>()
            .Single(item => item["systemId"]!.GetValue<string>() == "simulation.characters");
        version["version"] = CharacterContractVersions.PreviousSnapshot;
    }

    private static void DowngradeSerializedCharacterStateCollections(JsonObject characters)
    {
        foreach (string collection in new[] { "characterStates", "familyStates", "householdStates" })
        {
            foreach (JsonObject state in characters[collection]!.AsArray().OfType<JsonObject>())
            {
                state["contractVersion"] = CharacterContractVersions.PreviousState;
                if (collection == "characterStates")
                {
                    state.Remove("educationAttainments");
                }
            }
        }
    }

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
        // Schema 4 is reconstructed from the exact schema-4 contract at eaa3aaf.
        // Schema 5 is reconstructed from the exact schema-5 contract at ff7420f.
        // Schema 6 is generated from the exact SP-04C0 contract at 7d4612d.
        // Schema 7 is generated from the exact SP-04C1 contract at d5d2705.
        // Schema 8 is generated from the exact accepted SP-04C2 contract at e2d9590.
        // Schema 9 is generated from the exact accepted SP-04C3 contract at 7b9f795.
        // Schema 10 is generated from the exact accepted SP-04D0 contract at f7fef24.
        // Schema 11 is generated from the exact accepted SP-04D1 contract at 653ce71.
        // Schema 12 is generated from the exact accepted SP-04D2 contract at 62a5007.
        // Schema 13 is generated from the exact accepted SP-04D3 contract at 93d3881.
        // Schema 14 is generated from the exact accepted SP-04E0 contract at 30fd0ad.
        // Schema 15 is generated from the exact accepted SP-04E1 contract at 97b607a.
        // Schema 16 is generated from the exact accepted SP-04E2 contract at 7491da8.
        // Schema 17 is generated from the exact accepted SP-04E3 contract at 59588be.
        // Schema 18 is generated from the exact accepted SP-04E4 contract at 177346b.
        // Schema 19 is generated by a detached harness compiled from the exact
        // accepted SP-04E5 implementation source at 4b28fb7.
        // Schema 1/2 are synthetic fixtures inferred from the registered migration contracts.
        string fileName = schemaVersion switch
        {
            1 => "save-schema-1-inferred.json",
            2 => "save-schema-2-inferred.json",
            3 => "save-schema-3-history-backed.json",
            4 => string.Empty,
            5 => "save-schema-5-history-backed.json",
            6 => "save-schema-6-history-backed.json",
            7 => "save-schema-7-history-backed.json",
            8 => "save-schema-8-history-backed.json",
            9 => "save-schema-9-history-backed.json",
            10 => "save-schema-10-history-backed.json",
            11 => "save-schema-11-history-backed.json",
            12 => "save-schema-12-history-backed.json",
            13 => "save-schema-13-history-backed.json",
            14 => "save-schema-14-history-backed.json",
            15 => "save-schema-15-history-backed.json",
            16 => "save-schema-16-history-backed.json",
            17 => "save-schema-17-history-backed.json",
            18 => "save-schema-18-history-backed.json",
            19 => "save-schema-19-history-backed.json",
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion)),
        };
        return schemaVersion == 4
            ? FrozenSchemaFourFixture
            : File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static void DowngradeCharactersToLegacy(JsonObject snapshot)
    {
        JsonObject characters = snapshot["characters"]!.AsObject();
        characters["contractVersion"] = CharacterContractVersions.LegacySnapshot;
        JsonArray identities = characters["identityDefinitions"]!.AsArray();
        JsonNode[] v2Flaws = identities
            .Where(item => item!["kind"]!.GetValue<int>() == (int)CharacterIdentityKind.Flaw)
            .Select(item => item!)
            .ToArray();
        foreach (JsonNode flaw in v2Flaws)
        {
            identities.Remove(flaw);
        }

        foreach (string collection in new[]
                 {
                     "identityDefinitions",
                     "characterDefinitions",
                     "familyDefinitions",
                     "householdDefinitions",
                     "characterStates",
                     "familyStates",
                     "householdStates",
                 })
        {
            foreach (JsonObject item in characters[collection]!.AsArray().OfType<JsonObject>())
            {
                item["contractVersion"] = CharacterContractVersions.LegacyDefinition;
            }
        }

        StripCharacterV2Fields(snapshot);
        JsonObject version = snapshot["systemVersions"]!.AsArray()
            .OfType<JsonObject>()
            .Single(item => item["systemId"]!.GetValue<string>() == "simulation.characters");
        version["version"] = CharacterContractVersions.LegacySnapshot;
    }

    private static void StripCharacterV2Fields(JsonObject snapshot)
    {
        JsonObject characters = snapshot["characters"]!.AsObject();
        foreach (JsonObject definition in characters["characterDefinitions"]!.AsArray().OfType<JsonObject>())
        {
            definition.Remove("structuredName");
            definition.Remove("contentOrigin");
            definition.Remove("cultureId");
            definition.Remove("originLocationId");
            definition.Remove("flawIds");
        }

        foreach (JsonObject state in characters["characterStates"]!.AsArray().OfType<JsonObject>())
        {
            state.Remove("parentLinks");
            state.Remove("condition");
            state.Remove("educationAttainments");
        }
    }

    private sealed class SimulatedInterruptionException : Exception;
}
