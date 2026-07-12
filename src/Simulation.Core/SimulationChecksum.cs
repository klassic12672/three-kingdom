using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public readonly record struct SimulationChecksum(string Value)
{
    public override string ToString() => Value;

    public static SimulationChecksum Compute(WorldSnapshot snapshot)
    {
        WorldSnapshot canonical = snapshot with
        {
            RandomStreams = snapshot.RandomStreams.OrderBy(item => item.Context, StringComparer.Ordinal).ToArray(),
            Entities = snapshot.Entities.OrderBy(item => item.Id).Select(item => item.Canonicalize()).ToArray(),
            PendingCommands = snapshot.PendingCommands
                .OrderBy(command => command, CommandComparer.Instance)
                .Select(command => command with { Validation = CommandValidationResult.Valid })
                .ToArray(),
            SystemVersions = snapshot.SystemVersions.OrderBy(item => item.SystemId, StringComparer.Ordinal).ToArray(),
            Geography = snapshot.Geography.Canonicalize(),
        };
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJson.Options);
        return new SimulationChecksum(Convert.ToHexStringLower(SHA256.HashData(serialized)));
    }
}

internal static class CanonicalJson
{
    public static JsonSerializerOptions Options { get; } = SimulationJson.CreateOptions();
}

public static class SimulationJson
{
    public static JsonSerializerOptions CreateOptions() => new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}
