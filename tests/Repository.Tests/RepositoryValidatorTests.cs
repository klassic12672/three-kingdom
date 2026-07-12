using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Tools.ContentPipeline;

namespace Repository.Tests;

public sealed class RepositoryValidatorTests
{
    [Fact]
    public void Validate_AcceptsValidRepositoryWithUnbornHead()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReportsBrokenDocumentationLink()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        File.WriteAllText(Path.Combine(repository.Root, "README.md"), "[missing](docs/not-there.md)\n");

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(errors, error => error.Contains("Broken documentation link", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsMissingLfsObjectBeforeHead()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string pointerPath = Path.Combine(repository.Root, "game", "assets", "missing.png");
        Directory.CreateDirectory(Path.GetDirectoryName(pointerPath)!);
        File.WriteAllText(
            pointerPath,
            "version https://git-lfs.github.com/spec/v1\n" +
            "oid sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n" +
            "size 123\n");

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(errors, error => error.Contains("Git LFS object is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsCorruptLfsObjectBeforeHead()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        const string objectId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        WriteLfsPointer(repository.Root, "corrupt.png", objectId, 5);
        WriteLfsObject(repository.Root, objectId, Encoding.UTF8.GetBytes("wrong"));

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(errors, error => error.Contains("Git LFS object checksum mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsLfsObjectSizeMismatchBeforeHead()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        byte[] contents = Encoding.UTF8.GetBytes("right");
        string objectId = Convert.ToHexStringLower(SHA256.HashData(contents));
        WriteLfsPointer(repository.Root, "wrong-size.png", objectId, 999);
        WriteLfsObject(repository.Root, objectId, contents);

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(errors, error => error.Contains("Git LFS object size mismatch", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("checksum mismatch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("size invalid\n")]
    public void Validate_ReportsMissingOrMalformedLfsPointerSize(string sizeLine)
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string pointerPath = Path.Combine(repository.Root, "game", "assets", "malformed-size.png");
        Directory.CreateDirectory(Path.GetDirectoryName(pointerPath)!);
        File.WriteAllText(
            pointerPath,
            "version https://git-lfs.github.com/spec/v1\n" +
            "oid sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n" +
            sizeLine);

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(errors, error => error.Contains("Malformed Git LFS pointer", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RunsLfsFsckWhenHeadExists()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        const string objectId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string pointerPath = Path.Combine(repository.Root, "game", "assets", "corrupt.png");
        Directory.CreateDirectory(Path.GetDirectoryName(pointerPath)!);
        File.WriteAllText(
            pointerPath,
            "version https://git-lfs.github.com/spec/v1\n" +
            $"oid sha256:{objectId}\n" +
            "size 5\n");
        string objectPath = Path.Combine(
            repository.Root,
            ".git",
            "lfs",
            "objects",
            objectId[..2],
            objectId[2..4],
            objectId);
        Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);
        File.WriteAllText(objectPath, "wrong");
        repository.CommitAll();

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.DoesNotContain(errors, error => error.Contains("object is missing", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Git LFS object validation failed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ScansReviewableCandidateFileTypes()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string[] candidateFiles =
        [
            "settings.toml",
            "legacy.sln",
            "workspace.slnx",
            "scene.uid",
            "NuGet.Config",
            "settings.xml",
        ];
        foreach (string candidateFile in candidateFiles)
        {
            File.WriteAllText(Path.Combine(repository.Root, candidateFile), "path = '/" + "Users/example/project/'\n");
        }

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        foreach (string candidateFile in candidateFiles)
        {
            Assert.Contains(
                errors,
                error => error.Contains("Machine-local absolute path", StringComparison.Ordinal)
                    && error.Contains(candidateFile, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Validate_ScansCachedFilesEvenWhenTheyMatchIgnoreRules()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string cachedPath = Path.Combine(repository.Root, ".env");
        File.WriteAllText(cachedPath, "path=/" + "home/example/project/\n");
        repository.RunGit("add", "--force", ".env");

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(
            errors,
            error => error.Contains("Machine-local absolute path", StringComparison.Ordinal)
                && error.Contains(".env", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ExcludesIgnoredUntrackedOutputs()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string ignoredPath = Path.Combine(repository.Root, "artifacts", "ignored.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(ignoredPath)!);
        File.WriteAllText(
            ignoredPath,
            "-----BEGIN " + "PRIVATE KEY-----\nignored test content\n");

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.DoesNotContain(errors, error => error.Contains("ignored.toml", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsCachedSigningCredentialFilename()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        File.WriteAllText(Path.Combine(repository.Root, "export_credentials.cfg"), "[preset.0]\n");
        repository.RunGit("add", "--force", "export_credentials.cfg");

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(
            errors,
            error => error.Contains("Signing credential or secret material", StringComparison.Ordinal)
                && error.Contains("export_credentials.cfg", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-----BEGIN OPENSSH " + "PRIVATE KEY-----\ntest\n", "private-key")]
    [InlineData("token=gh" + "p_abcdefghijklmnopqrstuvwxyzABCDEFGHIJ\n", "token")]
    public void Validate_ReportsHighConfidenceSecretContent(string content, string fileStem)
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string fileName = $"{fileStem}.toml";
        File.WriteAllText(Path.Combine(repository.Root, fileName), content);

        IReadOnlyList<string> errors = new RepositoryValidator().Validate(repository.Root);

        Assert.Contains(
            errors,
            error => error.Contains("High-confidence secret or private-key content", StringComparison.Ordinal)
                && error.Contains(fileName, StringComparison.Ordinal));
    }

    [Fact]
    public void TemporaryRepository_DisposeClearsReadOnlyGitFiles()
    {
        TemporaryRepository repository = TemporaryRepository.Create();
        string root = repository.Root;
        string readOnlyPath = Path.Combine(root, ".git", "read-only-test-file");
        File.WriteAllText(readOnlyPath, "test");
        File.SetAttributes(readOnlyPath, File.GetAttributes(readOnlyPath) | FileAttributes.ReadOnly);

        repository.Dispose();

        Assert.False(Directory.Exists(root));
    }

    private static void WriteLfsPointer(string root, string fileName, string objectId, long size)
    {
        string pointerPath = Path.Combine(root, "game", "assets", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(pointerPath)!);
        File.WriteAllText(
            pointerPath,
            "version https://git-lfs.github.com/spec/v1\n" +
            $"oid sha256:{objectId}\n" +
            $"size {size}\n");
    }

    private static void WriteLfsObject(string root, string objectId, byte[] contents)
    {
        string objectPath = Path.Combine(root, ".git", "lfs", "objects", objectId[..2], objectId[2..4], objectId);
        Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);
        File.WriteAllBytes(objectPath, contents);
    }

    private sealed class TemporaryRepository : IDisposable
    {
        private static readonly string[] RequiredFiles =
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

        private TemporaryRepository(string root) => Root = root;

        public string Root { get; }

        public static TemporaryRepository Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"repository-validator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            foreach (string relativePath in RequiredFiles)
            {
                string path = Path.Combine(root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }

            string sourceAttributes = Path.Combine(ArchitectureBoundariesTests.FindRepositoryRoot(), ".gitattributes");
            File.Copy(sourceAttributes, Path.Combine(root, ".gitattributes"));
            string sourceIgnore = Path.Combine(ArchitectureBoundariesTests.FindRepositoryRoot(), ".gitignore");
            File.Copy(sourceIgnore, Path.Combine(root, ".gitignore"));

            TemporaryRepository repository = new(root);
            repository.RunGit("init", "--quiet");
            return repository;
        }

        public void CommitAll()
        {
            RunGit("config", "user.name", "Repository Validator Tests");
            RunGit("config", "user.email", "repository-validator@example.invalid");
            RunGit("add", "--all");
            RunGit("commit", "--quiet", "--message", "test fixture");
        }

        public void RunGit(params string[] arguments)
        {
            ProcessStartInfo startInfo = new("git")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start git.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed ({process.ExitCode}).\nstdout: {output}\nstderr: {error}");
        }

        public void Dispose()
        {
            if (!Directory.Exists(Root))
            {
                return;
            }

            File.SetAttributes(Root, FileAttributes.Normal);
            foreach (string path in Directory.EnumerateFileSystemEntries(
                Root,
                "*",
                SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}
