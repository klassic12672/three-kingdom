using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterGuardianshipWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Guardian = new("character:test/guardian");
    private static readonly EntityId Ward = new("character:test/ward");
    private static readonly EntityId Custodian = new("character:test/custodian");

    [Fact]
    public void ContractIsVersionOneDefaultEmptyAndDefensive()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipWorldState state = NewState(characters);

        Assert.Equal(1, CharacterGuardianshipContractVersions.Snapshot);
        Assert.Equal(1, CharacterGuardianshipContractVersions.State);
        Assert.Equal(1, CharacterGuardianshipContractVersions.AuthoritativeQuery);
        Assert.Equal(64, CharacterGuardianshipLimits.RetainedRecordsPerInvolvedCharacter);
        Assert.Equal("simulation.character_guardianships", CharacterGuardianshipSystem.SystemId);
        Assert.Equal(1, CharacterGuardianshipSystem.Version);
        Assert.Empty(state.Guardianships);
        Assert.Equal(
            Serialize(CharacterGuardianshipWorldSnapshot.Empty),
            Serialize(state.CaptureSnapshot()));
        Assert.False(state.TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        Assert.Empty(state.GetGuardianshipsInvolving(Guardian));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ParentChildLinkKind.Biological)]
    [InlineData(ParentChildLinkKind.LegalAdoptive)]
    public void EstablishmentAcceptsUnrelatedBiologicalAndAdoptiveGuardians(
        ParentChildLinkKind? linkKind)
    {
        CharacterParentLink[] links = linkKind is ParentChildLinkKind kind
            ? [new CharacterParentLink(Guardian, kind)]
            : [];
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1), parents: links));
        CharacterGuardianshipWorldState state = NewState(characters);

        CharacterGuardianshipEstablishmentPlan plan = Prepare(
            state,
            Guardian,
            Ward,
            null,
            $"accepted-{linkKind?.ToString().ToLowerInvariant() ?? "unrelated"}");

        Assert.Equal(CharacterGuardianshipStatus.Active, plan.Guardianship.Status);
        Assert.Equal(Guardian, plan.Guardianship.GuardianCharacterId);
        Assert.Equal(Ward, plan.Guardianship.WardCharacterId);
        Assert.Empty(state.Guardianships);
        Assert.Single(plan.GuardianshipPlan.Candidate.Guardianships);
        IAuthoritativeCharacterGuardianshipWorldQuery query = state;

        state.ApplyPrepared(plan.GuardianshipPlan);

        Assert.True(query.TryGetActivePrimaryGuardianshipForWard(
            Ward,
            out CharacterGuardianshipState? applied));
        Assert.Equal(plan.Guardianship, applied);
    }

    [Fact]
    public void EstablishmentUsesExactGuardianAndWardBirthdayBoundaries()
    {
        CharacterGuardianshipWorldState exactGuardian = NewState(CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(182, 5, 10)),
            Seed(Ward, new CampaignDate(190, 5, 10))));
        Assert.NotNull(Prepare(
            exactGuardian,
            Guardian,
            Ward,
            null,
            "guardian-exact-eighteen"));

        CharacterGuardianshipWorldState underageGuardian = NewState(CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(182, 5, 11)),
            Seed(Ward, new CampaignDate(190, 5, 10))));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            underageGuardian,
            Guardian,
            Ward,
            null,
            "guardian-underage"));

        CharacterGuardianshipWorldState wardDayBeforeAdulthood = NewState(CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 11))));
        Assert.NotNull(Prepare(
            wardDayBeforeAdulthood,
            Guardian,
            Ward,
            null,
            "ward-minor"));

        CharacterGuardianshipWorldState adultWard = NewState(CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10))));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            adultWard,
            Guardian,
            Ward,
            null,
            "ward-adult"));
    }

    [Fact]
    public void EstablishmentAllowsWardIncapacityAndCustody()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Custodian, new CampaignDate(159, 1, 1)),
            Seed(
                Ward,
                new CampaignDate(190, 1, 1),
                CharacterConditionState.Default with
                {
                    IsIncapacitated = true,
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = Custodian,
                }));

        CharacterGuardianshipEstablishmentPlan plan = Prepare(
            NewState(characters),
            Guardian,
            Ward,
            null,
            "ward-incapacitated-hostage");

        Assert.Equal(CharacterGuardianshipStatus.Active, plan.Guardianship.Status);
    }

    [Fact]
    public void EstablishmentRejectsMissingSelfAgeStaleAndIneligibleGuardian()
    {
        CharacterWorldState validCharacters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Custodian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipWorldState valid = NewState(validCharacters);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            valid,
            new EntityId("character:test/missing"),
            Ward,
            null,
            "missing"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            valid,
            Ward,
            Ward,
            null,
            "self"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            valid,
            Guardian,
            Ward,
            new EntityId("guardianship:test/stale"),
            "stale"));

        CharacterConditionState[] invalidGuardianConditions =
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
        for (int index = 0; index < invalidGuardianConditions.Length; index++)
        {
            CharacterGuardianshipWorldState state = NewState(CreateCharacters(
                Date,
                Seed(Guardian, new CampaignDate(160, 1, 1), invalidGuardianConditions[index]),
                Seed(Custodian, new CampaignDate(150, 1, 1)),
                Seed(Ward, new CampaignDate(190, 1, 1))));
            Assert.Throws<SimulationValidationException>(() => Prepare(
                state,
                Guardian,
                Ward,
                null,
                $"invalid-condition-{index}"));
        }

        CharacterGuardianshipWorldState youngerGuardian = NewState(CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(191, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1))));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            youngerGuardian,
            Guardian,
            Ward,
            null,
            "younger-guardian"));
    }

    [Fact]
    public void ExistingActiveGuardianshipRequiresExactExpectedIdButCannotBeReplacedInE1()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Custodian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState existing = Active(
            Ward,
            Custodian,
            Date.AddDays(-1),
            3,
            "existing");
        CharacterGuardianshipWorldState state = NewState(
            characters,
            [existing],
            new CampaignCalendar(Date, 4));

        Assert.Throws<SimulationValidationException>(() => Prepare(
            state,
            Guardian,
            Ward,
            null,
            "active-stale-null",
            turn: 4));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            state,
            Guardian,
            Ward,
            existing.GuardianshipId,
            "active-no-replacement",
            turn: 4));
    }

    [Fact]
    public void RetainedLimitAcceptsSixtyFourAndRejectsAdditionalPlanAndSnapshot()
    {
        List<CharacterSeed> seeds =
        [
            Seed(Guardian, new CampaignDate(150, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)),
        ];
        List<CharacterGuardianshipState> retained = [];
        for (int index = 0; index < 65; index++)
        {
            EntityId priorWard = new($"character:test/prior_ward_{index:D2}");
            seeds.Add(Seed(priorWard, new CampaignDate(190, 1, 1)));
            retained.Add(Ended(
                priorWard,
                Guardian,
                Date.AddDays(-2),
                1,
                $"retained-{index:D2}"));
        }

        CharacterWorldState characters = CreateCharacters(Date, [.. seeds]);
        CharacterGuardianshipWorldState exact = NewState(
            characters,
            retained.Take(64).ToArray(),
            new CampaignCalendar(Date, 3));
        Assert.Equal(64, exact.GetGuardianshipsInvolving(Guardian).Count);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            exact,
            Guardian,
            Ward,
            null,
            "retained-plan-cap",
            turn: 3));
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            retained,
            new CampaignCalendar(Date, 3)));
    }

    [Fact]
    public void TerminalRecordsRequireAllOrNoneCoherentEvidence()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState active = Active(
            Ward,
            Guardian,
            Date.AddDays(-2),
            1,
            "terminal-shape");
        CharacterGuardianshipState ended = Ended(
            Ward,
            Guardian,
            Date.AddDays(-2),
            1,
            "terminal-valid");

        Assert.Single(NewState(
            characters,
            [ended],
            new CampaignCalendar(Date, 3)).Guardianships);
        AssertInvalid(
            characters,
            active with { EndDate = Date },
            new CampaignCalendar(Date, 3));
        AssertInvalid(
            characters,
            ended with { EndSourceEventId = null },
            new CampaignCalendar(Date, 3));
        AssertInvalid(
            characters,
            ended with { EndTurnIndex = 0 },
            new CampaignCalendar(Date, 3));
        AssertInvalid(
            characters,
            ended with { EndReason = (CharacterGuardianshipEndReason)999 },
            new CampaignCalendar(Date, 3));
        AssertInvalid(
            characters,
            ended with { EndDate = Date.AddDays(1) },
            new CampaignCalendar(Date, 3));
    }

    [Fact]
    public void RestoreDoesNotCouplePersistedGuardianshipToCurrentMinorityOrCondition()
    {
        CampaignDate laterDate = new(220, 5, 10);
        CharacterWorldState laterCharacters = CreateCharacters(
            laterDate,
            Seed(
                Guardian,
                new CampaignDate(160, 1, 1),
                CharacterConditionState.Default with { IsIncapacitated = true }),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState historical = Active(
            Ward,
            Guardian,
            Date,
            2,
            "historical-active");

        CharacterGuardianshipWorldState restored = NewState(
            laterCharacters,
            [historical],
            new CampaignCalendar(laterDate, 20));

        Assert.Equal(historical, Assert.Single(restored.Guardianships));
        Assert.True(restored.TryGetActivePrimaryGuardianshipForWard(Ward, out _));
    }

    [Fact]
    public void ConstructionCanonicalizesAndQueriesAreDefensive()
    {
        EntityId secondWard = new("character:test/second_ward");
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)),
            Seed(secondWard, new CampaignDate(191, 1, 1)));
        CharacterGuardianshipState first = Ended(
            Ward,
            Guardian,
            Date.AddDays(-3),
            0,
            "z-record");
        CharacterGuardianshipState second = Active(
            secondWard,
            Guardian,
            Date.AddDays(-1),
            2,
            "a-record");
        CampaignCalendar calendar = new(Date, 3);
        CharacterGuardianshipWorldState ordered = NewState(
            characters,
            [first, second],
            calendar);
        CharacterGuardianshipWorldState shuffled = NewState(
            characters,
            [second, first],
            calendar);

        Assert.Equal(
            Serialize(ordered.CaptureSnapshot()),
            Serialize(shuffled.CaptureSnapshot()));
        Assert.Equal(
            ordered.Guardianships.Select(item => item.GuardianshipId),
            ordered.Guardianships.Select(item => item.GuardianshipId).Order());
        CharacterGuardianshipState[] exposed = Assert.IsType<
            CharacterGuardianshipState[]>(ordered.Guardianships);
        exposed[0] = exposed[0] with { Status = CharacterGuardianshipStatus.Active };
        Assert.Equal(2, ordered.Guardianships.Count);
        Assert.Single(ordered.GetGuardianshipsInvolving(Ward));
        Assert.Equal(2, ordered.GetGuardianshipsInvolving(Guardian).Count);
        Assert.True(ordered.TryGetActivePrimaryGuardianshipForWard(
            secondWard,
            out CharacterGuardianshipState? active));
        Assert.Equal(second.GuardianshipId, active.GuardianshipId);
    }

    [Fact]
    public void StableIdentityAndSourceCoordinatesAreExact()
    {
        EntityId commandId = new("command:test/guardianship-golden");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        EntityId guardianshipId = CharacterGuardianshipIds.DeriveGuardianshipId(
            eventId,
            Ward,
            Guardian);
        Assert.Equal(
            "event:sha256/dc9808b24236ebdfd1361fc336d64fe7dcbcb230a73e74945c740ce4579bd153",
            eventId.Value);
        Assert.Equal(
            "guardianship:sha256/75e59b22a8f8e6c880ab52beaa78e56498517f5f894f00aa40133ac50c84eacc",
            guardianshipId.Value);

        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState valid = Active(Ward, Guardian, Date, 3, "identity");
        AssertInvalid(
            characters,
            valid with { SourceEventId = eventId },
            new CampaignCalendar(Date, 3));
        AssertInvalid(
            characters,
            valid with { GuardianshipId = guardianshipId },
            new CampaignCalendar(Date, 3));
        AssertInvalid(
            characters,
            valid with { EstablishedTurnIndex = 4 },
            new CampaignCalendar(Date, 3));
    }

    [Fact]
    public void ConstructionRejectsNullVersionsParticipantsAndDuplicateActiveWard()
    {
        CharacterWorldState characters = CreateCharacters(
            Date,
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Custodian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CampaignCalendar calendar = new(Date, 3);
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterGuardianshipWorldState(null!, characters, calendar));
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterGuardianshipWorldState(
                CharacterGuardianshipWorldSnapshot.Empty,
                null!,
                calendar));
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterGuardianshipWorldState(
                CharacterGuardianshipWorldSnapshot.Empty,
                characters,
                default));
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            CharacterGuardianshipWorldSnapshot.Empty with { ContractVersion = 2 },
            calendar));
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            CharacterGuardianshipWorldSnapshot.Empty with { Guardianships = null! },
            calendar));

        CharacterGuardianshipState first = Active(
            Ward,
            Guardian,
            Date.AddDays(-2),
            1,
            "active-first");
        CharacterGuardianshipState second = Active(
            Ward,
            Custodian,
            Date.AddDays(-1),
            2,
            "active-second");
        Assert.Throws<SimulationValidationException>(() => NewState(
            characters,
            [first, second],
            calendar));
        AssertInvalid(
            characters,
            first with { ContractVersion = 2 },
            calendar);
        AssertInvalid(
            characters,
            first with { GuardianCharacterId = Ward },
            calendar);
    }

    private static CharacterGuardianshipEstablishmentPlan Prepare(
        CharacterGuardianshipWorldState state,
        EntityId guardian,
        EntityId ward,
        EntityId? expected,
        string suffix,
        long turn = 0)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return state.PrepareEstablishment(
            new EstablishPrimaryGuardianshipAction(guardian, ward, expected),
            Date,
            turn,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));
    }

    private static CharacterGuardianshipWorldState NewState(
        CharacterWorldState characters,
        IReadOnlyList<CharacterGuardianshipState>? guardianships = null,
        CampaignCalendar? calendar = null) => new(
        new CharacterGuardianshipWorldSnapshot(
            CharacterGuardianshipContractVersions.Snapshot,
            guardianships ?? []),
        characters,
        calendar ?? new CampaignCalendar(Date, 0));

    private static CharacterGuardianshipWorldState NewState(
        CharacterWorldState characters,
        CharacterGuardianshipWorldSnapshot snapshot,
        CampaignCalendar calendar) => new(snapshot, characters, calendar);

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

    private static void AssertInvalid(
        CharacterWorldState characters,
        CharacterGuardianshipState guardianship,
        CampaignCalendar calendar) => Assert.Throws<SimulationValidationException>(() =>
        NewState(characters, [guardianship], calendar));

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
            .Select(seed =>
            {
                CharacterParentLink[] parents = seed.Parents
                    .OrderBy(link => link.ParentCharacterId)
                    .ThenBy(link => link.Kind)
                    .ToArray();
                return new CharacterState(
                    CharacterContractVersions.State,
                    seed.Id,
                    parents.Select(link => link.ParentCharacterId).ToArray(),
                    parents,
                    seed.Condition);
            })
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
        CharacterConditionState? condition = null,
        IReadOnlyList<CharacterParentLink>? parents = null) => new(
        id,
        birthDate,
        condition ?? CharacterConditionState.Default,
        parents ?? []);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition,
        IReadOnlyList<CharacterParentLink> Parents);
}
