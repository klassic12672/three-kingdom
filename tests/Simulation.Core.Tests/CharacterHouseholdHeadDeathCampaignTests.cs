using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterHouseholdHeadDeathCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Head = new("character:test/f3-head");
    private static readonly EntityId Replacement = new("character:test/f3-replacement");
    private static readonly EntityId AlternateReplacement =
        new("character:test/f3-alternate-replacement");
    private static readonly EntityId Dependent = new("character:test/f3-dependent");
    private static readonly EntityId Outsider = new("character:test/f3-outsider");
    private static readonly EntityId Household = new("household:test/f3-primary");
    private static readonly EntityId OtherHousehold = new("household:test/f3-other");

    [Fact]
    public void F302_PublicHeadDeathAtomicallyChangesHeadAndPreservesSuccessionInputs()
    {
        CampaignSimulation simulation = CreateSimulation(dependentInCustody: true);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = HeadDeathCommand(simulation, "success");

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterConditionActionResolvedEventPayload payload = Assert.IsType<
            CharacterConditionActionResolvedEventPayload>(campaignEvent.Payload);
        ResolveHouseholdHeadDeathAction action = Assert.IsType<
            ResolveHouseholdHeadDeathAction>(payload.Action);
        HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
            HouseholdHeadDeathResolvedOutcome>(payload.Outcome);
        CharacterDeathChange death = outcome.Death;
        HouseholdHeadChange change = outcome.HouseholdHeadChange;

        Assert.Equal(Head, action.CharacterId);
        Assert.Equal(Household, action.HouseholdId);
        Assert.Equal(Replacement, action.ReplacementHeadCharacterId);
        Assert.Equal(CharacterConditionContractVersions.Death, death.ContractVersion);
        Assert.Equal(CharacterConditionContractVersions.HouseholdHeadChange, change.ContractVersion);
        Assert.Equal(
            CharacterConditionIds.DeriveHouseholdHeadChangeId(campaignEvent.EventId, Household),
            change.ChangeId);
        Assert.Equal(Household, change.HouseholdId);
        Assert.Equal(Head, change.PreviousHeadCharacterId);
        Assert.Equal(Replacement, change.CurrentHeadCharacterId);
        Assert.Equal(command.CommandId, change.SourceCommandId);
        Assert.Equal(campaignEvent.EventId, change.SourceEventId);
        Assert.Equal(Date, change.ResolutionDate);
        Assert.Equal(0, change.ResolutionTurnIndex);
        Assert.Single(death.ReleasedCustodyChanges);
        Assert.Equal(Dependent, death.ReleasedCustodyChanges[0].CharacterId);
        Assert.Null(payload.RelationshipMemoryConsequence);
        Assert.Equal(
            WorldState.GetCharacterConditionActionAffectedIds(payload, campaignEvent.EventId),
            campaignEvent.AffectedIds);
        Assert.Contains(Household, campaignEvent.AffectedIds);
        Assert.Contains(change.ChangeId, campaignEvent.AffectedIds);

        WorldSnapshot after = simulation.World.CaptureSnapshot();
        HouseholdState beforeHousehold = before.Characters.HouseholdStates.Single(
            item => item.HouseholdId == Household);
        HouseholdState afterHousehold = after.Characters.HouseholdStates.Single(
            item => item.HouseholdId == Household);
        Assert.Equal(Head, beforeHousehold.HeadCharacterId);
        Assert.Equal(Replacement, afterHousehold.HeadCharacterId);
        Assert.Equal(beforeHousehold.MemberIds, afterHousehold.MemberIds);
        Assert.Contains(Head, afterHousehold.MemberIds);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
        Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Replacement).Condition.VitalStatus);
        Assert.Equal(CharacterCustodyStatus.Free, Profile(simulation, Dependent).Condition.CustodyStatus);
        Assert.Null(Profile(simulation, Dependent).Condition.CustodianId);
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(Serialize(before.CharacterEstateHoldings), Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Head, Assert.Single(after.CharacterResources.Accounts).CharacterId);
        Assert.Equal(Head, Assert.Single(after.CharacterEstateHoldings.Holdings).OwnerCharacterId);

        string json = Serialize(campaignEvent);
        Assert.Contains("resolve_household_head_death.v1", json, StringComparison.Ordinal);
        Assert.Contains("household_head_death_resolved.v1", json, StringComparison.Ordinal);
        CampaignEvent roundTrip = JsonSerializer.Deserialize<CampaignEvent>(
            json,
            SimulationJson.CreateOptions())!;
        Assert.Equal(json, Serialize(roundTrip));
    }

    [Fact]
    public void F303_HeadDeathComposesEveryExistingDeathLifecycleAndPreservesInputs()
    {
        CampaignSimulation simulation = CreateLifecycleRichSimulation();
        WorldSnapshot before = simulation.World.CaptureSnapshot();

        Assert.True(simulation.Submit(HeadDeathCommand(simulation, "rich-lifecycle")).IsValid);
        HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
            HouseholdHeadDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        CharacterDeathChange death = outcome.Death;

        Assert.Single(death.ReleasedCustodyChanges);
        Assert.Single(death.MarriageChanges.EndedUnions);
        Assert.Single(death.EndedGuardianships);
        Assert.Single(death.RemovedPregnancies);
        Assert.Single(death.CareerChanges.InvalidatedProposals);
        Assert.Equal(CharacterMarriageLifecycleReason.CharacterDied, death.MarriageChanges.Reason);
        Assert.Equal(Replacement, outcome.HouseholdHeadChange.CurrentHeadCharacterId);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
        Assert.Equal(CharacterCustodyStatus.Free, Profile(simulation, Dependent).Condition.CustodyStatus);
        Assert.Equal(
            MarriageUnionStatus.Ended,
            Assert.Single(simulation.World.CharacterMarriages.Unions).Status);
        Assert.Equal(
            CharacterGuardianshipStatus.Ended,
            Assert.Single(simulation.World.CharacterGuardianships.Guardianships).Status);
        Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
        Assert.Equal(
            CareerProposalStatus.Invalidated,
            Assert.Single(simulation.World.Careers.Proposals).Status);
        WorldSnapshot after = simulation.World.CaptureSnapshot();
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(
            Serialize(before.CharacterEstateHoldings),
            Serialize(after.CharacterEstateHoldings));
    }

    [Fact]
    public void F303_LaterCareerFailureRollsBackHeadCustodyAndEverySubsystem()
    {
        CampaignSimulation simulation = CreateOverflowCareerSimulation();
        string before = SnapshotJson(simulation);

        Assert.False(simulation.Submit(HeadDeathCommand(simulation, "career-overflow")).IsValid);

        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Head).Condition.VitalStatus);
        Assert.Equal(CharacterCustodyStatus.Hostage, Profile(simulation, Dependent).Condition.CustodyStatus);
        Assert.Equal(Head, simulation.World.Characters.Households.Single(
            item => item.HouseholdId == Household).HeadCharacterId);
        Assert.Single(simulation.World.Careers.RetinueMemberships, item => item.IsActive);
    }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("self")]
    [InlineData("missing-replacement")]
    [InlineData("outsider")]
    [InlineData("dead-replacement")]
    [InlineData("wrong-household")]
    [InlineData("non-head-target")]
    public void F304_InvalidOrUnsupportedHeadDeathsFailClosed(string scenario)
    {
        CampaignSimulation simulation = CreateSimulation(
            replacementCondition: scenario == "dead-replacement"
                ? CanonicalDeadCondition()
                : CharacterConditionState.Default,
            replacementBirthDate: new CampaignDate(170, 1, 1));
        string before = SnapshotJson(simulation);
        ICharacterConditionAction action = scenario switch
        {
            "ordinary" => new ResolveCharacterDeathAction(
                Head,
                Profile(simulation, Head).Condition),
            "self" => new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                Household,
                Head),
            "missing-replacement" => new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                Household,
                new EntityId("character:test/f3-missing")),
            "outsider" => new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                Household,
                Outsider),
            "dead-replacement" => new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                Household,
                Replacement),
            "wrong-household" => new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(simulation, Head).Condition,
                OtherHousehold,
                Outsider),
            "non-head-target" => new ResolveHouseholdHeadDeathAction(
                Replacement,
                Profile(simulation, Replacement).Condition,
                Household,
                Head),
            _ => throw new InvalidOperationException(),
        };
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/f3-invalid-{scenario}"),
            CharacterConditionSystem.AuthoritativeActorId,
            Date,
            new CharacterConditionActionCommandPayload(action));

        Assert.False(simulation.Submit(command).IsValid);
        Assert.Equal(before, SnapshotJson(simulation));
    }

    [Fact]
    public void F304_FutureBornReplacementCannotEnterTheAuthoritativeWorld()
    {
        Assert.Throws<SimulationValidationException>(() => CreateSimulation(
            replacementBirthDate: Date.AddDays(1)));
    }

    [Theory]
    [InlineData("minor")]
    [InlineData("incapacitated")]
    [InlineData("captive")]
    [InlineData("hostage")]
    public void F304_LivingMemberReplacementDoesNotInferAdditionalEligibilityPolicy(
        string scenario)
    {
        CharacterConditionState condition = scenario switch
        {
            "incapacitated" => CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            "captive" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Outsider,
            },
            "hostage" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Hostage,
                CustodianId = Outsider,
            },
            "minor" => CharacterConditionState.Default,
            _ => throw new InvalidOperationException(),
        };
        CampaignSimulation simulation = CreateSimulation(
            replacementCondition: condition,
            replacementBirthDate: scenario == "minor"
                ? new CampaignDate(195, 1, 1)
                : new CampaignDate(170, 1, 1));

        Assert.True(simulation.Submit(HeadDeathCommand(simulation, $"permissive-{scenario}")).IsValid);
        HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
            HouseholdHeadDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);

        Assert.Equal(Replacement, outcome.HouseholdHeadChange.CurrentHeadCharacterId);
        Assert.Equal(Replacement, simulation.World.Characters.Households.Single(
            item => item.HouseholdId == Household).HeadCharacterId);
        Assert.Equal(condition, Profile(simulation, Replacement).Condition);
    }

    [Fact]
    public void F308_HeadChangeAffectedIdsPairingAndExactReplanRejectTampering()
    {
        CampaignSimulation simulation = CreateSimulation(dependentInCustody: true);
        CampaignCommand command = HeadDeathCommand(simulation, "tamper");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(Date, command.CommandId);
        CharacterConditionAggregatePlan plan = simulation.World.PrepareCharacterConditionAction(
            command.IssuingActor,
            Assert.IsType<CharacterConditionActionCommandPayload>(command.Payload),
            Date,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CharacterConditionActionResolvedEventPayload resolved = plan.ResolvedPayload;
        HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
            HouseholdHeadDeathResolvedOutcome>(resolved.Outcome);
        EntityId[] affected = WorldState.GetCharacterConditionActionAffectedIds(resolved, eventId);
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

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid with
        {
            AffectedIds = affected.Where(item => item != Household).ToArray(),
        }));
        Assert.Equal(before, SnapshotJson(simulation));

        CharacterConditionActionResolvedEventPayload wrongPair = resolved with
        {
            Outcome = new CharacterDeathResolvedOutcome(outcome.Death),
        };
        Assert.Throws<SimulationValidationException>(() =>
            WorldState.GetCharacterConditionActionAffectedIds(wrongPair, eventId));

        CharacterConditionActionResolvedEventPayload inverseWrongPair = resolved with
        {
            Action = new ResolveCharacterDeathAction(
                Head,
                Assert.IsType<ResolveHouseholdHeadDeathAction>(resolved.Action).ExpectedCurrent),
        };
        Assert.Throws<SimulationValidationException>(() =>
            WorldState.GetCharacterConditionActionAffectedIds(inverseWrongPair, eventId));

        CharacterConditionActionResolvedEventPayload badEvidence = resolved with
        {
            Outcome = outcome with
            {
                HouseholdHeadChange = outcome.HouseholdHeadChange with
                {
                    ChangeId = new EntityId("household_head_change:test/forged"),
                },
            },
        };
        Assert.Throws<SimulationValidationException>(() =>
            WorldState.GetCharacterConditionActionAffectedIds(badEvidence, eventId));

        ResolveHouseholdHeadDeathAction forgedAction = Assert.IsType<
            ResolveHouseholdHeadDeathAction>(resolved.Action) with
        {
            ReplacementHeadCharacterId = Outsider,
        };
        HouseholdHeadDeathResolvedOutcome forgedOutcome = outcome with
        {
            HouseholdHeadChange = outcome.HouseholdHeadChange with
            {
                CurrentHeadCharacterId = Outsider,
            },
        };
        CharacterConditionActionResolvedEventPayload structurallyValidForgery = resolved with
        {
            Action = forgedAction,
            Outcome = forgedOutcome,
        };
        CampaignEvent forgedEvent = valid with
        {
            Payload = structurallyValidForgery,
            AffectedIds = WorldState.GetCharacterConditionActionAffectedIds(
                structurallyValidForgery,
                eventId),
        };
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(forgedEvent));
        Assert.Equal(before, SnapshotJson(simulation));

        simulation.World.Apply(valid);
        Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
        Assert.Equal(Replacement, simulation.World.Characters.Households.Single(
            item => item.HouseholdId == Household).HeadCharacterId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F309_ReplacementDeathRaceIsPriorityAndSubmissionOrderDeterministic(
        bool replacementDiesFirst)
    {
        string? expectedEvents = null;
        string? expectedChecksum = null;
        for (int reverseSubmission = 0; reverseSubmission < 2; reverseSubmission++)
        {
            CampaignSimulation simulation = CreateSimulation();
            CampaignCommand headDeath = HeadDeathCommand(
                simulation,
                $"race-head-{replacementDiesFirst.ToString().ToLowerInvariant()}",
                priority: replacementDiesFirst ? 10 : -10);
            CampaignCommand replacementDeath = CampaignCommand.Create(
                new EntityId($"command:test/f3-race-replacement-{replacementDiesFirst.ToString().ToLowerInvariant()}"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                    Replacement,
                    Profile(simulation, Replacement).Condition)),
                priority: replacementDiesFirst ? -10 : 10);
            CampaignCommand[] commands = reverseSubmission == 1
                ? [replacementDeath, headDeath]
                : [headDeath, replacementDeath];
            foreach (CampaignCommand command in commands)
            {
                Assert.True(simulation.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
            Assert.Equal(2, events.Count);
            Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
            if (replacementDiesFirst)
            {
                Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Head).Condition.VitalStatus);
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Replacement).Condition.VitalStatus);
                Assert.Equal(Head, simulation.World.Characters.Households.Single(
                    item => item.HouseholdId == Household).HeadCharacterId);
            }
            else
            {
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
                Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Replacement).Condition.VitalStatus);
                Assert.Equal(Replacement, simulation.World.Characters.Households.Single(
                    item => item.HouseholdId == Household).HeadCharacterId);
            }

            string serializedEvents = Serialize(events);
            string checksum = SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F309_ReplacementExpulsionRaceIsPriorityAndSubmissionOrderDeterministic(
        bool expulsionFirst)
    {
        string? expectedEvents = null;
        string? expectedChecksum = null;
        for (int reverseSubmission = 0; reverseSubmission < 2; reverseSubmission++)
        {
            CampaignSimulation simulation = CreateSimulation();
            CampaignCommand headDeath = HeadDeathCommand(
                simulation,
                $"race-household-head-{expulsionFirst.ToString().ToLowerInvariant()}",
                priority: expulsionFirst ? 10 : -10);
            CampaignCommand expulsion = CampaignCommand.Create(
                new EntityId($"command:test/f3-race-expulsion-{expulsionFirst.ToString().ToLowerInvariant()}"),
                Head,
                Date,
                new HouseholdDecisionCommandPayload(new ExpelHouseholdMemberAction(
                    Household,
                    Replacement)),
                priority: expulsionFirst ? -10 : 10);
            CampaignCommand[] commands = reverseSubmission == 1
                ? [expulsion, headDeath]
                : [headDeath, expulsion];
            foreach (CampaignCommand command in commands)
            {
                Assert.True(simulation.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
            Assert.Equal(2, events.Count);
            Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
            HouseholdState household = simulation.World.CaptureSnapshot().Characters.HouseholdStates
                .Single(item => item.HouseholdId == Household);
            if (expulsionFirst)
            {
                Assert.Equal(CharacterVitalStatus.Alive, Profile(simulation, Head).Condition.VitalStatus);
                Assert.Equal(Head, household.HeadCharacterId);
                Assert.DoesNotContain(Replacement, household.MemberIds);
            }
            else
            {
                Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
                Assert.Equal(Replacement, household.HeadCharacterId);
                Assert.Contains(Replacement, household.MemberIds);
            }

            string serializedEvents = Serialize(events);
            string checksum = SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F309_CompetingHeadHandoffsResolveByPriorityAndNotSubmissionOrder(
        bool alternateWins)
    {
        string? expectedEvents = null;
        string? expectedChecksum = null;
        for (int reverseSubmission = 0; reverseSubmission < 2; reverseSubmission++)
        {
            CampaignSimulation simulation = CreateSimulation();
            CampaignCommand replacementHandoff = HeadDeathCommand(
                simulation,
                $"competing-replacement-{alternateWins.ToString().ToLowerInvariant()}",
                priority: alternateWins ? 10 : -10);
            CampaignCommand alternateHandoff = CampaignCommand.Create(
                new EntityId($"command:test/f3-competing-alternate-{alternateWins.ToString().ToLowerInvariant()}"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
                    Head,
                    Profile(simulation, Head).Condition,
                    Household,
                    AlternateReplacement)),
                priority: alternateWins ? -10 : 10);
            CampaignCommand[] commands = reverseSubmission == 0
                ? [replacementHandoff, alternateHandoff]
                : [alternateHandoff, replacementHandoff];
            foreach (CampaignCommand command in commands)
            {
                Assert.True(simulation.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
            Assert.Equal(2, events.Count);
            Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
            Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
            Assert.Equal(
                alternateWins ? AlternateReplacement : Replacement,
                simulation.World.Characters.Households.Single(
                    item => item.HouseholdId == Household).HeadCharacterId);

            string serializedEvents = Serialize(events);
            string checksum = SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
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
    public void F309_IndependentHouseholdHeadDeathsCommuteAcrossSubmissionOrder()
    {
        string? expectedEvents = null;
        string? expectedChecksum = null;
        for (int reverse = 0; reverse < 2; reverse++)
        {
            CampaignSimulation simulation = CreateSimulation();
            CampaignCommand first = HeadDeathCommand(simulation, "independent-primary");
            CampaignCommand second = CampaignCommand.Create(
                new EntityId("command:test/f3-independent-other"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
                    Outsider,
                    Profile(simulation, Outsider).Condition,
                    OtherHousehold,
                    Dependent)));
            CampaignCommand[] commands = reverse == 0 ? [first, second] : [second, first];
            foreach (CampaignCommand command in commands)
            {
                Assert.True(simulation.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
            Assert.Equal(2, events.Count);
            Assert.All(events, item => Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                item.Payload));
            string serializedEvents = Serialize(events);
            string checksum = SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
            Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Head).Condition.VitalStatus);
            Assert.Equal(CharacterVitalStatus.Dead, Profile(simulation, Outsider).Condition.VitalStatus);
            Assert.Equal(Replacement, simulation.World.Characters.Households.Single(
                item => item.HouseholdId == Household).HeadCharacterId);
            Assert.Equal(Dependent, simulation.World.Characters.Households.Single(
                item => item.HouseholdId == OtherHousehold).HeadCharacterId);
            if (reverse == 0)
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
    public void F310_CurrentPendingAndResolvedHeadDeathRoundTripThroughSaveStore()
    {
        CampaignSimulation original = CreateSimulation(dependentInCustody: true);
        CampaignCommand command = HeadDeathCommand(original, "save");
        Assert.True(original.Submit(command).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f3-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            SaveStore store = new();
            string pendingPath = Path.Combine(directory, "pending.save.gz");
            store.SaveAtomic(pendingPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope pending = store.Load(pendingPath);
            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
            Assert.Single(pending.Snapshot.PendingCommands);
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
            CharacterConditionActionResolvedEventPayload loaded = Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(resolved.DiagnosticEvents.Single(
                    item => item.Payload is CharacterConditionActionResolvedEventPayload
                    {
                        Outcome: HouseholdHeadDeathResolvedOutcome,
                    }).Payload);
            HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
                HouseholdHeadDeathResolvedOutcome>(loaded.Outcome);
            Assert.Equal(Replacement, outcome.HouseholdHeadChange.CurrentHeadCharacterId);
            WorldState restored = WorldState.Restore(resolved.Snapshot);
            Assert.Equal(CharacterVitalStatus.Dead, restored.Characters.Profiles.Single(
                item => item.CharacterId == Head).Condition.VitalStatus);
            Assert.Equal(Replacement, restored.Characters.Households.Single(
                item => item.HouseholdId == Household).HeadCharacterId);
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
    public void F310_LifecycleRichHeadDeathReplaysOnEveryLaterTurnDay(int dayOffset)
    {
        CampaignSimulation source = CreateLifecycleRichSimulation();
        CampaignSimulation original = new(WorldState.Restore(
            source.World.CaptureSnapshot() with
            {
                Calendar = new CampaignCalendar(Date, 1),
            }));
        CampaignDate resolutionDate = Date.AddDays(dayOffset);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/f3-later-day-{dayOffset}"),
            CharacterConditionSystem.AuthoritativeActorId,
            resolutionDate,
            new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
                Head,
                Profile(original, Head).Condition,
                Household,
                Replacement)));
        Assert.True(original.Submit(command).IsValid);
        CampaignSimulation replay = new(WorldState.Restore(original.World.CaptureSnapshot()));

        CampaignEvent first = Assert.Single(original.ResolveTurn());
        CampaignEvent second = Assert.Single(replay.ResolveTurn());

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        HouseholdHeadDeathResolvedOutcome outcome = Assert.IsType<
            HouseholdHeadDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(first.Payload).Outcome);
        Assert.Equal(resolutionDate, first.ResolutionDate);
        Assert.Equal(resolutionDate, outcome.Death.ResolutionDate);
        Assert.Equal(resolutionDate, outcome.HouseholdHeadChange.ResolutionDate);
        Assert.Equal(1, outcome.HouseholdHeadChange.ResolutionTurnIndex);
        Assert.Equal(command.CommandId, outcome.HouseholdHeadChange.SourceCommandId);
        Assert.Equal(first.EventId, outcome.HouseholdHeadChange.SourceEventId);
        Assert.Single(outcome.Death.MarriageChanges.EndedUnions);
        Assert.Single(outcome.Death.EndedGuardianships);
        Assert.Single(outcome.Death.RemovedPregnancies);
        Assert.Single(outcome.Death.CareerChanges.InvalidatedProposals);
        Assert.Equal(Replacement, original.World.Characters.Households.Single(
            item => item.HouseholdId == Household).HeadCharacterId);
    }

    private static CampaignSimulation CreateSimulation(
        bool dependentInCustody = false,
        CharacterConditionState? replacementCondition = null,
        CampaignDate? replacementBirthDate = null)
    {
        EntityId[] ids = [Head, Replacement, AlternateReplacement, Dependent, Outsider];
        CharacterDefinition[] definitions = ids.Select(id =>
        {
            EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                nameKey,
                id == Replacement
                    ? replacementBirthDate ?? new CampaignDate(170, 1, 1)
                    : id == AlternateReplacement
                        ? new CampaignDate(190, 1, 1)
                    : new CampaignDate(170, 1, 1),
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
        }).OrderBy(item => item.Id).ToArray();
        CharacterState[] states = ids.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            [],
            [],
            id == Replacement
                ? replacementCondition ?? CharacterConditionState.Default
                : id == Dependent && dependentInCustody
                    ? CharacterConditionState.Default with
                    {
                        CustodyStatus = CharacterCustodyStatus.Hostage,
                        CustodianId = Head,
                    }
                    : CharacterConditionState.Default,
            [])).OrderBy(item => item.CharacterId).ToArray();
        HouseholdDefinition[] householdDefinitions =
        [
            new HouseholdDefinition(
                CharacterContractVersions.Definition,
                Household,
                new EntityId("loc:household/f3_primary")),
            new HouseholdDefinition(
                CharacterContractVersions.Definition,
                OtherHousehold,
                new EntityId("loc:household/f3_other")),
        ];
        HouseholdState[] householdStates =
        [
            new HouseholdState(
                CharacterContractVersions.State,
                Household,
                Head,
                new[] { Head, Replacement, AlternateReplacement }.Order().ToArray()),
            new HouseholdState(
                CharacterContractVersions.State,
                OtherHousehold,
                Outsider,
                new[] { Outsider, Dependent }.Order().ToArray()),
        ];
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            householdDefinitions.OrderBy(item => item.Id).ToArray(),
            states,
            [],
            householdStates.OrderBy(item => item.HouseholdId).ToArray());
        CharacterResourceWorldSnapshot resources = new(
            CharacterResourceContractVersions.Snapshot,
            [new CharacterWealthAccountState(
                CharacterResourceContractVersions.State,
                CharacterResourceIds.DeriveWealthAccountId(Head),
                Head,
                99)],
            [],
            []);
        CharacterEstateHoldingWorldSnapshot estates = new(
            CharacterEstateHoldingContractVersions.Snapshot,
            [new CharacterEstateHoldingState(
                CharacterEstateHoldingContractVersions.State,
                new EntityId("estate:test/f3-preserved"),
                Head)]);
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            resources,
            estates,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty));
    }

    private static CampaignSimulation CreateLifecycleRichSimulation()
    {
        CampaignSimulation seed = CreateSimulation(dependentInCustody: true);
        EntityId practiceId = new("marriage_practice:test/f3-rich");
        EntityId proposalId = new("marriage_proposal:test/f3-rich");
        EntityId unionId = new("marriage_union:test/f3-rich");
        EntityId proposalCommand = new("command:test/f3-rich-marriage-proposal");
        EntityId firstSpouse = Head.CompareTo(Outsider) < 0 ? Head : Outsider;
        EntityId secondSpouse = Head.CompareTo(Outsider) < 0 ? Outsider : Head;
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            proposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.PoliticalArrangement,
            Head,
            Outsider,
            null,
            practiceId,
            Date.AddDays(-20),
            0,
            proposalCommand,
            MarriageProposalStatus.Accepted,
            Date.AddDays(-19),
            0,
            new EntityId("command:test/f3-rich-marriage-accepted"));
        MarriageUnionState union = new(
            CharacterMarriageContractVersions.State,
            unionId,
            firstSpouse,
            secondSpouse,
            MarriageUnionForm.PrincipalSpouse,
            null,
            MarriageBasis.Political,
            MarriageConsentKind.PoliticalArrangement,
            practiceId,
            proposalId,
            Date.AddDays(-19),
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
                practiceId,
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

        CampaignDate guardianshipStart = Date.AddDays(-30);
        EntityId guardianshipCommand = new("command:test/f3-rich-guardianship");
        EntityId guardianshipEvent = CharacterFamilyIds.DeriveActionEventId(
            guardianshipStart,
            guardianshipCommand);
        CharacterGuardianshipState guardianship = new(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(
                guardianshipEvent,
                AlternateReplacement,
                Head),
            AlternateReplacement,
            Head,
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

        CampaignDate pregnancyStart = Date.AddDays(-10);
        EntityId pregnancyCommand = new("command:test/f3-rich-pregnancy");
        EntityId pregnancyEvent = CharacterFamilyIds.DeriveActionEventId(
            pregnancyStart,
            pregnancyCommand);
        CharacterPregnancyState pregnancy = new(
            CharacterPregnancyContractVersions.State,
            CharacterPregnancyIds.DerivePregnancyId(
                pregnancyEvent,
                Head,
                Outsider,
                unionId),
            Head,
            Outsider,
            unionId,
            pregnancyStart,
            pregnancyStart.AddDays(CharacterPregnancyLimits.GestationDays),
            0,
            pregnancyCommand,
            pregnancyEvent);

        EntityId careerCommand = new("command:test/f3-rich-career-proposal");
        CareerProposalState careerProposal = new(
            CareerContractVersions.State,
            CareerIds.DeriveProposalId(CareerProposalKind.PatronageOffer, Date, careerCommand),
            CareerProposalKind.PatronageOffer,
            Head,
            Outsider,
            new ServicePrincipalReference(ServicePrincipalKind.Character, Head),
            null,
            Date,
            0,
            careerCommand,
            CareerProposalStatus.Active,
            null,
            null,
            null);
        WorldSnapshot snapshot = seed.World.CaptureSnapshot() with
        {
            Careers = new CareerWorldSnapshot(
                CareerContractVersions.Snapshot,
                [careerProposal],
                [],
                [],
                [],
                [],
                [],
                []),
            CharacterMarriages = marriages,
            CharacterGuardianships = new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [guardianship]),
            CharacterPregnancies = new CharacterPregnancyWorldSnapshot(
                CharacterPregnancyContractVersions.Snapshot,
                [pregnancy]),
        };
        return new CampaignSimulation(WorldState.Restore(snapshot));
    }

    private static CampaignSimulation CreateOverflowCareerSimulation()
    {
        CampaignSimulation seed = CreateSimulation(dependentInCustody: true);
        RetinueState retinue = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueId(Outsider),
            Outsider);
        RetinueMembershipState[] completed = Enumerable.Range(
                0,
                CareerLimits.CompletedRecordsPerCategoryPerCharacter)
            .Select(index =>
            {
                EntityId source = new($"career_proposal:test/f3-overflow-completed-{index:D2}");
                return new RetinueMembershipState(
                    CareerContractVersions.State,
                    CareerIds.DeriveRetinueMembershipId(source),
                    retinue.RetinueId,
                    Outsider,
                    Head,
                    source,
                    Date.AddDays(-10),
                    0,
                    Date.AddDays(-1),
                    0,
                    new EntityId($"command:test/f3-overflow-completed-{index:D2}"),
                    CareerServiceEndReason.MemberLeft);
            })
            .ToArray();
        EntityId activeSource = new("career_proposal:test/f3-overflow-active");
        RetinueMembershipState active = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueMembershipId(activeSource),
            retinue.RetinueId,
            Outsider,
            Head,
            activeSource,
            Date.AddDays(-1),
            0,
            null,
            null,
            null,
            null);
        CareerWorldSnapshot careers = new(
            CareerContractVersions.Snapshot,
            [],
            [retinue],
            completed.Append(active).OrderBy(item => item.MembershipId).ToArray(),
            [],
            [],
            [],
            [CareerHistoryAggregate.Empty(Head) with
            {
                FoldedRetinueMembershipCount = long.MaxValue,
                EarliestDate = Date.AddDays(-1),
                LatestDate = Date.AddDays(-1),
            }]);
        return new CampaignSimulation(WorldState.Restore(
            seed.World.CaptureSnapshot() with
            {
                Careers = careers,
            }));
    }

    private static CampaignCommand HeadDeathCommand(
        CampaignSimulation simulation,
        string suffix,
        int priority = 0) => CampaignCommand.Create(
        new EntityId($"command:test/f3-{suffix.ToLowerInvariant()}"),
        CharacterConditionSystem.AuthoritativeActorId,
        Date,
        new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
            Head,
            Profile(simulation, Head).Condition,
            Household,
            Replacement)),
        priority: priority);

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId characterId) => simulation.World.Characters.Profiles.Single(
        item => item.CharacterId == characterId);

    private static CharacterConditionState CanonicalDeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        null);

    private static string SnapshotJson(CampaignSimulation simulation) =>
        Serialize(simulation.World.CaptureSnapshot());

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
