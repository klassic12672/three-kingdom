using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public static class CharacterPregnancyContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int AuthoritativeQuery = 1;
}

public static class CharacterPregnancyLimits
{
    public const int GestationDays = 280;
    public const int MinimumParentAge = 18;
}

public static class CharacterPregnancySystem
{
    public const string SystemId = "simulation.character_pregnancies";
    public const int Version = 1;
}

public sealed record CharacterPregnancyState(
    int ContractVersion,
    EntityId PregnancyId,
    EntityId GestationalParentCharacterId,
    EntityId OtherBiologicalParentCharacterId,
    EntityId SourceUnionId,
    CampaignDate StartDate,
    CampaignDate ExpectedBirthDate,
    long StartTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record CharacterPregnancyWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<CharacterPregnancyState> ActivePregnancies)
{
    public static CharacterPregnancyWorldSnapshot Empty { get; } = new(
        CharacterPregnancyContractVersions.Snapshot,
        []);

    public CharacterPregnancyWorldSnapshot Canonicalize() => this with
    {
        ActivePregnancies = ActivePregnancies
            .OrderBy(item => item.PregnancyId)
            .ToArray(),
    };
}

public interface IAuthoritativeCharacterPregnancyWorldQuery
{
    IReadOnlyList<CharacterPregnancyState> ActivePregnancies { get; }

    bool TryGetActivePregnancyForGestationalParent(
        EntityId gestationalParentCharacterId,
        [NotNullWhen(true)] out CharacterPregnancyState? pregnancy);

    bool TryGetActivePregnancyForUnion(
        EntityId sourceUnionId,
        [NotNullWhen(true)] out CharacterPregnancyState? pregnancy);

    IReadOnlyList<CharacterPregnancyState> GetActivePregnanciesInvolving(
        EntityId characterId);
}

public sealed record RegisterActivePregnancyAction(
    EntityId GestationalParentCharacterId,
    EntityId OtherBiologicalParentCharacterId,
    EntityId SourceUnionId,
    EntityId? ExpectedCurrentPregnancyId)
    : ICharacterFamilyAction;

public sealed record ActivePregnancyRegisteredOutcome(
    CharacterPregnancyState Pregnancy)
    : ICharacterFamilyActionOutcome;

public static class CharacterPregnancyIds
{
    public static EntityId DerivePregnancyId(
        EntityId sourceEventId,
        EntityId gestationalParentCharacterId,
        EntityId otherBiologicalParentCharacterId,
        EntityId sourceUnionId) => StableId.Hash(
        "pregnancy",
        "active-character-pregnancy.v1",
        StableId.RequireId(sourceEventId, nameof(sourceEventId)).Value,
        StableId.RequireId(
            gestationalParentCharacterId,
            nameof(gestationalParentCharacterId)).Value,
        StableId.RequireId(
            otherBiologicalParentCharacterId,
            nameof(otherBiologicalParentCharacterId)).Value,
        StableId.RequireId(sourceUnionId, nameof(sourceUnionId)).Value);
}
