using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public static class CharacterGuardianshipContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int AuthoritativeQuery = 1;
}

public static class CharacterGuardianshipLimits
{
    public const int RetainedRecordsPerInvolvedCharacter = 64;
}

public static class CharacterGuardianshipSystem
{
    public const string SystemId = "simulation.character_guardianships";
    public const int Version = 1;
}

public enum CharacterGuardianshipStatus
{
    Active = 0,
    Ended = 1,
}

public enum CharacterGuardianshipEndReason
{
    WardCameOfAge = 0,
    WardDied = 1,
    GuardianDied = 2,
    GuardianUnavailable = 3,
    Replaced = 4,
    Revoked = 5,
}

public sealed record CharacterGuardianshipState(
    int ContractVersion,
    EntityId GuardianshipId,
    EntityId WardCharacterId,
    EntityId GuardianCharacterId,
    CampaignDate EstablishedDate,
    long EstablishedTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId,
    CharacterGuardianshipStatus Status,
    CampaignDate? EndDate,
    long? EndTurnIndex,
    EntityId? EndSourceCommandId,
    EntityId? EndSourceEventId,
    CharacterGuardianshipEndReason? EndReason);

public sealed record CharacterGuardianshipWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<CharacterGuardianshipState> Guardianships)
{
    public static CharacterGuardianshipWorldSnapshot Empty { get; } = new(
        CharacterGuardianshipContractVersions.Snapshot,
        []);

    public CharacterGuardianshipWorldSnapshot Canonicalize() => this with
    {
        Guardianships = Guardianships
            .OrderBy(item => item.GuardianshipId)
            .ToArray(),
    };
}

public interface IAuthoritativeCharacterGuardianshipWorldQuery
{
    IReadOnlyList<CharacterGuardianshipState> Guardianships { get; }

    bool TryGetActivePrimaryGuardianshipForWard(
        EntityId wardCharacterId,
        [NotNullWhen(true)] out CharacterGuardianshipState? guardianship);

    IReadOnlyList<CharacterGuardianshipState> GetGuardianshipsInvolving(
        EntityId characterId);
}

public sealed record EstablishPrimaryGuardianshipAction(
    EntityId GuardianCharacterId,
    EntityId WardCharacterId,
    EntityId? ExpectedCurrentPrimaryGuardianshipId)
    : ICharacterFamilyAction;

public sealed record PrimaryGuardianshipEstablishedOutcome(
    CharacterGuardianshipState Guardianship)
    : ICharacterFamilyActionOutcome;

public static class CharacterGuardianshipIds
{
    public static EntityId DeriveGuardianshipId(
        EntityId familyEventId,
        EntityId wardCharacterId,
        EntityId guardianCharacterId) => StableId.Hash(
        "guardianship",
        "primary-guardianship.v1",
        StableId.RequireId(familyEventId, nameof(familyEventId)).Value,
        StableId.RequireId(wardCharacterId, nameof(wardCharacterId)).Value,
        StableId.RequireId(guardianCharacterId, nameof(guardianCharacterId)).Value);
}
