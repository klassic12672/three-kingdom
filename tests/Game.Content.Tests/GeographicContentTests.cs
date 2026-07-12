using Game.Content;
using Simulation.Core;

namespace Game.Content.Tests;

public sealed class GeographicContentTests
{
    [Fact]
    public void Repository191SliceLoadsFromValidatedContentWithBilingualLabels()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");

        Assert.False(content.Report.HasErrors);
        GeographicRuntimeArtifact artifact = GeographicContentLoader.CreateRuntimeArtifact(content.Registry);
        GeographicWorldState geography = new(artifact.Geography);

        Assert.Single(geography.Graph.Definition.Regions);
        Assert.Equal(2, geography.Graph.Definition.Districts.Count);
        Assert.Equal(6, geography.Graph.Definition.Localities.Count);
        Assert.Equal(8, geography.Graph.Definition.Stops.Count);
        Assert.Equal(10, geography.Graph.Definition.Routes.Count);
        Assert.Equal(2, geography.Armies.Count);
        Assert.True(content.Registry.TryGetText(new EntityId("loc:admin/zhou"), "ko-KR", out string? korean));
        Assert.True(content.Registry.TryGetText(new EntityId("loc:admin/zhou"), "en-US", out string? english));
        Assert.Equal("주", korean);
        Assert.Equal("Province", english);
    }

    [Fact]
    public void EveryRequiredMapModeCanQueryKnownScenarioState()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");
        GeographicWorldState geography = new(GeographicContentLoader.LoadSingleScenario(content.Registry));

        GeographicContext context = geography.GetContext(
            new EntityId("stop:year191/xingyang_fort"),
            new EntityId("faction:coalition"));

        Assert.Equal(9, Enum.GetValues<CampaignMapMode>().Length);
        Assert.Equal(IntelligenceLevel.Current, context.PoliticalState.Intelligence);
        Assert.NotNull(context.PoliticalState.ControllerId);
        Assert.NotNull(context.PoliticalState.LegalAppointeeId);
        Assert.NotNull(context.PoliticalState.LocalAcceptance);
        Assert.True(context.PoliticalState.Stores > 0);
        Assert.NotEmpty(context.RouteIds);
        Assert.NotEmpty(context.BattleLocation.Fronts);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
