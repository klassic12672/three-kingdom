using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterBirthWorldStateTests
{
    private static readonly CampaignDate DueDate = new(201, 1, 1);
    private static readonly CampaignDate StartDate = DueDate.AddDays(-280);
    private static readonly EntityId GestationalParent = new("character:test/birth_gestational_parent");
    private static readonly EntityId OtherParent = new("character:test/birth_other_parent");
    private static readonly EntityId ThirdCharacter = new("character:test/birth_third");
    private static readonly EntityId UnionId = new("marriage_union:test/birth_source");
    private static readonly EntityId FamilyA = new("family:test/birth_a");
    private static readonly EntityId FamilyB = new("family:test/birth_b");
    private static readonly EntityId HouseholdA = new("household:test/birth_a");
    private static readonly EntityId HouseholdB = new("household:test/birth_b");
    private static readonly EntityId CultureA = new("culture:test/birth_a");
    private static readonly EntityId CultureB = new("culture:test/birth_b");
    private static readonly EntityId TraitA = new("trait:test/birth_a");
    private static readonly EntityId TraitB = new("trait:test/birth_b");
    private static readonly EntityId TraitC = new("trait:test/birth_c");

    [Fact]
    public void DueBirthProducesTwoImmutableCandidatesAndACompleteGeneratedChild()
    {
        CharacterSeed[] seeds =
        [
            Adult(
                GestationalParent,
                traits: [TraitA, TraitB],
                familyId: FamilyA,
                householdId: HouseholdA,
                cultureId: CultureA),
            Adult(
                OtherParent,
                traits: [TraitB, TraitC],
                familyId: FamilyB,
                householdId: HouseholdB,
                cultureId: CultureB),
        ];
        CharacterWorldState characters = CreateCharacters(DueDate, seeds);
        MutableMarriageQuery marriages = new(Union(
            GestationalParent,
            OtherParent));
        CharacterPregnancyState pregnancy = Pregnancy();
        CharacterPregnancyWorldState pregnancies = NewPregnancies(
            characters,
            marriages,
            pregnancy,
            DueDate);
        string charactersBefore = Serialize(characters.CaptureSnapshot());
        string pregnanciesBefore = Serialize(pregnancies.CaptureSnapshot());
        EntityId[] inheritedTraits = [TraitA, TraitC];
        GeneratedNewbornSpecification newborn = Newborn(
            inheritedTraits,
            CultureA,
            FamilyA,
            HouseholdB);

        (CharacterPregnancyBirthResolutionPlan pregnancyPlan,
            CharacterBirthMutationPlan characterPlan) = Prepare(
                characters,
                pregnancies,
                pregnancy,
                newborn,
                DueDate,
                "due-birth");

        Assert.Equal(charactersBefore, Serialize(characters.CaptureSnapshot()));
        Assert.Equal(pregnanciesBefore, Serialize(pregnancies.CaptureSnapshot()));
        Assert.Empty(pregnancyPlan.PregnancyPlan.Candidate.ActivePregnancies);
        Assert.Equal(pregnancy, pregnancyPlan.ResolvedPregnancy);
        CharacterBirthChange birth = characterPlan.Birth;
        EntityId expectedChildId = CharacterBirthIds.DeriveChildId(pregnancy.PregnancyId);
        EntityId expectedEventId = CharacterFamilyIds.DeriveActionEventId(
            DueDate,
            new EntityId("command:test/due-birth"));
        EntityId expectedBirthId = CharacterBirthIds.DeriveBirthId(
            expectedEventId,
            pregnancy.PregnancyId);
        Assert.Equal(expectedBirthId, birth.BirthId);
        Assert.Equal(pregnancy, birth.ResolvedPregnancy);
        Assert.Equal(expectedChildId, birth.ChildDefinition.Id);
        Assert.Equal(DueDate, birth.ChildDefinition.BirthDate);
        Assert.Equal(new EntityId("loc:test/generated_newborn"), birth.ChildDefinition.NameKey);
        Assert.Equal(
            birth.ChildDefinition.NameKey,
            birth.ChildDefinition.StructuredName!.PrimaryNameKey);
        Assert.Null(birth.ChildDefinition.StructuredName.CourtesyNameKey);
        Assert.Equal(CharacterOriginKind.Generated, birth.ChildDefinition.ContentOrigin!.OriginKind);
        Assert.Equal(
            CharacterHistoricalClassification.Fictional,
            birth.ChildDefinition.ContentOrigin.HistoricalClassification);
        Assert.Equal(expectedBirthId, birth.ChildDefinition.ContentOrigin.RecordId);
        Assert.Null(birth.ChildDefinition.ContentOrigin.OwningPackId);
        Assert.Empty(birth.ChildDefinition.ContentOrigin.AppliedOverridePackIds);
        Assert.Empty(birth.ChildDefinition.ContentOrigin.SourceIds);
        Assert.Equal(CultureA, birth.ChildDefinition.CultureId);
        Assert.Equal(inheritedTraits, birth.ChildDefinition.TraitIds);
        Assert.Empty(birth.ChildDefinition.AbilityIds);
        Assert.Empty(birth.ChildDefinition.AptitudeIds);
        Assert.Empty(birth.ChildDefinition.AmbitionIds);
        Assert.Empty(birth.ChildDefinition.ReputationIds);
        Assert.Empty(birth.ChildDefinition.FlawIds!);
        Assert.Equal(CharacterConditionState.Default, birth.ChildState.Condition);
        Assert.Equal(
            new[] { GestationalParent, OtherParent }.Order(),
            birth.ChildState.ParentIds);
        Assert.All(
            birth.ChildState.ParentLinks!,
            link => Assert.Equal(ParentChildLinkKind.Biological, link.Kind));
        Assert.Equal(FamilyA, birth.FamilyId);
        Assert.Equal(HouseholdB, birth.HouseholdId);

        Assert.True(characterPlan.CharacterPlan.Candidate.TryGetCharacterProfile(
            expectedChildId,
            out AuthoritativeCharacterProfile? child));
        Assert.Equal(FamilyA, child.FamilyId);
        Assert.Equal(HouseholdB, child.HouseholdId);
        Assert.Equal(CultureA, child.CultureId);
        Assert.Contains(
            child.CharacterId,
            characterPlan.CharacterPlan.Candidate.Profiles
                .Single(profile => profile.CharacterId == GestationalParent)
                .ChildIds);
        Assert.Contains(
            child.CharacterId,
            characterPlan.CharacterPlan.Candidate.Profiles
                .Single(profile => profile.CharacterId == OtherParent)
                .ChildIds);

        characters.ApplyPrepared(characterPlan.CharacterPlan);
        pregnancies.ApplyPrepared(pregnancyPlan.PregnancyPlan);

        Assert.True(characters.TryGetCharacterProfile(expectedChildId, out _));
        Assert.Empty(pregnancies.ActivePregnancies);
    }

    [Fact]
    public void OverdueResolutionUsesExpectedBirthDateForTheChildAndActualResolutionForEvidence()
    {
        CampaignDate overdueDate = DueDate.AddDays(9);
        CharacterWorldState characters = CreateCharacters(
            overdueDate,
            Adult(GestationalParent),
            Adult(OtherParent));
        MutableMarriageQuery marriages = new(Union(GestationalParent, OtherParent));
        CharacterPregnancyState pregnancy = Pregnancy();
        CharacterPregnancyWorldState pregnancies = NewPregnancies(
            characters,
            marriages,
            pregnancy,
            overdueDate);

        (CharacterPregnancyBirthResolutionPlan pregnancyPlan,
            CharacterBirthMutationPlan characterPlan) = Prepare(
                characters,
                pregnancies,
                pregnancy,
                Newborn([]),
                overdueDate,
                "overdue-birth");

        Assert.Empty(pregnancyPlan.PregnancyPlan.Candidate.ActivePregnancies);
        Assert.Equal(DueDate, characterPlan.Birth.ChildDefinition.BirthDate);
        Assert.Equal(overdueDate, characterPlan.Birth.ResolutionDate);
        Assert.Equal(
            CharacterBirthIds.DeriveBirthId(
                characterPlan.Birth.SourceEventId,
                pregnancy.PregnancyId),
            characterPlan.Birth.ChildDefinition.ContentOrigin!.RecordId);
    }

    [Fact]
    public void PregnancyRemovalRejectsEarlyStaleAndTamperedResolutionWithoutMutation()
    {
        CharacterWorldState characters = CreateCharacters(
            DueDate,
            Adult(GestationalParent),
            Adult(OtherParent));
        MutableMarriageQuery marriages = new(Union(GestationalParent, OtherParent));
        CharacterPregnancyState pregnancy = Pregnancy();
        CharacterPregnancyWorldState pregnancies = NewPregnancies(
            characters,
            marriages,
            pregnancy,
            DueDate);
        string before = Serialize(pregnancies.CaptureSnapshot());
        EntityId commandId = new("command:test/removal-tamper");

        Assert.Throws<SimulationValidationException>(() => pregnancies.PrepareBirthResolution(
            pregnancy.PregnancyId,
            DueDate.AddDays(-1),
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(DueDate.AddDays(-1), commandId)));
        Assert.Throws<SimulationValidationException>(() => pregnancies.PrepareBirthResolution(
            new EntityId("pregnancy:test/stale"),
            DueDate,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(DueDate, commandId)));
        Assert.Throws<SimulationValidationException>(() => pregnancies.PrepareBirthResolution(
            pregnancy.PregnancyId,
            DueDate,
            5,
            commandId,
            new EntityId("event:test/tampered")));
        Assert.Equal(before, Serialize(pregnancies.CaptureSnapshot()));
    }

    [Fact]
    public void BirthRevalidatesLivingParentsAndTheExactActiveSourceUnion()
    {
        CharacterWorldState deadParentCharacters = CreateCharacters(
            DueDate,
            Adult(GestationalParent),
            Adult(OtherParent));
        MutableMarriageQuery deadParentMarriages = new(Union(
            GestationalParent,
            OtherParent));
        CharacterPregnancyState pregnancy = Pregnancy();
        CharacterPregnancyWorldState deadParentPregnancies = NewPregnancies(
            deadParentCharacters,
            deadParentMarriages,
            pregnancy,
            DueDate);
        CharacterConditionMutationPlan death = deadParentCharacters.PrepareDeathPreview(
            OtherParent,
            CharacterConditionState.Default,
            DueDate,
            5,
            new EntityId("command:test/birth-parent-death"),
            CharacterConditionIds.DeriveActionEventId(
                DueDate,
                new EntityId("command:test/birth-parent-death")));
        deadParentCharacters.ApplyPrepared(death.CharacterPlan);
        Assert.Throws<SimulationValidationException>(() => PreparePregnancyOnly(
            deadParentPregnancies,
            pregnancy,
            DueDate,
            "dead-parent"));

        CharacterWorldState unionCharacters = CreateCharacters(
            DueDate,
            Adult(GestationalParent),
            Adult(OtherParent),
            Adult(ThirdCharacter));
        MutableMarriageQuery unionMarriages = new(Union(
            GestationalParent,
            OtherParent));
        CharacterPregnancyWorldState unionPregnancies = NewPregnancies(
            unionCharacters,
            unionMarriages,
            pregnancy,
            DueDate);
        unionMarriages.Current = unionMarriages.Current with
        {
            Status = MarriageUnionStatus.Ended,
            EndDate = DueDate,
            EndTurnIndex = 5,
            EndCommandId = new EntityId("command:test/union-ended-before-birth"),
            EndReason = MarriageUnionEndReason.Separated,
        };
        Assert.Throws<SimulationValidationException>(() => PreparePregnancyOnly(
            unionPregnancies,
            pregnancy,
            DueDate,
            "ended-union"));

        unionMarriages.Current = Union(GestationalParent, ThirdCharacter);
        Assert.Throws<SimulationValidationException>(() => PreparePregnancyOnly(
            unionPregnancies,
            pregnancy,
            DueDate,
            "mismatched-union"));
        Assert.Single(unionPregnancies.ActivePregnancies);
    }

    [Fact]
    public void NewbornSelectionsAreIndependentAndMustComeFromCurrentParentValues()
    {
        CharacterWorldState characters = CreateCharacters(
            DueDate,
            Adult(
                GestationalParent,
                familyId: FamilyA,
                householdId: HouseholdA,
                cultureId: CultureA),
            Adult(
                OtherParent,
                familyId: FamilyB,
                householdId: HouseholdB,
                cultureId: CultureB));
        CharacterPregnancyState pregnancy = Pregnancy();
        GeneratedNewbornSpecification mixed = Newborn(
            [],
            CultureB,
            FamilyA,
            HouseholdB);
        CharacterBirthMutationPlan accepted = PrepareCharacterOnly(
            characters,
            pregnancy,
            mixed,
            DueDate,
            "mixed-selections");
        Assert.Equal(CultureB, accepted.Birth.ChildDefinition.CultureId);
        Assert.Equal(FamilyA, accepted.Birth.FamilyId);
        Assert.Equal(HouseholdB, accepted.Birth.HouseholdId);

        GeneratedNewbornSpecification[] invalid =
        [
            Newborn([], null, FamilyA, HouseholdB),
            Newborn([], CultureA, null, HouseholdB),
            Newborn([], CultureA, FamilyA, null),
            Newborn([], new EntityId("culture:test/not_parent"), FamilyA, HouseholdB),
            Newborn([], CultureA, new EntityId("family:test/not_parent"), HouseholdB),
            Newborn([], CultureA, FamilyA, new EntityId("household:test/not_parent")),
        ];
        foreach (GeneratedNewbornSpecification specification in invalid)
        {
            Assert.Throws<SimulationValidationException>(() => PrepareCharacterOnly(
                characters,
                pregnancy,
                specification,
                DueDate,
                "invalid-selection"));
        }

        CharacterWorldState noMembership = CreateCharacters(
            DueDate,
            Adult(GestationalParent),
            Adult(OtherParent));
        Assert.NotNull(PrepareCharacterOnly(
            noMembership,
            pregnancy,
            Newborn([]),
            DueDate,
            "null-selections"));
        Assert.Throws<SimulationValidationException>(() => PrepareCharacterOnly(
            noMembership,
            pregnancy,
            Newborn([], CultureA),
            DueDate,
            "invented-selection"));
    }

    [Fact]
    public void NewbornNameAndInheritedTraitsMustBeCanonicalBoundedParentalData()
    {
        EntityId[] nineTraits = Enumerable.Range(0, 9)
            .Select(index => new EntityId($"trait:test/birth_{index:D2}"))
            .ToArray();
        CharacterWorldState characters = CreateCharacters(
            DueDate,
            Adult(GestationalParent, traits: nineTraits.Take(5).ToArray()),
            Adult(OtherParent, traits: nineTraits.Skip(5).ToArray()),
            Adult(ThirdCharacter, traits: [TraitA]));
        CharacterPregnancyState pregnancy = Pregnancy();

        GeneratedNewbornSpecification[] invalid =
        [
            Newborn([nineTraits[0], nineTraits[0]]),
            Newborn([nineTraits[1], nineTraits[0]]),
            Newborn([TraitA]),
            Newborn(nineTraits),
            Newborn([], primaryNameKey: new EntityId("name:test/not_localization")),
            Newborn([], primaryNameKey: new EntityId("location:test/not_localization")),
            new GeneratedNewbornSpecification(
                2,
                new EntityId("loc:test/generated_newborn"),
                null,
                null,
                null,
                []),
        ];
        foreach (GeneratedNewbornSpecification specification in invalid)
        {
            Assert.Throws<SimulationValidationException>(() => PrepareCharacterOnly(
                characters,
                pregnancy,
                specification,
                DueDate,
                "invalid-newborn"));
        }

        EntityId[] source = nineTraits.Take(3).ToArray();
        GeneratedNewbornSpecification defensive = Newborn(source);
        source[0] = TraitA;
        CharacterBirthMutationPlan accepted = PrepareCharacterOnly(
            characters,
            pregnancy,
            defensive,
            DueDate,
            "defensive-traits");
        Assert.Equal(nineTraits.Take(3), accepted.Birth.ChildDefinition.TraitIds);
    }

    [Fact]
    public void ChildAndBirthIdentityCollisionsRejectWithoutMutatingCharacters()
    {
        CharacterPregnancyState pregnancy = Pregnancy();
        EntityId childId = CharacterBirthIds.DeriveChildId(pregnancy.PregnancyId);
        CharacterWorldState childCollision = CreateCharacters(
            DueDate,
            Adult(GestationalParent),
            Adult(OtherParent),
            Seed(childId, new CampaignDate(190, 1, 1)));
        string childBefore = Serialize(childCollision.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => PrepareCharacterOnly(
            childCollision,
            pregnancy,
            Newborn([]),
            DueDate,
            "child-collision"));
        Assert.Equal(childBefore, Serialize(childCollision.CaptureSnapshot()));

        EntityId commandId = new("command:test/birth-collision");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(DueDate, commandId);
        EntityId birthId = CharacterBirthIds.DeriveBirthId(eventId, pregnancy.PregnancyId);
        CharacterWorldState birthCollision = CreateCharacters(
            DueDate,
            Adult(GestationalParent),
            Adult(OtherParent),
            Seed(birthId, new CampaignDate(190, 1, 1)));
        string birthBefore = Serialize(birthCollision.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => birthCollision.PreparePregnancyBirth(
            pregnancy,
            Newborn([]),
            DueDate,
            5,
            commandId,
            eventId));
        Assert.Equal(birthBefore, Serialize(birthCollision.CaptureSnapshot()));
    }

    private static (CharacterPregnancyBirthResolutionPlan Pregnancy,
        CharacterBirthMutationPlan Character) Prepare(
        CharacterWorldState characters,
        CharacterPregnancyWorldState pregnancies,
        CharacterPregnancyState pregnancy,
        GeneratedNewbornSpecification newborn,
        CampaignDate resolutionDate,
        string suffix)
    {
        CharacterPregnancyBirthResolutionPlan pregnancyPlan = PreparePregnancyOnly(
            pregnancies,
            pregnancy,
            resolutionDate,
            suffix);
        CharacterBirthMutationPlan characterPlan = PrepareCharacterOnly(
            characters,
            pregnancyPlan.ResolvedPregnancy,
            newborn,
            resolutionDate,
            suffix);
        return (pregnancyPlan, characterPlan);
    }

    private static CharacterPregnancyBirthResolutionPlan PreparePregnancyOnly(
        CharacterPregnancyWorldState pregnancies,
        CharacterPregnancyState pregnancy,
        CampaignDate resolutionDate,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return pregnancies.PrepareBirthResolution(
            pregnancy.PregnancyId,
            resolutionDate,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(resolutionDate, commandId));
    }

    private static CharacterBirthMutationPlan PrepareCharacterOnly(
        CharacterWorldState characters,
        CharacterPregnancyState pregnancy,
        GeneratedNewbornSpecification newborn,
        CampaignDate resolutionDate,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return characters.PreparePregnancyBirth(
            pregnancy,
            newborn,
            resolutionDate,
            5,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(resolutionDate, commandId));
    }

    private static CharacterPregnancyWorldState NewPregnancies(
        CharacterWorldState characters,
        MutableMarriageQuery marriages,
        CharacterPregnancyState pregnancy,
        CampaignDate date) => new(
        new CharacterPregnancyWorldSnapshot(
            CharacterPregnancyContractVersions.Snapshot,
            [pregnancy]),
        characters,
        marriages,
        new CampaignCalendar(date, 5));

    private static CharacterPregnancyState Pregnancy()
    {
        EntityId commandId = new("command:test/birth-pregnancy-start");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(StartDate, commandId);
        return new CharacterPregnancyState(
            CharacterPregnancyContractVersions.State,
            CharacterPregnancyIds.DerivePregnancyId(
                eventId,
                GestationalParent,
                OtherParent,
                UnionId),
            GestationalParent,
            OtherParent,
            UnionId,
            StartDate,
            DueDate,
            1,
            commandId,
            eventId);
    }

    private static GeneratedNewbornSpecification Newborn(
        IReadOnlyList<EntityId> inheritedTraits,
        EntityId? cultureId = null,
        EntityId? familyId = null,
        EntityId? householdId = null,
        EntityId? primaryNameKey = null) => new(
        CharacterBirthContractVersions.NewbornSpecification,
        primaryNameKey ?? new EntityId("loc:test/generated_newborn"),
        cultureId,
        familyId,
        householdId,
        inheritedTraits);

    private static MarriageUnionState Union(EntityId first, EntityId second) => new(
        CharacterMarriageContractVersions.State,
        UnionId,
        first.CompareTo(second) < 0 ? first : second,
        first.CompareTo(second) < 0 ? second : first,
        MarriageUnionForm.PrincipalSpouse,
        null,
        MarriageBasis.Political,
        MarriageConsentKind.Voluntary,
        new EntityId("marriage_practice:test/birth"),
        new EntityId("marriage_proposal:test/birth"),
        StartDate.AddDays(-1),
        0,
        MarriageUnionStatus.Active,
        null,
        null,
        null,
        null);

    private static CharacterSeed Adult(
        EntityId id,
        CharacterConditionState? condition = null,
        IReadOnlyList<EntityId>? traits = null,
        EntityId? familyId = null,
        EntityId? householdId = null,
        EntityId? cultureId = null) => new(
        id,
        new CampaignDate(150, 1, 1),
        condition ?? CharacterConditionState.Default,
        traits ?? [],
        familyId,
        householdId,
        cultureId);

    private static CharacterSeed Seed(EntityId id, CampaignDate birthDate) => new(
        id,
        birthDate,
        CharacterConditionState.Default,
        [],
        null,
        null,
        null);

    private static CharacterWorldState CreateCharacters(
        CampaignDate currentDate,
        params CharacterSeed[] seeds)
    {
        EntityId[] traitIds = seeds
            .SelectMany(seed => seed.TraitIds)
            .Distinct()
            .Order()
            .ToArray();
        CharacterIdentityDefinition[] identities = traitIds
            .Select(id => new CharacterIdentityDefinition(
                CharacterContractVersions.Definition,
                id,
                CharacterIdentityKind.Trait,
                new EntityId($"loc:{id.Value.Replace(':', '/')}")))
            .ToArray();
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
                    seed.TraitIds.Order().ToArray(),
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(seed.Id),
                    seed.CultureId,
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
        EntityId[] familyIds = seeds
            .Where(seed => seed.FamilyId is not null)
            .Select(seed => seed.FamilyId!.Value)
            .Distinct()
            .Order()
            .ToArray();
        FamilyDefinition[] familyDefinitions = familyIds
            .Select(id => new FamilyDefinition(
                CharacterContractVersions.Definition,
                id,
                new EntityId($"loc:{id.Value.Replace(':', '/')}")))
            .ToArray();
        FamilyState[] familyStates = familyIds
            .Select(id => new FamilyState(
                CharacterContractVersions.State,
                id,
                seeds.Where(seed => seed.FamilyId == id)
                    .Select(seed => seed.Id)
                    .Order()
                    .ToArray()))
            .ToArray();
        EntityId[] householdIds = seeds
            .Where(seed => seed.HouseholdId is not null)
            .Select(seed => seed.HouseholdId!.Value)
            .Distinct()
            .Order()
            .ToArray();
        HouseholdDefinition[] householdDefinitions = householdIds
            .Select(id => new HouseholdDefinition(
                CharacterContractVersions.Definition,
                id,
                new EntityId($"loc:{id.Value.Replace(':', '/')}")))
            .ToArray();
        HouseholdState[] householdStates = householdIds
            .Select(id =>
            {
                EntityId[] members = seeds.Where(seed => seed.HouseholdId == id)
                    .Select(seed => seed.Id)
                    .Order()
                    .ToArray();
                return new HouseholdState(
                    CharacterContractVersions.State,
                    id,
                    members[0],
                    members);
            })
            .ToArray();
        return new CharacterWorldState(
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                identities,
                definitions,
                familyDefinitions,
                householdDefinitions,
                states,
                familyStates,
                householdStates),
            currentDate);
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition,
        IReadOnlyList<EntityId> TraitIds,
        EntityId? FamilyId,
        EntityId? HouseholdId,
        EntityId? CultureId);

    private sealed class MutableMarriageQuery : IAuthoritativeCharacterMarriageWorldQuery
    {
        public MutableMarriageQuery(MarriageUnionState current)
        {
            Current = current;
        }

        public MarriageUnionState Current { get; set; }

        public IReadOnlyList<MarriagePracticeState> Practices => [];

        public IReadOnlyList<MarriageProposalState> Proposals => [];

        public IReadOnlyList<PoliticalBetrothalState> Betrothals => [];

        public IReadOnlyList<MarriageUnionState> Unions => [Current with { }];

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
            if (unionId == Current.UnionId)
            {
                union = Current with { };
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
            EntityId characterId) => Current.FirstCharacterId == characterId
                || Current.SecondCharacterId == characterId
                    ? [Current with { }]
                    : [];

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
