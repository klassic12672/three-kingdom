using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void IdenticalReplay_ProducesIdenticalChecksum()
    {
        WorldSnapshot initial = SyntheticSimulation.CreateWorld(20, 8675309).CaptureSnapshot();
        EntityId actor = initial.Entities[0].Id;
        CampaignCommand[] commands = Enumerable.Range(0, 25)
            .Select(index => CampaignCommand.Create(
                new EntityId($"command:replay/{index:D4}"),
                actor,
                initial.Calendar.Date.AddDays(index),
                new AdjustResourcesCommandPayload(initial.Entities[index % initial.Entities.Count].Id, 0, index, 1)))
            .ToArray();

        SimulationChecksum first = SimulationChecksum.Compute(SyntheticSimulation.Replay(initial, commands).World.CaptureSnapshot());
        SimulationChecksum second = SimulationChecksum.Compute(SyntheticSimulation.Replay(initial, commands.Reverse()).World.CaptureSnapshot());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Checksum_IsIndependentOfInputCollectionOrder()
    {
        WorldSnapshot snapshot = SyntheticSimulation.CreateWorld(100, 42).CaptureSnapshot();
        SimulationChecksum expected = SimulationChecksum.Compute(snapshot);
        Random random = new(17);

        for (int iteration = 0; iteration < 50; iteration++)
        {
            WorldSnapshot shuffled = snapshot with
            {
                Entities = snapshot.Entities.OrderBy(_ => random.Next()).ToArray(),
                RandomStreams = snapshot.RandomStreams.OrderBy(_ => random.Next()).ToArray(),
                SystemVersions = snapshot.SystemVersions.OrderBy(_ => random.Next()).ToArray(),
            };
            Assert.Equal(expected, SimulationChecksum.Compute(shuffled));
        }
    }

    [Fact]
    public void RandomStreams_AreIsolatedBySystemAndContext()
    {
        DeterministicRandomStreams baseline = new(1234);
        ulong first = baseline.NextUInt64("economy", "region/1");
        ulong second = baseline.NextUInt64("economy", "region/1");

        DeterministicRandomStreams withPresentationNoise = new(1234);
        ulong noisyFirst = withPresentationNoise.NextUInt64("economy", "region/1");
        _ = withPresentationNoise.NextUInt64("presentation", "sparkle/1");
        _ = withPresentationNoise.NextUInt64("economy", "region/2");
        ulong noisySecond = withPresentationNoise.NextUInt64("economy", "region/1");

        Assert.Equal((first, second), (noisyFirst, noisySecond));
    }

    [Fact]
    public void Commands_AreResolvedByDatePhasePriorityAndStableId()
    {
        WorldState world = SyntheticSimulation.CreateWorld(2, 1);
        CampaignSimulation simulation = new(world);
        EntityId actor = world.Entities[0].Id;
        CampaignDate date = world.Calendar.Date;

        CampaignCommand laterId = CampaignCommand.Create(
            new EntityId("command:z"), actor, date,
            new AdjustResourcesCommandPayload(actor, 0, 0, 2));
        CampaignCommand earlierId = CampaignCommand.Create(
            new EntityId("command:a"), actor, date,
            new AdjustResourcesCommandPayload(actor, 0, 0, 1));

        Assert.True(simulation.Submit(laterId).IsValid);
        Assert.True(simulation.Submit(earlierId).IsValid);
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.Equal([new EntityId("command:a"), new EntityId("command:z")], events.Select(item => item.CausalId!.Value));
    }

    [Fact]
    public void OutOfOrderEvents_AreRejectedBeforeMutation()
    {
        WorldState world = SyntheticSimulation.CreateWorld(1, 1);
        CampaignDate date = world.Calendar.Date;
        world.Apply(Event("event:z", date, 0));

        Assert.Throws<SimulationValidationException>(() => world.Apply(Event("event:a", date, 0)));
    }

    [Fact]
    public void ActorUnavailableAtResolution_ProducesDefinedCancellationEvent()
    {
        WorldSnapshot source = SyntheticSimulation.CreateWorld(2, 1).CaptureSnapshot();
        EntityId unavailableActor = source.Entities[1].Id;
        CampaignCommand pending = CampaignCommand.Create(
            new EntityId("command:pending"),
            unavailableActor,
            source.Calendar.Date,
            new ChangeSimulationTierCommandPayload(source.Entities[0].Id, SimulationTier.Reduced));
        WorldSnapshot actorRemoved = source with
        {
            Entities = [source.Entities[0]],
            PendingCommands = [pending],
        };
        CampaignSimulation simulation = new(WorldState.Restore(actorRemoved));

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());

        CommandCancelledEventPayload cancellation = Assert.IsType<CommandCancelledEventPayload>(campaignEvent.Payload);
        Assert.Equal("actor_unavailable", cancellation.ReasonCode);
    }

    [Fact]
    public void CommandInvalidatedByEarlierOutcome_ProducesCancellationInsteadOfUnderflow()
    {
        WorldState world = WorldState.Create(
            new CampaignDate(200, 1, 1),
            1,
            [new SyntheticEntitySnapshot(new EntityId("actor:test"), SimulationTier.Full, 10, 10, 10, [])]);
        CampaignSimulation simulation = new(world);
        foreach (string id in new[] { "a", "b" })
        {
            Assert.True(simulation.Submit(CampaignCommand.Create(
                new EntityId($"command:{id}"),
                new EntityId("actor:test"),
                world.Calendar.Date,
                new AdjustResourcesCommandPayload(new EntityId("actor:test"), -10, 0, 0))).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.IsType<ResourcesAdjustedEventPayload>(events[0].Payload);
        CommandCancelledEventPayload cancellation = Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
        Assert.Equal("command_invalidated", cancellation.ReasonCode);
        Assert.Equal(0, world.Entities[0].People);
    }

    [Fact]
    public void BackgroundResults_CommitOnlyInTheirOrderedDailyPhase()
    {
        WorldState world = SyntheticSimulation.CreateWorld(1, 5);
        EntityId target = world.Entities[0].Id;
        CampaignEvent background = new(
            ContractVersions.CampaignEvent,
            new EntityId("event:background/resource"),
            null,
            world.Calendar.Date,
            ResolutionPhase.BackgroundCommit,
            0,
            [target],
            new ResourcesAdjustedEventPayload(target, 0, 5, 0));

        IReadOnlyList<CampaignEvent> resolved = new CampaignSimulation(world).ResolveTurn([background]);

        Assert.Same(background, Assert.Single(resolved));
        Assert.Equal(10_005, world.Entities[0].Food);
    }

    [Fact]
    public void InvalidCommandDefaults_FailValidationBeforeQueueing()
    {
        CampaignSimulation simulation = new(SyntheticSimulation.CreateWorld(1, 1));
        CampaignCommand invalid = CampaignCommand.Create(
            default,
            simulation.World.Entities[0].Id,
            default,
            new ChangeSimulationTierCommandPayload(simulation.World.Entities[0].Id, SimulationTier.Full));

        CommandValidationResult result = simulation.Submit(invalid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid_command_id");
        Assert.Contains(result.Issues, issue => issue.Code == "invalid_date");
        Assert.Empty(simulation.World.CaptureSnapshot().PendingCommands);
    }

    [Fact]
    public void Checksum_ExcludesCommandValidationDiagnostics()
    {
        WorldSnapshot snapshot = SyntheticSimulation.CreateWorld(1, 1).CaptureSnapshot();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:diagnostic"),
            snapshot.Entities[0].Id,
            snapshot.Calendar.Date.AddDays(1),
            new ChangeSimulationTierCommandPayload(snapshot.Entities[0].Id, SimulationTier.Reduced));
        WorldSnapshot valid = snapshot with { PendingCommands = [command] };
        WorldSnapshot invalidDiagnostic = snapshot with
        {
            PendingCommands =
            [
                command with
                {
                    Validation = CommandValidationResult.Invalid(new ValidationIssue("display_only", "Diagnostic text")),
                },
            ],
        };

        Assert.Equal(SimulationChecksum.Compute(valid), SimulationChecksum.Compute(invalidDiagnostic));
    }

    [Fact]
    public async Task BackgroundCalculations_CommitInStableOrderRegardlessOfCompletionOrder()
    {
        CampaignDate date = new(200, 1, 1);
        BackgroundCalculation[] calculations =
        [
            new(new EntityId("work:z"), () => Event("event:z", date, 2)),
            new(new EntityId("work:a"), () => Event("event:a", date, 1)),
        ];

        IReadOnlyList<CampaignEvent> results = await DeterministicCalculationScheduler.CalculateAsync(calculations);

        Assert.Equal(["event:a", "event:z"], results.Select(item => item.EventId.Value));
    }

    [Fact]
    public void UnregisteredPayload_CannotBeSerialized()
    {
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:unknown"),
            new EntityId("actor:test"),
            new CampaignDate(200, 1, 1),
            new UnknownPayload());

        Assert.Throws<NotSupportedException>(() =>
            System.Text.Json.JsonSerializer.Serialize(command, CanonicalJson.Options));
    }

    private static CampaignEvent Event(string id, CampaignDate date, int priority) => new(
        ContractVersions.CampaignEvent,
        new EntityId(id),
        null,
        date,
        ResolutionPhase.BackgroundCommit,
        priority,
        [],
        new CommandCancelledEventPayload("test", "test"));

    private sealed record UnknownPayload : ICampaignCommandPayload;
}
