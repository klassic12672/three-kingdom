using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterGuardianshipCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Guardian = new("character:test/guardian");
    private static readonly EntityId OtherGuardian = new("character:test/other_guardian");
    private static readonly EntityId Ward = new("character:test/ward");
    private static readonly EntityId OtherWard = new("character:test/other_ward");
    private readonly ITestOutputHelper output;

    public CharacterGuardianshipCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void CampaignGuardianship_EnforcesAuthorityAndPhaseAndMutatesOnlyGuardianships()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        Assert.False(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-unauthorized"),
            Guardian,
            Ward,
            issuingActor: Guardian)).IsValid);
        Assert.False(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-wrong-phase"),
            Guardian,
            Ward,
            phase: ResolutionPhase.Systems)).IsValid);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-success"),
            Guardian,
            Ward);

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload payload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload);
        PrimaryGuardianshipEstablishedOutcome outcome = Assert.IsType<
            PrimaryGuardianshipEstablishedOutcome>(payload.Outcome);
        CharacterGuardianshipState guardianship = outcome.Guardianship;

        Assert.Equal(
            CharacterFamilyIds.DeriveActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(
            CharacterGuardianshipIds.DeriveGuardianshipId(
                campaignEvent.EventId,
                Ward,
                Guardian),
            guardianship.GuardianshipId);
        Assert.Equal(
            new EntityId[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                guardianship.GuardianshipId,
                Guardian,
                Ward,
            }.Distinct().Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        Assert.True(simulation.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(
                Ward,
                out CharacterGuardianshipState? active));
        Assert.Equal(guardianship, active);

        WorldSnapshot after = simulation.World.CaptureSnapshot();
        Assert.Equal(Serialize(before.Characters), Serialize(after.Characters));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
    }

    [Fact]
    public void CampaignGuardianship_TamperedOutcomeRollsBackAtomically()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        EntityId commandId = new("command:test/guardianship-tampered");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new EstablishPrimaryGuardianshipAction(Guardian, Ward, null)),
            Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId);
        PrimaryGuardianshipEstablishedOutcome outcome = Assert.IsType<
            PrimaryGuardianshipEstablishedOutcome>(plan.ResolvedPayload.Outcome);
        CharacterFamilyActionResolvedEventPayload tamperedPayload = plan.ResolvedPayload with
        {
            Outcome = outcome with
            {
                Guardianship = outcome.Guardianship with
                {
                    Status = CharacterGuardianshipStatus.Ended,
                },
            },
        };
        CampaignEvent tampered = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(tamperedPayload),
            tamperedPayload);
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(tampered));

        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(simulation.World.CharacterGuardianships.Guardianships);
    }

    [Fact]
    public void EventlessTurnAdvancesGuardianshipCalendarAndRejectsStalePlanAndEvent()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        EntityId commandId = new("command:test/guardianship-before-advance");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan stalePlan =
            simulation.World.PrepareCharacterFamilyAction(
                CharacterFamilySystem.AuthoritativeActorId,
                new CharacterFamilyActionCommandPayload(
                    new EstablishPrimaryGuardianshipAction(Guardian, Ward, null)),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId);
        CampaignEvent staleEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(stalePlan.ResolvedPayload),
            stalePlan.ResolvedPayload);

        Assert.Empty(simulation.ResolveTurn());
        Assert.True(simulation.World.Calendar.Date.CompareTo(Date) > 0);
        string afterAdvance = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.PrepareCharacterFamilyAction(
                CharacterFamilySystem.AuthoritativeActorId,
                new CharacterFamilyActionCommandPayload(
                    new EstablishPrimaryGuardianshipAction(Guardian, Ward, null)),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(staleEvent));
        Assert.Equal(afterAdvance, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(simulation.World.CharacterGuardianships.Guardianships);
    }

    [Fact]
    public void PendingGuardianship_SaveLoadReplayIsDeterministic()
    {
        CampaignSimulation original = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CampaignCommand command = GuardianshipCommand(
            original,
            new EntityId("command:test/guardianship-pending-save"),
            Guardian,
            Ward);
        Assert.True(original.Submit(command).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-guardianship-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "guardianship-pending.save.gz");
            new SaveStore().SaveAtomic(path, SaveEnvelope.Create("test", [], original));
            SaveEnvelope loaded = new SaveStore().Load(path);
            CampaignSimulation replay = new(WorldState.Restore(loaded.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.IsType<EstablishPrimaryGuardianshipAction>(Assert.IsType<
                CharacterFamilyActionCommandPayload>(
                    Assert.Single(loaded.Snapshot.PendingCommands).Payload).Action);
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SameTurnTwoGuardianRaceUsesEventIdOrderRegardlessOfSubmissionOrder()
    {
        (EntityId earlier, EntityId later) = OrderedCommandIds();
        for (int scenario = 0; scenario < 2; scenario++)
        {
            CampaignSimulation simulation = CreateCampaign(
                Seed(Guardian, new CampaignDate(160, 1, 1)),
                Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
                Seed(Ward, new CampaignDate(190, 1, 1)));
            EntityId earlierGuardian = scenario == 0 ? Guardian : OtherGuardian;
            EntityId laterGuardian = scenario == 0 ? OtherGuardian : Guardian;
            Assert.True(simulation.Submit(GuardianshipCommand(
                simulation,
                later,
                laterGuardian,
                Ward)).IsValid);
            Assert.True(simulation.Submit(GuardianshipCommand(
                simulation,
                earlier,
                earlierGuardian,
                Ward)).IsValid);

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

            Assert.Equal(2, events.Count);
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(events[0].Payload);
            Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
            Assert.True(simulation.World.CharacterGuardianships
                .TryGetActivePrimaryGuardianshipForWard(
                    Ward,
                    out CharacterGuardianshipState? active));
            Assert.Equal(earlierGuardian, active.GuardianCharacterId);
        }
    }

    [Fact]
    public void GuardianshipLifecycle_UsesExactAffectedIdsRoundTripsAndMutatesOnlyGuardianships()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        Assert.True(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/lifecycle-establish"),
            Guardian,
            Ward)).IsValid);
        Assert.Single(simulation.ResolveTurn());
        Assert.True(simulation.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(
                Ward,
                out CharacterGuardianshipState? active));
        ReplacePrimaryGuardianshipAction replaceAction = new(
            Ward,
            active.GuardianshipId,
            OtherGuardian);
        Assert.False(simulation.Submit(FamilyCommand(
            simulation,
            new EntityId("command:test/lifecycle-unauthorized"),
            replaceAction,
            issuingActor: Guardian)).IsValid);
        Assert.False(simulation.Submit(FamilyCommand(
            simulation,
            new EntityId("command:test/lifecycle-wrong-phase"),
            replaceAction,
            phase: ResolutionPhase.Systems)).IsValid);
        WorldSnapshot beforeReplacement = simulation.World.CaptureSnapshot();
        CampaignCommand replaceCommand = FamilyCommand(
            simulation,
            new EntityId("command:test/lifecycle-replace"),
            replaceAction);

        Assert.True(simulation.Submit(replaceCommand).IsValid);
        CampaignEvent replacementEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload replacementPayload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(replacementEvent.Payload);
        PrimaryGuardianshipReplacedOutcome replacementOutcome = Assert.IsType<
            PrimaryGuardianshipReplacedOutcome>(replacementPayload.Outcome);

        Assert.Equal(
            CharacterGuardianshipEndReason.Replaced,
            replacementOutcome.EndedGuardianship.EndReason);
        Assert.Equal(
            OtherGuardian,
            replacementOutcome.ReplacementGuardianship.GuardianCharacterId);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(replacementPayload),
            replacementEvent.AffectedIds);
        Assert.Equal(
            new EntityId[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                active.GuardianshipId,
                replacementOutcome.ReplacementGuardianship.GuardianshipId,
                Guardian,
                OtherGuardian,
                Ward,
            }.Distinct().Order(),
            replacementEvent.AffectedIds);
        Assert.IsType<ReplacePrimaryGuardianshipAction>(Assert.IsType<
            CharacterFamilyActionCommandPayload>(JsonSerializer.Deserialize<CampaignCommand>(
                Serialize(replaceCommand),
                SimulationJson.CreateOptions())!.Payload).Action);
        Assert.IsType<PrimaryGuardianshipReplacedOutcome>(Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(JsonSerializer.Deserialize<CampaignEvent>(
                Serialize(replacementEvent),
                SimulationJson.CreateOptions())!.Payload).Outcome);
        AssertOnlyGuardianshipSubsystemChanged(
            beforeReplacement,
            simulation.World.CaptureSnapshot());

        EndPrimaryGuardianshipAction endAction = new(
            Ward,
            replacementOutcome.ReplacementGuardianship.GuardianshipId,
            CharacterGuardianshipEndReason.Revoked);
        WorldSnapshot beforeEnd = simulation.World.CaptureSnapshot();
        CampaignCommand endCommand = FamilyCommand(
            simulation,
            new EntityId("command:test/lifecycle-end"),
            endAction);
        Assert.True(simulation.Submit(endCommand).IsValid);
        CampaignEvent endedEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload endedPayload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(endedEvent.Payload);
        PrimaryGuardianshipEndedOutcome endedOutcome = Assert.IsType<
            PrimaryGuardianshipEndedOutcome>(endedPayload.Outcome);

        Assert.Equal(CharacterGuardianshipStatus.Ended, endedOutcome.EndedGuardianship.Status);
        Assert.Equal(CharacterGuardianshipEndReason.Revoked, endedOutcome.EndedGuardianship.EndReason);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(endedPayload),
            endedEvent.AffectedIds);
        Assert.IsType<EndPrimaryGuardianshipAction>(Assert.IsType<
            CharacterFamilyActionCommandPayload>(JsonSerializer.Deserialize<CampaignCommand>(
                Serialize(endCommand),
                SimulationJson.CreateOptions())!.Payload).Action);
        Assert.IsType<PrimaryGuardianshipEndedOutcome>(Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(JsonSerializer.Deserialize<CampaignEvent>(
                Serialize(endedEvent),
                SimulationJson.CreateOptions())!.Payload).Outcome);
        Assert.False(simulation.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        AssertOnlyGuardianshipSubsystemChanged(beforeEnd, simulation.World.CaptureSnapshot());
    }

    [Fact]
    public void GuardianshipLifecycle_PinsExactNestedJsonDiscriminatorsAndProperties()
    {
        CharacterGuardianshipState active = ActiveGuardianship(
            Ward,
            Guardian,
            "lifecycle-json-active");
        CampaignSimulation endSimulation = CreateCampaign(
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [active]),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        EndPrimaryGuardianshipAction endAction = new(
            Ward,
            active.GuardianshipId,
            CharacterGuardianshipEndReason.Revoked);
        (CampaignCommand endCommand, CampaignEvent endEvent) = PlanLifecycleEvent(
            endSimulation,
            new EntityId("command:test/lifecycle-json-end"),
            endAction);

        AssertNestedJsonContract(
            endCommand,
            endEvent,
            "end_primary_guardianship.v1",
            ["$type", "endReason", "expectedCurrentPrimaryGuardianshipId", "wardCharacterId"],
            "primary_guardianship_ended.v1",
            ["$type", "endedGuardianship"]);

        CampaignSimulation replaceSimulation = CreateCampaign(
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [active]),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        ReplacePrimaryGuardianshipAction replaceAction = new(
            Ward,
            active.GuardianshipId,
            OtherGuardian);
        (CampaignCommand replaceCommand, CampaignEvent replaceEvent) = PlanLifecycleEvent(
            replaceSimulation,
            new EntityId("command:test/lifecycle-json-replace"),
            replaceAction);

        AssertNestedJsonContract(
            replaceCommand,
            replaceEvent,
            "replace_primary_guardianship.v1",
            [
                "$type",
                "expectedCurrentPrimaryGuardianshipId",
                "replacementGuardianCharacterId",
                "wardCharacterId",
            ],
            "primary_guardianship_replaced.v1",
            ["$type", "endedGuardianship", "replacementGuardianship"]);
    }

    [Fact]
    public void GuardianshipLifecycle_TamperingAffectedIdsAndStaleReplayRollBack()
    {
        CharacterGuardianshipState active = ActiveGuardianship(
            Ward,
            Guardian,
            "lifecycle-tamper-active");
        CampaignSimulation simulation = CreateCampaign(
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [active]),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        EntityId commandId = new("command:test/lifecycle-tamper");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new ReplacePrimaryGuardianshipAction(
                    Ward,
                    active.GuardianshipId,
                    OtherGuardian)),
            Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId);
        PrimaryGuardianshipReplacedOutcome outcome = Assert.IsType<
            PrimaryGuardianshipReplacedOutcome>(plan.ResolvedPayload.Outcome);
        CharacterFamilyActionResolvedEventPayload tamperedPayload = plan.ResolvedPayload with
        {
            Outcome = outcome with
            {
                ReplacementGuardianship = outcome.ReplacementGuardianship with
                {
                    EstablishedTurnIndex = outcome.ReplacementGuardianship.EstablishedTurnIndex + 1,
                },
            },
        };
        CampaignEvent tampered = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(tamperedPayload),
            tamperedPayload);
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(tampered));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));

        CampaignEvent wrongAffectedIds = tampered with
        {
            AffectedIds = [],
            Payload = plan.ResolvedPayload,
        };
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(wrongAffectedIds));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));

        CampaignEvent exact = wrongAffectedIds with
        {
            AffectedIds = WorldState.GetCharacterFamilyActionAffectedIds(
                plan.ResolvedPayload),
        };
        simulation.World.Apply(exact);
        string applied = Serialize(simulation.World.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(exact));
        Assert.Equal(applied, Serialize(simulation.World.CaptureSnapshot()));
    }

    [Fact]
    public void GuardianshipLifecycle_SameTurnRacesUseEventIdOrder()
    {
        EntityId thirdGuardian = new("character:test/third_guardian");

        (EntityId endEarlier, EntityId endLater) = OrderedCommandIds(
            "lifecycle-end-race-a",
            "lifecycle-end-race-b");
        CampaignSimulation twoEnds = CreateCampaignWithActive(
            "lifecycle-end-race-active",
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState endRaceActive = Assert.Single(
            twoEnds.World.CharacterGuardianships.Guardianships);
        Assert.True(twoEnds.Submit(FamilyCommand(
            twoEnds,
            endLater,
            new EndPrimaryGuardianshipAction(
                Ward,
                endRaceActive.GuardianshipId,
                CharacterGuardianshipEndReason.Revoked))).IsValid);
        Assert.True(twoEnds.Submit(FamilyCommand(
            twoEnds,
            endEarlier,
            new EndPrimaryGuardianshipAction(
                Ward,
                endRaceActive.GuardianshipId,
                CharacterGuardianshipEndReason.Revoked))).IsValid);
        IReadOnlyList<CampaignEvent> endEvents = twoEnds.ResolveTurn();
        Assert.IsType<PrimaryGuardianshipEndedOutcome>(Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(endEvents[0].Payload).Outcome);
        Assert.IsType<CommandCancelledEventPayload>(endEvents[1].Payload);
        Assert.Equal(
            endEarlier,
            Assert.Single(twoEnds.World.CharacterGuardianships.Guardianships)
                .EndSourceCommandId);

        (EntityId mixedEarlier, EntityId mixedLater) = OrderedCommandIds(
            "lifecycle-mixed-race-a",
            "lifecycle-mixed-race-b");
        for (int replacementWins = 0; replacementWins < 2; replacementWins++)
        {
            CampaignSimulation mixed = CreateCampaignWithActive(
                $"lifecycle-mixed-active-{replacementWins}",
                Seed(Guardian, new CampaignDate(160, 1, 1)),
                Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
                Seed(Ward, new CampaignDate(190, 1, 1)));
            CharacterGuardianshipState current = Assert.Single(
                mixed.World.CharacterGuardianships.Guardianships);
            ICharacterFamilyAction earlierAction = replacementWins == 1
                ? new ReplacePrimaryGuardianshipAction(
                    Ward,
                    current.GuardianshipId,
                    OtherGuardian)
                : new EndPrimaryGuardianshipAction(
                    Ward,
                    current.GuardianshipId,
                    CharacterGuardianshipEndReason.Revoked);
            ICharacterFamilyAction laterAction = replacementWins == 1
                ? new EndPrimaryGuardianshipAction(
                    Ward,
                    current.GuardianshipId,
                    CharacterGuardianshipEndReason.Revoked)
                : new ReplacePrimaryGuardianshipAction(
                    Ward,
                    current.GuardianshipId,
                    OtherGuardian);
            Assert.True(mixed.Submit(FamilyCommand(
                mixed,
                mixedLater,
                laterAction)).IsValid);
            Assert.True(mixed.Submit(FamilyCommand(
                mixed,
                mixedEarlier,
                earlierAction)).IsValid);

            IReadOnlyList<CampaignEvent> mixedEvents = mixed.ResolveTurn();

            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(mixedEvents[0].Payload);
            Assert.IsType<CommandCancelledEventPayload>(mixedEvents[1].Payload);
            Assert.Equal(
                replacementWins == 1,
                mixed.World.CharacterGuardianships
                    .TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        }

        (EntityId replaceEarlier, EntityId replaceLater) = OrderedCommandIds(
            "lifecycle-replace-race-a",
            "lifecycle-replace-race-b");
        for (int scenario = 0; scenario < 2; scenario++)
        {
            CampaignSimulation replacements = CreateCampaignWithActive(
                $"lifecycle-replace-active-{scenario}",
                Seed(Guardian, new CampaignDate(160, 1, 1)),
                Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
                Seed(thirdGuardian, new CampaignDate(158, 1, 1)),
                Seed(Ward, new CampaignDate(190, 1, 1)));
            CharacterGuardianshipState current = Assert.Single(
                replacements.World.CharacterGuardianships.Guardianships);
            EntityId earlierGuardian = scenario == 0 ? OtherGuardian : thirdGuardian;
            EntityId laterGuardian = scenario == 0 ? thirdGuardian : OtherGuardian;
            Assert.True(replacements.Submit(FamilyCommand(
                replacements,
                replaceLater,
                new ReplacePrimaryGuardianshipAction(
                    Ward,
                    current.GuardianshipId,
                    laterGuardian))).IsValid);
            Assert.True(replacements.Submit(FamilyCommand(
                replacements,
                replaceEarlier,
                new ReplacePrimaryGuardianshipAction(
                    Ward,
                    current.GuardianshipId,
                    earlierGuardian))).IsValid);

            IReadOnlyList<CampaignEvent> events = replacements.ResolveTurn();

            Assert.IsType<PrimaryGuardianshipReplacedOutcome>(Assert.IsType<
                CharacterFamilyActionResolvedEventPayload>(events[0].Payload).Outcome);
            Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
            Assert.True(replacements.World.CharacterGuardianships
                .TryGetActivePrimaryGuardianshipForWard(
                    Ward,
                    out CharacterGuardianshipState? winner));
            Assert.Equal(earlierGuardian, winner.GuardianCharacterId);
        }
    }

    [Fact]
    public void PendingReplacement_SaveLoadReplayAndResolvedDiagnosticsAreExact()
    {
        CampaignSimulation original = CreateCampaignWithActive(
            "lifecycle-save-active",
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CharacterGuardianshipState active = Assert.Single(
            original.World.CharacterGuardianships.Guardianships);
        CampaignCommand replacement = FamilyCommand(
            original,
            new EntityId("command:test/lifecycle-pending-replacement"),
            new ReplacePrimaryGuardianshipAction(
                Ward,
                active.GuardianshipId,
                OtherGuardian));
        Assert.True(original.Submit(replacement).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-guardianship-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string pendingPath = Path.Combine(directory, "pending.save.gz");
            new SaveStore().SaveAtomic(
                pendingPath,
                SaveEnvelope.Create("test", [], original));
            SaveEnvelope loadedPending = new SaveStore().Load(pendingPath);
            CampaignSimulation replay = new(WorldState.Restore(loadedPending.Snapshot));

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loadedPending.SchemaVersion);
            Assert.IsType<ReplacePrimaryGuardianshipAction>(Assert.IsType<
                CharacterFamilyActionCommandPayload>(
                    Assert.Single(loadedPending.Snapshot.PendingCommands).Payload).Action);
            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));

            string resolvedPath = Path.Combine(directory, "resolved.save.gz");
            WorldSnapshot resolvedSnapshot = original.World.CaptureSnapshot();
            new SaveStore().SaveAtomic(
                resolvedPath,
                SaveEnvelope.Create("test", [], original));
            SaveEnvelope loadedResolved = new SaveStore().Load(resolvedPath);
            Assert.Contains(
                loadedResolved.DiagnosticCommands,
                item => item.Payload is CharacterFamilyActionCommandPayload
                {
                    Action: ReplacePrimaryGuardianshipAction,
                });
            Assert.Contains(
                loadedResolved.DiagnosticEvents,
                item => item.Payload is CharacterFamilyActionResolvedEventPayload
                {
                    Outcome: PrimaryGuardianshipReplacedOutcome,
                });
            Assert.Contains(
                loadedResolved.Snapshot.CharacterGuardianships.Guardianships,
                item => item.Status == CharacterGuardianshipStatus.Ended
                    && item.EndReason == CharacterGuardianshipEndReason.Replaced);
            Assert.Contains(
                loadedResolved.Snapshot.CharacterGuardianships.Guardianships,
                item => item.Status == CharacterGuardianshipStatus.Active
                    && item.GuardianCharacterId == OtherGuardian);
            Assert.Equal(
                SimulationChecksum.Compute(resolvedSnapshot).Value,
                loadedResolved.Checksum);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GuardianshipChecksumIsOrderInvariantMutationSensitiveAndCurrentSaveRetainsDiagnostics()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)),
            Seed(OtherWard, new CampaignDate(191, 1, 1)));
        Assert.True(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-current-save-a"),
            Guardian,
            Ward)).IsValid);
        Assert.True(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-current-save-b"),
            OtherGuardian,
            OtherWard)).IsValid);
        Assert.Equal(2, simulation.ResolveTurn().Count);
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        WorldSnapshot shuffled = snapshot with
        {
            CharacterGuardianships = snapshot.CharacterGuardianships with
            {
                Guardianships = snapshot.CharacterGuardianships.Guardianships
                    .Reverse()
                    .ToArray(),
            },
        };
        CharacterGuardianshipState first = snapshot.CharacterGuardianships.Guardianships[0];
        WorldSnapshot mutated = snapshot with
        {
            CharacterGuardianships = snapshot.CharacterGuardianships with
            {
                Guardianships = snapshot.CharacterGuardianships.Guardianships
                    .Select(item => item.GuardianshipId == first.GuardianshipId
                        ? item with { EstablishedTurnIndex = item.EstablishedTurnIndex + 1 }
                        : item)
                    .ToArray(),
            },
        };

        Assert.Equal(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(shuffled));
        Assert.NotEqual(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(mutated));

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-guardianship-current-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "guardianship-current.save.gz");
            new SaveStore().SaveAtomic(path, SaveEnvelope.Create("test", [], simulation));
            SaveEnvelope loaded = new SaveStore().Load(path);

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Contains(
                loaded.DiagnosticCommands,
                item => item.Payload is CharacterFamilyActionCommandPayload
                {
                    Action: EstablishPrimaryGuardianshipAction,
                });
            Assert.Contains(
                loaded.DiagnosticEvents,
                item => item.Payload is CharacterFamilyActionResolvedEventPayload
                {
                    Outcome: PrimaryGuardianshipEstablishedOutcome,
                });
            Assert.Equal(
                Serialize(snapshot.CharacterGuardianships),
                Serialize(loaded.Snapshot.CharacterGuardianships));
            Assert.Equal(SimulationChecksum.Compute(snapshot).Value, loaded.Checksum);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ThousandCharacterWorld_ResolvesBoundedGuardianshipBatchAndRecordsRawEvidence()
    {
        CharacterSeed[] seeds = Enumerable.Range(0, 1_000)
            .Select(index => Seed(
                new EntityId($"character:performance/guardianship-{index:D4}"),
                index == 0
                    ? new CampaignDate(150, 1, 1)
                    : new CampaignDate(190, 1, 1)))
            .ToArray();
        CampaignSimulation simulation = CreateCampaign(seeds);
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 1; index <= 64; index++)
        {
            Assert.True(simulation.Submit(GuardianshipCommand(
                simulation,
                new EntityId($"command:performance/guardianship-{index:D2}"),
                seeds[0].Id,
                seeds[index].Id)).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch checksumWatch = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        checksumWatch.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            SimulationJson.CreateOptions());
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(64, events.Count);
        Assert.All(events, item =>
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(item.Payload));
        Assert.Equal(64, snapshot.CharacterGuardianships.Guardianships.Count);
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"guardianship_raw workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    [Fact]
    public void ThousandCharacterWorld_ResolvesBoundedGuardianshipLifecycleBatchAndRecordsRawEvidence()
    {
        CharacterSeed[] seeds = Enumerable.Range(0, 1_000)
            .Select(index => Seed(
                new EntityId($"character:performance/lifecycle-{index:D4}"),
                index == 0
                    ? new CampaignDate(150, 1, 1)
                    : index <= 64
                        ? new CampaignDate(190, 1, 1)
                        : new CampaignDate(155, 1, 1)))
            .ToArray();
        CharacterGuardianshipState[] active = Enumerable.Range(1, 64)
            .Select(index => ActiveGuardianship(
                seeds[index].Id,
                seeds[0].Id,
                $"performance-lifecycle-{index:D2}"))
            .ToArray();
        CampaignSimulation simulation = CreateCampaign(
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                active),
            seeds);
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 1; index <= 32; index++)
        {
            Assert.True(simulation.Submit(FamilyCommand(
                simulation,
                new EntityId($"command:performance/lifecycle-replace-{index:D2}"),
                new ReplacePrimaryGuardianshipAction(
                    seeds[index].Id,
                    active[index - 1].GuardianshipId,
                    seeds[index + 64].Id))).IsValid);
        }

        for (int index = 33; index <= 64; index++)
        {
            Assert.True(simulation.Submit(FamilyCommand(
                simulation,
                new EntityId($"command:performance/lifecycle-end-{index:D2}"),
                new EndPrimaryGuardianshipAction(
                    seeds[index].Id,
                    active[index - 1].GuardianshipId,
                    CharacterGuardianshipEndReason.Revoked))).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch checksumWatch = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        checksumWatch.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            SimulationJson.CreateOptions());
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(64, events.Count);
        Assert.All(events, item =>
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(item.Payload));
        Assert.Equal(96, snapshot.CharacterGuardianships.Guardianships.Count);
        Assert.Equal(
            32,
            snapshot.CharacterGuardianships.Guardianships.Count(
                item => item.Status == CharacterGuardianshipStatus.Active));
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"guardianship_lifecycle_raw workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    private static CampaignSimulation CreateCampaign(params CharacterSeed[] seeds) => new(
        WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(seeds),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty));

    private static CampaignSimulation CreateCampaign(
        CharacterGuardianshipWorldSnapshot guardianships,
        params CharacterSeed[] seeds) => new(
        WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(seeds),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            guardianships));

    private static CampaignSimulation CreateCampaignWithActive(
        string suffix,
        params CharacterSeed[] seeds)
    {
        CharacterGuardianshipState active = ActiveGuardianship(
            Ward,
            Guardian,
            suffix);
        return CreateCampaign(
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [active]),
            seeds);
    }

    private static CharacterWorldSnapshot CreateCharacters(CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds
            .Select(seed => new CharacterDefinition(
                CharacterContractVersions.Definition,
                seed.Id,
                new EntityId($"loc:{seed.Id.Value.Replace(':', '/')}"),
                seed.BirthDate,
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(
                    new EntityId($"loc:{seed.Id.Value.Replace(':', '/')}"),
                    null),
                CharacterContentOrigin.LegacyUnknown(seed.Id),
                null,
                null,
                []))
            .OrderBy(item => item.Id)
            .ToArray();
        CharacterState[] states = seeds
            .Select(seed => new CharacterState(
                CharacterContractVersions.State,
                seed.Id,
                [],
                [],
                seed.Condition))
            .OrderBy(item => item.CharacterId)
            .ToArray();
        EntityId familyId = new("family:test/guardianship_all");
        EntityId householdId = new("household:test/guardianship_all");
        EntityId[] members = definitions.Select(item => item.Id).ToArray();
        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [
                new FamilyDefinition(
                    CharacterContractVersions.Definition,
                    familyId,
                    new EntityId("loc:family/guardianship_all")),
            ],
            [
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    householdId,
                    new EntityId("loc:household/guardianship_all")),
            ],
            states,
            [
                new FamilyState(
                    CharacterContractVersions.State,
                    familyId,
                    members),
            ],
            [
                new HouseholdState(
                    CharacterContractVersions.State,
                    householdId,
                    seeds[0].Id,
                    members),
            ]);
    }

    private static CampaignCommand GuardianshipCommand(
        CampaignSimulation simulation,
        EntityId commandId,
        EntityId guardian,
        EntityId ward,
        EntityId? expected = null,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands) => FamilyCommand(
        simulation,
        commandId,
        new EstablishPrimaryGuardianshipAction(guardian, ward, expected),
        issuingActor,
        phase);

    private static CampaignCommand FamilyCommand(
        CampaignSimulation simulation,
        EntityId commandId,
        ICharacterFamilyAction action,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands) => CampaignCommand.Create(
        commandId,
        issuingActor ?? CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(action),
        phase);

    private static (EntityId Earlier, EntityId Later) OrderedCommandIds()
    {
        EntityId first = new("command:test/two-guardian-first");
        EntityId second = new("command:test/two-guardian-second");
        return CharacterFamilyIds.DeriveActionEventId(Date, first).CompareTo(
            CharacterFamilyIds.DeriveActionEventId(Date, second)) < 0
                ? (first, second)
                : (second, first);
    }

    private static (EntityId Earlier, EntityId Later) OrderedCommandIds(
        string firstSuffix,
        string secondSuffix)
    {
        EntityId first = new($"command:test/{firstSuffix}");
        EntityId second = new($"command:test/{secondSuffix}");
        return CharacterFamilyIds.DeriveActionEventId(Date, first).CompareTo(
            CharacterFamilyIds.DeriveActionEventId(Date, second)) < 0
                ? (first, second)
                : (second, first);
    }

    private static CharacterGuardianshipState ActiveGuardianship(
        EntityId ward,
        EntityId guardian,
        string suffix)
    {
        CampaignDate establishedDate = Date.AddDays(-1);
        EntityId commandId = new($"command:test/{suffix}");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            establishedDate,
            commandId);
        return new CharacterGuardianshipState(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(
                eventId,
                ward,
                guardian),
            ward,
            guardian,
            establishedDate,
            0,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
    }

    private static (CampaignCommand Command, CampaignEvent Event) PlanLifecycleEvent(
        CampaignSimulation simulation,
        EntityId commandId,
        ICharacterFamilyAction action)
    {
        CampaignCommand command = FamilyCommand(simulation, commandId, action);
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            command.IssuedDate,
            command.CommandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            command.IssuingActor,
            Assert.IsType<CharacterFamilyActionCommandPayload>(command.Payload),
            command.IssuedDate,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            command.IssuedDate,
            command.Phase,
            command.Priority,
            WorldState.GetCharacterFamilyActionAffectedIds(plan.ResolvedPayload),
            plan.ResolvedPayload);
        return (command, campaignEvent);
    }

    private static void AssertNestedJsonContract(
        CampaignCommand command,
        CampaignEvent campaignEvent,
        string actionDiscriminator,
        string[] actionProperties,
        string outcomeDiscriminator,
        string[] outcomeProperties)
    {
        JsonObject commandJson = JsonNode.Parse(Serialize(command))!.AsObject();
        JsonObject actionJson = commandJson["payload"]!["action"]!.AsObject();
        JsonObject eventJson = JsonNode.Parse(Serialize(campaignEvent))!.AsObject();
        JsonObject outcomeJson = eventJson["payload"]!["outcome"]!.AsObject();

        Assert.Equal(actionDiscriminator, actionJson["$type"]!.GetValue<string>());
        Assert.Equal(actionProperties.Order(), actionJson.Select(item => item.Key).Order());
        Assert.Equal(outcomeDiscriminator, outcomeJson["$type"]!.GetValue<string>());
        Assert.Equal(outcomeProperties.Order(), outcomeJson.Select(item => item.Key).Order());
    }

    private static void AssertOnlyGuardianshipSubsystemChanged(
        WorldSnapshot before,
        WorldSnapshot after)
    {
        Assert.Equal(Serialize(before.Geography), Serialize(after.Geography));
        Assert.Equal(Serialize(before.Characters), Serialize(after.Characters));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.Careers), Serialize(after.Careers));
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(
            Serialize(before.CharacterEstateHoldings),
            Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
        Assert.Equal(Serialize(before.RandomStreams), Serialize(after.RandomStreams));
    }

    private static CharacterSeed Seed(EntityId id, CampaignDate birthDate) =>
        new(id, birthDate, CharacterConditionState.Default);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);
}
