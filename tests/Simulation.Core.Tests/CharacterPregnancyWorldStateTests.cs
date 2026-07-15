using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterPregnancyWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId GestationalParent = new("character:test/gestational_parent");
    private static readonly EntityId OtherParent = new("character:test/other_biological_parent");
    private static readonly EntityId SecondGestationalParent = new("character:test/second_gestational_parent");
    private static readonly EntityId SecondOtherParent = new("character:test/second_other_parent");
    private static readonly EntityId UnionId = new("marriage_union:test/pregnancy_source");
    private static readonly EntityId SecondUnionId = new("marriage_union:test/pregnancy_source_second");

    [Fact]
    public void ContractIsVersionOneDefaultEmptyCanonicalAndDefensive()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent));
        TestMarriageQuery marriages = new([
            Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-2), 3),
        ]);
        CharacterPregnancyWorldState empty = NewState(characters, marriages, []);

        Assert.Equal(1, CharacterPregnancyContractVersions.Snapshot);
        Assert.Equal(1, CharacterPregnancyContractVersions.State);
        Assert.Equal(1, CharacterPregnancyContractVersions.AuthoritativeQuery);
        Assert.Equal(280, CharacterPregnancyLimits.GestationDays);
        Assert.Equal("simulation.character_pregnancies", CharacterPregnancySystem.SystemId);
        Assert.Equal(1, CharacterPregnancySystem.Version);
        Assert.Empty(empty.ActivePregnancies);
        Assert.False(empty.TryGetActivePregnancyForGestationalParent(
            GestationalParent,
            out _));
        Assert.False(empty.TryGetActivePregnancyForUnion(UnionId, out _));
        Assert.Empty(empty.GetActivePregnanciesInvolving(OtherParent));
        Assert.Equal(
            Serialize(CharacterPregnancyWorldSnapshot.Empty),
            Serialize(empty.CaptureSnapshot()));

        CharacterPregnancyState first = Pregnancy(
            "canonical-z",
            GestationalParent,
            OtherParent,
            UnionId,
            Date.AddDays(-1),
            4);
        CharacterWorldState fourCharacters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent),
            Adult(SecondGestationalParent),
            Adult(SecondOtherParent));
        TestMarriageQuery twoMarriages = new([
            Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-2), 3),
            Union(
                SecondUnionId,
                SecondGestationalParent,
                SecondOtherParent,
                Date.AddDays(-2),
                3),
        ]);
        CharacterPregnancyState second = Pregnancy(
            "canonical-a",
            SecondGestationalParent,
            SecondOtherParent,
            SecondUnionId,
            Date.AddDays(-1),
            4);
        CharacterPregnancyWorldState ordered = NewState(
            fourCharacters,
            twoMarriages,
            [first, second]);
        CharacterPregnancyWorldState shuffled = NewState(
            fourCharacters,
            twoMarriages,
            [second, first]);

        Assert.Equal(
            Serialize(ordered.CaptureSnapshot()),
            Serialize(shuffled.CaptureSnapshot()));
        CharacterPregnancyState[] exposed = Assert.IsType<CharacterPregnancyState[]>(
            ordered.ActivePregnancies);
        exposed[0] = exposed[0] with { StartTurnIndex = 0 };
        Assert.Equal(4, ordered.ActivePregnancies[0].StartTurnIndex);
        Assert.True(ordered.TryGetActivePregnancyForGestationalParent(
            GestationalParent,
            out CharacterPregnancyState? queried));
        Assert.Equal(first, queried);
        Assert.True(ordered.TryGetActivePregnancyForUnion(
            UnionId,
            out CharacterPregnancyState? unionPregnancy));
        Assert.Equal(first, unionPregnancy);
        Assert.Equal(first, Assert.Single(ordered.GetActivePregnanciesInvolving(OtherParent)));
    }

    [Fact]
    public void RegistrationCreatesAnImmutableCandidateWithExactDatesSourcesAndAffectedIds()
    {
        CharacterConditionState unrestrictedLivingCondition = CharacterConditionState.Default with
        {
            IsIncapacitated = true,
            CustodyStatus = CharacterCustodyStatus.Captive,
            CustodianId = SecondOtherParent,
        };
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent, unrestrictedLivingCondition),
            Adult(OtherParent),
            Adult(SecondOtherParent));
        MarriageUnionState union = Union(
            UnionId,
            OtherParent,
            GestationalParent,
            Date.AddDays(-2),
            3,
            consent: MarriageConsentKind.Coerced);
        TestMarriageQuery marriages = new([union]);
        CharacterPregnancyWorldState state = NewState(characters, marriages, []);
        string charactersBefore = Serialize(characters.CaptureSnapshot());
        string marriagesBefore = Serialize(marriages.Unions);
        string pregnanciesBefore = Serialize(state.CaptureSnapshot());

        CharacterPregnancyRegistrationPlan plan = Prepare(
            state,
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "register");

        EntityId commandId = new("command:test/register");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        Assert.Equal(pregnanciesBefore, Serialize(state.CaptureSnapshot()));
        Assert.Equal(GestationalParent, plan.Pregnancy.GestationalParentCharacterId);
        Assert.Equal(OtherParent, plan.Pregnancy.OtherBiologicalParentCharacterId);
        Assert.Equal(UnionId, plan.Pregnancy.SourceUnionId);
        Assert.Equal(Date, plan.Pregnancy.StartDate);
        Assert.Equal(Date.AddDays(280), plan.Pregnancy.ExpectedBirthDate);
        Assert.Equal(5, plan.Pregnancy.StartTurnIndex);
        Assert.Equal(commandId, plan.Pregnancy.SourceCommandId);
        Assert.Equal(eventId, plan.Pregnancy.SourceEventId);
        Assert.Equal(
            CharacterPregnancyIds.DerivePregnancyId(
                eventId,
                GestationalParent,
                OtherParent,
                UnionId),
            plan.Pregnancy.PregnancyId);
        Assert.Equal(
            new[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                plan.Pregnancy.PregnancyId,
                GestationalParent,
                OtherParent,
                UnionId,
            }.Order(),
            plan.AffectedIds);
        Assert.Single(plan.PregnancyPlan.Candidate.ActivePregnancies);

        state.ApplyPrepared(plan.PregnancyPlan);

        Assert.Equal(plan.Pregnancy, Assert.Single(state.ActivePregnancies));
        Assert.Equal(charactersBefore, Serialize(characters.CaptureSnapshot()));
        Assert.Equal(marriagesBefore, Serialize(marriages.Unions));
    }

    [Fact]
    public void ExplicitParentRolesAreNotInferredAndProduceDifferentStableIdentities()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent));
        TestMarriageQuery marriages = new([
            Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-2), 3),
        ]);
        CharacterPregnancyRegistrationPlan first = Prepare(
            NewState(characters, marriages, []),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "explicit-roles");
        CharacterPregnancyRegistrationPlan reversed = Prepare(
            NewState(characters, marriages, []),
            OtherParent,
            GestationalParent,
            UnionId,
            null,
            "explicit-roles");

        Assert.Equal(GestationalParent, first.Pregnancy.GestationalParentCharacterId);
        Assert.Equal(OtherParent, reversed.Pregnancy.GestationalParentCharacterId);
        Assert.NotEqual(first.Pregnancy.PregnancyId, reversed.Pregnancy.PregnancyId);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            NewState(characters, marriages, []),
            GestationalParent,
            GestationalParent,
            UnionId,
            null,
            "self-role"));
    }

    [Fact]
    public void FixedGestationHandlesLeapDatesAndRejectsCalendarOverflow()
    {
        CampaignDate leapStart = new(2000, 2, 29);
        CharacterWorldState leapCharacters = CreateCharacters(
            leapStart,
            Seed(GestationalParent, new CampaignDate(1970, 1, 1)),
            Seed(OtherParent, new CampaignDate(1969, 1, 1)));
        TestMarriageQuery leapMarriages = new([
            Union(
                UnionId,
                GestationalParent,
                OtherParent,
                leapStart.AddDays(-1),
                4),
        ]);
        CharacterPregnancyRegistrationPlan leap = Prepare(
            NewState(leapCharacters, leapMarriages, [], leapStart),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "leap",
            leapStart);
        Assert.Equal(new CampaignDate(2000, 12, 5), leap.Pregnancy.ExpectedBirthDate);

        CampaignDate finalDate = new(9999, 12, 31);
        CharacterWorldState finalCharacters = CreateCharacters(
            finalDate,
            Seed(GestationalParent, new CampaignDate(9900, 1, 1)),
            Seed(OtherParent, new CampaignDate(9900, 1, 2)));
        TestMarriageQuery finalMarriages = new([
            Union(
                UnionId,
                GestationalParent,
                OtherParent,
                finalDate.AddDays(-1),
                4),
        ]);
        CharacterPregnancyWorldState finalState = NewState(
            finalCharacters,
            finalMarriages,
            [],
            finalDate);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            finalState,
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "overflow",
            finalDate));
        Assert.Empty(finalState.ActivePregnancies);
    }

    [Fact]
    public void RegistrationRequiresKnownBornLivingAdultParentsAndExactActiveUnion()
    {
        CharacterConditionState dead = new(
            CharacterVitalStatus.Dead,
            CharacterHealthStatus.Critical,
            IsIncapacitated: true,
            CharacterCustodyStatus.Free,
            null);
        CharacterSeed[] invalidGestationalParents =
        [
            Seed(GestationalParent, new CampaignDate(182, 5, 11)),
            Adult(GestationalParent, dead),
        ];
        foreach (CharacterSeed invalid in invalidGestationalParents)
        {
            CharacterWorldState characters = CreateCharacters(
                Date,
                invalid,
                Adult(OtherParent));
            TestMarriageQuery marriages = new([
                Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-2), 3),
            ]);
            CharacterPregnancyWorldState state = NewState(characters, marriages, []);
            Assert.Throws<SimulationValidationException>(() => Prepare(
                state,
                GestationalParent,
                OtherParent,
                UnionId,
                null,
                "invalid-parent"));
            Assert.Empty(state.ActivePregnancies);
        }

        CharacterWorldState validCharacters = CreateCharacters(
            Date,
            Seed(GestationalParent, new CampaignDate(182, 5, 10)),
            Adult(OtherParent),
            Adult(SecondOtherParent));
        MarriageUnionState active = Union(
            UnionId,
            GestationalParent,
            OtherParent,
            Date.AddDays(-2),
            3);
        Assert.NotNull(Prepare(
            NewState(validCharacters, new TestMarriageQuery([active]), []),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "exact-eighteen"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            NewState(validCharacters, new TestMarriageQuery([]), []),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "missing-union"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            NewState(
                validCharacters,
                new TestMarriageQuery([
                    Union(
                        UnionId,
                        GestationalParent,
                        SecondOtherParent,
                        Date.AddDays(-2),
                        3),
                ]),
                []),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "wrong-pair"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            NewState(
                validCharacters,
                new TestMarriageQuery([
                    active with
                    {
                        Status = MarriageUnionStatus.Ended,
                        EndDate = Date,
                        EndTurnIndex = 5,
                        EndCommandId = new EntityId("command:test/union-ended"),
                        EndReason = MarriageUnionEndReason.Annulled,
                    },
                ]),
                []),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "ended-union"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            NewState(
                validCharacters,
                new TestMarriageQuery([
                    active with { StartDate = Date.AddDays(1), StartTurnIndex = 6 },
                ]),
                []),
            GestationalParent,
            OtherParent,
            UnionId,
            null,
            "future-union"));
    }

    [Fact]
    public void ActiveCapacityIsOnePerGestationalParentAndOnePerUnion()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent),
            Adult(SecondGestationalParent),
            Adult(SecondOtherParent));
        MarriageUnionState firstUnion = Union(
            UnionId,
            GestationalParent,
            OtherParent,
            Date.AddDays(-3),
            2);
        MarriageUnionState secondUnion = Union(
            SecondUnionId,
            GestationalParent,
            SecondOtherParent,
            Date.AddDays(-3),
            2);
        TestMarriageQuery marriages = new([firstUnion, secondUnion]);
        CharacterPregnancyState current = Pregnancy(
            "capacity-current",
            GestationalParent,
            OtherParent,
            UnionId,
            Date.AddDays(-1),
            4);
        CharacterPregnancyWorldState state = NewState(characters, marriages, [current]);
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => Prepare(
            state,
            GestationalParent,
            SecondOtherParent,
            SecondUnionId,
            null,
            "capacity-stale-null"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            state,
            GestationalParent,
            SecondOtherParent,
            SecondUnionId,
            current.PregnancyId,
            "capacity-exact"));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));

        CharacterPregnancyState sameUnionOtherGestational = Pregnancy(
            "capacity-same-union",
            OtherParent,
            GestationalParent,
            UnionId,
            Date.AddDays(-1),
            4);
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            marriages,
            [current, sameUnionOtherGestational]));

        CharacterPregnancyState sameGestationalOtherUnion = Pregnancy(
            "capacity-same-parent",
            GestationalParent,
            SecondOtherParent,
            SecondUnionId,
            Date.AddDays(-1),
            4);
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            marriages,
            [current, sameGestationalOtherUnion]));
    }

    [Fact]
    public void RestoreRejectsEndedSourceUnionAndEitherDeadCurrentParent()
    {
        CharacterConditionState deceased = new(
            CharacterVitalStatus.Dead,
            CharacterHealthStatus.Critical,
            IsIncapacitated: true,
            CharacterCustodyStatus.Free,
            null);
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent));
        CharacterPregnancyState pregnancy = Pregnancy(
            "historical-union",
            GestationalParent,
            OtherParent,
            UnionId,
            Date.AddDays(-2),
            3);
        MarriageUnionState activeUnion = Union(
            UnionId,
            GestationalParent,
            OtherParent,
            Date.AddDays(-5),
            1);

        CharacterPregnancyWorldState restored = NewState(
            characters,
            new TestMarriageQuery([activeUnion]),
            [pregnancy]);
        Assert.Equal(pregnancy, Assert.Single(restored.ActivePregnancies));

        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            new TestMarriageQuery([
                activeUnion with
                {
                    Status = MarriageUnionStatus.Ended,
                    EndDate = Date.AddDays(-1),
                    EndTurnIndex = 4,
                    EndCommandId = new EntityId("command:test/historical-union-end"),
                    EndReason = MarriageUnionEndReason.Separated,
                },
            ]),
            [pregnancy]));
        foreach (CharacterWorldState deadParentState in new[]
        {
            CreateCharacters(
                Date,
                Adult(GestationalParent, deceased),
                Adult(OtherParent)),
            CreateCharacters(
                Date,
                Adult(GestationalParent),
                Adult(OtherParent, deceased)),
        })
        {
            Assert.Throws<SimulationValidationException>(() => NewState(
                deadParentState,
                new TestMarriageQuery([activeUnion]),
                [pregnancy]));
        }
    }

    [Fact]
    public void SnapshotRejectsMalformedVersionsDatesSourcesAgesAndDuplicates()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent),
            Adult(SecondGestationalParent),
            Adult(SecondOtherParent));
        TestMarriageQuery marriages = new([
            Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-3), 2),
            Union(
                SecondUnionId,
                SecondGestationalParent,
                SecondOtherParent,
                Date.AddDays(-3),
                2),
        ]);
        CharacterPregnancyState valid = Pregnancy(
            "snapshot-valid",
            GestationalParent,
            OtherParent,
            UnionId,
            Date.AddDays(-1),
            4);

        Assert.Throws<SimulationValidationException>(() => new CharacterPregnancyWorldState(
            null!,
            characters,
            marriages,
            new CampaignCalendar(Date, 5)));
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            marriages,
            CharacterPregnancyWorldSnapshot.Empty with { ContractVersion = 2 }));
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            marriages,
            CharacterPregnancyWorldSnapshot.Empty with { ActivePregnancies = null! }));
        AssertInvalid(characters, marriages, valid with { ContractVersion = 2 });
        AssertInvalid(
            characters,
            marriages,
            valid with { PregnancyId = new EntityId("pregnancy:test/wrong") });
        AssertInvalid(
            characters,
            marriages,
            valid with { ExpectedBirthDate = valid.ExpectedBirthDate.AddDays(1) });
        AssertInvalid(
            characters,
            marriages,
            valid with { SourceEventId = new EntityId("event:test/wrong") });
        AssertInvalid(
            characters,
            marriages,
            valid with { StartTurnIndex = 6 });
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            marriages,
            [valid, valid]));

        CharacterPregnancyState second = Pregnancy(
            "snapshot-second",
            SecondGestationalParent,
            SecondOtherParent,
            SecondUnionId,
            Date.AddDays(-1),
            4);
        _ = NewState(characters, marriages, [second, valid]);

        CharacterWorldState underageCharacters = CreateCharacters(
            Date,
            Seed(GestationalParent, new CampaignDate(190, 1, 1)),
            Adult(OtherParent));
        AssertInvalid(
            underageCharacters,
            new TestMarriageQuery([
                Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-3), 2),
            ]),
            valid);
    }

    [Fact]
    public void RegistrationRejectsStaleTamperedAndCollidingInputsWithoutMutation()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Adult(GestationalParent),
            Adult(OtherParent));
        TestMarriageQuery marriages = new([
            Union(UnionId, GestationalParent, OtherParent, Date.AddDays(-3), 2),
        ]);
        CharacterPregnancyWorldState empty = NewState(characters, marriages, []);
        string before = Serialize(empty.CaptureSnapshot());
        RegisterActivePregnancyAction action = new(
            GestationalParent,
            OtherParent,
            UnionId,
            null);
        EntityId commandId = new("command:test/tamper");

        Assert.Throws<SimulationValidationException>(() => empty.PrepareRegistration(
            new EntityId("system:test/not_family"),
            action,
            Date,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId)));
        Assert.Throws<SimulationValidationException>(() => empty.PrepareRegistration(
            CharacterFamilySystem.AuthoritativeActorId,
            action,
            Date,
            5,
            commandId,
            new EntityId("event:test/tampered")));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            empty,
            GestationalParent,
            OtherParent,
            UnionId,
            new EntityId("pregnancy:test/stale"),
            "stale"));
        Assert.Equal(before, Serialize(empty.CaptureSnapshot()));

        CharacterPregnancyState existing = Pregnancy(
            "collision",
            GestationalParent,
            OtherParent,
            UnionId,
            Date,
            5);
        CharacterPregnancyWorldState collision = NewState(
            characters,
            marriages,
            [existing]);
        string collisionBefore = Serialize(collision.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => collision.PrepareRegistration(
            CharacterFamilySystem.AuthoritativeActorId,
            new RegisterActivePregnancyAction(
                GestationalParent,
                OtherParent,
                UnionId,
                existing.PregnancyId),
            Date,
            5,
            existing.SourceCommandId,
            existing.SourceEventId));
        Assert.Equal(collisionBefore, Serialize(collision.CaptureSnapshot()));
    }

    [Fact]
    public void StableIdentityIsGoldenAndRoleOrderSensitive()
    {
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            Date,
            new EntityId("command:test/pregnancy-golden"));
        EntityId pregnancyId = CharacterPregnancyIds.DerivePregnancyId(
            eventId,
            GestationalParent,
            OtherParent,
            UnionId);
        EntityId reversed = CharacterPregnancyIds.DerivePregnancyId(
            eventId,
            OtherParent,
            GestationalParent,
            UnionId);

        Assert.Equal(
            "pregnancy:sha256/c11636b0b7aefc847a254aafe433fa6d444813cb4921f3207e724a1431ace4c4",
            pregnancyId.Value);
        Assert.NotEqual(pregnancyId, reversed);
    }

    private static CharacterPregnancyRegistrationPlan Prepare(
        CharacterPregnancyWorldState state,
        EntityId gestationalParent,
        EntityId otherParent,
        EntityId unionId,
        EntityId? expected,
        string suffix,
        CampaignDate? startDate = null)
    {
        CampaignDate date = startDate ?? Date;
        EntityId commandId = new($"command:test/{suffix}");
        return state.PrepareRegistration(
            CharacterFamilySystem.AuthoritativeActorId,
            new RegisterActivePregnancyAction(
                gestationalParent,
                otherParent,
                unionId,
                expected),
            date,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(date, commandId));
    }

    private static CharacterPregnancyWorldState NewState(
        CharacterWorldState characters,
        TestMarriageQuery marriages,
        IReadOnlyList<CharacterPregnancyState> pregnancies,
        CampaignDate? date = null) => new(
        new CharacterPregnancyWorldSnapshot(
            CharacterPregnancyContractVersions.Snapshot,
            pregnancies),
        characters,
        marriages,
        new CampaignCalendar(date ?? Date, 5));

    private static CharacterPregnancyWorldState NewState(
        CharacterWorldState characters,
        TestMarriageQuery marriages,
        CharacterPregnancyWorldSnapshot snapshot) => new(
        snapshot,
        characters,
        marriages,
        new CampaignCalendar(Date, 5));

    private static void AssertInvalid(
        CharacterWorldState characters,
        TestMarriageQuery marriages,
        CharacterPregnancyState pregnancy) => Assert.Throws<
        SimulationValidationException>(() => NewState(characters, marriages, [pregnancy]));

    private static CharacterPregnancyState Pregnancy(
        string suffix,
        EntityId gestationalParent,
        EntityId otherParent,
        EntityId unionId,
        CampaignDate startDate,
        long startTurn)
    {
        EntityId commandId = new($"command:test/{suffix}");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(startDate, commandId);
        return new CharacterPregnancyState(
            CharacterPregnancyContractVersions.State,
            CharacterPregnancyIds.DerivePregnancyId(
                eventId,
                gestationalParent,
                otherParent,
                unionId),
            gestationalParent,
            otherParent,
            unionId,
            startDate,
            startDate.AddDays(CharacterPregnancyLimits.GestationDays),
            startTurn,
            commandId,
            eventId);
    }

    private static MarriageUnionState Union(
        EntityId unionId,
        EntityId first,
        EntityId second,
        CampaignDate startDate,
        long startTurn,
        MarriageUnionStatus status = MarriageUnionStatus.Active,
        MarriageConsentKind consent = MarriageConsentKind.Voluntary) => new(
        CharacterMarriageContractVersions.State,
        unionId,
        first.CompareTo(second) < 0 ? first : second,
        first.CompareTo(second) < 0 ? second : first,
        MarriageUnionForm.PrincipalSpouse,
        null,
        MarriageBasis.Political,
        consent,
        new EntityId("marriage_practice:test/pregnancy"),
        new EntityId($"marriage_proposal:test/{unionId.Value.Replace(':', '/')}"),
        startDate,
        startTurn,
        status,
        null,
        null,
        null,
        null);

    private static CharacterSeed Adult(
        EntityId id,
        CharacterConditionState? condition = null) => Seed(
        id,
        new CampaignDate(170, 1, 1),
        condition);

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        CharacterConditionState? condition = null) => new(
        id,
        birthDate,
        condition ?? CharacterConditionState.Default);

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

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);

    private sealed class TestMarriageQuery : IAuthoritativeCharacterMarriageWorldQuery
    {
        private readonly SortedDictionary<EntityId, MarriageUnionState> unions;

        public TestMarriageQuery(IReadOnlyList<MarriageUnionState> unions)
        {
            this.unions = new SortedDictionary<EntityId, MarriageUnionState>(
                unions.ToDictionary(item => item.UnionId, item => item with { }));
        }

        public IReadOnlyList<MarriagePracticeState> Practices => [];

        public IReadOnlyList<MarriageProposalState> Proposals => [];

        public IReadOnlyList<PoliticalBetrothalState> Betrothals => [];

        public IReadOnlyList<MarriageUnionState> Unions =>
            unions.Values.Select(item => item with { }).ToArray();

        public IReadOnlyList<RomanceRouteState> RomanceRoutes => [];

        public IReadOnlyList<RomanceInvitationState> RomanceInvitations => [];

        public IReadOnlyList<CharacterMarriageHistoryAggregate> History => [];

        public bool TryGetPractice(
            EntityId practiceId,
            [NotNullWhen(true)] out MarriagePracticeState? practice)
        {
            practice = null;
            return false;
        }

        public bool TryGetProposal(
            EntityId proposalId,
            [NotNullWhen(true)] out MarriageProposalState? proposal)
        {
            proposal = null;
            return false;
        }

        public bool TryGetBetrothal(
            EntityId betrothalId,
            [NotNullWhen(true)] out PoliticalBetrothalState? betrothal)
        {
            betrothal = null;
            return false;
        }

        public bool TryGetUnion(
            EntityId unionId,
            [NotNullWhen(true)] out MarriageUnionState? union)
        {
            if (unions.TryGetValue(unionId, out MarriageUnionState? stored))
            {
                union = stored with { };
                return true;
            }

            union = null;
            return false;
        }

        public bool TryGetRomanceRoute(
            EntityId routeId,
            [NotNullWhen(true)] out RomanceRouteState? route)
        {
            route = null;
            return false;
        }

        public bool TryGetRomanceInvitation(
            EntityId invitationId,
            [NotNullWhen(true)] out RomanceInvitationState? invitation)
        {
            invitation = null;
            return false;
        }

        public bool TryGetHistory(
            EntityId characterId,
            [NotNullWhen(true)] out CharacterMarriageHistoryAggregate? history)
        {
            history = null;
            return false;
        }

        public IReadOnlyList<MarriageProposalState> GetProposalsInvolving(
            EntityId characterId) => [];

        public IReadOnlyList<PoliticalBetrothalState> GetBetrothalsInvolving(
            EntityId characterId) => [];

        public IReadOnlyList<MarriageUnionState> GetUnionsInvolving(
            EntityId characterId) => unions.Values
            .Where(item => item.FirstCharacterId == characterId
                || item.SecondCharacterId == characterId)
            .Select(item => item with { })
            .ToArray();

        public IReadOnlyList<RomanceRouteState> GetRomanceRoutesInvolving(
            EntityId characterId) => [];

        public IReadOnlyList<RomanceInvitationState> GetRomanceInvitationsInvolving(
            EntityId characterId) => [];

        public MarriageEligibilityResult EvaluateEligibility(
            MarriageEligibilityRequest request,
            CampaignDate date) => new(
            CharacterMarriageContractVersions.Eligibility,
            true,
            []);
    }
}
