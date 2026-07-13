using Game.Content;
using Simulation.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Content.Tests;

public sealed class GeographicContentTests
{
    [Fact]
    public void Repository191SliceLoadsFromValidatedContentWithBilingualLabels()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");

        Assert.False(content.Report.HasErrors);
        Assert.Equal("0.2.0", Assert.Single(content.Registry.Packs).Manifest.Version);
        GeographicRuntimeArtifact artifact = GeographicContentLoader.CreateRuntimeArtifact(content.Registry);
        GeographicWorldState geography = new(artifact.Geography);

        Assert.Equal(13, geography.Graph.Definition.Regions.Count);
        Assert.Equal(99, geography.Graph.Definition.Districts.Count);
        Assert.Equal(1_160, geography.Graph.Definition.Localities.Count);
        Assert.Equal(8, geography.Graph.Definition.Stops.Count);
        Assert.Equal(10, geography.Graph.Definition.Routes.Count);
        Assert.Equal(2, geography.Armies.Count);
        Assert.Contains(geography.Graph.Definition.Regions,
            item => item.Id == new EntityId("region:later_han/a001"));
        Assert.Contains(geography.Graph.Definition.Districts,
            item => item.Id == new EntityId("district:later_han/a003")
                && item.RegionId == new EntityId("region:later_han/a001"));
        Assert.Contains(geography.Graph.Definition.Localities,
            item => item.Id == new EntityId("locality:later_han/a004")
                && item.DistrictId == new EntityId("district:later_han/a003"));
        GeographicDiplomaticRelation relation = Assert.Single(artifact.DiplomaticRelations);
        Assert.Equal(new EntityId("faction:coalition"), relation.ObserverId);
        Assert.Equal(new EntityId("faction:han_court"), relation.CounterpartyId);
        Assert.Equal(DiplomaticRelationCategory.Hostile, relation.Relation);
        Assert.True(content.Registry.TryGetText(new EntityId("loc:admin/zhou"), "ko-KR", out string? korean));
        Assert.True(content.Registry.TryGetText(new EntityId("loc:admin/zhou"), "en-US", out string? english));
        Assert.Equal("주", korean);
        Assert.Equal("Province", english);
    }

    [Fact]
    public void LaterHanHierarchyRetainsAuditedReleaseAndSourceMetadata()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");
        NormalizedContentRecord[] hierarchy = content.Registry.Records
            .Where(record => record.Id.Value.StartsWith("region:later_han/", StringComparison.Ordinal)
                || record.Id.Value.StartsWith("district:later_han/", StringComparison.Ordinal)
                || record.Id.Value.StartsWith("locality:later_han/", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(1_272, hierarchy.Length);
        Assert.Equal(8, hierarchy.Count(record => !record.ReleaseMarked));
        Assert.All(hierarchy, record => Assert.Equal(5, record.SourceIds.Count));
        Assert.Contains(content.Registry.Sources,
            source => source.SourceId == new EntityId("source:hou_han_shu_jun_guo"));
        Assert.Contains(content.Registry.Sources,
            source => source.SourceId == new EntityId("source:later_han_audited_workbook"));
        Assert.Contains(content.Registry.Sources,
            source => source.SourceId == new EntityId("source:unicode_unihan_17"));
        Assert.Contains(content.Registry.Sources,
            source => source.SourceId == new EntityId("source:dila_place_authority_2026_07"));
        Assert.Contains(content.Registry.Sources,
            source => source.SourceId == new EntityId("source:later_han_stylized_layout"));
    }

    [Fact]
    public void HierarchyImportDoesNotChangeStrategicRouteMechanics()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");
        GeographicGraphDefinition graph = GeographicContentLoader.LoadSingleScenario(content.Registry).Graph;
        (string Id, int Capacity, int TraversalCost, int SupplyThroughput)[] expected =
        [
            ("route:year191/ferry_hulao", 4_200, 2_300, 2_600),
            ("route:year191/hulao_xingyang", 6_500, 1_700, 3_900),
            ("route:year191/hulao_yingyin", 3_000, 3_100, 1_500),
            ("route:year191/luoyang_hulao", 12_000, 2_600, 8_000),
            ("route:year191/luoyang_mengjin", 9_000, 1_800, 6_500),
            ("route:year191/mengjin_ferry", 5_000, 1_300, 4_800),
            ("route:year191/songshan_luoyang", 2_400, 3_300, 1_200),
            ("route:year191/xingyang_yingyin", 10_000, 2_200, 7_200),
            ("route:year191/yingyin_chenliu", 11_000, 2_100, 9_000),
            ("route:year191/yingyin_songshan", 3_600, 2_800, 1_900),
        ];

        Assert.Equal(expected, graph.Routes
            .OrderBy(route => route.Id)
            .Select(route => (route.Id.Value, route.Capacity, route.TraversalCost, route.SupplyThroughput))
            .ToArray());
        Assert.All(graph.Stops, stop =>
            Assert.StartsWith("locality:later_han/", stop.LocalityId.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void LaterHanLocationSnapshotKeepsAttributionAndInferenceExplicit()
    {
        string root = FindRepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "data", "research", "later-han-locations.json")));
        JsonElement rootElement = document.RootElement;
        JsonElement source = rootElement.GetProperty("source");
        JsonElement[] records = rootElement.GetProperty("records").EnumerateArray().ToArray();

        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1_272, records.Length);
        Assert.Equal("385e3f557285d7a60346f85d698193e19b6cea2f", source.GetProperty("commit").GetString());
        Assert.Equal("6fcc9f650b0737f4379f58d605cb65de5ce08680de8ab5631dbc1427f3552efb",
            source.GetProperty("sha256").GetString());
        Assert.Equal("CC BY-SA 3.0 Unported", source.GetProperty("license").GetString());
        Assert.Contains(records, record => record.GetProperty("placementStatus").GetString() == "parent_inferred");
        Assert.All(records, record =>
        {
            Assert.InRange(record.GetProperty("longitude").GetDouble(), 90, 128);
            Assert.InRange(record.GetProperty("latitude").GetDouble(), 15, 46);
        });
        JsonElement luoyang = Assert.Single(records, record => record.GetProperty("sourceCell").GetString() == "A4");
        Assert.Equal("dila_direct", luoyang.GetProperty("placementStatus").GetString());
        Assert.InRange(luoyang.GetProperty("longitude").GetDouble(), 112.62, 112.64);
        Assert.InRange(luoyang.GetProperty("latitude").GetDouble(), 34.70, 34.72);
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

    [Fact]
    public void SchemaOneRuntimeArtifactWithoutDiplomaticRelationsRemainsCompatible()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");
        GeographicRuntimeArtifact artifact = GeographicContentLoader.CreateRuntimeArtifact(content.Registry);
        JsonSerializerOptions options = ContentJson.CreateOptions();
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(artifact, options))!.AsObject();
        Assert.True(json.Remove("diplomaticRelations"));

        GeographicRuntimeArtifact restored = JsonSerializer.Deserialize<GeographicRuntimeArtifact>(json, options)!;

        Assert.Equal(1, restored.SchemaVersion);
        Assert.Empty(restored.DiplomaticRelations);
        Assert.Equal(artifact.ContentChecksum, restored.ContentChecksum);
        Assert.Equal(
            JsonSerializer.Serialize(artifact.Geography, options),
            JsonSerializer.Serialize(restored.Geography, options));
    }

    [Fact]
    public void InvalidDiplomaticRelationFailsNormalRepositoryValidation()
    {
        string root = FindRepositoryRoot();
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"invalid-geography-diplomacy-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "data"), temporaryRoot);
            string geographyPath = Path.Combine(temporaryRoot, "authored", "geography-191.json");
            string geography = File.ReadAllText(geographyPath);
            Assert.Contains("\"relation\": \"hostile\"", geography, StringComparison.Ordinal);
            File.WriteAllText(
                geographyPath,
                geography.Replace("\"relation\": \"hostile\"", "\"relation\": \"unknown\"", StringComparison.Ordinal));

            string manifestPath = Path.Combine(temporaryRoot, "content-manifest.json");
            JsonSerializerOptions options = ContentJson.CreateOptions();
            ContentManifest manifest = JsonSerializer.Deserialize<ContentManifest>(File.ReadAllText(manifestPath), options)!;
            ContentManifest updated = manifest with
            {
                Files = manifest.Files.Select(file => file.Path == "authored/geography-191.json"
                    ? file with { Sha256 = ContentChecksum.ComputeFile(geographyPath) }
                    : file).ToArray(),
            };
            updated = updated with { Checksum = ContentChecksum.ComputePack(updated) };
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(updated, options) + "\n");

            ContentLoadResult result = new ContentPackLoader().LoadRepository(temporaryRoot, "0.1.0");

            Assert.Contains(result.Report.Diagnostics, diagnostic =>
                diagnostic.Code == "geography.graph"
                && diagnostic.Message.Contains("diplomatic relations", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void CampaignMapPresentationTextHasCompleteLaunchLanguageCoverage()
    {
        string root = FindRepositoryRoot();
        ContentLoadResult content = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");
        EntityId[] directIdentityKeys =
        [
            new("faction:coalition"),
            new("faction:han_court"),
            new("actor:wang_yun"),
            new("actor:hu_zhen"),
            new("actor:zhang_miao"),
            new("actor:kong_zhou"),
            new("culture:han"),
        ];
        LocalizationEntry[] mapText = content.Registry.LocalizationEntries
            .Where(entry => entry.Key.Value.StartsWith("loc:ui/campaign_map/", StringComparison.Ordinal)
                || directIdentityKeys.Contains(entry.Key))
            .ToArray();

        Assert.NotEmpty(mapText);
        Assert.All(mapText.GroupBy(entry => entry.Key), group =>
            Assert.Equal(["en-US", "ko-KR"], group.Select(entry => entry.Locale).Order(StringComparer.Ordinal).ToArray()));
        Assert.All(directIdentityKeys, key => Assert.Contains(mapText, entry => entry.Key == key));
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
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
