using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterEstateHoldingWorldState
    : IAuthoritativeCharacterEstateHoldingWorldQuery
{
    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, CharacterEstateHoldingState> holdings = [];
    private readonly CampaignDate snapshotDate;

    public CharacterEstateHoldingWorldState(
        CharacterEstateHoldingWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignDate snapshotDate)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException(
                "Character-estate-holding snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException(
                "Authoritative character query cannot be null.");
        if (!snapshotDate.IsValid)
        {
            throw new SimulationValidationException(
                "Character-estate-holding snapshot date is invalid.");
        }

        this.snapshotDate = snapshotDate;
        ValidateSnapshotShape(snapshot);
        AddHoldings(snapshot.Holdings);
    }

    public IReadOnlyList<CharacterEstateHoldingState> Holdings =>
        holdings.Values.Select(Clone).ToArray();

    public bool TryGetHolding(
        EntityId estateId,
        [NotNullWhen(true)] out CharacterEstateHoldingState? holding)
    {
        if (holdings.TryGetValue(estateId, out CharacterEstateHoldingState? stored))
        {
            holding = Clone(stored);
            return true;
        }

        holding = null;
        return false;
    }

    public IReadOnlyList<CharacterEstateHoldingState> GetHoldingsOwnedBy(
        EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Character-estate-holding query owner");
        return holdings.Values
            .Where(item => item.OwnerCharacterId == characterId)
            .Select(Clone)
            .ToArray();
    }

    public CharacterEstateHoldingWorldSnapshot CaptureSnapshot() => new(
        CharacterEstateHoldingContractVersions.Snapshot,
        holdings.Values.Select(Clone).ToArray());

    private static void ValidateSnapshotShape(CharacterEstateHoldingWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterEstateHoldingContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported character-estate-holding snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.Holdings is null || snapshot.Holdings.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Character-estate-holding snapshot collection and entries cannot be null.");
        }
    }

    private void AddHoldings(IReadOnlyList<CharacterEstateHoldingState> source)
    {
        Dictionary<EntityId, int> holdingsByOwner = [];
        foreach (CharacterEstateHoldingState holding in source)
        {
            if (holding.ContractVersion != CharacterEstateHoldingContractVersions.State)
            {
                throw new SimulationValidationException(
                    $"Estate holding '{holding.EstateId}' has unsupported contract version {holding.ContractVersion}.");
            }

            ValidateEstateId(holding.EstateId);
            ValidateOwner(holding.OwnerCharacterId, $"Estate holding '{holding.EstateId}' owner");
            if (!holdings.TryAdd(holding.EstateId, Clone(holding)))
            {
                throw new SimulationValidationException(
                    $"Duplicate estate holding '{holding.EstateId}'.");
            }

            int ownerCount = holdingsByOwner.TryGetValue(
                holding.OwnerCharacterId,
                out int currentCount)
                ? checked(currentCount + 1)
                : 1;
            if (ownerCount > CharacterEstateHoldingLimits.HoldingsPerCharacter)
            {
                throw new SimulationValidationException(
                    $"Character '{holding.OwnerCharacterId}' exceeds the estate-holding limit of "
                    + $"{CharacterEstateHoldingLimits.HoldingsPerCharacter}.");
            }

            holdingsByOwner[holding.OwnerCharacterId] = ownerCount;
        }
    }

    private static void ValidateEstateId(EntityId estateId)
    {
        if (!estateId.IsValid
            || !estateId.Value.StartsWith("estate:", StringComparison.Ordinal))
        {
            throw new SimulationValidationException(
                $"Estate holding ID '{estateId}' must use the 'estate:' namespace.");
        }
    }

    private void ValidateOwner(EntityId characterId, string label)
    {
        AuthoritativeCharacterProfile profile = RequireCharacter(characterId, label);

        if (profile.BirthDate.CompareTo(snapshotDate) > 0)
        {
            throw new SimulationValidationException(
                $"{label} '{characterId}' is not born by snapshot date '{snapshotDate}'.");
        }
    }

    private AuthoritativeCharacterProfile RequireCharacter(EntityId characterId, string label)
    {
        if (!characterId.IsValid
            || !characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            throw new SimulationValidationException(
                $"{label} '{characterId}' does not exist.");
        }

        return profile;
    }

    private static CharacterEstateHoldingState Clone(CharacterEstateHoldingState value) =>
        value with { };
}
