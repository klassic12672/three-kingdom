using System.Xml.Linq;

namespace Repository.Tests;

public sealed class ArchitectureBoundariesTests
{
    [Fact]
    public void ProjectReferences_FollowTheReservedAssemblyBoundaries()
    {
        string root = FindRepositoryRoot();
        AssertReferences(root, "src/Simulation.Core/Simulation.Core.csproj");
        AssertReferences(
            root,
            "src/Game.Application/Game.Application.csproj",
            "src/Simulation.Core/Simulation.Core.csproj");
        AssertReferences(
            root,
            "src/Game.Content/Game.Content.csproj",
            "src/Simulation.Core/Simulation.Core.csproj");
        AssertReferences(
            root,
            "game/Game.Presentation.csproj",
            "src/Game.Application/Game.Application.csproj",
            "src/Game.Content/Game.Content.csproj");
        AssertReferences(
            root,
            "src/Game.Platform.Steam/Game.Platform.Steam.csproj",
            "src/Game.Application/Game.Application.csproj");
        AssertReferences(
            root,
            "tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj",
            "src/Game.Content/Game.Content.csproj");
        AssertReferences(
            root,
            "tools/Tools.Simulation/Tools.Simulation.csproj",
            "src/Simulation.Core/Simulation.Core.csproj");
        AssertReferences(
            root,
            "tests/Game.Content.Tests/Game.Content.Tests.csproj",
            "src/Game.Content/Game.Content.csproj");
        AssertReferences(
            root,
            "tests/Game.Application.Tests/Game.Application.Tests.csproj",
            "src/Game.Application/Game.Application.csproj");
        AssertReferences(
            root,
            "tests/Simulation.Core.Tests/Simulation.Core.Tests.csproj",
            "src/Simulation.Core/Simulation.Core.csproj");
    }

    private static void AssertReferences(string root, string project, params string[] expected)
    {
        string projectPath = Path.Combine(root, project);
        XDocument document = XDocument.Load(projectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        string[] actual = document.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => Path.GetRelativePath(root, Path.GetFullPath(value!, projectDirectory)).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
    }

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
