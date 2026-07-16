using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionCandidateSetTests(ITestOutputHelper output)
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Subject = Character("subject");

    [Fact]
    public void F602_QuerySurfaceIsVersionedAndSchemaNeutral()
    {
        System.Reflection.MethodInfo find = Assert.Single(
            typeof(IAuthoritativeCharacterSuccessionWorldQuery).GetMethods(),
            method => method.Name == nameof(
                IAuthoritativeCharacterSuccessionWorldQuery.FindEligibleCandidates));
        Assert.Equal(typeof(SuccessionCandidateSetResult), find.ReturnType);
        Assert.Equal(
            [typeof(SuccessionCandidateSetRequest)],
            find.GetParameters().Select(parameter => parameter.ParameterType));

        Type[] contracts =
        [
            typeof(SuccessionCandidateSetRequest),
            typeof(SuccessionCandidateSetEntry),
            typeof(SuccessionCandidateSetIssue),
            typeof(SuccessionCandidateSetResult),
        ];
        Assert.All(contracts, contract => Assert.NotNull(contract.GetProperty("ContractVersion")));
        Assert.All(contracts, contract => Assert.False(
            typeof(ICampaignCommandPayload).IsAssignableFrom(contract)));
        Assert.All(contracts, contract => Assert.False(
            typeof(ICampaignEventPayload).IsAssignableFrom(contract)));
        Assert.Equal(1, CharacterSuccessionContractVersions.CandidateSet);
        Assert.Equal(5, CharacterSuccessionContractVersions.AuthoritativeQuery);
        Assert.Equal(3, CharacterSuccessionContractVersions.Snapshot);
        Assert.Equal(3, CharacterSuccessionSystem.Version);
        Assert.Equal(27, SaveEnvelope.CurrentSchemaVersion);
    }

    [Fact]
    public void F603_CompleteSetContainsEveryUniqueEligibleCandidateAndEveryBasis()
    {
        EntityId biological = Character("biological");
        EntityId adoptive = Character("adoptive");
        EntityId legacy = Character("legacy");
        EntityId multiBasis = Character("multi-basis");
        EntityId designated = Character("designated");
        WorldState world = CreateWorld(
            [ActiveDesignation(Subject, designated, "all-bases")],
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(
                biological,
                new CampaignDate(180, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological)),
            Seed(
                adoptive,
                new CampaignDate(181, 1, 1),
                Parent(Subject, ParentChildLinkKind.LegalAdoptive)),
            Seed(
                legacy,
                new CampaignDate(182, 1, 1),
                Parent(Subject, ParentChildLinkKind.UnspecifiedLegacy)),
            Seed(
                multiBasis,
                new CampaignDate(190, 1, 1),
                Parent(Subject, ParentChildLinkKind.Biological),
                Parent(adoptive, ParentChildLinkKind.Biological)),
            Seed(designated, new CampaignDate(183, 1, 1)));

        SuccessionCandidateSetResult result = Find(
            world,
            Rule(Enum.GetValues<SuccessionCandidateBasis>()),
            maximumCandidates: 5);

        Assert.Equal(SuccessionCandidateSetStatus.Complete, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(5, result.EligibleCandidateCount);
        Assert.Equal(
            new[] { biological, adoptive, legacy, multiBasis, designated }.Order(),
            result.Candidates.Select(item => item.CandidateCharacterId));
        Assert.Equal(5, result.Candidates.Select(item => item.CandidateCharacterId).Distinct().Count());
        AssertBasis(result, biological, SuccessionCandidateBasis.BiologicalDescendant, 1);
        AssertBasis(result, adoptive, SuccessionCandidateBasis.LegalAdoptiveDescendant, 1);
        AssertBasis(result, legacy, SuccessionCandidateBasis.UnspecifiedLegacyDescendant, 1);
        AssertBasis(result, designated, SuccessionCandidateBasis.ActiveDesignation, null);
        Assert.Equal(
            [
                (SuccessionCandidateBasis.BiologicalDescendant, 1),
                (SuccessionCandidateBasis.LegalAdoptiveDescendant, 2),
            ],
            result.Candidates.Single(item => item.CandidateCharacterId == multiBasis)
                .RecognizedBases.Select(item => (item.Basis, item.DescendantGeneration!.Value)));
    }

    [Fact]
    public void F604_CandidateConditionsAreFilteredOnlyByTheSuppliedRule()
    {
        EntityId adult = Character("adult");
        EntityId minor = Character("minor");
        EntityId incapacitated = Character("incapacitated");
        EntityId detained = Character("detained");
        EntityId captive = Character("captive");
        EntityId hostage = Character("hostage");
        EntityId dead = Character("dead");
        WorldState world = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Child(adult, new CampaignDate(180, 1, 1)),
            Child(minor, new CampaignDate(190, 6, 1)),
            Child(
                incapacitated,
                new CampaignDate(180, 1, 1),
                CharacterConditionState.Default with { IsIncapacitated = true }),
            Child(
                detained,
                new CampaignDate(180, 1, 1),
                CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Detained,
                    CustodianId = Subject,
                }),
            Child(
                captive,
                new CampaignDate(180, 1, 1),
                CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Subject,
                }),
            Child(
                hostage,
                new CampaignDate(180, 1, 1),
                CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = Subject,
                }),
            Child(dead, new CampaignDate(180, 1, 1), DeadCondition()));

        SuccessionCandidateSetResult strict = Find(
            world,
            Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                minimumAge: 18,
                allowsIncapacitated: false,
                allowedCustodyStatuses: [CharacterCustodyStatus.Free]),
            maximumCandidates: 7);
        Assert.Equal([adult], strict.Candidates.Select(item => item.CandidateCharacterId));

        SuccessionCandidateSetResult permissive = Find(
            world,
            Rule([SuccessionCandidateBasis.BiologicalDescendant]),
            maximumCandidates: 7);
        Assert.Equal(
            new[] { adult, minor, incapacitated, detained, captive, hostage }.Order(),
            permissive.Candidates.Select(item => item.CandidateCharacterId));
        Assert.DoesNotContain(permissive.Candidates, item => item.CandidateCharacterId == dead);
    }

    [Fact]
    public void F605_DepthAndCurrentDesignationBoundDiscoveryWithoutTerminalLeakage()
    {
        EntityId child = Character("depth-child");
        EntityId grandchild = Character("depth-grandchild");
        EntityId greatGrandchild = Character("depth-great-grandchild");
        EntityId activeNominee = Character("active-nominee");
        EntityId replacedNominee = Character("replaced-nominee");
        EntityId revokedNominee = Character("revoked-nominee");
        HeirDesignationState[] replacement = ReplacementDesignations(
            Subject,
            replacedNominee,
            activeNominee,
            "replacement");
        WorldState world = CreateWorld(
            replacement
                .Append(RevokedDesignation(Subject, revokedNominee, "revoked"))
                .ToArray(),
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Child(child, new CampaignDate(175, 1, 1)),
            Seed(
                grandchild,
                new CampaignDate(180, 1, 1),
                Parent(child, ParentChildLinkKind.Biological)),
            Seed(
                greatGrandchild,
                new CampaignDate(185, 1, 1),
                Parent(grandchild, ParentChildLinkKind.Biological)),
            Child(activeNominee, new CampaignDate(176, 1, 1)),
            Seed(replacedNominee, new CampaignDate(176, 1, 1)),
            Seed(revokedNominee, new CampaignDate(177, 1, 1)));

        SuccessionCandidateSetResult result = Find(
            world,
            Rule(
                [
                    SuccessionCandidateBasis.ActiveDesignation,
                    SuccessionCandidateBasis.BiologicalDescendant,
                ],
                maximumGeneration: 2),
            maximumCandidates: 4);

        Assert.Equal(
            new[] { child, grandchild, activeNominee }.Order(),
            result.Candidates.Select(item => item.CandidateCharacterId));
        Assert.DoesNotContain(result.Candidates, item => item.CandidateCharacterId == greatGrandchild);
        Assert.DoesNotContain(result.Candidates, item => item.CandidateCharacterId == replacedNominee);
        Assert.DoesNotContain(result.Candidates, item => item.CandidateCharacterId == revokedNominee);
        Assert.Equal(
            [
                (SuccessionCandidateBasis.ActiveDesignation, (int?)null),
                (SuccessionCandidateBasis.BiologicalDescendant, 1),
            ],
            result.Candidates.Single(item => item.CandidateCharacterId == activeNominee)
                .RecognizedBases.Select(item => (item.Basis, item.DescendantGeneration)));
    }

    [Fact]
    public void F606_SubjectConditionDoesNotGateEnumerationAndNoMatchIsComplete()
    {
        EntityId child = Character("subject-condition-child");
        CharacterConditionState[] subjectConditions =
        [
            CharacterConditionState.Default,
            CharacterConditionState.Default with { IsIncapacitated = true },
            CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Detained,
                CustodianId = child,
            },
            DeadCondition(),
        ];
        foreach (CharacterConditionState subjectCondition in subjectConditions)
        {
            WorldState world = CreateWorld(
                Seed(Subject, new CampaignDate(150, 1, 1), subjectCondition),
                Child(child, new CampaignDate(180, 1, 1)));
            SuccessionCandidateSetResult result = Find(
                world,
                Rule([SuccessionCandidateBasis.BiologicalDescendant]),
                maximumCandidates: 1);
            Assert.Equal(SuccessionCandidateSetStatus.Complete, result.Status);
            Assert.Equal([child], result.Candidates.Select(item => item.CandidateCharacterId));
        }

        WorldState unrelated = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Seed(Character("unrelated"), new CampaignDate(180, 1, 1)));
        SuccessionCandidateSetResult empty = Find(
            unrelated,
            Rule([SuccessionCandidateBasis.BiologicalDescendant]),
            maximumCandidates: 1);
        Assert.Equal(SuccessionCandidateSetStatus.Complete, empty.Status);
        Assert.Equal(0, empty.EligibleCandidateCount);
        Assert.Empty(empty.Candidates);
        Assert.Empty(empty.Issues);
    }

    [Fact]
    public void F607_MalformedRequestsReturnCanonicalControlledIssues()
    {
        WorldState world = CreateWorld(Seed(Subject, new CampaignDate(150, 1, 1)));
        SuccessionCandidateSetResult nullRequest = world.CharacterSuccessions
            .FindEligibleCandidates(null!);
        Assert.Equal(SuccessionCandidateSetStatus.InvalidRequest, nullRequest.Status);
        Assert.Equal(
            [
                SuccessionCandidateSetIssueReason.InvalidRequest,
                SuccessionCandidateSetIssueReason.InvalidMaximumCandidates,
                SuccessionCandidateSetIssueReason.UnsupportedRuleVersion,
                SuccessionCandidateSetIssueReason.InvalidSubject,
            ],
            Reasons(nullRequest));
        Assert.Null(nullRequest.SubjectCharacterId);

        SuccessionCandidateEligibilityRule malformedRule = new(
            999,
            [],
            0,
            CharacterSuccessionLimits.MaximumConfiguredMinimumCandidateAge + 1,
            false,
            []);
        SuccessionCandidateSetResult compound = world.CharacterSuccessions.FindEligibleCandidates(
            new SuccessionCandidateSetRequest(
                999,
                new EntityId("character:test/f6-unknown"),
                malformedRule,
                0));
        Assert.Equal(
            [
                SuccessionCandidateSetIssueReason.InvalidRequest,
                SuccessionCandidateSetIssueReason.InvalidMaximumCandidates,
                SuccessionCandidateSetIssueReason.UnsupportedRuleVersion,
                SuccessionCandidateSetIssueReason.MissingAllowedBasis,
                SuccessionCandidateSetIssueReason.InvalidMaximumDescendantGeneration,
                SuccessionCandidateSetIssueReason.InvalidMinimumCandidateAge,
                SuccessionCandidateSetIssueReason.MissingAllowedCustodyStatus,
                SuccessionCandidateSetIssueReason.UnknownSubject,
            ],
            Reasons(compound));
        Assert.Empty(compound.Candidates);

        SuccessionCandidateSetResult malformedCollections = Find(
            world,
            new SuccessionCandidateEligibilityRule(
                CharacterSuccessionContractVersions.CandidateEligibilityRule,
                [
                    SuccessionCandidateBasis.BiologicalDescendant,
                    SuccessionCandidateBasis.BiologicalDescendant,
                    (SuccessionCandidateBasis)999,
                ],
                1,
                0,
                true,
                [
                    CharacterCustodyStatus.Free,
                    CharacterCustodyStatus.Free,
                    (CharacterCustodyStatus)999,
                ]),
            maximumCandidates: 1);
        Assert.Equal(
            [
                SuccessionCandidateSetIssueReason.UnsupportedAllowedBasis,
                SuccessionCandidateSetIssueReason.DuplicateAllowedBasis,
                SuccessionCandidateSetIssueReason.UnsupportedAllowedCustodyStatus,
                SuccessionCandidateSetIssueReason.DuplicateAllowedCustodyStatus,
            ],
            Reasons(malformedCollections));
        SuccessionCandidateSetResult nullRule = world.CharacterSuccessions.FindEligibleCandidates(
            new SuccessionCandidateSetRequest(
                CharacterSuccessionContractVersions.CandidateSet,
                Subject,
                null!,
                1));
        Assert.Equal(
            [SuccessionCandidateSetIssueReason.UnsupportedRuleVersion],
            Reasons(nullRule));

        Assert.Throws<SimulationValidationException>(() => CreateWorld(
            Seed(Subject, new CampaignDate(201, 1, 1))));
    }

    [Fact]
    public void F608_ResultLimitFailsClosedWithExactEligibleCount()
    {
        EntityId[] children = Enumerable.Range(0, 3)
            .Select(index => Character($"limit-{index}"))
            .ToArray();
        WorldState world = CreateWorld(
            new[] { Seed(Subject, new CampaignDate(150, 1, 1)) }
                .Concat(children.Select(id => Child(id, new CampaignDate(180, 1, 1))))
                .ToArray());
        SuccessionCandidateEligibilityRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant]);
        string beforeJson = Serialize(world.CaptureSnapshot());
        string beforeChecksum = SimulationChecksum.Compute(world.CaptureSnapshot()).Value;

        SuccessionCandidateSetResult exceeded = Find(world, rule, maximumCandidates: 2);
        Assert.Equal(
            SuccessionCandidateSetStatus.MaximumCandidatesExceeded,
            exceeded.Status);
        Assert.Equal(3, exceeded.EligibleCandidateCount);
        Assert.Empty(exceeded.Candidates);
        Assert.Equal(
            [SuccessionCandidateSetIssueReason.MaximumCandidatesExceeded],
            Reasons(exceeded));

        SuccessionCandidateSetResult invalid = Find(world, rule, maximumCandidates: 0);
        Assert.Equal(SuccessionCandidateSetStatus.InvalidRequest, invalid.Status);
        Assert.Equal(
            [SuccessionCandidateSetIssueReason.InvalidMaximumCandidates],
            Reasons(invalid));

        SuccessionCandidateSetResult exact = Find(world, rule, maximumCandidates: 3);
        Assert.Equal(SuccessionCandidateSetStatus.Complete, exact.Status);
        Assert.Equal(children.Order(), exact.Candidates.Select(item => item.CandidateCharacterId));
        Assert.Equal(beforeJson, Serialize(world.CaptureSnapshot()));
        Assert.Equal(beforeChecksum, SimulationChecksum.Compute(world.CaptureSnapshot()).Value);
    }

    [Fact]
    public void F609_ResultIsCanonicalDefensiveAndChecksumNeutral()
    {
        EntityId biological = Character("purity-biological");
        EntityId adoptive = Character("purity-adoptive");
        CharacterSeed[] seeds =
        [
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Child(biological, new CampaignDate(180, 1, 1)),
            Seed(
                adoptive,
                new CampaignDate(181, 1, 1),
                Parent(Subject, ParentChildLinkKind.LegalAdoptive)),
        ];
        WorldState ordered = CreateWorld(seeds);
        WorldState reversed = CreateWorld(seeds.Reverse().ToArray());
        string beforeJson = Serialize(ordered.CaptureSnapshot());
        string beforeChecksum = SimulationChecksum.Compute(ordered.CaptureSnapshot()).Value;
        SuccessionCandidateEligibilityRule rule = Rule(
            [
                SuccessionCandidateBasis.LegalAdoptiveDescendant,
                SuccessionCandidateBasis.BiologicalDescendant,
            ],
            allowedCustodyStatuses: Enum.GetValues<CharacterCustodyStatus>().Reverse().ToArray());

        SuccessionCandidateSetResult first = Find(ordered, rule, maximumCandidates: 2);
        string pristine = Serialize(first);
        SuccessionCandidateSetEntry[] returned = Assert.IsType<SuccessionCandidateSetEntry[]>(
            first.Candidates);
        SuccessionCandidateBasisEvidence[] returnedBases =
            Assert.IsType<SuccessionCandidateBasisEvidence[]>(returned[0].RecognizedBases);
        returnedBases[0] = returnedBases[0] with
        {
            Basis = SuccessionCandidateBasis.ActiveDesignation,
        };
        returned[0] = returned[0] with { CandidateCharacterId = Subject };

        SuccessionCandidateSetResult repeated = Find(ordered, rule, maximumCandidates: 2);
        SuccessionCandidateSetResult reordered = Find(
            reversed,
            rule.Canonicalize(),
            maximumCandidates: 2);
        Assert.Equal(pristine, Serialize(repeated));
        Assert.Equal(pristine, Serialize(reordered));
        Assert.Equal(beforeJson, Serialize(ordered.CaptureSnapshot()));
        Assert.Equal(beforeChecksum, SimulationChecksum.Compute(ordered.CaptureSnapshot()).Value);
    }

    [Fact]
    public void F610_CompleteInvalidAndExceededResultsRoundTripExactly()
    {
        EntityId child = Character("json-child");
        EntityId otherChild = Character("json-other-child");
        WorldState world = CreateWorld(
            Seed(Subject, new CampaignDate(150, 1, 1)),
            Child(child, new CampaignDate(180, 1, 1)),
            Child(otherChild, new CampaignDate(181, 1, 1)));
        SuccessionCandidateEligibilityRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant]);
        SuccessionCandidateSetRequest request = new(
            CharacterSuccessionContractVersions.CandidateSet,
            Subject,
            rule,
            2);
        SuccessionCandidateSetResult complete = world.CharacterSuccessions
            .FindEligibleCandidates(request);
        SuccessionCandidateSetResult exceeded = Find(world, rule, maximumCandidates: 1);
        SuccessionCandidateSetResult invalid = world.CharacterSuccessions.FindEligibleCandidates(
            new SuccessionCandidateSetRequest(
                CharacterSuccessionContractVersions.CandidateSet,
                default,
                rule,
                2));

        Assert.Equal(
            Serialize(request),
            Serialize(JsonSerializer.Deserialize<SuccessionCandidateSetRequest>(
                Serialize(request),
                SimulationJson.CreateOptions())!));
        AssertRoundTrips(complete);
        AssertRoundTrips(exceeded);
        AssertRoundTrips(invalid);
        Assert.All(complete.Candidates, item => Assert.Equal(
            CharacterSuccessionContractVersions.CandidateSet,
            item.ContractVersion));
        Assert.All(complete.Candidates.SelectMany(item => item.RecognizedBases), item =>
            Assert.Equal(CharacterSuccessionContractVersions.CandidateEvaluation, item.ContractVersion));
        Assert.All(invalid.Issues, item => Assert.Equal(
            CharacterSuccessionContractVersions.CandidateSet,
            item.ContractVersion));
        Assert.Equal(27, SaveEnvelope.CurrentSchemaVersion);
        Assert.Equal(3, CharacterSuccessionSystem.Version);
        Assert.Equal(
            new SystemVersion(CharacterSuccessionSystem.SystemId, 3),
            world.CaptureSnapshot().SystemVersions.Single(item =>
                item.SystemId == CharacterSuccessionSystem.SystemId));
    }

    [Fact]
    public void F611_ThousandCharacterCandidateSetRecordsRawLocalPerformance()
    {
        CharacterSeed[] seeds = Enumerable.Range(0, 1_000)
            .Select(index => index == 0
                ? Seed(Subject, new CampaignDate(150, 1, 1))
                : Child(Character($"scale-{index:D4}"), new CampaignDate(180, 1, 1)))
            .ToArray();
        WorldState world = CreateWorld(seeds);
        Stopwatch query = Stopwatch.StartNew();
        SuccessionCandidateSetResult result = Find(
            world,
            Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                maximumGeneration: 1),
            maximumCandidates: 999);
        query.Stop();

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
        Assert.Equal(SuccessionCandidateSetStatus.Complete, result.Status);
        Assert.Equal(999, result.EligibleCandidateCount);
        Assert.Equal(999, result.Candidates.Count);
        output.WriteLine(
            $"SP-04F6 1,000-character/999-candidate query: {query.Elapsed.TotalMilliseconds:F3} ms; snapshot/checksum/JSON/gzip: {snapshot.Elapsed.TotalMilliseconds:F3} ms; JSON: {json.Length} bytes; gzip: {compressed.Length} bytes; checksum: {checksum}.");
    }

    private static SuccessionCandidateSetResult Find(
        WorldState world,
        SuccessionCandidateEligibilityRule rule,
        int maximumCandidates) => world.CharacterSuccessions.FindEligibleCandidates(new(
            CharacterSuccessionContractVersions.CandidateSet,
            Subject,
            rule,
            maximumCandidates));

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
        return WorldState.Create(
            Date,
            55,
            [],
            GeographicWorldSnapshot.Empty,
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                definitions,
                [],
                [],
                states,
                [],
                []),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                designations.OrderBy(item => item.DesignationId).ToArray(),
                [],
                [],
                []));
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

    private static CharacterSeed Child(
        EntityId id,
        CampaignDate birthDate,
        CharacterConditionState? condition = null) => Seed(
            id,
            birthDate,
            condition ?? CharacterConditionState.Default,
            Parent(Subject, ParentChildLinkKind.Biological));

    private static CharacterParentLink Parent(EntityId parentId, ParentChildLinkKind kind) =>
        new(parentId, kind);

    private static HeirDesignationState ActiveDesignation(
        EntityId designator,
        EntityId heir,
        string suffix)
    {
        EntityId sourceCommand = new($"command:test/f6-{suffix}-source");
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
        EntityId resolutionCommand = new($"command:test/f6-{suffix}-resolution");
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

    private static HeirDesignationState[] ReplacementDesignations(
        EntityId designator,
        EntityId replacedHeir,
        EntityId activeHeir,
        string suffix)
    {
        HeirDesignationState predecessor = ActiveDesignation(
            designator,
            replacedHeir,
            $"{suffix}-predecessor");
        EntityId replacementCommand = new($"command:test/f6-{suffix}-replacement");
        EntityId replacementEvent = CharacterSuccessionIds.DeriveActionEventId(
            Date,
            replacementCommand);
        HeirDesignationState replaced = predecessor with
        {
            Status = HeirDesignationStatus.Replaced,
            ResolutionDate = Date,
            ResolutionTurnIndex = 0,
            ResolutionCommandId = replacementCommand,
            ResolutionEventId = replacementEvent,
        };
        HeirDesignationState successor = new(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(
                replacementEvent,
                designator,
                activeHeir),
            designator,
            activeHeir,
            Date,
            0,
            replacementCommand,
            replacementEvent,
            HeirDesignationStatus.Active,
            null,
            null,
            null,
            null);
        return [replaced, successor];
    }

    private static CharacterConditionState DeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        null);

    private static void AssertBasis(
        SuccessionCandidateSetResult result,
        EntityId candidateId,
        SuccessionCandidateBasis basis,
        int? generation)
    {
        SuccessionCandidateSetEntry candidate = result.Candidates.Single(item =>
            item.CandidateCharacterId == candidateId);
        SuccessionCandidateBasisEvidence evidence = Assert.Single(candidate.RecognizedBases);
        Assert.Equal(basis, evidence.Basis);
        Assert.Equal(generation, evidence.DescendantGeneration);
    }

    private static SuccessionCandidateSetIssueReason[] Reasons(
        SuccessionCandidateSetResult result) =>
        result.Issues.Select(item => item.Reason).ToArray();

    private static void AssertRoundTrips(SuccessionCandidateSetResult result) => Assert.Equal(
        Serialize(result),
        Serialize(JsonSerializer.Deserialize<SuccessionCandidateSetResult>(
            Serialize(result),
            SimulationJson.CreateOptions())!));

    private static EntityId Character(string suffix) =>
        new($"character:test/f6-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition,
        IReadOnlyList<CharacterParentLink> ParentLinks);
}
