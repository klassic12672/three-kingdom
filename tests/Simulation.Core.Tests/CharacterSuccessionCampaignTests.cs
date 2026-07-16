using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Designator = Character("designator");
    private static readonly EntityId FirstHeir = Character("first-heir");
    private static readonly EntityId SecondHeir = Character("second-heir");
    private static readonly EntityId ThirdHeir = Character("third-heir");

    [Fact]
    public void F402_RegisteredDesignationResolvesThroughExactCommandEventAndQuery()
    {
        CampaignSimulation simulation = CreateSimulation();
        EntityId commandId = new("command:test/f4-designate");
        CampaignCommand command = CampaignCommand.Create(
            commandId,
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(FirstHeir, null)));

        Assert.Equal("character_succession_action.v1", command.CommandType);
        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterSuccessionActionResolvedEventPayload payload = Assert.IsType<
            CharacterSuccessionActionResolvedEventPayload>(campaignEvent.Payload);
        HeirDesignatedOutcome outcome = Assert.IsType<HeirDesignatedOutcome>(payload.Outcome);
        HeirDesignationState designation = outcome.CurrentDesignation;

        Assert.Equal("character_succession_action_resolved.v1", campaignEvent.EventType);
        Assert.Equal(
            CharacterSuccessionIds.DeriveActionEventId(Date, commandId),
            campaignEvent.EventId);
        Assert.Equal(
            CharacterSuccessionIds.DeriveDesignationId(
                campaignEvent.EventId,
                Designator,
                FirstHeir),
            designation.DesignationId);
        Assert.Equal(Designator, designation.DesignatorCharacterId);
        Assert.Equal(FirstHeir, designation.HeirCharacterId);
        Assert.Equal(HeirDesignationStatus.Active, designation.Status);
        Assert.Equal(Date, designation.EstablishedDate);
        Assert.Equal(0, designation.EstablishedTurnIndex);
        Assert.Equal(commandId, designation.SourceCommandId);
        Assert.Equal(campaignEvent.EventId, designation.SourceEventId);
        Assert.Null(designation.ResolutionDate);
        Assert.Equal(
            WorldState.GetCharacterSuccessionActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? queried));
        Assert.Equal(designation, queried);
        Assert.Single(
            simulation.World.CharacterSuccessions.GetDesignationRecordsInvolving(FirstHeir));

        string json = Serialize(campaignEvent);
        Assert.Contains("character_succession_action_resolved.v1", json, StringComparison.Ordinal);
        Assert.Contains("designate_heir.v1", json, StringComparison.Ordinal);
        Assert.Contains("heir_designated.v1", json, StringComparison.Ordinal);
        CampaignEvent roundTrip = JsonSerializer.Deserialize<CampaignEvent>(
            json,
            SimulationJson.CreateOptions())!;
        Assert.Equal(json, Serialize(roundTrip));
    }

    [Fact]
    public void F403_ReplacementAndRevocationRetainExactLifecycleEvidence()
    {
        CampaignSimulation simulation = CreateSimulation();
        CharacterSuccessionActionResolvedEventPayload initial = ResolveSuccession(
            simulation,
            "initial",
            new DesignateHeirAction(FirstHeir, null));
        HeirDesignationState first = Assert.IsType<HeirDesignatedOutcome>(
            initial.Outcome).CurrentDesignation;

        CampaignDate replacementDate = simulation.World.Calendar.Date;
        long replacementTurn = simulation.World.Calendar.TurnIndex;
        CharacterSuccessionActionResolvedEventPayload replacement = ResolveSuccession(
            simulation,
            "replacement",
            new DesignateHeirAction(SecondHeir, first.DesignationId));
        HeirDesignationReplacedOutcome replaced = Assert.IsType<
            HeirDesignationReplacedOutcome>(replacement.Outcome);

        Assert.Equal(first.DesignationId, replaced.PreviousDesignation.DesignationId);
        Assert.Equal(HeirDesignationStatus.Replaced, replaced.PreviousDesignation.Status);
        Assert.Equal(replacementDate, replaced.PreviousDesignation.ResolutionDate);
        Assert.Equal(replacementTurn, replaced.PreviousDesignation.ResolutionTurnIndex);
        Assert.Equal(HeirDesignationStatus.Active, replaced.CurrentDesignation.Status);
        Assert.Equal(SecondHeir, replaced.CurrentDesignation.HeirCharacterId);
        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? current));
        Assert.Equal(replaced.CurrentDesignation, current);

        CampaignDate revocationDate = simulation.World.Calendar.Date;
        long revocationTurn = simulation.World.Calendar.TurnIndex;
        CharacterSuccessionActionResolvedEventPayload revocation = ResolveSuccession(
            simulation,
            "revocation",
            new RevokeHeirDesignationAction(current.DesignationId));
        HeirDesignationRevokedOutcome revoked = Assert.IsType<
            HeirDesignationRevokedOutcome>(revocation.Outcome);

        Assert.Equal(current.DesignationId, revoked.PreviousDesignation.DesignationId);
        Assert.Equal(HeirDesignationStatus.Revoked, revoked.PreviousDesignation.Status);
        Assert.Equal(revocationDate, revoked.PreviousDesignation.ResolutionDate);
        Assert.Equal(revocationTurn, revoked.PreviousDesignation.ResolutionTurnIndex);
        Assert.False(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out _));
        Assert.Equal(
            [HeirDesignationStatus.Replaced, HeirDesignationStatus.Revoked],
            simulation.World.CharacterSuccessions.Designations
                .OrderBy(item => item.EstablishedTurnIndex)
                .Select(item => item.Status));
    }

    [Theory]
    [InlineData("minor")]
    [InlineData("incapacitated")]
    [InlineData("captive")]
    [InlineData("hostage")]
    [InlineData("non-kin-outsider")]
    public void F404_LivingNomineePolicyDoesNotInventLaterLegalEligibility(string scenario)
    {
        CharacterConditionState heirCondition = scenario switch
        {
            "incapacitated" => CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            "captive" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = Designator,
            },
            "hostage" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Hostage,
                CustodianId = Designator,
            },
            _ => CharacterConditionState.Default,
        };
        CampaignDate heirBirthDate = scenario == "minor"
            ? Date.AddDays(-365)
            : new CampaignDate(170, 1, 1);
        CampaignSimulation simulation = CreateSimulation(
            firstHeirCondition: heirCondition,
            firstHeirBirthDate: heirBirthDate);

        CharacterSuccessionActionResolvedEventPayload payload = ResolveSuccession(
            simulation,
            scenario,
            new DesignateHeirAction(FirstHeir, null));

        Assert.Equal(
            FirstHeir,
            Assert.IsType<HeirDesignatedOutcome>(payload.Outcome)
                .CurrentDesignation.HeirCharacterId);
    }

    [Fact]
    public void F404_LivingCapableFreeMinorMayRecordIntentWithoutGainingLegalStatus()
    {
        CampaignSimulation simulation = CreateSimulation(
            designatorBirthDate: Date.AddDays(-365));

        CharacterSuccessionActionResolvedEventPayload payload = ResolveSuccession(
            simulation,
            "minor-designator",
            new DesignateHeirAction(FirstHeir, null));

        Assert.Equal(
            Designator,
            Assert.IsType<HeirDesignatedOutcome>(payload.Outcome)
                .CurrentDesignation.DesignatorCharacterId);
    }

    [Theory]
    [InlineData("dead-designator")]
    [InlineData("incapacitated-designator")]
    [InlineData("captive-designator")]
    [InlineData("hostage-designator")]
    [InlineData("dead-heir")]
    [InlineData("future-heir")]
    [InlineData("missing-heir")]
    [InlineData("self")]
    public void F404_InvalidAgencyOrParticipantsFailClosed(string scenario)
    {
        CharacterConditionState designatorCondition = scenario switch
        {
            "dead-designator" => DeadCondition(),
            "incapacitated-designator" => CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            "captive-designator" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = SecondHeir,
            },
            "hostage-designator" => CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Hostage,
                CustodianId = SecondHeir,
            },
            _ => CharacterConditionState.Default,
        };
        CharacterConditionState heirCondition = scenario == "dead-heir"
            ? DeadCondition()
            : CharacterConditionState.Default;
        CampaignDate heirBirthDate = scenario == "future-heir"
            ? Date.AddDays(1)
            : new CampaignDate(170, 1, 1);
        if (scenario == "future-heir")
        {
            Assert.Throws<SimulationValidationException>(() => CreateSimulation(
                designatorCondition,
                heirCondition,
                heirBirthDate));
            return;
        }

        CampaignSimulation simulation = CreateSimulation(
            designatorCondition,
            heirCondition,
            heirBirthDate);
        EntityId heir = scenario switch
        {
            "missing-heir" => Character("missing"),
            "self" => Designator,
            _ => FirstHeir,
        };
        WorldSnapshot before = simulation.World.CaptureSnapshot();

        CommandValidationResult result = simulation.Submit(CampaignCommand.Create(
            new EntityId($"command:test/f4-invalid-{scenario}"),
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(heir, null))));

        Assert.False(result.IsValid);
        Assert.Equal(
            Serialize(before),
            Serialize(simulation.World.CaptureSnapshot()));
    }

    [Fact]
    public void F405_StaleConcurrentDesignationsResolveDeterministicallyAcrossSubmissionOrder()
    {
        CampaignCommand first = CampaignCommand.Create(
            new EntityId("command:test/f4-concurrent-a"),
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(FirstHeir, null)));
        CampaignCommand second = CampaignCommand.Create(
            new EntityId("command:test/f4-concurrent-b"),
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(SecondHeir, null)));

        (string Events, string Snapshot) forward = RunConcurrent(first, second);
        (string Events, string Snapshot) reverse = RunConcurrent(second, first);

        Assert.Equal(forward, reverse);
        CampaignEvent[] events = JsonSerializer.Deserialize<CampaignEvent[]>(
            forward.Events,
            SimulationJson.CreateOptions())!;
        Assert.Single(events, item => item.Payload is CharacterSuccessionActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.Empty(events.Single(item => item.Payload is CommandCancelledEventPayload).AffectedIds);
    }

    [Fact]
    public void F405_NoOpRedesignationFailsWithoutChangingState()
    {
        CampaignSimulation simulation = CreateSimulation();
        HeirDesignationState current = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                "no-op-initial",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand noOp = CampaignCommand.Create(
            new EntityId("command:test/f4-no-op"),
            Designator,
            simulation.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(FirstHeir, current.DesignationId)));

        Assert.False(simulation.Submit(noOp).IsValid);
        Assert.Equal(Serialize(before), Serialize(simulation.World.CaptureSnapshot()));
    }

    [Fact]
    public void F405_RetainedResolvedCommandIdentityCannotBeReusedAfterReload()
    {
        CampaignSimulation original = CreateSimulation();
        HeirDesignationState current = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                original,
                "identity-reuse-initial",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        CampaignSimulation reloaded = new(WorldState.Restore(
            original.World.CaptureSnapshot()));
        string before = Serialize(reloaded.World.CaptureSnapshot());
        CampaignCommand reused = CampaignCommand.Create(
            current.SourceCommandId,
            Designator,
            reloaded.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(SecondHeir, current.DesignationId)));

        CommandValidationResult result = reloaded.Submit(reused);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid_character_succession_action");
        Assert.Equal(before, Serialize(reloaded.World.CaptureSnapshot()));
    }

    [Theory]
    [InlineData("replacement-replacement", 0, 0)]
    [InlineData("replacement-replacement", -5, 5)]
    [InlineData("replacement-revoke", 0, 0)]
    [InlineData("replacement-revoke", 5, -5)]
    [InlineData("revoke-revoke", 0, 0)]
    [InlineData("revoke-revoke", -5, 5)]
    public void F405_StaleTerminalConflictsArePriorityAndIdDeterministic(
        string scenario,
        int firstPriority,
        int secondPriority)
    {
        (string Events, string Snapshot) forward = RunConcurrentTerminalConflict(
            scenario,
            firstPriority,
            secondPriority,
            reverseSubmission: false);
        (string Events, string Snapshot) reverse = RunConcurrentTerminalConflict(
            scenario,
            firstPriority,
            secondPriority,
            reverseSubmission: true);

        Assert.Equal(forward, reverse);
        CampaignEvent[] events = JsonSerializer.Deserialize<CampaignEvent[]>(
            forward.Events,
            SimulationJson.CreateOptions())!;
        Assert.Single(events, item => item.Payload is CharacterSuccessionActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
    }

    [Fact]
    public void F405_IndependentDesignatorsResolveWithoutFalseConflict()
    {
        CampaignCommand first = CampaignCommand.Create(
            new EntityId("command:test/f4-independent-a"),
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(FirstHeir, null)),
            priority: 4);
        CampaignCommand second = CampaignCommand.Create(
            new EntityId("command:test/f4-independent-b"),
            ThirdHeir,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(SecondHeir, null)),
            priority: -4);

        (string Events, string Snapshot) forward = RunConcurrent(first, second);
        (string Events, string Snapshot) reverse = RunConcurrent(second, first);

        Assert.Equal(forward, reverse);
        CampaignEvent[] events = JsonSerializer.Deserialize<CampaignEvent[]>(
            forward.Events,
            SimulationJson.CreateOptions())!;
        Assert.Equal(
            2,
            events.Count(item => item.Payload is CharacterSuccessionActionResolvedEventPayload));
    }

    [Theory]
    [InlineData("designator", true)]
    [InlineData("designator", false)]
    [InlineData("heir", true)]
    [InlineData("heir", false)]
    public void F406_SameDayDeathOrderingIsExplicitAndSubmissionOrderIndependent(
        string deathTarget,
        bool deathFirst)
    {
        (string Events, string Snapshot) forward = RunDesignationDeathRace(
            deathTarget,
            deathFirst,
            reverseSubmission: false);
        (string Events, string Snapshot) reverse = RunDesignationDeathRace(
            deathTarget,
            deathFirst,
            reverseSubmission: true);

        Assert.Equal(forward, reverse);
        CampaignEvent[] events = JsonSerializer.Deserialize<CampaignEvent[]>(
            forward.Events,
            SimulationJson.CreateOptions())!;
        if (deathFirst)
        {
            Assert.Contains(events, item => item.Payload is CommandCancelledEventPayload);
        }
        else
        {
            Assert.DoesNotContain(events, item => item.Payload is CommandCancelledEventPayload);
            WorldSnapshot snapshot = JsonSerializer.Deserialize<WorldSnapshot>(
                forward.Snapshot,
                SimulationJson.CreateOptions())!;
            Assert.Single(snapshot.CharacterSuccessions.Designations);
        }
    }

    [Fact]
    public void F406_LaterHeirAndDesignatorDeathsDoNotSilentlyConsumeDesignationEvidence()
    {
        CampaignSimulation simulation = CreateSimulation();
        HeirDesignationState designation = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                "death-preservation",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;

        ResolveDeath(simulation, FirstHeir, "heir");
        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? afterHeirDeath));
        Assert.Equal(designation, afterHeirDeath);

        ResolveDeath(simulation, Designator, "designator");
        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? afterDesignatorDeath));
        Assert.Equal(designation, afterDesignatorDeath);
        Assert.Equal(
            Serialize(designation),
            Serialize(Assert.Single(
                simulation.World.CharacterSuccessions.GetDesignationRecordsInvolving(
                    Designator))));
    }

    [Fact]
    public void F407_SnapshotIsCanonicalChecksumCoveredDefensiveAndRestorable()
    {
        CampaignSimulation simulation = CreateSimulation();
        HeirDesignationState first = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                "checksum-a",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        _ = ResolveSuccession(
            simulation,
            "checksum-b",
            new DesignateHeirAction(SecondHeir, first.DesignationId));
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        CharacterSuccessionWorldSnapshot succession = snapshot.CharacterSuccessions;
        WorldSnapshot shuffled = snapshot with
        {
            CharacterSuccessions = succession with
            {
                Designations = succession.Designations.Reverse().ToArray(),
                History = succession.History.Reverse().ToArray(),
            },
        };

        Assert.Equal(checksum, SimulationChecksum.Compute(shuffled));
        WorldState restored = WorldState.Restore(shuffled);
        Assert.Equal(checksum, SimulationChecksum.Compute(restored.CaptureSnapshot()));
        HeirDesignationState[] leaked = Assert.IsType<HeirDesignationState[]>(
            restored.CharacterSuccessions.Designations);
        Array.Reverse(leaked);
        Assert.Equal(
            succession.Canonicalize().Designations,
            restored.CharacterSuccessions.Designations);

        HeirDesignationState changed = succession.Designations[0] with
        {
            HeirCharacterId = succession.Designations[0].HeirCharacterId == FirstHeir
                ? SecondHeir
                : FirstHeir,
        };
        WorldSnapshot tampered = snapshot with
        {
            CharacterSuccessions = succession with
            {
                Designations = succession.Designations
                    .Select(item => item.DesignationId == changed.DesignationId
                        ? changed
                        : item)
                    .ToArray(),
            },
        };
        Assert.NotEqual(checksum, SimulationChecksum.Compute(tampered));
        Assert.Throws<SimulationValidationException>(() => WorldState.Restore(tampered));
    }

    [Fact]
    public void F407_TamperedOutcomeOrAffectedIdsCannotPartiallyApply()
    {
        CampaignSimulation simulation = CreateSimulation();
        EntityId commandId = new("command:test/f4-direct-event");
        EntityId eventId = CharacterSuccessionIds.DeriveActionEventId(Date, commandId);
        CharacterSuccessionActionCommandPayload commandPayload = new(
            new DesignateHeirAction(FirstHeir, null));
        CharacterSuccessionActionResolvedEventPayload payload =
            simulation.World.CharacterSuccessions.PlanAction(
                Designator,
                commandPayload,
                Date,
                0,
                commandId,
                eventId);
        CampaignEvent valid = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterSuccessionActionAffectedIds(payload),
            payload);
        string before = Serialize(simulation.World.CaptureSnapshot());
        HeirDesignatedOutcome designated = Assert.IsType<HeirDesignatedOutcome>(
            payload.Outcome);
        CharacterSuccessionActionResolvedEventPayload tamperedPayload = payload with
        {
            Outcome = designated with
            {
                CurrentDesignation = designated.CurrentDesignation with
                {
                    EstablishedTurnIndex = 1,
                },
            },
        };
        CampaignEvent tamperedOutcome = valid with { Payload = tamperedPayload };
        CampaignEvent tamperedAffected = valid with
        {
            AffectedIds = valid.AffectedIds.Skip(1).ToArray(),
        };
        CharacterSuccessionActionResolvedEventPayload nullNestedPayload = payload with
        {
            Outcome = new HeirDesignatedOutcome(null!),
        };
        CampaignEvent nullNested = valid with { Payload = nullNestedPayload };

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(tamperedOutcome));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(tamperedAffected));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(nullNested));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));

        simulation.World.Apply(valid);
        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? current));
        Assert.Equal(designated.CurrentDesignation, current);
    }

    [Fact]
    public void F408_TerminalHistoryIsBoundedAndFoldedDeterministically()
    {
        CampaignSimulation simulation = CreateSimulation();
        int terminalCount = CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter + 3;
        HeirDesignationState active = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                "bounded-initial",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        List<HeirDesignationState> terminal = [];
        for (int index = 0; index < terminalCount; index++)
        {
            if (index % 2 == 0)
            {
                EntityId heir = active.HeirCharacterId == FirstHeir ? SecondHeir : FirstHeir;
                HeirDesignationReplacedOutcome replacement = Assert.IsType<
                    HeirDesignationReplacedOutcome>(ResolveSuccession(
                        simulation,
                        $"bounded-replace-{index}",
                        new DesignateHeirAction(heir, active.DesignationId)).Outcome);
                terminal.Add(replacement.PreviousDesignation);
                active = replacement.CurrentDesignation;
            }
            else
            {
                HeirDesignationRevokedOutcome revocation = Assert.IsType<
                    HeirDesignationRevokedOutcome>(ResolveSuccession(
                        simulation,
                        $"bounded-revoke-{index}",
                        new RevokeHeirDesignationAction(active.DesignationId)).Outcome);
                terminal.Add(revocation.PreviousDesignation);
                if (index + 1 < terminalCount)
                {
                    active = Assert.IsType<HeirDesignatedOutcome>(ResolveSuccession(
                        simulation,
                        $"bounded-redesignate-{index}",
                        new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
                }
            }
        }

        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
            Designator,
            out HeirDesignationState? retainedActive));
        Assert.Equal(active, retainedActive);
        Assert.Equal(
            CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter,
            simulation.World.CharacterSuccessions.Designations.Count(
                item => item.Status != HeirDesignationStatus.Active));
        HeirDesignationState[] expectedRetained = terminal.Skip(3)
            .OrderBy(item => item.DesignationId)
            .ToArray();
        Assert.Equal(
            expectedRetained,
            simulation.World.CharacterSuccessions.Designations
                .Where(item => item.Status != HeirDesignationStatus.Active)
                .OrderBy(item => item.DesignationId));
        Assert.True(simulation.World.CharacterSuccessions.TryGetHistory(
            Designator,
            out HeirDesignationHistoryAggregate? history));
        Assert.Equal(2, history.FoldedReplacedCount);
        Assert.Equal(1, history.FoldedRevokedCount);
        Assert.Equal(3, history.TotalFoldedCount);
        Assert.Equal(
            terminal.Take(3).Min(item => item.ResolutionDate!.Value),
            history.EarliestDate);
        Assert.Equal(
            terminal.Take(3).Max(item => item.ResolutionDate!.Value),
            history.LatestDate);
        Assert.Equal(
            SimulationChecksum.Compute(simulation.World.CaptureSnapshot()),
            SimulationChecksum.Compute(WorldState.Restore(
                simulation.World.CaptureSnapshot()).CaptureSnapshot()));
    }

    [Fact]
    public void F408_CrossCategoryFoldOverflowRejectsAtomicallyBeforeResolution()
    {
        CampaignSimulation simulation = CreateSimulation();
        for (int index = 0;
             index < CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter;
             index++)
        {
            EntityId heir = index % 2 == 0 ? FirstHeir : SecondHeir;
            HeirDesignationState active = Assert.IsType<HeirDesignatedOutcome>(
                ResolveSuccession(
                    simulation,
                    $"overflow-designate-{index}",
                    new DesignateHeirAction(heir, null)).Outcome).CurrentDesignation;
            _ = ResolveSuccession(
                simulation,
                $"overflow-revoke-{index}",
                new RevokeHeirDesignationAction(active.DesignationId));
        }

        HeirDesignationState current = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                "overflow-current",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        CampaignDate oldestResolution = snapshot.CharacterSuccessions.Designations
            .Where(item => item.Status != HeirDesignationStatus.Active)
            .Min(item => item.ResolutionDate!.Value);
        WorldSnapshot nearCapacity = snapshot with
        {
            CharacterSuccessions = snapshot.CharacterSuccessions with
            {
                History =
                [
                    new HeirDesignationHistoryAggregate(
                        CharacterSuccessionContractVersions.State,
                        Designator,
                        long.MaxValue,
                        0,
                        oldestResolution,
                        oldestResolution),
                ],
            },
        };
        CampaignSimulation restored = new(WorldState.Restore(nearCapacity));
        string before = Serialize(restored.World.CaptureSnapshot());
        CommandValidationResult result = restored.Submit(CampaignCommand.Create(
            new EntityId("command:test/f4-overflow-revoke"),
            Designator,
            restored.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(
                new RevokeHeirDesignationAction(current.DesignationId))));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "heir_designation_history_overflow");
        Assert.Equal(before, Serialize(restored.World.CaptureSnapshot()));
    }

    [Theory]
    [InlineData("history-without-retention")]
    [InlineData("history-newer-than-retention")]
    [InlineData("orphan-replaced")]
    [InlineData("same-event-terminal")]
    public void F408_ImpossibleRestoredLifecycleOrHistoryFailsClosed(string scenario)
    {
        CampaignSimulation simulation = CreateSimulation();
        HeirDesignationState initial = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                $"restore-{scenario}",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        if (scenario.StartsWith("history", StringComparison.Ordinal))
        {
            HeirDesignationRevokedOutcome revoked = Assert.IsType<
                HeirDesignationRevokedOutcome>(ResolveSuccession(
                    simulation,
                    $"restore-revoke-{scenario}",
                    new RevokeHeirDesignationAction(initial.DesignationId)).Outcome);
            if (scenario == "history-newer-than-retention")
            {
                for (int index = 1;
                     index < CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter;
                     index++)
                {
                    EntityId heir = index % 2 == 0 ? FirstHeir : SecondHeir;
                    HeirDesignationState active = Assert.IsType<HeirDesignatedOutcome>(
                        ResolveSuccession(
                            simulation,
                            $"restore-history-designate-{index}",
                            new DesignateHeirAction(heir, null)).Outcome).CurrentDesignation;
                    _ = ResolveSuccession(
                        simulation,
                        $"restore-history-revoke-{index}",
                        new RevokeHeirDesignationAction(active.DesignationId));
                }
            }

            snapshot = simulation.World.CaptureSnapshot();
            CampaignDate aggregateDate = scenario == "history-newer-than-retention"
                ? snapshot.CharacterSuccessions.Designations
                    .Where(item => item.Status != HeirDesignationStatus.Active)
                    .Min(item => item.ResolutionDate!.Value)
                    .AddDays(1)
                : revoked.PreviousDesignation.ResolutionDate!.Value;
            snapshot = snapshot with
            {
                CharacterSuccessions = snapshot.CharacterSuccessions with
                {
                    History =
                    [
                        new HeirDesignationHistoryAggregate(
                            CharacterSuccessionContractVersions.State,
                            Designator,
                            0,
                            1,
                            aggregateDate,
                            aggregateDate),
                    ],
                },
            };
        }
        else
        {
            EntityId resolutionCommand = scenario == "same-event-terminal"
                ? initial.SourceCommandId
                : new EntityId($"command:test/f4-orphan-{scenario}");
            EntityId resolutionEvent = scenario == "same-event-terminal"
                ? initial.SourceEventId
                : CharacterSuccessionIds.DeriveActionEventId(
                    snapshot.Calendar.Date,
                    resolutionCommand);
            HeirDesignationState terminal = initial with
            {
                Status = HeirDesignationStatus.Replaced,
                ResolutionDate = scenario == "same-event-terminal"
                    ? initial.EstablishedDate
                    : snapshot.Calendar.Date,
                ResolutionTurnIndex = scenario == "same-event-terminal"
                    ? initial.EstablishedTurnIndex
                    : snapshot.Calendar.TurnIndex,
                ResolutionCommandId = resolutionCommand,
                ResolutionEventId = resolutionEvent,
            };
            snapshot = snapshot with
            {
                CharacterSuccessions = snapshot.CharacterSuccessions with
                {
                    Designations = [terminal],
                },
            };
        }

        Assert.Throws<SimulationValidationException>(() => WorldState.Restore(snapshot));
    }

    [Fact]
    public void F408_SameDayChainFoldsOnlyItsRootAndRemainsRestorable()
    {
        WorldSnapshot snapshot = SameDayReplacementChainSnapshot(
            CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter);
        HeirDesignationState root = snapshot.CharacterSuccessions.Designations[0];
        HeirDesignationState active = snapshot.CharacterSuccessions.Designations[^1];
        CampaignSimulation simulation = new(WorldState.Restore(snapshot));
        EntityId nextHeir = active.HeirCharacterId == FirstHeir
            ? SecondHeir
            : FirstHeir;
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/f4-same-day-chain-next"),
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(nextHeir, active.DesignationId)));

        Assert.True(simulation.Submit(command).IsValid);
        _ = Assert.IsType<CharacterSuccessionActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);

        Assert.DoesNotContain(
            simulation.World.CharacterSuccessions.Designations,
            item => item.DesignationId == root.DesignationId);
        Assert.All(
            snapshot.CharacterSuccessions.Designations.Skip(1),
            item => Assert.Contains(
                simulation.World.CharacterSuccessions.Designations,
                retained => retained.DesignationId == item.DesignationId));
        Assert.True(simulation.World.CharacterSuccessions.TryGetHistory(
            Designator,
            out HeirDesignationHistoryAggregate? history));
        Assert.Equal(1, history.FoldedReplacedCount);
        _ = WorldState.Restore(simulation.World.CaptureSnapshot());
    }

    [Theory]
    [InlineData("merge")]
    [InlineData("cycle")]
    public void F408_RestoredLifecycleRejectsMergeOrCycleTopology(string scenario)
    {
        WorldSnapshot snapshot = SameDayReplacementChainSnapshot(2);
        HeirDesignationState[] records = snapshot.CharacterSuccessions.Designations.ToArray();
        if (scenario == "merge")
        {
            HeirDesignationState terminal = records[0];
            HeirDesignationState sharedSuccessor = records[2];
            records[0] = terminal with
            {
                ResolutionDate = sharedSuccessor.EstablishedDate,
                ResolutionTurnIndex = sharedSuccessor.EstablishedTurnIndex,
                ResolutionCommandId = sharedSuccessor.SourceCommandId,
                ResolutionEventId = sharedSuccessor.SourceEventId,
            };
        }
        else
        {
            HeirDesignationState first = records[0];
            records =
            [
                first,
                records[1] with
                {
                    Status = HeirDesignationStatus.Replaced,
                    ResolutionDate = first.EstablishedDate,
                    ResolutionTurnIndex = first.EstablishedTurnIndex,
                    ResolutionCommandId = first.SourceCommandId,
                    ResolutionEventId = first.SourceEventId,
                },
            ];
        }

        snapshot = snapshot with
        {
            CharacterSuccessions = snapshot.CharacterSuccessions with
            {
                Designations = records,
            },
        };
        Assert.Throws<SimulationValidationException>(() => WorldState.Restore(snapshot));
    }

    [Theory]
    [InlineData("duplicate-source")]
    [InlineData("duplicate-resolution")]
    [InlineData("revoked-spawns-successor")]
    [InlineData("duplicate-source-cross")]
    [InlineData("duplicate-resolution-cross")]
    [InlineData("revoked-spawns-successor-cross")]
    public void F408_RestoredLifecycleRejectsImpossibleEventRoleCardinality(
        string scenario)
    {
        WorldSnapshot snapshot = CreateSimulation().World.CaptureSnapshot();
        EntityId sourceA = new("command:test/f4-event-role-source-a");
        EntityId sourceB = scenario.StartsWith("duplicate-source", StringComparison.Ordinal)
            ? sourceA
            : new EntityId("command:test/f4-event-role-source-b");
        EntityId sourceEventA = CharacterSuccessionIds.DeriveActionEventId(Date, sourceA);
        EntityId sourceEventB = CharacterSuccessionIds.DeriveActionEventId(Date, sourceB);
        EntityId resolutionA = new("command:test/f4-event-role-resolution-a");
        EntityId resolutionB = scenario.StartsWith(
            "duplicate-resolution",
            StringComparison.Ordinal)
            ? resolutionA
            : new EntityId("command:test/f4-event-role-resolution-b");
        EntityId resolutionEventA = CharacterSuccessionIds.DeriveActionEventId(
            Date,
            resolutionA);
        EntityId resolutionEventB = CharacterSuccessionIds.DeriveActionEventId(
            Date,
            resolutionB);
        if (scenario.StartsWith("revoked-spawns-successor", StringComparison.Ordinal))
        {
            resolutionA = sourceB;
            resolutionEventA = sourceEventB;
        }

        HeirDesignationState first = TerminalDesignation(
            Designator,
            FirstHeir,
            sourceA,
            sourceEventA,
            resolutionA,
            resolutionEventA);
        EntityId secondDesignator = scenario.EndsWith("-cross", StringComparison.Ordinal)
            ? ThirdHeir
            : Designator;
        HeirDesignationState second = scenario.StartsWith(
                "revoked-spawns-successor",
                StringComparison.Ordinal)
            ? ActiveDesignation(secondDesignator, SecondHeir, sourceB, sourceEventB)
            : TerminalDesignation(
                secondDesignator,
                SecondHeir,
                sourceB,
                sourceEventB,
                resolutionB,
                resolutionEventB);
        snapshot = snapshot with
        {
            CharacterSuccessions = new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                [first, second],
                []),
        };

        Assert.Throws<SimulationValidationException>(() => WorldState.Restore(snapshot));
    }

    private static (string Events, string Snapshot) RunConcurrent(
        CampaignCommand first,
        CampaignCommand second)
    {
        CampaignSimulation simulation = CreateSimulation();
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);
        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        return (Serialize(events), Serialize(simulation.World.CaptureSnapshot()));
    }

    private static WorldSnapshot SameDayReplacementChainSnapshot(int replacementCount)
    {
        WorldSnapshot snapshot = CreateSimulation().World.CaptureSnapshot();
        EntityId[] heirs = [FirstHeir, SecondHeir, ThirdHeir];
        EntityId[] commandIds = Enumerable.Range(0, replacementCount + 1)
            .Select(index => new EntityId($"command:test/f4-same-day-chain-{index}"))
            .ToArray();
        EntityId[] eventIds = commandIds
            .Select(commandId => CharacterSuccessionIds.DeriveActionEventId(Date, commandId))
            .ToArray();
        HeirDesignationState[] records = Enumerable.Range(0, replacementCount + 1)
            .Select(index => new HeirDesignationState(
                CharacterSuccessionContractVersions.State,
                CharacterSuccessionIds.DeriveDesignationId(
                    eventIds[index],
                    Designator,
                    heirs[index % heirs.Length]),
                Designator,
                heirs[index % heirs.Length],
                Date,
                0,
                commandIds[index],
                eventIds[index],
                index < replacementCount
                    ? HeirDesignationStatus.Replaced
                    : HeirDesignationStatus.Active,
                index < replacementCount ? Date : null,
                index < replacementCount ? 0 : null,
                index < replacementCount ? commandIds[index + 1] : null,
                index < replacementCount ? eventIds[index + 1] : null))
            .ToArray();
        return snapshot with
        {
            CharacterSuccessions = new CharacterSuccessionWorldSnapshot(
                CharacterSuccessionContractVersions.Snapshot,
                records,
                []),
        };
    }

    private static HeirDesignationState ActiveDesignation(
        EntityId designator,
        EntityId heir,
        EntityId sourceCommand,
        EntityId sourceEvent) => new(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(
                sourceEvent,
                designator,
                heir),
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

    private static HeirDesignationState TerminalDesignation(
        EntityId designator,
        EntityId heir,
        EntityId sourceCommand,
        EntityId sourceEvent,
        EntityId resolutionCommand,
        EntityId resolutionEvent) => ActiveDesignation(
            designator,
            heir,
            sourceCommand,
            sourceEvent) with
        {
            Status = HeirDesignationStatus.Revoked,
            ResolutionDate = Date,
            ResolutionTurnIndex = 0,
            ResolutionCommandId = resolutionCommand,
            ResolutionEventId = resolutionEvent,
        };

    private static (string Events, string Snapshot) RunConcurrentTerminalConflict(
        string scenario,
        int firstPriority,
        int secondPriority,
        bool reverseSubmission)
    {
        CampaignSimulation simulation = CreateSimulation();
        HeirDesignationState current = Assert.IsType<HeirDesignatedOutcome>(
            ResolveSuccession(
                simulation,
                $"conflict-initial-{scenario}",
                new DesignateHeirAction(FirstHeir, null)).Outcome).CurrentDesignation;
        ICharacterSuccessionAction firstAction = scenario == "revoke-revoke"
            ? new RevokeHeirDesignationAction(current.DesignationId)
            : new DesignateHeirAction(SecondHeir, current.DesignationId);
        ICharacterSuccessionAction secondAction = scenario == "replacement-replacement"
            ? new DesignateHeirAction(ThirdHeir, current.DesignationId)
            : new RevokeHeirDesignationAction(current.DesignationId);
        CampaignCommand first = CampaignCommand.Create(
            new EntityId($"command:test/f4-conflict-a-{scenario}"),
            Designator,
            simulation.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(firstAction),
            priority: firstPriority);
        CampaignCommand second = CampaignCommand.Create(
            new EntityId($"command:test/f4-conflict-b-{scenario}"),
            Designator,
            simulation.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(secondAction),
            priority: secondPriority);
        CampaignCommand[] submission = reverseSubmission ? [second, first] : [first, second];
        Assert.All(submission, command => Assert.True(simulation.Submit(command).IsValid));
        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        return (Serialize(events), Serialize(simulation.World.CaptureSnapshot()));
    }

    private static (string Events, string Snapshot) RunDesignationDeathRace(
        string deathTarget,
        bool deathFirst,
        bool reverseSubmission)
    {
        CampaignSimulation simulation = CreateSimulation();
        EntityId target = deathTarget == "designator" ? Designator : FirstHeir;
        AuthoritativeCharacterProfile profile = simulation.World.Characters.Profiles.Single(
            item => item.CharacterId == target);
        string order = deathFirst ? "death-first" : "designation-first";
        CampaignCommand designation = CampaignCommand.Create(
            new EntityId($"command:test/f4-race-designate-{deathTarget}-{order}"),
            Designator,
            Date,
            new CharacterSuccessionActionCommandPayload(
                new DesignateHeirAction(FirstHeir, null)),
            priority: deathFirst ? 5 : -5);
        CampaignCommand death = CampaignCommand.Create(
            new EntityId($"command:test/f4-race-death-{deathTarget}-{order}"),
            CharacterConditionSystem.AuthoritativeActorId,
            Date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterDeathAction(target, profile.Condition)),
            priority: deathFirst ? -5 : 5);
        CampaignCommand[] submission = reverseSubmission
            ? [death, designation]
            : [designation, death];
        Assert.All(submission, command => Assert.True(simulation.Submit(command).IsValid));
        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        return (Serialize(events), Serialize(simulation.World.CaptureSnapshot()));
    }

    private static CharacterSuccessionActionResolvedEventPayload ResolveSuccession(
        CampaignSimulation simulation,
        string suffix,
        ICharacterSuccessionAction action)
    {
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/f4-{suffix}"),
            Designator,
            simulation.World.Calendar.Date,
            new CharacterSuccessionActionCommandPayload(action));
        Assert.True(simulation.Submit(command).IsValid);
        return Assert.IsType<CharacterSuccessionActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
    }

    private static void ResolveDeath(
        CampaignSimulation simulation,
        EntityId characterId,
        string suffix)
    {
        AuthoritativeCharacterProfile profile = simulation.World.Characters.Profiles.Single(
            item => item.CharacterId == characterId);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/f4-death-{suffix}"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterDeathAction(characterId, profile.Condition)));
        Assert.True(simulation.Submit(command).IsValid);
        _ = Assert.IsType<CharacterConditionActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
    }

    private static CampaignSimulation CreateSimulation(
        CharacterConditionState? designatorCondition = null,
        CharacterConditionState? firstHeirCondition = null,
        CampaignDate? firstHeirBirthDate = null,
        CampaignDate? designatorBirthDate = null)
    {
        (EntityId Id, CampaignDate BirthDate, CharacterConditionState Condition)[] inputs =
        [
            (Designator, designatorBirthDate ?? new CampaignDate(170, 1, 1),
                designatorCondition ?? CharacterConditionState.Default),
            (FirstHeir, firstHeirBirthDate ?? new CampaignDate(170, 1, 1),
                firstHeirCondition ?? CharacterConditionState.Default),
            (SecondHeir, new CampaignDate(170, 1, 1), CharacterConditionState.Default),
            (ThirdHeir, new CampaignDate(170, 1, 1), CharacterConditionState.Default),
        ];
        CharacterDefinition[] definitions = inputs.Select(input =>
        {
            EntityId nameKey = new($"loc:{input.Id.Value.Replace(':', '/')}");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                input.Id,
                nameKey,
                input.BirthDate,
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(nameKey, null),
                CharacterContentOrigin.LegacyUnknown(input.Id),
                null,
                null,
                []);
        }).ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            inputs.Select(input => new CharacterState(
                CharacterContractVersions.State,
                input.Id,
                [],
                [],
                input.Condition)).ToArray(),
            [],
            []);
        WorldState world = WorldState.Create(
            Date,
            44,
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
            CharacterSuccessionWorldSnapshot.Empty);
        return new CampaignSimulation(world);
    }

    private static CharacterConditionState DeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        CustodianId: null);

    private static EntityId Character(string suffix) => new($"character:test/f4-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());
}
