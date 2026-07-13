using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public interface IGeographicWorldQuery
{
    GeographicGraph Graph { get; }

    CampaignSeason Season { get; }

    CampaignWeather Weather { get; }

    IReadOnlyList<LocationState> Locations { get; }

    IReadOnlyList<RouteState> RouteStates { get; }

    IReadOnlyList<ArmyGeographicState> Armies { get; }

    GeographicContext GetContext(EntityId stopId, EntityId observerId);

    CampaignMapPresentationState GetCampaignMapPresentation(
        EntityId observerId,
        IReadOnlyDictionary<EntityId, DiplomaticRelationCategory>? diplomaticRelations = null);
}

public sealed class GeographicWorldState : IGeographicWorldQuery
{
    private readonly SortedDictionary<EntityId, LocationState> locations = [];
    private readonly SortedDictionary<EntityId, RouteState> routes = [];
    private readonly SortedDictionary<EntityId, ArmyGeographicState> armies = [];
    private readonly Dictionary<EntityId, long> supplyUsedByRoute = [];
    private CampaignDate? supplyUsageDate;

    public GeographicWorldState(GeographicWorldSnapshot snapshot)
    {
        GeographicWorldSnapshot canonical = snapshot.Canonicalize();
        Graph = new GeographicGraph(canonical.Graph);
        Season = canonical.Season;
        Weather = canonical.Weather;
        if (!Enum.IsDefined(Season) || !Enum.IsDefined(Weather))
        {
            throw new SimulationValidationException("Geographic season or weather is invalid.");
        }

        foreach (LocationState location in canonical.Locations)
        {
            ValidateLocation(location);
            if (!locations.TryAdd(location.StopId, Clone(location)))
            {
                throw new SimulationValidationException($"Duplicate location state '{location.StopId}'.");
            }
        }

        foreach (RouteStop stop in Graph.Definition.Stops)
        {
            if (!locations.ContainsKey(stop.Id))
            {
                throw new SimulationValidationException($"Route stop '{stop.Id}' has no scenario location state.");
            }
        }

        foreach (RouteState route in canonical.Routes)
        {
            ValidateRouteState(route);
            if (!routes.TryAdd(route.RouteId, Clone(route)))
            {
                throw new SimulationValidationException($"Duplicate route state '{route.RouteId}'.");
            }
        }

        foreach (Route route in Graph.Definition.Routes)
        {
            if (!routes.ContainsKey(route.Id))
            {
                throw new SimulationValidationException($"Route '{route.Id}' has no scenario route state.");
            }
        }

        foreach (ArmyGeographicState army in canonical.Armies)
        {
            ValidateArmy(army);
            if (!armies.TryAdd(army.ArmyId, Clone(army)))
            {
                throw new SimulationValidationException($"Duplicate geographic army '{army.ArmyId}'.");
            }
        }
    }

    public GeographicGraph Graph { get; }

    public CampaignSeason Season { get; private set; }

    public CampaignWeather Weather { get; private set; }

    public IReadOnlyList<LocationState> Locations => locations.Values.Select(Clone).ToArray();

    public IReadOnlyList<RouteState> RouteStates => routes.Values.Select(Clone).ToArray();

    public IReadOnlyList<ArmyGeographicState> Armies => armies.Values.Select(Clone).ToArray();

    public bool TryGetArmy(EntityId id, [NotNullWhen(true)] out ArmyGeographicState? army)
    {
        if (armies.TryGetValue(id, out ArmyGeographicState? stored))
        {
            army = Clone(stored);
            return true;
        }

        army = null;
        return false;
    }

    public GeographicWorldSnapshot CaptureSnapshot() => new GeographicWorldSnapshot(
        Graph.Definition,
        Season,
        Weather,
        Locations,
        RouteStates,
        Armies).Canonicalize();

    public CommandValidationResult ValidateMovementOrder(MovementOrderPayload order)
    {
        List<ValidationIssue> issues = [];
        if (!armies.TryGetValue(order.ArmyId, out ArmyGeographicState? army))
        {
            issues.Add(new("unknown_army", $"Army '{order.ArmyId}' does not exist."));
            return new(false, issues);
        }

        if (army.ActiveRouteId is not null)
        {
            issues.Add(new("army_in_transit", $"Army '{order.ArmyId}' must stop before receiving a replacement route."));
        }

        if (!Enum.IsDefined(order.TransportMode) || !Enum.IsDefined(order.Stance) || !Enum.IsDefined(order.Fallback))
        {
            issues.Add(new("movement_contract", "Movement mode, stance, or fallback is invalid."));
        }

        if (order.PlannedRouteIds.Count == 0 || order.PlannedRouteIds.Distinct().Count() != order.PlannedRouteIds.Count)
        {
            issues.Add(new("movement_route", "Movement requires a non-empty route plan without duplicate edges."));
        }
        else if (!TryValidateRouteChain(
                     army.CurrentStopId,
                     order.PlannedRouteIds,
                     order.TransportMode,
                     out _,
                     out string? routeIssue))
        {
            issues.Add(new("movement_route", routeIssue!));
        }

        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CommandValidationResult ValidateRetreatOrder(RetreatOrderPayload order)
    {
        if (!armies.TryGetValue(order.ArmyId, out ArmyGeographicState? army))
        {
            return CommandValidationResult.Invalid(new ValidationIssue("unknown_army", $"Army '{order.ArmyId}' does not exist."));
        }

        if (!Graph.TryGetStop(order.PreferredStopId, out _) || !Enum.IsDefined(order.TransportMode))
        {
            return CommandValidationResult.Invalid(new ValidationIssue("retreat_target", "Retreat target or transport mode is invalid."));
        }

        PathResult? path = FindPath(army.CurrentStopId, order.PreferredStopId, order.TransportMode, army.FactionId);
        return path is null || path.RouteIds.Count == 0
            ? CommandValidationResult.Invalid(new ValidationIssue("retreat_unreachable", "No permitted retreat route is available."))
            : CommandValidationResult.Valid;
    }

    public CommandValidationResult ValidateSupplyOrder(SupplyOrderPayload order)
    {
        List<ValidationIssue> issues = [];
        if (order.RequestedAmount <= 0)
        {
            issues.Add(new("supply_amount", "Supply request must be positive."));
        }

        if (!locations.ContainsKey(order.SourceStopId) || !locations.ContainsKey(order.DestinationStopId))
        {
            issues.Add(new("supply_stop", "Supply source or destination is unknown."));
        }
        else if (!TryValidateRouteChain(
                     order.SourceStopId,
                     order.RouteIds,
                     order.TransportMode,
                     out EntityId destination,
                     out string? routeIssue)
                 || destination != order.DestinationStopId)
        {
            issues.Add(new("supply_route", routeIssue ?? "Supply route does not reach its destination."));
        }
        else if (order.RouteIds.Any(routeId => !IsRouteAvailable(routes[routeId], order.FactionId)
            || IsClosed(GetRoute(routeId))))
        {
            issues.Add(new("supply_permission", "Supply route is closed or not permitted for the faction."));
        }

        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CommandValidationResult ValidateControlChange(ChangeControlCommandPayload change)
    {
        if (!locations.ContainsKey(change.StopId))
        {
            return CommandValidationResult.Invalid(new ValidationIssue("unknown_stop", $"Stop '{change.StopId}' does not exist."));
        }

        if (change.LocalAcceptance is < 0 or > 1_000
            || change.Claims.Any(claim => !claim.ClaimantId.IsValid || string.IsNullOrWhiteSpace(claim.Basis))
            || change.Claims.Select(claim => claim.ClaimantId).Distinct().Count() != change.Claims.Count)
        {
            return CommandValidationResult.Invalid(new ValidationIssue("control_contract", "Acceptance or claims are invalid."));
        }

        return CommandValidationResult.Valid;
    }

    public MovementEventPayload PlanMovementOrder(MovementOrderPayload order)
    {
        ArmyGeographicState army = armies[order.ArmyId];
        _ = TryValidateRouteChain(
            army.CurrentStopId,
            order.PlannedRouteIds,
            order.TransportMode,
            out EntityId destination,
            out _);
        return new MovementEventPayload(
            army.ArmyId,
            MovementEventKind.Ordered,
            army.CurrentStopId,
            null,
            null,
            null,
            0,
            order.PlannedRouteIds.ToArray(),
            destination,
            order.TransportMode,
            order.Stance,
            order.Fallback,
            "order_accepted");
    }

    public MovementEventPayload PlanRetreat(RetreatOrderPayload order)
    {
        ArmyGeographicState army = armies[order.ArmyId];
        PathResult path = FindPath(army.CurrentStopId, order.PreferredStopId, order.TransportMode, army.FactionId)
            ?? throw new SimulationValidationException("Validated retreat route became unavailable.");
        Route firstRoute = GetRoute(path.RouteIds[0]);
        EntityId retreatStop = Graph.GetOtherStop(firstRoute, army.CurrentStopId);
        return new MovementEventPayload(
            army.ArmyId,
            MovementEventKind.Retreated,
            retreatStop,
            null,
            null,
            null,
            0,
            [],
            null,
            order.TransportMode,
            MovementStance.Cautious,
            MovementFallback.Stop,
            "retreat_completed");
    }

    public SupplyTransferredEventPayload PlanSupplyTransfer(SupplyOrderPayload order, CampaignDate date)
    {
        EnsureSupplyUsageDate(date);
        long capacity = order.RouteIds
            .Select(routeId => Math.Max(
                0,
                GetEffectiveCapacity(GetRoute(routeId), supply: true)
                    - supplyUsedByRoute.GetValueOrDefault(routeId)))
            .DefaultIfEmpty(0)
            .Min();
        long transferred = Math.Min(order.RequestedAmount, Math.Min(locations[order.SourceStopId].Stores, capacity));
        return new SupplyTransferredEventPayload(
            order.FactionId,
            order.SourceStopId,
            order.DestinationStopId,
            order.RouteIds.ToArray(),
            order.TransportMode,
            order.RequestedAmount,
            transferred,
            capacity);
    }

    public ControlChangedEventPayload PlanControlChange(ChangeControlCommandPayload change)
    {
        LocationState previous = locations[change.StopId];
        return new ControlChangedEventPayload(
            change.StopId,
            previous.ControllerId,
            change.ControllerId,
            change.LegalAppointeeId,
            change.LocalAcceptance,
            change.Claims.OrderBy(claim => claim.ClaimantId).ToArray(),
            change.Occupied);
    }

    internal IReadOnlyList<CampaignEvent> PlanDailyEvents(CampaignDate date, long turnIndex)
    {
        List<CampaignEvent> events = [];
        List<MovementProposal> proposals = armies.Values
            .Where(army => army.PlannedRouteIds.Count > 0 || army.ActiveRouteId is not null)
            .OrderBy(army => army.ArmyId)
            .Select(PlanMovement)
            .ToList();
        HashSet<EntityId> intercepted = [];

        foreach (IGrouping<EntityId?, MovementProposal> routeGroup in proposals
                     .Where(proposal => proposal.Route is not null && proposal.CanAdvance)
                     .GroupBy(proposal => proposal.Route?.Id)
                     .OrderBy(group => group.Key))
        {
            MovementProposal[] candidates = routeGroup.OrderBy(item => item.Army.ArmyId).ToArray();
            for (int firstIndex = 0; firstIndex < candidates.Length; firstIndex++)
            {
                MovementProposal first = candidates[firstIndex];
                if (intercepted.Contains(first.Army.ArmyId))
                {
                    continue;
                }

                MovementProposal? opponent = candidates.Skip(firstIndex + 1)
                    .FirstOrDefault(second => !intercepted.Contains(second.Army.ArmyId)
                        && second.Army.FactionId != first.Army.FactionId
                        && MovingTowardEachOther(first, second));
                if (opponent is null)
                {
                    continue;
                }

                intercepted.Add(first.Army.ArmyId);
                intercepted.Add(opponent.Army.ArmyId);
                Route route = first.Route!;
                int meeting = Math.Clamp((first.ProposedNormalized + opponent.ProposedNormalized) / 2, 0, route.TraversalCost);
                EntityId interceptor = InterceptionScore(first.Army) > InterceptionScore(opponent.Army)
                    ? first.Army.ArmyId
                    : InterceptionScore(first.Army) < InterceptionScore(opponent.Army)
                        ? opponent.Army.ArmyId
                        : new[] { first.Army.ArmyId, opponent.Army.ArmyId }.Min();
                InterceptionEventPayload payload = new(
                    new[] { first.Army.ArmyId, opponent.Army.ArmyId }.Min(),
                    new[] { first.Army.ArmyId, opponent.Army.ArmyId }.Max(),
                    route.Id,
                    meeting,
                    interceptor,
                    Graph.GetBattleLocation(route.Id, Weather));
                events.Add(CreateDailyEvent(
                    date,
                    50,
                    $"interception/{route.Id.Value.Replace(':', '/')}/{first.Army.ArmyId.Value.Replace(':', '-')}-{opponent.Army.ArmyId.Value.Replace(':', '-')}",
                    [first.Army.ArmyId, opponent.Army.ArmyId, route.Id],
                    payload));
            }
        }

        foreach (MovementProposal proposal in proposals.Where(item => !intercepted.Contains(item.Army.ArmyId)))
        {
            events.Add(CreateDailyEvent(
                date,
                100,
                $"movement/{proposal.Army.ArmyId.Value.Replace(':', '/')}",
                Affected(proposal.Payload.ArmyId, proposal.Payload.ActiveRouteId, proposal.Payload.CurrentStopId),
                proposal.Payload));
        }

        foreach (LocationState location in locations.Values.Where(item => item.DailyProduction > 0))
        {
            events.Add(CreateDailyEvent(
                date,
                200,
                $"production/{location.StopId.Value.Replace(':', '/')}",
                [location.StopId],
                new SupplyProducedEventPayload(location.StopId, location.DailyProduction)));
        }

        foreach (ArmyGeographicState army in armies.Values.Where(item => item.DailySupplyDemand > 0))
        {
            long consumed = Math.Min(army.Supply, army.DailySupplyDemand);
            events.Add(CreateDailyEvent(
                date,
                300,
                $"supply-demand/{army.ArmyId.Value.Replace(':', '/')}",
                [army.ArmyId],
                new ArmySupplyConsumedEventPayload(
                    army.ArmyId,
                    army.DailySupplyDemand,
                    consumed,
                    army.DailySupplyDemand - consumed)));
        }

        return events.OrderBy(item => item.Priority).ThenBy(item => item.EventId).ToArray();
    }

    internal void Apply(ICampaignEventPayload payload, CampaignDate date)
    {
        switch (payload)
        {
            case MovementEventPayload movement:
                ApplyMovement(movement);
                break;
            case InterceptionEventPayload interception:
                ApplyInterception(interception);
                break;
            case ControlChangedEventPayload control:
                ApplyControl(control);
                break;
            case SupplyTransferredEventPayload supply:
                ApplySupply(supply, date);
                break;
            case SupplyProducedEventPayload production:
                ApplyProduction(production);
                break;
            case ArmySupplyConsumedEventPayload consumption:
                ApplyArmySupplyConsumption(consumption);
                break;
            default:
                throw new SimulationValidationException($"Unsupported geographic event '{payload.GetType().Name}'.");
        }
    }

    public PathResult? FindPath(EntityId from, EntityId to, TransportMode mode, EntityId factionId) => Graph.FindPath(
        from,
        to,
        mode,
        route => IsRouteAvailable(routes[route.Id], factionId) && !IsClosed(route),
        GetEffectiveTraversalCost);

    public IReadOnlyList<ReinforcementCandidate> GetReinforcements(
        EntityId battleLocationId,
        EntityId factionId,
        TransportMode mode,
        int maximumRouteCost)
    {
        EntityId[] targetStops;
        if (Graph.TryGetStop(battleLocationId, out _))
        {
            targetStops = [battleLocationId];
        }
        else if (Graph.TryGetRoute(battleLocationId, out Route? route))
        {
            targetStops = [route.FromStopId, route.ToStopId];
        }
        else
        {
            return [];
        }

        return armies.Values
            .Where(army => army.FactionId == factionId && army.ActiveRouteId is null)
            .Select(army => targetStops
                .Select(stop => FindPath(army.CurrentStopId, stop, mode, factionId))
                .Where(path => path is not null)
                .Cast<PathResult>()
                .OrderBy(path => path.TotalCost)
                .ThenBy(path => string.Join('\n', path.RouteIds.Select(id => id.Value)), StringComparer.Ordinal)
                .Select(path => new ReinforcementCandidate(army.ArmyId, path.TotalCost, path.RouteIds))
                .FirstOrDefault())
            .Where(candidate => candidate is not null && candidate.RouteCost <= maximumRouteCost)
            .Cast<ReinforcementCandidate>()
            .OrderBy(candidate => candidate.RouteCost)
            .ThenBy(candidate => candidate.ArmyId)
            .ToArray();
    }

    public GeographicContext GetContext(EntityId stopId, EntityId observerId)
    {
        RouteStop stop = Graph.TryGetStop(stopId, out RouteStop? found)
            ? found
            : throw new SimulationValidationException($"Unknown geographic stop '{stopId}'.");
        Locality locality = Graph.TryGetLocality(stop.LocalityId, out Locality? foundLocality)
            ? foundLocality
            : throw new SimulationValidationException($"Stop '{stopId}' has no locality.");
        District district = Graph.TryGetDistrict(locality.DistrictId, out District? foundDistrict)
            ? foundDistrict
            : throw new SimulationValidationException($"Locality '{locality.Id}' has no district.");
        LocationState state = locations[stopId];
        KnownLocationState known = GetKnownLocationState(state, observerId);
        Route[] connected = Graph.GetRoutes(stopId).ToArray();
        return new GeographicContext(
            stopId,
            locality.Id,
            district.Id,
            district.RegionId,
            connected.Select(route => Graph.GetOtherStop(route, stopId)).Order().ToArray(),
            connected.Select(route => route.Id).Order().ToArray(),
            stop.Terrain,
            Season,
            Weather,
            known,
            locality.CultureId,
            locality.Population,
            Graph.GetBattleLocation(stopId, Weather));
    }

    public CampaignMapPresentationState GetCampaignMapPresentation(
        EntityId observerId,
        IReadOnlyDictionary<EntityId, DiplomaticRelationCategory>? diplomaticRelations = null)
    {
        KnownLocationPresentationState[] knownLocations = locations.Values
            .Select(location =>
            {
                KnownLocationState politicalState = GetKnownLocationState(location, observerId);
                IntelligenceLevel intelligence = politicalState.Intelligence;
                bool current = intelligence >= IntelligenceLevel.Current;
                EntityId? knownController = politicalState.ControllerId;
                RouteStop stop = Graph.TryGetStop(location.StopId, out RouteStop? foundStop)
                    ? foundStop
                    : throw new SimulationValidationException($"Unknown geographic stop '{location.StopId}'.");
                Locality locality = Graph.TryGetLocality(stop.LocalityId, out Locality? foundLocality)
                    ? foundLocality
                    : throw new SimulationValidationException($"Stop '{location.StopId}' has no locality.");
                ArmyGeographicState[] stationedArmies = current
                    ? armies.Values
                        .Where(army => army.ActiveRouteId is null && army.CurrentStopId == location.StopId)
                        .ToArray()
                    : [];
                long? dailyDemand = current
                    ? stationedArmies.Aggregate(0L, (total, army) => checked(total + army.DailySupplyDemand))
                    : null;
                long? dailyShortage = current
                    ? stationedArmies.Aggregate(
                        0L,
                        (total, army) => checked(total + Math.Max(0, army.DailySupplyDemand - army.Supply)))
                    : null;
                return new KnownLocationPresentationState(
                    location.StopId,
                    intelligence,
                    knownController,
                    ResolveDiplomaticRelation(
                        observerId,
                        intelligence,
                        knownController,
                        diplomaticRelations),
                    current ? location.Stores : null,
                    current ? location.DailyProduction : null,
                    dailyDemand,
                    dailyShortage,
                    politicalState,
                    intelligence >= IntelligenceLevel.Observed ? locality.Population : null,
                    intelligence >= IntelligenceLevel.Observed ? locality.CultureId : null);
            })
            .OrderBy(location => location.StopId)
            .ToArray();

        KnownRoutePresentationState[] knownRoutes = Graph.Definition.Routes
            .Select(route =>
            {
                IntelligenceLevel intelligence = (IntelligenceLevel)Math.Min(
                    (int)GetIntelligence(locations[route.FromStopId], observerId),
                    (int)GetIntelligence(locations[route.ToStopId], observerId));
                if (intelligence < IntelligenceLevel.Current)
                {
                    return new KnownRoutePresentationState(
                        route.Id,
                        intelligence,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);
                }

                RouteState state = routes[route.Id];
                bool available = IsRouteAvailable(state, observerId) && !IsClosed(route);
                return new KnownRoutePresentationState(
                    route.Id,
                    intelligence,
                    route.Capacity,
                    route.SupplyThroughput,
                    available ? GetEffectiveCapacity(route, supply: true) : 0,
                    state.ControllerId,
                    state.ControlState,
                    state.DisruptionPermille,
                    available);
            })
            .OrderBy(route => route.RouteId)
            .ToArray();

        return new CampaignMapPresentationState(knownLocations, knownRoutes);
    }

    private static IntelligenceLevel GetIntelligence(LocationState location, EntityId observerId)
    {
        if (location.ControllerId == observerId)
        {
            return IntelligenceLevel.Current;
        }

        return location.Intelligence.FirstOrDefault(item => item.ObserverId == observerId)?.Level
            ?? IntelligenceLevel.Unknown;
    }

    private static KnownLocationState GetKnownLocationState(LocationState location, EntityId observerId)
    {
        IntelligenceLevel intelligence = GetIntelligence(location, observerId);
        return new KnownLocationState(
            location.StopId,
            intelligence,
            intelligence >= IntelligenceLevel.Rumored ? location.ControllerId : null,
            intelligence >= IntelligenceLevel.Observed ? location.LegalAppointeeId : null,
            intelligence >= IntelligenceLevel.Current ? location.LocalAcceptance : null,
            intelligence >= IntelligenceLevel.Observed ? location.Claims.ToArray() : [],
            intelligence >= IntelligenceLevel.Observed ? location.Occupied : null,
            intelligence >= IntelligenceLevel.Current ? location.Stores : null);
    }

    private static DiplomaticRelationCategory ResolveDiplomaticRelation(
        EntityId observerId,
        IntelligenceLevel intelligence,
        EntityId? knownController,
        IReadOnlyDictionary<EntityId, DiplomaticRelationCategory>? diplomaticRelations)
    {
        if (intelligence < IntelligenceLevel.Rumored)
        {
            return DiplomaticRelationCategory.Unknown;
        }

        if (knownController is null)
        {
            return DiplomaticRelationCategory.Uncontrolled;
        }

        if (knownController == observerId)
        {
            return DiplomaticRelationCategory.Self;
        }

        if (diplomaticRelations is null
            || !diplomaticRelations.TryGetValue(knownController.Value, out DiplomaticRelationCategory relation)
            || !Enum.IsDefined(relation))
        {
            return DiplomaticRelationCategory.Unknown;
        }

        return relation is DiplomaticRelationCategory.Friendly
            or DiplomaticRelationCategory.Neutral
            or DiplomaticRelationCategory.Hostile
            ? relation
            : DiplomaticRelationCategory.Unknown;
    }

    private MovementProposal PlanMovement(ArmyGeographicState army)
    {
        EntityId routeId = army.ActiveRouteId ?? army.PlannedRouteIds[0];
        Route route = GetRoute(routeId);
        EntityId from = army.RouteFromStopId ?? army.CurrentStopId;
        EntityId to = army.RouteToStopId ?? Graph.GetOtherStop(route, from);
        RouteState state = routes[route.Id];
        bool available = route.PermittedModes.Contains(army.TransportMode)
            && IsRouteAvailable(state, army.FactionId)
            && !IsClosed(route)
            && army.Strength <= GetEffectiveCapacity(route, supply: false);
        if (!available)
        {
            if (army.Fallback == MovementFallback.Reroute && army.DestinationStopId is EntityId destination)
            {
                PathResult? alternate = FindPath(army.CurrentStopId, destination, army.TransportMode, army.FactionId);
                if (alternate is not null && alternate.RouteIds.Count > 0 && !alternate.RouteIds.SequenceEqual(army.PlannedRouteIds))
                {
                    MovementEventPayload rerouted = new(
                        army.ArmyId,
                        MovementEventKind.Rerouted,
                        army.CurrentStopId,
                        null,
                        null,
                        null,
                        0,
                        alternate.RouteIds,
                        destination,
                        army.TransportMode,
                        army.Stance,
                        army.Fallback,
                        "route_recomputed");
                    return MovementProposal.Blocked(army, route, rerouted);
                }
            }

            bool cancel = army.Fallback == MovementFallback.Stop;
            MovementEventPayload blocked = new(
                army.ArmyId,
                cancel ? MovementEventKind.Cancelled : MovementEventKind.Blocked,
                army.CurrentStopId,
                null,
                null,
                null,
                0,
                cancel ? [] : army.PlannedRouteIds,
                cancel ? null : army.DestinationStopId,
                army.TransportMode,
                army.Stance,
                army.Fallback,
                IsClosed(route) ? "seasonal_closure" : "route_unavailable");
            return MovementProposal.Blocked(army, route, blocked);
        }

        int traversalCost = GetEffectiveTraversalCost(route);
        int currentProgress = army.ActiveRouteId is null ? 0 : army.RouteProgress;
        int proposedProgress = Math.Min(traversalCost, currentProgress + DailyMovement(army.Stance));
        bool arrived = proposedProgress >= traversalCost;
        EntityId[] remaining = army.PlannedRouteIds
            .Where((_, index) => !(arrived && index == 0))
            .ToArray();
        MovementEventPayload movement = new(
            army.ArmyId,
            arrived ? MovementEventKind.Arrived : MovementEventKind.Advanced,
            arrived ? to : army.CurrentStopId,
            arrived ? null : route.Id,
            arrived ? null : from,
            arrived ? null : to,
            arrived ? 0 : proposedProgress,
            remaining,
            remaining.Length == 0 && arrived ? null : army.DestinationStopId,
            army.TransportMode,
            army.Stance,
            army.Fallback,
            arrived ? "stop_reached" : "route_progress");
        int currentNormalized = route.FromStopId == from ? currentProgress : traversalCost - currentProgress;
        int proposedNormalized = route.FromStopId == from ? proposedProgress : traversalCost - proposedProgress;
        return new MovementProposal(
            army,
            route,
            movement,
            true,
            currentNormalized,
            proposedNormalized);
    }

    private bool TryValidateRouteChain(
        EntityId start,
        IReadOnlyList<EntityId> routeIds,
        TransportMode mode,
        out EntityId destination,
        out string? issue)
    {
        destination = start;
        issue = null;
        foreach (EntityId routeId in routeIds)
        {
            if (!Graph.TryGetRoute(routeId, out Route? route))
            {
                issue = $"Route '{routeId}' does not exist.";
                return false;
            }

            if (!route.PermittedModes.Contains(mode))
            {
                issue = $"Route '{routeId}' does not permit {mode}.";
                return false;
            }

            if (route.FromStopId != destination && route.ToStopId != destination)
            {
                issue = $"Route '{routeId}' is not contiguous with stop '{destination}'.";
                return false;
            }

            destination = Graph.GetOtherStop(route, destination);
        }

        return true;
    }

    private bool IsRouteAvailable(RouteState state, EntityId factionId) => state.ControlState switch
    {
        RouteControlState.Open => true,
        RouteControlState.Contested => true,
        RouteControlState.Controlled => state.ControllerId == factionId || state.PermittedFactionIds.Contains(factionId),
        RouteControlState.Blockaded => state.ControllerId == factionId,
        _ => false,
    };

    private bool IsClosed(Route route) => ApplicableModifiers(route).Any(modifier => modifier.Closed);

    private int GetEffectiveTraversalCost(Route route)
    {
        long cost = route.TraversalCost;
        foreach (RouteModifier modifier in ApplicableModifiers(route))
        {
            cost = checked((cost * modifier.TraversalPermille + 999) / 1_000);
        }

        int disruption = routes[route.Id].DisruptionPermille;
        cost = checked((cost * 1_000 + (999 - disruption)) / Math.Max(1, 1_000 - disruption));
        return checked((int)Math.Max(1, cost));
    }

    private int GetEffectiveCapacity(Route route, bool supply)
    {
        long capacity = supply ? Math.Min(route.Capacity, route.SupplyThroughput) : route.Capacity;
        foreach (RouteModifier modifier in ApplicableModifiers(route))
        {
            capacity = checked(capacity * modifier.CapacityPermille / 1_000);
        }

        capacity = checked(capacity * (1_000 - routes[route.Id].DisruptionPermille) / 1_000);
        return checked((int)Math.Max(0, capacity));
    }

    private IEnumerable<RouteModifier> ApplicableModifiers(Route route) => route.Modifiers.Where(
        modifier => (modifier.Season is null || modifier.Season == Season)
            && (modifier.Weather is null || modifier.Weather == Weather));

    private Route GetRoute(EntityId id) => Graph.TryGetRoute(id, out Route? route)
        ? route
        : throw new SimulationValidationException($"Unknown route '{id}'.");

    private static int DailyMovement(MovementStance stance) => stance switch
    {
        MovementStance.Cautious => 700,
        MovementStance.Normal => 1_000,
        MovementStance.Forced => 1_400,
        MovementStance.Ambush => 500,
        _ => 0,
    };

    private static int InterceptionScore(ArmyGeographicState army) => checked(
        army.Scouting * 10 + (army.Stance switch
        {
            MovementStance.Ambush => 300,
            MovementStance.Cautious => 100,
            MovementStance.Normal => 50,
            MovementStance.Forced => 0,
            _ => 0,
        }));

    private static bool MovingTowardEachOther(MovementProposal first, MovementProposal second) =>
        Math.Sign(first.ProposedNormalized - first.CurrentNormalized)
            != Math.Sign(second.ProposedNormalized - second.CurrentNormalized)
        && Math.Min(first.CurrentNormalized, first.ProposedNormalized)
            <= Math.Max(second.CurrentNormalized, second.ProposedNormalized)
        && Math.Min(second.CurrentNormalized, second.ProposedNormalized)
            <= Math.Max(first.CurrentNormalized, first.ProposedNormalized);

    private static CampaignEvent CreateDailyEvent(
        CampaignDate date,
        int priority,
        string idPath,
        IReadOnlyList<EntityId> affected,
        ICampaignEventPayload payload) => new(
            ContractVersions.CampaignEvent,
            new EntityId($"event:geography/{date}/{idPath}"),
            null,
            date,
            ResolutionPhase.Systems,
            priority,
            affected.Distinct().Order().ToArray(),
            payload);

    private static EntityId[] Affected(params EntityId?[] ids) => ids
        .Where(id => id is not null)
        .Select(id => id!.Value)
        .Distinct()
        .Order()
        .ToArray();

    private void ApplyMovement(MovementEventPayload movement)
    {
        ArmyGeographicState army = armies.TryGetValue(movement.ArmyId, out ArmyGeographicState? found)
            ? found
            : throw new SimulationValidationException($"Movement army '{movement.ArmyId}' is unavailable.");
        ArmyGeographicState updated = army with
        {
            CurrentStopId = movement.CurrentStopId,
            ActiveRouteId = movement.ActiveRouteId,
            RouteFromStopId = movement.RouteFromStopId,
            RouteToStopId = movement.RouteToStopId,
            RouteProgress = movement.RouteProgress,
            PlannedRouteIds = movement.RemainingRouteIds.ToArray(),
            DestinationStopId = movement.DestinationStopId,
            TransportMode = movement.TransportMode,
            Stance = movement.Stance,
            Fallback = movement.Fallback,
        };
        ValidateArmy(updated);
        armies[updated.ArmyId] = updated;
    }

    private void ApplyInterception(InterceptionEventPayload interception)
    {
        Route route = GetRoute(interception.RouteId);
        foreach (EntityId armyId in new[] { interception.FirstArmyId, interception.SecondArmyId })
        {
            ArmyGeographicState army = armies[armyId];
            armies[armyId] = army with
            {
                ActiveRouteId = null,
                RouteFromStopId = null,
                RouteToStopId = null,
                RouteProgress = 0,
                PlannedRouteIds = [],
                DestinationStopId = null,
            };
        }
    }

    private void ApplyControl(ControlChangedEventPayload control)
    {
        LocationState state = locations[control.StopId];
        locations[control.StopId] = state with
        {
            ControllerId = control.ControllerId,
            LegalAppointeeId = control.LegalAppointeeId,
            LocalAcceptance = control.LocalAcceptance,
            Claims = control.Claims.OrderBy(claim => claim.ClaimantId).ToArray(),
            Occupied = control.Occupied,
        };
    }

    private void ApplySupply(SupplyTransferredEventPayload supply, CampaignDate date)
    {
        if (supply.TransferredAmount < 0 || supply.TransferredAmount > supply.BottleneckCapacity)
        {
            throw new SimulationValidationException("Supply transfer exceeds its route-chain bottleneck.");
        }

        LocationState source = locations[supply.SourceStopId];
        LocationState destination = locations[supply.DestinationStopId];
        if (source.Stores < supply.TransferredAmount)
        {
            throw new SimulationValidationException("Supply source no longer has the planned stores.");
        }

        locations[source.StopId] = source with { Stores = source.Stores - supply.TransferredAmount };
        locations[destination.StopId] = destination with
        {
            Stores = checked(destination.Stores + supply.TransferredAmount),
        };
        EnsureSupplyUsageDate(date);
        foreach (EntityId routeId in supply.RouteIds)
        {
            supplyUsedByRoute[routeId] = checked(
                supplyUsedByRoute.GetValueOrDefault(routeId) + supply.TransferredAmount);
        }
    }

    private void ApplyProduction(SupplyProducedEventPayload production)
    {
        LocationState location = locations[production.StopId];
        locations[location.StopId] = location with { Stores = checked(location.Stores + production.Amount) };
    }

    private void ApplyArmySupplyConsumption(ArmySupplyConsumedEventPayload consumption)
    {
        ArmyGeographicState army = armies[consumption.ArmyId];
        if (consumption.Consumed < 0
            || consumption.Shortage < 0
            || consumption.Consumed + consumption.Shortage != consumption.Demand
            || army.Supply < consumption.Consumed)
        {
            throw new SimulationValidationException("Army supply consumption violates demand or available stores.");
        }

        armies[army.ArmyId] = army with { Supply = army.Supply - consumption.Consumed };
    }

    private void EnsureSupplyUsageDate(CampaignDate date)
    {
        if (supplyUsageDate == date)
        {
            return;
        }

        supplyUsageDate = date;
        supplyUsedByRoute.Clear();
    }

    private void ValidateLocation(LocationState location)
    {
        if (!Graph.TryGetStop(location.StopId, out _)
            || location.LocalAcceptance is < 0 or > 1_000
            || location.Stores < 0
            || location.DailyProduction < 0
            || location.Claims.Any(claim => !claim.ClaimantId.IsValid || !Enum.IsDefined(claim.Strength) || string.IsNullOrWhiteSpace(claim.Basis))
            || location.Claims.Select(claim => claim.ClaimantId).Distinct().Count() != location.Claims.Count
            || location.Intelligence.Any(item => !item.ObserverId.IsValid || !Enum.IsDefined(item.Level) || item.LastObservedTurn < 0)
            || location.Intelligence.Select(item => item.ObserverId).Distinct().Count() != location.Intelligence.Count)
        {
            throw new SimulationValidationException($"Location state '{location.StopId}' is invalid.");
        }
    }

    private void ValidateRouteState(RouteState route)
    {
        if (!Graph.TryGetRoute(route.RouteId, out _)
            || !Enum.IsDefined(route.ControlState)
            || route.DisruptionPermille is < 0 or > 1_000
            || route.PermittedFactionIds.Any(id => !id.IsValid)
            || route.PermittedFactionIds.Distinct().Count() != route.PermittedFactionIds.Count)
        {
            throw new SimulationValidationException($"Route state '{route.RouteId}' is invalid.");
        }
    }

    private void ValidateArmy(ArmyGeographicState army)
    {
        if (!army.ArmyId.IsValid
            || !army.FactionId.IsValid
            || !Graph.TryGetStop(army.CurrentStopId, out _)
            || army.RouteProgress < 0
            || army.Scouting is < 0 or > 100
            || army.Strength <= 0
            || army.Supply < 0
            || army.DailySupplyDemand < 0
            || !Enum.IsDefined(army.TransportMode)
            || !Enum.IsDefined(army.Stance)
            || !Enum.IsDefined(army.Fallback)
            || army.PlannedRouteIds.Any(id => !Graph.TryGetRoute(id, out _)))
        {
            throw new SimulationValidationException($"Army geography state '{army.ArmyId}' is invalid.");
        }

        if (army.ActiveRouteId is EntityId routeId
            && (!Graph.TryGetRoute(routeId, out Route? route)
                || army.RouteFromStopId is null
                || army.RouteToStopId is null
                || !new[] { army.RouteFromStopId.Value, army.RouteToStopId.Value }.ToHashSet()
                    .SetEquals([route.FromStopId, route.ToStopId])))
        {
            throw new SimulationValidationException($"Army '{army.ArmyId}' has invalid active-route state.");
        }
    }

    private static LocationState Clone(LocationState item) => item with
    {
        Claims = item.Claims.ToArray(),
        Intelligence = item.Intelligence.ToArray(),
    };

    private static RouteState Clone(RouteState item) => item with
    {
        PermittedFactionIds = item.PermittedFactionIds.ToArray(),
    };

    private static ArmyGeographicState Clone(ArmyGeographicState item) => item with
    {
        PlannedRouteIds = item.PlannedRouteIds.ToArray(),
    };

    private sealed record MovementProposal(
        ArmyGeographicState Army,
        Route? Route,
        MovementEventPayload Payload,
        bool CanAdvance,
        int CurrentNormalized,
        int ProposedNormalized)
    {
        public static MovementProposal Blocked(
            ArmyGeographicState army,
            Route route,
            MovementEventPayload payload) => new(army, route, payload, false, 0, 0);
    }
}
