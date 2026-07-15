using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public readonly record struct SimulationChecksum(string Value)
{
    public override string ToString() => Value;

    public static SimulationChecksum Compute(WorldSnapshot snapshot)
    {
        WorldSnapshot canonical = Canonicalize(snapshot);
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJson.Options);
        return FromBytes(serialized);
    }

    internal static SimulationChecksum ComputeForSaveSchema(WorldSnapshot snapshot, int schemaVersion)
    {
        if (schemaVersion is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        JsonObject canonical = JsonSerializer.SerializeToNode(Canonicalize(snapshot), CanonicalJson.Options)!.AsObject();
        // Schemas 1-4 predate relationships; schemas 1-3 predate characters;
        // schemas 1-2 predate geography.
        canonical.Remove("relationships");
        if (schemaVersion < 4)
        {
            canonical.Remove("characters");
        }

        if (schemaVersion < 3)
        {
            canonical.Remove("geography");
        }

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJson.Options);
        return FromBytes(serialized);
    }

    private static WorldSnapshot Canonicalize(WorldSnapshot snapshot) => snapshot with
    {
        RandomStreams = snapshot.RandomStreams.OrderBy(item => item.Context, StringComparer.Ordinal).ToArray(),
        Entities = snapshot.Entities.OrderBy(item => item.Id).Select(item => item.Canonicalize()).ToArray(),
        PendingCommands = snapshot.PendingCommands
            .OrderBy(command => command, CommandComparer.Instance)
            .Select(command => command with { Validation = CommandValidationResult.Valid })
            .ToArray(),
        SystemVersions = snapshot.SystemVersions.OrderBy(item => item.SystemId, StringComparer.Ordinal).ToArray(),
        Geography = snapshot.Geography.Canonicalize(),
        Characters = snapshot.Characters.Canonicalize(),
        Relationships = CanonicalizeRelationships(snapshot.Relationships),
    };

    private static RelationshipWorldSnapshot CanonicalizeRelationships(RelationshipWorldSnapshot snapshot) =>
        snapshot with
        {
            Subjects = snapshot.Subjects
                .OrderBy(subject => subject.SubjectCharacterId)
                .Select(subject => subject with
                {
                    DetailedRelationships = subject.DetailedRelationships
                        .OrderBy(relationship => relationship.RelationshipId)
                        .Select(relationship => relationship with
                        {
                            Memories = relationship.Memories
                                .OrderBy(memory => memory.MemoryId)
                                .Select(memory => memory with
                                {
                                    WitnessIds = memory.WitnessIds.Order().ToArray(),
                                    AppliedImpact = memory.AppliedImpact with { },
                                })
                                .ToArray(),
                            Dimensions = relationship.Dimensions with { },
                            FoldedMemories = relationship.FoldedMemories with { },
                        })
                        .ToArray(),
                    ArchivedRelationships = subject.ArchivedRelationships
                        .OrderBy(relationship => relationship.RelationshipId)
                        .Select(relationship => relationship with
                        {
                            Dimensions = relationship.Dimensions with { },
                            FoldedMemories = relationship.FoldedMemories with { },
                        })
                        .ToArray(),
                    DistantHistory = subject.DistantHistory with { },
                })
                .ToArray(),
        };

    private static SimulationChecksum FromBytes(byte[] serialized) =>
        new(Convert.ToHexStringLower(SHA256.HashData(serialized)));
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
