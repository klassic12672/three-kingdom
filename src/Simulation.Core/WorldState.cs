using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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

    IAuthoritativeCharacterGuardianshipWorldQuery CharacterGuardianships { get; }

    IAuthoritativeCharacterPregnancyWorldQuery CharacterPregnancies { get; }

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
        new(CharacterGuardianshipSystem.SystemId, CharacterGuardianshipSystem.Version),
        new(CharacterPregnancySystem.SystemId, CharacterPregnancySystem.Version),
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
        CharacterMarriageWorldSnapshot characterMarriages,
        CharacterGuardianshipWorldSnapshot characterGuardianships,
        CharacterPregnancyWorldSnapshot characterPregnancies)
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
        CharacterGuardianships = new CharacterGuardianshipWorldState(
            characterGuardianships,
            Characters,
            calendar);
        CharacterPregnancies = new CharacterPregnancyWorldState(
            characterPregnancies,
            Characters,
            CharacterMarriages,
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

    public CharacterGuardianshipWorldState CharacterGuardianships { get; }

    public CharacterPregnancyWorldState CharacterPregnancies { get; }

    IGeographicWorldQuery IWorldQuery.Geography => Geography;

    IAuthoritativeCharacterWorldQuery IWorldQuery.Characters => Characters;

    IAuthoritativeRelationshipWorldQuery IWorldQuery.Relationships => Relationships;

    IAuthoritativeCareerWorldQuery IWorldQuery.Careers => Careers;

    IAuthoritativeCharacterResourceWorldQuery IWorldQuery.CharacterResources => CharacterResources;

    IAuthoritativeCharacterEstateHoldingWorldQuery IWorldQuery.CharacterEstateHoldings =>
        CharacterEstateHoldings;

    IAuthoritativeCharacterMarriageWorldQuery IWorldQuery.CharacterMarriages =>
        CharacterMarriages;

    IAuthoritativeCharacterGuardianshipWorldQuery IWorldQuery.CharacterGuardianships =>
        CharacterGuardianships;

    IAuthoritativeCharacterPregnancyWorldQuery IWorldQuery.CharacterPregnancies =>
        CharacterPregnancies;

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
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty);

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
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty);

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
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty);

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
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty);

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
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty);

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
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty);

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
        => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            relationships,
            careers,
            characterResources,
            characterEstateHoldings,
            characterMarriages,
            CharacterGuardianshipWorldSnapshot.Empty);

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
        CharacterMarriageWorldSnapshot characterMarriages,
        CharacterGuardianshipWorldSnapshot characterGuardianships)
        => Create(
            startDate,
            seed,
            initialEntities,
            geography,
            characters,
            relationships,
            careers,
            characterResources,
            characterEstateHoldings,
            characterMarriages,
            characterGuardianships,
            CharacterPregnancyWorldSnapshot.Empty);

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
        CharacterMarriageWorldSnapshot characterMarriages,
        CharacterGuardianshipWorldSnapshot characterGuardianships,
        CharacterPregnancyWorldSnapshot characterPregnancies)
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
            characterMarriages,
            characterGuardianships,
            characterPregnancies);
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
            || snapshot.CharacterGuardianships is null
            || snapshot.CharacterPregnancies is null
            || snapshot.Entities.Any(entity => entity is null)
            || snapshot.PendingCommands.Any(command => command is null))
        {
            throw new SaveCompatibilityException("Snapshot is missing required objects or collections.");
        }

        if (snapshot.ContractVersion != ContractVersions.WorldSnapshot)
        {
            throw new SaveCompatibilityException($"Unsupported world snapshot contract version {snapshot.ContractVersion}.");
        }

        (CharacterWorldSnapshot characters, CareerWorldSnapshot careers, CharacterResourceWorldSnapshot characterResources, CharacterEstateHoldingWorldSnapshot characterEstateHoldings, CharacterMarriageWorldSnapshot characterMarriages, CharacterGuardianshipWorldSnapshot characterGuardianships, CharacterPregnancyWorldSnapshot characterPregnancies) = ValidateSystemVersions(
            snapshot.SystemVersions,
            snapshot.Characters,
            snapshot.Relationships,
            snapshot.Careers,
            snapshot.CharacterResources,
            snapshot.CharacterEstateHoldings,
            snapshot.CharacterMarriages,
            snapshot.CharacterGuardianships,
            snapshot.CharacterPregnancies);
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
            characterMarriages,
            characterGuardianships,
            characterPregnancies)
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
        CharacterGuardianships = CharacterGuardianships.CaptureSnapshot(),
        CharacterPregnancies = CharacterPregnancies.CaptureSnapshot(),
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
            case CharacterConditionActionResolvedEventPayload conditionAction:
                ApplyCharacterConditionAction(campaignEvent, conditionAction);
                break;
            case CharacterFamilyActionResolvedEventPayload familyAction:
                ApplyCharacterFamilyAction(campaignEvent, familyAction);
                break;
            case HouseholdDecisionResolvedEventPayload householdDecision:
                ApplyHouseholdDecision(campaignEvent, householdDecision);
                break;
            case CharacterCameOfAgeEventPayload comingOfAge:
                ApplyCharacterCameOfAge(campaignEvent, comingOfAge);
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
        CharacterGuardianships.UpdateCampaignCalendar(Calendar);
        CharacterPregnancies.UpdateCampaignCalendar(Calendar);
    }

    internal IReadOnlyList<CampaignEvent> PlanGeographicEvents(CampaignDate date) =>
        Geography.PlanDailyEvents(date, Calendar.TurnIndex);

    internal IReadOnlyList<CampaignCommand> PlanCharacterComingOfAgeCommands(
        CampaignDate date) => CharacterComingOfAgePlanner.PlanCommands(
            date,
            Characters,
            CharacterGuardianships);

    internal CharacterComingOfAgeResolutionPlan PrepareCharacterComingOfAge(
        EntityId actingActorId,
        CharacterComingOfAgeCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId) => CharacterComingOfAgePlanner.PrepareResolution(
            actingActorId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            Characters,
            CharacterGuardianships);

    internal CharacterConditionAggregatePlan PrepareCharacterConditionAction(
        EntityId actingActorId,
        CharacterConditionActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (actingActorId != CharacterConditionSystem.AuthoritativeActorId
            || payload?.Action is null
            || eventId != CharacterConditionIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Character-condition actions require the reserved authoritative simulation actor and exact event identity.");
        }

        CharacterConditionMutationPlan character = Characters.PrepareConditionAction(
            payload.Action,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        CharacterMarriageLifecycleUpdatePlan marriage = CharacterMarriages.PrepareLifecycleChange(
            character.CharacterPlan.Candidate,
            character.Change.CharacterId,
            CharacterMarriageLifecycleReason.ConditionChanged,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        RelationshipMemoryConsequenceSpecification? consequence = null;
        RelationshipWorldUpdatePlan? relationship = null;
        if (payload.Action is EnterCharacterCustodyAction custody)
        {
            consequence = Relationships.PlanHarmfulConsequence(
                eventId,
                CharacterConditionIds.DeriveRelationshipConsequenceId(eventId, 0),
                character.Change.CharacterId,
                custody.CustodianCharacterId,
                new EntityId("memory_meaning:condition/entered_custody"),
                resolutionDate,
                authoritativeTurnIndex);
        }

        CharacterConditionActionResolvedEventPayload resolved = new(
            actingActorId,
            payload.Action,
            new CharacterConditionChangedOutcome(
                character.Change,
                marriage.Changes),
            consequence);
        if (consequence is not null)
        {
            relationship = Relationships.PrepareCharacterConditionConsequence(
                resolved,
                resolutionDate,
                authoritativeTurnIndex,
                eventId);
        }

        return new CharacterConditionAggregatePlan(
            resolved,
            character.CharacterPlan,
            marriage.MarriagePlan,
            relationship);
    }

    internal CharacterFamilyAggregatePlan PrepareCharacterFamilyAction(
        EntityId actingActorId,
        CharacterFamilyActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (actingActorId != CharacterFamilySystem.AuthoritativeActorId
            || payload?.Action is null
            || eventId != CharacterFamilyIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Character-family actions require the reserved authoritative simulation actor and exact event identity.");
        }

        switch (payload.Action)
        {
            case EstablishLegalAdoptiveParentAction:
                {
                    CharacterFamilyMutationPlan character = Characters.PrepareFamilyAction(
                        payload.Action,
                        resolutionDate,
                        authoritativeTurnIndex,
                        commandId,
                        eventId);

                    // Every retained marriage record is revalidated against the candidate parent graph.
                    // Adoption is atomic: it is rejected rather than silently rewriting marriage history.
                    CampaignCalendar candidateCalendar = new(
                        resolutionDate.CompareTo(Calendar.Date) > 0 ? resolutionDate : Calendar.Date,
                        Math.Max(Calendar.TurnIndex, authoritativeTurnIndex));
                    _ = new CharacterMarriageWorldState(
                        CharacterMarriages.CaptureSnapshot(),
                        character.CharacterPlan.Candidate,
                        candidateCalendar);

                    CharacterFamilyActionResolvedEventPayload resolved = new(
                        actingActorId,
                        payload.Action,
                        new LegalAdoptiveParentEstablishedOutcome(character.Change));
                    return new CharacterFamilyAggregatePlan(
                        resolved,
                        character.CharacterPlan,
                        null,
                        null);
                }
            case EstablishPrimaryGuardianshipAction guardianshipAction:
                {
                    CharacterGuardianshipEstablishmentPlan guardianship =
                        CharacterGuardianships.PrepareEstablishment(
                            guardianshipAction,
                            resolutionDate,
                            authoritativeTurnIndex,
                            commandId,
                            eventId);
                    CharacterFamilyActionResolvedEventPayload resolved = new(
                        actingActorId,
                        payload.Action,
                        new PrimaryGuardianshipEstablishedOutcome(
                            guardianship.Guardianship));
                    return new CharacterFamilyAggregatePlan(
                        resolved,
                        null,
                        guardianship.GuardianshipPlan,
                        null);
                }
            case EndPrimaryGuardianshipAction terminationAction:
                {
                    CharacterGuardianshipTerminationPlan termination =
                        CharacterGuardianships.PrepareTermination(
                            terminationAction,
                            resolutionDate,
                            authoritativeTurnIndex,
                            commandId,
                            eventId);
                    CharacterFamilyActionResolvedEventPayload resolved = new(
                        actingActorId,
                        payload.Action,
                        new PrimaryGuardianshipEndedOutcome(
                            termination.EndedGuardianship));
                    return new CharacterFamilyAggregatePlan(
                        resolved,
                        null,
                        termination.GuardianshipPlan,
                        null);
                }
            case ReplacePrimaryGuardianshipAction replacementAction:
                {
                    CharacterGuardianshipReplacementPlan replacement =
                        CharacterGuardianships.PrepareReplacement(
                            replacementAction,
                            resolutionDate,
                            authoritativeTurnIndex,
                            commandId,
                            eventId);
                    CharacterFamilyActionResolvedEventPayload resolved = new(
                        actingActorId,
                        payload.Action,
                        new PrimaryGuardianshipReplacedOutcome(
                            replacement.EndedGuardianship,
                            replacement.ReplacementGuardianship));
                    return new CharacterFamilyAggregatePlan(
                        resolved,
                        null,
                        replacement.GuardianshipPlan,
                        null);
                }
            case RegisterActivePregnancyAction pregnancyAction:
                {
                    CharacterPregnancyRegistrationPlan registration =
                        CharacterPregnancies.PrepareRegistration(
                            actingActorId,
                            pregnancyAction,
                            resolutionDate,
                            authoritativeTurnIndex,
                            commandId,
                            eventId);
                    CharacterFamilyActionResolvedEventPayload resolved = new(
                        actingActorId,
                        payload.Action,
                        new ActivePregnancyRegisteredOutcome(
                            registration.Pregnancy));
                    return new CharacterFamilyAggregatePlan(
                        resolved,
                        null,
                        null,
                        registration.PregnancyPlan);
                }
            default:
                throw new SimulationValidationException(
                    $"Unsupported character-family action '{payload.Action.GetType().Name}'.");
        }
    }

    internal CharacterDeathPreviewAggregatePlan PrepareCharacterDeathPreview(
        EntityId characterId,
        CharacterConditionState expectedCurrent,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (eventId != CharacterConditionIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Character-death preview requires the exact reserved condition event identity.");
        }

        CharacterConditionMutationPlan character = Characters.PrepareDeathPreview(
            characterId,
            expectedCurrent,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        CharacterMarriageLifecycleUpdatePlan marriage = CharacterMarriages.PrepareLifecycleChange(
            character.CharacterPlan.Candidate,
            characterId,
            CharacterMarriageLifecycleReason.CharacterDied,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        return new CharacterDeathPreviewAggregatePlan(
            character.Change,
            marriage.Changes,
            character.CharacterPlan,
            marriage.MarriagePlan);
    }

    internal HouseholdDecisionAggregatePlan PrepareHouseholdDecision(
        EntityId actingCharacterId,
        HouseholdDecisionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload?.Action is null
            || eventId != HouseholdDecisionIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Household decision requires a registered action and exact event identity.");
        }

        HouseholdDecisionMutationPlan character = Characters.PrepareHouseholdDecision(
            actingCharacterId,
            payload.Action,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        EntityId meaningId = character.Transition.Kind switch
        {
            HouseholdDecisionKind.Expulsion =>
                new EntityId("memory_meaning:household/expulsion"),
            HouseholdDecisionKind.CaptiveIncorporation =>
                new EntityId("memory_meaning:household/captive_incorporation"),
            _ => throw new SimulationValidationException(
                "Household decision produced an unregistered transition kind."),
        };
        RelationshipMemoryConsequenceSpecification consequence =
            Relationships.PlanHarmfulConsequence(
                eventId,
                HouseholdDecisionIds.DeriveRelationshipConsequenceId(eventId, 0),
                character.Transition.MemberCharacterId,
                actingCharacterId,
                meaningId,
                resolutionDate,
                authoritativeTurnIndex);
        HouseholdDecisionResolvedEventPayload resolved = new(
            actingCharacterId,
            payload.Action,
            new HouseholdMembershipChangedOutcome(character.Transition),
            consequence);
        RelationshipWorldUpdatePlan relationship =
            Relationships.PrepareHouseholdDecisionConsequence(
                resolved,
                resolutionDate,
                authoritativeTurnIndex,
                eventId);
        return new HouseholdDecisionAggregatePlan(
            resolved,
            character.CharacterPlan,
            relationship);
    }

    internal CharacterMarriageAggregatePlan PrepareCharacterMarriageAction(
        EntityId actingCharacterId,
        CharacterMarriageActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload?.Action is null
            || eventId != CharacterMarriageIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Character-marriage action requires a registered action and exact event identity.");
        }

        CharacterMarriageActionResolvedEventPayload resolved = CharacterMarriages.PlanAction(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        RelationshipWorldUpdatePlan? relationship = null;
        if (payload.Action is ImposeCoercedUnionAction coercive)
        {
            RelationshipMemoryConsequenceSpecification consequence =
                Relationships.PlanHarmfulConsequence(
                    eventId,
                    CharacterMarriageIds.DeriveRelationshipConsequenceId(eventId, 0),
                    coercive.RecipientCharacterId,
                    actingCharacterId,
                    new EntityId("memory_meaning:marriage/coerced_union"),
                    resolutionDate,
                    authoritativeTurnIndex);
            resolved = resolved with { RelationshipMemoryConsequence = consequence };
            relationship = Relationships.PrepareCharacterMarriageConsequence(
                resolved,
                resolutionDate,
                authoritativeTurnIndex,
                eventId);
        }

        CharacterMarriageWorldUpdatePlan marriage = CharacterMarriages.PrepareOutcome(
            resolved,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        return new CharacterMarriageAggregatePlan(resolved, marriage, relationship);
    }

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
                GetCharacterMarriageActionAffectedIds(payload, campaignEvent.EventId)))
        {
            throw new SimulationValidationException(
                "Character-marriage action event identity or affected IDs do not match its exact deterministic outcome.");
        }

        CharacterMarriageAggregatePlan aggregate = PrepareCharacterMarriageAction(
            payload.ActingCharacterId,
            new CharacterMarriageActionCommandPayload(payload.Action),
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        if (!PayloadsEqual(aggregate.ResolvedPayload, payload))
        {
            throw new SimulationValidationException(
                "Character-marriage action event payload does not match its exact deterministic plan.");
        }

        CharacterMarriages.ApplyPrepared(aggregate.MarriagePlan);
        if (aggregate.RelationshipPlan is not null)
        {
            Relationships.ApplyPrepared(aggregate.RelationshipPlan);
        }
    }

    private void ApplyCharacterConditionAction(
        CampaignEvent campaignEvent,
        CharacterConditionActionResolvedEventPayload payload)
    {
        if (campaignEvent.Phase != ResolutionPhase.Commands
            || campaignEvent.CausalId is not EntityId commandId
            || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Character-condition action events require the Commands phase and a valid causal command ID.");
        }

        EntityId expectedEventId = CharacterConditionIds.DeriveActionEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                GetCharacterConditionActionAffectedIds(payload, campaignEvent.EventId)))
        {
            throw new SimulationValidationException(
                "Character-condition action event identity or affected IDs do not match its exact deterministic outcome.");
        }

        CharacterConditionAggregatePlan aggregate = PrepareCharacterConditionAction(
            payload.ActingActorId,
            new CharacterConditionActionCommandPayload(payload.Action),
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        if (!PayloadsEqual(aggregate.ResolvedPayload, payload))
        {
            throw new SimulationValidationException(
                "Character-condition action event payload does not match its exact deterministic plan.");
        }

        Characters.ApplyPrepared(aggregate.CharacterPlan);
        CharacterMarriages.ApplyPrepared(aggregate.MarriagePlan);
        if (aggregate.RelationshipPlan is not null)
        {
            Relationships.ApplyPrepared(aggregate.RelationshipPlan);
        }
    }

    private void ApplyCharacterFamilyAction(
        CampaignEvent campaignEvent,
        CharacterFamilyActionResolvedEventPayload payload)
    {
        if (campaignEvent.Phase != ResolutionPhase.Commands
            || campaignEvent.CausalId is not EntityId commandId
            || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Character-family action events require the Commands phase and a valid causal command ID.");
        }

        EntityId expectedEventId = CharacterFamilyIds.DeriveActionEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                GetCharacterFamilyActionAffectedIds(payload)))
        {
            throw new SimulationValidationException(
                "Character-family action event identity or affected IDs do not match its exact deterministic outcome.");
        }

        CharacterFamilyAggregatePlan aggregate = PrepareCharacterFamilyAction(
            payload.ActingActorId,
            new CharacterFamilyActionCommandPayload(payload.Action),
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        if (!PayloadsEqual(aggregate.ResolvedPayload, payload))
        {
            throw new SimulationValidationException(
                "Character-family action event payload does not match its exact deterministic plan.");
        }

        if (aggregate.CharacterPlan is not null)
        {
            Characters.ApplyPrepared(aggregate.CharacterPlan);
        }

        if (aggregate.GuardianshipPlan is not null)
        {
            CharacterGuardianships.ApplyPrepared(aggregate.GuardianshipPlan);
        }

        if (aggregate.PregnancyPlan is not null)
        {
            CharacterPregnancies.ApplyPrepared(aggregate.PregnancyPlan);
        }
    }

    private void ApplyHouseholdDecision(
        CampaignEvent campaignEvent,
        HouseholdDecisionResolvedEventPayload payload)
    {
        if (campaignEvent.Phase != ResolutionPhase.Commands
            || campaignEvent.CausalId is not EntityId commandId
            || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Household decision events require the Commands phase and a valid causal command ID.");
        }

        EntityId expectedEventId = HouseholdDecisionIds.DeriveActionEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                GetHouseholdDecisionAffectedIds(payload, campaignEvent.EventId)))
        {
            throw new SimulationValidationException(
                "Household decision event identity or affected IDs do not match its exact deterministic outcome.");
        }

        HouseholdDecisionAggregatePlan aggregate = PrepareHouseholdDecision(
            payload.ActingCharacterId,
            new HouseholdDecisionCommandPayload(payload.Action),
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        if (!PayloadsEqual(aggregate.ResolvedPayload, payload))
        {
            throw new SimulationValidationException(
                "Household decision event payload does not match its exact deterministic plan.");
        }

        Characters.ApplyPrepared(aggregate.CharacterPlan);
        Relationships.ApplyPrepared(aggregate.RelationshipPlan);
    }

    private void ApplyCharacterCameOfAge(
        CampaignEvent campaignEvent,
        CharacterCameOfAgeEventPayload payload)
    {
        if (campaignEvent.Phase != ResolutionPhase.Systems
            || campaignEvent.Priority != CharacterComingOfAgeSystem.Priority
            || campaignEvent.CausalId is not EntityId commandId
            || !commandId.IsValid)
        {
            throw new SimulationValidationException(
                "Coming-of-age events require the exact Systems-phase priority and a valid causal command ID.");
        }

        EntityId expectedCommandId = CharacterComingOfAgeIds.DeriveCommandId(
            campaignEvent.ResolutionDate,
            payload.CharacterId);
        EntityId expectedEventId = CharacterComingOfAgeIds.DeriveEventId(
            campaignEvent.ResolutionDate,
            commandId);
        if (commandId != expectedCommandId
            || campaignEvent.EventId != expectedEventId
            || campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(
                CharacterComingOfAgePlanner.GetAffectedIds(payload)))
        {
            throw new SimulationValidationException(
                "Coming-of-age event identities or affected IDs do not match its exact deterministic outcome.");
        }

        CharacterComingOfAgeCommandPayload commandPayload = new(
            payload.CharacterId,
            payload.EndedPrimaryGuardianship?.GuardianshipId);
        CharacterComingOfAgeResolutionPlan plan = PrepareCharacterComingOfAge(
            CharacterComingOfAgeSystem.AuthoritativeActorId,
            commandPayload,
            campaignEvent.ResolutionDate,
            Calendar.TurnIndex,
            commandId,
            campaignEvent.EventId);
        if (!PayloadsEqual(plan.Payload, payload))
        {
            throw new SimulationValidationException(
                "Coming-of-age event payload does not match its exact deterministic plan.");
        }

        if (plan.GuardianshipPlan is not null)
        {
            CharacterGuardianships.ApplyPrepared(plan.GuardianshipPlan);
        }
    }

    internal static EntityId[] GetCharacterMarriageActionAffectedIds(
        CharacterMarriageActionResolvedEventPayload payload,
        EntityId? eventId = null)
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
                if (action.ConcubinagePrincipalCharacterId is EntityId proposalPrincipal)
                {
                    affected.Add(proposalPrincipal);
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
            case ImposeCoercedUnionAction action:
                affected.Add(action.RecipientCharacterId);
                affected.Add(action.PracticeId);
                if (action.ConcubinagePrincipalCharacterId is EntityId coercivePrincipal)
                {
                    affected.Add(coercivePrincipal);
                }

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
            case CoercedPoliticalUnionImposedOutcome value:
                AddMarriageProposal(affected, value.Proposal);
                AddMarriageUnion(affected, value.Union);
                if (value.InvalidatedRomanceRoute is not null)
                {
                    AddRomanceRoute(affected, value.InvalidatedRomanceRoute);
                }

                break;
            default:
                throw new SimulationValidationException(
                    $"Unregistered character-marriage outcome '{payload.Outcome.GetType().Name}'.");
        }

        if (payload.RelationshipMemoryConsequence is not null)
        {
            if (eventId is not EntityId sourceEventId || !sourceEventId.IsValid)
            {
                throw new SimulationValidationException(
                    "Character-marriage relationship consequences require their source event ID.");
            }

            AddRelationshipConsequence(
                affected,
                payload.RelationshipMemoryConsequence,
                sourceEventId,
                0);
        }

        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Character-marriage action event contains an invalid affected ID.");
        }

        return affected.Order().ToArray();
    }

    internal static EntityId[] GetCharacterConditionActionAffectedIds(
        CharacterConditionActionResolvedEventPayload payload,
        EntityId? eventId = null)
    {
        if (payload?.Action is null
            || payload.Outcome is not CharacterConditionChangedOutcome outcome
            || outcome.Change is null
            || outcome.MarriageChanges is null)
        {
            throw new SimulationValidationException(
                "Character-condition action event contains null or unsupported data.");
        }

        HashSet<EntityId> affected =
        [
            payload.ActingActorId,
            outcome.Change.ChangeId,
            outcome.Change.CharacterId,
        ];
        AddConditionCustodian(affected, outcome.Change.PreviousCondition);
        AddConditionCustodian(affected, outcome.Change.CurrentCondition);
        AddMarriageLifecycleChanges(affected, outcome.MarriageChanges);
        if (payload.RelationshipMemoryConsequence is not null)
        {
            if (eventId is not EntityId sourceEventId || !sourceEventId.IsValid)
            {
                throw new SimulationValidationException(
                    "Character-condition relationship consequences require their source event ID.");
            }

            AddRelationshipConsequence(
                affected,
                payload.RelationshipMemoryConsequence,
                sourceEventId,
                0);
        }

        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Character-condition action event contains an invalid affected ID.");
        }

        return affected.Order().ToArray();
    }

    internal static EntityId[] GetCharacterFamilyActionAffectedIds(
        CharacterFamilyActionResolvedEventPayload payload)
    {
        if (payload?.Action is null || payload.Outcome is null)
        {
            throw new SimulationValidationException(
                "Character-family action event contains null or unsupported data.");
        }

        EntityId[] affected = (payload.Action, payload.Outcome) switch
        {
            (EstablishLegalAdoptiveParentAction action,
                LegalAdoptiveParentEstablishedOutcome outcome)
                when outcome.Change is not null
                    && outcome.Change.AdoptedCharacterId == action.AdoptedCharacterId =>
            [
                payload.ActingActorId,
                outcome.Change.ChangeId,
                action.AdoptiveParentCharacterId,
                action.AdoptedCharacterId,
            ],
            (EstablishPrimaryGuardianshipAction action,
                PrimaryGuardianshipEstablishedOutcome outcome)
                when outcome.Guardianship is not null
                    && outcome.Guardianship.GuardianCharacterId
                        == action.GuardianCharacterId
                    && outcome.Guardianship.WardCharacterId
                        == action.WardCharacterId =>
            [
                payload.ActingActorId,
                outcome.Guardianship.GuardianshipId,
                action.GuardianCharacterId,
                action.WardCharacterId,
            ],
            (EndPrimaryGuardianshipAction action,
                PrimaryGuardianshipEndedOutcome outcome)
                when outcome.EndedGuardianship is not null
                    && outcome.EndedGuardianship.GuardianshipId
                        == action.ExpectedCurrentPrimaryGuardianshipId
                    && outcome.EndedGuardianship.WardCharacterId
                        == action.WardCharacterId
                    && outcome.EndedGuardianship.EndReason == action.EndReason =>
            [
                payload.ActingActorId,
                outcome.EndedGuardianship.GuardianshipId,
                outcome.EndedGuardianship.GuardianCharacterId,
                action.WardCharacterId,
            ],
            (ReplacePrimaryGuardianshipAction action,
                PrimaryGuardianshipReplacedOutcome outcome)
                when outcome.EndedGuardianship is not null
                    && outcome.ReplacementGuardianship is not null
                    && outcome.EndedGuardianship.GuardianshipId
                        == action.ExpectedCurrentPrimaryGuardianshipId
                    && outcome.EndedGuardianship.WardCharacterId
                        == action.WardCharacterId
                    && outcome.EndedGuardianship.EndReason
                        == CharacterGuardianshipEndReason.Replaced
                    && outcome.ReplacementGuardianship.WardCharacterId
                        == action.WardCharacterId
                    && outcome.ReplacementGuardianship.GuardianCharacterId
                        == action.ReplacementGuardianCharacterId =>
            [
                payload.ActingActorId,
                outcome.EndedGuardianship.GuardianshipId,
                outcome.ReplacementGuardianship.GuardianshipId,
                outcome.EndedGuardianship.GuardianCharacterId,
                action.ReplacementGuardianCharacterId,
                action.WardCharacterId,
            ],
            (RegisterActivePregnancyAction action,
                ActivePregnancyRegisteredOutcome outcome)
                when outcome.Pregnancy is not null
                    && outcome.Pregnancy.GestationalParentCharacterId
                        == action.GestationalParentCharacterId
                    && outcome.Pregnancy.OtherBiologicalParentCharacterId
                        == action.OtherBiologicalParentCharacterId
                    && outcome.Pregnancy.SourceUnionId == action.SourceUnionId =>
            [
                payload.ActingActorId,
                outcome.Pregnancy.PregnancyId,
                action.GestationalParentCharacterId,
                action.OtherBiologicalParentCharacterId,
                action.SourceUnionId,
            ],
            _ => throw new SimulationValidationException(
                "Character-family action event contains mismatched action and outcome data."),
        };
        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Character-family action event contains an invalid affected ID.");
        }

        return affected.Distinct().Order().ToArray();
    }

    internal static EntityId[] GetHouseholdDecisionAffectedIds(
        HouseholdDecisionResolvedEventPayload payload,
        EntityId eventId)
    {
        if (payload?.Action is null
            || payload.Outcome is not HouseholdMembershipChangedOutcome outcome
            || outcome.Transition is null
            || payload.RelationshipMemoryConsequence is null
            || !eventId.IsValid)
        {
            throw new SimulationValidationException(
                "Household decision event contains null or unsupported data.");
        }

        HouseholdMembershipTransition transition = outcome.Transition;
        HashSet<EntityId> affected =
        [
            payload.ActingCharacterId,
            transition.TransitionId,
            transition.MemberCharacterId,
        ];
        if (transition.SourceHouseholdId is EntityId source)
        {
            affected.Add(source);
        }

        if (transition.DestinationHouseholdId is EntityId destination)
        {
            affected.Add(destination);
        }

        AddRelationshipConsequence(
            affected,
            payload.RelationshipMemoryConsequence,
            eventId,
            0);
        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Household decision event contains an invalid affected ID.");
        }

        return affected.Order().ToArray();
    }

    private static void AddConditionCustodian(
        ISet<EntityId> affected,
        CharacterConditionState condition)
    {
        if (condition?.CustodianId is EntityId custodian)
        {
            affected.Add(custodian);
        }
    }

    private static void AddMarriageLifecycleChanges(
        ISet<EntityId> affected,
        CharacterMarriageLifecycleChangeSet changes)
    {
        if (changes.ContractVersion
                != CharacterMarriageContractVersions.LifecycleChangeSet
            || changes.InvalidatedProposals is null
            || changes.InvalidatedBetrothals is null
            || changes.EndedUnions is null
            || changes.CancelledInvitations is null
            || changes.InvalidatedRomanceRoutes is null)
        {
            throw new SimulationValidationException(
                "Character-marriage lifecycle changes are malformed.");
        }

        foreach (MarriageProposalState proposal in changes.InvalidatedProposals)
        {
            AddMarriageProposal(affected, proposal);
        }

        foreach (PoliticalBetrothalState betrothal in changes.InvalidatedBetrothals)
        {
            AddPoliticalBetrothal(affected, betrothal);
        }

        foreach (MarriageUnionState union in changes.EndedUnions)
        {
            AddMarriageUnion(affected, union);
        }

        foreach (RomanceInvitationState invitation in changes.CancelledInvitations)
        {
            AddRomanceInvitation(affected, invitation);
        }

        foreach (RomanceRouteState route in changes.InvalidatedRomanceRoutes)
        {
            AddRomanceRoute(affected, route);
        }
    }

    private static void AddRelationshipConsequence(
        ISet<EntityId> affected,
        RelationshipMemoryConsequenceSpecification consequence,
        EntityId eventId,
        int zeroBasedIndex)
    {
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
            zeroBasedIndex));
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

    private static bool PayloadsEqual<T>(T expected, T actual) =>
        StringComparer.Ordinal.Equals(
            JsonSerializer.Serialize(expected, SimulationJson.CreateOptions()),
            JsonSerializer.Serialize(actual, SimulationJson.CreateOptions()));

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
        CharacterMarriageWorldSnapshot CharacterMarriages,
        CharacterGuardianshipWorldSnapshot CharacterGuardianships,
        CharacterPregnancyWorldSnapshot CharacterPregnancies) ValidateSystemVersions(
        IReadOnlyList<SystemVersion> versions,
        CharacterWorldSnapshot characters,
        RelationshipWorldSnapshot relationships,
        CareerWorldSnapshot careers,
        CharacterResourceWorldSnapshot characterResources,
        CharacterEstateHoldingWorldSnapshot characterEstateHoldings,
        CharacterMarriageWorldSnapshot characterMarriages,
        CharacterGuardianshipWorldSnapshot characterGuardianships,
        CharacterPregnancyWorldSnapshot characterPregnancies)
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

        if (characterGuardianships is null)
        {
            throw new SaveCompatibilityException("Snapshot character-guardianship state is missing.");
        }

        if (characterPregnancies is null)
        {
            throw new SaveCompatibilityException("Snapshot character-pregnancy state is missing.");
        }

        string[] expectedCore = CurrentSystemVersions
            .Where(version => version.SystemId is not "simulation.characters"
                and not "simulation.relationships"
                and not "simulation.character_careers"
                and not CharacterResourceSystem.SystemId
                and not CharacterEstateHoldingSystem.SystemId
                and not CharacterMarriageSystem.SystemId
                and not CharacterGuardianshipSystem.SystemId
                and not CharacterPregnancySystem.SystemId)
            .Select(version => $"{version.SystemId}@{version.Version}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualCore = versions
            .Where(version => version.SystemId is not "simulation.characters"
                and not "simulation.relationships"
                and not "simulation.character_careers"
                and not CharacterResourceSystem.SystemId
                and not CharacterEstateHoldingSystem.SystemId
                and not CharacterMarriageSystem.SystemId
                and not CharacterGuardianshipSystem.SystemId
                and not CharacterPregnancySystem.SystemId)
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

        SystemVersion[] characterGuardianshipVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(
                version.SystemId,
                CharacterGuardianshipSystem.SystemId))
            .ToArray();
        CharacterGuardianshipWorldSnapshot normalizedCharacterGuardianships;
        if (characterGuardianshipVersions.Length == 1
            && characterGuardianshipVersions[0].Version == CharacterGuardianshipSystem.Version)
        {
            if (characterGuardianships.ContractVersion
                != CharacterGuardianshipContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares '{CharacterGuardianshipSystem.SystemId}@{CharacterGuardianshipSystem.Version}' but contains character-guardianship contract {characterGuardianships.ContractVersion}.");
            }

            normalizedCharacterGuardianships = characterGuardianships;
        }
        else if (characterGuardianshipVersions.Length == 0)
        {
            if (!IsCompleteEmptyCharacterGuardianshipSnapshot(characterGuardianships))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current character-guardianship data must contain a complete, valid, empty character-guardianship snapshot.");
            }

            normalizedCharacterGuardianships = CharacterGuardianshipWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot character-guardianship system version is incompatible. Expected '{CharacterGuardianshipSystem.SystemId}@{CharacterGuardianshipSystem.Version}'.");
        }

        SystemVersion[] characterPregnancyVersions = versions
            .Where(version => StringComparer.Ordinal.Equals(
                version.SystemId,
                CharacterPregnancySystem.SystemId))
            .ToArray();
        CharacterPregnancyWorldSnapshot normalizedCharacterPregnancies;
        if (characterPregnancyVersions.Length == 1
            && characterPregnancyVersions[0].Version == CharacterPregnancySystem.Version)
        {
            if (characterPregnancies.ContractVersion
                != CharacterPregnancyContractVersions.Snapshot)
            {
                throw new SaveCompatibilityException(
                    $"Snapshot declares '{CharacterPregnancySystem.SystemId}@{CharacterPregnancySystem.Version}' but contains character-pregnancy contract {characterPregnancies.ContractVersion}.");
            }

            normalizedCharacterPregnancies = characterPregnancies;
        }
        else if (characterPregnancyVersions.Length == 0)
        {
            if (!IsCompleteEmptyCharacterPregnancySnapshot(characterPregnancies))
            {
                throw new SaveCompatibilityException(
                    "A legacy snapshot without current character-pregnancy data must contain a complete, valid, empty character-pregnancy snapshot.");
            }

            normalizedCharacterPregnancies = CharacterPregnancyWorldSnapshot.Empty;
        }
        else
        {
            throw new SaveCompatibilityException(
                $"Snapshot character-pregnancy system version is incompatible. Expected '{CharacterPregnancySystem.SystemId}@{CharacterPregnancySystem.Version}'.");
        }

        return (
            normalizedCharacters,
            normalizedCareers,
            normalizedCharacterResources,
            normalizedCharacterEstateHoldings,
            normalizedCharacterMarriages,
            normalizedCharacterGuardianships,
            normalizedCharacterPregnancies);
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

    private static bool IsCompleteEmptyCharacterGuardianshipSnapshot(
        CharacterGuardianshipWorldSnapshot characterGuardianships) =>
        characterGuardianships.ContractVersion
            == CharacterGuardianshipContractVersions.Snapshot
        && characterGuardianships.Guardianships is { Count: 0 };

    private static bool IsCompleteEmptyCharacterPregnancySnapshot(
        CharacterPregnancyWorldSnapshot characterPregnancies) =>
        characterPregnancies.ContractVersion
            == CharacterPregnancyContractVersions.Snapshot
        && characterPregnancies.ActivePregnancies is { Count: 0 };

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

        if (commands.Any(command => command.Payload is CharacterComingOfAgeCommandPayload))
        {
            throw new SimulationValidationException(
                "Snapshot cannot persist internally generated coming-of-age commands as pending work.");
        }

        foreach (CampaignCommand command in commands.Where(
            command => command.Payload is RelationshipActionCommandPayload
                or CharacterActionCommandPayload
                or CharacterResourceActionCommandPayload
                or CharacterMarriageActionCommandPayload
                or CharacterConditionActionCommandPayload
                or CharacterFamilyActionCommandPayload
                or HouseholdDecisionCommandPayload))
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
                    CharacterConditionActionCommandPayload => CharacterConditionIds.DeriveActionEventId(
                        command.IssuedDate,
                        command.CommandId),
                    CharacterFamilyActionCommandPayload => CharacterFamilyIds.DeriveActionEventId(
                        command.IssuedDate,
                        command.CommandId),
                    HouseholdDecisionCommandPayload => HouseholdDecisionIds.DeriveActionEventId(
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

internal sealed record CharacterConditionAggregatePlan(
    CharacterConditionActionResolvedEventPayload ResolvedPayload,
    CharacterWorldUpdatePlan CharacterPlan,
    CharacterMarriageWorldUpdatePlan MarriagePlan,
    RelationshipWorldUpdatePlan? RelationshipPlan);

internal sealed record CharacterFamilyAggregatePlan(
    CharacterFamilyActionResolvedEventPayload ResolvedPayload,
    CharacterWorldUpdatePlan? CharacterPlan,
    CharacterGuardianshipWorldUpdatePlan? GuardianshipPlan,
    CharacterPregnancyWorldUpdatePlan? PregnancyPlan);

internal sealed record CharacterDeathPreviewAggregatePlan(
    CharacterConditionChange Change,
    CharacterMarriageLifecycleChangeSet MarriageChanges,
    CharacterWorldUpdatePlan CharacterPlan,
    CharacterMarriageWorldUpdatePlan MarriagePlan);

internal sealed record HouseholdDecisionAggregatePlan(
    HouseholdDecisionResolvedEventPayload ResolvedPayload,
    CharacterWorldUpdatePlan CharacterPlan,
    RelationshipWorldUpdatePlan RelationshipPlan);

internal sealed record CharacterMarriageAggregatePlan(
    CharacterMarriageActionResolvedEventPayload ResolvedPayload,
    CharacterMarriageWorldUpdatePlan MarriagePlan,
    RelationshipWorldUpdatePlan? RelationshipPlan);

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
