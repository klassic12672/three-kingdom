using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionClaimWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId Claimant = Character("claimant");

    [Fact]
    public void F702_ContractsIdsAndPolymorphicVocabularyAreVersionedSeparately()
    {
        EntityId commandId = new("command:test/f7-contract");
        EntityId eventId = CharacterSuccessionIds.DeriveClaimActionEventId(Date, commandId);
        EntityId claimId = CharacterSuccessionIds.DeriveClaimId(
            eventId,
            Subject,
            Claimant);
        SuccessionClaimState claim = ActiveClaim(
            Subject,
            Claimant,
            commandId,
            eventId);
        CharacterSuccessionClaimActionResolvedEventPayload asserted = new(
            Claimant,
            new AssertSuccessionClaimAction(Subject),
            new SuccessionClaimAssertedOutcome(claim));
        CharacterSuccessionClaimActionResolvedEventPayload withdrawn = asserted with
        {
            Action = new WithdrawSuccessionClaimAction(Subject, claimId),
            Outcome = new SuccessionClaimWithdrawnOutcome(claim with
            {
                Status = SuccessionClaimStatus.Withdrawn,
                WithdrawalDate = Date,
                WithdrawalTurnIndex = 55,
                WithdrawalCommandId = new("command:test/f7-contract-withdraw"),
                WithdrawalEventId = CharacterSuccessionIds.DeriveClaimActionEventId(
                    Date,
                    new("command:test/f7-contract-withdraw")),
            }),
        };

        Assert.Equal(3, CharacterSuccessionContractVersions.Snapshot);
        Assert.Equal(1, CharacterSuccessionContractVersions.ClaimState);
        Assert.Equal(1, CharacterSuccessionContractVersions.ClaimHistory);
        Assert.Equal(1, CharacterSuccessionContractVersions.ClaimAction);
        Assert.Equal(1, CharacterSuccessionContractVersions.ClaimOutcome);
        Assert.Equal(5, CharacterSuccessionContractVersions.AuthoritativeQuery);
        Assert.Equal(3, CharacterSuccessionSystem.Version);
        Assert.Equal(claimId, claim.ClaimId);
        Assert.NotEqual(
            CharacterSuccessionIds.DeriveActionEventId(Date, commandId),
            eventId);
        Assert.Contains("assert_succession_claim.v1", Serialize(asserted));
        Assert.Contains("succession_claim_asserted.v1", Serialize(asserted));
        Assert.Contains("withdraw_succession_claim.v1", Serialize(withdrawn));
        Assert.Contains("succession_claim_withdrawn.v1", Serialize(withdrawn));
        Assert.Equal(
            Serialize(asserted),
            Serialize(JsonSerializer.Deserialize<
                CharacterSuccessionClaimActionResolvedEventPayload>(
                    Serialize(asserted),
                    SimulationJson.CreateOptions())!));
        Assert.DoesNotContain(
            typeof(IAuthoritativeCharacterSuccessionWorldQuery).GetProperties(),
            property => property.Name is "Claims" or "ClaimHistory");
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("incapacitated")]
    [InlineData("captive")]
    [InlineData("hostage")]
    public void F703_SubjectConditionDoesNotPreventNeutralPersonalClaim(string scenario)
    {
        CharacterConditionState subjectCondition = scenario switch
        {
            "dead" => DeadCondition(),
            "incapacitated" => CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            "captive" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Claimant,
            },
            "hostage" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Hostage,
                CustodianId = Claimant,
            },
            _ => throw new InvalidOperationException(),
        };
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject, subjectCondition),
            Seed(Claimant));

        SuccessionClaimState claim = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Apply(state, Claimant, new AssertSuccessionClaimAction(Subject), scenario)
                .Outcome).CurrentClaim;

        Assert.Equal(SuccessionClaimStatus.Active, claim.Status);
        Assert.Equal(Subject, claim.SubjectCharacterId);
        Assert.Equal(Claimant, claim.ClaimantCharacterId);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("incapacitated")]
    [InlineData("captive")]
    [InlineData("hostage")]
    public void F703_ClaimantMustBeLivingCapableAndFree(string scenario)
    {
        CharacterConditionState claimantCondition = scenario switch
        {
            "dead" => DeadCondition(),
            "incapacitated" => CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            "captive" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Subject,
            },
            "hostage" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Hostage,
                CustodianId = Subject,
            },
            _ => throw new InvalidOperationException(),
        };
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Claimant, claimantCondition));

        CommandValidationResult result = state.ValidateClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(Subject)),
            Date,
            55);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == scenario switch
        {
            "dead" => "claimant_dead",
            "incapacitated" => "claimant_incapacitated",
            _ => "claimant_not_free",
        });
        Assert.Empty(state.GetActiveClaimsForSubject(Subject));
    }

    [Fact]
    public void F704_MinorMayAssertButUnknownOrSelfSubjectFailsClosed()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Claimant, birthDate: Date.AddDays(-365)));

        Assert.True(state.ValidateClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(Subject)),
            Date,
            55).IsValid);
        Assert.False(state.ValidateClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(Character("missing"))),
            Date,
            55).IsValid);
        Assert.False(state.ValidateClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(Claimant)),
            Date,
            55).IsValid);
    }

    [Fact]
    public void F705_AssertWithdrawAndReassertRetainExactEvidenceWithNewIdentity()
    {
        CharacterSuccessionWorldState state = CreateState(Seed(Subject), Seed(Claimant));
        SuccessionClaimState first = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Apply(state, Claimant, new AssertSuccessionClaimAction(Subject), "first")
                .Outcome).CurrentClaim;

        Assert.True(state.TryGetActiveClaim(Subject, Claimant, out SuccessionClaimState? active));
        Assert.Equal(first, active);
        Assert.False(state.ValidateClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(Subject)),
            Date,
            55).IsValid);

        SuccessionClaimState withdrawn = Assert.IsType<SuccessionClaimWithdrawnOutcome>(
            Apply(
                state,
                Claimant,
                new WithdrawSuccessionClaimAction(Subject, first.ClaimId),
                "withdraw").Outcome).PreviousClaim;
        Assert.Equal(SuccessionClaimStatus.Withdrawn, withdrawn.Status);
        Assert.NotNull(withdrawn.WithdrawalDate);
        Assert.False(state.TryGetActiveClaim(Subject, Claimant, out _));

        SuccessionClaimState second = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Apply(state, Claimant, new AssertSuccessionClaimAction(Subject), "second")
                .Outcome).CurrentClaim;
        Assert.NotEqual(first.ClaimId, second.ClaimId);
        Assert.Equal(
            [first.ClaimId, second.ClaimId],
            state.GetRecentClaimRecordsForSubject(Subject)
                .Select(item => item.ClaimId)
                .Order());
    }

    [Fact]
    public void F706_WithdrawalRequiresExactSubjectPairAndClaimIdentity()
    {
        EntityId otherSubject = Character("other-subject");
        EntityId otherClaimant = Character("other-claimant");
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(otherSubject),
            Seed(Claimant),
            Seed(otherClaimant));
        SuccessionClaimState active = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Apply(state, Claimant, new AssertSuccessionClaimAction(Subject), "exact")
                .Outcome).CurrentClaim;

        Assert.False(state.ValidateClaimAction(
            Claimant,
            new(new WithdrawSuccessionClaimAction(
                otherSubject,
                active.ClaimId)),
            Date,
            55).IsValid);
        Assert.False(state.ValidateClaimAction(
            otherClaimant,
            new(new WithdrawSuccessionClaimAction(
                Subject,
                active.ClaimId)),
            Date,
            55).IsValid);
        Assert.False(state.ValidateClaimAction(
            Claimant,
            new(new WithdrawSuccessionClaimAction(
                Subject,
                new("succession_claim:test/stale"))),
            Date,
            55).IsValid);
        Assert.True(state.TryGetActiveClaim(Subject, Claimant, out _));
    }

    [Fact]
    public void F708_ActiveCapacityIsBoundedPerSubjectAndPerClaimant()
    {
        CharacterSeed[] claimantSeeds = Enumerable.Range(
                0,
                CharacterSuccessionLimits.MaximumActiveClaimsPerSubject + 1)
            .Select(index => Seed(Character($"subject-capacity-claimant-{index}")))
            .ToArray();
        CharacterSuccessionWorldState subjectBound = CreateState(
            [Seed(Subject), .. claimantSeeds]);
        for (int index = 0;
             index < CharacterSuccessionLimits.MaximumActiveClaimsPerSubject;
             index++)
        {
            Apply(
                subjectBound,
                claimantSeeds[index].Id,
                new AssertSuccessionClaimAction(Subject),
                $"subject-capacity-{index}");
        }

        Assert.False(subjectBound.ValidateClaimAction(
            claimantSeeds[^1].Id,
            new(new AssertSuccessionClaimAction(Subject)),
            Date,
            55).IsValid);

        CharacterSeed[] subjectSeeds = Enumerable.Range(
                0,
                CharacterSuccessionLimits.MaximumActiveClaimsPerClaimant + 1)
            .Select(index => Seed(Character($"claimant-capacity-subject-{index}")))
            .ToArray();
        CharacterSuccessionWorldState claimantBound = CreateState(
            [Seed(Claimant), .. subjectSeeds]);
        for (int index = 0;
             index < CharacterSuccessionLimits.MaximumActiveClaimsPerClaimant;
             index++)
        {
            Apply(
                claimantBound,
                Claimant,
                new AssertSuccessionClaimAction(subjectSeeds[index].Id),
                $"claimant-capacity-{index}");
        }

        Assert.False(claimantBound.ValidateClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(subjectSeeds[^1].Id)),
            Date,
            55).IsValid);
    }

    [Fact]
    public void F708_WithdrawnRetentionFoldsDeterministicallyAndOverflowRejectsAtomically()
    {
        CharacterSuccessionWorldState state = CreateState(Seed(Subject), Seed(Claimant));
        int count = CharacterSuccessionLimits.RecentWithdrawnClaimsPerSubject + 3;
        for (int index = 0; index < count; index++)
        {
            SuccessionClaimState active = Assert.IsType<SuccessionClaimAssertedOutcome>(
                Apply(
                    state,
                    Claimant,
                    new AssertSuccessionClaimAction(Subject),
                    $"retention-assert-{index}").Outcome).CurrentClaim;
            Apply(
                state,
                Claimant,
                new WithdrawSuccessionClaimAction(Subject, active.ClaimId),
                $"retention-withdraw-{index}");
        }

        Assert.Equal(
            CharacterSuccessionLimits.RecentWithdrawnClaimsPerSubject,
            state.GetRecentClaimRecordsForSubject(Subject).Count);
        Assert.True(state.TryGetClaimHistory(
            Subject,
            out SuccessionClaimHistoryAggregate? history));
        Assert.Equal(3, history.FoldedWithdrawnCount);
        Assert.Equal(Date, history.EarliestDate);
        Assert.Equal(Date, history.LatestDate);

        SuccessionClaimState activeAtCapacity = Assert.IsType<
            SuccessionClaimAssertedOutcome>(Apply(
                state,
                Claimant,
                new AssertSuccessionClaimAction(Subject),
                "overflow-active").Outcome).CurrentClaim;
        CharacterSuccessionWorldSnapshot snapshot = state.CaptureSnapshot() with
        {
            ClaimHistory =
            [
                history with { FoldedWithdrawnCount = long.MaxValue },
            ],
        };
        CharacterSuccessionWorldState nearOverflow = CreateState(
            snapshot,
            Seed(Subject),
            Seed(Claimant));
        string before = Serialize(nearOverflow.CaptureSnapshot());

        CommandValidationResult result = nearOverflow.ValidateClaimAction(
            Claimant,
            new(new WithdrawSuccessionClaimAction(
                Subject,
                activeAtCapacity.ClaimId)),
            Date,
            55);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "succession_claim_history_overflow");
        Assert.Equal(before, Serialize(nearOverflow.CaptureSnapshot()));
    }

    [Fact]
    public void F709_QueriesAndSnapshotsAreCanonicalDefensiveAndSubjectBounded()
    {
        EntityId otherSubject = Character("query-other-subject");
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(otherSubject),
            Seed(Claimant));
        SuccessionClaimState first = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Apply(state, Claimant, new AssertSuccessionClaimAction(Subject), "query-a")
                .Outcome).CurrentClaim;
        SuccessionClaimState second = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Apply(
                state,
                Claimant,
                new AssertSuccessionClaimAction(otherSubject),
                "query-b").Outcome).CurrentClaim;
        CharacterSuccessionWorldSnapshot canonical = state.CaptureSnapshot();
        CharacterSuccessionWorldSnapshot shuffled = canonical with
        {
            Claims = canonical.Claims.Reverse().ToArray(),
        };
        CharacterSuccessionWorldState restored = CreateState(
            shuffled,
            Seed(Subject),
            Seed(otherSubject),
            Seed(Claimant));

        Assert.Equal(
            new[] { first.ClaimId, second.ClaimId }.Order(),
            restored.CaptureSnapshot().Claims.Select(item => item.ClaimId));
        Assert.Equal(
            [first],
            restored.GetActiveClaimsForSubject(Subject));
        SuccessionClaimState[] leaked = Assert.IsType<SuccessionClaimState[]>(
            restored.GetRecentClaimRecordsForSubject(Subject));
        Array.Reverse(leaked);
        Assert.Equal(
            [first],
            restored.GetRecentClaimRecordsForSubject(Subject));
        Assert.Throws<SimulationValidationException>(() =>
            restored.GetActiveClaimsForSubject(Character("unknown")));
    }

    [Fact]
    public void F710_CrossWorkflowIdentityReuseAndMalformedClaimSnapshotsFailClosed()
    {
        EntityId designationCommand = new("command:test/f7-cross-designation");
        EntityId designationEvent = CharacterSuccessionIds.DeriveActionEventId(
            Date,
            designationCommand);
        HeirDesignationState designation = new(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(
                designationEvent,
                Subject,
                Claimant),
            Subject,
            Claimant,
            Date,
            55,
            designationCommand,
            designationEvent,
            HeirDesignationStatus.Active,
            null,
            null,
            null,
            null);
        CharacterSuccessionWorldState state = CreateState(
            new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                [designation],
                [],
                [],
                []),
            Seed(Subject),
            Seed(Claimant));
        EntityId claimEvent = CharacterSuccessionIds.DeriveClaimActionEventId(
            Date,
            designationEvent);

        Assert.Throws<SimulationValidationException>(() => state.PlanClaimAction(
            Claimant,
            new(new AssertSuccessionClaimAction(Subject)),
            Date,
            55,
            designationEvent,
            claimEvent));

        SuccessionClaimState crossRole = ActiveClaim(
            Subject,
            Claimant,
            designationEvent,
            claimEvent);
        Assert.Throws<SimulationValidationException>(() => CreateState(
            new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                [designation],
                [],
                [crossRole],
                []),
            Seed(Subject),
            Seed(Claimant)));
        Assert.Throws<SimulationValidationException>(() => CreateState(
            CharacterSuccessionWorldSnapshot.Empty with
            {
                Claims = [crossRole with { ClaimId = Character("tampered-claim-id") }],
            },
            Seed(Subject),
            Seed(Claimant)));
    }

    [Fact]
    public void F711_ClaimDoesNotCreateEligibilityBasisOrCandidateMembership()
    {
        CharacterSuccessionWorldState state = CreateState(Seed(Subject), Seed(Claimant));
        Apply(state, Claimant, new AssertSuccessionClaimAction(Subject), "independent");
        SuccessionCandidateEligibilityRule rule = new(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            [SuccessionCandidateBasis.BiologicalDescendant],
            1,
            0,
            true,
            Enum.GetValues<CharacterCustodyStatus>());

        SuccessionCandidateEvaluationResult evaluation = state.EvaluateCandidate(new(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            Subject,
            Claimant,
            rule));
        SuccessionCandidateSetResult set = state.FindEligibleCandidates(new(
            CharacterSuccessionContractVersions.CandidateSet,
            Subject,
            rule,
            1));

        Assert.False(evaluation.IsEligible);
        Assert.Empty(evaluation.RecognizedBases);
        Assert.Contains(evaluation.Issues, issue =>
            issue.Reason == SuccessionCandidateEligibilityReason.NoRecognizedBasis);
        Assert.Equal(SuccessionCandidateSetStatus.Complete, set.Status);
        Assert.Empty(set.Candidates);
        Assert.True(state.TryGetActiveClaim(Subject, Claimant, out _));
    }

    private static CharacterSuccessionClaimActionResolvedEventPayload Apply(
        CharacterSuccessionWorldState state,
        EntityId claimant,
        ICharacterSuccessionClaimAction action,
        string suffix)
    {
        EntityId commandId = new($"command:test/f7-{suffix}");
        EntityId eventId = CharacterSuccessionIds.DeriveClaimActionEventId(Date, commandId);
        CharacterSuccessionClaimActionResolvedEventPayload payload = state.PlanClaimAction(
            claimant,
            new CharacterSuccessionClaimActionCommandPayload(action),
            Date,
            55,
            commandId,
            eventId);
        CharacterSuccessionWorldUpdatePlan plan = state.PrepareClaimOutcome(
            payload,
            Date,
            55,
            commandId,
            eventId);
        state.ApplyPrepared(plan);
        return payload;
    }

    private static CharacterSuccessionWorldState CreateState(
        params CharacterSeed[] seeds) => CreateState(
            CharacterSuccessionWorldSnapshot.Empty,
            seeds);

    private static CharacterSuccessionWorldState CreateState(
        CharacterSuccessionWorldSnapshot snapshot,
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
            .Select(item => new CharacterState(
                CharacterContractVersions.State,
                item.Id,
                [],
                [],
                item.Condition,
                []))
            .ToArray();
        CharacterWorldState characters = new(
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
        return new CharacterSuccessionWorldState(
            snapshot,
            characters,
            new CampaignCalendar(Date, 55));
    }

    private static CharacterSeed Seed(
        EntityId id,
        CharacterConditionState? condition = null,
        CampaignDate? birthDate = null) => new(
            id,
            birthDate ?? new CampaignDate(170, 1, 1),
            condition ?? CharacterConditionState.Default);

    private static SuccessionClaimState ActiveClaim(
        EntityId subject,
        EntityId claimant,
        EntityId sourceCommand,
        EntityId sourceEvent) => new(
            CharacterSuccessionContractVersions.ClaimState,
            CharacterSuccessionIds.DeriveClaimId(sourceEvent, subject, claimant),
            subject,
            claimant,
            SuccessionClaimOrigin.PersonalAssertion,
            Date,
            55,
            sourceCommand,
            sourceEvent,
            SuccessionClaimStatus.Active,
            null,
            null,
            null,
            null);

    private static CharacterConditionState DeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        null);

    private static EntityId Character(string suffix) =>
        new($"character:test/f7-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);
}
