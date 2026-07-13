using System.Text.Json;
using Game.Content;

namespace Tools.ContentPipeline;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        string root = GetOption(args, "--repository-root") ?? Directory.GetCurrentDirectory();
        root = Path.GetFullPath(root);

        return args[0] switch
        {
            "validate" => Validate(root),
            "manifest" => WriteManifest(root, args),
            "content" => RunContent(root, args),
            "later-han" => RunLaterHan(root, args),
            _ => UnknownCommand(args[0]),
        };
    }

    private static int RunLaterHan(string root, string[] args)
    {
        if (args.Length >= 2 && args[1] == "import-locations")
        {
            string source = RequiredOption(args, "--dila-xml");
            LocationImportResult import = LaterHanLocationImporter.Import(root, source);
            Console.WriteLine($"Imported Later Han locations: records={import.Records} " +
                string.Join(' ', import.StatusCounts.Select(item => $"{item.Key}={item.Value}")) + $" output={import.OutputPath}");
            return 0;
        }

        if (args.Length < 2 || args[1] != "generate")
        {
            PrintUsage();
            return 2;
        }

        GenerationResult result = LaterHanGeographyGenerator.Generate(root);
        Console.WriteLine(
            $"Generated Later Han geography: regions={result.Regions} districts={result.Districts} " +
            $"localities={result.Localities}.");
        return 0;
    }

    private static int Validate(string root)
    {
        IReadOnlyList<string> errors = new RepositoryValidator().Validate(root);
        foreach (string error in errors)
        {
            Console.Error.WriteLine($"error: {error}");
        }

        ContentLoadResult content = LoadContent(root, []);
        WriteDiagnostics(content.Report);
        if (errors.Count > 0 || content.Report.HasErrors)
        {
            Console.Error.WriteLine(
                $"Repository/content validation failed with {errors.Count + content.Report.ErrorCount} error(s).");
            return 1;
        }

        Console.WriteLine(
            $"Repository and content are valid: {content.Registry.RecordCount} records, " +
            $"{content.Registry.LocalizationCount} translations, checksum {content.Registry.Checksum}.");
        return 0;
    }

    private static int RunContent(string root, string[] args)
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return 2;
        }

        ContentLoadResult result = LoadContent(root, args);
        switch (args[1])
        {
            case "validate":
                WriteDiagnostics(result.Report);
                Console.WriteLine(
                    $"records={result.Registry.RecordCount} localization={result.Registry.LocalizationCount} " +
                    $"packs={result.LoadOrder.Count} checksum={result.Registry.Checksum} " +
                    $"errors={result.Report.ErrorCount} warnings={result.Report.WarningCount}");
                return result.Report.HasErrors ? 1 : 0;
            case "normalize":
                result.ThrowIfInvalid();
                string normalizedOutput = RequiredOption(args, "--output");
                ContentArtifacts.WriteNormalized(result, Path.GetFullPath(normalizedOutput, root));
                Console.WriteLine($"Wrote normalized content to {Path.GetFullPath(normalizedOutput, root)}.");
                return 0;
            case "report":
                string reportOutput = RequiredOption(args, "--output");
                ContentArtifacts.WriteReport(result.Report, Path.GetFullPath(reportOutput, root));
                Console.WriteLine($"Wrote content report to {Path.GetFullPath(reportOutput, root)}.");
                return result.Report.HasErrors ? 1 : 0;
            case "fixtures":
                result.ThrowIfInvalid();
                string fixtureOutput = RequiredOption(args, "--output");
                ContentArtifacts.WriteDevelopmentFixture(result.Registry, Path.GetFullPath(fixtureOutput, root));
                Console.WriteLine($"Wrote development fixture to {Path.GetFullPath(fixtureOutput, root)}.");
                return 0;
            case "geography":
                result.ThrowIfInvalid();
                string geographyOutput = RequiredOption(args, "--output");
                ContentArtifacts.WriteGeography(result.Registry, Path.GetFullPath(geographyOutput, root));
                Console.WriteLine($"Wrote geography runtime artifact to {Path.GetFullPath(geographyOutput, root)}.");
                return 0;
            default:
                return UnknownCommand($"content {args[1]}");
        }
    }

    private static ContentLoadResult LoadContent(string root, string[] args)
    {
        string dataRoot = GetOption(args, "--data-root") ?? Path.Combine(root, "data");
        string gameVersion = GetOption(args, "--game-version") ?? ReadGameVersion(root);
        return new ContentPackLoader().LoadRepository(Path.GetFullPath(dataRoot, root), gameVersion);
    }

    private static string ReadGameVersion(string root)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "build", "version.json")));
        return document.RootElement.GetProperty("projectVersion").GetString()
            ?? throw new InvalidDataException("build/version.json projectVersion is empty.");
    }

    private static void WriteDiagnostics(ContentValidationReport report)
    {
        foreach (ContentDiagnostic diagnostic in report.Diagnostics)
        {
            TextWriter writer = diagnostic.Severity == ContentDiagnosticSeverity.Error ? Console.Error : Console.Out;
            writer.WriteLine(
                $"{diagnostic.Severity.ToString().ToLowerInvariant()}: {diagnostic.File}{diagnostic.JsonPath} " +
                $"[{diagnostic.Code}] {diagnostic.Message} Remediation: {diagnostic.Remediation}");
        }
    }

    private static int WriteManifest(string root, string[] args)
    {
        string platform = RequiredOption(args, "--platform");
        string architecture = RequiredOption(args, "--architecture");
        string configuration = RequiredOption(args, "--configuration");
        string output = RequiredOption(args, "--output");

        BuildManifest manifest = BuildManifest.Create(root, platform, architecture, configuration);
        manifest.Write(Path.GetFullPath(output, root));
        Console.WriteLine($"Wrote build manifest: {Path.GetFullPath(output, root)}");
        return 0;
    }

    private static string RequiredOption(string[] args, string option) =>
        GetOption(args, option)
        ?? throw new ArgumentException($"Missing required option: {option}");

    private static string? GetOption(string[] args, string option)
    {
        int index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  Tools.ContentPipeline validate [--repository-root PATH]");
        Console.Error.WriteLine("  Tools.ContentPipeline manifest --platform NAME --architecture NAME --configuration NAME --output PATH [--repository-root PATH]");
        Console.Error.WriteLine("  Tools.ContentPipeline content validate [--data-root PATH] [--game-version VERSION]");
        Console.Error.WriteLine("  Tools.ContentPipeline content normalize --output PATH [--data-root PATH] [--game-version VERSION]");
        Console.Error.WriteLine("  Tools.ContentPipeline content report --output FILE [--data-root PATH] [--game-version VERSION]");
        Console.Error.WriteLine("  Tools.ContentPipeline content fixtures --output FILE [--data-root PATH] [--game-version VERSION]");
        Console.Error.WriteLine("  Tools.ContentPipeline content geography --output FILE [--data-root PATH] [--game-version VERSION]");
        Console.Error.WriteLine("  Tools.ContentPipeline later-han generate [--repository-root PATH]");
        Console.Error.WriteLine("  Tools.ContentPipeline later-han import-locations --dila-xml PATH [--repository-root PATH]");
    }
}
