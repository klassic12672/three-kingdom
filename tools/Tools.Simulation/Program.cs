using System.Text.Json;
using Simulation.Core;

namespace Tools.Simulation;

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
            Usage();
            return 2;
        }

        return args[0] switch
        {
            "soak" => Soak(args),
            "replay" => Replay(args),
            "checksum" => Checksum(args),
            "compare" => Compare(args),
            "inspect" => Inspect(args),
            _ => Unknown(args[0]),
        };
    }

    private static int Soak(string[] args)
    {
        int years = GetInt(args, "--years", 10);
        int entities = GetInt(args, "--entities", 1_000);
        ulong seed = GetUInt64(args, "--seed", 1);
        SyntheticSoakResult result = SyntheticSimulation.RunSoak(years, entities, seed);
        Console.WriteLine($"years={result.Years} turns={result.Turns} finalDate={result.FinalDate} checksum={result.Checksum} elapsedMs={result.Elapsed.TotalMilliseconds:F0}");
        return 0;
    }

    private static int Replay(string[] args)
    {
        string input = Required(args, "--input");
        ReplaySpecification specification = JsonSerializer.Deserialize<ReplaySpecification>(
            File.ReadAllText(input),
            SimulationJson.CreateOptions())
            ?? throw new InvalidDataException("Replay specification is empty.");
        CampaignSimulation simulation = SyntheticSimulation.Replay(specification.InitialSnapshot, specification.Commands);
        Console.WriteLine(SimulationChecksum.Compute(simulation.World.CaptureSnapshot()));
        return 0;
    }

    private static int Checksum(string[] args)
    {
        SaveEnvelope envelope = new SaveStore().Load(Required(args, "--save"));
        Console.WriteLine(SimulationChecksum.Compute(envelope.Snapshot));
        return 0;
    }

    private static int Compare(string[] args)
    {
        SaveStore store = new();
        SimulationChecksum left = SimulationChecksum.Compute(store.Load(Required(args, "--left")).Snapshot);
        SimulationChecksum right = SimulationChecksum.Compute(store.Load(Required(args, "--right")).Snapshot);
        Console.WriteLine($"left={left}");
        Console.WriteLine($"right={right}");
        Console.WriteLine(left == right ? "match" : "mismatch");
        return left == right ? 0 : 1;
    }

    private static int Inspect(string[] args)
    {
        string path = Required(args, "--save");
        SaveLoadResult result = new SaveStore().LoadWithRecovery(path);
        SaveEnvelope save = result.Envelope;
        Console.WriteLine($"source={result.SourcePath}");
        Console.WriteLine($"schema={save.SchemaVersion} game={save.GameVersion} created={save.CreatedUtc:O}");
        Console.WriteLine($"date={save.Snapshot.Calendar.Date} turn={save.Snapshot.Calendar.TurnIndex} entities={save.Snapshot.Entities.Count}");
        Console.WriteLine($"checksum={save.Checksum} manifests={save.ContentManifests.Count}");
        if (result.RecoveryDiagnostic is not null)
        {
            Console.WriteLine($"recovery={result.RecoveryDiagnostic}");
        }

        return 0;
    }

    private static string Required(string[] args, string name) => Get(args, name)
        ?? throw new ArgumentException($"Missing required option {name}.");

    private static string? Get(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int GetInt(string[] args, string name, int fallback) =>
        Get(args, name) is { } value ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;

    private static ulong GetUInt64(string[] args, string name, ulong fallback) =>
        Get(args, name) is { } value ? ulong.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Usage();
        return 2;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  Tools.Simulation soak [--years N] [--entities N] [--seed N]");
        Console.Error.WriteLine("  Tools.Simulation replay --input replay.json");
        Console.Error.WriteLine("  Tools.Simulation checksum --save campaign.save.gz");
        Console.Error.WriteLine("  Tools.Simulation compare --left a.save.gz --right b.save.gz");
        Console.Error.WriteLine("  Tools.Simulation inspect --save campaign.save.gz");
    }
}
