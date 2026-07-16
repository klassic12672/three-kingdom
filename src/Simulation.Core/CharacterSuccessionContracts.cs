using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterSuccessionContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int Action = 1;
    public const int Outcome = 1;
    public const int AuthoritativeQuery = 1;
}

public static class CharacterSuccessionLimits
{
    public const int RecentTerminalDesignationsPerCharacter = 32;
}

public static class CharacterSuccessionSystem
{
    public const string SystemId = "simulation.character_succession";
    public const int Version = 1;
}

public enum HeirDesignationStatus
{
    Active = 0,
    Replaced = 1,
    Revoked = 2,
}

public sealed record HeirDesignationState(
    int ContractVersion,
    EntityId DesignationId,
    EntityId DesignatorCharacterId,
    EntityId HeirCharacterId,
    CampaignDate EstablishedDate,
    long EstablishedTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId,
    HeirDesignationStatus Status,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId,
    EntityId? ResolutionEventId);

public sealed record HeirDesignationHistoryAggregate(
    int ContractVersion,
    EntityId DesignatorCharacterId,
    long FoldedReplacedCount,
    long FoldedRevokedCount,
    CampaignDate EarliestDate,
    CampaignDate LatestDate)
{
    [JsonIgnore]
    public long TotalFoldedCount => checked(FoldedReplacedCount + FoldedRevokedCount);
}

public sealed record CharacterSuccessionWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<HeirDesignationState> Designations,
    IReadOnlyList<HeirDesignationHistoryAggregate> History)
{
    public static CharacterSuccessionWorldSnapshot Empty { get; } = new(
        CharacterSuccessionContractVersions.Snapshot,
        [],
        []);

    public CharacterSuccessionWorldSnapshot Canonicalize() => this with
    {
        Designations = Designations.OrderBy(item => item.DesignationId).ToArray(),
        History = History.OrderBy(item => item.DesignatorCharacterId).ToArray(),
    };
}

public interface IAuthoritativeCharacterSuccessionWorldQuery
{
    IReadOnlyList<HeirDesignationState> Designations { get; }

    IReadOnlyList<HeirDesignationHistoryAggregate> History { get; }

    bool TryGetCurrentDesignation(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationState? designation);

    IReadOnlyList<HeirDesignationState> GetDesignationRecordsInvolving(
        EntityId characterId);

    bool TryGetHistory(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationHistoryAggregate? history);
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(DesignateHeirAction), "designate_heir.v1")]
[JsonDerivedType(typeof(RevokeHeirDesignationAction), "revoke_heir_designation.v1")]
public interface ICharacterSuccessionAction;

public sealed record DesignateHeirAction(
    EntityId HeirCharacterId,
    EntityId? ExpectedCurrentDesignationId) : ICharacterSuccessionAction;

public sealed record RevokeHeirDesignationAction(
    EntityId ExpectedCurrentDesignationId) : ICharacterSuccessionAction;

[method: JsonConstructor]
public sealed record CharacterSuccessionActionCommandPayload(ICharacterSuccessionAction Action)
    : ICampaignCommandPayload;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HeirDesignatedOutcome), "heir_designated.v1")]
[JsonDerivedType(typeof(HeirDesignationReplacedOutcome), "heir_designation_replaced.v1")]
[JsonDerivedType(typeof(HeirDesignationRevokedOutcome), "heir_designation_revoked.v1")]
public interface ICharacterSuccessionActionOutcome;

public sealed record HeirDesignatedOutcome(HeirDesignationState CurrentDesignation)
    : ICharacterSuccessionActionOutcome;

public sealed record HeirDesignationReplacedOutcome(
    HeirDesignationState PreviousDesignation,
    HeirDesignationState CurrentDesignation)
    : ICharacterSuccessionActionOutcome;

public sealed record HeirDesignationRevokedOutcome(HeirDesignationState PreviousDesignation)
    : ICharacterSuccessionActionOutcome;

public sealed record CharacterSuccessionActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterSuccessionAction Action,
    ICharacterSuccessionActionOutcome Outcome)
    : ICampaignEventPayload;

public static class CharacterSuccessionIds
{
    public static EntityId DeriveActionEventId(CampaignDate resolutionDate, EntityId commandId) =>
        StableId.Hash(
            "event",
            "character-succession-action-event.v1",
            StableId.FormatDate(resolutionDate),
            StableId.RequireId(commandId, nameof(commandId)).Value);

    public static EntityId DeriveDesignationId(
        EntityId eventId,
        EntityId designatorCharacterId,
        EntityId heirCharacterId) =>
        StableId.Hash(
            "heir_designation",
            "heir-designation.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(designatorCharacterId, nameof(designatorCharacterId)).Value,
            StableId.RequireId(heirCharacterId, nameof(heirCharacterId)).Value);
}
