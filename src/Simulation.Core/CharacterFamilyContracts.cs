using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterFamilyContractVersions
{
    public const int Action = 1;
    public const int Outcome = 1;
    public const int Change = 1;
}

public static class CharacterFamilySystem
{
    public static EntityId AuthoritativeActorId { get; } =
        new("system:simulation/character_family");
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(
    typeof(EstablishLegalAdoptiveParentAction),
    "establish_legal_adoptive_parent.v1")]
[JsonDerivedType(
    typeof(EstablishPrimaryGuardianshipAction),
    "establish_primary_guardianship.v1")]
[JsonDerivedType(
    typeof(EndPrimaryGuardianshipAction),
    "end_primary_guardianship.v1")]
[JsonDerivedType(
    typeof(ReplacePrimaryGuardianshipAction),
    "replace_primary_guardianship.v1")]
[JsonDerivedType(
    typeof(RegisterActivePregnancyAction),
    "register_active_pregnancy.v1")]
[JsonDerivedType(
    typeof(ResolvePregnancyBirthAction),
    "resolve_pregnancy_birth.v1")]
[JsonDerivedType(
    typeof(CompletePrimaryGuardianEducationAction),
    "complete_primary_guardian_education.v1")]
public interface ICharacterFamilyAction;

public sealed record EstablishLegalAdoptiveParentAction : ICharacterFamilyAction
{
    [JsonConstructor]
    public EstablishLegalAdoptiveParentAction(
        EntityId adoptiveParentCharacterId,
        EntityId adoptedCharacterId,
        IReadOnlyList<CharacterParentLink> expectedCurrentParentLinks)
    {
        AdoptiveParentCharacterId = adoptiveParentCharacterId;
        AdoptedCharacterId = adoptedCharacterId;
        ExpectedCurrentParentLinks = expectedCurrentParentLinks is null
            ? null!
            : Array.AsReadOnly(expectedCurrentParentLinks
                .Select(link => link is null ? null! : link with { })
                .ToArray());
    }

    public EntityId AdoptiveParentCharacterId { get; }

    public EntityId AdoptedCharacterId { get; }

    public IReadOnlyList<CharacterParentLink> ExpectedCurrentParentLinks { get; }
}

[method: JsonConstructor]
public sealed record CharacterFamilyActionCommandPayload(ICharacterFamilyAction Action)
    : ICampaignCommandPayload;

public sealed record CharacterParentageChange(
    int ContractVersion,
    EntityId ChangeId,
    EntityId AdoptedCharacterId,
    IReadOnlyList<CharacterParentLink> PreviousParentLinks,
    IReadOnlyList<CharacterParentLink> CurrentParentLinks,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId);

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(
    typeof(LegalAdoptiveParentEstablishedOutcome),
    "legal_adoptive_parent_established.v1")]
[JsonDerivedType(
    typeof(PrimaryGuardianshipEstablishedOutcome),
    "primary_guardianship_established.v1")]
[JsonDerivedType(
    typeof(PrimaryGuardianshipEndedOutcome),
    "primary_guardianship_ended.v1")]
[JsonDerivedType(
    typeof(PrimaryGuardianshipReplacedOutcome),
    "primary_guardianship_replaced.v1")]
[JsonDerivedType(
    typeof(ActivePregnancyRegisteredOutcome),
    "active_pregnancy_registered.v1")]
[JsonDerivedType(
    typeof(PregnancyBirthResolvedOutcome),
    "pregnancy_birth_resolved.v1")]
[JsonDerivedType(
    typeof(PrimaryGuardianEducationCompletedOutcome),
    "primary_guardian_education_completed.v1")]
public interface ICharacterFamilyActionOutcome;

public sealed record LegalAdoptiveParentEstablishedOutcome(CharacterParentageChange Change)
    : ICharacterFamilyActionOutcome;

public sealed record CharacterFamilyActionResolvedEventPayload(
    EntityId ActingActorId,
    ICharacterFamilyAction Action,
    ICharacterFamilyActionOutcome Outcome)
    : ICampaignEventPayload;

public static class CharacterFamilyIds
{
    public static EntityId DeriveActionEventId(CampaignDate resolutionDate, EntityId commandId) =>
        StableId.Hash(
            "event",
            "character-family-action-event.v1",
            StableId.FormatDate(resolutionDate),
            StableId.RequireId(commandId, nameof(commandId)).Value);

    public static EntityId DeriveParentageChangeId(EntityId eventId, EntityId adoptedCharacterId) =>
        StableId.Hash(
            "character_parentage_change",
            "character-parentage-change.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(adoptedCharacterId, nameof(adoptedCharacterId)).Value);
}
