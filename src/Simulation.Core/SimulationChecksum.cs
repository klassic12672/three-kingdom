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
        if (schemaVersion is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        JsonObject canonical = JsonSerializer.SerializeToNode(Canonicalize(snapshot), CanonicalJson.Options)!.AsObject();
        // Schemas 1-4 predate relationships; schemas 1-3 predate characters;
        // schemas 1-2 predate geography. Schemas 4-5 used character contract v1,
        // schemas 5-6 used relationship contract v1, and all historical schemas
        // schemas 1-6 predate the separate career world, schemas 1-7 predate
        // character resources, schemas 1-8 predate the separate
        // character-estate-holding world, and schemas 1-9 predate the
        // separate character-marriage world. Schema 10 includes the D0
        // marriage state but predates D1 command/event discriminators.
        if (schemaVersion < 10)
        {
            canonical.Remove("characterMarriages");
        }
        if (schemaVersion < 9)
        {
            canonical.Remove("characterEstateHoldings");
        }

        if (schemaVersion < 8)
        {
            canonical.Remove("characterResources");
        }

        if (schemaVersion < 7)
        {
            canonical.Remove("careers");
        }
        DowngradeSystemVersions(canonical, schemaVersion);
        if (schemaVersion < 5)
        {
            canonical.Remove("relationships");
        }
        else if (schemaVersion < 7)
        {
            StripRelationshipV2Fields(canonical);
        }

        if (schemaVersion < 4)
        {
            canonical.Remove("characters");
        }
        else if (schemaVersion < 6)
        {
            StripCharacterV2Fields(canonical);
        }

        if (schemaVersion < 3)
        {
            canonical.Remove("geography");
        }

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJson.Options);
        return FromBytes(serialized);
    }

    private static void StripCharacterV2Fields(JsonObject canonical)
    {
        if (canonical["characters"] is not JsonObject characters)
        {
            return;
        }

        if (characters["characterDefinitions"] is JsonArray definitions)
        {
            foreach (JsonObject definition in definitions.OfType<JsonObject>())
            {
                definition.Remove("structuredName");
                definition.Remove("contentOrigin");
                definition.Remove("cultureId");
                definition.Remove("originLocationId");
                definition.Remove("flawIds");
            }
        }

        if (characters["characterStates"] is JsonArray states)
        {
            foreach (JsonObject state in states.OfType<JsonObject>())
            {
                state.Remove("parentLinks");
                state.Remove("condition");
            }
        }
    }

    private static void StripRelationshipV2Fields(JsonObject canonical)
    {
        if (canonical["relationships"] is not JsonObject relationships)
        {
            return;
        }

        relationships["contractVersion"] = RelationshipContractVersions.LegacySnapshot;
        if (relationships["subjects"] is not JsonArray subjects)
        {
            return;
        }

        foreach (JsonObject subject in subjects.OfType<JsonObject>())
        {
            if (subject["detailedRelationships"] is not JsonArray detailed)
            {
                continue;
            }

            foreach (JsonObject relationship in detailed.OfType<JsonObject>())
            {
                if (relationship["memories"] is not JsonArray memories)
                {
                    continue;
                }

                foreach (JsonObject memory in memories.OfType<JsonObject>())
                {
                    if (memory["identityScheme"]?.GetValue<int>()
                        != (int)RelationshipMemoryIdentityScheme.LegacyRelationshipActionV1
                        || memory["sourceKind"]?.GetValue<int>()
                        != (int)RelationshipMemorySourceKind.RelationshipAction
                        || memory["consequenceIndex"]?.GetValue<int>() != 0)
                    {
                        throw new SaveCompatibilityException(
                            "A current relationship memory cannot be represented by historical relationship contract v1.");
                    }

                    memory["contractVersion"] = RelationshipContractVersions.LegacyMemory;
                    memory["sourceRelationshipActionEventId"] = memory["sourceEventId"]?.DeepClone();
                    memory.Remove("sourceEventId");
                    memory.Remove("sourceKind");
                    memory.Remove("identityScheme");
                    memory.Remove("consequenceIndex");
                }
            }
        }
    }

    private static void DowngradeSystemVersions(JsonObject canonical, int schemaVersion)
    {
        if (canonical["systemVersions"] is not JsonArray versions)
        {
            return;
        }

        for (int index = versions.Count - 1; index >= 0; index--)
        {
            if (versions[index] is not JsonObject version)
            {
                continue;
            }

            string? systemId = version["systemId"]?.GetValue<string>();
            if (schemaVersion < 7
                && StringComparer.Ordinal.Equals(systemId, "simulation.character_careers"))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion < 8
                && StringComparer.Ordinal.Equals(
                    systemId,
                    CharacterResourceSystem.SystemId))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion < 9
                && StringComparer.Ordinal.Equals(
                systemId,
                CharacterEstateHoldingSystem.SystemId))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion < 10 && StringComparer.Ordinal.Equals(
                systemId,
                CharacterMarriageSystem.SystemId))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion is >= 5 and < 7
                && StringComparer.Ordinal.Equals(systemId, "simulation.relationships"))
            {
                version["version"] = RelationshipContractVersions.LegacySnapshot;
            }
        }
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
        Careers = snapshot.Careers.Canonicalize(),
        CharacterResources = snapshot.CharacterResources.Canonicalize(),
        CharacterEstateHoldings = snapshot.CharacterEstateHoldings.Canonicalize(),
        CharacterMarriages = snapshot.CharacterMarriages.Canonicalize(),
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
