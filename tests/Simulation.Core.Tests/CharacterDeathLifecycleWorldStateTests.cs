using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterDeathLifecycleWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 6, 10);
    private static readonly EntityId Target = new("character:test/death_target");
    private static readonly EntityId Guardian = new("character:test/death_guardian");
    private static readonly EntityId WardA = new("character:test/death_ward_a");
    private static readonly EntityId WardB = new("character:test/death_ward_b");
    private static readonly EntityId PartnerA = new("character:test/death_partner_a");
    private static readonly EntityId PartnerB = new("character:test/death_partner_b");
    private static readonly EntityId OtherA = new("character:test/death_other_a");
    private static readonly EntityId OtherB = new("character:test/death_other_b");
    private static readonly EntityId UnionA = new("marriage_union:test/death_a");
    private static readonly EntityId UnionB = new("marriage_union:test/death_b");
    private static readonly EntityId UnionC = new("marriage_union:test/death_c");
    private static readonly CampaignCalendar Calendar = new(Date, 10);

    [Fact]
    public void GuardianshipDeathHandlesZeroAndOneWardRecordWithoutMutatingSource()
    {
        CharacterWorldState current = Characters(
            Alive(Target, new CampaignDate(190, 1, 1)),
            Alive(Guardian, new CampaignDate(160, 1, 1)));
        CharacterWorldState candidateCharacters = Characters(
            Dead(Target, new CampaignDate(190, 1, 1)),
            Alive(Guardian, new CampaignDate(160, 1, 1)));
        CharacterGuardianshipWorldState empty = Guardianships(current, []);

        CharacterGuardianshipDeathPlan emptyPlan = PrepareGuardianshipDeath(
            empty,
            candidateCharacters,
            "guardianship-zero");

        Assert.Empty(emptyPlan.EndedGuardianships);
        Assert.Empty(emptyPlan.GuardianshipPlan.Candidate.Guardianships);
        Assert.Empty(empty.Guardianships);

        CharacterGuardianshipState active = ActiveGuardianship(
            Target,
            Guardian,
            "ward-death");
        CharacterGuardianshipWorldState one = Guardianships(current, [active]);
        string before = Serialize(one.CaptureSnapshot());

        CharacterGuardianshipDeathPlan onePlan = PrepareGuardianshipDeath(
            one,
            candidateCharacters,
            "guardianship-one");

        Assert.Equal(before, Serialize(one.CaptureSnapshot()));
        CharacterGuardianshipState ended = Assert.Single(onePlan.EndedGuardianships);
        Assert.Equal(active.GuardianshipId, ended.GuardianshipId);
        Assert.Equal(CharacterGuardianshipStatus.Ended, ended.Status);
        Assert.Equal(CharacterGuardianshipEndReason.WardDied, ended.EndReason);
        Assert.Equal(Date, ended.EndDate);
        Assert.Equal(Calendar.TurnIndex, ended.EndTurnIndex);
        Assert.Equal(
            CharacterConditionIds.DeriveActionEventId(
                Date,
                ended.EndSourceCommandId!.Value),
            ended.EndSourceEventId);
        Assert.Equal(
            ended,
            Assert.Single(onePlan.GuardianshipPlan.Candidate.Guardianships));
    }

    [Fact]
    public void GuardianshipDeathEndsEveryActiveGuardianRecordCanonicallyOnce()
    {
        CharacterWorldState current = Characters(
            Alive(Target, new CampaignDate(160, 1, 1)),
            Alive(WardA, new CampaignDate(190, 1, 1)),
            Alive(WardB, new CampaignDate(191, 1, 1)));
        CharacterWorldState candidateCharacters = Characters(
            Dead(Target, new CampaignDate(160, 1, 1)),
            Alive(WardA, new CampaignDate(190, 1, 1)),
            Alive(WardB, new CampaignDate(191, 1, 1)));
        CharacterGuardianshipState first = ActiveGuardianship(
            WardA,
            Target,
            "guardian-first");
        CharacterGuardianshipState second = ActiveGuardianship(
            WardB,
            Target,
            "guardian-second");
        CharacterGuardianshipState historical = EndedGuardianship(
            WardA,
            Target,
            "guardian-history");
        CharacterGuardianshipWorldState state = Guardianships(
            current,
            [second, historical, first]);

        CharacterGuardianshipDeathPlan plan = PrepareGuardianshipDeath(
            state,
            candidateCharacters,
            "guardianship-multiple");

        Assert.Equal(
            plan.EndedGuardianships.OrderBy(item => item.GuardianshipId),
            plan.EndedGuardianships);
        Assert.Equal(2, plan.EndedGuardianships.Count);
        Assert.Equal(2, plan.EndedGuardianships.Select(item => item.GuardianshipId).Distinct().Count());
        Assert.All(
            plan.EndedGuardianships,
            item => Assert.Equal(CharacterGuardianshipEndReason.GuardianDied, item.EndReason));
        Assert.Equal(
            historical,
            plan.GuardianshipPlan.Candidate.Guardianships.Single(
                item => item.GuardianshipId == historical.GuardianshipId));
        IList<CharacterGuardianshipState> readOnly = Assert.IsAssignableFrom<
            IList<CharacterGuardianshipState>>(plan.EndedGuardianships);
        Assert.Throws<NotSupportedException>(() => readOnly[0] = historical);
        CharacterGuardianshipState returned = plan.EndedGuardianships[0];
        CharacterGuardianshipState[] queried = plan.GuardianshipPlan.Candidate.Guardianships
            .ToArray();
        queried[0] = historical;
        Assert.Contains(
            plan.GuardianshipPlan.Candidate.Guardianships,
            item => item.GuardianshipId == returned.GuardianshipId
                && item.Status == CharacterGuardianshipStatus.Ended);
    }

    [Fact]
    public void PregnancyDeathHandlesZeroAndOneRecordWithoutMutatingSource()
    {
        CharacterWorldState current = Characters(
            Alive(Target, new CampaignDate(160, 1, 1)),
            Alive(PartnerA, new CampaignDate(161, 1, 1)));
        CharacterWorldState candidateCharacters = Characters(
            Dead(Target, new CampaignDate(160, 1, 1)),
            Alive(PartnerA, new CampaignDate(161, 1, 1)));
        TestMarriageQuery marriages = new([Union(UnionA, Target, PartnerA)]);
        TestMarriageQuery candidateMarriages = new([
            EndedUnion(UnionA, Target, PartnerA),
        ]);
        CharacterPregnancyWorldState empty = Pregnancies(current, marriages, []);

        CharacterPregnancyDeathPlan emptyPlan = PreparePregnancyDeath(
            empty,
            candidateCharacters,
            candidateMarriages,
            "pregnancy-zero");

        Assert.Empty(emptyPlan.RemovedPregnancies);
        Assert.Empty(emptyPlan.PregnancyPlan.Candidate.ActivePregnancies);

        CharacterPregnancyState pregnancy = Pregnancy(
            "pregnancy-one",
            Target,
            PartnerA,
            UnionA);
        CharacterPregnancyWorldState one = Pregnancies(
            current,
            marriages,
            [pregnancy]);
        string before = Serialize(one.CaptureSnapshot());

        CharacterPregnancyDeathPlan onePlan = PreparePregnancyDeath(
            one,
            candidateCharacters,
            candidateMarriages,
            "pregnancy-one-resolution");

        Assert.Equal(before, Serialize(one.CaptureSnapshot()));
        Assert.Equal(pregnancy, Assert.Single(onePlan.RemovedPregnancies));
        Assert.Empty(onePlan.PregnancyPlan.Candidate.ActivePregnancies);
    }

    [Fact]
    public void PregnancyDeathRemovesBothRolesOnceAndRetainsUnrelatedPregnancy()
    {
        CharacterSeed[] living =
        [
            Alive(Target, new CampaignDate(160, 1, 1)),
            Alive(PartnerA, new CampaignDate(161, 1, 1)),
            Alive(PartnerB, new CampaignDate(162, 1, 1)),
            Alive(OtherA, new CampaignDate(163, 1, 1)),
            Alive(OtherB, new CampaignDate(164, 1, 1)),
        ];
        CharacterWorldState current = Characters(living);
        CharacterWorldState candidateCharacters = Characters(
            living.Select(seed => seed.Id == Target
                ? Dead(seed.Id, seed.BirthDate)
                : seed).ToArray());
        MarriageUnionState targetGestationalUnion = Union(UnionA, Target, PartnerA);
        MarriageUnionState targetOtherUnion = Union(UnionB, PartnerB, Target);
        MarriageUnionState unrelatedUnion = Union(UnionC, OtherA, OtherB);
        TestMarriageQuery marriages = new([
            targetGestationalUnion,
            targetOtherUnion,
            unrelatedUnion,
        ]);
        TestMarriageQuery candidateMarriages = new([
            EndedUnion(UnionA, Target, PartnerA),
            EndedUnion(UnionB, PartnerB, Target),
            unrelatedUnion,
        ]);
        CharacterPregnancyState gestational = Pregnancy(
            "target-gestational",
            Target,
            PartnerA,
            UnionA);
        CharacterPregnancyState other = Pregnancy(
            "target-other",
            PartnerB,
            Target,
            UnionB);
        CharacterPregnancyState unrelated = Pregnancy(
            "unrelated",
            OtherA,
            OtherB,
            UnionC);
        CharacterPregnancyWorldState state = Pregnancies(
            current,
            marriages,
            [unrelated, other, gestational]);

        CharacterPregnancyDeathPlan plan = PreparePregnancyDeath(
            state,
            candidateCharacters,
            candidateMarriages,
            "pregnancy-multiple");

        Assert.Equal(
            plan.RemovedPregnancies.OrderBy(item => item.PregnancyId),
            plan.RemovedPregnancies);
        Assert.Equal(2, plan.RemovedPregnancies.Count);
        Assert.Equal(2, plan.RemovedPregnancies.Select(item => item.PregnancyId).Distinct().Count());
        Assert.Contains(
            plan.RemovedPregnancies,
            item => item.GestationalParentCharacterId == Target);
        Assert.Contains(
            plan.RemovedPregnancies,
            item => item.OtherBiologicalParentCharacterId == Target);
        Assert.Equal(
            unrelated,
            Assert.Single(plan.PregnancyPlan.Candidate.ActivePregnancies));
        IList<CharacterPregnancyState> readOnly = Assert.IsAssignableFrom<
            IList<CharacterPregnancyState>>(plan.RemovedPregnancies);
        Assert.Throws<NotSupportedException>(() => readOnly[0] = unrelated);
        CharacterPregnancyState[] queried = plan.PregnancyPlan.Candidate.ActivePregnancies
            .ToArray();
        queried[0] = gestational;
        Assert.Equal(
            unrelated,
            Assert.Single(plan.PregnancyPlan.Candidate.ActivePregnancies));
    }

    [Fact]
    public void DeathPlannersRejectAliveCandidateStaleCurrentAndWrongCoordinatesWithoutMutation()
    {
        CharacterWorldState alive = Characters(
            Alive(Target, new CampaignDate(160, 1, 1)));
        CharacterWorldState dead = Characters(
            Dead(Target, new CampaignDate(160, 1, 1)));
        CharacterGuardianshipWorldState guardianships = Guardianships(alive, []);
        CharacterPregnancyWorldState pregnancies = Pregnancies(
            alive,
            new TestMarriageQuery([]),
            []);
        string guardianshipsBefore = Serialize(guardianships.CaptureSnapshot());
        string pregnanciesBefore = Serialize(pregnancies.CaptureSnapshot());
        EntityId commandId = new("command:test/death-invalid");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(Date, commandId);

        Assert.Throws<SimulationValidationException>(() => guardianships.PrepareCharacterDeath(
            Target,
            alive,
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId));
        Assert.Throws<SimulationValidationException>(() => pregnancies.PrepareCharacterDeath(
            Target,
            alive,
            new TestMarriageQuery([]),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId));
        Assert.Throws<SimulationValidationException>(() => guardianships.PrepareCharacterDeath(
            Target,
            dead,
            Date,
            Calendar.TurnIndex - 1,
            commandId,
            eventId));
        Assert.Throws<SimulationValidationException>(() => pregnancies.PrepareCharacterDeath(
            Target,
            dead,
            new TestMarriageQuery([]),
            Date.AddDays(-1),
            Calendar.TurnIndex,
            commandId,
            CharacterConditionIds.DeriveActionEventId(Date.AddDays(-1), commandId)));
        Assert.Throws<SimulationValidationException>(() => guardianships.PrepareCharacterDeath(
            Target,
            dead,
            Date,
            Calendar.TurnIndex,
            commandId,
            new EntityId("event:test/forged")));
        Assert.Throws<SimulationValidationException>(() => pregnancies.PrepareCharacterDeath(
            Target,
            dead,
            new TestMarriageQuery([]),
            Date,
            Calendar.TurnIndex,
            commandId,
            new EntityId("event:test/forged")));

        CharacterGuardianshipWorldState staleGuardianships = Guardianships(dead, []);
        CharacterPregnancyWorldState stalePregnancies = Pregnancies(
            dead,
            new TestMarriageQuery([]),
            []);
        Assert.Throws<SimulationValidationException>(() => staleGuardianships.PrepareCharacterDeath(
            Target,
            dead,
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId));
        Assert.Throws<SimulationValidationException>(() => stalePregnancies.PrepareCharacterDeath(
            Target,
            dead,
            new TestMarriageQuery([]),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId));
        Assert.Equal(guardianshipsBefore, Serialize(guardianships.CaptureSnapshot()));
        Assert.Equal(pregnanciesBefore, Serialize(pregnancies.CaptureSnapshot()));
    }

    [Fact]
    public void PregnancyDeathCandidateRevalidatesRetainedRecordsAgainstSuppliedCandidates()
    {
        CharacterWorldState current = Characters(
            Alive(Target, new CampaignDate(160, 1, 1)),
            Alive(OtherA, new CampaignDate(163, 1, 1)),
            Alive(OtherB, new CampaignDate(164, 1, 1)));
        CharacterWorldState candidateCharacters = Characters(
            Dead(Target, new CampaignDate(160, 1, 1)),
            Alive(OtherA, new CampaignDate(163, 1, 1)),
            Alive(OtherB, new CampaignDate(164, 1, 1)));
        MarriageUnionState unrelatedUnion = Union(UnionC, OtherA, OtherB);
        TestMarriageQuery marriages = new([unrelatedUnion]);
        CharacterPregnancyWorldState state = Pregnancies(
            current,
            marriages,
            [Pregnancy("candidate-revalidation", OtherA, OtherB, UnionC)]);
        string before = Serialize(state.CaptureSnapshot());
        EntityId commandId = new("command:test/death-candidate-revalidation");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(Date, commandId);

        Assert.Throws<SimulationValidationException>(() => state.PrepareCharacterDeath(
            Target,
            candidateCharacters,
            new TestMarriageQuery([]),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    private static CharacterGuardianshipDeathPlan PrepareGuardianshipDeath(
        CharacterGuardianshipWorldState state,
        CharacterWorldState candidateCharacters,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return state.PrepareCharacterDeath(
            Target,
            candidateCharacters,
            Date,
            Calendar.TurnIndex,
            commandId,
            CharacterConditionIds.DeriveActionEventId(Date, commandId));
    }

    private static CharacterPregnancyDeathPlan PreparePregnancyDeath(
        CharacterPregnancyWorldState state,
        CharacterWorldState candidateCharacters,
        TestMarriageQuery candidateMarriages,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return state.PrepareCharacterDeath(
            Target,
            candidateCharacters,
            candidateMarriages,
            Date,
            Calendar.TurnIndex,
            commandId,
            CharacterConditionIds.DeriveActionEventId(Date, commandId));
    }

    private static CharacterGuardianshipWorldState Guardianships(
        CharacterWorldState characters,
        IReadOnlyList<CharacterGuardianshipState> records) => new(
        new CharacterGuardianshipWorldSnapshot(
            CharacterGuardianshipContractVersions.Snapshot,
            records),
        characters,
        Calendar);

    private static CharacterPregnancyWorldState Pregnancies(
        CharacterWorldState characters,
        TestMarriageQuery marriages,
        IReadOnlyList<CharacterPregnancyState> records) => new(
        new CharacterPregnancyWorldSnapshot(
            CharacterPregnancyContractVersions.Snapshot,
            records),
        characters,
        marriages,
        Calendar);

    private static CharacterGuardianshipState ActiveGuardianship(
        EntityId ward,
        EntityId guardian,
        string suffix)
    {
        CampaignDate establishedDate = Date.AddDays(-40);
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
            1,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
    }

    private static CharacterGuardianshipState EndedGuardianship(
        EntityId ward,
        EntityId guardian,
        string suffix)
    {
        CharacterGuardianshipState active = ActiveGuardianship(ward, guardian, suffix);
        EntityId commandId = new($"command:test/{suffix}-ended");
        return active with
        {
            Status = CharacterGuardianshipStatus.Ended,
            EndDate = Date.AddDays(-20),
            EndTurnIndex = 2,
            EndSourceCommandId = commandId,
            EndSourceEventId = CharacterFamilyIds.DeriveActionEventId(
                Date.AddDays(-20),
                commandId),
            EndReason = CharacterGuardianshipEndReason.Revoked,
        };
    }

    private static CharacterPregnancyState Pregnancy(
        string suffix,
        EntityId gestationalParent,
        EntityId otherParent,
        EntityId unionId)
    {
        CampaignDate startDate = Date.AddDays(-30);
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
            2,
            commandId,
            eventId);
    }

    private static MarriageUnionState Union(
        EntityId unionId,
        EntityId first,
        EntityId second) => Union(unionId, first, second, MarriageUnionStatus.Active);

    private static MarriageUnionState EndedUnion(
        EntityId unionId,
        EntityId first,
        EntityId second) => Union(unionId, first, second, MarriageUnionStatus.Ended) with
        {
            EndDate = Date,
            EndTurnIndex = Calendar.TurnIndex,
            EndCommandId = new EntityId("command:test/death-union-ended"),
            EndReason = MarriageUnionEndReason.SpouseDied,
        };

    private static MarriageUnionState Union(
        EntityId unionId,
        EntityId first,
        EntityId second,
        MarriageUnionStatus status) => new(
        CharacterMarriageContractVersions.State,
        unionId,
        first.CompareTo(second) < 0 ? first : second,
        first.CompareTo(second) < 0 ? second : first,
        MarriageUnionForm.PrincipalSpouse,
        null,
        MarriageBasis.Political,
        MarriageConsentKind.Voluntary,
        new EntityId("marriage_practice:test/death"),
        new EntityId($"marriage_proposal:test/{unionId.Value.Replace(':', '/')}"),
        Date.AddDays(-60),
        1,
        status,
        null,
        null,
        null,
        null);

    private static CharacterWorldState Characters(params CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds
            .Select(seed =>
            {
                EntityId nameKey = new($"loc:test/{seed.Id.Value.Replace(':', '/')}");
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
                seed.Condition,
                []))
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
            Date);
    }

    private static CharacterSeed Alive(EntityId id, CampaignDate birthDate) => new(
        id,
        birthDate,
        CharacterConditionState.Default);

    private static CharacterSeed Dead(EntityId id, CampaignDate birthDate) => new(
        id,
        birthDate,
        new CharacterConditionState(
            CharacterVitalStatus.Dead,
            CharacterHealthStatus.Critical,
            IsIncapacitated: true,
            CharacterCustodyStatus.Free,
            null));

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
