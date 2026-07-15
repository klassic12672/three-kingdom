using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterContractVersions
{
    public const int LegacySnapshot = 1;
    public const int LegacyDefinition = 1;
    public const int LegacyState = 1;
    public const int AuthoredSnapshot = 2;
    public const int PreviousSnapshot = 2;
    public const int Snapshot = 3;
    public const int Definition = 2;
    public const int AuthoredState = 2;
    public const int PreviousState = 2;
    public const int State = 3;
    public const int AuthoritativeQuery = 3;
}

public enum CharacterIdentityKind
{
    Ability = 0,
    Aptitude = 1,
    Trait = 2,
    Ambition = 3,
    Reputation = 4,
    Flaw = 5,
}

public enum CharacterOriginKind
{
    LegacyUnknown = 0,
    Authored = 1,
    Custom = 2,
    Generated = 3,
}

public enum CharacterHistoricalClassification
{
    Historical = 0,
    Disputed = 1,
    Inferred = 2,
    Romance = 3,
    Fictional = 4,
}

public enum ParentChildLinkKind
{
    UnspecifiedLegacy = 0,
    Biological = 1,
    LegalAdoptive = 2,
}

public enum CharacterVitalStatus
{
    Alive = 0,
    Dead = 1,
}

public enum CharacterHealthStatus
{
    Healthy = 0,
    Injured = 1,
    Ill = 2,
    Critical = 3,
}

public enum CharacterCustodyStatus
{
    Free = 0,
    Detained = 1,
    Captive = 2,
    Hostage = 3,
}

public sealed record StructuredCharacterName(
    EntityId PrimaryNameKey,
    EntityId? CourtesyNameKey);

public sealed record CharacterContentOrigin(
    CharacterOriginKind OriginKind,
    CharacterHistoricalClassification? HistoricalClassification,
    EntityId RecordId,
    EntityId? OwningPackId,
    IReadOnlyList<EntityId> AppliedOverridePackIds,
    IReadOnlyList<EntityId> SourceIds)
{
    public static CharacterContentOrigin LegacyUnknown(EntityId recordId) => new(
        CharacterOriginKind.LegacyUnknown,
        null,
        recordId,
        null,
        [],
        []);

    public CharacterContentOrigin Canonicalize() => this with
    {
        AppliedOverridePackIds = AppliedOverridePackIds is null
            ? null!
            : AppliedOverridePackIds.Order().ToArray(),
        SourceIds = SourceIds is null ? null! : SourceIds.Order().ToArray(),
    };
}

public sealed record CharacterConditionState(
    CharacterVitalStatus VitalStatus,
    CharacterHealthStatus HealthStatus,
    bool IsIncapacitated,
    CharacterCustodyStatus CustodyStatus,
    EntityId? CustodianId)
{
    public static CharacterConditionState Default { get; } = new(
        CharacterVitalStatus.Alive,
        CharacterHealthStatus.Healthy,
        IsIncapacitated: false,
        CharacterCustodyStatus.Free,
        null);
}

public sealed record CharacterParentLink(
    EntityId ParentCharacterId,
    ParentChildLinkKind Kind);

public sealed record CharacterChildLink(
    EntityId ChildCharacterId,
    ParentChildLinkKind Kind);

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
    IReadOnlyList<EntityId> ReputationIds,
    StructuredCharacterName? StructuredName = null,
    CharacterContentOrigin? ContentOrigin = null,
    EntityId? CultureId = null,
    EntityId? OriginLocationId = null,
    IReadOnlyList<EntityId>? FlawIds = null)
{
    public CharacterDefinition Canonicalize() => this with
    {
        AbilityIds = AbilityIds.Order().ToArray(),
        AptitudeIds = AptitudeIds.Order().ToArray(),
        TraitIds = TraitIds.Order().ToArray(),
        AmbitionIds = AmbitionIds.Order().ToArray(),
        ReputationIds = ReputationIds.Order().ToArray(),
        FlawIds = FlawIds is null ? null : FlawIds.Order().ToArray(),
        ContentOrigin = ContentOrigin?.Canonicalize(),
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

[method: JsonConstructor]
public sealed record CharacterState(
    int ContractVersion,
    EntityId CharacterId,
    IReadOnlyList<EntityId> ParentIds,
    IReadOnlyList<CharacterParentLink>? ParentLinks,
    CharacterConditionState? Condition,
    IReadOnlyList<CharacterEducationAttainment>? EducationAttainments)
{
    public CharacterState(
        int contractVersion,
        EntityId characterId,
        IReadOnlyList<EntityId> parentIds)
        : this(contractVersion, characterId, parentIds, null, null, [])
    {
    }

    public CharacterState(
        int contractVersion,
        EntityId characterId,
        IReadOnlyList<EntityId> parentIds,
        IReadOnlyList<CharacterParentLink>? parentLinks)
        : this(contractVersion, characterId, parentIds, parentLinks, null, [])
    {
    }

    public CharacterState(
        int contractVersion,
        EntityId characterId,
        IReadOnlyList<EntityId> parentIds,
        IReadOnlyList<CharacterParentLink>? parentLinks,
        CharacterConditionState? condition)
        : this(contractVersion, characterId, parentIds, parentLinks, condition, [])
    {
    }

    public CharacterState Canonicalize() => this with
    {
        ParentIds = ParentIds.Order().ToArray(),
        ParentLinks = ParentLinks is null
            ? null
            : ParentLinks.OrderBy(link => link.ParentCharacterId)
                .ThenBy(link => link.Kind)
                .ToArray(),
        EducationAttainments = EducationAttainments is null
            ? null
            : EducationAttainments.OrderBy(item => item.AttainmentId).ToArray(),
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

    internal static CharacterWorldSnapshot LegacyV1Empty { get; } = new(
        CharacterContractVersions.LegacySnapshot,
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
    IReadOnlyList<EntityId> ReputationIds,
    StructuredCharacterName StructuredName,
    CharacterContentOrigin ContentOrigin,
    EntityId? CultureId,
    EntityId? OriginLocationId,
    IReadOnlyList<EntityId> FlawIds,
    CharacterConditionState Condition,
    IReadOnlyList<CharacterParentLink> ParentLinks,
    IReadOnlyList<CharacterChildLink> ChildLinks,
    IReadOnlyList<CharacterEducationAttainment> EducationAttainments);

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
