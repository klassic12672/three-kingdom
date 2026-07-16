using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionResolutionWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 1, 1);
    private static readonly CampaignCalendar Calendar = new(Date, 10);
    private static readonly EntityId Root = Character("root");
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId FirstChild = Character("first-child");
    private static readonly EntityId SecondChild = Character("second-child");
    private static readonly EntityId Sibling = Character("sibling");
    private static readonly EntityId Spouse = Character("spouse");
    private static readonly EntityId Concubine = Character("concubine");

    [Fact]
    public void ResolutionDecision_UsesExplicitPrecedenceAndExcludesConcubinage()
    {
        CharacterWorldState characters = CreateCharacters(
            Seed(Root, new(150, 1, 1)),
            Seed(Subject, new(170, 1, 1), Parent(Root)),
            Seed(FirstChild, new(190, 1, 1), Parent(Subject)),
            Seed(SecondChild, new(190, 1, 1), Parent(Subject)),
            Seed(Sibling, new(172, 1, 1), Parent(Root)),
            Seed(Spouse, new(175, 1, 1)),
            Seed(Concubine, new(176, 1, 1)));
        CharacterSuccessionWorldState succession = NewSuccession(characters);
        StubMarriageQuery marriages = new(
            Union("principal", Subject, Spouse, MarriageUnionForm.PrincipalSpouse),
            Union("concubine", Subject, Concubine, MarriageUnionForm.Concubinage));
        CampaignDate resolutionDate = new(201, 1, 1);

        SuccessionResolutionDecision spouseFirst = succession.PlanResolutionDecision(
            Subject,
            Rule(
                [
                    SuccessionLegalBasis.PrincipalSpouse,
                    SuccessionLegalBasis.BiologicalDescendant,
                    SuccessionLegalBasis.BiologicalCollateral,
                ],
                includesPrincipalSpouse: true,
                [ParentChildLinkKind.Biological]),
            marriages,
            StubGuardianshipQuery.Empty,
            null,
            resolutionDate,
            11);

        Assert.Equal(SuccessionResolutionStatus.Selected, spouseFirst.Status);
        Assert.Equal(Spouse, spouseFirst.SelectedCandidate!.CandidateCharacterId);
        Assert.Equal(4, spouseFirst.EligibleCandidateCount);
        Assert.Equal(26, spouseFirst.SelectedCandidate.CandidateAge);
        Assert.Contains(
            spouseFirst.SelectedCandidate.LegalBases,
            item => item.Basis == SuccessionLegalBasis.PrincipalSpouse);
        Assert.DoesNotContain(
            spouseFirst.DisputedCandidates,
            item => item.CandidateCharacterId == Concubine);

        SuccessionResolutionDecision disputedChildren =
            succession.PlanResolutionDecision(
                Subject,
                Rule(
                    [
                        SuccessionLegalBasis.BiologicalDescendant,
                        SuccessionLegalBasis.PrincipalSpouse,
                        SuccessionLegalBasis.BiologicalCollateral,
                    ],
                    includesPrincipalSpouse: true,
                    [ParentChildLinkKind.Biological],
                    SuccessionContestResolutionMode.RecordDispute),
                marriages,
                StubGuardianshipQuery.Empty,
                null,
                resolutionDate,
                11);

        Assert.Equal(SuccessionResolutionStatus.Disputed, disputedChildren.Status);
        Assert.Equal(
            [FirstChild, SecondChild],
            disputedChildren.DisputedCandidates
                .Select(item => item.CandidateCharacterId)
                .ToArray());
    }

    [Theory]
    [InlineData(
        ParentChildLinkKind.Biological,
        false,
        SuccessionLegalBasis.BiologicalDescendant)]
    [InlineData(
        ParentChildLinkKind.LegalAdoptive,
        false,
        SuccessionLegalBasis.LegalAdoptiveDescendant)]
    [InlineData(
        ParentChildLinkKind.UnspecifiedLegacy,
        false,
        SuccessionLegalBasis.UnspecifiedLegacyDescendant)]
    [InlineData(
        ParentChildLinkKind.Biological,
        true,
        SuccessionLegalBasis.BiologicalCollateral)]
    [InlineData(
        ParentChildLinkKind.LegalAdoptive,
        true,
        SuccessionLegalBasis.LegalAdoptiveCollateral)]
    [InlineData(
        ParentChildLinkKind.UnspecifiedLegacy,
        true,
        SuccessionLegalBasis.UnspecifiedLegacyCollateral)]
    public void ResolutionDecision_PreservesTypedDescendantAndNearestCollateralEvidence(
        ParentChildLinkKind kind,
        bool collateral,
        SuccessionLegalBasis expectedBasis)
    {
        SuccessionCandidateBasis candidateBasis = kind switch
        {
            ParentChildLinkKind.Biological =>
                SuccessionCandidateBasis.BiologicalDescendant,
            ParentChildLinkKind.LegalAdoptive =>
                SuccessionCandidateBasis.LegalAdoptiveDescendant,
            ParentChildLinkKind.UnspecifiedLegacy =>
                SuccessionCandidateBasis.UnspecifiedLegacyDescendant,
            _ => throw new InvalidOperationException(),
        };
        SuccessionLegalBasis descendantBasis = kind switch
        {
            ParentChildLinkKind.Biological =>
                SuccessionLegalBasis.BiologicalDescendant,
            ParentChildLinkKind.LegalAdoptive =>
                SuccessionLegalBasis.LegalAdoptiveDescendant,
            ParentChildLinkKind.UnspecifiedLegacy =>
                SuccessionLegalBasis.UnspecifiedLegacyDescendant,
            _ => throw new InvalidOperationException(),
        };
        CharacterWorldState characters = collateral
            ? CreateCharacters(
                Seed(Root, new(150, 1, 1)),
                Seed(
                    Subject,
                    new(170, 1, 1),
                    new CharacterParentLink(Root, kind)),
                Seed(
                    FirstChild,
                    new(180, 1, 1),
                    new CharacterParentLink(Root, kind)))
            : CreateCharacters(
                Seed(Subject, new(170, 1, 1)),
                Seed(
                    FirstChild,
                    new(180, 1, 1),
                    new CharacterParentLink(Subject, kind)));
        CharacterSuccessionWorldState succession = NewSuccession(characters);
        SuccessionResolutionRule rule = Rule(
            collateral
                ? [expectedBasis, descendantBasis]
                : [expectedBasis],
            includesPrincipalSpouse: false,
            collateral ? [kind] : [],
            candidateBases: [candidateBasis]);

        SuccessionResolutionDecision decision =
            succession.PlanResolutionDecision(
                Subject,
                rule,
                StubMarriageQuery.Empty,
                StubGuardianshipQuery.Empty,
                null,
                Date,
                Calendar.TurnIndex);
        SuccessionResolutionCandidate selected =
            Assert.IsType<SuccessionResolutionCandidate>(
                decision.SelectedCandidate);
        SuccessionLegalBasisEvidence evidence = Assert.Single(
            selected.LegalBases,
            item => item.Basis == expectedBasis);

        Assert.Equal(FirstChild, selected.CandidateCharacterId);
        Assert.Equal(collateral ? 2 : 1, selected.KinshipDistance);
        Assert.Equal(
            collateral ? 2 : null,
            evidence.CollateralDistance);
        Assert.Equal(
            collateral ? null : 1,
            evidence.DescendantGeneration);
        Assert.Equal(
            collateral ? Root : null,
            evidence.SharedAncestorCharacterId);
    }

    [Fact]
    public void PreparedResolution_TransfersInheritanceAndControlledCharacter()
    {
        CharacterWorldState characters = CreateCharacters(
            Seed(Subject, new(170, 1, 1)),
            Seed(FirstChild, new(180, 1, 1), Parent(Subject)));
        PlayerCampaignContinuityState continuity = new(
            CharacterSuccessionContractVersions.CampaignContinuity,
            PlayerCampaignContinuityStatus.Active,
            Subject,
            Date,
            Calendar.TurnIndex,
            new("command:test/continuity-seed"),
            new("event:test/continuity-seed"));
        CharacterSuccessionWorldState succession = NewSuccession(
            characters,
            continuity);
        CharacterResourceWorldState resources = new(
            CharacterResourceWorldSnapshot.Empty with
            {
                Accounts =
                [
                    Account(Subject, 40),
                    Account(FirstChild, 2),
                ],
            },
            characters,
            Calendar);
        CharacterEstateHoldingWorldState estates = new(
            new(
                CharacterEstateHoldingContractVersions.Snapshot,
                [
                    Holding("estate:test/one", Subject),
                    Holding("estate:test/two", Subject),
                ]),
            characters,
            Date);
        CampaignDate resolutionDate = new(201, 1, 1);
        long resolutionTurnIndex = 11;
        EntityId commandId = new("command:test/resolve-inheritance");
        EntityId conditionEventId = CharacterConditionIds.DeriveActionEventId(
            resolutionDate,
            commandId);
        EntityId resourceEventId = CharacterResourceIds.DeriveActionEventId(
            resolutionDate,
            commandId);
        SuccessionResolutionDecision decision = succession.PlanResolutionDecision(
            Subject,
            Rule([SuccessionLegalBasis.BiologicalDescendant], false, []),
            StubMarriageQuery.Empty,
            StubGuardianshipQuery.Empty,
            null,
            resolutionDate,
            resolutionTurnIndex);
        CharacterResourceInheritancePlan wealth = resources.PrepareInheritance(
            Subject,
            FirstChild,
            resolutionDate,
            resolutionTurnIndex,
            commandId,
            resourceEventId);
        CharacterEstateInheritancePlan estate = estates.PrepareInheritance(
            Subject,
            FirstChild,
            resolutionDate);
        SuccessionInheritanceChange inheritance = new(
            CharacterSuccessionContractVersions.Inheritance,
            wealth.WealthTransfer,
            estate.Transfers);
        CharacterSuccessionResolutionPlan resolution =
            succession.PrepareResolution(
                decision,
                CharacterConditionIds.DeriveDeathId(conditionEventId, Subject),
                inheritance,
                resolutionDate,
                resolutionTurnIndex,
                commandId,
                conditionEventId);

        resources.ApplyPrepared(wealth.ResourcePlan);
        estates.CommitPrepared(estate.EstatePlan);
        succession.ApplyPrepared(resolution.SuccessionPlan);

        Assert.Equal(0, resources.GetWealth(Subject));
        Assert.Equal(42, resources.GetWealth(FirstChild));
        Assert.All(
            estates.Holdings,
            item => Assert.Equal(FirstChild, item.OwnerCharacterId));
        Assert.Equal(
            FirstChild,
            succession.CampaignContinuity!.ControlledCharacterId);
        Assert.True(succession.TryGetResolutionForSubject(
            Subject,
            out SuccessionResolutionState? stored));
        Assert.Equal(Serialize(resolution.Resolution), Serialize(stored));
        Assert.Equal(
            Serialize(succession.CaptureSnapshot()),
            Serialize(new CharacterSuccessionWorldState(
                succession.CaptureSnapshot(),
                characters,
                new CampaignCalendar(
                    resolutionDate,
                    resolutionTurnIndex)).CaptureSnapshot()));
        Assert.Throws<SimulationValidationException>(() =>
            succession.PlanResolutionDecision(
                Subject,
                decision.Rule,
                StubMarriageQuery.Empty,
                StubGuardianshipQuery.Empty,
                null,
                resolutionDate,
                resolutionTurnIndex));
    }

    [Fact]
    public void WealthInheritance_RejectsOverflowWithoutMutation()
    {
        CharacterWorldState characters = CreateCharacters(
            Seed(Subject, new(170, 1, 1)),
            Seed(FirstChild, new(180, 1, 1)));
        CharacterResourceWorldState resources = new(
            CharacterResourceWorldSnapshot.Empty with
            {
                Accounts =
                [
                    Account(Subject, 1),
                    Account(FirstChild, long.MaxValue),
                ],
            },
            characters,
            Calendar);
        CharacterResourceWorldSnapshot before = resources.CaptureSnapshot();
        EntityId commandId = new("command:test/inheritance-overflow");

        Assert.Throws<SimulationValidationException>(() =>
            resources.PrepareInheritance(
                Subject,
                FirstChild,
                Date,
                Calendar.TurnIndex,
                commandId,
                CharacterResourceIds.DeriveActionEventId(Date, commandId)));
        Assert.Equal(Serialize(before), Serialize(resources.CaptureSnapshot()));
    }

    [Fact]
    public void ResolutionRetention_FoldsTheOldestCanonicalRecordAtTheBound()
    {
        EntityId[] subjects = Enumerable.Range(
                0,
                CharacterSuccessionLimits.RecentSuccessionResolutions + 1)
            .Select(index => Character($"retention-{index:D3}"))
            .ToArray();
        CharacterWorldState characters = CreateCharacters(subjects
            .Select(subject => Seed(subject, new(170, 1, 1)))
            .ToArray());
        CharacterSuccessionWorldState succession = NewSuccession(characters);
        SuccessionResolutionRule rule = Rule(
            [SuccessionLegalBasis.BiologicalDescendant],
            includesPrincipalSpouse: false,
            []);

        foreach ((EntityId subject, int index) in subjects.Select(
                     (subject, index) => (subject, index)))
        {
            EntityId commandId =
                new($"command:test/f9-retention-{index:D3}");
            EntityId eventId = CharacterConditionIds.DeriveActionEventId(
                Date,
                commandId);
            SuccessionResolutionDecision decision =
                succession.PlanResolutionDecision(
                    subject,
                    rule,
                    StubMarriageQuery.Empty,
                    StubGuardianshipQuery.Empty,
                    null,
                    Date,
                    Calendar.TurnIndex);
            CharacterSuccessionResolutionPlan plan =
                succession.PrepareResolution(
                    decision,
                    CharacterConditionIds.DeriveDeathId(eventId, subject),
                    new(
                        CharacterSuccessionContractVersions.Inheritance,
                        null,
                        []),
                    Date,
                    Calendar.TurnIndex,
                    commandId,
                    eventId);
            succession.ApplyPrepared(plan.SuccessionPlan);
        }

        Assert.Equal(
            CharacterSuccessionLimits.RecentSuccessionResolutions,
            succession.Resolutions.Count);
        Assert.Equal(1, succession.ResolutionHistory.FoldedNoSuccessorCount);
        Assert.Equal(0, succession.ResolutionHistory.FoldedSelectedCount);
        Assert.Equal(0, succession.ResolutionHistory.FoldedDisputedCount);
        Assert.Equal(Date, succession.ResolutionHistory.EarliestDate);
        Assert.Equal(Date, succession.ResolutionHistory.LatestDate);
        Assert.Single(
            subjects,
            subject => !succession.TryGetResolutionForSubject(subject, out _));
        Assert.Equal(
            Serialize(succession.CaptureSnapshot()),
            Serialize(new CharacterSuccessionWorldState(
                succession.CaptureSnapshot(),
                characters,
                Calendar).CaptureSnapshot()));
    }

    [Fact]
    public void ResolutionRetention_AllowsAValidPartiallyFoldedSameDayContinuityChain()
    {
        EntityId controlledSuccessor = Character("retention-successor");
        EntityId[] unrelated = Enumerable.Range(
                0,
                CharacterSuccessionLimits.RecentSuccessionResolutions)
            .Select(index => Character($"retention-unrelated-{index:D3}"))
            .ToArray();
        CharacterWorldState characters = CreateCharacters(
            [
                Seed(Subject, new(170, 1, 1)),
                Seed(
                    controlledSuccessor,
                    new(180, 1, 1),
                    Parent(Subject)),
                .. unrelated.Select(subject =>
                    Seed(subject, new(171, 1, 1))),
            ]);
        PlayerCampaignContinuityState continuity = new(
            CharacterSuccessionContractVersions.CampaignContinuity,
            PlayerCampaignContinuityStatus.Active,
            Subject,
            Date.AddDays(-1),
            Calendar.TurnIndex,
            new("command:test/f9-retention-continuity"),
            new("event:test/f9-retention-continuity"));
        CharacterSuccessionWorldState succession = NewSuccession(
            characters,
            continuity);
        SuccessionResolutionRule rule = Rule(
            [SuccessionLegalBasis.BiologicalDescendant],
            includesPrincipalSpouse: false,
            []);
        (EntityId Subject, EntityId CommandId, EntityId EventId,
            EntityId ResolutionId)[] unrelatedCoordinates = unrelated
            .Select((subject, index) =>
            {
                EntityId commandId =
                    new($"command:test/f9-partial-{index:D3}");
                EntityId eventId =
                    CharacterConditionIds.DeriveActionEventId(
                        Date,
                        commandId);
                return (
                    subject,
                    commandId,
                    eventId,
                    CharacterSuccessionIds.DeriveResolutionId(
                        eventId,
                        subject));
            })
            .ToArray();
        EntityId minimumUnrelated = unrelatedCoordinates
            .Select(item => item.ResolutionId)
            .Min();
        EntityId controlledCommandId = default;
        EntityId controlledEventId = default;
        for (int index = 0; index < 100_000; index++)
        {
            EntityId candidateCommand =
                new($"command:test/f9-partial-controlled-{index:D5}");
            EntityId candidateEvent =
                CharacterConditionIds.DeriveActionEventId(
                    Date,
                    candidateCommand);
            if (CharacterSuccessionIds.DeriveResolutionId(
                    candidateEvent,
                    Subject)
                .CompareTo(minimumUnrelated) < 0)
            {
                controlledCommandId = candidateCommand;
                controlledEventId = candidateEvent;
                break;
            }
        }

        Assert.True(controlledCommandId.IsValid);

        void ApplyResolution(
            EntityId subject,
            EntityId commandId,
            EntityId eventId)
        {
            SuccessionResolutionDecision decision =
                succession.PlanResolutionDecision(
                    subject,
                    rule,
                    StubMarriageQuery.Empty,
                    StubGuardianshipQuery.Empty,
                    null,
                    Date,
                    Calendar.TurnIndex);
            CharacterSuccessionResolutionPlan plan =
                succession.PrepareResolution(
                    decision,
                    CharacterConditionIds.DeriveDeathId(
                        eventId,
                        subject),
                    new(
                        CharacterSuccessionContractVersions.Inheritance,
                        null,
                        []),
                    Date,
                    Calendar.TurnIndex,
                    commandId,
                    eventId);
            succession.ApplyPrepared(plan.SuccessionPlan);
        }

        foreach (var coordinates in unrelatedCoordinates.Take(
                     unrelatedCoordinates.Length / 2))
        {
            ApplyResolution(
                coordinates.Subject,
                coordinates.CommandId,
                coordinates.EventId);
        }

        ApplyResolution(
            Subject,
            controlledCommandId,
            controlledEventId);
        foreach (var coordinates in unrelatedCoordinates.Skip(
                     unrelatedCoordinates.Length / 2))
        {
            ApplyResolution(
                coordinates.Subject,
                coordinates.CommandId,
                coordinates.EventId);
        }

        Assert.Equal(
            controlledSuccessor,
            succession.CampaignContinuity!.ControlledCharacterId);
        Assert.Equal(1, succession.ResolutionHistory.FoldedSelectedCount);
        Assert.DoesNotContain(
            succession.Resolutions,
            item => item.SubjectCharacterId == Subject);
        Assert.Equal(
            Serialize(succession.CaptureSnapshot()),
            Serialize(new CharacterSuccessionWorldState(
                succession.CaptureSnapshot(),
                characters,
                Calendar).CaptureSnapshot()));
        CharacterSuccessionWorldSnapshot tampered =
            succession.CaptureSnapshot() with
            {
                CampaignContinuity = succession.CampaignContinuity! with
                {
                    ControlledCharacterId = unrelated[0],
                },
            };
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterSuccessionWorldState(
                tampered,
                characters,
                Calendar));
    }

    [Fact]
    public void ResolutionIdentityReuse_IsRejectedDuringPlanningAndRestore()
    {
        EntityId otherSubject = Character("identity-other");
        CharacterWorldState characters = CreateCharacters(
            Seed(Subject, new(170, 1, 1)),
            Seed(otherSubject, new(171, 1, 1)));
        CharacterSuccessionWorldState succession = NewSuccession(characters);
        SuccessionResolutionRule rule = Rule(
            [SuccessionLegalBasis.BiologicalDescendant],
            includesPrincipalSpouse: false,
            []);
        EntityId commandId = new("command:test/f9-shared-resolution");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(
            Date,
            commandId);
        SuccessionResolutionDecision firstDecision =
            succession.PlanResolutionDecision(
                Subject,
                rule,
                StubMarriageQuery.Empty,
                StubGuardianshipQuery.Empty,
                null,
                Date,
                Calendar.TurnIndex);
        CharacterSuccessionResolutionPlan first =
            succession.PrepareResolution(
                firstDecision,
                CharacterConditionIds.DeriveDeathId(eventId, Subject),
                new(
                    CharacterSuccessionContractVersions.Inheritance,
                    null,
                    []),
                Date,
                Calendar.TurnIndex,
                commandId,
                eventId);
        succession.ApplyPrepared(first.SuccessionPlan);
        SuccessionResolutionDecision secondDecision =
            succession.PlanResolutionDecision(
                otherSubject,
                rule,
                StubMarriageQuery.Empty,
                StubGuardianshipQuery.Empty,
                null,
                Date,
                Calendar.TurnIndex);

        Assert.Throws<SimulationValidationException>(() =>
            succession.PrepareResolution(
                secondDecision,
                CharacterConditionIds.DeriveDeathId(eventId, otherSubject),
                new(
                    CharacterSuccessionContractVersions.Inheritance,
                    null,
                    []),
                Date,
                Calendar.TurnIndex,
                commandId,
                eventId));

        SuccessionResolutionState forged = first.Resolution with
        {
            ResolutionId = CharacterSuccessionIds.DeriveResolutionId(
                eventId,
                otherSubject),
            SubjectCharacterId = otherSubject,
            DeathId = CharacterConditionIds.DeriveDeathId(
                eventId,
                otherSubject),
        };
        CharacterSuccessionWorldSnapshot malformed =
            succession.CaptureSnapshot() with
            {
                Resolutions = [first.Resolution, forged],
            };
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterSuccessionWorldState(
                malformed,
                characters,
                Calendar));
    }

    private static CharacterSuccessionWorldState NewSuccession(
        CharacterWorldState characters,
        PlayerCampaignContinuityState? continuity = null) => new(
        CharacterSuccessionWorldSnapshot.Empty with
        {
            CampaignContinuity = continuity,
        },
        characters,
        Calendar);

    private static SuccessionResolutionRule Rule(
        IReadOnlyList<SuccessionLegalBasis> precedence,
        bool includesPrincipalSpouse,
        IReadOnlyList<ParentChildLinkKind> collateralKinds,
        SuccessionContestResolutionMode contestMode =
            SuccessionContestResolutionMode.ResolveByStableId,
        IReadOnlyList<SuccessionCandidateBasis>? candidateBases = null) => new(
        CharacterSuccessionContractVersions.ResolutionRule,
        new(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            candidateBases ?? [SuccessionCandidateBasis.BiologicalDescendant],
            8,
            0,
            AllowsIncapacitatedCandidates: true,
            Enum.GetValues<CharacterCustodyStatus>()),
        precedence,
        includesPrincipalSpouse,
        collateralKinds,
        collateralKinds.Count == 0 ? 0 : 8,
        contestMode,
        32,
        8,
        CreatesRegencyForIncapacitatedSuccessor: true,
        SuccessionNoAcceptedSuccessorBehavior.EndCampaign);

    private static CharacterWorldState CreateCharacters(params CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds.Select(item =>
        {
            EntityId nameKey = new($"loc:{item.Id.Value.Replace(':', '/')}");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                item.Id,
                nameKey,
                item.BirthDate,
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(nameKey, null),
                CharacterContentOrigin.LegacyUnknown(item.Id),
                null,
                null,
                []);
        }).ToArray();
        CharacterState[] states = seeds.Select(item => new CharacterState(
            CharacterContractVersions.State,
            item.Id,
            item.Parents.Select(parent => parent.ParentCharacterId).ToArray(),
            item.Parents,
            CharacterConditionState.Default,
            [])).ToArray();
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

    private static CharacterWealthAccountState Account(
        EntityId characterId,
        long wealth) => new(
        CharacterResourceContractVersions.State,
        CharacterResourceIds.DeriveWealthAccountId(characterId),
        characterId,
        wealth);

    private static CharacterEstateHoldingState Holding(
        string estateId,
        EntityId owner) => new(
        CharacterEstateHoldingContractVersions.State,
        new(estateId),
        owner);

    private static MarriageUnionState Union(
        string suffix,
        EntityId first,
        EntityId second,
        MarriageUnionForm form) => new(
        CharacterMarriageContractVersions.State,
        new($"marriage_union:test/{suffix}"),
        first,
        second,
        form,
        form == MarriageUnionForm.Concubinage ? first : null,
        MarriageBasis.Political,
        MarriageConsentKind.PoliticalArrangement,
        new("marriage_practice:test/default"),
        new($"marriage_proposal:test/{suffix}"),
        Date,
        0,
        MarriageUnionStatus.Active,
        null,
        null,
        null,
        null);

    private static CharacterParentLink Parent(EntityId parent) =>
        new(parent, ParentChildLinkKind.Biological);

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        params CharacterParentLink[] parents) =>
        new(id, birthDate, parents);

    private static EntityId Character(string suffix) =>
        new($"character:test/f9-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        IReadOnlyList<CharacterParentLink> Parents);

    private sealed class StubMarriageQuery(params MarriageUnionState[] unions)
        : IAuthoritativeCharacterMarriageWorldQuery
    {
        public static StubMarriageQuery Empty { get; } = new();
        public IReadOnlyList<MarriagePracticeState> Practices => [];
        public IReadOnlyList<MarriageProposalState> Proposals => [];
        public IReadOnlyList<PoliticalBetrothalState> Betrothals => [];
        public IReadOnlyList<MarriageUnionState> Unions { get; } = unions;
        public IReadOnlyList<RomanceRouteState> RomanceRoutes => [];
        public IReadOnlyList<RomanceInvitationState> RomanceInvitations => [];
        public IReadOnlyList<CharacterMarriageHistoryAggregate> History => [];
        public bool TryGetPractice(EntityId practiceId, [NotNullWhen(true)] out MarriagePracticeState? practice) { practice = null; return false; }
        public bool TryGetProposal(EntityId proposalId, [NotNullWhen(true)] out MarriageProposalState? proposal) { proposal = null; return false; }
        public bool TryGetBetrothal(EntityId betrothalId, [NotNullWhen(true)] out PoliticalBetrothalState? betrothal) { betrothal = null; return false; }
        public bool TryGetUnion(EntityId unionId, [NotNullWhen(true)] out MarriageUnionState? union) { union = Unions.FirstOrDefault(item => item.UnionId == unionId); return union is not null; }
        public bool TryGetRomanceRoute(EntityId routeId, [NotNullWhen(true)] out RomanceRouteState? route) { route = null; return false; }
        public bool TryGetRomanceInvitation(EntityId invitationId, [NotNullWhen(true)] out RomanceInvitationState? invitation) { invitation = null; return false; }
        public bool TryGetHistory(EntityId characterId, [NotNullWhen(true)] out CharacterMarriageHistoryAggregate? history) { history = null; return false; }
        public IReadOnlyList<MarriageProposalState> GetProposalsInvolving(EntityId characterId) => [];
        public IReadOnlyList<PoliticalBetrothalState> GetBetrothalsInvolving(EntityId characterId) => [];
        public IReadOnlyList<MarriageUnionState> GetUnionsInvolving(EntityId characterId) => Unions.Where(item => item.FirstCharacterId == characterId || item.SecondCharacterId == characterId).ToArray();
        public IReadOnlyList<RomanceRouteState> GetRomanceRoutesInvolving(EntityId characterId) => [];
        public IReadOnlyList<RomanceInvitationState> GetRomanceInvitationsInvolving(EntityId characterId) => [];
        public MarriageEligibilityResult EvaluateEligibility(MarriageEligibilityRequest request, CampaignDate date) => throw new NotSupportedException();
    }

    private sealed class StubGuardianshipQuery : IAuthoritativeCharacterGuardianshipWorldQuery
    {
        public static StubGuardianshipQuery Empty { get; } = new();
        public IReadOnlyList<CharacterGuardianshipState> Guardianships => [];
        public bool TryGetActivePrimaryGuardianshipForWard(EntityId wardCharacterId, [NotNullWhen(true)] out CharacterGuardianshipState? guardianship) { guardianship = null; return false; }
        public IReadOnlyList<CharacterGuardianshipState> GetGuardianshipsInvolving(EntityId characterId) => [];
    }
}
