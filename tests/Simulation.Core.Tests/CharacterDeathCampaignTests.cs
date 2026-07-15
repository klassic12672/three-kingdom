using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterDeathCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Target = new("character:test/death-target");
    private static readonly EntityId Spouse = new("character:test/death-spouse");
    private static readonly EntityId Ward = new("character:test/death-ward");
    private static readonly EntityId Other = new("character:test/death-other");
    private static readonly EntityId PracticeId = new("marriage_practice:test/death");
    private static readonly EntityId PreservedEducationAbility =
        new("ability:test/death-preserved");
    private static readonly CampaignDate PreservedGuardianshipDate =
        new(179, 1, 1);
    private static readonly EntityId PreservedGuardianshipCommand =
        new("command:test/death-preserved-guardianship");
    private static readonly EntityId PreservedGuardianshipEvent =
        CharacterFamilyIds.DeriveActionEventId(
            PreservedGuardianshipDate,
            PreservedGuardianshipCommand);
    private static readonly EntityId PreservedGuardianshipId =
        CharacterGuardianshipIds.DeriveGuardianshipId(
            PreservedGuardianshipEvent,
            Target,
            Other);
    private readonly ITestOutputHelper output;

    public CharacterDeathCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void F001_PublicDeathAtomicallyClosesLifecycleAndPreservesInheritanceInputs()
    {
        CampaignSimulation simulation = CreateLifecycleSimulation(targetIsGestationalParent: true);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = DeathCommand(simulation, "success");

        AssertValid(simulation.Submit(command));
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterConditionActionResolvedEventPayload payload = Assert.IsType<
            CharacterConditionActionResolvedEventPayload>(campaignEvent.Payload);
        ResolveCharacterDeathAction action = Assert.IsType<ResolveCharacterDeathAction>(payload.Action);
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(payload.Outcome).Death;

        Assert.Equal(Target, action.CharacterId);
        Assert.Equal(CharacterConditionSystem.AuthoritativeActorId, payload.ActingActorId);
        Assert.Null(payload.RelationshipMemoryConsequence);
        Assert.Equal(command.CommandId, campaignEvent.CausalId);
        Assert.Equal(
            CharacterConditionIds.DeriveActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(
            CharacterConditionIds.DeriveDeathId(campaignEvent.EventId, Target),
            death.DeathId);
        Assert.Equal(CharacterVitalStatus.Dead, death.ConditionChange.CurrentCondition.VitalStatus);
        Assert.Equal(CharacterHealthStatus.Critical, death.ConditionChange.CurrentCondition.HealthStatus);
        Assert.True(death.ConditionChange.CurrentCondition.IsIncapacitated);
        Assert.Equal(CharacterCustodyStatus.Free, death.ConditionChange.CurrentCondition.CustodyStatus);
        Assert.Null(death.ConditionChange.CurrentCondition.CustodianId);
        Assert.Equal(CharacterMarriageLifecycleReason.CharacterDied, death.MarriageChanges.Reason);
        Assert.Equal(MarriageUnionEndReason.SpouseDied, Assert.Single(death.MarriageChanges.EndedUnions).EndReason);
        Assert.Equal(
            CharacterGuardianshipEndReason.GuardianDied,
            Assert.Single(death.EndedGuardianships).EndReason);
        Assert.Single(death.RemovedPregnancies);
        Assert.Equal(
            WorldState.GetCharacterConditionActionAffectedIds(payload, campaignEvent.EventId),
            campaignEvent.AffectedIds);

        WorldSnapshot after = simulation.World.CaptureSnapshot();
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
        Assert.Empty(after.CharacterPregnancies.ActivePregnancies);
        Assert.Equal(
            CharacterGuardianshipStatus.Ended,
            after.CharacterGuardianships.Guardianships.Single(item =>
                item.GuardianshipId == death.EndedGuardianships[0].GuardianshipId).Status);
        Assert.Equal(
            Serialize(before.CharacterGuardianships.Guardianships.Single(item =>
                item.GuardianshipId == PreservedGuardianshipId)),
            Serialize(after.CharacterGuardianships.Guardianships.Single(item =>
                item.GuardianshipId == PreservedGuardianshipId)));
        Assert.Equal(
            MarriageUnionStatus.Ended,
            Assert.Single(after.CharacterMarriages.Unions).Status);
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(Serialize(before.CharacterEstateHoldings), Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.Careers), Serialize(after.Careers));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
        Assert.Equal(Serialize(before.RandomStreams), Serialize(after.RandomStreams));
        CharacterState targetBefore = before.Characters.CharacterStates.Single(
            item => item.CharacterId == Target);
        CharacterState targetAfter = after.Characters.CharacterStates.Single(
            item => item.CharacterId == Target);
        Assert.NotEmpty(targetBefore.ParentLinks!);
        Assert.NotEmpty(targetBefore.EducationAttainments!);
        Assert.Equal(Serialize(targetBefore.ParentIds), Serialize(targetAfter.ParentIds));
        Assert.Equal(Serialize(targetBefore.ParentLinks), Serialize(targetAfter.ParentLinks));
        Assert.Equal(
            Serialize(targetBefore.EducationAttainments),
            Serialize(targetAfter.EducationAttainments));
        Assert.Equal(
            Serialize(before.Characters.FamilyStates),
            Serialize(after.Characters.FamilyStates));
        Assert.Equal(
            Serialize(before.Characters.HouseholdStates),
            Serialize(after.Characters.HouseholdStates));
        Assert.Contains(
            after.Characters.HouseholdStates.Single().MemberIds,
            item => item == Target);
        Assert.NotEqual(
            Target,
            after.Characters.HouseholdStates.Single().HeadCharacterId);
        Assert.Equal(Target, Assert.Single(after.CharacterEstateHoldings.Holdings).OwnerCharacterId);
        Assert.Equal(Target, Assert.Single(after.CharacterResources.Accounts).CharacterId);

        string json = JsonSerializer.Serialize(campaignEvent, SimulationJson.CreateOptions());
        Assert.Contains("character_condition_action_resolved.v1", json, StringComparison.Ordinal);
        Assert.Contains("resolve_character_death.v1", json, StringComparison.Ordinal);
        Assert.Contains("character_death_resolved.v1", json, StringComparison.Ordinal);
        CampaignEvent roundTrip = JsonSerializer.Deserialize<CampaignEvent>(
            json,
            SimulationJson.CreateOptions())!;
        Assert.Equal(json, JsonSerializer.Serialize(roundTrip, SimulationJson.CreateOptions()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F002_DeathDuringPregnancyRemovesEitherParentRole(bool targetIsGestationalParent)
    {
        CampaignSimulation simulation = CreateLifecycleSimulation(targetIsGestationalParent);

        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(SubmitDeath(
                simulation,
                $"pregnancy-role-{targetIsGestationalParent.ToString().ToLowerInvariant()}").Payload).Outcome).Death;

        CharacterPregnancyState removed = Assert.Single(death.RemovedPregnancies);
        Assert.Equal(
            targetIsGestationalParent,
            removed.GestationalParentCharacterId == Target);
        Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
        Assert.Equal(
            MarriageUnionStatus.Ended,
            Assert.Single(simulation.World.CharacterMarriages.Unions).Status);
    }

    [Fact]
    public void F002A_DeathRejectsUnauthorizedWrongPhaseMissingAndDeadTargets()
    {
        CampaignSimulation unauthorized = CreateSimpleSimulation();
        string unauthorizedBefore = SnapshotJson(unauthorized);
        AssertInvalid(unauthorized.Submit(CampaignCommand.Create(
            new EntityId("command:test/death-unauthorized"),
            Other,
            Date,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                Target,
                Profile(unauthorized, Target).Condition)))));
        AssertAliveAndUnchanged(unauthorized, unauthorizedBefore);

        CampaignSimulation wrongPhase = CreateSimpleSimulation();
        string wrongPhaseBefore = SnapshotJson(wrongPhase);
        AssertInvalid(wrongPhase.Submit(CampaignCommand.Create(
            new EntityId("command:test/death-wrong-phase"),
            CharacterConditionSystem.AuthoritativeActorId,
            Date,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                Target,
                Profile(wrongPhase, Target).Condition)),
            ResolutionPhase.Systems)));
        AssertAliveAndUnchanged(wrongPhase, wrongPhaseBefore);

        CampaignSimulation missing = CreateSimpleSimulation();
        string missingBefore = SnapshotJson(missing);
        AssertInvalid(missing.Submit(CampaignCommand.Create(
            new EntityId("command:test/death-missing"),
            CharacterConditionSystem.AuthoritativeActorId,
            Date,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                new EntityId("character:test/death-missing"),
                CharacterConditionState.Default)))));
        Assert.Equal(missingBefore, SnapshotJson(missing));

        CampaignSimulation livingSource = CreateSimpleSimulation();
        WorldSnapshot deadSnapshot = livingSource.World.CaptureSnapshot();
        deadSnapshot = deadSnapshot with
        {
            Characters = deadSnapshot.Characters with
            {
                CharacterStates = deadSnapshot.Characters.CharacterStates
                    .Select(item => item.CharacterId == Target
                        ? item with
                        {
                            Condition = new CharacterConditionState(
                                CharacterVitalStatus.Dead,
                                CharacterHealthStatus.Critical,
                                IsIncapacitated: true,
                                CharacterCustodyStatus.Free,
                                null),
                        }
                        : item)
                    .ToArray(),
            },
        };
        CampaignSimulation dead = new(WorldState.Restore(deadSnapshot));
        string deadBefore = SnapshotJson(dead);
        AssertInvalid(dead.Submit(CampaignCommand.Create(
            new EntityId("command:test/death-already-dead"),
            CharacterConditionSystem.AuthoritativeActorId,
            Date,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                Target,
                Profile(dead, Target).Condition)))));
        Assert.Equal(deadBefore, SnapshotJson(dead));
    }

    [Fact]
    public void F003_HouseholdHeadCustodianAndCareerRetinueBlockersFailClosed()
    {
        CampaignSimulation householdHead = CreateSimpleSimulation(
            householdHead: true);
        string householdHeadBefore = SnapshotJson(householdHead);
        AssertInvalid(householdHead.Submit(DeathCommand(householdHead, "head")));
        AssertAliveAndUnchanged(householdHead, householdHeadBefore);

        CampaignSimulation custodian = CreateSimpleSimulation(
            otherCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Target,
            });
        string custodianBefore = SnapshotJson(custodian);
        AssertInvalid(custodian.Submit(DeathCommand(custodian, "custodian")));
        AssertAliveAndUnchanged(custodian, custodianBefore);

        CampaignSimulation retinueLeader = CreateSimpleSimulation(
            careers: new CareerWorldSnapshot(
                CareerContractVersions.Snapshot,
                [],
                [new RetinueState(
                    CareerContractVersions.State,
                    CareerIds.DeriveRetinueId(Target),
                    Target)],
                [],
                [],
                [],
                [],
                []));
        string retinueLeaderBefore = SnapshotJson(retinueLeader);
        AssertInvalid(retinueLeader.Submit(DeathCommand(retinueLeader, "retinue")));
        AssertAliveAndUnchanged(retinueLeader, retinueLeaderBefore);
    }

    [Fact]
    public void F004_ActiveCareerRolesBlockButCompletedHistoryAndRecommendationsDoNot()
    {
        CareerProposalState activeProposal = new(
            CareerContractVersions.State,
            CareerIds.DeriveProposalId(
                CareerProposalKind.PatronageOffer,
                Date,
                new EntityId("command:test/death-career-proposal")),
            CareerProposalKind.PatronageOffer,
            Target,
            Other,
            new ServicePrincipalReference(ServicePrincipalKind.Character, Target),
            null,
            Date,
            0,
            new EntityId("command:test/death-career-proposal"),
            CareerProposalStatus.Active,
            null,
            null,
            null);
        CampaignSimulation blocked = CreateSimpleSimulation(careers: new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            [activeProposal],
            [],
            [],
            [],
            [],
            [],
            []));
        string blockedBefore = SnapshotJson(blocked);
        AssertInvalid(blocked.Submit(DeathCommand(blocked, "career-active")));
        AssertAliveAndUnchanged(blocked, blockedBefore);

        CareerProposalState completedProposal = activeProposal with
        {
            Status = CareerProposalStatus.Refused,
            ResolutionDate = Date,
            ResolutionTurnIndex = 0,
            ResolutionCommandId = new EntityId("command:test/death-career-refused"),
        };
        EntityId recommendationCommand = new("command:test/death-career-recommendation");
        RecommendationRecord recommendation = new(
            CareerContractVersions.State,
            CareerIds.DeriveRecommendationId(Date, recommendationCommand),
            Other,
            Target,
            new ServicePrincipalReference(ServicePrincipalKind.Character, Other),
            null,
            Date,
            0,
            recommendationCommand);
        CareerHistoryAggregate history = CareerHistoryAggregate.Empty(Target) with
        {
            FoldedPatronageProposalCount = 1,
            FoldedRecommendationCount = 1,
            EarliestDate = Date,
            LatestDate = Date,
        };
        CampaignSimulation allowed = CreateSimpleSimulation(careers: new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            [completedProposal],
            [],
            [],
            [],
            [recommendation],
            [],
            [history]));
        string retainedCareer = Serialize(allowed.World.Careers.CaptureSnapshot());

        _ = SubmitDeath(allowed, "career-completed");

        Assert.Equal(CharacterVitalStatus.Dead, Profile(allowed, Target).Condition.VitalStatus);
        Assert.Equal(retainedCareer, Serialize(allowed.World.Careers.CaptureSnapshot()));
    }

    [Theory]
    [InlineData(CareerProposalKind.RetinueInvitation)]
    [InlineData(CareerProposalKind.PatronageOffer)]
    [InlineData(CareerProposalKind.EmploymentOffer)]
    public void F004A_EachActiveCareerServiceRoleBlocksWithoutMutation(CareerProposalKind kind)
    {
        CampaignSimulation simulation = CreateSimpleSimulation();
        ICharacterAction offer = kind switch
        {
            CareerProposalKind.RetinueInvitation => new RetinueInviteAction(Target),
            CareerProposalKind.PatronageOffer => new PatronageOfferAction(Target),
            CareerProposalKind.EmploymentOffer => new EmploymentOfferAction(
                Target,
                new ServicePrincipalReference(ServicePrincipalKind.Character, Other),
                new EntityId("role:test/death-employment")),
            _ => throw new InvalidOperationException(),
        };
        CharacterActionResolvedEventPayload offered = SubmitCareer(
            simulation,
            Other,
            offer,
            $"service-offer-{kind.ToString().ToLowerInvariant()}");
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(
            offered.Outcome).Proposal;
        ICharacterAction accept = kind switch
        {
            CareerProposalKind.RetinueInvitation =>
                new RespondToRetinueInvitationAction(proposal.ProposalId, CareerProposalResponse.Accept),
            CareerProposalKind.PatronageOffer =>
                new RespondToPatronageOfferAction(proposal.ProposalId, CareerProposalResponse.Accept),
            CareerProposalKind.EmploymentOffer =>
                new RespondToEmploymentOfferAction(proposal.ProposalId, CareerProposalResponse.Accept),
            _ => throw new InvalidOperationException(),
        };
        _ = SubmitCareer(
            simulation,
            Target,
            accept,
            $"service-accept-{kind.ToString().ToLowerInvariant()}");
        string before = SnapshotJson(simulation);

        AssertInvalid(simulation.Submit(DeathCommand(
            simulation,
            $"service-death-{kind.ToString().ToLowerInvariant()}")));
        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Target).Condition.VitalStatus);
    }

    [Fact]
    public void F005_ConditionAndDeathRaceUsesCanonicalPriorityAndRollsBackTheStaleLoser()
    {
        for (int deathFirst = 0; deathFirst < 2; deathFirst++)
        {
            string? expectedChecksum = null;
            string? expectedEvents = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateSimpleSimulation();
                CharacterConditionState expected = Profile(simulation, Target).Condition;
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"race-death-{deathFirst}",
                    priority: deathFirst == 1 ? 0 : 1);
                CampaignCommand condition = CampaignCommand.Create(
                    new EntityId($"command:test/death-race-condition-{deathFirst}"),
                    CharacterConditionSystem.AuthoritativeActorId,
                    Date,
                    new CharacterConditionActionCommandPayload(
                        new IncapacitateCharacterAction(Target, expected)),
                    priority: deathFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [death, condition]
                    : [condition, death];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
                if (deathFirst == 1)
                {
                    Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                }
                else
                {
                    Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Target).Condition.VitalStatus);
                    Assert.True(Profile(simulation, Target).Condition.IsIncapacitated);
                }

                string eventKinds = string.Join(",", events.Select(item => item.Payload.GetType().Name));
                string checksum = SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = eventKinds;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, eventKinds);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F006_ForgedDeathEvidenceAndAffectedIdsRollBackCompletely()
    {
        CampaignSimulation simulation = CreateLifecycleSimulation(targetIsGestationalParent: true);
        CampaignCommand command = DeathCommand(simulation, "tamper");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(Date, command.CommandId);
        CharacterConditionAggregatePlan plan = simulation.World.PrepareCharacterConditionAction(
            command.IssuingActor,
            Assert.IsType<CharacterConditionActionCommandPayload>(command.Payload),
            Date,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CharacterConditionActionResolvedEventPayload resolved = plan.ResolvedPayload;
        CharacterDeathResolvedOutcome outcome = Assert.IsType<CharacterDeathResolvedOutcome>(resolved.Outcome);
        CampaignEvent valid = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            Date,
            ResolutionPhase.Commands,
            command.Priority,
            WorldState.GetCharacterConditionActionAffectedIds(resolved, eventId),
            resolved);
        string before = SnapshotJson(simulation);

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            AffectedIds = valid.AffectedIds.Skip(1).ToArray(),
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            Phase = ResolutionPhase.Systems,
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            CausalId = new EntityId("command:test/death-tamper-causal"),
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            EventId = new EntityId("event:test/death-tamper-event"),
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        CharacterConditionActionResolvedEventPayload forgedPayload = resolved with
        {
            Outcome = outcome with
            {
                Death = outcome.Death with
                {
                    DeathId = new EntityId("character_death:test/forged"),
                },
            },
        };
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            Payload = forgedPayload,
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        CharacterGuardianshipState ended = Assert.Single(outcome.Death.EndedGuardianships);
        CharacterConditionActionResolvedEventPayload reorderedPayload = resolved with
        {
            Outcome = outcome with
            {
                Death = outcome.Death with
                {
                    EndedGuardianships =
                    [
                        ended with
                        {
                            GuardianshipId = new EntityId("guardianship:test/reordered"),
                        },
                        ended,
                    ],
                },
            },
        };
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            Payload = reorderedPayload,
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        CharacterConditionActionResolvedEventPayload forgedNestedPayload = resolved with
        {
            Outcome = outcome with
            {
                Death = outcome.Death with
                {
                    RemovedPregnancies = [],
                },
            },
        };
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            Payload = forgedNestedPayload,
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        simulation.World.Apply(valid);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid));
    }

    [Fact]
    public void F007_PendingAndResolvedDeathRoundTripAtCurrentSchema()
    {
        CampaignSimulation original = CreateLifecycleSimulation(targetIsGestationalParent: false);
        AssertValid(original.Submit(DeathCommand(original, "pending-save")));
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f0-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string pendingPath = Path.Combine(directory, "pending.save.gz");
            SaveStore store = new();
            store.SaveAtomic(pendingPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope pending = store.Load(pendingPath);
            Assert.Equal(21, pending.SchemaVersion);
            CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));

            string resolvedPath = Path.Combine(directory, "resolved.save.gz");
            store.SaveAtomic(resolvedPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope resolved = store.Load(resolvedPath);
            Assert.Contains(
                resolved.DiagnosticEvents,
                item => item.Payload is CharacterConditionActionResolvedEventPayload
                {
                    Outcome: CharacterDeathResolvedOutcome,
                });
            Assert.Equal(
                CharacterVitalStatus.Dead,
                WorldState.Restore(resolved.Snapshot).Characters.Profiles
                    .Single(item => item.CharacterId == Target).Condition.VitalStatus);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void F007H_PendingDeathReplaysOnEveryLaterDayOfAFourDayTurn(int dayOffset)
    {
        CampaignSimulation source = CreateLifecycleSimulation(targetIsGestationalParent: true);
        WorldSnapshot turnOne = source.World.CaptureSnapshot() with
        {
            Calendar = new CampaignCalendar(Date, 1),
        };
        CampaignSimulation original = new(WorldState.Restore(turnOne));
        CampaignDate resolutionDate = Date.AddDays(dayOffset);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/death-later-turn-day-{dayOffset}"),
            CharacterConditionSystem.AuthoritativeActorId,
            resolutionDate,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                Target,
                Profile(original, Target).Condition)));
        AssertValid(original.Submit(command));
        CampaignSimulation replay = new(WorldState.Restore(original.World.CaptureSnapshot()));

        CampaignEvent first = Assert.Single(original.ResolveTurn());
        CampaignEvent second = Assert.Single(replay.ResolveTurn());

        Assert.Equal(resolutionDate, first.ResolutionDate);
        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(first.Payload).Outcome).Death;
        Assert.Equal(resolutionDate, death.ResolutionDate);
        Assert.All(death.EndedGuardianships, item => Assert.Equal(resolutionDate, item.EndDate));
        Assert.Empty(original.World.CharacterPregnancies.ActivePregnancies);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(original, Target).Condition.VitalStatus);
    }

    [Fact]
    public void F007A_SimultaneousSpouseParentDeathsAreCanonicalInBothIdAssignments()
    {
        EntityId firstId = new("command:test/death-simultaneous-a");
        EntityId secondId = new("command:test/death-simultaneous-b");
        for (int assignment = 0; assignment < 2; assignment++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateLifecycleSimulation(
                    targetIsGestationalParent: false);
                EntityId firstCharacter = assignment == 0 ? Target : Spouse;
                EntityId secondCharacter = assignment == 0 ? Spouse : Target;
                CampaignCommand first = DeathCommand(
                    simulation,
                    firstId,
                    firstCharacter);
                CampaignCommand second = DeathCommand(
                    simulation,
                    secondId,
                    secondCharacter);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [first, second]
                    : [second, first];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                CharacterDeathChange[] deaths = events.Select(item =>
                    Assert.IsType<CharacterDeathResolvedOutcome>(
                        Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                            item.Payload).Outcome).Death).ToArray();
                Assert.Equal(2, deaths.Length);
                Assert.Equal(1, deaths.Sum(item => item.RemovedPregnancies.Count));
                Assert.Equal(1, deaths.Sum(item => item.MarriageChanges.EndedUnions.Count));
                Assert.Equal(1, deaths.Sum(item => item.EndedGuardianships.Count));
                Assert.All(
                    new[] { Target, Spouse },
                    id => Assert.Equal(
                        CharacterVitalStatus.Dead,
                        Profile(simulation, id).Condition.VitalStatus));
                Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F007B_CareerBlockerCreationRaceReplansInBothPriorityOrders()
    {
        for (int careerFirst = 0; careerFirst < 2; careerFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateSimpleSimulation();
                CampaignCommand career = CampaignCommand.Create(
                    new EntityId($"command:test/death-career-race-{careerFirst}"),
                    Other,
                    Date,
                    new CharacterActionCommandPayload(new RetinueInviteAction(Target)),
                    priority: careerFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"career-race-death-{careerFirst}",
                    priority: careerFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [career, death]
                    : [death, career];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
                Assert.Equal(
                    careerFirst == 1 ? CharacterVitalStatus.Alive : CharacterVitalStatus.Dead,
                    Profile(simulation, Target).Condition.VitalStatus);
                Assert.Equal(careerFirst == 1 ? 1 : 0, simulation.World.Careers.Proposals.Count);

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F007C_PregnancyRegistrationAndDeathRaceReplansBothOrders()
    {
        for (int registrationFirst = 0; registrationFirst < 2; registrationFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation seeded = CreateLifecycleSimulation(
                    targetIsGestationalParent: true);
                WorldSnapshot snapshot = seeded.World.CaptureSnapshot() with
                {
                    CharacterPregnancies = CharacterPregnancyWorldSnapshot.Empty,
                };
                CampaignSimulation simulation = new(WorldState.Restore(snapshot));
                MarriageUnionState union = Assert.Single(simulation.World.CharacterMarriages.Unions);
                CampaignCommand registration = CampaignCommand.Create(
                    new EntityId($"command:test/death-pregnancy-race-{registrationFirst}"),
                    CharacterFamilySystem.AuthoritativeActorId,
                    Date,
                    new CharacterFamilyActionCommandPayload(new RegisterActivePregnancyAction(
                        Target,
                        Spouse,
                        union.UnionId,
                        null)),
                    priority: registrationFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"pregnancy-race-death-{registrationFirst}",
                    priority: registrationFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [registration, death]
                    : [death, registration];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
                Assert.Equal(
                    registrationFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));
                CharacterDeathChange deathOutcome = Assert.IsType<CharacterDeathResolvedOutcome>(
                    Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(
                        item => item.Payload is CharacterConditionActionResolvedEventPayload).Payload).Outcome).Death;
                Assert.Equal(registrationFirst == 1 ? 1 : 0, deathOutcome.RemovedPregnancies.Count);

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F007D_GuardianshipClosureAndDeathRaceRetainsTheCanonicalCause()
    {
        for (int closureFirst = 0; closureFirst < 2; closureFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateLifecycleSimulation(
                    targetIsGestationalParent: true);
                CharacterGuardianshipState active = Assert.Single(
                    simulation.World.CharacterGuardianships.Guardianships,
                    item => item.Status == CharacterGuardianshipStatus.Active);
                CampaignCommand closure = CampaignCommand.Create(
                    new EntityId($"command:test/death-guardianship-race-{closureFirst}"),
                    CharacterFamilySystem.AuthoritativeActorId,
                    Date,
                    new CharacterFamilyActionCommandPayload(
                        new EndPrimaryGuardianshipAction(
                            active.WardCharacterId,
                            active.GuardianshipId,
                            CharacterGuardianshipEndReason.Revoked)),
                    priority: closureFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"guardianship-race-death-{closureFirst}",
                    priority: closureFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [closure, death]
                    : [death, closure];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                CharacterGuardianshipState ended = simulation.World.CharacterGuardianships
                    .Guardianships.Single(item => item.GuardianshipId == active.GuardianshipId);
                Assert.Equal(CharacterGuardianshipStatus.Ended, ended.Status);
                Assert.Equal(
                    closureFirst == 1
                        ? CharacterGuardianshipEndReason.Revoked
                        : CharacterGuardianshipEndReason.GuardianDied,
                    ended.EndReason);
                Assert.Equal(
                    closureFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F007E_DueBirthAndDeathRaceEitherCreatesTheChildOrRemovesThePregnancy()
    {
        for (int birthFirst = 0; birthFirst < 2; birthFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateLifecycleSimulation(
                    targetIsGestationalParent: true,
                    pregnancyDue: true);
                CharacterPregnancyState pregnancy = Assert.Single(
                    simulation.World.CharacterPregnancies.ActivePregnancies);
                CampaignCommand birth = CampaignCommand.Create(
                    new EntityId($"command:test/death-birth-race-{birthFirst}"),
                    CharacterFamilySystem.AuthoritativeActorId,
                    Date,
                    new CharacterFamilyActionCommandPayload(new ResolvePregnancyBirthAction(
                        pregnancy.PregnancyId,
                        new GeneratedNewbornSpecification(
                            CharacterBirthContractVersions.NewbornSpecification,
                            new EntityId("loc:character/death-race-child"),
                            null,
                            Profile(simulation, Target).FamilyId,
                            Profile(simulation, Target).HouseholdId,
                            []))),
                    priority: birthFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"birth-race-death-{birthFirst}",
                    priority: birthFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [birth, death]
                    : [death, birth];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
                Assert.Equal(
                    birthFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));
                Assert.Equal(
                    birthFirst == 1 ? 5 : 4,
                    simulation.World.Characters.Profiles.Count);
                CharacterDeathChange deathOutcome = Assert.IsType<CharacterDeathResolvedOutcome>(
                    Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(
                        item => item.Payload is CharacterConditionActionResolvedEventPayload).Payload).Outcome).Death;
                Assert.Equal(birthFirst == 1 ? 0 : 1, deathOutcome.RemovedPregnancies.Count);

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F007F_EducationAndGuardianDeathRaceRetainsOnlyCompletedEducation()
    {
        EntityId ability = new("ability:test/death-education");
        for (int educationFirst = 0; educationFirst < 2; educationFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation seeded = CreateLifecycleSimulation(
                    targetIsGestationalParent: true);
                WorldSnapshot source = seeded.World.CaptureSnapshot();
                CharacterWorldSnapshot characters = source.Characters with
                {
                    IdentityDefinitions = source.Characters.IdentityDefinitions.Append(
                        new CharacterIdentityDefinition(
                            CharacterContractVersions.Definition,
                            ability,
                            CharacterIdentityKind.Ability,
                            new EntityId("loc:ability/death_education"))).ToArray(),
                    CharacterDefinitions = source.Characters.CharacterDefinitions.Select(item =>
                        item.Id == Target
                            ? item with { AbilityIds = [ability] }
                            : item).ToArray(),
                };
                CampaignSimulation simulation = new(WorldState.Restore(source with
                {
                    Characters = characters.Canonicalize(),
                }));
                CharacterGuardianshipState guardianship = Assert.Single(
                    simulation.World.CharacterGuardianships.Guardianships,
                    item => item.Status == CharacterGuardianshipStatus.Active);
                CampaignCommand education = CampaignCommand.Create(
                    new EntityId($"command:test/death-education-race-{educationFirst}"),
                    CharacterFamilySystem.AuthoritativeActorId,
                    Date,
                    new CharacterFamilyActionCommandPayload(
                        new CompletePrimaryGuardianEducationAction(
                            Ward,
                            guardianship.GuardianshipId,
                            ability)),
                    priority: educationFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"education-race-death-{educationFirst}",
                    priority: educationFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [education, death]
                    : [death, education];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                AuthoritativeCharacterProfile ward = Profile(simulation, Ward);
                Assert.Equal(educationFirst == 1 ? 1 : 0, ward.EducationAttainments.Count);
                Assert.Equal(
                    educationFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));
                CharacterGuardianshipState ended = simulation.World.CharacterGuardianships
                    .Guardianships.Single(item =>
                        item.GuardianshipId == guardianship.GuardianshipId);
                Assert.Equal(CharacterGuardianshipEndReason.GuardianDied, ended.EndReason);
                if (educationFirst == 1)
                {
                    CharacterEducationAttainment attainment = Assert.Single(
                        ward.EducationAttainments);
                    Assert.Equal(guardianship.GuardianshipId, attainment.PrimaryGuardianshipId);
                    Assert.Equal(Target, attainment.TeacherCharacterId);
                }

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F007G_MarriageProposalAndDeathRaceEitherInvalidatesOrCancelsCreation()
    {
        for (int proposalFirst = 0; proposalFirst < 2; proposalFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateLifecycleSimulation(
                    targetIsGestationalParent: true);
                CampaignCommand proposal = CampaignCommand.Create(
                    new EntityId($"command:test/death-marriage-race-{proposalFirst}"),
                    Target,
                    Date,
                    new CharacterMarriageActionCommandPayload(
                        new ProposePoliticalMarriageAction(
                            Other,
                            MarriageProposalKind.LegalUnion,
                            MarriageUnionForm.PrincipalSpouse,
                            null,
                            PracticeId)),
                    priority: proposalFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"marriage-race-death-{proposalFirst}",
                    priority: proposalFirst == 1 ? 1 : 0);
                CampaignCommand[] commands = submissionOrder == 0
                    ? [proposal, death]
                    : [death, proposal];
                foreach (CampaignCommand command in commands)
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                CharacterDeathChange deathOutcome = Assert.IsType<CharacterDeathResolvedOutcome>(
                    Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(
                        item => item.Payload is CharacterConditionActionResolvedEventPayload).Payload).Outcome).Death;
                Assert.Equal(
                    proposalFirst == 1 ? 1 : 0,
                    deathOutcome.MarriageChanges.InvalidatedProposals.Count);
                Assert.Equal(
                    proposalFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));
                Assert.Equal(
                    proposalFirst == 1 ? 2 : 1,
                    simulation.World.CharacterMarriages.Proposals.Count);

                string serialized = Serialize(events);
                string checksum = SimulationChecksum.Compute(
                    simulation.World.CaptureSnapshot()).Value;
                if (submissionOrder == 0)
                {
                    expectedEvents = serialized;
                    expectedChecksum = checksum;
                }
                else
                {
                    Assert.Equal(expectedEvents, serialized);
                    Assert.Equal(expectedChecksum, checksum);
                }
            }
        }
    }

    [Fact]
    public void F012_ThousandCharacterRestrictedDeathWorkloadRecordsRawPerformance()
    {
        const int population = 1_000;
        const int deaths = 200;
        EntityId[] ids = Enumerable.Range(0, population)
            .Select(index => new EntityId($"character:death-performance/{index:D4}"))
            .ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            ids.Select(id =>
            {
                EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    new CampaignDate(170, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(id),
                    null,
                    null,
                    []);
            }).ToArray(),
            [],
            [],
            ids.Select(id => new CharacterState(
                CharacterContractVersions.State,
                id,
                [],
                [],
                CharacterConditionState.Default)).ToArray(),
            [],
            []);
        CampaignSimulation simulation = new(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters));

        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 0; index < deaths; index++)
        {
            EntityId id = ids[index];
            AssertValid(simulation.Submit(CampaignCommand.Create(
                new EntityId($"command:test/death-performance-{index:D4}"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(
                    new ResolveCharacterDeathAction(
                        id,
                        CharacterConditionState.Default)))));
        }

        Assert.Equal(deaths, simulation.ResolveTurn().Count);
        workflow.Stop();
        Stopwatch query = Stopwatch.StartNew();
        Assert.Equal(
            deaths,
            simulation.World.Characters.Profiles.Count(
                item => item.Condition.VitalStatus == CharacterVitalStatus.Dead));
        query.Stop();
        Stopwatch checksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum value = SimulationChecksum.Compute(snapshot);
        checksum.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, CanonicalJson.Options);
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(population, snapshot.Characters.CharacterStates.Count);
        Assert.False(string.IsNullOrWhiteSpace(value.Value));
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"SP-04F0 raw fixture: characters={population}; deaths={deaths}; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={checksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={value.Value}");
    }

    private static CampaignEvent SubmitDeath(CampaignSimulation simulation, string suffix)
    {
        AssertValid(simulation.Submit(DeathCommand(simulation, suffix)));
        return Assert.Single(simulation.ResolveTurn());
    }

    private static CharacterActionResolvedEventPayload SubmitCareer(
        CampaignSimulation simulation,
        EntityId actor,
        ICharacterAction action,
        string suffix)
    {
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/death-{suffix}"),
            actor,
            simulation.World.Calendar.Date,
            new CharacterActionCommandPayload(action));
        AssertValid(simulation.Submit(command));
        return Assert.IsType<CharacterActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
    }

    private static CampaignCommand DeathCommand(
        CampaignSimulation simulation,
        string suffix,
        int priority = 0) => CampaignCommand.Create(
        new EntityId($"command:test/death-{suffix}"),
        CharacterConditionSystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
            Target,
            Profile(simulation, Target).Condition)),
        ResolutionPhase.Commands,
        priority);

    private static CampaignCommand DeathCommand(
        CampaignSimulation simulation,
        EntityId commandId,
        EntityId characterId,
        int priority = 0) => CampaignCommand.Create(
        commandId,
        CharacterConditionSystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
            characterId,
            Profile(simulation, characterId).Condition)),
        ResolutionPhase.Commands,
        priority);

    private static CampaignSimulation CreateLifecycleSimulation(
        bool targetIsGestationalParent,
        bool pregnancyDue = false)
    {
        EntityId proposalId = new("marriage_proposal:test/death-source");
        EntityId unionId = new("marriage_union:test/death-source");
        EntityId proposalCommand = new("command:test/death-proposal");
        EntityId firstParticipant = Target.CompareTo(Spouse) < 0 ? Target : Spouse;
        EntityId secondParticipant = Target.CompareTo(Spouse) < 0 ? Spouse : Target;
        int unionLeadDays = pregnancyDue ? 300 : 20;
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            proposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.PoliticalArrangement,
            Target,
            Spouse,
            null,
            PracticeId,
            Date.AddDays(-unionLeadDays),
            0,
            proposalCommand,
            MarriageProposalStatus.Accepted,
            Date.AddDays(-(unionLeadDays - 1)),
            0,
            new EntityId("command:test/death-proposal-accepted"));
        MarriageUnionState union = new(
            CharacterMarriageContractVersions.State,
            unionId,
            firstParticipant,
            secondParticipant,
            MarriageUnionForm.PrincipalSpouse,
            null,
            MarriageBasis.Political,
            MarriageConsentKind.PoliticalArrangement,
            PracticeId,
            proposalId,
            Date.AddDays(-(unionLeadDays - 1)),
            0,
            MarriageUnionStatus.Active,
            null,
            null,
            null,
            null);
        CharacterMarriageWorldSnapshot marriages = new(
            CharacterMarriageContractVersions.Snapshot,
            [new MarriagePracticeState(
                CharacterMarriageContractVersions.Practice,
                PracticeId,
                18,
                18,
                8,
                64,
                64,
                true,
                true,
                MarriageProhibitedKinship.None)],
            [proposal],
            [],
            [union],
            [],
            [],
            []);
        CampaignDate pregnancyStart = Date.AddDays(
            pregnancyDue ? -CharacterPregnancyLimits.GestationDays : -10);
        EntityId pregnancyCommand = new("command:test/death-pregnancy");
        EntityId pregnancyEvent = CharacterFamilyIds.DeriveActionEventId(
            pregnancyStart,
            pregnancyCommand);
        EntityId gestational = targetIsGestationalParent ? Target : Spouse;
        EntityId otherParent = targetIsGestationalParent ? Spouse : Target;
        CharacterPregnancyState pregnancy = new(
            CharacterPregnancyContractVersions.State,
            CharacterPregnancyIds.DerivePregnancyId(
                pregnancyEvent,
                gestational,
                otherParent,
                unionId),
            gestational,
            otherParent,
            unionId,
            pregnancyStart,
            pregnancyStart.AddDays(CharacterPregnancyLimits.GestationDays),
            0,
            pregnancyCommand,
            pregnancyEvent);
        CampaignDate guardianshipStart = Date.AddDays(-30);
        EntityId guardianshipCommand = new("command:test/death-guardianship");
        EntityId guardianshipEvent = CharacterFamilyIds.DeriveActionEventId(
            guardianshipStart,
            guardianshipCommand);
        CharacterGuardianshipState guardianship = new(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(
                guardianshipEvent,
                Ward,
                Target),
            Ward,
            Target,
            guardianshipStart,
            0,
            guardianshipCommand,
            guardianshipEvent,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
        CampaignDate preservedEndDate = new(181, 1, 1);
        EntityId preservedEndCommand = new("command:test/death-preserved-guardianship-end");
        CharacterGuardianshipState preservedGuardianship = new(
            CharacterGuardianshipContractVersions.State,
            PreservedGuardianshipId,
            Target,
            Other,
            PreservedGuardianshipDate,
            0,
            PreservedGuardianshipCommand,
            PreservedGuardianshipEvent,
            CharacterGuardianshipStatus.Ended,
            preservedEndDate,
            0,
            preservedEndCommand,
            CharacterFamilyIds.DeriveActionEventId(preservedEndDate, preservedEndCommand),
            CharacterGuardianshipEndReason.Revoked);
        return CreateSimpleSimulation(
            richPreservedState: true,
            marriages: marriages,
            guardianships: new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                new[] { guardianship, preservedGuardianship }
                    .OrderBy(item => item.GuardianshipId)
                    .ToArray()),
            pregnancies: new CharacterPregnancyWorldSnapshot(
                CharacterPregnancyContractVersions.Snapshot,
                [pregnancy]),
            resources: new CharacterResourceWorldSnapshot(
                CharacterResourceContractVersions.Snapshot,
                [new CharacterWealthAccountState(
                    CharacterResourceContractVersions.State,
                    CharacterResourceIds.DeriveWealthAccountId(Target),
                    Target,
                    75)],
                [],
                []),
            estates: new CharacterEstateHoldingWorldSnapshot(
                CharacterEstateHoldingContractVersions.Snapshot,
                [new CharacterEstateHoldingState(
                    CharacterEstateHoldingContractVersions.State,
                    new EntityId("estate:test/death-inheritance-input"),
                    Target)]));
    }

    private static CampaignSimulation CreateSimpleSimulation(
        bool householdHead = false,
        bool richPreservedState = false,
        CharacterConditionState? otherCondition = null,
        CareerWorldSnapshot? careers = null,
        CharacterMarriageWorldSnapshot? marriages = null,
        CharacterGuardianshipWorldSnapshot? guardianships = null,
        CharacterPregnancyWorldSnapshot? pregnancies = null,
        CharacterResourceWorldSnapshot? resources = null,
        CharacterEstateHoldingWorldSnapshot? estates = null)
    {
        EntityId[] ids = [Target, Spouse, Ward, Other];
        CharacterIdentityDefinition[] identityDefinitions = richPreservedState
            ? [new CharacterIdentityDefinition(
                CharacterContractVersions.Definition,
                PreservedEducationAbility,
                CharacterIdentityKind.Ability,
                new EntityId("loc:ability/death_preserved"))]
            : [];
        CharacterDefinition[] definitions = ids.Select(id =>
        {
            EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                nameKey,
                id == Ward
                    ? new CampaignDate(190, 1, 1)
                    : id == Other
                    ? new CampaignDate(150, 1, 1)
                    : new CampaignDate(170, 1, 1),
                richPreservedState && id == Other ? [PreservedEducationAbility] : [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(nameKey, null),
                CharacterContentOrigin.LegacyUnknown(id),
                null,
                null,
                []);
        }).OrderBy(item => item.Id).ToArray();
        CampaignDate educationDate = new(180, 1, 1);
        EntityId educationCommand = new("command:test/death-preserved-education");
        EntityId educationEvent = CharacterFamilyIds.DeriveActionEventId(
            educationDate,
            educationCommand);
        CharacterEducationAttainment attainment = new(
            CharacterEducationContractVersions.Attainment,
            CharacterEducationIds.DeriveAttainmentId(
                educationEvent,
                Target,
                Other,
                PreservedEducationAbility),
            Target,
            Other,
            PreservedGuardianshipId,
            PreservedEducationAbility,
            educationDate,
            0,
            educationCommand,
            educationEvent);
        CharacterState[] states = ids.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            richPreservedState && id == Target ? [Other] : [],
            richPreservedState && id == Target
                ? [new CharacterParentLink(Other, ParentChildLinkKind.Biological)]
                : [],
            id == Other ? otherCondition ?? CharacterConditionState.Default : CharacterConditionState.Default,
            richPreservedState && id == Target ? [attainment] : []))
            .OrderBy(item => item.CharacterId)
            .ToArray();
        FamilyDefinition[] familyDefinitions = richPreservedState
            ? [new FamilyDefinition(
                CharacterContractVersions.Definition,
                new EntityId("family:test/death-preserved"),
                new EntityId("loc:family/death_preserved"))]
            : [];
        FamilyState[] familyStates = richPreservedState
            ? [new FamilyState(
                CharacterContractVersions.State,
                familyDefinitions[0].Id,
                new[] { Target, Other }.Order().ToArray())]
            : [];
        HouseholdDefinition[] householdDefinitions = householdHead || richPreservedState
            ? [new HouseholdDefinition(
                CharacterContractVersions.Definition,
                new EntityId("household:test/death"),
                new EntityId("loc:household/death"))]
            : [];
        HouseholdState[] householdStates = householdHead || richPreservedState
            ? [new HouseholdState(
                CharacterContractVersions.State,
                householdDefinitions[0].Id,
                householdHead ? Target : Other,
                householdHead
                    ? [Target]
                    : new[] { Target, Other }.Order().ToArray())]
            : [];
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            identityDefinitions,
            definitions,
            familyDefinitions,
            householdDefinitions,
            states,
            familyStates,
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
            marriages ?? CharacterMarriageWorldSnapshot.Empty,
            guardianships ?? CharacterGuardianshipWorldSnapshot.Empty,
            pregnancies ?? CharacterPregnancyWorldSnapshot.Empty));
    }

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId characterId)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile));
        return profile;
    }

    private static void AssertAliveAndUnchanged(
        CampaignSimulation simulation,
        string expectedSnapshot)
    {
        Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Target).Condition.VitalStatus);
        Assert.Equal(expectedSnapshot, SnapshotJson(simulation));
    }

    private static void AssertValid(CommandValidationResult result) => Assert.True(
        result.IsValid,
        string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));

    private static void AssertInvalid(CommandValidationResult result) => Assert.False(result.IsValid);

    private static string SnapshotJson(CampaignSimulation simulation) =>
        Serialize(simulation.World.CaptureSnapshot());

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
