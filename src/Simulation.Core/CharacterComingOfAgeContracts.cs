namespace Simulation.Core;

public static class CharacterComingOfAgeSystem
{
    public static EntityId AuthoritativeActorId { get; } =
        new("system:simulation/character_lifecycle");

    public const int Priority = 0;
}

public sealed record CharacterComingOfAgeCommandPayload(
    EntityId CharacterId,
    EntityId? ExpectedActivePrimaryGuardianshipId)
    : ICampaignCommandPayload;

public sealed record CharacterCameOfAgeEventPayload(
    EntityId CharacterId,
    CharacterGuardianshipState? EndedPrimaryGuardianship)
    : ICampaignEventPayload;

public static class CharacterComingOfAgeIds
{
    public static EntityId DeriveCommandId(
        CampaignDate resolutionDate,
        EntityId characterId) => StableId.Hash(
        "command",
        "character-coming-of-age-command.v1",
        StableId.FormatDate(resolutionDate),
        StableId.RequireId(characterId, nameof(characterId)).Value);

    public static EntityId DeriveEventId(
        CampaignDate resolutionDate,
        EntityId commandId) => StableId.Hash(
        "event",
        "character-coming-of-age-event.v1",
        StableId.FormatDate(resolutionDate),
        StableId.RequireId(commandId, nameof(commandId)).Value);
}
