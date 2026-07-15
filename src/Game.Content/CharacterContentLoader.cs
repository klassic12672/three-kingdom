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
                    $"Record '{recordId}' is not an SP-04A typed character record.");
        }
    }

    public static CharacterWorldSnapshot LoadWorld(ContentRegistry registry, EntityId scenarioId)
    {
        ArgumentNullException.ThrowIfNull(registry);

        NormalizedContentRecord worldRecord = GetRecord(registry, scenarioId, "character_world");
        CharacterWorldData world = ReadWorldData(worldRecord);

        CharacterIdentityDefinition[] identityDefinitions = world.IdentityDefinitionIds
            .Select(id => LoadIdentityDefinition(registry, id))
            .ToArray();
        CharacterDefinition[] characterDefinitions = world.CharacterDefinitionIds
            .Select(id => LoadCharacterDefinition(registry, id))
            .ToArray();
        FamilyDefinition[] familyDefinitions = world.FamilyDefinitionIds
            .Select(id => LoadFamilyDefinition(registry, id))
            .ToArray();
        HouseholdDefinition[] householdDefinitions = world.HouseholdDefinitionIds
            .Select(id => LoadHouseholdDefinition(registry, id))
            .ToArray();
        CharacterState[] characterStates = world.CharacterStates
            .Select(data => CreateCharacterState(data, worldRecord))
            .ToArray();
        FamilyState[] familyStates = world.FamilyStates
            .Select(data => CreateFamilyState(data, worldRecord))
            .ToArray();
        HouseholdState[] householdStates = world.HouseholdStates
            .Select(data => CreateHouseholdState(data, worldRecord))
            .ToArray();

        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            identityDefinitions,
            characterDefinitions,
            familyDefinitions,
            householdDefinitions,
            characterStates,
            familyStates,
            householdStates);
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

    private static CharacterWorldData ReadWorldData(NormalizedContentRecord worldRecord)
    {
        CharacterWorldData world = Deserialize<CharacterWorldData>(worldRecord);
        RequireVersion(world.ContractVersion, CharacterContractVersions.Snapshot, worldRecord, "world");
        RequireList(world.IdentityDefinitionIds, worldRecord, "identityDefinitionIds");
        RequireList(world.CharacterDefinitionIds, worldRecord, "characterDefinitionIds");
        RequireList(world.FamilyDefinitionIds, worldRecord, "familyDefinitionIds");
        RequireList(world.HouseholdDefinitionIds, worldRecord, "householdDefinitionIds");
        RequireList(world.CharacterStates, worldRecord, "characterStates");
        RequireList(world.FamilyStates, worldRecord, "familyStates");
        RequireList(world.HouseholdStates, worldRecord, "householdStates");
        RequireList(world.References, worldRecord, "references");
        foreach (CharacterStateData state in world.CharacterStates)
        {
            RequireVersion(state.ContractVersion, CharacterContractVersions.State, worldRecord, "character state");
            RequireList(state.ParentIds, worldRecord, "characterStates[].parentIds");
        }

        foreach (FamilyStateData state in world.FamilyStates)
        {
            RequireVersion(state.ContractVersion, CharacterContractVersions.State, worldRecord, "family state");
            RequireList(state.MemberIds, worldRecord, "familyStates[].memberIds");
        }

        foreach (HouseholdStateData state in world.HouseholdStates)
        {
            RequireVersion(state.ContractVersion, CharacterContractVersions.State, worldRecord, "household state");
            RequireList(state.MemberIds, worldRecord, "householdStates[].memberIds");
        }

        RequireMetadata(
            worldRecord,
            null,
            world.References,
            world.IdentityDefinitionIds
                .Concat(world.CharacterDefinitionIds)
                .Concat(world.FamilyDefinitionIds)
                .Concat(world.HouseholdDefinitionIds)
                .Concat(world.CharacterStates.SelectMany(state => state.ParentIds.Append(state.CharacterId)))
                .Concat(world.FamilyStates.SelectMany(state => state.MemberIds.Append(state.FamilyId)))
                .Concat(world.HouseholdStates.SelectMany(state =>
                    state.MemberIds.Append(state.HeadCharacterId).Append(state.HouseholdId))));
        return world;
    }

    private static CharacterIdentityDefinition LoadIdentityDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "character_identity_definition");
        CharacterIdentityDefinitionData data = Deserialize<CharacterIdentityDefinitionData>(record);
        RequireVersion(data.ContractVersion, CharacterContractVersions.Definition, record);
        RequireList(data.References, record, "references");
        RequireMetadata(record, data.NameKey, data.References, []);
        if (!Enum.IsDefined(data.Kind))
        {
            throw new InvalidDataException($"Character identity definition '{record.Id}' has an invalid kind.");
        }

        RequireBilingualName(registry, record, data.NameKey);
        return new CharacterIdentityDefinition(data.ContractVersion, record.Id, data.Kind, data.NameKey);
    }

    private static CharacterDefinition LoadCharacterDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "character_definition");
        CharacterDefinitionData data = Deserialize<CharacterDefinitionData>(record);
        RequireVersion(data.ContractVersion, CharacterContractVersions.Definition, record);
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
        RequireMetadata(
            record,
            data.NameKey,
            data.References,
            data.AbilityIds
                .Concat(data.AptitudeIds)
                .Concat(data.TraitIds)
                .Concat(data.AmbitionIds)
                .Concat(data.ReputationIds));
        if (!data.BirthDate.IsValid)
        {
            throw new InvalidDataException($"Character definition '{record.Id}' has an invalid birthDate.");
        }

        RequireBilingualName(registry, record, data.NameKey);
        return new CharacterDefinition(
            data.ContractVersion,
            record.Id,
            data.NameKey,
            data.BirthDate,
            data.AbilityIds,
            data.AptitudeIds,
            data.TraitIds,
            data.AmbitionIds,
            data.ReputationIds);
    }

    private static FamilyDefinition LoadFamilyDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "family_definition");
        FamilyDefinitionData data = Deserialize<FamilyDefinitionData>(record);
        RequireVersion(data.ContractVersion, CharacterContractVersions.Definition, record);
        RequireList(data.References, record, "references");
        RequireMetadata(record, data.NameKey, data.References, []);
        RequireBilingualName(registry, record, data.NameKey);
        return new FamilyDefinition(data.ContractVersion, record.Id, data.NameKey);
    }

    private static HouseholdDefinition LoadHouseholdDefinition(ContentRegistry registry, EntityId id)
    {
        NormalizedContentRecord record = GetRecord(registry, id, "household_definition");
        HouseholdDefinitionData data = Deserialize<HouseholdDefinitionData>(record);
        RequireVersion(data.ContractVersion, CharacterContractVersions.Definition, record);
        RequireList(data.References, record, "references");
        RequireMetadata(record, data.NameKey, data.References, []);
        RequireBilingualName(registry, record, data.NameKey);
        return new HouseholdDefinition(data.ContractVersion, record.Id, data.NameKey);
    }

    private static CharacterState CreateCharacterState(CharacterStateData data, NormalizedContentRecord worldRecord)
    {
        RequireVersion(data.ContractVersion, CharacterContractVersions.State, worldRecord, "character state");
        RequireList(data.ParentIds, worldRecord, "characterStates[].parentIds");
        return new CharacterState(data.ContractVersion, data.CharacterId, data.ParentIds);
    }

    private static FamilyState CreateFamilyState(FamilyStateData data, NormalizedContentRecord worldRecord)
    {
        RequireVersion(data.ContractVersion, CharacterContractVersions.State, worldRecord, "family state");
        RequireList(data.MemberIds, worldRecord, "familyStates[].memberIds");
        return new FamilyState(data.ContractVersion, data.FamilyId, data.MemberIds);
    }

    private static HouseholdState CreateHouseholdState(HouseholdStateData data, NormalizedContentRecord worldRecord)
    {
        RequireVersion(data.ContractVersion, CharacterContractVersions.State, worldRecord, "household state");
        RequireList(data.MemberIds, worldRecord, "householdStates[].memberIds");
        return new HouseholdState(data.ContractVersion, data.HouseholdId, data.HeadCharacterId, data.MemberIds);
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
        }

        foreach (CharacterState state in snapshot.CharacterStates)
        {
            RequireSelected(state.CharacterId, characterIds, worldRecord, "character state");
            foreach (EntityId parentId in state.ParentIds)
            {
                RequireSelected(parentId, characterIds, worldRecord, "parent");
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

    private static void RequireBilingualName(
        ContentRegistry registry,
        NormalizedContentRecord record,
        EntityId nameKey)
    {
        if (!registry.TryGetText(nameKey, "ko-KR", out string? korean) || string.IsNullOrWhiteSpace(korean))
        {
            throw new InvalidDataException($"Character record '{record.Id}' nameKey '{nameKey}' lacks ko-KR text.");
        }

        if (!registry.TryGetText(nameKey, "en-US", out string? english) || string.IsNullOrWhiteSpace(english))
        {
            throw new InvalidDataException($"Character record '{record.Id}' nameKey '{nameKey}' lacks en-US text.");
        }
    }

    private static void RequireMetadata(
        NormalizedContentRecord record,
        EntityId? nameKey,
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

        EntityId[] expectedLocalizationKeys = nameKey is EntityId key ? [key] : [];
        if (!record.LocalizationKeys.SequenceEqual(expectedLocalizationKeys))
        {
            throw new InvalidDataException(
                $"Character record '{record.Id}' localizationKeys must canonically equal "
                + $"[{string.Join(", ", expectedLocalizationKeys)}].");
        }
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

    private static void RequireVersion(
        int actual,
        int expected,
        NormalizedContentRecord record,
        string contractKind = "definition")
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

    private sealed record CharacterWorldData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] IReadOnlyList<EntityId> IdentityDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> CharacterDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> FamilyDefinitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> HouseholdDefinitionIds,
        [property: JsonRequired] IReadOnlyList<CharacterStateData> CharacterStates,
        [property: JsonRequired] IReadOnlyList<FamilyStateData> FamilyStates,
        [property: JsonRequired] IReadOnlyList<HouseholdStateData> HouseholdStates,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private sealed record CharacterIdentityDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] CharacterIdentityKind Kind,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private sealed record CharacterDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] CampaignDate BirthDate,
        [property: JsonRequired] IReadOnlyList<EntityId> AbilityIds,
        [property: JsonRequired] IReadOnlyList<EntityId> AptitudeIds,
        [property: JsonRequired] IReadOnlyList<EntityId> TraitIds,
        [property: JsonRequired] IReadOnlyList<EntityId> AmbitionIds,
        [property: JsonRequired] IReadOnlyList<EntityId> ReputationIds,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private sealed record FamilyDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private sealed record HouseholdDefinitionData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId NameKey,
        [property: JsonRequired] IReadOnlyList<EntityId> References);

    private sealed record CharacterStateData(
        [property: JsonRequired] int ContractVersion,
        [property: JsonRequired] EntityId CharacterId,
        [property: JsonRequired] IReadOnlyList<EntityId> ParentIds);

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
