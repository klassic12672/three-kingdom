using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterEstateHoldingWorldState
    : IAuthoritativeCharacterEstateHoldingWorldQuery
{
    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, CharacterEstateHoldingState> holdings = [];
    private CampaignDate snapshotDate;

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

    internal CharacterEstateInheritancePlan PrepareInheritance(
        EntityId sourceCharacterId,
        EntityId recipientCharacterId,
        CampaignDate resolutionDate)
    {
        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(snapshotDate) < 0)
        {
            throw new SimulationValidationException(
                "Estate inheritance cannot precede estate-holding state.");
        }

        AuthoritativeCharacterProfile source = RequireCharacter(
            sourceCharacterId,
            "Estate-inheritance source");
        AuthoritativeCharacterProfile recipient = RequireCharacter(
            recipientCharacterId,
            "Estate-inheritance recipient");
        if (sourceCharacterId == recipientCharacterId)
        {
            throw new SimulationValidationException(
                "Estate inheritance cannot transfer holdings to the current owner.");
        }

        if (source.BirthDate.CompareTo(resolutionDate) > 0
            || recipient.BirthDate.CompareTo(resolutionDate) > 0)
        {
            throw new SimulationValidationException(
                "Estate-inheritance participants are not born by resolution.");
        }

        CharacterEstateHoldingState[] inherited = holdings.Values
            .Where(item => item.OwnerCharacterId == sourceCharacterId)
            .OrderBy(item => item.EstateId)
            .ToArray();
        int recipientCount = holdings.Values.Count(
            item => item.OwnerCharacterId == recipientCharacterId);
        if (recipientCount > CharacterEstateHoldingLimits.HoldingsPerCharacter
            - inherited.Length)
        {
            throw new SimulationValidationException(
                $"Estate inheritance would exceed the recipient holding limit of "
                + $"{CharacterEstateHoldingLimits.HoldingsPerCharacter}.");
        }

        Dictionary<EntityId, CharacterEstateHoldingState> replacements = inherited
            .ToDictionary(
                item => item.EstateId,
                item => item with { OwnerCharacterId = recipientCharacterId });
        CharacterEstateHoldingWorldState candidate = new(
            new CharacterEstateHoldingWorldSnapshot(
                CharacterEstateHoldingContractVersions.Snapshot,
                holdings.Values
                    .Select(item => replacements.TryGetValue(
                            item.EstateId,
                            out CharacterEstateHoldingState? replacement)
                        ? replacement
                        : Clone(item))
                    .ToArray()),
            characters,
            resolutionDate);
        SuccessionEstateTransfer[] transfers = inherited
            .Select(item => new SuccessionEstateTransfer(
                CharacterSuccessionContractVersions.Inheritance,
                item.EstateId,
                sourceCharacterId,
                recipientCharacterId))
            .ToArray();
        return new CharacterEstateInheritancePlan(
            transfers,
            new CharacterEstateHoldingWorldUpdatePlan(candidate));
    }

    internal void CommitPrepared(CharacterEstateHoldingWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException(
                "Prepared character-estate-holding update cannot be null.");
        }

        holdings.Clear();
        foreach (CharacterEstateHoldingState holding in plan.Candidate.holdings.Values)
        {
            holdings.Add(holding.EstateId, Clone(holding));
        }

        snapshotDate = plan.Candidate.snapshotDate;
    }

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

internal sealed record CharacterEstateHoldingWorldUpdatePlan(
    CharacterEstateHoldingWorldState Candidate);

internal sealed record CharacterEstateInheritancePlan(
    IReadOnlyList<SuccessionEstateTransfer> Transfers,
    CharacterEstateHoldingWorldUpdatePlan EstatePlan);
