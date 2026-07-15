using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class TierAndSoakTests
{
    [Theory]
    [InlineData(SimulationTier.Full, SimulationTier.Reduced)]
    [InlineData(SimulationTier.Reduced, SimulationTier.Aggregate)]
    [InlineData(SimulationTier.Aggregate, SimulationTier.Full)]
    public void TierTransitions_PreserveConservedTotalsAndPendingWork(SimulationTier from, SimulationTier to)
    {
        CampaignDate date = new(200, 1, 1);
        SyntheticEntitySnapshot entity = new(
            new EntityId("synthetic:target"),
            from,
            123,
            456,
            789,
            [new PendingWorkItem(new EntityId("work:promise"), date.AddDays(10), 99)]);
        WorldState world = WorldState.Create(date, 1, [entity]);
        CampaignSimulation simulation = new(world);
        ConservationLedger before = ConservationLedger.From(world.Entities[0]);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:tier"), entity.Id, date,
            new ChangeSimulationTierCommandPayload(entity.Id, to));

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        SyntheticEntitySnapshot after = Assert.Single(world.Entities);
        SimulationTierChangedEventPayload payload = Assert.IsType<SimulationTierChangedEventPayload>(campaignEvent.Payload);

        Assert.Equal(to, after.Tier);
        Assert.Equal(before, ConservationLedger.From(after));
        Assert.Equal(before, payload.Before);
        Assert.Equal(payload.Before, payload.After);
        Assert.Equal(entity.PendingWork, after.PendingWork);
    }

    [Fact]
    public void TenYearThousandEntitySoak_CompletesWithoutInvariantFailure()
    {
        SyntheticSoakResult result = SyntheticSimulation.RunSoak(10, 1_000, 20260712);

        Assert.True(result.Turns > 1_000);
        Assert.Equal(200, result.FinalDate.Year);
        // SP-04A adds the authoritative simulation.characters@1 system and empty character snapshot
        // to newly captured worlds, so the canonical golden changes without altering the soak inputs.
        Assert.Equal("cc6cba9f2b5408921fdbcd15a8d5494ca2351e73d7f3052f16702a09639af702", result.Checksum.Value);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void LongRangeSyntheticSoaks_RemainDeterministic(int years)
    {
        SyntheticSoakResult first = SyntheticSimulation.RunSoak(years, 10, 77);
        SyntheticSoakResult second = SyntheticSimulation.RunSoak(years, 10, 77);

        Assert.Equal(first.Checksum, second.Checksum);
    }
}
