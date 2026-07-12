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
        Assert.Equal("6c527133073ffece29d4d75f7372cc783f2855f6354ed5be9eb1a6c971936449", manifest.ContentManifestChecksum);
        Assert.Equal("e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d", manifest.ContentRegistryChecksum);
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
                "6c527133073ffece29d4d75f7372cc783f2855f6354ed5be9eb1a6c971936449",
                document.RootElement.GetProperty("contentManifestChecksum").GetString());
            Assert.Equal(
                "e937297a171e33d102e18e02ba774b44d61e1b6b5d1b4e485fcb8b2878de672d",
                document.RootElement.GetProperty("contentRegistryChecksum").GetString());
        }
        finally
        {
            File.Delete(output);
        }
    }
}
