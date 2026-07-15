using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public static class CharacterContractVersions
{
    public const int Snapshot = 1;
    public const int Definition = 1;
    public const int State = 1;
    public const int AuthoritativeQuery = 1;
}

public enum CharacterIdentityKind
{
    Ability,
    Aptitude,
    Trait,
    Ambition,
    Reputation,
}

public sealed record CharacterIdentityDefinition(
    int ContractVersion,
    EntityId Id,
    CharacterIdentityKind Kind,
    EntityId NameKey);

public sealed record CharacterDefinition(
    int ContractVersion,
    EntityId Id,
    EntityId NameKey,
    CampaignDate BirthDate,
    IReadOnlyList<EntityId> AbilityIds,
    IReadOnlyList<EntityId> AptitudeIds,
    IReadOnlyList<EntityId> TraitIds,
    IReadOnlyList<EntityId> AmbitionIds,
    IReadOnlyList<EntityId> ReputationIds)
{
    public CharacterDefinition Canonicalize() => this with
    {
        AbilityIds = AbilityIds.Order().ToArray(),
        AptitudeIds = AptitudeIds.Order().ToArray(),
        TraitIds = TraitIds.Order().ToArray(),
        AmbitionIds = AmbitionIds.Order().ToArray(),
        ReputationIds = ReputationIds.Order().ToArray(),
    };
}

public sealed record FamilyDefinition(
    int ContractVersion,
    EntityId Id,
    EntityId NameKey);

public sealed record HouseholdDefinition(
    int ContractVersion,
    EntityId Id,
    EntityId NameKey);

public sealed record CharacterState(
    int ContractVersion,
    EntityId CharacterId,
    IReadOnlyList<EntityId> ParentIds)
{
    public CharacterState Canonicalize() => this with
    {
        ParentIds = ParentIds.Order().ToArray(),
    };
}

public sealed record FamilyState(
    int ContractVersion,
    EntityId FamilyId,
    IReadOnlyList<EntityId> MemberIds)
{
    public FamilyState Canonicalize() => this with
    {
        MemberIds = MemberIds.Order().ToArray(),
    };
}

public sealed record HouseholdState(
    int ContractVersion,
    EntityId HouseholdId,
    EntityId HeadCharacterId,
    IReadOnlyList<EntityId> MemberIds)
{
    public HouseholdState Canonicalize() => this with
    {
        MemberIds = MemberIds.Order().ToArray(),
    };
}

public sealed record CharacterWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<CharacterIdentityDefinition> IdentityDefinitions,
    IReadOnlyList<CharacterDefinition> CharacterDefinitions,
    IReadOnlyList<FamilyDefinition> FamilyDefinitions,
    IReadOnlyList<HouseholdDefinition> HouseholdDefinitions,
    IReadOnlyList<CharacterState> CharacterStates,
    IReadOnlyList<FamilyState> FamilyStates,
    IReadOnlyList<HouseholdState> HouseholdStates)
{
    public static CharacterWorldSnapshot Empty { get; } = new(
        CharacterContractVersions.Snapshot,
        [],
        [],
        [],
        [],
        [],
        [],
        []);

    public CharacterWorldSnapshot Canonicalize() => this with
    {
        IdentityDefinitions = IdentityDefinitions.OrderBy(item => item.Id).ToArray(),
        CharacterDefinitions = CharacterDefinitions.OrderBy(item => item.Id)
            .Select(item => item.Canonicalize())
            .ToArray(),
        FamilyDefinitions = FamilyDefinitions.OrderBy(item => item.Id).ToArray(),
        HouseholdDefinitions = HouseholdDefinitions.OrderBy(item => item.Id).ToArray(),
        CharacterStates = CharacterStates.OrderBy(item => item.CharacterId)
            .Select(item => item.Canonicalize())
            .ToArray(),
        FamilyStates = FamilyStates.OrderBy(item => item.FamilyId)
            .Select(item => item.Canonicalize())
            .ToArray(),
        HouseholdStates = HouseholdStates.OrderBy(item => item.HouseholdId)
            .Select(item => item.Canonicalize())
            .ToArray(),
    };
}

public sealed record AuthoritativeCharacterProfile(
    int ContractVersion,
    EntityId CharacterId,
    EntityId NameKey,
    CampaignDate BirthDate,
    int Age,
    IReadOnlyList<EntityId> ParentIds,
    IReadOnlyList<EntityId> ChildIds,
    EntityId? FamilyId,
    EntityId? HouseholdId,
    IReadOnlyList<EntityId> AbilityIds,
    IReadOnlyList<EntityId> AptitudeIds,
    IReadOnlyList<EntityId> TraitIds,
    IReadOnlyList<EntityId> AmbitionIds,
    IReadOnlyList<EntityId> ReputationIds);

public sealed record AuthoritativeHouseholdView(
    int ContractVersion,
    EntityId HouseholdId,
    EntityId NameKey,
    EntityId HeadCharacterId,
    IReadOnlyList<EntityId> MemberIds);

public interface IAuthoritativeCharacterWorldQuery
{
    IReadOnlyList<AuthoritativeCharacterProfile> Profiles { get; }

    IReadOnlyList<AuthoritativeHouseholdView> Households { get; }

    bool TryGetCharacterProfile(EntityId id, [NotNullWhen(true)] out AuthoritativeCharacterProfile? profile);

    bool TryGetHousehold(EntityId id, [NotNullWhen(true)] out AuthoritativeHouseholdView? household);
}
