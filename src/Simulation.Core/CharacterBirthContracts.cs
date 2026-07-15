using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterBirthContractVersions
{
    public const int NewbornSpecification = 1;
    public const int Change = 1;
}

public static class CharacterBirthLimits
{
    public const int MaximumInheritedTraits = 8;
}

public sealed record GeneratedNewbornSpecification
{
    [JsonConstructor]
    public GeneratedNewbornSpecification(
        int contractVersion,
        EntityId primaryNameKey,
        EntityId? cultureId,
        EntityId? familyId,
        EntityId? householdId,
        IReadOnlyList<EntityId> inheritedTraitIds)
    {
        ContractVersion = contractVersion;
        PrimaryNameKey = primaryNameKey;
        CultureId = cultureId;
        FamilyId = familyId;
        HouseholdId = householdId;
        InheritedTraitIds = inheritedTraitIds is null
            ? null!
            : Array.AsReadOnly(inheritedTraitIds.ToArray());
    }

    public int ContractVersion { get; }

    public EntityId PrimaryNameKey { get; }

    public EntityId? CultureId { get; }

    public EntityId? FamilyId { get; }

    public EntityId? HouseholdId { get; }

    public IReadOnlyList<EntityId> InheritedTraitIds { get; }
}

public sealed record ResolvePregnancyBirthAction(
    EntityId ExpectedPregnancyId,
    GeneratedNewbornSpecification Newborn)
    : ICharacterFamilyAction;

public sealed record CharacterBirthChange(
    int ContractVersion,
    EntityId BirthId,
    CharacterPregnancyState ResolvedPregnancy,
    CharacterDefinition ChildDefinition,
    CharacterState ChildState,
    EntityId? FamilyId,
    EntityId? HouseholdId,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record PregnancyBirthResolvedOutcome(CharacterBirthChange Birth)
    : ICharacterFamilyActionOutcome;

public static class CharacterBirthIds
{
    public static EntityId DeriveBirthId(
        EntityId sourceEventId,
        EntityId pregnancyId) => StableId.Hash(
        "character_birth",
        "pregnancy-birth.v1",
        StableId.RequireId(sourceEventId, nameof(sourceEventId)).Value,
        StableId.RequireId(pregnancyId, nameof(pregnancyId)).Value);

    public static EntityId DeriveChildId(EntityId pregnancyId) => StableId.Hash(
        "character",
        "pregnancy-child.v1",
        StableId.RequireId(pregnancyId, nameof(pregnancyId)).Value);
}
