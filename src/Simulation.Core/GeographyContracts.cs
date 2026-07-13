using System.Text.Json.Serialization;

namespace Simulation.Core;

public enum GeographicAreaKind
{
    Region,
    District,
    Locality,
}

public enum RouteType
{
    Road,
    MountainPath,
    River,
    CoastalLane,
    OpenSeaLane,
    FrontierTrail,
    SeasonalPassage,
}

public enum RouteStopType
{
    Settlement,
    Port,
    Ferry,
    Bridge,
    Pass,
    Gate,
    Fort,
    Watchtower,
    Camp,
    Depot,
    NaturalBattlefield,
}

public enum TransportMode
{
    Foot,
    Horse,
    Wagon,
    RiverBoat,
    CoastalShip,
    OceanShip,
}

public enum TerrainType
{
    Plains,
    Hills,
    Mountains,
    Forest,
    Marsh,
    River,
    Coast,
    OpenSea,
    Urban,
}

public enum CampaignSeason
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

public enum CampaignWeather
{
    Clear,
    Rain,
    Storm,
    Snow,
    Flood,
    Drought,
}

public enum RouteControlState
{
    Open,
    Controlled,
    Contested,
    Blockaded,
}

public enum MovementStance
{
    Cautious,
    Normal,
    Forced,
    Ambush,
}

public enum MovementFallback
{
    Wait,
    Stop,
    Reroute,
}

public enum MovementEventKind
{
    Ordered,
    Advanced,
    Arrived,
    Blocked,
    Rerouted,
    Retreated,
    Cancelled,
}

public enum ClaimStrength
{
    Weak,
    Pressed,
    Strong,
}

public enum IntelligenceLevel
{
    Unknown,
    Rumored,
    Observed,
    Current,
}

public enum DiplomaticRelationCategory
{
    Unknown,
    Self,
    Uncontrolled,
    Friendly,
    Neutral,
    Hostile,
}

public enum BattleFrontType
{
    Field,
    RiverCrossing,
    Shoreline,
    Port,
    Gate,
    Settlement,
    MountainPass,
    Fortification,
}

public enum CampaignMapMode
{
    PoliticalControl,
    Claims,
    Administration,
    Diplomacy,
    Supply,
    Population,
    Culture,
    Intelligence,
    Routes,
}

public readonly record struct MapPoint(int X, int Y, int Elevation);

public sealed record Region(
    EntityId Id,
    EntityId NameKey,
    EntityId LabelKey,
    long Population,
    EntityId CultureId,
    MapPoint Anchor);

public sealed record District(
    EntityId Id,
    EntityId RegionId,
    EntityId NameKey,
    EntityId LabelKey,
    long Population,
    EntityId CultureId,
    MapPoint Anchor);

public sealed record Locality(
    EntityId Id,
    EntityId DistrictId,
    EntityId NameKey,
    EntityId LabelKey,
    long Population,
    EntityId CultureId,
    TerrainType Terrain,
    MapPoint Anchor);

public sealed record RouteStop(
    EntityId Id,
    EntityId LocalityId,
    EntityId NameKey,
    RouteStopType StopType,
    TerrainType Terrain,
    MapPoint Position,
    IReadOnlyList<BattleFrontType> BattleFronts);

public sealed record RouteModifier(
    CampaignSeason? Season,
    CampaignWeather? Weather,
    int TraversalPermille,
    int CapacityPermille,
    bool Closed);

public sealed record Route(
    EntityId Id,
    EntityId NameKey,
    EntityId FromStopId,
    EntityId ToStopId,
    RouteType RouteType,
    int Capacity,
    int TraversalCost,
    IReadOnlyList<TransportMode> PermittedModes,
    int SupplyThroughput,
    IReadOnlyList<RouteModifier> Modifiers);

public sealed record GeographicGraphDefinition(
    IReadOnlyList<Region> Regions,
    IReadOnlyList<District> Districts,
    IReadOnlyList<Locality> Localities,
    IReadOnlyList<RouteStop> Stops,
    IReadOnlyList<Route> Routes)
{
    public static GeographicGraphDefinition Empty { get; } = new([], [], [], [], []);

    public GeographicGraphDefinition Canonicalize() => this with
    {
        Regions = Regions.OrderBy(item => item.Id).ToArray(),
        Districts = Districts.OrderBy(item => item.Id).ToArray(),
        Localities = Localities.OrderBy(item => item.Id).ToArray(),
        Stops = Stops.OrderBy(item => item.Id)
            .Select(item => item with { BattleFronts = item.BattleFronts.Order().ToArray() })
            .ToArray(),
        Routes = Routes.OrderBy(item => item.Id)
            .Select(item => item with
            {
                PermittedModes = item.PermittedModes.Order().ToArray(),
                Modifiers = item.Modifiers
                    .OrderBy(modifier => modifier.Season)
                    .ThenBy(modifier => modifier.Weather)
                    .ToArray(),
            })
            .ToArray(),
    };
}

public sealed record ClaimState(EntityId ClaimantId, ClaimStrength Strength, string Basis);

public sealed record LocationIntelligence(
    EntityId ObserverId,
    IntelligenceLevel Level,
    long LastObservedTurn);

public sealed record LocationState(
    EntityId StopId,
    EntityId? ControllerId,
    EntityId? LegalAppointeeId,
    int LocalAcceptance,
    IReadOnlyList<ClaimState> Claims,
    bool Occupied,
    IReadOnlyList<LocationIntelligence> Intelligence,
    long Stores,
    long DailyProduction);

public sealed record RouteState(
    EntityId RouteId,
    EntityId? ControllerId,
    RouteControlState ControlState,
    IReadOnlyList<EntityId> PermittedFactionIds,
    int DisruptionPermille);

public sealed record ArmyGeographicState(
    EntityId ArmyId,
    EntityId FactionId,
    EntityId CurrentStopId,
    EntityId? ActiveRouteId,
    EntityId? RouteFromStopId,
    EntityId? RouteToStopId,
    int RouteProgress,
    IReadOnlyList<EntityId> PlannedRouteIds,
    EntityId? DestinationStopId,
    TransportMode TransportMode,
    MovementStance Stance,
    MovementFallback Fallback,
    int Scouting,
    int Strength,
    long Supply,
    long DailySupplyDemand);

public sealed record GeographicWorldSnapshot(
    GeographicGraphDefinition Graph,
    CampaignSeason Season,
    CampaignWeather Weather,
    IReadOnlyList<LocationState> Locations,
    IReadOnlyList<RouteState> Routes,
    IReadOnlyList<ArmyGeographicState> Armies)
{
    public static GeographicWorldSnapshot Empty { get; } = new(
        GeographicGraphDefinition.Empty,
        CampaignSeason.Spring,
        CampaignWeather.Clear,
        [],
        [],
        []);

    public GeographicWorldSnapshot Canonicalize() => this with
    {
        Graph = Graph.Canonicalize(),
        Locations = Locations.OrderBy(item => item.StopId)
            .Select(item => item with
            {
                Claims = item.Claims.OrderBy(claim => claim.ClaimantId).ToArray(),
                Intelligence = item.Intelligence.OrderBy(intelligence => intelligence.ObserverId).ToArray(),
            })
            .ToArray(),
        Routes = Routes.OrderBy(item => item.RouteId)
            .Select(item => item with
            {
                PermittedFactionIds = item.PermittedFactionIds.Order().ToArray(),
            })
            .ToArray(),
        Armies = Armies.OrderBy(item => item.ArmyId)
            .Select(item => item with { PlannedRouteIds = item.PlannedRouteIds.ToArray() })
            .ToArray(),
    };
}

public sealed record KnownLocationState(
    EntityId StopId,
    IntelligenceLevel Intelligence,
    EntityId? ControllerId,
    EntityId? LegalAppointeeId,
    int? LocalAcceptance,
    IReadOnlyList<ClaimState> Claims,
    bool? Occupied,
    long? Stores);

public sealed record KnownLocationPresentationState(
    EntityId StopId,
    IntelligenceLevel Intelligence,
    EntityId? ControllerId,
    DiplomaticRelationCategory DiplomaticRelation,
    long? Stores,
    long? DailyProduction,
    long? StationedArmyDailyDemand,
    long? StationedArmyDailyShortage,
    KnownLocationState PoliticalState,
    long? Population,
    EntityId? CultureId);

public sealed record KnownRoutePresentationState(
    EntityId RouteId,
    IntelligenceLevel Intelligence,
    int? Capacity,
    int? SupplyThroughput,
    int? EffectiveSupplyThroughput,
    EntityId? ControllerId,
    RouteControlState? ControlState,
    int? DisruptionPermille,
    bool? AvailableToObserver);

public sealed record CampaignMapPresentationState(
    IReadOnlyList<KnownLocationPresentationState> Locations,
    IReadOnlyList<KnownRoutePresentationState> Routes);

public sealed record BattleLocationDescriptor(
    EntityId LocationId,
    TerrainType Terrain,
    CampaignWeather Weather,
    int Elevation,
    IReadOnlyList<BattleFrontType> Fronts,
    bool IsRiverCrossing,
    bool IsCoastal,
    IReadOnlyList<EntityId> ReinforcementRoutes);

public sealed record GeographicContext(
    EntityId LocationId,
    EntityId LocalityId,
    EntityId DistrictId,
    EntityId RegionId,
    IReadOnlyList<EntityId> AdjacentStopIds,
    IReadOnlyList<EntityId> RouteIds,
    TerrainType Terrain,
    CampaignSeason Season,
    CampaignWeather Weather,
    KnownLocationState PoliticalState,
    EntityId CultureId,
    long Population,
    BattleLocationDescriptor BattleLocation);

public sealed record PathResult(
    IReadOnlyList<EntityId> RouteIds,
    IReadOnlyList<EntityId> StopIds,
    int TotalCost);

public sealed record ReinforcementCandidate(EntityId ArmyId, int RouteCost, IReadOnlyList<EntityId> RouteIds);

public interface IGeographicCommandPayload : ICampaignCommandPayload;

public sealed record MovementOrderPayload(
    EntityId ArmyId,
    IReadOnlyList<EntityId> PlannedRouteIds,
    TransportMode TransportMode,
    MovementStance Stance,
    CampaignDate Departure,
    MovementFallback Fallback)
    : IGeographicCommandPayload;

public sealed record RetreatOrderPayload(EntityId ArmyId, EntityId PreferredStopId, TransportMode TransportMode)
    : IGeographicCommandPayload;

public sealed record SupplyOrderPayload(
    EntityId FactionId,
    EntityId SourceStopId,
    EntityId DestinationStopId,
    IReadOnlyList<EntityId> RouteIds,
    TransportMode TransportMode,
    long RequestedAmount)
    : IGeographicCommandPayload;

public sealed record ChangeControlCommandPayload(
    EntityId StopId,
    EntityId? ControllerId,
    EntityId? LegalAppointeeId,
    int LocalAcceptance,
    IReadOnlyList<ClaimState> Claims,
    bool Occupied)
    : IGeographicCommandPayload;

public sealed record MovementEventPayload(
    EntityId ArmyId,
    MovementEventKind Kind,
    EntityId CurrentStopId,
    EntityId? ActiveRouteId,
    EntityId? RouteFromStopId,
    EntityId? RouteToStopId,
    int RouteProgress,
    IReadOnlyList<EntityId> RemainingRouteIds,
    EntityId? DestinationStopId,
    TransportMode TransportMode,
    MovementStance Stance,
    MovementFallback Fallback,
    string ReasonCode)
    : ICampaignEventPayload;

public sealed record InterceptionEventPayload(
    EntityId FirstArmyId,
    EntityId SecondArmyId,
    EntityId RouteId,
    int ProgressFromRouteStart,
    EntityId InterceptorArmyId,
    BattleLocationDescriptor Location)
    : ICampaignEventPayload;

public sealed record ControlChangedEventPayload(
    EntityId StopId,
    EntityId? PreviousControllerId,
    EntityId? ControllerId,
    EntityId? LegalAppointeeId,
    int LocalAcceptance,
    IReadOnlyList<ClaimState> Claims,
    bool Occupied)
    : ICampaignEventPayload;

public sealed record SupplyTransferredEventPayload(
    EntityId FactionId,
    EntityId SourceStopId,
    EntityId DestinationStopId,
    IReadOnlyList<EntityId> RouteIds,
    TransportMode TransportMode,
    long RequestedAmount,
    long TransferredAmount,
    long BottleneckCapacity)
    : ICampaignEventPayload;

public sealed record SupplyProducedEventPayload(EntityId StopId, long Amount)
    : ICampaignEventPayload;

public sealed record ArmySupplyConsumedEventPayload(
    EntityId ArmyId,
    long Demand,
    long Consumed,
    long Shortage)
    : ICampaignEventPayload;
