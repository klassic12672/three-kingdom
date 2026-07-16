using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionSupportWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId Supporter = Character("supporter");
    private static readonly EntityId Candidate = Character("candidate");
    private static readonly EntityId OtherCandidate = Character("other-candidate");

    [Fact]
    public void F802_ContractsIdsVocabularyAndCrossWorkflowRolesAreExact()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        CharacterSuccessionSupportActionResolvedEventPayload declared = Apply(
            state,
            Supporter,
            new DeclareSuccessionSupportAction(Subject, Candidate, null),
            "contract-declare");
        SuccessionSupportState first = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(declared.Outcome).CurrentSupport;
        CharacterSuccessionSupportActionResolvedEventPayload replaced = Apply(
            state,
            Supporter,
            new DeclareSuccessionSupportAction(
                Subject,
                OtherCandidate,
                first.SupportId),
            "contract-replace");
        SuccessionSupportState second = Assert.IsType<
            SuccessionSupportReplacedOutcome>(replaced.Outcome).CurrentSupport;
        CharacterSuccessionSupportActionResolvedEventPayload withdrawn = Apply(
            state,
            Supporter,
            new WithdrawSuccessionSupportAction(Subject, second.SupportId),
            "contract-withdraw");

        Assert.Equal(3, CharacterSuccessionContractVersions.Snapshot);
        Assert.Equal(1, CharacterSuccessionContractVersions.SupportState);
        Assert.Equal(1, CharacterSuccessionContractVersions.SupportHistory);
        Assert.Equal(1, CharacterSuccessionContractVersions.SupportAction);
        Assert.Equal(1, CharacterSuccessionContractVersions.SupportOutcome);
        Assert.Equal(5, CharacterSuccessionContractVersions.AuthoritativeQuery);
        Assert.Equal(3, CharacterSuccessionSystem.Version);
        Assert.Equal(27, SaveEnvelope.CurrentSchemaVersion);
        Assert.StartsWith("event:", first.SourceEventId.Value);
        Assert.StartsWith("succession_support:", first.SupportId.Value);
        Assert.NotEqual(
            CharacterSuccessionIds.DeriveActionEventId(
                first.DeclaredDate,
                first.SourceCommandId),
            first.SourceEventId);
        Assert.NotEqual(
            CharacterSuccessionIds.DeriveClaimActionEventId(
                first.DeclaredDate,
                first.SourceCommandId),
            first.SourceEventId);
        Assert.NotEqual(
            first.SupportId,
            CharacterSuccessionIds.DeriveSupportId(
                first.SourceEventId,
                Subject,
                Candidate,
                Supporter));

        Assert.Equal(
            new[]
            {
                "ContractVersion",
                "DeclaredDate",
                "DeclaredTurnIndex",
                "ResolutionCommandId",
                "ResolutionDate",
                "ResolutionEventId",
                "ResolutionTurnIndex",
                "SourceCommandId",
                "SourceEventId",
                "Status",
                "SubjectId",
                "SupportedCandidateId",
                "SupporterId",
                "SupportId",
            },
            typeof(SuccessionSupportState).GetProperties()
                .Select(property => property.Name)
                .Order()
                .ToArray());
        Assert.Equal(
            new[]
            {
                nameof(IAuthoritativeCharacterSuccessionWorldQuery
                    .GetActiveSupportsForCandidate),
                nameof(IAuthoritativeCharacterSuccessionWorldQuery
                    .GetActiveSupportsForSubject),
                nameof(IAuthoritativeCharacterSuccessionWorldQuery
                    .GetRecentSupportRecordsForSubject),
                nameof(IAuthoritativeCharacterSuccessionWorldQuery
                    .TryGetCurrentSupport),
                nameof(IAuthoritativeCharacterSuccessionWorldQuery
                    .TryGetSupportHistory),
            },
            typeof(IAuthoritativeCharacterSuccessionWorldQuery).GetMethods()
                .Where(method => method.Name.Contains(
                    "Support",
                    StringComparison.Ordinal))
                .Select(method => method.Name)
                .Order()
                .ToArray());

        AssertSupportPayloadVocabulary(
            declared,
            "declare_succession_support.v1",
            "succession_support_declared.v1");
        AssertSupportPayloadVocabulary(
            replaced,
            "declare_succession_support.v1",
            "succession_support_replaced.v1");
        AssertSupportPayloadVocabulary(
            withdrawn,
            "withdraw_succession_support.v1",
            "succession_support_withdrawn.v1");

        HeirDesignationState designation = ActiveDesignation(
            Subject,
            Candidate,
            "cross-role");
        SuccessionClaimState claim = ActiveClaim(
            Subject,
            OtherCandidate,
            "cross-role");
        CharacterSuccessionWorldState retained = CreateState(
            new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                [designation],
                [],
                [claim],
                [],
                [],
                []),
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        Assert.Throws<SimulationValidationException>(() =>
            retained.PlanSupportAction(
                Supporter,
                new(new DeclareSuccessionSupportAction(
                    Subject,
                    Candidate,
                    null)),
                Date,
                55,
                designation.SourceCommandId,
                CharacterSuccessionIds.DeriveSupportActionEventId(
                    Date,
                    designation.SourceCommandId)));
        Assert.Throws<SimulationValidationException>(() =>
            retained.PlanSupportAction(
                Supporter,
                new(new DeclareSuccessionSupportAction(
                    Subject,
                    Candidate,
                    null)),
                Date,
                55,
                claim.SourceEventId,
                CharacterSuccessionIds.DeriveSupportActionEventId(
                    Date,
                    claim.SourceEventId)));
    }

    [Fact]
    public void F804_SubjectMayHaveAnyConditionAndCandidateMayBeMinorIncapableOrInCustody()
    {
        CharacterConditionState restrictedCandidate = CharacterConditionState.Default with
        {
            IsIncapacitated = true,
            CustodyStatus = CharacterCustodyStatus.Hostage,
            CustodianId = Supporter,
        };
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject, DeadCondition()),
            Seed(Supporter),
            Seed(Candidate, restrictedCandidate, Date.AddDays(-100)));

        CommandValidationResult validation = state.ValidateSupportAction(
            Supporter,
            new(new DeclareSuccessionSupportAction(Subject, Candidate, null)),
            Date,
            55);
        SuccessionSupportState support = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "condition-matrix").Outcome).CurrentSupport;

        Assert.True(validation.IsValid);
        Assert.Equal(SuccessionSupportStatus.Active, support.Status);
        Assert.Equal(Subject, support.SubjectId);
        Assert.Equal(Supporter, support.SupporterId);
        Assert.Equal(Candidate, support.SupportedCandidateId);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("incapacitated")]
    [InlineData("captive")]
    [InlineData("hostage")]
    public void F804_SupporterMustBeLivingCapableAndFree(string scenario)
    {
        CharacterConditionState condition = scenario switch
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
            Seed(Supporter, condition),
            Seed(Candidate));

        CommandValidationResult result = state.ValidateSupportAction(
            Supporter,
            new(new DeclareSuccessionSupportAction(Subject, Candidate, null)),
            Date,
            55);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == scenario switch
        {
            "dead" => "supporter_dead",
            "incapacitated" => "supporter_incapacitated",
            _ => "supporter_not_free",
        });
        Assert.Empty(state.GetActiveSupportsForSubject(Subject));
    }

    [Fact]
    public void F805_AllParticipantsMustBeDistinctExistingAndCandidateMustLive()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate, DeadCondition()));

        Assert.False(ValidateDeclare(state, Supporter, Subject, Supporter).IsValid);
        Assert.False(ValidateDeclare(state, Subject, Subject, Candidate).IsValid);
        Assert.False(ValidateDeclare(state, Supporter, Subject, Subject).IsValid);
        Assert.False(ValidateDeclare(
            state,
            Supporter,
            Character("missing"),
            Candidate).IsValid);
        Assert.False(ValidateDeclare(state, Supporter, Subject, Candidate).IsValid);
    }

    [Fact]
    public void F805_SupportDoesNotChangeDesignationClaimOrEligibility()
    {
        HeirDesignationState designation = ActiveDesignation(
            Subject,
            OtherCandidate,
            "independence");
        SuccessionClaimState claim = ActiveClaim(
            Subject,
            OtherCandidate,
            "independence");
        CharacterSuccessionWorldState state = CreateState(
            new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                [designation],
                [],
                [claim],
                [],
                [],
                []),
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        CharacterSuccessionWorldSnapshot before = state.CaptureSnapshot();

        Apply(
            state,
            Supporter,
            new DeclareSuccessionSupportAction(Subject, Candidate, null),
            "independence");
        CharacterSuccessionWorldSnapshot after = state.CaptureSnapshot();
        SuccessionCandidateEligibilityRule rule = new(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            [SuccessionCandidateBasis.ActiveDesignation],
            1,
            0,
            true,
            Enum.GetValues<CharacterCustodyStatus>());
        SuccessionCandidateEvaluationResult evaluation = state.EvaluateCandidate(new(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            Subject,
            Candidate,
            rule));
        SuccessionCandidateSetResult set = state.FindEligibleCandidates(new(
            CharacterSuccessionContractVersions.CandidateSet,
            Subject,
            rule,
            4));

        Assert.Equal(Serialize(before.Designations), Serialize(after.Designations));
        Assert.Equal(Serialize(before.History), Serialize(after.History));
        Assert.Equal(Serialize(before.Claims), Serialize(after.Claims));
        Assert.Equal(Serialize(before.ClaimHistory), Serialize(after.ClaimHistory));
        Assert.False(evaluation.IsEligible);
        Assert.Empty(evaluation.RecognizedBases);
        Assert.Contains(evaluation.Issues, issue =>
            issue.Reason == SuccessionCandidateEligibilityReason.NoRecognizedBasis);
        Assert.Equal(SuccessionCandidateSetStatus.Complete, set.Status);
        Assert.DoesNotContain(
            set.Candidates,
            entry => entry.CandidateCharacterId == Candidate);
        Assert.Contains(
            set.Candidates,
            entry => entry.CandidateCharacterId == OtherCandidate);
        Assert.True(state.TryGetCurrentSupport(Subject, Supporter, out _));
        Assert.True(state.TryGetActiveClaim(Subject, OtherCandidate, out _));
        Assert.True(state.TryGetCurrentDesignation(Subject, out _));
    }

    [Fact]
    public void F806_DeclareReplaceWithdrawAndRedeclareUseExactLifecycleEvidence()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        SuccessionSupportState first = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "declare").Outcome).CurrentSupport;

        Assert.False(ValidateDeclare(state, Supporter, Subject, Candidate).IsValid);
        Assert.False(ValidateDeclare(
            state,
            Supporter,
            Subject,
            OtherCandidate).IsValid);

        SuccessionSupportReplacedOutcome replaced = Assert.IsType<
            SuccessionSupportReplacedOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(
                    Subject,
                    OtherCandidate,
                    first.SupportId),
                "replace").Outcome);
        Assert.Equal(SuccessionSupportStatus.Replaced, replaced.PreviousSupport.Status);
        Assert.Equal(
            replaced.PreviousSupport.ResolutionCommandId,
            replaced.CurrentSupport.SourceCommandId);
        Assert.Equal(
            replaced.PreviousSupport.ResolutionEventId,
            replaced.CurrentSupport.SourceEventId);
        Assert.NotEqual(first.SupportId, replaced.CurrentSupport.SupportId);

        Assert.False(state.ValidateSupportAction(
            Supporter,
            new(new WithdrawSuccessionSupportAction(Subject, first.SupportId)),
            Date,
            55).IsValid);
        SuccessionSupportState withdrawn = Assert.IsType<
            SuccessionSupportWithdrawnOutcome>(Apply(
                state,
                Supporter,
                new WithdrawSuccessionSupportAction(
                    Subject,
                    replaced.CurrentSupport.SupportId),
                "withdraw").Outcome).PreviousSupport;
        Assert.Equal(SuccessionSupportStatus.Withdrawn, withdrawn.Status);
        Assert.False(state.TryGetCurrentSupport(Subject, Supporter, out _));

        SuccessionSupportState redeclared = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "redeclare").Outcome).CurrentSupport;
        Assert.NotEqual(first.SupportId, redeclared.SupportId);
        Assert.NotEqual(withdrawn.SupportId, redeclared.SupportId);
    }

    [Fact]
    public void F807_PlansAreDeterministicAndStaleOrTamperedOutcomesFailAtomically()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate));
        EntityId commandId = new("command:test/f8-plan");
        EntityId eventId = CharacterSuccessionIds.DeriveSupportActionEventId(
            Date,
            commandId);
        CharacterSuccessionSupportActionCommandPayload command = new(
            new DeclareSuccessionSupportAction(Subject, Candidate, null));
        CharacterSuccessionSupportActionResolvedEventPayload first =
            state.PlanSupportAction(
                Supporter,
                command,
                Date,
                55,
                commandId,
                eventId);
        CharacterSuccessionSupportActionResolvedEventPayload replay =
            state.PlanSupportAction(
                Supporter,
                command,
                Date,
                55,
                commandId,
                eventId);
        string before = Serialize(state.CaptureSnapshot());
        SuccessionSupportDeclaredOutcome declared = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(first.Outcome);
        CharacterSuccessionSupportActionResolvedEventPayload tampered = first with
        {
            Outcome = declared with
            {
                CurrentSupport = declared.CurrentSupport with
                {
                    SupportedCandidateId = OtherCandidate,
                },
            },
        };

        Assert.Equal(Serialize(first), Serialize(replay));
        Assert.Throws<SimulationValidationException>(() =>
            state.PrepareSupportOutcome(
                tampered,
                Date,
                55,
                commandId,
                eventId));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void F808_LaterParticipantConditionChangesDoNotRewriteSupportEvidence()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate));
        SuccessionSupportState support = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "preserve").Outcome).CurrentSupport;

        CharacterSuccessionWorldState restored = CreateState(
            state.CaptureSnapshot(),
            Seed(Subject, DeadCondition()),
            Seed(Supporter, DeadCondition()),
            Seed(Candidate, DeadCondition()));

        Assert.True(restored.TryGetCurrentSupport(
            Subject,
            Supporter,
            out SuccessionSupportState? retained));
        Assert.Equal(support, retained);
    }

    [Fact]
    public void F808_LaterLivingParticipantCustodyChangesDoNotRewriteSupportEvidence()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        SuccessionSupportState support = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "preserve-custody").Outcome).CurrentSupport;
        CharacterSuccessionWorldState restored = CreateState(
            state.CaptureSnapshot(),
            Seed(Subject, InCustody(CharacterCustodyStatus.Captive)),
            Seed(Supporter, InCustody(CharacterCustodyStatus.Hostage)),
            Seed(Candidate, InCustody(CharacterCustodyStatus.Detained)),
            Seed(OtherCandidate));

        Assert.True(restored.TryGetCurrentSupport(
            Subject,
            Supporter,
            out SuccessionSupportState? retained));
        Assert.Equal(support, retained);
    }

    [Fact]
    public void F809_ActiveCapacityIsBoundedPerSubjectAndPerSupporter()
    {
        CharacterSeed[] supporters = Enumerable.Range(
                0,
                CharacterSuccessionLimits.MaximumActiveSupportsPerSubject + 1)
            .Select(index => Seed(Character($"subject-supporter-{index}")))
            .ToArray();
        CharacterSuccessionWorldState subjectBound = CreateState(
            [Seed(Subject), Seed(Candidate), .. supporters]);
        for (int index = 0;
             index < CharacterSuccessionLimits.MaximumActiveSupportsPerSubject;
             index++)
        {
            Apply(
                subjectBound,
                supporters[index].Id,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                $"subject-capacity-{index}");
        }

        Assert.False(ValidateDeclare(
            subjectBound,
            supporters[^1].Id,
            Subject,
            Candidate).IsValid);

        CharacterSeed[] subjects = Enumerable.Range(
                0,
                CharacterSuccessionLimits.MaximumActiveSupportsPerSupporter + 1)
            .Select(index => Seed(Character($"supporter-subject-{index}")))
            .ToArray();
        CharacterSuccessionWorldState supporterBound = CreateState(
            [Seed(Supporter), Seed(Candidate), .. subjects]);
        for (int index = 0;
             index < CharacterSuccessionLimits.MaximumActiveSupportsPerSupporter;
             index++)
        {
            Apply(
                supporterBound,
                Supporter,
                new DeclareSuccessionSupportAction(
                    subjects[index].Id,
                    Candidate,
                    null),
                $"supporter-capacity-{index}");
        }

        Assert.False(ValidateDeclare(
            supporterBound,
            Supporter,
            subjects[^1].Id,
            Candidate).IsValid);
    }

    [Fact]
    public void F809_TerminalRetentionFoldsDeterministicallyAndOverflowIsAtomic()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        SuccessionSupportState current = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "retention-initial").Outcome).CurrentSupport;
        int replacements =
            CharacterSuccessionLimits.RecentTerminalSupportsPerSubject + 3;
        for (int index = 0; index < replacements; index++)
        {
            EntityId nextCandidate = index % 2 == 0 ? OtherCandidate : Candidate;
            current = Assert.IsType<SuccessionSupportReplacedOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(
                    Subject,
                    nextCandidate,
                    current.SupportId),
                $"retention-replace-{index}").Outcome).CurrentSupport;
        }

        Assert.Equal(
            CharacterSuccessionLimits.RecentTerminalSupportsPerSubject + 1,
            state.GetRecentSupportRecordsForSubject(Subject).Count);
        Assert.True(state.TryGetSupportHistory(
            Subject,
            out SuccessionSupportHistoryAggregate? history));
        Assert.Equal(3, history.FoldedReplacedCount);
        Assert.Equal(0, history.FoldedWithdrawnCount);

        CharacterSuccessionWorldState nearOverflow = CreateState(
            state.CaptureSnapshot() with
            {
                SupportHistory =
                [
                    history with { FoldedReplacedCount = long.MaxValue },
                ],
            },
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        string before = Serialize(nearOverflow.CaptureSnapshot());
        CommandValidationResult result = nearOverflow.ValidateSupportAction(
            Supporter,
            new(new WithdrawSuccessionSupportAction(Subject, current.SupportId)),
            Date,
            55);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "succession_support_history_overflow");
        Assert.Equal(before, Serialize(nearOverflow.CaptureSnapshot()));
    }

    [Fact]
    public void F809_WithdrawnTerminalRecordsFoldIntoTheirSeparateCheckedTotal()
    {
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(Supporter),
            Seed(Candidate),
            Seed(OtherCandidate));
        SuccessionSupportState current = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "withdraw-fold-declare",
                Date,
                55).Outcome).CurrentSupport;
        Apply(
            state,
            Supporter,
            new WithdrawSuccessionSupportAction(Subject, current.SupportId),
            "withdraw-fold-withdraw",
            Date.AddDays(1),
            56);
        current = Assert.IsType<SuccessionSupportDeclaredOutcome>(Apply(
            state,
            Supporter,
            new DeclareSuccessionSupportAction(Subject, Candidate, null),
            "withdraw-fold-redeclare",
            Date.AddDays(2),
            57).Outcome).CurrentSupport;
        for (int index = 0;
             index < CharacterSuccessionLimits.RecentTerminalSupportsPerSubject;
             index++)
        {
            EntityId nextCandidate = index % 2 == 0 ? OtherCandidate : Candidate;
            current = Assert.IsType<SuccessionSupportReplacedOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(
                    Subject,
                    nextCandidate,
                    current.SupportId),
                $"withdraw-fold-replace-{index}",
                Date.AddDays(3 + index),
                58 + index).Outcome).CurrentSupport;
        }

        Assert.True(state.TryGetSupportHistory(
            Subject,
            out SuccessionSupportHistoryAggregate? history));
        Assert.Equal(0, history.FoldedReplacedCount);
        Assert.Equal(1, history.FoldedWithdrawnCount);
        Assert.Equal(
            CharacterSuccessionLimits.RecentTerminalSupportsPerSubject + 1,
            state.GetRecentSupportRecordsForSubject(Subject).Count);
    }

    [Fact]
    public void F810_QueriesAndSnapshotsAreCanonicalDefensiveAndSubjectBounded()
    {
        EntityId otherSubject = Character("query-other-subject");
        EntityId otherSupporter = Character("query-other-supporter");
        CharacterSuccessionWorldState state = CreateState(
            Seed(Subject),
            Seed(otherSubject),
            Seed(Supporter),
            Seed(otherSupporter),
            Seed(Candidate));
        SuccessionSupportState first = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                Supporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "query-a").Outcome).CurrentSupport;
        SuccessionSupportState second = Assert.IsType<
            SuccessionSupportDeclaredOutcome>(Apply(
                state,
                otherSupporter,
                new DeclareSuccessionSupportAction(Subject, Candidate, null),
                "query-b").Outcome).CurrentSupport;
        Apply(
            state,
            Supporter,
            new DeclareSuccessionSupportAction(otherSubject, Candidate, null),
            "query-c");
        CharacterSuccessionWorldSnapshot canonical = state.CaptureSnapshot();
        CharacterSuccessionWorldState restored = CreateState(
            canonical with { Supports = canonical.Supports.Reverse().ToArray() },
            Seed(Subject),
            Seed(otherSubject),
            Seed(Supporter),
            Seed(otherSupporter),
            Seed(Candidate));

        Assert.Equal(
            new[] { first.SupportId, second.SupportId }.Order(),
            restored.GetActiveSupportsForSubject(Subject)
                .Select(item => item.SupportId));
        Assert.Equal(
            new[] { first.SupportId, second.SupportId }.Order(),
            restored.GetActiveSupportsForCandidate(Subject, Candidate)
                .Select(item => item.SupportId));
        SuccessionSupportState[] leaked = Assert.IsType<SuccessionSupportState[]>(
            restored.GetRecentSupportRecordsForSubject(Subject));
        Array.Reverse(leaked);
        Assert.Equal(
            new[] { first.SupportId, second.SupportId }.Order(),
            restored.GetRecentSupportRecordsForSubject(Subject)
                .Select(item => item.SupportId));
        Assert.Throws<SimulationValidationException>(() =>
            restored.GetActiveSupportsForSubject(Character("unknown")));
    }

    private static CommandValidationResult ValidateDeclare(
        CharacterSuccessionWorldState state,
        EntityId supporter,
        EntityId subject,
        EntityId candidate) => state.ValidateSupportAction(
            supporter,
            new(new DeclareSuccessionSupportAction(subject, candidate, null)),
            Date,
            55);

    private static CharacterSuccessionSupportActionResolvedEventPayload Apply(
        CharacterSuccessionWorldState state,
        EntityId supporter,
        ICharacterSuccessionSupportAction action,
        string suffix,
        CampaignDate? resolutionDate = null,
        long? resolutionTurnIndex = null)
    {
        CampaignDate date = resolutionDate ?? Date;
        long turnIndex = resolutionTurnIndex ?? 55;
        EntityId commandId = new($"command:test/f8-{suffix}");
        EntityId eventId = CharacterSuccessionIds.DeriveSupportActionEventId(
            date,
            commandId);
        CharacterSuccessionSupportActionResolvedEventPayload payload =
            state.PlanSupportAction(
                supporter,
                new CharacterSuccessionSupportActionCommandPayload(action),
                date,
                turnIndex,
                commandId,
                eventId);
        CharacterSuccessionWorldUpdatePlan plan = state.PrepareSupportOutcome(
            payload,
            date,
            turnIndex,
            commandId,
            eventId);
        state.ApplyPrepared(plan);
        return payload;
    }

    private static void AssertSupportPayloadVocabulary(
        CharacterSuccessionSupportActionResolvedEventPayload payload,
        string actionDiscriminator,
        string outcomeDiscriminator)
    {
        string json = JsonSerializer.Serialize<ICampaignEventPayload>(
            payload,
            SimulationJson.CreateOptions());
        Assert.Contains(
            "character_succession_support_action_resolved.v1",
            json);
        Assert.Contains(actionDiscriminator, json);
        Assert.Contains(outcomeDiscriminator, json);
        ICampaignEventPayload roundTrip =
            JsonSerializer.Deserialize<ICampaignEventPayload>(
                json,
                SimulationJson.CreateOptions())!;
        Assert.Equal(
            json,
            JsonSerializer.Serialize(
                roundTrip,
                SimulationJson.CreateOptions()));
    }

    private static HeirDesignationState ActiveDesignation(
        EntityId designator,
        EntityId heir,
        string suffix)
    {
        EntityId sourceCommand = new($"command:test/f8-{suffix}-designation");
        EntityId sourceEvent = CharacterSuccessionIds.DeriveActionEventId(
            Date,
            sourceCommand);
        return new(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(
                sourceEvent,
                designator,
                heir),
            designator,
            heir,
            Date,
            55,
            sourceCommand,
            sourceEvent,
            HeirDesignationStatus.Active,
            null,
            null,
            null,
            null);
    }

    private static SuccessionClaimState ActiveClaim(
        EntityId subject,
        EntityId claimant,
        string suffix)
    {
        EntityId sourceCommand = new($"command:test/f8-{suffix}-claim");
        EntityId sourceEvent = CharacterSuccessionIds.DeriveClaimActionEventId(
            Date,
            sourceCommand);
        return new(
            CharacterSuccessionContractVersions.ClaimState,
            CharacterSuccessionIds.DeriveClaimId(
                sourceEvent,
                subject,
                claimant),
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

    private static CharacterConditionState DeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        null);

    private static CharacterConditionState InCustody(
        CharacterCustodyStatus status) => CharacterConditionState.Default with
        {
            CustodyStatus = status,
            CustodianId = OtherCandidate,
        };

    private static EntityId Character(string suffix) =>
        new($"character:test/f8-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);
}
