namespace Simulation.Core;

public static class CharacterComingOfAgePlanner
{
    public static IReadOnlyList<CampaignCommand> PlanCommands(
        CampaignDate resolutionDate,
        IAuthoritativeCharacterWorldQuery characters,
        IAuthoritativeCharacterGuardianshipWorldQuery guardianships)
    {
        if (!resolutionDate.IsValid)
        {
            throw new SimulationValidationException(
                "Coming-of-age resolution date is invalid.");
        }

        if (characters is null || guardianships is null)
        {
            throw new SimulationValidationException(
                "Coming-of-age planning requires authoritative character and guardianship queries.");
        }

        List<CampaignCommand> commands = [];
        foreach (AuthoritativeCharacterProfile profile in characters.Profiles)
        {
            if (profile is null)
            {
                throw new SimulationValidationException(
                    "Coming-of-age planning encountered a null character profile.");
            }

            if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive
                || !IsComingOfAgeTransition(profile.BirthDate, resolutionDate))
            {
                continue;
            }

            EntityId? expectedGuardianshipId = guardianships
                .TryGetActivePrimaryGuardianshipForWard(
                    profile.CharacterId,
                    out CharacterGuardianshipState? active)
                        ? active.GuardianshipId
                        : null;
            EntityId commandId = CharacterComingOfAgeIds.DeriveCommandId(
                resolutionDate,
                profile.CharacterId);
            commands.Add(CampaignCommand.Create(
                commandId,
                CharacterComingOfAgeSystem.AuthoritativeActorId,
                resolutionDate,
                new CharacterComingOfAgeCommandPayload(
                    profile.CharacterId,
                    expectedGuardianshipId),
                ResolutionPhase.Systems,
                CharacterComingOfAgeSystem.Priority));
        }

        return Array.AsReadOnly(commands
            .OrderBy(command => command.CommandId)
            .ToArray());
    }

    internal static CharacterComingOfAgeResolutionPlan PrepareResolution(
        EntityId actingActorId,
        CharacterComingOfAgeCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId,
        IAuthoritativeCharacterWorldQuery characters,
        CharacterGuardianshipWorldState guardianships)
    {
        if (payload is null
            || characters is null
            || guardianships is null
            || !resolutionDate.IsValid
            || authoritativeTurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Coming-of-age resolution contains null or invalid coordinates.");
        }

        if (actingActorId != CharacterComingOfAgeSystem.AuthoritativeActorId
            || commandId != CharacterComingOfAgeIds.DeriveCommandId(
                resolutionDate,
                payload.CharacterId)
            || eventId != CharacterComingOfAgeIds.DeriveEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Coming-of-age resolution requires reserved authority and exact command/event identities.");
        }

        if (!characters.TryGetCharacterProfile(
                payload.CharacterId,
                out AuthoritativeCharacterProfile? profile)
            || profile is null
            || profile.Condition.VitalStatus != CharacterVitalStatus.Alive
            || !IsComingOfAgeTransition(profile.BirthDate, resolutionDate))
        {
            throw new SimulationValidationException(
                $"Character '{payload.CharacterId}' is not living through an exact 17-to-18 transition.");
        }

        bool hasActiveGuardianship = guardianships
            .TryGetActivePrimaryGuardianshipForWard(
                payload.CharacterId,
                out CharacterGuardianshipState? active);
        EntityId? currentGuardianshipId = hasActiveGuardianship
            ? active!.GuardianshipId
            : null;
        if (payload.ExpectedActivePrimaryGuardianshipId != currentGuardianshipId)
        {
            throw new SimulationValidationException(
                $"Coming-of-age guardianship expectation is stale for character '{payload.CharacterId}'.");
        }

        CharacterGuardianshipComingOfAgePlan? guardianship = hasActiveGuardianship
            ? guardianships.PrepareComingOfAgeTermination(
                payload.CharacterId,
                currentGuardianshipId!.Value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId,
                eventId)
            : null;
        CharacterCameOfAgeEventPayload resolved = new(
            payload.CharacterId,
            guardianship?.EndedPrimaryGuardianship);
        return new CharacterComingOfAgeResolutionPlan(
            resolved,
            GetAffectedIds(resolved),
            guardianship?.GuardianshipPlan);
    }

    internal static IReadOnlyList<EntityId> GetAffectedIds(
        CharacterCameOfAgeEventPayload payload)
    {
        if (payload is null || !payload.CharacterId.IsValid)
        {
            throw new SimulationValidationException(
                "Coming-of-age event payload is invalid.");
        }

        List<EntityId> affected =
        [
            payload.CharacterId,
        ];
        if (payload.EndedPrimaryGuardianship is CharacterGuardianshipState ended)
        {
            if (ended.WardCharacterId != payload.CharacterId
                || ended.Status != CharacterGuardianshipStatus.Ended
                || ended.EndReason != CharacterGuardianshipEndReason.WardCameOfAge)
            {
                throw new SimulationValidationException(
                    "Coming-of-age event guardianship does not match the character transition.");
            }

            affected.Add(ended.GuardianshipId);
            affected.Add(ended.GuardianCharacterId);
        }

        if (affected.Any(id => !id.IsValid))
        {
            throw new SimulationValidationException(
                "Coming-of-age event contains an invalid affected ID.");
        }

        return Array.AsReadOnly(affected
            .Distinct()
            .Order()
            .ToArray());
    }

    internal static bool IsComingOfAgeTransition(
        CampaignDate birthDate,
        CampaignDate resolutionDate)
    {
        if (!birthDate.IsValid
            || !resolutionDate.IsValid
            || birthDate.CompareTo(resolutionDate) > 0
            || CalculateAge(birthDate, resolutionDate) != 18)
        {
            return false;
        }

        return CalculateAge(birthDate, resolutionDate.AddDays(-1)) == 17;
    }

    private static int CalculateAge(CampaignDate birthDate, CampaignDate date)
    {
        int age = date.Year - birthDate.Year;
        if (date.Month < birthDate.Month
            || (date.Month == birthDate.Month && date.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }
}

internal sealed record CharacterComingOfAgeResolutionPlan(
    CharacterCameOfAgeEventPayload Payload,
    IReadOnlyList<EntityId> AffectedIds,
    CharacterGuardianshipWorldUpdatePlan? GuardianshipPlan);
