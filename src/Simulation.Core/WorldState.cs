using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public interface IWorldQuery
{
    CampaignCalendar Calendar { get; }

    IReadOnlyList<SyntheticEntitySnapshot> Entities { get; }

    IGeographicWorldQuery Geography { get; }

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
        GeographicWorldSnapshot geography)
    {
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException("Campaign calendar is invalid.");
        }

        Calendar = calendar;
        RootSeed = rootSeed;
        Random = new DeterministicRandomStreams(rootSeed, streams);
        Geography = new GeographicWorldState(geography);
    }

    public CampaignCalendar Calendar { get; private set; }

    public ulong RootSeed { get; }

    internal DeterministicRandomStreams Random { get; }

    public GeographicWorldState Geography { get; }

    IGeographicWorldQuery IWorldQuery.Geography => Geography;

    public IReadOnlyList<SyntheticEntitySnapshot> Entities => entities.Values.Select(CloneEntity).ToArray();

    public static WorldState Create(
        CampaignDate startDate,
        ulong seed,
        IEnumerable<SyntheticEntitySnapshot> initialEntities,
        GeographicWorldSnapshot? geography = null)
    {
        WorldState world = new(
            new CampaignCalendar(startDate, 0),
            seed,
            null,
            geography ?? GeographicWorldSnapshot.Empty);
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
        if (snapshot.ContractVersion != ContractVersions.WorldSnapshot)
        {
            throw new SaveCompatibilityException($"Unsupported world snapshot contract version {snapshot.ContractVersion}.");
        }

        ValidateSystemVersions(snapshot.SystemVersions);
        ValidatePendingCommands(snapshot.PendingCommands);

        WorldState world = new(snapshot.Calendar, snapshot.RootSeed, snapshot.RandomStreams, snapshot.Geography)
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
            default:
                throw new SimulationValidationException($"Unregistered event payload '{campaignEvent.Payload.GetType().Name}'.");
        }

        lastEventDate = campaignEvent.ResolutionDate;
        lastEventPhase = campaignEvent.Phase;
        lastEventPriority = campaignEvent.Priority;
        lastEventId = campaignEvent.EventId;
    }

    internal void AdvanceCalendar() => Calendar = Calendar.NextTurn();

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

    private static void ValidateSystemVersions(IReadOnlyList<SystemVersion> versions)
    {
        string[] expected = CurrentSystemVersions
            .Select(version => $"{version.SystemId}@{version.Version}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actual = versions
            .Select(version => $"{version.SystemId}@{version.Version}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new SaveCompatibilityException(
                $"Snapshot system versions are incompatible. Expected [{string.Join(", ", expected)}], found [{string.Join(", ", actual)}].");
        }
    }

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
            || !Enum.IsDefined(command.Phase)))
        {
            throw new SimulationValidationException("Snapshot contains an invalid pending command.");
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
