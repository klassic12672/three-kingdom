using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterWorldState : IAuthoritativeCharacterWorldQuery
{
    private const int AdultAge = 18;
    private const int LegalAdoptiveParentsPerCharacter = 2;
    private const int TotalParentsPerCharacterForAdoption = 4;
    private const int LegalAdoptiveChildrenPerCharacter = 64;

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

    internal CharacterFamilyMutationPlan PrepareFamilyAction(
        ICharacterFamilyAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is not EstablishLegalAdoptiveParentAction adoption)
        {
            throw new SimulationValidationException(
                $"Unsupported character-family action '{action?.GetType().Name ?? "null"}'.");
        }

        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(campaignDate) < 0
            || authoritativeTurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-family resolution date or turn is invalid.");
        }

        if (!commandId.IsValid
            || eventId != CharacterFamilyIds.DeriveActionEventId(resolutionDate, commandId))
        {
            throw new SimulationValidationException(
                "Character-family command or event identity is invalid.");
        }

        if (adoption.AdoptiveParentCharacterId == adoption.AdoptedCharacterId)
        {
            throw new SimulationValidationException(
                "A character cannot become their own legal-adoptive parent.");
        }

        AuthoritativeCharacterProfile adoptiveParent = RequireCurrentCharacter(
            adoption.AdoptiveParentCharacterId,
            resolutionDate,
            "Legal-adoptive parent");
        AuthoritativeCharacterProfile adoptedCharacter = RequireCurrentCharacter(
            adoption.AdoptedCharacterId,
            resolutionDate,
            "Adopted character");
        RequireLivingCapableFree(adoptiveParent, "Legal-adoptive parent");
        if (CalculateAge(adoptiveParent.BirthDate, resolutionDate) < AdultAge)
        {
            throw new SimulationValidationException(
                $"Legal-adoptive parent '{adoptiveParent.CharacterId}' must be at least 18 years old.");
        }

        if (adoptiveParent.BirthDate.CompareTo(adoptedCharacter.BirthDate) >= 0)
        {
            throw new SimulationValidationException(
                $"Legal-adoptive parent '{adoptiveParent.CharacterId}' must be born before adopted character '{adoptedCharacter.CharacterId}'.");
        }

        if (adoptedCharacter.Condition.VitalStatus != CharacterVitalStatus.Alive
            || adoptedCharacter.Condition.CustodyStatus != CharacterCustodyStatus.Free
            || (CalculateAge(adoptedCharacter.BirthDate, resolutionDate) >= AdultAge
                && adoptedCharacter.Condition.IsIncapacitated))
        {
            throw new SimulationValidationException(
                $"Adopted character '{adoptedCharacter.CharacterId}' must be living and free, and an adult adoptee must have capacity.");
        }

        CharacterParentLink[] expected = ValidateAndCloneExpectedParentLinks(
            adoption.ExpectedCurrentParentLinks);
        CharacterParentLink[] previous = adoptedCharacter.ParentLinks
            .Select(Clone)
            .ToArray();
        if (!previous.SequenceEqual(expected))
        {
            throw new SimulationValidationException(
                $"Character-family expected-current parent links are stale for '{adoptedCharacter.CharacterId}'.");
        }

        if (previous.Any(link =>
                link.ParentCharacterId == adoptiveParent.CharacterId))
        {
            throw new SimulationValidationException(
                $"Character '{adoptiveParent.CharacterId}' is already a parent of '{adoptedCharacter.CharacterId}'.");
        }

        if (previous.Count(link => link.Kind == ParentChildLinkKind.LegalAdoptive)
            >= LegalAdoptiveParentsPerCharacter)
        {
            throw new SimulationValidationException(
                $"Character '{adoptedCharacter.CharacterId}' already has the maximum two legal-adoptive parents.");
        }

        if (previous.Length >= TotalParentsPerCharacterForAdoption)
        {
            throw new SimulationValidationException(
                $"Character '{adoptedCharacter.CharacterId}' already has the maximum four total parents for a new adoption.");
        }

        if (adoptiveParent.ChildLinks.Count(link =>
                link.Kind == ParentChildLinkKind.LegalAdoptive)
            >= LegalAdoptiveChildrenPerCharacter)
        {
            throw new SimulationValidationException(
                $"Character '{adoptiveParent.CharacterId}' already has the maximum 64 legal-adoptive children.");
        }

        CharacterParentLink[] current = previous
            .Append(new CharacterParentLink(
                adoptiveParent.CharacterId,
                ParentChildLinkKind.LegalAdoptive))
            .OrderBy(link => link.ParentCharacterId)
            .ThenBy(link => link.Kind)
            .Select(Clone)
            .ToArray();
        CharacterWorldSnapshot updated = CaptureSnapshot() with
        {
            CharacterStates = characterStates.Values.Select(state =>
                state.CharacterId == adoptedCharacter.CharacterId
                    ? state with
                    {
                        ParentIds = current.Select(link => link.ParentCharacterId).ToArray(),
                        ParentLinks = current.Select(Clone).ToArray(),
                    }
                    : Clone(state)).ToArray(),
        };
        CampaignDate candidateDate = resolutionDate.CompareTo(campaignDate) > 0
            ? resolutionDate
            : campaignDate;
        CharacterParentageChange change = new(
            CharacterFamilyContractVersions.Change,
            CharacterFamilyIds.DeriveParentageChangeId(
                eventId,
                adoptedCharacter.CharacterId),
            adoptedCharacter.CharacterId,
            previous.Select(Clone).ToArray(),
            current.Select(Clone).ToArray(),
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        return new CharacterFamilyMutationPlan(
            change,
            new CharacterWorldUpdatePlan(new CharacterWorldState(updated, candidateDate)));
    }

    internal CharacterConditionMutationPlan PrepareConditionAction(
        ICharacterConditionAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is null)
        {
            throw new SimulationValidationException("Character-condition action cannot be null.");
        }

        EntityId characterId = action switch
        {
            IncapacitateCharacterAction value => value.CharacterId,
            RestoreCharacterCapacityAction value => value.CharacterId,
            EnterCharacterCustodyAction value => value.CharacterId,
            ReleaseCharacterCustodyAction value => value.CharacterId,
            _ => throw new SimulationValidationException(
                $"Unsupported character-condition action '{action.GetType().Name}'."),
        };
        CharacterConditionState expected = action switch
        {
            IncapacitateCharacterAction value => value.ExpectedCurrent,
            RestoreCharacterCapacityAction value => value.ExpectedCurrent,
            EnterCharacterCustodyAction value => value.ExpectedCurrent,
            ReleaseCharacterCustodyAction value => value.ExpectedCurrent,
            _ => throw new SimulationValidationException(
                $"Unsupported character-condition action '{action.GetType().Name}'."),
        } ?? throw new SimulationValidationException(
            "Character-condition expected-current state cannot be null.");

        AuthoritativeCharacterProfile profile = RequireCurrentCharacter(
            characterId,
            resolutionDate,
            "Character-condition target");
        if (profile.Condition != expected)
        {
            throw new SimulationValidationException(
                $"Character-condition expected-current state is stale for '{characterId}'.");
        }

        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException(
                $"Character-condition target '{characterId}' must be alive.");
        }

        CharacterConditionState next = action switch
        {
            IncapacitateCharacterAction when !profile.Condition.IsIncapacitated =>
                profile.Condition with { IsIncapacitated = true },
            IncapacitateCharacterAction => throw new SimulationValidationException(
                $"Character '{characterId}' is already incapacitated."),
            RestoreCharacterCapacityAction when profile.Condition.IsIncapacitated
                && profile.Condition.HealthStatus != CharacterHealthStatus.Critical =>
                profile.Condition with { IsIncapacitated = false },
            RestoreCharacterCapacityAction when profile.Condition.HealthStatus
                == CharacterHealthStatus.Critical => throw new SimulationValidationException(
                    $"Critical character '{characterId}' cannot restore capacity."),
            RestoreCharacterCapacityAction => throw new SimulationValidationException(
                $"Character '{characterId}' already has capacity."),
            EnterCharacterCustodyAction value => PlanCustodyEntry(
                profile,
                value,
                resolutionDate),
            ReleaseCharacterCustodyAction when profile.Condition.CustodyStatus
                != CharacterCustodyStatus.Free => profile.Condition with
                {
                    CustodyStatus = CharacterCustodyStatus.Free,
                    CustodianId = null,
                },
            ReleaseCharacterCustodyAction => throw new SimulationValidationException(
                $"Character '{characterId}' is already free of custody."),
            _ => throw new SimulationValidationException(
                $"Unsupported character-condition action '{action.GetType().Name}'."),
        };

        return PrepareConditionMutation(
            characterId,
            profile.Condition,
            next,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            allowDeath: false);
    }

    internal CharacterConditionMutationPlan PrepareDeathPreview(
        EntityId characterId,
        CharacterConditionState expectedCurrent,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        AuthoritativeCharacterProfile profile = RequireCurrentCharacter(
            characterId,
            resolutionDate,
            "Character-death preview target");
        if (expectedCurrent is null || profile.Condition != expectedCurrent)
        {
            throw new SimulationValidationException(
                $"Character-death expected-current state is stale for '{characterId}'.");
        }

        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException(
                $"Character-death preview target '{characterId}' must be alive.");
        }

        CharacterConditionState deceased = new(
            CharacterVitalStatus.Dead,
            CharacterHealthStatus.Critical,
            IsIncapacitated: true,
            CharacterCustodyStatus.Free,
            null);
        return PrepareConditionMutation(
            characterId,
            profile.Condition,
            deceased,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            allowDeath: true);
    }

    internal CharacterBirthMutationPlan PreparePregnancyBirth(
        CharacterPregnancyState resolvedPregnancy,
        GeneratedNewbornSpecification newborn,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (resolvedPregnancy is null
            || newborn is null
            || !resolutionDate.IsValid
            || resolutionDate.CompareTo(campaignDate) < 0
            || authoritativeTurnIndex < 0
            || !commandId.IsValid
            || eventId != CharacterFamilyIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Pregnancy-birth character preparation contains null or invalid resolution data.");
        }

        if (resolvedPregnancy.ContractVersion != CharacterPregnancyContractVersions.State
            || !resolvedPregnancy.PregnancyId.IsValid
            || resolvedPregnancy.GestationalParentCharacterId
                == resolvedPregnancy.OtherBiologicalParentCharacterId
            || resolutionDate.CompareTo(resolvedPregnancy.ExpectedBirthDate) < 0)
        {
            throw new SimulationValidationException(
                "Pregnancy-birth character preparation requires an exact due or overdue pregnancy.");
        }

        if (newborn.ContractVersion != CharacterBirthContractVersions.NewbornSpecification
            || !newborn.PrimaryNameKey.IsValid
            || !newborn.PrimaryNameKey.Value.StartsWith("loc:", StringComparison.Ordinal)
            || newborn.InheritedTraitIds is null)
        {
            throw new SimulationValidationException(
                "Generated-newborn specification version, localization key, or traits are invalid.");
        }

        AuthoritativeCharacterProfile gestationalParent = RequireCurrentCharacter(
            resolvedPregnancy.GestationalParentCharacterId,
            resolutionDate,
            "Pregnancy-birth gestational parent");
        AuthoritativeCharacterProfile otherParent = RequireCurrentCharacter(
            resolvedPregnancy.OtherBiologicalParentCharacterId,
            resolutionDate,
            "Pregnancy-birth other biological parent");
        if (gestationalParent.Condition.VitalStatus != CharacterVitalStatus.Alive
            || otherParent.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException(
                "Pregnancy birth requires both biological parents to be living.");
        }

        EntityId[] inheritedTraits = newborn.InheritedTraitIds.ToArray();
        EntityId[] canonicalTraits = inheritedTraits
            .Distinct()
            .Order()
            .ToArray();
        HashSet<EntityId> parentalTraits = gestationalParent.TraitIds
            .Concat(otherParent.TraitIds)
            .ToHashSet();
        if (inheritedTraits.Length > CharacterBirthLimits.MaximumInheritedTraits
            || !inheritedTraits.SequenceEqual(canonicalTraits)
            || inheritedTraits.Any(id => !id.IsValid || !parentalTraits.Contains(id)))
        {
            throw new SimulationValidationException(
                "Generated-newborn inherited traits must be a canonical distinct parental subset of at most eight traits.");
        }

        EntityId? familyId = RequireSelectedParentValue(
            newborn.FamilyId,
            gestationalParent.FamilyId,
            otherParent.FamilyId,
            "family");
        EntityId? householdId = RequireSelectedParentValue(
            newborn.HouseholdId,
            gestationalParent.HouseholdId,
            otherParent.HouseholdId,
            "household");
        EntityId? cultureId = RequireSelectedParentValue(
            newborn.CultureId,
            gestationalParent.CultureId,
            otherParent.CultureId,
            "culture");

        EntityId childId = CharacterBirthIds.DeriveChildId(
            resolvedPregnancy.PregnancyId);
        EntityId birthId = CharacterBirthIds.DeriveBirthId(
            eventId,
            resolvedPregnancy.PregnancyId);
        if (IsDefinitionIdInUse(childId)
            || IsDefinitionIdInUse(birthId)
            || characterStates.ContainsKey(childId))
        {
            throw new SimulationValidationException(
                $"Pregnancy birth child '{childId}' or birth '{birthId}' collides with existing character-world identity.");
        }

        CharacterContentOrigin generatedOrigin = new(
            CharacterOriginKind.Generated,
            CharacterHistoricalClassification.Fictional,
            birthId,
            null,
            [],
            []);
        CharacterDefinition childDefinition = new(
            CharacterContractVersions.Definition,
            childId,
            newborn.PrimaryNameKey,
            resolvedPregnancy.ExpectedBirthDate,
            [],
            [],
            canonicalTraits,
            [],
            [],
            new StructuredCharacterName(newborn.PrimaryNameKey, null),
            generatedOrigin,
            cultureId,
            null,
            []);
        CharacterParentLink[] parentLinks = new[]
        {
            new CharacterParentLink(
                gestationalParent.CharacterId,
                ParentChildLinkKind.Biological),
            new CharacterParentLink(
                otherParent.CharacterId,
                ParentChildLinkKind.Biological),
        }
            .OrderBy(link => link.ParentCharacterId)
            .ToArray();
        CharacterState childState = new(
            CharacterContractVersions.State,
            childId,
            parentLinks.Select(link => link.ParentCharacterId).ToArray(),
            parentLinks.Select(Clone).ToArray(),
            CharacterConditionState.Default);
        CharacterWorldSnapshot updated = CaptureSnapshot() with
        {
            CharacterDefinitions =
            [
                .. characterDefinitions.Values.Select(Clone),
                Clone(childDefinition),
            ],
            CharacterStates =
            [
                .. characterStates.Values.Select(Clone),
                Clone(childState),
            ],
            FamilyStates = familyStates.Values.Select(state =>
                state.FamilyId == familyId
                    ? state with
                    {
                        MemberIds = state.MemberIds
                            .Append(childId)
                            .Order()
                            .ToArray(),
                    }
                    : Clone(state)).ToArray(),
            HouseholdStates = householdStates.Values.Select(state =>
                state.HouseholdId == householdId
                    ? state with
                    {
                        MemberIds = state.MemberIds
                            .Append(childId)
                            .Order()
                            .ToArray(),
                    }
                    : Clone(state)).ToArray(),
        };
        CampaignDate candidateDate = resolutionDate.CompareTo(campaignDate) > 0
            ? resolutionDate
            : campaignDate;
        CharacterBirthChange birth = new(
            CharacterBirthContractVersions.Change,
            birthId,
            resolvedPregnancy with { },
            Clone(childDefinition),
            Clone(childState),
            familyId,
            householdId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        return new CharacterBirthMutationPlan(
            birth,
            new CharacterWorldUpdatePlan(
                new CharacterWorldState(updated, candidateDate)));
    }

    internal HouseholdDecisionMutationPlan PrepareHouseholdDecision(
        EntityId actingCharacterId,
        IHouseholdDecisionAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is null)
        {
            throw new SimulationValidationException("Household decision action cannot be null.");
        }

        AuthoritativeCharacterProfile actor = RequireCurrentCharacter(
            actingCharacterId,
            resolutionDate,
            "Household decision actor");
        RequireLivingCapableFree(actor, "Household decision actor");

        HouseholdMembershipTransition transition = action switch
        {
            ExpelHouseholdMemberAction value => PlanExpulsion(
                actor,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId,
                eventId),
            IncorporateCaptiveHouseholdMemberAction value => PlanIncorporation(
                actor,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId,
                eventId),
            _ => throw new SimulationValidationException(
                $"Unsupported household decision action '{action.GetType().Name}'."),
        };

        CharacterWorldSnapshot updated = CaptureSnapshot() with
        {
            HouseholdStates = ApplyHouseholdTransition(transition),
        };
        CampaignDate candidateDate = resolutionDate.CompareTo(campaignDate) > 0
            ? resolutionDate
            : campaignDate;
        return new HouseholdDecisionMutationPlan(
            transition,
            new CharacterWorldUpdatePlan(new CharacterWorldState(updated, candidateDate)));
    }

    internal void ApplyPrepared(CharacterWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException("Prepared character-world update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

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

    private CharacterConditionMutationPlan PrepareConditionMutation(
        EntityId characterId,
        CharacterConditionState previous,
        CharacterConditionState next,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId,
        bool allowDeath)
    {
        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(campaignDate) < 0
            || authoritativeTurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-condition resolution date or turn is invalid.");
        }

        if (!commandId.IsValid
            || eventId != CharacterConditionIds.DeriveActionEventId(resolutionDate, commandId))
        {
            throw new SimulationValidationException(
                "Character-condition command or event identity is invalid.");
        }

        if (!allowDeath && next.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException(
                "Public character-condition actions cannot change vital status; death is reserved for SP-04F.");
        }

        CharacterWorldSnapshot updated = CaptureSnapshot() with
        {
            CharacterStates = characterStates.Values.Select(state =>
                state.CharacterId == characterId
                    ? state with { Condition = Clone(next) }
                    : Clone(state)).ToArray(),
        };
        CampaignDate candidateDate = resolutionDate.CompareTo(campaignDate) > 0
            ? resolutionDate
            : campaignDate;
        CharacterConditionChange change = new(
            CharacterConditionContractVersions.Change,
            CharacterConditionIds.DeriveChangeId(eventId, characterId),
            characterId,
            Clone(previous),
            Clone(next),
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        return new CharacterConditionMutationPlan(
            change,
            new CharacterWorldUpdatePlan(new CharacterWorldState(updated, candidateDate)));
    }

    private CharacterConditionState PlanCustodyEntry(
        AuthoritativeCharacterProfile target,
        EnterCharacterCustodyAction action,
        CampaignDate resolutionDate)
    {
        if (target.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            throw new SimulationValidationException(
                $"Character '{target.CharacterId}' is already in custody.");
        }

        if (action.CustodyStatus is CharacterCustodyStatus.Free
            || !Enum.IsDefined(action.CustodyStatus))
        {
            throw new SimulationValidationException(
                "Custody entry requires a supported non-free custody status.");
        }

        AuthoritativeCharacterProfile custodian = RequireCurrentCharacter(
            action.CustodianCharacterId,
            resolutionDate,
            "Character custodian");
        if (custodian.CharacterId == target.CharacterId
            || custodian.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException(
                "Character custodian must be a different living character.");
        }

        return target.Condition with
        {
            CustodyStatus = action.CustodyStatus,
            CustodianId = custodian.CharacterId,
        };
    }

    private HouseholdMembershipTransition PlanExpulsion(
        AuthoritativeCharacterProfile actor,
        ExpelHouseholdMemberAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (!TryGetHousehold(action.HouseholdId, out AuthoritativeHouseholdView? household))
        {
            throw new SimulationValidationException(
                $"Expulsion household '{action.HouseholdId}' does not exist.");
        }

        if (household.HeadCharacterId != actor.CharacterId)
        {
            throw new SimulationValidationException(
                "Only the current household head may expel a member.");
        }

        AuthoritativeCharacterProfile member = RequireCurrentCharacter(
            action.MemberCharacterId,
            resolutionDate,
            "Expulsion target");
        if (member.HouseholdId != household.HouseholdId)
        {
            throw new SimulationValidationException(
                "Expulsion target is not a current member of the specified household.");
        }

        if (member.CharacterId == household.HeadCharacterId)
        {
            throw new SimulationValidationException("A household head cannot be expelled.");
        }

        if (member.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException("Expulsion target must be alive.");
        }

        return CreateTransition(
            HouseholdDecisionKind.Expulsion,
            member.CharacterId,
            household.HouseholdId,
            null,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
    }

    private HouseholdMembershipTransition PlanIncorporation(
        AuthoritativeCharacterProfile actor,
        IncorporateCaptiveHouseholdMemberAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (!TryGetHousehold(
                action.DestinationHouseholdId,
                out AuthoritativeHouseholdView? destination))
        {
            throw new SimulationValidationException(
                $"Incorporation household '{action.DestinationHouseholdId}' does not exist.");
        }

        if (destination.HeadCharacterId != actor.CharacterId)
        {
            throw new SimulationValidationException(
                "Only the destination household head may incorporate a captive member.");
        }

        AuthoritativeCharacterProfile member = RequireCurrentCharacter(
            action.MemberCharacterId,
            resolutionDate,
            "Incorporation target");
        if (member.Condition.VitalStatus != CharacterVitalStatus.Alive
            || member.Condition.CustodyStatus is not (
                CharacterCustodyStatus.Captive or CharacterCustodyStatus.Hostage)
            || member.Condition.CustodianId != actor.CharacterId)
        {
            throw new SimulationValidationException(
                "Incorporation requires a living captive or hostage held by the destination household head.");
        }

        if (member.HouseholdId == destination.HouseholdId)
        {
            throw new SimulationValidationException(
                "Incorporation target already belongs to the destination household.");
        }

        if (member.HouseholdId is EntityId sourceId
            && householdStates[sourceId].HeadCharacterId == member.CharacterId)
        {
            throw new SimulationValidationException(
                "A household head cannot be moved by captive incorporation.");
        }

        return CreateTransition(
            HouseholdDecisionKind.CaptiveIncorporation,
            member.CharacterId,
            member.HouseholdId,
            destination.HouseholdId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
    }

    private static HouseholdMembershipTransition CreateTransition(
        HouseholdDecisionKind kind,
        EntityId memberCharacterId,
        EntityId? sourceHouseholdId,
        EntityId? destinationHouseholdId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId) => new(
            HouseholdDecisionContractVersions.Transition,
            HouseholdDecisionIds.DeriveTransitionId(eventId, memberCharacterId),
            kind,
            memberCharacterId,
            sourceHouseholdId,
            destinationHouseholdId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);

    private HouseholdState[] ApplyHouseholdTransition(HouseholdMembershipTransition transition)
    {
        if (transition.ContractVersion != HouseholdDecisionContractVersions.Transition
            || !Enum.IsDefined(transition.Kind)
            || transition.TransitionId != HouseholdDecisionIds.DeriveTransitionId(
                CharacterConditionSafeEventId(transition),
                transition.MemberCharacterId))
        {
            throw new SimulationValidationException(
                "Household membership transition contract or identity is invalid.");
        }

        if (!householdByCharacter.TryGetValue(
                transition.MemberCharacterId,
                out EntityId currentHouseholdId))
        {
            if (transition.SourceHouseholdId is not null)
            {
                throw new SimulationValidationException(
                    "Household transition source does not match current membership.");
            }
        }
        else if (transition.SourceHouseholdId != currentHouseholdId)
        {
            throw new SimulationValidationException(
                "Household transition source does not match current membership.");
        }

        if (transition.DestinationHouseholdId is EntityId destinationId
            && !householdStates.ContainsKey(destinationId))
        {
            throw new SimulationValidationException(
                $"Household transition destination '{destinationId}' does not exist.");
        }

        return householdStates.Values.Select(state =>
        {
            IEnumerable<EntityId> members = state.MemberIds;
            if (transition.SourceHouseholdId == state.HouseholdId)
            {
                members = members.Where(id => id != transition.MemberCharacterId);
            }

            if (transition.DestinationHouseholdId == state.HouseholdId)
            {
                members = members.Append(transition.MemberCharacterId);
            }

            return state with { MemberIds = members.Distinct().Order().ToArray() };
        }).ToArray();
    }

    private static EntityId CharacterConditionSafeEventId(
        HouseholdMembershipTransition transition) =>
        HouseholdDecisionIds.DeriveActionEventId(
            transition.ResolutionDate,
            transition.SourceCommandId);

    private AuthoritativeCharacterProfile RequireCurrentCharacter(
        EntityId characterId,
        CampaignDate resolutionDate,
        string description)
    {
        if (!characterId.IsValid
            || !TryGetCharacterProfile(characterId, out AuthoritativeCharacterProfile? profile)
            || !resolutionDate.IsValid
            || resolutionDate.CompareTo(campaignDate) < 0
            || profile.BirthDate.CompareTo(resolutionDate) > 0)
        {
            throw new SimulationValidationException(
                $"{description} '{characterId}' is unavailable at '{resolutionDate}'.");
        }

        return profile;
    }

    private static void RequireLivingCapableFree(
        AuthoritativeCharacterProfile profile,
        string description)
    {
        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive
            || profile.Condition.IsIncapacitated
            || profile.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            throw new SimulationValidationException(
                $"{description} '{profile.CharacterId}' must be living, capable, and free.");
        }
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

    private static EntityId? RequireSelectedParentValue(
        EntityId? selected,
        EntityId? gestationalParentValue,
        EntityId? otherParentValue,
        string category)
    {
        EntityId[] available = new[]
        {
            gestationalParentValue,
            otherParentValue,
        }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .Order()
            .ToArray();
        if (available.Length == 0)
        {
            if (selected is not null)
            {
                throw new SimulationValidationException(
                    $"Generated-newborn {category} cannot be selected when neither parent has one.");
            }

            return null;
        }

        if (selected is not EntityId selectedValue
            || !available.Contains(selectedValue))
        {
            throw new SimulationValidationException(
                $"Generated-newborn {category} must select one current non-null parent value.");
        }

        return selectedValue;
    }

    private bool IsDefinitionIdInUse(EntityId id) =>
        identityDefinitions.ContainsKey(id)
        || characterDefinitions.ContainsKey(id)
        || familyDefinitions.ContainsKey(id)
        || householdDefinitions.ContainsKey(id);

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

    private static CharacterParentLink[] ValidateAndCloneExpectedParentLinks(
        IReadOnlyList<CharacterParentLink>? links)
    {
        if (links is null)
        {
            throw new SimulationValidationException(
                "Character-family expected-current parent links cannot be null.");
        }

        List<CharacterParentLink> canonical = new(links.Count);
        EntityId? previousParentId = null;
        foreach (CharacterParentLink? link in links)
        {
            if (link is null)
            {
                throw new SimulationValidationException(
                    "Character-family expected-current parent links cannot contain null entries.");
            }

            ValidateId(
                link.ParentCharacterId,
                "Character-family expected-current parent ID");
            if (!Enum.IsDefined(link.Kind))
            {
                throw new SimulationValidationException(
                    "Character-family expected-current parent links contain an invalid kind.");
            }

            if (previousParentId is EntityId previous
                && previous.CompareTo(link.ParentCharacterId) >= 0)
            {
                throw new SimulationValidationException(
                    "Character-family expected-current parent links must contain unique parent IDs in ordinal canonical order.");
            }

            canonical.Add(Clone(link));
            previousParentId = link.ParentCharacterId;
        }

        return canonical.ToArray();
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

    private void ReplaceFrom(CharacterWorldState source)
    {
        identityDefinitions.Clear();
        characterDefinitions.Clear();
        familyDefinitions.Clear();
        householdDefinitions.Clear();
        characterStates.Clear();
        familyStates.Clear();
        householdStates.Clear();
        familyByCharacter.Clear();
        householdByCharacter.Clear();
        childrenByCharacter.Clear();

        foreach (CharacterIdentityDefinition value in source.identityDefinitions.Values)
        {
            identityDefinitions.Add(value.Id, value with { });
        }

        foreach (CharacterDefinition value in source.characterDefinitions.Values)
        {
            characterDefinitions.Add(value.Id, Clone(value));
        }

        foreach (FamilyDefinition value in source.familyDefinitions.Values)
        {
            familyDefinitions.Add(value.Id, value with { });
        }

        foreach (HouseholdDefinition value in source.householdDefinitions.Values)
        {
            householdDefinitions.Add(value.Id, value with { });
        }

        foreach (CharacterState value in source.characterStates.Values)
        {
            characterStates.Add(value.CharacterId, Clone(value));
        }

        foreach (FamilyState value in source.familyStates.Values)
        {
            familyStates.Add(value.FamilyId, Clone(value));
        }

        foreach (HouseholdState value in source.householdStates.Values)
        {
            householdStates.Add(value.HouseholdId, Clone(value));
        }

        foreach ((EntityId characterId, EntityId familyId) in source.familyByCharacter)
        {
            familyByCharacter.Add(characterId, familyId);
        }

        foreach ((EntityId characterId, EntityId householdId) in source.householdByCharacter)
        {
            householdByCharacter.Add(characterId, householdId);
        }

        foreach ((EntityId characterId, CharacterChildLink[] children) in source.childrenByCharacter)
        {
            childrenByCharacter.Add(characterId, children.Select(Clone).ToArray());
        }

        campaignDate = source.campaignDate;
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}

internal sealed record CharacterWorldUpdatePlan(CharacterWorldState Candidate);

internal sealed record CharacterFamilyMutationPlan(
    CharacterParentageChange Change,
    CharacterWorldUpdatePlan CharacterPlan);

internal sealed record CharacterConditionMutationPlan(
    CharacterConditionChange Change,
    CharacterWorldUpdatePlan CharacterPlan);

internal sealed record HouseholdDecisionMutationPlan(
    HouseholdMembershipTransition Transition,
    CharacterWorldUpdatePlan CharacterPlan);

internal sealed record CharacterBirthMutationPlan(
    CharacterBirthChange Birth,
    CharacterWorldUpdatePlan CharacterPlan);
