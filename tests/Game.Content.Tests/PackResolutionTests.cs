using Game.Content;
using Simulation.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Content.Tests;

public sealed class PackResolutionTests
{
    [Fact]
    public void RepositoryFixture_LoadsWithStableGoldenChecksum()
    {
        string root = FindRepositoryRoot();

        ContentLoadResult result = new ContentPackLoader().LoadRepository(Path.Combine(root, "data"), "0.1.0");

        Assert.False(result.Report.HasErrors);
        Assert.Equal(1_295, result.Registry.RecordCount);
        Assert.Equal(2_820, result.Registry.LocalizationCount);
        Assert.Equal("b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0", result.Registry.Checksum);
    }

    [Fact]
    public void DependenciesLoadBeforeModsAndExplicitOverrideApplies()
    {
        using ContentPackFixture fixture = new();
        EntityId baseId = new("pack:base");
        EntityId target = new("record:target");
        string baseManifest = fixture.WritePack(baseId, builtIn: true, priority: 100, records: [ContentPackFixture.FictionalRecord(target.Value)]);
        ContentOverride contentOverride = new(
            1,
            target,
            [new FieldOverride("/data/value", System.Text.Json.Nodes.JsonValue.Create(9))]);
        string modManifest = fixture.WritePack(
            new EntityId("pack:mod"),
            priority: -100,
            dependencies: [new ContentDependency(baseId, ">=1.0.0", true)],
            overrides: [contentOverride]);

        ContentLoadResult result = new ContentPackLoader().Load([modManifest, baseManifest], "0.1.0", fixture.Root);

        Assert.False(result.Report.HasErrors);
        Assert.Equal([baseId, new EntityId("pack:mod")], result.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.True(result.Registry.TryGet(target, out NormalizedContentRecord? record));
        Assert.Equal(9, record.Data.GetProperty("value").GetInt32());
    }

    [Fact]
    public void AllBuiltInPacksLoadBeforeMods()
    {
        using ContentPackFixture fixture = new();
        EntityId foundationId = new("pack:foundation");
        EntityId dependentBaseId = new("pack:dependent_base");
        EntityId modId = new("pack:mod");
        string dependentBase = fixture.WritePack(
            dependentBaseId,
            builtIn: true,
            priority: 100,
            dependencies: [new ContentDependency(foundationId, "1.0.0", true)]);
        string mod = fixture.WritePack(modId, priority: -100);
        string foundation = fixture.WritePack(foundationId, builtIn: true);

        ContentLoadResult result = new ContentPackLoader().Load(
            [dependentBase, mod, foundation],
            "0.1.0",
            fixture.Root);

        Assert.False(result.Report.HasErrors);
        Assert.Equal([foundationId, dependentBaseId, modId], result.LoadOrder.Select(pack => pack.Manifest.PackId));
    }

    [Fact]
    public void DependencyOrdersSamePriorityOverrides()
    {
        using ContentPackFixture fixture = new();
        EntityId baseId = new("pack:base");
        EntityId firstModId = new("pack:first_mod");
        EntityId target = new("record:target");
        string basePath = fixture.WritePack(
            baseId,
            builtIn: true,
            records: [ContentPackFixture.FictionalRecord(target.Value)]);
        ContentOverride firstOverride = new(
            1,
            target,
            [new FieldOverride("/data/value", System.Text.Json.Nodes.JsonValue.Create(2))]);
        string firstMod = fixture.WritePack(
            firstModId,
            priority: 10,
            dependencies: [new ContentDependency(baseId, "1.0.0", true)],
            overrides: [firstOverride]);
        string secondMod = fixture.WritePack(
            new EntityId("pack:second_mod"),
            priority: 10,
            dependencies: [new ContentDependency(firstModId, "1.0.0", true)],
            overrides:
            [
                new ContentOverride(
                    1,
                    target,
                    [new FieldOverride("/data/value", System.Text.Json.Nodes.JsonValue.Create(3))]),
            ]);

        ContentLoadResult result = new ContentPackLoader().Load(
            [secondMod, basePath, firstMod],
            "0.1.0",
            fixture.Root);

        Assert.False(result.Report.HasErrors);
        Assert.Equal(
            [baseId, firstModId, new EntityId("pack:second_mod")],
            result.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.True(result.Registry.TryGet(target, out NormalizedContentRecord? record));
        Assert.Equal(3, record.Data.GetProperty("value").GetInt32());
    }

    [Fact]
    public void DependencyCycleRejectsOnlyCyclicPacks()
    {
        using ContentPackFixture fixture = new();
        EntityId a = new("pack:a");
        EntityId b = new("pack:b");
        string valid = fixture.WritePack(new EntityId("pack:base"), builtIn: true, records: [ContentPackFixture.FictionalRecord("record:base")]);
        string aPath = fixture.WritePack(a, dependencies: [new ContentDependency(b, "1.0.0", true)]);
        string bPath = fixture.WritePack(b, dependencies: [new ContentDependency(a, "1.0.0", true)]);

        ContentLoadResult result = new ContentPackLoader().Load([aPath, valid, bPath], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "dependency.cycle");
        Assert.Equal([new EntityId("pack:base")], result.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.Equal(1, result.Registry.RecordCount);
    }

    [Fact]
    public void SamePriorityFieldConflictRejectsBothModsAndPreservesBase()
    {
        using ContentPackFixture fixture = new();
        EntityId baseId = new("pack:base");
        EntityId target = new("record:target");
        string basePath = fixture.WritePack(baseId, builtIn: true, records: [ContentPackFixture.FictionalRecord(target.Value)]);
        ContentDependency dependency = new(baseId, "1.0.0", true);
        string modA = fixture.WritePack(
            new EntityId("pack:mod_a"),
            priority: 10,
            dependencies: [dependency],
            overrides: [new ContentOverride(1, target, [new FieldOverride("/data/value", System.Text.Json.Nodes.JsonValue.Create(2))])]);
        string modB = fixture.WritePack(
            new EntityId("pack:mod_b"),
            priority: 10,
            dependencies: [dependency],
            overrides: [new ContentOverride(1, target, [new FieldOverride("/data/value", System.Text.Json.Nodes.JsonValue.Create(3))])]);

        ContentLoadResult result = new ContentPackLoader().Load([modB, basePath, modA], "0.1.0", fixture.Root);

        Assert.Equal(2, result.Report.Diagnostics.Count(item => item.Code == "override.ambiguous"));
        Assert.Equal([baseId], result.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.True(result.Registry.TryGet(target, out NormalizedContentRecord? record));
        Assert.Equal(1, record.Data.GetProperty("value").GetInt32());
    }

    [Fact]
    public void SemanticallyInvalidOverrideRejectsModAndPreservesBaseRecord()
    {
        using ContentPackFixture fixture = new();
        EntityId baseId = new("pack:base");
        EntityId target = new("region:target");
        ContentRecord region = ContentPackFixture.FictionalRecord(target.Value) with
        {
            RecordType = "region",
            Data = new System.Text.Json.Nodes.JsonObject
            {
                ["population"] = 100,
                ["references"] = new System.Text.Json.Nodes.JsonArray(),
            },
        };
        string basePath = fixture.WritePack(baseId, builtIn: true, records: [region]);
        string modPath = fixture.WritePack(
            new EntityId("pack:bad_override"),
            dependencies: [new ContentDependency(baseId, "1.0.0", true)],
            overrides:
            [
                new ContentOverride(1, target, [new FieldOverride("/data/population", System.Text.Json.Nodes.JsonValue.Create(-1))]),
            ]);

        ContentLoadResult result = new ContentPackLoader().Load([modPath, basePath], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "record.range");
        Assert.Equal([baseId], result.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.True(result.Registry.TryGet(target, out NormalizedContentRecord? loaded));
        Assert.Equal(100, loaded.Data.GetProperty("population").GetInt32());
    }

    [Fact]
    public void ManifestInputOrderCannotChangeRegistryChecksum()
    {
        using ContentPackFixture fixture = new();
        EntityId baseId = new("pack:base");
        string basePath = fixture.WritePack(baseId, builtIn: true, records: [ContentPackFixture.FictionalRecord("record:a")]);
        string modPath = fixture.WritePack(
            new EntityId("pack:mod"),
            dependencies: [new ContentDependency(baseId, "1.0.0", true)],
            records: [ContentPackFixture.FictionalRecord("record:b")]);
        ContentPackLoader loader = new();

        ContentLoadResult first = loader.Load([basePath, modPath], "0.1.0", fixture.Root);
        ContentLoadResult second = loader.Load([modPath, basePath], "0.1.0", fixture.Root);

        Assert.Equal(first.Registry.Checksum, second.Registry.Checksum);
        Assert.Equal(first.LoadOrder.Select(pack => pack.Manifest.PackId), second.LoadOrder.Select(pack => pack.Manifest.PackId));
    }

    [Fact]
    public void PlatformSpecificManifestPathIsRejected()
    {
        using ContentPackFixture fixture = new();
        string manifestPath = fixture.WritePack(
            new EntityId("pack:bad_path"),
            builtIn: true,
            records: [ContentPackFixture.FictionalRecord("record:a")]);
        JsonObject json = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        json["files"]!.AsArray()[0]!["path"] = "nested\\records.json";
        ContentManifest manifest = json.Deserialize<ContentManifest>(ContentJson.CreateOptions())!;
        manifest = manifest with { Checksum = ContentChecksum.ComputePack(manifest) };
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, ContentJson.CreateOptions(indented: true)) + "\n");

        ContentLoadResult result = new ContentPackLoader().Load([manifestPath], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "manifest.file_path");
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void ThousandRecordRegistryBuildMeetsVerticalSliceBudget()
    {
        using ContentPackFixture fixture = new();
        ContentRecord[] records = Enumerable.Range(0, 1_000)
            .Select(index => ContentPackFixture.FictionalRecord($"record:scale/{index:D4}"))
            .ToArray();
        string manifest = fixture.WritePack(new EntityId("pack:scale"), builtIn: true, records: records);
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);
        stopwatch.Stop();

        Assert.False(result.Report.HasErrors);
        Assert.Equal(1_000, result.Registry.RecordCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Registry build took {stopwatch.Elapsed}.");
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
