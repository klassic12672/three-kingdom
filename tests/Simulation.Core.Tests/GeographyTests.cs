using System.Diagnostics;
using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class GeographyTests
{
    [Fact]
    public void GraphRejectsDanglingContainmentAndRoutesWithoutModes()
    {
        GeographicWorldSnapshot source = GeographyFixture.Snapshot();
        GeographicGraphDefinition dangling = source.Graph with
        {
            Districts =
            [
                source.Graph.Districts[0] with { RegionId = new EntityId("region:missing/value") },
            ],
        };
        GeographicGraphDefinition noModes = source.Graph with
        {
            Routes =
            [
                source.Graph.Routes[0] with { PermittedModes = [] },
            ],
        };

        Assert.Throws<SimulationValidationException>(() => new GeographicGraph(dangling));
        Assert.Throws<SimulationValidationException>(() => new GeographicGraph(noModes));
    }

    [Fact]
    public void PathfindingUsesRoutesOnlyAndMeetsInteractionBudget()
    {
        GeographicWorldState geography = new(GeographyFixture.Snapshot());
        Stopwatch stopwatch = Stopwatch.StartNew();

        PathResult? path = null;
        for (int index = 0; index < 100; index++)
        {
            path = geography.FindPath(GeographyFixture.A, GeographyFixture.C, TransportMode.Wagon, GeographyFixture.FactionOne);
        }

        stopwatch.Stop();
        Assert.NotNull(path);
        Assert.Equal([GeographyFixture.RoadAb, GeographyFixture.RoadBc], path.RouteIds);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(50), $"100 path queries took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void MovementAdvancesByRouteProgressAndArrivesDeterministically()
    {
        ArmyGeographicState army = GeographyFixture.Army("marcher", GeographyFixture.FactionOne, GeographyFixture.A);
        WorldState world = GeographyFixture.World(GeographyFixture.Snapshot([army]));
        CampaignSimulation simulation = new(world);
        CampaignCommand order = CampaignCommand.Create(
            new EntityId("command:test/move"),
            GeographyFixture.Actor,
            world.Calendar.Date,
            new MovementOrderPayload(
                army.ArmyId,
                [GeographyFixture.RoadAb],
                TransportMode.Foot,
                MovementStance.Normal,
                world.Calendar.Date,
                MovementFallback.Wait));

        Assert.True(simulation.Submit(order).IsValid);
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.Contains(events, item => item.Payload is MovementEventPayload { Kind: MovementEventKind.Advanced });
        Assert.Contains(events, item => item.Payload is MovementEventPayload { Kind: MovementEventKind.Arrived });
        Assert.True(world.Geography.TryGetArmy(army.ArmyId, out ArmyGeographicState? actual));
        Assert.Equal(GeographyFixture.B, actual.CurrentStopId);
        Assert.Null(actual.ActiveRouteId);
    }

    [Fact]
    public void OpposingEdgeMovementsProduceOneStableInterception()
    {
        ArmyGeographicState first = GeographyFixture.Army("first", GeographyFixture.FactionOne, GeographyFixture.A, scouting: 40);
        ArmyGeographicState second = GeographyFixture.Army("second", GeographyFixture.FactionTwo, GeographyFixture.B, scouting: 70);
        CampaignSimulation simulation = new(GeographyFixture.World(GeographyFixture.Snapshot([first, second])));
        foreach ((ArmyGeographicState army, string id) in new[] { (first, "first"), (second, "second") })
        {
            Assert.True(simulation.Submit(CampaignCommand.Create(
                new EntityId($"command:test/{id}"),
                GeographyFixture.Actor,
                simulation.World.Calendar.Date,
                new MovementOrderPayload(
                    army.ArmyId,
                    [GeographyFixture.RoadAb],
                    TransportMode.Foot,
                    MovementStance.Normal,
                    simulation.World.Calendar.Date,
                    MovementFallback.Wait))).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        InterceptionEventPayload interception = Assert.IsType<InterceptionEventPayload>(
            Assert.Single(events, item => item.Payload is InterceptionEventPayload).Payload);

        Assert.Equal(second.ArmyId, interception.InterceptorArmyId);
        Assert.Equal(GeographyFixture.RoadAb, interception.RouteId);
        Assert.Equal(TerrainType.Plains, interception.Location.Terrain);
        Assert.All([first.ArmyId, second.ArmyId], id =>
        {
            Assert.True(simulation.World.Geography.TryGetArmy(id, out ArmyGeographicState? stopped));
            Assert.Empty(stopped.PlannedRouteIds);
        });
    }

    [Fact]
    public void SeasonalClosureWaitsAtLastStopAndRetreatUsesReachableRoute()
    {
        ArmyGeographicState army = GeographyFixture.Army("winter", GeographyFixture.FactionOne, GeographyFixture.A);
        WorldState world = GeographyFixture.World(GeographyFixture.Snapshot([army], season: CampaignSeason.Winter));
        CampaignSimulation simulation = new(world);
        Assert.True(simulation.Submit(CampaignCommand.Create(
            new EntityId("command:test/winter"),
            GeographyFixture.Actor,
            world.Calendar.Date,
            new MovementOrderPayload(
                army.ArmyId,
                [GeographyFixture.SeasonalAc],
                TransportMode.Foot,
                MovementStance.Normal,
                world.Calendar.Date,
                MovementFallback.Wait))).IsValid);

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.Contains(events, item => item.Payload is MovementEventPayload { Kind: MovementEventKind.Blocked, ReasonCode: "seasonal_closure" });
        Assert.True(world.Geography.TryGetArmy(army.ArmyId, out ArmyGeographicState? waiting));
        Assert.Equal(GeographyFixture.A, waiting.CurrentStopId);

        CampaignCommand retreat = CampaignCommand.Create(
            new EntityId("command:test/retreat"),
            GeographyFixture.Actor,
            world.Calendar.Date,
            new RetreatOrderPayload(army.ArmyId, GeographyFixture.C, TransportMode.Foot));
        Assert.True(simulation.Submit(retreat).IsValid);
        MovementEventPayload retreatEvent = Assert.IsType<MovementEventPayload>(simulation.ResolveTurn()
            .First(item => item.CausalId == retreat.CommandId).Payload);
        Assert.Equal(MovementEventKind.Retreated, retreatEvent.Kind);
        Assert.Equal(GeographyFixture.B, retreatEvent.CurrentStopId);
    }

    [Fact]
    public void SupplyUsesDisruptedChainBottleneckAndPortModeRules()
    {
        GeographicWorldSnapshot source = GeographyFixture.Snapshot();
        RouteState[] disrupted = source.Routes.Select(state => state.RouteId switch
        {
            var id when id == GeographyFixture.RoadAb => state with { DisruptionPermille = 200 },
            var id when id == GeographyFixture.RoadBc => state with { DisruptionPermille = 500 },
            _ => state,
        }).ToArray();
        WorldState world = GeographyFixture.World(source with { Routes = disrupted });
        CampaignSimulation simulation = new(world);
        CampaignCommand supply = CampaignCommand.Create(
            new EntityId("command:test/supply"),
            GeographyFixture.Actor,
            world.Calendar.Date,
            new SupplyOrderPayload(
                GeographyFixture.FactionOne,
                GeographyFixture.A,
                GeographyFixture.C,
                [GeographyFixture.RoadAb, GeographyFixture.RoadBc],
                TransportMode.Wagon,
                10_000));
        CampaignCommand competingSupply = CampaignCommand.Create(
            new EntityId("command:test/supply_competing"),
            GeographyFixture.Actor,
            world.Calendar.Date,
            supply.Payload,
            priority: 1);

        Assert.True(simulation.Submit(supply).IsValid);
        Assert.True(simulation.Submit(competingSupply).IsValid);
        SupplyTransferredEventPayload[] transfers = simulation.ResolveTurn()
            .Where(item => item.CausalId == supply.CommandId || item.CausalId == competingSupply.CommandId)
            .Select(item => Assert.IsType<SupplyTransferredEventPayload>(item.Payload))
            .ToArray();

        Assert.Equal(1_500, transfers.Sum(transfer => transfer.TransferredAmount));
        Assert.Equal(1_500, transfers[0].BottleneckCapacity);
        Assert.Equal(0, transfers[1].BottleneckCapacity);
        SupplyOrderPayload invalidRiverMode = new(
            GeographyFixture.FactionOne,
            GeographyFixture.B,
            GeographyFixture.D,
            [GeographyFixture.RiverBd],
            TransportMode.Wagon,
            100);
        Assert.False(world.Geography.ValidateSupplyOrder(invalidRiverMode).IsValid);
    }

    [Fact]
    public void PoliticalStateFieldsAndFogRemainIndependent()
    {
        EntityId observer = new("faction:test/observer");
        GeographicWorldSnapshot snapshot = GeographyFixture.Snapshot();
        snapshot = snapshot with
        {
            Locations = snapshot.Locations.Select(location => location.StopId == GeographyFixture.C
                ? location with
                {
                    Intelligence = location.Intelligence
                        .Append(new LocationIntelligence(observer, IntelligenceLevel.Observed, 0))
                        .ToArray(),
                }
                : location).ToArray(),
        };
        GeographicWorldState geography = new(snapshot);

        GeographicContext observed = geography.GetContext(GeographyFixture.C, observer);
        GeographicContext unknown = geography.GetContext(GeographyFixture.C, new EntityId("faction:test/unknown"));

        Assert.Equal(GeographyFixture.FactionTwo, observed.PoliticalState.ControllerId);
        Assert.Null(observed.PoliticalState.LegalAppointeeId);
        Assert.Null(observed.PoliticalState.LocalAcceptance);
        Assert.Single(observed.PoliticalState.Claims);
        Assert.Equal(IntelligenceLevel.Unknown, unknown.PoliticalState.Intelligence);
        Assert.Null(unknown.PoliticalState.ControllerId);
        Assert.Empty(unknown.PoliticalState.Claims);
    }

    [Fact]
    public void ReinforcementAndBattleDescriptorsAreEngineIndependent()
    {
        ArmyGeographicState nearby = GeographyFixture.Army("nearby", GeographyFixture.FactionOne, GeographyFixture.A);
        GeographicWorldState geography = new(GeographyFixture.Snapshot([nearby]));

        ReinforcementCandidate reinforcement = Assert.Single(geography.GetReinforcements(
            GeographyFixture.B,
            GeographyFixture.FactionOne,
            TransportMode.Foot,
            2_500));
        BattleLocationDescriptor descriptor = geography.Graph.GetBattleLocation(GeographyFixture.RiverBd, CampaignWeather.Rain);

        Assert.Equal(nearby.ArmyId, reinforcement.ArmyId);
        Assert.Equal([GeographyFixture.RoadAb], reinforcement.RouteIds);
        Assert.Equal(TerrainType.River, descriptor.Terrain);
        Assert.True(descriptor.IsRiverCrossing);
        Assert.DoesNotContain("Godot", System.Text.Json.JsonSerializer.Serialize(descriptor, SimulationJson.CreateOptions()), StringComparison.Ordinal);
    }

    [Fact]
    public void GeographyPersistsInWorldSnapshotAndChecksum()
    {
        ArmyGeographicState army = GeographyFixture.Army("saved", GeographyFixture.FactionOne, GeographyFixture.A);
        WorldState source = GeographyFixture.World(GeographyFixture.Snapshot([army]));

        WorldSnapshot snapshot = source.CaptureSnapshot();
        WorldState restored = WorldState.Restore(snapshot);

        Assert.Equal(SimulationChecksum.Compute(snapshot), SimulationChecksum.Compute(restored.CaptureSnapshot()));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(snapshot.Geography, SimulationJson.CreateOptions()),
            System.Text.Json.JsonSerializer.Serialize(restored.CaptureSnapshot().Geography, SimulationJson.CreateOptions()));
        Assert.True(restored.Geography.TryGetArmy(army.ArmyId, out _));
    }
}
