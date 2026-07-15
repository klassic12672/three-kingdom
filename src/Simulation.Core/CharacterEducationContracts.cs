namespace Simulation.Core;

public static class CharacterEducationContractVersions
{
    public const int Attainment = 1;
}

public static class CharacterEducationLimits
{
    public const int MaximumAttainmentsPerCharacter = 64;
}

public sealed record CharacterEducationAttainment(
    int ContractVersion,
    EntityId AttainmentId,
    EntityId WardCharacterId,
    EntityId TeacherCharacterId,
    EntityId PrimaryGuardianshipId,
    EntityId AbilityId,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record CompletePrimaryGuardianEducationAction(
    EntityId WardCharacterId,
    EntityId ExpectedPrimaryGuardianshipId,
    EntityId AbilityId)
    : ICharacterFamilyAction;

public sealed record PrimaryGuardianEducationCompletedOutcome(
    CharacterEducationAttainment Attainment)
    : ICharacterFamilyActionOutcome;

public static class CharacterEducationIds
{
    public static EntityId DeriveAttainmentId(
        EntityId sourceEventId,
        EntityId wardCharacterId,
        EntityId teacherCharacterId,
        EntityId abilityId) => StableId.Hash(
        "character_education_attainment",
        "primary-guardian-education-attainment.v1",
        StableId.RequireId(sourceEventId, nameof(sourceEventId)).Value,
        StableId.RequireId(wardCharacterId, nameof(wardCharacterId)).Value,
        StableId.RequireId(teacherCharacterId, nameof(teacherCharacterId)).Value,
        StableId.RequireId(abilityId, nameof(abilityId)).Value);
}
