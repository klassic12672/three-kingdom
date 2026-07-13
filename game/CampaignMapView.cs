using Game.Content;
using Godot;
using Simulation.Core;

namespace Game.Presentation;

/// <summary>Presentation-only illustrated route map; authoritative geography remains in Simulation.Core.</summary>
public partial class CampaignMapView : Control
{
    private const float WorldWidth = 1_400f;
    private const float WorldHeight = 1_080f;
    private const float MinimumZoom = 0.85f;
    private const float MaximumZoom = 6f;
    private const float DistrictDetailZoom = 1.35f;
    private const float DistrictLabelZoom = 2.15f;
    private const float LocalityPointZoom = 2.7f;
    private const float LocalityLabelZoom = 4.4f;
    private static readonly Color UnknownColor = new("#697078");
    private static readonly Color KnownEmptyColor = new("#8a8580");
    private static readonly Color InkColor = new("#1d2529");
    private static readonly Color LabelColor = new("#f4e8c9");
    private static readonly Vector2[] LabelOffsets =
    [
        new(14, -8),
        new(14, 20),
        new(-14, -8),
        new(-14, 20),
        new(0, -28),
        new(0, 34),
    ];
    private static readonly Vector2[] Landmass =
    [
        new(90, 220), new(160, 150), new(305, 150), new(520, 115), new(845, 100),
        new(1_165, 185), new(1_240, 255), new(1_165, 325), new(1_130, 530), new(1_095, 670),
        new(845, 845), new(700, 880), new(590, 845), new(450, 740), new(305, 635),
        new(270, 495), new(160, 360),
    ];
    private static readonly Vector2[] YellowRiver =
    [
        new(235, 395), new(450, 340), new(590, 340), new(700, 410), new(770, 375),
        new(770, 305), new(860, 305), new(950, 325), new(1_040, 305), new(1_080, 295),
    ];
    private static readonly Vector2[] YangtzeRiver =
    [
        new(235, 460), new(340, 565), new(520, 550), new(665, 540), new(810, 550),
        new(915, 540), new(1_025, 540), new(1_150, 520),
    ];
    private static readonly Vector2[][] MountainChains =
    [
        [new(375, 425), new(410, 520), new(450, 635), new(485, 735)],
        [new(520, 455), new(625, 455), new(735, 465), new(805, 480)],
        [new(790, 220), new(805, 305), new(790, 390)],
        [new(950, 135), new(1_025, 175), new(1_080, 220)],
        [new(700, 690), new(805, 685), new(915, 675)],
    ];

    private readonly Dictionary<(EntityId Key, string Locale), string> text = [];
    private readonly EntityId observerId = new("faction:coalition");
    private GeographicWorldState? world;
    private IReadOnlyDictionary<EntityId, KnownLocationPresentationState> presentedLocations =
        new Dictionary<EntityId, KnownLocationPresentationState>();
    private IReadOnlyDictionary<EntityId, KnownRoutePresentationState> presentedRoutes =
        new Dictionary<EntityId, KnownRoutePresentationState>();
    private readonly List<AreaLabelHit> areaLabelHits = [];
    private readonly List<AreaPointHit> areaPointHits = [];
    private readonly Dictionary<EntityId, Vector2[]> regionHulls = [];
    private readonly Dictionary<EntityId, Vector2[]> districtHulls = [];
    private EntityId? selectedStopId;
    private EntityId? selectedRouteId;
    private EntityId? selectedAreaId;
    private GeographicAreaKind? selectedAreaKind;
    private EntityId? hoveredStopId;
    private EntityId? hoveredRouteId;
    private EntityId? hoveredAreaId;
    private GeographicAreaKind? hoveredAreaKind;
    private string locale = "en-US";
    private long modeChangeStarted;
    private float zoom = 1f;
    private Vector2 panWorld;
    private bool panning;
    private Vector2 lastPanPosition;

    public CampaignMapMode MapMode { get; private set; } = CampaignMapMode.PoliticalControl;

    public EntityId? SelectedStopId => selectedStopId;

    public EntityId? SelectedRouteId => selectedRouteId;

    public EntityId? SelectedAreaId => selectedAreaId;

    public void Initialize(GeographicRuntimeArtifact artifact, string selectedLocale)
    {
        if (artifact.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported geography artifact schema {artifact.SchemaVersion}.");
        }

        world = new GeographicWorldState(artifact.Geography);
        IReadOnlyDictionary<EntityId, DiplomaticRelationCategory> diplomaticRelations = artifact.DiplomaticRelations
            .Where(item => item.ObserverId == observerId)
            .OrderBy(item => item.CounterpartyId)
            .ToDictionary(item => item.CounterpartyId, item => item.Relation);
        CampaignMapPresentationState presentation = world.GetCampaignMapPresentation(observerId, diplomaticRelations);
        presentedLocations = presentation.Locations.ToDictionary(item => item.StopId);
        presentedRoutes = presentation.Routes.ToDictionary(item => item.RouteId);
        locale = selectedLocale;
        text.Clear();
        foreach (LocalizationEntry entry in artifact.Localization)
        {
            text[(entry.Key, entry.Locale)] = entry.Text;
        }

        BuildAdministrativeGeometry();

        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Arrow;
        QueueRedraw();
    }

    public void SetMapMode(CampaignMapMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        modeChangeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        MapMode = mode;
        QueueRedraw();
    }

    public void SetLocale(string selectedLocale)
    {
        locale = selectedLocale == "ko-KR" ? "ko-KR" : "en-US";
        QueueRedraw();
    }

    public string GetModeDisplayName(CampaignMapMode mode) => ModeName(mode);

    public string GetUiText(string suffix) => Ui(suffix);

    public void SetCamera(float requestedZoom, Vector2? requestedCenter = null)
    {
        zoom = Math.Clamp(requestedZoom, MinimumZoom, MaximumZoom);
        panWorld = requestedCenter is Vector2 center
            ? center - new Vector2(WorldWidth / 2f, WorldHeight / 2f)
            : Vector2.Zero;
        QueueRedraw();
    }

    public bool SelectStop(EntityId stopId)
    {
        if (world is null || !world.Graph.TryGetStop(stopId, out _))
        {
            return false;
        }

        selectedStopId = stopId;
        selectedRouteId = null;
        selectedAreaId = null;
        selectedAreaKind = null;
        QueueRedraw();
        return true;
    }

    public bool SelectRoute(EntityId routeId)
    {
        if (world is null || !world.Graph.TryGetRoute(routeId, out _))
        {
            return false;
        }

        selectedRouteId = routeId;
        selectedStopId = null;
        selectedAreaId = null;
        selectedAreaKind = null;
        QueueRedraw();
        return true;
    }

    public bool SelectArea(EntityId areaId, GeographicAreaKind kind)
    {
        bool exists = world is not null && kind switch
        {
            GeographicAreaKind.Region => world.Graph.TryGetRegion(areaId, out _),
            GeographicAreaKind.District => world.Graph.TryGetDistrict(areaId, out _),
            GeographicAreaKind.Locality => world.Graph.TryGetLocality(areaId, out _),
            _ => false,
        };
        if (!exists)
        {
            return false;
        }

        selectedAreaId = areaId;
        selectedAreaKind = kind;
        selectedStopId = null;
        selectedRouteId = null;
        QueueRedraw();
        return true;
    }

    public void ClearSelection()
    {
        selectedStopId = null;
        selectedRouteId = null;
        selectedAreaId = null;
        selectedAreaKind = null;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (world is null)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                float factor = mouseButton.ButtonIndex == MouseButton.WheelUp ? 1.2f : 1f / 1.2f;
                zoom = Math.Clamp(zoom * factor, MinimumZoom, MaximumZoom);
                QueueRedraw();
                AcceptEvent();
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                panning = mouseButton.Pressed;
                lastPanPosition = mouseButton.Position;
                MouseDefaultCursorShape = panning ? CursorShape.Drag : CursorShape.Arrow;
                AcceptEvent();
                return;
            }
        }

        if (inputEvent is InputEventMouseMotion motion)
        {
            if (panning)
            {
                float scale = WorldScale();
                Vector2 delta = motion.Position - lastPanPosition;
                panWorld -= delta / Math.Max(0.001f, scale * zoom);
                lastPanPosition = motion.Position;
                QueueRedraw();
                AcceptEvent();
                return;
            }

            EntityId? previousStop = hoveredStopId;
            EntityId? previousRoute = hoveredRouteId;
            EntityId? previousArea = hoveredAreaId;
            GeographicAreaKind? previousAreaKind = hoveredAreaKind;
            hoveredStopId = PickStop(motion.Position);
            AreaLabelHit? hoveredArea = hoveredStopId is null ? PickArea(motion.Position) : null;
            hoveredAreaId = hoveredArea?.Id;
            hoveredAreaKind = hoveredArea?.Kind;
            hoveredRouteId = hoveredStopId is null && hoveredArea is null ? PickRoute(motion.Position) : null;
            MouseDefaultCursorShape = hoveredStopId is not null || hoveredRouteId is not null || hoveredAreaId is not null
                ? CursorShape.PointingHand
                : CursorShape.Arrow;
            if (previousStop != hoveredStopId || previousRoute != hoveredRouteId
                || previousArea != hoveredAreaId || previousAreaKind != hoveredAreaKind)
            {
                QueueRedraw();
            }

            return;
        }

        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
        {
            return;
        }

        EntityId? pickedStop = PickStop(click.Position);
        AreaLabelHit? pickedArea = pickedStop is null ? PickArea(click.Position) : null;
        EntityId? pickedRoute = pickedStop is null && pickedArea is null ? PickRoute(click.Position) : null;
        selectedStopId = pickedStop;
        selectedRouteId = pickedRoute;
        selectedAreaId = pickedArea?.Id;
        selectedAreaKind = pickedArea?.Kind;
        GD.Print($"MAP_SELECTION stop={pickedStop?.Value ?? "-"} route={pickedRoute?.Value ?? "-"} area={pickedArea?.Id.Value ?? "-"} tier={pickedArea?.Kind.ToString() ?? "-"}");
        QueueRedraw();
        AcceptEvent();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#0c151c"));
        if (world is null || Size.X < 10 || Size.Y < 10)
        {
            return;
        }

        DrawTerrain();
        DrawAdministrativeContainment();
        DrawModeOverlay();

        foreach (Route route in world.Graph.Definition.Routes.OrderBy(route => route.Id))
        {
            DrawRoute(route);
        }

        areaLabelHits.Clear();
        List<Rect2> occupiedLabels = [];
        DrawAreaLabels(occupiedLabels);
        foreach (RouteStop stop in world.Graph.Definition.Stops
                     .OrderByDescending(stop => stop.Position.Elevation)
                     .ThenBy(stop => stop.Id))
        {
            DrawStop(stop, occupiedLabels);
        }

        if (MapMode == CampaignMapMode.Routes)
        {
            DrawRouteLabels(occupiedLabels);
        }

        DrawLegend();
        DrawInspector();
        if (modeChangeStarted != 0)
        {
            double elapsedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(modeChangeStarted).TotalMilliseconds;
            GD.Print($"MAP_MODE_TIMING mode={MapMode} elapsed_ms={elapsedMilliseconds:F3}");
            modeChangeStarted = 0;
        }
    }

    private void DrawTerrain()
    {
        Rect2 mapRect = MapRect();
        DrawStyleBox(CreateMapPanel(), mapRect);
        Vector2[] land = Landmass.Select(ToScreen).ToArray();
        DrawColoredPolygon(land, new Color("#6f7858"));
        DrawPolyline(ClosePolygon(land), new Color("#c8b984"), 2.2f, true);

        foreach (Region region in world!.Graph.Definition.Regions.OrderBy(item => item.Id))
        {
            if (!regionHulls.TryGetValue(region.Id, out Vector2[]? hull) || hull.Length < 3)
            {
                continue;
            }

            Vector2[] polygon = hull.Select(ToScreen).ToArray();
            Color color = IdColor(region.Id) with
            {
                A = MapMode == CampaignMapMode.Administration ? 0.3f : 0.16f,
            };
            DrawColoredPolygon(polygon, color);
        }

        DrawRiver(YellowRiver, 15f);
        DrawRiver(YangtzeRiver, 18f);
        foreach (Vector2[] chain in MountainChains)
        {
            Vector2[] points = chain.Select(ToScreen).ToArray();
            DrawPolyline(points, new Color(0.2f, 0.22f, 0.18f, 0.48f), 9f, true);
            DrawPolyline(points, new Color(0.55f, 0.51f, 0.39f, 0.7f), 2.5f, true);
        }
    }

    private void DrawAdministrativeContainment()
    {
        foreach (Region region in world!.Graph.Definition.Regions.OrderBy(item => item.Id))
        {
            if (!regionHulls.TryGetValue(region.Id, out Vector2[]? hull) || hull.Length < 3)
            {
                continue;
            }

            bool selected = selectedAreaKind == GeographicAreaKind.Region && selectedAreaId == region.Id;
            Vector2[] outline = ClosePolygon(hull.Select(ToScreen).ToArray());
            DrawPolyline(outline,
                selected ? new Color("#fff0ab") : new Color(0.83f, 0.78f, 0.58f, 0.42f),
                selected ? 4f : 1.5f,
                true);
        }

        if (zoom >= DistrictDetailZoom)
        {
            foreach (District district in world!.Graph.Definition.Districts.OrderBy(item => item.Id))
            {
                if (!districtHulls.TryGetValue(district.Id, out Vector2[]? hull) || hull.Length < 3)
                {
                    continue;
                }

                bool selected = selectedAreaKind == GeographicAreaKind.District && selectedAreaId == district.Id;
                Vector2[] polygon = hull.Select(ToScreen).ToArray();
                if (!MapRect().Grow(40f).Intersects(Bounds(polygon)))
                {
                    continue;
                }

                if (selected)
                {
                    DrawColoredPolygon(polygon, new Color(1f, 0.9f, 0.45f, 0.12f));
                }
                DrawPolyline(ClosePolygon(polygon),
                    selected ? new Color("#fff0ab") : new Color(0.88f, 0.82f, 0.65f, 0.24f),
                    selected ? 3.5f : 1f,
                    true);
            }
        }

        if (selectedAreaKind == GeographicAreaKind.Locality
            && selectedAreaId is EntityId localityId
            && world!.Graph.TryGetLocality(localityId, out Locality? locality))
        {
            DrawDiamond(ToScreen(locality.Anchor), 9f, new Color(1f, 0.94f, 0.67f, 0.18f), new Color("#fff0ab"), 3f);
        }
    }

    private void DrawModeOverlay()
    {
        foreach (RouteStop stop in world!.Graph.Definition.Stops.OrderBy(item => item.Id))
        {
            KnownLocationPresentationState presentation = presentedLocations[stop.Id];
            Color color = ModeColor(stop, presentation) with { A = 0.2f };
            DrawDiamond(ToScreen(stop.Position), 28f, color, color with { A = 0.5f }, 1f);

            if (MapMode == CampaignMapMode.Administration
                && presentation.PoliticalState.LocalAcceptance is int acceptance)
            {
                Color acceptanceColor = Heat(acceptance, 1000);
                DrawArc(ToScreen(stop.Position), 25f, -Mathf.Pi / 2f,
                    -Mathf.Pi / 2f + Mathf.Tau * acceptance / 1000f, 32, acceptanceColor, 4f, true);
            }

            if (presentation.PoliticalState.Occupied == true
                && MapMode is CampaignMapMode.PoliticalControl or CampaignMapMode.Administration)
            {
                Vector2 point = ToScreen(stop.Position);
                DrawLine(point + new Vector2(-18, -18), point + new Vector2(18, 18), new Color("#d76b51"), 3f, true);
                DrawLine(point + new Vector2(-18, 18), point + new Vector2(18, -18), new Color("#d76b51"), 3f, true);
            }
        }
    }

    private void DrawRoute(Route route)
    {
        RouteStop from = GetStop(route.FromStopId);
        RouteStop to = GetStop(route.ToStopId);
        Vector2 start = ToScreen(from.Position);
        Vector2 end = ToScreen(to.Position);
        bool selected = selectedRouteId == route.Id;
        bool hovered = hoveredRouteId == route.Id;
        bool connectedToSelection = selectedStopId == route.FromStopId || selectedStopId == route.ToStopId;
        KnownRoutePresentationState state = presentedRoutes[route.Id];

        Color color = MapMode switch
        {
            CampaignMapMode.Routes => RouteTypeColor(route.RouteType),
            CampaignMapMode.Supply when state.EffectiveSupplyThroughput is not null => SupplyRouteColor(state),
            CampaignMapMode.Supply => UnknownColor,
            _ when state.ControlState is RouteControlState control => RouteControlColor(control),
            _ => new Color("#70787a"),
        };
        float baseWidth = MapMode == CampaignMapMode.Supply
            ? SupplyWidth(state.EffectiveSupplyThroughput)
            : route.RouteType is RouteType.River or RouteType.CoastalLane or RouteType.OpenSeaLane ? 5f : 3f;
        float width = selected ? baseWidth + 5f : hovered || connectedToSelection ? baseWidth + 2.5f : baseWidth;
        float capacityWidth = MapMode == CampaignMapMode.Supply && state.Capacity is int knownCapacity
            ? SupplyWidth(knownCapacity) + 3f
            : width;

        DrawLine(start + new Vector2(0, 4), end + new Vector2(0, 4), new Color(0, 0, 0, 0.58f), Math.Max(width, capacityWidth) + 4f, true);
        if (selected || hovered || connectedToSelection)
        {
            DrawLine(start, end, new Color("#f5df91"), Math.Max(width, capacityWidth) + 4f, true);
        }

        if (MapMode == CampaignMapMode.Supply && state.Capacity is int capacity)
        {
            DrawLine(start, end, new Color(0.12f, 0.15f, 0.13f, 0.76f), capacityWidth, true);
            if (state.SupplyThroughput is int throughput)
            {
                DrawLine(start, end, new Color(0.76f, 0.65f, 0.35f, 0.55f), SupplyWidth(throughput), true);
            }
        }

        DrawLine(start, end, color, width, true);
        DrawRoutePattern(route, start, end, color);

        if (MapMode == CampaignMapMode.Supply && state.DisruptionPermille is int disruption && disruption > 0)
        {
            float ratio = Math.Clamp(disruption / 1000f, 0f, 1f);
            Vector2 disruptedEnd = start.Lerp(end, ratio);
            DrawDashedLine(start, disruptedEnd, new Color("#db6b4e"), width + 1f, 9f, true, true);
        }
    }

    private void DrawRoutePattern(Route route, Vector2 start, Vector2 end, Color color)
    {
        Vector2 direction = end - start;
        float length = direction.Length();
        if (length < 1f)
        {
            return;
        }

        Vector2 normal = new(-direction.Y / length, direction.X / length);
        switch (route.RouteType)
        {
            case RouteType.MountainPath:
            case RouteType.FrontierTrail:
            case RouteType.SeasonalPassage:
                DrawDashedLine(start, end, color.Lightened(0.28f), 1.5f, 8f, true, true);
                break;
            case RouteType.River:
            case RouteType.CoastalLane:
            case RouteType.OpenSeaLane:
                DrawLine(start + normal * 3f, end + normal * 3f, color.Lightened(0.22f), 1.4f, true);
                break;
        }
    }

    private void DrawStop(RouteStop stop, ICollection<Rect2> occupiedLabels)
    {
        Vector2 position = ToScreen(stop.Position);
        KnownLocationPresentationState presentation = presentedLocations[stop.Id];
        Color fill = ModeColor(stop, presentation);
        bool selected = selectedStopId == stop.Id;
        bool hovered = hoveredStopId == stop.Id;
        float radius = selected ? 12f : stop.StopType is RouteStopType.Settlement or RouteStopType.Fort ? 9f : 7f;

        DrawCircle(position + new Vector2(0, 5), radius + 3f, new Color(0, 0, 0, 0.62f));
        DrawStopSymbol(stop.StopType, position, radius, fill);
        DrawCircle(position, radius + (selected || hovered ? 4f : 2f),
            selected ? Colors.White : hovered ? new Color("#f5df91") : new Color("#d8c89a"), false,
            selected ? 3f : hovered ? 2.5f : 1.4f, true);

        PlaceLabel(Localize(stop.NameKey), position, 14, LabelTier.Stop, occupiedLabels, alignLeft: true);
    }

    private void DrawStopSymbol(RouteStopType type, Vector2 position, float radius, Color fill)
    {
        switch (type)
        {
            case RouteStopType.Settlement:
                DrawRect(new Rect2(position - new Vector2(radius, radius), Vector2.One * radius * 2f), fill, true);
                break;
            case RouteStopType.Port:
            case RouteStopType.Ferry:
                DrawPolygon(
                    [position + new Vector2(0, -radius), position + new Vector2(radius, 0),
                        position + new Vector2(0, radius), position + new Vector2(-radius, 0)],
                    [fill]);
                DrawLine(position + new Vector2(-radius, 2), position + new Vector2(radius, 2), Colors.White with { A = 0.7f }, 1.5f);
                break;
            case RouteStopType.Pass:
                DrawPolygon(
                    [position + new Vector2(0, -radius), position + new Vector2(radius, radius),
                        position + new Vector2(-radius, radius)],
                    [fill]);
                break;
            case RouteStopType.Gate:
            case RouteStopType.Fort:
                DrawRect(new Rect2(position - new Vector2(radius, radius * 0.75f), new Vector2(radius * 2f, radius * 1.5f)), fill, true);
                DrawLine(position + new Vector2(-radius, -radius), position + new Vector2(-radius, radius), InkColor, 2f);
                DrawLine(position + new Vector2(radius, -radius), position + new Vector2(radius, radius), InkColor, 2f);
                break;
            case RouteStopType.Depot:
                DrawCircle(position, radius, fill);
                DrawLine(position + new Vector2(-radius * 0.6f, 0), position + new Vector2(radius * 0.6f, 0), InkColor, 2f);
                DrawLine(position + new Vector2(0, -radius * 0.6f), position + new Vector2(0, radius * 0.6f), InkColor, 2f);
                break;
            default:
                DrawCircle(position, radius, fill);
                break;
        }
    }

    private void DrawAreaLabels(ICollection<Rect2> occupiedLabels)
    {
        areaPointHits.Clear();
        foreach (Region region in world!.Graph.Definition.Regions.OrderBy(item => item.Id))
        {
            bool selected = selectedAreaKind == GeographicAreaKind.Region && selectedAreaId == region.Id;
            bool hovered = hoveredAreaKind == GeographicAreaKind.Region && hoveredAreaId == region.Id;
            if (zoom < DistrictLabelZoom || selected || hovered)
            {
                string label = AreaDisplayName(region.NameKey, region.LabelKey);
                PlaceLabel(label, ToScreen(region.Anchor), 19, LabelTier.Region, occupiedLabels, false,
                    region.Id, GeographicAreaKind.Region);
            }
        }

        if (zoom < DistrictDetailZoom)
        {
            return;
        }

        foreach (District district in world.Graph.Definition.Districts.OrderBy(item => item.Id))
        {
            Vector2 position = ToScreen(district.Anchor);
            if (!MapRect().Grow(50f).HasPoint(position))
            {
                continue;
            }

            DrawDiamond(position, 4f, new Color("#d8c58b"), new Color(0.1f, 0.12f, 0.1f, 0.75f), 1f);
            areaPointHits.Add(new(district.Id, GeographicAreaKind.District, position, 9f));
            bool selected = selectedAreaKind == GeographicAreaKind.District && selectedAreaId == district.Id;
            bool hovered = hoveredAreaKind == GeographicAreaKind.District && hoveredAreaId == district.Id;
            if (zoom >= DistrictLabelZoom && zoom < LocalityLabelZoom || selected || hovered)
            {
                string label = AreaDisplayName(district.NameKey, district.LabelKey);
                PlaceLabel(label, position + new Vector2(0, -8), 14, LabelTier.District, occupiedLabels, false,
                    district.Id, GeographicAreaKind.District);
            }
        }

        if (zoom < LocalityPointZoom)
        {
            return;
        }

        foreach (Locality locality in world.Graph.Definition.Localities.OrderBy(item => item.Id))
        {
            Vector2 position = ToScreen(locality.Anchor);
            if (!MapRect().Grow(30f).HasPoint(position))
            {
                continue;
            }

            bool selected = selectedAreaKind == GeographicAreaKind.Locality && selectedAreaId == locality.Id;
            bool hovered = hoveredAreaKind == GeographicAreaKind.Locality && hoveredAreaId == locality.Id;
            float radius = selected || hovered ? 5f : 2.6f;
            DrawDiamond(position, radius, TerrainColor(locality.Terrain),
                selected ? Colors.White : hovered ? new Color("#f5df91") : new Color(0.08f, 0.1f, 0.09f, 0.8f),
                selected ? 2.5f : 1f);
            areaPointHits.Add(new(locality.Id, GeographicAreaKind.Locality, position, Math.Max(7f, radius + 3f)));
            if (zoom >= LocalityLabelZoom || selected || hovered)
            {
                string label = AreaDisplayName(locality.NameKey, locality.LabelKey);
                PlaceLabel(label, position + new Vector2(0, 9), 11, LabelTier.Locality, occupiedLabels, false,
                    locality.Id, GeographicAreaKind.Locality);
            }
        }
    }

    private void DrawRouteLabels(ICollection<Rect2> occupiedLabels)
    {
        foreach (Route route in world!.Graph.Definition.Routes.OrderBy(item => item.Id))
        {
            Vector2 midpoint = (ToScreen(GetStop(route.FromStopId).Position) + ToScreen(GetStop(route.ToStopId).Position)) / 2f;
            PlaceLabel(Localize(route.NameKey), midpoint, 12, LabelTier.Route, occupiedLabels, false);
        }
    }

    private void PlaceLabel(
        string label,
        Vector2 anchor,
        int fontSize,
        LabelTier tier,
        ICollection<Rect2> occupiedLabels,
        bool alignLeft,
        EntityId? areaId = null,
        GeographicAreaKind? areaKind = null)
    {
        Font font = ThemeDB.FallbackFont;
        float textWidth = Math.Max(44f, font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize).X);
        float height = fontSize + 8f;
        foreach (Vector2 candidateOffset in LabelOffsets)
        {
            Vector2 offset = candidateOffset;
            if (!alignLeft && offset.X > 0)
            {
                offset.X = -textWidth / 2f;
            }

            Vector2 baseline = anchor + offset;
            Rect2 rect = new(baseline + new Vector2(-4, -fontSize - 2), new Vector2(textWidth + 8, height));
            if (!MapRect().Grow(-5).Encloses(rect) || occupiedLabels.Any(other => other.Intersects(rect)))
            {
                continue;
            }

            occupiedLabels.Add(rect);
            if (areaId is EntityId id && areaKind is GeographicAreaKind kind)
            {
                areaLabelHits.Add(new AreaLabelHit(id, kind, rect));
            }
            bool hovered = areaId == hoveredAreaId && areaKind == hoveredAreaKind;
            bool selected = areaId == selectedAreaId && areaKind == selectedAreaKind;
            Color panel = tier switch
            {
                LabelTier.Region => new Color(0.11f, 0.13f, 0.12f, 0.78f),
                LabelTier.District => new Color(0.13f, 0.14f, 0.13f, 0.72f),
                LabelTier.Locality => new Color(0.08f, 0.1f, 0.1f, 0.62f),
                _ => new Color(0.035f, 0.05f, 0.06f, 0.86f),
            };
            DrawRect(rect, panel, true);
            if (hovered || selected)
            {
                DrawRect(rect, selected ? Colors.White : new Color("#f5df91"), false, selected ? 2.5f : 1.5f);
            }
            DrawString(font, baseline, label, HorizontalAlignment.Left, -1, fontSize,
                tier == LabelTier.Region ? new Color("#e9d48c") : LabelColor);
            return;
        }
    }

    private void DrawLegend()
    {
        Rect2 panel = new(24, Size.Y - 78, Math.Min(720f, Size.X - 48), 56);
        DrawRect(panel, new Color(0.02f, 0.03f, 0.035f, 0.92f), true);
        DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14, 22),
            ModeName(MapMode),
            HorizontalAlignment.Left, -1, 16, new Color("#e8cf89"));
        DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14, 43), LegendHint(),
            HorizontalAlignment.Left, panel.Size.X - 28, 12, new Color("#b8c0b5"));
        DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(panel.Size.X - 275, 22),
            $"{Ui("navigation_hint")} · {zoom:0.0}x",
            HorizontalAlignment.Left, 260, 11, new Color("#9eb0a4"));
    }

    private void DrawInspector()
    {
        EntityId? stopId = selectedStopId ?? hoveredStopId;
        EntityId? routeId = selectedRouteId ?? hoveredRouteId;
        EntityId? areaId = selectedAreaId ?? hoveredAreaId;
        GeographicAreaKind? areaKind = selectedAreaId is not null ? selectedAreaKind : hoveredAreaKind;
        if (stopId is null && routeId is null && areaId is null)
        {
            return;
        }

        const float width = 390f;
        Rect2 panel = new(Math.Max(20f, Size.X - width - 24f), 22, width, 190);
        DrawRect(panel, new Color(0.025f, 0.035f, 0.04f, 0.94f), true);
        DrawRect(panel, new Color("#d3bb76"), false, 1.5f);

        if (stopId is EntityId knownStopId)
        {
            DrawStopInspector(knownStopId, panel);
        }
        else if (routeId is EntityId knownRouteId)
        {
            DrawRouteInspector(knownRouteId, panel);
        }
        else if (areaId is EntityId knownAreaId && areaKind is GeographicAreaKind knownAreaKind)
        {
            DrawAreaInspector(knownAreaId, knownAreaKind, panel);
        }
    }

    private void DrawStopInspector(EntityId stopId, Rect2 panel)
    {
        RouteStop stop = GetStop(stopId);
        KnownLocationPresentationState presentation = presentedLocations[stop.Id];
        KnownLocationState political = presentation.PoliticalState;
        string[] lines =
        [
            Localize(stop.NameKey),
            $"{Ui("field/stop")}: {StopTypeName(stop.StopType)} · {Ui("field/terrain")}: {TerrainName(stop.Terrain)}",
            $"{Ui("field/controller")}: {KnownController(political)} · {Ui("field/legal_appointee")}: {KnownOptionalId(political.LegalAppointeeId, political.Intelligence >= IntelligenceLevel.Observed)}",
            $"{Ui("field/local_acceptance")}: {KnownNumber(political.LocalAcceptance)} · {Ui("field/occupation")}: {KnownBoolean(political.Occupied)}",
            $"{Ui("field/claims")}: {KnownClaims(political)} · {Ui("field/stores")}: {KnownNumber(presentation.Stores)}",
            $"{Ui("field/daily_production")}: {KnownNumber(presentation.DailyProduction)}",
            $"{Ui("field/stationed_demand")}: {KnownNumber(presentation.StationedArmyDailyDemand)} · {Ui("field/shortage")}: {KnownNumber(presentation.StationedArmyDailyShortage)}",
            $"{Ui("field/intelligence")}: {IntelligenceName(presentation.Intelligence)}",
        ];
        DrawInspectorLines(panel, lines);
    }

    private void DrawRouteInspector(EntityId routeId, Rect2 panel)
    {
        Route route = world!.Graph.Definition.Routes.Single(item => item.Id == routeId);
        KnownRoutePresentationState state = presentedRoutes[routeId];
        string[] lines =
        [
            Localize(route.NameKey),
            $"{Ui("field/route")}: {RouteTypeName(route.RouteType)} · {Ui("field/traversal_cost")}: {route.TraversalCost:N0}",
            $"{Ui("field/capacity")}: {KnownNumber(state.Capacity)} · {Ui("field/supply_throughput")}: {KnownNumber(state.SupplyThroughput)}",
            $"{Ui("field/effective_throughput")}: {KnownNumber(state.EffectiveSupplyThroughput)} · {Ui("field/available")}: {KnownBoolean(state.AvailableToObserver)}",
            $"{Ui("field/transport")}: {string.Join(", ", route.PermittedModes.Select(TransportName))}",
            $"{Ui("field/control")}: {(state.ControlState is RouteControlState control ? ControlName(control) : Ui("value/unknown"))}",
            $"{Ui("field/disruption")}: {(state.DisruptionPermille is int disruption ? $"{disruption / 10f:0.#}%" : Ui("value/unknown"))}",
        ];
        DrawInspectorLines(panel, lines);
    }

    private void DrawAreaInspector(EntityId areaId, GeographicAreaKind kind, Rect2 panel)
    {
        string[] lines = kind switch
        {
            GeographicAreaKind.Region when world!.Graph.TryGetRegion(areaId, out Region? region) =>
            [
                AreaDisplayName(region.NameKey, region.LabelKey),
                $"{Ui("field/tier")}: {Ui("field/region")}",
                $"{Ui("field/contains")}: {world.Graph.Definition.Districts.Count(item => item.RegionId == region.Id)} {Ui("field/districts")}",
            ],
            GeographicAreaKind.District when world!.Graph.TryGetDistrict(areaId, out District? district) =>
            [
                AreaDisplayName(district.NameKey, district.LabelKey),
                $"{Ui("field/tier")}: {Ui("field/district")}",
                $"{Ui("field/contains")}: {world.Graph.Definition.Localities.Count(item => item.DistrictId == district.Id)} {Ui("field/localities")}",
            ],
            GeographicAreaKind.Locality when world!.Graph.TryGetLocality(areaId, out Locality? locality) =>
            [
                AreaDisplayName(locality.NameKey, locality.LabelKey),
                $"{Ui("field/tier")}: {Ui("field/locality")}",
                $"{Ui("field/terrain")}: {TerrainName(locality.Terrain)}",
                $"{Ui("field/contains")}: {world.Graph.Definition.Stops.Count(item => item.LocalityId == locality.Id)} {Ui("field/stops")}",
            ],
            _ => [Ui("value/unknown")],
        };
        DrawInspectorLines(panel, lines);
    }

    private void DrawInspectorLines(Rect2 panel, IReadOnlyList<string> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14, 23 + index * 21), lines[index],
                HorizontalAlignment.Left, panel.Size.X - 28, index == 0 ? 16 : 12,
                index == 0 ? new Color("#efd98f") : new Color("#d9ddd2"));
        }
    }

    private Color ModeColor(RouteStop stop, KnownLocationPresentationState presentation) => MapMode switch
    {
        CampaignMapMode.PoliticalControl => KnownIdentityColor(
            presentation.PoliticalState.ControllerId,
            presentation.Intelligence >= IntelligenceLevel.Rumored),
        CampaignMapMode.Claims => KnownIdentityColor(
            presentation.PoliticalState.Claims.OrderBy(claim => claim.ClaimantId).FirstOrDefault()?.ClaimantId,
            presentation.Intelligence >= IntelligenceLevel.Observed),
        CampaignMapMode.Administration => KnownIdentityColor(
            presentation.PoliticalState.LegalAppointeeId,
            presentation.Intelligence >= IntelligenceLevel.Observed),
        CampaignMapMode.Diplomacy => DiplomacyColor(presentation.DiplomaticRelation),
        CampaignMapMode.Supply => presentation.Stores is long stores ? Heat(stores, 30_000) : UnknownColor,
        CampaignMapMode.Population => presentation.Population is long population ? Heat(population, 420_000) : UnknownColor,
        CampaignMapMode.Culture => IdColor(presentation.CultureId),
        CampaignMapMode.Intelligence => IntelligenceColor(presentation.Intelligence),
        CampaignMapMode.Routes => TerrainColor(stop.Terrain),
        _ => UnknownColor,
    };

    private static Color DiplomacyColor(DiplomaticRelationCategory relation) => relation switch
    {
        DiplomaticRelationCategory.Self => new Color("#4c9c72"),
        DiplomaticRelationCategory.Friendly => new Color("#69a9bf"),
        DiplomaticRelationCategory.Neutral => new Color("#c39a50"),
        DiplomaticRelationCategory.Hostile => new Color("#b8544f"),
        DiplomaticRelationCategory.Uncontrolled => KnownEmptyColor,
        _ => UnknownColor,
    };

    private EntityId? PickStop(Vector2 position) => world!.Graph.Definition.Stops
        .Select(stop => new { stop.Id, Distance = position.DistanceTo(ToScreen(stop.Position)) })
        .Where(item => item.Distance <= 20f)
        .OrderBy(item => item.Distance)
        .ThenBy(item => item.Id)
        .Select(item => (EntityId?)item.Id)
        .FirstOrDefault();

    private EntityId? PickRoute(Vector2 position) => world!.Graph.Definition.Routes
        .Select(route => new
        {
            route.Id,
            Distance = DistanceToSegment(
                position,
                ToScreen(GetStop(route.FromStopId).Position),
                ToScreen(GetStop(route.ToStopId).Position)),
        })
        .Where(item => item.Distance <= 12f)
        .OrderBy(item => item.Distance)
        .ThenBy(item => item.Id)
        .Select(item => (EntityId?)item.Id)
        .FirstOrDefault();

    private AreaLabelHit? PickArea(Vector2 position)
    {
        AreaLabelHit? label = areaLabelHits
            .Where(item => item.Rect.HasPoint(position))
            .OrderByDescending(item => item.Kind)
            .ThenBy(item => item.Id)
            .Select(item => (AreaLabelHit?)item)
            .FirstOrDefault();
        if (label is not null)
        {
            return label;
        }

        return areaPointHits
            .Where(item => item.Position.DistanceTo(position) <= item.Radius)
            .OrderBy(item => item.Position.DistanceTo(position))
            .ThenByDescending(item => item.Kind)
            .ThenBy(item => item.Id)
            .Select(item => (AreaLabelHit?)new AreaLabelHit(
                item.Id,
                item.Kind,
                new Rect2(item.Position - Vector2.One * item.Radius, Vector2.One * item.Radius * 2f)))
            .FirstOrDefault();
    }

    private Vector2 ToScreen(MapPoint point)
    {
        Rect2 rect = MapRect();
        Vector2 worldPoint = new(point.X, point.Y - point.Elevation * 0.025f);
        return rect.GetCenter() + (worldPoint - new Vector2(WorldWidth / 2f, WorldHeight / 2f) - panWorld)
            * WorldScale() * zoom;
    }

    private Vector2 ToScreen(Vector2 point)
    {
        Rect2 rect = MapRect();
        return rect.GetCenter() + (point - new Vector2(WorldWidth / 2f, WorldHeight / 2f) - panWorld)
            * WorldScale() * zoom;
    }

    private float WorldScale()
    {
        Rect2 rect = MapRect();
        return Math.Max(0.05f, Math.Min(rect.Size.X / WorldWidth, rect.Size.Y / WorldHeight));
    }

    private void BuildAdministrativeGeometry()
    {
        regionHulls.Clear();
        districtHulls.Clear();
        if (world is null)
        {
            return;
        }

        IReadOnlyDictionary<EntityId, District> districts = world.Graph.Definition.Districts
            .ToDictionary(item => item.Id);
        foreach (District district in world.Graph.Definition.Districts.OrderBy(item => item.Id))
        {
            Vector2[] localityPoints = world.Graph.Definition.Localities
                .Where(item => item.DistrictId == district.Id)
                .OrderBy(item => item.Id)
                .Select(item => new Vector2(item.Anchor.X, item.Anchor.Y))
                .ToArray();
            districtHulls[district.Id] = BuildHull(localityPoints, 12f);
        }

        foreach (Region region in world.Graph.Definition.Regions.OrderBy(item => item.Id))
        {
            Vector2[] localityPoints = world.Graph.Definition.Localities
                .Where(item => districts[item.DistrictId].RegionId == region.Id)
                .OrderBy(item => item.Id)
                .Select(item => new Vector2(item.Anchor.X, item.Anchor.Y))
                .ToArray();
            regionHulls[region.Id] = BuildHull(localityPoints, 30f);
        }
    }

    private static Vector2[] BuildHull(IReadOnlyCollection<Vector2> sourcePoints, float padding)
    {
        Vector2[] points = sourcePoints
            .Distinct()
            .OrderBy(item => item.X)
            .ThenBy(item => item.Y)
            .ToArray();
        if (points.Length == 0)
        {
            return [];
        }

        if (points.Length < 3)
        {
            Vector2 center = points.Aggregate(Vector2.Zero, (sum, item) => sum + item) / points.Length;
            return
            [
                center + new Vector2(-padding, -padding * 0.45f),
                center + new Vector2(padding * 0.35f, -padding),
                center + new Vector2(padding, padding * 0.35f),
                center + new Vector2(-padding * 0.4f, padding),
            ];
        }

        List<Vector2> lower = [];
        foreach (Vector2 point in points)
        {
            while (lower.Count >= 2 && Cross(lower[^1] - lower[^2], point - lower[^1]) <= 0f)
            {
                lower.RemoveAt(lower.Count - 1);
            }
            lower.Add(point);
        }

        List<Vector2> upper = [];
        for (int index = points.Length - 1; index >= 0; index--)
        {
            Vector2 point = points[index];
            while (upper.Count >= 2 && Cross(upper[^1] - upper[^2], point - upper[^1]) <= 0f)
            {
                upper.RemoveAt(upper.Count - 1);
            }
            upper.Add(point);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        Vector2[] hull = lower.Concat(upper).ToArray();
        Vector2 centroid = hull.Aggregate(Vector2.Zero, (sum, item) => sum + item) / hull.Length;
        return hull.Select(point =>
        {
            Vector2 direction = point - centroid;
            return point + (direction.LengthSquared() < 0.001f ? Vector2.Right : direction.Normalized()) * padding;
        }).ToArray();
    }

    private static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;

    private static Vector2[] ClosePolygon(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count == 0)
        {
            return [];
        }

        Vector2[] closed = new Vector2[polygon.Count + 1];
        for (int index = 0; index < polygon.Count; index++)
        {
            closed[index] = polygon[index];
        }
        closed[^1] = polygon[0];
        return closed;
    }

    private static Rect2 Bounds(IReadOnlyCollection<Vector2> points)
    {
        if (points.Count == 0)
        {
            return new Rect2();
        }

        float minimumX = points.Min(item => item.X);
        float minimumY = points.Min(item => item.Y);
        float maximumX = points.Max(item => item.X);
        float maximumY = points.Max(item => item.Y);
        return new Rect2(minimumX, minimumY, maximumX - minimumX, maximumY - minimumY);
    }

    private void DrawRiver(IReadOnlyList<Vector2> worldPoints, float width)
    {
        Vector2[] points = worldPoints.Select(ToScreen).ToArray();
        float scaledWidth = Math.Clamp(width * WorldScale() * zoom, 4f, 24f);
        DrawPolyline(points, new Color(0.08f, 0.18f, 0.22f, 0.72f), scaledWidth + 3f, true);
        DrawPolyline(points, new Color("#4d91a5"), scaledWidth, true);
        DrawPolyline(points, new Color(0.67f, 0.83f, 0.82f, 0.45f), Math.Max(1f, scaledWidth * 0.18f), true);
    }

    private void DrawDiamond(Vector2 center, float radius, Color fill, Color border, float borderWidth)
    {
        Vector2[] points =
        [
            center + new Vector2(0, -radius),
            center + new Vector2(radius, 0),
            center + new Vector2(0, radius),
            center + new Vector2(-radius, 0),
        ];
        DrawPolygon(points, [fill]);
        DrawPolyline(ClosePolygon(points), border, borderWidth, true);
    }

    private Rect2 MapRect() => new(22, 16, Math.Max(0, Size.X - 44), Math.Max(0, Size.Y - 108));

    private RouteStop GetStop(EntityId id) => world!.Graph.TryGetStop(id, out RouteStop? stop)
        ? stop
        : throw new InvalidDataException($"Map route references missing stop '{id}'.");

    private string Localize(EntityId key) => text.TryGetValue((key, locale), out string? value)
        ? value
        : text.TryGetValue((key, "en-US"), out string? english)
            ? english
            : "—";

    private string AreaDisplayName(EntityId nameKey, EntityId labelKey)
    {
        string name = Localize(nameKey);
        string label = Localize(labelKey);
        return locale == "ko-KR" && name.EndsWith(label, StringComparison.Ordinal)
            ? name
            : $"{name} · {label}";
    }

    private string ModeName(CampaignMapMode mode) => Ui(mode switch
    {
        CampaignMapMode.PoliticalControl => "mode/political_control",
        CampaignMapMode.Claims => "mode/claims",
        CampaignMapMode.Administration => "mode/administration",
        CampaignMapMode.Diplomacy => "mode/diplomacy",
        CampaignMapMode.Supply => "mode/supply",
        CampaignMapMode.Population => "mode/population",
        CampaignMapMode.Culture => "mode/culture",
        CampaignMapMode.Intelligence => "mode/intelligence",
        CampaignMapMode.Routes => "mode/routes",
        _ => "value/unknown",
    });

    private string LegendHint() => MapMode switch
    {
        CampaignMapMode.PoliticalControl => Ui("hint/political_control"),
        CampaignMapMode.Claims => Ui("hint/claims"),
        CampaignMapMode.Administration => Ui("hint/administration"),
        CampaignMapMode.Diplomacy => Ui("hint/diplomacy"),
        CampaignMapMode.Supply => Ui("hint/supply"),
        CampaignMapMode.Population => Ui("hint/population"),
        CampaignMapMode.Culture => Ui("hint/culture"),
        CampaignMapMode.Intelligence => Ui("hint/intelligence"),
        CampaignMapMode.Routes => Ui("hint/routes"),
        _ => Ui("value/unknown"),
    };

    private string Ui(string suffix) => Localize(new EntityId($"loc:ui/campaign_map/{suffix}"));

    private string KnownController(KnownLocationState state)
    {
        if (state.Intelligence < IntelligenceLevel.Rumored)
        {
            return Ui("value/unknown");
        }

        return state.ControllerId is EntityId controllerId
            ? Localize(controllerId)
            : Ui("value/uncontrolled");
    }

    private string KnownOptionalId(EntityId? id, bool known) => !known
        ? Ui("value/unknown")
        : id is EntityId knownId ? Localize(knownId) : Ui("value/none");

    private string KnownNumber(long? value) => value?.ToString("N0") ?? Ui("value/unknown");

    private string KnownNumber(int? value) => value?.ToString("N0") ?? Ui("value/unknown");

    private string KnownBoolean(bool? value) => value is null
        ? Ui("value/unknown")
        : value.Value ? Ui("value/yes") : Ui("value/no");

    private string KnownClaims(KnownLocationState state)
    {
        if (state.Claims.Count > 0)
        {
            return string.Join(", ", state.Claims
                .OrderBy(claim => claim.ClaimantId)
                .Select(claim => Localize(claim.ClaimantId)));
        }

        return state.Intelligence >= IntelligenceLevel.Observed
            ? Ui("value/none")
            : Ui("value/unknown");
    }

    private string TerrainName(TerrainType terrain) => Ui($"terrain/{EnumKey(terrain.ToString())}");

    private string StopTypeName(RouteStopType type) => Ui($"stop_type/{EnumKey(type.ToString())}");

    private string RouteTypeName(RouteType type) => Ui($"route_type/{EnumKey(type.ToString())}");

    private string TransportName(TransportMode mode) => Ui($"transport_mode/{EnumKey(mode.ToString())}");

    private string ControlName(RouteControlState state) => Ui($"route_control/{EnumKey(state.ToString())}");

    private string IntelligenceName(IntelligenceLevel level) => Ui($"intelligence/{EnumKey(level.ToString())}");

    private static string EnumKey(string value) => string.Concat(value.Select((character, index) =>
        index > 0 && char.IsUpper(character) ? $"_{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return point.DistanceTo(start);
        }

        float factor = Math.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * factor);
    }

    private static StyleBoxFlat CreateMapPanel() => new()
    {
        BgColor = new Color("#8b956d"),
        BorderColor = new Color("#2e3d39"),
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };

    private void DrawMountainRidge(Vector2 center)
    {
        for (int index = -2; index <= 2; index++)
        {
            Vector2 basePoint = center + new Vector2(index * 30f, Math.Abs(index) * 8f);
            Vector2[] ridge =
            [
                basePoint + new Vector2(-22, 26),
                basePoint + new Vector2(0, -30 - (index & 1) * 10),
                basePoint + new Vector2(24, 26),
            ];
            DrawPolyline(ridge, new Color("#4b554b"), 3f, true);
            DrawLine(ridge[1], ridge[1].Lerp(ridge[2], 0.42f), new Color("#b2b49a"), 2f, true);
        }
    }

    private void DrawHillContours(Vector2 center)
    {
        for (int index = 0; index < 3; index++)
        {
            DrawArc(center + new Vector2(index * 12 - 10, index * 4), 46f - index * 10f,
                Mathf.Pi, Mathf.Tau, 24, new Color(0.25f, 0.28f, 0.22f, 0.5f), 2f, true);
        }
    }

    private void DrawFieldLines(Vector2 center)
    {
        for (int index = -2; index <= 2; index++)
        {
            DrawLine(center + new Vector2(-52, index * 11), center + new Vector2(52, index * 11 - 7),
                new Color(0.38f, 0.42f, 0.25f, 0.35f), 1.5f, true);
        }
    }

    private void DrawCityRelief(Vector2 center)
    {
        DrawRect(new Rect2(center + new Vector2(-46, -28), new Vector2(92, 56)), new Color(0.3f, 0.25f, 0.17f, 0.34f), false, 3f);
        for (int index = -1; index <= 1; index++)
        {
            DrawLine(center + new Vector2(index * 24, -28), center + new Vector2(index * 24, 28),
                new Color(0.3f, 0.25f, 0.17f, 0.34f), 2f);
        }
    }

    private static Color Heat(long value, long maximum)
    {
        float factor = Math.Clamp((float)value / Math.Max(1, maximum), 0f, 1f);
        return new Color(0.48f + factor * 0.3f, 0.25f + factor * 0.54f, 0.18f, 1f);
    }

    private static Color IdColor(EntityId? id)
    {
        if (id is null)
        {
            return UnknownColor;
        }

        uint hash = 2166136261;
        foreach (char character in id.Value.Value)
        {
            hash = (hash ^ character) * 16777619;
        }

        return Color.FromHsv((hash % 360) / 360f, 0.5f, 0.82f);
    }

    private static Color KnownIdentityColor(EntityId? id, bool known) => !known
        ? UnknownColor
        : id is null ? KnownEmptyColor : IdColor(id);

    private static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Plains => new Color("#86985f"),
        TerrainType.Hills => new Color("#9a8158"),
        TerrainType.Mountains => new Color("#6b736c"),
        TerrainType.Forest => new Color("#426b52"),
        TerrainType.Marsh => new Color("#58766f"),
        TerrainType.River => new Color("#4f91aa"),
        TerrainType.Coast => new Color("#6097a8"),
        TerrainType.OpenSea => new Color("#315f82"),
        TerrainType.Urban => new Color("#ac875b"),
        _ => UnknownColor,
    };

    private static Color RouteTypeColor(RouteType type) => type switch
    {
        RouteType.Road => new Color("#e2c36f"),
        RouteType.MountainPath => new Color("#b8895c"),
        RouteType.River => new Color("#65bad0"),
        RouteType.CoastalLane => new Color("#4c9fc0"),
        RouteType.OpenSeaLane => new Color("#3f73ad"),
        RouteType.FrontierTrail => new Color("#bd7550"),
        RouteType.SeasonalPassage => new Color("#b8a0d6"),
        _ => UnknownColor,
    };

    private static Color RouteControlColor(RouteControlState state) => state switch
    {
        RouteControlState.Blockaded => new Color("#a6463d"),
        RouteControlState.Contested => new Color("#d29b3d"),
        RouteControlState.Controlled => new Color("#9aa3a2"),
        _ => new Color("#69787a"),
    };

    private static Color SupplyRouteColor(KnownRoutePresentationState state)
    {
        float capacity = Math.Max(1f, state.Capacity ?? 1);
        float throughput = Math.Clamp((state.EffectiveSupplyThroughput ?? 0) / capacity, 0f, 1f);
        float disruption = Math.Clamp((state.DisruptionPermille ?? 0) / 1000f, 0f, 1f);
        return new Color(0.35f + throughput * 0.28f, 0.58f + throughput * 0.25f, 0.36f - disruption * 0.18f);
    }

    private static float SupplyWidth(int? value) => value is int known
        ? 2.5f + 5.5f * Math.Clamp(known / 9000f, 0f, 1f)
        : 3f;

    private static Color IntelligenceColor(IntelligenceLevel level) => level switch
    {
        IntelligenceLevel.Unknown => new Color("#40484d"),
        IntelligenceLevel.Rumored => new Color("#6d6673"),
        IntelligenceLevel.Observed => new Color("#6689a0"),
        IntelligenceLevel.Current => new Color("#83c4c6"),
        _ => UnknownColor,
    };

    private enum LabelTier
    {
        Region,
        District,
        Locality,
        Stop,
        Route,
    }

    private readonly record struct AreaLabelHit(EntityId Id, GeographicAreaKind Kind, Rect2 Rect);

    private readonly record struct AreaPointHit(EntityId Id, GeographicAreaKind Kind, Vector2 Position, float Radius);
}
