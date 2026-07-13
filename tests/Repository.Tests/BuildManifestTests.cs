using System.Text.Json;
using Tools.ContentPipeline;

namespace Repository.Tests;

public sealed class BuildManifestTests
{
    [Fact]
    public void Create_ContainsPinnedToolchainAndDistinctContentChecksums()
    {
        string root = ArchitectureBoundariesTests.FindRepositoryRoot();

        BuildManifest manifest = BuildManifest.Create(root, "macOS", "arm64", "Development");

        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal("0.1.0", manifest.ProjectVersion);
        Assert.Equal("4.6.1.stable.mono.official.14d19694e", manifest.GodotVersion);
        Assert.Equal("10.0.301", manifest.DotnetSdkVersion);
        Assert.Equal("f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef", manifest.ContentManifestChecksum);
        Assert.Equal("b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0", manifest.ContentRegistryChecksum);
        Assert.NotEqual(manifest.ContentManifestChecksum, manifest.ContentRegistryChecksum);
    }

    [Fact]
    public void Write_UsesMachineReadableCamelCaseProperties()
    {
        string root = ArchitectureBoundariesTests.FindRepositoryRoot();
        string output = Path.Combine(Path.GetTempPath(), $"build-manifest-{Guid.NewGuid():N}.json");

        try
        {
            BuildManifest.Create(root, "Windows", "x86_64", "Release").Write(output);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(output));

            Assert.Equal("Windows", document.RootElement.GetProperty("platform").GetString());
            Assert.Equal("x86_64", document.RootElement.GetProperty("architecture").GetString());
            Assert.Equal("Release", document.RootElement.GetProperty("buildConfiguration").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(
                "f6024dea64ac6db0ae3af3bdc134a449e6f68223f89e98657e7dab120aa656ef",
                document.RootElement.GetProperty("contentManifestChecksum").GetString());
            Assert.Equal(
                "b04754a678bbb971045e4b2d602df5bf5c48fe26fc606b595449391e54d6b2a0",
                document.RootElement.GetProperty("contentRegistryChecksum").GetString());
        }
        finally
        {
            File.Delete(output);
        }
    }
}
