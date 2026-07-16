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
        Assert.Empty(death.CareerChanges.InvalidatedProposals);
        Assert.Empty(death.CareerChanges.EndedRetinueMemberships);
        Assert.Empty(death.CareerChanges.EndedPatronageBonds);
        Assert.Empty(death.CareerChanges.EndedEmploymentTenures);
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
    public void F207_HouseholdHeadRemainsBlockedWhileCustodianDeathReleasesAndBareRetinueIsPreserved()
    {
        CampaignSimulation householdHead = CreateSimpleSimulation(
            householdHead: true,
            otherCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Target,
            });
        string householdHeadBefore = SnapshotJson(householdHead);
        AssertInvalid(householdHead.Submit(DeathCommand(householdHead, "head")));
        AssertAliveAndUnchanged(householdHead, householdHeadBefore);

        CampaignSimulation custodian = CreateSimpleSimulation(
            otherCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Target,
            });
        CharacterConditionState dependentBefore = Profile(custodian, Other).Condition;
        CharacterDeathChange custodianDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(custodian, "custodian").Payload).Outcome).Death;
        CharacterConditionChange release = Assert.Single(
            custodianDeath.ReleasedCustodyChanges);
        Assert.Equal(Other, release.CharacterId);
        Assert.Equal(dependentBefore, release.PreviousCondition);
        Assert.Equal(CharacterCustodyStatus.Free, release.CurrentCondition.CustodyStatus);
        Assert.Null(release.CurrentCondition.CustodianId);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(custodian, Target).Condition.VitalStatus);
        Assert.Equal(release.CurrentCondition, Profile(custodian, Other).Condition);

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
        CharacterDeathChange retinueDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(retinueLeader, "retinue").Payload).Outcome).Death;
        Assert.Equal(CharacterVitalStatus.Dead, Profile(retinueLeader, Target).Condition.VitalStatus);
        Assert.Empty(retinueDeath.CareerChanges.EndedRetinueMemberships);
        Assert.Single(retinueLeader.World.Careers.Retinues);
        WorldSnapshot retinueAfter = retinueLeader.World.CaptureSnapshot();
        WorldSnapshot retinueBefore = JsonSerializer.Deserialize<WorldSnapshot>(
            retinueLeaderBefore,
            SimulationJson.CreateOptions())!;
        Assert.Equal(Serialize(retinueBefore.Careers), Serialize(retinueAfter.Careers));
    }

    [Fact]
    public void F202_F205_DeathV3ReleasesEveryCustodyStatusCanonicallyAndDefensively()
    {
        IReadOnlyDictionary<EntityId, CharacterConditionState> conditions =
            new Dictionary<EntityId, CharacterConditionState>
            {
                [Spouse] = CharacterConditionState.Default with
                {
                    HealthStatus = CharacterHealthStatus.Injured,
                    CustodyStatus = CharacterCustodyStatus.Detained,
                    CustodianId = Target,
                },
                [Ward] = CharacterConditionState.Default with
                {
                    IsIncapacitated = true,
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Target,
                },
                [Other] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = Target,
                },
            };
        CampaignSimulation simulation = CreateSimpleSimulation(
            conditionOverrides: conditions);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = DeathCommand(simulation, "custody-all-statuses");
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(simulation, "custody-all-statuses").Payload).Outcome).Death;

        Assert.Equal(3, CharacterConditionContractVersions.Death);
        Assert.Equal(CharacterConditionContractVersions.Death, death.ContractVersion);
        Assert.Equal(3, death.ReleasedCustodyChanges.Count);
        Assert.Equal(
            death.ReleasedCustodyChanges.OrderBy(item => item.ChangeId),
            death.ReleasedCustodyChanges);
        Assert.Equal(
            death.ReleasedCustodyChanges.Count,
            death.ReleasedCustodyChanges.Select(item => item.ChangeId).Distinct().Count());
        Assert.Equal(
            new[] { Spouse, Ward, Other }.Order(),
            death.ReleasedCustodyChanges.Select(item => item.CharacterId).Order());
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(Date, command.CommandId);
        foreach (CharacterConditionChange release in death.ReleasedCustodyChanges)
        {
            Assert.Equal(CharacterConditionContractVersions.Change, release.ContractVersion);
            Assert.Equal(
                CharacterConditionIds.DeriveChangeId(eventId, release.CharacterId),
                release.ChangeId);
            Assert.Equal(Date, release.ResolutionDate);
            Assert.Equal(0, release.ResolutionTurnIndex);
            Assert.Equal(command.CommandId, release.SourceCommandId);
            Assert.Equal(Target, release.PreviousCondition.CustodianId);
            Assert.Equal(
                release.PreviousCondition with
                {
                    CustodyStatus = CharacterCustodyStatus.Free,
                    CustodianId = null,
                },
                release.CurrentCondition);
            Assert.Equal(release.CurrentCondition, Profile(simulation, release.CharacterId).Condition);
            CharacterState previous = before.Characters.CharacterStates.Single(
                item => item.CharacterId == release.CharacterId);
            CharacterState current = simulation.World.CaptureSnapshot().Characters.CharacterStates.Single(
                item => item.CharacterId == release.CharacterId);
            Assert.Equal(
                Serialize(previous with { Condition = release.CurrentCondition }),
                Serialize(current));
        }

        IList<CharacterConditionChange> readOnly =
            Assert.IsAssignableFrom<IList<CharacterConditionChange>>(
                death.ReleasedCustodyChanges);
        Assert.Throws<NotSupportedException>(() => readOnly[0] = readOnly[0]);
        CharacterDeathChange roundTrip = JsonSerializer.Deserialize<CharacterDeathChange>(
            Serialize(death),
            SimulationJson.CreateOptions())!;
        Assert.Equal(Serialize(death), Serialize(roundTrip));
    }

    [Fact]
    public void F206_TargetCustodyUsesOnlyThePrimaryDeathChange()
    {
        CharacterConditionState targetBefore = CharacterConditionState.Default with
        {
            HealthStatus = CharacterHealthStatus.Injured,
            CustodyStatus = CharacterCustodyStatus.Captive,
            CustodianId = Other,
        };
        CampaignSimulation simulation = CreateSimpleSimulation(
            conditionOverrides: new Dictionary<EntityId, CharacterConditionState>
            {
                [Target] = targetBefore,
            });

        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(simulation, "target-in-custody").Payload).Outcome).Death;

        Assert.Empty(death.ReleasedCustodyChanges);
        Assert.Equal(targetBefore, death.ConditionChange.PreviousCondition);
        Assert.Equal(CharacterVitalStatus.Dead, death.ConditionChange.CurrentCondition.VitalStatus);
        Assert.Equal(CharacterCustodyStatus.Free, death.ConditionChange.CurrentCondition.CustodyStatus);
        Assert.Null(death.ConditionChange.CurrentCondition.CustodianId);
    }

    [Fact]
    public void F207_UnrelatedCustodyRemainsUnchanged()
    {
        CharacterConditionState unrelated = CharacterConditionState.Default with
        {
            CustodyStatus = CharacterCustodyStatus.Hostage,
            CustodianId = Spouse,
        };
        CampaignSimulation simulation = CreateSimpleSimulation(
            otherCondition: unrelated);

        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(simulation, "unrelated-custody").Payload).Outcome).Death;

        Assert.Empty(death.ReleasedCustodyChanges);
        Assert.Equal(unrelated, Profile(simulation, Other).Condition);
    }

    [Fact]
    public void F209_LaterCandidateFailureRollsBackPreparedCustodyReleases()
    {
        CampaignSimulation simulation = CreateSimpleSimulation(
            otherCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Target,
            },
            careers: CreateOverflowCareerDeathSnapshot());
        string before = SnapshotJson(simulation);

        AssertInvalid(simulation.Submit(DeathCommand(simulation, "custody-career-overflow")));

        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Target).Condition.VitalStatus);
        Assert.Equal(CharacterCustodyStatus.Captive, Profile(simulation, Other).Condition.CustodyStatus);
        Assert.Equal(Target, Profile(simulation, Other).Condition.CustodianId);
    }

    [Fact]
    public void F210_CustodyReleaseAffectedIdsAndEvidenceTamperingRollBack()
    {
        CampaignSimulation simulation = CreateSimpleSimulation(
            conditionOverrides: new Dictionary<EntityId, CharacterConditionState>
            {
                [Spouse] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Detained,
                    CustodianId = Target,
                },
                [Other] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Target,
                },
            });
        CampaignCommand command = DeathCommand(simulation, "custody-tamper");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(Date, command.CommandId);
        CharacterConditionAggregatePlan plan = simulation.World.PrepareCharacterConditionAction(
            command.IssuingActor,
            Assert.IsType<CharacterConditionActionCommandPayload>(command.Payload),
            Date,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CharacterConditionActionResolvedEventPayload resolved = plan.ResolvedPayload;
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            resolved.Outcome).Death;
        Assert.Equal(2, death.ReleasedCustodyChanges.Count);
        CharacterConditionChange release = death.ReleasedCustodyChanges[0];
        EntityId[] affected = WorldState.GetCharacterConditionActionAffectedIds(resolved, eventId);
        Assert.Equal(affected.Order(), affected);
        Assert.Equal(affected.Length, affected.Distinct().Count());
        Assert.Contains(release.ChangeId, affected);
        Assert.Contains(release.CharacterId, affected);
        Assert.Contains(Target, affected);
        CampaignEvent valid = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            Date,
            ResolutionPhase.Commands,
            command.Priority,
            affected,
            resolved);
        string before = SnapshotJson(simulation);

        void AssertRejected(
            IReadOnlyList<CharacterConditionChange> forged,
            bool recomputeAffectedIds = false)
        {
            CharacterConditionActionResolvedEventPayload payload = resolved with
            {
                Outcome = new CharacterDeathResolvedOutcome(death with
                {
                    ReleasedCustodyChanges = forged,
                }),
            };
            CampaignEvent forgedEvent = valid with
            {
                Payload = payload,
            };
            if (recomputeAffectedIds)
            {
                forgedEvent = forgedEvent with
                {
                    AffectedIds = WorldState.GetCharacterConditionActionAffectedIds(
                        payload,
                        eventId),
                };
            }

            Assert.Throws<SimulationValidationException>(() =>
                simulation.World.Apply(forgedEvent));
            Assert.Equal(before, SnapshotJson(simulation));
        }

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            AffectedIds = affected.Where(item => item != release.ChangeId).ToArray(),
        }));
        Assert.Equal(before, SnapshotJson(simulation));
        AssertRejected(null!);
        AssertRejected([]);
        AssertRejected([release, release]);
        AssertRejected(death.ReleasedCustodyChanges.Reverse().ToArray());
        AssertRejected([release with { ContractVersion = 2 }]);
        AssertRejected([release with { ChangeId = new EntityId("character_condition_change:test/forged") }]);
        AssertRejected([release with { SourceCommandId = new EntityId("command:test/forged") }]);
        AssertRejected([release with
        {
            CurrentCondition = release.CurrentCondition with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Target,
            },
        }]);
        CharacterConditionState forgedPrevious = CharacterConditionState.Default with
        {
            CustodyStatus = CharacterCustodyStatus.Captive,
            CustodianId = Target,
        };
        CharacterConditionChange forgedFreeCharacterRelease = release with
        {
            ChangeId = CharacterConditionIds.DeriveChangeId(eventId, Ward),
            CharacterId = Ward,
            PreviousCondition = forgedPrevious,
            CurrentCondition = forgedPrevious with
            {
                CustodyStatus = CharacterCustodyStatus.Free,
                CustodianId = null,
            },
        };
        AssertRejected(
            death.ReleasedCustodyChanges.Append(forgedFreeCharacterRelease)
                .OrderBy(item => item.ChangeId)
                .ToArray(),
            recomputeAffectedIds: true);

        simulation.World.Apply(valid);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
        Assert.Equal(CharacterCustodyStatus.Free, Profile(simulation, Spouse).Condition.CustodyStatus);
        Assert.Equal(CharacterCustodyStatus.Free, Profile(simulation, Other).Condition.CustodyStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F211_CustodyEntryOrReleaseAndDeathRaceAcrossPrioritiesAndSubmissionOrders(
        bool custodyEntry)
    {
        for (int conditionFirst = 0; conditionFirst < 2; conditionFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                string raceKind = custodyEntry ? "entry" : "release";
                CharacterConditionState initial = custodyEntry
                    ? CharacterConditionState.Default
                    : CharacterConditionState.Default with
                    {
                        CustodyStatus = CharacterCustodyStatus.Captive,
                        CustodianId = Target,
                    };
                CampaignSimulation simulation = CreateSimpleSimulation(
                    otherCondition: initial);
                ICharacterConditionAction conditionAction = custodyEntry
                    ? new EnterCharacterCustodyAction(
                        Other,
                        initial,
                        CharacterCustodyStatus.Captive,
                        Target)
                    : new ReleaseCharacterCustodyAction(Other, initial);
                CampaignCommand condition = CampaignCommand.Create(
                    new EntityId($"command:test/f2-condition-race-{raceKind}-{conditionFirst}"),
                    CharacterConditionSystem.AuthoritativeActorId,
                    Date,
                    new CharacterConditionActionCommandPayload(conditionAction),
                    priority: conditionFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"f2-condition-race-death-{raceKind}-{conditionFirst}",
                    priority: conditionFirst == 1 ? 1 : 0);
                foreach (CampaignCommand pending in submissionOrder == 0
                    ? new[] { condition, death }
                    : new[] { death, condition })
                {
                    AssertValid(simulation.Submit(pending));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(conditionFirst == 1 ? 0 : 1, events.Count(
                    item => item.Payload is CommandCancelledEventPayload));
                CharacterDeathChange deathChange = Assert.IsType<CharacterDeathResolvedOutcome>(
                    Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(item =>
                        item.Payload is CharacterConditionActionResolvedEventPayload
                        {
                            Outcome: CharacterDeathResolvedOutcome,
                        }).Payload).Outcome).Death;
                int expectedReleases = conditionFirst == 1
                    ? custodyEntry ? 1 : 0
                    : custodyEntry ? 0 : 1;
                Assert.Equal(expectedReleases, deathChange.ReleasedCustodyChanges.Count);
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                Assert.Equal(CharacterCustodyStatus.Free, Profile(simulation, Other).Condition.CustodyStatus);
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
    public void F211_DependentConditionMutationAndDeathPreserveOrCancelExactState()
    {
        for (int mutationFirst = 0; mutationFirst < 2; mutationFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CharacterConditionState initial = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Target,
                };
                CampaignSimulation simulation = CreateSimpleSimulation(otherCondition: initial);
                CampaignCommand mutation = CampaignCommand.Create(
                    new EntityId($"command:test/f2-dependent-mutation-{mutationFirst}"),
                    CharacterConditionSystem.AuthoritativeActorId,
                    Date,
                    new CharacterConditionActionCommandPayload(new IncapacitateCharacterAction(
                        Other,
                        initial)),
                    priority: mutationFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"f2-dependent-mutation-death-{mutationFirst}",
                    priority: mutationFirst == 1 ? 1 : 0);
                foreach (CampaignCommand pending in submissionOrder == 0
                    ? new[] { mutation, death }
                    : new[] { death, mutation })
                {
                    AssertValid(simulation.Submit(pending));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(mutationFirst == 1 ? 0 : 1, events.Count(
                    item => item.Payload is CommandCancelledEventPayload));
                Assert.Equal(
                    mutationFirst == 1,
                    Profile(simulation, Other).Condition.IsIncapacitated);
                Assert.Equal(CharacterCustodyStatus.Free, Profile(simulation, Other).Condition.CustodyStatus);
                CharacterConditionChange release = Assert.Single(
                    Assert.IsType<CharacterDeathResolvedOutcome>(
                        Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(item =>
                            item.Payload is CharacterConditionActionResolvedEventPayload
                            {
                                Outcome: CharacterDeathResolvedOutcome,
                            }).Payload).Outcome)
                    .Death.ReleasedCustodyChanges);
                Assert.Equal(mutationFirst == 1, release.PreviousCondition.IsIncapacitated);
                Assert.Equal(mutationFirst == 1, release.CurrentCondition.IsIncapacitated);
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
    public void F212_CustodianAndDependentDeathsFollowExpectedStateDeterministically()
    {
        for (int custodianFirst = 0; custodianFirst < 2; custodianFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateSimpleSimulation(
                    otherCondition: CharacterConditionState.Default with
                    {
                        CustodyStatus = CharacterCustodyStatus.Hostage,
                        CustodianId = Target,
                    });
                CampaignCommand custodianDeath = DeathCommand(
                    simulation,
                    new EntityId($"command:test/f2-simultaneous-custodian-{custodianFirst}"),
                    Target,
                    priority: custodianFirst == 1 ? 0 : 1);
                CampaignCommand dependentDeath = DeathCommand(
                    simulation,
                    new EntityId($"command:test/f2-simultaneous-dependent-{custodianFirst}"),
                    Other,
                    priority: custodianFirst == 1 ? 1 : 0);
                foreach (CampaignCommand pending in submissionOrder == 0
                    ? new[] { custodianDeath, dependentDeath }
                    : new[] { dependentDeath, custodianDeath })
                {
                    AssertValid(simulation.Submit(pending));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(custodianFirst == 1 ? 1 : 0, events.Count(
                    item => item.Payload is CommandCancelledEventPayload));
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                Assert.Equal(
                    custodianFirst == 1
                        ? CharacterVitalStatus.Alive
                        : CharacterVitalStatus.Dead,
                    Profile(simulation, Other).Condition.VitalStatus);
                CharacterDeathChange targetDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
                    Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(item =>
                        item.Payload is CharacterConditionActionResolvedEventPayload payload
                        && payload.Outcome is CharacterDeathResolvedOutcome outcome
                        && outcome.Death.ConditionChange.CharacterId == Target).Payload).Outcome).Death;
                Assert.Equal(custodianFirst == 1 ? 1 : 0, targetDeath.ReleasedCustodyChanges.Count);
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void F213_CustodyRichPendingDeathReplaysOnEveryLaterTurnDay(int dayOffset)
    {
        CampaignSimulation seeded = CreateSimpleSimulation(
            otherCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Detained,
                CustodianId = Target,
            });
        CampaignSimulation original = new(WorldState.Restore(
            seeded.World.CaptureSnapshot() with
            {
                Calendar = new CampaignCalendar(Date, 1),
            }));
        CampaignDate resolutionDate = Date.AddDays(dayOffset);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/f2-later-day-{dayOffset}"),
            CharacterConditionSystem.AuthoritativeActorId,
            resolutionDate,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                Target,
                Profile(original, Target).Condition)));
        AssertValid(original.Submit(command));
        CampaignSimulation replay = new(WorldState.Restore(original.World.CaptureSnapshot()));

        CampaignEvent first = Assert.Single(original.ResolveTurn());
        CampaignEvent second = Assert.Single(replay.ResolveTurn());

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        CharacterConditionChange release = Assert.Single(
            Assert.IsType<CharacterDeathResolvedOutcome>(
                Assert.IsType<CharacterConditionActionResolvedEventPayload>(first.Payload).Outcome)
            .Death.ReleasedCustodyChanges);
        Assert.Equal(resolutionDate, release.ResolutionDate);
        Assert.Equal(command.CommandId, release.SourceCommandId);
        Assert.Equal(CharacterCustodyStatus.Free, Profile(original, Other).Condition.CustodyStatus);
    }

    [Fact]
    public void F103_ActiveProposalInvalidatesWhileCompletedHistoryAndRecommendationsRemain()
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
        CampaignSimulation active = CreateSimpleSimulation(careers: new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            [activeProposal],
            [],
            [],
            [],
            [],
            [],
            []));
        CharacterDeathChange activeDeath = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(active, "career-active").Payload).Outcome).Death;
        CareerProposalState invalidated = Assert.Single(
            activeDeath.CareerChanges.InvalidatedProposals);
        Assert.Equal(activeProposal.ProposalId, invalidated.ProposalId);
        Assert.Equal(CareerProposalStatus.Invalidated, invalidated.Status);
        Assert.Equal(
            invalidated,
            Assert.Single(active.World.Careers.Proposals));

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
    [InlineData(CareerProposalKind.RetinueInvitation, true)]
    [InlineData(CareerProposalKind.RetinueInvitation, false)]
    [InlineData(CareerProposalKind.PatronageOffer, true)]
    [InlineData(CareerProposalKind.PatronageOffer, false)]
    [InlineData(CareerProposalKind.EmploymentOffer, true)]
    [InlineData(CareerProposalKind.EmploymentOffer, false)]
    public void F103_AllProposalKindsInvalidateForEitherDirectCharacterRole(
        CareerProposalKind kind,
        bool targetIsProposer)
    {
        CampaignSimulation simulation = CreateSimpleSimulation();
        EntityId proposer = targetIsProposer ? Target : Other;
        EntityId recipient = targetIsProposer ? Other : Target;
        ICharacterAction action = kind switch
        {
            CareerProposalKind.RetinueInvitation => new RetinueInviteAction(recipient),
            CareerProposalKind.PatronageOffer => new PatronageOfferAction(recipient),
            CareerProposalKind.EmploymentOffer => new EmploymentOfferAction(
                recipient,
                new ServicePrincipalReference(ServicePrincipalKind.Character, proposer),
                new EntityId("role:test/death-proposal-matrix")),
            _ => throw new InvalidOperationException(),
        };
        CareerProposalState active = Assert.IsType<CareerProposalCreatedOutcome>(
            SubmitCareer(
                simulation,
                proposer,
                action,
                $"proposal-matrix-{(int)kind}-{targetIsProposer.ToString().ToLowerInvariant()}")
            .Outcome).Proposal;

        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                SubmitDeath(
                    simulation,
                    $"proposal-matrix-death-{(int)kind}-{targetIsProposer.ToString().ToLowerInvariant()}")
                .Payload).Outcome).Death;

        CareerProposalState invalidated = Assert.Single(
            death.CareerChanges.InvalidatedProposals);
        Assert.Equal(active.ProposalId, invalidated.ProposalId);
        Assert.Equal(CareerProposalStatus.Invalidated, invalidated.Status);
        Assert.Equal(death.ResolutionDate, invalidated.ResolutionDate);
        Assert.Equal(death.ResolutionTurnIndex, invalidated.ResolutionTurnIndex);
        Assert.Equal(death.SourceCommandId, invalidated.ResolutionCommandId);
    }

    [Theory]
    [InlineData(CareerProposalKind.RetinueInvitation)]
    [InlineData(CareerProposalKind.PatronageOffer)]
    [InlineData(CareerProposalKind.EmploymentOffer)]
    public void F104_EachActiveCareerServiceRoleEndsWithTheTargetRoleDeathReason(
        CareerProposalKind kind)
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
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(SubmitDeath(
                simulation,
                $"service-death-{kind.ToString().ToLowerInvariant()}").Payload).Outcome).Death;
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
        switch (kind)
        {
            case CareerProposalKind.RetinueInvitation:
                Assert.Equal(
                    CareerServiceEndReason.MemberDied,
                    Assert.Single(death.CareerChanges.EndedRetinueMemberships).EndReason);
                Assert.DoesNotContain(simulation.World.Careers.RetinueMemberships, item => item.IsActive);
                break;
            case CareerProposalKind.PatronageOffer:
                Assert.Equal(
                    CareerServiceEndReason.BeneficiaryDied,
                    Assert.Single(death.CareerChanges.EndedPatronageBonds).EndReason);
                Assert.DoesNotContain(simulation.World.Careers.PatronageBonds, item => item.IsActive);
                break;
            case CareerProposalKind.EmploymentOffer:
                Assert.Equal(
                    CareerServiceEndReason.EmployeeDied,
                    Assert.Single(death.CareerChanges.EndedEmploymentTenures).EndReason);
                Assert.DoesNotContain(simulation.World.Careers.EmploymentTenures, item => item.IsActive);
                break;
        }
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
    public void F110_CareerDeathEvidenceAffectedIdsAndTamperValidationAreExactAndAtomic()
    {
        CampaignSimulation simulation = CreateSimpleSimulation(
            careers: CreateCareerRichDeathSnapshot());
        CampaignCommand command = DeathCommand(simulation, "career-tamper");
        CampaignDate date = simulation.World.Calendar.Date;
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(date, command.CommandId);
        CharacterConditionAggregatePlan plan = simulation.World.PrepareCharacterConditionAction(
            command.IssuingActor,
            Assert.IsType<CharacterConditionActionCommandPayload>(command.Payload),
            date,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CharacterConditionActionResolvedEventPayload resolved = plan.ResolvedPayload;
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            resolved.Outcome).Death;
        Assert.Equal(2, death.CareerChanges.InvalidatedProposals.Count);
        Assert.Equal(2, death.CareerChanges.EndedRetinueMemberships.Count);
        Assert.Equal(2, death.CareerChanges.EndedPatronageBonds.Count);
        Assert.Equal(2, death.CareerChanges.EndedEmploymentTenures.Count);
        EntityId[] affected = WorldState.GetCharacterConditionActionAffectedIds(resolved, eventId);
        Assert.Equal(affected.Order(), affected);
        Assert.Equal(affected.Length, affected.Distinct().Count());
        Assert.DoesNotContain(
            new EntityId("career_proposal:test/death-service-membership-0"),
            affected);
        Assert.DoesNotContain(
            new EntityId("career_proposal:test/death-service-bond-0"),
            affected);
        Assert.DoesNotContain(
            new EntityId("career_proposal:test/death-service-tenure-0"),
            affected);
        CampaignEvent valid = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            date,
            ResolutionPhase.Commands,
            command.Priority,
            affected,
            resolved);
        string before = SnapshotJson(simulation);

        void AssertRejected(CharacterConditionActionResolvedEventPayload forged)
        {
            Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
            {
                Payload = forged,
            }));
            Assert.Equal(before, SnapshotJson(simulation));
        }

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            AffectedIds = affected.Skip(1).ToArray(),
        }));
        Assert.Equal(before, SnapshotJson(simulation));
        CharacterCareerDeathChangeSet changes = death.CareerChanges;
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with { ContractVersion = 2 },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with { InvalidatedProposals = null! },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with
                {
                    InvalidatedProposals = changes.InvalidatedProposals.Reverse().ToArray(),
                },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with
                {
                    InvalidatedProposals = changes.InvalidatedProposals
                        .Append(changes.InvalidatedProposals[0])
                        .OrderBy(item => item.ProposalId)
                        .ToArray(),
                },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with
                {
                    InvalidatedProposals = changes.InvalidatedProposals.Select((item, index) =>
                        index == 0 ? item with { Status = CareerProposalStatus.Active } : item)
                        .ToArray(),
                },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with
                {
                    EndedRetinueMemberships = changes.EndedRetinueMemberships.Select((item, index) =>
                        index == 0 ? item with { EndReason = CareerServiceEndReason.LeaderDied } : item)
                        .ToArray(),
                },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with
                {
                    EndedPatronageBonds = changes.EndedPatronageBonds.Select((item, index) =>
                        index == 0 ? item with { EndDate = date.AddDays(1) } : item)
                        .ToArray(),
                },
            }),
        });
        AssertRejected(resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(death with
            {
                CareerChanges = changes with
                {
                    EndedEmploymentTenures = changes.EndedEmploymentTenures.Select((item, index) =>
                        index == 0
                            ? item with
                            {
                                EndCommandId = new EntityId("command:test/forged-career-death"),
                            }
                            : item).ToArray(),
                },
            }),
        });

        simulation.World.Apply(valid);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
        Assert.DoesNotContain(simulation.World.Careers.Proposals, item =>
            item.Status == CareerProposalStatus.Active);
        Assert.DoesNotContain(simulation.World.Careers.RetinueMemberships, item => item.IsActive);
        string after = SnapshotJson(simulation);
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid));
        Assert.Equal(after, SnapshotJson(simulation));
    }

    [Fact]
    public void F109_SaturatedCareerHistoryRejectsDeathWithoutPartialSubsystemMutation()
    {
        CampaignSimulation simulation = CreateSimpleSimulation(
            careers: CreateOverflowCareerDeathSnapshot());
        string before = SnapshotJson(simulation);

        AssertInvalid(simulation.Submit(DeathCommand(simulation, "career-overflow")));

        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Target).Condition.VitalStatus);
        Assert.Single(simulation.World.Careers.RetinueMemberships, item => item.IsActive);
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
            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void F113_CareerRichPendingDeathReplaysOnEveryLaterTurnDay(int dayOffset)
    {
        CampaignSimulation source = CreateSimpleSimulation(
            careers: CreateCareerRichDeathSnapshot());
        WorldSnapshot turnOne = source.World.CaptureSnapshot() with
        {
            Calendar = new CampaignCalendar(Date, 1),
        };
        CampaignSimulation original = new(WorldState.Restore(turnOne));
        CampaignDate resolutionDate = Date.AddDays(dayOffset);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/death-career-later-day-{dayOffset}"),
            CharacterConditionSystem.AuthoritativeActorId,
            resolutionDate,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                Target,
                Profile(original, Target).Condition)));
        AssertValid(original.Submit(command));
        CampaignSimulation replay = new(WorldState.Restore(original.World.CaptureSnapshot()));

        CampaignEvent first = Assert.Single(original.ResolveTurn());
        CampaignEvent second = Assert.Single(replay.ResolveTurn());

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        CharacterDeathChange death = Assert.IsType<CharacterDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(first.Payload).Outcome).Death;
        Assert.Equal(resolutionDate, death.ResolutionDate);
        Assert.All(
            death.CareerChanges.InvalidatedProposals,
            item => Assert.Equal(resolutionDate, item.ResolutionDate));
        Assert.All(
            death.CareerChanges.EndedRetinueMemberships,
            item => Assert.Equal(resolutionDate, item.EndDate));
        Assert.All(
            death.CareerChanges.EndedPatronageBonds,
            item => Assert.Equal(resolutionDate, item.EndDate));
        Assert.All(
            death.CareerChanges.EndedEmploymentTenures,
            item => Assert.Equal(resolutionDate, item.EndDate));
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
    public void F111_CareerCreationAndDeathRaceReplansInBothPriorityOrders()
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
                Assert.Equal(
                    careerFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                Assert.Equal(careerFirst == 1 ? 1 : 0, simulation.World.Careers.Proposals.Count);
                if (careerFirst == 1)
                {
                    CareerProposalState proposal = Assert.Single(simulation.World.Careers.Proposals);
                    Assert.Equal(CareerProposalStatus.Invalidated, proposal.Status);
                    CharacterDeathChange change = Assert.IsType<CharacterDeathResolvedOutcome>(
                        Assert.IsType<CharacterConditionActionResolvedEventPayload>(events.Single(item =>
                            item.Payload is CharacterConditionActionResolvedEventPayload).Payload).Outcome).Death;
                    Assert.Equal(proposal, Assert.Single(change.CareerChanges.InvalidatedProposals));
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

    [Theory]
    [InlineData(CareerProposalKind.RetinueInvitation)]
    [InlineData(CareerProposalKind.PatronageOffer)]
    [InlineData(CareerProposalKind.EmploymentOffer)]
    public void F111_ServiceEndingAndDeathRaceReplansBothPrioritiesAndSubmissionOrders(
        CareerProposalKind kind)
    {
        for (int careerFirst = 0; careerFirst < 2; careerFirst++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateSimpleSimulation();
                ICharacterAction offer = kind switch
                {
                    CareerProposalKind.RetinueInvitation => new RetinueInviteAction(Target),
                    CareerProposalKind.PatronageOffer => new PatronageOfferAction(Target),
                    CareerProposalKind.EmploymentOffer => new EmploymentOfferAction(
                        Target,
                        new ServicePrincipalReference(ServicePrincipalKind.Character, Other),
                        new EntityId("role:test/death-service-race")),
                    _ => throw new InvalidOperationException(),
                };
                CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(
                    SubmitCareer(
                        simulation,
                        Other,
                        offer,
                        $"service-race-offer-{(int)kind}-{careerFirst}").Outcome).Proposal;
                ICharacterAction accept = kind switch
                {
                    CareerProposalKind.RetinueInvitation =>
                        new RespondToRetinueInvitationAction(
                            proposal.ProposalId,
                            CareerProposalResponse.Accept),
                    CareerProposalKind.PatronageOffer =>
                        new RespondToPatronageOfferAction(
                            proposal.ProposalId,
                            CareerProposalResponse.Accept),
                    CareerProposalKind.EmploymentOffer =>
                        new RespondToEmploymentOfferAction(
                            proposal.ProposalId,
                            CareerProposalResponse.Accept),
                    _ => throw new InvalidOperationException(),
                };
                _ = SubmitCareer(
                    simulation,
                    Target,
                    accept,
                    $"service-race-accept-{(int)kind}-{careerFirst}");
                ICharacterAction ending = kind switch
                {
                    CareerProposalKind.RetinueInvitation => new LeaveRetinueAction(
                        Assert.Single(simulation.World.Careers.RetinueMemberships).MembershipId),
                    CareerProposalKind.PatronageOffer => new EndPatronageAction(
                        Assert.Single(simulation.World.Careers.PatronageBonds).BondId),
                    CareerProposalKind.EmploymentOffer => new EndEmploymentAction(
                        Assert.Single(simulation.World.Careers.EmploymentTenures).TenureId),
                    _ => throw new InvalidOperationException(),
                };
                CampaignCommand career = CampaignCommand.Create(
                    new EntityId($"command:test/death-service-race-end-{(int)kind}-{careerFirst}"),
                    Target,
                    simulation.World.Calendar.Date,
                    new CharacterActionCommandPayload(ending),
                    priority: careerFirst == 1 ? 0 : 1);
                CampaignCommand death = DeathCommand(
                    simulation,
                    $"service-race-death-{(int)kind}-{careerFirst}",
                    priority: careerFirst == 1 ? 1 : 0);
                foreach (CampaignCommand command in submissionOrder == 0
                    ? new[] { career, death }
                    : new[] { death, career })
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(
                    careerFirst == 1 ? 0 : 1,
                    events.Count(item => item.Payload is CommandCancelledEventPayload));
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                CareerServiceEndReason expectedReason = (kind, careerFirst) switch
                {
                    (CareerProposalKind.RetinueInvitation, 1) => CareerServiceEndReason.MemberLeft,
                    (CareerProposalKind.RetinueInvitation, _) => CareerServiceEndReason.MemberDied,
                    (CareerProposalKind.PatronageOffer, 1) => CareerServiceEndReason.BeneficiaryEnded,
                    (CareerProposalKind.PatronageOffer, _) => CareerServiceEndReason.BeneficiaryDied,
                    (CareerProposalKind.EmploymentOffer, 1) => CareerServiceEndReason.EmployeeLeft,
                    _ => CareerServiceEndReason.EmployeeDied,
                };
                CareerServiceEndReason? actualReason = kind switch
                {
                    CareerProposalKind.RetinueInvitation =>
                        Assert.Single(simulation.World.Careers.RetinueMemberships).EndReason,
                    CareerProposalKind.PatronageOffer =>
                        Assert.Single(simulation.World.Careers.PatronageBonds).EndReason,
                    CareerProposalKind.EmploymentOffer =>
                        Assert.Single(simulation.World.Careers.EmploymentTenures).EndReason,
                    _ => null,
                };
                Assert.Equal(expectedReason, actualReason);

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
    public void F112_SimultaneousDeathsSharingCareerRecordsEmitTerminalEvidenceOnce()
    {
        for (int assignment = 0; assignment < 2; assignment++)
        {
            string? expectedEvents = null;
            string? expectedChecksum = null;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateSimpleSimulation(
                    careers: CreateSharedCareerDeathSnapshot());
                string retinueBefore = Serialize(simulation.World.Careers.Retinues);
                EntityId firstCharacter = assignment == 0 ? Target : Spouse;
                EntityId secondCharacter = assignment == 0 ? Spouse : Target;
                CampaignCommand first = DeathCommand(
                    simulation,
                    new EntityId("command:test/death-shared-a"),
                    firstCharacter);
                CampaignCommand second = DeathCommand(
                    simulation,
                    new EntityId("command:test/death-shared-b"),
                    secondCharacter);
                foreach (CampaignCommand command in submissionOrder == 0
                    ? new[] { first, second }
                    : new[] { second, first })
                {
                    AssertValid(simulation.Submit(command));
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
                Assert.Equal(2, events.Count);
                CharacterDeathChange[] deaths = events.Select(item =>
                    Assert.IsType<CharacterDeathResolvedOutcome>(
                        Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                            item.Payload).Outcome).Death).ToArray();
                Assert.Equal(1, deaths.Sum(item => item.CareerChanges.InvalidatedProposals.Count));
                Assert.Equal(1, deaths.Sum(item => item.CareerChanges.EndedRetinueMemberships.Count));
                Assert.Equal(1, deaths.Sum(item => item.CareerChanges.EndedPatronageBonds.Count));
                Assert.Equal(1, deaths.Sum(item => item.CareerChanges.EndedEmploymentTenures.Count));
                Assert.NotEmpty(deaths[0].CareerChanges.InvalidatedProposals);
                Assert.Empty(deaths[1].CareerChanges.InvalidatedProposals);
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Target).Condition.VitalStatus);
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Spouse).Condition.VitalStatus);
                Assert.Equal(retinueBefore, Serialize(simulation.World.Careers.Retinues));
                Assert.Single(simulation.World.Careers.RetinueMemberships);
                Assert.DoesNotContain(simulation.World.Careers.RetinueMemberships, item => item.IsActive);

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

    [Fact]
    public void F117_ThousandCharacterCareerRichDeathWorkloadRecordsRawPerformance()
    {
        const int population = 1_000;
        const int deaths = 200;
        EntityId[] ids = Enumerable.Range(0, population)
            .Select(index => new EntityId($"character:career-death-performance/{index:D4}"))
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
        List<CareerProposalState> proposals = [];
        List<RetinueState> retinues = [];
        List<RetinueMembershipState> memberships = [];
        List<PatronageBondState> bonds = [];
        List<EmploymentTenure> tenures = [];
        for (int index = 0; index < deaths; index++)
        {
            EntityId target = ids[index];
            EntityId proposer = ids[deaths + (index * 4)];
            EntityId leader = ids[deaths + (index * 4) + 1];
            EntityId patron = ids[deaths + (index * 4) + 2];
            EntityId employer = ids[deaths + (index * 4) + 3];
            EntityId proposalCommand = new($"command:test/career-death-performance-proposal-{index:D4}");
            proposals.Add(new CareerProposalState(
                CareerContractVersions.State,
                CareerIds.DeriveProposalId(
                    CareerProposalKind.PatronageOffer,
                    Date.AddDays(-5),
                    proposalCommand),
                CareerProposalKind.PatronageOffer,
                proposer,
                target,
                new ServicePrincipalReference(ServicePrincipalKind.Character, proposer),
                null,
                Date.AddDays(-5),
                0,
                proposalCommand,
                CareerProposalStatus.Active,
                null,
                null,
                null));
            RetinueState retinue = new(
                CareerContractVersions.State,
                CareerIds.DeriveRetinueId(leader),
                leader);
            retinues.Add(retinue);
            EntityId membershipSource = new($"career_proposal:test/career-death-membership-{index:D4}");
            memberships.Add(new RetinueMembershipState(
                CareerContractVersions.State,
                CareerIds.DeriveRetinueMembershipId(membershipSource),
                retinue.RetinueId,
                leader,
                target,
                membershipSource,
                Date.AddDays(-4),
                0,
                null,
                null,
                null,
                null));
            EntityId bondSource = new($"career_proposal:test/career-death-bond-{index:D4}");
            bonds.Add(new PatronageBondState(
                CareerContractVersions.State,
                CareerIds.DerivePatronageBondId(bondSource),
                patron,
                target,
                bondSource,
                Date.AddDays(-3),
                0,
                null,
                null,
                null,
                null));
            EntityId tenureSource = new($"career_proposal:test/career-death-tenure-{index:D4}");
            tenures.Add(new EmploymentTenure(
                CareerContractVersions.State,
                CareerIds.DeriveEmploymentTenureId(tenureSource),
                target,
                new ServicePrincipalReference(ServicePrincipalKind.Character, employer),
                new EntityId($"role:test/career-death-{index:D4}"),
                tenureSource,
                Date.AddDays(-2),
                0,
                null,
                null,
                null,
                null));
        }

        CareerWorldSnapshot careers = new(
            CareerContractVersions.Snapshot,
            proposals.OrderBy(item => item.ProposalId).ToArray(),
            retinues.OrderBy(item => item.RetinueId).ToArray(),
            memberships.OrderBy(item => item.MembershipId).ToArray(),
            bonds.OrderBy(item => item.BondId).ToArray(),
            [],
            tenures.OrderBy(item => item.TenureId).ToArray(),
            []);
        CampaignSimulation simulation = new(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            careers,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty));
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 0; index < deaths; index++)
        {
            AssertValid(simulation.Submit(CampaignCommand.Create(
                new EntityId($"command:test/career-death-performance-{index:D4}"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(
                    new ResolveCharacterDeathAction(
                        ids[index],
                        CharacterConditionState.Default)))));
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Assert.Equal(deaths, events.Count);
        Assert.Equal(
            deaths,
            events.Sum(item => Assert.IsType<CharacterDeathResolvedOutcome>(
                Assert.IsType<CharacterConditionActionResolvedEventPayload>(item.Payload).Outcome)
                .Death.CareerChanges.InvalidatedProposals.Count));
        Stopwatch query = Stopwatch.StartNew();
        Assert.Equal(
            deaths,
            simulation.World.Characters.Profiles.Count(
                item => item.Condition.VitalStatus == CharacterVitalStatus.Dead));
        Assert.Equal(deaths, simulation.World.Careers.Retinues.Count);
        Assert.DoesNotContain(simulation.World.Careers.RetinueMemberships, item => item.IsActive);
        Assert.DoesNotContain(simulation.World.Careers.PatronageBonds, item => item.IsActive);
        Assert.DoesNotContain(simulation.World.Careers.EmploymentTenures, item => item.IsActive);
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

        Assert.False(string.IsNullOrWhiteSpace(value.Value));
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"SP-04F1 raw fixture: characters={population}; deaths={deaths}; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={checksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={value.Value}");
    }

    [Fact]
    public void F217_ThousandCharacterCustodyRichDeathWorkloadRecordsRawPerformance()
    {
        const int population = 1_000;
        const int deaths = 200;
        EntityId[] ids = Enumerable.Range(0, population)
            .Select(index => new EntityId($"character:test/f2-performance-{index:D4}"))
            .ToArray();
        CharacterDefinition[] definitions = ids.Select(id =>
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
        }).ToArray();
        CharacterState[] states = ids.Select((id, index) => new CharacterState(
            CharacterContractVersions.State,
            id,
            [],
            [],
            index switch
            {
                >= deaths and < deaths * 2 => CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Detained,
                    CustodianId = ids[index - deaths],
                },
                >= deaths * 2 and < deaths * 3 => CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = ids[index - deaths * 2],
                },
                >= deaths * 3 and < deaths * 4 => CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = ids[index - deaths * 3],
                },
                _ => CharacterConditionState.Default,
            },
            []))
            .ToArray();
        CampaignSimulation simulation = new(WorldState.Create(
            Date,
            20260716,
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
            CharacterPregnancyWorldSnapshot.Empty));
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 0; index < deaths; index++)
        {
            EntityId target = ids[index];
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:test/f2-performance-{index:D4}"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                    target,
                    Profile(simulation, target).Condition)));
            AssertValid(simulation.Submit(command));
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Assert.Equal(deaths, events.Count);
        Assert.Equal(
            deaths * 3,
            events.Sum(item => Assert.IsType<CharacterDeathResolvedOutcome>(
                Assert.IsType<CharacterConditionActionResolvedEventPayload>(item.Payload).Outcome)
            .Death.ReleasedCustodyChanges.Count));
        Stopwatch query = Stopwatch.StartNew();
        Assert.Equal(
            deaths,
            simulation.World.Characters.Profiles.Count(
                item => item.Condition.VitalStatus == CharacterVitalStatus.Dead));
        Assert.DoesNotContain(
            simulation.World.Characters.Profiles,
            item => item.Condition.CustodyStatus != CharacterCustodyStatus.Free);
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

        Assert.False(string.IsNullOrWhiteSpace(value.Value));
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"SP-04F2 raw fixture: characters={population}; deaths={deaths}; "
            + $"released={deaths * 3}; workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
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
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditionOverrides = null,
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
            conditionOverrides is not null
                && conditionOverrides.TryGetValue(id, out CharacterConditionState? condition)
                    ? condition
                    : id == Other
                        ? otherCondition ?? CharacterConditionState.Default
                        : CharacterConditionState.Default,
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

    private static CareerWorldSnapshot CreateCareerRichDeathSnapshot()
    {
        EntityId[] leaders = [Other, Spouse];
        CareerProposalState[] proposals = leaders.Select((leader, index) =>
        {
            CareerProposalKind kind = index == 0
                ? CareerProposalKind.PatronageOffer
                : CareerProposalKind.EmploymentOffer;
            EntityId commandId = new($"command:test/death-active-proposal-{index}");
            return new CareerProposalState(
                CareerContractVersions.State,
                CareerIds.DeriveProposalId(kind, Date.AddDays(-5), commandId),
                kind,
                leader,
                Target,
                new ServicePrincipalReference(ServicePrincipalKind.Character, leader),
                kind == CareerProposalKind.EmploymentOffer
                    ? new EntityId("role:test/death-active")
                    : null,
                Date.AddDays(-5),
                0,
                commandId,
                CareerProposalStatus.Active,
                null,
                null,
                null);
        }).OrderBy(item => item.ProposalId).ToArray();
        RetinueState[] retinues = leaders.Select(leader => new RetinueState(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueId(leader),
            leader)).OrderBy(item => item.RetinueId).ToArray();
        RetinueMembershipState[] memberships = leaders.Select((leader, index) =>
        {
            EntityId source = new($"career_proposal:test/death-service-membership-{index}");
            return new RetinueMembershipState(
                CareerContractVersions.State,
                CareerIds.DeriveRetinueMembershipId(source),
                CareerIds.DeriveRetinueId(leader),
                leader,
                Target,
                source,
                Date.AddDays(-4),
                0,
                null,
                null,
                null,
                null);
        }).OrderBy(item => item.MembershipId).ToArray();
        PatronageBondState[] bonds = leaders.Select((leader, index) =>
        {
            EntityId source = new($"career_proposal:test/death-service-bond-{index}");
            return new PatronageBondState(
                CareerContractVersions.State,
                CareerIds.DerivePatronageBondId(source),
                leader,
                Target,
                source,
                Date.AddDays(-3),
                0,
                null,
                null,
                null,
                null);
        }).OrderBy(item => item.BondId).ToArray();
        EmploymentTenure[] tenures = leaders.Select((leader, index) =>
        {
            EntityId source = new($"career_proposal:test/death-service-tenure-{index}");
            return new EmploymentTenure(
                CareerContractVersions.State,
                CareerIds.DeriveEmploymentTenureId(source),
                Target,
                new ServicePrincipalReference(ServicePrincipalKind.Character, leader),
                new EntityId($"role:test/death-service-{index}"),
                source,
                Date.AddDays(-2),
                0,
                null,
                null,
                null,
                null);
        }).OrderBy(item => item.TenureId).ToArray();
        return new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            proposals,
            retinues,
            memberships,
            bonds,
            [],
            tenures,
            []);
    }

    private static CareerWorldSnapshot CreateSharedCareerDeathSnapshot()
    {
        EntityId proposalCommand = new("command:test/death-shared-proposal");
        CareerProposalState proposal = new(
            CareerContractVersions.State,
            CareerIds.DeriveProposalId(
                CareerProposalKind.EmploymentOffer,
                Date.AddDays(-5),
                proposalCommand),
            CareerProposalKind.EmploymentOffer,
            Target,
            Spouse,
            new ServicePrincipalReference(ServicePrincipalKind.Character, Target),
            new EntityId("role:test/death-shared-proposal"),
            Date.AddDays(-5),
            0,
            proposalCommand,
            CareerProposalStatus.Active,
            null,
            null,
            null);
        EntityId membershipSource = new("career_proposal:test/death-shared-membership");
        EntityId bondSource = new("career_proposal:test/death-shared-bond");
        EntityId tenureSource = new("career_proposal:test/death-shared-tenure");
        RetinueState retinue = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueId(Target),
            Target);
        return new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            [proposal],
            [retinue],
            [new RetinueMembershipState(
                CareerContractVersions.State,
                CareerIds.DeriveRetinueMembershipId(membershipSource),
                retinue.RetinueId,
                Target,
                Spouse,
                membershipSource,
                Date.AddDays(-4),
                0,
                null,
                null,
                null,
                null)],
            [new PatronageBondState(
                CareerContractVersions.State,
                CareerIds.DerivePatronageBondId(bondSource),
                Target,
                Spouse,
                bondSource,
                Date.AddDays(-3),
                0,
                null,
                null,
                null,
                null)],
            [],
            [new EmploymentTenure(
                CareerContractVersions.State,
                CareerIds.DeriveEmploymentTenureId(tenureSource),
                Spouse,
                new ServicePrincipalReference(ServicePrincipalKind.Character, Target),
                new EntityId("role:test/death-shared-tenure"),
                tenureSource,
                Date.AddDays(-2),
                0,
                null,
                null,
                null,
                null)],
            []);
    }

    private static CareerWorldSnapshot CreateOverflowCareerDeathSnapshot()
    {
        RetinueState retinue = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueId(Other),
            Other);
        RetinueMembershipState[] completed = Enumerable.Range(
                0,
                CareerLimits.CompletedRecordsPerCategoryPerCharacter)
            .Select(index =>
            {
                EntityId source = new($"career_proposal:test/death-overflow-completed-{index:D2}");
                return new RetinueMembershipState(
                    CareerContractVersions.State,
                    CareerIds.DeriveRetinueMembershipId(source),
                    retinue.RetinueId,
                    Other,
                    Target,
                    source,
                    Date.AddDays(-10),
                    0,
                    Date.AddDays(-1),
                    0,
                    new EntityId($"command:test/death-overflow-completed-{index:D2}"),
                    CareerServiceEndReason.MemberLeft);
            })
            .ToArray();
        EntityId activeSource = new("career_proposal:test/death-overflow-active");
        RetinueMembershipState active = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueMembershipId(activeSource),
            retinue.RetinueId,
            Other,
            Target,
            activeSource,
            Date.AddDays(-1),
            0,
            null,
            null,
            null,
            null);
        return new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            [],
            [retinue],
            completed.Append(active).OrderBy(item => item.MembershipId).ToArray(),
            [],
            [],
            [],
            [CareerHistoryAggregate.Empty(Target) with
            {
                FoldedRetinueMembershipCount = long.MaxValue,
                EarliestDate = Date.AddDays(-1),
                LatestDate = Date.AddDays(-1),
            }]);
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
