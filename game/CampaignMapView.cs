using Game.Content;
using Godot;
using Simulation.Core;

namespace Game.Presentation;

/// <summary>Presentation-only illustrated route map; authoritative geography remains in Simulation.Core.</summary>
public partial class CampaignMapView : Control
{
    private readonly Dictionary<(EntityId Key, string Locale), string> text = [];
    private readonly EntityId observerId = new("faction:coalition");
    private GeographicWorldState? world;
    private EntityId? selectedStopId;
    private string locale = "en-US";

    public CampaignMapMode MapMode { get; private set; } = CampaignMapMode.PoliticalControl;

    public EntityId? SelectedStopId => selectedStopId;

    public void Initialize(GeographicRuntimeArtifact artifact, string selectedLocale)
    {
        if (artifact.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported geography artifact schema {artifact.SchemaVersion}.");
        }

        world = new GeographicWorldState(artifact.Geography);
        locale = selectedLocale;
        text.Clear();
        foreach (LocalizationEntry entry in artifact.Localization)
        {
            text[(entry.Key, entry.Locale)] = entry.Text;
        }

        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    public void SetMapMode(CampaignMapMode mode)
    {
        MapMode = mode;
        QueueRedraw();
    }

    public void SetLocale(string selectedLocale)
    {
        locale = selectedLocale;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (world is null
            || inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
        {
            return;
        }

        selectedStopId = world.Graph.Definition.Stops
            .Select(stop => new { stop.Id, Distance = click.Position.DistanceTo(ToScreen(stop.Position)) })
            .Where(item => item.Distance <= 22f)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Id)
            .Select(item => (EntityId?)item.Id)
            .FirstOrDefault();
        QueueRedraw();
        AcceptEvent();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#111923"));
        if (world is null || Size.X < 10 || Size.Y < 10)
        {
            return;
        }

        DrawTerrain();
        foreach (Route route in world.Graph.Definition.Routes.OrderBy(route => route.Id))
        {
            DrawRoute(route);
        }

        List<Rect2> occupiedLabels = [];
        foreach (RouteStop stop in world.Graph.Definition.Stops
                     .OrderByDescending(stop => stop.Position.Elevation)
                     .ThenBy(stop => stop.Id))
        {
            DrawStop(stop, occupiedLabels);
        }

        DrawLegend();
    }

    private void DrawTerrain()
    {
        Color parchment = new("#202b34");
        DrawRect(new Rect2(22, 46, Math.Max(0, Size.X - 44), Math.Max(0, Size.Y - 72)), parchment, true);

        foreach (Locality locality in world!.Graph.Definition.Localities.OrderBy(item => item.Id))
        {
            Vector2 center = ToScreen(locality.Anchor);
            Color tint = TerrainColor(locality.Terrain) with { A = 0.2f };
            float radius = locality.Terrain == TerrainType.Mountains ? 92f : 72f;
            DrawCircle(center, radius, tint);
            if (locality.Terrain == TerrainType.Mountains)
            {
                Vector2[] ridge =
                [
                    center + new Vector2(-45, 28),
                    center + new Vector2(-8, -48),
                    center + new Vector2(18, 4),
                    center + new Vector2(42, -28),
                    center + new Vector2(68, 30),
                ];
                DrawPolyline(ridge, new Color("#6f7c72"), 3f, true);
            }
        }

        Route? river = world.Graph.Definition.Routes.FirstOrDefault(route => route.RouteType == RouteType.River);
        if (river is not null)
        {
            RouteStop from = GetStop(river.FromStopId);
            RouteStop to = GetStop(river.ToStopId);
            Vector2 start = ToScreen(from.Position) + new Vector2(-180, -8);
            Vector2 end = ToScreen(to.Position) + new Vector2(260, 12);
            DrawLine(start, end, new Color("#326b84"), 18f, true);
            DrawLine(start, end, new Color("#68a8bb"), 4f, true);
        }
    }

    private void DrawRoute(Route route)
    {
        RouteStop from = GetStop(route.FromStopId);
        RouteStop to = GetStop(route.ToStopId);
        Vector2 start = ToScreen(from.Position);
        Vector2 end = ToScreen(to.Position);
        RouteState state = world!.RouteStates.Single(item => item.RouteId == route.Id);
        Color color = MapMode == CampaignMapMode.Routes
            ? RouteTypeColor(route.RouteType)
            : state.ControlState switch
            {
                RouteControlState.Blockaded => new Color("#a6463d"),
                RouteControlState.Contested => new Color("#d29b3d"),
                RouteControlState.Controlled => new Color("#87939a"),
                _ => new Color("#59666f"),
            };
        bool highlighted = selectedStopId == route.FromStopId || selectedStopId == route.ToStopId;
        float width = highlighted ? 7f : route.RouteType is RouteType.River or RouteType.CoastalLane ? 5f : 3f;
        DrawLine(start + new Vector2(0, 3), end + new Vector2(0, 3), new Color(0, 0, 0, 0.55f), width + 3f, true);
        DrawLine(start, end, color, width, true);
        if (route.RouteType is RouteType.MountainPath or RouteType.FrontierTrail or RouteType.SeasonalPassage)
        {
            Vector2 midpoint = (start + end) / 2;
            DrawCircle(midpoint, 4f, color.Lightened(0.25f));
        }
    }

    private void DrawStop(RouteStop stop, ICollection<Rect2> occupiedLabels)
    {
        Vector2 position = ToScreen(stop.Position);
        GeographicContext context = world!.GetContext(stop.Id, observerId);
        Color fill = ModeColor(context);
        bool selected = selectedStopId == stop.Id;
        float radius = selected ? 12f : stop.StopType is RouteStopType.Settlement or RouteStopType.Fort ? 9f : 7f;
        DrawCircle(position + new Vector2(0, 4), radius + 2f, new Color(0, 0, 0, 0.6f));
        DrawCircle(position, radius, fill);
        DrawCircle(position, radius, selected ? Colors.White : new Color("#d7c59a"), false, selected ? 3f : 1.5f, true);

        string label = Localize(stop.NameKey);
        Font font = ThemeDB.FallbackFont;
        float labelWidth = Math.Max(70f, label.Length * 9f);
        Vector2 labelPosition = position + new Vector2(13, -8);
        Rect2 labelRect = new(labelPosition + new Vector2(-3, -15), new Vector2(labelWidth + 6, 20));
        while (occupiedLabels.Any(rect => rect.Intersects(labelRect)))
        {
            labelPosition += new Vector2(0, 18);
            labelRect.Position += new Vector2(0, 18);
        }

        occupiedLabels.Add(labelRect);
        DrawRect(labelRect, new Color(0.035f, 0.05f, 0.065f, 0.82f), true);
        DrawString(font, labelPosition, label, HorizontalAlignment.Left, -1, 14, new Color("#f1e6c7"));
    }

    private void DrawLegend()
    {
        string title = MapMode.ToString();
        DrawRect(new Rect2(30, Size.Y - 56, 280, 34), new Color(0.02f, 0.03f, 0.04f, 0.88f), true);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(44, Size.Y - 33),
            $"{title} · {world!.Season} / {world.Weather}",
            HorizontalAlignment.Left,
            -1,
            15,
            new Color("#e0c98f"));
    }

    private Color ModeColor(GeographicContext context) => MapMode switch
    {
        CampaignMapMode.PoliticalControl => IdColor(context.PoliticalState.ControllerId),
        CampaignMapMode.Claims => IdColor(context.PoliticalState.Claims.FirstOrDefault()?.ClaimantId),
        CampaignMapMode.Administration => IdColor(context.DistrictId),
        CampaignMapMode.Diplomacy => context.PoliticalState.ControllerId == observerId
            ? new Color("#4c9c72")
            : new Color("#a75851"),
        CampaignMapMode.Supply => Heat(context.PoliticalState.Stores ?? 0, 30_000),
        CampaignMapMode.Population => Heat(context.Population, 420_000),
        CampaignMapMode.Culture => IdColor(context.CultureId),
        CampaignMapMode.Intelligence => Heat((long)context.PoliticalState.Intelligence, 3),
        CampaignMapMode.Routes => TerrainColor(context.Terrain),
        _ => Colors.Gray,
    };

    private Vector2 ToScreen(MapPoint point)
    {
        float scaleX = Math.Max(0.1f, (Size.X - 80f) / 1_100f);
        float scaleY = Math.Max(0.1f, (Size.Y - 120f) / 650f);
        return new Vector2(
            40f + point.X * scaleX,
            65f + point.Y * scaleY - point.Elevation * 0.08f);
    }

    private RouteStop GetStop(EntityId id) => world!.Graph.TryGetStop(id, out RouteStop? stop)
        ? stop
        : throw new InvalidDataException($"Map route references missing stop '{id}'.");

    private string Localize(EntityId key) => text.TryGetValue((key, locale), out string? value)
        ? value
        : key.Value;

    private static Color Heat(long value, long maximum)
    {
        float factor = Math.Clamp((float)value / Math.Max(1, maximum), 0f, 1f);
        return new Color(0.45f + factor * 0.25f, 0.22f + factor * 0.55f, 0.2f, 1f);
    }

    private static Color IdColor(EntityId? id)
    {
        if (id is null)
        {
            return new Color("#6b7174");
        }

        uint hash = 2166136261;
        foreach (char character in id.Value.Value)
        {
            hash = (hash ^ character) * 16777619;
        }

        return Color.FromHsv((hash % 360) / 360f, 0.48f, 0.78f);
    }

    private static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Plains => new Color("#7f9362"),
        TerrainType.Hills => new Color("#8b7958"),
        TerrainType.Mountains => new Color("#65706c"),
        TerrainType.Forest => new Color("#426b52"),
        TerrainType.Marsh => new Color("#58766f"),
        TerrainType.River => new Color("#4f91aa"),
        TerrainType.Coast => new Color("#6097a8"),
        TerrainType.OpenSea => new Color("#315f82"),
        TerrainType.Urban => new Color("#a8815b"),
        _ => Colors.Gray,
    };

    private static Color RouteTypeColor(RouteType type) => type switch
    {
        RouteType.Road => new Color("#d7b66f"),
        RouteType.MountainPath => new Color("#b28a61"),
        RouteType.River => new Color("#5bb2cc"),
        RouteType.CoastalLane => new Color("#4c9fc0"),
        RouteType.OpenSeaLane => new Color("#3f73ad"),
        RouteType.FrontierTrail => new Color("#b77a53"),
        RouteType.SeasonalPassage => new Color("#b7a4d4"),
        _ => Colors.Gray,
    };
}
