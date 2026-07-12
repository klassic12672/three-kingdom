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

    public override void _Ready()
    {
        string manifest = ReadBuildManifest();
        GeographicRuntimeArtifact artifact = ReadGeographyArtifact();
        CampaignMapView map = GetNode<CampaignMapView>("CampaignMap");
        map.Initialize(artifact, "en-US");

        OptionButton modes = GetNode<OptionButton>("TopBar/Modes");
        foreach (CampaignMapMode mode in Enum.GetValues<CampaignMapMode>())
        {
            modes.AddItem(mode.ToString(), (int)mode);
        }

        modes.Select((int)CampaignMapMode.PoliticalControl);
        modes.ItemSelected += index => map.SetMapMode((CampaignMapMode)modes.GetItemId((int)index));

        OptionButton locales = GetNode<OptionButton>("TopBar/Locales");
        locales.AddItem("English", 0);
        locales.AddItem("한국어", 1);
        locales.ItemSelected += index => map.SetLocale(index == 1 ? "ko-KR" : "en-US");

        GetNode<Label>("TopBar/Status").Text =
            $"191 Central Plains · {artifact.Geography.Graph.Stops.Count} stops · {artifact.Geography.Graph.Routes.Count} routes";

        GD.Print($"BUILD_MANIFEST {manifest.Replace('\n', ' ')}");
        GD.Print($"GEOGRAPHY_CHECKSUM {artifact.ContentChecksum}");

        if (OS.GetCmdlineUserArgs().Contains("--smoke-test", StringComparer.Ordinal))
        {
            GetTree().Quit();
        }
    }

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
