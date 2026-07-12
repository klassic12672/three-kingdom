using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Tools.ContentPipeline;

public sealed class RepositoryValidator
{
    private static readonly Regex MarkdownLink = new(
        @"!?(?:\[[^\]]*\])\((?<target>[^)]+)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredPaths =
    [
        "global.json",
        "build/toolchain.json",
        "ThreeKingdom.slnx",
        "game/project.godot",
        "game/Game.Presentation.sln",
        "game/export_presets.cfg",
        "src/Simulation.Core/Simulation.Core.csproj",
        "src/Game.Application/Game.Application.csproj",
        "src/Game.Content/Game.Content.csproj",
        "game/Game.Presentation.csproj",
        "src/Game.Platform.Steam/Game.Platform.Steam.csproj",
        "tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj",
        "tools/Tools.Simulation/Tools.Simulation.csproj",
        "tests/Game.Content.Tests/Game.Content.Tests.csproj",
        "tests/Simulation.Core.Tests/Simulation.Core.Tests.csproj",
        "data/content-manifest.json",
        "data/schemas/content-manifest.schema.json",
        "data/schemas/content-record.schema.json",
        "data/schemas/localization-entry.schema.json",
        "data/schemas/source-reference.schema.json",
        "data/schemas/asset-provenance.schema.json",
    ];

    private static readonly string[] RequiredLfsPatterns =
    [
        "*.psd", "*.blend", "*.png", "*.fbx", "*.glb", "*.wav", "*.ogg", "*.mp4", "*.mov",
    ];

    private static readonly string[] ForbiddenCredentialExtensions =
    [
        ".p8", ".p12", ".pfx", ".cer", ".crt", ".key", ".mobileprovision", ".provisionprofile",
        ".keystore", ".jks",
    ];

    private static readonly Regex[] HighConfidenceSecretPatterns =
    [
        new(@"-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED )?PRIVATE KEY-----", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"\bgh[pousr]_[A-Za-z0-9]{36,255}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"\bAIza[0-9A-Za-z_-]{35}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"\bsk_live_[0-9A-Za-z]{16,}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"\bxox[baprs]-[0-9A-Za-z-]{20,}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant),
    ];

    private static readonly Regex[] MachineLocalPathPatterns =
    [
        new("/" + "Users/", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new("/" + "home/", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"[A-Za-z]:[\\/]Users[\\/]", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new("file" + "://", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
    ];

    public IReadOnlyList<string> Validate(string repositoryRoot)
    {
        List<string> errors = [];
        ValidateRequiredPaths(repositoryRoot, errors);
        IReadOnlyList<string> candidatePaths = EnumerateCandidatePaths(repositoryRoot, errors);
        ValidateDocumentationLinks(repositoryRoot, candidatePaths, errors);
        ValidateLfsConfiguration(repositoryRoot, candidatePaths, errors);
        ValidateCandidatePaths(repositoryRoot, candidatePaths, errors);
        return errors;
    }

    private static void ValidateRequiredPaths(string root, List<string> errors)
    {
        foreach (string requiredPath in RequiredPaths)
        {
            if (!File.Exists(Path.Combine(root, requiredPath)))
            {
                errors.Add($"Missing required repository file: {requiredPath}");
            }
        }
    }

    private static IReadOnlyList<string> EnumerateCandidatePaths(string root, List<string> errors)
    {
        ProcessResult candidates = Run(root, "git", "ls-files", "--cached", "--others", "--exclude-standard", "-z");
        if (candidates.ExitCode != 0)
        {
            errors.Add($"Unable to enumerate repository candidate files: {candidates.Error.Trim()}");
            return [];
        }

        return candidates.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateDocumentationLinks(
        string root,
        IReadOnlyList<string> candidatePaths,
        List<string> errors)
    {
        IEnumerable<string> markdownFiles = candidatePaths
            .Where(path => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.Combine(root, path))
            .Where(File.Exists);

        foreach (string markdownFile in markdownFiles)
        {
            string contents = File.ReadAllText(markdownFile);
            foreach (Match match in MarkdownLink.Matches(contents))
            {
                string target = match.Groups["target"].Value.Trim();
                if (target.StartsWith('<') && target.EndsWith('>'))
                {
                    target = target[1..^1];
                }

                int titleSeparator = target.IndexOf(" \"", StringComparison.Ordinal);
                if (titleSeparator >= 0)
                {
                    target = target[..titleSeparator];
                }

                if (target.Length == 0 || target[0] == '#' || Uri.TryCreate(target, UriKind.Absolute, out _))
                {
                    continue;
                }

                string pathPart = target.Split('#', 2)[0];
                string decoded = Uri.UnescapeDataString(pathPart);
                string resolved = Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(markdownFile)!, decoded));
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    string relativeSource = Path.GetRelativePath(root, markdownFile);
                    errors.Add($"Broken documentation link in {relativeSource}: {target}");
                }
            }
        }
    }

    private static void ValidateLfsConfiguration(
        string root,
        IReadOnlyList<string> candidatePaths,
        List<string> errors)
    {
        string attributesPath = Path.Combine(root, ".gitattributes");
        if (!File.Exists(attributesPath))
        {
            errors.Add("Missing .gitattributes Git LFS configuration.");
            return;
        }

        string attributes = File.ReadAllText(attributesPath);
        foreach (string pattern in RequiredLfsPatterns)
        {
            if (!attributes.Contains($"{pattern} filter=lfs", StringComparison.Ordinal))
            {
                errors.Add($"Git LFS pattern is missing: {pattern}");
            }
        }

        ValidateCandidateLfsPointers(root, candidatePaths, errors);

        ProcessResult head = Run(root, "git", "rev-parse", "--verify", "HEAD");
        if (head.ExitCode == 0)
        {
            ProcessResult lfs = Run(root, "git", "lfs", "fsck");
            if (lfs.ExitCode != 0)
            {
                errors.Add($"Git LFS object validation failed: {lfs.Error.Trim()}");
            }
        }
    }

    private static void ValidateCandidateLfsPointers(
        string root,
        IReadOnlyList<string> candidatePaths,
        List<string> errors)
    {
        const string pointerHeader = "version https://git-lfs.github.com/spec/v1";
        foreach (string relativePath in candidatePaths)
        {
            string path = Path.Combine(root, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            using FileStream stream = File.OpenRead(path);
            if (stream.Length > 512)
            {
                continue;
            }

            using StreamReader reader = new(stream);
            string content = reader.ReadToEnd();
            if (!content.StartsWith(pointerHeader, StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = content.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .ToArray();
            string[] objectIdLines = lines
                .Where(line => line.StartsWith("oid sha256:", StringComparison.Ordinal))
                .ToArray();
            string[] sizeLines = lines
                .Where(line => line.StartsWith("size ", StringComparison.Ordinal))
                .ToArray();
            string? objectId = objectIdLines.Length == 1
                ? objectIdLines[0]["oid sha256:".Length..].Trim()
                : null;
            long expectedSize = -1;
            bool validSize = sizeLines.Length == 1
                && long.TryParse(
                    sizeLines[0]["size ".Length..].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out expectedSize);
            if (objectId is null
                || !Regex.IsMatch(objectId, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant)
                || !validSize)
            {
                errors.Add($"Malformed Git LFS pointer: {relativePath}");
                continue;
            }

            string objectPath = Path.Combine(root, ".git", "lfs", "objects", objectId[..2], objectId[2..4], objectId);
            if (!File.Exists(objectPath))
            {
                errors.Add($"Git LFS object is missing for pointer: {relativePath}");
                continue;
            }

            long actualSize = new FileInfo(objectPath).Length;
            if (actualSize != expectedSize)
            {
                errors.Add(
                    $"Git LFS object size mismatch for pointer: {relativePath} "
                    + $"(expected {expectedSize}, found {actualSize})");
            }

            using FileStream objectStream = File.OpenRead(objectPath);
            string actualObjectId = Convert.ToHexStringLower(SHA256.HashData(objectStream));
            if (!StringComparer.Ordinal.Equals(actualObjectId, objectId))
            {
                errors.Add($"Git LFS object checksum mismatch for pointer: {relativePath}");
            }
        }
    }

    private static void ValidateCandidatePaths(
        string root,
        IReadOnlyList<string> candidatePaths,
        List<string> errors)
    {
        foreach (string relativePath in candidatePaths)
        {
            string normalized = relativePath.Replace('\\', '/');
            if (IsCredentialOrSecretPath(normalized))
            {
                errors.Add($"Signing credential or secret material must not be included in the repository: {normalized}");
            }

            string fullPath = Path.Combine(root, normalized);
            if (!File.Exists(fullPath) || !IsReviewableTextFile(fullPath))
            {
                continue;
            }

            string content = File.ReadAllText(fullPath);
            if (HighConfidenceSecretPatterns.Any(pattern => pattern.IsMatch(content)))
            {
                errors.Add($"High-confidence secret or private-key content found in repository file: {normalized}");
            }

            if (MachineLocalPathPatterns.Any(pattern => pattern.IsMatch(content)))
            {
                errors.Add($"Machine-local absolute path found in repository file: {normalized}");
            }
        }
    }

    private static bool IsCredentialOrSecretPath(string normalizedPath)
    {
        string fileName = Path.GetFileName(normalizedPath);
        string extension = Path.GetExtension(fileName);
        return fileName.Equals("export_credentials.cfg", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("steam_appid.txt", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || (fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals(".env.example", StringComparison.OrdinalIgnoreCase))
            || Regex.IsMatch(fileName, @"^AuthKey_[A-Za-z0-9]+\.p8$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            || ForbiddenCredentialExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("secrets/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/secrets/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReviewableTextFile(string path)
    {
        string[] extensions =
        [
            ".cs", ".csproj", ".props", ".json", ".jsonc", ".md", ".yml", ".yaml", ".sh", ".ps1",
            ".cfg", ".config", ".godot", ".tscn", ".txt", ".csv", ".toml", ".sln", ".slnx", ".uid",
            ".xml", ".gitattributes", ".gitignore", ".editorconfig",
        ];
        return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
            || Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal);
    }

    private static ProcessResult Run(string root, string executable, params string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start {executable}.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output, error);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
