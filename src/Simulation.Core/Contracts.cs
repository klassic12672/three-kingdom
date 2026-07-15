using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class ContractVersions
{
    public const int EntityId = 1;
    public const int CampaignCommand = 1;
    public const int CampaignEvent = 1;
    public const int WorldSnapshot = 1;
    public const int SaveEnvelope = 2;
}

public sealed record ValidationIssue(string Code, string Message);

public sealed record CommandValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues)
{
    public static CommandValidationResult Valid { get; } = new(true, []);

    public static CommandValidationResult Invalid(params ValidationIssue[] issues) => new(false, issues);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AdjustResourcesCommandPayload), "adjust_resources.v1")]
[JsonDerivedType(typeof(ChangeSimulationTierCommandPayload), "change_tier.v1")]
[JsonDerivedType(typeof(MovementOrderPayload), "movement_order.v1")]
[JsonDerivedType(typeof(RetreatOrderPayload), "retreat_order.v1")]
[JsonDerivedType(typeof(SupplyOrderPayload), "supply_order.v1")]
[JsonDerivedType(typeof(ChangeControlCommandPayload), "change_control.v1")]
public interface ICampaignCommandPayload;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ResourcesAdjustedEventPayload), "resources_adjusted.v1")]
[JsonDerivedType(typeof(SimulationTierChangedEventPayload), "tier_changed.v1")]
[JsonDerivedType(typeof(CommandCancelledEventPayload), "command_cancelled.v1")]
[JsonDerivedType(typeof(MovementEventPayload), "movement.v1")]
[JsonDerivedType(typeof(InterceptionEventPayload), "interception.v1")]
[JsonDerivedType(typeof(ControlChangedEventPayload), "control_changed.v1")]
[JsonDerivedType(typeof(SupplyTransferredEventPayload), "supply_transferred.v1")]
[JsonDerivedType(typeof(SupplyProducedEventPayload), "supply_produced.v1")]
[JsonDerivedType(typeof(ArmySupplyConsumedEventPayload), "army_supply_consumed.v1")]
public interface ICampaignEventPayload;

public sealed record AdjustResourcesCommandPayload(EntityId Target, long PeopleDelta, long FoodDelta, long GoldDelta)
    : ICampaignCommandPayload;

public sealed record ChangeSimulationTierCommandPayload(EntityId Target, SimulationTier Tier)
    : ICampaignCommandPayload;

public sealed record ResourcesAdjustedEventPayload(EntityId Target, long PeopleDelta, long FoodDelta, long GoldDelta)
    : ICampaignEventPayload;

public sealed record SimulationTierChangedEventPayload(
    EntityId Target,
    SimulationTier PreviousTier,
    SimulationTier Tier,
    ConservationLedger Before,
    ConservationLedger After)
    : ICampaignEventPayload;

public sealed record CommandCancelledEventPayload(string ReasonCode, string Message)
    : ICampaignEventPayload;

public sealed record CampaignCommand(
    int ContractVersion,
    EntityId CommandId,
    EntityId IssuingActor,
    CampaignDate IssuedDate,
    ResolutionPhase Phase,
    int Priority,
    ICampaignCommandPayload Payload,
    CommandValidationResult Validation)
{
    public string CommandType => Payload switch
    {
        AdjustResourcesCommandPayload => "adjust_resources.v1",
        ChangeSimulationTierCommandPayload => "change_tier.v1",
        MovementOrderPayload => "movement_order.v1",
        RetreatOrderPayload => "retreat_order.v1",
        SupplyOrderPayload => "supply_order.v1",
        ChangeControlCommandPayload => "change_control.v1",
        _ => "unregistered",
    };

    public static CampaignCommand Create(
        EntityId commandId,
        EntityId issuingActor,
        CampaignDate issuedDate,
        ICampaignCommandPayload payload,
        ResolutionPhase phase = ResolutionPhase.Commands,
        int priority = 0) => new(
            ContractVersions.CampaignCommand,
            commandId,
            issuingActor,
            issuedDate,
            phase,
            priority,
            payload,
            CommandValidationResult.Valid);
}

public sealed record CampaignEvent(
    int ContractVersion,
    EntityId EventId,
    EntityId? CausalId,
    CampaignDate ResolutionDate,
    ResolutionPhase Phase,
    int Priority,
    IReadOnlyList<EntityId> AffectedIds,
    ICampaignEventPayload Payload)
{
    public string EventType => Payload switch
    {
        ResourcesAdjustedEventPayload => "resources_adjusted.v1",
        SimulationTierChangedEventPayload => "tier_changed.v1",
        CommandCancelledEventPayload => "command_cancelled.v1",
        MovementEventPayload => "movement.v1",
        InterceptionEventPayload => "interception.v1",
        ControlChangedEventPayload => "control_changed.v1",
        SupplyTransferredEventPayload => "supply_transferred.v1",
        SupplyProducedEventPayload => "supply_produced.v1",
        ArmySupplyConsumedEventPayload => "army_supply_consumed.v1",
        _ => "unregistered",
    };
}

public enum SimulationTier
{
    Full = 0,
    Reduced = 1,
    Aggregate = 2,
}

public sealed record PendingWorkItem(EntityId WorkId, CampaignDate DueDate, long Amount);

public readonly record struct ConservationLedger(long People, long Food, long Gold, long PendingWorkAmount)
{
    public static ConservationLedger From(SyntheticEntitySnapshot entity) => new(
        entity.People,
        entity.Food,
        entity.Gold,
        entity.PendingWork.Sum(work => work.Amount));
}

public sealed record SyntheticEntitySnapshot(
    EntityId Id,
    SimulationTier Tier,
    long People,
    long Food,
    long Gold,
    IReadOnlyList<PendingWorkItem> PendingWork)
{
    public SyntheticEntitySnapshot Canonicalize() => this with
    {
        PendingWork = PendingWork.OrderBy(work => work.DueDate)
            .ThenBy(work => work.WorkId)
            .ToArray(),
    };
}

public sealed record SystemVersion(string SystemId, int Version);

public sealed record WorldSnapshot(
    int ContractVersion,
    CampaignCalendar Calendar,
    ulong RootSeed,
    IReadOnlyList<RandomStreamState> RandomStreams,
    IReadOnlyList<SyntheticEntitySnapshot> Entities,
    IReadOnlyList<CampaignCommand> PendingCommands,
    IReadOnlyList<SystemVersion> SystemVersions,
    CampaignDate? LastEventDate,
    ResolutionPhase? LastEventPhase,
    int? LastEventPriority,
    EntityId? LastEventId)
{
    public GeographicWorldSnapshot Geography { get; init; } = GeographicWorldSnapshot.Empty;

    public CharacterWorldSnapshot Characters { get; init; } = CharacterWorldSnapshot.Empty;
}
