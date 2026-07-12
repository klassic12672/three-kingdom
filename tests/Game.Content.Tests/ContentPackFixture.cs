using System.Text.Json;
using Game.Content;
using Simulation.Core;

namespace Game.Content.Tests;

internal sealed class ContentPackFixture : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJson.CreateOptions(indented: true);

    public ContentPackFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), $"content-pack-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string WritePack(
        EntityId packId,
        bool builtIn = false,
        int priority = 0,
        IReadOnlyList<ContentDependency>? dependencies = null,
        IReadOnlyList<ContentRecord>? records = null,
        IReadOnlyList<ContentOverride>? overrides = null,
        string? localizationCsv = null,
        byte[]? localizationBytes = null,
        string? glossaryCsv = null,
        IReadOnlyList<SourceReference>? sources = null,
        IReadOnlyList<AssetProvenance>? assets = null,
        bool releaseEligible = false)
    {
        string directory = Path.Combine(Root, packId.Value.Replace(':', '_').Replace('/', '_'));
        Directory.CreateDirectory(directory);
        List<ContentFile> files = [];
        if (records is not null)
        {
            string path = Path.Combine(directory, "records.json");
            WriteJson(path, new ContentRecordDocument(1, records));
            files.Add(new("records.json", ContentFileKind.Records, ContentChecksum.ComputeFile(path)));
        }

        if (overrides is not null)
        {
            string path = Path.Combine(directory, "overrides.json");
            WriteJson(path, new ContentOverrideDocument(1, overrides));
            files.Add(new("overrides.json", ContentFileKind.Overrides, ContentChecksum.ComputeFile(path)));
        }

        if (localizationCsv is not null || localizationBytes is not null)
        {
            string path = Path.Combine(directory, "localization.csv");
            if (localizationBytes is not null)
            {
                File.WriteAllBytes(path, localizationBytes);
            }
            else
            {
                File.WriteAllText(path, localizationCsv!.ReplaceLineEndings("\n"));
            }

            files.Add(new("localization.csv", ContentFileKind.Localization, ContentChecksum.ComputeFile(path)));
        }

        if (glossaryCsv is not null)
        {
            string path = Path.Combine(directory, "glossary.csv");
            File.WriteAllText(path, glossaryCsv.ReplaceLineEndings("\n"));
            files.Add(new("glossary.csv", ContentFileKind.Glossary, ContentChecksum.ComputeFile(path)));
        }

        if (sources is not null)
        {
            string path = Path.Combine(directory, "sources.json");
            WriteJson(path, new SourceReferenceDocument(1, sources));
            files.Add(new("sources.json", ContentFileKind.Sources, ContentChecksum.ComputeFile(path)));
        }

        if (assets is not null)
        {
            string path = Path.Combine(directory, "assets.json");
            WriteJson(path, new AssetProvenanceDocument(1, assets));
            files.Add(new("assets.json", ContentFileKind.Provenance, ContentChecksum.ComputeFile(path)));
        }

        ContentManifest manifest = new(
            1,
            packId,
            "1.0.0",
            "0.1.0",
            1,
            builtIn,
            releaseEligible,
            dependencies ?? [],
            priority,
            files,
            ["Test author"],
            new ProvenanceSummary("Test license", "Test owner", "sources.json", "assets.json"),
            string.Empty);
        manifest = manifest with { Checksum = ContentChecksum.ComputePack(manifest) };
        string manifestPath = Path.Combine(directory, "content-manifest.json");
        WriteJson(manifestPath, manifest);
        return manifestPath;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    public static ContentRecord FictionalRecord(string id, int value = 1, bool releaseMarked = false) => new(
        1,
        new EntityId(id),
        "synthetic",
        ContentTag.Fictional,
        ContentClassification.General,
        [],
        [],
        releaseMarked,
        new System.Text.Json.Nodes.JsonObject
        {
            ["value"] = value,
            ["references"] = new System.Text.Json.Nodes.JsonArray(),
        });

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + "\n");
}
