using System.Text.Json;
using Game.Content;

namespace Game.Content.Tests;

public sealed class PublishedSchemaTests
{
    [Fact]
    public void InitialPublishedSchemasAreValidDraft202012Documents()
    {
        string root = FindRepositoryRoot();
        string schemaDirectory = Path.Combine(root, "data", "schemas");
        string[] expected =
        [
            "asset-provenance.schema.json",
            "content-manifest.schema.json",
            "content-override.schema.json",
            "content-record.schema.json",
            "localization-entry.schema.json",
            "source-reference.schema.json",
        ];

        foreach (string file in expected)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(schemaDirectory, file)));
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", document.RootElement.GetProperty("$schema").GetString());
            Assert.Equal("object", document.RootElement.GetProperty("type").GetString());
            Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void ManifestContractHasNoRuntimeCodeOrScriptFileKind()
    {
        string root = FindRepositoryRoot();
        string schema = File.ReadAllText(Path.Combine(root, "data", "schemas", "content-manifest.schema.json"));
        string source = string.Join(
            '\n',
            Directory.EnumerateFiles(Path.Combine(root, "src", "Game.Content"), "*.cs")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("script", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assembly.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CSharpCompilation", source, StringComparison.Ordinal);
        Assert.Equal(
            ["glossary", "localization", "overrides", "provenance", "records", "sources"],
            Enum.GetValues<ContentFileKind>().Select(value => value.ToString().ToLowerInvariant()).Order(StringComparer.Ordinal));
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
