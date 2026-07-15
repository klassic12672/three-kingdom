using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public interface IWorldQuery
{
    CampaignCalendar Calendar { get; }

    IReadOnlyList<SyntheticEntitySnapshot> Entities { get; }

    IGeographicWorldQuery Geography { get; }

    IAuthoritativeCharacterWorldQuery Characters { get; }

    IAuthoritativeRelationshipWorldQuery Relationships { get; }

    IAuthoritativeCareerWorldQuery Careers { get; }

    bool TryGetEntity(EntityId id, [NotNullWhen(true)] out SyntheticEntitySnapshot? entity);
}

public sealed class WorldState : IWorldQuery
{
    private static readonly IReadOnlyList<SystemVersion> CurrentSystemVersions =
    [
        new("simulation.calendar", 1),
        new("simulation.synthetic_entities", 1),
        new("simulation.command_events", 1),
        new("simulation.geography", 1),
        new("simulation.characters", CharacterContractVersions.Snapshot),
        new("simulation.relationships", RelationshipContractVersions.Snapshot),
        new("simulation.character_careers", CareerContractVersions.Snapshot),
    ];

    private readonly SortedDictionary<EntityId, SyntheticEntitySnapshot> entities = [];
    private readonly List<CampaignCommand> pendingCommands = [];
    private CampaignDate? lastEventDate;
    private ResolutionPhase? lastEventPhase;
    private int? lastEventPriority;
    private EntityId? lastEventId;

    private WorldState(
        CampaignCalendar calendar,
        ulong rootSeed,
        IEnumerable<RandomStreamState>? streams,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers)
    {
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException("Campaign calendar is invalid.");
        }

        Calendar = calendar;
        RootSeed = rootSeed;
        Random = new DeterministicRandomStreams(rootSeed, streams);
        Geography = new GeographicWorldState(geography);
        Characters = new CharacterWorldState(characters, calendar.Date);
        Relationships = new RelationshipWorldState(relationships, Characters, calendar);
        Careers = new CharacterCareerWorldState(careers, Characters, calendar);
    }

    public CampaignCalendar Calendar { get; private set; }

    public ulong RootSeed { get; }

    internal DeterministicRandomStreams Random { get; }

    public GeographicWorldState Geography { get; }

    public CharacterWorldState Characters { get; }

    public RelationshipWorldState Relationships { get; }

    public CharacterCareerWorldState Careers { get; }

    IGeographicWorldQuery IWorldQuery.Geography => Geography;

    IAuthoritativeCharacterWorldQuery IWorldQuery.Characters => Characters;

    IAuthoritativeRelationshipWorldQuery IWorldQuery.Relationships => Relationships;

    IAuthoritativeCareerWorldQuery IWorldQuery.Careers => Careers;

    public IReadOnlyList<SyntheticEntitySnapshot> Entities => entities.Values.Select(CloneEntity).ToArray();

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot? geography = null) => Create(
            startDate,
            seed,
            initialEntities,
            geography ?? GeographicWorldSnapshot.Empty,
            CharacterWorldSnapshot.Empty,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters) => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships) => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            relationships,
            CareerWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers)
    {
        WorldState world = new(
            new CampaignCalendar(startDate, 0),
            seed,
            null,
            geography,
            characters,
            relationships,
            careers);
        foreach (SyntheticEntitySnapshot entity in initialEntities.OrderBy(item => item.Id))
        {
            SyntheticEntitySnapshot canonical = ValidateEntity(entity).Canonicalize();
            if (!world.entities.TryAdd(canonical.Id, canonical))
            {
                throw new SimulationValidationException($"Duplicate entity ID '{canonical.Id}'.");
            }
        }

        return world;
    }

    public static WorldState Restore(WorldSnapshot snapshot)
    {
        if (snapshot is null
            || snapshot.RandomStreams is null
            || snapshot.Entities is null
            || snapshot.PendingCommands is null
            || snapshot.SystemVersions is null
            || snapshot.Geography is null
            || snapshot.Characters is null
            || snapshot.Relationships is null
            || snapshot.Careers is null
            || snapshot.Entities.Any(entity => entity is null)
            || snapshot.PendingCommands.Any(command => command is null))
        {
            throw new SaveCompatibilityException("Snapshot is missing required objects or collections.");
        }

        if (snapshot.ContractVersion != ContractVersions.WorldSnapshot)
        {
            throw new SaveCompatibilityException($"Unsupported world snapshot contract version {snapshot.ContractVersion}.");
        }

        (CharacterWorldSnapshot characters, CareerWorldSnapshot careers) = ValidateSystemVersions(
            snapshot.SystemVersions,
            snapshot.Characters,
            snapshot.Relationships,
            snapshot.Careers);
        ValidatePendingCommands(snapshot.PendingCommands);

        WorldState world = new(
            snapshot.Calendar,
            snapshot.RootSeed,
            snapshot.RandomStreams,
            snapshot.Geography,
            characters,
            snapshot.Relationships,
            careers)
        {
            lastEventDate = snapshot.LastEventDate,
            lastEventPhase = snapshot.LastEventPhase,
            lastEventPriority = snapshot.LastEventPriority,
            lastEventId = snapshot.LastEventId,
        };

        foreach (SyntheticEntitySnapshot entity in snapshot.Entities.OrderBy(item => item.Id))
        {
            SyntheticEntitySnapshot canonical = ValidateEntity(entity).Canonicalize();
            if (!world.entities.TryAdd(canonical.Id, canonical))
            {
                throw new SimulationValidationException($"Duplicate entity ID '{canonical.Id}' in snapshot.");
            }
        }

        world.pendingCommands.AddRange(snapshot.PendingCommands.OrderBy(command => command, CommandComparer.Instance));
        return world;
    }

    public bool TryGetEntity(EntityId id, [NotNullWhen(true)] out SyntheticEntitySnapshot? entity)
    {
        if (entities.TryGetValue(id, out SyntheticEntitySnapshot? stored))
        {
            entity = CloneEntity(stored);
            return true;
        }

        entity = null;
        return false;
    }

    public WorldSnapshot CaptureSnapshot() => new(
        ContractVersions.WorldSnapshot,
        Calendar,
        RootSeed,
        Random.Capture(),
        entities.Values.Select(CloneEntity).ToArray(),
        pendingCommands.OrderBy(command => command, CommandComparer.Instance).ToArray(),
        CurrentSystemVersions.ToArray(),
        lastEventDate,
        lastEventPhase,
        lastEventPriority,
        lastEventId)
    {
        Geography = Geography.CaptureSnapshot(),
        Characters = Characters.CaptureSnapshot(),
        Relationships = Relationships.CaptureSnapshot(),
        Careers = Careers.CaptureSnapshot(),
    };

    internal void Enqueue(CampaignCommand command)
    {
        if (pendingCommands.Any(existing => existing.CommandId == command.CommandId))
        {
            throw new SimulationValidationException($"Duplicate command ID '{command.CommandId}'.");
        }

        pendingCommands.Add(command);
    }

    internal bool ContainsCommand(EntityId commandId) =>
        pendingCommands.Any(command => command.CommandId == commandId);

    internal IReadOnlyList<CampaignCommand> DequeueCommandsFor(CampaignDate date, ResolutionPhase phase)
    {
        CampaignCommand[] due = pendingCommands
            .Where(command => command.IssuedDate == date && command.Phase == phase)
            .OrderBy(command => command, CommandComparer.Instance)
            .ToArray();
        foreach (CampaignCommand command in due)
        {
            pendingCommands.Remove(command);
        }

        return due;
    }

    internal void Apply(CampaignEvent campaignEvent)
    {
        ValidateEventOrder(campaignEvent);
        switch (campaignEvent.Payload)
        {
            case ResourcesAdjustedEventPayload adjustment:
                ApplyAdjustment(adjustment);
                break;
            case SimulationTierChangedEventPayload transition:
                ApplyTransition(transition);
                break;
            case CommandCancelledEventPayload:
                break;
            case MovementEventPayload
                or InterceptionEventPayload
                or ControlChangedEventPayload
                or SupplyTransferredEventPayload
                or SupplyProducedEventPayload
                or ArmySupplyConsumedEventPayload:
                Geography.Apply(campaignEvent.Payload, campaignEvent.ResolutionDate);
                break;
            case RelationshipActionResolvedEventPayload:
                Relationships.Apply(campaignEvent, Calendar.TurnIndex);
                break;
            case CharacterActionResolvedEventPayload characterAction:
                ApplyCharacterAction(campaignEvent, characterAction);
                break;
            default:
                throw new SimulationValidationException($"Unregistered event payload '{campaignEvent.Payload.GetType().Name}'.");
        }

        lastEventDate = campaignEvent.ResolutionDate;
        lastEventPhase = campaignEvent.Phase;
        lastEventPriority = campaignEvent.Priority;
        lastEventId = campaignEvent.EventId;
    }

    internal void AdvanceCalendar()
    {
        Calendar = Calendar.NextTurn();
        Characters.UpdateCampaignDate(Calendar.Date);
        Careers.UpdateCampaignCalendar(Calendar);
    }

    internal IReadOnlyList<CampaignEvent> PlanGeographicEvents(CampaignDate date) =>
        Geography.PlanDailyEvents(date, Calendar.TurnIndex);

    private void ApplyAdjustment(ResourcesAdjustedEventPayload adjustment)
    {
        if (!entities.TryGetValue(adjustment.Target, out SyntheticEntitySnapshot? entity) || entity is null)
        {
            throw new SimulationValidationException($"Target entity '{adjustment.Target}' does not exist.");
        }

        SyntheticEntitySnapshot updated = entity with
        {
            People = checked(entity.People + adjustment.PeopleDelta),
            Food = checked(entity.Food + adjustment.FoodDelta),
            Gold = checked(entity.Gold + adjustment.GoldDelta),
        };
        entities[entity.Id] = ValidateEntity(updated);
    }

    private void ApplyTransition(SimulationTierChangedEventPayload transition)
    {
        if (!entities.TryGetValue(transition.Target, out SyntheticEntitySnapshot? entity) || entity is null)
        {
            throw new SimulationValidationException($"Tier target '{transition.Target}' does not exist.");
        }

        ConservationLedger actualBefore = ConservationLedger.From(entity);
        if (actualBefore != transition.Before || transition.Before != transition.After)
        {
            throw new SimulationValidationException($"Tier transition for '{transition.Target}' violates conservation.");
        }

        entities[entity.Id] = entity with { Tier = transition.Tier };
    }

    private void ApplyCharacterAction(
        CampaignEvent campaignEvent,
        CharacterActionResolvedEventPayload payload)
    {
        if (campaignEvent.CausalId is not EntityId commandId || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Character action event requires a valid causal command ID.");
        }

        EntityId expectedEventId = CareerIds.DeriveCharacterActionEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                GetCharacterActionAffectedIds(payload, campaignEvent.EventId)))
        {
            throw new SimulationValidationException(
                "Character action event identity or affected IDs do not match its exact deterministic consequences.");
        }

        CharacterCareerWorldUpdatePlan careerPlan = Careers.PrepareOutcome(
            payload,
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        RelationshipWorldUpdatePlan relationshipPlan = Relationships.PrepareCharacterActionConsequences(
            payload,
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            campaignEvent.EventId);
        Careers.ApplyPrepared(careerPlan);
        Relationships.ApplyPrepared(relationshipPlan);
    }

    internal static EntityId[] GetCharacterActionAffectedIds(
        CharacterActionResolvedEventPayload payload,
        EntityId eventId)
    {
        if (payload is null
            || payload.Outcome is null
            || payload.RelationshipMemoryConsequences is null)
        {
            throw new SimulationValidationException(
                "Character action event contains null consequence data.");
        }

        HashSet<EntityId> affected = [payload.ActingCharacterId];
        switch (payload.Outcome)
        {
            case CareerProposalCreatedOutcome value:
                AddProposal(affected, value.Proposal);
                break;
            case CareerProposalRefusedOutcome value:
                AddProposal(affected, value.Proposal);
                break;
            case CareerProposalWithdrawnOutcome value:
                AddProposal(affected, value.Proposal);
                break;
            case CareerProposalInvalidatedOutcome value:
                AddProposal(affected, value.Proposal);
                break;
            case RetinueInvitationAcceptedOutcome value:
                AddProposal(affected, value.Proposal);
                affected.Add(value.Retinue.RetinueId);
                affected.Add(value.Retinue.LeaderCharacterId);
                affected.Add(value.Membership.MembershipId);
                affected.Add(value.Membership.MemberCharacterId);
                break;
            case RetinueMembershipEndedOutcome value:
                affected.Add(value.Membership.MembershipId);
                affected.Add(value.Membership.RetinueId);
                affected.Add(value.Membership.LeaderCharacterId);
                affected.Add(value.Membership.MemberCharacterId);
                break;
            case PatronageOfferAcceptedOutcome value:
                AddProposal(affected, value.Proposal);
                affected.Add(value.Bond.BondId);
                affected.Add(value.Bond.PatronCharacterId);
                affected.Add(value.Bond.BeneficiaryCharacterId);
                break;
            case PatronageBondEndedOutcome value:
                affected.Add(value.Bond.BondId);
                affected.Add(value.Bond.PatronCharacterId);
                affected.Add(value.Bond.BeneficiaryCharacterId);
                break;
            case RecommendationRecordedOutcome value:
                affected.Add(value.Recommendation.RecommendationId);
                affected.Add(value.Recommendation.RecommenderCharacterId);
                affected.Add(value.Recommendation.BeneficiaryCharacterId);
                affected.Add(value.Recommendation.Principal.PrincipalId);
                if (value.Recommendation.RecommendedRoleId is EntityId roleId)
                {
                    affected.Add(roleId);
                }

                break;
            case EmploymentOfferAcceptedOutcome value:
                AddProposal(affected, value.Proposal);
                affected.Add(value.Tenure.TenureId);
                affected.Add(value.Tenure.EmployeeCharacterId);
                affected.Add(value.Tenure.Employer.PrincipalId);
                affected.Add(value.Tenure.RoleId);
                break;
            case EmploymentTenureEndedOutcome value:
                affected.Add(value.Tenure.TenureId);
                affected.Add(value.Tenure.EmployeeCharacterId);
                affected.Add(value.Tenure.Employer.PrincipalId);
                affected.Add(value.Tenure.RoleId);
                break;
            default:
                throw new SimulationValidationException(
                    $"Unregistered character action outcome '{payload.Outcome.GetType().Name}'.");
        }

        for (int index = 0; index < payload.RelationshipMemoryConsequences.Count; index++)
        {
            RelationshipMemoryConsequenceSpecification consequence =
                payload.RelationshipMemoryConsequences[index];
            affected.Add(consequence.ConsequenceId);
            affected.Add(consequence.SubjectCharacterId);
            affected.Add(consequence.TargetCharacterId);
            affected.Add(RelationshipIds.DeriveRelationshipId(
                consequence.SubjectCharacterId,
                consequence.TargetCharacterId));
            affected.Add(RelationshipIds.DeriveMemoryId(
                eventId,
                consequence.SubjectCharacterId,
                consequence.TargetCharacterId,
                index));
        }

        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Character action event contains an invalid affected ID.");
        }

        return affected.Order().ToArray();
    }

    private static void AddProposal(ISet<EntityId> affected, CareerProposalState proposal)
    {
        affected.Add(proposal.ProposalId);
        affected.Add(proposal.ProposerCharacterId);
        affected.Add(proposal.RecipientCharacterId);
        affected.Add(proposal.Principal.PrincipalId);
        if (proposal.ProposedRoleId is EntityId roleId)
        {
            affected.Add(roleId);
        }
    }

    private void ValidateEventOrder(CampaignEvent campaignEvent)
    {
        if (campaignEvent.ContractVersion != ContractVersions.CampaignEvent)
        {
            throw new SimulationValidationException("Unsupported event contract version.");
        }

        if (!campaignEvent.EventId.IsValid || !campaignEvent.ResolutionDate.IsValid)
        {
            throw new SimulationValidationException("Event has an invalid ID or resolution date.");
        }

        if (!Enum.IsDefined(campaignEvent.Phase))
        {
            throw new SimulationValidationException($"Event '{campaignEvent.EventId}' has an unregistered resolution phase.");
        }

        EntityId[] canonicalAffected = campaignEvent.AffectedIds.Order().ToArray();
        if (canonicalAffected.Any(id => !id.IsValid)
            || canonicalAffected.Distinct().Count() != canonicalAffected.Length
            || !canonicalAffected.SequenceEqual(campaignEvent.AffectedIds))
        {
            throw new SimulationValidationException($"Event '{campaignEvent.EventId}' has invalid or non-canonical affected IDs.");
        }

        if (lastEventDate is null)
        {
            return;
        }

        CampaignDate previousDate = lastEventDate.GetValueOrDefault();
        ResolutionPhase previousPhase = lastEventPhase.GetValueOrDefault();
        int previousPriority = lastEventPriority.GetValueOrDefault();
        EntityId previousId = lastEventId.GetValueOrDefault();
        int dateComparison = campaignEvent.ResolutionDate.CompareTo(previousDate);
        bool before = dateComparison < 0
            || (dateComparison == 0 && campaignEvent.Phase < previousPhase)
            || (dateComparison == 0 && campaignEvent.Phase == previousPhase && campaignEvent.Priority < previousPriority)
            || (dateComparison == 0 && campaignEvent.Phase == previousPhase && campaignEvent.Priority == previousPriority && campaignEvent.EventId.CompareTo(previousId) <= 0);
        if (before)
        {
            throw new SimulationValidationException($"Event '{campaignEvent.EventId}' is out of authoritative order.");
        }
    }

    private static SyntheticEntitySnapshot ValidateEntity(SyntheticEntitySnapshot entity)
    {
        if (!entity.Id.IsValid)
        {
            throw new SimulationValidationException("Entity has an invalid ID.");
        }

        if (entity.People < 0 || entity.Food < 0 || entity.Gold < 0 || entity.PendingWork.Any(work => work.Amount < 0))
        {
            throw new SimulationValidationException($"Entity '{entity.Id}' has negative conserved values.");
        }

        if (entity.PendingWork.Select(work => work.WorkId).Distinct().Count() != entity.PendingWork.Count)
        {
            throw new SimulationValidationException($"Entity '{entity.Id}' has duplicate pending work IDs.");
        }

        if (entity.PendingWork.Any(work => !work.WorkId.IsValid || !work.DueDate.IsValid))
        {
            throw new SimulationValidationException($"Entity '{entity.Id}' has invalid pending work.");
        }

        return entity;
    }

    private static (CharacterWorldSnapshot Characters, CareerWorldSnapshot Careers) ValidateSystemVersions(
        IReadOnlyList<SystemVersion> versions,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers)
    {
        if (versions is null || versions.Any(version => version is null))
        {
            throw new SaveCompatibilityException("Snapshot system versions are missing or contain null entries.");
        }

        if (characters is null)
        {
            throw new SaveCompatibilityException("Snapshot character state is missing.");
        }

        if (relationships is null)
        {
            throw new SaveCompatibilityException("Snapshot relationship state is missing.");
        }

        if (careers is null)
        {
            throw new SaveCompatibilityException("Snapshot character-career state is missing.");
        }

        string[] expectedCore = CurrentSystemVersions
            .Where(version => version.SystemId is not "simulation.characters"
                and not "simulation.relationships"
                and not "simulation.character_careers")
            .Select(version => $"{version.SystemId}@{version.Version}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualCore = versions
            .Where(version => version.SystemId is not "simulation.characters"
                and not "simulation.relationships"
                and not "simulation.character_careers")
            .Select(version => $"{version.SystemId}@{version.Version}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!expectedCore.SequenceEqual(actualCore, StringComparer.Ordinal))
        {
            throw new SaveCompatibilityException(
                $"Snapshot core system versions are incompatible. Expected [{string.Join(", ", expectedCore)}], "
                + $"found [{string.Join(", ", actualCore)}].");
        }

        SystemVersion[] characterVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(version.SystemId, "simulation.characters"))
            .ToArray();
        CharacterWorldSnapshot normalizedCharacters;
        if (characterVersions.Length == 1
            && characterVersions[0].Version == CharacterContractVersions.Snapshot)
        {
            if (characters.ContractVersion != CharacterContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares 'simulation.characters@{CharacterContractVersions.Snapshot}' but contains "
                    + $"character contract {characters.ContractVersion}.");
            }

            normalizedCharacters = characters;
        }
        else if (characterVersions.Length == 0
            || (characterVersions.Length == 1
                && characterVersions[0].Version == CharacterContractVersions.LegacySnapshot))
        {
            if (!IsCompleteEmptyCharacterSnapshot(characters))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current 'simulation.characters@2' data must contain a complete, valid, empty character snapshot.");
            }

            normalizedCharacters = CharacterWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot character system version is incompatible. Expected 'simulation.characters@{CharacterContractVersions.Snapshot}'.");
        }

        SystemVersion[] relationshipVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(version.SystemId, "simulation.relationships"))
            .ToArray();
        if (relationshipVersions.Length == 0)
        {
            if (!IsCompleteEmptyRelationshipSnapshot(relationships))
            {
                throw new SaveCompatibilityException(
                    $"A legacy snapshot without 'simulation.relationships@{RelationshipContractVersions.Snapshot}' must contain a complete, valid, empty relationship snapshot.");
            }
        }
        else if (relationshipVersions.Length != 1
            || relationshipVersions[0].Version != RelationshipContractVersions.Snapshot)
        {
            throw new SaveCompatibilityException(
                $"Snapshot relationship system version is incompatible. Expected 'simulation.relationships@{RelationshipContractVersions.Snapshot}'.");
        }

        SystemVersion[] careerVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(
                version.SystemId,
                "simulation.character_careers"))
            .ToArray();
        CareerWorldSnapshot normalizedCareers;
        if (careerVersions.Length == 1
            && careerVersions[0].Version == CareerContractVersions.Snapshot)
        {
            if (careers.ContractVersion != CareerContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares 'simulation.character_careers@{CareerContractVersions.Snapshot}' but contains career contract {careers.ContractVersion}.");
            }

            normalizedCareers = careers;
        }
        else if (careerVersions.Length == 0)
        {
            if (!IsCompleteEmptyCareerSnapshot(careers))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current character-career data must contain a complete, valid, empty career snapshot.");
            }

            normalizedCareers = CareerWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot career system version is incompatible. Expected 'simulation.character_careers@{CareerContractVersions.Snapshot}'.");
        }

        return (normalizedCharacters, normalizedCareers);
    }

    private static bool IsCompleteEmptyCharacterSnapshot(CharacterWorldSnapshot characters) =>
        characters.ContractVersion is CharacterContractVersions.LegacySnapshot or CharacterContractVersions.Snapshot
        && characters.IdentityDefinitions is { Count: 0 }
        && characters.CharacterDefinitions is { Count: 0 }
        && characters.FamilyDefinitions is { Count: 0 }
        && characters.HouseholdDefinitions is { Count: 0 }
        && characters.CharacterStates is { Count: 0 }
        && characters.FamilyStates is { Count: 0 }
        && characters.HouseholdStates is { Count: 0 };

    private static bool IsCompleteEmptyRelationshipSnapshot(RelationshipWorldSnapshot relationships) =>
        relationships.ContractVersion == RelationshipContractVersions.Snapshot
        && relationships.Subjects is { Count: 0 };

    private static bool IsCompleteEmptyCareerSnapshot(CareerWorldSnapshot careers) =>
        careers.ContractVersion == CareerContractVersions.Snapshot
        && careers.Proposals is { Count: 0 }
        && careers.Retinues is { Count: 0 }
        && careers.RetinueMemberships is { Count: 0 }
        && careers.PatronageBonds is { Count: 0 }
        && careers.Recommendations is { Count: 0 }
        && careers.EmploymentTenures is { Count: 0 }
        && careers.History is { Count: 0 };

    private static void ValidatePendingCommands(IReadOnlyList<CampaignCommand> commands)
    {
        if (commands.Select(command => command.CommandId).Distinct().Count() != commands.Count)
        {
            throw new SimulationValidationException("Snapshot contains duplicate pending command IDs.");
        }

        if (commands.Any(command => command.ContractVersion != ContractVersions.CampaignCommand
            || !command.CommandId.IsValid
            || !command.IssuingActor.IsValid
            || !command.IssuedDate.IsValid
            || !Enum.IsDefined(command.Phase)
            || command.Payload is null))
        {
            throw new SimulationValidationException("Snapshot contains an invalid pending command.");
        }

        foreach (CampaignCommand command in commands.Where(
            command => command.Payload is RelationshipActionCommandPayload
                or CharacterActionCommandPayload))
        {
            try
            {
                _ = command.Payload is RelationshipActionCommandPayload
                    ? RelationshipWorldState.DeriveEventId(command.IssuedDate, command.CommandId)
                    : CareerIds.DeriveCharacterActionEventId(command.IssuedDate, command.CommandId);
            }
            catch (Exception exception) when (exception is SimulationValidationException
                or ArgumentException)
            {
                throw new SimulationValidationException(
                    $"Snapshot contains a character-domain command with invalid event identity: {exception.Message}");
            }
        }
    }

    private static SyntheticEntitySnapshot CloneEntity(SyntheticEntitySnapshot entity) => entity with
    {
        PendingWork = entity.PendingWork.ToArray(),
    };
}

internal sealed class CommandComparer : IComparer<CampaignCommand>
{
    public static CommandComparer Instance { get; } = new();

    public int Compare(CampaignCommand? left, CampaignCommand? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int result = left.IssuedDate.CompareTo(right.IssuedDate);
        result = result != 0 ? result : left.Phase.CompareTo(right.Phase);
        result = result != 0 ? result : left.Priority.CompareTo(right.Priority);
        return result != 0 ? result : left.CommandId.CompareTo(right.CommandId);
    }
}
