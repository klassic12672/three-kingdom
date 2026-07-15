using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Simulation.Core;

namespace Game.Content.Tests;

public sealed class CharacterContentTests
{
    private static readonly EntityId BasePackId = new("pack:fictional_character_foundations");
    private static readonly EntityId WorldPackId = new("pack:fictional_character_worlds");
    private static readonly EntityId WorldId = new("character_world:dawn_pine");
    private static readonly EntityId CharacterA = new("character:fictional/ara");
    private static readonly EntityId CharacterB = new("character:fictional/boram");
    private static readonly EntityId CharacterC = new("character:fictional/chun");
    private static readonly EntityId CharacterD = new("character:fictional/duri");
    private static readonly EntityId FamilyId = new("family:fictional/dawn_pine");
    private static readonly EntityId MainHouseholdId = new("household:fictional/dawn_pine");
    private static readonly EntityId BranchHouseholdId = new("household:fictional/river_stone");
    private static readonly EntityId AbilityId = new("ability:fictional/clear_judgment");
    private static readonly EntityId AptitudeId = new("aptitude:fictional/river_travel");
    private static readonly EntityId TraitId = new("trait:fictional/patient_listener");
    private static readonly EntityId AmbitionId = new("ambition:fictional/restore_orchard");
    private static readonly EntityId ReputationId = new("reputation:fictional/fair_mediator");
    private static readonly EntityId FlawId = new("flaw:fictional/overcautious");
    private static readonly EntityId CourtesyNameKey = new("loc:fictional/ara_courtesy");
    private static readonly EntityId CultureId = new("culture:fictional/riverfolk");
    private static readonly EntityId OriginLocationId = new("locality:fictional/pine_crossing");

    private static readonly (EntityId Id, CharacterIdentityKind Kind, EntityId NameKey, string Korean, string English)[] Identities =
    [
        (AbilityId, CharacterIdentityKind.Ability, new("loc:fictional/clear_judgment"), "맑은 판단", "Clear Judgment"),
        (AptitudeId, CharacterIdentityKind.Aptitude, new("loc:fictional/river_travel"), "강길 익숙함", "River Travel"),
        (TraitId, CharacterIdentityKind.Trait, new("loc:fictional/patient_listener"), "참을성 있는 경청", "Patient Listener"),
        (AmbitionId, CharacterIdentityKind.Ambition, new("loc:fictional/restore_orchard"), "과수원 재건", "Restore the Orchard"),
        (ReputationId, CharacterIdentityKind.Reputation, new("loc:fictional/fair_mediator"), "공정한 중재자", "Fair Mediator"),
    ];

    private static readonly (EntityId Id, EntityId NameKey, string Korean, string English)[] NamedDefinitions =
    [
        (CharacterA, new("loc:fictional/ara"), "아라", "Ara"),
        (CharacterB, new("loc:fictional/boram"), "보람", "Boram"),
        (CharacterC, new("loc:fictional/chun"), "춘", "Chun"),
        (CharacterD, new("loc:fictional/duri"), "두리", "Duri"),
        (FamilyId, new("loc:fictional/dawn_pine_family"), "새벽솔 가문", "Dawn Pine Family"),
        (MainHouseholdId, new("loc:fictional/dawn_pine_household"), "새벽솔 집안", "Dawn Pine Household"),
        (BranchHouseholdId, new("loc:fictional/river_stone_household"), "강돌 집안", "River Stone Household"),
    ];

    [Fact]
    public void LoadsFictionalBilingualFamilyAndIndependentHouseholds()
    {
        using ContentPackFixture fixture = new();
        PackFiles files = WriteWorldPacks(fixture);
        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.False(content.Report.HasErrors);
        CharacterWorldSnapshot snapshot = CharacterContentLoader.LoadSingleWorld(content.Registry);
        CharacterWorldState world = new(snapshot, new CampaignDate(191, 4, 1));

        Assert.Equal(4, snapshot.CharacterDefinitions.Count);
        Assert.Equal(
            Enum.GetValues<CharacterIdentityKind>().Where(kind => kind != CharacterIdentityKind.Flaw).Order(),
            snapshot.IdentityDefinitions.Select(definition => definition.Kind).Order());
        Assert.Equal(CharacterContractVersions.Snapshot, snapshot.ContractVersion);
        Assert.All(snapshot.CharacterDefinitions, definition =>
            Assert.Equal(CharacterContractVersions.Definition, definition.ContractVersion));
        Assert.All(snapshot.CharacterStates, state =>
            Assert.Equal(CharacterContractVersions.State, state.ContractVersion));

        CharacterDefinition araDefinition = Assert.Single(
            snapshot.CharacterDefinitions,
            definition => definition.Id == CharacterA);
        Assert.Equal([AbilityId], araDefinition.AbilityIds);
        Assert.Equal([AptitudeId], araDefinition.AptitudeIds);
        Assert.Equal([TraitId], araDefinition.TraitIds);
        Assert.Equal([AmbitionId], araDefinition.AmbitionIds);
        Assert.Equal([ReputationId], araDefinition.ReputationIds);

        AuthoritativeCharacterProfile child = Assert.Single(world.Profiles, profile => profile.CharacterId == CharacterC);
        Assert.Equal(25, child.Age);
        Assert.Equal([CharacterA], child.ParentIds);
        CharacterParentLink migratedParent = Assert.Single(child.ParentLinks);
        Assert.Equal(ParentChildLinkKind.UnspecifiedLegacy, migratedParent.Kind);
        Assert.Equal(CharacterConditionState.Default, child.Condition);
        Assert.Equal(FamilyId, child.FamilyId);
        Assert.Equal(MainHouseholdId, child.HouseholdId);
        AuthoritativeCharacterProfile branchMember = Assert.Single(world.Profiles, profile => profile.CharacterId == CharacterD);
        Assert.Equal(FamilyId, branchMember.FamilyId);
        Assert.Equal(BranchHouseholdId, branchMember.HouseholdId);

        FamilyState family = Assert.Single(snapshot.FamilyStates);
        Assert.Equal([CharacterA, CharacterB, CharacterC, CharacterD], family.MemberIds);
        AuthoritativeHouseholdView mainHousehold = Assert.Single(world.Households, household => household.HouseholdId == MainHouseholdId);
        Assert.Equal(CharacterA, mainHousehold.HeadCharacterId);
        Assert.Equal([CharacterA, CharacterB, CharacterC], mainHousehold.MemberIds);

        EntityId childNameKey = Assert.Single(
            snapshot.CharacterDefinitions,
            definition => definition.Id == CharacterC).NameKey;
        Assert.True(content.Registry.TryGetText(childNameKey, "ko-KR", out string? korean));
        Assert.True(content.Registry.TryGetText(childNameKey, "en-US", out string? english));
        Assert.Equal("춘", korean);
        Assert.Equal("Chun", english);

        Assert.Equal(CharacterOriginKind.Authored, araDefinition.ContentOrigin!.OriginKind);
        Assert.Equal(CharacterHistoricalClassification.Fictional, araDefinition.ContentOrigin.HistoricalClassification);
        Assert.Equal(BasePackId, araDefinition.ContentOrigin.OwningPackId);
        Assert.Empty(araDefinition.ContentOrigin.SourceIds);
    }

    [Fact]
    public void LoadsStrictVersionTwoDescriptorsConditionsTypedKinshipAndReferenceBoundary()
    {
        using ContentPackFixture fixture = new();
        PackFiles files = WriteWorldPacks(
            fixture,
            definitions: CreateCurrentDefinitionRecords(),
            worlds: [CreateCurrentWorldRecord(WorldId)],
            localizationCsv: CreateCurrentLocalizationCsv());

        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.False(content.Report.HasErrors);
        CharacterWorldSnapshot snapshot = CharacterContentLoader.LoadSingleWorld(content.Registry);
        CharacterDefinition ara = Assert.Single(
            snapshot.CharacterDefinitions,
            definition => definition.Id == CharacterA);
        Assert.Equal(new StructuredCharacterName(ara.NameKey, CourtesyNameKey), ara.StructuredName);
        Assert.Equal(CultureId, ara.CultureId);
        Assert.Equal(OriginLocationId, ara.OriginLocationId);
        Assert.Equal([FlawId], ara.FlawIds);
        Assert.Equal(CharacterOriginKind.Custom, ara.ContentOrigin!.OriginKind);
        Assert.Equal(BasePackId, ara.ContentOrigin.OwningPackId);

        CharacterState child = Assert.Single(snapshot.CharacterStates, state => state.CharacterId == CharacterC);
        CharacterParentLink parent = Assert.Single(child.ParentLinks!);
        Assert.Equal(CharacterA, parent.ParentCharacterId);
        Assert.Equal(ParentChildLinkKind.Biological, parent.Kind);
        Assert.Equal(CharacterCustodyStatus.Captive, child.Condition!.CustodyStatus);
        Assert.Equal(CharacterB, child.Condition.CustodianId);

        Assert.True(content.Registry.TryGetText(CourtesyNameKey, "ko-KR", out string? korean));
        Assert.True(content.Registry.TryGetText(CourtesyNameKey, "en-US", out string? english));
        Assert.Equal("자명", korean);
        Assert.Equal("Jamyeong", english);
    }

    [Theory]
    [InlineData("missingCourtesyEnglish", "character.record")]
    [InlineData("missingCultureRecord", "record.reference")]
    [InlineData("nullCondition", "character.world")]
    [InlineData("mismatchedParentLinks", "character.world")]
    public void StrictVersionTwoFailuresAreControlledAndRejectTheOwningPack(
        string mutation,
        string expectedCode)
    {
        using ContentPackFixture fixture = new();
        List<ContentRecord> definitions = CreateCurrentDefinitionRecords().ToList();
        ContentRecord world = CreateCurrentWorldRecord(WorldId);
        string localization = CreateCurrentLocalizationCsv(
            omitEnglishKey: mutation == "missingCourtesyEnglish" ? CourtesyNameKey : null);
        if (mutation == "missingCultureRecord")
        {
            definitions.RemoveAll(record => record.Id == CultureId);
        }
        else if (mutation is "nullCondition" or "mismatchedParentLinks")
        {
            JsonArray states = world.Data["characterStates"]!.AsArray();
            JsonObject child = states.Single(node =>
                node!["characterId"]!.GetValue<string>() == CharacterC.Value)!.AsObject();
            if (mutation == "nullCondition")
            {
                child["condition"] = null;
            }
            else
            {
                child["parentLinks"] = new JsonArray();
            }
        }

        PackFiles files = WriteWorldPacks(
            fixture,
            definitions: definitions,
            worlds: [world],
            localizationCsv: localization);
        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        Assert.Contains(content.Report.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
        Assert.DoesNotContain(content.LoadOrder, pack => pack.Manifest.PackId == (expectedCode == "character.world"
            ? WorldPackId
            : BasePackId));
    }

    [Fact]
    public void ShuffledManifestsRecordsAndWorldListsProduceTheSameCanonicalSnapshot()
    {
        using ContentPackFixture orderedFixture = new();
        using ContentPackFixture shuffledFixture = new();
        PackFiles orderedFiles = WriteWorldPacks(orderedFixture);
        PackFiles shuffledFiles = WriteWorldPacks(shuffledFixture, reverse: true);
        ContentPackLoader loader = new();

        ContentLoadResult ordered = loader.Load(
            [orderedFiles.BaseManifest, orderedFiles.WorldManifest],
            "0.1.0",
            orderedFixture.Root);
        ContentLoadResult shuffled = loader.Load(
            [shuffledFiles.WorldManifest, shuffledFiles.BaseManifest],
            "0.1.0",
            shuffledFixture.Root);

        Assert.False(ordered.Report.HasErrors);
        Assert.False(shuffled.Report.HasErrors);
        string expected = JsonSerializer.Serialize(
            CharacterContentLoader.LoadSingleWorld(ordered.Registry),
            ContentJson.CreateOptions());
        string actual = JsonSerializer.Serialize(
            CharacterContentLoader.LoadSingleWorld(shuffled.Registry),
            ContentJson.CreateOptions());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FixtureFacingNameRequiresBothLaunchLanguages()
    {
        using ContentPackFixture fixture = new();
        EntityId missingEnglish = NamedDefinitions.Single(item => item.Id == CharacterD).NameKey;
        PackFiles files = WriteWorldPacks(
            fixture,
            localizationCsv: CreateLocalizationCsv(missingEnglish));
        ContentLoadResult content = new ContentPackLoader().Load(
            [files.BaseManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            content.Report.Diagnostics,
            item => item.Code == "character.record");
        Assert.Contains("en-US", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(missingEnglish.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(content.LoadOrder, pack => pack.Manifest.PackId == WorldPackId);
    }

    [Fact]
    public void DeclaredNameKeyWithoutAnyLocalizedTextFailsRegistryConstruction()
    {
        using ContentPackFixture fixture = new();
        EntityId missingNameKey = new("loc:character/fictional_missing_name");
        List<ContentRecord> definitions = CreateDefinitionRecords().ToList();
        int index = definitions.FindIndex(record => record.Id == CharacterA);
        ContentRecord character = definitions[index];
        definitions[index] = character with
        {
            LocalizationKeys = [missingNameKey],
            Data = SetProperty(
                (JsonObject)character.Data.DeepClone(),
                "nameKey",
                JsonValue.Create(missingNameKey.Value)),
        };
        PackFiles files = WriteWorldPacks(fixture, definitions: definitions);

        ContentLoadResult content = new ContentPackLoader().Load(
            [files.BaseManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            content.Report.Diagnostics,
            item => item.Code == "character.record");
        Assert.Contains("ko-KR", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(missingNameKey.Value, diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DanglingOrWrongDefinitionRecordFailsBeforeReturning(bool wrongRecordType)
    {
        using ContentPackFixture fixture = new();
        EntityId badId = new("character:fictional/missing_or_wrong");
        List<ContentRecord> definitions = CreateDefinitionRecords().ToList();
        if (wrongRecordType)
        {
            definitions.Add(ContentPackFixture.FictionalRecord(badId.Value));
        }

        ContentRecord world = CreateWorldRecord(
            WorldId,
            extraCharacterDefinitionId: badId);
        PackFiles files = WriteWorldPacks(fixture, definitions: definitions, worlds: [world]);
        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            content.Report.Diagnostics,
            item => item.Code == (wrongRecordType ? "character.world" : "record.reference"));
        Assert.Contains(
            wrongRecordType ? "must have type" : "broken content reference",
            diagnostic.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(content.LoadOrder, pack => pack.Manifest.PackId == WorldPackId);
    }

    [Theory]
    [InlineData("world")]
    [InlineData("definition")]
    public void UnsupportedWorldOrDefinitionVersionFails(string target)
    {
        using ContentPackFixture fixture = new();
        IReadOnlyList<ContentRecord> definitions = CreateDefinitionRecords(
            firstCharacterVersion: target == "definition" ? 3 : 1);
        ContentRecord world = CreateWorldRecord(WorldId, contractVersion: target == "world" ? 3 : 1);
        PackFiles files = WriteWorldPacks(fixture, definitions: definitions, worlds: [world]);
        ContentLoadResult content = new ContentPackLoader().Load(
            [files.BaseManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            content.Report.Diagnostics,
            item => item.Code == (target == "world" ? "character.world" : "character.record"));
        Assert.Contains("unsupported", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(content.LoadOrder, pack => pack.Manifest.PackId == WorldPackId);
    }

    [Theory]
    [InlineData("references")]
    [InlineData("localizationKeys")]
    [InlineData("missingBirthDate")]
    public void TypedMetadataAndRequiredFieldsAreRejectedDuringRegistryConstruction(string mutation)
    {
        using ContentPackFixture fixture = new();
        List<ContentRecord> definitions = CreateDefinitionRecords().ToList();
        int index = definitions.FindIndex(record => record.Id == CharacterA);
        ContentRecord character = definitions[index];
        JsonObject data = (JsonObject)character.Data.DeepClone();
        definitions[index] = mutation switch
        {
            "references" => character with
            {
                Data = SetProperty(data, "references", JsonSerializer.SerializeToNode(
                    Array.Empty<EntityId>(),
                    ContentJson.CreateOptions())),
            },
            "localizationKeys" => character with { LocalizationKeys = [] },
            "missingBirthDate" => character with { Data = RemoveProperty(data, "birthDate") },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
        PackFiles files = WriteWorldPacks(fixture, definitions: definitions);

        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            content.Report.Diagnostics,
            item => item.Code == "character.record");
        Assert.Contains(
            mutation == "localizationKeys" ? "localizationKeys" : mutation == "references" ? "references" : "malformed",
            diagnostic.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidResolvedModIsRejectedAndPreservesThePreviouslyValidRegistry()
    {
        using ContentPackFixture fixture = new();
        PackFiles files = WriteWorldPacks(fixture);
        EntityId modPackId = new("pack:fictional_character_invalid_mod");
        ContentOverride invalidReferences = new(
            1,
            CharacterA,
            [new FieldOverride(
                "/data/references",
                JsonSerializer.SerializeToNode(Array.Empty<EntityId>(), ContentJson.CreateOptions()))]);
        string modManifest = fixture.WritePack(
            modPackId,
            priority: 20,
            dependencies: [new ContentDependency(WorldPackId, "1.0.0", true)],
            overrides: [invalidReferences]);

        ContentPackLoader loader = new();
        ContentLoadResult content = loader.Load(
            [modManifest, files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);
        ContentLoadResult shuffled = loader.Load(
            [files.BaseManifest, modManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        Assert.Contains(content.Report.Diagnostics, item => item.Code == "character.record");
        Assert.Equal([BasePackId, WorldPackId], content.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.Equal(content.Report.Diagnostics, shuffled.Report.Diagnostics);
        Assert.Equal(content.LoadOrder.Select(pack => pack.Manifest.PackId),
            shuffled.LoadOrder.Select(pack => pack.Manifest.PackId));
        CharacterDefinition preserved = Assert.Single(
            CharacterContentLoader.LoadWorld(content.Registry, WorldId).CharacterDefinitions,
            definition => definition.Id == CharacterA);
        Assert.Equal(new CampaignDate(130, 2, 10), preserved.BirthDate);
        Assert.Empty(preserved.ContentOrigin!.AppliedOverridePackIds);
        Assert.Equal([AbilityId, AmbitionId, AptitudeId, ReputationId, TraitId],
            preserved.AbilityIds
                .Concat(preserved.AmbitionIds)
                .Concat(preserved.AptitudeIds)
                .Concat(preserved.ReputationIds)
                .Concat(preserved.TraitIds)
                .Order());
    }

    [Theory]
    [InlineData("versionedCharacter")]
    [InlineData("malformedCharacter")]
    [InlineData("identity")]
    [InlineData("family")]
    [InlineData("household")]
    [InlineData("numericReferences")]
    [InlineData("duplicateIdentityIds")]
    public void UnselectedInvalidTypedRecordRejectsItsOwningPackDeterministically(string mutation)
    {
        using ContentPackFixture fixture = new();
        List<ContentRecord> definitions = CreateDefinitionRecords().ToList();
        ContentRecord invalid = CreateUnselectedInvalidTypedRecord(mutation, definitions);
        definitions.Add(invalid);
        PackFiles files = WriteWorldPacks(fixture, definitions: definitions);
        ContentPackLoader loader = new();

        ContentLoadResult first = loader.Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);
        ContentLoadResult shuffled = loader.Load(
            [files.BaseManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(first.Report.HasErrors);
        string expectedCode = mutation == "numericReferences"
            ? "record.reference"
            : "character.record";
        ContentDiagnostic diagnostic = Assert.Single(
            first.Report.Diagnostics,
            item => item.Code == expectedCode);
        Assert.Equal(invalid.Id, diagnostic.RecordId);
        Assert.Equal(
            Path.GetRelativePath(fixture.Root, files.BaseManifest).Replace('\\', '/'),
            diagnostic.File);
        Assert.DoesNotContain(first.Report.Diagnostics, item => item.Code == "character.world");
        Assert.Contains(first.Report.Diagnostics, item =>
            item.Code == "dependency.invalid" && item.RecordId == WorldPackId);
        Assert.DoesNotContain(first.LoadOrder, pack => pack.Manifest.PackId == BasePackId);
        Assert.DoesNotContain(first.LoadOrder, pack => pack.Manifest.PackId == WorldPackId);
        Assert.Equal(first.Report.Diagnostics, shuffled.Report.Diagnostics);
        Assert.Equal(
            first.LoadOrder.Select(pack => pack.Manifest.PackId),
            shuffled.LoadOrder.Select(pack => pack.Manifest.PackId));
    }

    [Theory]
    [InlineData("characterStates", "parentIds", "characterStates[].parentIds")]
    [InlineData("familyStates", "memberIds", "familyStates[].memberIds")]
    [InlineData("householdStates", "memberIds", "householdStates[].memberIds")]
    public void NullNestedWorldCollectionRejectsModAndPreservesPriorRegistryDeterministically(
        string stateCollection,
        string nestedCollection,
        string expectedDiagnostic)
    {
        using ContentPackFixture fixture = new();
        PackFiles files = WriteWorldPacks(fixture);
        ContentPackLoader loader = new();
        ContentLoadResult baseline = loader.Load(
            [files.BaseManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);
        string expectedSnapshot = JsonSerializer.Serialize(
            CharacterContentLoader.LoadWorld(baseline.Registry, WorldId),
            ContentJson.CreateOptions());

        ContentRecord world = CreateWorldRecord(WorldId);
        JsonArray invalidStates = (JsonArray)world.Data[stateCollection]!.DeepClone();
        invalidStates[0]![nestedCollection] = null;
        EntityId modPackId = new($"pack:fictional_null_{stateCollection.ToLowerInvariant()}");
        ContentOverride invalidOverride = new(
            1,
            WorldId,
            [new FieldOverride($"/data/{stateCollection}", invalidStates)]);
        string modManifest = fixture.WritePack(
            modPackId,
            priority: 20,
            dependencies: [new ContentDependency(WorldPackId, "1.0.0", true)],
            overrides: [invalidOverride]);

        ContentLoadResult first = loader.Load(
            [modManifest, files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);
        ContentLoadResult shuffled = loader.Load(
            [files.BaseManifest, modManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(first.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            first.Report.Diagnostics,
            item => item.Code == "character.world");
        Assert.Equal(WorldId, diagnostic.RecordId);
        Assert.Equal(
            Path.GetRelativePath(fixture.Root, modManifest).Replace('\\', '/'),
            diagnostic.File);
        Assert.Contains(expectedDiagnostic, diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(first.LoadOrder, pack => pack.Manifest.PackId == modPackId);
        Assert.Equal([BasePackId, WorldPackId], first.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.Equal(expectedSnapshot, JsonSerializer.Serialize(
            CharacterContentLoader.LoadWorld(first.Registry, WorldId),
            ContentJson.CreateOptions()));
        Assert.Equal(first.Report.Diagnostics, shuffled.Report.Diagnostics);
        Assert.Equal(
            first.LoadOrder.Select(pack => pack.Manifest.PackId),
            shuffled.LoadOrder.Select(pack => pack.Manifest.PackId));
    }

    [Theory]
    [InlineData("/sourceIds")]
    [InlineData("/localizationKeys")]
    public void NullRecordEnvelopeOverrideRejectsModAndPreservesPriorRegistryDeterministically(
        string jsonPath)
    {
        using ContentPackFixture fixture = new();
        PackFiles files = WriteWorldPacks(fixture);
        ContentPackLoader loader = new();
        ContentLoadResult baseline = loader.Load(
            [files.BaseManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);
        string expectedSnapshot = JsonSerializer.Serialize(
            CharacterContentLoader.LoadWorld(baseline.Registry, WorldId),
            ContentJson.CreateOptions());

        EntityId modPackId = new($"pack:fictional_null_{jsonPath[1..].ToLowerInvariant()}");
        string modManifest = fixture.WritePack(
            modPackId,
            priority: 20,
            dependencies: [new ContentDependency(WorldPackId, "1.0.0", true)],
            overrides:
            [
                new ContentOverride(1, CharacterA, [new FieldOverride(jsonPath, null)]),
            ]);

        ContentLoadResult first = loader.Load(
            [modManifest, files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);
        ContentLoadResult shuffled = loader.Load(
            [files.BaseManifest, modManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        ContentDiagnostic diagnostic = Assert.Single(
            first.Report.Diagnostics,
            item => item.Code == "record.contract");
        Assert.Equal(CharacterA, diagnostic.RecordId);
        Assert.Equal(
            Path.GetRelativePath(fixture.Root, modManifest).Replace('\\', '/'),
            diagnostic.File);
        Assert.DoesNotContain(first.LoadOrder, pack => pack.Manifest.PackId == modPackId);
        Assert.Equal([BasePackId, WorldPackId], first.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.Equal(expectedSnapshot, JsonSerializer.Serialize(
            CharacterContentLoader.LoadWorld(first.Registry, WorldId),
            ContentJson.CreateOptions()));
        Assert.Equal(first.Report.Diagnostics, shuffled.Report.Diagnostics);
        Assert.Equal(
            first.LoadOrder.Select(pack => pack.Manifest.PackId),
            shuffled.LoadOrder.Select(pack => pack.Manifest.PackId));
    }

    [Fact]
    public void GenericCharacterRecordDoesNotEnterSp04aTypedValidation()
    {
        using ContentPackFixture fixture = new();
        List<ContentRecord> definitions = CreateDefinitionRecords().ToList();
        definitions.Add(ContentPackFixture.FictionalRecord("character:fictional/generic_record"));
        PackFiles files = WriteWorldPacks(fixture, definitions: definitions);

        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.False(content.Report.HasErrors);
        Assert.True(content.Registry.TryGet(new EntityId("character:fictional/generic_record"), out _));
    }

    [Fact]
    public void LoadSingleWorldRequiresAnUnambiguousSelection()
    {
        using ContentPackFixture emptyFixture = new();
        PackFiles emptyFiles = WriteWorldPacks(emptyFixture, worlds: []);
        ContentLoadResult empty = new ContentPackLoader().Load(
            [emptyFiles.WorldManifest, emptyFiles.BaseManifest],
            "0.1.0",
            emptyFixture.Root);
        Assert.False(empty.Report.HasErrors);
        Assert.Throws<InvalidDataException>(() => CharacterContentLoader.LoadSingleWorld(empty.Registry));

        using ContentPackFixture multipleFixture = new();
        ContentRecord first = CreateWorldRecord(WorldId);
        ContentRecord second = CreateWorldRecord(new EntityId("character_world:river_stone"), reverse: true);
        PackFiles multipleFiles = WriteWorldPacks(multipleFixture, worlds: [second, first]);
        ContentLoadResult multiple = new ContentPackLoader().Load(
            [multipleFiles.WorldManifest, multipleFiles.BaseManifest],
            "0.1.0",
            multipleFixture.Root);
        Assert.False(multiple.Report.HasErrors);
        Assert.Throws<InvalidDataException>(() => CharacterContentLoader.LoadSingleWorld(multiple.Registry));
        Assert.Equal(4, CharacterContentLoader.LoadWorld(multiple.Registry, WorldId).CharacterDefinitions.Count);
    }

    [Fact]
    public void RegistryValidationVisitsEveryResolvedCharacterWorldById()
    {
        using ContentPackFixture fixture = new();
        EntityId invalidWorldId = new("character_world:river_stone");
        ContentRecord valid = CreateWorldRecord(WorldId);
        ContentRecord invalid = CreateWorldRecord(invalidWorldId, contractVersion: 3, reverse: true);
        PackFiles files = WriteWorldPacks(fixture, worlds: [invalid, valid]);

        ContentLoadResult content = new ContentPackLoader().Load(
            [files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);

        Assert.True(content.Report.HasErrors);
        ContentDiagnostic diagnostic = Assert.Single(
            content.Report.Diagnostics,
            item => item.Code == "character.world");
        Assert.Equal(invalidWorldId, diagnostic.RecordId);
        Assert.DoesNotContain(content.LoadOrder, pack => pack.Manifest.PackId == WorldPackId);
    }

    [Fact]
    public void ResolvedModOverrideWinsRegardlessOfManifestInputOrder()
    {
        using ContentPackFixture fixture = new();
        PackFiles files = WriteWorldPacks(fixture);
        EntityId modPackId = new("pack:fictional_character_dates_mod");
        ContentOverride dateOverride = new(
            1,
            CharacterA,
            [new FieldOverride("/data/birthDate/year", JsonValue.Create(129))]);
        string modManifest = fixture.WritePack(
            modPackId,
            priority: 20,
            dependencies: [new ContentDependency(WorldPackId, "1.0.0", true)],
            overrides: [dateOverride]);
        ContentPackLoader loader = new();

        ContentLoadResult first = loader.Load(
            [modManifest, files.WorldManifest, files.BaseManifest],
            "0.1.0",
            fixture.Root);
        ContentLoadResult second = loader.Load(
            [files.BaseManifest, modManifest, files.WorldManifest],
            "0.1.0",
            fixture.Root);

        Assert.False(first.Report.HasErrors);
        Assert.False(second.Report.HasErrors);
        Assert.Equal([BasePackId, WorldPackId, modPackId], first.LoadOrder.Select(pack => pack.Manifest.PackId));
        CharacterDefinition overridden = Assert.Single(
            CharacterContentLoader.LoadSingleWorld(first.Registry).CharacterDefinitions,
            definition => definition.Id == CharacterA);
        Assert.Equal(129, overridden.BirthDate.Year);
        Assert.Equal(BasePackId, overridden.ContentOrigin!.OwningPackId);
        Assert.Equal([modPackId], overridden.ContentOrigin.AppliedOverridePackIds);
        Assert.Equal(
            JsonSerializer.Serialize(CharacterContentLoader.LoadSingleWorld(first.Registry), ContentJson.CreateOptions()),
            JsonSerializer.Serialize(CharacterContentLoader.LoadSingleWorld(second.Registry), ContentJson.CreateOptions()));
    }

    private static PackFiles WriteWorldPacks(
        ContentPackFixture fixture,
        bool reverse = false,
        IReadOnlyList<ContentRecord>? definitions = null,
        IReadOnlyList<ContentRecord>? worlds = null,
        string? localizationCsv = null)
    {
        ContentRecord[] definitionRecords = (definitions ?? CreateDefinitionRecords())
            .ToArray();
        ContentRecord[] worldRecords = (worlds ?? [CreateWorldRecord(WorldId, reverse: reverse)])
            .ToArray();
        if (reverse)
        {
            Array.Reverse(definitionRecords);
            Array.Reverse(worldRecords);
        }

        string baseManifest = fixture.WritePack(
            BasePackId,
            builtIn: true,
            records: definitionRecords,
            localizationCsv: localizationCsv ?? CreateLocalizationCsv());
        string worldManifest = fixture.WritePack(
            WorldPackId,
            builtIn: true,
            dependencies: [new ContentDependency(BasePackId, "1.0.0", true)],
            records: worldRecords);
        return new PackFiles(baseManifest, worldManifest);
    }

    private static IReadOnlyList<ContentRecord> CreateDefinitionRecords(int firstCharacterVersion = 1)
    {
        List<ContentRecord> records = Identities
            .Select(identity => Record(
                identity.Id,
                "character_identity_definition",
                Data(new
                {
                    contractVersion = 1,
                    kind = identity.Kind,
                    nameKey = identity.NameKey,
                    references = Array.Empty<EntityId>(),
                }),
                identity.NameKey))
            .ToList();
        records.AddRange(
        [
            CharacterRecord(
                CharacterA,
                firstCharacterVersion,
                new CampaignDate(130, 2, 10),
                [AbilityId],
                [AptitudeId],
                [TraitId],
                [AmbitionId],
                [ReputationId]),
            CharacterRecord(CharacterB, 1, new CampaignDate(136, 7, 11), [], [], [], [], []),
            CharacterRecord(CharacterC, 1, new CampaignDate(166, 3, 5), [], [], [TraitId], [], []),
            CharacterRecord(CharacterD, 1, new CampaignDate(150, 9, 2), [], [AptitudeId], [], [], []),
            DefinitionRecord(FamilyId, "family_definition"),
            DefinitionRecord(MainHouseholdId, "household_definition"),
            DefinitionRecord(BranchHouseholdId, "household_definition"),
        ]);
        return records;
    }

    private static IReadOnlyList<ContentRecord> CreateCurrentDefinitionRecords()
    {
        List<ContentRecord> records = Identities
            .Select(identity => Record(
                identity.Id,
                "character_identity_definition",
                Data(new
                {
                    contractVersion = 2,
                    kind = identity.Kind,
                    nameKey = identity.NameKey,
                    references = Array.Empty<EntityId>(),
                }),
                identity.NameKey))
            .ToList();
        EntityId flawNameKey = new("loc:fictional/overcautious");
        records.Add(Record(
            FlawId,
            "character_identity_definition",
            Data(new
            {
                contractVersion = 2,
                kind = CharacterIdentityKind.Flaw,
                nameKey = flawNameKey,
                references = Array.Empty<EntityId>(),
            }),
            flawNameKey));
        records.AddRange(
        [
            CurrentCharacterRecord(
                CharacterA,
                new CampaignDate(130, 2, 10),
                [AbilityId],
                [AptitudeId],
                [TraitId],
                [AmbitionId],
                [ReputationId],
                [FlawId],
                CourtesyNameKey,
                CultureId,
                OriginLocationId),
            CurrentCharacterRecord(CharacterB, new CampaignDate(136, 7, 11), [], [], [], [], [], [], null, null, null),
            CurrentCharacterRecord(CharacterC, new CampaignDate(166, 3, 5), [], [], [TraitId], [], [], [], null, null, null),
            CurrentCharacterRecord(CharacterD, new CampaignDate(150, 9, 2), [], [AptitudeId], [], [], [], [], null, null, null),
            CurrentDefinitionRecord(FamilyId, "family_definition"),
            CurrentDefinitionRecord(MainHouseholdId, "household_definition"),
            CurrentDefinitionRecord(BranchHouseholdId, "household_definition"),
            ContentPackFixture.FictionalRecord(CultureId.Value),
            ContentPackFixture.FictionalRecord(OriginLocationId.Value),
        ]);
        return records;
    }

    private static ContentRecord CurrentCharacterRecord(
        EntityId id,
        CampaignDate birthDate,
        IReadOnlyList<EntityId> abilityIds,
        IReadOnlyList<EntityId> aptitudeIds,
        IReadOnlyList<EntityId> traitIds,
        IReadOnlyList<EntityId> ambitionIds,
        IReadOnlyList<EntityId> reputationIds,
        IReadOnlyList<EntityId> flawIds,
        EntityId? courtesyNameKey,
        EntityId? cultureId,
        EntityId? originLocationId)
    {
        EntityId nameKey = NamedDefinitions.Single(item => item.Id == id).NameKey;
        EntityId[] references = abilityIds
            .Concat(aptitudeIds)
            .Concat(traitIds)
            .Concat(ambitionIds)
            .Concat(reputationIds)
            .Concat(flawIds)
            .Concat(cultureId is EntityId culture ? [culture] : [])
            .Concat(originLocationId is EntityId origin ? [origin] : [])
            .Distinct()
            .Order()
            .ToArray();
        EntityId[] localizationKeys = courtesyNameKey is EntityId courtesy
            ? [nameKey, courtesy]
            : [nameKey];
        return Record(
            id,
            "character_definition",
            Data(new
            {
                contractVersion = 2,
                nameKey,
                courtesyNameKey,
                originKind = CharacterOriginKind.Custom,
                cultureId,
                originLocationId,
                birthDate,
                abilityIds,
                aptitudeIds,
                traitIds,
                ambitionIds,
                reputationIds,
                flawIds,
                references,
            }),
            localizationKeys.Order().ToArray());
    }

    private static ContentRecord CurrentDefinitionRecord(EntityId id, string recordType)
    {
        EntityId nameKey = NamedDefinitions.Single(item => item.Id == id).NameKey;
        return Record(
            id,
            recordType,
            Data(new
            {
                contractVersion = 2,
                nameKey,
                references = Array.Empty<EntityId>(),
            }),
            nameKey);
    }

    private static ContentRecord CharacterRecord(
        EntityId id,
        int contractVersion,
        CampaignDate birthDate,
        IReadOnlyList<EntityId> abilityIds,
        IReadOnlyList<EntityId> aptitudeIds,
        IReadOnlyList<EntityId> traitIds,
        IReadOnlyList<EntityId> ambitionIds,
        IReadOnlyList<EntityId> reputationIds)
    {
        EntityId nameKey = NamedDefinitions.Single(item => item.Id == id).NameKey;
        EntityId[] references = abilityIds
            .Concat(aptitudeIds)
            .Concat(traitIds)
            .Concat(ambitionIds)
            .Concat(reputationIds)
            .Distinct()
            .Order()
            .ToArray();
        return Record(
            id,
            "character_definition",
            Data(new
            {
                contractVersion,
                nameKey,
                birthDate,
                abilityIds,
                aptitudeIds,
                traitIds,
                ambitionIds,
                reputationIds,
                references,
            }),
            nameKey);
    }

    private static ContentRecord DefinitionRecord(EntityId id, string recordType)
    {
        EntityId nameKey = NamedDefinitions.Single(item => item.Id == id).NameKey;
        return Record(
            id,
            recordType,
            Data(new
            {
                contractVersion = 1,
                nameKey,
                references = Array.Empty<EntityId>(),
            }),
            nameKey);
    }

    private static ContentRecord CreateUnselectedInvalidTypedRecord(
        string mutation,
        IReadOnlyList<ContentRecord> definitions)
    {
        ContentRecord source;
        EntityId id;
        JsonObject data;
        switch (mutation)
        {
            case "versionedCharacter":
            case "malformedCharacter":
                source = definitions.Single(record => record.Id == CharacterA);
                id = new EntityId($"character:fictional/unselected_{mutation.ToLowerInvariant()}");
                data = (JsonObject)source.Data.DeepClone();
                if (mutation == "versionedCharacter")
                {
                    data["contractVersion"] = 3;
                }
                else
                {
                    data["birthDate"]!["year"] = 0;
                }

                break;
            case "identity":
                source = definitions.Single(record => record.Id == AbilityId);
                id = new EntityId("ability:fictional/unselected_invalid");
                data = (JsonObject)source.Data.DeepClone();
                data["kind"] = 999;
                break;
            case "family":
                source = definitions.Single(record => record.Id == FamilyId);
                id = new EntityId("family:fictional/unselected_invalid");
                data = (JsonObject)source.Data.DeepClone();
                data["contractVersion"] = 3;
                break;
            case "household":
                source = definitions.Single(record => record.Id == MainHouseholdId);
                id = new EntityId("household:fictional/unselected_invalid");
                data = (JsonObject)source.Data.DeepClone();
                data.Remove("references");
                break;
            case "numericReferences":
                source = definitions.Single(record => record.Id == MainHouseholdId);
                id = new EntityId("household:fictional/unselected_numeric_references");
                data = (JsonObject)source.Data.DeepClone();
                data["references"] = new JsonArray(123);
                break;
            case "duplicateIdentityIds":
                source = definitions.Single(record => record.Id == CharacterA);
                id = new EntityId("character:fictional/unselected_duplicate_identity_ids");
                data = (JsonObject)source.Data.DeepClone();
                data["abilityIds"]!.AsArray().Add(AbilityId.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        return source with { Id = id, Data = data };
    }

    private static ContentRecord CreateWorldRecord(
        EntityId id,
        int contractVersion = 1,
        bool reverse = false,
        EntityId? extraCharacterDefinitionId = null)
    {
        EntityId[] identityIds = MaybeReverse(Identities.Select(identity => identity.Id), reverse);
        EntityId[] characterIds = MaybeReverse(
            new[] { CharacterA, CharacterB, CharacterC, CharacterD }
                .Concat(extraCharacterDefinitionId is EntityId extra ? [extra] : []),
            reverse);
        EntityId[] familyIds = [FamilyId];
        EntityId[] householdIds = MaybeReverse([MainHouseholdId, BranchHouseholdId], reverse);
        LegacyCharacterStateFixture[] characterStates = MaybeReverse<LegacyCharacterStateFixture>(
        [
            new(1, CharacterA, []),
            new(1, CharacterB, []),
            new(1, CharacterC, [CharacterA]),
            new(1, CharacterD, []),
        ], reverse);
        FamilyState[] familyStates =
        [
            new(1, FamilyId, MaybeReverse([CharacterA, CharacterB, CharacterC, CharacterD], reverse)),
        ];
        HouseholdState[] householdStates = MaybeReverse<HouseholdState>(
        [
            new(1, MainHouseholdId, CharacterA, MaybeReverse([CharacterA, CharacterB, CharacterC], reverse)),
            new(1, BranchHouseholdId, CharacterD, [CharacterD]),
        ], reverse);
        IEnumerable<EntityId> references = identityIds
            .Concat(characterIds)
            .Concat(familyIds)
            .Concat(householdIds);
        return Record(
            id,
            "character_world",
            Data(new
            {
                contractVersion,
                identityDefinitionIds = identityIds,
                characterDefinitionIds = characterIds,
                familyDefinitionIds = familyIds,
                householdDefinitionIds = householdIds,
                characterStates,
                familyStates,
                householdStates,
                references = references.Order().ToArray(),
            }));
    }

    private static ContentRecord CreateCurrentWorldRecord(EntityId id)
    {
        EntityId[] identityIds = Identities.Select(identity => identity.Id).Append(FlawId).Order().ToArray();
        EntityId[] characterIds = [CharacterA, CharacterB, CharacterC, CharacterD];
        EntityId[] familyIds = [FamilyId];
        EntityId[] householdIds = [BranchHouseholdId, MainHouseholdId];
        CharacterState[] characterStates =
        [
            new(2, CharacterA, [], [], CharacterConditionState.Default),
            new(2, CharacterB, [], [], CharacterConditionState.Default),
            new(2, CharacterC, [CharacterA], [new CharacterParentLink(CharacterA, ParentChildLinkKind.Biological)], new CharacterConditionState(
                CharacterVitalStatus.Alive,
                CharacterHealthStatus.Healthy,
                false,
                CharacterCustodyStatus.Captive,
                CharacterB)),
            new(2, CharacterD, [], [], CharacterConditionState.Default),
        ];
        FamilyState[] familyStates =
        [
            new(2, FamilyId, [CharacterA, CharacterB, CharacterC, CharacterD]),
        ];
        HouseholdState[] householdStates =
        [
            new(2, BranchHouseholdId, CharacterD, [CharacterD]),
            new(2, MainHouseholdId, CharacterA, [CharacterA, CharacterB, CharacterC]),
        ];
        EntityId[] references = identityIds
            .Concat(characterIds)
            .Concat(familyIds)
            .Concat(householdIds)
            .Distinct()
            .Order()
            .ToArray();
        return Record(
            id,
            "character_world",
            Data(new
            {
                contractVersion = 2,
                identityDefinitionIds = identityIds,
                characterDefinitionIds = characterIds,
                familyDefinitionIds = familyIds,
                householdDefinitionIds = householdIds,
                characterStates,
                familyStates,
                householdStates,
                references,
            }));
    }

    private static ContentRecord Record(
        EntityId id,
        string recordType,
        JsonObject data,
        params EntityId[] localizationKeys) => new(
            1,
            id,
            recordType,
            ContentTag.Fictional,
            ContentClassification.General,
            [],
            localizationKeys,
            false,
            data);

    private static JsonObject Data<T>(T value) =>
        JsonSerializer.SerializeToNode(value, ContentJson.CreateOptions())!.AsObject();

    private static JsonObject SetProperty(JsonObject data, string property, JsonNode? value)
    {
        data[property] = value;
        return data;
    }

    private static JsonObject RemoveProperty(JsonObject data, string property)
    {
        data.Remove(property);
        return data;
    }

    private static T[] MaybeReverse<T>(IEnumerable<T> values, bool reverse)
    {
        T[] items = values.ToArray();
        if (reverse)
        {
            Array.Reverse(items);
        }

        return items;
    }

    private static string CreateLocalizationCsv(EntityId? omitEnglishKey = null)
    {
        StringBuilder csv = new(
            "key,locale,text,context,variables,review_state,source_content_ids,release_marked\n");
        foreach ((EntityId _, CharacterIdentityKind _, EntityId key, string korean, string english) in Identities)
        {
            AddRows(key, korean, english);
        }

        foreach ((EntityId _, EntityId key, string korean, string english) in NamedDefinitions)
        {
            AddRows(key, korean, english);
        }

        return csv.ToString();

        void AddRows(EntityId key, string korean, string english)
        {
            csv.Append(key.Value).Append(",ko-KR,").Append(korean)
                .Append(",Fictional test name,,approved,,false\n");
            if (key != omitEnglishKey)
            {
                csv.Append(key.Value).Append(",en-US,").Append(english)
                    .Append(",Fictional test name,,approved,,false\n");
            }
        }
    }

    private static string CreateCurrentLocalizationCsv(EntityId? omitEnglishKey = null)
    {
        StringBuilder csv = new(CreateLocalizationCsv(omitEnglishKey));
        AddRows(new EntityId("loc:fictional/overcautious"), "지나친 신중함", "Overcautious");
        AddRows(CourtesyNameKey, "자명", "Jamyeong");
        return csv.ToString();

        void AddRows(EntityId key, string korean, string english)
        {
            csv.Append(key.Value).Append(",ko-KR,").Append(korean)
                .Append(",Fictional test name,,approved,,false\n");
            if (key != omitEnglishKey)
            {
                csv.Append(key.Value).Append(",en-US,").Append(english)
                    .Append(",Fictional test name,,approved,,false\n");
            }
        }
    }

    private sealed record PackFiles(string BaseManifest, string WorldManifest);

    private sealed record LegacyCharacterStateFixture(
        int ContractVersion,
        EntityId CharacterId,
        IReadOnlyList<EntityId> ParentIds);
}
