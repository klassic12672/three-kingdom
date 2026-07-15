using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterWorldState : IAuthoritativeCharacterWorldQuery
{
    private readonly SortedDictionary<EntityId, CharacterIdentityDefinition> identityDefinitions = [];
    private readonly SortedDictionary<EntityId, CharacterDefinition> characterDefinitions = [];
    private readonly SortedDictionary<EntityId, FamilyDefinition> familyDefinitions = [];
    private readonly SortedDictionary<EntityId, HouseholdDefinition> householdDefinitions = [];
    private readonly SortedDictionary<EntityId, CharacterState> characterStates = [];
    private readonly SortedDictionary<EntityId, FamilyState> familyStates = [];
    private readonly SortedDictionary<EntityId, HouseholdState> householdStates = [];
    private readonly Dictionary<EntityId, EntityId> familyByCharacter = [];
    private readonly Dictionary<EntityId, EntityId> householdByCharacter = [];
    private readonly Dictionary<EntityId, CharacterChildLink[]> childrenByCharacter = [];
    private CampaignDate campaignDate;

    public CharacterWorldState(CharacterWorldSnapshot snapshot, CampaignDate campaignDate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!campaignDate.IsValid)
        {
            throw new SimulationValidationException("Character-world campaign date is invalid.");
        }

        ValidateSnapshotShape(snapshot);
        this.campaignDate = campaignDate;

        HashSet<EntityId> definitionIds = [];
        AddIdentityDefinitions(snapshot.IdentityDefinitions, definitionIds);
        AddCharacterDefinitions(snapshot.CharacterDefinitions, definitionIds);
        AddFamilyDefinitions(snapshot.FamilyDefinitions, definitionIds);
        AddHouseholdDefinitions(snapshot.HouseholdDefinitions, definitionIds);
        AddCharacterStates(snapshot.CharacterStates);
        AddFamilyStates(snapshot.FamilyStates);
        AddHouseholdStates(snapshot.HouseholdStates);

        ValidateOneToOneState(characterDefinitions.Keys, characterStates.Keys, "character");
        ValidateOneToOneState(familyDefinitions.Keys, familyStates.Keys, "family");
        ValidateOneToOneState(householdDefinitions.Keys, householdStates.Keys, "household");
        ValidateCharacterDefinitions();
        ValidateParentage();
        IndexChildren();
        ValidateAndIndexFamilies();
        ValidateAndIndexHouseholds();
    }

    public IReadOnlyList<AuthoritativeCharacterProfile> Profiles =>
        characterDefinitions.Keys.Select(CreateProfile).ToArray();

    public IReadOnlyList<AuthoritativeHouseholdView> Households =>
        householdDefinitions.Keys.Select(CreateHouseholdView).ToArray();

    public bool TryGetCharacterProfile(
        EntityId id,
        [NotNullWhen(true)] out AuthoritativeCharacterProfile? profile)
    {
        if (characterDefinitions.ContainsKey(id))
        {
            profile = CreateProfile(id);
            return true;
        }

        profile = null;
        return false;
    }

    public bool TryGetHousehold(
        EntityId id,
        [NotNullWhen(true)] out AuthoritativeHouseholdView? household)
    {
        if (householdDefinitions.ContainsKey(id))
        {
            household = CreateHouseholdView(id);
            return true;
        }

        household = null;
        return false;
    }

    public CharacterWorldSnapshot CaptureSnapshot() => new(
        CharacterContractVersions.Snapshot,
        identityDefinitions.Values.Select(item => item with { }).ToArray(),
        characterDefinitions.Values.Select(Clone).ToArray(),
        familyDefinitions.Values.Select(item => item with { }).ToArray(),
        householdDefinitions.Values.Select(item => item with { }).ToArray(),
        characterStates.Values.Select(Clone).ToArray(),
        familyStates.Values.Select(Clone).ToArray(),
        householdStates.Values.Select(Clone).ToArray());

    internal void UpdateCampaignDate(CampaignDate value)
    {
        if (!value.IsValid)
        {
            throw new SimulationValidationException("Character-world campaign date is invalid.");
        }

        CharacterDefinition? futureBirth = characterDefinitions.Values
            .FirstOrDefault(character => character.BirthDate.CompareTo(value) > 0);
        if (futureBirth is not null)
        {
            throw new SimulationValidationException(
                $"Character '{futureBirth.Id}' is born after campaign date '{value}'.");
        }

        campaignDate = value;
    }

    private static void ValidateSnapshotShape(CharacterWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported character-world snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.IdentityDefinitions is null
            || snapshot.CharacterDefinitions is null
            || snapshot.FamilyDefinitions is null
            || snapshot.HouseholdDefinitions is null
            || snapshot.CharacterStates is null
            || snapshot.FamilyStates is null
            || snapshot.HouseholdStates is null
            || snapshot.IdentityDefinitions.Any(item => item is null)
            || snapshot.CharacterDefinitions.Any(item => item is null)
            || snapshot.FamilyDefinitions.Any(item => item is null)
            || snapshot.HouseholdDefinitions.Any(item => item is null)
            || snapshot.CharacterStates.Any(item => item is null)
            || snapshot.FamilyStates.Any(item => item is null)
            || snapshot.HouseholdStates.Any(item => item is null))
        {
            throw new SimulationValidationException("Character-world snapshot collections and entries cannot be null.");
        }
    }

    private void AddIdentityDefinitions(
        IReadOnlyList<CharacterIdentityDefinition> definitions,
        HashSet<EntityId> definitionIds)
    {
        foreach (CharacterIdentityDefinition definition in definitions)
        {
            ValidateDefinitionVersion(definition.ContractVersion, "character identity", definition.Id);
            AddDefinitionId(definitionIds, definition.Id, "character identity");
            ValidateId(definition.NameKey, $"Character identity '{definition.Id}' name key");
            if (!Enum.IsDefined(definition.Kind))
            {
                throw new SimulationValidationException(
                    $"Character identity '{definition.Id}' has an invalid kind.");
            }

            identityDefinitions.Add(definition.Id, definition with { });
        }
    }

    private void AddCharacterDefinitions(
        IReadOnlyList<CharacterDefinition> definitions,
        HashSet<EntityId> definitionIds)
    {
        foreach (CharacterDefinition definition in definitions)
        {
            ValidateDefinitionVersion(definition.ContractVersion, "character", definition.Id);
            AddDefinitionId(definitionIds, definition.Id, "character");
            ValidateId(definition.NameKey, $"Character '{definition.Id}' name key");
            if (!definition.BirthDate.IsValid)
            {
                throw new SimulationValidationException($"Character '{definition.Id}' has an invalid birth date.");
            }

            if (definition.BirthDate.CompareTo(campaignDate) > 0)
            {
                throw new SimulationValidationException(
                    $"Character '{definition.Id}' is born after campaign date '{campaignDate}'.");
            }

            ValidateCanonicalIds(definition.AbilityIds, $"Character '{definition.Id}' ability IDs");
            ValidateCanonicalIds(definition.AptitudeIds, $"Character '{definition.Id}' aptitude IDs");
            ValidateCanonicalIds(definition.TraitIds, $"Character '{definition.Id}' trait IDs");
            ValidateCanonicalIds(definition.AmbitionIds, $"Character '{definition.Id}' ambition IDs");
            ValidateCanonicalIds(definition.ReputationIds, $"Character '{definition.Id}' reputation IDs");
            ValidateCharacterDescriptor(definition);
            characterDefinitions.Add(definition.Id, Clone(definition));
        }
    }

    private void AddFamilyDefinitions(
        IReadOnlyList<FamilyDefinition> definitions,
        HashSet<EntityId> definitionIds)
    {
        foreach (FamilyDefinition definition in definitions)
        {
            ValidateDefinitionVersion(definition.ContractVersion, "family", definition.Id);
            AddDefinitionId(definitionIds, definition.Id, "family");
            ValidateId(definition.NameKey, $"Family '{definition.Id}' name key");
            familyDefinitions.Add(definition.Id, definition with { });
        }
    }

    private void AddHouseholdDefinitions(
        IReadOnlyList<HouseholdDefinition> definitions,
        HashSet<EntityId> definitionIds)
    {
        foreach (HouseholdDefinition definition in definitions)
        {
            ValidateDefinitionVersion(definition.ContractVersion, "household", definition.Id);
            AddDefinitionId(definitionIds, definition.Id, "household");
            ValidateId(definition.NameKey, $"Household '{definition.Id}' name key");
            householdDefinitions.Add(definition.Id, definition with { });
        }
    }

    private void AddCharacterStates(IReadOnlyList<CharacterState> states)
    {
        foreach (CharacterState state in states)
        {
            ValidateStateVersion(state.ContractVersion, "character", state.CharacterId);
            ValidateId(state.CharacterId, "Character state ID");
            ValidateCanonicalIds(state.ParentIds, $"Character '{state.CharacterId}' parent IDs");
            ValidateParentLinks(state);
            ValidateCondition(state);
            if (!characterStates.TryAdd(state.CharacterId, Clone(state)))
            {
                throw new SimulationValidationException(
                    $"Duplicate character state for '{state.CharacterId}'.");
            }
        }
    }

    private void AddFamilyStates(IReadOnlyList<FamilyState> states)
    {
        foreach (FamilyState state in states)
        {
            ValidateStateVersion(state.ContractVersion, "family", state.FamilyId);
            ValidateId(state.FamilyId, "Family state ID");
            ValidateCanonicalIds(state.MemberIds, $"Family '{state.FamilyId}' member IDs");
            if (!familyStates.TryAdd(state.FamilyId, Clone(state)))
            {
                throw new SimulationValidationException($"Duplicate family state for '{state.FamilyId}'.");
            }
        }
    }

    private void AddHouseholdStates(IReadOnlyList<HouseholdState> states)
    {
        foreach (HouseholdState state in states)
        {
            ValidateStateVersion(state.ContractVersion, "household", state.HouseholdId);
            ValidateId(state.HouseholdId, "Household state ID");
            ValidateId(state.HeadCharacterId, $"Household '{state.HouseholdId}' head ID");
            ValidateCanonicalIds(state.MemberIds, $"Household '{state.HouseholdId}' member IDs");
            if (!householdStates.TryAdd(state.HouseholdId, Clone(state)))
            {
                throw new SimulationValidationException(
                    $"Duplicate household state for '{state.HouseholdId}'.");
            }
        }
    }

    private void ValidateCharacterDefinitions()
    {
        foreach (CharacterDefinition definition in characterDefinitions.Values)
        {
            ValidateIdentityReferences(definition.Id, definition.AbilityIds, CharacterIdentityKind.Ability);
            ValidateIdentityReferences(definition.Id, definition.AptitudeIds, CharacterIdentityKind.Aptitude);
            ValidateIdentityReferences(definition.Id, definition.TraitIds, CharacterIdentityKind.Trait);
            ValidateIdentityReferences(definition.Id, definition.AmbitionIds, CharacterIdentityKind.Ambition);
            ValidateIdentityReferences(definition.Id, definition.ReputationIds, CharacterIdentityKind.Reputation);
            ValidateIdentityReferences(definition.Id, definition.FlawIds!, CharacterIdentityKind.Flaw);
        }
    }

    private static void ValidateCharacterDescriptor(CharacterDefinition definition)
    {
        if (definition.StructuredName is null)
        {
            throw new SimulationValidationException(
                $"Character '{definition.Id}' structured name cannot be null in contract version {CharacterContractVersions.Definition}.");
        }

        ValidateId(
            definition.StructuredName.PrimaryNameKey,
            $"Character '{definition.Id}' structured primary name key");
        if (definition.StructuredName.PrimaryNameKey != definition.NameKey)
        {
            throw new SimulationValidationException(
                $"Character '{definition.Id}' structured primary name key must equal its name key.");
        }

        if (definition.StructuredName.CourtesyNameKey is EntityId courtesyNameKey)
        {
            ValidateId(courtesyNameKey, $"Character '{definition.Id}' courtesy name key");
        }

        ValidateContentOrigin(definition.Id, definition.ContentOrigin);
        if (definition.CultureId is EntityId cultureId)
        {
            ValidateId(cultureId, $"Character '{definition.Id}' culture ID");
        }

        if (definition.OriginLocationId is EntityId originLocationId)
        {
            ValidateId(originLocationId, $"Character '{definition.Id}' origin location ID");
        }

        ValidateCanonicalIds(definition.FlawIds!, $"Character '{definition.Id}' flaw IDs");
    }

    private static void ValidateContentOrigin(EntityId characterId, CharacterContentOrigin? origin)
    {
        if (origin is null)
        {
            throw new SimulationValidationException(
                $"Character '{characterId}' content origin cannot be null in contract version {CharacterContractVersions.Definition}.");
        }

        if (!Enum.IsDefined(origin.OriginKind))
        {
            throw new SimulationValidationException(
                $"Character '{characterId}' has an invalid content-origin kind.");
        }

        if (origin.HistoricalClassification is CharacterHistoricalClassification classification
            && !Enum.IsDefined(classification))
        {
            throw new SimulationValidationException(
                $"Character '{characterId}' has an invalid historical classification.");
        }

        ValidateId(origin.RecordId, $"Character '{characterId}' content record ID");
        if (origin.OwningPackId is EntityId owningPackId)
        {
            ValidateId(owningPackId, $"Character '{characterId}' owning pack ID");
        }

        ValidateCanonicalIds(
            origin.AppliedOverridePackIds,
            $"Character '{characterId}' applied override pack IDs");
        ValidateCanonicalIds(origin.SourceIds, $"Character '{characterId}' source IDs");

        switch (origin.OriginKind)
        {
            case CharacterOriginKind.LegacyUnknown:
                if (origin.HistoricalClassification is not null
                    || origin.OwningPackId is not null
                    || origin.AppliedOverridePackIds.Count != 0
                    || origin.SourceIds.Count != 0)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' legacy-unknown content origin cannot assert classification, pack lineage, overrides, or sources.");
                }

                break;
            case CharacterOriginKind.Authored:
                if (origin.HistoricalClassification is null
                    || origin.OwningPackId is null
                    || (origin.HistoricalClassification != CharacterHistoricalClassification.Fictional
                        && origin.SourceIds.Count == 0))
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' authored content origin requires classification, an owning pack, and sources for non-fictional classification.");
                }

                break;
            case CharacterOriginKind.Custom:
                if (origin.HistoricalClassification != CharacterHistoricalClassification.Fictional
                    || origin.OwningPackId is null)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' custom content origin requires fictional classification and an owning pack.");
                }

                break;
            case CharacterOriginKind.Generated:
                if (origin.HistoricalClassification != CharacterHistoricalClassification.Fictional
                    || origin.OwningPackId is not null
                    || origin.AppliedOverridePackIds.Count != 0
                    || origin.SourceIds.Count != 0)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' generated content origin requires fictional classification and cannot assert pack lineage, overrides, or sources.");
                }

                break;
        }

        if (origin.OwningPackId is EntityId packId && origin.AppliedOverridePackIds.Contains(packId))
        {
            throw new SimulationValidationException(
                $"Character '{characterId}' owning pack cannot also be an applied override pack.");
        }
    }

    private static void ValidateParentLinks(CharacterState state)
    {
        if (state.ParentLinks is null)
        {
            throw new SimulationValidationException(
                $"Character '{state.CharacterId}' typed parent links cannot be null in contract version {CharacterContractVersions.State}.");
        }

        EntityId? previousParentId = null;
        foreach (CharacterParentLink? link in state.ParentLinks)
        {
            if (link is null)
            {
                throw new SimulationValidationException(
                    $"Character '{state.CharacterId}' typed parent links cannot contain null entries.");
            }

            ValidateId(link.ParentCharacterId, $"Character '{state.CharacterId}' typed parent ID");
            if (!Enum.IsDefined(link.Kind))
            {
                throw new SimulationValidationException(
                    $"Character '{state.CharacterId}' has an invalid parent-link kind.");
            }

            if (previousParentId is EntityId previous && previous.CompareTo(link.ParentCharacterId) >= 0)
            {
                throw new SimulationValidationException(
                    $"Character '{state.CharacterId}' typed parent links must contain unique parent IDs in ordinal canonical order.");
            }

            previousParentId = link.ParentCharacterId;
        }

        if (!state.ParentIds.SequenceEqual(state.ParentLinks.Select(link => link.ParentCharacterId)))
        {
            throw new SimulationValidationException(
                $"Character '{state.CharacterId}' typed parent-link IDs must exactly equal its retained parent IDs.");
        }
    }

    private void ValidateCondition(CharacterState state)
    {
        CharacterConditionState? condition = state.Condition;
        if (condition is null)
        {
            throw new SimulationValidationException(
                $"Character '{state.CharacterId}' condition cannot be null in contract version {CharacterContractVersions.State}.");
        }

        if (!Enum.IsDefined(condition.VitalStatus)
            || !Enum.IsDefined(condition.HealthStatus)
            || !Enum.IsDefined(condition.CustodyStatus))
        {
            throw new SimulationValidationException(
                $"Character '{state.CharacterId}' condition contains an invalid enum value.");
        }

        if (condition.HealthStatus == CharacterHealthStatus.Critical && !condition.IsIncapacitated)
        {
            throw new SimulationValidationException(
                $"Critical character '{state.CharacterId}' must be incapacitated.");
        }

        if (condition.VitalStatus == CharacterVitalStatus.Dead
            && (condition.HealthStatus != CharacterHealthStatus.Critical
                || !condition.IsIncapacitated
                || condition.CustodyStatus != CharacterCustodyStatus.Free
                || condition.CustodianId is not null))
        {
            throw new SimulationValidationException(
                $"Dead character '{state.CharacterId}' must be critical, incapacitated, and free of custody.");
        }

        if (condition.CustodyStatus == CharacterCustodyStatus.Free)
        {
            if (condition.CustodianId is not null)
            {
                throw new SimulationValidationException(
                    $"Free character '{state.CharacterId}' cannot have a custodian.");
            }
        }
        else
        {
            if (condition.CustodianId is not EntityId custodianId)
            {
                throw new SimulationValidationException(
                    $"Character '{state.CharacterId}' in custody requires a custodian ID.");
            }

            ValidateId(custodianId, $"Character '{state.CharacterId}' custodian ID");
            if (custodianId == state.CharacterId)
            {
                throw new SimulationValidationException(
                    $"Character '{state.CharacterId}' cannot be their own custodian.");
            }

            if (!characterDefinitions.ContainsKey(custodianId))
            {
                throw new SimulationValidationException(
                    $"Character '{state.CharacterId}' references missing custodian '{custodianId}'.");
            }
        }
    }

    private void ValidateIdentityReferences(
        EntityId characterId,
        IReadOnlyList<EntityId> ids,
        CharacterIdentityKind expectedKind)
    {
        foreach (EntityId id in ids)
        {
            if (!identityDefinitions.TryGetValue(id, out CharacterIdentityDefinition? definition))
            {
                throw new SimulationValidationException(
                    $"Character '{characterId}' references missing {expectedKind} definition '{id}'.");
            }

            if (definition.Kind != expectedKind)
            {
                throw new SimulationValidationException(
                    $"Character '{characterId}' references {definition.Kind} definition '{id}' as {expectedKind}.");
            }
        }
    }

    private void ValidateParentage()
    {
        foreach (CharacterState state in characterStates.Values)
        {
            foreach (EntityId parentId in state.ParentIds)
            {
                if (parentId == state.CharacterId)
                {
                    throw new SimulationValidationException(
                        $"Character '{state.CharacterId}' cannot be their own parent.");
                }

                if (!characterDefinitions.ContainsKey(parentId))
                {
                    throw new SimulationValidationException(
                        $"Character '{state.CharacterId}' references missing parent '{parentId}'.");
                }
            }
        }

        Dictionary<EntityId, VisitState> visits = [];
        foreach (EntityId characterId in characterDefinitions.Keys)
        {
            VisitParentage(characterId, visits);
        }

        foreach (CharacterState state in characterStates.Values)
        {
            CampaignDate childBirth = characterDefinitions[state.CharacterId].BirthDate;
            foreach (EntityId parentId in state.ParentIds)
            {
                CampaignDate parentBirth = characterDefinitions[parentId].BirthDate;
                if (parentBirth.CompareTo(childBirth) >= 0)
                {
                    throw new SimulationValidationException(
                        $"Parent '{parentId}' must be born before child '{state.CharacterId}'.");
                }
            }
        }
    }

    private void VisitParentage(EntityId characterId, Dictionary<EntityId, VisitState> visits)
    {
        visits.TryGetValue(characterId, out VisitState state);
        if (state == VisitState.Visited)
        {
            return;
        }

        if (state == VisitState.Visiting)
        {
            throw new SimulationValidationException(
                $"Parentage cycle includes character '{characterId}'.");
        }

        visits[characterId] = VisitState.Visiting;
        foreach (EntityId parentId in characterStates[characterId].ParentIds)
        {
            VisitParentage(parentId, visits);
        }

        visits[characterId] = VisitState.Visited;
    }

    private void IndexChildren()
    {
        Dictionary<EntityId, List<CharacterChildLink>> childLists = characterDefinitions.Keys
            .ToDictionary(id => id, _ => new List<CharacterChildLink>());
        foreach (CharacterState state in characterStates.Values)
        {
            foreach (CharacterParentLink parentLink in state.ParentLinks!)
            {
                childLists[parentLink.ParentCharacterId].Add(new CharacterChildLink(
                    state.CharacterId,
                    parentLink.Kind));
            }
        }

        foreach ((EntityId id, List<CharacterChildLink> children) in childLists)
        {
            childrenByCharacter.Add(
                id,
                children.OrderBy(link => link.ChildCharacterId)
                    .ThenBy(link => link.Kind)
                    .ToArray());
        }
    }

    private void ValidateAndIndexFamilies()
    {
        foreach (FamilyState family in familyStates.Values)
        {
            foreach (EntityId characterId in family.MemberIds)
            {
                if (!characterDefinitions.ContainsKey(characterId))
                {
                    throw new SimulationValidationException(
                        $"Family '{family.FamilyId}' references missing character '{characterId}'.");
                }

                if (!familyByCharacter.TryAdd(characterId, family.FamilyId))
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' belongs to more than one family.");
                }
            }
        }
    }

    private void ValidateAndIndexHouseholds()
    {
        foreach (HouseholdState household in householdStates.Values)
        {
            foreach (EntityId characterId in household.MemberIds)
            {
                if (!characterDefinitions.ContainsKey(characterId))
                {
                    throw new SimulationValidationException(
                        $"Household '{household.HouseholdId}' references missing character '{characterId}'.");
                }

                if (!householdByCharacter.TryAdd(characterId, household.HouseholdId))
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' belongs to more than one household.");
                }
            }

            if (!household.MemberIds.Contains(household.HeadCharacterId))
            {
                throw new SimulationValidationException(
                    $"Household '{household.HouseholdId}' head '{household.HeadCharacterId}' is not a member.");
            }
        }
    }

    private AuthoritativeCharacterProfile CreateProfile(EntityId id)
    {
        CharacterDefinition definition = characterDefinitions[id];
        CharacterState state = characterStates[id];
        return new AuthoritativeCharacterProfile(
            CharacterContractVersions.AuthoritativeQuery,
            definition.Id,
            definition.NameKey,
            definition.BirthDate,
            CalculateAge(definition.BirthDate, campaignDate),
            state.ParentIds.ToArray(),
            childrenByCharacter[id].Select(link => link.ChildCharacterId).ToArray(),
            familyByCharacter.TryGetValue(id, out EntityId familyId) ? familyId : null,
            householdByCharacter.TryGetValue(id, out EntityId householdId) ? householdId : null,
            definition.AbilityIds.ToArray(),
            definition.AptitudeIds.ToArray(),
            definition.TraitIds.ToArray(),
            definition.AmbitionIds.ToArray(),
            definition.ReputationIds.ToArray(),
            Clone(definition.StructuredName!),
            Clone(definition.ContentOrigin!),
            definition.CultureId,
            definition.OriginLocationId,
            definition.FlawIds!.ToArray(),
            Clone(state.Condition!),
            state.ParentLinks!.Select(Clone).ToArray(),
            childrenByCharacter[id].Select(Clone).ToArray());
    }

    private AuthoritativeHouseholdView CreateHouseholdView(EntityId id)
    {
        HouseholdDefinition definition = householdDefinitions[id];
        HouseholdState state = householdStates[id];
        return new AuthoritativeHouseholdView(
            CharacterContractVersions.AuthoritativeQuery,
            definition.Id,
            definition.NameKey,
            state.HeadCharacterId,
            state.MemberIds.ToArray());
    }

    private static int CalculateAge(CampaignDate birthDate, CampaignDate currentDate)
    {
        int age = currentDate.Year - birthDate.Year;
        if (currentDate.Month < birthDate.Month
            || (currentDate.Month == birthDate.Month && currentDate.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }

    private static void ValidateOneToOneState(
        IEnumerable<EntityId> definitionIds,
        IEnumerable<EntityId> stateIds,
        string kind)
    {
        EntityId[] definitions = definitionIds.Order().ToArray();
        EntityId[] states = stateIds.Order().ToArray();
        if (!definitions.SequenceEqual(states))
        {
            throw new SimulationValidationException(
                $"Every {kind} definition must have exactly one matching state.");
        }
    }

    private static void ValidateDefinitionVersion(int version, string kind, EntityId id)
    {
        if (version != CharacterContractVersions.Definition)
        {
            throw new SimulationValidationException(
                $"Unsupported {kind} definition contract version {version} for '{id}'.");
        }
    }

    private static void ValidateStateVersion(int version, string kind, EntityId id)
    {
        if (version != CharacterContractVersions.State)
        {
            throw new SimulationValidationException(
                $"Unsupported {kind} state contract version {version} for '{id}'.");
        }
    }

    private static void AddDefinitionId(HashSet<EntityId> ids, EntityId id, string kind)
    {
        ValidateId(id, $"{kind} definition ID");
        if (!ids.Add(id))
        {
            throw new SimulationValidationException($"Duplicate global definition ID '{id}'.");
        }
    }

    private static void ValidateId(EntityId id, string description)
    {
        if (!id.IsValid)
        {
            throw new SimulationValidationException($"{description} is invalid.");
        }
    }

    private static void ValidateCanonicalIds(IReadOnlyList<EntityId> ids, string description)
    {
        if (ids is null)
        {
            throw new SimulationValidationException($"{description} cannot be null.");
        }

        EntityId? previous = null;
        foreach (EntityId id in ids)
        {
            ValidateId(id, description);
            if (previous is EntityId previousId && previousId.CompareTo(id) >= 0)
            {
                throw new SimulationValidationException(
                    $"{description} must contain unique IDs in ordinal canonical order.");
            }

            previous = id;
        }
    }

    private static CharacterDefinition Clone(CharacterDefinition definition) => definition with
    {
        AbilityIds = definition.AbilityIds.ToArray(),
        AptitudeIds = definition.AptitudeIds.ToArray(),
        TraitIds = definition.TraitIds.ToArray(),
        AmbitionIds = definition.AmbitionIds.ToArray(),
        ReputationIds = definition.ReputationIds.ToArray(),
        StructuredName = definition.StructuredName is null ? null : Clone(definition.StructuredName),
        ContentOrigin = definition.ContentOrigin is null ? null : Clone(definition.ContentOrigin),
        FlawIds = definition.FlawIds?.ToArray(),
    };

    private static CharacterState Clone(CharacterState state) => state with
    {
        ParentIds = state.ParentIds.ToArray(),
        ParentLinks = state.ParentLinks?.Select(Clone).ToArray(),
        Condition = state.Condition is null ? null : Clone(state.Condition),
    };

    private static StructuredCharacterName Clone(StructuredCharacterName name) => name with { };

    private static CharacterContentOrigin Clone(CharacterContentOrigin origin) => origin with
    {
        AppliedOverridePackIds = origin.AppliedOverridePackIds.ToArray(),
        SourceIds = origin.SourceIds.ToArray(),
    };

    private static CharacterConditionState Clone(CharacterConditionState condition) => condition with { };

    private static CharacterParentLink Clone(CharacterParentLink link) => link with { };

    private static CharacterChildLink Clone(CharacterChildLink link) => link with { };

    private static FamilyState Clone(FamilyState state) => state with
    {
        MemberIds = state.MemberIds.ToArray(),
    };

    private static HouseholdState Clone(HouseholdState state) => state with
    {
        MemberIds = state.MemberIds.ToArray(),
    };

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
