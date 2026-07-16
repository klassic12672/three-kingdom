using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionClaimCampaignTests
{
    private static readonly CampaignDate Date = new(200, 7, 14);
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId Claimant = Character("claimant");

    [Fact]
    public void F703_RegisteredCommandProducesExactEventAndChecksumBearingClaim()
    {
        CampaignSimulation simulation = CreateSimulation();
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = Assertion("registered");

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());

        CharacterSuccessionClaimActionResolvedEventPayload payload = Assert.IsType<
            CharacterSuccessionClaimActionResolvedEventPayload>(campaignEvent.Payload);
        SuccessionClaimState claim = Assert.IsType<SuccessionClaimAssertedOutcome>(
            payload.Outcome).CurrentClaim;
        Assert.Equal(
            CharacterSuccessionIds.DeriveClaimActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(command.CommandId, campaignEvent.CausalId);
        Assert.Equal(
            new[] { claim.ClaimId, Subject, Claimant }.Distinct().Order(),
            campaignEvent.AffectedIds);
        Assert.True(simulation.World.CharacterSuccessions.TryGetActiveClaim(
            Subject,
            Claimant,
            out SuccessionClaimState? stored));
        Assert.Equal(claim, stored);
        Assert.NotEqual(
            SimulationChecksum.Compute(before),
            SimulationChecksum.Compute(simulation.World.CaptureSnapshot()));
        Assert.Equal(before.RandomStreams, simulation.World.CaptureSnapshot().RandomStreams);
    }

    [Fact]
    public void F706_ConcurrentDuplicateAssertionsAreSubmissionOrderInvariant()
    {
        CampaignCommand first = Assertion("race-a");
        CampaignCommand second = Assertion("race-b");

        (string Events, string Snapshot) forward = RunConcurrent(first, second);
        (string Events, string Snapshot) reverse = RunConcurrent(second, first);

        Assert.Equal(forward, reverse);
        CampaignEvent[] events = JsonSerializer.Deserialize<CampaignEvent[]>(
            forward.Events,
            SimulationJson.CreateOptions())!;
        Assert.Single(events, item =>
            item.Payload is CharacterSuccessionClaimActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
    }

    [Fact]
    public void F706_ConcurrentExactWithdrawalsCancelTheStaleCommand()
    {
        CampaignSimulation simulation = CreateSimulation();
        Assert.True(simulation.Submit(Assertion("withdraw-race-source")).IsValid);
        SuccessionClaimState active = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).CurrentClaim;
        CampaignCommand first = Withdrawal("withdraw-race-a", active.ClaimId);
        CampaignCommand second = Withdrawal("withdraw-race-b", active.ClaimId);
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();

        Assert.Single(events, item =>
            item.Payload is CharacterSuccessionClaimActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.False(simulation.World.CharacterSuccessions.TryGetActiveClaim(
            Subject,
            Claimant,
            out _));
        Assert.Single(simulation.World.CharacterSuccessions
            .GetRecentClaimRecordsForSubject(Subject));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void F707_ActiveClaimSurvivesLaterParticipantDeathUnchanged(
        bool killClaimant)
    {
        CampaignSimulation simulation = CreateSimulation();
        Assert.True(simulation.Submit(Assertion("lifecycle-source")).IsValid);
        SuccessionClaimState asserted = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload).Outcome).CurrentClaim;
        EntityId target = killClaimant ? Claimant : Subject;
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            target,
            out AuthoritativeCharacterProfile? profile));
        CampaignCommand death = CampaignCommand.Create(
            new EntityId($"command:test/f7-lifecycle-{(killClaimant ? "claimant" : "subject")}"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(new ResolveCharacterDeathAction(
                target,
                profile.Condition)));

        Assert.True(simulation.Submit(death).IsValid);
        Assert.IsType<CharacterConditionActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);

        Assert.True(simulation.World.CharacterSuccessions.TryGetActiveClaim(
            Subject,
            Claimant,
            out SuccessionClaimState? stored));
        Assert.Equal(asserted, stored);
    }

    [Fact]
    public void F710_PendingAndResolvedSavesReplayExactlyOnLaterDays()
    {
        CampaignSimulation pendingSimulation = CreateSimulation();
        CampaignCommand pendingCommand = Assertion(
            "pending-save",
            pendingSimulation.World.Calendar.Date.AddDays(2));
        Assert.True(pendingSimulation.Submit(pendingCommand).IsValid);
        SaveEnvelope pending = SaveStoreRoundTrip(
            SaveEnvelope.Create("test", [], pendingSimulation),
            "pending");
        Assert.Single(pending.Snapshot.PendingCommands);

        CampaignSimulation first = new(WorldState.Restore(pending.Snapshot));
        CampaignSimulation second = new(WorldState.Restore(pending.Snapshot));
        CampaignEvent[] firstEvents = first.ResolveTurn().ToArray();
        CampaignEvent[] secondEvents = second.ResolveTurn().ToArray();

        Assert.Equal(Serialize(firstEvents), Serialize(secondEvents));
        Assert.Equal(
            SimulationChecksum.Compute(first.World.CaptureSnapshot()),
            SimulationChecksum.Compute(second.World.CaptureSnapshot()));
        SuccessionClaimState claim = Assert.IsType<SuccessionClaimAssertedOutcome>(
            Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
                Assert.Single(firstEvents).Payload).Outcome).CurrentClaim;
        Assert.Equal(pendingCommand.IssuedDate, claim.AssertedDate);
        SaveEnvelope resolved = SaveStoreRoundTrip(
            SaveEnvelope.Create("test", [], first),
            "resolved");
        Assert.Single(resolved.Snapshot.CharacterSuccessions.Claims);
        Assert.Equal(
            SimulationChecksum.Compute(first.World.CaptureSnapshot()),
            SimulationChecksum.Compute(resolved.Snapshot));
    }

    [Fact]
    public void F710_TamperedClaimEventFailsBeforeMutation()
    {
        WorldState world = CreateSimulation().World;
        EntityId commandId = new("command:test/f7-tampered");
        EntityId eventId = CharacterSuccessionIds.DeriveClaimActionEventId(Date, commandId);
        CharacterSuccessionClaimActionResolvedEventPayload planned =
            world.CharacterSuccessions.PlanClaimAction(
                Claimant,
                new(new AssertSuccessionClaimAction(Subject)),
                Date,
                world.Calendar.TurnIndex,
                commandId,
                eventId);
        SuccessionClaimState original = Assert.IsType<SuccessionClaimAssertedOutcome>(
            planned.Outcome).CurrentClaim;
        CharacterSuccessionClaimActionResolvedEventPayload tampered = planned with
        {
            Outcome = new SuccessionClaimAssertedOutcome(original with
            {
                ClaimId = new("succession_claim:test/tampered"),
            }),
        };
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterSuccessionClaimActionAffectedIds(tampered),
            tampered);
        string before = Serialize(world.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => world.Apply(campaignEvent));
        Assert.Equal(before, Serialize(world.CaptureSnapshot()));
    }

    private static (string Events, string Snapshot) RunConcurrent(
        CampaignCommand first,
        CampaignCommand second)
    {
        CampaignSimulation simulation = CreateSimulation();
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);
        return (
            Serialize(simulation.ResolveTurn()),
            Serialize(simulation.World.CaptureSnapshot()));
    }

    private static CampaignCommand Assertion(
        string suffix,
        CampaignDate? date = null) => CampaignCommand.Create(
        new EntityId($"command:test/f7-{suffix}"),
        Claimant,
        date ?? Date,
        new CharacterSuccessionClaimActionCommandPayload(
            new AssertSuccessionClaimAction(Subject)));

    private static CampaignCommand Withdrawal(
        string suffix,
        EntityId claimId) => CampaignCommand.Create(
        new EntityId($"command:test/f7-{suffix}"),
        Claimant,
        Date.AddDays(3),
        new CharacterSuccessionClaimActionCommandPayload(
            new WithdrawSuccessionClaimAction(Subject, claimId)));

    private static CampaignSimulation CreateSimulation()
    {
        CharacterDefinition[] definitions =
        [
            Definition(Subject),
            Definition(Claimant),
        ];
        CharacterState[] states =
        [
            State(Subject),
            State(Claimant),
        ];
        WorldState world = WorldState.Create(
            Date,
            57,
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
            CharacterPregnancyWorldSnapshot.Empty);
        return new CampaignSimulation(world);
    }

    private static CharacterDefinition Definition(EntityId id)
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
    }

    private static CharacterState State(EntityId id) => new(
        CharacterContractVersions.State,
        id,
        [],
        [],
        CharacterConditionState.Default,
        []);

    private static SaveEnvelope SaveStoreRoundTrip(
        SaveEnvelope envelope,
        string suffix)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f7-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, $"{suffix}.save.gz");
            SaveStore store = new();
            store.SaveAtomic(path, envelope);
            return store.Load(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static EntityId Character(string suffix) =>
        new($"character:test/f7-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        CanonicalJson.Options);
}
