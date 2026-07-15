using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.Core;

namespace Game.Content;

public static class CharacterContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJson.CreateOptions();

    internal static bool IsTypedCharacterRecordType(string recordType) => recordType is
        "character_world"
        or "character_definition"
        or "character_identity_definition"
        or "family_definition"
        or "household_definition";

    internal static void ValidateOwnedRecord(ContentRegistry registry, EntityId recordId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.TryGet(recordId, out NormalizedContentRecord? record))
        {
            throw new InvalidDataException($"Owned character record '{recordId}' is missing from the candidate registry.");
        }

        switch (record.RecordType)
        {
            case "character_world":
                _ = ReadWorldData(record);
                break;
            case "character_definition":
                _ = LoadCharacterDefinition(registry, recordId);
                break;
            case "character_identity_definition":
                _ = LoadIdentityDefinition(registry, recordId);
                break;
            case "family_definition":
                _ = LoadFamilyDefinition(registry, recordId);
                break;
            case "household_definition":
                _ = LoadHouseholdDefinition(registry, recordId);
                break;
            default:
                throw new InvalidDataException(
                    $"Record '{recordId}' is not an SP-04 typed character record.");
        }
    }

    public static CharacterWorldSnapshot LoadWorld(ContentRegistry registry, EntityId scenarioId)
    {
        ArgumentNullException.ThrowIfNull(registry);

        NormalizedContentRecord worldRecord = GetRecord(registry, scenarioId, "character_world");
        NormalizedCharacterWorldData world = ReadWorldData(worldRecord);
        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            world.IdentityDefinitionIds.Select(id => LoadIdentityDefinition(registry, id)).ToArray(),
            world.CharacterDefinitionIds.Select(id => LoadCharacterDefinition(registry, id)).ToArray(),
            world.FamilyDefinitionIds.Select(id => LoadFamilyDefinition(registry, id)).ToArray(),
            world.HouseholdDefinitionIds.Select(id => LoadHouseholdDefinition(registry, id)).ToArray(),
            world.CharacterStates,
            world.FamilyStates,
            world.HouseholdStates);
        ValidateTypedReferences(snapshot, worldRecord);
        return snapshot.Canonicalize();
    }

    public static CharacterWorldSnapshot LoadSingleWorld(ContentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        NormalizedContentRecord[] worlds = registry.Records
            .Where(record => StringComparer.Ordinal.Equals(record.RecordType, "character_world"))
            .OrderBy(record => record.Id)
            .ToArray();
        return worlds.Length switch
        {
            1 => LoadWorld(registry, worlds[0].Id),
            0 => throw new InvalidDataException("No character_world record is loaded."),
            _ => throw new InvalidDataException("More than one character_world is loaded; select one explicitly."),
        };
    }

    private static NormalizedCharacterWorldData ReadWorldData(NormalizedContentRecord worldRecord)
    {
        int version = ReadContractVersion(worldRecord, "world");
        return version switch
        {
            CharacterContractVersions.LegacySnapshot => NormalizeLegacyWorld(
                Deserialize<LegacyCharacterWorldData>(worldRecord),
                worldRecord),
            CharacterContractVersions.Snapshot => NormalizeCurrentWorld(
                Deserialize<CurrentCharacterWorldData>(worldRecord),
                worldRecord),
            _ => throw UnsupportedVersion(worldRecord, "world", version),
        };
    }

    private static NormalizedCharacterWorldData NormalizeLegacyWorld(
        LegacyCharacterWorldData world,
        NormalizedContentRecord worldRecord)
    {
        RequireWorldLists(world, worldRecord);
        foreach (LegacyCharacterStateData state in world.CharacterStates)
        {
            RequireVersion(state.ContractVersion, CharacterContractVersions.LegacyState, worldRecord, "character state");
            RequireList(state.ParentIds, worldRecord, "characterStates[].parentIds");
        }

        ValidateCommonWorldStates(world.FamilyStates, world.HouseholdStates, CharacterContractVersions.LegacyState, worldRecord);
        CharacterState[] characters = world.CharacterStates.Select(state => new CharacterState(
            CharacterContractVersions.State,
            state.CharacterId,
            state.ParentIds,
            state.ParentIds.Select(parentId => new CharacterParentLink(
                parentId,
                ParentChildLinkKind.UnspecifiedLegacy)).ToArray(),
            CharacterConditionState.Default)).ToArray();
        FamilyState[] families = world.FamilyStates.Select(state => new FamilyState(
            CharacterContractVersions.State,
            state.FamilyId,
            state.MemberIds)).ToArray();
        HouseholdState[] households = world.HouseholdStates.Select(state => new HouseholdState(
            CharacterContractVersions.State,
            state.HouseholdId,
            state.HeadCharacterId,
            state.MemberIds)).ToArray();

        RequireWorldMetadata(
            worldRecord,
            world.References,
            world.IdentityDefinitionIds,
            world.CharacterDefinitionIds,
            world.FamilyDefinitionIds,
            world.HouseholdDefinitionIds,
            characters,
            families,
            households);
        return new(
            world.IdentityDefinitionIds,
            world.CharacterDefinitionIds,
            world.FamilyDefinitionIds,
            world.HouseholdDefinitionIds,
            characters,
            families,
            households);
    }

    private static NormalizedCharacterWorldData NormalizeCurrentWorld(
        CurrentCharacterWorldData world,
        NormalizedContentRecord worldRecord)
    {
        RequireWorldLists(world, worldRecord);
        foreach (CurrentCharacterStateData state in world.CharacterStates)
        {
            RequireVersion(state.ContractVersion, CharacterContractVersions.State, worldRecord, "character state");
            RequireList(state.ParentIds, worldRecord, "characterStates[].parentIds");
            RequireList(state.ParentLinks, worldRecord, "characterStates[].parentLinks");
            RequireCanonicalIds(state.ParentIds, worldRecord, "characterStates[].parentIds");
            EntityId[] linkedParentIds = state.ParentLinks.Select(link => link.ParentCharacterId).ToArray();
            RequireCanonicalIds(linkedParentIds, worldRecord, "characterStates[].parentLinks");
            if (!state.ParentIds.SequenceEqual(linkedParentIds))
            {
                throw new InvalidDataException(
                    $"Character record '{worldRecord.Id}' parentIds must exactly equal parentLinks IDs.");
            }

            if (state.ParentLinks.Any(link => !Enum.IsDefined(link.Kind)))
            {
                throw new InvalidDataException(
                    $"Character record '{worldRecord.Id}' has an invalid parent-link kind.");
            }

            ValidateCondition(state.Condition, worldRecord);
        }

        ValidateCommonWorldStates(world.FamilyStates, world.HouseholdStates, CharacterContractVersions.State, worldRecord);
        CharacterState[] characters = world.CharacterStates.Select(state => new CharacterState(
            CharacterContractVersions.State,
            state.CharacterId,
            state.ParentIds,
            state.ParentLinks.Select(link => new CharacterParentLink(link.ParentCharacterId, link.Kind)).ToArray(),
            new CharacterConditionState(
                state.Condition!.VitalStatus,
                state.Condition.HealthStatus,
                state.Condition.IsIncapacitated,
                state.Condition.CustodyStatus,
                state.Condition.CustodianId))).ToArray();
        FamilyState[] families = world.FamilyStates.Select(state => new FamilyState(
            CharacterContractVersions.State,
            state.FamilyId,
            state.MemberIds)).ToArray();
        HouseholdState[] households = world.HouseholdStates.Select(state => new HouseholdState(
            CharacterContractVersions.State,
            state.HouseholdId,
            state.HeadCharacterId,
            state.MemberIds)).ToArray();

        RequireWorldMetadata(
            worldRecord,
            world.References,
            world.IdentityDefinitionIds,
            world.CharacterDefinitionIds,
            world.FamilyDefinitionIds,
            world.HouseholdDefinitionIds,
            characters,
            families,
            households);
        return new(
            world.IdentityDefinitionIds,
            world.CharacterDefinitionIds,
            world.FamilyDefinitionIds,
            world.HouseholdDefinitionIds,
            characters,
            families,
            households);
    }

    private static CharacterIdentityDefinition LoadIdentityDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "character_identity_definition");
        CharacterIdentityDefinitionData data = Deserialize<CharacterIdentityDefinitionData>(record);
        RequireSupportedDefinitionVersion(data.ContractVersion, record);
        RequireList(data.References, record, "references");
        RequireMetadata(record, [data.NameKey], data.References, []);
        if (!Enum.IsDefined(data.Kind)
            || (data.ContractVersion == CharacterContractVersions.LegacyDefinition
                && data.Kind == CharacterIdentityKind.Flaw))
        {
            throw new InvalidDataException($"Character identity definition '{record.Id}' has an invalid kind.");
        }

        RequireBilingualName(registry, record, data.NameKey, "nameKey");
        return new CharacterIdentityDefinition(
            CharacterContractVersions.Definition,
            record.Id,
            data.Kind,
            data.NameKey);
    }

    private static CharacterDefinition LoadCharacterDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "character_definition");
        int version = ReadContractVersion(record);
        return version switch
        {
            CharacterContractVersions.LegacyDefinition => LoadLegacyCharacterDefinition(registry, record),
            CharacterContractVersions.Definition => LoadCurrentCharacterDefinition(registry, record),
            _ => throw UnsupportedVersion(record, "definition", version),
        };
    }

    private static CharacterDefinition LoadLegacyCharacterDefinition(
        ContentRegistry registry,
        NormalizedContentRecord record)
    {
        LegacyCharacterDefinitionData data = Deserialize<LegacyCharacterDefinitionData>(record);
        ValidateCharacterDefinitionLists(record, data);
        RequireMetadata(
            record,
            [data.NameKey],
            data.References,
            data.AbilityIds
                .Concat(data.AptitudeIds)
                .Concat(data.TraitIds)
                .Concat(data.AmbitionIds)
                .Concat(data.ReputationIds));
        RequireValidBirthDate(record, data.BirthDate);
        RequireBilingualName(registry, record, data.NameKey, "nameKey");
        return new CharacterDefinition(
            CharacterContractVersions.Definition,
            record.Id,
            data.NameKey,
            data.BirthDate,
            data.AbilityIds,
            data.AptitudeIds,
            data.TraitIds,
            data.AmbitionIds,
            data.ReputationIds,
            new StructuredCharacterName(data.NameKey, null),
            CreateOrigin(record, CharacterOriginKind.Authored),
            null,
            null,
            []);
    }

    private static CharacterDefinition LoadCurrentCharacterDefinition(
        ContentRegistry registry,
        NormalizedContentRecord record)
    {
        CurrentCharacterDefinitionData data = Deserialize<CurrentCharacterDefinitionData>(record);
        ValidateCharacterDefinitionLists(record, data);
        RequireList(data.FlawIds, record, "flawIds");
        RequireCanonicalIds(data.FlawIds, record, "flawIds");
        ValidateExplicitOrigin(record, data.OriginKind);

        EntityId[] nameKeys = data.CourtesyNameKey is EntityId courtesyNameKey
            ? [data.NameKey, courtesyNameKey]
            : [data.NameKey];
        IEnumerable<EntityId> consumedReferences = data.AbilityIds
            .Concat(data.AptitudeIds)
            .Concat(data.TraitIds)
            .Concat(data.AmbitionIds)
            .Concat(data.ReputationIds)
            .Concat(data.FlawIds)
            .Concat(data.CultureId is EntityId cultureId ? [cultureId] : [])
            .Concat(data.OriginLocationId is EntityId originId ? [originId] : []);
        RequireMetadata(record, nameKeys, data.References, consumedReferences);
        RequireValidBirthDate(record, data.BirthDate);
        RequireBilingualName(registry, record, data.NameKey, "nameKey");
        if (data.CourtesyNameKey is EntityId courtesy)
        {
            RequireBilingualName(registry, record, courtesy, "courtesyNameKey");
        }

        // Culture and origin location currently have no dedicated SP-04 content definitions.
        // Requiring loaded stable records keeps the boundary typed by ID without inventing a new subsystem.
        RequireLoadedReference(registry, record, data.CultureId, "cultureId");
        RequireLoadedReference(registry, record, data.OriginLocationId, "originLocationId");
        return new CharacterDefinition(
            CharacterContractVersions.Definition,
            record.Id,
            data.NameKey,
            data.BirthDate,
            data.AbilityIds,
            data.AptitudeIds,
            data.TraitIds,
            data.AmbitionIds,
            data.ReputationIds,
            new StructuredCharacterName(data.NameKey, data.CourtesyNameKey),
            CreateOrigin(record, data.OriginKind),
            data.CultureId,
            data.OriginLocationId,
            data.FlawIds);
    }

    private static FamilyDefinition LoadFamilyDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "family_definition");
        NamedCharacterDefinitionData data = Deserialize<NamedCharacterDefinitionData>(record);
        RequireSupportedDefinitionVersion(data.ContractVersion, record);
        RequireList(data.References, record, "references");
        RequireMetadata(record, [data.NameKey], data.References, []);
        RequireBilingualName(registry, record, data.NameKey, "nameKey");
        return new FamilyDefinition(CharacterContractVersions.Definition, record.Id, data.NameKey);
    }

    private static HouseholdDefinition LoadHouseholdDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "household_definition");
        NamedCharacterDefinitionData data = Deserialize<NamedCharacterDefinitionData>(record);
        RequireSupportedDefinitionVersion(data.ContractVersion, record);
        RequireList(data.References, record, "references");
        RequireMetadata(record, [data.NameKey], data.References, []);
        RequireBilingualName(registry, record, data.NameKey, "nameKey");
        return new HouseholdDefinition(CharacterContractVersions.Definition, record.Id, data.NameKey);
    }

    private static void ValidateTypedReferences(CharacterWorldSnapshot snapshot, NormalizedContentRecord worldRecord)
    {
        Dictionary<EntityId, CharacterIdentityDefinition> identities = snapshot.IdentityDefinitions
            .GroupBy(definition => definition.Id)
            .ToDictionary(group => group.Key, group => group.First());
        HashSet<EntityId> characterIds = snapshot.CharacterDefinitions.Select(definition => definition.Id).ToHashSet();
        HashSet<EntityId> familyIds = snapshot.FamilyDefinitions.Select(definition => definition.Id).ToHashSet();
        HashSet<EntityId> householdIds = snapshot.HouseholdDefinitions.Select(definition => definition.Id).ToHashSet();

        foreach (CharacterDefinition definition in snapshot.CharacterDefinitions)
        {
            RequireIdentityKind(definition, definition.AbilityIds, CharacterIdentityKind.Ability, identities);
            RequireIdentityKind(definition, definition.AptitudeIds, CharacterIdentityKind.Aptitude, identities);
            RequireIdentityKind(definition, definition.TraitIds, CharacterIdentityKind.Trait, identities);
            RequireIdentityKind(definition, definition.AmbitionIds, CharacterIdentityKind.Ambition, identities);
            RequireIdentityKind(definition, definition.ReputationIds, CharacterIdentityKind.Reputation, identities);
            RequireIdentityKind(definition, definition.FlawIds!, CharacterIdentityKind.Flaw, identities);
        }

        foreach (CharacterState state in snapshot.CharacterStates)
        {
            RequireSelected(state.CharacterId, characterIds, worldRecord, "character state");
            foreach (CharacterParentLink parentLink in state.ParentLinks!)
            {
                RequireSelected(parentLink.ParentCharacterId, characterIds, worldRecord, "parent");
            }

            if (state.Condition!.CustodianId is EntityId custodianId)
            {
                RequireSelected(custodianId, characterIds, worldRecord, "custodian");
            }
        }

        foreach (FamilyState state in snapshot.FamilyStates)
        {
            RequireSelected(state.FamilyId, familyIds, worldRecord, "family state");
            foreach (EntityId memberId in state.MemberIds)
            {
                RequireSelected(memberId, characterIds, worldRecord, "family member");
            }
        }

        foreach (HouseholdState state in snapshot.HouseholdStates)
        {
            RequireSelected(state.HouseholdId, householdIds, worldRecord, "household state");
            RequireSelected(state.HeadCharacterId, characterIds, worldRecord, "household head");
            foreach (EntityId memberId in state.MemberIds)
            {
                RequireSelected(memberId, characterIds, worldRecord, "household member");
            }
        }
    }

    private static void RequireIdentityKind(
        CharacterDefinition character,
        IReadOnlyList<EntityId> ids,
        CharacterIdentityKind expectedKind,
        IReadOnlyDictionary<EntityId, CharacterIdentityDefinition> identities)
    {
        foreach (EntityId id in ids)
        {
            if (!identities.TryGetValue(id, out CharacterIdentityDefinition? identity))
            {
                throw new InvalidDataException(
                    $"Character definition '{character.Id}' references unselected {expectedKind} definition '{id}'.");
            }

            if (identity.Kind != expectedKind)
            {
                throw new InvalidDataException(
                    $"Character definition '{character.Id}' references identity '{id}' as {expectedKind}, but it is {identity.Kind}.");
            }
        }
    }

    private static void RequireSelected(
        EntityId id,
        IReadOnlySet<EntityId> selected,
        NormalizedContentRecord worldRecord,
        string referenceKind)
    {
        if (!selected.Contains(id))
        {
            throw new InvalidDataException(
                $"Character world '{worldRecord.Id}' has a {referenceKind} reference to unselected definition '{id}'.");
        }
    }

    private static void ValidateCharacterDefinitionLists(
        NormalizedContentRecord record,
        ILegacyCharacterDefinitionData data)
    {
        RequireList(data.AbilityIds, record, "abilityIds");
        RequireList(data.AptitudeIds, record, "aptitudeIds");
        RequireList(data.TraitIds, record, "traitIds");
        RequireList(data.AmbitionIds, record, "ambitionIds");
        RequireList(data.ReputationIds, record, "reputationIds");
        RequireList(data.References, record, "references");
        RequireCanonicalIds(data.AbilityIds, record, "abilityIds");
        RequireCanonicalIds(data.AptitudeIds, record, "aptitudeIds");
        RequireCanonicalIds(data.TraitIds, record, "traitIds");
        RequireCanonicalIds(data.AmbitionIds, record, "ambitionIds");
        RequireCanonicalIds(data.ReputationIds, record, "reputationIds");
    }

    private static void ValidateExplicitOrigin(NormalizedContentRecord record, CharacterOriginKind originKind)
    {
        if (originKind is CharacterOriginKind.LegacyUnknown or CharacterOriginKind.Generated
            || !Enum.IsDefined(originKind))
        {
            throw new InvalidDataException(
                $"Character definition '{record.Id}' has invalid authored originKind '{originKind}'.");
        }

        if (record.ContentTag == ContentTag.Fictional && originKind != CharacterOriginKind.Custom)
        {
            throw new InvalidDataException(
                $"Fictional v2 character definition '{record.Id}' must use custom originKind.");
        }

        if (record.ContentTag != ContentTag.Fictional
            && (originKind != CharacterOriginKind.Authored || record.SourceIds.Count == 0))
        {
            throw new InvalidDataException(
                $"Non-fictional v2 character definition '{record.Id}' requires authored originKind and source evidence.");
        }
    }

    private static CharacterContentOrigin CreateOrigin(
        NormalizedContentRecord record,
        CharacterOriginKind originKind) => new CharacterContentOrigin(
            originKind,
            record.ContentTag switch
            {
                ContentTag.Historical => CharacterHistoricalClassification.Historical,
                ContentTag.Disputed => CharacterHistoricalClassification.Disputed,
                ContentTag.Inferred => CharacterHistoricalClassification.Inferred,
                ContentTag.Romance => CharacterHistoricalClassification.Romance,
                ContentTag.Fictional => CharacterHistoricalClassification.Fictional,
                _ => throw new InvalidDataException($"Character record '{record.Id}' has an invalid content tag."),
            },
            record.Id,
            record.OwningPackId,
            record.AppliedOverridePackIds,
            record.SourceIds).Canonicalize();

    private static void RequireBilingualName(
        ContentRegistry registry,
        NormalizedContentRecord record,
        EntityId nameKey,
        string property)
    {
        if (!registry.TryGetText(nameKey, "ko-KR", out string? korean) || string.IsNullOrWhiteSpace(korean))
        {
            throw new InvalidDataException($"Character record '{record.Id}' {property} '{nameKey}' lacks ko-KR text.");
        }

        if (!registry.TryGetText(nameKey, "en-US", out string? english) || string.IsNullOrWhiteSpace(english))
        {
            throw new InvalidDataException($"Character record '{record.Id}' {property} '{nameKey}' lacks en-US text.");
        }
    }

    private static void RequireMetadata(
        NormalizedContentRecord record,
        IEnumerable<EntityId> nameKeys,
        IReadOnlyList<EntityId> declaredReferences,
        IEnumerable<EntityId> consumedReferences)
    {
        EntityId[] expectedReferences = consumedReferences.Distinct().Order().ToArray();
        if (!declaredReferences.SequenceEqual(expectedReferences))
        {
            throw new InvalidDataException(
                $"Character record '{record.Id}' data.references must canonically equal its consumed typed references. "
                + $"Expected [{string.Join(", ", expectedReferences)}], found [{string.Join(", ", declaredReferences)}].");
        }

        EntityId[] expectedLocalizationKeys = nameKeys.Distinct().Order().ToArray();
        if (!record.LocalizationKeys.SequenceEqual(expectedLocalizationKeys))
        {
            throw new InvalidDataException(
                $"Character record '{record.Id}' localizationKeys must canonically equal "
                + $"[{string.Join(", ", expectedLocalizationKeys)}].");
        }
    }

    private static void RequireWorldMetadata(
        NormalizedContentRecord worldRecord,
        IReadOnlyList<EntityId> declaredReferences,
        IReadOnlyList<EntityId> identityDefinitionIds,
        IReadOnlyList<EntityId> characterDefinitionIds,
        IReadOnlyList<EntityId> familyDefinitionIds,
        IReadOnlyList<EntityId> householdDefinitionIds,
        IReadOnlyList<CharacterState> characterStates,
        IReadOnlyList<FamilyState> familyStates,
        IReadOnlyList<HouseholdState> householdStates)
    {
        RequireMetadata(
            worldRecord,
            [],
            declaredReferences,
            identityDefinitionIds
                .Concat(characterDefinitionIds)
                .Concat(familyDefinitionIds)
                .Concat(householdDefinitionIds)
                .Concat(characterStates.SelectMany(state => state.ParentLinks!
                    .Select(link => link.ParentCharacterId)
                    .Append(state.CharacterId)
                    .Concat(state.Condition!.CustodianId is EntityId custodian ? [custodian] : [])))
                .Concat(familyStates.SelectMany(state => state.MemberIds.Append(state.FamilyId)))
                .Concat(householdStates.SelectMany(state =>
                    state.MemberIds.Append(state.HeadCharacterId).Append(state.HouseholdId))));
    }

    private static void RequireLoadedReference(
        ContentRegistry registry,
        NormalizedContentRecord record,
        EntityId? referenceId,
        string property)
    {
        if (referenceId is EntityId id && !registry.TryGet(id, out _))
        {
            throw new InvalidDataException(
                $"Character definition '{record.Id}' {property} references missing content record '{id}'.");
        }
    }

    private static void RequireValidBirthDate(NormalizedContentRecord record, CampaignDate birthDate)
    {
        if (!birthDate.IsValid)
        {
            throw new InvalidDataException($"Character definition '{record.Id}' has an invalid birthDate.");
        }
    }

    private static void ValidateCondition(
        CharacterConditionData? condition,
        NormalizedContentRecord record)
    {
        if (condition is null)
        {
            throw new InvalidDataException($"Character record '{record.Id}' has missing or null condition data.");
        }

        if (!Enum.IsDefined(condition.VitalStatus)
            || !Enum.IsDefined(condition.HealthStatus)
            || !Enum.IsDefined(condition.CustodyStatus))
        {
            throw new InvalidDataException($"Character record '{record.Id}' has invalid condition data.");
        }
    }

    private static void ValidateCommonWorldStates(
        IReadOnlyList<FamilyStateData> familyStates,
        IReadOnlyList<HouseholdStateData> householdStates,
        int expectedVersion,
        NormalizedContentRecord worldRecord)
    {
        foreach (FamilyStateData state in familyStates)
        {
            RequireVersion(state.ContractVersion, expectedVersion, worldRecord, "family state");
            RequireList(state.MemberIds, worldRecord, "familyStates[].memberIds");
        }

        foreach (HouseholdStateData state in householdStates)
        {
            RequireVersion(state.ContractVersion, expectedVersion, worldRecord, "household state");
            RequireList(state.MemberIds, worldRecord, "householdStates[].memberIds");
        }
    }

    private static void RequireWorldLists(ICharacterWorldData world, NormalizedContentRecord worldRecord)
    {
        RequireList(world.IdentityDefinitionIds, worldRecord, "identityDefinitionIds");
        RequireList(world.CharacterDefinitionIds, worldRecord, "characterDefinitionIds");
        RequireList(world.FamilyDefinitionIds, worldRecord, "familyDefinitionIds");
        RequireList(world.HouseholdDefinitionIds, worldRecord, "householdDefinitionIds");
        RequireUntypedList(world.CharacterStatesUntyped, worldRecord, "characterStates");
        RequireList(world.FamilyStates, worldRecord, "familyStates");
        RequireList(world.HouseholdStates, worldRecord, "householdStates");
        RequireList(world.References, worldRecord, "references");
    }

    private static NormalizedContentRecord GetRecord(ContentRegistry registry, EntityId id, string expectedType)
    {
        if (!registry.TryGet(id, out NormalizedContentRecord? record))
        {
            throw new InvalidDataException($"Character world references missing content record '{id}'.");
        }

        if (!StringComparer.Ordinal.Equals(record.RecordType, expectedType))
        {
            throw new InvalidDataException(
                $"Character record '{id}' must have type '{expectedType}', found '{record.RecordType}'.");
        }

        return record;
    }

    private static T Deserialize<T>(NormalizedContentRecord record) where T : class
    {
        try
        {
            return record.Data.Deserialize<T>(JsonOptions)
                ?? throw new InvalidDataException($"Character record '{record.Id}' has empty data.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException($"Character record '{record.Id}' has malformed typed data.", exception);
        }
    }

    private static int ReadContractVersion(NormalizedContentRecord record, string contractKind = "definition")
    {
        if (!record.Data.TryGetProperty("contractVersion", out JsonElement version)
            || !version.TryGetInt32(out int value))
        {
            throw new InvalidDataException(
                $"Character record '{record.Id}' has a malformed {contractKind} contractVersion.");
        }

        return value;
    }

    private static InvalidDataException UnsupportedVersion(
        NormalizedContentRecord record,
        string contractKind,
        int actual) => new(
            $"Character record '{record.Id}' uses unsupported {contractKind} version {actual}; expected "
            + $"{CharacterContractVersions.LegacySnapshot} or {CharacterContractVersions.Snapshot}.");

    private static void RequireSupportedDefinitionVersion(int actual, NormalizedContentRecord record)
    {
        if (actual is not (CharacterContractVersions.LegacyDefinition or CharacterContractVersions.Definition))
        {
            throw UnsupportedVersion(record, "definition", actual);
        }
    }

    private static void RequireVersion(
        int actual,
        int expected,
        NormalizedContentRecord record,
        string contractKind)
    {
        if (actual != expected)
        {
            throw new InvalidDataException(
                $"Character record '{record.Id}' uses unsupported {contractKind} version {actual}; expected {expected}.");
        }
    }

    private static void RequireList<T>(IReadOnlyList<T>? items, NormalizedContentRecord record, string property)
    {
        if (items is null || items.Any(item => item is null))
        {
            throw new InvalidDataException($"Character record '{record.Id}' has missing or null '{property}' data.");
        }
    }

    private static void RequireUntypedList(
        System.Collections.IEnumerable? items,
        NormalizedContentRecord record,
        string property)
    {
        if (items is null || items.Cast<object?>().Any(item => item is null))
        {
            throw new InvalidDataException($"Character record '{record.Id}' has missing or null '{property}' data.");
        }
    }

    private static void RequireCanonicalIds(
        IReadOnlyList<EntityId> ids,
        NormalizedContentRecord record,
        string property)
    {
        if (!ids.SequenceEqual(ids.Distinct().Order()))
        {
            throw new InvalidDataException(
                $"Character record '{record.Id}' '{property}' must contain unique IDs in canonical order.");
        }
    }

    private interface ICharacterWorldData
    {
        IReadOnlyList<EntityId> IdentityDefinitionIds { get; }
        IReadOnlyList<EntityId> CharacterDefinitionIds { get; }
        IReadOnlyList<EntityId> FamilyDefinitionIds { get; }
        IReadOnlyList<EntityId> HouseholdDefinitionIds { get; }
        System.Collections.IEnumerable CharacterStatesUntyped { get; }
        IReadOnlyList<FamilyStateData> FamilyStates { get; }
        IReadOnlyList<HouseholdStateData> HouseholdStates { get; }
        IReadOnlyList<EntityId> References { get; }
    }

    private sealed record LegacyCharacterWorldData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] IReadOnlyList<EntityId> IdentityDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> CharacterDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> FamilyDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> HouseholdDefinitionIds,
        [property: JsonRequired] IReadOnlyList<LegacyCharacterStateData> CharacterStates,
        [property: JsonRequired] IReadOnlyList<FamilyStateData> FamilyStates,
        [property: JsonRequired] IReadOnlyList<HouseholdStateData> HouseholdStates,
        [property: JsonRequired] IReadOnlyList<EntityId> References) : ICharacterWorldData
    {
        public System.Collections.IEnumerable CharacterStatesUntyped => CharacterStates;
    }

    private sealed record CurrentCharacterWorldData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] IReadOnlyList<EntityId> IdentityDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> CharacterDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> FamilyDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> HouseholdDefinitionIds,
        [property: JsonRequired] IReadOnlyList<CurrentCharacterStateData> CharacterStates,
        [property: JsonRequired] IReadOnlyList<FamilyStateData> FamilyStates,
        [property: JsonRequired] IReadOnlyList<HouseholdStateData> HouseholdStates,
        [property: JsonRequired] IReadOnlyList<EntityId> References) : ICharacterWorldData
    {
        public System.Collections.IEnumerable CharacterStatesUntyped => CharacterStates;
    }

    private sealed record NormalizedCharacterWorldData(
        IReadOnlyList<EntityId> IdentityDefinitionIds,
        IReadOnlyList<EntityId> CharacterDefinitionIds,
        IReadOnlyList<EntityId> FamilyDefinitionIds,
        IReadOnlyList<EntityId> HouseholdDefinitionIds,
        IReadOnlyList<CharacterState> CharacterStates,
        IReadOnlyList<FamilyState> FamilyStates,
        IReadOnlyList<HouseholdState> HouseholdStates);

    private sealed record CharacterIdentityDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] CharacterIdentityKind Kind,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private interface ILegacyCharacterDefinitionData
    {
        EntityId NameKey { get; }
        CampaignDate BirthDate { get; }
        IReadOnlyList<EntityId> AbilityIds { get; }
        IReadOnlyList<EntityId> AptitudeIds { get; }
        IReadOnlyList<EntityId> TraitIds { get; }
        IReadOnlyList<EntityId> AmbitionIds { get; }
        IReadOnlyList<EntityId> ReputationIds { get; }
        IReadOnlyList<EntityId> References { get; }
    }

    private sealed record LegacyCharacterDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] CampaignDate BirthDate,
        [property: JsonRequired] IReadOnlyList<EntityId> AbilityIds,
        [property: JsonRequired] IReadOnlyList<EntityId> AptitudeIds,
        [property: JsonRequired] IReadOnlyList<EntityId> TraitIds,
        [property: JsonRequired] IReadOnlyList<EntityId> AmbitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> ReputationIds,
        [property: JsonRequired] IReadOnlyList<EntityId> References) : ILegacyCharacterDefinitionData;

    private sealed record CurrentCharacterDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] EntityId? CourtesyNameKey,
        [property: JsonRequired] CharacterOriginKind OriginKind,
        [property: JsonRequired] EntityId? CultureId,
        [property: JsonRequired] EntityId? OriginLocationId,
        [property: JsonRequired] CampaignDate BirthDate,
        [property: JsonRequired] IReadOnlyList<EntityId> AbilityIds,
        [property: JsonRequired] IReadOnlyList<EntityId> AptitudeIds,
        [property: JsonRequired] IReadOnlyList<EntityId> TraitIds,
        [property: JsonRequired] IReadOnlyList<EntityId> AmbitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> ReputationIds,
        [property: JsonRequired] IReadOnlyList<EntityId> FlawIds,
        [property: JsonRequired] IReadOnlyList<EntityId> References) : ILegacyCharacterDefinitionData;

    private sealed record NamedCharacterDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private sealed record LegacyCharacterStateData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId CharacterId,
        [property: JsonRequired] IReadOnlyList<EntityId> ParentIds);

    private sealed record CurrentCharacterStateData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId CharacterId,
        [property: JsonRequired] IReadOnlyList<EntityId> ParentIds,
        [property: JsonRequired] IReadOnlyList<CharacterParentLinkData> ParentLinks,
        [property: JsonRequired] CharacterConditionData? Condition);

    private sealed record CharacterParentLinkData(
        [property: JsonRequired] EntityId ParentCharacterId,
        [property: JsonRequired] ParentChildLinkKind Kind);

    private sealed record CharacterConditionData(
        [property: JsonRequired] CharacterVitalStatus VitalStatus,
        [property: JsonRequired] CharacterHealthStatus HealthStatus,
        [property: JsonRequired] bool IsIncapacitated,
        [property: JsonRequired] CharacterCustodyStatus CustodyStatus,
        [property: JsonRequired] EntityId? CustodianId);

    private sealed record FamilyStateData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId FamilyId,
        [property: JsonRequired] IReadOnlyList<EntityId> MemberIds);

    private sealed record HouseholdStateData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId HouseholdId,
        [property: JsonRequired] EntityId HeadCharacterId,
        [property: JsonRequired] IReadOnlyList<EntityId> MemberIds);
}
