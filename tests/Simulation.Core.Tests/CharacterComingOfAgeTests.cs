using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterComingOfAgeTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Ward = new("character:test/coming_of_age_ward");
    private static readonly EntityId SecondWard = new("character:test/coming_of_age_second_ward");
    private static readonly EntityId Guardian = new("character:test/coming_of_age_guardian");

    [Fact]
    public void PlannerCreatesCanonicalSystemCommandsOnlyForLivingExactTransitions()
    {
        EntityId dead = new("character:test/coming_of_age_dead");
        EntityId dayBefore = new("character:test/coming_of_age_day_before");
        EntityId dayAfter = new("character:test/coming_of_age_day_after");
        EntityId older = new("character:test/coming_of_age_older");
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)),
            Seed(SecondWard, new CampaignDate(182, 5, 10)),
            Seed(
                dead,
                new CampaignDate(182, 5, 10),
                new CharacterConditionState(
                    CharacterVitalStatus.Dead,
                    CharacterHealthStatus.Critical,
                    IsIncapacitated: true,
                    CharacterCustodyStatus.Free,
                    null)),
            Seed(dayBefore, new CampaignDate(182, 5, 11)),
            Seed(dayAfter, new CampaignDate(182, 5, 9)),
            Seed(older, new CampaignDate(181, 5, 10)));
        CharacterGuardianshipState active = Active(
            Ward,
            Guardian,
            Date.AddDays(-1),
            4,
            "planned-active");
        CharacterGuardianshipWorldState guardianships = NewGuardianships(
            characters,
            Date,
            [active]);

        IReadOnlyList<CampaignCommand> commands = CharacterComingOfAgePlanner.PlanCommands(
            Date,
            characters,
            guardianships);

        Assert.Equal(2, commands.Count);
        Assert.Equal(
            commands.Select(command => command.CommandId).Order(),
            commands.Select(command => command.CommandId));
        Assert.All(commands, command =>
        {
            Assert.Equal(ContractVersions.CampaignCommand, command.ContractVersion);
            Assert.Equal(CharacterComingOfAgeSystem.AuthoritativeActorId, command.IssuingActor);
            Assert.Equal(Date, command.IssuedDate);
            Assert.Equal(ResolutionPhase.Systems, command.Phase);
            Assert.Equal(CharacterComingOfAgeSystem.Priority, command.Priority);
            Assert.Equal(CommandValidationResult.Valid, command.Validation);
            CharacterComingOfAgeCommandPayload payload = Assert.IsType<
                CharacterComingOfAgeCommandPayload>(command.Payload);
            Assert.Equal(
                CharacterComingOfAgeIds.DeriveCommandId(Date, payload.CharacterId),
                command.CommandId);
        });
        Dictionary<EntityId, CharacterComingOfAgeCommandPayload> payloads = commands
            .Select(command => Assert.IsType<CharacterComingOfAgeCommandPayload>(command.Payload))
            .ToDictionary(payload => payload.CharacterId);
        Assert.Equal(active.GuardianshipId, payloads[Ward].ExpectedActivePrimaryGuardianshipId);
        Assert.Null(payloads[SecondWard].ExpectedActivePrimaryGuardianshipId);
        Assert.DoesNotContain(dead, payloads.Keys);
        Assert.DoesNotContain(dayBefore, payloads.Keys);
        Assert.DoesNotContain(dayAfter, payloads.Keys);
        Assert.DoesNotContain(older, payloads.Keys);
    }

    [Fact]
    public void TransitionRuleHandlesLeapDayOnMarchFirstWithoutBackfill()
    {
        CampaignDate leapBirth = new(2000, 2, 29);

        Assert.False(CharacterComingOfAgePlanner.IsComingOfAgeTransition(
            leapBirth,
            new CampaignDate(2018, 2, 28)));
        Assert.True(CharacterComingOfAgePlanner.IsComingOfAgeTransition(
            leapBirth,
            new CampaignDate(2018, 3, 1)));
        Assert.False(CharacterComingOfAgePlanner.IsComingOfAgeTransition(
            leapBirth,
            new CampaignDate(2018, 3, 2)));
        Assert.False(CharacterComingOfAgePlanner.IsComingOfAgeTransition(
            leapBirth,
            new CampaignDate(2019, 3, 1)));
        Assert.True(CharacterComingOfAgePlanner.IsComingOfAgeTransition(
            new CampaignDate(182, 5, 10),
            Date));
        Assert.False(CharacterComingOfAgePlanner.IsComingOfAgeTransition(
            new CampaignDate(182, 5, 10),
            Date.AddDays(1)));
    }

    [Fact]
    public void ResolutionWithoutGuardianshipStillProducesAPublicEvent()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CharacterGuardianshipWorldState guardianships = NewGuardianships(
            characters,
            Date,
            []);
        CharacterComingOfAgeResolutionPlan plan = Prepare(
            characters,
            guardianships,
            new CharacterComingOfAgeCommandPayload(Ward, null));

        Assert.Equal(Ward, plan.Payload.CharacterId);
        Assert.Null(plan.Payload.EndedPrimaryGuardianship);
        Assert.Null(plan.GuardianshipPlan);
        Assert.Equal([Ward], plan.AffectedIds);
        Assert.IsAssignableFrom<ICampaignEventPayload>(plan.Payload);
    }

    [Fact]
    public void ResolutionAtomicallyEndsTheExactActiveGuardianshipAsWardCameOfAge()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CharacterGuardianshipState active = Active(
            Ward,
            Guardian,
            Date.AddDays(-1),
            4,
            "resolution-active");
        CharacterGuardianshipWorldState guardianships = NewGuardianships(
            characters,
            Date,
            [active]);
        string charactersBefore = Serialize(characters.CaptureSnapshot());
        string guardianshipsBefore = Serialize(guardianships.CaptureSnapshot());

        CharacterComingOfAgeResolutionPlan plan = Prepare(
            characters,
            guardianships,
            new CharacterComingOfAgeCommandPayload(
                Ward,
                active.GuardianshipId));

        Assert.Equal(guardianshipsBefore, Serialize(guardianships.CaptureSnapshot()));
        CharacterGuardianshipState ended = Assert.IsType<CharacterGuardianshipState>(
            plan.Payload.EndedPrimaryGuardianship);
        Assert.Equal(active.GuardianshipId, ended.GuardianshipId);
        Assert.Equal(CharacterGuardianshipStatus.Ended, ended.Status);
        Assert.Equal(CharacterGuardianshipEndReason.WardCameOfAge, ended.EndReason);
        Assert.Equal(Date, ended.EndDate);
        Assert.Equal(5, ended.EndTurnIndex);
        Assert.Equal(CharacterComingOfAgeIds.DeriveCommandId(Date, Ward), ended.EndSourceCommandId);
        Assert.Equal(
            CharacterComingOfAgeIds.DeriveEventId(
                Date,
                CharacterComingOfAgeIds.DeriveCommandId(Date, Ward)),
            ended.EndSourceEventId);
        Assert.Equal(
            new[] { Ward, Guardian, active.GuardianshipId }.Order(),
            plan.AffectedIds);
        Assert.NotNull(plan.GuardianshipPlan);
        Assert.False(plan.GuardianshipPlan.Candidate.TryGetActivePrimaryGuardianshipForWard(
            Ward,
            out _));

        guardianships.ApplyPrepared(plan.GuardianshipPlan);

        Assert.False(guardianships.TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        Assert.Equal(ended, Assert.Single(guardianships.Guardianships));
        Assert.Equal(charactersBefore, Serialize(characters.CaptureSnapshot()));
    }

    [Fact]
    public void ResolutionRejectsStaleExpectationsAuthorityAndIdentityTamperingWithoutMutation()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CharacterGuardianshipState active = Active(
            Ward,
            Guardian,
            Date.AddDays(-1),
            4,
            "revalidation-active");
        CharacterGuardianshipWorldState guardianships = NewGuardianships(
            characters,
            Date,
            [active]);
        string before = Serialize(guardianships.CaptureSnapshot());
        EntityId commandId = CharacterComingOfAgeIds.DeriveCommandId(Date, Ward);
        EntityId eventId = CharacterComingOfAgeIds.DeriveEventId(Date, commandId);

        Assert.Throws<SimulationValidationException>(() => CharacterComingOfAgePlanner
            .PrepareResolution(
                CharacterComingOfAgeSystem.AuthoritativeActorId,
                new CharacterComingOfAgeCommandPayload(Ward, null),
                Date,
                5,
                commandId,
                eventId,
                characters,
                guardianships));
        Assert.Throws<SimulationValidationException>(() => CharacterComingOfAgePlanner
            .PrepareResolution(
                new EntityId("system:test/not_character_lifecycle"),
                new CharacterComingOfAgeCommandPayload(Ward, active.GuardianshipId),
                Date,
                5,
                commandId,
                eventId,
                characters,
                guardianships));
        EntityId wrongCommandId = new("command:test/not_derived");
        Assert.Throws<SimulationValidationException>(() => CharacterComingOfAgePlanner
            .PrepareResolution(
                CharacterComingOfAgeSystem.AuthoritativeActorId,
                new CharacterComingOfAgeCommandPayload(Ward, active.GuardianshipId),
                Date,
                5,
                wrongCommandId,
                CharacterComingOfAgeIds.DeriveEventId(Date, wrongCommandId),
                characters,
                guardianships));
        Assert.Throws<SimulationValidationException>(() => CharacterComingOfAgePlanner
            .PrepareResolution(
                CharacterComingOfAgeSystem.AuthoritativeActorId,
                new CharacterComingOfAgeCommandPayload(Ward, active.GuardianshipId),
                Date,
                5,
                commandId,
                new EntityId("event:test/not_derived"),
                characters,
                guardianships));
        Assert.Equal(before, Serialize(guardianships.CaptureSnapshot()));
    }

    [Fact]
    public void ResolutionRejectsDeadOrNonTransitioningCharactersAndMalformedAffectedData()
    {
        CharacterWorldState deadCharacters = CreateCharacters(
            Date,
            Seed(
                Ward,
                new CampaignDate(182, 5, 10),
                new CharacterConditionState(
                    CharacterVitalStatus.Dead,
                    CharacterHealthStatus.Critical,
                    IsIncapacitated: true,
                    CharacterCustodyStatus.Free,
                    null)));
        CharacterGuardianshipWorldState deadGuardianships = NewGuardianships(
            deadCharacters,
            Date,
            []);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            deadCharacters,
            deadGuardianships,
            new CharacterComingOfAgeCommandPayload(Ward, null)));

        CampaignDate lateDate = Date.AddDays(1);
        CharacterWorldState lateCharacters = CreateCharacters(
            lateDate,
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CharacterGuardianshipWorldState lateGuardianships = NewGuardianships(
            lateCharacters,
            lateDate,
            []);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            lateCharacters,
            lateGuardianships,
            new CharacterComingOfAgeCommandPayload(Ward, null),
            lateDate));
        Assert.Empty(CharacterComingOfAgePlanner.PlanCommands(
            lateDate,
            lateCharacters,
            lateGuardianships));

        CharacterGuardianshipState malformed = Active(
            Ward,
            Guardian,
            Date.AddDays(-1),
            4,
            "malformed-affected") with
        {
            Status = CharacterGuardianshipStatus.Ended,
            EndDate = Date,
            EndTurnIndex = 5,
            EndSourceCommandId = CharacterComingOfAgeIds.DeriveCommandId(Date, Ward),
            EndSourceEventId = CharacterComingOfAgeIds.DeriveEventId(
                Date,
                CharacterComingOfAgeIds.DeriveCommandId(Date, Ward)),
            EndReason = CharacterGuardianshipEndReason.Revoked,
        };
        Assert.Throws<SimulationValidationException>(() => CharacterComingOfAgePlanner
            .GetAffectedIds(new CharacterCameOfAgeEventPayload(Ward, malformed)));
        Assert.Throws<SimulationValidationException>(() => CharacterComingOfAgePlanner
            .GetAffectedIds(new CharacterCameOfAgeEventPayload(
                Ward,
                malformed with
                {
                    EndReason = CharacterGuardianshipEndReason.WardCameOfAge,
                    WardCharacterId = SecondWard,
                })));
    }

    [Fact]
    public void StableCommandAndEventIdentitiesAreGolden()
    {
        EntityId commandId = CharacterComingOfAgeIds.DeriveCommandId(Date, Ward);
        EntityId eventId = CharacterComingOfAgeIds.DeriveEventId(Date, commandId);

        Assert.Equal(
            "command:sha256/93fd669f188cd4b417d1f7c16ffd4a745598022044e4faf2f2d3c629f987e07c",
            commandId.Value);
        Assert.Equal(
            "event:sha256/02de1f52581057b7e044079712ec6abe3eee16d1cf98d6d4da93e99c9aa5a7c0",
            eventId.Value);
    }

    private static CharacterComingOfAgeResolutionPlan Prepare(
        CharacterWorldState characters,
        CharacterGuardianshipWorldState guardianships,
        CharacterComingOfAgeCommandPayload payload,
        CampaignDate? date = null)
    {
        CampaignDate resolutionDate = date ?? Date;
        EntityId commandId = CharacterComingOfAgeIds.DeriveCommandId(
            resolutionDate,
            payload.CharacterId);
        return CharacterComingOfAgePlanner.PrepareResolution(
            CharacterComingOfAgeSystem.AuthoritativeActorId,
            payload,
            resolutionDate,
            5,
            commandId,
            CharacterComingOfAgeIds.DeriveEventId(resolutionDate, commandId),
            characters,
            guardianships);
    }

    private static CharacterGuardianshipWorldState NewGuardianships(
        CharacterWorldState characters,
        CampaignDate date,
        IReadOnlyList<CharacterGuardianshipState> guardianships) => new(
        new CharacterGuardianshipWorldSnapshot(
            CharacterGuardianshipContractVersions.Snapshot,
            guardianships),
        characters,
        new CampaignCalendar(date, 5));

    private static CharacterGuardianshipState Active(
        EntityId ward,
        EntityId guardian,
        CampaignDate establishedDate,
        long establishedTurn,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            establishedDate,
            commandId);
        return new CharacterGuardianshipState(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(
                eventId,
                ward,
                guardian),
            ward,
            guardian,
            establishedDate,
            establishedTurn,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
    }

    private static CharacterWorldState CreateCharacters(
        CampaignDate currentDate,
        params CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds
            .Select(seed =>
            {
                EntityId nameKey = new($"loc:{seed.Id.Value.Replace(':', '/')}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    seed.Id,
                    nameKey,
                    seed.BirthDate,
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(seed.Id),
                    null,
                    null,
                    []);
            })
            .OrderBy(item => item.Id)
            .ToArray();
        CharacterState[] states = seeds
            .Select(seed => new CharacterState(
                CharacterContractVersions.State,
                seed.Id,
                [],
                [],
                seed.Condition))
            .OrderBy(item => item.CharacterId)
            .ToArray();
        return new CharacterWorldState(
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                definitions,
                [],
                [],
                states,
                [],
                []),
            currentDate);
    }

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        CharacterConditionState? condition = null) => new(
        id,
        birthDate,
        condition ?? CharacterConditionState.Default);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);
}
