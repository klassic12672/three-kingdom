using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterConditionContractVersions
{
    public const int Action = 1;
    public const int Outcome = 1;
    public const int Change = 1;
    public const int Death = 3;
}

public static class CharacterConditionSystem
{
    public static EntityId AuthoritativeActorId { get; } =
        new("system:simulation/character_conditions");
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(IncapacitateCharacterAction), "incapacitate_character.v1")]
[JsonDerivedType(typeof(RestoreCharacterCapacityAction), "restore_character_capacity.v1")]
[JsonDerivedType(typeof(EnterCharacterCustodyAction), "enter_character_custody.v1")]
[JsonDerivedType(typeof(ReleaseCharacterCustodyAction), "release_character_custody.v1")]
[JsonDerivedType(typeof(ResolveCharacterDeathAction), "resolve_character_death.v1")]
public interface ICharacterConditionAction;

public sealed record IncapacitateCharacterAction(
    EntityId CharacterId,
    CharacterConditionState ExpectedCurrent) : ICharacterConditionAction;

public sealed record RestoreCharacterCapacityAction(
    EntityId CharacterId,
    CharacterConditionState ExpectedCurrent) : ICharacterConditionAction;

public sealed record EnterCharacterCustodyAction(
    EntityId CharacterId,
    CharacterConditionState ExpectedCurrent,
    CharacterCustodyStatus CustodyStatus,
    EntityId CustodianCharacterId) : ICharacterConditionAction;

public sealed record ReleaseCharacterCustodyAction(
    EntityId CharacterId,
    CharacterConditionState ExpectedCurrent) : ICharacterConditionAction;

public sealed record ResolveCharacterDeathAction(
    EntityId CharacterId,
    CharacterConditionState ExpectedCurrent) : ICharacterConditionAction;

[method: JsonConstructor]
public sealed record CharacterConditionActionCommandPayload(ICharacterConditionAction Action)
    : ICampaignCommandPayload;

public sealed record CharacterConditionChange(
    int ContractVersion,
    EntityId ChangeId,
    EntityId CharacterId,
    CharacterConditionState PreviousCondition,
    CharacterConditionState CurrentCondition,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId);

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CharacterConditionChangedOutcome), "character_condition_changed.v1")]
[JsonDerivedType(typeof(CharacterDeathResolvedOutcome), "character_death_resolved.v1")]
public interface ICharacterConditionActionOutcome;

public sealed record CharacterConditionChangedOutcome(
    CharacterConditionChange Change,
    CharacterMarriageLifecycleChangeSet MarriageChanges)
    : ICharacterConditionActionOutcome;

public sealed record CharacterDeathChange(
    int ContractVersion,
    EntityId DeathId,
    CharacterConditionChange ConditionChange,
    IReadOnlyList<CharacterConditionChange> ReleasedCustodyChanges,
    CharacterMarriageLifecycleChangeSet MarriageChanges,
    IReadOnlyList<CharacterGuardianshipState> EndedGuardianships,
    IReadOnlyList<CharacterPregnancyState> RemovedPregnancies,
    CharacterCareerDeathChangeSet CareerChanges,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record CharacterDeathResolvedOutcome(CharacterDeathChange Death)
    : ICharacterConditionActionOutcome;

public sealed record CharacterConditionActionResolvedEventPayload(
    EntityId ActingActorId,
    ICharacterConditionAction Action,
    ICharacterConditionActionOutcome Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    RelationshipMemoryConsequenceSpecification? RelationshipMemoryConsequence = null)
    : ICampaignEventPayload;

public static class HouseholdDecisionContractVersions
{
    public const int Action = 1;
    public const int Outcome = 1;
    public const int Transition = 1;
}

public enum HouseholdDecisionKind
{
    Expulsion = 0,
    CaptiveIncorporation = 1,
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ExpelHouseholdMemberAction), "expel_household_member.v1")]
[JsonDerivedType(typeof(IncorporateCaptiveHouseholdMemberAction), "incorporate_captive_household_member.v1")]
public interface IHouseholdDecisionAction;

public sealed record ExpelHouseholdMemberAction(
    EntityId HouseholdId,
    EntityId MemberCharacterId) : IHouseholdDecisionAction;

public sealed record IncorporateCaptiveHouseholdMemberAction(
    EntityId DestinationHouseholdId,
    EntityId MemberCharacterId) : IHouseholdDecisionAction;

[method: JsonConstructor]
public sealed record HouseholdDecisionCommandPayload(IHouseholdDecisionAction Action)
    : ICampaignCommandPayload;

public sealed record HouseholdMembershipTransition(
    int ContractVersion,
    EntityId TransitionId,
    HouseholdDecisionKind Kind,
    EntityId MemberCharacterId,
    EntityId? SourceHouseholdId,
    EntityId? DestinationHouseholdId,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId);

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HouseholdMembershipChangedOutcome), "household_membership_changed.v1")]
public interface IHouseholdDecisionOutcome;

public sealed record HouseholdMembershipChangedOutcome(HouseholdMembershipTransition Transition)
    : IHouseholdDecisionOutcome;

public sealed record HouseholdDecisionResolvedEventPayload(
    EntityId ActingCharacterId,
    IHouseholdDecisionAction Action,
    IHouseholdDecisionOutcome Outcome,
    RelationshipMemoryConsequenceSpecification RelationshipMemoryConsequence)
    : ICampaignEventPayload;

public static class CharacterConditionIds
{
    public static EntityId DeriveActionEventId(CampaignDate resolutionDate, EntityId commandId) =>
        StableId.Hash(
            "event",
            "character-condition-action-event.v1",
            StableId.FormatDate(resolutionDate),
            StableId.RequireId(commandId, nameof(commandId)).Value);

    public static EntityId DeriveChangeId(EntityId eventId, EntityId characterId) =>
        StableId.Hash(
            "character_condition_change",
            "character-condition-change.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(characterId, nameof(characterId)).Value);

    public static EntityId DeriveDeathId(EntityId eventId, EntityId characterId) =>
        StableId.Hash(
            "character_death",
            "character-death.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(characterId, nameof(characterId)).Value);

    public static EntityId DeriveRelationshipConsequenceId(
        EntityId eventId,
        int zeroBasedIndex)
    {
        if (zeroBasedIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
        }

        return StableId.Hash(
            "relationship_consequence",
            "character-condition-action-relationship-consequence.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            zeroBasedIndex.ToString(CultureInfo.InvariantCulture));
    }
}

public static class HouseholdDecisionIds
{
    public static EntityId DeriveActionEventId(CampaignDate resolutionDate, EntityId commandId) =>
        StableId.Hash(
            "event",
            "household-decision-event.v1",
            StableId.FormatDate(resolutionDate),
            StableId.RequireId(commandId, nameof(commandId)).Value);

    public static EntityId DeriveTransitionId(EntityId eventId, EntityId characterId) =>
        StableId.Hash(
            "household_transition",
            "household-membership-transition.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(characterId, nameof(characterId)).Value);

    public static EntityId DeriveRelationshipConsequenceId(EntityId eventId, int zeroBasedIndex)
    {
        if (zeroBasedIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
        }

        return StableId.Hash(
            "relationship_consequence",
            "household-decision-relationship-consequence.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            zeroBasedIndex.ToString(CultureInfo.InvariantCulture));
    }
}

internal static class StableId
{
    internal static EntityId Hash(string entityNamespace, string domain, params string[] fields)
    {
        StringBuilder canonical = new();
        AppendField(canonical, domain);
        foreach (string field in fields)
        {
            AppendField(canonical, field);
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return new EntityId($"{entityNamespace}:sha256/{Convert.ToHexStringLower(digest)}");
    }

    internal static EntityId RequireId(EntityId value, string parameterName)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException("A valid stable ID is required.", parameterName);
        }

        return value;
    }

    internal static string FormatDate(CampaignDate value)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException("A valid campaign date is required.", nameof(value));
        }

        return string.Concat(
            value.Year.ToString("D4", CultureInfo.InvariantCulture),
            "-",
            value.Month.ToString("D2", CultureInfo.InvariantCulture),
            "-",
            value.Day.ToString("D2", CultureInfo.InvariantCulture));
    }

    private static void AppendField(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
        target.Append(';');
    }
}
