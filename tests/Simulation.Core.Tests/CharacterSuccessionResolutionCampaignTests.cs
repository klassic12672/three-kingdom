using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionResolutionCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId Successor = Character("successor");
    private static readonly EntityId Alternate = Character("alternate");
    private static readonly EntityId Grandchild = Character("grandchild");
    private static readonly EntityId ReplacementHead = Character("replacement-head");
    private static readonly EntityId Regent = Character("regent");
    private static readonly EntityId Guardian = Character("guardian");
    private static readonly EntityId Custodian = Character("custodian");
    private static readonly EntityId Supporter = Character("supporter");
    private static readonly EntityId Household = new("household:test/f9-primary");

    [Fact]
    public void F901_SelectedResolutionComposesEvidenceInheritanceHeadServiceAndControl()
    {
        CharacterSuccessionWorldSnapshot succession = SuccessionSnapshot(
            ActiveDesignation(Subject, Successor, "selected"),
            ActiveClaim(Subject, Successor, "selected"),
            ActiveSupport(Subject, Supporter, Successor, "selected"));
        CampaignSimulation simulation = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
                Seed(Alternate, new(178, 1, 1), Parent(Subject)),
                Seed(ReplacementHead, new(170, 1, 1)),
                Seed(Regent, new(165, 1, 1)),
                Seed(Supporter, new(168, 1, 1)),
            ],
            succession,
            resources: Resources((Subject, 40), (Successor, 2)),
            estates: Estates((new("estate:test/f9-selected"), Subject)),
            household: new(
                Household,
                Subject,
                [Subject, Successor, ReplacementHead]),
            careers: ActiveRetinueMembership(Regent, Subject, "selected"));
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.ActiveDesignation,
                SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.ActiveDesignation,
                SuccessionLegalBasis.BiologicalDescendant]);
        CampaignCommand command = SuccessionDeathCommand(
            simulation,
            Subject,
            rule,
            "selected",
            householdId: Household,
            replacementHeadCharacterId: ReplacementHead);

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterConditionActionResolvedEventPayload payload = Assert.IsType<
            CharacterConditionActionResolvedEventPayload>(campaignEvent.Payload);
        ResolveCharacterSuccessionDeathAction action = Assert.IsType<
            ResolveCharacterSuccessionDeathAction>(payload.Action);
        CharacterSuccessionDeathResolvedOutcome outcome = Assert.IsType<
            CharacterSuccessionDeathResolvedOutcome>(payload.Outcome);
        SuccessionResolutionState resolution = outcome.Succession;
        SuccessionResolutionCandidate selected =
            Assert.IsType<SuccessionResolutionCandidate>(
                resolution.SelectedCandidate);

        Assert.Equal(SuccessionResolutionStatus.Selected, resolution.Status);
        Assert.Equal(Successor, selected.CandidateCharacterId);
        Assert.Equal(
            CharacterSuccessionIds.DeriveResolutionId(
                campaignEvent.EventId,
                Subject),
            resolution.ResolutionId);
        Assert.Contains(
            selected.LegalBases,
            item => item.Basis == SuccessionLegalBasis.ActiveDesignation
                && item.SourceDesignationId is not null);
        Assert.Contains(
            selected.LegalBases,
            item => item.Basis == SuccessionLegalBasis.BiologicalDescendant
                && item.DescendantGeneration == 1);
        Assert.NotNull(selected.ActiveClaimId);
        Assert.Single(selected.ActiveSupportIds);
        Assert.Equal(42, simulation.World.CharacterResources.GetWealth(Successor));
        Assert.Equal(0, simulation.World.CharacterResources.GetWealth(Subject));
        Assert.Equal(
            Successor,
            Assert.Single(simulation.World.CharacterEstateHoldings.Holdings)
                .OwnerCharacterId);
        Assert.Equal(
            ReplacementHead,
            simulation.World.Characters.Households.Single(
                item => item.HouseholdId == Household).HeadCharacterId);
        Assert.NotEqual(
            selected.CandidateCharacterId,
            outcome.HouseholdHeadChange!.CurrentHeadCharacterId);
        Assert.Single(outcome.Death.CareerChanges.EndedRetinueMemberships);
        Assert.Equal(
            CareerServiceEndReason.MemberDied,
            outcome.Death.CareerChanges.EndedRetinueMemberships[0].EndReason);
        Assert.DoesNotContain(
            simulation.World.Careers.RetinueMemberships,
            item => item.MemberCharacterId == Successor && item.IsActive);
        Assert.Equal(
            Successor,
            simulation.World.CharacterSuccessions.CampaignContinuity!
                .ControlledCharacterId);
        Assert.Equal(
            WorldState.GetCharacterConditionActionAffectedIds(
                payload,
                campaignEvent.EventId),
            campaignEvent.AffectedIds);
        Assert.Equal(
            action.ExpectedResolutionStateId,
            command.Payload is CharacterConditionActionCommandPayload commandPayload
                ? Assert.IsType<ResolveCharacterSuccessionDeathAction>(
                    commandPayload.Action).ExpectedResolutionStateId
                : default);
        Assert.Contains(
            "character_succession_death_resolved.v1",
            Serialize(campaignEvent),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "successor")]
    [InlineData(true, "alternate")]
    public void F901_ClaimsAndSupportRemainDistinctOrderedRankingFacts(
        bool bothHaveClaims,
        string expected)
    {
        string scenario = bothHaveClaims.ToString().ToLowerInvariant();
        SuccessionClaimState successorClaim =
            ActiveClaim(Subject, Successor, $"rank-{scenario}-successor");
        SuccessionClaimState alternateClaim =
            ActiveClaim(Subject, Alternate, $"rank-{scenario}-alternate");
        SuccessionSupportState alternateSupport = ActiveSupport(
            Subject,
            Supporter,
            Alternate,
            $"rank-{scenario}-alternate");
        CharacterSuccessionWorldSnapshot succession =
            CharacterSuccessionWorldSnapshot.Empty with
            {
                Claims = bothHaveClaims
                    ? [successorClaim, alternateClaim]
                    : [successorClaim],
                Supports = [alternateSupport],
            };
        CampaignSimulation simulation = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
                Seed(Alternate, new(180, 1, 1), Parent(Subject)),
                Seed(Supporter, new(168, 1, 1)),
            ],
            succession);
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.BiologicalDescendant]);

        Assert.True(simulation.Submit(SuccessionDeathCommand(
            simulation,
            Subject,
            rule,
            $"rank-{scenario}")).IsValid);
        SuccessionResolutionCandidate selected = Assert.IsType<
            SuccessionResolutionCandidate>(Assert.IsType<
                CharacterSuccessionDeathResolvedOutcome>(Assert.IsType<
                    CharacterConditionActionResolvedEventPayload>(
                        Assert.Single(simulation.ResolveTurn()).Payload).Outcome)
                .Succession.SelectedCandidate);

        Assert.Equal(
            expected == "successor" ? Successor : Alternate,
            selected.CandidateCharacterId);
        Assert.NotNull(selected.ActiveClaimId);
        Assert.Equal(
            expected == "alternate" ? 1 : 0,
            selected.ActiveSupportIds.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F902_DisputedAndExtinctResultsPreservePropertyAndUseExplicitContinuity(
        bool disputed)
    {
        List<CharacterSeed> seeds =
        [
            Seed(Subject, new(160, 1, 1)),
        ];
        if (disputed)
        {
            seeds.Add(Seed(Successor, new(180, 1, 1), Parent(Subject)));
            seeds.Add(Seed(Alternate, new(180, 1, 1), Parent(Subject)));
        }

        SuccessionNoAcceptedSuccessorBehavior behavior = disputed
            ? SuccessionNoAcceptedSuccessorBehavior.ContinueWithoutControlledCharacter
            : SuccessionNoAcceptedSuccessorBehavior.EndCampaign;
        CampaignSimulation simulation = CreateSimulation(
            seeds,
            SuccessionSnapshot(),
            resources: Resources((Subject, 17)),
            estates: Estates((new("estate:test/f9-unaccepted"), Subject)));
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.BiologicalDescendant],
            disputed
                ? SuccessionContestResolutionMode.RecordDispute
                : SuccessionContestResolutionMode.ResolveByStableId,
            behavior);

        Assert.True(simulation.Submit(SuccessionDeathCommand(
            simulation,
            Subject,
            rule,
            disputed ? "disputed" : "extinct")).IsValid);
        CharacterSuccessionDeathResolvedOutcome outcome = Assert.IsType<
            CharacterSuccessionDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);

        Assert.Equal(
            disputed
                ? SuccessionResolutionStatus.Disputed
                : SuccessionResolutionStatus.NoSuccessor,
            outcome.Succession.Status);
        Assert.Null(outcome.Succession.SelectedCandidate);
        Assert.Equal(disputed ? 2 : 0, outcome.Succession.DisputedCandidates.Count);
        Assert.Null(outcome.Succession.Inheritance.WealthTransfer);
        Assert.Empty(outcome.Succession.Inheritance.EstateTransfers);
        Assert.Equal(17, simulation.World.CharacterResources.GetWealth(Subject));
        Assert.Equal(
            Subject,
            Assert.Single(simulation.World.CharacterEstateHoldings.Holdings)
                .OwnerCharacterId);
        Assert.Equal(
            disputed
                ? PlayerCampaignContinuityStatus.ContinueWithoutControlledCharacter
                : PlayerCampaignContinuityStatus.Ended,
            simulation.World.CharacterSuccessions.CampaignContinuity!.Status);
        Assert.Null(
            simulation.World.CharacterSuccessions.CampaignContinuity!
                .ControlledCharacterId);
    }

    [Fact]
    public void F903_MinorIncapacitatedSuccessorFreezesDistinctRegencyEvidence()
    {
        CharacterConditionState successorCondition = new(
            CharacterVitalStatus.Alive,
            CharacterHealthStatus.Injured,
            IsIncapacitated: true,
            CharacterCustodyStatus.Hostage,
            Custodian);
        CampaignDate guardianshipDate = Date.AddDays(-30);
        EntityId guardianshipCommand =
            new("command:test/f9-regency-guardianship");
        EntityId guardianshipEvent = CharacterFamilyIds.DeriveActionEventId(
            guardianshipDate,
            guardianshipCommand);
        CharacterGuardianshipState guardianship = new(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(
                guardianshipEvent,
                Successor,
                Guardian),
            Successor,
            Guardian,
            guardianshipDate,
            0,
            guardianshipCommand,
            guardianshipEvent,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
        CampaignSimulation simulation = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(
                    Successor,
                    new(190, 1, 1),
                    Parent(Subject),
                    condition: successorCondition),
                Seed(Regent, new(165, 1, 1)),
                Seed(Guardian, new(166, 1, 1)),
                Seed(Custodian, new(167, 1, 1)),
            ],
            SuccessionSnapshot(),
            guardianships: new(
                CharacterGuardianshipContractVersions.Snapshot,
                [guardianship]));
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.BiologicalDescendant]);

        Assert.True(simulation.Submit(SuccessionDeathCommand(
            simulation,
            Subject,
            rule,
            "regency",
            regentCharacterId: Regent)).IsValid);
        CharacterSuccessionDeathResolvedOutcome outcome = Assert.IsType<
            CharacterSuccessionDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        SuccessionRegencyHook regency =
            Assert.IsType<SuccessionRegencyHook>(outcome.Succession.Regency);

        Assert.Equal(
            SuccessionRegencyReason.Minor
                | SuccessionRegencyReason.Incapacitated,
            regency.Reasons);
        Assert.Equal(Successor, regency.SuccessorCharacterId);
        Assert.Equal(Regent, regency.RegentCharacterId);
        Assert.Equal(guardianship.GuardianshipId, regency.SourceGuardianshipId);
        Assert.Equal(Guardian, regency.SourceGuardianCharacterId);
        Assert.Equal(Custodian, regency.SourceCustodianCharacterId);
        Assert.Equal(5, new[]
        {
            regency.SuccessorCharacterId,
            regency.RegentCharacterId!.Value,
            regency.SourceGuardianshipId!.Value,
            regency.SourceGuardianCharacterId!.Value,
            regency.SourceCustodianCharacterId!.Value,
        }.Distinct().Count());
    }

    [Fact]
    public void F904_OverflowStaleAndTamperedTransactionsFailWithoutMutation()
    {
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.BiologicalDescendant]);
        CampaignSimulation overflow = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
            ],
            SuccessionSnapshot(),
            resources: Resources((Subject, 1), (Successor, long.MaxValue)));
        string overflowBefore = SnapshotJson(overflow);
        Assert.False(overflow.Submit(SuccessionDeathCommand(
            overflow,
            Subject,
            rule,
            "overflow")).IsValid);
        Assert.Equal(overflowBefore, SnapshotJson(overflow));

        CampaignSimulation source = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
            ],
            SuccessionSnapshot(),
            resources: Resources((Subject, 10), (Successor, 1)));
        CampaignCommand staleCommand = SuccessionDeathCommand(
            source,
            Subject,
            rule,
            "stale");
        WorldSnapshot changed = source.World.CaptureSnapshot() with
        {
            CharacterResources = source.World.CharacterResources.CaptureSnapshot()
                with
            {
                Accounts =
                    [
                        Account(Subject, 11),
                        Account(Successor, 1),
                    ],
            },
        };
        CampaignSimulation stale = new(WorldState.Restore(changed));
        string staleBefore = SnapshotJson(stale);
        Assert.False(stale.Submit(staleCommand).IsValid);
        Assert.Equal(staleBefore, SnapshotJson(stale));

        CampaignSimulation rankingSource = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
                Seed(Alternate, new(181, 1, 1), Parent(Subject)),
                Seed(Supporter, new(168, 1, 1)),
            ],
            SuccessionSnapshot(
                designation: ActiveDesignation(
                    Subject,
                    Successor,
                    "stale-ranking")));
        SuccessionResolutionRule designationRule = Rule(
            [
                SuccessionCandidateBasis.ActiveDesignation,
                SuccessionCandidateBasis.BiologicalDescendant,
            ],
            [
                SuccessionLegalBasis.ActiveDesignation,
                SuccessionLegalBasis.BiologicalDescendant,
            ]);
        CampaignCommand staleRankingCommand = SuccessionDeathCommand(
            rankingSource,
            Subject,
            designationRule,
            "stale-ranking");
        WorldSnapshot changedRanking =
            rankingSource.World.CaptureSnapshot() with
            {
                CharacterSuccessions = rankingSource.World.CharacterSuccessions
                    .CaptureSnapshot() with
                {
                    Supports =
                    [
                        ActiveSupport(
                            Subject,
                            Supporter,
                            Alternate,
                            "stale-ranking"),
                    ],
                },
            };
        CampaignSimulation staleRanking =
            new(WorldState.Restore(changedRanking));
        string staleRankingBefore = SnapshotJson(staleRanking);
        Assert.False(staleRanking.Submit(staleRankingCommand).IsValid);
        Assert.Equal(staleRankingBefore, SnapshotJson(staleRanking));

        CampaignSimulation consequenceSource = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
                Seed(Regent, new(165, 1, 1)),
            ],
            SuccessionSnapshot());
        CampaignCommand staleConsequenceCommand = SuccessionDeathCommand(
            consequenceSource,
            Subject,
            rule,
            "stale-consequence");
        WorldSnapshot changedConsequence =
            consequenceSource.World.CaptureSnapshot() with
            {
                Careers = ActiveRetinueMembership(
                    Regent,
                    Subject,
                    "stale-consequence"),
            };
        CampaignSimulation staleConsequence =
            new(WorldState.Restore(changedConsequence));
        string staleConsequenceBefore = SnapshotJson(staleConsequence);
        Assert.False(staleConsequence.Submit(
            staleConsequenceCommand).IsValid);
        Assert.Equal(
            staleConsequenceBefore,
            SnapshotJson(staleConsequence));

        (EntityId EstateId, EntityId OwnerCharacterId)[] holdings =
            Enumerable.Range(
                    0,
                    CharacterEstateHoldingLimits.HoldingsPerCharacter)
                .Select(index => (
                    new EntityId($"estate:test/f9-capacity-{index:D2}"),
                    Successor))
                .Append((new("estate:test/f9-capacity-subject"), Subject))
                .ToArray();
        CampaignSimulation estateCapacity = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
            ],
            SuccessionSnapshot(),
            estates: Estates(holdings));
        string estateCapacityBefore = SnapshotJson(estateCapacity);
        Assert.False(estateCapacity.Submit(SuccessionDeathCommand(
            estateCapacity,
            Subject,
            rule,
            "estate-capacity")).IsValid);
        Assert.Equal(estateCapacityBefore, SnapshotJson(estateCapacity));

        CampaignSimulation tamper = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
                Seed(Alternate, new(181, 1, 1), Parent(Subject)),
            ],
            SuccessionSnapshot());
        CampaignCommand command = SuccessionDeathCommand(
            tamper,
            Subject,
            rule,
            "tamper");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(
            Date,
            command.CommandId);
        CharacterConditionAggregatePlan plan =
            tamper.World.PrepareCharacterConditionAction(
                command.IssuingActor,
                Assert.IsType<CharacterConditionActionCommandPayload>(
                    command.Payload),
                Date,
                tamper.World.Calendar.TurnIndex,
                command.CommandId,
                eventId);
        CharacterConditionActionResolvedEventPayload resolved =
            plan.ResolvedPayload;
        CharacterSuccessionDeathResolvedOutcome outcome = Assert.IsType<
            CharacterSuccessionDeathResolvedOutcome>(resolved.Outcome);
        CharacterConditionActionResolvedEventPayload forged = resolved with
        {
            Outcome = outcome with
            {
                Succession = outcome.Succession with
                {
                    SelectedCandidate = outcome.Succession.SelectedCandidate!
                        with
                    {
                        CandidateCharacterId = Alternate,
                    },
                },
            },
        };
        CampaignEvent forgedEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            Date,
            ResolutionPhase.Commands,
            command.Priority,
            WorldState.GetCharacterConditionActionAffectedIds(
                forged,
                eventId),
            forged);
        string tamperBefore = SnapshotJson(tamper);
        Assert.Throws<SimulationValidationException>(() =>
            tamper.World.Apply(forgedEvent));
        Assert.Equal(tamperBefore, SnapshotJson(tamper));

        CampaignEvent validEvent = forgedEvent with
        {
            AffectedIds = WorldState.GetCharacterConditionActionAffectedIds(
                resolved,
                eventId),
            Payload = resolved,
        };
        tamper.World.Apply(validEvent);
        string applied = SnapshotJson(tamper);
        Assert.Throws<SimulationValidationException>(() =>
            tamper.World.Apply(validEvent));
        Assert.Equal(applied, SnapshotJson(tamper));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void F904_LegacyDeathCannotBypassActivePlayerContinuity(
        bool householdHead)
    {
        CampaignSimulation simulation = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(ReplacementHead, new(170, 1, 1)),
            ],
            SuccessionSnapshot(),
            household: householdHead
                ? new(
                    Household,
                    Subject,
                    [Subject, ReplacementHead])
                : null);
        ICharacterConditionAction action = householdHead
            ? new ResolveHouseholdHeadDeathAction(
                Subject,
                Profile(simulation, Subject).Condition,
                Household,
                ReplacementHead)
            : new ResolveCharacterDeathAction(
                Subject,
                Profile(simulation, Subject).Condition);
        CampaignCommand command = CampaignCommand.Create(
            new(
                $"command:test/f9-legacy-control-{householdHead.ToString().ToLowerInvariant()}"),
            CharacterConditionSystem.AuthoritativeActorId,
            Date,
            new CharacterConditionActionCommandPayload(action));
        string before = SnapshotJson(simulation);

        Assert.False(simulation.Submit(command).IsValid);
        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(
            CharacterVitalStatus.Alive,
            Profile(simulation, Subject).Condition.VitalStatus);
        WorldState restored = WorldState.Restore(
            SaveEnvelope.Create("test", [], simulation).Snapshot);
        Assert.Equal(
            Subject,
            restored.CharacterSuccessions.CampaignContinuity!
                .ControlledCharacterId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F905_SimultaneousDeathsArePriorityAndSubmissionOrderDeterministic(
        bool successorDiesFirst)
    {
        string? expectedEvents = null;
        string? expectedChecksum = null;
        for (int reverseSubmission = 0; reverseSubmission < 2; reverseSubmission++)
        {
            CampaignSimulation simulation = CreateSimulation(
                [
                    Seed(Subject, new(160, 1, 1)),
                    Seed(Successor, new(180, 1, 1), Parent(Subject)),
                    Seed(Grandchild, new(195, 1, 1), Parent(Successor)),
                ],
                CharacterSuccessionWorldSnapshot.Empty);
            SuccessionResolutionRule rule = Rule(
                [SuccessionCandidateBasis.BiologicalDescendant],
                [SuccessionLegalBasis.BiologicalDescendant]);
            CampaignCommand subjectDeath = SuccessionDeathCommand(
                simulation,
                Subject,
                rule,
                $"race-subject-{successorDiesFirst}",
                priority: successorDiesFirst ? 10 : -10);
            CampaignCommand successorDeath = SuccessionDeathCommand(
                simulation,
                Successor,
                rule,
                $"race-successor-{successorDiesFirst}",
                priority: successorDiesFirst ? -10 : 10);
            CampaignCommand[] commands = reverseSubmission == 0
                ? [subjectDeath, successorDeath]
                : [successorDeath, subjectDeath];
            foreach (CampaignCommand command in commands)
            {
                Assert.True(simulation.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
            Assert.Equal(2, events.Count);
            if (successorDiesFirst)
            {
                Assert.Single(
                    events,
                    item => item.Payload is CommandCancelledEventPayload);
                Assert.Equal(
                    CharacterVitalStatus.Alive,
                    Profile(simulation, Subject).Condition.VitalStatus);
                Assert.Equal(
                    CharacterVitalStatus.Dead,
                    Profile(simulation, Successor).Condition.VitalStatus);
                Assert.Single(simulation.World.CharacterSuccessions.Resolutions);
            }
            else
            {
                Assert.All(
                    events,
                    item => Assert.IsType<
                        CharacterConditionActionResolvedEventPayload>(
                            item.Payload));
                Assert.Equal(
                    CharacterVitalStatus.Dead,
                    Profile(simulation, Subject).Condition.VitalStatus);
                Assert.Equal(
                    CharacterVitalStatus.Dead,
                    Profile(simulation, Successor).Condition.VitalStatus);
                Assert.Equal(2, simulation.World.CharacterSuccessions.Resolutions.Count);
                Assert.Equal(
                    Grandchild,
                    simulation.World.CharacterSuccessions.Resolutions.Single(
                        item => item.SubjectCharacterId == Successor)
                        .SelectedCandidate!.CandidateCharacterId);
            }

            string serializedEvents = Serialize(events);
            string checksum =
                SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
            if (reverseSubmission == 0)
            {
                expectedEvents = serializedEvents;
                expectedChecksum = checksum;
            }
            else
            {
                Assert.Equal(expectedEvents, serializedEvents);
                Assert.Equal(expectedChecksum, checksum);
            }
        }
    }

    [Fact]
    public void F905_SameTurnContinuityUsesEventOrderNotResolutionHashOrder()
    {
        EntityId other = Character("priority-other");
        EntityId otherSuccessor = Character("priority-other-successor");
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.BiologicalDescendant]);
        string? expectedEvents = null;
        string? expectedChecksum = null;
        for (int reverseSubmission = 0; reverseSubmission < 2; reverseSubmission++)
        {
            CampaignSimulation simulation = CreateSimulation(
                [
                    Seed(Subject, new(160, 1, 1)),
                    Seed(Successor, new(180, 1, 1), Parent(Subject)),
                    Seed(other, new(161, 1, 1)),
                    Seed(
                        otherSuccessor,
                        new(181, 1, 1),
                        Parent(other)),
                ],
                SuccessionSnapshot());
            string otherSuffix = string.Empty;
            string subjectSuffix = string.Empty;
            for (int index = 0; index < 10_000; index++)
            {
                string candidateOther = $"priority-other-{index:D4}";
                string candidateSubject = $"priority-subject-{index:D4}";
                EntityId otherEvent = CharacterConditionIds.DeriveActionEventId(
                    Date,
                    new($"command:test/f9-{candidateOther}"));
                EntityId subjectEvent =
                    CharacterConditionIds.DeriveActionEventId(
                        Date,
                        new($"command:test/f9-{candidateSubject}"));
                if (CharacterSuccessionIds.DeriveResolutionId(
                        otherEvent,
                        other)
                    .CompareTo(CharacterSuccessionIds.DeriveResolutionId(
                        subjectEvent,
                        Subject)) > 0)
                {
                    otherSuffix = candidateOther;
                    subjectSuffix = candidateSubject;
                    break;
                }
            }

            Assert.NotEmpty(otherSuffix);
            CampaignCommand otherDeath = SuccessionDeathCommand(
                simulation,
                other,
                rule,
                otherSuffix,
                priority: -10);
            CampaignCommand subjectDeath = SuccessionDeathCommand(
                simulation,
                Subject,
                rule,
                subjectSuffix,
                priority: 10);
            CampaignCommand[] commands = reverseSubmission == 0
                ? [otherDeath, subjectDeath]
                : [subjectDeath, otherDeath];
            foreach (CampaignCommand command in commands)
            {
                Assert.True(simulation.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
            Assert.Equal(2, events.Count);
            Assert.All(events, item => Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(item.Payload));
            Assert.Equal(
                Successor,
                simulation.World.CharacterSuccessions.CampaignContinuity!
                    .ControlledCharacterId);
            Assert.Equal(2, simulation.World.CharacterSuccessions.Resolutions.Count);

            string serializedEvents = Serialize(events);
            string checksum =
                SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
            if (reverseSubmission == 0)
            {
                expectedEvents = serializedEvents;
                expectedChecksum = checksum;
            }
            else
            {
                Assert.Equal(expectedEvents, serializedEvents);
                Assert.Equal(expectedChecksum, checksum);
            }
        }
    }

    [Fact]
    public void F906_PendingResolvedAndLaterDaySaveRecoveryReplayExactly()
    {
        CampaignSimulation original = CreateSimulation(
            [
                Seed(Subject, new(160, 1, 1)),
                Seed(Successor, new(180, 1, 1), Parent(Subject)),
            ],
            SuccessionSnapshot(),
            resources: Resources((Subject, 12), (Successor, 3)),
            estates: Estates((new("estate:test/f9-save"), Subject)));
        CampaignDate resolutionDate = Date.AddDays(2);
        SuccessionResolutionRule rule = Rule(
            [SuccessionCandidateBasis.BiologicalDescendant],
            [SuccessionLegalBasis.BiologicalDescendant]);
        CampaignCommand command = SuccessionDeathCommand(
            original,
            Subject,
            rule,
            "save",
            resolutionDate: resolutionDate);
        Assert.True(original.Submit(command).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f9-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            SaveStore store = new();
            string pendingPath = Path.Combine(directory, "pending.save.gz");
            SaveEnvelope pendingEnvelope =
                SaveEnvelope.Create("test", [], original);
            store.SaveAtomic(
                pendingPath,
                pendingEnvelope);
            SaveEnvelope pending = store.Load(pendingPath);
            Assert.Equal(28, pending.SchemaVersion);
            Assert.Single(pending.Snapshot.PendingCommands);
            CampaignSimulation replay =
                new(WorldState.Restore(pending.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
            CharacterSuccessionDeathResolvedOutcome outcome = Assert.IsType<
                CharacterSuccessionDeathResolvedOutcome>(Assert.IsType<
                    CharacterConditionActionResolvedEventPayload>(
                        Assert.Single(first).Payload).Outcome);
            Assert.Equal(resolutionDate, outcome.Succession.ResolutionDate);
            Assert.Equal(
                Successor,
                outcome.Succession.CurrentCampaignContinuity!
                    .ControlledCharacterId);

            string resolvedPath = Path.Combine(directory, "resolved.save.gz");
            store.SaveAtomic(
                resolvedPath,
                SaveEnvelope.Create("test", [], original));
            SaveEnvelope resolved = store.Load(resolvedPath);
            WorldState restored = WorldState.Restore(resolved.Snapshot);
            Assert.Equal(
                Serialize(original.World.CaptureSnapshot()),
                Serialize(restored.CaptureSnapshot()));
            Assert.Equal(
                Successor,
                restored.CharacterSuccessions.CampaignContinuity!
                    .ControlledCharacterId);
            Assert.Equal(
                Successor,
                Assert.Single(restored.CharacterEstateHoldings.Holdings)
                    .OwnerCharacterId);

            string recoveryPath = Path.Combine(
                directory,
                "recovery.save.gz");
            store.SaveAutosave(recoveryPath, pendingEnvelope);
            store.SaveAutosave(
                recoveryPath,
                SaveEnvelope.Create("test", [], original));
            byte[] corrupt = [0x00, 0x01, 0x02, 0x03];
            File.WriteAllBytes(recoveryPath, corrupt);
            SaveLoadResult recovered =
                store.LoadWithRecovery(recoveryPath);
            Assert.Equal(recoveryPath + ".1", recovered.SourcePath);
            Assert.Equal(pendingEnvelope.Checksum, recovered.Envelope.Checksum);
            Assert.NotNull(recovered.RecoveryDiagnostic);
            Assert.Equal(corrupt, File.ReadAllBytes(recoveryPath));
            CampaignSimulation recoveredReplay =
                new(WorldState.Restore(recovered.Envelope.Snapshot));
            Assert.Equal(
                Serialize(first),
                Serialize(recoveredReplay.ResolveTurn()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CampaignSimulation CreateSimulation(
        IReadOnlyList<CharacterSeed> seeds,
        CharacterSuccessionWorldSnapshot succession,
        CharacterResourceWorldSnapshot? resources = null,
        CharacterEstateHoldingWorldSnapshot? estates = null,
        HouseholdSeed? household = null,
        CareerWorldSnapshot? careers = null,
        CharacterGuardianshipWorldSnapshot? guardianships = null)
    {
        CharacterDefinition[] definitions = seeds
            .OrderBy(item => item.Id)
            .Select(item =>
            {
                EntityId nameKey =
                    new($"loc:{item.Id.Value.Replace(':', '/')}");
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
                item.Parents.Select(parent => parent.ParentCharacterId).ToArray(),
                item.Parents,
                item.Condition,
                []))
            .ToArray();
        HouseholdDefinition[] householdDefinitions = household is null
            ? []
            :
            [
                new(
                    CharacterContractVersions.Definition,
                    household.HouseholdId,
                    new("loc:household/f9_primary")),
            ];
        HouseholdState[] householdStates = household is null
            ? []
            :
            [
                new(
                    CharacterContractVersions.State,
                    household.HouseholdId,
                    household.HeadCharacterId,
                    household.MemberIds.Order().ToArray()),
            ];
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            householdDefinitions,
            states,
            [],
            householdStates);
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            careers ?? CareerWorldSnapshot.Empty,
            resources ?? CharacterResourceWorldSnapshot.Empty,
            estates ?? CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            guardianships ?? CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            succession));
    }

    private static CampaignCommand SuccessionDeathCommand(
        CampaignSimulation simulation,
        EntityId subject,
        SuccessionResolutionRule rule,
        string suffix,
        EntityId? householdId = null,
        EntityId? replacementHeadCharacterId = null,
        EntityId? regentCharacterId = null,
        CampaignDate? resolutionDate = null,
        int priority = 0)
    {
        CampaignDate date = resolutionDate ?? Date;
        EntityId stateId =
            simulation.World.GetCharacterSuccessionResolutionStateId(
                subject,
                rule,
                date,
                simulation.World.Calendar.TurnIndex,
                householdId,
                replacementHeadCharacterId,
                regentCharacterId);
        return CampaignCommand.Create(
            new($"command:test/f9-{suffix.ToLowerInvariant()}"),
            CharacterConditionSystem.AuthoritativeActorId,
            date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterSuccessionDeathAction(
                    subject,
                    Profile(simulation, subject).Condition,
                    rule,
                    stateId,
                    householdId,
                    replacementHeadCharacterId,
                    regentCharacterId)),
            priority: priority);
    }

    private static SuccessionResolutionRule Rule(
        IReadOnlyList<SuccessionCandidateBasis> candidateBases,
        IReadOnlyList<SuccessionLegalBasis> legalPrecedence,
        SuccessionContestResolutionMode contestMode =
            SuccessionContestResolutionMode.ResolveByStableId,
        SuccessionNoAcceptedSuccessorBehavior behavior =
            SuccessionNoAcceptedSuccessorBehavior.EndCampaign) => new(
        CharacterSuccessionContractVersions.ResolutionRule,
        new(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            candidateBases,
            8,
            0,
            AllowsIncapacitatedCandidates: true,
            Enum.GetValues<CharacterCustodyStatus>()),
        legalPrecedence,
        IncludesPrincipalSpouse: false,
        AllowedCollateralKinds: [],
        MaximumCollateralDistance: 0,
        contestMode,
        MaximumCandidates: 64,
        MaximumDisputedCandidates: 16,
        CreatesRegencyForIncapacitatedSuccessor: true,
        behavior);

    private static CharacterSuccessionWorldSnapshot SuccessionSnapshot(
        HeirDesignationState? designation = null,
        SuccessionClaimState? claim = null,
        SuccessionSupportState? support = null) =>
        CharacterSuccessionWorldSnapshot.Empty with
        {
            Designations = designation is null ? [] : [designation],
            Claims = claim is null ? [] : [claim],
            Supports = support is null ? [] : [support],
            CampaignContinuity = new(
                CharacterSuccessionContractVersions.CampaignContinuity,
                PlayerCampaignContinuityStatus.Active,
                Subject,
                Date.AddDays(-1),
                0,
                new("command:test/f9-continuity-seed"),
                new("event:test/f9-continuity-seed")),
        };

    private static HeirDesignationState ActiveDesignation(
        EntityId subject,
        EntityId heir,
        string suffix)
    {
        CampaignDate date = Date.AddDays(-3);
        EntityId commandId =
            new($"command:test/f9-{suffix}-designation");
        EntityId eventId =
            CharacterSuccessionIds.DeriveActionEventId(date, commandId);
        return new(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(
                eventId,
                subject,
                heir),
            subject,
            heir,
            date,
            0,
            commandId,
            eventId,
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
        CampaignDate date = Date.AddDays(-2);
        EntityId commandId = new($"command:test/f9-{suffix}-claim");
        EntityId eventId =
            CharacterSuccessionIds.DeriveClaimActionEventId(date, commandId);
        return new(
            CharacterSuccessionContractVersions.ClaimState,
            CharacterSuccessionIds.DeriveClaimId(
                eventId,
                subject,
                claimant),
            subject,
            claimant,
            SuccessionClaimOrigin.PersonalAssertion,
            date,
            0,
            commandId,
            eventId,
            SuccessionClaimStatus.Active,
            null,
            null,
            null,
            null);
    }

    private static SuccessionSupportState ActiveSupport(
        EntityId subject,
        EntityId supporter,
        EntityId candidate,
        string suffix)
    {
        CampaignDate date = Date.AddDays(-1);
        EntityId commandId = new($"command:test/f9-{suffix}-support");
        EntityId eventId =
            CharacterSuccessionIds.DeriveSupportActionEventId(date, commandId);
        return new(
            CharacterSuccessionContractVersions.SupportState,
            CharacterSuccessionIds.DeriveSupportId(
                eventId,
                subject,
                supporter,
                candidate),
            subject,
            supporter,
            candidate,
            date,
            0,
            commandId,
            eventId,
            SuccessionSupportStatus.Active,
            null,
            null,
            null,
            null);
    }

    private static CareerWorldSnapshot ActiveRetinueMembership(
        EntityId leader,
        EntityId member,
        string suffix)
    {
        RetinueState retinue = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueId(leader),
            leader);
        EntityId proposalId =
            new($"career_proposal:test/f9-{suffix}-retinue");
        RetinueMembershipState membership = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueMembershipId(proposalId),
            retinue.RetinueId,
            leader,
            member,
            proposalId,
            Date.AddDays(-10),
            0,
            null,
            null,
            null,
            null);
        return new(
            CareerContractVersions.Snapshot,
            [],
            [retinue],
            [membership],
            [],
            [],
            [],
            []);
    }

    private static CharacterResourceWorldSnapshot Resources(
        params (EntityId CharacterId, long Wealth)[] accounts) => new(
        CharacterResourceContractVersions.Snapshot,
        accounts.Select(item => Account(item.CharacterId, item.Wealth)).ToArray(),
        [],
        []);

    private static CharacterWealthAccountState Account(
        EntityId characterId,
        long wealth) => new(
        CharacterResourceContractVersions.State,
        CharacterResourceIds.DeriveWealthAccountId(characterId),
        characterId,
        wealth);

    private static CharacterEstateHoldingWorldSnapshot Estates(
        params (EntityId EstateId, EntityId OwnerCharacterId)[] holdings) => new(
        CharacterEstateHoldingContractVersions.Snapshot,
        holdings.Select(item => new CharacterEstateHoldingState(
            CharacterEstateHoldingContractVersions.State,
            item.EstateId,
            item.OwnerCharacterId)).ToArray());

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId characterId) =>
        simulation.World.Characters.Profiles.Single(
            item => item.CharacterId == characterId);

    private static CharacterParentLink Parent(EntityId parent) =>
        new(parent, ParentChildLinkKind.Biological);

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        CharacterParentLink? parent = null,
        CharacterConditionState? condition = null) => new(
        id,
        birthDate,
        parent is null ? [] : [parent],
        condition ?? CharacterConditionState.Default);

    private static EntityId Character(string suffix) =>
        new($"character:test/f9-{suffix}");

    private static string SnapshotJson(CampaignSimulation simulation) =>
        Serialize(simulation.World.CaptureSnapshot());

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        IReadOnlyList<CharacterParentLink> Parents,
        CharacterConditionState Condition);

    private sealed record HouseholdSeed(
        EntityId HouseholdId,
        EntityId HeadCharacterId,
        IReadOnlyList<EntityId> MemberIds);
}
