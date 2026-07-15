using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public static class CharacterEstateHoldingContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int AuthoritativeQuery = 1;
}

public static class CharacterEstateHoldingLimits
{
    public const int HoldingsPerCharacter = 64;
}

public static class CharacterEstateHoldingSystem
{
    public const string SystemId = "simulation.character_estate_holdings";
    public const int Version = 1;
}

public sealed record CharacterEstateHoldingState(
    int ContractVersion,
    EntityId EstateId,
    EntityId OwnerCharacterId);

public sealed record CharacterEstateHoldingWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<CharacterEstateHoldingState> Holdings)
{
    public static CharacterEstateHoldingWorldSnapshot Empty { get; } = new(
        CharacterEstateHoldingContractVersions.Snapshot,
        []);

    public CharacterEstateHoldingWorldSnapshot Canonicalize() => this with
    {
        Holdings = Holdings.OrderBy(item => item.EstateId).ToArray(),
    };
}

public interface IAuthoritativeCharacterEstateHoldingWorldQuery
{
    IReadOnlyList<CharacterEstateHoldingState> Holdings { get; }

    bool TryGetHolding(
        EntityId estateId,
        [NotNullWhen(true)] out CharacterEstateHoldingState? holding);

    IReadOnlyList<CharacterEstateHoldingState> GetHoldingsOwnedBy(EntityId characterId);
}
