using System.Text.Json;
using Simulation.Core;

namespace Game.Content;

public static class GeographicContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJson.CreateOptions();

    public static GeographicWorldSnapshot LoadScenario(ContentRegistry registry, EntityId scenarioId)
    {
        NormalizedContentRecord scenarioRecord = GetRecord(registry, scenarioId, "geography_scenario");
        GeographyScenarioData scenario = Deserialize<GeographyScenarioData>(scenarioRecord);
        _ = GetDiplomaticRelations(scenario);
        GeographicGraphDefinition graph = new(
            scenario.RegionIds.Select(id =>
            {
                NormalizedContentRecord record = GetRecord(registry, id, "region");
                RegionData data = Deserialize<RegionData>(record);
                return new Region(record.Id, data.NameKey, data.LabelKey, data.Population, data.CultureId, data.Anchor);
            }).ToArray(),
            scenario.DistrictIds.Select(id =>
            {
                NormalizedContentRecord record = GetRecord(registry, id, "district");
                DistrictData data = Deserialize<DistrictData>(record);
                return new District(record.Id, data.RegionId, data.NameKey, data.LabelKey, data.Population, data.CultureId, data.Anchor);
            }).ToArray(),
            scenario.LocalityIds.Select(id =>
            {
                NormalizedContentRecord record = GetRecord(registry, id, "locality");
                LocalityData data = Deserialize<LocalityData>(record);
                return new Locality(record.Id, data.DistrictId, data.NameKey, data.LabelKey, data.Population, data.CultureId, data.Terrain, data.Anchor);
            }).ToArray(),
            scenario.StopIds.Select(id =>
            {
                NormalizedContentRecord record = GetRecord(registry, id, "route_stop");
                RouteStopData data = Deserialize<RouteStopData>(record);
                return new RouteStop(record.Id, data.LocalityId, data.NameKey, data.StopType, data.Terrain, data.Position, data.BattleFronts);
            }).ToArray(),
            scenario.RouteIds.Select(id =>
            {
                NormalizedContentRecord record = GetRecord(registry, id, "route");
                RouteData data = Deserialize<RouteData>(record);
                return new Route(
                    record.Id,
                    data.NameKey,
                    data.FromStopId,
                    data.ToStopId,
                    data.RouteType,
                    data.Capacity,
                    data.TraversalCost,
                    data.PermittedModes,
                    data.SupplyThroughput,
                    data.Modifiers);
            }).ToArray());

        return new GeographicWorldSnapshot(
            graph,
            scenario.Season,
            scenario.Weather,
            scenario.Locations,
            scenario.RouteStates,
            scenario.Armies).Canonicalize();
    }

    public static GeographicWorldSnapshot LoadSingleScenario(ContentRegistry registry) =>
        LoadScenario(registry, GetSingleScenarioRecord(registry).Id);

    public static GeographicRuntimeArtifact CreateRuntimeArtifact(ContentRegistry registry)
    {
        NormalizedContentRecord scenarioRecord = GetSingleScenarioRecord(registry);
        GeographyScenarioData scenario = Deserialize<GeographyScenarioData>(scenarioRecord);
        GeographicDiplomaticRelation[] relations = GetDiplomaticRelations(scenario);

        return new GeographicRuntimeArtifact(
            1,
            registry.Checksum,
            LoadScenario(registry, scenarioRecord.Id),
            registry.LocalizationEntries)
        {
            DiplomaticRelations = relations,
        };
    }

    private static GeographicDiplomaticRelation[] GetDiplomaticRelations(GeographyScenarioData scenario)
    {
        GeographicDiplomaticRelation[] relations = (scenario.DiplomaticRelations ?? [])
            .OrderBy(relation => relation.ObserverId)
            .ThenBy(relation => relation.CounterpartyId)
            .ToArray();
        if (relations.Any(relation => !relation.ObserverId.IsValid
                || !relation.CounterpartyId.IsValid
                || relation.ObserverId == relation.CounterpartyId
                || relation.Relation is not (DiplomaticRelationCategory.Friendly
                    or DiplomaticRelationCategory.Neutral
                    or DiplomaticRelationCategory.Hostile))
            || relations.Select(relation => (relation.ObserverId, relation.CounterpartyId)).Distinct().Count()
                != relations.Length)
        {
            throw new InvalidDataException("Geography scenario diplomatic relations must have unique valid observer/counterparty pairs and a friendly, neutral, or hostile category.");
        }

        return relations;
    }

    private static NormalizedContentRecord GetSingleScenarioRecord(ContentRegistry registry)
    {
        NormalizedContentRecord[] scenarios = registry.Records
            .Where(record => record.RecordType == "geography_scenario")
            .OrderBy(record => record.Id)
            .ToArray();
        return scenarios.Length switch
        {
            1 => scenarios[0],
            0 => throw new InvalidDataException("No geography_scenario record is loaded."),
            _ => throw new InvalidDataException("More than one geography_scenario is loaded; select one explicitly."),
        };
    }

    private static NormalizedContentRecord GetRecord(ContentRegistry registry, EntityId id, string expectedType)
    {
        if (!registry.TryGet(id, out NormalizedContentRecord? record))
        {
            throw new InvalidDataException($"Geography references missing content record '{id}'.");
        }

        if (!StringComparer.Ordinal.Equals(record.RecordType, expectedType))
        {
            throw new InvalidDataException($"Geography record '{id}' must have type '{expectedType}', found '{record.RecordType}'.");
        }

        return record;
    }

    private static T Deserialize<T>(NormalizedContentRecord record) => record.Data.Deserialize<T>(JsonOptions)
        ?? throw new InvalidDataException($"Geography record '{record.Id}' has empty data.");

    private sealed record RegionData(
        EntityId NameKey,
        EntityId LabelKey,
        long Population,
        EntityId CultureId,
        MapPoint Anchor,
        IReadOnlyList<EntityId> References);

    private sealed record DistrictData(
        EntityId RegionId,
        EntityId NameKey,
        EntityId LabelKey,
        long Population,
        EntityId CultureId,
        MapPoint Anchor,
        IReadOnlyList<EntityId> References);

    private sealed record LocalityData(
        EntityId DistrictId,
        EntityId NameKey,
        EntityId LabelKey,
        long Population,
        EntityId CultureId,
        TerrainType Terrain,
        MapPoint Anchor,
        IReadOnlyList<EntityId> References);

    private sealed record RouteStopData(
        EntityId LocalityId,
        EntityId NameKey,
        RouteStopType StopType,
        TerrainType Terrain,
        MapPoint Position,
        IReadOnlyList<BattleFrontType> BattleFronts,
        IReadOnlyList<EntityId> References);

    private sealed record RouteData(
        EntityId NameKey,
        EntityId FromStopId,
        EntityId ToStopId,
        RouteType RouteType,
        int Capacity,
        int TraversalCost,
        IReadOnlyList<TransportMode> PermittedModes,
        int SupplyThroughput,
        IReadOnlyList<RouteModifier> Modifiers,
        IReadOnlyList<EntityId> References);

    private sealed record GeographyScenarioData(
        IReadOnlyList<EntityId> RegionIds,
        IReadOnlyList<EntityId> DistrictIds,
        IReadOnlyList<EntityId> LocalityIds,
        IReadOnlyList<EntityId> StopIds,
        IReadOnlyList<EntityId> RouteIds,
        CampaignSeason Season,
        CampaignWeather Weather,
        IReadOnlyList<LocationState> Locations,
        IReadOnlyList<RouteState> RouteStates,
        IReadOnlyList<ArmyGeographicState> Armies,
        IReadOnlyList<EntityId> References,
        IReadOnlyList<GeographicDiplomaticRelation>? DiplomaticRelations = null);
}

public sealed record GeographicRuntimeArtifact(
    int SchemaVersion,
    string ContentChecksum,
    GeographicWorldSnapshot Geography,
    IReadOnlyList<LocalizationEntry> Localization)
{
    // Additive schema-1 field: older artifacts deserialize with an empty relation set.
    public IReadOnlyList<GeographicDiplomaticRelation> DiplomaticRelations { get; init; } = [];
}

public sealed record GeographicDiplomaticRelation(
    EntityId ObserverId,
    EntityId CounterpartyId,
    DiplomaticRelationCategory Relation);
