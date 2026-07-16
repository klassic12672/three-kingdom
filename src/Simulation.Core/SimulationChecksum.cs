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
        if (schemaVersion is < 1 or > 27)
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
        // separate character-marriage world. Schemas 10-11 include the
        // version-1 marriage state and predate D2 romance invitations/routes.
        // Schema 12 is the exact D2 world shape and differs from schema 13 only
        // in the registered command/event and relationship-source vocabulary.
        // Schema 13 is the exact D3 world shape and differs from schema 14 only
        // in the registered character-family command/event vocabulary. Schema
        // 14 is the exact E0 world shape and predates character guardianships.
        // Schema 15 is the exact E1 world shape and differs from schema 16 only
        // in the registered character-family guardianship-lifecycle vocabulary.
        // Schema 16 is the exact E2 world shape and differs from schema 17 only
        // in the registered coming-of-age command/event vocabulary. Schema 17
        // is the exact E3 world shape and predates character pregnancies.
        // Schema 18 is the exact E4 world shape and differs from schema 19
        // only in the registered pregnancy-birth command/event vocabulary.
        // Schema 19 is the exact E5 world shape and predates runtime
        // character-v3 education attainments. Schema 20 is the exact E6
        // world shape and differs from schema 21 only in registered public
        // death vocabulary. Schema 21 is the exact F0 world shape and differs
        // from schema 22 only in embedded career-death closure evidence and
        // death-specific career service-end vocabulary. Schema 22 is the
        // exact F1 world shape and differs from schema 23 only in embedded
        // custodian-death release evidence. Schema 23 is the exact F2 world
        // shape and differs from schema 24 only in registered household-head
        // death resolution vocabulary. Schema 24 is the exact F3 world shape
        // and predates the separate character-succession world. Schema 25 is
        // the exact F6 world shape and predates persistent succession claims.
        // Schema 26 is the exact F7 world shape and predates persistent
        // explicit succession-support evidence. Schema 27 is the exact F8
        // world shape and predates succession resolution and player
        // continuity state.
        if (schemaVersion < 28)
        {
            StripCharacterSuccessionV4Fields(canonical);
        }

        if (schemaVersion < 25)
        {
            canonical.Remove("characterSuccessions");
        }
        else if (schemaVersion < 26)
        {
            StripCharacterSuccessionV2Fields(canonical);
        }
        else if (schemaVersion < 27)
        {
            StripCharacterSuccessionV3Fields(canonical);
        }

        if (schemaVersion < 18)
        {
            canonical.Remove("characterPregnancies");
        }

        if (schemaVersion < 15)
        {
            canonical.Remove("characterGuardianships");
        }

        if (schemaVersion < 10)
        {
            canonical.Remove("characterMarriages");
        }
        else if (schemaVersion < 12)
        {
            StripCharacterMarriageV2Fields(canonical);
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
        else if (schemaVersion < 20)
        {
            StripCharacterV3Fields(canonical);
            if (schemaVersion < 6)
            {
                StripCharacterV2Fields(canonical);
            }
        }

        if (schemaVersion < 3)
        {
            canonical.Remove("geography");
        }

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJson.Options);
        return FromBytes(serialized);
    }

    private static void StripCharacterMarriageV2Fields(JsonObject canonical)
    {
        if (canonical["characterMarriages"] is not JsonObject marriages)
        {
            return;
        }

        marriages["contractVersion"] = 1;
        marriages.Remove("invitations");
        if (marriages["romanceRoutes"] is not JsonArray routes)
        {
            return;
        }

        string[] v2Fields =
        [
            "sourceInvitationId",
            "invitationInitiatorCharacterId",
            "invitationCreatedDate",
            "invitationCreatedTurnIndex",
            "invitationSourceCommandId",
            "lastPositiveProgressDate",
            "lastPositiveProgressTurnIndex",
            "lastPositiveProgressCommandId",
        ];
        foreach (JsonObject route in routes.OfType<JsonObject>())
        {
            foreach (string field in v2Fields)
            {
                route.Remove(field);
            }
        }
    }

    private static void StripCharacterSuccessionV2Fields(JsonObject canonical)
    {
        if (canonical["characterSuccessions"] is not JsonObject successions)
        {
            return;
        }

        successions["contractVersion"] = 1;
        successions.Remove("claims");
        successions.Remove("claimHistory");
        successions.Remove("supports");
        successions.Remove("supportHistory");
    }

    private static void StripCharacterSuccessionV3Fields(JsonObject canonical)
    {
        if (canonical["characterSuccessions"] is not JsonObject successions)
        {
            return;
        }

        successions["contractVersion"] = 2;
        successions.Remove("supports");
        successions.Remove("supportHistory");
    }

    private static void StripCharacterSuccessionV4Fields(JsonObject canonical)
    {
        if (canonical["characterSuccessions"] is not JsonObject successions)
        {
            return;
        }

        successions["contractVersion"] = 3;
        successions.Remove("resolutions");
        successions.Remove("resolutionHistory");
        successions.Remove("campaignContinuity");
    }

    private static void StripCharacterV2Fields(JsonObject canonical)
    {
        if (canonical["characters"] is not JsonObject characters)
        {
            return;
        }

        characters["contractVersion"] = CharacterContractVersions.LegacySnapshot;
        SetCharacterEntryVersions(
            characters,
            ["identityDefinitions", "characterDefinitions", "familyDefinitions", "householdDefinitions"],
            CharacterContractVersions.LegacyDefinition);
        SetCharacterEntryVersions(
            characters,
            ["characterStates", "familyStates", "householdStates"],
            CharacterContractVersions.LegacyState);

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

    private static void StripCharacterV3Fields(JsonObject canonical)
    {
        if (canonical["characters"] is not JsonObject characters)
        {
            return;
        }

        characters["contractVersion"] = CharacterContractVersions.PreviousSnapshot;
        SetCharacterEntryVersions(
            characters,
            ["characterStates", "familyStates", "householdStates"],
            CharacterContractVersions.PreviousState);
        if (characters["characterStates"] is JsonArray states)
        {
            foreach (JsonObject state in states.OfType<JsonObject>())
            {
                state.Remove("educationAttainments");
            }
        }
    }

    private static void SetCharacterEntryVersions(
        JsonObject characters,
        IReadOnlyList<string> collections,
        int version)
    {
        foreach (string collection in collections)
        {
            if (characters[collection] is not JsonArray entries)
            {
                continue;
            }

            foreach (JsonObject entry in entries.OfType<JsonObject>())
            {
                entry["contractVersion"] = version;
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
            if (schemaVersion < 20
                && StringComparer.Ordinal.Equals(systemId, "simulation.characters"))
            {
                version["version"] = schemaVersion < 6
                    ? CharacterContractVersions.LegacySnapshot
                    : CharacterContractVersions.PreviousSnapshot;
            }
            else if (schemaVersion < 7
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
            else if (StringComparer.Ordinal.Equals(
                systemId,
                CharacterSuccessionSystem.SystemId))
            {
                if (schemaVersion < 25)
                {
                    versions.RemoveAt(index);
                }
                else if (schemaVersion < 26)
                {
                    version["version"] = 1;
                }
                else if (schemaVersion < 27)
                {
                    version["version"] = 2;
                }
                else if (schemaVersion < 28)
                {
                    version["version"] = 3;
                }
            }
            else if (schemaVersion < 15 && StringComparer.Ordinal.Equals(
                systemId,
                CharacterGuardianshipSystem.SystemId))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion < 18 && StringComparer.Ordinal.Equals(
                systemId,
                CharacterPregnancySystem.SystemId))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion < 25 && StringComparer.Ordinal.Equals(
                systemId,
                CharacterSuccessionSystem.SystemId))
            {
                versions.RemoveAt(index);
            }
            else if (schemaVersion < 12 && StringComparer.Ordinal.Equals(
                systemId,
                CharacterMarriageSystem.SystemId))
            {
                version["version"] = 1;
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
        CharacterGuardianships = snapshot.CharacterGuardianships.Canonicalize(),
        CharacterPregnancies = snapshot.CharacterPregnancies.Canonicalize(),
        CharacterSuccessions = snapshot.CharacterSuccessions.Canonicalize(),
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
