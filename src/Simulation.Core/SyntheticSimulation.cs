namespace Simulation.Core;

public sealed record SyntheticSoakResult(
    int Years,
    long Turns,
    CampaignDate FinalDate,
    SimulationChecksum Checksum,
    TimeSpan Elapsed);

public static class SyntheticSimulation
{
    public static WorldState CreateWorld(int entityCount, ulong seed, CampaignDate? startDate = null)
    {
        if (entityCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(entityCount));
        }

        CampaignDate date = startDate ?? new CampaignDate(190, 1, 1);
        SyntheticEntitySnapshot[] entities = Enumerable.Range(0, entityCount)
            .Select(index => new SyntheticEntitySnapshot(
                new EntityId($"synthetic:entity/{index:D5}"),
                (index % 3) switch
                {
                    0 => SimulationTier.Full,
                    1 => SimulationTier.Reduced,
                    _ => SimulationTier.Aggregate,
                },
                1_000 + index,
                10_000 + index,
                1_000 + index,
                [new PendingWorkItem(new EntityId($"work:entity/{index:D5}/baseline"), date.AddDays(30), 10 + index)]))
            .ToArray();
        return WorldState.Create(date, seed, entities);
    }

    public static SyntheticSoakResult RunSoak(int years, int entityCount, ulong seed)
    {
        if (years < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(years));
        }

        WorldState world = CreateWorld(entityCount, seed);
        CampaignSimulation simulation = new(world);
        int targetYear = checked(world.Calendar.Date.Year + years);
        long turns = 0;
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (world.Calendar.Date.Year < targetYear)
        {
            int entityIndex = world.Random.NextInt32("synthetic-economy", $"turn/{turns}", entityCount);
            SyntheticEntitySnapshot entity = world.Entities[entityIndex];
            EntityId actor = world.Entities[0].Id;
            CampaignDate date = world.Calendar.Date;
            CampaignCommand resources = CampaignCommand.Create(
                new EntityId($"command:soak/{turns:D10}/resources"),
                actor,
                date,
                new AdjustResourcesCommandPayload(entity.Id, 0, 1, 1));
            EnsureValid(simulation.Submit(resources));

            SimulationTier nextTier = (SimulationTier)(((int)entity.Tier + 1) % 3);
            CampaignCommand transition = CampaignCommand.Create(
                new EntityId($"command:soak/{turns:D10}/tier"),
                actor,
                date,
                new ChangeSimulationTierCommandPayload(entity.Id, nextTier),
                priority: 1);
            EnsureValid(simulation.Submit(transition));

            simulation.ResolveTurn();
            AssertInvariants(world);
            turns++;
        }

        stopwatch.Stop();
        return new SyntheticSoakResult(
            years,
            turns,
            world.Calendar.Date,
            SimulationChecksum.Compute(world.CaptureSnapshot()),
            stopwatch.Elapsed);
    }

    public static CampaignSimulation Replay(WorldSnapshot initialSnapshot, IEnumerable<CampaignCommand> commands)
    {
        WorldState world = WorldState.Restore(initialSnapshot);
        CampaignSimulation simulation = new(world);
        CampaignCommand[] ordered = commands.OrderBy(command => command, CommandComparer.Instance).ToArray();
        foreach (CampaignCommand command in ordered)
        {
            EnsureValid(simulation.Submit(command));
        }

        if (ordered.Length == 0)
        {
            return simulation;
        }

        CampaignDate finalDate = ordered[^1].IssuedDate;
        while (world.Calendar.Date.CompareTo(finalDate) <= 0)
        {
            simulation.ResolveTurn();
        }

        return simulation;
    }

    public static void AssertInvariants(IWorldQuery world)
    {
        foreach (SyntheticEntitySnapshot entity in world.Entities)
        {
            if (entity.People < 0 || entity.Food < 0 || entity.Gold < 0 || entity.PendingWork.Any(work => work.Amount < 0))
            {
                throw new SimulationValidationException($"Invariant failure for '{entity.Id}'.");
            }
        }

        if (world.Entities.Select(entity => entity.Id).Distinct().Count() != world.Entities.Count)
        {
            throw new SimulationValidationException("Duplicate entity IDs detected.");
        }
    }

    private static void EnsureValid(CommandValidationResult validation)
    {
        if (!validation.IsValid)
        {
            throw new SimulationValidationException(string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }
    }
}

public sealed record ReplaySpecification(WorldSnapshot InitialSnapshot, IReadOnlyList<CampaignCommand> Commands);
