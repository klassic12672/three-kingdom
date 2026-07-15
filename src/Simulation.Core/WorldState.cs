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

    IAuthoritativeCharacterResourceWorldQuery CharacterResources { get; }

    IAuthoritativeCharacterEstateHoldingWorldQuery CharacterEstateHoldings { get; }

    IAuthoritativeCharacterMarriageWorldQuery CharacterMarriages { get; }

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
        new(CharacterResourceSystem.SystemId, CharacterResourceSystem.Version),
        new(CharacterEstateHoldingSystem.SystemId, CharacterEstateHoldingSystem.Version),
        new(CharacterMarriageSystem.SystemId, CharacterMarriageSystem.Version),
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
        CareerWorldSnapshot careers,
        CharacterResourceWorldSnapshot characterResources,
        CharacterEstateHoldingWorldSnapshot characterEstateHoldings,
        CharacterMarriageWorldSnapshot characterMarriages)
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
        CharacterResources = new CharacterResourceWorldState(
            characterResources,
            Characters,
            calendar);
        CharacterEstateHoldings = new CharacterEstateHoldingWorldState(
            characterEstateHoldings,
            Characters,
            calendar.Date);
        CharacterMarriages = new CharacterMarriageWorldState(
            characterMarriages,
            Characters,
            calendar);
    }

    public CampaignCalendar Calendar { get; private set; }

    public ulong RootSeed { get; }

    internal DeterministicRandomStreams Random { get; }

    public GeographicWorldState Geography { get; }

    public CharacterWorldState Characters { get; }

    public RelationshipWorldState Relationships { get; }

    public CharacterCareerWorldState Careers { get; }

    public CharacterResourceWorldState CharacterResources { get; }

    public CharacterEstateHoldingWorldState CharacterEstateHoldings { get; }

    public CharacterMarriageWorldState CharacterMarriages { get; }

    IGeographicWorldQuery IWorldQuery.Geography => Geography;

    IAuthoritativeCharacterWorldQuery IWorldQuery.Characters => Characters;

    IAuthoritativeRelationshipWorldQuery IWorldQuery.Relationships => Relationships;

    IAuthoritativeCareerWorldQuery IWorldQuery.Careers => Careers;

    IAuthoritativeCharacterResourceWorldQuery IWorldQuery.CharacterResources => CharacterResources;

    IAuthoritativeCharacterEstateHoldingWorldQuery IWorldQuery.CharacterEstateHoldings =>
        CharacterEstateHoldings;

    IAuthoritativeCharacterMarriageWorldQuery IWorldQuery.CharacterMarriages =>
        CharacterMarriages;

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
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty);

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
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty);

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
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers) => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            relationships,
            careers,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers,
        CharacterResourceWorldSnapshot characterResources) => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            relationships,
            careers,
            characterResources,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers,
        CharacterResourceWorldSnapshot characterResources,
        CharacterEstateHoldingWorldSnapshot characterEstateHoldings) => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            relationships,
            careers,
            characterResources,
            characterEstateHoldings,
            CharacterMarriageWorldSnapshot.Empty);

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot geography,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers,
        CharacterResourceWorldSnapshot characterResources,
        CharacterEstateHoldingWorldSnapshot characterEstateHoldings,
        CharacterMarriageWorldSnapshot characterMarriages)
    {
        WorldState world = new(
            new CampaignCalendar(startDate, 0),
            seed,
            null,
            geography,
            characters,
            relationships,
            careers,
            characterResources,
            characterEstateHoldings,
            characterMarriages);
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
            || snapshot.CharacterResources is null
            || snapshot.CharacterEstateHoldings is null
            || snapshot.CharacterMarriages is null
            || snapshot.Entities.Any(entity => entity is null)
            || snapshot.PendingCommands.Any(command => command is null))
        {
            throw new SaveCompatibilityException("Snapshot is missing required objects or collections.");
        }

        if (snapshot.ContractVersion != ContractVersions.WorldSnapshot)
        {
            throw new SaveCompatibilityException($"Unsupported world snapshot contract version {snapshot.ContractVersion}.");
        }

        (CharacterWorldSnapshot characters, CareerWorldSnapshot careers, CharacterResourceWorldSnapshot characterResources, CharacterEstateHoldingWorldSnapshot characterEstateHoldings, CharacterMarriageWorldSnapshot characterMarriages) = ValidateSystemVersions(
            snapshot.SystemVersions,
            snapshot.Characters,
            snapshot.Relationships,
            snapshot.Careers,
            snapshot.CharacterResources,
            snapshot.CharacterEstateHoldings,
            snapshot.CharacterMarriages);
        ValidatePendingCommands(snapshot.PendingCommands);

        WorldState world = new(
            snapshot.Calendar,
            snapshot.RootSeed,
            snapshot.RandomStreams,
            snapshot.Geography,
            characters,
            snapshot.Relationships,
            careers,
            characterResources,
            characterEstateHoldings,
            characterMarriages)
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
        CharacterResources = CharacterResources.CaptureSnapshot(),
        CharacterEstateHoldings = CharacterEstateHoldings.CaptureSnapshot(),
        CharacterMarriages = CharacterMarriages.CaptureSnapshot(),
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
            case CharacterResourceActionResolvedEventPayload resourceAction:
                ApplyCharacterResourceAction(campaignEvent, resourceAction);
                break;
            case CharacterMarriageActionResolvedEventPayload marriageAction:
                ApplyCharacterMarriageAction(campaignEvent, marriageAction);
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
        CharacterResources.UpdateCampaignCalendar(Calendar);
        CharacterMarriages.UpdateCampaignCalendar(Calendar);
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

    private void ApplyCharacterResourceAction(
        CampaignEvent campaignEvent,
        CharacterResourceActionResolvedEventPayload payload)
    {
        if (campaignEvent.CausalId is not EntityId commandId || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Character-resource action event requires a valid causal command ID.");
        }

        EntityId expectedEventId = CharacterResourceIds.DeriveActionEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                GetCharacterResourceActionAffectedIds(payload)))
        {
            throw new SimulationValidationException(
                "Character-resource action event identity or affected IDs do not match its exact deterministic outcome.");
        }

        CharacterResourceWorldUpdatePlan resourcePlan = CharacterResources.PrepareOutcome(
            payload,
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        CharacterResources.ApplyPrepared(resourcePlan);
    }

    private void ApplyCharacterMarriageAction(
        CampaignEvent campaignEvent,
        CharacterMarriageActionResolvedEventPayload payload)
    {
        if (campaignEvent.Phase != ResolutionPhase.Commands)
        {
            throw new SimulationValidationException(
                "Character-marriage action events must resolve in the Commands phase.");
        }

        if (campaignEvent.CausalId is not EntityId commandId || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Character-marriage action event requires a valid causal command ID.");
        }

        EntityId expectedEventId = CharacterMarriageIds.DeriveActionEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                GetCharacterMarriageActionAffectedIds(payload)))
        {
            throw new SimulationValidationException(
                "Character-marriage action event identity or affected IDs do not match its exact deterministic outcome.");
        }

        CharacterMarriageWorldUpdatePlan marriagePlan = CharacterMarriages.PrepareOutcome(
            payload,
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        CharacterMarriages.ApplyPrepared(marriagePlan);
    }

    internal static EntityId[] GetCharacterMarriageActionAffectedIds(
        CharacterMarriageActionResolvedEventPayload payload)
    {
        if (payload is null
            || payload.Action is null
            || payload.Outcome is null
            || !payload.ActingCharacterId.IsValid)
        {
            throw new SimulationValidationException(
                "Character-marriage action event contains null or invalid data.");
        }

        HashSet<EntityId> affected = [payload.ActingCharacterId];
        switch (payload.Action)
        {
            case ProposePoliticalMarriageAction action:
                affected.Add(action.RecipientCharacterId);
                affected.Add(action.PracticeId);
                if (action.ConcubinagePrincipalCharacterId is EntityId principal)
                {
                    affected.Add(principal);
                }

                break;
            case RespondToPoliticalMarriageProposalAction action:
                affected.Add(action.ProposalId);
                break;
            case WithdrawPoliticalMarriageProposalAction action:
                affected.Add(action.ProposalId);
                break;
            case CancelPoliticalBetrothalAction action:
                affected.Add(action.BetrothalId);
                break;
            case FulfillPoliticalBetrothalAction action:
                affected.Add(action.BetrothalId);
                break;
            case OfferRomanceRouteAction action:
                affected.Add(action.RecipientCharacterId);
                affected.Add(action.PracticeId);
                break;
            case RespondToRomanceInvitationAction action:
                affected.Add(action.InvitationId);
                break;
            case WithdrawRomanceInvitationAction action:
                affected.Add(action.InvitationId);
                break;
            case AdvanceRomanceRouteAction action:
                affected.Add(action.RouteId);
                break;
            case EndRomanceRouteAction action:
                affected.Add(action.RouteId);
                break;
            default:
                throw new SimulationValidationException(
                    $"Unregistered character-marriage action '{payload.Action.GetType().Name}'.");
        }

        switch (payload.Outcome)
        {
            case MarriageProposalCreatedOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                break;
            case MarriageProposalRefusedOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                break;
            case MarriageProposalWithdrawnOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                break;
            case MarriageProposalCancelledOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                break;
            case PoliticalBetrothalAcceptedOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                AddPoliticalBetrothal(affected, value.Betrothal);
                break;
            case DirectPoliticalUnionAcceptedOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                AddMarriageUnion(affected, value.Union);
                break;
            case PoliticalBetrothalCancelledOutcome value:
                AddPoliticalBetrothal(affected, value.Betrothal);
                break;
            case PoliticalBetrothalFulfilledOutcome value:
                AddPoliticalBetrothal(affected, value.Betrothal);
                AddMarriageProposal(affected, value.FulfillmentProposal);
                AddMarriageUnion(affected, value.Union);
                break;
            case RomanceInvitationCreatedOutcome value:
                AddRomanceInvitation(affected, value.Invitation);
                break;
            case RomanceInvitationRefusedOutcome value:
                AddRomanceInvitation(affected, value.Invitation);
                break;
            case RomanceInvitationWithdrawnOutcome value:
                AddRomanceInvitation(affected, value.Invitation);
                break;
            case RomanceInvitationCancelledOutcome value:
                AddRomanceInvitation(affected, value.Invitation);
                break;
            case RomanceRouteStartedOutcome value:
                affected.Add(value.InvitationId);
                AddRomanceRoute(affected, value.Route);
                break;
            case RomanceRouteAdvancedOutcome value:
                AddRomanceRoute(affected, value.Route);
                break;
            case RomanceRouteCompletedOutcome value:
                AddRomanceRoute(affected, value.Route);
                break;
            case RomanceRouteEndedOutcome value:
                AddRomanceRoute(affected, value.Route);
                break;
            default:
                throw new SimulationValidationException(
                    $"Unregistered character-marriage outcome '{payload.Outcome.GetType().Name}'.");
        }

        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Character-marriage action event contains an invalid affected ID.");
        }

        return affected.Order().ToArray();
    }

    private static void AddMarriageProposal(
        ISet<EntityId> affected,
        MarriageProposalState proposal)
    {
        affected.Add(proposal.ProposalId);
        affected.Add(proposal.ProposerCharacterId);
        affected.Add(proposal.RecipientCharacterId);
        affected.Add(proposal.PracticeId);
        if (proposal.ConcubinagePrincipalCharacterId is EntityId principal)
        {
            affected.Add(principal);
        }
    }

    private static void AddPoliticalBetrothal(
        ISet<EntityId> affected,
        PoliticalBetrothalState betrothal)
    {
        affected.Add(betrothal.BetrothalId);
        affected.Add(betrothal.FirstCharacterId);
        affected.Add(betrothal.SecondCharacterId);
        affected.Add(betrothal.PracticeId);
        affected.Add(betrothal.SourceProposalId);
        if (betrothal.ConcubinagePrincipalCharacterId is EntityId principal)
        {
            affected.Add(principal);
        }

        if (betrothal.FulfillmentUnionId is EntityId unionId)
        {
            affected.Add(unionId);
        }
    }

    private static void AddMarriageUnion(
        ISet<EntityId> affected,
        MarriageUnionState union)
    {
        affected.Add(union.UnionId);
        affected.Add(union.FirstCharacterId);
        affected.Add(union.SecondCharacterId);
        affected.Add(union.PracticeId);
        affected.Add(union.SourceProposalId);
        if (union.ConcubinagePrincipalCharacterId is EntityId principal)
        {
            affected.Add(principal);
        }
    }

    private static void AddRomanceInvitation(
        ISet<EntityId> affected,
        RomanceInvitationState invitation)
    {
        affected.Add(invitation.InvitationId);
        affected.Add(invitation.InitiatorCharacterId);
        affected.Add(invitation.RecipientCharacterId);
        affected.Add(invitation.PracticeId);
    }

    private static void AddRomanceRoute(
        ISet<EntityId> affected,
        RomanceRouteState route)
    {
        affected.Add(route.RouteId);
        affected.Add(route.FirstCharacterId);
        affected.Add(route.SecondCharacterId);
        affected.Add(route.PracticeId);
        if (route.SourceInvitationId is EntityId invitationId)
        {
            affected.Add(invitationId);
        }
    }

    internal static EntityId[] GetCharacterResourceActionAffectedIds(
        CharacterResourceActionResolvedEventPayload payload)
    {
        if (payload is null
            || payload.Action is not TransferWealthAction action
            || payload.Outcome is null)
        {
            throw new SimulationValidationException(
                "Character-resource action event contains null or unsupported data.");
        }

        HashSet<EntityId> affected =
        [
            payload.ActingCharacterId,
            action.RecipientCharacterId,
        ];
        switch (payload.Outcome)
        {
            case WealthTransferredOutcome value:
                affected.Add(value.Transfer.TransferId);
                affected.Add(value.Transfer.SourceCharacterId);
                affected.Add(value.Transfer.RecipientCharacterId);
                affected.Add(CharacterResourceIds.DeriveWealthAccountId(
                    value.Transfer.SourceCharacterId));
                affected.Add(CharacterResourceIds.DeriveWealthAccountId(
                    value.Transfer.RecipientCharacterId));
                affected.Add(value.OutgoingEntry.EntryId);
                affected.Add(value.IncomingEntry.EntryId);
                break;
            case WealthTransferCancelledOutcome value when Enum.IsDefined(value.Reason):
                break;
            default:
                throw new SimulationValidationException(
                    $"Unregistered character-resource action outcome '{payload.Outcome.GetType().Name}'.");
        }

        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Character-resource action event contains an invalid affected ID.");
        }

        return affected.Order().ToArray();
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

    private static (
        CharacterWorldSnapshot Characters,
        CareerWorldSnapshot Careers,
        CharacterResourceWorldSnapshot CharacterResources,
        CharacterEstateHoldingWorldSnapshot CharacterEstateHoldings,
        CharacterMarriageWorldSnapshot CharacterMarriages) ValidateSystemVersions(
        IReadOnlyList<SystemVersion> versions,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers,
        CharacterResourceWorldSnapshot characterResources,
        CharacterEstateHoldingWorldSnapshot characterEstateHoldings,
        CharacterMarriageWorldSnapshot characterMarriages)
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

        if (characterResources is null)
        {
            throw new SaveCompatibilityException("Snapshot character-resource state is missing.");
        }

        if (characterEstateHoldings is null)
        {
            throw new SaveCompatibilityException("Snapshot character-estate-holding state is missing.");
        }

        if (characterMarriages is null)
        {
            throw new SaveCompatibilityException("Snapshot character-marriage state is missing.");
        }

        string[] expectedCore = CurrentSystemVersions
            .Where(version => version.SystemId is not "simulation.characters"
                and not "simulation.relationships"
                and not "simulation.character_careers"
                and not CharacterResourceSystem.SystemId
                and not CharacterEstateHoldingSystem.SystemId
                and not CharacterMarriageSystem.SystemId)
            .Select(version => $"{version.SystemId}@{version.Version}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualCore = versions
            .Where(version => version.SystemId is not "simulation.characters"
                and not "simulation.relationships"
                and not "simulation.character_careers"
                and not CharacterResourceSystem.SystemId
                and not CharacterEstateHoldingSystem.SystemId
                and not CharacterMarriageSystem.SystemId)
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

        SystemVersion[] characterResourceVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(
                version.SystemId,
                CharacterResourceSystem.SystemId))
            .ToArray();
        CharacterResourceWorldSnapshot normalizedCharacterResources;
        if (characterResourceVersions.Length == 1
            && characterResourceVersions[0].Version == CharacterResourceSystem.Version)
        {
            if (characterResources.ContractVersion != CharacterResourceContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares '{CharacterResourceSystem.SystemId}@{CharacterResourceSystem.Version}' but contains character-resource contract {characterResources.ContractVersion}.");
            }

            normalizedCharacterResources = characterResources;
        }
        else if (characterResourceVersions.Length == 0)
        {
            if (!IsCompleteEmptyCharacterResourceSnapshot(characterResources))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current character-resource data must contain a complete, valid, empty character-resource snapshot.");
            }

            normalizedCharacterResources = CharacterResourceWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot character-resource system version is incompatible. Expected '{CharacterResourceSystem.SystemId}@{CharacterResourceSystem.Version}'.");
        }

        SystemVersion[] characterEstateHoldingVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(
                version.SystemId,
                CharacterEstateHoldingSystem.SystemId))
            .ToArray();
        CharacterEstateHoldingWorldSnapshot normalizedCharacterEstateHoldings;
        if (characterEstateHoldingVersions.Length == 1
            && characterEstateHoldingVersions[0].Version == CharacterEstateHoldingSystem.Version)
        {
            if (characterEstateHoldings.ContractVersion
                != CharacterEstateHoldingContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares '{CharacterEstateHoldingSystem.SystemId}@{CharacterEstateHoldingSystem.Version}' but contains character-estate-holding contract {characterEstateHoldings.ContractVersion}.");
            }

            normalizedCharacterEstateHoldings = characterEstateHoldings;
        }
        else if (characterEstateHoldingVersions.Length == 0)
        {
            if (!IsCompleteEmptyCharacterEstateHoldingSnapshot(characterEstateHoldings))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current character-estate-holding data must contain a complete, valid, empty character-estate-holding snapshot.");
            }

            normalizedCharacterEstateHoldings = CharacterEstateHoldingWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot character-estate-holding system version is incompatible. Expected '{CharacterEstateHoldingSystem.SystemId}@{CharacterEstateHoldingSystem.Version}'.");
        }

        SystemVersion[] characterMarriageVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(
                version.SystemId,
                CharacterMarriageSystem.SystemId))
            .ToArray();
        CharacterMarriageWorldSnapshot normalizedCharacterMarriages;
        if (characterMarriageVersions.Length == 1
            && characterMarriageVersions[0].Version == CharacterMarriageSystem.Version)
        {
            if (characterMarriages.ContractVersion != CharacterMarriageContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares '{CharacterMarriageSystem.SystemId}@{CharacterMarriageSystem.Version}' but contains character-marriage contract {characterMarriages.ContractVersion}.");
            }

            normalizedCharacterMarriages = characterMarriages;
        }
        else if (characterMarriageVersions.Length == 0)
        {
            if (!IsCompleteEmptyCharacterMarriageSnapshot(characterMarriages))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current character-marriage data must contain a complete, valid, empty character-marriage snapshot.");
            }

            normalizedCharacterMarriages = CharacterMarriageWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot character-marriage system version is incompatible. Expected '{CharacterMarriageSystem.SystemId}@{CharacterMarriageSystem.Version}'.");
        }

        return (
            normalizedCharacters,
            normalizedCareers,
            normalizedCharacterResources,
            normalizedCharacterEstateHoldings,
            normalizedCharacterMarriages);
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

    private static bool IsCompleteEmptyCharacterResourceSnapshot(
        CharacterResourceWorldSnapshot characterResources) =>
        characterResources.ContractVersion == CharacterResourceContractVersions.Snapshot
        && characterResources.Accounts is { Count: 0 }
        && characterResources.LedgerEntries is { Count: 0 }
        && characterResources.History is { Count: 0 };

    private static bool IsCompleteEmptyCharacterEstateHoldingSnapshot(
        CharacterEstateHoldingWorldSnapshot characterEstateHoldings) =>
        characterEstateHoldings.ContractVersion
            == CharacterEstateHoldingContractVersions.Snapshot
        && characterEstateHoldings.Holdings is { Count: 0 };

    private static bool IsCompleteEmptyCharacterMarriageSnapshot(
        CharacterMarriageWorldSnapshot characterMarriages) =>
        characterMarriages.ContractVersion == CharacterMarriageContractVersions.Snapshot
        && characterMarriages.Practices is { Count: 0 }
        && characterMarriages.Proposals is { Count: 0 }
        && characterMarriages.Betrothals is { Count: 0 }
        && characterMarriages.Unions is { Count: 0 }
        && characterMarriages.Invitations is { Count: 0 }
        && characterMarriages.RomanceRoutes is { Count: 0 }
        && characterMarriages.History is { Count: 0 };

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
                or CharacterActionCommandPayload
                or CharacterResourceActionCommandPayload
                or CharacterMarriageActionCommandPayload))
        {
            try
            {
                _ = command.Payload switch
                {
                    RelationshipActionCommandPayload => RelationshipWorldState.DeriveEventId(
                        command.IssuedDate,
                        command.CommandId),
                    CharacterActionCommandPayload => CareerIds.DeriveCharacterActionEventId(
                        command.IssuedDate,
                        command.CommandId),
                    CharacterResourceActionCommandPayload => CharacterResourceIds.DeriveActionEventId(
                        command.IssuedDate,
                        command.CommandId),
                    CharacterMarriageActionCommandPayload => CharacterMarriageIds.DeriveActionEventId(
                        command.IssuedDate,
                        command.CommandId),
                    _ => throw new SimulationValidationException(
                        "Snapshot character-domain command payload is unregistered."),
                };
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
