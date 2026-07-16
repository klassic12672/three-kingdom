using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionCandidateEvaluationTests(ITestOutputHelper output)
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId Candidate = Character("candidate");
    private static readonly EntityId Other = Character("other");

    [Fact]
    public void F502_QuerySurfaceIsExplicitlyVersionedAndSchemaNeutral()
    {
        System.Reflection.MethodInfo evaluate = Assert.Single(
            typeof(IAuthoritativeCharacterSuccessionWorldQuery).GetMethods(),
            method => method.Name == nameof(
                IAuthoritativeCharacterSuccessionWorldQuery.EvaluateCandidate));
        Assert.Equal(typeof(SuccessionCandidateEvaluationResult), evaluate.ReturnType);
        Assert.Equal(
            [typeof(SuccessionCandidateEvaluationRequest)],
            evaluate.GetParameters().Select(parameter => parameter.ParameterType));

        Type[] contracts =
        [
            typeof(SuccessionCandidateEligibilityRule),
            typeof(SuccessionCandidateEvaluationRequest),
            typeof(SuccessionCandidateBasisEvidence),
            typeof(SuccessionCandidateEligibilityIssue),
            typeof(SuccessionCandidateEvaluationResult),
        ];
        Assert.All(contracts, contract => Assert.NotNull(contract.GetProperty("ContractVersion")));
        Assert.All(contracts, contract => Assert.False(
            typeof(ICampaignCommandPayload).IsAssignableFrom(contract)));
        Assert.All(contracts, contract => Assert.False(
            typeof(ICampaignEventPayload).IsAssignableFrom(contract)));
        Assert.Equal(6, CharacterSuccessionContractVersions.AuthoritativeQuery);
        Assert.Equal(4, CharacterSuccessionContractVersions.Snapshot);
        Assert.Equal(4, CharacterSuccessionSystem.Version);
        Assert.Equal(29, SaveEnvelope.CurrentSchemaVersion);
    }

    [Fact]
    public void F503_TypedDescendantPathsRemainDistinctAndUseShortestRecognizedGeneration()
    {
        EntityId biologicalChild = Character("biological-child");
        EntityId adoptiveChild = Character("adoptive-child");
        EntityId legacyChild = Character("legacy-child");
        EntityId multiPathGrandchild = Character("multi-path-grandchild");
        EntityId deepOnlyGrandchild = Character("deep-only-grandchild");
        EntityId ancestor = Character("ancestor");
        EntityId sibling = Character("sibling");
        WorldState world = CreateWorld(
            Seed(ancestor, new CampaignDate(120, 1, 1)),
            Seed(
                Subject,
                new CampaignDate(150, 1, 1),
                Parent(ancestor, ParentChildLinkKind.Biological)),
            Seed(
                sibling,
                new CampaignDate(151, 1, 1),
                Parent(ancestor, ParentChildLinkKind.Biological)),
            Seed(
                biologicalChild,
                new CampaignDate(180, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological)),
            Seed(
                adoptiveChild,
                new CampaignDate(181, 1, 1),
                Parent(Subject, ParentChildLinkKind.LegalAdoptive)),
            Seed(
                legacyChild,
                new CampaignDate(182, 1, 1),
                Parent(Subject, ParentChildLinkKind.UnspecifiedLegacy)),
            Seed(
                multiPathGrandchild,
                new CampaignDate(195, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological),
                Parent(biologicalChild, ParentChildLinkKind.Biological),
                Parent(adoptiveChild, ParentChildLinkKind.Biological)),
            Seed(
                deepOnlyGrandchild,
                new CampaignDate(196, 1, 1),
                Parent(biologicalChild, ParentChildLinkKind.Biological),
                Parent(legacyChild, ParentChildLinkKind.Biological)));
        SuccessionCandidateEligibilityRule allDescendants = Rule(
            [
                SuccessionCandidateBasis.BiologicalDescendant,
                SuccessionCandidateBasis.LegalAdoptiveDescendant,
                SuccessionCandidateBasis.UnspecifiedLegacyDescendant,
            ],
            maximumGeneration: 2);

        AssertBasis(
            Evaluate(world, biologicalChild, allDescendants),
            SuccessionCandidateBasis.BiologicalDescendant,
            generation: 1);
        AssertBasis(
            Evaluate(world, adoptiveChild, allDescendants),
            SuccessionCandidateBasis.LegalAdoptiveDescendant,
            generation: 1);
        AssertBasis(
            Evaluate(world, legacyChild, allDescendants),
            SuccessionCandidateBasis.UnspecifiedLegacyDescendant,
            generation: 1);

        SuccessionCandidateEvaluationResult multiPath = Evaluate(
            world,
            multiPathGrandchild,
            allDescendants);
        Assert.True(multiPath.IsEligible);
        Assert.Equal(
            [
                (SuccessionCandidateBasis.BiologicalDescendant, 1),
                (SuccessionCandidateBasis.LegalAdoptiveDescendant, 2),
            ],
            multiPath.RecognizedBases.Select(item =>
                (item.Basis, item.DescendantGeneration!.Value)));

        SuccessionCandidateEvaluationResult recursiveLegacy = Evaluate(
            world,
            deepOnlyGrandchild,
            allDescendants);
        Assert.Equal(
            [
                (SuccessionCandidateBasis.BiologicalDescendant, 2),
                (SuccessionCandidateBasis.UnspecifiedLegacyDescendant, 2),
            ],
            recursiveLegacy.RecognizedBases.Select(item =>
                (item.Basis, item.DescendantGeneration!.Value)));

        SuccessionCandidateEvaluationResult directOnly = Evaluate(
            world,
            deepOnlyGrandchild,
            allDescendants with { MaximumDescendantGeneration = 1 });
        Assert.False(directOnly.IsEligible);
        Assert.Empty(directOnly.RecognizedBases);
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.NoRecognizedBasis],
            Reasons(directOnly));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.NoRecognizedBasis],
            Reasons(Evaluate(world, ancestor, allDescendants)));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.NoRecognizedBasis],
            Reasons(Evaluate(world, sibling, allDescendants)));
    }

    [Fact]
    public void F504_OnlyCurrentPermittedDesignationQualifiesAndMultipleBasesDoNotCollapse()
    {
        EntityId activeNominee = Character("active-nominee");
        EntityId revokedNominee = Character("revoked-nominee");
        HeirDesignationState active = ActiveDesignation(Subject, activeNominee, "active");
        HeirDesignationState revoked = RevokedDesignation(Subject, revokedNominee, "revoked");
        WorldState world = CreateWorld(
            [active, revoked],
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(activeNominee, new CampaignDate(180, 1, 1)),
            Seed(revokedNominee, new CampaignDate(181, 1, 1)));

        SuccessionCandidateEligibilityRule designationOnly = Rule(
            [SuccessionCandidateBasis.ActiveDesignation]);
        SuccessionCandidateEvaluationResult recognized = Evaluate(
            world,
            activeNominee,
            designationOnly);
        Assert.True(recognized.IsEligible);
        SuccessionCandidateBasisEvidence designation = Assert.Single(
            recognized.RecognizedBases);
        Assert.Equal(SuccessionCandidateBasis.ActiveDesignation, designation.Basis);
        Assert.Equal(active.DesignationId, designation.SourceDesignationId);
        Assert.Null(designation.DescendantGeneration);

        SuccessionCandidateEvaluationResult terminal = Evaluate(
            world,
            revokedNominee,
            designationOnly);
        Assert.False(terminal.IsEligible);
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.NoRecognizedBasis],
            Reasons(terminal));

        SuccessionCandidateEvaluationResult ignored = Evaluate(
            world,
            activeNominee,
            Rule([SuccessionCandidateBasis.BiologicalDescendant]));
        Assert.False(ignored.IsEligible);
        Assert.Empty(ignored.RecognizedBases);

        WorldState overlapping = CreateWorld(
            [ActiveDesignation(Subject, Candidate, "overlap")],
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(180, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological)));
        SuccessionCandidateEvaluationResult both = Evaluate(
            overlapping,
            Candidate,
            Rule(
            [
                SuccessionCandidateBasis.BiologicalDescendant,
                SuccessionCandidateBasis.ActiveDesignation,
            ]));
        Assert.True(both.IsEligible);
        Assert.Equal(
            [
                SuccessionCandidateBasis.ActiveDesignation,
                SuccessionCandidateBasis.BiologicalDescendant,
            ],
            both.RecognizedBases.Select(item => item.Basis));
    }

    [Fact]
    public void F505_AgeCapacityAndEveryCustodyStateAreExplicitRuleInputs()
    {
        EntityId custodian = Character("custodian");
        CharacterConditionState restrictedCondition = CharacterConditionState.Default with
        {
            IsIncapacitated = true,
            CustodyStatus = CharacterCustodyStatus.Captive,
            CustodianId = custodian,
        };
        WorldState restricted = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(custodian, new CampaignDate(151, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(190, 5, 11),
                restrictedCondition,
                Parent(Subject, ParentChildLinkKind.Biological)));

        SuccessionCandidateEvaluationResult denied = Evaluate(
            restricted,
            Candidate,
            Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                minimumAge: 18,
                allowsIncapacitated: false,
                allowedCustodyStatuses: [CharacterCustodyStatus.Free]));
        Assert.False(denied.IsEligible);
        Assert.Equal(
            [
                SuccessionCandidateEligibilityReason.CandidateBelowMinimumAge,
                SuccessionCandidateEligibilityReason.CandidateIncapacitated,
                SuccessionCandidateEligibilityReason.CandidateCustodyNotAllowed,
            ],
            Reasons(denied));

        SuccessionCandidateEvaluationResult permitted = Evaluate(
            restricted,
            Candidate,
            Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                minimumAge: 0,
                allowsIncapacitated: true,
                allowedCustodyStatuses: [CharacterCustodyStatus.Captive]));
        Assert.True(permitted.IsEligible);

        foreach (CharacterCustodyStatus status in Enum.GetValues<CharacterCustodyStatus>())
        {
            CharacterConditionState condition = status == CharacterCustodyStatus.Free
                ? CharacterConditionState.Default
                : CharacterConditionState.Default with
                {
                    CustodyStatus = status,
                    CustodianId = custodian,
                };
            WorldState custodyWorld = CreateWorld(
                Seed(Subject, new CampaignDate(150, 1, 1)),
                Seed(custodian, new CampaignDate(151, 1, 1)),
                Seed(
                    Candidate,
                    new CampaignDate(182, 5, 10),
                    condition,
                    Parent(Subject, ParentChildLinkKind.Biological)));
            Assert.True(Evaluate(
                custodyWorld,
                Candidate,
                Rule(
                    [SuccessionCandidateBasis.BiologicalDescendant],
                    minimumAge: 18,
                    allowedCustodyStatuses: [status])).IsEligible);
        }

        WorldState dead = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(180, 1, 1),
                DeadCondition(),
                Parent(Subject, ParentChildLinkKind.Biological)));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.CandidateDead],
            Reasons(Evaluate(
                dead,
                Candidate,
                Rule(
                    [SuccessionCandidateBasis.BiologicalDescendant],
                    allowsIncapacitated: true))));
    }

    [Fact]
    public void F506_SubjectConditionNeverSelectsOrDisqualifiesTheCandidate()
    {
        EntityId custodian = Character("subject-custodian");
        CharacterConditionState[] subjectConditions =
        [
            CharacterConditionState.Default,
            CharacterConditionState.Default with { IsIncapacitated = true },
            CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Detained,
                CustodianId = custodian,
            },
            DeadCondition(),
        ];
        foreach (CharacterConditionState subjectCondition in subjectConditions)
        {
            WorldState world = CreateWorld(
                Seed(Subject, new CampaignDate(150, 1, 1), subjectCondition),
                Seed(custodian, new CampaignDate(151, 1, 1)),
                Seed(
                    Candidate,
                    new CampaignDate(180, 1, 1),
                    Parent(Subject, ParentChildLinkKind.LegalAdoptive)));
            SuccessionCandidateEvaluationResult result = Evaluate(
                world,
                Candidate,
                Rule([SuccessionCandidateBasis.LegalAdoptiveDescendant]));
            Assert.True(result.IsEligible);
        }
    }

    [Fact]
    public void F507_InvalidParticipantsAndCompoundFailuresAreCanonicalAndControlled()
    {
        EntityId custodian = Character("compound-custodian");
        WorldState world = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(custodian, new CampaignDate(151, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(195, 1, 1),
                CharacterConditionState.Default with
                {
                    IsIncapacitated = true,
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = custodian,
                },
                Parent(Subject, ParentChildLinkKind.Biological)),
            Seed(Other, new CampaignDate(180, 1, 1)));
        SuccessionCandidateEligibilityRule restrictive = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            minimumAge: 18,
            allowsIncapacitated: false,
            allowedCustodyStatuses: [CharacterCustodyStatus.Free]);

        Assert.Equal(
            [
                SuccessionCandidateEligibilityReason.CandidateBelowMinimumAge,
                SuccessionCandidateEligibilityReason.CandidateIncapacitated,
                SuccessionCandidateEligibilityReason.CandidateCustodyNotAllowed,
            ],
            Reasons(Evaluate(world, Candidate, restrictive)));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.SameCharacter],
            Reasons(Evaluate(world, Subject, restrictive)));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.NoRecognizedBasis],
            Reasons(Evaluate(world, Other, restrictive)));

        SuccessionCandidateEvaluationResult unknown = world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                Subject,
                Character("missing"),
                restrictive));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.UnknownCandidate],
            Reasons(unknown));
        Assert.Equal(Character("missing"), unknown.CandidateCharacterId);
        AssertJsonRoundTrip(unknown);

        SuccessionCandidateEvaluationResult unknownSubject = world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                Character("missing-subject"),
                Candidate,
                restrictive));
        Assert.Equal(
            [
                SuccessionCandidateEligibilityReason.UnknownSubject,
                SuccessionCandidateEligibilityReason.CandidateBelowMinimumAge,
                SuccessionCandidateEligibilityReason.CandidateIncapacitated,
                SuccessionCandidateEligibilityReason.CandidateCustodyNotAllowed,
            ],
            Reasons(unknownSubject));
        Assert.Equal(Character("missing-subject"), unknownSubject.SubjectCharacterId);
        AssertJsonRoundTrip(unknownSubject);

        SuccessionCandidateEvaluationResult invalidCandidate = world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                Subject,
                default,
                restrictive));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.InvalidCandidate],
            Reasons(invalidCandidate));
        Assert.Null(invalidCandidate.CandidateCharacterId);
        AssertJsonRoundTrip(invalidCandidate);

        SuccessionCandidateEvaluationResult invalid = world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                default,
                Candidate,
                restrictive));
        Assert.Equal(
            [
                SuccessionCandidateEligibilityReason.InvalidSubject,
                SuccessionCandidateEligibilityReason.CandidateBelowMinimumAge,
                SuccessionCandidateEligibilityReason.CandidateIncapacitated,
                SuccessionCandidateEligibilityReason.CandidateCustodyNotAllowed,
            ],
            Reasons(invalid));
        Assert.Null(invalid.SubjectCharacterId);
        AssertJsonRoundTrip(invalid);

        Assert.Throws<SimulationValidationException>(() => CreateWorld(
            Seed(Subject, Date.AddDays(1)),
            Seed(Candidate, new CampaignDate(180, 1, 1))));
        Assert.Throws<SimulationValidationException>(() => CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(Candidate, Date.AddDays(1))));
    }

    [Fact]
    public void F507_MalformedRuleShapesReturnExactStableIssues()
    {
        WorldState world = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(180, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological)));
        (SuccessionCandidateEligibilityRule Rule, SuccessionCandidateEligibilityReason Reason)[] cases =
        [
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    ContractVersion = 99,
                }, SuccessionCandidateEligibilityReason.UnsupportedRuleVersion),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    AllowedBases = [],
                }, SuccessionCandidateEligibilityReason.MissingAllowedBasis),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    AllowedBases = null!,
                }, SuccessionCandidateEligibilityReason.MissingAllowedBasis),
            (Rule([(SuccessionCandidateBasis)99]),
                SuccessionCandidateEligibilityReason.UnsupportedAllowedBasis),
            (Rule([
                    SuccessionCandidateBasis.BiologicalDescendant,
                    SuccessionCandidateBasis.BiologicalDescendant,
                ]), SuccessionCandidateEligibilityReason.DuplicateAllowedBasis),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    MaximumDescendantGeneration = 0,
                }, SuccessionCandidateEligibilityReason.InvalidMaximumDescendantGeneration),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    MaximumDescendantGeneration =
                        CharacterSuccessionLimits.MaximumEvaluatedDescendantGeneration + 1,
                }, SuccessionCandidateEligibilityReason.InvalidMaximumDescendantGeneration),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    MinimumCandidateAge = -1,
                }, SuccessionCandidateEligibilityReason.InvalidMinimumCandidateAge),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    MinimumCandidateAge =
                        CharacterSuccessionLimits.MaximumConfiguredMinimumCandidateAge + 1,
                }, SuccessionCandidateEligibilityReason.InvalidMinimumCandidateAge),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    AllowedCustodyStatuses = [],
                }, SuccessionCandidateEligibilityReason.MissingAllowedCustodyStatus),
            (Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
                {
                    AllowedCustodyStatuses = null!,
                }, SuccessionCandidateEligibilityReason.MissingAllowedCustodyStatus),
            (Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                allowedCustodyStatuses: [(CharacterCustodyStatus)99]),
                SuccessionCandidateEligibilityReason.UnsupportedAllowedCustodyStatus),
            (Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                allowedCustodyStatuses:
                [
                    CharacterCustodyStatus.Free,
                    CharacterCustodyStatus.Free,
                ]), SuccessionCandidateEligibilityReason.DuplicateAllowedCustodyStatus),
        ];

        foreach ((SuccessionCandidateEligibilityRule malformed, SuccessionCandidateEligibilityReason reason) in cases)
        {
            SuccessionCandidateEvaluationResult result = Evaluate(world, Candidate, malformed);
            Assert.False(result.IsEligible);
            Assert.Empty(result.RecognizedBases);
            Assert.Equal([reason], Reasons(result));
            Assert.All(result.Issues, issue => Assert.Equal(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                issue.ContractVersion));
        }

        SuccessionCandidateEvaluationResult invalidRequest = world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                99,
                Subject,
                Candidate,
                Rule([SuccessionCandidateBasis.BiologicalDescendant])));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.InvalidRequest],
            Reasons(invalidRequest));
        Assert.Empty(invalidRequest.RecognizedBases);

        SuccessionCandidateEvaluationResult nullRule = world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                Subject,
                Candidate,
                null!));
        Assert.Equal(
            [SuccessionCandidateEligibilityReason.UnsupportedRuleVersion],
            Reasons(nullRule));

        SuccessionCandidateEvaluationResult nullRequest = world.CharacterSuccessions.EvaluateCandidate(
            null!);
        Assert.Equal(
            [
                SuccessionCandidateEligibilityReason.InvalidRequest,
                SuccessionCandidateEligibilityReason.UnsupportedRuleVersion,
                SuccessionCandidateEligibilityReason.InvalidSubject,
                SuccessionCandidateEligibilityReason.InvalidCandidate,
            ],
            Reasons(nullRequest));
        Assert.Null(nullRequest.SubjectCharacterId);
        Assert.Null(nullRequest.CandidateCharacterId);
        AssertJsonRoundTrip(nullRequest);

        SuccessionCandidateEvaluationResult exactUpperBounds = Evaluate(
            world,
            Candidate,
            Rule([SuccessionCandidateBasis.BiologicalDescendant]) with
            {
                MaximumDescendantGeneration =
                    CharacterSuccessionLimits.MaximumEvaluatedDescendantGeneration,
                MinimumCandidateAge =
                    CharacterSuccessionLimits.MaximumConfiguredMinimumCandidateAge,
            });
        Assert.DoesNotContain(
            exactUpperBounds.Issues,
            issue => issue.Reason is
                SuccessionCandidateEligibilityReason.InvalidMaximumDescendantGeneration
                or SuccessionCandidateEligibilityReason.InvalidMinimumCandidateAge);
    }

    [Fact]
    public void F508_EvaluationIsInputOrderInvariantDefensiveAndChecksumNeutral()
    {
        WorldState world = CreateWorld(
            [ActiveDesignation(Subject, Candidate, "purity")],
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(180, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological)));
        WorldSnapshot before = world.CaptureSnapshot();
        string beforeJson = Serialize(before);
        string beforeChecksum = SimulationChecksum.Compute(before).Value;
        SuccessionCandidateEligibilityRule firstRule = Rule(
            [
                SuccessionCandidateBasis.BiologicalDescendant,
                SuccessionCandidateBasis.ActiveDesignation,
            ],
            allowedCustodyStatuses:
            [
                CharacterCustodyStatus.Free,
                CharacterCustodyStatus.Hostage,
            ]);
        SuccessionCandidateEligibilityRule reversedRule = firstRule with
        {
            AllowedBases = firstRule.AllowedBases.Reverse().ToArray(),
            AllowedCustodyStatuses = firstRule.AllowedCustodyStatuses.Reverse().ToArray(),
        };

        SuccessionCandidateEvaluationResult first = Evaluate(world, Candidate, firstRule);
        SuccessionCandidateEvaluationResult reversed = Evaluate(world, Candidate, reversedRule);
        Assert.Equal(Serialize(first), Serialize(reversed));
        Assert.Equal(
            Serialize(firstRule.Canonicalize()),
            Serialize(reversedRule.Canonicalize()));

        SuccessionCandidateBasisEvidence[] exposed = Assert.IsType<
            SuccessionCandidateBasisEvidence[]>(first.RecognizedBases);
        exposed[0] = new SuccessionCandidateBasisEvidence(
            99,
            SuccessionCandidateBasis.UnspecifiedLegacyDescendant,
            64,
            new EntityId("heir_designation:test/forged"));

        SuccessionCandidateEvaluationResult repeated = Evaluate(world, Candidate, firstRule);
        Assert.Equal(Serialize(reversed), Serialize(repeated));
        Assert.Equal(beforeJson, Serialize(world.CaptureSnapshot()));
        Assert.Equal(beforeChecksum, SimulationChecksum.Compute(world.CaptureSnapshot()).Value);
    }

    [Fact]
    public void F509_QueryContractsRoundTripWithoutChangingSaveOrSystemVersions()
    {
        WorldState world = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(
                Candidate,
                new CampaignDate(180, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological)));
        SuccessionCandidateEligibilityRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant]);
        SuccessionCandidateEvaluationRequest request = new(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            Subject,
            Candidate,
            rule);
        SuccessionCandidateEvaluationResult result = world.CharacterSuccessions.EvaluateCandidate(
            request);

        Assert.Equal(
            Serialize(request),
            Serialize(JsonSerializer.Deserialize<SuccessionCandidateEvaluationRequest>(
                Serialize(request),
                SimulationJson.CreateOptions())!));
        Assert.Equal(
            Serialize(result),
            Serialize(JsonSerializer.Deserialize<SuccessionCandidateEvaluationResult>(
                Serialize(result),
                SimulationJson.CreateOptions())!));
        Assert.Equal(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            result.ContractVersion);
        Assert.All(result.RecognizedBases, basis => Assert.Equal(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            basis.ContractVersion));
        SuccessionCandidateEvaluationResult issueResult = Evaluate(
            world,
            Candidate,
            Rule([SuccessionCandidateBasis.ActiveDesignation]));
        SuccessionCandidateEligibilityIssue issue = Assert.Single(issueResult.Issues);
        Assert.Equal(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            issue.ContractVersion);
        Assert.Equal(
            Serialize(issueResult),
            Serialize(JsonSerializer.Deserialize<SuccessionCandidateEvaluationResult>(
                Serialize(issueResult),
                SimulationJson.CreateOptions())!));
        Assert.Equal(6, CharacterSuccessionContractVersions.AuthoritativeQuery);
        Assert.Equal(4, CharacterSuccessionContractVersions.Snapshot);
        Assert.Equal(4, CharacterSuccessionSystem.Version);
        Assert.Equal(29, SaveEnvelope.CurrentSchemaVersion);
        Assert.Equal(
            new SystemVersion(
                CharacterSuccessionSystem.SystemId,
                CharacterSuccessionSystem.Version),
            world.CaptureSnapshot().SystemVersions.Single(item =>
                item.SystemId == CharacterSuccessionSystem.SystemId));
    }

    [Fact]
    public void F510_ThousandCharacterPairwiseEvaluationRecordsRawLocalPerformance()
    {
        CharacterSeed[] seeds = Enumerable.Range(0, 1_000)
            .Select(index => index == 0
                ? Seed(Subject, new CampaignDate(150, 1, 1))
                : Seed(
                    Character($"scale-{index:D4}"),
                    new CampaignDate(180, 1, 1),
                    Parent(Subject, ParentChildLinkKind.Biological)))
            .ToArray();
        WorldState world = CreateWorld(seeds);
        SuccessionCandidateEligibilityRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            maximumGeneration: 1);
        Stopwatch evaluations = Stopwatch.StartNew();
        int eligible = 0;
        foreach (CharacterSeed seed in seeds.Skip(1))
        {
            if (Evaluate(world, seed.Id, rule).IsEligible)
            {
                eligible++;
            }
        }

        evaluations.Stop();
        Stopwatch snapshot = Stopwatch.StartNew();
        WorldSnapshot captured = world.CaptureSnapshot();
        string checksum = SimulationChecksum.Compute(captured).Value;
        byte[] json = Encoding.UTF8.GetBytes(Serialize(captured));
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
                   compressed,
                   CompressionLevel.SmallestSize,
                   leaveOpen: true))
        {
            gzip.Write(json);
        }

        snapshot.Stop();
        Assert.Equal(999, eligible);
        Assert.Equal(1_000, captured.Characters.CharacterDefinitions.Count);
        output.WriteLine(
            $"SP-04F5 1,000-character/999-pair evaluation: {evaluations.Elapsed.TotalMilliseconds:F3} ms; snapshot/checksum/JSON/gzip: {snapshot.Elapsed.TotalMilliseconds:F3} ms; JSON: {json.Length} bytes; gzip: {compressed.Length} bytes; checksum: {checksum}.");
    }

    private static SuccessionCandidateEvaluationResult Evaluate(
        WorldState world,
        EntityId candidateCharacterId,
        SuccessionCandidateEligibilityRule rule) =>
        world.CharacterSuccessions.EvaluateCandidate(
            new SuccessionCandidateEvaluationRequest(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                Subject,
                candidateCharacterId,
                rule));

    private static SuccessionCandidateEligibilityRule Rule(
        IReadOnlyList<SuccessionCandidateBasis> allowedBases,
        int maximumGeneration = CharacterSuccessionLimits.MaximumEvaluatedDescendantGeneration,
        int minimumAge = 0,
        bool allowsIncapacitated = true,
        IReadOnlyList<CharacterCustodyStatus>? allowedCustodyStatuses = null) => new(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            allowedBases,
            maximumGeneration,
            minimumAge,
            allowsIncapacitated,
            allowedCustodyStatuses ?? Enum.GetValues<CharacterCustodyStatus>());

    private static WorldState CreateWorld(params CharacterSeed[] seeds) =>
        CreateWorld([], seeds);

    private static WorldState CreateWorld(
        IReadOnlyList<HeirDesignationState> designations,
        params CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds
            .OrderBy(item => item.Id)
            .Select(item =>
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
            })
            .ToArray();
        CharacterState[] states = seeds
            .OrderBy(item => item.Id)
            .Select(item =>
            {
                CharacterParentLink[] parents = item.ParentLinks
                    .OrderBy(link => link.ParentCharacterId)
                    .Select(link => link with { })
                    .ToArray();
                return new CharacterState(
                    CharacterContractVersions.State,
                    item.Id,
                    parents.Select(link => link.ParentCharacterId).ToArray(),
                    parents,
                    item.Condition,
                    []);
            })
            .ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
        CharacterSuccessionWorldSnapshot successions = new(
            CharacterSuccessionContractVersions.Snapshot,
            designations.OrderBy(item => item.DesignationId).ToArray(),
            [],
            [],
            []);
        return WorldState.Create(
            Date,
            55,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            successions);
    }

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        params CharacterParentLink[] parentLinks) =>
        Seed(id, birthDate, CharacterConditionState.Default, parentLinks);

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        CharacterConditionState condition,
        params CharacterParentLink[] parentLinks) =>
        new(id, birthDate, condition, parentLinks);

    private static CharacterParentLink Parent(EntityId parentId, ParentChildLinkKind kind) =>
        new(parentId, kind);

    private static HeirDesignationState ActiveDesignation(
        EntityId designator,
        EntityId heir,
        string suffix)
    {
        EntityId sourceCommand = new($"command:test/f5-{suffix}-source");
        EntityId sourceEvent = CharacterSuccessionIds.DeriveActionEventId(Date, sourceCommand);
        return new HeirDesignationState(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(sourceEvent, designator, heir),
            designator,
            heir,
            Date,
            0,
            sourceCommand,
            sourceEvent,
            HeirDesignationStatus.Active,
            null,
            null,
            null,
            null);
    }

    private static HeirDesignationState RevokedDesignation(
        EntityId designator,
        EntityId heir,
        string suffix)
    {
        HeirDesignationState active = ActiveDesignation(designator, heir, suffix);
        EntityId resolutionCommand = new($"command:test/f5-{suffix}-resolution");
        EntityId resolutionEvent = CharacterSuccessionIds.DeriveActionEventId(
            Date,
            resolutionCommand);
        return active with
        {
            Status = HeirDesignationStatus.Revoked,
            ResolutionDate = Date,
            ResolutionTurnIndex = 0,
            ResolutionCommandId = resolutionCommand,
            ResolutionEventId = resolutionEvent,
        };
    }

    private static CharacterConditionState DeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        null);

    private static void AssertBasis(
        SuccessionCandidateEvaluationResult result,
        SuccessionCandidateBasis basis,
        int generation)
    {
        Assert.True(result.IsEligible);
        SuccessionCandidateBasisEvidence evidence = Assert.Single(result.RecognizedBases);
        Assert.Equal(basis, evidence.Basis);
        Assert.Equal(generation, evidence.DescendantGeneration);
        Assert.Null(evidence.SourceDesignationId);
    }

    private static SuccessionCandidateEligibilityReason[] Reasons(
        SuccessionCandidateEvaluationResult result) =>
        result.Issues.Select(item => item.Reason).ToArray();

    private static void AssertJsonRoundTrip(SuccessionCandidateEvaluationResult result) =>
        Assert.Equal(
            Serialize(result),
            Serialize(JsonSerializer.Deserialize<SuccessionCandidateEvaluationResult>(
                Serialize(result),
                SimulationJson.CreateOptions())!));

    private static EntityId Character(string suffix) =>
        new($"character:test/f5-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition,
        IReadOnlyList<CharacterParentLink> ParentLinks);
}
