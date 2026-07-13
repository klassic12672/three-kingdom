using System.Text.Json;
using Game.Content;
using Godot;
using Simulation.Core;

namespace Game.Presentation;

/// <summary>Content-backed 191 campaign-map slice with platform-smoke behavior.</summary>
public partial class Main : Control
{
    private const string BuildManifestPath = "res://generated/build-manifest.json";
    private const string GeographyArtifactPath = "res://generated/geography-191.json";
    private CampaignMapView? map;
    private OptionButton? modes;
    private OptionButton? locales;
    private GeographicRuntimeArtifact? artifact;

    public override void _Ready()
    {
        string manifest = ReadBuildManifest();
        artifact = ReadGeographyArtifact();
        map = GetNode<CampaignMapView>("CampaignMap");
        string selectedLocale = ReadArgument("--locale=") == "ko-KR" ? "ko-KR" : "en-US";
        map.Initialize(artifact, selectedLocale);
        ApplyStartupCamera();

        modes = GetNode<OptionButton>("TopBar/Modes");
        foreach (CampaignMapMode mode in Enum.GetValues<CampaignMapMode>())
        {
            modes.AddItem(map.GetModeDisplayName(mode), (int)mode);
        }

        CampaignMapMode initialMode = Enum.TryParse(ReadArgument("--map-mode="), true, out CampaignMapMode parsedMode)
            && Enum.IsDefined(parsedMode)
            ? parsedMode
            : CampaignMapMode.PoliticalControl;
        modes.Select((int)initialMode);
        map.SetMapMode(initialMode);
        modes.ItemSelected += index => map.SetMapMode((CampaignMapMode)modes.GetItemId((int)index));

        locales = GetNode<OptionButton>("TopBar/Locales");
        locales.AddItem(map.GetUiText("locale/en"), 0);
        locales.AddItem(map.GetUiText("locale/ko"), 1);
        locales.Select(selectedLocale == "ko-KR" ? 1 : 0);
        locales.ItemSelected += index => ApplyLocale(index == 1 ? "ko-KR" : "en-US");

        ApplyLocale(selectedLocale);
        ApplyStartupSelection();

        if (ReadArgument("--capture=") is not null)
        {
            CallDeferred(nameof(CaptureRequested));
        }
        else if (OS.GetCmdlineUserArgs().Contains("--benchmark-modes", StringComparer.Ordinal))
        {
            CallDeferred(nameof(BenchmarkModes));
        }

        GD.Print($"BUILD_MANIFEST {manifest.Replace('\n', ' ')}");
        GD.Print($"GEOGRAPHY_CHECKSUM {artifact.ContentChecksum}");

        if (OS.GetCmdlineUserArgs().Contains("--smoke-test", StringComparer.Ordinal))
        {
            GetTree().Quit();
        }
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (map is null || modes is null || locales is null
            || inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        int modeIndex = key.Keycode switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            Key.Key4 => 3,
            Key.Key5 => 4,
            Key.Key6 => 5,
            Key.Key7 => 6,
            Key.Key8 => 7,
            Key.Key9 => 8,
            _ => -1,
        };
        if (modeIndex >= 0)
        {
            CampaignMapMode mode = (CampaignMapMode)modes.GetItemId(modeIndex);
            modes.Select(modeIndex);
            map.SetMapMode(mode);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.L)
        {
            int localeIndex = locales.Selected == 1 ? 0 : 1;
            locales.Select(localeIndex);
            ApplyLocale(localeIndex == 1 ? "ko-KR" : "en-US");
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Escape)
        {
            map.ClearSelection();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyLocale(string selectedLocale)
    {
        if (map is null || modes is null || locales is null || artifact is null)
        {
            return;
        }

        map.SetLocale(selectedLocale);
        for (int index = 0; index < modes.ItemCount; index++)
        {
            modes.SetItemText(index, map.GetModeDisplayName((CampaignMapMode)modes.GetItemId(index)));
        }

        locales.SetItemText(0, map.GetUiText("locale/en"));
        locales.SetItemText(1, map.GetUiText("locale/ko"));
        GetNode<Label>("TopBar/Title").Text = map.GetUiText("title");
        GetNode<Label>("TopBar/Status").Text = map.GetUiText("status")
            .Replace("{regions}", artifact.Geography.Graph.Regions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{districts}", artifact.Geography.Graph.Districts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{localities}", artifact.Geography.Graph.Localities.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{stops}", artifact.Geography.Graph.Stops.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{routes}", artifact.Geography.Graph.Routes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private void ApplyStartupCamera()
    {
        if (map is null)
        {
            return;
        }

        float requestedZoom = float.TryParse(
            ReadArgument("--map-zoom="),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float parsedZoom)
            ? parsedZoom
            : 1f;
        Vector2? requestedCenter = null;
        string? centerArgument = ReadArgument("--map-center=");
        string[] coordinates = centerArgument?.Split(',', StringSplitOptions.TrimEntries) ?? [];
        if (coordinates.Length == 2
            && float.TryParse(coordinates[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float x)
            && float.TryParse(coordinates[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float y))
        {
            requestedCenter = new Vector2(x, y);
        }

        map.SetCamera(requestedZoom, requestedCenter);
    }

    private void ApplyStartupSelection()
    {
        if (map is null)
        {
            return;
        }

        string? stop = ReadArgument("--select-stop=");
        string? route = ReadArgument("--select-route=");
        if (stop is not null && !map.SelectStop(new EntityId(stop)))
        {
            GD.PushWarning($"Unknown --select-stop value '{stop}'.");
        }
        else if (route is not null && !map.SelectRoute(new EntityId(route)))
        {
            GD.PushWarning($"Unknown --select-route value '{route}'.");
        }
        else
        {
            ApplyStartupAreaSelection("--select-region=", GeographicAreaKind.Region);
            ApplyStartupAreaSelection("--select-district=", GeographicAreaKind.District);
            ApplyStartupAreaSelection("--select-locality=", GeographicAreaKind.Locality);
        }
    }

    private void ApplyStartupAreaSelection(string prefix, GeographicAreaKind kind)
    {
        string? value = ReadArgument(prefix);
        if (value is not null && map is not null && !map.SelectArea(new EntityId(value), kind))
        {
            GD.PushWarning($"Unknown {prefix[..^1]} value '{value}'.");
        }
    }

    private async void CaptureRequested()
    {
        for (int frame = 0; frame < 5; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        string? path = ReadArgument("--capture=");
        if (path is null)
        {
            return;
        }

        Error result = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"MAP_CAPTURE path={path} result={result}");
        if (OS.GetCmdlineUserArgs().Contains("--quit-after-capture", StringComparer.Ordinal))
        {
            GetTree().Quit(result == Error.Ok ? 0 : 1);
        }
    }

    private async void BenchmarkModes()
    {
        if (map is null || modes is null)
        {
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (CampaignMapMode mode in Enum.GetValues<CampaignMapMode>())
        {
            modes.Select((int)mode);
            map.SetMapMode(mode);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GetTree().Quit();
    }

    private static string? ReadArgument(string prefix) => OS.GetCmdlineUserArgs()
        .FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    private static GeographicRuntimeArtifact ReadGeographyArtifact()
    {
        if (Godot.FileAccess.FileExists(GeographyArtifactPath))
        {
            using Godot.FileAccess file = Godot.FileAccess.Open(
                GeographyArtifactPath,
                Godot.FileAccess.ModeFlags.Read);
            return JsonSerializer.Deserialize<GeographicRuntimeArtifact>(
                file.GetAsText(),
                ContentJson.CreateOptions())
                ?? throw new InvalidDataException("Geography runtime artifact is empty.");
        }

        string dataRoot = ProjectSettings.GlobalizePath("res://../data");
        ContentLoadResult content = new ContentPackLoader().LoadRepository(dataRoot, "0.1.0");
        content.ThrowIfInvalid();
        return GeographicContentLoader.CreateRuntimeArtifact(content.Registry);
    }

    private static string ReadBuildManifest()
    {
        if (!Godot.FileAccess.FileExists(BuildManifestPath))
        {
            return "Build manifest: development editor run (not packaged)";
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(
            BuildManifestPath,
            Godot.FileAccess.ModeFlags.Read);
        return file.GetAsText();
    }
}
