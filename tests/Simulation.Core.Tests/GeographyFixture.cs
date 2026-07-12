using Simulation.Core;

namespace Simulation.Core.Tests;

internal static class GeographyFixture
{
    public static readonly EntityId A = new("stop:test/a");
    public static readonly EntityId B = new("stop:test/b");
    public static readonly EntityId C = new("stop:test/c");
    public static readonly EntityId D = new("stop:test/d");
    public static readonly EntityId RoadAb = new("route:test/a_b");
    public static readonly EntityId RoadBc = new("route:test/b_c");
    public static readonly EntityId SeasonalAc = new("route:test/a_c");
    public static readonly EntityId RiverBd = new("route:test/b_d");
    public static readonly EntityId FactionOne = new("faction:test/one");
    public static readonly EntityId FactionTwo = new("faction:test/two");
    public static readonly EntityId Actor = new("actor:test/commander");

    public static GeographicWorldSnapshot Snapshot(
        IReadOnlyList<ArmyGeographicState>? armies = null,
        CampaignSeason season = CampaignSeason.Autumn,
        CampaignWeather weather = CampaignWeather.Clear,
        IReadOnlyList<RouteState>? routeStates = null)
    {
        GeographicGraphDefinition graph = new(
            [new Region(new EntityId("region:test/core"), new EntityId("loc:test/region"), new EntityId("loc:test/zhou"), 1_000, new EntityId("culture:test/han"), new MapPoint(50, 50, 0))],
            [new District(new EntityId("district:test/core"), new EntityId("region:test/core"), new EntityId("loc:test/district"), new EntityId("loc:test/jun"), 900, new EntityId("culture:test/han"), new MapPoint(50, 50, 0))],
            [
                Locality("a", TerrainType.Plains, 0, 0),
                Locality("b", TerrainType.River, 100, 0),
                Locality("c", TerrainType.Mountains, 200, 0),
                Locality("d", TerrainType.River, 100, -100),
            ],
            [
                Stop(A, "a", RouteStopType.Settlement, TerrainType.Plains, 0, 0),
                Stop(B, "b", RouteStopType.Port, TerrainType.River, 100, 0),
                Stop(C, "c", RouteStopType.Pass, TerrainType.Mountains, 200, 0),
                Stop(D, "d", RouteStopType.Ferry, TerrainType.River, 100, -100),
            ],
            [
                new Route(RoadAb, new EntityId("loc:test/road_ab"), A, B, RouteType.Road, 5_000, 2_000, [TransportMode.Foot, TransportMode.Horse, TransportMode.Wagon], 4_000, []),
                new Route(RoadBc, new EntityId("loc:test/road_bc"), B, C, RouteType.Road, 4_000, 1_500, [TransportMode.Foot, TransportMode.Horse, TransportMode.Wagon], 3_000, []),
                new Route(SeasonalAc, new EntityId("loc:test/seasonal_ac"), A, C, RouteType.SeasonalPassage, 2_000, 500, [TransportMode.Foot, TransportMode.Horse], 1_000, [new RouteModifier(CampaignSeason.Winter, null, 2_000, 0, true)]),
                new Route(RiverBd, new EntityId("loc:test/river_bd"), B, D, RouteType.River, 3_000, 1_000, [TransportMode.RiverBoat], 2_500, []),
            ]);
        LocationState[] locations = new[] { A, B, C, D }
            .Select((id, index) => new LocationState(
                id,
                index < 2 ? FactionOne : FactionTwo,
                index == 0 ? Actor : null,
                500 + index * 10,
                index == 2 ? [new ClaimState(FactionOne, ClaimStrength.Pressed, "test claim")] : [],
                index == 1,
                [new LocationIntelligence(FactionOne, IntelligenceLevel.Current, 0), new LocationIntelligence(FactionTwo, IntelligenceLevel.Observed, 0)],
                index == 0 ? 10_000 : 1_000,
                index == 0 ? 100 : 10))
            .ToArray();
        RouteState[] states = routeStates?.ToArray()
            ?? graph.Routes.Select(route => new RouteState(route.Id, null, RouteControlState.Open, [], 0)).ToArray();
        return new GeographicWorldSnapshot(graph, season, weather, locations, states, armies ?? []);
    }

    public static ArmyGeographicState Army(
        string id,
        EntityId faction,
        EntityId stop,
        int scouting = 50,
        int strength = 1_000) => new(
        new EntityId($"army:test/{id}"),
        faction,
        stop,
        null,
        null,
        null,
        0,
        [],
        null,
        TransportMode.Foot,
        MovementStance.Normal,
        MovementFallback.Wait,
        scouting,
        strength,
        2_000,
        100);

    public static WorldState World(GeographicWorldSnapshot geography) => WorldState.Create(
        new CampaignDate(191, 1, 1),
        42,
        [new SyntheticEntitySnapshot(Actor, SimulationTier.Full, 1, 1, 1, [])],
        geography);

    private static Locality Locality(string suffix, TerrainType terrain, int x, int y) => new(
        new EntityId($"locality:test/{suffix}"),
        new EntityId("district:test/core"),
        new EntityId($"loc:test/locality_{suffix}"),
        new EntityId("loc:test/xian"),
        100,
        new EntityId("culture:test/han"),
        terrain,
        new MapPoint(x, y, 0));

    private static RouteStop Stop(EntityId id, string locality, RouteStopType type, TerrainType terrain, int x, int y) => new(
        id,
        new EntityId($"locality:test/{locality}"),
        new EntityId($"loc:test/stop_{locality}"),
        type,
        terrain,
        new MapPoint(x, y, 0),
        type == RouteStopType.Port ? [BattleFrontType.Port, BattleFrontType.RiverCrossing] : [BattleFrontType.Field]);
}
