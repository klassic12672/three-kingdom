using System.Text.Json;
using Game.Content;

namespace Tools.ContentPipeline;

public static class ContentArtifacts
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJson.CreateOptions(indented: true);

    public static void WriteNormalized(ContentLoadResult result, string outputDirectory)
    {
        string output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        Write(
            Path.Combine(output, "content-registry.json"),
            new
            {
                schemaVersion = 1,
                checksum = result.Registry.Checksum,
                records = result.Registry.Records,
            });
        Write(
            Path.Combine(output, "localization.json"),
            new
            {
                schemaVersion = 1,
                entries = result.Registry.LocalizationEntries,
            });
        Write(
            Path.Combine(output, "glossary.json"),
            new
            {
                schemaVersion = 1,
                entries = result.Registry.GlossaryEntries,
            });
        Write(
            Path.Combine(output, "content-manifests.json"),
            new
            {
                schemaVersion = 1,
                packs = result.LoadOrder.Select(pack => new
                {
                    pack.Manifest.PackId,
                    pack.Manifest.Version,
                    checksum = pack.Checksum,
                }),
            });
        WriteReport(result.Report, Path.Combine(output, "validation-report.json"));
    }

    public static void WriteReport(ContentValidationReport report, string outputPath) => Write(
        outputPath,
        new
        {
            schemaVersion = 1,
            errors = report.ErrorCount,
            warnings = report.WarningCount,
            diagnostics = report.Diagnostics,
        });

    public static void WriteDevelopmentFixture(ContentRegistry registry, string outputPath) => Write(
        outputPath,
        new
        {
            schemaVersion = 1,
            description = "Deterministic development fixture generated from the validated registry.",
            registry.Checksum,
            manifests = registry.Packs.Select(pack => new
            {
                packId = pack.Manifest.PackId,
                pack.Manifest.Version,
                pack.Checksum,
            }),
            records = registry.Records.Take(10),
            localization = registry.LocalizationEntries.Take(20),
        });

    public static void WriteGeography(ContentRegistry registry, string outputPath) => Write(
        outputPath,
        GeographicContentLoader.CreateRuntimeArtifact(registry));

    private static void Write<T>(string path, T value)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
    }
}
