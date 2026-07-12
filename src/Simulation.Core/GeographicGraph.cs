using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class GeographicGraph
{
    private readonly FrozenDictionary<EntityId, Region> regions;
    private readonly FrozenDictionary<EntityId, District> districts;
    private readonly FrozenDictionary<EntityId, Locality> localities;
    private readonly FrozenDictionary<EntityId, RouteStop> stops;
    private readonly FrozenDictionary<EntityId, Route> routes;
    private readonly FrozenDictionary<EntityId, IReadOnlyList<Route>> routesByStop;

    public GeographicGraph(GeographicGraphDefinition definition)
    {
        Definition = Validate(definition).Canonicalize();
        regions = Definition.Regions.ToFrozenDictionary(item => item.Id);
        districts = Definition.Districts.ToFrozenDictionary(item => item.Id);
        localities = Definition.Localities.ToFrozenDictionary(item => item.Id);
        stops = Definition.Stops.ToFrozenDictionary(item => item.Id);
        routes = Definition.Routes.ToFrozenDictionary(item => item.Id);
        routesByStop = Definition.Stops.ToFrozenDictionary(
            stop => stop.Id,
            stop => (IReadOnlyList<Route>)Definition.Routes
                .Where(route => route.FromStopId == stop.Id || route.ToStopId == stop.Id)
                .OrderBy(route => route.Id)
                .ToArray());
    }

    public GeographicGraphDefinition Definition { get; }

    public bool TryGetRegion(EntityId id, [NotNullWhen(true)] out Region? region) => regions.TryGetValue(id, out region);

    public bool TryGetDistrict(EntityId id, [NotNullWhen(true)] out District? district) => districts.TryGetValue(id, out district);

    public bool TryGetLocality(EntityId id, [NotNullWhen(true)] out Locality? locality) => localities.TryGetValue(id, out locality);

    public bool TryGetStop(EntityId id, [NotNullWhen(true)] out RouteStop? stop) => stops.TryGetValue(id, out stop);

    public bool TryGetRoute(EntityId id, [NotNullWhen(true)] out Route? route) => routes.TryGetValue(id, out route);

    public IReadOnlyList<Route> GetRoutes(EntityId stopId) => routesByStop.TryGetValue(stopId, out IReadOnlyList<Route>? connected)
        ? connected
        : [];

    public EntityId GetOtherStop(Route route, EntityId stopId) => route.FromStopId == stopId
        ? route.ToStopId
        : route.ToStopId == stopId
            ? route.FromStopId
            : throw new SimulationValidationException($"Stop '{stopId}' is not an endpoint of route '{route.Id}'.");

    public PathResult? FindPath(
        EntityId fromStopId,
        EntityId toStopId,
        TransportMode mode,
        Func<Route, bool>? isAvailable = null,
        Func<Route, int>? getCost = null)
    {
        if (!stops.ContainsKey(fromStopId) || !stops.ContainsKey(toStopId))
        {
            return null;
        }

        if (fromStopId == toStopId)
        {
            return new PathResult([], [fromStopId], 0);
        }

        Dictionary<EntityId, int> costs = stops.Keys.ToDictionary(id => id, _ => int.MaxValue);
        Dictionary<EntityId, string> signatures = stops.Keys.ToDictionary(id => id, _ => string.Empty);
        Dictionary<EntityId, (EntityId Previous, EntityId Route)> previous = [];
        HashSet<EntityId> remaining = stops.Keys.ToHashSet();
        costs[fromStopId] = 0;
        signatures[fromStopId] = fromStopId.Value;

        while (remaining.Count > 0)
        {
            EntityId current = remaining
                .OrderBy(id => costs[id])
                .ThenBy(id => signatures[id], StringComparer.Ordinal)
                .ThenBy(id => id)
                .First();
            if (costs[current] == int.MaxValue)
            {
                break;
            }

            remaining.Remove(current);
            if (current == toStopId)
            {
                break;
            }

            foreach (Route route in GetRoutes(current))
            {
                if (!route.PermittedModes.Contains(mode) || (isAvailable is not null && !isAvailable(route)))
                {
                    continue;
                }

                EntityId adjacent = GetOtherStop(route, current);
                if (!remaining.Contains(adjacent))
                {
                    continue;
                }

                int routeCost = getCost?.Invoke(route) ?? route.TraversalCost;
                int candidate = checked(costs[current] + routeCost);
                string signature = $"{signatures[current]}\n{route.Id.Value}";
                if (candidate < costs[adjacent]
                    || (candidate == costs[adjacent]
                        && StringComparer.Ordinal.Compare(signature, signatures[adjacent]) < 0))
                {
                    costs[adjacent] = candidate;
                    signatures[adjacent] = signature;
                    previous[adjacent] = (current, route.Id);
                }
            }
        }

        if (!previous.ContainsKey(toStopId))
        {
            return null;
        }

        List<EntityId> routeIds = [];
        List<EntityId> stopIds = [toStopId];
        EntityId cursor = toStopId;
        while (cursor != fromStopId)
        {
            (EntityId prior, EntityId route) = previous[cursor];
            routeIds.Add(route);
            stopIds.Add(prior);
            cursor = prior;
        }

        routeIds.Reverse();
        stopIds.Reverse();
        return new PathResult(routeIds, stopIds, costs[toStopId]);
    }

    public BattleLocationDescriptor GetBattleLocation(
        EntityId locationId,
        CampaignWeather weather)
    {
        if (stops.TryGetValue(locationId, out RouteStop? stop))
        {
            EntityId[] reinforcementRoutes = GetRoutes(stop.Id).Select(route => route.Id).Order().ToArray();
            return new BattleLocationDescriptor(
                stop.Id,
                stop.Terrain,
                weather,
                stop.Position.Elevation,
                stop.BattleFronts.Order().ToArray(),
                stop.StopType is RouteStopType.Ferry or RouteStopType.Bridge,
                stop.StopType == RouteStopType.Port,
                reinforcementRoutes);
        }

        if (routes.TryGetValue(locationId, out Route? route))
        {
            RouteStop from = stops[route.FromStopId];
            RouteStop to = stops[route.ToStopId];
            TerrainType terrain = route.RouteType switch
            {
                RouteType.River => TerrainType.River,
                RouteType.CoastalLane => TerrainType.Coast,
                RouteType.OpenSeaLane => TerrainType.OpenSea,
                RouteType.MountainPath => TerrainType.Mountains,
                _ => from.Terrain,
            };
            BattleFrontType[] fronts = route.RouteType switch
            {
                RouteType.River => [BattleFrontType.RiverCrossing],
                RouteType.CoastalLane => [BattleFrontType.Shoreline, BattleFrontType.Port],
                RouteType.OpenSeaLane => [BattleFrontType.Shoreline],
                RouteType.MountainPath => [BattleFrontType.MountainPass],
                _ => [BattleFrontType.Field],
            };
            EntityId[] reinforcementRoutes = GetRoutes(from.Id)
                .Concat(GetRoutes(to.Id))
                .Select(item => item.Id)
                .Where(id => id != route.Id)
                .Distinct()
                .Order()
                .ToArray();
            return new BattleLocationDescriptor(
                route.Id,
                terrain,
                weather,
                (from.Position.Elevation + to.Position.Elevation) / 2,
                fronts,
                route.RouteType == RouteType.River,
                route.RouteType is RouteType.CoastalLane or RouteType.OpenSeaLane,
                reinforcementRoutes);
        }

        throw new SimulationValidationException($"Unknown battle location '{locationId}'.");
    }

    private static GeographicGraphDefinition Validate(GeographicGraphDefinition definition)
    {
        if (definition.Regions is null
            || definition.Districts is null
            || definition.Localities is null
            || definition.Stops is null
            || definition.Routes is null)
        {
            throw new SimulationValidationException("Geographic graph collections cannot be null.");
        }

        ValidateUnique(definition.Regions.Select(item => item.Id), "region");
        ValidateUnique(definition.Districts.Select(item => item.Id), "district");
        ValidateUnique(definition.Localities.Select(item => item.Id), "locality");
        ValidateUnique(definition.Stops.Select(item => item.Id), "route stop");
        ValidateUnique(definition.Routes.Select(item => item.Id), "route");

        HashSet<EntityId> regionIds = definition.Regions.Select(item => item.Id).ToHashSet();
        HashSet<EntityId> districtIds = definition.Districts.Select(item => item.Id).ToHashSet();
        HashSet<EntityId> localityIds = definition.Localities.Select(item => item.Id).ToHashSet();
        HashSet<EntityId> stopIds = definition.Stops.Select(item => item.Id).ToHashSet();
        if (definition.Regions.Any(item => item.Population < 0 || !ValidIdentity(item.Id, item.NameKey, item.LabelKey)))
        {
            throw new SimulationValidationException("Region identity, localization, or population is invalid.");
        }

        if (definition.Districts.Any(item => item.Population < 0
            || !ValidIdentity(item.Id, item.NameKey, item.LabelKey)
            || !regionIds.Contains(item.RegionId)))
        {
            throw new SimulationValidationException("District containment or identity is invalid.");
        }

        if (definition.Localities.Any(item => item.Population < 0
            || !ValidIdentity(item.Id, item.NameKey, item.LabelKey)
            || !districtIds.Contains(item.DistrictId)
            || !Enum.IsDefined(item.Terrain)))
        {
            throw new SimulationValidationException("Locality containment or identity is invalid.");
        }

        if (definition.Stops.Any(item => !item.Id.IsValid
            || !item.NameKey.IsValid
            || !localityIds.Contains(item.LocalityId)
            || !Enum.IsDefined(item.StopType)
            || !Enum.IsDefined(item.Terrain)
            || item.BattleFronts.Count == 0
            || item.BattleFronts.Any(front => !Enum.IsDefined(front))))
        {
            throw new SimulationValidationException("Route-stop placement, type, or battle fronts are invalid.");
        }

        if (definition.Stops.GroupBy(item => item.Position).Any(group => group.Count() > 1))
        {
            throw new SimulationValidationException("Route stops cannot share an identical map position.");
        }

        foreach (Route route in definition.Routes)
        {
            if (!route.Id.IsValid
                || !route.NameKey.IsValid
                || !stopIds.Contains(route.FromStopId)
                || !stopIds.Contains(route.ToStopId)
                || route.FromStopId == route.ToStopId
                || route.Capacity <= 0
                || route.TraversalCost <= 0
                || route.SupplyThroughput <= 0
                || route.PermittedModes.Count == 0
                || route.PermittedModes.Distinct().Count() != route.PermittedModes.Count
                || route.PermittedModes.Any(mode => !Enum.IsDefined(mode))
                || !Enum.IsDefined(route.RouteType)
                || route.Modifiers.Any(modifier => modifier.TraversalPermille is < 1 or > 10_000
                    || modifier.CapacityPermille is < 0 or > 1_000
                    || (modifier.Season is not null && !Enum.IsDefined(modifier.Season.Value))
                    || (modifier.Weather is not null && !Enum.IsDefined(modifier.Weather.Value)))
                || route.Modifiers.GroupBy(modifier => (modifier.Season, modifier.Weather)).Any(group => group.Count() > 1))
            {
                throw new SimulationValidationException($"Route '{route.Id}' is invalid.");
            }

            ValidateWaterRoute(route, definition.Stops);
        }

        return definition;
    }

    private static void ValidateWaterRoute(Route route, IReadOnlyList<RouteStop> stops)
    {
        TransportMode? requiredMode = route.RouteType switch
        {
            RouteType.River => TransportMode.RiverBoat,
            RouteType.CoastalLane => TransportMode.CoastalShip,
            RouteType.OpenSeaLane => TransportMode.OceanShip,
            _ => null,
        };
        if (requiredMode is null)
        {
            return;
        }

        RouteStop from = stops.Single(item => item.Id == route.FromStopId);
        RouteStop to = stops.Single(item => item.Id == route.ToStopId);
        if (!route.PermittedModes.Contains(requiredMode.Value)
            || from.StopType is not (RouteStopType.Port or RouteStopType.Ferry)
            || to.StopType is not (RouteStopType.Port or RouteStopType.Ferry))
        {
            throw new SimulationValidationException($"Water route '{route.Id}' requires suitable ports/ferries and transport mode.");
        }
    }

    private static bool ValidIdentity(EntityId id, EntityId nameKey, EntityId labelKey) =>
        id.IsValid && nameKey.IsValid && labelKey.IsValid;

    private static void ValidateUnique(IEnumerable<EntityId> ids, string kind)
    {
        EntityId[] values = ids.ToArray();
        if (values.Any(id => !id.IsValid) || values.Distinct().Count() != values.Length)
        {
            throw new SimulationValidationException($"Geographic graph contains invalid or duplicate {kind} IDs.");
        }
    }
}
