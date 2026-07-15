using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterGuardianshipLifecycleTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId CurrentGuardian = new("character:test/current_guardian");
    private static readonly EntityId ReplacementGuardian = new("character:test/replacement_guardian");
    private static readonly EntityId Ward = new("character:test/ward");
    private static readonly EntityId Custodian = new("character:test/custodian");

    [Fact]
    public void RevocationRequiresTheExactActiveIdAndAppliesPreparedTerminationInPlace()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState active = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "revoked-active");
        CharacterGuardianshipWorldState state = NewState(characters, [active]);
        string charactersBefore = Serialize(characters.CaptureSnapshot());
        string guardianshipsBefore = Serialize(state.CaptureSnapshot());
        IAuthoritativeCharacterGuardianshipWorldQuery query = state;

        CharacterGuardianshipTerminationPlan plan = PrepareTermination(
            state,
            Ward,
            active.GuardianshipId,
            CharacterGuardianshipEndReason.Revoked,
            "revoke");

        Assert.Equal(guardianshipsBefore, Serialize(state.CaptureSnapshot()));
        Assert.Equal(active.GuardianshipId, plan.EndedGuardianship.GuardianshipId);
        Assert.Equal(CharacterGuardianshipStatus.Ended, plan.EndedGuardianship.Status);
        Assert.Equal(Date, plan.EndedGuardianship.EndDate);
        Assert.Equal(5, plan.EndedGuardianship.EndTurnIndex);
        Assert.Equal(CharacterGuardianshipEndReason.Revoked, plan.EndedGuardianship.EndReason);
        Assert.Equal(new EntityId("command:test/revoke"), plan.EndedGuardianship.EndSourceCommandId);
        Assert.Equal(
            CharacterFamilyIds.DeriveActionEventId(
                Date,
                new EntityId("command:test/revoke")),
            plan.EndedGuardianship.EndSourceEventId);
        Assert.False(plan.GuardianshipPlan.Candidate.TryGetActivePrimaryGuardianshipForWard(
            Ward,
            out _));

        state.ApplyPrepared(plan.GuardianshipPlan);

        Assert.False(query.TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        Assert.Equal(plan.EndedGuardianship, Assert.Single(query.Guardianships));
        Assert.Equal(charactersBefore, Serialize(characters.CaptureSnapshot()));
    }

    [Fact]
    public void GuardianUnavailableRequiresADeadIncapacitatedOrUnfreeGuardian()
    {
        CharacterConditionState[] unavailableConditions =
        [
            new(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
            CharacterConditionState.Default with { IsIncapacitated = true },
            CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Custodian,
            },
        ];
        for (int index = 0; index < unavailableConditions.Length; index++)
        {
            CharacterWorldState characters = CreateCharacters(
                Date,
                Seed(
                    CurrentGuardian,
                    new CampaignDate(150, 1, 1),
                    unavailableConditions[index]),
                Seed(Custodian, new CampaignDate(140, 1, 1)),
                Seed(Ward, new CampaignDate(190, 1, 1)));
            CharacterGuardianshipState active = Active(
                Ward,
                CurrentGuardian,
                Date.AddDays(-1),
                4,
                $"unavailable-{index}");
            CharacterGuardianshipWorldState state = NewState(characters, [active]);

            CharacterGuardianshipTerminationPlan plan = PrepareTermination(
                state,
                Ward,
                active.GuardianshipId,
                CharacterGuardianshipEndReason.GuardianUnavailable,
                $"end-unavailable-{index}");

            Assert.Equal(
                CharacterGuardianshipEndReason.GuardianUnavailable,
                plan.EndedGuardianship.EndReason);
        }

        CharacterWorldState eligibleCharacters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState eligibleActive = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "available-active");
        CharacterGuardianshipWorldState eligible = NewState(
            eligibleCharacters,
            [eligibleActive]);
        string before = Serialize(eligible.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => PrepareTermination(
            eligible,
            Ward,
            eligibleActive.GuardianshipId,
            CharacterGuardianshipEndReason.GuardianUnavailable,
            "guardian-still-available"));
        Assert.Equal(before, Serialize(eligible.CaptureSnapshot()));
    }

    [Fact]
    public void ExplicitTerminationRejectsStaleIdsUnsupportedReasonsAndTamperedCoordinates()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState active = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "termination-validation");
        CharacterGuardianshipWorldState state = NewState(characters, [active]);
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => PrepareTermination(
            state,
            Ward,
            new EntityId("guardianship:test/stale"),
            CharacterGuardianshipEndReason.Revoked,
            "termination-stale"));
        CharacterGuardianshipEndReason[] unsupported =
        [
            CharacterGuardianshipEndReason.WardCameOfAge,
            CharacterGuardianshipEndReason.WardDied,
            CharacterGuardianshipEndReason.GuardianDied,
            CharacterGuardianshipEndReason.Replaced,
            (CharacterGuardianshipEndReason)999,
        ];
        foreach (CharacterGuardianshipEndReason reason in unsupported)
        {
            Assert.Throws<SimulationValidationException>(() => PrepareTermination(
                state,
                Ward,
                active.GuardianshipId,
                reason,
                $"unsupported-{(int)reason}"));
        }

        EntityId commandId = new("command:test/termination-tampered-event");
        Assert.Throws<SimulationValidationException>(() => state.PrepareTermination(
            new EndPrimaryGuardianshipAction(
                Ward,
                active.GuardianshipId,
                CharacterGuardianshipEndReason.Revoked),
            Date,
            5,
            commandId,
            new EntityId("event:test/tampered")));
        Assert.Throws<SimulationValidationException>(() => state.PrepareTermination(
            new EndPrimaryGuardianshipAction(
                Ward,
                active.GuardianshipId,
                CharacterGuardianshipEndReason.Revoked),
            Date.AddDays(-1),
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date.AddDays(-1), commandId)));
        Assert.Throws<SimulationValidationException>(() => state.PrepareTermination(
            new EndPrimaryGuardianshipAction(
                Ward,
                active.GuardianshipId,
                CharacterGuardianshipEndReason.Revoked),
            Date,
            4,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId)));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void ReplacementAtomicallyEndsTheOldRecordAndCreatesOneActiveRecord()
    {
        EntityId otherWard = new("character:test/other_ward");
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(150, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(155, 1, 1)),
            Seed(Custodian, new CampaignDate(140, 1, 1)),
            Seed(
                Ward,
                new CampaignDate(190, 1, 1),
                CharacterConditionState.Default with
                {
                    IsIncapacitated = true,
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = Custodian,
                }),
            Seed(otherWard, new CampaignDate(191, 1, 1)));
        CharacterGuardianshipState active = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "replacement-active");
        CharacterGuardianshipState unrelated = Ended(
            otherWard,
            CurrentGuardian,
            Date.AddDays(-3),
            2,
            "replacement-unrelated");
        CharacterGuardianshipWorldState state = NewState(
            characters,
            [unrelated, active]);
        string charactersBefore = Serialize(characters.CaptureSnapshot());
        string guardianshipsBefore = Serialize(state.CaptureSnapshot());

        CharacterGuardianshipReplacementPlan plan = PrepareReplacement(
            state,
            Ward,
            active.GuardianshipId,
            ReplacementGuardian,
            "replace");

        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            Date,
            new EntityId("command:test/replace"));
        Assert.Equal(guardianshipsBefore, Serialize(state.CaptureSnapshot()));
        Assert.Equal(CharacterGuardianshipStatus.Ended, plan.EndedGuardianship.Status);
        Assert.Equal(CharacterGuardianshipEndReason.Replaced, plan.EndedGuardianship.EndReason);
        Assert.Equal(eventId, plan.EndedGuardianship.EndSourceEventId);
        Assert.Equal(
            CharacterGuardianshipIds.DeriveGuardianshipId(
                eventId,
                Ward,
                ReplacementGuardian),
            plan.ReplacementGuardianship.GuardianshipId);
        Assert.Equal(CharacterGuardianshipStatus.Active, plan.ReplacementGuardianship.Status);
        Assert.Equal(ReplacementGuardian, plan.ReplacementGuardianship.GuardianCharacterId);
        Assert.Equal(3, plan.GuardianshipPlan.Candidate.Guardianships.Count);
        Assert.Equal(unrelated, plan.GuardianshipPlan.Candidate.Guardianships.Single(
            item => item.GuardianshipId == unrelated.GuardianshipId));

        state.ApplyPrepared(plan.GuardianshipPlan);

        Assert.True(state.TryGetActivePrimaryGuardianshipForWard(
            Ward,
            out CharacterGuardianshipState? replacement));
        Assert.Equal(plan.ReplacementGuardianship, replacement);
        Assert.Equal(plan.EndedGuardianship, state.Guardianships.Single(
            item => item.GuardianshipId == active.GuardianshipId));
        Assert.Equal(charactersBefore, Serialize(characters.CaptureSnapshot()));
    }

    [Fact]
    public void ReplacementRevalidatesParticipantsAgeAndCurrentConditionWithoutMutation()
    {
        CharacterConditionState[] invalidReplacementConditions =
        [
            new(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
            CharacterConditionState.Default with { IsIncapacitated = true },
            CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Hostage,
                CustodianId = Custodian,
            },
        ];
        foreach (CharacterConditionState condition in invalidReplacementConditions)
        {
            AssertReplacementRejected(
                Seed(ReplacementGuardian, new CampaignDate(155, 1, 1), condition));
        }

        AssertReplacementRejected(Seed(
            ReplacementGuardian,
            new CampaignDate(185, 5, 11)));
        AssertReplacementRejected(Seed(
            ReplacementGuardian,
            new CampaignDate(191, 1, 1)));

        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(150, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(155, 1, 1)),
            Seed(Custodian, new CampaignDate(140, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState active = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "participant-validation");
        CharacterGuardianshipWorldState state = NewState(characters, [active]);
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            state,
            Ward,
            active.GuardianshipId,
            Ward,
            "replacement-self"));
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            state,
            Ward,
            active.GuardianshipId,
            CurrentGuardian,
            "replacement-same-current"));
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            state,
            Ward,
            active.GuardianshipId,
            new EntityId("character:test/missing"),
            "replacement-missing"));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));

        CharacterWorldState deadWardCharacters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(140, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(150, 1, 1)),
            Seed(
                Ward,
                new CampaignDate(190, 1, 1),
                new CharacterConditionState(
                    CharacterVitalStatus.Dead,
                    CharacterHealthStatus.Critical,
                    IsIncapacitated: true,
                    CharacterCustodyStatus.Free,
                    null)));
        CharacterGuardianshipState deadWardActive = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-2),
            3,
            "dead-ward-active");
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            NewState(deadWardCharacters, [deadWardActive]),
            Ward,
            deadWardActive.GuardianshipId,
            ReplacementGuardian,
            "dead-ward-replacement"));

        CharacterWorldState adultWardCharacters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(140, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(170, 1, 1)));
        CharacterGuardianshipState adultWardActive = Active(
            Ward,
            CurrentGuardian,
            new CampaignDate(180, 1, 1),
            1,
            "adult-ward-active");
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            NewState(adultWardCharacters, [adultWardActive]),
            Ward,
            adultWardActive.GuardianshipId,
            ReplacementGuardian,
            "adult-ward-replacement"));
    }

    [Fact]
    public void ReplacementRejectsStaleExpectedIdAndRetainedWardCapacity()
    {
        List<CharacterSeed> seeds =
        [
            Seed(CurrentGuardian, new CampaignDate(140, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)),
        ];
        CharacterGuardianshipState active = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "ward-capacity-active");
        List<CharacterGuardianshipState> retained = [active];
        for (int index = 0; index < 63; index++)
        {
            EntityId priorGuardian = new($"character:test/prior_guardian_{index:D2}");
            seeds.Add(Seed(priorGuardian, new CampaignDate(145, 1, 1)));
            retained.Add(Ended(
                Ward,
                priorGuardian,
                Date.AddDays(-3),
                2,
                $"ward-retained-{index:D2}"));
        }

        CharacterGuardianshipWorldState state = NewState(
            CreateCharacters(Date, [.. seeds]),
            retained);
        string before = Serialize(state.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            state,
            Ward,
            new EntityId("guardianship:test/stale"),
            ReplacementGuardian,
            "replacement-stale"));
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            state,
            Ward,
            active.GuardianshipId,
            ReplacementGuardian,
            "ward-at-cap"));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void ReplacementRejectsRetainedGuardianCapacityAndDeterministicIdCollision()
    {
        List<CharacterSeed> capacitySeeds =
        [
            Seed(CurrentGuardian, new CampaignDate(140, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)),
        ];
        CharacterGuardianshipState capacityActive = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "guardian-capacity-active");
        List<CharacterGuardianshipState> capacityRecords = [capacityActive];
        for (int index = 0; index < 64; index++)
        {
            EntityId priorWard = new($"character:test/prior_ward_{index:D2}");
            capacitySeeds.Add(Seed(priorWard, new CampaignDate(190, 1, 1)));
            capacityRecords.Add(Ended(
                priorWard,
                ReplacementGuardian,
                Date.AddDays(-3),
                2,
                $"guardian-retained-{index:D2}"));
        }

        CharacterGuardianshipWorldState capacityState = NewState(
            CreateCharacters(Date, [.. capacitySeeds]),
            capacityRecords);
        string capacityBefore = Serialize(capacityState.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            capacityState,
            Ward,
            capacityActive.GuardianshipId,
            ReplacementGuardian,
            "guardian-at-cap"));
        Assert.Equal(capacityBefore, Serialize(capacityState.CaptureSnapshot()));

        CharacterWorldState collisionCharacters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(140, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState collisionActive = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "collision-active");
        EntityId collisionCommand = new("command:test/replacement-collision");
        CharacterGuardianshipState colliding = Active(
            Ward,
            ReplacementGuardian,
            Date,
            5,
            collisionCommand) with
        {
            Status = CharacterGuardianshipStatus.Ended,
            EndDate = Date,
            EndTurnIndex = 5,
            EndSourceCommandId = new EntityId("command:test/collision-end"),
            EndSourceEventId = new EntityId("event:test/collision-end"),
            EndReason = CharacterGuardianshipEndReason.Revoked,
        };
        CharacterGuardianshipWorldState collisionState = NewState(
            collisionCharacters,
            [collisionActive, colliding]);
        string collisionBefore = Serialize(collisionState.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => collisionState.PrepareReplacement(
            new ReplacePrimaryGuardianshipAction(
                Ward,
                collisionActive.GuardianshipId,
                ReplacementGuardian),
            Date,
            5,
            collisionCommand,
            CharacterFamilyIds.DeriveActionEventId(Date, collisionCommand)));
        Assert.Equal(collisionBefore, Serialize(collisionState.CaptureSnapshot()));
    }

    private static void AssertReplacementRejected(CharacterSeed replacementSeed)
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(CurrentGuardian, new CampaignDate(140, 1, 1)),
            replacementSeed,
            Seed(Custodian, new CampaignDate(130, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState active = Active(
            Ward,
            CurrentGuardian,
            Date.AddDays(-1),
            4,
            "invalid-replacement-active");
        CharacterGuardianshipWorldState state = NewState(characters, [active]);
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => PrepareReplacement(
            state,
            Ward,
            active.GuardianshipId,
            replacementSeed.Id,
            "invalid-replacement"));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    private static CharacterGuardianshipTerminationPlan PrepareTermination(
        CharacterGuardianshipWorldState state,
        EntityId ward,
        EntityId expected,
        CharacterGuardianshipEndReason reason,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return state.PrepareTermination(
            new EndPrimaryGuardianshipAction(ward, expected, reason),
            Date,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));
    }

    private static CharacterGuardianshipReplacementPlan PrepareReplacement(
        CharacterGuardianshipWorldState state,
        EntityId ward,
        EntityId expected,
        EntityId replacementGuardian,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return state.PrepareReplacement(
            new ReplacePrimaryGuardianshipAction(
                ward,
                expected,
                replacementGuardian),
            Date,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));
    }

    private static CharacterGuardianshipWorldState NewState(
        CharacterWorldState characters,
        IReadOnlyList<CharacterGuardianshipState> guardianships) => new(
        new CharacterGuardianshipWorldSnapshot(
            CharacterGuardianshipContractVersions.Snapshot,
            guardianships),
        characters,
        new CampaignCalendar(Date, 5));

    private static CharacterGuardianshipState Active(
        EntityId ward,
        EntityId guardian,
        CampaignDate establishedDate,
        long establishedTurn,
        string suffix) => Active(
        ward,
        guardian,
        establishedDate,
        establishedTurn,
        new EntityId($"command:test/{suffix}"));

    private static CharacterGuardianshipState Active(
        EntityId ward,
        EntityId guardian,
        CampaignDate establishedDate,
        long establishedTurn,
        EntityId commandId)
    {
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

    private static CharacterGuardianshipState Ended(
        EntityId ward,
        EntityId guardian,
        CampaignDate establishedDate,
        long establishedTurn,
        string suffix)
    {
        CharacterGuardianshipState active = Active(
            ward,
            guardian,
            establishedDate,
            establishedTurn,
            suffix);
        return active with
        {
            Status = CharacterGuardianshipStatus.Ended,
            EndDate = establishedDate.AddDays(1),
            EndTurnIndex = establishedTurn + 1,
            EndSourceCommandId = new EntityId($"command:test/{suffix}-end"),
            EndSourceEventId = new EntityId($"event:test/{suffix}-end"),
            EndReason = CharacterGuardianshipEndReason.Revoked,
        };
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
