using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

public static class ContentChecksum
{
    public static string ComputeFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string ComputePack(ContentManifest manifest)
    {
        StringBuilder canonical = new();
        AppendCanonicalLine($"schema={manifest.SchemaVersion}");
        AppendCanonicalLine($"pack={manifest.PackId.Value}");
        AppendCanonicalLine($"version={manifest.Version}");
        AppendCanonicalLine($"game={manifest.MinimumGameVersion}");
        AppendCanonicalLine($"contentSchema={manifest.ContentSchemaVersion}");
        AppendCanonicalLine($"builtIn={manifest.IsBuiltIn}");
        AppendCanonicalLine($"release={manifest.ReleaseEligible}");
        AppendCanonicalLine($"priority={manifest.LoadPriority}");
        foreach (ContentDependency dependency in manifest.Dependencies.OrderBy(item => item.PackId))
        {
            AppendCanonicalLine($"dependency={dependency.PackId.Value}|{dependency.VersionRequirement}|{dependency.Required}");
        }

        foreach (ContentFile file in manifest.Files.OrderBy(item => NormalizePath(item.Path), StringComparer.Ordinal))
        {
            AppendCanonicalLine($"file={NormalizePath(file.Path)}|{file.Kind}|{file.Sha256}");
        }

        foreach (string author in manifest.Authors.Order(StringComparer.Ordinal))
        {
            AppendCanonicalLine($"author={author}");
        }

        AppendCanonicalLine($"license={manifest.Provenance.License}");
        AppendCanonicalLine($"rights={manifest.Provenance.RightsHolder}");
        AppendCanonicalLine($"sources={manifest.Provenance.SourceRegister}");
        AppendCanonicalLine($"assets={manifest.Provenance.AssetRegister}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));

        void AppendCanonicalLine(FormattableString line) =>
            canonical.Append(line.ToString(CultureInfo.InvariantCulture)).Append('\n');

        static string NormalizePath(string path) => path.Replace('\\', '/');
    }

    public static string ComputeRegistry(IEnumerable<LoadedContentPack> packs)
    {
        string canonical = string.Join(
            '\n',
            packs.OrderBy(pack => pack.Manifest.PackId)
                .Select(pack => $"{pack.Manifest.PackId.Value}@{pack.Manifest.Version}:{pack.Checksum}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
