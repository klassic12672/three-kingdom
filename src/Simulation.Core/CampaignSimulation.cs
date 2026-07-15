namespace Simulation.Core;

public sealed class CampaignSimulation
{
    private const int DiagnosticLimit = 256;
    private static readonly ResolutionPhase[] OrderedPhases = Enum.GetValues<ResolutionPhase>()
        .Order()
        .ToArray();

    private readonly Queue<CampaignCommand> recentCommands = new(DiagnosticLimit);
    private readonly Queue<CampaignEvent> recentEvents = new(DiagnosticLimit);

    public CampaignSimulation(WorldState world)
    {
        World = world;
    }

    public WorldState World { get; }

    public IReadOnlyList<CampaignCommand> RecentCommands => recentCommands.ToArray();

    public IReadOnlyList<CampaignEvent> RecentEvents => recentEvents.ToArray();

    public CommandValidationResult Submit(CampaignCommand command)
    {
        CommandValidationResult validation = ValidateForSubmission(command);
        CampaignCommand validated = command with { Validation = validation };
        AddBounded(recentCommands, validated);
        if (validation.IsValid)
        {
            World.Enqueue(validated);
        }

        return validation;
    }

    public IReadOnlyList<CampaignEvent> ResolveTurn() => ResolveTurn([]);

    public IReadOnlyList<CampaignEvent> ResolveTurn(IEnumerable<CampaignEvent> backgroundEvents)
    {
        CampaignEvent[] proposed = backgroundEvents.ToArray();
        CampaignDate[] turnDays = World.Calendar.CurrentTurnDays().ToArray();
        if (proposed.Any(campaignEvent => campaignEvent.Phase != ResolutionPhase.BackgroundCommit
            || !turnDays.Contains(campaignEvent.ResolutionDate)))
        {
            throw new SimulationValidationException(
                "Background events must target the BackgroundCommit phase of the current turn.");
        }

        List<CampaignEvent> resolved = [];
        foreach (CampaignDate date in turnDays)
        {
            foreach (ResolutionPhase phase in OrderedPhases)
            {
                IEnumerable<PendingResolution> commandResolutions = World.DequeueCommandsFor(date, phase)
                    .Select(command => new PendingResolution(
                        command.Priority,
                        GetEventId(command),
                        command,
                        null));
                IEnumerable<PendingResolution> backgroundResolutions = phase == ResolutionPhase.BackgroundCommit
                    ? proposed.Where(campaignEvent => campaignEvent.ResolutionDate == date)
                        .Select(campaignEvent => new PendingResolution(
                            campaignEvent.Priority,
                            campaignEvent.EventId,
                            null,
                            campaignEvent))
                    : [];
                IEnumerable<PendingResolution> geographicResolutions = phase == ResolutionPhase.Systems
                    ? World.PlanGeographicEvents(date)
                        .Select(campaignEvent => new PendingResolution(
                            campaignEvent.Priority,
                            campaignEvent.EventId,
                            null,
                            campaignEvent))
                    : [];
                PendingResolution[] phaseResolutions = commandResolutions
                    .Concat(backgroundResolutions)
                    .Concat(geographicResolutions)
                    .OrderBy(item => item.Priority)
                    .ThenBy(item => item.EventId)
                    .ToArray();
                foreach (PendingResolution pending in phaseResolutions)
                {
                    CampaignEvent campaignEvent = pending.Command is not null
                        ? Resolve(pending.Command, date, phase)
                        : pending.BackgroundEvent!;
                    World.Apply(campaignEvent);
                    AddBounded(recentEvents, campaignEvent);
                    resolved.Add(campaignEvent);
                }
            }
        }

        World.AdvanceCalendar();
        return resolved;
    }

    private CommandValidationResult ValidateForSubmission(CampaignCommand command)
    {
        List<ValidationIssue> issues = [];
        if (!command.CommandId.IsValid)
        {
            issues.Add(new("invalid_command_id", "Command ID is invalid."));
        }

        if (!command.IssuingActor.IsValid)
        {
            issues.Add(new("invalid_actor_id", "Issuing actor ID is invalid."));
        }

        if (World.ContainsCommand(command.CommandId))
        {
            issues.Add(new("duplicate_command_id", $"Command ID '{command.CommandId}' is already pending."));
        }

        if (command.ContractVersion != ContractVersions.CampaignCommand)
        {
            issues.Add(new("unsupported_contract", $"Command contract version {command.ContractVersion} is unsupported."));
        }

        if (!command.IssuedDate.IsValid)
        {
            issues.Add(new("invalid_date", "Command date is invalid."));
        }
        else if (command.IssuedDate.CompareTo(World.Calendar.Date) < 0)
        {
            issues.Add(new("date_in_past", "Command date precedes the authoritative calendar."));
        }

        if (!Enum.IsDefined(command.Phase))
        {
            issues.Add(new("unregistered_phase", $"Resolution phase '{command.Phase}' is not registered."));
        }

        if (!IsActorAvailable(command))
        {
            issues.Add(new("unknown_actor", $"Issuing actor '{command.IssuingActor}' does not exist."));
        }

        switch (command.Payload)
        {
            case AdjustResourcesCommandPayload adjustment:
                ValidateAdjustment(adjustment, issues);
                break;
            case ChangeSimulationTierCommandPayload transition:
                if (!World.TryGetEntity(transition.Target, out _))
                {
                    issues.Add(new("unknown_target", $"Tier target '{transition.Target}' does not exist."));
                }

                break;
            case MovementOrderPayload movement:
                if (movement.Departure != command.IssuedDate || command.Phase != ResolutionPhase.Commands)
                {
                    issues.Add(new("movement_departure", "Movement departure must equal the command date in the Commands phase."));
                }

                AddIssues(World.Geography.ValidateMovementOrder(movement), issues);
                break;
            case RetreatOrderPayload retreat:
                AddIssues(World.Geography.ValidateRetreatOrder(retreat), issues);
                break;
            case SupplyOrderPayload supply:
                AddIssues(World.Geography.ValidateSupplyOrder(supply), issues);
                break;
            case ChangeControlCommandPayload control:
                AddIssues(World.Geography.ValidateControlChange(control), issues);
                break;
            case RelationshipActionCommandPayload relationship:
                AddIssues(
                    World.Relationships.ValidateAction(
                        command.IssuingActor,
                        relationship,
                        command.IssuedDate,
                        World.Calendar.TurnIndex),
                    issues);
                if (command.CommandId.IsValid && command.IssuedDate.IsValid)
                {
                    try
                    {
                        _ = RelationshipWorldState.DeriveEventId(command.IssuedDate, command.CommandId);
                    }
                    catch (SimulationValidationException exception)
                    {
                        issues.Add(new("invalid_event_id", exception.Message));
                    }
                }

                break;
            case CharacterActionCommandPayload characterAction:
                CommandValidationResult careerValidation = World.Careers.ValidateAction(
                    command.IssuingActor,
                    characterAction,
                    command.IssuedDate,
                    World.Calendar.TurnIndex);
                AddIssues(careerValidation, issues);
                if (careerValidation.IsValid
                    && command.CommandId.IsValid
                    && command.IssuedDate.IsValid)
                {
                    try
                    {
                        EntityId eventId = CareerIds.DeriveCharacterActionEventId(
                            command.IssuedDate,
                            command.CommandId);
                        CharacterActionResolvedEventPayload planned = World.Careers.PlanAction(
                            command.IssuingActor,
                            characterAction,
                            command.IssuedDate,
                            World.Calendar.TurnIndex,
                            command.CommandId,
                            eventId,
                            characterAction.RelationshipMemoryConsequences);
                        _ = World.Relationships.PrepareCharacterActionConsequences(
                            planned,
                            command.IssuedDate,
                            World.Calendar.TurnIndex,
                            eventId);
                    }
                    catch (Exception exception) when (exception is SimulationValidationException
                        or ArgumentException
                        or OverflowException)
                    {
                        issues.Add(new("invalid_character_action", exception.Message));
                    }
                }

                break;
            default:
                issues.Add(new("unregistered_payload", $"Command payload '{command.Payload?.GetType().Name ?? "null"}' is not registered."));
                break;
        }

        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    private void ValidateAdjustment(AdjustResourcesCommandPayload adjustment, ICollection<ValidationIssue> issues)
    {
        if (!World.TryGetEntity(adjustment.Target, out SyntheticEntitySnapshot? entity))
        {
            issues.Add(new("unknown_target", $"Resource target '{adjustment.Target}' does not exist."));
            return;
        }

        try
        {
            if (checked(entity.People + adjustment.PeopleDelta) < 0
                || checked(entity.Food + adjustment.FoodDelta) < 0
                || checked(entity.Gold + adjustment.GoldDelta) < 0)
            {
                issues.Add(new("conservation_underflow", "Resource adjustment would produce a negative value."));
            }
        }
        catch (OverflowException)
        {
            issues.Add(new("numeric_overflow", "Resource adjustment exceeds the supported integer range."));
        }
    }

    private CampaignEvent Resolve(CampaignCommand command, CampaignDate date, ResolutionPhase phase)
    {
        ICampaignEventPayload payload;
        EntityId[] affected;

        if (!IsActorAvailable(command))
        {
            payload = new CommandCancelledEventPayload("actor_unavailable", $"Actor '{command.IssuingActor}' is unavailable at resolution.");
            affected = [];
        }
        else
        {
            switch (command.Payload)
            {
                case AdjustResourcesCommandPayload adjustment:
                    List<ValidationIssue> adjustmentIssues = [];
                    ValidateAdjustment(adjustment, adjustmentIssues);
                    if (adjustmentIssues.Count > 0)
                    {
                        payload = new CommandCancelledEventPayload(
                            "command_invalidated",
                            string.Join("; ", adjustmentIssues.Select(issue => issue.Message)));
                        affected = [];
                        break;
                    }

                    payload = new ResourcesAdjustedEventPayload(
                        adjustment.Target,
                        adjustment.PeopleDelta,
                        adjustment.FoodDelta,
                        adjustment.GoldDelta);
                    affected = [adjustment.Target];
                    break;
                case ChangeSimulationTierCommandPayload transition:
                    if (!World.TryGetEntity(transition.Target, out SyntheticEntitySnapshot? entity))
                    {
                        payload = new CommandCancelledEventPayload("target_unavailable", $"Target '{transition.Target}' is unavailable at resolution.");
                        affected = [];
                        break;
                    }

                    ConservationLedger ledger = ConservationLedger.From(entity);
                    payload = new SimulationTierChangedEventPayload(
                        transition.Target,
                        entity.Tier,
                        transition.Tier,
                        ledger,
                        ledger);
                    affected = [transition.Target];
                    break;
                case MovementOrderPayload movement:
                    payload = World.Geography.PlanMovementOrder(movement);
                    affected = [movement.ArmyId];
                    break;
                case RetreatOrderPayload retreat:
                    payload = World.Geography.PlanRetreat(retreat);
                    affected = [retreat.ArmyId, retreat.PreferredStopId];
                    break;
                case SupplyOrderPayload supply:
                    payload = World.Geography.PlanSupplyTransfer(supply, date);
                    affected = supply.RouteIds
                        .Append(supply.SourceStopId)
                        .Append(supply.DestinationStopId)
                        .Distinct()
                        .Order()
                        .ToArray();
                    break;
                case ChangeControlCommandPayload control:
                    payload = World.Geography.PlanControlChange(control);
                    affected = [control.StopId];
                    break;
                case RelationshipActionCommandPayload relationship:
                    CommandValidationResult relationshipValidation = World.Relationships.ValidateAction(
                        command.IssuingActor,
                        relationship,
                        date,
                        World.Calendar.TurnIndex);
                    if (!relationshipValidation.IsValid)
                    {
                        payload = new CommandCancelledEventPayload(
                            "command_invalidated",
                            string.Join("; ", relationshipValidation.Issues.Select(issue => issue.Message)));
                        affected = [];
                        break;
                    }

                    EntityId eventId = GetEventId(command);
                    RelationshipActionResolvedEventPayload resolvedRelationship = World.Relationships.PlanAction(
                        command.IssuingActor,
                        relationship,
                        date,
                        World.Calendar.TurnIndex,
                        command.CommandId,
                        eventId);
                    payload = resolvedRelationship;
                    affected =
                    [
                        resolvedRelationship.SubjectCharacterId,
                        resolvedRelationship.TargetCharacterId,
                        resolvedRelationship.RelationshipId,
                        resolvedRelationship.Memory.MemoryId,
                    ];
                    break;
                case CharacterActionCommandPayload characterAction:
                    try
                    {
                        EntityId characterEventId = GetEventId(command);
                        CharacterActionResolvedEventPayload resolvedCharacterAction =
                            World.Careers.PlanAction(
                                command.IssuingActor,
                                characterAction,
                                date,
                                World.Calendar.TurnIndex,
                                command.CommandId,
                                characterEventId,
                                characterAction.RelationshipMemoryConsequences);
                        _ = World.Relationships.PrepareCharacterActionConsequences(
                            resolvedCharacterAction,
                            date,
                            World.Calendar.TurnIndex,
                            characterEventId);
                        payload = resolvedCharacterAction;
                        affected = WorldState.GetCharacterActionAffectedIds(
                            resolvedCharacterAction,
                            characterEventId);
                    }
                    catch (Exception exception) when (exception is SimulationValidationException
                        or ArgumentException
                        or OverflowException)
                    {
                        payload = new CommandCancelledEventPayload(
                            "command_invalidated",
                            exception.Message);
                        affected = [];
                    }

                    break;
                default:
                    throw new SimulationValidationException($"Unregistered command payload '{command.Payload.GetType().Name}'.");
            }
        }

        return new CampaignEvent(
            ContractVersions.CampaignEvent,
            GetEventId(command),
            command.CommandId,
            date,
            phase,
            command.Priority,
            affected.Order().ToArray(),
            payload);
    }

    private static void AddBounded<T>(Queue<T> queue, T value)
    {
        if (queue.Count == DiagnosticLimit)
        {
            queue.Dequeue();
        }

        queue.Enqueue(value);
    }

    private static void AddIssues(CommandValidationResult validation, ICollection<ValidationIssue> issues)
    {
        foreach (ValidationIssue issue in validation.Issues)
        {
            issues.Add(issue);
        }
    }

    private bool IsActorAvailable(CampaignCommand command) => command.Payload is RelationshipActionCommandPayload
        or CharacterActionCommandPayload
        ? World.Characters.TryGetCharacterProfile(command.IssuingActor, out _)
        : World.TryGetEntity(command.IssuingActor, out _);

    private static EntityId GetEventId(CampaignCommand command)
    {
        if (command.Payload is RelationshipActionCommandPayload)
        {
            return RelationshipWorldState.DeriveEventId(command.IssuedDate, command.CommandId);
        }

        if (command.Payload is CharacterActionCommandPayload)
        {
            return CareerIds.DeriveCharacterActionEventId(command.IssuedDate, command.CommandId);
        }

        string eventPath = command.CommandId.Value.Replace(':', '/');
        return new EntityId($"event:{eventPath}");
    }

    private sealed record PendingResolution(
        int Priority,
        EntityId EventId,
        CampaignCommand? Command,
        CampaignEvent? BackgroundEvent);
}

public sealed record BackgroundCalculation(EntityId WorkId, Func<CampaignEvent> Calculate);

public static class DeterministicCalculationScheduler
{
    public static async Task<IReadOnlyList<CampaignEvent>> CalculateAsync(
        IEnumerable<BackgroundCalculation> calculations,
        CancellationToken cancellationToken = default)
    {
        BackgroundCalculation[] ordered = calculations.OrderBy(item => item.WorkId).ToArray();
        if (ordered.Select(item => item.WorkId).Distinct().Count() != ordered.Length)
        {
            throw new SimulationValidationException("Background work IDs must be unique.");
        }

        Task<CampaignEvent>[] tasks = ordered
            .Select(item => Task.Run(item.Calculate, cancellationToken))
            .ToArray();
        CampaignEvent[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.OrderBy(item => item.ResolutionDate)
            .ThenBy(item => item.Phase)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.EventId)
            .ToArray();
    }
}
