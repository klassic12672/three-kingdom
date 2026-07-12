using System.Diagnostics;
using System.Text.Json;
using Game.Content;

namespace Tools.ContentPipeline;

public sealed record BuildManifest(
    int SchemaVersion,
    string ProjectVersion,
    string GitCommit,
    string GodotVersion,
    string DotnetSdkVersion,
    string Platform,
    string Architecture,
    string BuildConfiguration,
    string ContentManifestChecksum,
    string ContentRegistryChecksum)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static BuildManifest Create(
        string repositoryRoot,
        string platform,
        string architecture,
        string configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);

        string versionPath = Path.Combine(repositoryRoot, "build", "version.json");
        string toolchainPath = Path.Combine(repositoryRoot, "build", "toolchain.json");
        string sdkPath = Path.Combine(repositoryRoot, "global.json");

        using JsonDocument version = JsonDocument.Parse(File.ReadAllText(versionPath));
        using JsonDocument toolchain = JsonDocument.Parse(File.ReadAllText(toolchainPath));
        using JsonDocument sdk = JsonDocument.Parse(File.ReadAllText(sdkPath));

        string gameVersion = RequiredString(version.RootElement, "projectVersion", versionPath);
        string dataRoot = Path.Combine(repositoryRoot, "data");
        ContentLoadResult content = new ContentPackLoader().LoadRepository(dataRoot, gameVersion);
        content.ThrowIfInvalid();
        if (StringComparer.OrdinalIgnoreCase.Equals(configuration, "Release")
            && content.LoadOrder.Any(pack => !pack.Manifest.ReleaseEligible))
        {
            string packs = string.Join(", ", content.LoadOrder
                .Where(pack => !pack.Manifest.ReleaseEligible)
                .Select(pack => pack.Manifest.PackId.Value));
            throw new InvalidDataException($"Release build includes development-only content packs: {packs}.");
        }

        LoadedContentPack topLevelPack = content.LoadOrder.SingleOrDefault(pack =>
                StringComparer.Ordinal.Equals(
                    pack.ManifestPath.Replace('\\', '/'),
                    "content-manifest.json"))
            ?? throw new InvalidDataException("Validated content does not include the top-level data/content-manifest.json pack.");

        return new BuildManifest(
            SchemaVersion: 2,
            ProjectVersion: gameVersion,
            GitCommit: ReadGitCommit(repositoryRoot),
            GodotVersion: RequiredString(toolchain.RootElement, "godotVersion", toolchainPath),
            DotnetSdkVersion: RequiredString(sdk.RootElement.GetProperty("sdk"), "version", sdkPath),
            Platform: platform,
            Architecture: architecture,
            BuildConfiguration: configuration,
            ContentManifestChecksum: topLevelPack.Checksum,
            ContentRegistryChecksum: content.Registry.Checksum);
    }

    public void Write(string outputPath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(this, JsonOptions) + Environment.NewLine);
    }

    private static string RequiredString(JsonElement root, string name, string source)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{source} must contain a string property named '{name}'.");
        }

        return value.GetString()!;
    }

    private static string ReadGitCommit(string repositoryRoot)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && output.Length > 0 ? output : "uncommitted";
    }
}
